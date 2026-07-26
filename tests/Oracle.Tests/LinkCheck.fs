/// Walks the built site and asserts every internal link resolves to a page that was
/// actually generated. Snapshots pin the output; this proves the output is navigable.
module Oracle.Tests.LinkCheck

open System
open System.IO
open System.Text.RegularExpressions

let private hrefPattern = Regex("href=\"(?<href>[^\"]*)\"", RegexOptions.Compiled)

/// Astro emits directory-style pages: /api/foo -> api/foo/index.html.
/// Accept either that or a literal file, so the check does not depend on build style.
let private resolves (distRoot: string) (href: string) =
    let relative = href.Trim('/').Replace('/', Path.DirectorySeparatorChar)

    let candidates =
        [
            Path.Combine(distRoot, relative)
            Path.Combine(distRoot, relative + ".html")
            Path.Combine(distRoot, relative, "index.html")
        ]

    relative = "" || candidates |> List.exists (fun path -> File.Exists path || Directory.Exists path)

/// Links we do not own and cannot verify from disk.
let private isExternal (href: string) =
    href = ""
    || href.StartsWith "#"
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

    let broken =
        [
            for page in pages do
                let html = File.ReadAllText page

                for m in hrefPattern.Matches html do
                    let href = m.Groups.["href"].Value

                    if not (isExternal href) then
                        // Drop any fragment or query before resolving to a file.
                        let target = href.Split([| '#'; '?' |]).[0]

                        if not (isExternal target) && not (resolves distRoot target) then
                            yield Path.GetRelativePath(distRoot, page), href
        ]
        |> List.distinct

    printfn ""
    printfn "links: %i page(s) scanned, %i broken" pages.Length broken.Length

    if broken.IsEmpty then
        0
    else
        printfn ""

        for page, href in broken |> List.truncate 40 do
            printfn "  BROKEN %s -> %s" page href

        if broken.Length > 40 then
            printfn "  ... and %i more" (broken.Length - 40)

        printfn ""
        1
