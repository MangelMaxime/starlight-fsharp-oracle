module TextNode.Extensions

open FSharp.Oracle.Schema
open Starlight.FSharp.RenderImpl

/// Escape `{`/`}` which MDX parses as JSX expressions. Angle brackets are left
/// untouched since callers may pass intentional HTML (e.g. anchors).
let private escapeJsx (s: string) =
    s.Replace("{", "&#123;").Replace("}", "&#125;")

/// Like `escapeJsx` but also escapes angle brackets, which MDX would parse as
/// JSX tags (e.g. the `<Struct>` in a `[<Struct>]` attribute).
let private escapeText (s: string) =
    s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("{", "&#123;")
        .Replace("}", "&#125;")

let wrapWithClass cls (text: string) =
    $"""<span class="{cls}">{escapeJsx text}</span>"""

let wrapInKeyword text = wrapWithClass "fsharp-doc-kw" text

type TextNode with

    static member ToHtml(links: LinkResolver, nodes: TextNode list) : string =
        (TextNode.Node nodes).ToHtml(links)

    member this.ToHtml(links: LinkResolver) : string =
        match this with
        | TextNode.Text s -> escapeText s
        | TextNode.Colon -> wrapInKeyword ":"
        | TextNode.Arrow -> wrapInKeyword "->"
        | TextNode.Dot -> wrapInKeyword "."
        | TextNode.Comma -> wrapInKeyword ","
        // MDX strips whitespace-only text nodes (mashing adjacent signature
        // tokens together), but keeps text that is not purely whitespace. Pair the
        // non-breaking space with an (invisible) zero-width non-joiner so the node
        // survives while still rendering - and copying - as a single space.
        | TextNode.Space -> "&nbsp;&zwnj;"
        | TextNode.GreaterThan -> wrapInKeyword "&gt;"
        | TextNode.LessThan -> wrapInKeyword "&lt;"
        | TextNode.LeftBrace -> wrapInKeyword "{"
        | TextNode.RightBrace -> wrapInKeyword "}"
        | TextNode.Equal -> wrapInKeyword "="
        | TextNode.Tick -> "&#x27;"
        | TextNode.LeftParen -> wrapInKeyword "("
        | TextNode.RightParen -> wrapInKeyword ")"
        | TextNode.Node node ->
            node |> List.map (fun node -> node.ToHtml(links)) |> String.concat ""
        | TextNode.Keyword text -> wrapInKeyword text
        // Emit the asterisk as an HTML entity so MDX's markdown parser does not
        // treat it as emphasis and pair it with a `*` from a neighbouring node.
        | TextNode.Star -> wrapInKeyword "&#42;"
        // The url baked into the node by the extractor is ignored: it was generated for
        // every named type, including ones with no page. The resolver decides instead.
        | TextNode.TypeRef(name, fullName, _) ->
            wrapWithClass "fsharp-doc-type" (links.Link(escapeText name, fullName))
        | TextNode.TypeVar name -> wrapWithClass "fsharp-doc-typevar" name
        | TextNode.NewLine -> "\n"
        | TextNode.OpenTag tagName -> $"""<{tagName}>"""
        | TextNode.OpenTagWithClass(tagName, cls) -> $"""<{tagName} class="{cls}">"""
        | TextNode.CloseTag tagName -> $"""</{tagName}>"""
        | TextNode.Anchor(text, href) -> $"""<a href="{href}">{text}</a>"""
        | TextNode.AnchoredProperty(text, href) ->
            wrapWithClass "fsharp-doc-property" $"""<a href="{href}">{text}</a>"""
        | TextNode.AnchoredKeyword(text, href) ->
            wrapWithClass "fsharp-doc-kw" $"""<a href="{href}">{text}</a>"""
        | TextNode.Spaces count ->
            [
                for _ in 1..count do
                    TextNode.Space
            ]
            |> TextNode.Node
            |> fun node -> node.ToHtml(links)
