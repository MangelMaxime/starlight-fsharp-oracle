/// <summary>Function shapes, parameter kinds, generic constraints and active patterns.</summary>
module Reference.Functions

open System

// -- values -----------------------------------------------------------------

/// <summary>A literal, whose value is part of its contract.</summary>
[<Literal>]
let LiteralInt = 3

/// <summary>A string literal, which renders quoted.</summary>
[<Literal>]
let LiteralString = "hello"

/// <summary>A value with no parameters, so it is not a function.</summary>
let valueWithoutParameters: float * float = failwith "fixture"

// -- parameter shapes -------------------------------------------------------

/// <summary>Two curried parameter groups, separated by arrows.</summary>
/// <param name="factor">What to multiply by.</param>
/// <param name="value">The value to scale.</param>
/// <returns>The scaled value.</returns>
let functionWithCurriedParameters (factor: float) (value: float) : float = failwith "fixture"

/// <summary>A single tupled group, whose parameters are separated by <c>*</c>.</summary>
/// <param name="x">The x coordinate.</param>
/// <param name="y">The y coordinate.</param>
let functionWithTupledParameters (x: float, y: float) : float = failwith "fixture"

/// <summary>A function whose name contains spaces.</summary>
/// <param name="value">Any value.</param>
let ``function with spaces`` (value: int) : int = failwith "fixture"

/// <summary>Function-typed parameters, whose arrows nest.</summary>
/// <param name="project">Turns a value into text.</param>
/// <param name="accept">Decides on the text, given a label.</param>
/// <param name="value">The value to test.</param>
let functionWithFunctionParameters
    (project: int -> string)
    (accept: (int -> string) -> string -> bool)
    (value: int)
    : bool =
    failwith "fixture"

/// <summary>Optional and byref parameters.</summary>
type MembersWithParameterModifiers() =

    /// <summary>An optional parameter, written <c>?name</c> rather than an option.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="fallback">Used when parsing fails.</param>
    /// <returns>The parsed value, or the fallback.</returns>
    member _.ParseWithOptional(input: string, ?fallback: int) : int = failwith "fixture"

    /// <summary>A byref parameter.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="result">Receives the parsed value.</param>
    member _.ParseIntoByref(input: string, result: byref<int>) : bool = failwith "fixture"

// -- generic constraints ----------------------------------------------------

/// <summary>Requires comparison.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="items">The items to search.</param>
let constrainedByComparison<'T when 'T: comparison> (items: 'T list) : 'T = failwith "fixture"

/// <summary>Requires equality.</summary>
let constrainedByEquality<'T when 'T: equality> (left: 'T) (right: 'T) : bool = failwith "fixture"

/// <summary>Requires an unmanaged struct, so it carries two constraints.</summary>
let constrainedByUnmanagedStruct<'T when 'T: unmanaged and 'T: struct> (value: 'T) : 'T =
    failwith "fixture"

/// <summary>Requires a nullable reference.</summary>
let constrainedByNull<'T when 'T: null> (value: 'T) : 'T = failwith "fixture"

/// <summary>Requires a subtype.</summary>
let constrainedBySubtype<'T when 'T :> IDisposable> (items: 'T list) : unit = failwith "fixture"

/// <summary>Requires a parameterless constructor.</summary>
let constrainedByDefaultConstructor<'T when 'T: (new: unit -> 'T)> () : 'T = failwith "fixture"

/// <summary>Requires the static members an SRTP constraint names.</summary>
/// <typeparam name="T">A type supporting addition and a zero.</typeparam>
/// <param name="items">The items to total.</param>
let inline constrainedByStaticMembers< ^T
    when ^T: (static member (+): ^T * ^T -> ^T) and ^T: (static member Zero: ^T)>
    (items: ^T list)
    : ^T =
    failwith "fixture"

// -- active patterns --------------------------------------------------------

/// <summary>A single-case active pattern.</summary>
/// <param name="text">The string to normalize.</param>
/// <returns>The trimmed string.</returns>
let (|SingleCasePattern|) (text: string) : string = failwith "fixture"

/// <summary>A multi-case active pattern.</summary>
/// <param name="value">The number to classify.</param>
/// <returns>Its sign.</returns>
let (|MultiCasePositive|MultiCaseNegative|MultiCaseZero|) (value: float) : Choice<unit, unit, unit> =
    failwith "fixture"

/// <summary>A partial active pattern, which returns an option.</summary>
/// <param name="text">The text to parse.</param>
/// <returns>The integer, when the text is one.</returns>
let (|PartialPattern|_|) (text: string) : int option = failwith "fixture"

/// <summary>A parameterised partial pattern, so it takes two curried groups.</summary>
/// <param name="divisor">The number to divide by.</param>
/// <param name="value">The number to test.</param>
/// <returns>The quotient, when it divides exactly.</returns>
let (|ParameterisedPattern|_|) (divisor: int) (value: int) : int option = failwith "fixture"
