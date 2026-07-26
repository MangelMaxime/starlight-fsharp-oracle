namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open SignatureRendering
open MemberExtractor

module internal EntityExtractor =
    let caseFieldNodes (fields: Field list) =
        fields
        |> List.mapi (fun i f ->
            let separator =
                if i = 0 then
                    []
                else
                    [
                        TextNode.Space
                        TextNode.Star
                        TextNode.Space
                    ]

            let nameNodes =
                if f.Name = "" then
                    []
                else
                    [
                        TextNode.Text f.Name
                        TextNode.Space
                        TextNode.Colon
                        TextNode.Space
                    ]

            separator @ nameNodes @ [ f.Type ]
        )
        |> List.concat

    let memberDeclarationLine (m: Member) : TextNode list =
        let prefixNodes =
            match m.Kind with
            | MemberKind.Constructor -> [ TextNode.AnchoredKeyword("new", $"#{m.Name}") ]
            | MemberKind.Operator ->
                [
                    TextNode.Keyword "static"
                    TextNode.Space
                    TextNode.Keyword "member"
                    TextNode.Space
                ]
            | MemberKind.Property ->
                if m.IsStatic then
                    [
                        TextNode.Keyword "static"
                        TextNode.Space
                        TextNode.Keyword "property"
                        TextNode.Space
                    ]
                elif m.IsAbstract then
                    [
                        TextNode.Keyword "abstract"
                        TextNode.Space
                        TextNode.Keyword "property"
                        TextNode.Space
                    ]
                else
                    [
                        TextNode.Keyword "property"
                        TextNode.Space
                    ]
            | MemberKind.Method ->
                if m.IsStatic then
                    [
                        TextNode.Keyword "static"
                        TextNode.Space
                        TextNode.Keyword "member"
                        TextNode.Space
                    ]
                elif m.IsAbstract then
                    [
                        TextNode.Keyword "abstract"
                        TextNode.Space
                        TextNode.Keyword "member"
                        TextNode.Space
                    ]
                else
                    [
                        TextNode.Keyword "member"
                        TextNode.Space
                    ]

        let allParams = m.Parameters |> List.collect id

        let typeNodes =
            if allParams.IsEmpty then
                [ m.ReturnType ]
            else
                [
                    for i, param in List.indexed allParams do
                        if i > 0 then
                            yield TextNode.Space
                            yield TextNode.Arrow
                            yield TextNode.Space

                        yield param.Declaration
                    yield TextNode.Space
                    yield TextNode.Arrow
                    yield TextNode.Space
                    yield m.ReturnType
                ]

        let withGetNodes =
            if m.Kind = MemberKind.Property then
                [
                    TextNode.Space
                    TextNode.Keyword "with"
                    TextNode.Space
                    TextNode.Keyword "get"
                ]
            else
                []

        [
            yield TextNode.NewLine
            yield TextNode.Spaces 4
            yield! prefixNodes
            if m.Kind <> MemberKind.Constructor then
                yield TextNode.AnchoredProperty(m.Name, $"#{m.Name}")
            yield TextNode.Space
            yield TextNode.Colon
            yield TextNode.Space
            yield! typeNodes
            yield! withGetNodes
        ]

    let typeHeadNodes (entity: FSharpEntity) =
        let name = entity.DisplayName

        let genericParams =
            if entity.GenericParameters.Count = 0 then
                []
            else
                [
                    TextNode.LessThan
                    for i in 0 .. entity.GenericParameters.Count - 1 do
                        let gp = entity.GenericParameters.[i]

                        if i > 0 then
                            TextNode.Comma
                            TextNode.Space

                        TextNode.Tick
                        TextNode.Text gp.DisplayName
                    TextNode.GreaterThan
                ]

        [
            TextNode.Keyword "type"
            TextNode.Space
            TextNode.Text name
            yield! genericParams
        ]

    let extractField
        (toUrl: string -> string)
        (docs: Map<string, string>)
        (field: FSharpField)
        : Field
        =
        let typeNode = renderFSharpType toUrl false field.FieldType

        {
            Name = field.Name
            Type = typeNode
            Declaration =
                TextNode.Node
                    [
                        TextNode.Text field.Name
                        TextNode.Space
                        TextNode.Colon
                        TextNode.Space
                        typeNode
                    ]
            XmlDoc = xmlDocOf docs field.XmlDocSig
        }

    let extractUnionCase
        (toUrl: string -> string)
        (docs: Map<string, string>)
        (uc: FSharpUnionCase)
        : UnionCase
        =
        let fields = uc.Fields |> Seq.map (extractField toUrl docs) |> Seq.toList

        let declaration =
            TextNode.Node
                [
                    TextNode.Keyword "|"
                    TextNode.Space
                    TextNode.Text uc.Name
                    if not fields.IsEmpty then
                        TextNode.Keyword " of"
                        TextNode.Space
                        yield! caseFieldNodes fields
                ]

        {
            Name = uc.Name
            FullName = uc.FullName
            Fields = fields
            XmlDoc = xmlDocOf docs uc.XmlDocSig
            Declaration = declaration
        }

    let extractEntity
        (toUrl: string -> string)
        (docs: Map<string, string>)
        (entity: FSharpEntity)
        : Entity
        =
        let name = entity.DisplayName
        let fullName = safeFullName entity
        let xmlDoc = xmlDocOf docs entity.XmlDocSig
        let obsoleteInfo = obsoleteOfEntity entity

        let members () =
            let ms =
                entity.MembersFunctionsAndValues
                |> Seq.filter (fun m ->
                    not m.IsCompilerGenerated
                    && not (entity.IsFSharpUnion && m.IsUnionCaseTester)
                    && not m.IsPropertyGetterMethod
                    && not m.IsPropertySetterMethod
                )
                // Sort the symbols, not the extracted members: the key needs XmlDocSig
                // to separate overloads deterministically.
                |> Seq.sortBy memberSortKey
                |> Seq.map (extractMember toUrl docs)
                |> Seq.toList

            // When there are multiple constructors they all start as Name = "new".
            // Assign unique anchor ids: first stays "new", subsequent get "new-1", "new-2", …
            let ctorCount =
                ms |> List.filter (fun m -> m.Kind = MemberKind.Constructor) |> List.length

            if ctorCount <= 1 then
                ms
            else
                ms
                |> List.mapFold
                    (fun ctorIdx m ->
                        if m.Kind = MemberKind.Constructor then
                            let name =
                                if ctorIdx = 0 then
                                    "new"
                                else
                                    $"new-{ctorIdx}"

                            { m with
                                Name = name
                            },
                            ctorIdx + 1
                        else
                            m, ctorIdx
                    )
                    0
                |> fst

        let isStruct = isStruct entity

        if isMeasure entity then
            let declaration =
                TextNode.Node
                    [
                        TextNode.OpenTagWithClass("span", "fsharp-doc-attr")
                        TextNode.Text "[<Measure>]"
                        TextNode.CloseTag "span"
                        TextNode.NewLine
                        yield! typeHeadNodes entity
                    ]

            Entity.Measure
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                }

        elif entity.IsFSharpExceptionDeclaration then
            let fields = entity.FSharpFields |> Seq.map (extractField toUrl docs) |> Seq.toList

            let fieldNodes =
                fields
                |> List.mapi (fun i f ->
                    let separator =
                        if i = 0 then
                            []
                        else
                            [ TextNode.Space; TextNode.Star; TextNode.Space ]

                    if f.Name = "" then
                        separator @ [ f.Type ]
                    else
                        separator
                        @ [
                            TextNode.Text f.Name
                            TextNode.Colon
                            TextNode.Space
                            f.Type
                        ]
                )
                |> List.concat

            let declaration =
                TextNode.Node
                    [
                        TextNode.Keyword "exception"
                        TextNode.Space
                        TextNode.Text name
                        if not fields.IsEmpty then
                            TextNode.Keyword " of"
                            TextNode.Space
                            yield! fieldNodes
                    ]

            Entity.Exception
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Fields = fields
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                }

        elif entity.IsDelegate then
            let invokeMethod =
                entity.MembersFunctionsAndValues
                |> Seq.tryFind (fun m -> m.LogicalName = "Invoke")

            let signature =
                match invokeMethod with
                | Some m ->
                    let parameters = m.CurriedParameterGroups |> Seq.toList

                    let paramNodes =
                        parameters
                        |> List.collect (fun group ->
                            group
                            |> Seq.map (fun p ->
                                renderFSharpType toUrl false p.Type
                            )
                            |> Seq.toList
                        )
                        |> List.mapi (fun i t ->
                            if i = 0 then
                                [ t ]
                            else
                                [ TextNode.Space; TextNode.Star; TextNode.Space; t ]
                        )
                        |> List.concat

                    let returnNode = renderFSharpType toUrl false m.ReturnParameter.Type

                    TextNode.Node
                        [
                            TextNode.Keyword "delegate"
                            TextNode.Space
                            TextNode.Keyword "of"
                            TextNode.Space
                            yield! paramNodes
                            TextNode.Space
                            TextNode.Arrow
                            TextNode.Space
                            returnNode
                        ]
                | None -> TextNode.Text "delegate"

            let declaration =
                TextNode.Node
                    [
                        yield! typeHeadNodes entity
                        TextNode.Space
                        TextNode.Equal
                        TextNode.Space
                        signature
                    ]

            Entity.Delegate
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Signature = signature
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                }

        elif entity.IsFSharpUnion then
            let cases = entity.UnionCases |> Seq.map (extractUnionCase toUrl docs) |> Seq.toList
            let ms = members ()

            let caseNodes =
                cases
                |> List.collect (fun case ->
                    [
                        TextNode.NewLine
                        TextNode.Spaces 4
                        TextNode.Keyword "|"
                        TextNode.Space
                        TextNode.Anchor(case.Name, $"#{case.Name}")
                        if not case.Fields.IsEmpty then
                            TextNode.Keyword " of"
                            TextNode.Space
                            yield! caseFieldNodes case.Fields
                    ]
                )

            let declaration =
                TextNode.Node
                    [
                        if isStruct then
                            TextNode.OpenTagWithClass("span", "fsharp-doc-attr")
                            TextNode.Text "[<Struct>]"
                            TextNode.CloseTag "span"
                            TextNode.NewLine
                        yield! typeHeadNodes entity
                        TextNode.Space
                        TextNode.Equal
                        yield! caseNodes
                        yield! (ms |> List.collect memberDeclarationLine)
                    ]

            Entity.Union
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Cases = cases
                    Members = ms
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsFSharpRecord then
            let fields = entity.FSharpFields |> Seq.map (extractField toUrl docs) |> Seq.toList
            let ms = members ()

            let fieldNodes =
                fields
                |> List.collect (fun f ->
                    [
                        TextNode.NewLine
                        TextNode.Spaces 8
                        TextNode.AnchoredProperty(f.Name, $"#{f.Name}")
                        TextNode.Space
                        TextNode.Colon
                        TextNode.Space
                        f.Type
                    ]
                )

            let declaration =
                TextNode.Node
                    [
                        if isStruct then
                            TextNode.OpenTagWithClass("span", "fsharp-doc-attr")
                            TextNode.Text "[<Struct>]"
                            TextNode.CloseTag "span"
                            TextNode.NewLine
                        yield! typeHeadNodes entity
                        TextNode.Space
                        TextNode.Equal
                        TextNode.NewLine
                        TextNode.Spaces 4
                        TextNode.LeftBrace
                        yield! fieldNodes
                        TextNode.NewLine
                        TextNode.Spaces 4
                        TextNode.RightBrace
                        yield! (ms |> List.collect memberDeclarationLine)
                    ]

            Entity.Record
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Fields = fields
                    Members = ms
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsEnum then
            // Enums carry a synthetic `value__` instance field (the underlying
            // storage). Keep only the named literal members (the actual cases).
            let enumValueNodes (f: FSharpField) =
                match f.LiteralValue with
                | Some value ->
                    [
                        TextNode.Space
                        TextNode.Equal
                        TextNode.Space
                        TextNode.Text(string value)
                    ]
                | None -> []

            let fields =
                entity.FSharpFields
                |> Seq.filter (fun f -> f.Name <> "value__")
                |> Seq.map (fun f ->
                    let baseField = extractField toUrl docs f

                    // Render each case as `Name = value` rather than the useless
                    // `Name : underlyingType` the generic field extractor produces.
                    // The case name is styled as a property (blue), like record fields.
                    { baseField with
                        Declaration =
                            TextNode.Node(
                                TextNode.AnchoredProperty(f.Name, $"#{f.Name}") :: enumValueNodes f
                            )
                    }
                )
                |> Seq.toList

            let caseNodes =
                Seq.zip (entity.FSharpFields |> Seq.filter (fun f -> f.Name <> "value__")) fields
                |> Seq.collect (fun (fcsField, f) ->
                    [
                        TextNode.NewLine
                        TextNode.Spaces 4
                        TextNode.Keyword "|"
                        TextNode.Space
                        TextNode.AnchoredProperty(f.Name, $"#{f.Name}")
                        yield! enumValueNodes fcsField
                    ]
                )
                |> Seq.toList

            let declaration =
                TextNode.Node
                    [
                        yield! typeHeadNodes entity
                        TextNode.Space
                        TextNode.Equal
                        yield! caseNodes
                    ]

            Entity.Enum
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Fields = fields
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsFSharpAbbreviation then
            let signature = renderFSharpType toUrl false entity.AbbreviatedType

            let declaration =
                TextNode.Node
                    [
                        yield! typeHeadNodes entity
                        TextNode.Space
                        TextNode.Equal
                        TextNode.Space
                        signature
                    ]

            Entity.Abbrev
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Signature = signature
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsInterface then
            let ms = members ()

            let declaration =
                TextNode.Node
                    [
                        yield! typeHeadNodes entity
                        yield! (ms |> List.collect memberDeclarationLine)
                    ]

            Entity.Interface
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Members = ms
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        else
            let ms = members ()

            let declaration =
                TextNode.Node
                    [
                        if isStruct then
                            TextNode.OpenTagWithClass("span", "fsharp-doc-attr")
                            TextNode.Text "[<Struct>]"
                            TextNode.CloseTag "span"
                            TextNode.NewLine
                        yield! typeHeadNodes entity
                        yield! (ms |> List.collect memberDeclarationLine)
                    ]

            Entity.Class
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Members = ms
                    Declaration = declaration
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }
