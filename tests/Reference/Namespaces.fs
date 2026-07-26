namespace Reference.Data

/// <summary>A type declared straight into a namespace, with no enclosing module.</summary>
type Identifier =
    {
        /// <summary>The underlying value.</summary>
        Value: string
    }

/// <summary>A mutable cell. Shares its name with the module below.</summary>
type Var<'T>(initial: 'T) =

    let mutable current = initial

    /// <summary>The current value.</summary>
    member _.Value
        with get () = current
        and set value = current <- value

/// <summary>
/// The companion module of <c>Var</c>. It slugs to the same page as the type, so the
/// two are merged rather than one overwriting the other.
/// </summary>
module Var =

    /// <summary>Creates a cell holding an initial value.</summary>
    /// <param name="initial">The starting value.</param>
    /// <returns>A new cell.</returns>
    let create (initial: 'T) = Var<'T>(initial)

/// <summary>A module nested one level inside the namespace.</summary>
module Parsing =

    /// <summary>Parses text into an identifier.</summary>
    /// <param name="text">The text to parse.</param>
    let parse (text: string) = { Value = text }
