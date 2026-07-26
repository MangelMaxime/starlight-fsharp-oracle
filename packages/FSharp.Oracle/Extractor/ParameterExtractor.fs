namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open SignatureRendering

module internal ParameterExtractor =
    let extractParameter (param: FSharpParameter) : Parameter =
        {
            Name = param.DisplayName
            Type = renderFSharpType false param.Type
        }

    let curriedParams (mfv: FSharpMemberOrFunctionOrValue) =
        mfv.CurriedParameterGroups
        |> Seq.map (fun group -> group |> Seq.map extractParameter |> Seq.toList)
        |> Seq.toList
        // Drop unit-only groups for properties only - they are FCS artifacts on
        // no-arg getters (e.g. `member _.Zero`). For real functions and methods
        // (e.g. `let timestamp ()`) the unit group is explicit and must be kept.
        |> List.filter (fun group ->
            not (
                mfv.IsProperty
                && group.Length = 1
                && (
                    match group.[0].Type with
                    | TextNode.TypeRef(name, _) -> name = "unit"
                    | TextNode.Text "unit" -> true
                    | _ -> false
                )
            )
        )
