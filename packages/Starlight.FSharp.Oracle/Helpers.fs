module Starlight.FSharp.Helpers

open Fable.Core
open Fable.Core.JsInterop
open Node.Api

/// Wraps fs.writeFileSync with error handling
let writeSync (path: string) (content: string) : Result<unit, string> =
    try
        fs.writeFileSync (path, content, box "utf-8")
        Ok()
    with ex ->
        Error $"Failed to write file '{path}': {ex.Message}"

/// Wraps fs.mkdirSync with error handling
let mkdirSync (path: string) : Result<unit, string> =
    try
        fs?mkdirSync (
            path,
            {|
                recursive = true
            |}
        )
        |> ignore

        Ok()
    with ex ->
        Error $"Failed to create directory '{path}': {ex.Message}"

/// Deletes generated pages left over from a previous run.
///
/// The generator only ever wrote files, so a renamed or deleted type kept its page
/// forever - stale, unreachable, and still in the built site.
let cleanSync (path: string) (extension: string) : Result<unit, string> =
    try
        if fs.existsSync (U2.Case1 path) then
            for entry in fs.readdirSync (U2.Case1 path) do
                if entry.EndsWith extension then
                    fs.unlinkSync (U2.Case1(path + "/" + entry))

        Ok()
    with ex ->
        Error $"Failed to clean directory '{path}': {ex.Message}"

/// Collects all errors from a list of results
let collectErrors (results: Result<unit, string> list) : string list =
    results
    |> List.choose (
        function
        | Error msg -> Some msg
        | Ok _ -> None
    )
