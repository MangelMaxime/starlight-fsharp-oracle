/// <summary>A module at the root of the assembly, with no enclosing namespace.</summary>
module Helpers

/// <summary>The library version.</summary>
let version = "1.0.0"

/// <summary>Pads a string on the left.</summary>
/// <param name="width">The target width.</param>
/// <param name="text">The string to pad.</param>
/// <returns>The padded string.</returns>
let padLeft (width: int) (text: string) = text.PadLeft(width)

/// <summary>Takes no arguments, so its unit parameter is explicit.</summary>
let timestamp () = System.DateTime.UtcNow

/// <summary>Settings for the helpers.</summary>
type Config =
    {
        /// <summary>How many times to retry.</summary>
        Retries: int
        /// <summary>Whether to log verbosely.</summary>
        Verbose: bool
    }

/// <summary>Superseded by the members above.</summary>
[<System.Obsolete("Use Helpers instead.")>]
module LegacyHelpers =

    /// <summary>Does nothing useful.</summary>
    [<System.Obsolete>]
    let oldPad (text: string) = text
