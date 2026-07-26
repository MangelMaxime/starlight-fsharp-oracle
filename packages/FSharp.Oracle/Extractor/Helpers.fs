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

    /// True for types F# conventionally writes postfix, e.g. `int list`, `string option`.
    let isPostfixType (td: FSharpEntity) =
        Set.contains td.DisplayName postfixTypeDisplayNames
        || match tryGetFullName td with
           | Some fullName -> Set.contains fullName postfixTypeNames
           | None -> false

    /// A `[<CLIEvent>]` member surfaces as a property returning `IEvent<_>` plus a
    /// pair of add_/remove_ methods. FCS does not flag any of them as an event here,
    /// so the return type is what identifies it.
    let isEventProperty (mfv: FSharpMemberOrFunctionOrValue) =
        mfv.IsProperty
        && (let returnType = mfv.ReturnParameter.Type

            returnType.HasTypeDefinition
            && (let td = returnType.TypeDefinition

                // `IEvent<'T>` is an abbreviation, and FullName throws for those, so
                // the display name has to be checked too - the same trap as `obj`.
                td.DisplayName = "IEvent"
                || td.DisplayName = "IDelegateEvent"
                || (match tryGetFullName td with
                    | Some fullName ->
                        fullName.StartsWith "Microsoft.FSharp.Control.IEvent"
                        || fullName.StartsWith "Microsoft.FSharp.Control.IDelegateEvent"
                    | None -> false)))

    let memberKindOf (mfv: FSharpMemberOrFunctionOrValue) =
        if mfv.IsConstructor then
            MemberKind.Constructor
        elif mfv.IsEvent || isEventProperty mfv then
            MemberKind.Event
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
            | MemberKind.Event, _ -> 5
            | MemberKind.Operator, _ -> 6

        rank, mfv.DisplayName, mfv.XmlDocSig

    /// `inline` is meaningless on an active pattern, and FCS reports them as inline.
    let isInlineAnnotated (mfv: FSharpMemberOrFunctionOrValue) =
        (mfv.InlineAnnotation = FSharpInlineAnnotation.AlwaysInline
         || mfv.InlineAnnotation = FSharpInlineAnnotation.AggressiveInline)
        && not mfv.IsActivePattern

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

    /// A literal as F# source would write it: strings quoted, chars ticked.
    /// `val Greeting : string = hello` reads as though the value were an identifier.
    let literalText (value: obj) =
        match value with
        | :? string as text -> "\"" + text + "\""
        | :? char as c -> "'" + string c + "'"
        | :? bool as b ->
            if b then
                "true"
            else
                "false"
        | value -> string value

    /// Attributes the compiler adds, or that the renderer already shows another way.
    /// Listing these would bury the ones the author actually wrote.
    let private hiddenAttributes =
        Set.ofList
            [
                "Microsoft.FSharp.Core.CompilationMappingAttribute"
                "Microsoft.FSharp.Core.CompilationRepresentationAttribute"
                "Microsoft.FSharp.Core.CompilationArgumentCountsAttribute"
                "Microsoft.FSharp.Core.CompiledNameAttribute"
                // Rendered as part of the declaration itself.
                "Microsoft.FSharp.Core.StructAttribute"
                "Microsoft.FSharp.Core.MeasureAttribute"
                // Rendered as a banner.
                "System.ObsoleteAttribute"
            ]

    /// `[<RequireQualifiedAccess>]`, `[<Literal>]`, and anything else the author wrote.
    let attributesOf (attributes: FSharpAttribute seq) =
        attributes
        |> Seq.filter (fun a ->
            let fullName =
                try
                    a.AttributeType.FullName
                with _ ->
                    ""

            not (Set.contains fullName hiddenAttributes)
            && not (fullName.StartsWith "System.Diagnostics.")
            && not (fullName.StartsWith "System.Runtime.CompilerServices.")
        )
        |> Seq.map (fun a ->
            // FCS reports the class name; F# source writes it without the suffix.
            let name =
                let displayName = a.AttributeType.DisplayName

                if displayName.EndsWith "Attribute" then
                    displayName.Substring(0, displayName.Length - "Attribute".Length)
                else
                    displayName

            let args =
                a.ConstructorArguments
                |> Seq.map (fun (_, value) ->
                    literalText value
                )
                |> String.concat ", "

            if args = "" then
                $"[<{name}>]"
            else
                $"[<{name}({args})>]"
        )
        |> Seq.toList

    /// Interfaces F# derives for records and unions on its own. Listing them as though
    /// the author wrote them is noise.
    let private derivedInterfaces =
        Set.ofList
            [
                "System.IEquatable`1"
                "System.IComparable"
                "System.IComparable`1"
                "System.Collections.IStructuralEquatable"
                "System.Collections.IStructuralComparable"
            ]

    let isDerivedInterface (typ: FSharpType) =
        typ.HasTypeDefinition
        && (match tryGetFullName typ.TypeDefinition with
            | Some fullName -> Set.contains fullName derivedInterfaces
            | None -> false)

    /// True when a base type carries no information, i.e. it is `obj`.
    /// Matched on the display name as well: `obj` is a type abbreviation, and
    /// `FullName` throws for those, so a full-name check alone lets it through.
    let isTrivialBaseType (typ: FSharpType) =
        typ.HasTypeDefinition
        && (let td = typ.TypeDefinition

            td.DisplayName = "obj"
            || td.DisplayName = "Object"
            || (match tryGetFullName td with
                | Some fullName -> fullName = "System.Object" || fullName = "obj"
                | None -> false))

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
