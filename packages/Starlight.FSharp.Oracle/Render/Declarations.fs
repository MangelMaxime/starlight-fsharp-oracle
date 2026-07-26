namespace Starlight.FSharp.RenderImpl

open FSharp.Oracle.Schema

/// Builds the token stream for every kind of declaration.
///
/// This is presentation: indentation, column alignment, which names anchor to their
/// own entry, where the `[<Struct>]` attribute sits. It lives here rather than in the
/// extractor so the IR stays a description of the API rather than of this website.
module Declarations =

    let private punct symbol = TextNode.Punctuation symbol
    let private colon = punct Symbol.Colon
    let private arrow = punct Symbol.Arrow
    let private equals = punct Symbol.Equals
    let private star = punct Symbol.Star

    let private keyword k = TextNode.Keyword k

    /// n spaces, used for column alignment (as opposed to structural indentation).
    let private padding n =
        TextNode.Node [ for _ in 1..n -> TextNode.Space ]

    let private spaced nodes =
        nodes |> List.map (fun n -> [ TextNode.Space; n ]) |> List.concat

    // -----------------------------------------------------------------------
    // Generic parameters
    // -----------------------------------------------------------------------

    /// The clause on one line, appended after a signature: ` when 'T : comparison`.
    let constraintClause (constraints: TextNode list) : TextNode =
        TextNode.Node
            [
                for i, constraint' in List.indexed constraints do
                    TextNode.Space

                    if i = 0 then
                        keyword Keyword.When
                    else
                        keyword Keyword.And

                    TextNode.Space
                    constraint'
            ]

    /// The clause with one constraint per line, indented to `column`.
    ///
    /// SRTP constraints are long - the widest line in the reference fixture was a
    /// 110-character constraint, not a type - and a wrapped line restarts at column 0,
    /// which destroys the alignment. Breaking where F# would breaks it deliberately.
    let constraintLines (column: int) (constraints: TextNode list) : TextNode list =
        [
            for i, constraint' in List.indexed constraints do
                TextNode.NewLine
                TextNode.Indent 1
                padding column

                if i = 0 then
                    keyword Keyword.When
                else
                    keyword Keyword.And

                TextNode.Space
                constraint'
        ]

    // -----------------------------------------------------------------------
    // Parameters
    // -----------------------------------------------------------------------

    /// `name : type`, with the name styled as a parameter. Used in the documentation
    /// block for each parameter and inside member signatures.
    let parameterDeclaration (p: Parameter) : TextNode =
        // An unnamed parameter is the `()` of `let timestamp ()`. Writing ` : unit`
        // with nothing before the colon reads as a mistake; the type alone does not.
        if p.Name = "" then
            p.Type
        else
            TextNode.Node
                [
                    if p.IsOptional then
                        punct Symbol.Question

                    TextNode.ParameterName p.Name
                    TextNode.Space
                    colon
                    TextNode.Space
                    p.Type
                ]

    /// Parameters of one curried group, tupled together with `*`.
    let private parameterGroup (group: Parameter list) : TextNode list =
        group
        |> List.map parameterDeclaration
        |> List.mapi (fun i node ->
            if i = 0 then
                [ node ]
            else
                [ TextNode.Space; star; TextNode.Space; node ]
        )
        |> List.concat

    // -----------------------------------------------------------------------
    // Functions and values
    // -----------------------------------------------------------------------

    /// `val`, `val inline`, `val mutable` - as separate tokens, so no keyword smuggles
    /// a space inside its own span.
    let private valKeyword (isInline: bool) (isMutable: bool) =
        [
            keyword Keyword.Val

            if isInline then
                TextNode.Space
                keyword Keyword.Inline
            elif isMutable then
                TextNode.Space
                keyword Keyword.Mutable
        ]

    /// A function signature laid out over several lines with the colons in one column:
    ///
    ///     val combineWith:
    ///         combine : 'T -> 'T -> 'U
    ///         first   : 'T
    ///         second  : 'T
    ///                 -> 'U
    ///
    /// The column comes from the parameter names alone. Including the function name -
    /// which is what the old layout did - made the gap depend on something unrelated to
    /// what is being aligned, and opened rivers of up to 38 blank columns between a
    /// parameter and its type.
    ///
    /// This has to happen here: the extractor emits names and types, not layout.
    let functionSignature (f: Function) : TextNode =
        let groups = f.Parameters |> List.filter (List.isEmpty >> not)
        let allParams = groups |> List.collect id

        let column =
            match allParams |> List.map (fun p -> p.Name.Length) with
            | [] -> 0
            | lengths -> List.max lengths + 1

        TextNode.Node
            [
                yield! valKeyword f.IsInline f.IsMutable
                TextNode.Space
                TextNode.DeclaredName(f.Name, DeclarationRole.Function)
                TextNode.Space
                colon

                for group in groups do
                    TextNode.NewLine
                    TextNode.Indent 1

                    match group with
                    // The common case: one parameter per curried group, colon aligned.
                    | [ p ] ->
                        if p.IsOptional then
                            punct Symbol.Question

                        // ParameterName, not plain text: the aligned layout was the
                        // one place a parameter lost its colour.
                        TextNode.ParameterName p.Name
                        padding (column - p.Name.Length)
                        colon
                        TextNode.Space
                        p.Type
                    // A tupled group stays on one line so the `*` between its members
                    // is not mistaken for currying.
                    | group -> yield! parameterGroup group

                if allParams.IsEmpty then
                    TextNode.Space
                    f.ReturnType
                    constraintClause f.Constraints
                else
                    // The arrow lines up under the column of colons above.
                    TextNode.NewLine
                    TextNode.Indent 1
                    padding column
                    arrow
                    TextNode.Space
                    f.ReturnType
                    yield! constraintLines column f.Constraints
            ]

    let valueDeclaration (v: Value) : TextNode =
        TextNode.Node
            [
                yield! valKeyword v.IsInline v.IsMutable
                TextNode.Space
                TextNode.DeclaredName(v.Name, DeclarationRole.Function)
                TextNode.Space
                colon
                TextNode.Space
                v.Type
                match v.LiteralValue with
                | Some value ->
                    TextNode.Space
                    equals
                    TextNode.Space
                    TextNode.Literal value
                | None -> ()
                constraintClause v.Constraints
            ]

    // -----------------------------------------------------------------------
    // Members
    // -----------------------------------------------------------------------

    /// The `static member` / `abstract property` / `new` prefix.
    /// `nameNode` differs between a standalone entry and a type header, where the name
    /// anchors to its own entry further down the page.
    let private memberPrefix (m: Member) (nameNode: TextNode list) : TextNode list =
        let modifier =
            if m.Kind = MemberKind.Operator || m.IsStatic then
                [ keyword Keyword.Static; TextNode.Space ]
            elif m.IsAbstract then
                [ keyword Keyword.Abstract; TextNode.Space ]
            else
                []

        let noun =
            match m.Kind with
            | MemberKind.Property -> "property"
            | MemberKind.Event -> "event"
            | _ -> "member"

        let inlineKeyword =
            if m.IsInline then
                [ keyword Keyword.Inline; TextNode.Space ]
            else
                []

        match m.Kind with
        | MemberKind.Constructor -> nameNode
        | _ ->
            [
                yield! modifier
                keyword noun
                TextNode.Space
                yield! inlineKeyword
                yield! nameNode
            ]

    /// ` with get, set`
    let private accessors (names: string list) =
        [
            TextNode.Space
            keyword Keyword.With
            TextNode.Space
            for i, name in List.indexed names do
                if i > 0 then
                    punct Symbol.Comma
                    TextNode.Space

                keyword name
        ]

    /// `: paramA -> paramB -> returnType`, or `: returnType` when there are none.
    let private memberTypeNodes (m: Member) (trailing: TextNode list) : TextNode list =
        // Curried groups are separated by `->`, parameters within a group by `*`.
        // Flattening the two together turns a .NET-style `Format(value, digits)` into
        // a curried signature that is not what the caller writes.
        let groups = m.Parameters |> List.filter (List.isEmpty >> not)

        [
            TextNode.Space
            colon
            TextNode.Space

            for i, group in List.indexed groups do
                if i > 0 then
                    TextNode.Space
                    arrow
                    TextNode.Space

                yield! parameterGroup group

            if not groups.IsEmpty then
                TextNode.Space
                arrow
                TextNode.Space

            m.ReturnType
            yield! trailing

            if m.Kind = MemberKind.Property then
                match m.HasGetter, m.HasSetter with
                | true, true -> yield! accessors [ "get"; "set" ]
                | false, true -> yield! accessors [ "set" ]
                // A property with neither accessor reported is still readable: FCS
                // leaves both false for some abstract and interface properties.
                | _ -> yield! accessors [ "get" ]
        ]

    /// A member as its own documented entry.
    let memberDeclaration (m: Member) : TextNode =
        let nameNode =
            match m.Kind with
            | MemberKind.Constructor -> [ keyword Keyword.New ]
            | _ -> [ TextNode.DeclaredName(m.Name, DeclarationRole.Member) ]

        TextNode.Node
            [
                yield! memberPrefix m nameNode
                yield! memberTypeNodes m [ constraintClause m.Constraints ]
            ]

    /// The anchor slug of a member. Operators anchor on their compiled name, because
    /// `(+)` cannot go into a URL fragment.
    let memberSlug (m: Member) =
        match m.Kind with
        | MemberKind.Operator -> Anchor.slug m.CompiledName
        | _ -> Anchor.slug m.Name

    /// Members paired with their page-unique anchors. Both the type header and the
    /// member sections derive anchors this way, from the same list in the same order,
    /// so the header's links and the entries' ids always agree.
    let anchoredMembers (members: Member list) = Anchor.assign memberSlug members

    /// Fields and union cases anchor the same way members do. They used to use their
    /// name verbatim, which breaks for a backticked F# name: `` ``Field With Spaces`` ``
    /// put spaces straight into an `id` and an `href`.
    let anchoredFields (fields: Field list) =
        Anchor.assign (fun (f: Field) -> Anchor.slug f.Name) fields

    let anchoredCases (cases: UnionCase list) =
        Anchor.assign (fun (c: UnionCase) -> Anchor.slug c.Name) cases

    /// A module's functions, split into the sections the page renders, each with its
    /// anchors assigned. Anchors are assigned per section, so anything deriving them
    /// has to split the same way - the sidebar used to guess, and used the raw name.
    let anchoredFunctionSections (functions: Function list) =
        let patterns, plain = functions |> List.partition (fun f -> f.IsActivePattern)

        let assign items =
            Anchor.assign (fun (f: Function) -> Anchor.slug f.Name) items

        [
            "Functions", "functions", assign plain
            "Active Patterns", "active-patterns", assign patterns
        ]

    let anchoredValues (values: Value list) =
        Anchor.assign (fun (v: Value) -> Anchor.slug v.Name) values

    /// A member as a line inside its type's header block, linking to its own entry.
    let memberHeaderLine (anchor: string) (m: Member) : TextNode list =
        let nameNode =
            match m.Kind with
            // Overloaded constructors all read `new` but anchor to new, new-1, ...
            | MemberKind.Constructor ->
                [ TextNode.DeclarationName("new", anchor, DeclarationRole.Constructor) ]
            | _ -> [ TextNode.DeclarationName(m.Name, anchor, DeclarationRole.Member) ]

        [
            TextNode.NewLine
            TextNode.Indent 1
            yield! memberPrefix m nameNode
            yield! memberTypeNodes m []
        ]

    // -----------------------------------------------------------------------
    // Fields, union cases
    // -----------------------------------------------------------------------

    /// A record field or an enum case as its own documented entry.
    /// Enum cases read `Name = 3`; record fields read `name : type`.
    let fieldDeclaration (isEnumCase: bool) (f: Field) : TextNode =
        if isEnumCase then
            TextNode.Node
                [
                    TextNode.DeclaredName(f.Name, DeclarationRole.Member)
                    match f.LiteralValue with
                    | Some value ->
                        TextNode.Space
                        equals
                        TextNode.Space
                        TextNode.Literal value
                    | None -> ()
                ]
        else
            TextNode.Node
                [
                    TextNode.DeclaredName(f.Name, DeclarationRole.Member)
                    TextNode.Space
                    colon
                    TextNode.Space
                    f.Type
                ]

    /// The `a: int * b: string` payload of a union case or exception.
    ///
    let private payloadFields (fields: Field list) : TextNode list =
        fields
        |> List.mapi (fun i f ->
            [
                if i > 0 then
                    TextNode.Space
                    star
                    TextNode.Space

                if f.Name <> "" then
                    TextNode.DeclaredName(f.Name, DeclarationRole.Member)
                    TextNode.Space
                    colon
                    TextNode.Space

                f.Type
            ]
        )
        |> List.concat

    /// A union case as its own documented entry.
    let unionCaseDeclaration (case: UnionCase) : TextNode =
        TextNode.Node
            [
                punct Symbol.Bar
                TextNode.Space
                TextNode.DeclaredName(case.Name, DeclarationRole.UnionCase)
                if not case.Fields.IsEmpty then
                    TextNode.Space
                    keyword Keyword.Of
                    TextNode.Space
                    yield! payloadFields case.Fields
            ]

    // -----------------------------------------------------------------------
    // Entity headers
    // -----------------------------------------------------------------------

    /// `type Name<'T when 'T : comparison>`. The constraints belong here: they are part
    /// of how a caller must satisfy the type.
    let private typeHead (name: string) (generics: TextNode option) =
        [
            keyword Keyword.Type
            TextNode.Space
            TextNode.DeclaredName(name, DeclarationRole.Type)
            match generics with
            | Some nodes -> nodes
            | None -> ()
        ]

    /// Attributes sit on their own lines above the declaration, as they are written.
    let private attributeLines (attributes: string list) (isStruct: bool) =
        [
            for attribute in attributes do
                TextNode.Attribute attribute
                TextNode.NewLine

            if isStruct then
                TextNode.Attribute "[<Struct>]"
                TextNode.NewLine
        ]

    /// `inherit Base` and `interface IFoo` lines, which say what a caller may pass the
    /// type as - often the most important fact about it.
    let private supertypeLines (baseType: TextNode option) (interfaces: TextNode list) =
        [
            match baseType with
            | Some node ->
                TextNode.NewLine
                TextNode.Indent 1
                keyword Keyword.Inherit
                TextNode.Space
                node
            | None -> ()

            for node in interfaces do
                TextNode.NewLine
                TextNode.Indent 1
                keyword Keyword.Interface
                TextNode.Space
                node
        ]

    let private memberLines (members: Member list) =
        anchoredMembers members
        |> List.collect (fun (m, anchor) -> memberHeaderLine anchor m)

    /// `type System.String with` followed by its extension members, so a section of
    /// extensions opens with the same at-a-glance overview an entity page has.
    let extensionDeclaration (extendedTypeName: string) (members: Member list) : TextNode =
        TextNode.Node
            [
                keyword Keyword.Type
                TextNode.Space
                TextNode.DeclaredName(extendedTypeName, DeclarationRole.Type)
                TextNode.Space
                keyword Keyword.With
                yield! memberLines members
            ]

    /// The full declaration shown at the top of an entity's page.
    let entityDeclaration (entity: Entity) : TextNode =
        match entity with
        | Entity.Measure e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes false
                    TextNode.Attribute "[<Measure>]"
                    TextNode.NewLine
                    yield! typeHead e.Name e.GenericParameters
                ]

        | Entity.Exception e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes false
                    keyword Keyword.Exception
                    TextNode.Space
                    TextNode.DeclaredName(e.Name, DeclarationRole.Type)
                    if not e.Fields.IsEmpty then
                        keyword " of"
                        TextNode.Space
                        yield! payloadFields e.Fields
                ]

        | Entity.Delegate e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes false
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals
                    TextNode.Space
                    keyword Keyword.Delegate
                    TextNode.Space
                    keyword Keyword.Of
                    TextNode.Space
                    yield!
                        e.Parameters
                        |> List.mapi (fun i t ->
                            [
                                if i > 0 then
                                    TextNode.Space
                                    star
                                    TextNode.Space
                                t
                            ]
                        )
                        |> List.concat
                    TextNode.Space
                    arrow
                    TextNode.Space
                    e.ReturnType
                ]

        | Entity.Union e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals

                    for case, anchor in anchoredCases e.Cases do
                        TextNode.NewLine
                        TextNode.Indent 1
                        punct Symbol.Bar
                        TextNode.Space
                        TextNode.DeclarationName(case.Name, anchor, DeclarationRole.UnionCase)

                        if not case.Fields.IsEmpty then
                            keyword " of"
                            TextNode.Space
                            yield! payloadFields case.Fields

                    yield! supertypeLines None e.Interfaces
                    yield! memberLines e.Members
                ]

        | Entity.Record e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals
                    TextNode.NewLine
                    TextNode.Indent 1
                    punct Symbol.LeftBrace

                    for f, anchor in anchoredFields e.Fields do
                        TextNode.NewLine
                        TextNode.Indent 2
                        TextNode.DeclarationName(f.Name, anchor, DeclarationRole.Member)
                        TextNode.Space
                        colon
                        TextNode.Space
                        f.Type

                    TextNode.NewLine
                    TextNode.Indent 1
                    punct Symbol.RightBrace

                    yield! supertypeLines None e.Interfaces
                    yield! memberLines e.Members
                ]

        | Entity.Enum e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals

                    for f, anchor in anchoredFields e.Fields do
                        TextNode.NewLine
                        TextNode.Indent 1
                        punct Symbol.Bar
                        TextNode.Space
                        TextNode.DeclarationName(f.Name, anchor, DeclarationRole.Member)

                        match f.LiteralValue with
                        | Some value ->
                            TextNode.Space
                            equals
                            TextNode.Space
                            TextNode.Literal value
                        | None -> ()
                ]

        | Entity.Abbrev e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals
                    TextNode.Space
                    e.AbbreviatedType
                ]

        | Entity.Interface e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes false
                    yield! typeHead e.Name e.GenericParameters
                    yield! supertypeLines None e.Interfaces
                    yield! memberLines e.Members
                ]

        | Entity.Class e ->
            TextNode.Node
                [
                    yield! attributeLines e.Attributes e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    yield! supertypeLines e.BaseType e.Interfaces
                    yield! memberLines e.Members
                ]
