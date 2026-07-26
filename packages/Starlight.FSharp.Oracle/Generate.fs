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

/// Every fully-qualified name that gets a page. Anything outside this set - BCL types,
/// types from assemblies we are not documenting, synthetic `Ns.global` holders - must
/// render as plain text, not as a link to a page that was never written.
let documentedNames (modules: Module list) : Set<string> =
    Set.ofList
        [
            for m in modules do
                // Synthetic modules are namespace-level type holders; they have no page.
                if not m.IsSynthetic then
                    m.FullName

                for e in m.Entities do
                    e.FullName

            yield! namespacesOf modules
        ]

let linkResolver (basePath: string) (outputBase: string) (modules: Module list) : Render.LinkResolver =
    let documented = documentedNames modules

    {
        IsDocumented = fun fullName -> Set.contains fullName documented
        Href = fun fullName -> $"{basePath}/{outputBase}/{toSlug fullName}"
    }

let namespacePages (links: Render.LinkResolver) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)
    let allNamespaces = namespacesOf modules

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
        toMdxPage (Render.renderNamespacePage links ns subNamespaces entitiesInNs modulesInNs)
    )

let modulePages (links: Render.LinkResolver) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    realModules
    |> List.map (fun m ->
        let subModules =
            realModules |> List.filter (fun other -> other.Namespace = m.FullName)

        toSlug m.FullName, toMdxPage (Render.renderModulePage links m subModules)
    )

let entityPages (links: Render.LinkResolver) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    // A generic type and its companion module (e.g. `type Var<'T>` + `module Var`)
    // collapse to the same slug once the generic arity suffix is stripped. Find that
    // module so its members can be folded into the type page instead of one silently
    // overwriting the other on disk.
    let companionOf (entity: Entity) (parent: Module) =
        let slug = toSlug entity.FullName

        realModules
        |> List.tryFind (fun m -> m.FullName <> parent.FullName && toSlug m.FullName = slug)

    [
        for m in modules do
            for e in m.Entities do
                toSlug e.FullName, toMdxPage (Render.renderEntityPage links e m (companionOf e m))
    ]

/// Slugs of modules whose page is merged into a same-slug entity page.
/// Their standalone module page must be dropped so it does not clobber the merged one.
let mergedModuleSlugs (modules: Module list) : string list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)
    let entitySlugs = modules |> List.collect (fun m -> m.Entities) |> List.map (fun e -> toSlug e.FullName)

    realModules
    |> List.map (fun m -> toSlug m.FullName)
    |> List.filter (fun slug -> List.contains slug entitySlugs)
    |> List.distinct

let rootIndexPage
    (links: Render.LinkResolver)
    (assemblies: Assembly list)
    (modules: Module list)
    : string * string
    =
    let globalModules =
        modules |> List.filter (fun m -> not m.IsSynthetic && m.Namespace = "")

    "index", toMdxPage (Render.renderRootIndexPage links assemblies globalModules)

/// Every page the plugin writes, as (slug, mdx content).
/// Shared by the plugin and the snapshot tests so the two cannot drift apart.
let allPages (basePath: string) (outputBase: string) (root: Root) : (string * string) list =
    let modules = root.Assemblies |> List.collect _.Modules
    let links = linkResolver basePath outputBase modules

    // Modules whose page is folded into a same-slug entity page (generic type +
    // companion module) must not be written standalone, or they clobber it.
    let mergedSlugs = mergedModuleSlugs modules |> Set.ofList

    let moduleOutputs =
        modulePages links modules
        |> List.filter (fun (slug, _) -> not (Set.contains slug mergedSlugs))

    let moduleSlugs = moduleOutputs |> List.map fst |> Set.ofList

    let namespaceOutputs =
        namespacePages links modules
        |> List.filter (fun (slug, _) -> not (Set.contains slug moduleSlugs))

    [
        rootIndexPage links root.Assemblies modules
        yield! namespaceOutputs
        yield! moduleOutputs
        yield! entityPages links modules
    ]

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

    // A generic type and its companion module share a slug and are merged onto one
    // page (see `entityPages`). The module already appears in the tree as a group,
    // so drop the redundant bare entity link that points to the same page.
    let realModuleSlugs = realModules |> List.map (fun m -> toSlug m.FullName)

    let entitySidebarItems (entities: Entity list) =
        entities
        |> List.filter (fun e -> not (List.contains (toSlug e.FullName) realModuleSlugs))
        |> List.map (entitySidebarItem outputBase)

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
                        yield! entitySidebarItems sm.Entities
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
                                (entitySidebarItems sm.Entities)
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
