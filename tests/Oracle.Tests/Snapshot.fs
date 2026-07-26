/// Minimal snapshot comparison, using Verify's `.verified` / `.received` file
/// convention (see the matching block in .editorconfig) without taking a
/// dependency on a test framework.
module Oracle.Tests.Snapshot

open System
open System.IO

type Result =
    /// Snapshot matched.
    | Passed
    /// Snapshot was created or rewritten (first run, or --update).
    | Written
    /// Snapshot differed; a `.received` file was left next to it.
    | Failed of message: string
    /// A `.verified` file exists that nothing produced any more.
    | Orphaned of path: string

let private receivedPathOf (verifiedPath: string) =
    verifiedPath.Replace(".verified.", ".received.")

/// Compare on normalized line endings so snapshots are portable across platforms.
let private normalize (text: string) = text.Replace("\r\n", "\n")

/// First line index where the two texts differ, plus the two lines, for reporting.
let private firstDifference (expected: string) (actual: string) =
    let expectedLines = expected.Split('\n')
    let actualLines = actual.Split('\n')

    let index =
        Seq.init (min expectedLines.Length actualLines.Length) id
        |> Seq.tryFind (fun i -> expectedLines.[i] <> actualLines.[i])
        |> Option.defaultValue (min expectedLines.Length actualLines.Length)

    let lineAt (lines: string array) =
        if index < lines.Length then
            lines.[index]
        else
            "<end of file>"

    index + 1, lineAt expectedLines, lineAt actualLines

/// Compare `actual` against the snapshot at `verifiedPath`.
/// With `update`, or when no snapshot exists yet, the snapshot is written instead.
let verify (update: bool) (verifiedPath: string) (actual: string) : Result =
    let receivedPath = receivedPathOf verifiedPath
    let actual = normalize actual

    let write () =
        Directory.CreateDirectory(Path.GetDirectoryName verifiedPath: string) |> ignore
        File.WriteAllText(verifiedPath, actual)

        if File.Exists receivedPath then
            File.Delete receivedPath

    if not (File.Exists verifiedPath) then
        write ()
        Written
    else

    let expected = normalize (File.ReadAllText verifiedPath)

    if expected = actual then
        if File.Exists receivedPath then
            File.Delete receivedPath

        Passed
    elif update then
        write ()
        Written
    else
        File.WriteAllText(receivedPath, actual)

        let line, expectedLine, actualLine = firstDifference expected actual

        Failed
            $"line %i{line}\n     expected: %s{expectedLine}\n     actual:   %s{actualLine}\n     received file: %s{receivedPath}"

/// `.verified` files under `directory` that this run did not produce, i.e. output that
/// used to exist and no longer does. Deleted when `update` is set.
let orphans (update: bool) (directory: string) (produced: string Set) : Result list =
    if not (Directory.Exists directory) then
        []
    else
        Directory.GetFiles(directory, "*.verified.*")
        |> Array.filter (fun path -> not (Set.contains (Path.GetFullPath path) produced))
        |> Array.map (fun path ->
            if update then
                File.Delete path
                Written
            else
                Orphaned path
        )
        |> Array.toList

/// Print a run summary and return the process exit code.
let report (name: string) (results: (string * Result) list) : int =
    let failures =
        results
        |> List.choose (fun (label, result) ->
            match result with
            | Failed message -> Some $"  FAIL {label}: {message}"
            | Orphaned path -> Some $"  FAIL {label}: stale snapshot with no matching output: {path}"
            | Passed
            | Written -> None
        )

    let written = results |> List.filter (snd >> (=) Written) |> List.length
    let passed = results |> List.filter (snd >> (=) Passed) |> List.length

    printfn ""
    printfn "%s: %i passed, %i written, %i failed" name passed written failures.Length

    if not failures.IsEmpty then
        printfn ""
        failures |> List.iter (printfn "%s")
        printfn ""
        printfn "Run './build.sh test --update' to accept these changes."
        1
    else
        0
