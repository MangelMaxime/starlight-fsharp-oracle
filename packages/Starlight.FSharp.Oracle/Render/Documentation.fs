namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open TextNode.Extensions
open FSharp.Oracle.Schema
open Primitives

module Documentation =

    /// Documentation prose: cross-references resolved, then escaped for MDX.
    let docText (links: LinkResolver) (text: string) =
        text |> resolveCrefLinks links |> escapeMdxMarkdown

    let renderSummary (links: LinkResolver) (sb: StringBuilder) (xmlDoc: XmlDoc) =
        match xmlDoc.Summary with
        | Some summary ->
            sb.WriteLine(docText links summary)
            sb.NewLine()
        | None -> ()

    /// A labelled block of prose, e.g. **Returns** followed by its description.
    let private labelled (sb: StringBuilder) (label: string) (body: string) =
        sb.WriteLine($"<strong>{label}</strong>")
        sb.NewLine()
        sb.WriteLine(body)
        sb.NewLine()

    /// `<typeparam>` entries. Rendered like parameters, since they are parameters -
    /// just at the type level.
    let private renderTypeParams (links: LinkResolver) (sb: StringBuilder) (xmlDoc: XmlDoc) =
        if not xmlDoc.TypeParams.IsEmpty then
            sb.WriteLine("<strong>Type parameters</strong>")

            for typeParam in xmlDoc.TypeParams do
                TextNode.ToHtml(
                    links,
                    [
                        TextNode.Tick
                        TextNode.TypeVar typeParam.Name
                    ]
                )
                |> signatureBlock
                |> sb.WriteLine

                sb.WriteLine "<div class='fs-parameter__documentation'>"
                sb.NewLine()
                sb.WriteLine(docText links typeParam.Doc)
                sb.NewLine()
                sb.WriteLine "</div>"

    /// `<exception>` entries: what a caller has to be ready to catch.
    let private renderExceptions (links: LinkResolver) (sb: StringBuilder) (xmlDoc: XmlDoc) =
        if not xmlDoc.Exceptions.IsEmpty then
            sb.WriteLine("<strong>Exceptions</strong>")

            for exn in xmlDoc.Exceptions do
                let name = Cref.displayName exn.Type

                TextNode.ToHtml(links, [ TextNode.TypeRef(name, exn.Type) ])
                |> signatureBlock
                |> sb.WriteLine

                sb.WriteLine "<div class='fs-parameter__documentation'>"
                sb.NewLine()
                sb.WriteLine(docText links exn.Doc)
                sb.NewLine()
                sb.WriteLine "</div>"

    /// `<seealso>` targets, as a list of links to the ones that have a page.
    let private renderSeeAlso (links: LinkResolver) (sb: StringBuilder) (xmlDoc: XmlDoc) =
        let documented = xmlDoc.SeeAlso |> List.filter links.IsDocumented

        if not documented.IsEmpty then
            sb.WriteLine("<strong>See also</strong>")
            sb.NewLine()

            documented
            |> List.map (fun target -> links.Link(Cref.displayName target, target))
            |> String.concat ", "
            |> sb.WriteLine

            sb.NewLine()

    /// Summary, then whatever the caller adds, then the sections every kind of
    /// declaration can carry.
    let renderDocumentationBlock
        (links: LinkResolver)
        (sb: StringBuilder)
        (xmlDoc: XmlDoc)
        (renderExtra: unit -> unit)
        =
        renderSummary links sb xmlDoc

        // Remarks were rendered on entity pages only, so a function's or member's
        // <remarks> was silently dropped.
        xmlDoc.Remarks
        |> Option.iter (fun remarks ->
            sb.WriteLine(docText links remarks)
            sb.NewLine()
        )

        renderExtra ()

        xmlDoc.Value |> Option.iter (fun value -> labelled sb "Value" (docText links value))

        renderExceptions links sb xmlDoc
        renderExamples sb xmlDoc.Examples
        renderSeeAlso links sb xmlDoc

    let renderXmlDocSummaryAndRemarks
        (links: LinkResolver)
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (xmlDoc: XmlDoc)
        =
        xmlDoc.Summary
        |> Option.iter (fun summary ->
            h2 sb toc "description" "Description"
            sb.WriteLine(docText links summary)
            sb.NewLine()
        )

        xmlDoc.Remarks
        |> Option.iter (fun remarks ->
            sb.WriteLine(docText links remarks)
            sb.NewLine()
        )

        renderTypeParams links sb xmlDoc
        renderSeeAlso links sb xmlDoc

    let private renderParamsAndReturns
        (links: LinkResolver)
        (sb: StringBuilder)
        (parameters: Parameter list list)
        (xmlDoc: XmlDoc)
        =
        let allParams = parameters |> List.collect id

        // Listed whenever there are parameters: an undocumented parameter still
        // needs to appear, or the reader cannot tell what the function takes.
        if not allParams.IsEmpty then
            sb.WriteLine("<strong>Parameters</strong>")

            for parameter in allParams do
                (Declarations.parameterDeclaration parameter).ToHtml(links)
                |> signatureBlock
                |> sb.WriteLine

                match xmlDoc.Params |> List.tryFind (fun p -> p.Name = parameter.Name) with
                | Some paramDoc ->
                    sb.WriteLine "<div class='fs-parameter__documentation'>"
                    sb.NewLine()
                    sb.WriteLine(docText links paramDoc.Doc)
                    sb.NewLine()
                    sb.WriteLine "</div>"
                | None -> ()

        renderTypeParams links sb xmlDoc

        xmlDoc.Returns
        |> Option.iter (fun returnDoc -> labelled sb "Returns" (docText links returnDoc))

    let renderXmlDocBody
        (links: LinkResolver)
        (sb: StringBuilder)
        (parameters: Parameter list list)
        (xmlDoc: XmlDoc)
        =
        renderDocumentationBlock links sb xmlDoc (fun () ->
            renderParamsAndReturns links sb parameters xmlDoc
        )
