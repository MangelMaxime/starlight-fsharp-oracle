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
                    (Declarations.fieldDeclaration false f).ToHtml(links)
                    |> signatureBlock
                    |> sb.WriteLine

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
            ((Declarations.unionCaseDeclaration case).ToHtml(links))
            ObsoleteInfo.Active
            renderDocumentation

    /// Enum cases read `Name = 3` rather than `name : type`, but are otherwise
    /// rendered like record fields.
    let renderField (links: LinkResolver) (isEnumCase: bool) (sb: StringBuilder) (field: Field) =
        let renderDocumentation () =
            renderDocumentationBlock sb field.XmlDoc ignore

        renderDocEntry
            sb
            field.Name
            ((Declarations.fieldDeclaration isEnumCase field).ToHtml(links))
            ObsoleteInfo.Active
            renderDocumentation

    let renderMemberEntry (links: LinkResolver) (sb: StringBuilder) (m: Member) =
        renderDocEntry
            sb
            m.Name
            ((Declarations.memberDeclaration m).ToHtml(links))
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

    let renderFunctionEntry (links: LinkResolver) (sb: StringBuilder) (f: Function) =
        renderDocEntry
            sb
            f.Name
            ((Declarations.functionSignature f).ToHtml(links))
            f.ObsoleteInfo
            (fun () -> renderXmlDocBody links sb f.Parameters f.XmlDoc)

    let renderValueEntry (links: LinkResolver) (sb: StringBuilder) (v: Value) =
        let renderDocumentation () =
            renderDocumentationBlock sb v.XmlDoc ignore

        renderDocEntry
            sb
            v.Name
            ((Declarations.valueDeclaration v).ToHtml(links))
            v.ObsoleteInfo
            renderDocumentation
