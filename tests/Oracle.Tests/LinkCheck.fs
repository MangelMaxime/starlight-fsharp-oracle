/// Walks the built site and asserts every internal link resolves - both the page it
/// names and, when it carries one, the fragment within that page.
///
/// Snapshots pin the output; this proves the output is navigable. The fragment half
/// matters because a link to a real page with a dead `#anchor` still lands the reader
/// in the wrong place, and nothing else checks it: the sidebar drifted that way for
/// three phases before anyone noticed.
module Oracle.Tests.LinkCheck

open System
open System.IO
open System.Text.RegularExpressions

let private hrefPattern = Regex("href=\"(?<href>[^\"]*)\"", RegexOptions.Compiled)
let private idPattern = Regex("id=\"(?<id>[^\"]*)\"", RegexOptions.Compiled)

/// Astro emits directory-style pages: /api/foo -> api/foo/index.html.
/// Accept either that or a literal file, so the check does not depend on build style.
let private resolveFile (distRoot: string) (href: string) =
    let relative = href.Trim('/').Replace('/', Path.DirectorySeparatorChar)

    [
        Path.Combine(distRoot, relative)
        Path.Combine(distRoot, relative + ".html")
        Path.Combine(distRoot, relative, "index.html")
    ]
    |> List.tryFind File.Exists

let private resolves (distRoot: string) (href: string) =
    let relative = href.Trim('/')

    relative = ""
    || (resolveFile distRoot href).IsSome
    || Directory.Exists(Path.Combine(distRoot, relative.Replace('/', Path.DirectorySeparatorChar)))

/// Links we do not own and cannot verify from disk.
let private isExternal (href: string) =
    href = ""
    || href.StartsWith "http://"
    || href.StartsWith "https://"
    || href.StartsWith "mailto:"
    || href.StartsWith "//"
    || href.StartsWith "data:"

let run (distRoot: string) : int =
    if not (Directory.Exists distRoot) then
        eprintfn "Site not built: %s" distRoot
        eprintfn "Run: ./build.sh docs"
        1
    else

    let pages = Directory.GetFiles(distRoot, "*.html", SearchOption.AllDirectories)

    // Ids are read once per page: a fragment is checked against the page it points at,
    // which is usually not the page the link is on.
    let idsOf =
        pages
        |> Array.map (fun page ->
            let ids =
                idPattern.Matches(File.ReadAllText page)
                |> Seq.map (fun m -> m.Groups.["id"].Value)
                |> Set.ofSeq

            Path.GetFullPath page, ids
        )
        |> Map.ofArray

    let fragmentResolves (fromPage: string) (target: string) (fragment: string) =
        // An empty fragment is "top of page", and the browser always honours it.
        if fragment = "" || fragment = "_top" then
            true
        else
            let targetFile =
                if target = "" then
                    Some fromPage
                else
                    resolveFile distRoot target

            match targetFile with
            // A missing target page is already reported as a broken link; do not
            // report the same link twice.
            | None -> true
            | Some file ->
                match Map.tryFind (Path.GetFullPath file) idsOf with
                | Some ids -> Set.contains fragment ids
                | None -> true

    let broken =
        [
            for page in pages do
                let html = File.ReadAllText page

                for m in hrefPattern.Matches html do
                    let href = m.Groups.["href"].Value

                    if not (isExternal href) then
                        let withoutQuery = href.Split('?').[0]
                        let parts = withoutQuery.Split('#')
                        let target = parts.[0]

                        let fragment =
                            if parts.Length > 1 then
                                parts.[1]
                            else
                                ""

                        if target <> "" && not (resolves distRoot target) then
                            yield Path.GetRelativePath(distRoot, page), href, "no such page"
                        elif not (fragmentResolves page target fragment) then
                            yield Path.GetRelativePath(distRoot, page), href, "no such anchor"
        ]
        |> List.distinct

    printfn ""
    printfn "links: %i page(s) scanned, %i broken" pages.Length broken.Length

    if broken.IsEmpty then
        0
    else
        printfn ""

        for page, href, reason in broken |> List.truncate 40 do
            printfn "  BROKEN (%s) %s -> %s" reason page href

        if broken.Length > 40 then
            printfn "  ... and %i more" (broken.Length - 40)

        printfn ""
        1
