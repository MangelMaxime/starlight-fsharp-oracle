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

/// Which class a symbol takes, following the captures in tree-sitter-fsharp's
/// highlights.scm rather than colouring every symbol as a keyword.
///
/// `->` and `|` really are `@keyword.control` there - F# reads them as control
/// syntax, not as operators - which is why they keep the keyword colour while `:`
/// and the brackets do not.
let private symbolClass =
    function
    | Symbol.Colon
    | Symbol.Comma
    | Symbol.Semicolon -> "fsharp-doc-punct"
    | Symbol.LeftParen
    | Symbol.RightParen
    | Symbol.LeftBrace
    | Symbol.RightBrace -> "fsharp-doc-bracket"
    | Symbol.Arrow
    | Symbol.Bar -> "fsharp-doc-kw"
    | Symbol.Equals
    | Symbol.Star
    | Symbol.LessThan
    | Symbol.GreaterThan
    | Symbol.SubtypeOf
    | Symbol.Question -> "fsharp-doc-op"

/// How wide one level of structural indentation is.
let private indentWidth = 4

/// MDX strips whitespace-only text nodes (mashing adjacent signature tokens
/// together), but keeps text that is not purely whitespace. Pair the non-breaking
/// space with an (invisible) zero-width non-joiner so the node survives while still
/// rendering - and copying - as a single space.
let private space = "&nbsp;&zwnj;"

/// Punctuation, already escaped for HTML and MDX. Exhaustive on purpose: a new symbol
/// cannot be added without deciding here whether it needs escaping. `*` in particular
/// would otherwise pair with a `*` from a neighbouring token and become emphasis.
let private symbolText =
    function
    | Symbol.Colon -> ":"
    | Symbol.Arrow -> "->"
    | Symbol.SubtypeOf -> ":>"
    | Symbol.Equals -> "="
    | Symbol.Comma -> ","
    | Symbol.Semicolon -> ";"
    | Symbol.Star -> "&#42;"
    | Symbol.Bar -> "|"
    | Symbol.Question -> "?"
    | Symbol.LessThan -> "&lt;"
    | Symbol.GreaterThan -> "&gt;"
    | Symbol.LeftParen -> "("
    | Symbol.RightParen -> ")"
    | Symbol.LeftBrace -> "{"
    | Symbol.RightBrace -> "}"

let private anchored (href: string) (text: string) = $"""<a href="{href}">{text}</a>"""

type TextNode with

    static member ToHtml(links: LinkResolver, nodes: TextNode list) : string =
        (TextNode.Node nodes).ToHtml(links)

    member this.ToHtml(links: LinkResolver) : string =
        match this with
        | TextNode.Text s -> escapeText s
        | TextNode.Punctuation symbol -> wrapWithClass (symbolClass symbol) (symbolText symbol)
        | TextNode.Keyword text -> wrapInKeyword text
        | TextNode.Tick -> "&#x27;"
        | TextNode.Space -> space
        | TextNode.NewLine -> "\n"
        | TextNode.Indent levels -> String.replicate (levels * indentWidth) space
        | TextNode.TypeVar name -> wrapWithClass "fsharp-doc-typevar" name
        // A parameter is not a type variable; it only borrowed that class to keep the
        // output stable while the token model was being sorted out.
        | TextNode.ParameterName name -> wrapWithClass "fsharp-doc-param" name
        | TextNode.Attribute text -> $"""<span class="fsharp-doc-attr">{escapeText text}</span>"""
        // Whether a type reference becomes a link is the resolver's call: only it
        // knows which types have a page.
        | TextNode.TypeRef(name, fullName) ->
            wrapWithClass "fsharp-doc-type" (links.Link(escapeText name, fullName))
        | TextNode.DeclarationName(text, anchor, role) ->
            let link = anchored $"#{anchor}" (escapeText text)

            match role with
            | DeclarationRole.Member -> wrapWithClass "fsharp-doc-property" link
            | DeclarationRole.Constructor -> wrapWithClass "fsharp-doc-kw" link
            | DeclarationRole.UnionCase -> link
        | TextNode.Node nodes ->
            nodes |> List.map (fun node -> node.ToHtml(links)) |> String.concat ""
