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

    /// Escape MDX-hostile characters in Markdown text while leaving code spans
    /// (`` `inline` `` and ``` ```fenced``` ```) untouched. MDX never parses `{`
    /// or `<` as JSX inside code, and HTML entities render verbatim there, so
    /// escaping code content would turn `` `Var<'T>` `` into a literal
    /// `Var&lt;'T&gt;`. Only the prose between code spans needs escaping.
    let escapeMdxMarkdown (text: string) : string =
        let sb = StringBuilder(text.Length)
        let n = text.Length
        let mutable i = 0

        while i < n do
            if i + 2 < n && text.[i] = '`' && text.[i + 1] = '`' && text.[i + 2] = '`' then
                // Fenced code block: copy verbatim up to and including the closing fence.
                let close = text.IndexOf("```", i + 3)
                let endIdx = if close < 0 then n else close + 3
                sb.Append(text.Substring(i, endIdx - i)) |> ignore
                i <- endIdx
            elif text.[i] = '`' then
                // Inline code span: copy verbatim up to and including the closing backtick.
                let close = text.IndexOf("`", i + 1)
                let endIdx = if close < 0 then n else close + 1
                sb.Append(text.Substring(i, endIdx - i)) |> ignore
                i <- endIdx
            else
                (match text.[i] with
                 | '&' -> sb.Append("&amp;")
                 | '<' -> sb.Append("&lt;")
                 | '>' -> sb.Append("&gt;")
                 | '{' -> sb.Append("&#123;")
                 | '}' -> sb.Append("&#125;")
                 | c -> sb.Append(c))
                |> ignore

                i <- i + 1

        sb.ToString()

    /// Collapse whitespace runs before escaping, so a blank line can't be read as
    /// a paragraph break that closes an inline element like `<dd>` early.
    /// Markdown code spans are preserved (see `escapeMdxMarkdown`).
    let escapeMdxInline (text: string) : string =
        let collapsed =
            System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim()

        escapeMdxMarkdown collapsed

    /// MDX reflows multi-line raw HTML as a markdown paragraph, collapsing layout.
    /// Convert newlines to <br/> so the signature block renders verbatim.
    let inlineSignatureHtml (html: string) : string =
        html.Replace("\n", "<br/>")

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
        sb.Write(inlineSignatureHtml signatureHtml)
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
            sb.WriteLine(escapeMdxMarkdown single)
            sb.NewLine()
        | multiple ->
            for i, example in List.indexed multiple do
                sb.WriteLine($"<strong>Example {i + 1}</strong>")
                sb.NewLine()
                sb.WriteLine(escapeMdxMarkdown example)
                sb.NewLine()
