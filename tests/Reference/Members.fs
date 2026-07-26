/// <summary>One of every kind of type member.</summary>
module Reference.Members

open System

/// <summary>Overloads, accessors, statics and operators.</summary>
type Counter(label: string) =

    let mutable count = 0
    let ticked = Event<int>()

    /// <summary>Creates a counter labelled "default".</summary>
    new() = Counter("default")

    /// <summary>The label. Read-only.</summary>
    member _.Label = label

    /// <summary>The current count. Settable.</summary>
    member _.Count
        with get () = count
        and set value = count <- value

    /// <summary>Formats the count.</summary>
    /// <returns>A string.</returns>
    member _.Format() = string count

    /// <summary>Formats the count to a fixed width.</summary>
    /// <param name="width">The target width.</param>
    /// <returns>A padded string.</returns>
    member _.Format(width: int) = (string count).PadLeft(width)

    /// <summary>Raised on each increment.</summary>
    [<CLIEvent>]
    member _.Ticked = ticked.Publish

    /// <summary>Increments the counter.</summary>
    member _.Increment() =
        count <- count + 1
        ticked.Trigger count

    /// <summary>A shared counter.</summary>
    static member Shared = Counter("shared")

    /// <summary>Adds two counters' totals.</summary>
    /// <param name="a">Left operand.</param>
    /// <param name="b">Right operand.</param>
    static member (+)(a: Counter, b: Counter) = a.Count + b.Count

/// <summary>An indexed property and an inline member.</summary>
type Row(values: int list) =

    /// <summary>The value at a position.</summary>
    /// <param name="index">The zero-based position.</param>
    member _.Item
        with get (index: int) = List.item index values

    /// <summary>Returns whichever argument is larger.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    member inline _.Largest(first: 'T, second: 'T) = max first second

/// <summary>Members added to a type declared elsewhere.</summary>
module StringExtensions =

    type System.String with

        /// <summary>Returns the string in upper case with an exclamation mark.</summary>
        member this.Shout() = this.ToUpper() + "!"
