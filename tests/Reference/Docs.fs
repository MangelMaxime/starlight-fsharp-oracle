/// <summary>Every XML documentation tag the generator understands.</summary>
/// <remarks>
/// Also carries the two names that collide once slugged, so the collision handling has
/// something to act on.
/// </remarks>
module Reference.Docs

/// <summary>A function documented with every tag at once.</summary>
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
/// <seealso cref="T:Reference.Docs.TypeWithDocumentedValue"/>
/// <example>
/// <code>
/// let half = functionWithEveryDocTag 10 2
/// </code>
/// </example>
let functionWithEveryDocTag<'T> (numerator: int) (denominator: int) : int = failwith "fixture"

/// <summary>A type carrying a documented property value.</summary>
type TypeWithDocumentedValue() =

    /// <summary>The running total.</summary>
    /// <value>The number of increments recorded so far.</value>
    member _.Total
        with get (): int = failwith "fixture"
        and set (value: int) = failwith "fixture"

    /// <summary>
    /// Refers to <see cref="T:Reference.Docs.TypeWithDocumentedValue"/>, which has a
    /// page, and to <see cref="T:System.Console"/>, which does not and so must render
    /// as plain text.
    /// </summary>
    member _.Record() : unit = failwith "fixture"

/// <summary>Superseded by <see cref="T:Reference.Docs.TypeWithDocumentedValue"/>.</summary>
[<System.Obsolete("Use TypeWithDocumentedValue instead.")>]
type ObsoleteType() =
    /// <summary>An obsolete member of an obsolete type.</summary>
    member _.Unused: unit = failwith "fixture"

/// <summary>Differs from <c>CASECOLLISION</c> only by case, so both slug alike.</summary>
type CaseCollision = { First: string }

/// <summary>Differs from <c>CaseCollision</c> only by case, so both slug alike.</summary>
type CASECOLLISION = { Second: string }
