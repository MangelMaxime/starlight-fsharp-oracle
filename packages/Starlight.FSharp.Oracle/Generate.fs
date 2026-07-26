module Starlight.FSharp.Generate

open Fable.Core
open FSharp.Oracle.Schema

// ---------------------------------------------------------------------------
// Starlight sidebar POJO types
// ---------------------------------------------------------------------------

[<AllowNullLiteral; Global>]
type SidebarBadge [<ParamObject; Emit("$0")>] (text: string, ?``class``: string) =
    member val text: string = jsNative with get, set
    member val ``class``: string option = jsNative with get, set

[<AllowNullLiteral; Global>]
type SidebarLink [<ParamObject; Emit("$0")>] (label: string, link: string, ?badge: SidebarBadge) =
    member val label: string = jsNative with get, set
    member val link: string = jsNative with get, set
    member val badge: SidebarBadge option = jsNative with get, set

/// A sidebar item is either a link or a collapsible group.
/// U2 is erased at runtime — compiles to the raw JS object.
type SidebarItem = U2<SidebarLink, SidebarGroup>

and [<AllowNullLiteral; Global>] SidebarGroup
    [<ParamObject; Emit("$0")>]
    (label: string, items: SidebarItem array, ?collapsed: bool, ?badge: SidebarBadge)
    =
    member val label: string = jsNative with get, set
    member val items: SidebarItem array = jsNative with get, set
    member val collapsed: bool = jsNative with get, set
    member val badge: SidebarBadge option = jsNative with get, set

let private toSlug (name: string) =
    let sanitized = name.ToLowerInvariant().Replace(".", "-")
    // Strip F# generic arity suffix (e.g. Tree`1 -> tree)
    // Use string ops rather than Regex so Fable/JS behaves identically to .NET.
    let backtickIdx = sanitized.LastIndexOf("`")
    if backtickIdx >= 0 then sanitized.Substring(0, backtickIdx) else sanitized

let private expandNamespaces (namespaces: string list) : string list =
    namespaces
    |> List.collect (fun ns ->
        let parts = ns.Split('.')

        parts
        |> Array.scan
            (fun acc part ->
                if acc = "" then
                    part
                else
                    $"{acc}.{part}"
            )
            ""
        |> Array.tail
        |> Array.toList
    )
    |> List.distinct
    |> List.sort

let private namespacesOf (modules: Module list) =
    modules
    |> List.filter (fun m -> not m.IsSynthetic)
    |> List.map (fun m -> m.Namespace)
    |> List.filter (fun ns -> ns <> "")
    |> List.distinct
    |> expandNamespaces

// ---------------------------------------------------------------------------
// Astro page assembly
// ---------------------------------------------------------------------------

let private escapeYaml (s: string) =
    s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "")

let private toMdxPage (page: Render.RenderedPage) : string =
    let headingsSection =
        if page.TocEntries.IsEmpty then
            "headings: []"
        else
            let entries =
                page.TocEntries
                |> List.map (fun e ->
                    $"  - depth: {e.Depth}\n    slug: \"{escapeYaml e.Slug}\"\n    text: \"{escapeYaml e.Text}\""
                )
                |> String.concat "\n"

            "headings:\n" + entries

    $"""---
layout: 'starlight-fsharp-oracle/layouts/FSharpDocLayout.astro'
title: "{escapeYaml page.Title}"
{headingsSection}
---

import DocEntry from 'starlight-fsharp-oracle/components/DocEntry.astro';
import {{ Aside }} from '@astrojs/starlight/components';

{page.TemplateBody}"""

// ---------------------------------------------------------------------------
// Page generation
// ---------------------------------------------------------------------------

let namespacePages (basePath: string) (outputBase: string) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)
    let allNamespaces = namespacesOf modules

    let htmlLinkGen (name: string) (fullName: string) =
        $"""<a href="{basePath}/{outputBase}/{toSlug fullName}">{name}</a>"""

    let directChildNamespaces (ns: string) =
        allNamespaces
        |> List.filter (fun other ->
            other.StartsWith(ns + ".")
            && not (other.[ns.Length + 1 ..].Contains("."))
            // Skip namespaces that are also modules — they're already in Declared Modules
            && not (realModules |> List.exists (fun m -> m.FullName = other))
        )

    let syntheticModules = modules |> List.filter (fun m -> m.IsSynthetic)

    allNamespaces
    |> List.map (fun ns ->
        let slug = toSlug ns
        let modulesInNs = realModules |> List.filter (fun m -> m.Namespace = ns)
        let subNamespaces = directChildNamespaces ns

        let entitiesInNs =
            syntheticModules
            |> List.filter (fun m -> m.Namespace = ns)
            |> List.collect (fun m -> m.Entities)

        slug,
        toMdxPage (
            Render.renderNamespacePage htmlLinkGen ns subNamespaces entitiesInNs modulesInNs
        )
    )

let modulePages (basePath: string) (outputBase: string) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    let htmlLinkGen (name: string) (fullName: string) =
        $"""<a href="{basePath}/{outputBase}/{toSlug fullName}">{name}</a>"""

    realModules
    |> List.map (fun m ->
        let subModules =
            realModules |> List.filter (fun other -> other.Namespace = m.FullName)

        toSlug m.FullName, toMdxPage (Render.renderModulePage htmlLinkGen m subModules)
    )

let entityPages (basePath: string) (outputBase: string) (modules: Module list) : (string * string) list =
    let htmlLinkGen (name: string) (fullName: string) =
        $"""<a href="{basePath}/{outputBase}/{toSlug fullName}">{name}</a>"""

    [
        for m in modules do
            for e in m.Entities do
                toSlug e.FullName, toMdxPage (Render.renderEntityPage htmlLinkGen e m)
    ]

let rootIndexPage
    (basePath: string)
    (outputBase: string)
    (assemblies: Assembly list)
    (modules: Module list)
    : string * string
    =
    let htmlLinkGen (name: string) (fullName: string) =
        $"""<a href="{basePath}/{outputBase}/{toSlug fullName}">{name}</a>"""

    let globalModules =
        modules |> List.filter (fun m -> not m.IsSynthetic && m.Namespace = "")

    "index", toMdxPage (Render.renderRootIndexPage htmlLinkGen assemblies globalModules)

// ---------------------------------------------------------------------------
// Sidebar
// ---------------------------------------------------------------------------

let private sidebarLink
    (outputBase: string)
    (label: string)
    (fullName: string)
    (letter: string)
    (kind: string)
    : SidebarItem
    =
    U2.Case1(SidebarLink(label, $"/{outputBase}/{toSlug fullName}"))

let private sidebarGroup
    (label: string)
    (letter: string)
    (kind: string)
    (items: SidebarItem list)
    : SidebarItem
    =
    U2.Case2(SidebarGroup(label, items |> Array.ofList, collapsed = true))

let private entityLetterAndKind (entity: Entity) =
    match entity with
    | Entity.Record _ -> "R", "record"
    | Entity.Union _ -> "U", "union"
    | Entity.Class _ -> "C", "class"
    | Entity.Interface _ -> "I", "interface"
    | Entity.Abbrev _ -> "A", "abbrev"
    | Entity.Enum _ -> "E", "enum"
    | Entity.Measure _ -> "M", "measure"
    | Entity.Exception _ -> "X", "exception"
    | Entity.Delegate _ -> "D", "delegate"

let private entitySidebarItem (outputBase: string) (entity: Entity) =
    let letter, kind = entityLetterAndKind entity

    SidebarLink(entity.Name, $"/{outputBase}/{toSlug entity.FullName}")
    |> U2.Case1

/// Returns a Starlight sidebar group with a full hierarchy:
/// namespaces → modules → entities.
let sidebarTree (outputBase: string) (label: string) (modules: Module list) =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)
    let syntheticModules = modules |> List.filter (fun m -> m.IsSynthetic)
    let allNamespaces = namespacesOf modules

    let moduleHref (m: Module) = $"/{outputBase}/{toSlug m.FullName}"

    let anchorLink (label: string) (href: string) : SidebarItem =
        U2.Case1(SidebarLink(label, href))

    // Build a module item, recursively including sub-modules as children.
    // Always a collapsible group with an Overview link first.
    let rec moduleSidebarItem (m: Module) =
        let subModules =
            realModules |> List.filter (fun other -> other.Namespace = m.FullName)

        let overviewLink: SidebarItem = U2.Case1(SidebarLink("Overview", moduleHref m))

        let entityItems = m.Entities |> List.map (entitySidebarItem outputBase)

        let functionItems =
            m.Functions
            |> List.map (fun f -> anchorLink f.Name $"{moduleHref m}#{f.Name}")

        let valueItems =
            m.Values
            |> List.map (fun v -> anchorLink v.Name $"{moduleHref m}#{v.Name}")

        let subModuleItems = subModules |> List.map moduleSidebarItem

        let children =
            overviewLink :: entityItems @ functionItems @ valueItems @ subModuleItems

        sidebarGroup m.Name "M" "module" children

    let rec buildNsGroup (ns: string) =
        let shortName =
            let lastDot = ns.LastIndexOf('.')

            if lastDot < 0 then
                ns
            else
                ns.[lastDot + 1 ..]

        // Skip child namespaces that are already represented by a real module
        // (e.g. Reference.Text is both a module and the namespace of Words/Lines).
        let directChildNs =
            allNamespaces
            |> List.filter (fun other ->
                other.StartsWith(ns + ".")
                && not (other.[ns.Length + 1 ..].Contains("."))
                && not (realModules |> List.exists (fun m -> m.FullName = other))
            )

        let modulesInNs = realModules |> List.filter (fun m -> m.Namespace = ns)
        // Synthetic modules represent bare namespace declarations; show them as sub-groups.
        let syntheticInNs = syntheticModules |> List.filter (fun m -> m.Namespace = ns)

        let items =
            [
                yield! modulesInNs |> List.map moduleSidebarItem
                for sm in syntheticInNs do
                    // "global" synthetic modules group bare namespace-level types.
                    // Inline them directly rather than nesting under a "global" sub-group.
                    if sm.Name = "global" then
                        yield! sm.Entities |> List.map (entitySidebarItem outputBase)
                    else
                        let smShortName =
                            let lastDot = sm.FullName.LastIndexOf('.')

                            if lastDot < 0 then
                                sm.FullName
                            else
                                sm.FullName.[lastDot + 1 ..]

                        yield
                            sidebarGroup
                                smShortName
                                "N"
                                "namespace"
                                (sm.Entities |> List.map (entitySidebarItem outputBase))
                yield! directChildNs |> List.map buildNsGroup
            ]

        sidebarGroup shortName "N" "namespace" items

    let topLevelNs =
        allNamespaces
        |> List.filter (fun ns ->
            not (allNamespaces |> List.exists (fun other -> ns.StartsWith(other + ".")))
            // Skip namespaces that are also real modules — the module tree already
            // shows everything inside them (values, entities, sub-modules).
            && not (realModules |> List.exists (fun m -> m.FullName = ns))
        )

    let globalModules = realModules |> List.filter (fun m -> m.Namespace = "")

    let overviewLink: SidebarItem = U2.Case1(SidebarLink("Overview", $"/{outputBase}"))

    let items =
        [
            overviewLink
            yield! globalModules |> List.map moduleSidebarItem
            yield! topLevelNs |> List.map buildNsGroup
        ]

    SidebarGroup(label, items |> Array.ofList)

/// Returns a tiny inline script that publishes the sidebar label so that
/// the external sidebar-controls.js file can find the right section without
/// needing any F#-generated JS logic.
let sidebarLabelInitScript (sidebarLabel: string) : string =
    let escaped = sidebarLabel.Replace("\\", "\\\\").Replace("'", "\\'")
    $"window.__fsharpSidebarLabel = '{escaped}';"
