namespace Starlight.FSharp.RenderImpl

open FSharp.Oracle.Schema

/// Builds the token stream for every kind of declaration.
///
/// This is presentation: indentation, column alignment, which names anchor to their
/// own entry, where the `[<Struct>]` attribute sits. It lives here rather than in the
/// extractor so the IR stays a description of the API rather than of this website.
module Declarations =

    let private punct s = TextNode.Punctuation s
    let private colon = punct ":"
    let private arrow = punct "->"
    let private equals = punct "="
    let private star = punct "*"

    let private keyword k = TextNode.Keyword k

    /// n spaces, used for column alignment (as opposed to structural indentation).
    let private padding n =
        TextNode.Node [ for _ in 1..n -> TextNode.Space ]

    let private spaced nodes =
        nodes |> List.map (fun n -> [ TextNode.Space; n ]) |> List.concat

    // -----------------------------------------------------------------------
    // Generic parameters
    // -----------------------------------------------------------------------

    /// The constraint clause of a generic parameter list, with the angle brackets and
    /// leading type variable stripped, e.g. ` when 'T : comparison`. Appended after a
    /// signature rather than before it.
    let constraintClause (genericParameters: TextNode option) : TextNode =
        match genericParameters with
        | Some(TextNode.Node nodes) ->
            let inner =
                nodes
                |> List.skipWhile (
                    function
                    | TextNode.Punctuation "<" -> true
                    | _ -> false
                )
                |> List.skipWhile (
                    function
                    | TextNode.Tick
                    | TextNode.Text _ -> true
                    | _ -> false
                )
                |> List.rev
                |> List.skipWhile (
                    function
                    | TextNode.Punctuation ">" -> true
                    | _ -> false
                )
                |> List.rev
                |> List.skipWhile (
                    function
                    | TextNode.Space -> true
                    | _ -> false
                )

            if List.isEmpty inner then
                TextNode.Node []
            else
                TextNode.Node(TextNode.Space :: inner)
        | _ -> TextNode.Node []

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
                    TextNode.ParameterName p.Name
                    TextNode.Space
                    colon
                    TextNode.Space
                    p.Type
                ]

    // -----------------------------------------------------------------------
    // Functions and values
    // -----------------------------------------------------------------------

    let private valKeyword (isInline: bool) (isMutable: bool) =
        if isInline then "val inline"
        elif isMutable then "val mutable"
        else "val"

    /// A function signature laid out over several lines with the colons in one column:
    ///
    ///     val name   :
    ///         first  : int
    ///         second : string
    ///                -> bool
    ///
    /// The column is the longest of the function name and the parameter names, which
    /// is why this has to happen here: the extractor emits names and types, not layout.
    let functionSignature (f: Function) : TextNode =
        let allParams = f.Parameters |> List.collect id

        let column =
            let longestParameter =
                allParams |> List.map (fun p -> p.Name.Length) |> function
                    | [] -> 0
                    | lengths -> List.max lengths

            (max f.Name.Length longestParameter) + 1

        TextNode.Node
            [
                keyword (valKeyword f.IsInline f.IsMutable)
                TextNode.Space
                TextNode.Text f.Name
                padding (max (column - f.Name.Length) 1)
                colon

                for p in allParams do
                    TextNode.NewLine
                    TextNode.Indent 1
                    TextNode.Text p.Name
                    padding (column - p.Name.Length)
                    colon
                    TextNode.Space
                    p.Type

                if allParams.IsEmpty then
                    TextNode.Space
                    f.ReturnType
                else
                    // The arrow lines up under the colons above.
                    TextNode.NewLine
                    TextNode.Indent 1
                    padding column
                    arrow
                    TextNode.Space
                    f.ReturnType

                constraintClause f.GenericParameters
            ]

    let valueDeclaration (v: Value) : TextNode =
        TextNode.Node
            [
                keyword (valKeyword v.IsInline v.IsMutable)
                TextNode.Space
                TextNode.Text v.Name
                TextNode.Space
                colon
                TextNode.Space
                v.Type
                constraintClause v.GenericParameters
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
                [ keyword "static"; TextNode.Space ]
            elif m.IsAbstract then
                [ keyword "abstract"; TextNode.Space ]
            else
                []

        let noun =
            match m.Kind with
            | MemberKind.Property -> "property"
            | _ -> "member"

        let inlineKeyword =
            if m.IsInline then
                [ keyword "inline"; TextNode.Space ]
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
            keyword "with"
            TextNode.Space
            for i, name in List.indexed names do
                if i > 0 then
                    punct ","
                    TextNode.Space

                keyword name
        ]

    /// `: paramA -> paramB -> returnType`, or `: returnType` when there are none.
    let private memberTypeNodes (m: Member) (trailing: TextNode list) : TextNode list =
        let allParams = m.Parameters |> List.collect id

        [
            TextNode.Space
            colon
            TextNode.Space

            for i, p in List.indexed allParams do
                if i > 0 then
                    TextNode.Space
                    arrow
                    TextNode.Space

                parameterDeclaration p

            if not allParams.IsEmpty then
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
            | MemberKind.Constructor -> [ keyword "new" ]
            | _ -> [ TextNode.Text m.Name ]

        TextNode.Node
            [
                yield! memberPrefix m nameNode
                yield! memberTypeNodes m [ constraintClause m.GenericParameters ]
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
                    TextNode.DeclarationName(f.Name, f.Name, DeclarationRole.Member)
                    match f.LiteralValue with
                    | Some value ->
                        TextNode.Space
                        equals
                        TextNode.Space
                        TextNode.Text value
                    | None -> ()
                ]
        else
            TextNode.Node
                [
                    TextNode.Text f.Name
                    TextNode.Space
                    colon
                    TextNode.Space
                    f.Type
                ]

    /// The `a: int * b: string` payload of a union case or exception.
    ///
    /// `spaceBeforeColon` exists only because unions and exceptions disagree today -
    /// unions write `a : int`, exceptions write `a: int`. Fantomas writes neither with
    /// a space, so phase 4's colon-spacing decision removes this parameter. Keeping the
    /// difference explicit beats leaving it duplicated in two places.
    let private payloadFields (spaceBeforeColon: bool) (fields: Field list) : TextNode list =
        fields
        |> List.mapi (fun i f ->
            [
                if i > 0 then
                    TextNode.Space
                    star
                    TextNode.Space

                if f.Name <> "" then
                    TextNode.Text f.Name

                    if spaceBeforeColon then
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
                keyword "|"
                TextNode.Space
                TextNode.Text case.Name
                if not case.Fields.IsEmpty then
                    keyword " of"
                    TextNode.Space
                    yield! payloadFields true case.Fields
            ]

    // -----------------------------------------------------------------------
    // Entity headers
    // -----------------------------------------------------------------------

    /// `type Name<'T when 'T : comparison>`. The constraints belong here: they are part
    /// of how a caller must satisfy the type.
    let private typeHead (name: string) (generics: TextNode option) =
        [
            keyword "type"
            TextNode.Space
            TextNode.Text name
            match generics with
            | Some nodes -> nodes
            | None -> ()
        ]

    let private structAttribute (isStruct: bool) =
        [
            if isStruct then
                TextNode.Attribute "[<Struct>]"
                TextNode.NewLine
        ]

    let private memberLines (members: Member list) =
        anchoredMembers members
        |> List.collect (fun (m, anchor) -> memberHeaderLine anchor m)

    /// The full declaration shown at the top of an entity's page.
    let entityDeclaration (entity: Entity) : TextNode =
        match entity with
        | Entity.Measure e ->
            TextNode.Node
                [
                    TextNode.Attribute "[<Measure>]"
                    TextNode.NewLine
                    yield! typeHead e.Name e.GenericParameters
                ]

        | Entity.Exception e ->
            TextNode.Node
                [
                    keyword "exception"
                    TextNode.Space
                    TextNode.Text e.Name
                    if not e.Fields.IsEmpty then
                        keyword " of"
                        TextNode.Space
                        yield! payloadFields false e.Fields
                ]

        | Entity.Delegate e ->
            TextNode.Node
                [
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals
                    TextNode.Space
                    keyword "delegate"
                    TextNode.Space
                    keyword "of"
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
                    yield! structAttribute e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals

                    for case in e.Cases do
                        TextNode.NewLine
                        TextNode.Indent 1
                        keyword "|"
                        TextNode.Space
                        TextNode.DeclarationName(case.Name, case.Name, DeclarationRole.UnionCase)

                        if not case.Fields.IsEmpty then
                            keyword " of"
                            TextNode.Space
                            yield! payloadFields true case.Fields

                    yield! memberLines e.Members
                ]

        | Entity.Record e ->
            TextNode.Node
                [
                    yield! structAttribute e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals
                    TextNode.NewLine
                    TextNode.Indent 1
                    punct "{"

                    for f in e.Fields do
                        TextNode.NewLine
                        TextNode.Indent 2
                        TextNode.DeclarationName(f.Name, f.Name, DeclarationRole.Member)
                        TextNode.Space
                        colon
                        TextNode.Space
                        f.Type

                    TextNode.NewLine
                    TextNode.Indent 1
                    punct "}"

                    yield! memberLines e.Members
                ]

        | Entity.Enum e ->
            TextNode.Node
                [
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals

                    for f in e.Fields do
                        TextNode.NewLine
                        TextNode.Indent 1
                        keyword "|"
                        TextNode.Space
                        TextNode.DeclarationName(f.Name, f.Name, DeclarationRole.Member)

                        match f.LiteralValue with
                        | Some value ->
                            TextNode.Space
                            equals
                            TextNode.Space
                            TextNode.Text value
                        | None -> ()
                ]

        | Entity.Abbrev e ->
            TextNode.Node
                [
                    yield! typeHead e.Name e.GenericParameters
                    TextNode.Space
                    equals
                    TextNode.Space
                    e.AbbreviatedType
                ]

        | Entity.Interface e ->
            TextNode.Node
                [
                    yield! typeHead e.Name e.GenericParameters
                    yield! memberLines e.Members
                ]

        | Entity.Class e ->
            TextNode.Node
                [
                    yield! structAttribute e.IsStruct
                    yield! typeHead e.Name e.GenericParameters
                    yield! memberLines e.Members
                ]
