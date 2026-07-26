namespace Reference.Namespaces

/// <summary>A type declared straight into a namespace, with no enclosing module.</summary>
type TypeDeclaredInNamespace =
    {
        /// <summary>The underlying value.</summary>
        Value: string
    }

/// <summary>
/// A generic type whose companion module below slugs to the same page, so the two are
/// merged rather than one overwriting the other.
/// </summary>
type TypeMergedWithCompanionModule<'T>(initial: 'T) =

    /// <summary>The current value.</summary>
    member _.Value
        with get (): 'T = failwith "fixture"
        and set (value: 'T) = failwith "fixture"

/// <summary>The companion module of the type above.</summary>
module TypeMergedWithCompanionModule =

    /// <summary>Creates a cell holding an initial value.</summary>
    /// <param name="initial">The starting value.</param>
    /// <returns>A new cell.</returns>
    let create (initial: 'T) : TypeMergedWithCompanionModule<'T> = failwith "fixture"

/// <summary>A module nested one level inside the namespace.</summary>
module NestedModule =

    /// <summary>A binding inside the nested module.</summary>
    let functionInNestedModule (text: string) : TypeDeclaredInNamespace = failwith "fixture"

/// <summary>A module whose name contains spaces, so its slug cannot be its name.</summary>
module ``Module With Spaces`` =

    /// <summary>A binding inside the backticked module.</summary>
    let functionInBacktickedModule () : unit = failwith "fixture"
