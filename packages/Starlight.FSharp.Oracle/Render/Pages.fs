namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open TextNode.Extensions
open FSharp.Oracle.Schema
open Primitives
open Documentation
open Entries

module Pages =

    /// A `<dl>` of links with optional summaries - the shape every index-style
    /// section on every page uses.
    let private definitionList
        (sb: StringBuilder)
        (entries: (string * string option) list)
        =
        sb.WriteLine("<dl>")

        for term, description in entries do
            sb.WriteLine($"<dt>{term}</dt>")

            match description with
            | Some text -> sb.WriteLine($"<dd>{escapeMdxInline text}</dd>")
            | None -> ()

        sb.WriteLine("</dl>")
        sb.NewLine()

    let private renderFunctionsAndValues
        (links: LinkResolver)
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (functions: Function list)
        (values: Value list)
        =
        if not functions.IsEmpty then
            h2 sb toc "functions" "Functions"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for f in functions do
                tocH3 toc f.Name f.Name
                renderFunctionEntry links sb f

            sb.WriteLine("</div>")
            sb.NewLine()

        if not values.IsEmpty then
            h2 sb toc "values" "Values"
            sb.WriteLine("<div class=\"collapsible-group\">")

            for v in values do
                tocH3 toc v.Name v.Name
                renderValueEntry links sb v

            sb.WriteLine("</div>")
            sb.NewLine()

    let renderDeclaredModules
        (links: LinkResolver)
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (modules: Module list)
        =
        if not modules.IsEmpty then
            h2 sb toc "declared-modules" "Declared Modules"

            modules
            |> List.map (fun m ->
                links.Link(m.Name, m.FullName) + obsoleteInlineHtml m.ObsoleteInfo, m.XmlDoc
            )
            |> definitionList sb

    let private renderTypeList
        (links: LinkResolver)
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (entities: Entity list)
        =
        if not entities.IsEmpty then
            h2 sb toc "types" "Types"

            entities
            |> List.map (fun e ->
                links.Link(e.Name, e.FullName) + obsoleteInlineHtml e.ObsoleteInfo,
                e.XmlDoc.Summary
            )
            |> definitionList sb

    let private renderFields
        (links: LinkResolver)
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (heading: (string * string) option)
        (fields: Field list)
        =
        if not fields.IsEmpty then
            match heading with
            | Some(slug, title) -> h2 sb toc slug title
            | None -> ()

            sb.WriteLine("<div class=\"collapsible-group\">")

            for field in fields do
                tocH3 toc field.Name field.Name
                renderRecordField links sb field

            sb.WriteLine("</div>")
            sb.NewLine()

    let renderEntityPage
        (links: LinkResolver)
        (entity: Entity)
        (parentModule: Module)
        (companionModule: Module option)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        // The root namespace has no page and no name worth printing, and a synthetic
        // module ("Ns.global", holding bare namespace-level types) has no page either -
        // both used to be emitted as links, to `/api/` and `/api/ns-global`.
        let breadcrumbs =
            [
                if parentModule.Namespace <> "" then
                    "Namespace", links.Link(parentModule.Namespace, parentModule.Namespace)

                if not parentModule.IsSynthetic then
                    "Parent", links.Link(parentModule.FullName, parentModule.FullName)
            ]
            |> List.map (fun (label, link) -> $"<strong>{label}:</strong> {link}")
            |> String.concat "&nbsp;&nbsp;"

        if breadcrumbs <> "" then
            sb.WriteLine($"<p>{breadcrumbs}</p>")
            sb.NewLine()

        renderObsoleteBanner sb entity.ObsoleteInfo

        sb.WriteLine("<div class=\"not-content\">")
        sb.Write("<div class=\"fsharp-doc-sig\">")
        sb.Write(inlineSignatureHtml (entity.Declaration.ToHtml(links)))
        sb.WriteLine("</div>")
        sb.WriteLine("</div>")
        sb.NewLine()

        renderXmlDocSummaryAndRemarks sb toc entity.XmlDoc

        match entity with
        | Entity.Union e ->
            sb.WriteLine("<div class=\"collapsible-group\">")

            for c in e.Cases do
                tocH3 toc c.Name c.Name
                renderUnionCaseEntry links sb c

            sb.WriteLine("</div>")
            sb.NewLine()

            renderMemberSections links sb toc e.Members
        | Entity.Record r ->
            renderFields links sb toc (Some("fields", "Fields")) r.Fields
            renderMemberSections links sb toc r.Members
        | Entity.Enum e -> renderFields links sb toc None e.Fields
        | Entity.Class e -> renderMemberSections links sb toc e.Members
        | Entity.Interface e -> renderMemberSections links sb toc e.Members
        | Entity.Exception e -> renderFields links sb toc (Some("fields", "Fields")) e.Fields
        | Entity.Abbrev _
        | Entity.Measure _
        | Entity.Delegate _ -> ()

        // A generic type (e.g. `Var<'T>`) and its companion module (`Var`) share a
        // URL slug. Rather than let one page overwrite the other, fold the module's
        // functions and values into this type page so nothing is lost.
        match companionModule with
        | Some cm -> renderFunctionsAndValues links sb toc cm.Functions cm.Values
        | None -> ()

        {
            Title = entity.Name
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }

    let renderNamespacePage
        (links: LinkResolver)
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

            subNamespaces
            |> List.map (fun subNs ->
                let shortName =
                    let lastDot = subNs.LastIndexOf('.')

                    if lastDot < 0 then
                        subNs
                    else
                        subNs.[lastDot + 1 ..]

                links.Link(shortName, subNs), None
            )
            |> definitionList sb

        renderTypeList links sb toc entities
        renderDeclaredModules links sb toc modules

        {
            Title = ns
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }

    let renderModulePage
        (links: LinkResolver)
        (m: Module)
        (subModules: Module list)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        renderObsoleteBanner sb m.ObsoleteInfo
        renderDeclaredModules links sb toc subModules
        renderTypeList links sb toc m.Entities
        renderFunctionsAndValues links sb toc m.Functions m.Values

        {
            Title = m.FullName
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }

    let renderRootIndexPage
        (links: LinkResolver)
        (assemblies: Assembly list)
        (globalModules: Module list)
        : RenderedPage
        =

        let sb = StringBuilder()
        let toc = ResizeArray<TocEntry>()

        if not globalModules.IsEmpty then
            h2 sb toc "modules" "Modules"

            globalModules
            |> List.map (fun m ->
                links.Link(m.Name, m.FullName) + obsoleteInlineHtml m.ObsoleteInfo, m.XmlDoc
            )
            |> definitionList sb

        // Assemblies are listed for orientation only - there is no per-assembly page,
        // so these are deliberately plain text rather than links to nowhere.
        if not assemblies.IsEmpty then
            h2 sb toc "assemblies" "Assemblies"

            assemblies |> List.map (fun a -> a.Name, None) |> definitionList sb

        {
            Title = "API Reference"
            TemplateBody = sb.ToString()
            TocEntries = toc |> Seq.toList
        }
