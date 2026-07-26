/// <summary>
/// Constructs that the generator must render correctly but which no other fixture
/// module exercises. Added alongside the v1 snapshot tests.
/// </summary>
module Reference.Coverage

open System

// ---------------------------------------------------------------------------
// Literals
// ---------------------------------------------------------------------------

/// <summary>The maximum number of retry attempts.</summary>
[<Literal>]
let MaxRetries = 3

/// <summary>The greeting used when none is supplied.</summary>
[<Literal>]
let DefaultGreeting = "hello"

// ---------------------------------------------------------------------------
// RequireQualifiedAccess union
// ---------------------------------------------------------------------------

/// <summary>A log severity level. Callers must qualify the case names.</summary>
[<RequireQualifiedAccess>]
type Severity =
    /// <summary>Diagnostic detail, normally hidden.</summary>
    | Debug
    /// <summary>Routine progress information.</summary>
    | Info
    /// <summary>Something unexpected but recoverable.</summary>
    | Warning
    /// <summary>A failure, carrying its numeric code.</summary>
    | Error of code: int

/// <summary>
/// A union carrying members. The type header links each member to an anchor on the
/// page, so the page has to actually render those members.
/// </summary>
type Temperature =
    /// <summary>Degrees Celsius.</summary>
    | Celsius of float
    /// <summary>Degrees Fahrenheit.</summary>
    | Fahrenheit of float

    /// <summary>The temperature expressed in Celsius.</summary>
    member this.AsCelsius =
        match this with
        | Celsius value -> value
        | Fahrenheit value -> (value - 32.0) / 1.8

    /// <summary>Freezing point of water.</summary>
    static member Freezing = Celsius 0.0

// ---------------------------------------------------------------------------
// Interface, base class, inheritance and implementation
// ---------------------------------------------------------------------------

/// <summary>A contract for things that carry a name.</summary>
type INamed =
    /// <summary>The name of this object.</summary>
    abstract member Name: string

/// <summary>A base class carrying a unique identifier.</summary>
[<AbstractClass>]
type EntityBase(id: int) =

    /// <summary>The unique identifier.</summary>
    member _.Id = id

    /// <summary>Produces a human-readable description.</summary>
    abstract member Describe: unit -> string

/// <summary>
/// A user, inheriting <see cref="T:Reference.Coverage.EntityBase"/> and
/// implementing <see cref="T:Reference.Coverage.INamed"/>.
/// </summary>
type User(id: int, name: string) =
    inherit EntityBase(id)

    let mutable displayName = name

    /// <summary>The display name. Settable.</summary>
    member _.DisplayName
        with get () = displayName
        and set value = displayName <- value

    /// <summary>Whether the user is currently active.</summary>
    member val IsActive = true with get, set

    override _.Describe() = $"user {name}"

    interface INamed with
        member _.Name = name

// ---------------------------------------------------------------------------
// Constrained generic type, and an inline member
// ---------------------------------------------------------------------------

/// <summary>
/// A bag that keeps its contents sorted. The constraint is part of the type's
/// signature, so it belongs in the rendered type head.
/// </summary>
type SortedBag<'T when 'T: comparison>(items: 'T list) =

    /// <summary>The items, in ascending order.</summary>
    member _.Items = List.sort items

    /// <summary>The number of items.</summary>
    member _.Count = List.length items

    /// <summary>Returns whichever of two items sorts higher.</summary>
    /// <param name="first">The first item.</param>
    /// <param name="second">The second item.</param>
    /// <returns>The larger of the two.</returns>
    member inline _.Largest(first: 'T, second: 'T) = max first second

// ---------------------------------------------------------------------------
// Overloads
// ---------------------------------------------------------------------------

/// <summary>Formats values in several ways.</summary>
type Formatter() =

    /// <summary>Formats an integer.</summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The value as a string.</returns>
    member _.Format(value: int) = string value

    /// <summary>Formats a float to a fixed number of digits.</summary>
    /// <param name="value">The value to format.</param>
    /// <param name="digits">How many digits to keep.</param>
    /// <returns>The value as a string.</returns>
    member _.Format(value: float, digits: int) = value.ToString("F" + string digits)

// ---------------------------------------------------------------------------
// Optional and byref parameters
// ---------------------------------------------------------------------------

/// <summary>Parses text into numbers.</summary>
type Parser() =

    /// <summary>Parses a string, falling back to a supplied value.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="fallback">Value to use when parsing fails. Defaults to zero.</param>
    /// <returns>The parsed value, or the fallback.</returns>
    member _.Parse(input: string, ?fallback: int) =
        match Int32.TryParse input with
        | true, value -> value
        | _ -> defaultArg fallback 0

    /// <summary>Tries to parse a string, writing the result to an out parameter.</summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="result">Receives the parsed value.</param>
    /// <returns>True when parsing succeeded.</returns>
    member _.TryParse(input: string, result: byref<int>) = Int32.TryParse(input, &result)

// ---------------------------------------------------------------------------
// XML doc: typeparam and list
// ---------------------------------------------------------------------------

/// <summary>Returns the first element of a sequence, if there is one.</summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="items">The sequence to inspect.</param>
/// <returns>The first element, or <c>None</c> when the sequence is empty.</returns>
/// <remarks>
/// Behaviour by input:
/// <list type="bullet">
/// <item><description>An empty sequence yields <c>None</c>.</description></item>
/// <item><description>Any other sequence yields <c>Some first</c>.</description></item>
/// </list>
/// </remarks>
let tryFirst (items: 'T seq) : 'T option = Seq.tryHead items

/// <summary>Divides one number by another.</summary>
/// <param name="numerator">The number to divide.</param>
/// <param name="denominator">The number to divide by.</param>
/// <returns>The quotient.</returns>
/// <exception cref="T:System.DivideByZeroException">
/// Thrown when <paramref name="denominator"/> is zero.
/// </exception>
/// <seealso cref="T:Reference.Coverage.SortedBag`1"/>
let divide (numerator: int) (denominator: int) : int = numerator / denominator

/// <summary>A counter whose value can be read and written.</summary>
type Tally() =

    let mutable total = 0

    /// <summary>The running total.</summary>
    /// <value>The number of increments recorded so far.</value>
    member _.Total
        with get () = total
        and set value = total <- value

    /// <summary>
    /// Records one increment. See <see cref="T:System.Console"/>, which has no page
    /// here and so must not be linked.
    /// </summary>
    member _.Record() = total <- total + 1

// ---------------------------------------------------------------------------
// Events and indexed properties
// ---------------------------------------------------------------------------

/// <summary>Raises an event on each tick.</summary>
type Ticker() =

    let tick = Event<int>()

    /// <summary>Raised once per advance, carrying the new count.</summary>
    [<CLIEvent>]
    member _.Tick = tick.Publish

    /// <summary>Advances the ticker by one.</summary>
    member _.Advance() = tick.Trigger 1

/// <summary>A row of values addressable by index.</summary>
type Row(values: int list) =

    /// <summary>The value at the given position.</summary>
    /// <param name="index">The zero-based position.</param>
    member _.Item
        with get (index: int) = List.item index values

    /// <summary>How many values the row holds.</summary>
    member _.Length = List.length values

// ---------------------------------------------------------------------------
// Optional type extension, i.e. one declared away from the type it extends
// ---------------------------------------------------------------------------

/// <summary>Adds members to a type declared elsewhere.</summary>
module StringExtensions =

    type System.String with

        /// <summary>Returns the string in upper case, with an exclamation mark.</summary>
        member this.Shout() = this.ToUpper() + "!"

/// <summary>Combines two values using a supplied function.</summary>
/// <typeparam name="T">The input type.</typeparam>
/// <typeparam name="U">The result type.</typeparam>
/// <param name="combine">The combining function.</param>
/// <param name="first">The first value.</param>
/// <param name="second">The second value.</param>
/// <returns>The combined result.</returns>
let combineWith (combine: 'T -> 'T -> 'U) (first: 'T) (second: 'T) : 'U =
    combine first second

// ---------------------------------------------------------------------------
// Slug collision: these two differ only by case, so both slug to
// "reference-coverage-casing". They must not overwrite each other on disk.
// ---------------------------------------------------------------------------

/// <summary>Collides with <c>CASING</c> once slugged.</summary>
type Casing = { Value: string }

/// <summary>Collides with <c>Casing</c> once slugged.</summary>
type CASING = { Other: string }
