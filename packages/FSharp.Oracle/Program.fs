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

        let assemblies =
            dllPaths
            |> Array.map (extractAssembly checker allDllPaths)
            |> Array.toList

        let root =
            {
                Assemblies = assemblies
            }

        let json =
            root
            |> Encode.Auto.generateEncoder(losslessOption = true)
            |> Encode.toString 4

        Console.WriteLine(json)

        0
