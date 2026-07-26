namespace Starlight.FSharp.RenderImpl

/// Decides whether a fully-qualified name has a generated page, and what it links to.
///
/// Only the renderer can answer the first question - it is what knows the complete page
/// set - which is why the extractor must not decide it. Emitting a link for every named
/// type is what produced hrefs like `/api/system-datetime` and
/// `/api/microsoft-fsharp-core-fsharpchoice`, none of which exist.
type LinkResolver =
    {
        /// True when a page will be generated for this fully-qualified name.
        IsDocumented: string -> bool
        /// The href of the page for a documented name.
        Href: string -> string
    }

    /// An anchor when the target is documented, plain text when it is not.
    /// Undocumented names are still worth showing - the reader wants to know the type
    /// is `DateTime`, they just must not be promised a page that does not exist.
    member this.Link(text: string, fullName: string) : string =
        if this.IsDocumented fullName then
            $"""<a href="{this.Href fullName}">{text}</a>"""
        else
            text

/// Page-local anchors, which are a different problem from page URLs: they must be
/// usable as an HTML id and a URL fragment, and unique within one page.
module Cref =

    /// The scheme the extractor uses for `<see cref="..."/>` targets in doc text. It
    /// records what was referenced; only the renderer can say whether that has a page.
    [<Literal>]
    let Scheme = "fsharp-doc:"

    /// The last segment of a fully-qualified name, without the generic arity suffix
    /// F# appends: `Reference.Coverage.SortedBag\`1` reads as `SortedBag`.
    let displayName (fullName: string) =
        let withoutArity =
            let backtick = fullName.LastIndexOf('`')

            if backtick >= 0 then
                fullName.Substring(0, backtick)
            else
                fullName

        let lastDot = withoutArity.LastIndexOf('.')

        if lastDot >= 0 then
            withoutArity.Substring(lastDot + 1)
        else
            withoutArity

module Anchor =

    /// Keep identifier characters and collapse everything else into a separator, so
    /// `(|Positive|Negative|Zero|)` anchors as `Positive-Negative-Zero` rather than
    /// putting pipes and parentheses into an href. Segments that are only underscores
    /// are dropped: they are the wildcard of a partial active pattern and add nothing.
    /// String operations rather than a Regex, so Fable and .NET agree.
    let slug (text: string) =
        let parts = ResizeArray<string>()
        let current = System.Text.StringBuilder()

        let flush () =
            if current.Length > 0 then
                let segment = current.ToString()

                if segment |> Seq.exists (fun c -> c <> '_') then
                    parts.Add segment

                current.Clear() |> ignore

        for c in text do
            if System.Char.IsLetterOrDigit c || c = '_' then
                current.Append(c) |> ignore
            else
                flush ()

        flush ()
        String.concat "-" parts

    /// Pair each item with an anchor, suffixing repeats `-1`, `-2`, ... This is what
    /// keeps overloads apart: two `Format` methods anchor as `Format` and `Format-1`,
    /// and it subsumes the old constructor-only special case.
    let assign (slugOf: 'T -> string) (items: 'T list) : ('T * string) list =
        items
        |> List.mapFold
            (fun (seen: Map<string, int>) item ->
                let baseSlug =
                    match slugOf item with
                    | "" -> "item"
                    | s -> s

                let count = seen |> Map.tryFind baseSlug |> Option.defaultValue 0

                let anchor =
                    if count = 0 then
                        baseSlug
                    else
                        $"{baseSlug}-{count}"

                (item, anchor), Map.add baseSlug (count + 1) seen
            )
            Map.empty
        |> fst
