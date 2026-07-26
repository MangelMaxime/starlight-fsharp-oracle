namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open EntityExtractor
open ValueExtractor

module internal ModuleExtractor =
    let extractModule (docs: Map<string, string>) (entity: FSharpEntity) : Module =
        let nested = entity.NestedEntities |> Seq.toList

        let functions, values =
            entity.MembersFunctionsAndValues
            |> Seq.filter (fun m -> not m.IsCompilerGenerated)
            |> Seq.toList
            // A binding with parameter groups is a function; anything else is a value,
            // including `let f = fun x -> x`, which has a function type but no named
            // parameters to tabulate.
            |> List.partition (fun m -> not (Seq.isEmpty m.CurriedParameterGroups))

        let obsoleteInfo = obsoleteOfEntity entity

        let entities =
            nested
            |> List.filter (fun e -> not e.IsFSharpModule)
            |> List.map (extractEntity docs)

        let funcs = functions |> List.map (extractFunction docs)
        let vals = values |> List.map (extractValue docs)

        {
            Name = entity.DisplayName
            FullName = entity.FullName
            Namespace = namespaceOf entity.FullName
            XmlDoc = moduleDocOf docs entity.XmlDocSig
            Entities = entities
            Functions = funcs
            Values = vals
            IsSynthetic = false
            ObsoleteInfo = obsoleteInfo
        }
