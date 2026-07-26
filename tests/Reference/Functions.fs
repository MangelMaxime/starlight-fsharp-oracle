/// <summary>Function shapes, parameter kinds, constraints and active patterns.</summary>
module Reference.Functions

open System

// -- values -----------------------------------------------------------------

/// <summary>The maximum number of retry attempts.</summary>
[<Literal>]
let MaxRetries = 3

/// <summary>The greeting used when none is supplied.</summary>
[<Literal>]
let DefaultGreeting = "hello"

/// <summary>A plain value, not a function.</summary>
let defaultPoint = 0.0, 0.0

// -- parameter shapes -------------------------------------------------------

/// <summary>Two curried parameter groups.</summary>
/// <param name="factor">What to multiply by.</param>
/// <param name="value">The value to scale.</param>
/// <returns>The scaled value.</returns>
let scale (factor: float) (value: float) = factor * value

/// <summary>A single tupled group, so its parameters are separated by <c>*</c>.</summary>
/// <param name="x">The x coordinate.</param>
/// <param name="y">The y coordinate.</param>
let distance (x: float, y: float) = sqrt (x * x + y * y)

/// <summary>Optional and byref parameters.</summary>
type Parser() =

    /// <summary>Parses a string, falling back to a supplied value.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="fallback">Used when parsing fails. Defaults to zero.</param>
    /// <returns>The parsed value, or the fallback.</returns>
    member _.Parse(input: string, ?fallback: int) =
        match Int32.TryParse input with
        | true, value -> value
        | _ -> defaultArg fallback 0

    /// <summary>Writes its result to an out parameter.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="result">Receives the parsed value.</param>
    member _.TryParse(input: string, result: byref<int>) = Int32.TryParse(input, &result)

// -- generic constraints ----------------------------------------------------

/// <summary>Requires comparison.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="items">The items to search.</param>
let largest<'T when 'T: comparison> (items: 'T list) = List.max items

/// <summary>Requires equality.</summary>
let areEqual<'T when 'T: equality> (a: 'T) (b: 'T) = a = b

/// <summary>Requires an unmanaged struct.</summary>
let sizeOf<'T when 'T: unmanaged and 'T: struct> (value: 'T) = value

/// <summary>Requires a nullable reference.</summary>
let orDefault<'T when 'T: null> (value: 'T) (fallback: 'T) =
    if isNull (box value) then fallback else value

/// <summary>Requires a subtype.</summary>
let disposeAll<'T when 'T :> IDisposable> (items: 'T list) =
    for item in items do
        item.Dispose()

/// <summary>Requires a parameterless constructor.</summary>
let create<'T when 'T: (new: unit -> 'T)> () = new 'T()

/// <summary>Requires the static members an SRTP constraint names.</summary>
/// <typeparam name="T">A type supporting addition and a zero.</typeparam>
let inline total< ^T when ^T: (static member (+): ^T * ^T -> ^T) and ^T: (static member Zero: ^T)>
    (items: ^T list)
    =
    List.fold (+) LanguagePrimitives.GenericZero items

/// <summary>A function whose name contains spaces.</summary>
/// <param name="value">Any value.</param>
let ``function with spaces`` (value: int) = value

// -- active patterns --------------------------------------------------------

/// <summary>A single-case active pattern.</summary>
/// <param name="text">The string to normalize.</param>
/// <returns>The trimmed string.</returns>
let (|Trimmed|) (text: string) = text.Trim()

/// <summary>A multi-case active pattern.</summary>
/// <param name="value">The number to classify.</param>
/// <returns>Its sign.</returns>
let (|Positive|Negative|Zero|) (value: float) =
    if value > 0.0 then Positive
    elif value < 0.0 then Negative
    else Zero

/// <summary>A partial active pattern.</summary>
/// <param name="text">The text to parse.</param>
/// <returns>The integer, when the text is one.</returns>
let (|Integer|_|) (text: string) =
    match Int32.TryParse text with
    | true, value -> Some value
    | _ -> None

/// <summary>A parameterised partial pattern, so it takes two curried groups.</summary>
/// <param name="divisor">The number to divide by.</param>
/// <param name="value">The number to test.</param>
/// <returns>The quotient, when it divides exactly.</returns>
let (|DivisibleBy|_|) (divisor: int) (value: int) =
    if value % divisor = 0 then Some(value / divisor) else None
