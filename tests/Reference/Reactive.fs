namespace Reference.Reactive

/// Marking state for a reactive node. Ordered Clean < Check < Dirty; an F# enum
/// so it is a plain int at runtime (erased by Fable) - named and type-checked,
/// zero cost vs raw ints.
type NodeState =
    /// The value is current; nothing downstream needs touching
    | Clean = 0
    /// A transitive dependency *might* have changed (verify before recomputing)
    | Check = 1
    /// A direct dependency changed
    | Dirty = 2

/// The reactive node and the writable handle. A **source** (`Var.create`) is a
/// `Var<'T>` you read and write; a **computed** is the same node type built with a
/// recompute function but handed back read-only as a `Signal<'T>`. ONE runtime
/// class backs both, so every read stays monomorphic (no dispatch, no hidden-class
/// polymorphism). Reading `.Value` inside a computation registers a dependency.
type Var<'T>(initial: 'T) =
    /// The current value held by this node.
    member _.Value = initial

/// <summary>Operations for creating and updating <see cref="T:Reference.Reactive.Var`1"/> nodes.</summary>
module Var =

    /// <summary>Create a new source node holding <paramref name="initial"/>.</summary>
    /// <param name="initial">The starting value of the node.</param>
    let create (initial: 'T) = Var<'T>(initial)

    /// <summary>Read the current value of a node.</summary>
    let get (node: Var<'T>) = node.Value
