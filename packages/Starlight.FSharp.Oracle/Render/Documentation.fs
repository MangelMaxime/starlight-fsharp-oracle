namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open TextNode.Extensions
open FSharp.Oracle.Schema
open Primitives

module Documentation =

    let renderSummary (sb: StringBuilder) (xmlDoc: XmlDoc) =
        match xmlDoc.Summary with
        | Some summary ->
            sb.WriteLine(escapeMdxMarkdown summary)
            sb.NewLine()
        | None -> ()

    /// Renders summary + optional extra content + examples.
    /// Used by all DocEntry documentation closures for consistency.
    let renderDocumentationBlock
        (sb: StringBuilder)
        (xmlDoc: XmlDoc)
        (renderExtra: unit -> unit)
        =
        renderSummary sb xmlDoc
        renderExtra ()
        renderExamples sb xmlDoc.Examples

    let renderXmlDocSummaryAndRemarks
        (sb: StringBuilder)
        (toc: ResizeArray<TocEntry>)
        (xmlDoc: XmlDoc)
        =
        xmlDoc.Summary
        |> Option.iter (fun summary ->
            h2 sb toc "description" "Description"
            sb.WriteLine(escapeMdxMarkdown summary)
            sb.NewLine()
        )

        xmlDoc.Remarks
        |> Option.iter (fun remarks ->
            sb.WriteLine(escapeMdxMarkdown remarks)
            sb.NewLine()
        )

    let private renderParamsAndReturns
        (sb: StringBuilder)
        (parameters: Parameter list list)
        (xmlDoc: XmlDoc)
        =
        if not xmlDoc.Params.IsEmpty then
            sb.WriteLine("<strong>Parameters</strong>")

            for parameter in parameters |> List.collect id do
                let paramDoc = xmlDoc.Params |> List.tryFind (fun p -> p.Name = parameter.Name)

                match paramDoc with
                | Some paramDoc ->
                    [
                        TextNode.OpenTagWithClass("div", "fs-parameter__signature")
                        TextNode.NewLine
                        parameter.Declaration
                        TextNode.NewLine
                        TextNode.CloseTag "div"
                        TextNode.NewLine
                    ]
                    |> TextNode.ToHtml
                    |> sb.WriteLine

                    sb.WriteLine "<div class='fs-parameter__documentation'>"
                    sb.NewLine()
                    sb.WriteLine(escapeMdxMarkdown paramDoc.Doc)
                    sb.NewLine()
                    sb.WriteLine "</div>"
                | None -> sb.WriteLine(parameter.Declaration.Html)

        match xmlDoc.Returns with
        | Some returnDoc ->
            sb.WriteLine("<strong>Returns</strong>")
            sb.NewLine()
            sb.WriteLine(escapeMdxMarkdown returnDoc)
            sb.NewLine()
        | None -> ()

    let renderXmlDocBody
        (sb: StringBuilder)
        (parameters: Parameter list list)
        (xmlDoc: XmlDoc)
        =
        renderDocumentationBlock sb xmlDoc (fun () -> renderParamsAndReturns sb parameters xmlDoc)
