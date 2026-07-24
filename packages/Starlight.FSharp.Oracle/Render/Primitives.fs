namespace Starlight.FSharp.RenderImpl

open System.Text
open StringBuilder.Extensions
open FSharp.Oracle.Schema

module Primitives =

    /// Escape characters that MDX interprets as JSX syntax so plain
    /// documentation text can be emitted safely into an .mdx file.
    let escapeMdxText (text: string) : string =
        text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("{", "&#123;")
            .Replace("}", "&#125;")

    /// Escape text destined for an inline element such as `<dd>`. A blank line in
    /// the source would be parsed by MDX as a paragraph break and close the
    /// element early, so all whitespace runs are collapsed to single spaces first.
    let escapeMdxInline (text: string) : string =
        let collapsed =
            System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim()

        escapeMdxText collapsed

    let h2 (sb: StringBuilder) (toc: ResizeArray<TocEntry>) (slug: string) (text: string) =
        toc.Add(
            {
                Depth = 2
                Slug = slug
                Text = text
            }
        )

        sb.WriteLine($"<h2 id=\"{slug}\">{text}</h2>")
        sb.NewLine()

    let tocH3 (toc: ResizeArray<TocEntry>) (slug: string) (text: string) =
        toc.Add(
            {
                Depth = 3
                Slug = slug
                Text = text
            }
        )

    let renderDocEntry
        (sb: StringBuilder)
        (name: string)
        (signatureHtml: string)
        (obsoleteInfo: ObsoleteInfo)
        renderDocumentation
        =
        let obsoleteAttr, obsoleteMessageAttr =
            match obsoleteInfo with
            | ObsoleteInfo.Active -> "", ""
            | ObsoleteInfo.Deprecated -> " obsolete", ""
            | ObsoleteInfo.DeprecatedWithMessage msg ->
                let escaped = msg.Replace("\"", "&quot;")
                " obsolete", $" obsoleteMessage=\"{escaped}\""

        sb.WriteLine($"<DocEntry name=\"{name}\"{obsoleteAttr}{obsoleteMessageAttr}>")
        sb.Write("<div class=\"fsharp-doc-sig\" slot=\"signature\">")
        sb.Write(signatureHtml)
        sb.WriteLine("</div>")
        sb.NewLine()

        renderDocumentation ()

        sb.WriteLine("</DocEntry>")
        sb.NewLine()

    let renderObsoleteBanner (sb: StringBuilder) (obsoleteInfo: ObsoleteInfo) =
        match obsoleteInfo with
        | ObsoleteInfo.Active -> ()
        | ObsoleteInfo.Deprecated ->
            sb.WriteLine("""<Aside type="caution" title="Deprecated">This type or module is obsolete.</Aside>""")
            sb.NewLine()
        | ObsoleteInfo.DeprecatedWithMessage msg ->
            sb.WriteLine($"""<Aside type="caution" title="Deprecated">{escapeMdxText msg}</Aside>""")
            sb.NewLine()

    let obsoleteInlineHtml (obsoleteInfo: ObsoleteInfo) =
        match obsoleteInfo with
        | ObsoleteInfo.Active -> ""
        | ObsoleteInfo.Deprecated -> """ <span class="fsharp-doc-obsolete-inline">Deprecated</span>"""
        | ObsoleteInfo.DeprecatedWithMessage msg -> $""" <span class="fsharp-doc-obsolete-inline" title="{msg}">Deprecated</span>"""

    let renderExamples (sb: StringBuilder) (examples: string list) =
        match examples with
        | [] -> ()
        | [ single ] ->
            sb.WriteLine("<strong>Example</strong>")
            sb.NewLine()
            sb.WriteLine(escapeMdxText single)
            sb.NewLine()
        | multiple ->
            for i, example in List.indexed multiple do
                sb.WriteLine($"<strong>Example {i + 1}</strong>")
                sb.NewLine()
                sb.WriteLine(escapeMdxText example)
                sb.NewLine()
