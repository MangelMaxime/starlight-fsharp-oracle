/// <summary>One of every kind of F# type declaration.</summary>
module Reference.Types

open System

// -- records ----------------------------------------------------------------

/// <summary>A record with documented fields.</summary>
type Point =
    {
        /// <summary>Distance along the x axis.</summary>
        X: float
        /// <summary>Distance along the y axis.</summary>
        Y: float
    }

    /// <summary>The distance from the origin.</summary>
    member this.Magnitude = sqrt (this.X * this.X + this.Y * this.Y)

/// <summary>A record stored on the stack.</summary>
[<Struct>]
type StructPoint = { Sx: float; Sy: float }

// -- unions -----------------------------------------------------------------

/// <summary>A union whose cases carry named and unnamed fields, plus members.</summary>
type Shape =
    /// <summary>A circle of the given radius.</summary>
    | Circle of radius: float
    /// <summary>A rectangle.</summary>
    | Rectangle of width: float * height: float
    /// <summary>A shape with no extent.</summary>
    | Empty

    /// <summary>The area covered.</summary>
    member this.Area =
        match this with
        | Circle r -> Math.PI * r * r
        | Rectangle(w, h) -> w * h
        | Empty -> 0.0

    /// <summary>The shape covering nothing.</summary>
    static member Nothing = Empty

/// <summary>Callers must qualify these case names.</summary>
[<RequireQualifiedAccess>]
type Severity =
    /// <summary>Routine progress information.</summary>
    | Info
    /// <summary>A failure, carrying its numeric code.</summary>
    | Error of code: int

/// <summary>A union stored on the stack.</summary>
[<Struct>]
type StructOption<'T> =
    | StructSome of value: 'T
    | StructNone

// -- enum, abbreviation, measure --------------------------------------------

/// <summary>An enumeration with explicit values.</summary>
type Level =
    /// <summary>The lowest level.</summary>
    | Low = 0
    /// <summary>The highest level.</summary>
    | High = 10

/// <summary>An abbreviation for a primitive.</summary>
type Radius = float

/// <summary>A unit of length.</summary>
[<Measure>]
type m

/// <summary>Converts a raw float to metres.</summary>
/// <param name="value">The raw value.</param>
let metres (value: float) : float<m> = LanguagePrimitives.FloatWithMeasure value

// -- exception, delegate ----------------------------------------------------

/// <summary>Raised when parsing fails.</summary>
exception ParseError of message: string * position: int

/// <summary>A callback that combines two values.</summary>
type Combiner<'T> = delegate of 'T * 'T -> 'T

// -- interface, inheritance, implementation ---------------------------------

/// <summary>A contract for things that carry a name.</summary>
type INamed =
    /// <summary>The name of this object.</summary>
    abstract member Name: string
    /// <summary>Describes this object.</summary>
    abstract member Describe: unit -> string

/// <summary>A base class carrying an identifier.</summary>
[<AbstractClass>]
type EntityBase(id: int) =

    /// <summary>The unique identifier.</summary>
    member _.Id = id

    /// <summary>Produces a human-readable description.</summary>
    abstract member Describe: unit -> string

/// <summary>
/// Inherits <see cref="T:Reference.Types.EntityBase"/> and implements
/// <see cref="T:Reference.Types.INamed"/>.
/// </summary>
type User(id: int, name: string) =
    inherit EntityBase(id)

    /// <summary>The user's name.</summary>
    member _.Name = name

    override _.Describe() = $"user {name}"

    interface INamed with
        member _.Name = name
        member this.Describe() = this.Describe()

/// <summary>A generic type whose constraint is part of its signature.</summary>
type SortedBag<'T when 'T: comparison>(items: 'T list) =

    /// <summary>The items, in ascending order.</summary>
    member _.Items = List.sort items
