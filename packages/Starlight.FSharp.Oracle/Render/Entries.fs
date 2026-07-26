namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open TextNode.Extensions
open FSharp.Oracle.Schema
open Primitives
open Documentation

module Entries =

    let renderUnionCaseEntry (links: LinkResolver) (sb: StringBuilder) (case: UnionCase) =
        let renderFields () =
            let documentedFields = case.Fields |> List.filter (fun f -> f.XmlDoc.Summary.IsSome)

            if not documentedFields.IsEmpty then
                sb.WriteLine("<strong>Fields</strong>")

                for f in documentedFields do
                    let fieldDeclaration =
                        TextNode.ToHtml(
                            links,
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
                        )

                    sb.WriteLine fieldDeclaration

                    sb.WriteLine "<div class='fs-parameter__documentation'>"
                    sb.NewLine()
                    sb.WriteLine(escapeMdxMarkdown f.XmlDoc.Summary.Value)
                    sb.NewLine()
                    sb.WriteLine "</div>"

        let renderDocumentation () =
            renderDocumentationBlock sb case.XmlDoc renderFields

        renderDocEntry
            sb
            case.Name
            (case.Declaration.ToHtml(links))
            ObsoleteInfo.Active
            renderDocumentation

    let renderRecordField (links: LinkResolver) (sb: StringBuilder) (field: Field) =
        let renderDocumentation () =
            renderDocumentationBlock sb field.XmlDoc ignore

        renderDocEntry
            sb
            field.Name
            (field.Declaration.ToHtml(links))
            ObsoleteInfo.Active
            renderDocumentation

    let renderMemberEntry (links: LinkResolver) (sb: StringBuilder) (m: Member) =
        renderDocEntry
            sb
            m.Name
            (m.Declaration.ToHtml(links))
            m.ObsoleteInfo
            (fun () -> renderXmlDocBody links sb m.Parameters m.XmlDoc)

    let renderMemberSections
        (links: LinkResolver)
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (members: Member list)
        =
        let section (title: string) (slug: string) (items: Member list) =
            if not items.IsEmpty then
                h2 sb toc slug title
                sb.WriteLine("<div class=\"collapsible-group\">")

                for m in items do
                    tocH3 toc m.Name m.Name
                    renderMemberEntry links sb m

                sb.WriteLine("</div>")
                sb.NewLine()

        let ofKind kind = members |> List.filter (fun m -> m.Kind = kind)

        section "Constructors" "constructors" (ofKind MemberKind.Constructor)
        section "Properties" "properties" (ofKind MemberKind.Property)
        section "Methods" "methods" (ofKind MemberKind.Method)
        section "Operators" "operators" (ofKind MemberKind.Operator)

    let functionSignatureHtml (links: LinkResolver) (f: Function) =
        [
            f.AlignedDeclaration
            for p in f.Parameters |> List.collect id do
                p.AlignedDeclaration
            f.ReturnType
        ]
        |> List.map (fun n -> n.ToHtml(links))
        |> String.concat ""

    let renderFunctionEntry (links: LinkResolver) (sb: StringBuilder) (f: Function) =
        renderDocEntry
            sb
            f.Name
            (functionSignatureHtml links f)
            f.ObsoleteInfo
            (fun () -> renderXmlDocBody links sb f.Parameters f.XmlDoc)

    let renderValueEntry (links: LinkResolver) (sb: StringBuilder) (v: Value) =
        let renderDocumentation () =
            renderDocumentationBlock sb v.XmlDoc ignore

        renderDocEntry
            sb
            v.Name
            (v.Declaration.ToHtml(links))
            v.ObsoleteInfo
            renderDocumentation
