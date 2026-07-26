/// <summary>Every XML documentation tag the generator understands.</summary>
/// <remarks>
/// Also carries the two names that collide once slugged, so the collision handling has
/// something to act on.
/// </remarks>
module Reference.Docs

/// <summary>Divides one number by another.</summary>
/// <remarks>
/// Behaviour by input:
/// <list type="bullet">
/// <item><description>A zero denominator throws.</description></item>
/// <item><description>Any other denominator divides normally.</description></item>
/// </list>
/// </remarks>
/// <typeparam name="T">Unused; present so a type parameter is documented.</typeparam>
/// <param name="numerator">The number to divide.</param>
/// <param name="denominator">The number to divide by.</param>
/// <returns>The quotient.</returns>
/// <exception cref="T:System.DivideByZeroException">
/// Thrown when <paramref name="denominator"/> is zero.
/// </exception>
/// <seealso cref="T:Reference.Docs.Tally"/>
/// <example>
/// <code>
/// let half = divide 10 2
/// </code>
/// </example>
let divide<'T> (numerator: int) (denominator: int) : int = numerator / denominator

/// <summary>A counter whose value can be read and written.</summary>
type Tally() =

    let mutable total = 0

    /// <summary>The running total.</summary>
    /// <value>The number of increments recorded so far.</value>
    member _.Total
        with get () = total
        and set value = total <- value

    /// <summary>
    /// Records an increment. Refers to <see cref="T:Reference.Docs.Tally"/>, which has a
    /// page, and to <see cref="T:System.Console"/>, which does not and so must not be
    /// linked.
    /// </summary>
    member _.Record() = total <- total + 1

/// <summary>Superseded by <see cref="T:Reference.Docs.Tally"/>.</summary>
[<System.Obsolete("Use Tally instead.")>]
type OldTally() =
    /// <summary>Does nothing.</summary>
    member _.Nothing = ()

/// <summary>Collides with <c>CASING</c> once slugged to a URL.</summary>
type Casing = { Value: string }

/// <summary>Collides with <c>Casing</c> once slugged to a URL.</summary>
type CASING = { Other: string }
