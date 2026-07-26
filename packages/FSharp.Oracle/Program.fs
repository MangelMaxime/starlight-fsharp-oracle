module FSharp.Oracle.Program

open System
open FSharp.Compiler.CodeAnalysis
open FSharp.Oracle.Schema
open FSharp.Oracle.Extractor
open Thoth.Json.System.Text.Json
open Thoth.Json.Core.Auto

[<EntryPoint>]
let main argv =
    let rec parseArgs outputBase basePath dlls =
        function
        | "--output-base" :: value :: rest -> parseArgs value basePath dlls rest
        | "--base" :: value :: rest -> parseArgs outputBase value dlls rest
        | path :: rest -> parseArgs outputBase basePath (path :: dlls) rest
        | [] -> outputBase, basePath, List.rev dlls |> List.toArray

    let outputBase, basePath, dllPaths = parseArgs "api" "" [] (argv |> Array.toList)

    match dllPaths with
    | [||] ->
        eprintfn "Usage: fsharp-docs-oracle [--output-base <base>] <path/to/Assembly.dll> [...]"
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
            |> Array.map (extractAssembly checker allDllPaths basePath outputBase)
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
