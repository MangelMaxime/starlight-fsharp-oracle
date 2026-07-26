# Road to v1

Working checklist. Tick boxes as items land. This file is the durable state of the
effort - if a session ends mid-way, the next one resumes from here.

## How to run this

Phases are ordered by dependency, not by size. Do not reorder 1 -> 2 -> 3.

After each phase:

1. Run the gates (below).
2. Commit that phase on its own (Conventional Commits, scope = `oracle`, `render`,
   `plugin`, `build`, or `test`).
3. Tick the boxes here and commit this file with the phase.

If something is ambiguous and not covered here, add it under **Blocked** at the
bottom and keep working on everything else. Do not stall the run on one decision.

## Gates

Phase 1 creates the first two. Until then only `./build.sh docs` exists.

| Gate | Command | From |
|---|---|---|
| Unit/snapshot tests | `./build.sh test` | phase 1 |
| Link check | `./build.sh test --links` | phase 2 |
| Docs build | `./build.sh docs` | today |

"Done" = all three green, every box below ticked.

---

## Formatting reference

**When unsure how a declaration should be presented, do not invent it - generate it.**

`telplin` and `fantomas` are both in `dotnet-tools.json`. Telplin derives a signature
file from an implementation file; Fantomas formats it against this repo's
`.editorconfig`. The result is the canonical F# rendering of that construct, and it is
the tie-breaker for any layout question in phases 3 and 4.

```sh
dotnet telplin <path>.fsproj      # derive .fsi from .fs
dotnet fantomas <path>.fsi        # format to repo conventions
```

`tests/Reference/ActivePatterns.fsi` is exactly that output, kept in the tree as a
worked example. It is deliberately **not** in `Reference.fsproj` - it is documentation
for us, not a compilation unit. Read it before guessing at active-pattern rendering:

- `val (|Integer|): str: string -> int option` - parameter names are part of the
  signature
- `val (|DivisibleBy|_|): divisor: int -> n: int -> int option` - curried groups
- `val (|InRange|_|): min: int * max: int -> n: int -> int option` - tupled group
  uses `*`
- `val (|Timed|): f: (unit -> 'T) -> 'T * float` - function-typed parameter is
  parenthesised
- `val mutable a: int`

Regenerate it against any construct in doubt rather than reasoning from first
principles.

**One divergence already spotted:** Fantomas writes `val name: type` (no space before
the colon); the Oracle currently emits `val name : type`
(`Extractor/ValueExtractor.fs:78-79`, and the same in the member builders). The spaced
form is the older F#-tooling convention and may well be the right call for docs - but
make it a deliberate choice, not an accident. Logged as a phase 4 decision.

---

## Phase 1 - Make regressions visible

Nothing else on this list is safe to attempt without this. Almost every defect
below is a string-formatting defect, which is exactly what golden files catch.

### Fixture coverage

- [x] Add `MissingSyntaxes.fs` to `tests/Reference/Reference.fsproj` (was written but
      **not compiled** - measures, structs, exceptions, delegates, type extensions and
      `<see cref>` were all untested end to end)
- [x] Leave `tests/Reference/ActivePatterns.fsi` out of the fsproj on purpose - it is
      a **formatting reference**, not a fixture (see "Formatting reference" above).
      Listed as `<None>` with a header comment saying so.
- [x] Extend fixtures to cover what the report found missing and nothing covers yet -
      added as `tests/Reference/Coverage.fs`: settable property, auto-property with
      `get, set`, `[<Literal>]`, `[<RequireQualifiedAccess>]` union, class implementing
      an interface, abstract base class + inheriting class, overloaded `Format`,
      optional `?fallback` and `byref` parameters, `<list>` and `<typeparam>` XML doc

### Test harness

- [x] New `build/Commands/Test.fs`, registered in `build/Main.fs` alongside
      `DocsCommand`/`ServeCommand`
- [x] Snapshot the **JSON IR**: `tests/__snapshots__/reference.ir.verified.json`
- [x] Snapshot the **generated `.mdx`**: `tests/__snapshots__/pages/*.verified.mdx`
      (48 pages)
- [x] `--update` flag to rewrite snapshots, so intentional changes are a reviewable
      diff. Stale snapshots with no matching output are reported as failures too, so
      a page that stops being generated cannot pass silently.

**Decision made:** neither (a) nor (b) was needed. `Starlight.FSharp.Oracle.fsproj`
builds *and runs* under .NET as-is - the `jsNative` members are only reached by
`sidebarTree` and the plugin hook, not by page rendering. So the harness is a plain
.NET console project, `tests/Oracle.Tests`, driving the real extractor and the real
renderer. No test framework: it uses Verify's `.verified`/`.received` file convention
(already anticipated in `.editorconfig`) with a ~90-line comparer, which keeps the
central-package-management lock files untouched.

Not snapshotted: `Generate.sidebarTree`, which constructs `jsNative` POJOs and can
only run under Fable. Worth covering in Node later; noted under Blocked.

- [x] Extracted `Generate.allPages` from `Plugin.fs` so the plugin and the tests
      assemble pages through the same code path and cannot drift.

### Found while building the harness: output was non-deterministic

Not in the original plan. The first snapshot run failed against itself: entity members
came out in a different order on nearly every run, so `Counter`, `Vector2D` and `User`
churned. Cause: FCS gives no stable enumeration order for
`entity.MembersFunctionsAndValues` - it varies from process to process - and the old
`Seq.sortBy` only ranked by kind, leaving same-kind members in that unstable order.

This is a shipped bug, not just a test problem: every `astro build` could emit
different HTML for the same input, so anyone committing generated docs got spurious
diffs and builds were not reproducible. It also made the gate itself worthless, so it
had to be fixed here rather than deferred.

- [x] `Helpers.memberSortKey` - rank by kind, then name, then `XmlDocSig` (which
      encodes parameter types, so overloads order deterministically). Sort the FCS
      symbols before mapping, since the key needs `XmlDocSig`.
      (`Extractor/Helpers.fs`, `Extractor/EntityExtractor.fs`)
- [x] Verified stable across 5 consecutive runs.

---

## Phase 2 - Fix the dead links

All four of these are visible in the committed output under `docs/src/pages/api/`.
They are the most damaging defect class for a docs tool: a reader clicking a type
name lands on a 404.

Done via a `LinkResolver` (`Render/Links.fs`) built once from the IR and threaded
through the renderer. It answers two questions - "does this name have a page?" and
"what is its href?" - and undocumented names render as plain text rather than as a
promise of a page that was never written. This is the page-set lookup phase 3 absorbs:
the extractor's baked `TypeRef` url is now ignored, which is what lets phase 3 delete
it along with `--base`/`--output-base`.

- [x] **Foreign types link to nowhere.** Confirmed fixed in the snapshot diff:
      `Choice`, `DateTime`, `IDisposable`, `ICloneable`, `float`, `MeasureProduct`,
      `MeasureOne` all render as plain text now, while documented types in the same
      signatures (`Tree<'T>`, the `m` measure) stay linked.
- [x] **Synthetic `global` modules link to nowhere.** Synthetic modules are excluded
      from the documented set, and the entity-page breadcrumb omits a Parent it cannot
      link to, instead of pointing at `/api/reference-geometry-global`.
- [x] **Assemblies section on the index links to nowhere.** Now plain text, with a
      comment saying why. Generating per-assembly pages is a phase 6 option question,
      not a link bug.
- [x] **Empty namespace link.** The breadcrumb is assembled from the parts that exist,
      so a root-level type gets `Parent:` only rather than an empty `<a href="/api/">`.
- [x] Link-check gate: `./build.sh test --links` walks `docs/dist/**/*.html`, resolves
      every internal href against disk (accepting Astro's directory-style pages), and
      fails with a per-link report. Implemented in the existing .NET runner rather
      than a separate Node script, so there is one test entry point.
      **55 pages scanned, 0 broken.** Verified it fails on an injected bad link.

### Found while fixing: union members were rendered nowhere

`renderEntityPage` handled `Record`, `Class`, `Interface` and `Exception` members but
fell through to `()` for `Union`, even though the union's type header emits anchor
links for each member. Any union with members had a header full of `#Name` links to
ids that were never rendered - the same defect class, just page-local.

- [x] Unions now render member sections. Added a `Temperature` union with an instance
      and a static member to `Coverage.fs`, since no existing fixture had one and the
      fix would otherwise have been unverified.

---

## Phase 3 - Semantic IR (**review checkpoint - stop here**)

> **Stop and produce a diff for review at the end of this phase.** The shape of the
> new `TextNode` is a design call, not a mechanical one. Everything before and after
> this phase can run unattended; this one gets looked at once.

Presentation currently lives in the extractor:

- `TextNode` carries `OpenTag`, `CloseTag`, `OpenTagWithClass`, `Anchor`,
  `AnchoredProperty`, `AnchoredKeyword`, `Spaces of int`
  (`packages/FSharp.Oracle.Schema/Schema.fs:36-72`)
- `TypeRef` carries a **pre-computed URL**, so the Oracle takes `--base` and
  `--output-base` - Astro site config passed into a compiler tool
  (`packages/FSharp.Oracle/Program.fs:12-19`)
- `Function` carries pre-formatted `Declaration` / `AlignedDeclaration` / `ReturnType`
  with embedded `\n` and column alignment computed at
  `packages/FSharp.Oracle/Extractor/ValueExtractor.fs:22-31` - i.e. layout decided
  before anything knows the rendering font

Tasks:

- [x] `TextNode` reduced to semantics. Final shape: `Text | TypeRef of name * fullName
      | TypeVar | ParameterName | Keyword | Punctuation of string | Tick | Attribute |
      DeclarationName of text * anchor * role | Space | NewLine | Indent of levels |
      Node`. Gone: `OpenTag`/`CloseTag`/`OpenTagWithClass` (raw HTML), the baked url on
      `TypeRef`, `Spaces of int` (raw counts), and the three `Anchor*` cases.
      `DeclarationName` carries text and anchor separately because overloaded
      constructors all read `new` but anchor to `new`, `new-1`, ...
- [x] The Schema is now data, not presentation. Every `Declaration` /
      `AlignedDeclaration` field is gone, along with the five `Signature` fields that
      turned out to be dead payload - nothing consumed them. Entities gained
      `GenericParameters`, `Field` gained `LiteralValue`, and `Function`/`Value` gained
      `IsInline`/`IsMutable`, all of which the renderer previously received
      pre-formatted.
- [x] Link generation, indentation and column alignment now live in
      `Render/Declarations.fs`. Column alignment in particular could only move once the
      renderer had the parameter names, which it does.
- [x] Drop `--base` and `--output-base` from the Oracle CLI, and from the plugin's
      invocation of it. The extractor no longer knows anything about the site.
- [x] Collapsed the duplicated declaration builders. `memberDeclarationLine` and
      `buildMemberDeclaration` are now `memberPrefix` + `memberTypeNodes`, shared by
      `memberDeclaration` (standalone entry) and `memberHeaderLine` (type header).
      Phase 4's `with get, set` fix is now a one-line change instead of two.
- [x] `caseFieldNodes` and the exception field nodes are now `payloadFields`. They
      disagreed - unions write `a : int`, exceptions write `a: int` - so the shared
      function takes a `spaceBeforeColon` flag to keep output identical. Fantomas
      writes neither with a space, so phase 4's colon decision deletes the flag.
- [x] `valKeyword` and `constraintClause` shared between functions and values.
- [x] Renderer `<dl>` loop factored into `definitionList` (landed in phase 2).
- [x] Single `toSlug`: the extractor's copy is gone entirely, deleted along with
      `toUrl`. Only `Generate.fs` has one now.

### Result

The refactor is **byte-identical across 50 snapshots except for 4 pages**, and those 4
differ only by a stray trailing space that the old `stripGenericParamBrackets` emitted
for unconstrained generics like `<'T>`: it stripped the brackets and type variable,
found nothing left, and returned `Node [Space]` anyway. Dropped deliberately.

That near-zero diff is the evidence the refactor preserved behaviour. Secondary
effects worth noting:

- The IR for the fixture shrank from **1.5 MB to 0.5 MB** - two thirds of it was
  pre-rendered declaration text, much of it duplicated between a type's header and its
  members' own entries.
- `EntityExtractor.fs` went from **639 to 206 lines** and lost its nine-branch
  formatting chain, which also settles most of the phase 6 "split EntityExtractor"
  item. Extraction total: 1710 -> 1054 lines.

Snapshots will churn heavily here. Review the snapshot diff as part of the checkpoint -
it is the clearest evidence of what actually changed.

---

## Phase 4 - F# feature gaps

Ordered by how wrong the output is today.

### Wrong output

- [x] **Property setters.** `Member` now carries `HasGetter`/`HasSetter`, and
      properties render `with get`, `with set` or `with get, set`. Verified against
      `User.DisplayName` and `User.IsActive` (both `get, set`) and `Counter.Count`
      (`get` only).
- [x] **Overload anchors collide.** Replaced the constructor-only hack with
      `Anchor.assign`, which suffixes any repeat `-1`, `-2`. `Formatter` now anchors
      `Format` and `Format-1`. The extractor no longer renames constructors: anchoring
      is a page concern and lives in the renderer.
- [x] **Undocumented parameters vanish.** Now gated on there being parameters, not on
      there being parameter docs. Unnamed parameters - the `()` of `let timestamp ()` -
      render as the type alone rather than as a headless `: unit`.
- [x] **Type-level generic constraints dropped.** Entities carry their rendered
      generic parameters, so the type head reads
      `type SortedBag<'T when 'T : comparison>`. No fixture had a constrained generic
      *type* (only constrained functions), so `SortedBag` was added to cover it.
- [x] **`Function` vs `Value` split.** Now partitioned on having parameter groups
      rather than on `IsFunction`, which is the question actually being asked: a
      binding with no named parameters has no parameter table to lose, so `Value` is
      the right home for it.
- [x] **Active patterns.** They now get their own "Active Patterns" section rather
      than sitting among ordinary functions, and anchor cleanly (`Integer`,
      `Positive-Negative-Zero`).

      Checking the formatting reference first was worth it: the *signatures* were
      already right. `ActivePatterns.fsi` shows Fantomas writing
      `val (|EvenInt|_|): str: string -> int option`, so the `option` return on a
      partial pattern is correct, not the defect the plan assumed. Only the section
      and the anchors needed work.
- [ ] **Colon spacing.** `val name : type` today vs `val name: type` from Fantomas.
      Decide once, apply everywhere (`ValueExtractor.fs:78-79`, both member builders,
      field/case/parameter declarations), record in the decisions log.
- [x] **Anchors are not escaped.** `Anchor.slug` keeps identifier characters and
      collapses the rest to `-`, dropping wildcard-only segments. Operators anchor on
      their compiled name, since `(+)` cannot be a URL fragment: `(*)` -> `op_Multiply`,
      `(|Positive|Negative|Zero|)` -> `Positive-Negative-Zero`.

### Missing information

- [ ] **Inheritance and interface implementations.** `entity.BaseType` and
      `entity.DeclaredInterfaces` are never read anywhere in the codebase. A class
      page never states what it inherits or implements, and interface
      implementations get mixed into "Methods" unmarked.
- [ ] **Attributes.** Only `[<Struct>]` and `[<Measure>]` render. Add at minimum
      `[<RequireQualifiedAccess>]`, `[<AutoOpen>]`, `[<Literal>]`, `[<CLIMutable>]`,
      `[<Sealed>]`, `[<AbstractClass>]`, `[<Extension>]`. `[<RequireQualifiedAccess>]`
      matters most - it changes how callers write code.
- [ ] **Literal values.** `[<Literal>] let X = 42` renders `val X : int`, dropping
      the value.
- [ ] **Type extensions / extension members.** `type Foo with ...` and `[<Extension>]`
      methods are extracted as ordinary module functions and never attached to the
      extended type.
- [ ] **Parameter modifiers.** `extractParameter`
      (`Extractor/ParameterExtractor.fs:8`) ignores `IsOptionalArg` (`?x`),
      `IsInArg`/`IsOutArg` (byref/inref/outref) and `[<ParamArray>]`.
- [x] **`inline` on members.** Renders as `member inline Largest : ...`; fixture
      added, since none existed.
- [ ] **Events, indexed properties (`Item`), explicit interface implementations.**

### XML documentation

- [ ] **`<see cref>` does not link.** `resolveCref`
      (`Extractor/Helpers.fs:131`) is written but has zero call sites - it is dead
      code. `MemberRef` renders as `` ``Name`` `` code text instead of a hyperlink.
- [ ] **`<list>` is a no-op.** `HandleMicrosoftOrList = id` at
      `packages/FSharp.Oracle/XmlDoc.fs:266`, so list markup passes through raw and
      then gets escaped into visible tags.
- [ ] **`<typeparam>`** - no field for it on `XmlDoc` in the Schema, needs adding.
- [ ] **`<exception>`, `<seealso>`, `<value>`** - not extracted.
- [ ] `<inheritdoc>` - decide in or out for v1.

---

## Phase 5 - Robustness

- [ ] **Slug collisions are silent.** `toSlug` lowercases and strips generic arity,
      so `MyType`/`Mytype`, and identically-named types across two assemblies,
      overwrite each other on disk with no warning. Detect and report; decide a
      disambiguation policy (assembly prefix?). The generic-type-plus-companion-module
      case is already handled, but as a special case
      (`Generate.fs:164-185`) rather than by a general rule.
- [ ] **One bad entity kills the build.** `failwithf "Could not load assembly: %s"`
      (`Extractor/Assembly.fs:67`) has no diagnostics; any throwing entity takes the
      whole Astro build down. Degrade per-entity with a warning instead.
- [ ] **FCS runs once per dll.** `Program.fs:52` maps `extractAssembly` over
      `dllPaths` and each call does its own `GetProjectOptionsFromScript` +
      `ParseAndCheckProject`. Six assemblies means six full checks of the same
      reference set. Hoist the project context out of the loop.
- [ ] **IR serialization cost.** Pretty-printed at indent 4 (`Program.fs:63`) through
      a 50 MB stdout buffer (`Plugin.fs:49`). Use indent 0, or a temp file.
- [ ] **O(n^2) page filtering.** `List.contains` over lists in `Plugin.fs:227,236`
      and `Generate.fs:257`, once per page. Use a `Set`.
- [ ] `Starlight.FSharp.Oracle/Helpers.fs` defines a `FileOperationResult` DU whose
      cases are named `Ok`/`Error`, shadowing `Result` in every file that opens it.
      Use `Result<unit, string>`.

---

## Phase 6 - Syntax highligthing

- [ ] Make a pass on the code generation to improve syntax coloration. The goal is to have as good coloration as in IDE.

    Either you are able to find the information yourself or you can perhaps use tree-sitter from https://github.com/MangelMaxime/tree-sitter-fsharp/ to request tokens color candidates for the differents type of constructions.

    You can clone the repo in a temp folder, and use tree sitter queries to test the different construction we support.

---

## Phase 7 - Ship

- [ ] **README `## Usage` currently says `TODO`.** Write a getting-started that
      actually works, plus a full option reference.
- [ ] **Plugin options.** Today only `output`, `assemblies`, `sidebar.label`. Decide
      the v1 surface: include/exclude filters, document-internals toggle, per-assembly
      grouping, external-type link base, sidebar collapse defaults.
- [ ] **Distribution decision.** `postinstall` runs `dotnet publish` on every consumer
      install, requiring the .NET SDK on any machine that installs the npm package -
      including Node-only CI images. Pick one: ship prebuilt per-RID binaries, ship a
      `dotnet tool` the user installs explicitly, or keep it and document the
      requirement loudly. Blocking for 1.0.
- [ ] Namespace convention in the Oracle is inconsistent: `module Oracle.XmlDoc`,
      `module FSharp.Oracle.Program`, `module FSharp.Oracle.Extractor` (for
      `Assembly.fs`), and `namespace FSharp.Oracle` + `module internal X` elsewhere.
      Pick one.
- [ ] `XmlDoc.fs` sits at the Oracle project root while every other extraction file
      is under `Extractor/`. Move it in.
- [ ] Rename `Extractor/Assembly.fs` -> `Extractor/Extract.fs` (it is the entry
      point, not a peer of the other extractors).
- [ ] Split `EntityExtractor.fs` (639 lines, a nine-branch `if/elif` chain). It is
      where base types, attributes and extension members all had to land in phase 4 -
      one file per entity kind, or at minimum one named function per branch.
- [ ] `Render.fs` is a 19-line re-export shim over `Render/*`. Either drop it, or make
      it the sole public surface and mark the rest `internal`.
- [ ] Remove stray artifacts: tracked `starlight-fsharp-oracle-0.1.0.tgz`, leftover
      `docs/src/pages/prototype.astro` and `docs/src/pages/test/api.astro`, empty
      `guides/`.

---

## Blocked

Questions that came up mid-run and need a human answer. Keep working around these.

- **Sidebar tree is not snapshotted.** `Generate.sidebarTree` builds `jsNative` POJOs,
  so it only runs under Fable and the .NET harness cannot cover it. Options: a small
  Fable-compiled Node harness, or make the sidebar types plain records that Fable
  erases. Not urgent - the sidebar has had no reported defects - but it is the one
  untested output. (raised phase 1)

---

## Decisions log

Record choices made during the run so later phases do not relitigate them.

| Phase | Decision | Choice |
|---|---|---|
| 1 | Renderer snapshot harness (.NET vs Node) | .NET console project against the real fsproj; no split needed, no test framework, Verify file convention |
| 1 | Entity member ordering | kind, then name, then `XmlDocSig` - FCS order is not stable across processes |
| 2 | External types: plain text vs MS Learn links | Plain text for v1. Linking out needs a per-source URL scheme (MS Learn for BCL, nothing for arbitrary third-party assemblies) - that is an option-surface question, deferred to phase 6 |
| 2 | Link check location | Inside the .NET runner, not a separate Node script - one test entry point, and it can reuse the page set later |
| 3 | Final `TextNode` shape | Token stream, no HTML/urls/space-counts. `Punctuation of string` rather than a case per mark; `DeclarationName` carries text and anchor separately for overloaded constructors |
| 4 | Colon spacing: `val name : t` vs Fantomas `val name: t` | _tbd - also deletes `payloadFields`' `spaceBeforeColon` flag_ |
| 5 | Slug collision disambiguation policy | _tbd_ |
| 6 | Distribution: prebuilt binaries vs dotnet tool vs SDK requirement | _tbd_ |
