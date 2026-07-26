module Starlight.FSharp.Generate

open Fable.Core
open FSharp.Oracle.Schema
open Starlight.FSharp.RenderImpl

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

/// The page slug of a fully-qualified name.
///
/// Only `.` used to be replaced, so a backticked F# name - ``My Type`` - kept its
/// spaces all the way into the filename and the href. `Anchor.slug` already collapses
/// anything that is not an identifier character, so this reuses it and folds case.
let private toSlug (name: string) =
    // The generic arity suffix FCS appends (Tree`1) goes first: collapsing punctuation
    // before stripping it would leave `tree-1` behind.
    let withoutArity =
        let backtick = name.LastIndexOf('`')

        if
            backtick >= 0
            && backtick < name.Length - 1
            && name.Substring(backtick + 1) |> Seq.forall System.Char.IsDigit
        then
            name.Substring(0, backtick)
        else
            name

    (Anchor.slug withoutArity).ToLowerInvariant()

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

let private toMdxPage (page: RenderedPage) : string =
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

/// Assign a slug to every name of one kind, disambiguating names that collapse to the
/// same one. Deterministic: the names are sorted, the first keeps the plain slug.
let private assignSlugs (names: string list) =
    names
    |> List.distinct
    |> List.sort
    |> List.groupBy toSlug
    |> List.collect (fun (baseSlug, group) ->
        group
        |> List.mapi (fun i name ->
            let slug =
                if i = 0 then
                    baseSlug
                else
                    $"{baseSlug}-{i + 1}"

            name, slug
        )
    )

/// Names of one kind that collapse to the same slug, as warning lines.
/// Reports the URL each name actually received: knowing there is a clash is much less
/// use than knowing which page moved.
let private collisionsIn (kind: string) (names: string list) =
    let assigned = assignSlugs names |> Map.ofList

    names
    |> List.distinct
    |> List.sort
    |> List.groupBy toSlug
    |> List.filter (fun (_, group) -> group.Length > 1)
    |> List.map (fun (baseSlug, group) ->
        let assignments =
            group
            |> List.map (fun name ->
                let slug = assigned |> Map.tryFind name |> Option.defaultValue baseSlug
                $"{name} -> /{slug}"
            )
            |> String.concat ", "

        $"{kind} collapse to the same URL: {assignments}. Renaming one is the only way "
        + "to keep these stable - the suffixes shift as names are added or removed."
    )

/// Slug collisions worth telling the user about.
///
/// A module and a type may share a slug on purpose - a generic type and its companion
/// module are merged onto one page - so only collisions within one kind are reported.
let slugWarnings (modules: Module list) : string list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    [
        yield!
            collisionsIn
                "types"
                (modules |> List.collect (fun m -> m.Entities) |> List.map (fun e -> e.FullName))
        yield! collisionsIn "modules" (realModules |> List.map (fun m -> m.FullName))
    ]

let linkResolver (basePath: string) (outputBase: string) (modules: Module list) : LinkResolver =
    let documented = documentedNames modules
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    // Types and modules are slugged separately, so the one intentional collision -
    // a generic type and its companion module, merged onto one page - is preserved
    // while two types that collapse to the same slug are pulled apart.
    let slugs =
        [
            yield!
                assignSlugs (
                    modules |> List.collect (fun m -> m.Entities) |> List.map (fun e -> e.FullName)
                )
            yield! assignSlugs (realModules |> List.map (fun m -> m.FullName))
        ]
        |> Map.ofList

    let slugOf fullName =
        slugs |> Map.tryFind fullName |> Option.defaultValue (toSlug fullName)

    {
        IsDocumented = fun fullName -> Set.contains fullName documented
        Href = fun fullName -> $"{basePath}/{outputBase}/{slugOf fullName}"
        Slug = slugOf
    }

let namespacePages (links: LinkResolver) (modules: Module list) : (string * string) list =
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
        toMdxPage (Pages.renderNamespacePage links ns subNamespaces entitiesInNs modulesInNs)
    )

/// Extension members targeting a given type, labelled with the module that declares
/// them. A reader has to know which module to open for them to be in scope.
let private extensionsFor (modules: Module list) (entity: Entity) =
    [
        for m in modules do
            let members =
                m.ExtensionMembers
                |> List.filter (fun e -> e.ExtendedType = entity.FullName)
                |> List.map (fun e -> e.Member)

            if not members.IsEmpty then
                entity.Name, Some m.FullName, members
    ]

let modulePages (links: LinkResolver) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    realModules
    |> List.map (fun m ->
        let subModules =
            realModules |> List.filter (fun other -> other.Namespace = m.FullName)

        // Extensions whose target has no page would otherwise vanish, so they stay
        // with the module that declares them.
        let orphans =
            m.ExtensionMembers
            |> List.filter (fun e -> not (links.IsDocumented e.ExtendedType))
            |> List.groupBy (fun e -> e.ExtendedTypeName)
            |> List.map (fun (typeName, extensions) ->
                // Declared on this very page, so naming the source would be noise.
                typeName, None, extensions |> List.map (fun e -> e.Member)
            )

        links.Slug m.FullName, toMdxPage (Pages.renderModulePage links m subModules orphans)
    )

let entityPages (links: LinkResolver) (modules: Module list) : (string * string) list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)

    // A generic type and its companion module (e.g. `type Var<'T>` + `module Var`)
    // collapse to the same slug once the generic arity suffix is stripped. Find that
    // module so its members can be folded into the type page instead of one silently
    // overwriting the other on disk.
    let companionOf (entity: Entity) (parent: Module) =
        let slug = links.Slug entity.FullName

        realModules
        |> List.tryFind (fun m -> m.FullName <> parent.FullName && links.Slug m.FullName = slug)

    [
        for m in modules do
            for e in m.Entities do
                links.Slug e.FullName,
                toMdxPage (
                    Pages.renderEntityPage links e m (companionOf e m) (extensionsFor modules e)
                )
    ]

/// Slugs of modules whose page is merged into a same-slug entity page.
/// Their standalone module page must be dropped so it does not clobber the merged one.
let mergedModuleSlugs (modules: Module list) : string list =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)
    let entitySlugs =
        modules |> List.collect (fun m -> m.Entities) |> List.map (fun e -> toSlug e.FullName) |> Set.ofList

    realModules
    |> List.map (fun m -> toSlug m.FullName)
    |> List.filter (fun slug -> Set.contains slug entitySlugs)
    |> List.distinct

let rootIndexPage
    (links: LinkResolver)
    (assemblies: Assembly list)
    (modules: Module list)
    : string * string
    =
    let globalModules =
        modules |> List.filter (fun m -> not m.IsSynthetic && m.Namespace = "")

    "index", toMdxPage (Pages.renderRootIndexPage links assemblies globalModules)

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
/// A sidebar entry, as plain F#.
///
/// The tree used to be built straight into Starlight's `jsNative` POJOs, which only run
/// under Fable - so the sidebar was the one output the .NET harness could not test, and
/// it drifted: its links used raw names where the pages used slugged anchors, leaving
/// every active pattern and operator entry pointing at nothing. Building a plain model
/// first makes the part that can be wrong testable.
type SidebarNode =
    | SidebarLeaf of label: string * href: string
    | SidebarBranch of label: string * children: SidebarNode list

/// The sidebar, derived from the same slugs and anchors the pages are written with.
let sidebarModel
    (links: LinkResolver)
    (outputBase: string)
    (label: string)
    (modules: Module list)
    : SidebarNode
    =
    let realModules = modules |> List.filter (fun m -> not m.IsSynthetic)
    let syntheticModules = modules |> List.filter (fun m -> m.IsSynthetic)
    let allNamespaces = namespacesOf modules

    // A generic type and its companion module share a slug and are merged onto one
    // page. The module already appears in the tree as a group, so drop the redundant
    // bare entity link that points to the same page.
    let realModuleSlugs = realModules |> List.map (fun m -> links.Slug m.FullName) |> Set.ofList

    let pageHref (fullName: string) = $"/{outputBase}/{links.Slug fullName}"

    let entityLeaf (entity: Entity) =
        SidebarLeaf(entity.Name, pageHref entity.FullName)

    let entityLeaves (entities: Entity list) =
        entities
        |> List.filter (fun e -> not (Set.contains (links.Slug e.FullName) realModuleSlugs))
        |> List.map entityLeaf

    let rec moduleBranch (m: Module) =
        let subModules =
            realModules |> List.filter (fun other -> other.Namespace = m.FullName)

        let href = pageHref m.FullName

        let functionLeaves =
            Declarations.anchoredFunctionSections m.Functions
            |> List.collect (fun (_, _, items) -> items)
            |> List.map (fun (f, anchor) -> SidebarLeaf(f.Name, $"{href}#{anchor}"))

        let valueLeaves =
            Declarations.anchoredValues m.Values
            |> List.map (fun (v, anchor) -> SidebarLeaf(v.Name, $"{href}#{anchor}"))

        let children =
            [
                SidebarLeaf("Overview", href)
                yield! m.Entities |> List.map entityLeaf
                yield! functionLeaves
                yield! valueLeaves
                yield! subModules |> List.map moduleBranch
            ]

        SidebarBranch(m.Name, children)

    let shortNameOf (fullName: string) =
        let lastDot = fullName.LastIndexOf('.')

        if lastDot < 0 then
            fullName
        else
            fullName.[lastDot + 1 ..]

    let rec namespaceBranch (ns: string) =
        // Skip child namespaces already represented by a real module.
        let directChildNs =
            allNamespaces
            |> List.filter (fun other ->
                other.StartsWith(ns + ".")
                && not (other.[ns.Length + 1 ..].Contains("."))
                && not (realModules |> List.exists (fun m -> m.FullName = other))
            )

        let children =
            [
                yield! realModules |> List.filter (fun m -> m.Namespace = ns) |> List.map moduleBranch

                for synthetic in syntheticModules |> List.filter (fun m -> m.Namespace = ns) do
                    // "global" synthetic modules hold bare namespace-level types.
                    // Inline them rather than nesting under a "global" sub-group.
                    if synthetic.Name = "global" then
                        yield! entityLeaves synthetic.Entities
                    else
                        SidebarBranch(shortNameOf synthetic.FullName, entityLeaves synthetic.Entities)

                yield! directChildNs |> List.map namespaceBranch
            ]

        SidebarBranch(shortNameOf ns, children)

    let topLevelNamespaces =
        allNamespaces
        |> List.filter (fun ns ->
            not (allNamespaces |> List.exists (fun other -> ns.StartsWith(other + ".")))
            // Skip namespaces that are also real modules - the module tree already shows
            // everything inside them.
            && not (realModules |> List.exists (fun m -> m.FullName = ns))
        )

    SidebarBranch(
        label,
        [
            SidebarLeaf("Overview", $"/{outputBase}")
            yield! realModules |> List.filter (fun m -> m.Namespace = "") |> List.map moduleBranch
            yield! topLevelNamespaces |> List.map namespaceBranch
        ]
    )

/// Converts the model to the POJOs Starlight expects. Deliberately trivial: everything
/// that can be wrong happens in `sidebarModel`, which is covered by the snapshots.
let sidebarTree
    (links: LinkResolver)
    (outputBase: string)
    (label: string)
    (modules: Module list)
    =
    let rec toItem node : SidebarItem =
        match node with
        | SidebarLeaf(label, href) -> U2.Case1(SidebarLink(label, href))
        | SidebarBranch(label, children) ->
            U2.Case2(SidebarGroup(label, children |> List.map toItem |> Array.ofList, collapsed = true))

    match sidebarModel links outputBase label modules with
    | SidebarBranch(label, children) ->
        SidebarGroup(label, children |> List.map toItem |> Array.ofList)
    | SidebarLeaf(label, href) -> SidebarGroup(label, [| U2.Case1(SidebarLink(label, href)) |])

/// Returns a tiny inline script that publishes the sidebar label so that
/// the external sidebar-controls.js file can find the right section without
/// needing any F#-generated JS logic.
let sidebarLabelInitScript (sidebarLabel: string) : string =
    let escaped = sidebarLabel.Replace("\\", "\\\\").Replace("'", "\\'")
    $"window.__fsharpSidebarLabel = '{escaped}';"
