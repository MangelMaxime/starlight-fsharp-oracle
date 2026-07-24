module Oracle.XmlDoc

// XML doc formatter adapted from Ionide.XmlDocFormatter
// https://github.com/ionide/Ionide.XmlDocFormatter/blob/develop/src/Ionide.XmlDocFormatter.fs

open System
open System.Text.RegularExpressions
open FSharp.Oracle.Schema

// ---------------------------------------------------------------------------
// Formatter config and tag infrastructure
// ---------------------------------------------------------------------------

type private FormatterConfig =
    {
        Paragraph: string -> string
        CodeBlock: string -> string -> string
        InlineCode: string -> string
        HandleMicrosoftOrList: string -> string
        BeforeTransform: string -> string
        AfterTransform: string -> string
        Example: string -> string
        Block: string -> string
        // Reference / link formatters
        MemberRef: string -> string
        LangwordRef: string -> string
        ExternalLink: string -> string -> string
    }

let private tagPattern (tagName: string) =
    sprintf
        """(?'void_element'<%s(?'void_attributes'\s+[^\/>]+)?\/>)|(?'non_void_element'<%s(?'non_void_attributes'\s+[^>]+)?>(?'non_void_innerText'(?:(?!<%s>)(?!<\/%s>)[\s\S])*)<\/%s\s*>)"""
        tagName
        tagName
        tagName
        tagName
        tagName

type private TagInfo =
    | VoidElement of attributes: Map<string, string>
    | NonVoidElement of innerText: string * attributes: Map<string, string>

let private extractTextFromQuote (quotedText: string) =
    quotedText.Substring(1, quotedText.Length - 2)

let private extractMemberText (text: string) =
    let pattern = "(?'member_type'[a-z]{1}:)?(?'member_text'.*)"
    let m = Regex.Match(text, pattern, RegexOptions.IgnoreCase)

    if m.Groups.["member_text"].Success then
        m.Groups.["member_text"].Value
    else
        text

let private getAttributes (attributes: Group) =
    if attributes.Success then
        let pattern = """(?'key'\S+)=(?'value''[^']*'|"[^"]*")"""

        Regex.Matches(attributes.Value, pattern, RegexOptions.IgnoreCase)
        |> Seq.cast<Match>
        |> Seq.map (fun m -> m.Groups.["key"].Value, extractTextFromQuote m.Groups.["value"].Value)
        |> Map.ofSeq
    else
        Map.empty

let rec private applyFormatter
    (info:
        {|
            TagName: string
            Formatter: TagInfo -> string option
        |})
    text
    =
    let pattern = tagPattern info.TagName

    match Regex.Match(text, pattern, RegexOptions.IgnoreCase) with
    | m when m.Success ->
        if m.Groups.["void_element"].Success then
            let attributes = getAttributes m.Groups.["void_attributes"]

            match VoidElement attributes |> info.Formatter with
            | Some replacement ->
                text.Replace(m.Groups.["void_element"].Value, replacement)
                |> applyFormatter info
            | None -> text

        elif m.Groups.["non_void_element"].Success then
            let innerText = m.Groups.["non_void_innerText"].Value
            let attributes = getAttributes m.Groups.["non_void_attributes"]

            match NonVoidElement(innerText, attributes) |> info.Formatter with
            | Some replacement ->
                text.Replace(m.Groups.["non_void_element"].Value, replacement)
                |> applyFormatter info
            | None -> text
        else
            text
    | _ -> text

// ---------------------------------------------------------------------------
// Individual tag formatters
// ---------------------------------------------------------------------------

let private codeBlock (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "code"
            Formatter =
                function
                | VoidElement _ -> None
                | NonVoidElement(innerText, attributes) ->
                    let lang = attributes |> Map.tryFind "lang" |> Option.defaultValue "fsharp"

                    let innerText = innerText.TrimEnd()

                    let normalizedText =
                        match
                            innerText.StartsWith("\n", StringComparison.Ordinal),
                            innerText.EndsWith("\n", StringComparison.Ordinal)
                        with
                        | true, true -> $"%s{innerText}"
                        | true, false -> $"%s{innerText}\n"
                        | false, true -> $"\n%s{innerText}"
                        | false, false -> $"\n%s{innerText}\n"

                    config.CodeBlock lang normalizedText |> Some
        |}

let private example (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "example"
            Formatter =
                function
                | VoidElement _ -> None
                | NonVoidElement(innerText, _) -> config.Example innerText |> Some
        |}

let private codeInline (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "c"
            Formatter =
                function
                | VoidElement _ -> None
                | NonVoidElement(innerText, _) -> config.InlineCode innerText |> Some
        |}

let private anchor (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "a"
            Formatter =
                function
                | VoidElement attributes ->
                    match Map.tryFind "href" attributes with
                    | Some href -> Some(config.ExternalLink href href)
                    | None -> None
                | NonVoidElement(innerText, attributes) ->
                    match Map.tryFind "href" attributes with
                    | Some href -> Some(config.ExternalLink href innerText)
                    | None -> Some(config.InlineCode innerText)
        |}

let private paragraph (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "para"
            Formatter =
                function
                | VoidElement _ -> None
                | NonVoidElement(innerText, _) -> config.Paragraph innerText |> Some
        |}

let private block (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "block"
            Formatter =
                function
                | VoidElement _ -> None
                | NonVoidElement(innerText, _) -> config.Block innerText |> Some
        |}

let private see (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "see"
            Formatter =
                let fromAttrs (attrs: Map<string, string>) =
                    match Map.tryFind "cref" attrs with
                    | Some cref -> Some(config.MemberRef(extractMemberText cref))
                    | None ->
                        match Map.tryFind "langword" attrs with
                        | Some langword -> Some(config.LangwordRef langword)
                        | None -> None

                function
                | VoidElement attributes -> fromAttrs attributes
                | NonVoidElement(innerText, attributes) ->
                    if String.IsNullOrWhiteSpace innerText then
                        fromAttrs attributes
                    else
                        match Map.tryFind "href" attributes with
                        | Some externalUrl -> Some(config.ExternalLink externalUrl innerText)
                        | None -> Some(config.InlineCode innerText)
        |}

let private paramRef (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "paramref"
            Formatter =
                function
                | VoidElement attributes ->
                    match Map.tryFind "name" attributes with
                    | Some name -> Some(config.InlineCode name)
                    | None -> None
                | NonVoidElement(innerText, attributes) ->
                    if String.IsNullOrWhiteSpace innerText then
                        match Map.tryFind "name" attributes with
                        | Some name -> Some(config.InlineCode name)
                        | None -> None
                    else
                        Some(config.InlineCode innerText)
        |}

let private typeParamRef (config: FormatterConfig) =
    applyFormatter
        {|
            TagName = "typeparamref"
            Formatter =
                function
                | VoidElement attributes ->
                    match Map.tryFind "name" attributes with
                    | Some name -> Some(config.InlineCode name)
                    | None -> None
                | NonVoidElement(innerText, attributes) ->
                    if String.IsNullOrWhiteSpace innerText then
                        match Map.tryFind "name" attributes with
                        | Some name -> Some(config.InlineCode name)
                        | None -> None
                    else
                        Some(config.InlineCode innerText)
        |}

let private unescapeSpecialCharacters (text: string) =
    text
        .Replace("&lt;", "<")
        .Replace("&gt;", ">")
        .Replace("&quot;", "\"")
        .Replace("&apos;", "'")
        .Replace("&amp;", "&")

// ---------------------------------------------------------------------------
// Configs and main entry points
// ---------------------------------------------------------------------------

let private markdownConfig =
    {
        BeforeTransform = id
        AfterTransform = unescapeSpecialCharacters
        Paragraph = fun content -> Environment.NewLine + content + Environment.NewLine
        CodeBlock = fun lang code -> "```" + lang + code + "```"
        InlineCode = fun code -> "`" + code + "`"
        HandleMicrosoftOrList = id
        Example =
            fun content ->
                Environment.NewLine
                + Environment.NewLine
                + "**Example:**"
                + Environment.NewLine
                + Environment.NewLine
                + content
        Block = fun content -> Environment.NewLine + content + Environment.NewLine
        MemberRef = fun name -> $"``{name}``"
        LangwordRef = fun word -> $"`{word}`"
        ExternalLink = fun href text -> $"[`{text}`]({href})"
    }

let private formatToMarkdown (text: string) =
    text
    |> markdownConfig.BeforeTransform
    |> paragraph markdownConfig
    |> example markdownConfig
    |> block markdownConfig
    |> codeInline markdownConfig
    |> codeBlock markdownConfig
    |> see markdownConfig
    |> paramRef markdownConfig
    |> typeParamRef markdownConfig
    |> anchor markdownConfig
    |> markdownConfig.HandleMicrosoftOrList
    |> markdownConfig.AfterTransform

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

let private emptyXmlDoc =
    {
        Summary = None
        Remarks = None
        Returns = None
        Params = []
        Examples = []
    }

let private extractFirst (tagName: string) (text: string) : string option =
    match Regex.Match(text, tagPattern tagName, RegexOptions.IgnoreCase) with
    | m when m.Success && m.Groups.["non_void_element"].Success ->
        m.Groups.["non_void_innerText"].Value
        |> formatToMarkdown
        |> fun s -> s.Trim()
        |> Some
    | _ -> None

let private extractAll (tagName: string) (text: string) : string list =
    Regex.Matches(text, tagPattern tagName, RegexOptions.IgnoreCase)
    |> Seq.cast<Match>
    |> Seq.choose (fun m ->
        if m.Groups.["non_void_element"].Success then
            m.Groups.["non_void_innerText"].Value
            |> formatToMarkdown
            |> fun s -> s.Trim()
            |> Some
        else
            None
    )
    |> Seq.toList

let private extractParams (text: string) : XmlDocParam list =
    Regex.Matches(text, tagPattern "param", RegexOptions.IgnoreCase)
    |> Seq.cast<Match>
    |> Seq.choose (fun m ->
        if m.Groups.["non_void_element"].Success then
            let attrs = getAttributes m.Groups.["non_void_attributes"]
            let inner = m.Groups.["non_void_innerText"].Value

            match Map.tryFind "name" attrs with
            | Some name ->
                Some
                    {
                        Name = name
                        Doc = formatToMarkdown inner |> fun s -> s.Trim()
                    }
            | None -> None
        else
            None
    )
    |> Seq.toList

/// Parse the inner XML content of a <member> element into the structured
/// XmlDoc IR type, with all inline tags converted to Markdown.
let private parseXmlContent (text: string) : XmlDoc =
    if String.IsNullOrWhiteSpace text then
        emptyXmlDoc
    else
        {
            Summary = extractFirst "summary" text
            Remarks = extractFirst "remarks" text
            Returns = extractFirst "returns" text
            Params = extractParams text
            Examples = extractAll "example" text
        }

// ---------------------------------------------------------------------------
// XML documentation file loader
// ---------------------------------------------------------------------------

/// Escape `<` and `>` characters that appear inside XML attribute values.
/// The F# compiler sometimes generates invalid XML for anonymous record
/// type signatures (e.g. `name="M:Foo(<>f__AnonymousType...)`).
let private fixXmlAttributeChars (xml: string) : string =
    let sb = System.Text.StringBuilder(xml.Length)
    let mutable inTag = false
    let mutable inQuotes = false
    let mutable i = 0

    while i < xml.Length do
        let c = xml.[i]

        if not inTag then
            if c = '<' then
                inTag <- true

            sb.Append(c) |> ignore
        elif inQuotes then
            if c = '"' then
                inQuotes <- false
                sb.Append(c) |> ignore
            elif c = '<' then
                sb.Append("&lt;") |> ignore
            elif c = '>' then
                sb.Append("&gt;") |> ignore
            else
                sb.Append(c) |> ignore
        else
            if c = '"' then
                inQuotes <- true
            elif c = '>' then
                inTag <- false

            sb.Append(c) |> ignore

        i <- i + 1

    sb.ToString()

/// Load the XML documentation file that lives next to a compiled DLL and
/// return a lookup map from the standard XML-doc member ID (e.g.
/// "M:My.Namespace.func(System.Int32)") to the inner XML content of that
/// <member> element.
let loadXmlDocFile (dllPath: string) : Map<string, string> =
    let xmlPath = System.IO.Path.ChangeExtension(dllPath, ".xml")

    if not (System.IO.File.Exists xmlPath) then
        Map.empty
    else
        let rawXmlText = System.IO.File.ReadAllText(xmlPath)

        // The F# compiler can emit unescaped `<`/`>` in member IDs (e.g. `<>f__AnonymousType...`),
        // breaking XML parsing. Escape them inside `name="..."`; XDocument decodes them back unchanged.
        let xmlText =
            System.Text.RegularExpressions.Regex.Replace(
                rawXmlText,
                "name=\"([^\"]*)\"",
                fun m ->
                    let escaped = m.Groups.[1].Value.Replace("<", "&lt;").Replace(">", "&gt;")
                    $"name=\"{escaped}\""
            )

        let doc =
            try
                System.Xml.Linq.XDocument.Parse(xmlText)
            with
            | :? System.Xml.XmlException as ex ->
                eprintfn "ERROR: Failed to parse XML documentation file: %s" xmlPath
                let lines = xmlText.Split('\n')
                let lineIdx = ex.LineNumber - 1
                let startLine = max 0 (lineIdx - 2)
                let endLine = min (lines.Length - 1) (lineIdx + 2)

                for i in startLine .. endLine do
                    let marker = if i = lineIdx then ">>> " else "    "
                    eprintfn "%sLine %d: %s" marker (i + 1) lines.[i]

                reraise()

        doc.Descendants(System.Xml.Linq.XName.Get "member")
        |> Seq.choose (fun m ->
            let nameAttr = m.Attribute(System.Xml.Linq.XName.Get "name")

            if nameAttr <> null then
                let innerXml = m.Nodes() |> Seq.map (fun n -> n.ToString()) |> String.concat "\n"

                Some(nameAttr.Value, innerXml)
            else
                None
        )
        |> Map.ofSeq

/// Look up and parse the XML doc for a given member signature.
/// Returns an empty XmlDoc when the signature is not found in the map.
let xmlDocOf (xmlDocMap: Map<string, string>) (xmlDocSig: string) : XmlDoc =
    xmlDocMap
    |> Map.tryFind xmlDocSig
    |> Option.map parseXmlContent
    |> Option.defaultValue emptyXmlDoc

/// Look up just the summary for a module-level signature.
/// Used for Module.XmlDoc which is stored as a plain string in the IR.
let moduleDocOf (xmlDocMap: Map<string, string>) (xmlDocSig: string) : string option =
    xmlDocMap |> Map.tryFind xmlDocSig |> Option.bind (extractFirst "summary")
