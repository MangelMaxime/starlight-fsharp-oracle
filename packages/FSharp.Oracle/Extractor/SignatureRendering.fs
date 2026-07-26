namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Helpers

/// Turns FCS types and constraints into signature tokens.
/// These are semantic tokens only - no links, no layout. See `TextNode`.
module internal SignatureRendering =

    let private colon = TextNode.Punctuation Symbol.Colon
    let private arrow = TextNode.Punctuation Symbol.Arrow
    let private comma = TextNode.Punctuation Symbol.Comma
    let private star = TextNode.Punctuation Symbol.Star
    let private openAngle = TextNode.Punctuation Symbol.LessThan
    let private closeAngle = TextNode.Punctuation Symbol.GreaterThan
    let private openParen = TextNode.Punctuation Symbol.LeftParen
    let private closeParen = TextNode.Punctuation Symbol.RightParen

    /// Joins rendered items with a separator token sequence.
    let private separated (separator: TextNode list) (items: TextNode list) =
        items
        |> List.mapi (fun i item ->
            if i = 0 then
                [ item ]
            else
                separator @ [ item ]
        )
        |> List.concat

    let rec renderFSharpType (isTopLevel: bool) (typ: FSharpType) : TextNode =
        if typ.IsGenericParameter then
            let gp = typ.GenericParameter

            TextNode.Node
                [
                    if isSrtp gp then
                        TextNode.Text "^"
                    else
                        TextNode.Tick
                    TextNode.Text gp.DisplayName
                ]
        elif typ.IsFunctionType then
            let parts =
                typ.GenericArguments
                |> Seq.map (renderFSharpType false)
                |> Seq.toList
                |> separated [ TextNode.Space; arrow; TextNode.Space ]

            if isTopLevel then
                TextNode.Node parts
            else
                TextNode.Node
                    [
                        openParen
                        yield! parts
                        closeParen
                    ]
        elif typ.IsTupleType then
            typ.GenericArguments
            |> Seq.map (renderFSharpType false)
            |> Seq.toList
            |> separated [ TextNode.Space; star; TextNode.Space ]
            |> TextNode.Node
        elif typ.IsAnonRecordType then
            let names = typ.AnonRecordTypeDetails.SortedFieldNames
            let types = typ.GenericArguments |> Seq.toArray

            let fields =
                names
                |> Array.mapi (fun i name ->
                    TextNode.Node
                        [
                            TextNode.Text name
                            TextNode.Space
                            colon
                            TextNode.Space
                            renderFSharpType false types.[i]
                        ]
                )
                |> Array.toList
                |> separated [ TextNode.Punctuation Symbol.Semicolon; TextNode.Space ]

            TextNode.Node
                [
                    TextNode.Text "{|"
                    TextNode.Space
                    yield! fields
                    TextNode.Space
                    TextNode.Text "|}"
                ]
        elif typ.HasTypeDefinition then
            let td = typ.TypeDefinition

            let head =
                match tryGetFullName td with
                | Some fullName -> TextNode.TypeRef(td.DisplayName, fullName)
                | None -> TextNode.Text td.DisplayName

            let args = typ.GenericArguments |> Seq.map (renderFSharpType false) |> Seq.toList

            match args with
            | [] -> head
            | [ single ] when td.IsArrayType ->
                TextNode.Node
                    [
                        single
                        TextNode.Text "[]"
                    ]
            | [ single ] when isPostfixType td ->
                // `int list`, `string option` - F# writes these the other way round.
                TextNode.Node
                    [
                        single
                        TextNode.Space
                        head
                    ]
            | args ->
                TextNode.Node
                    [
                        head
                        openAngle
                        yield! separated [ comma; TextNode.Space ] args
                        closeAngle
                    ]
        else
            TextNode.Text(typ.Format FSharpDisplayContext.Empty)

    /// Render a single generic parameter constraint as a token list.
    let renderConstraint
        (gp: FSharpGenericParameter)
        (c: FSharpGenericParameterConstraint)
        : TextNode list
        =
        /// `: keyword`
        let simple keyword =
            [
                colon
                TextNode.Space
                TextNode.Keyword keyword
            ]

        if c.IsComparisonConstraint then
            simple Keyword.Comparison
        elif c.IsEqualityConstraint then
            simple Keyword.Equality
        elif c.IsUnmanagedConstraint then
            simple Keyword.Unmanaged
        elif c.IsNonNullableValueTypeConstraint then
            simple Keyword.Struct
        elif c.IsReferenceTypeConstraint || c.IsNotSupportsNullConstraint then
            [
                colon
                TextNode.Space
                TextNode.Keyword Keyword.Not
                TextNode.Space
                TextNode.Keyword Keyword.Null
            ]
        elif c.IsSupportsNullConstraint then
            simple Keyword.Null
        elif c.IsCoercesToConstraint then
            [
                TextNode.Punctuation Symbol.SubtypeOf
                TextNode.Space
                renderFSharpType false c.CoercesToTarget
            ]
        elif c.IsMemberConstraint then
            // SRTP - render a simplified member constraint
            let m = c.MemberConstraintData
            let argTypes = m.MemberArgumentTypes |> Seq.toList

            let argNodes =
                if List.isEmpty argTypes then
                    []
                else
                    [
                        TextNode.Space
                        yield!
                            argTypes
                            |> List.map (renderFSharpType false)
                            |> separated [ TextNode.Space; star; TextNode.Space ]
                    ]

            let retNodes =
                let rt = m.MemberReturnType

                if isNull (box rt) then
                    []
                elif List.isEmpty argTypes then
                    [
                        TextNode.Space
                        renderFSharpType false rt
                    ]
                else
                    [
                        TextNode.Space
                        arrow
                        TextNode.Space
                        renderFSharpType false rt
                    ]

            [
                colon
                TextNode.Space
                openParen
                if m.MemberIsStatic then
                    TextNode.Keyword Keyword.Static
                    TextNode.Space

                TextNode.Keyword Keyword.Member
                TextNode.Space
                TextNode.Text m.MemberName
                if not (List.isEmpty argTypes) || not (isNull (box m.MemberReturnType)) then
                    TextNode.Space
                    colon
                    yield! argNodes
                    yield! retNodes
                closeParen
            ]
        elif c.IsRequiresDefaultConstructorConstraint then
            [
                colon
                TextNode.Space
                openParen
                TextNode.Keyword Keyword.New
                TextNode.Space
                colon
                TextNode.Space
                TextNode.Text "unit"
                TextNode.Space
                arrow
                TextNode.Space
                TextNode.Tick
                TextNode.Text gp.DisplayName
                closeParen
            ]
        elif c.IsEnumConstraint then
            [
                colon
                TextNode.Space
                TextNode.Keyword Keyword.Enum
                openAngle
                renderFSharpType false c.EnumConstraintTarget
                closeAngle
            ]
        elif c.IsDelegateConstraint then
            simple Keyword.Delegate
        elif c.IsDefaultsToConstraint then
            [
                TextNode.Punctuation Symbol.Equals
                TextNode.Space
                renderFSharpType false c.DefaultsToConstraintData.DefaultsToTarget
            ]
        elif c.IsSimpleChoiceConstraint then
            c.SimpleChoices
            |> Seq.map (renderFSharpType false)
            |> Seq.toList
            |> separated
                [
                    TextNode.Space
                    TextNode.Punctuation Symbol.Bar
                    TextNode.Space
                ]
        else
            []

    /// One entry per constraint, e.g. `'T : comparison`.
    ///
    /// A list rather than a single `when ... and ...` clause: the renderer decides how
    /// to lay them out, and does not have to recover the boundaries by matching on the
    /// text of an `and` keyword.
    let renderConstraints (gps: FSharpGenericParameter seq) : TextNode list =
        let tickOf (gp: FSharpGenericParameter) =
            if isSrtp gp then
                TextNode.Text "^"
            else
                TextNode.Tick

        gps
        |> Seq.collect (fun gp -> gp.Constraints |> Seq.map (fun c -> gp, c))
        |> Seq.map (fun (gp, c) ->
            TextNode.Node
                [
                    tickOf gp
                    TextNode.Text gp.DisplayName
                    TextNode.Space
                    yield! renderConstraint gp c
                ]
        )
        |> Seq.toList

    /// The generic-parameter list for a function or member, e.g.
    /// `<'T when 'T : comparison>` or `<^T when ^T : (static member (+) : ^T * ^T -> ^T)>`.
    /// `None` when there are no parameters.
    let renderGenericParams (gps: FSharpGenericParameter seq) : TextNode option =
        let gps = gps |> Seq.toList

        if List.isEmpty gps then
            None
        else
            let tickOf (gp: FSharpGenericParameter) =
                if isSrtp gp then
                    TextNode.Text "^"
                else
                    TextNode.Tick

            let nodes =
                [
                    openAngle
                    for i, gp in List.indexed gps do
                        if i > 0 then
                            comma
                            TextNode.Space

                        tickOf gp
                        TextNode.Text gp.DisplayName

                        let constraints =
                            gp.Constraints |> Seq.toList |> List.map (renderConstraint gp)

                        if not (List.isEmpty constraints) then
                            TextNode.Space
                            TextNode.Keyword Keyword.When
                            TextNode.Space
                            tickOf gp
                            TextNode.Text gp.DisplayName

                            for i, cList in List.indexed constraints do
                                TextNode.Space

                                if i > 0 then
                                    TextNode.Keyword Keyword.And
                                    TextNode.Space
                                    tickOf gp
                                    TextNode.Text gp.DisplayName
                                    TextNode.Space

                                yield! cList
                    closeAngle
                ]

            Some(TextNode.Node nodes)
