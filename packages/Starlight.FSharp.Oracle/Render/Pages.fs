namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open TextNode.Extensions
open FSharp.Oracle.Schema
open Primitives
open Documentation
open Entries

module Pages =

    let renderDeclaredModules
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (htmlLinkGen: string -> string -> string)
        (modules: Module list)
        =
        if not modules.IsEmpty then
            h2 sb toc "declared-modules" "Declared Modules"
            sb.WriteLine("<dl>")

            for m in modules do
                sb.WriteLine(
                    $"<dt>{htmlLinkGen m.Name m.FullName}{obsoleteInlineHtml m.ObsoleteInfo}</dt>"
                )

                match m.XmlDoc with
                | Some doc -> sb.WriteLine($"<dd>{escapeMdxInline doc}</dd>")
                | None -> ()

            sb.WriteLine("</dl>")
            sb.NewLine()

    let renderEntityPage
        (htmlLinkGen: string -> string -> string)
        (entity: Entity)
        (parentModule: Module)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        sb.WriteLine(
            $"<p><strong>Namespace:</strong> {htmlLinkGen parentModule.Namespace parentModule.Namespace}"
            + $"&nbsp;&nbsp;<strong>Parent:</strong> {htmlLinkGen parentModule.FullName parentModule.FullName}</p>"
        )

        sb.NewLine()

        renderObsoleteBanner sb entity.ObsoleteInfo

        sb.WriteLine("<div class=\"not-content\">")
        sb.WriteLine("<div class=\"fsharp-doc-sig\">")
        sb.Write(entity.Declaration.Html)
        sb.NewLine()
        sb.WriteLine("</div>")
        sb.WriteLine("</div>")
        sb.NewLine()

        renderXmlDocSummaryAndRemarks sb toc entity.XmlDoc

        match entity with
        | Entity.Union e ->
            sb.WriteLine("<div class=\"collapsible-group\">")

            for c in e.Cases do
                tocH3 toc c.Name c.Name
                renderUnionCaseEntry sb c

            sb.WriteLine("</div>")
            sb.NewLine()
        | Entity.Record r ->
            if not r.Fields.IsEmpty then
                h2 sb toc "fields" "Fields"
                sb.WriteLine("<div class=\"collapsible-group\">")

                for field in r.Fields do
                    tocH3 toc field.Name field.Name
                    renderRecordField sb field

                sb.WriteLine("</div>")
                sb.NewLine()

            renderMemberSections sb toc r.Members
        | Entity.Class e -> renderMemberSections sb toc e.Members
        | Entity.Interface e -> renderMemberSections sb toc e.Members
        | Entity.Exception e ->
            if not e.Fields.IsEmpty then
                h2 sb toc "fields" "Fields"
                sb.WriteLine("<div class=\"collapsible-group\">")

                for field in e.Fields do
                    tocH3 toc field.Name field.Name
                    renderRecordField sb field

                sb.WriteLine("</div>")
                sb.NewLine()
        | _ -> ()

        {
            Title = entity.Name
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }

    let renderNamespacePage
        (htmlLinkGen: string -> string -> string)
        (ns: string)
        (subNamespaces: string list)
        (entities: Entity list)
        (modules: Module list)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        if not subNamespaces.IsEmpty then
            h2 sb toc "namespaces" "Namespaces"
            sb.WriteLine("<dl>")

            for subNs in subNamespaces do
                let shortName =
                    let lastDot = subNs.LastIndexOf('.')

                    if lastDot < 0 then
                        subNs
                    else
                        subNs.[lastDot + 1 ..]

                sb.WriteLine($"<dt>{htmlLinkGen shortName subNs}</dt>")

            sb.WriteLine("</dl>")
            sb.NewLine()

        if not entities.IsEmpty then
            h2 sb toc "types" "Types"
            sb.WriteLine("<dl>")

            for e in entities do
                sb.WriteLine(
                    $"<dt>{htmlLinkGen e.Name e.FullName}{obsoleteInlineHtml e.ObsoleteInfo}</dt>"
                )

                match e.XmlDoc.Summary with
                | Some summary -> sb.WriteLine($"<dd>{escapeMdxInline summary}</dd>")
                | None -> ()

            sb.WriteLine("</dl>")
            sb.NewLine()

        renderDeclaredModules sb toc htmlLinkGen modules

        {
            Title = ns
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }

    let renderModulePage
        (htmlLinkGen: string -> string -> string)
        (m: Module)
        (subModules: Module list)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        renderObsoleteBanner sb m.ObsoleteInfo

        renderDeclaredModules sb toc htmlLinkGen subModules

        if not m.Entities.IsEmpty then
            h2 sb toc "types" "Types"
            sb.WriteLine("<dl>")

            for e in m.Entities do
                sb.WriteLine(
                    $"<dt>{htmlLinkGen e.Name e.FullName}{obsoleteInlineHtml e.ObsoleteInfo}</dt>"
                )

                match e.XmlDoc.Summary with
                | Some summary -> sb.WriteLine($"<dd>{escapeMdxInline summary}</dd>")
                | None -> ()

            sb.WriteLine("</dl>")
            sb.NewLine()

        if not m.Functions.IsEmpty then
            h2 sb toc "functions" "Functions"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for f in m.Functions do
                tocH3 toc f.Name f.Name
                renderFunctionEntry sb f

            sb.WriteLine("</div>")
            sb.NewLine()

        if not m.Values.IsEmpty then
            h2 sb toc "values" "Values"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for v in m.Values do
                tocH3 toc v.Name v.Name
                renderValueEntry sb v

            sb.WriteLine("</div>")
            sb.NewLine()

        {
            Title = m.FullName
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }

    let renderRootIndexPage
        (htmlLinkGen: string -> string -> string)
        (assemblies: Assembly list)
        (globalModules: Module list)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        if not globalModules.IsEmpty then
            h2 sb toc "modules" "Modules"
            sb.WriteLine("<dl>")

            for m in globalModules do
                sb.WriteLine(
                    $"<dt>{htmlLinkGen m.Name m.FullName}{obsoleteInlineHtml m.ObsoleteInfo}</dt>"
                )

                match m.XmlDoc with
                | Some doc -> sb.WriteLine($"<dd>{escapeMdxInline doc}</dd>")
                | None -> ()

            sb.WriteLine("</dl>")
            sb.NewLine()

        if not assemblies.IsEmpty then
            h2 sb toc "assemblies" "Assemblies"
            sb.WriteLine("<dl>")

            for a in assemblies do
                sb.WriteLine($"<dt>{htmlLinkGen a.Name a.Name}</dt>")

            sb.WriteLine("</dl>")
            sb.NewLine()

        {
            Title = "API Reference"
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }
