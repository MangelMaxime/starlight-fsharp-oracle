namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open TextNode.Extensions
open FSharp.Oracle.Schema
open Primitives
open Documentation

module Entries =

    let renderUnionCaseEntry (sb: StringBuilder) (case: UnionCase) =
        let renderFields () =
            let documentedFields = case.Fields |> List.filter (fun f -> f.XmlDoc.Summary.IsSome)

            if not documentedFields.IsEmpty then
                sb.WriteLine("<strong>Fields</strong>")

                for f in documentedFields do
                    let fieldDeclaration =
                        [
                            TextNode.OpenTagWithClass("div", "fs-parameter__signature")
                            TextNode.NewLine
                            TextNode.Text f.Name
                            TextNode.Space
                            TextNode.Colon
                            TextNode.Space
                            f.Type
                            TextNode.NewLine
                            TextNode.CloseTag "div"
                            TextNode.NewLine
                        ]
                        |> TextNode.ToHtml

                    sb.WriteLine fieldDeclaration

                    sb.WriteLine "<div class='fs-parameter__documentation'>"
                    sb.NewLine()
                    sb.WriteLine(escapeMdxMarkdown f.XmlDoc.Summary.Value)
                    sb.NewLine()
                    sb.WriteLine "</div>"

        let renderDocumentation () =
            renderDocumentationBlock sb case.XmlDoc renderFields

        renderDocEntry sb case.Name case.Declaration.Html ObsoleteInfo.Active renderDocumentation

    let renderRecordField (sb: StringBuilder) (field: Field) =
        let renderDocumentation () =
            renderDocumentationBlock sb field.XmlDoc ignore

        renderDocEntry sb field.Name field.Declaration.Html ObsoleteInfo.Active renderDocumentation

    let renderMemberEntry (sb: StringBuilder) (m: Member) =
        renderDocEntry
            sb
            m.Name
            m.Declaration.Html
            m.ObsoleteInfo
            (fun () -> renderXmlDocBody sb m.Parameters m.XmlDoc)

    let renderMemberSections
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (members: Member list)
        =
        let constructors = members |> List.filter (fun m -> m.Kind = MemberKind.Constructor)
        let properties = members |> List.filter (fun m -> m.Kind = MemberKind.Property)
        let methods = members |> List.filter (fun m -> m.Kind = MemberKind.Method)
        let operators = members |> List.filter (fun m -> m.Kind = MemberKind.Operator)

        if not constructors.IsEmpty then
            h2 sb toc "constructors" "Constructors"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for m in constructors do
                tocH3 toc m.Name m.Name
                renderMemberEntry sb m

            sb.WriteLine("</div>")
            sb.NewLine()

        if not properties.IsEmpty then
            h2 sb toc "properties" "Properties"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for m in properties do
                tocH3 toc m.Name m.Name
                renderMemberEntry sb m

            sb.WriteLine("</div>")
            sb.NewLine()

        if not methods.IsEmpty then
            h2 sb toc "methods" "Methods"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for m in methods do
                tocH3 toc m.Name m.Name
                renderMemberEntry sb m

            sb.WriteLine("</div>")
            sb.NewLine()

        if not operators.IsEmpty then
            h2 sb toc "operators" "Operators"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for m in operators do
                tocH3 toc m.Name m.Name
                renderMemberEntry sb m

            sb.WriteLine("</div>")
            sb.NewLine()

    let functionSignatureHtml (f: Function) =
        [
            f.AlignedDeclaration
            for p in f.Parameters |> List.collect id do
                p.AlignedDeclaration
            f.ReturnType
        ]
        |> List.map (fun n -> n.Html)
        |> String.concat ""

    let renderFunctionEntry (sb: StringBuilder) (f: Function) =
        renderDocEntry
            sb
            f.Name
            (functionSignatureHtml f)
            f.ObsoleteInfo
            (fun () -> renderXmlDocBody sb f.Parameters f.XmlDoc)

    let renderValueEntry (sb: StringBuilder) (v: Value) =
        let renderDocumentation () =
            renderDocumentationBlock sb v.XmlDoc ignore

        renderDocEntry sb v.Name v.Declaration.Html v.ObsoleteInfo renderDocumentation
