namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open SignatureRendering
open MemberExtractor

module internal EntityExtractor =

    let private genericParameterNames (entity: FSharpEntity) =
        entity.GenericParameters |> Seq.map (fun gp -> gp.DisplayName) |> Seq.toList

    let extractField (docs: Map<string, string>) (field: FSharpField) : Field =
        {
            Name = field.Name
            Type = renderFSharpType false field.FieldType
            LiteralValue = field.LiteralValue |> Option.map string
            XmlDoc = xmlDocOf docs field.XmlDocSig
        }

    let extractUnionCase (docs: Map<string, string>) (uc: FSharpUnionCase) : UnionCase =
        {
            Name = uc.Name
            FullName = uc.FullName
            Fields = uc.Fields |> Seq.map (extractField docs) |> Seq.toList
            XmlDoc = xmlDocOf docs uc.XmlDocSig
        }

    /// Members of an entity, filtered, deterministically ordered, and with overloaded
    /// constructors given distinct anchor names.
    let private extractMembers (docs: Map<string, string>) (entity: FSharpEntity) =
        let members =
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
            |> Seq.map (extractMember docs)
            |> Seq.toList

        // When there are multiple constructors they all start as Name = "new".
        // Assign unique anchor ids: first stays "new", subsequent get "new-1", "new-2", ...
        let constructorCount =
            members |> List.filter (fun m -> m.Kind = MemberKind.Constructor) |> List.length

        if constructorCount <= 1 then
            members
        else
            members
            |> List.mapFold
                (fun index m ->
                    if m.Kind = MemberKind.Constructor then
                        let name =
                            if index = 0 then
                                "new"
                            else
                                $"new-{index}"

                        { m with Name = name }, index + 1
                    else
                        m, index
                )
                0
            |> fst

    let extractEntity (docs: Map<string, string>) (entity: FSharpEntity) : Entity =
        let name = entity.DisplayName
        let fullName = safeFullName entity
        let xmlDoc = xmlDocOf docs entity.XmlDocSig
        let obsoleteInfo = obsoleteOfEntity entity
        let generics = genericParameterNames entity
        let isStruct = isStruct entity

        if isMeasure entity then
            Entity.Measure
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    ObsoleteInfo = obsoleteInfo
                }

        elif entity.IsFSharpExceptionDeclaration then
            Entity.Exception
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    Fields = entity.FSharpFields |> Seq.map (extractField docs) |> Seq.toList
                    ObsoleteInfo = obsoleteInfo
                }

        elif entity.IsDelegate then
            let invoke =
                entity.MembersFunctionsAndValues
                |> Seq.tryFind (fun m -> m.LogicalName = "Invoke")

            let parameters, returnType =
                match invoke with
                | Some m ->
                    let ps =
                        m.CurriedParameterGroups
                        |> Seq.collect (Seq.map (fun p -> renderFSharpType false p.Type))
                        |> Seq.toList

                    ps, renderFSharpType false m.ReturnParameter.Type
                | None -> [], TextNode.Text "unit"

            Entity.Delegate
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    Parameters = parameters
                    ReturnType = returnType
                    ObsoleteInfo = obsoleteInfo
                }

        elif entity.IsFSharpUnion then
            Entity.Union
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    Cases = entity.UnionCases |> Seq.map (extractUnionCase docs) |> Seq.toList
                    Members = extractMembers docs entity
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsFSharpRecord then
            Entity.Record
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    Fields = entity.FSharpFields |> Seq.map (extractField docs) |> Seq.toList
                    Members = extractMembers docs entity
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsEnum then
            // Enums carry a synthetic `value__` instance field (the underlying storage).
            // Keep only the named literal members, which are the actual cases.
            Entity.Enum
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    Fields =
                        entity.FSharpFields
                        |> Seq.filter (fun f -> f.Name <> "value__")
                        |> Seq.map (extractField docs)
                        |> Seq.toList
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsFSharpAbbreviation then
            Entity.Abbrev
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    AbbreviatedType = renderFSharpType false entity.AbbreviatedType
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        elif entity.IsInterface then
            Entity.Interface
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    Members = extractMembers docs entity
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }

        else
            Entity.Class
                {
                    Name = name
                    FullName = fullName
                    XmlDoc = xmlDoc
                    GenericParameters = generics
                    Members = extractMembers docs entity
                    ObsoleteInfo = obsoleteInfo
                    IsStruct = isStruct
                }
