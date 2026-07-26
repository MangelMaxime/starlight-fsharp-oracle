/// <summary>One of every kind of F# type declaration.</summary>
module Reference.Types

// -- records ----------------------------------------------------------------

/// <summary>A record with documented fields and a member.</summary>
type RecordWithDocumentedFields =
    {
        /// <summary>Distance along the x axis.</summary>
        X: float
        /// <summary>Distance along the y axis.</summary>
        Y: float
    }

    /// <summary>The distance from the origin.</summary>
    member this.Magnitude: float = failwith "fixture"

/// <summary>A record stored on the stack.</summary>
[<Struct>]
type StructRecord = { Width: float; Height: float }

/// <summary>A type whose name contains spaces, so its slug cannot be its name.</summary>
type ``Type With Spaces`` =
    {
        /// <summary>A field whose name also contains spaces.</summary>
        ``Field With Spaces``: string
    }

// -- unions -----------------------------------------------------------------

/// <summary>A union with named and unnamed case fields, plus members.</summary>
type UnionWithCaseFieldsAndMembers =
    /// <summary>A case with one named field.</summary>
    | CaseWithNamedField of radius: float
    /// <summary>A case with several named fields.</summary>
    | CaseWithSeveralFields of width: float * height: float
    /// <summary>A case with an unnamed field.</summary>
    | CaseWithUnnamedField of string
    /// <summary>A case carrying nothing.</summary>
    | CaseWithoutFields

    /// <summary>An instance member on a union.</summary>
    member this.Area: float = failwith "fixture"

    /// <summary>A static member on a union.</summary>
    static member Nothing: UnionWithCaseFieldsAndMembers = failwith "fixture"

/// <summary>Callers must qualify these case names.</summary>
[<RequireQualifiedAccess>]
type RequireQualifiedAccessUnion =
    /// <summary>Routine progress information.</summary>
    | Info
    /// <summary>A failure, carrying its numeric code.</summary>
    | Error of code: int

/// <summary>A union stored on the stack.</summary>
[<Struct>]
type StructUnion<'T> =
    | StructCaseWithField of value: 'T
    | StructCaseWithoutField

// -- enum, abbreviation, measure --------------------------------------------

/// <summary>An enumeration with explicit values.</summary>
type EnumWithExplicitValues =
    /// <summary>The lowest level.</summary>
    | Low = 0
    /// <summary>The highest level.</summary>
    | High = 10

/// <summary>An abbreviation for a primitive type.</summary>
type AbbreviationOfPrimitive = float

/// <summary>A unit of measure. Deliberately one character, to vary name length.</summary>
[<Measure>]
type m

/// <summary>Returns a value carrying a unit of measure.</summary>
/// <param name="value">The raw value.</param>
let functionReturningMeasure (value: float) : float<m> = failwith "fixture"

// -- exception, delegate ----------------------------------------------------

/// <summary>An exception carrying named fields.</summary>
exception ExceptionWithNamedFields of message: string * position: int

/// <summary>A delegate taking a tupled pair.</summary>
type DelegateWithTupledParameters<'T> = delegate of 'T * 'T -> 'T

// -- interface, inheritance, implementation ---------------------------------

/// <summary>An interface with a property and a method.</summary>
type InterfaceWithMembers =
    /// <summary>The name of this object.</summary>
    abstract member Name: string
    /// <summary>Describes this object.</summary>
    abstract member Describe: unit -> string

/// <summary>An abstract base class.</summary>
[<AbstractClass>]
type AbstractBaseClass(id: int) =

    /// <summary>The unique identifier.</summary>
    member _.Id: int = failwith "fixture"

    /// <summary>Implemented by subclasses.</summary>
    abstract member Describe: unit -> string

/// <summary>
/// Inherits <see cref="T:Reference.Types.AbstractBaseClass"/> and implements
/// <see cref="T:Reference.Types.InterfaceWithMembers"/>.
/// </summary>
type ClassInheritingAndImplementing(id: int, name: string) =
    inherit AbstractBaseClass(id)

    /// <summary>The name of this object.</summary>
    member _.Name: string = failwith "fixture"

    override _.Describe() : string = failwith "fixture"

    interface InterfaceWithMembers with
        member _.Name: string = failwith "fixture"
        member _.Describe() : string = failwith "fixture"

/// <summary>A generic class whose constraint is part of its signature.</summary>
type GenericClassWithConstraint<'T when 'T: comparison>(items: 'T list) =

    /// <summary>The items, in ascending order.</summary>
    member _.SortedItems: 'T list = failwith "fixture"
