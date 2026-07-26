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
