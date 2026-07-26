/// Snapshot tests for the doc generator.
///
/// Runs the Oracle over the compiled `tests/Reference` fixture and snapshots both
/// halves of the pipeline: the JSON IR the extractor emits, and the .mdx pages the
/// renderer produces from it. Nearly every defect in this generator is a
/// string-formatting defect, which is exactly what these catch.
module Oracle.Tests.Program

open System
open System.IO
open FSharp.Compiler.CodeAnalysis
open FSharp.Oracle.Schema
open FSharp.Oracle.Extractor
open Thoth.Json.System.Text.Json
open Thoth.Json.Core.Auto
open Oracle.Tests

[<Literal>]
let private sourceDirectory = __SOURCE_DIRECTORY__

let private repoRoot = Path.GetFullPath(Path.Combine(sourceDirectory, "..", ".."))

let private snapshotDirectory = Path.Combine(repoRoot, "tests", "__snapshots__")

let private fixtureDll =
    Path.Combine(repoRoot, "tests", "Reference", "bin", "Debug", "net10.0", "Reference.dll")

/// The plugin's defaults, so snapshots reflect what a real site gets.
let private basePath = ""
let private outputBase = "api"

let private extract () =
    let checker = FSharpChecker.Create()

    let allDlls =
        [|
            yield!
                Directory.GetFiles(
                    Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                    "*.dll"
                )
            yield! Directory.GetFiles(Path.GetDirectoryName fixtureDll, "*.dll")
        |]
        |> Array.map Path.GetFullPath
        |> Array.distinct

    let assembly = extractAssembly checker allDlls fixtureDll

    {
        Assemblies = [ assembly ]
    }

[<EntryPoint>]
let main argv =
    let update = argv |> Array.contains "--update"

    // The link check runs against the built site rather than the IR, so it is a
    // separate mode: `./build.sh docs` first, then `./build.sh test --links`.
    if argv |> Array.contains "--links" then
        LinkCheck.run (Path.Combine(repoRoot, "docs", "dist"))
    else

    if not (File.Exists fixtureDll) then
        eprintfn "Fixture not built: %s" fixtureDll
        eprintfn "Run: dotnet build tests/Reference/Reference.fsproj"
        1
    else

    let root = extract ()

    // ---- IR snapshot -------------------------------------------------------

    let irJson =
        root
        |> Encode.Auto.generateEncoder (losslessOption = true)
        |> Encode.toString 4

    let irResult =
        Snapshot.verify update (Path.Combine(snapshotDirectory, "reference.ir.verified.json")) irJson

    // ---- Page snapshots ----------------------------------------------------

    let pagesDirectory = Path.Combine(snapshotDirectory, "pages")
    let pages = Starlight.FSharp.Generate.allPages basePath outputBase root

    let pageResults =
        pages
        |> List.map (fun (slug, content) ->
            let path = Path.Combine(pagesDirectory, $"{slug}.verified.mdx")
            path, (slug, Snapshot.verify update path content)
        )

    let producedPaths =
        pageResults |> List.map (fst >> Path.GetFullPath) |> Set.ofList

    let orphanResults =
        Snapshot.orphans update pagesDirectory producedPaths
        |> List.map (fun result -> "pages", result)

    printfn "Extracted %i module(s), rendered %i page(s)" root.Assemblies.Head.Modules.Length pages.Length

    let results =
        [
            "reference.ir.json", irResult
            yield! pageResults |> List.map snd
            yield! orphanResults
        ]

    Snapshot.report "snapshots" results
