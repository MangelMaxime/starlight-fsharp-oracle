namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open SignatureRendering
open ParameterExtractor

module internal ValueExtractor =

    /// `inline` is meaningless on an active pattern, and FCS reports them as inline.
    let private isInline (mfv: FSharpMemberOrFunctionOrValue) =
        (mfv.InlineAnnotation = FSharpInlineAnnotation.AlwaysInline
         || mfv.InlineAnnotation = FSharpInlineAnnotation.AggressiveInline)
        && not mfv.IsActivePattern

    let extractFunction (docs: Map<string, string>) (mfv: FSharpMemberOrFunctionOrValue) : Function =
        {
            Name = mfv.DisplayName
            FullName = mfv.FullName
            Parameters = curriedParams mfv
            ReturnType = renderFSharpType false mfv.ReturnParameter.Type
            GenericParameters = renderGenericParams mfv.GenericParameters
            IsInline = isInline mfv
            IsMutable = mfv.IsMutable
            XmlDoc = xmlDocOf docs mfv.XmlDocSig
            ObsoleteInfo = obsoleteOf mfv
        }

    let extractValue (docs: Map<string, string>) (mfv: FSharpMemberOrFunctionOrValue) : Value =
        {
            Name = mfv.DisplayName
            FullName = mfv.FullName
            Type = renderFSharpType true mfv.FullType
            GenericParameters = renderGenericParams mfv.GenericParameters
            IsInline = isInline mfv
            IsMutable = mfv.IsMutable
            XmlDoc = xmlDocOf docs mfv.XmlDocSig
            ObsoleteInfo = obsoleteOf mfv
        }
