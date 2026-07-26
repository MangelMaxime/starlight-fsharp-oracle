/// Reports which F# constructs the fixture actually exercises.
///
/// The fixture exists to cover the language, not to be large. This turns "does it still
/// cover everything?" into a diffable list, so the fixture can be cut down without
/// silently losing a construct.
module Oracle.Tests.Coverage

open FSharp.Oracle.Schema

let private textOf (node: TextNode) =
    let rec walk node =
        match node with
        | TextNode.Text s -> s
        | TextNode.Keyword s -> s
        | TextNode.Literal s -> s
        | TextNode.TypeVar s -> s
        | TextNode.ParameterName s -> s
        | TextNode.TypeRef(name, _) -> name
        | TextNode.DeclarationName(text, _, _) -> text
        | TextNode.DeclaredName(text, _) -> text
        | TextNode.Node nodes -> nodes |> List.map walk |> String.concat ""
        | TextNode.Punctuation Symbol.SubtypeOf -> ":>"
        | TextNode.Punctuation Symbol.Colon -> ":"
        | _ -> " "

    walk node

/// Every construct the generator has to handle, and whether the fixture has one.
let report (root: Root) : (string * int) list =
    let modules = root.Assemblies |> List.collect (fun a -> a.Modules)
    let entities = modules |> List.collect (fun m -> m.Entities)
    let functions = modules |> List.collect (fun m -> m.Functions)
    let values = modules |> List.collect (fun m -> m.Values)
    let extensions = modules |> List.collect (fun m -> m.ExtensionMembers)

    let members =
        entities
        |> List.collect (
            function
            | Entity.Record e -> e.Members
            | Entity.Union e -> e.Members
            | Entity.Class e -> e.Members
            | Entity.Interface e -> e.Members
            | _ -> []
        )

    let allParameters =
        (functions |> List.collect (fun f -> f.Parameters))
        @ (members |> List.collect (fun m -> m.Parameters))

    let docs =
        [
            yield! entities |> List.map (fun e -> e.XmlDoc)
            yield! functions |> List.map (fun f -> f.XmlDoc)
            yield! values |> List.map (fun v -> v.XmlDoc)
            yield! members |> List.map (fun m -> m.XmlDoc)
        ]

    let constraintText =
        (functions |> List.collect (fun f -> f.Constraints))
        @ (members |> List.collect (fun m -> m.Constraints))
        |> List.map textOf
        |> String.concat " "

    let countEntity predicate = entities |> List.filter predicate |> List.length

    let hasConstraint (keyword: string) =
        if constraintText.Contains keyword then 1 else 0

    [
        // Entity kinds
        "entity: record",
        countEntity (
            function
            | Entity.Record _ -> true
            | _ -> false
        )
        "entity: union",
        countEntity (
            function
            | Entity.Union _ -> true
            | _ -> false
        )
        "entity: class",
        countEntity (
            function
            | Entity.Class _ -> true
            | _ -> false
        )
        "entity: interface",
        countEntity (
            function
            | Entity.Interface _ -> true
            | _ -> false
        )
        "entity: enum",
        countEntity (
            function
            | Entity.Enum _ -> true
            | _ -> false
        )
        "entity: abbreviation",
        countEntity (
            function
            | Entity.Abbrev _ -> true
            | _ -> false
        )
        "entity: measure",
        countEntity (
            function
            | Entity.Measure _ -> true
            | _ -> false
        )
        "entity: exception",
        countEntity (
            function
            | Entity.Exception _ -> true
            | _ -> false
        )
        "entity: delegate",
        countEntity (
            function
            | Entity.Delegate _ -> true
            | _ -> false
        )
        "entity: struct", countEntity (fun e -> e.IsStruct)
        "entity: generic", countEntity (fun e -> e.GenericParameters.IsSome)
        "entity: with attributes", countEntity (fun e -> not e.Attributes.IsEmpty)
        "entity: obsolete", countEntity (fun e -> e.ObsoleteInfo <> ObsoleteInfo.Active)

        // Type shapes
        "entity: base type",
        countEntity (
            function
            | Entity.Class e -> e.BaseType.IsSome
            | _ -> false
        )
        "entity: implements interface",
        countEntity (
            function
            | Entity.Class e -> not e.Interfaces.IsEmpty
            | Entity.Record e -> not e.Interfaces.IsEmpty
            | Entity.Union e -> not e.Interfaces.IsEmpty
            | Entity.Interface e -> not e.Interfaces.IsEmpty
            | _ -> false
        )

        // Members
        "member: constructor",
        (members |> List.filter (fun m -> m.Kind = MemberKind.Constructor) |> List.length)
        "member: method", (members |> List.filter (fun m -> m.Kind = MemberKind.Method) |> List.length)
        "member: property",
        (members |> List.filter (fun m -> m.Kind = MemberKind.Property) |> List.length)
        "member: operator",
        (members |> List.filter (fun m -> m.Kind = MemberKind.Operator) |> List.length)
        "member: event", (members |> List.filter (fun m -> m.Kind = MemberKind.Event) |> List.length)
        "member: static", (members |> List.filter (fun m -> m.IsStatic) |> List.length)
        "member: abstract", (members |> List.filter (fun m -> m.IsAbstract) |> List.length)
        "member: inline", (members |> List.filter (fun m -> m.IsInline) |> List.length)
        "member: settable", (members |> List.filter (fun m -> m.HasSetter) |> List.length)
        "member: overloaded",
        (members
         |> List.countBy (fun m -> m.FullName)
         |> List.filter (fun (_, n) -> n > 1)
         |> List.length)
        "member: extension", extensions.Length

        // Functions, values, parameters
        "function: plain", (functions |> List.filter (fun f -> not f.IsActivePattern) |> List.length)
        "function: active pattern", (functions |> List.filter (fun f -> f.IsActivePattern) |> List.length)
        "function: inline", (functions |> List.filter (fun f -> f.IsInline) |> List.length)
        "function: curried (2+ groups)",
        (functions |> List.filter (fun f -> f.Parameters.Length > 1) |> List.length)
        "function: tupled group",
        ((functions |> List.collect (fun f -> f.Parameters))
         @ (members |> List.collect (fun m -> m.Parameters))
         |> List.filter (fun g -> g.Length > 1)
         |> List.length)
        "parameter: optional",
        (allParameters |> List.collect id |> List.filter (fun p -> p.IsOptional) |> List.length)
        "parameter: unit", (allParameters |> List.collect id |> List.filter (fun p -> p.IsUnit) |> List.length)
        "value: plain", values.Length
        "value: literal", (values |> List.filter (fun v -> v.LiteralValue.IsSome) |> List.length)

        // Generic constraints
        "constraint: comparison", hasConstraint "comparison"
        "constraint: equality", hasConstraint "equality"
        "constraint: unmanaged", hasConstraint "unmanaged"
        "constraint: struct", hasConstraint "struct"
        "constraint: null", hasConstraint "null"
        "constraint: subtype", hasConstraint ":>"
        "constraint: srtp member", hasConstraint "static member"
        "constraint: default constructor", hasConstraint "new"

        // XML documentation
        "doc: summary", (docs |> List.filter (fun d -> d.Summary.IsSome) |> List.length)
        "doc: remarks", (docs |> List.filter (fun d -> d.Remarks.IsSome) |> List.length)
        "doc: returns", (docs |> List.filter (fun d -> d.Returns.IsSome) |> List.length)
        "doc: param", (docs |> List.filter (fun d -> not d.Params.IsEmpty) |> List.length)
        "doc: typeparam", (docs |> List.filter (fun d -> not d.TypeParams.IsEmpty) |> List.length)
        "doc: exception", (docs |> List.filter (fun d -> not d.Exceptions.IsEmpty) |> List.length)
        "doc: value", (docs |> List.filter (fun d -> d.Value.IsSome) |> List.length)
        "doc: seealso", (docs |> List.filter (fun d -> not d.SeeAlso.IsEmpty) |> List.length)
        "doc: example", (docs |> List.filter (fun d -> not d.Examples.IsEmpty) |> List.length)
        "doc: cref link",
        (docs
         |> List.filter (fun d ->
             match d.Summary with
             | Some text -> text.Contains "fsharp-doc:"
             | None -> false
         )
         |> List.length)

        // Names F# allows but URLs do not
        "naming: backticked (contains a space)",
        ((entities |> List.filter (fun e -> e.Name.Contains " ") |> List.length)
         + (members |> List.filter (fun m -> m.Name.Contains " ") |> List.length)
         + (functions |> List.filter (fun f -> f.Name.Contains " ") |> List.length)
         + (modules |> List.filter (fun m -> m.Name.Contains " ") |> List.length))

        // Page-structure cases the generator has to handle
        "structure: module", (modules |> List.filter (fun m -> not m.IsSynthetic) |> List.length)
        "structure: nested module",
        (modules |> List.filter (fun m -> m.Namespace.Contains "." && not m.IsSynthetic) |> List.length)
        "structure: root module (no namespace)",
        (modules |> List.filter (fun m -> m.Namespace = "" && not m.IsSynthetic) |> List.length)
        "structure: namespace-level types (synthetic)",
        (modules |> List.filter (fun m -> m.IsSynthetic) |> List.length)
        "structure: obsolete module",
        (modules |> List.filter (fun m -> m.ObsoleteInfo <> ObsoleteInfo.Active) |> List.length)
    ]
