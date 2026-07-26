/// <summary>One of every kind of type member.</summary>
module Reference.Members

open System

/// <summary>Overloads, accessors, statics, an operator and an event.</summary>
type MembersOfEveryKind(label: string) =

    let mutable count = 0
    let changed = Event<int>()

    /// <summary>An overloaded constructor taking no arguments.</summary>
    new() = MembersOfEveryKind("default")

    /// <summary>A read-only property.</summary>
    member _.Label: string = failwith "fixture"

    /// <summary>A property with both accessors.</summary>
    member _.Count
        with get (): int = count
        and set (value: int) = count <- value

    /// <summary>An overload taking nothing.</summary>
    /// <returns>The formatted count.</returns>
    member _.Format() : string = failwith "fixture"

    /// <summary>An overload taking a width, so the two share an anchor base.</summary>
    /// <param name="width">The target width.</param>
    /// <returns>The padded count.</returns>
    member _.Format(width: int) : string = failwith "fixture"

    /// <summary>An event, which also produces add and remove accessors.</summary>
    [<CLIEvent>]
    member _.Changed = changed.Publish

    /// <summary>A method returning unit.</summary>
    member _.Increment() : unit = failwith "fixture"

    /// <summary>A member whose name contains spaces.</summary>
    member _.``Member With Spaces``() : unit = failwith "fixture"

    /// <summary>A static property.</summary>
    static member Shared: MembersOfEveryKind = failwith "fixture"

    /// <summary>An operator, which anchors on its compiled name.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    static member (+)(left: MembersOfEveryKind, right: MembersOfEveryKind) : int =
        failwith "fixture"

/// <summary>An indexed property and an inline member.</summary>
type IndexedAndInlineMembers(values: int list) =

    /// <summary>The value at a position.</summary>
    /// <param name="index">The zero-based position.</param>
    member _.Item
        with get (index: int): int = failwith "fixture"

    /// <summary>An inline member.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    member inline _.Larger(first: 'T, second: 'T) : 'T = failwith "fixture"

/// <summary>Members added to a type declared elsewhere.</summary>
module ExtensionMembers =

    type System.String with

        /// <summary>An extension member on a type with no page of its own.</summary>
        member this.ShoutedCopy() : string = failwith "fixture"
