namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open EntityExtractor
open MemberExtractor
open ValueExtractor

module internal ModuleExtractor =
    let extractModule (docs: Map<string, string>) (entity: FSharpEntity) : Module =
        let nested = entity.NestedEntities |> Seq.toList

        // Members this module adds to a type declared elsewhere belong with that type,
        // not among the module's own functions.
        let extensionMembers, ownBindings =
            entity.MembersFunctionsAndValues
            |> Seq.filter (fun m -> not m.IsCompilerGenerated)
            |> Seq.toList
            |> List.partition (fun m -> m.IsExtensionMember)

        let functions, values =
            ownBindings
            // A binding with parameter groups is a function; anything else is a value,
            // including `let f = fun x -> x`, which has a function type but no named
            // parameters to tabulate.
            |> List.partition (fun m -> not (Seq.isEmpty m.CurriedParameterGroups))

        let obsoleteInfo = obsoleteOfEntity entity

        let entities =
            nested
            |> List.filter (fun e -> not e.IsFSharpModule)
            |> List.choose (fun e ->
                tryExtract $"type {safeFullName e}" (fun () -> extractEntity docs e)
            )

        let funcs =
            functions
            |> List.choose (fun f ->
                tryExtract $"function {f.FullName}" (fun () -> extractFunction docs f)
            )

        let vals =
            values
            |> List.choose (fun v ->
                tryExtract $"value {v.FullName}" (fun () -> extractValue docs v)
            )

        let extensions =
            extensionMembers
            |> List.choose (fun m ->
                m.ApparentEnclosingEntity
                |> Option.map (fun extended ->
                    {
                        ExtendedType = safeFullName extended
                        ExtendedTypeName = extended.DisplayName
                        Member = extractMember docs m
                    }
                )
            )

        {
            Name = entity.DisplayName
            FullName = entity.FullName
            Namespace = namespaceOf entity.FullName
            XmlDoc = moduleDocOf docs entity.XmlDocSig
            Entities = entities
            Functions = funcs
            Values = vals
            ExtensionMembers = extensions
            IsSynthetic = false
            ObsoleteInfo = obsoleteInfo
        }
