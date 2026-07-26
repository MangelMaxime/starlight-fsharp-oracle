module FSharp.Oracle.Program

open System
open FSharp.Compiler.CodeAnalysis
open FSharp.Oracle.Schema
open FSharp.Oracle.Extractor
open Thoth.Json.System.Text.Json
open Thoth.Json.Core.Auto

[<EntryPoint>]
let main argv =
    // The Oracle emits a semantic IR and knows nothing about the site consuming it -
    // no base path, no output folder, no URLs. Linking is the renderer's job, because
    // only the renderer knows which names end up with a page.
    let dllPaths = argv

    match dllPaths with
    | [||] ->
        eprintfn "Usage: fsharp-docs-oracle <path/to/Assembly.dll> [...]"
        eprintfn "Output: JSON IR written to stdout"
        1
    | _ ->
        // Validate up front: a bad path otherwise surfaces as
        // "The value cannot be an empty string (Parameter 'path')" from deep inside
        // a directory scan, which says nothing about which argument was wrong.
        let missing =
            dllPaths
            |> Array.filter (fun path -> String.IsNullOrWhiteSpace path || not (IO.File.Exists path))

        if missing.Length > 0 then
            for path in missing do
                if String.IsNullOrWhiteSpace path then
                    eprintfn "error: empty assembly path"
                else
                    eprintfn "error: assembly not found: %s" path

            1
        else

        let checker = FSharpChecker.Create()

        // Gather every .dll in the same directories as the specified assemblies
        // so FCS can resolve transitive dependencies.  Also include the current
        // .NET runtime directory because framework assemblies (e.g.
        // System.Text.RegularExpressions) are not copied to the publish output.
        let allDllPaths =
            let runtimeDir =
                System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()

            [|
                yield! IO.Directory.GetFiles(runtimeDir, "*.dll")

                yield!
                    dllPaths
                    |> Array.collect (fun path ->
                        let dir = IO.Path.GetDirectoryName(path)
                        IO.Directory.GetFiles(dir, "*.dll")
                    )
            |]
            |> Array.map IO.Path.GetFullPath
            |> Array.distinct

        // One check of the shared reference set, then one pass per target assembly.
        let resolved = resolveAssemblies checker allDllPaths

        let assemblies =
            dllPaths |> Array.map (extractAssembly resolved) |> Array.toList

        let root =
            {
                Assemblies = assemblies
            }

        let json =
            root
            |> Encode.Auto.generateEncoder(losslessOption = true)
            // Compact: this goes down a pipe to the plugin, not to a reader.
            |> Encode.toString 0

        Console.WriteLine(json)

        0
