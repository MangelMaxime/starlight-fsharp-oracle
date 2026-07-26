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

    let resolved = resolveAssemblies checker allDlls
    let assembly = extractAssembly resolved fixtureDll

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

    // ---- Construct coverage -------------------------------------------------

    // Snapshotted so cutting the fixture down cannot quietly drop a construct: a
    // count reaching zero shows up as a failing diff.
    let coverageReport =
        Coverage.report root
        |> List.map (fun (name, count) ->
            let status =
                if count = 0 then
                    "MISSING"
                else
                    string count

            $"%-46s{name} %s{status}"
        )
        |> String.concat "\n"

    let coverageResult =
        Snapshot.verify
            update
            (Path.Combine(snapshotDirectory, "coverage.verified.txt"))
            coverageReport

    // A construct dropping to zero is a failure in its own right, not just a diff:
    // --update would otherwise accept the loss without comment.
    let uncovered = Coverage.report root |> List.filter (fun (_, count) -> count = 0)

    for name, _ in uncovered do
        eprintfn "error: no fixture covers %s" name

    // ---- Page snapshots ----------------------------------------------------

    let pagesDirectory = Path.Combine(snapshotDirectory, "pages")
    let modules = root.Assemblies |> List.collect (fun a -> a.Modules)

    for warning in Starlight.FSharp.Generate.slugWarnings modules do
        printfn "  warning: %s" warning

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
            "coverage", coverageResult
            yield! pageResults |> List.map snd
            yield! orphanResults
        ]

    let exitCode = Snapshot.report "snapshots" results

    if uncovered.IsEmpty then
        exitCode
    else
        printfn ""
        printfn "%i construct(s) have no fixture." uncovered.Length
        1
