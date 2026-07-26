namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open SignatureRendering

module internal ParameterExtractor =
    /// True for the `()` of a no-argument member. Asked of the FCS type rather than of
    /// the rendered token: recovering meaning by matching rendered output breaks the
    /// moment the rendering changes.
    let private isUnit (typ: FSharpType) =
        typ.HasTypeDefinition && typ.TypeDefinition.DisplayName = "unit"

    let extractParameter (param: FSharpParameter) : Parameter =
        // An optional parameter's type is reported as `int option`, but F# source
        // writes `?x: int` - the option is what the `?` means.
        let declaredType =
            if param.IsOptionalArg && param.Type.HasTypeDefinition && param.Type.GenericArguments.Count = 1 then
                param.Type.GenericArguments.[0]
            else
                param.Type

        {
            Name = param.DisplayName
            Type = renderFSharpType false declaredType
            IsOptional = param.IsOptionalArg
            IsUnit = isUnit param.Type
        }

    let curriedParams (mfv: FSharpMemberOrFunctionOrValue) =
        mfv.CurriedParameterGroups
        |> Seq.map (fun group -> group |> Seq.map extractParameter |> Seq.toList)
        |> Seq.toList
        // Drop unit-only groups for properties only - they are FCS artifacts on
        // no-arg getters (e.g. `member _.Zero`). For real functions and methods
        // (e.g. `let timestamp ()`) the unit group is explicit and must be kept.
        |> List.filter (fun group ->
            not (mfv.IsProperty && group.Length = 1 && group.[0].IsUnit)
        )
