namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema

module internal Helpers =
    /// Returns everything before the last dot, e.g. "A.B.C" → "A.B".
    /// Returns "" for unqualified names.
    let namespaceOf (fullName: string) =
        let lastDot = fullName.LastIndexOf('.')

        if lastDot < 0 then
            ""
        else
            fullName.[.. lastDot - 1]

    /// Try to get FullName from a FSharpEntity; returns None for primitives
    /// like 'string', 'int' that are F# type abbreviations with no qualified name.
    let tryGetFullName (entity: FSharpEntity) =
        try
            Some entity.FullName
        with _ ->
            None

    /// Compute a safe FullName for any entity.
    /// Type abbreviations for primitives (e.g. `type Meters = float`) throw on FullName;
    /// fall back to constructing it from Namespace + DisplayName.
    let safeFullName (entity: FSharpEntity) =
        match tryGetFullName entity with
        | Some fullName -> fullName
        | None ->
            let ns = entity.Namespace |> Option.defaultValue ""

            if ns = "" then
                entity.DisplayName
            else
                $"{ns}.{entity.DisplayName}"

    /// True for SRTP parameters (have member/static-member constraints).
    let isSrtp (gp: FSharpGenericParameter) =
        gp.Constraints |> Seq.exists (fun c -> c.IsMemberConstraint)

    /// Display names for types that conventionally use postfix syntax in F#.
    let postfixTypeDisplayNames =
        Set.ofList
            [
                "list"
                "option"
                "seq"
                "voption"
                "ref"
            ]

    /// Full names for types that conventionally use postfix syntax in F#.
    let postfixTypeNames =
        Set.ofList
            [
                // list
                "Microsoft.FSharp.Collections.list"
                "Microsoft.FSharp.Collections.List"
                "Microsoft.FSharp.Collections.FSharpList"
                // option
                "Microsoft.FSharp.Core.option"
                "Microsoft.FSharp.Core.Option"
                "Microsoft.FSharp.Core.FSharpOption"
                // seq
                "Microsoft.FSharp.Collections.seq"
                "Microsoft.FSharp.Collections.Seq"
                "System.Collections.Generic.IEnumerable"
                "System.Collections.Generic.IEnumerable`1"
                // voption
                "Microsoft.FSharp.Core.voption"
                "Microsoft.FSharp.Core.ValueOption"
                "Microsoft.FSharp.Core.FSharpValueOption"
                // ref
                "Microsoft.FSharp.Core.ref"
                "Microsoft.FSharp.Core.Ref"
                "Microsoft.FSharp.Core.FSharpRef"
            ]

    let memberKindOf (mfv: FSharpMemberOrFunctionOrValue) =
        if mfv.IsConstructor then
            MemberKind.Constructor
        elif mfv.IsProperty then
            MemberKind.Property
        elif mfv.LogicalName.StartsWith("op_") && not mfv.IsActivePattern then
            MemberKind.Operator
        else
            MemberKind.Method

    /// Deterministic ordering key for entity members.
    /// FCS gives no stable enumeration order for `MembersFunctionsAndValues` - it varies
    /// from one process to the next - so the docs must impose one, or every rebuild
    /// churns the output. Groups by kind, then alphabetically, with the XML doc
    /// signature separating overloads (it encodes the parameter types).
    let memberSortKey (mfv: FSharpMemberOrFunctionOrValue) =
        let isStatic = mfv.IsModuleValueOrMember && not mfv.IsInstanceMember

        let rank =
            match memberKindOf mfv, isStatic with
            | MemberKind.Constructor, _ -> 0
            | MemberKind.Method, false -> 1
            | MemberKind.Property, false -> 2
            | MemberKind.Method, true -> 3
            | MemberKind.Property, true -> 4
            | MemberKind.Operator, _ -> 5

        rank, mfv.DisplayName, mfv.XmlDocSig

    let obsoleteOf (mfv: FSharpMemberOrFunctionOrValue) : ObsoleteInfo =
        mfv.Attributes
        |> Seq.tryFind (fun a -> a.AttributeType.FullName = "System.ObsoleteAttribute")
        |> function
            | None -> ObsoleteInfo.Active
            | Some a ->
                a.ConstructorArguments
                |> Seq.tryHead
                |> Option.map (snd >> string)
                |> function
                    | Some "" -> ObsoleteInfo.Deprecated
                    | Some msg -> ObsoleteInfo.DeprecatedWithMessage msg
                    | None -> ObsoleteInfo.Deprecated

    let hasAttribute (fullName: string) (entity: FSharpEntity) =
        entity.Attributes
        |> Seq.exists (fun a -> a.AttributeType.FullName = fullName)

    let isStruct (entity: FSharpEntity) =
        hasAttribute "Microsoft.FSharp.Core.StructAttribute" entity

    let isMeasure (entity: FSharpEntity) =
        hasAttribute "Microsoft.FSharp.Core.MeasureAttribute" entity

    let obsoleteOfEntity (entity: FSharpEntity) : ObsoleteInfo =
        entity.Attributes
        |> Seq.tryFind (fun a -> a.AttributeType.FullName = "System.ObsoleteAttribute")
        |> function
            | None -> ObsoleteInfo.Active
            | Some a ->
                a.ConstructorArguments
                |> Seq.tryHead
                |> Option.map (snd >> string)
                |> function
                    | Some "" -> ObsoleteInfo.Deprecated
                    | Some msg -> ObsoleteInfo.DeprecatedWithMessage msg
                    | None -> ObsoleteInfo.Deprecated

    /// Resolves XML-doc <see cref="..."/> strings to page URLs.
    /// cref format: T:Namespace.Type or M:Namespace.Type.Member(...)
    let resolveCref (toUrl: string -> string) (cref: string) : string option =
        if System.String.IsNullOrWhiteSpace cref then
            None
        else
            let stripped =
                if cref.Length > 2 && cref.[1] = ':' then
                    cref.[2..]
                else
                    cref

            let typeName =
                // For member refs, drop parameters and the last segment to get the type
                let withoutParams =
                    let paren = stripped.IndexOf('(')
                    if paren >= 0 then stripped.[..paren - 1] else stripped

                let lastDot = withoutParams.LastIndexOf('.')
                if lastDot > 0 then withoutParams.[..lastDot - 1] else withoutParams

            Some(toUrl typeName)
