// FORMATTING REFERENCE - deliberately not compiled (listed as <None> in Reference.fsproj).
//
// This is `dotnet telplin` output formatted with `dotnet fantomas`, kept as the
// canonical example of how F# renders signatures: parameter names, curried vs tupled
// groups, parenthesised function types, active-pattern names. Consult it before
// deciding how the generator should present a declaration, and regenerate it for any
// construct in doubt rather than inventing a format. See V1-PLAN.md.

/// <summary>Active patterns used to test pattern matching extraction.</summary>
module Reference.ActivePatterns

open System
/// <summary>Parses a string as an integer using a single-case active pattern.</summary>
/// <param name="str">The string to parse.</param>
/// <returns>The integer if parsing succeeds.</returns>
val (|Integer|): str: string -> int option
/// <summary>Normalizes whitespace in a string.</summary>
/// <param name="str">The input string.</param>
/// <returns>The string with normalized whitespace.</returns>
val (|NormalizeSpaces|): str: string -> string
/// <summary>Recognizes different shapes of numeric values.</summary>
/// <param name="n">The number to classify.</param>
/// <returns>The shape classification.</returns>
val (|Positive|Negative|Zero|): n: float -> Choice<unit, unit, unit>
/// <summary>Classifies a day of the week.</summary>
/// <param name="date">The date to classify.</param>
/// <returns>Weekday or Weekend classification.</returns>
val (|Weekday|Weekend|): date: DateTime -> Choice<unit, unit>
/// <summary>Analyzes string length.</summary>
/// <param name="str">The string to analyze.</param>
/// <returns>Classification by length.</returns>
val (|Empty|Short|Medium|Long|): str: string -> Choice<unit, unit, unit, unit>
/// <summary>Classifies an option value.</summary>
/// <typeparam name="T">The option's value type.</typeparam>
/// <param name="opt">The option to classify.</param>
/// <returns>Some or None classification.</returns>
val (|IsSome|IsNone|): opt: 'T option -> Choice<'T, unit>
/// <summary>Decomposes a result into success or failure.</summary>
/// <typeparam name="T">The success type.</typeparam>
/// <typeparam name="E">The error type.</typeparam>
/// <param name="result">The result to decompose.</param>
/// <returns>Success or Failure classification.</returns>
val (|Success|Failure|): result: Result<'T, 'E> -> Choice<'T, 'E>
/// <summary>Tries to parse a string as an even integer.</summary>
/// <param name="str">The string to parse.</param>
/// <returns>The even integer if successful.</returns>
val (|EvenInt|_|): str: string -> int option
/// <summary>Tries to parse a string as a prime number.</summary>
/// <param name="str">The string to parse.</param>
/// <returns>The prime number if successful.</returns>
val (|Prime|_|): str: string -> int option
/// <summary>Recognizes palindrome strings.</summary>
/// <param name="str">The string to check.</param>
/// <returns>The palindrome if it is one.</returns>
val (|Palindrome|_|): str: string -> string option
/// <summary>Recognizes valid email addresses (simplified).</summary>
/// <param name="str">The string to validate.</param>
/// <returns>The email if valid.</returns>
val (|Email|_|): str: string -> string option
/// <summary>Recognizes valid URL strings (simplified).</summary>
/// <param name="str">The string to validate.</param>
/// <returns>The URL if valid.</returns>
val (|Url|_|): str: string -> string option
/// <summary>Tries to extract a key-value pair from a string.</summary>
/// <param name="str">The string like "key=value".</param>
/// <returns>The key and value if the format matches.</returns>
val (|KeyValue|_|): str: string -> (string * string) option
/// <summary>Checks if a number is divisible by a given divisor.</summary>
/// <param name="divisor">The number to divide by.</param>
/// <param name="n">The number to check.</param>
/// <returns>The quotient if divisible.</returns>
val (|DivisibleBy|_|): divisor: int -> n: int -> int option
/// <summary>Checks if a string matches a regex pattern.</summary>
/// <param name="pattern">The regex pattern.</param>
/// <param name="str">The string to match.</param>
/// <returns>The matched value if successful.</returns>
val (|RegexMatch|_|): pattern: string -> str: string -> string option
/// <summary>Checks if a string starts with a given prefix.</summary>
/// <param name="prefix">The prefix to check.</param>
/// <param name="str">The string to check.</param>
/// <returns>The remainder if it starts with the prefix.</returns>
val (|StartsWith|_|): prefix: string -> str: string -> string option
/// <summary>Checks if a string ends with a given suffix.</summary>
/// <param name="suffix">The suffix to check.</param>
/// <param name="str">The string to check.</param>
/// <returns>The prefix if it ends with the suffix.</returns>
val (|EndsWith|_|): suffix: string -> str: string -> string option
/// <summary>Checks if a number is within a range.</summary>
/// <param name="min">The minimum value (inclusive).</param>
/// <param name="max">The maximum value (inclusive).</param>
/// <param name="n">The number to check.</param>
/// <returns>The number if within range.</returns>
val (|InRange|_|): min: int * max: int -> n: int -> int option
/// <summary>Checks if a list contains exactly N elements.</summary>
/// <param name="n">The expected count.</param>
/// <param name="lst">The list to check.</param>
/// <returns>The list if it has exactly N elements.</returns>
val (|Exactly|_|): n: int -> lst: 'T list -> 'T list option
/// <summary>Recognizes different types of numeric strings.</summary>
/// <param name="str">The string to classify.</param>
/// <returns>Integer, Float, or NotNumeric classification.</returns>
val (|IntegerString|FloatString|NotNumeric|): str: string -> Choice<int, float, unit>

/// <summary>Decomposes a tree structure into cases.</summary>
/// <typeparam name="T">The tree element type.</typeparam>
/// <param name="tree">The tree to decompose.</param>
/// <returns>Single, Pair, or Many classification.</returns>
type Tree<'T> =
    | Empty
    | Leaf of 'T
    | Node of Tree<'T> * Tree<'T>

val (|Single|Pair|Many|): tree: Tree<'T> -> Choice<'T, ('T * 'T), unit>
/// <summary>Analyzes collection emptiness and size.</summary>
/// <param name="seq">The sequence to analyze.</param>
/// <returns>Empty, Singleton, or Multiple classification.</returns>
val (|EmptySeq|Singleton|Multiple|): seq: 'T seq -> Choice<unit, 'T, int>
/// <summary>Logs and returns the value (for debugging patterns).</summary>
/// <param name="label">The label to log.</param>
/// <param name="value">The value to return.</param>
/// <returns>The value unchanged.</returns>
val (|Log|): label: string -> value: 'T -> 'T
/// <summary>Times the execution of a function and returns the result with elapsed time.</summary>
/// <param name="f">The function to time.</param>
/// <returns>The result and elapsed milliseconds.</returns>
val (|Timed|): f: (unit -> 'T) -> 'T * float
val mutable a: int
