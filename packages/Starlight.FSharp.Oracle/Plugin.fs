module Starlight.FSharp.Plugin

open Fable.Core
open Fable.Core.JS
open Fable.Core.JsInterop
open Node.Api
open FSharp.Oracle.Schema
open Thoth.Json.JavaScript
open Starlight.FSharp.Helpers
open Thoth.Json.Core.Auto

[<Import("createRenderer", "astro-expressive-code")>]
let private createEcRenderer: obj -> JS.Promise<obj> = jsNative

[<AllowNullLiteral>]
[<Interface>]
type AstroIntegrationLogger =
    abstract member label: string with get, set
    /// <summary>
    /// Creates a new logger instance with a new label, but the same log options.
    /// </summary>
    abstract member fork: label: string -> AstroIntegrationLogger
    abstract member info: message: string -> unit
    abstract member warn: message: string -> unit
    abstract member error: message: string -> unit
    abstract member debug: message: string -> unit

type AstroConfig =
    abstract member root: Node.Url.URL
    /// Astro's site `base` path (defaults to "/"). Prefixes all in-content links.
    abstract member ``base``: string

[<StringEnum(CaseRules.None)>]
[<RequireQualifiedAccess>]
type AstroCommand =
    | dev
    | build
    | preview
    | sync

[<Emit("import.meta.url")>]
let private importMetaUrl: string = jsNative

[<Import("fileURLToPath", "node:url")>]
let private fileURLToPath (url: string) : string = jsNative

let private runExtractor (logger: AstroIntegrationLogger) (dllPaths: string list) =
    // 50MB buffer to handle large API documentation outputs
    let maxBuffer = 50 * 1024 * 1024

#if DEBUG
    // During development use `dotnet run` so changes to the Oracle source are
    // picked up immediately without having to run `dotnet publish` manually.
    let oracleProject =
        path.join (path.dirname (fileURLToPath importMetaUrl), "..", "packages", "FSharp.Oracle")

    let args =
        [
            "run"
            "--project"
            oracleProject
            "--"
            yield! dllPaths
        ]
#else
    // In release builds the Oracle binary is produced by the postinstall script.
    let oracleDll =
        path.join (path.dirname (fileURLToPath importMetaUrl), "..", "oracle-bin", "Oracle.dll")

    let args =
        [
            oracleDll
            yield! dllPaths
        ]
#endif

    let result =
        childProcess.spawnSync (
            "dotnet",
            args |> ResizeArray,
            {|
                encoding = "utf-8"
                maxBuffer = maxBuffer
            |}
        )

    if result?error then
        raise result?error
    else if result?status <> 0 then
        failwith $"F# docs extractor failed (exit %i{result?status}):\n%A{result?stderr}"
    else
        let json = result?stdout

        let decoder = Decode.Auto.generateDecoder<Root> (losslessOption = true)

        Decode.unsafeFromString decoder json

type PluginOptions =
    abstract output: string option
    abstract assemblies: ResizeArray<string>

    abstract sidebar:
        {|
            label: string
        |} option

let private starlightFSharpDoc (pluginOptions: PluginOptions) =
    {|
        name = "starlight-fsharp-oracle"
        hooks =
            {|
                ``config:setup`` =
                    fun hookOptions ->
                        promise {
                            let logger: AstroIntegrationLogger = hookOptions?logger
                            let astroConfig: AstroConfig = hookOptions?astroConfig
                            let command: AstroCommand = hookOptions?command

                            match command with
                            | AstroCommand.preview -> ()
                            | AstroCommand.dev
                            | AstroCommand.build
                            | AstroCommand.sync ->
                                logger.info "Extracting F# API docs..."

                                let updateConfig: obj -> unit = hookOptions?updateConfig

                                let! ecRenderer = createEcRenderer {| |}

                                // Declare Starlight's cascade-layer order first (before any
                                // component style registers a layer) so content styles beat its
                                // reset; in dev Vite otherwise inverts the order on our pages.
                                let layerOrder =
                                    "@layer starlight.base, starlight.reset, starlight.core, starlight.content, starlight.components, starlight.utils;\n"

                                let themesCss = layerOrder + Themes.generateCss ecRenderer

                                let outputBase: string =
                                    pluginOptions.output |> Option.defaultValue "api"

                                // Astro's site `base` (e.g. "/my-repo") must prefix every
                                // in-content link. Normalize "/" or a trailing slash away so
                                // it composes cleanly as `{basePath}/{outputBase}/...`.
                                let basePath: string =
                                    let raw =
                                        astroConfig.``base``
                                        |> Option.ofObj
                                        |> Option.defaultValue ""

                                    raw.TrimEnd('/')

                                let root =
                                    runExtractor logger (pluginOptions.assemblies |> Seq.toList)

                                let modules = root.Assemblies |> List.collect _.Modules

                                let sidebarLabel =
                                    pluginOptions.sidebar
                                    |> Option.map (fun s -> s.label)
                                    |> Option.defaultValue "API Reference"

                                let existingSidebar: obj array =
                                    hookOptions?config?sidebar |> Option.defaultValue [||]

                                updateConfig
                                    {|
                                        head =
                                            [|
                                                {|
                                                    tag = "style"
                                                    content = themesCss
                                                |}
                                                {|
                                                    tag = "script"
                                                    content =
                                                        Generate.sidebarLabelInitScript sidebarLabel
                                                |}
                                            |]
                                        sidebar =
                                            [|
                                                yield! existingSidebar
                                                Generate.sidebarTree outputBase sidebarLabel modules
                                            |]
                                    |}

                                let outputDir =
                                    path.join (
                                        astroConfig.root.pathname,
                                        "src",
                                        "pages",
                                        outputBase
                                    )

                                match Helpers.mkdirSync outputDir with
                                | Error msg ->
                                    logger.error msg
                                    raise (System.Exception(msg))
                                | Ok() ->

                                    // Everything in here is generated, so clear it
                                    // first: otherwise a renamed or deleted type keeps
                                    // its page, stale and unreachable but still built.
                                    match Helpers.cleanSync outputDir ".mdx" with
                                    | Error msg -> logger.warn msg
                                    | Ok() -> ()

                                    match
                                        Helpers.writeSync
                                            (path.join (outputDir, ".gitignore"))
                                            """# Autogenerated by starlight-fsharp-oracle
**/*
"""
                                    with
                                    | Error msg -> logger.warn $"Failed to write .gitignore: {msg}"
                                    | Ok() -> ()

                                // Two names that slug the same would overwrite each
                                // other on disk; they are pulled apart, but the author
                                // still needs to know.
                                for warning in Generate.slugWarnings modules do
                                    logger.warn warning

                                let pages = Generate.allPages basePath outputBase root

                                let writeResults =
                                    pages
                                    |> List.map (fun (slug, content) ->
                                        let filePath = path.join (outputDir, slug + ".mdx")

                                        match Helpers.writeSync filePath content with
                                        | Error msg ->
                                            logger.error msg
                                            Error msg
                                        | Ok() ->
                                            logger.info $"  wrote {filePath}"
                                            Ok()
                                    )

                                let errors = Helpers.collectErrors writeResults

                                if not errors.IsEmpty then
                                    let errorMsg =
                                        $"Failed to write {errors.Length} file(s):\n"
                                        + String.concat "\n" errors

                                    logger.error errorMsg
                                    raise (System.Exception(errorMsg))
                        }
            |}
    |}

exportDefault starlightFSharpDoc
