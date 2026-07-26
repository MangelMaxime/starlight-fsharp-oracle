namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open SignatureRendering
open ParameterExtractor

module internal MemberExtractor =
    let extractMember (docs: Map<string, string>) (mfv: FSharpMemberOrFunctionOrValue) : Member =
        // Constructors are all named ".ctor" by FCS; use "new" as a canonical display
        // name. Multiple-constructor disambiguation happens in extractEntity.
        let name =
            if mfv.IsConstructor then
                "new"
            else
                mfv.DisplayName

        {
            Kind = memberKindOf mfv
            Name = name
            FullName = mfv.FullName
            CompiledName = mfv.CompiledName
            Parameters = curriedParams mfv
            ReturnType = renderFSharpType false mfv.ReturnParameter.Type
            Constraints = renderConstraints mfv.GenericParameters
            XmlDoc = xmlDocOf docs mfv.XmlDocSig
            IsStatic = mfv.IsModuleValueOrMember && not mfv.IsInstanceMember
            IsAbstract = mfv.IsDispatchSlot
            IsInline = isInlineAnnotated mfv
            HasGetter = mfv.HasGetterMethod
            HasSetter = mfv.HasSetterMethod
            ObsoleteInfo = obsoleteOf mfv
        }
