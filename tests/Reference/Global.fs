/// <summary>A module at the root of the assembly, with no enclosing namespace.</summary>
/// <remarks>
/// Bodies throughout this fixture are <c>failwith</c>: the generator reads signatures
/// and documentation from compiled metadata, so an implementation would be noise.
/// </remarks>
module RootModule

/// <summary>A value bound at the root of the assembly.</summary>
let valueAtRoot: string = failwith "fixture"

/// <summary>Two curried parameters, one of them long enough to set the colon column.</summary>
/// <param name="width">The target width.</param>
/// <param name="text">The string to pad.</param>
/// <returns>The padded string.</returns>
let functionWithCurriedParameters (width: int) (text: string) : string = failwith "fixture"

/// <summary>Takes no arguments, so its unit parameter is explicit.</summary>
let functionTakingUnit () : System.DateTime = failwith "fixture"

/// <summary>A record declared inside the root module.</summary>
type RecordInRootModule =
    {
        /// <summary>How many times to retry.</summary>
        Retries: int
        /// <summary>Whether to log verbosely.</summary>
        Verbose: bool
    }

/// <summary>A module carrying an obsolete attribute.</summary>
[<System.Obsolete("Superseded by RootModule.")>]
module ObsoleteNestedModule =

    /// <summary>An obsolete binding inside an obsolete module.</summary>
    [<System.Obsolete>]
    let obsoleteFunction (text: string) : string = failwith "fixture"
