namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open SignatureRendering
open MemberExtractor

module internal EntityExtractor =

    let private genericParametersOf (entity: FSharpEntity) =
        renderGenericParams entity.GenericParameters

    let private extractField (docs: Map<string, string>) (field: FSharpField) : Field =
        {
            Name = field.Name
            Type = renderFSharpType false field.FieldType
            LiteralValue = field.LiteralValue |> Option.map literalText
            XmlDoc = xmlDocOf docs field.XmlDocSig
        }

    let private extractUnionCase (docs: Map<string, string>) (uc: FSharpUnionCase) : UnionCase =
        {
            Name = uc.Name
            FullName = uc.FullName
            Fields = uc.Fields |> Seq.map (extractField docs) |> Seq.toList
            XmlDoc = xmlDocOf docs uc.XmlDocSig
        }

    /// Members of an entity, filtered and deterministically ordered.
    /// Anchors (including disambiguating overloads) are the renderer's job.
    let private extractMembers (docs: Map<string, string>) (entity: FSharpEntity) =
        // A `[<CLIEvent>]` member also surfaces as add_X/remove_X methods. Documenting
        // those alongside the event itself triples it. Matched on the event's display
        // name: its logical name is `get_Tick`, while the accessors are `add_Tick` and
        // `remove_Tick`.
        let eventNames =
            entity.MembersFunctionsAndValues
            |> Seq.filter isEventProperty
            |> Seq.map (fun m -> m.DisplayName)
            |> Set.ofSeq

        let isEventAccessor (m: FSharpMemberOrFunctionOrValue) =
            m.IsEventAddMethod
            || m.IsEventRemoveMethod
            || [ "add_"; "remove_" ]
               |> List.exists (fun prefix ->
                   m.LogicalName.StartsWith prefix
                   && Set.contains (m.LogicalName.Substring prefix.Length) eventNames
               )

        entity.MembersFunctionsAndValues
        |> Seq.filter (fun m ->
            not m.IsCompilerGenerated
            && not (entity.IsFSharpUnion && m.IsUnionCaseTester)
            && not m.IsPropertyGetterMethod
            && not m.IsPropertySetterMethod
            && not (isEventAccessor m)
        )
        // Sort the symbols, not the extracted members: the key needs XmlDocSig
        // to separate overloads deterministically.
        |> Seq.sortBy memberSortKey
        |> Seq.map (extractMember docs)
        |> Seq.toList

    /// The parts every entity kind carries, computed once and threaded into whichever
    /// branch claims the entity. Keeps each `extract*` below to what is specific to it.
    type private Common =
        {
            Name: string
            FullName: string
            XmlDoc: XmlDoc
            Attributes: string list
            GenericParameters: TextNode option
            ObsoleteInfo: ObsoleteInfo
            IsStruct: bool
            /// What a caller may pass this type as. Interfaces F# derives for records
            /// and unions are excluded: the author did not write them, and they crowd
            /// out the ones who did.
            Interfaces: TextNode list
        }

    let private commonOf (docs: Map<string, string>) (entity: FSharpEntity) =
        {
            Name = entity.DisplayName
            FullName = safeFullName entity
            XmlDoc = xmlDocOf docs entity.XmlDocSig
            Attributes = attributesOf entity.Attributes
            GenericParameters = renderGenericParams entity.GenericParameters
            ObsoleteInfo = obsoleteOfEntity entity
            IsStruct = isStruct entity
            Interfaces =
                entity.DeclaredInterfaces
                |> Seq.filter (isDerivedInterface >> not)
                |> Seq.map (renderFSharpType false)
                |> Seq.toList
        }

    let private extractMeasure (c: Common) =
        Entity.Measure
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                ObsoleteInfo = c.ObsoleteInfo
            }

    let private extractException (docs: Map<string, string>) (entity: FSharpEntity) (c: Common) =
        Entity.Exception
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                Fields = entity.FSharpFields |> Seq.map (extractField docs) |> Seq.toList
                ObsoleteInfo = c.ObsoleteInfo
            }

    let private extractDelegate (entity: FSharpEntity) (c: Common) =
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
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                Parameters = parameters
                ReturnType = returnType
                ObsoleteInfo = c.ObsoleteInfo
            }

    let private extractUnion (docs: Map<string, string>) (entity: FSharpEntity) (c: Common) =
        Entity.Union
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                Interfaces = c.Interfaces
                Cases = entity.UnionCases |> Seq.map (extractUnionCase docs) |> Seq.toList
                Members = extractMembers docs entity
                ObsoleteInfo = c.ObsoleteInfo
                IsStruct = c.IsStruct
            }

    let private extractRecord (docs: Map<string, string>) (entity: FSharpEntity) (c: Common) =
        Entity.Record
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                Interfaces = c.Interfaces
                Fields = entity.FSharpFields |> Seq.map (extractField docs) |> Seq.toList
                Members = extractMembers docs entity
                ObsoleteInfo = c.ObsoleteInfo
                IsStruct = c.IsStruct
            }

    let private extractEnum (docs: Map<string, string>) (entity: FSharpEntity) (c: Common) =
        Entity.Enum
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                // Enums carry a synthetic `value__` instance field, the underlying
                // storage. Keep only the named literals, which are the actual cases.
                Fields =
                    entity.FSharpFields
                    |> Seq.filter (fun f -> f.Name <> "value__")
                    |> Seq.map (extractField docs)
                    |> Seq.toList
                ObsoleteInfo = c.ObsoleteInfo
                IsStruct = c.IsStruct
            }

    let private extractAbbreviation (entity: FSharpEntity) (c: Common) =
        Entity.Abbrev
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                AbbreviatedType = renderFSharpType false entity.AbbreviatedType
                ObsoleteInfo = c.ObsoleteInfo
                IsStruct = c.IsStruct
            }

    let private extractInterface (docs: Map<string, string>) (entity: FSharpEntity) (c: Common) =
        Entity.Interface
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                Interfaces = c.Interfaces
                Members = extractMembers docs entity
                ObsoleteInfo = c.ObsoleteInfo
                IsStruct = c.IsStruct
            }

    let private extractClass (docs: Map<string, string>) (entity: FSharpEntity) (c: Common) =
        Entity.Class
            {
                Name = c.Name
                FullName = c.FullName
                XmlDoc = c.XmlDoc
                Attributes = c.Attributes
                GenericParameters = c.GenericParameters
                BaseType =
                    entity.BaseType
                    |> Option.filter (isTrivialBaseType >> not)
                    |> Option.map (renderFSharpType false)
                Interfaces = c.Interfaces
                Members = extractMembers docs entity
                ObsoleteInfo = c.ObsoleteInfo
                IsStruct = c.IsStruct
            }

    /// Which kind of entity this is. Order matters: a measure is also an abbreviation,
    /// and an exception is also a class, so the more specific tests come first.
    let extractEntity (docs: Map<string, string>) (entity: FSharpEntity) : Entity =
        let common = commonOf docs entity

        if isMeasure entity then
            extractMeasure common
        elif entity.IsFSharpExceptionDeclaration then
            extractException docs entity common
        elif entity.IsDelegate then
            extractDelegate entity common
        elif entity.IsFSharpUnion then
            extractUnion docs entity common
        elif entity.IsFSharpRecord then
            extractRecord docs entity common
        elif entity.IsEnum then
            extractEnum docs entity common
        elif entity.IsFSharpAbbreviation then
            extractAbbreviation entity common
        elif entity.IsInterface then
            extractInterface docs entity common
        else
            extractClass docs entity common
