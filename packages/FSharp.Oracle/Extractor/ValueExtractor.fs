namespace FSharp.Oracle

open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open Helpers
open SignatureRendering
open ParameterExtractor

module internal ValueExtractor =

    let extractFunction (docs: Map<string, string>) (mfv: FSharpMemberOrFunctionOrValue) : Function =
        {
            Name = mfv.DisplayName
            FullName = mfv.FullName
            Parameters = curriedParams mfv
            ReturnType = renderFSharpType false mfv.ReturnParameter.Type
            GenericParameters = renderGenericParams mfv.GenericParameters
            IsInline = isInlineAnnotated mfv
            IsMutable = mfv.IsMutable
            IsActivePattern = mfv.IsActivePattern
            XmlDoc = xmlDocOf docs mfv.XmlDocSig
            ObsoleteInfo = obsoleteOf mfv
        }

    let extractValue (docs: Map<string, string>) (mfv: FSharpMemberOrFunctionOrValue) : Value =
        {
            Name = mfv.DisplayName
            FullName = mfv.FullName
            Type = renderFSharpType true mfv.FullType
            LiteralValue = mfv.LiteralValue |> Option.map literalText
            GenericParameters = renderGenericParams mfv.GenericParameters
            IsInline = isInlineAnnotated mfv
            IsMutable = mfv.IsMutable
            XmlDoc = xmlDocOf docs mfv.XmlDocSig
            ObsoleteInfo = obsoleteOf mfv
        }
