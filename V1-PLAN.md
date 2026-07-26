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
- [x] **Colon spacing.** Settled: the colon attaches to the name everywhere, except in
      the aligned parameter block of a function, where padding fills the gap so the
      colons form a column:

      ```
      val combineWith:
          combine : 'T -> 'T -> 'U
          first   : 'T
          second  : 'T
                  -> 'U
      ```

      The column now comes from the **parameter names alone**. Including the function
      name - what the old layout did - made the gap depend on something unrelated to
      what is being aligned. Measured across the fixture: 66 of 75 functions with
      parameters had padding driven by the name rather than the parameters, median 5
      blank columns for ordinary functions and up to 38 for active patterns. That is
      the readability defect, and it was never an active-pattern-only problem.

      This also deletes `payloadFields`' `spaceBeforeColon` flag: the union/exception
      disagreement disappears rather than being preserved behind a parameter.

### Found while re-laying out: the constraint clause was reverse-engineered

Not in the plan, and pre-existing - visible in the committed snapshot as
`-> 'U , 'U` on `combineWith`. The renderer recovered the `when ...` clause by
stripping `<`, the leading type variable and `>` back off the *rendered* generic
parameter list. That only works for a single type parameter; `<'T, 'U>` left a stray
`, 'U` behind. Moving the layout onto its own line is what made it obvious.

- [x] The extractor now emits the clause directly (`renderConstraints`), and the
      renderer's text-stripping is gone. `Function`, `Value` and `Member` carry
      `Constraints` rather than the whole bracketed list, since the clause is all the
      renderer ever used them for. Entities keep the combined form, because a type head
      does write its constraints inside the brackets.
- [x] `inherit obj` was leaking onto every class: `obj` is a type abbreviation and
      `FullName` throws for those, so the full-name-only filter never matched.

### Long signatures

- [x] The constraint clause breaks onto its own line, and again at each `and`, which is
      where F# breaks it too. The widest signature line in the fixture went from **110
      characters to 63** - and the 110 was a constraint, not a type.
- [x] `.fsharp-doc-sig` switched from `white-space: pre-wrap` to `white-space: pre` with
      `overflow-x: auto`. A wrapped line restarted at column 0 and destroyed the
      alignment; now anything still too wide scrolls in its own box, the way code blocks
      do elsewhere on the page.
- [x] **Anchors are not escaped.** `Anchor.slug` keeps identifier characters and
      collapses the rest to `-`, dropping wildcard-only segments. Operators anchor on
      their compiled name, since `(+)` cannot be a URL fragment: `(*)` -> `op_Multiply`,
      `(|Positive|Negative|Zero|)` -> `Positive-Negative-Zero`.

### Found while fixing: remarks were dropped everywhere but entity pages

`renderDocumentationBlock` rendered summary, extras and examples but never `<remarks>`,
so a function's, member's or value's remarks were silently discarded - only entity
pages showed them. Fixed.

### Missing information

- [x] **Inheritance and interface implementations.** Type heads now carry
      `inherit Base` and `interface IFoo` lines. `obj` base types are dropped as
      noise, as are the interfaces F# derives for records and unions
      (`IEquatable`, `IComparable`, `IStructural*`) - the author did not write those
      and they would crowd out the ones who did.
      Explicit interface *implementations* are still unmarked among the methods.
- [x] **Attributes.** Rendered above the declaration, with constructor arguments.
      Filtered by denylist rather than allowlist, so user attributes appear without
      being enumerated: compiler-inserted ones (`CompilationMapping`,
      `CompilationRepresentation`, `CompiledName`), `System.Diagnostics.*`,
      `System.Runtime.CompilerServices.*`, and the three already shown another way
      (`Struct`, `Measure`, `Obsolete`). Verified the fixture emits exactly
      `[<Measure>]`, `[<Struct>]`, `[<RequireQualifiedAccess>]`, `[<AbstractClass>]`
      and no noise.
- [x] **Literal values.** `val MaxRetries : int = 3`, and
      `val DefaultGreeting : string = "hello"` - strings quoted and chars ticked, since
      an unquoted literal reads as an identifier.
- [x] **Type extensions / extension members.** The premise was only half right.
      *Intrinsic* extensions - `type StructPoint with ...` in the same file - already
      arrived as members of the extended type; FCS attaches them itself. Only
      *optional* extensions, declared in another module, landed among that module's
      functions.

      Those are now extracted as `Module.ExtensionMembers`, carrying the extended
      type, and rendered in an "Extension Members" section on the extended type's page
      when it has one, labelled with the module that declares them - a reader needs to
      know what to open for them to be in scope. When the extended type has no page
      (`System.String`), they stay on the declaring module's page rather than vanish.

      Each group opens with its own declaration block, the way an entity page does:

      ```
      type String with
          member Shout: unit -> string
      ```

      Grouped by extended type, so a module that extends several types gets one block
      each.
- [x] Attributes were double-spaced: `.fsharp-doc-attr` was `display: block` with a
      margin *and* the signature emits a line break after an attribute, so the two
      stacked. The CSS no longer inserts a break the token stream already describes.
- [x] **Parameter modifiers.** Optional parameters render `?fallback : int` rather
      than `fallback : int option`, the option being what the `?` means. byref already
      rendered correctly (`result : byref<int>`). `[<ParamArray>]` is still not marked;
      no fixture covers it.
- [x] **`inline` on members.** Renders as `member inline Largest : ...`; fixture
      added, since none existed.
- [x] **Events.** A `[<CLIEvent>]` member produced *three* entries: the event plus its
      `add_X`/`remove_X` accessors, all as plain methods. Events now have their own
      `MemberKind` and section and render as `event Tick : IEvent<int>`, with the
      accessors filtered out.

      Two traps here, both worth remembering: FCS does not report these via `IsEvent`,
      so detection is by `IEvent<_>` return type - and `IEvent<'T>` is a type
      abbreviation, so `FullName` throws and a full-name-only check silently fails, the
      same way `inherit obj` slipped through. And the event's *logical* name is
      `get_Tick` while its accessors are `add_Tick`/`remove_Tick`, so matching has to be
      on the display name.
- [x] **Indexed properties** already render correctly: `property Item : index : int ->
      int with get`. Fixture added to keep it that way.
- [ ] **Explicit interface implementations** are still not listed. They are currently
      omitted entirely rather than mislabelled, which is defensible - the `interface
      INamed` line already states the type implements it, and the members are reachable
      only through that interface. Left as a deliberate omission rather than a fix.

### Found while fixing: tupled parameters rendered as curried

Not in the plan. `Parameter list list` encodes curried groups (outer) and tupled
parameters within a group (inner), but every renderer flattened it with
`List.collect id` and joined the lot with `->`. So a .NET-style
`Format(value, digits)` was documented as `value : float -> digits : int -> string`,
which is not what a caller writes and is not valid for that method.

The formatting reference settles it: `ActivePatterns.fsi` has both forms side by side -
`val (|DivisibleBy|_|): divisor: int -> n: int -> int option` (curried) against
`val (|InRange|_|): min: int * max: int -> n: int -> int option` (tupled).

- [x] Groups are separated by `->`, parameters within a group by `*`. `Parse` now reads
      `input : string * ?fallback : int -> int`.
- [x] In the aligned multi-line function layout, a tupled group stays on one line so
      its `*` cannot be mistaken for currying. The one-parameter-per-group case, which
      is nearly all F# functions, is unchanged and still column-aligned.

### XML documentation

- [x] **`<see cref>` does not link.** The extractor now emits
      `[`Name`](fsharp-doc:Ns.Type)` and the renderer resolves that scheme through the
      same `LinkResolver` as everything else, so an undocumented target keeps its text
      and loses the link. `M:`/`P:`/`F:` refs resolve to their declaring type, which is
      the closest thing with a page. The old dead `resolveCref` is gone.

      The link-check gate earned its keep here: module-page summaries went through a
      different escape path that skipped resolution, so two literal `fsharp-doc:` hrefs
      reached the built site. Caught and fixed.
- [x] **`<list>` is a no-op.** Bullet and numbered lists become Markdown lists;
      table-style lists become `term - description` lines, which is as close as
      Markdown gets without building a table.
- [x] **`<typeparam>`** - added to `XmlDoc` and rendered as a "Type parameters"
      block, in the same shape as parameters, because that is what they are.
- [x] **`<exception>`, `<seealso>`, `<value>`** - all three extracted and rendered.
      Exception types and see-also targets link when they have a page and degrade to
      text when they do not; both display without the generic arity suffix.
- [x] `<inheritdoc>` - **out for v1.** Resolving it means walking to the base member
      or interface member and merging its docs, across assemblies that may not be in
      the documented set. That is a feature, not a gap, and nothing in the fixtures
      uses it. Recorded in the decisions log.

---

## Token vocabulary (done before phase 5)

Prompted by spotting `| TextNode.Keyword "and" ->` in the renderer: a layout decision
driven by matching a string literal, which fails silently on a typo.

- [x] **`Punctuation of Symbol`.** A closed union of 15 symbols. The renderer decides
      something per symbol - `<`, `>` and `*` need escaping, the rest do not - and the
      old string lookup passed anything it did not recognise through unescaped. The
      match is now exhaustive, so a new symbol cannot be added without saying how it
      escapes. It also resolved the inconsistency where `|`, `;` and `:>` were sometimes
      `Keyword` and sometimes `Text` depending on the file.
- [x] **Keywords stay strings, with `[<Literal>]` constants.** Every keyword renders
      identically, so a union would be two dozen cases feeding one branch. The literals
      give typo safety at the construction sites and still work as patterns.
- [x] **Deleted the pattern match rather than making it safer.** `Constraints` is now a
      list with one entry per constraint, so the renderer lays them out structurally and
      never looks for the `and`s in token text.
- [x] Same fix for the other content-sensitive match: `Parameter.IsUnit` is decided from
      the FCS type instead of matching `TextNode.Text "unit"` in the rendered output.
- [x] Normalized compound keywords. `"val inline"`, `"static member"` and `"not null"`
      are separate tokens now, and `" of"` no longer carries a literal leading space
      inside its own span - which MDX could strip, mashing `Error of` into `Errorof`.
      That was a live bug, visible in the snapshot diff.

Zero content-sensitive matches on tokens remain.

## Phase 5 - Robustness

- [x] **Slug collisions are silent.** There is now one authoritative name-to-slug map,
      built once and used for both the pages that get written and the links that point
      at them, so the two cannot disagree. Colliding names are pulled apart
      deterministically (sorted, first keeps the plain slug, rest get `-2`, `-3`), and
      the collision is reported through the Astro logger, naming the URL each type
      received - knowing there is a clash is much less use than knowing which page
      moved.

      Types and modules are slugged separately, which preserves the one *intentional*
      collision - a generic type and its companion module share a slug and are merged
      onto one page - while still separating two types that collapse together.

      Fixture added: `Casing` and `CASING` differ only by case. They now get
      `reference-coverage-casing` and `reference-coverage-casing-2`, with the warning
      naming both, instead of one silently overwriting the other on disk.
- [x] **One bad entity kills the build.** `tryExtract` wraps each entity, module,
      function and value: on failure it warns to stderr and skips that one item rather
      than taking the whole documentation build down. The "could not load assembly"
      message now names what it looked for and lists what it resolved, so a typo is
      distinguishable from a missing reference.
- [x] **Assembly paths are validated up front.** A bad argument used to surface as
      `The value cannot be an empty string (Parameter 'path')` from inside a directory
      scan, naming neither the argument nor the problem.
- [x] **FCS runs once per dll.** Split into `resolveAssemblies` (one project check of
      the shared reference set) and `extractAssembly` (one pass per target). Six
      assemblies used to mean six full checks of the same references.
- [x] **IR serialization cost.** `Encode.toString 0` rather than indent 4: the IR goes
      down a pipe to the plugin, not to a reader. The fixture's IR went from **0.59 MB
      to 0.12 MB**, roughly a fifth of the size, which is also a fifth of the parse
      cost at the other end.
- [x] **O(n^2) page filtering.** The remaining `List.contains` scans in `Generate.fs`
      are `Set` lookups.
- [x] `FileOperationResult` is gone; `Result<unit, string>` replaces it, so `Ok` and
      `Error` no longer shadow the built-ins in every file that opens `Helpers`.

## Phase 6 - Syntax highlighting

Grounded in `tree-sitter-fsharp`'s `queries/highlights.scm` rather than invented scope
names, so the colouring follows the grammar's own captures.

The token model was already fine-grained enough - `Symbol`, `Keyword`, `TypeRef`,
`TypeVar`, `ParameterName`, `DeclarationName` are distinct. Everything wrong was in the
mapping from tokens to CSS classes, which collapsed six distinctions into three colours
plus one hardcoded hex.

| Symbol | grammar capture | was | now |
|---|---|---|---|
| `:` `,` `;` | `@punctuation.delimiter` | keyword | neutral foreground |
| `(` `)` `{` `}` | `@punctuation.bracket` | keyword | neutral foreground |
| `=` `<` `>` `*` `:>` `?` | `@operator` | keyword | `keyword.operator` |
| `->` `\|` | `@keyword.control` | keyword | keyword - already right |

- [x] **Punctuation is no longer keyword-coloured.** This was the loudest mismatch:
      `new:` and `-> EntityBase` were as red as `type` and `abstract`. Delimiters and
      brackets now resolve to scopes themes generally leave undefined, so they fall
      through to the editor foreground - which is what an IDE does.
- [x] **`->` and `|` keep the keyword colour.** The grammar captures them as
      `@keyword.control`, not operators. Worth checking rather than assuming: the
      "obvious" fix would have made them neutral and been wrong.
- [x] **Primitive types are coloured.** `int`, `string` and `unit` are abbreviations,
      so `FullName` throws and they fell back to plain `Text` - uncoloured beside user
      types on the same line. The same abbreviation trap as `obj` and `IEvent<'T>`,
      now the third time it has bitten.
- [x] **Parameter names have their own colour.** They were borrowing the type-variable
      class as a stopgap from phase 3.
- [x] **Member names are theme-derived.** `.fsharp-doc-property` was a hardcoded
      `#0a86ff`, ignoring the reader's light/dark theme and the site's Expressive Code
      theme entirely.
- [x] **Attributes are theme-derived** rather than a fixed Starlight grey.
- [x] Dropped `--fsharp-doc-fn`, which was generated but mapped to no token.

### Second pass: declared names carried no class at all

Only *referenced* types were coloured. Every name in declaration position - the `Foo`
in `type Foo`, `val foo`, `member Foo`, a record field, a union case - was plain
`TextNode.Text`, so it rendered black. `TextNode.TypeVar` turned out to be emitted in
exactly one place: type variables were built as a tick beside plain text, which is why
`'T` was black next to a purple `list`.

- [x] `DeclaredName of text * role` covers every declaration-position name, with `Type`
      and `Function` added to `DeclarationRole`. A name now colours the same whether it
      is being declared or referred to.
- [x] `TypeVar` carries its sigil (`'T`, `^T`) as one token, so the whole thing colours
      as one rather than leaving the tick outside the span.
- [x] Parameter names in the aligned function layout - the one place a parameter still
      lost its colour, since that branch used plain text while member signatures used
      `ParameterName`.
- [x] Union case and exception payload field names, SRTP member names in constraints,
      and anonymous record field names.
- [x] `Literal` tokens for `[<Literal>]` values and enum case values, which rendered as
      bare text.
- [x] A space before the colon after a declared name, so `val tryFirst :` reads the same
      as the `items : 'T seq` rows beneath it.

Verified by sweeping every signature block in all 57 snapshots for text outside a
`<span>`: **zero uncoloured tokens remain**.

Type parameters intentionally still share the type colour: most themes define no
separate scope for them, and falling back says so honestly rather than inventing one.

## Fixture consolidation (done alongside phase 7)

The fixture had grown to 1407 lines across 10 files, organised by theme rather than by
what it covers - 22 active patterns where 4 shapes suffice, and the same tree helpers
rewritten in several files.

- [x] **A construct-coverage report, snapshotted.** `tests/Oracle.Tests/Coverage.fs`
      counts every construct the generator handles - entity kinds, member kinds,
      parameter shapes, each generic constraint, each XML doc tag, each page-structure
      case - and the result is a snapshot. Cutting the fixture down cannot silently drop
      a construct, because the count reaching zero is a visible diff. A count of zero
      also fails the run outright, so `--update` cannot accept the loss quietly.
- [x] **Rewrote the fixture around what it covers**: `Global`, `Namespaces`, `Types`,
      `Members`, `Functions`, `Docs`, with the fsproj naming what each one is for.
      **1407 lines -> 429**, 10 files -> 6, 60 pages -> 35, with **no construct lost**.
- [x] The report found a real gap on its first run: nothing exercised
      `when 'T : (new : unit -> 'T)`. Now covered.
- [x] **Bodies are `failwith "fixture"`.** The generator reads signatures and
      documentation from compiled metadata, so implementations were noise that invited
      the reader to wonder whether behaviour was under test. It also forces an explicit
      return type on everything, which turns the fixture into a signature spec.

      Kept where the language requires a value: `[<Literal>]` bindings, enum case
      values, and class-level `let mutable` initialisers.

      The risk was SRTP: `static member (+)` was previously *inferred* from a
      `List.fold (+)` body, and a stub would have dropped it. Writing the constraint
      explicitly keeps it, and the coverage report is what confirms FCS still reports
      it - exactly the case the report exists for.
- [x] **Declarations are named for what they cover** - `UnionWithCaseFieldsAndMembers`,
      `constrainedByDefaultConstructor`, `RecordWithDocumentedFields` - so the fixture
      reads as a list of what is exercised rather than as a small library.

      Name length is deliberately varied, from `m` (1 character) to
      `(|MultiCasePositive|MultiCaseNegative|MultiCaseZero|)` (53), because the aligned
      signature layout is driven by name lengths: uniform names would stop exercising
      it, and the 38-column river found in phase 4 only showed up because a name was
      long.

### Found while adding backticked-name coverage: slugs kept their spaces

F# allows `` ``Type With Spaces`` ``, and nothing in the fixture used one. `toSlug`
replaced only `.`, so such a name reached the filename and the href intact:
`/api/reference-types-type with spaces`. `Anchor.slug` had always collapsed
non-identifier characters, so page anchors were fine and only page slugs broke.

- [x] `toSlug` reuses `Anchor.slug` and folds case, after stripping the generic arity
      suffix (collapsing punctuation first would leave `tree-1` behind). No churn on
      existing names - the fix is behaviour-preserving for identifiers.
- [x] Record fields, enum cases and union cases were anchoring on their raw name,
      bypassing the slugging members already went through, so
      `` ``Field With Spaces`` `` put spaces into an `id` and an `href`. They now go
      through `Anchor.assign` like members.
- [x] Fixture covers a backticked module, type, record field, member and function, with
      a coverage line so it cannot be dropped.

Verified: no anchor value, href fragment or page slug contains a space.

### Found while consolidating: generated pages were never cleaned

Removing fixture modules left their pages behind in `docs/src/pages/api`, and the site
kept building them - 86 pages on disk for 35 generated. The plugin only ever wrote
files, so a renamed or deleted type kept its page forever: stale, unreachable from the
sidebar, and still published.

- [x] The output directory is cleared of `.mdx` before writing. 35 pages generated, 35
      on disk.

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
- [x] Renamed `Extractor/Assembly.fs` -> `Extractor/Extract.fs`.
- [x] Split `EntityExtractor.fs`: one named `extract*` function per entity kind,
      dispatched from a chain that now reads as a list of kinds. A `Common` record
      carries the parts every kind shares, so each function holds only what is specific
      to it. (Phase 3 had already taken the file from 639 lines to 206.)
- [x] Dropped `Render.fs`. It forwarded four functions and three types within a single
      assembly, adding a hop without hiding anything.
- [x] Removed the tracked `.tgz` (and gitignored `*.tgz`), `prototype.astro`,
      `test/api.astro`, and the empty `guides/`.

---

## Blocked

Questions that came up mid-run and need a human answer. Keep working around these.

- ~~**Sidebar tree is not snapshotted.**~~ **Resolved** - and it had drifted, exactly as
  the entry feared. `sidebarTree` reimplemented linking instead of using the resolver
  and anchor helpers the pages use, so its fragments were raw names: every active
  pattern and operator entry had pointed at a non-existent anchor since phase 4, and
  backticked names put spaces in the href. Module links also used `toSlug` directly,
  bypassing collision disambiguation.

  Split into `sidebarModel` (plain F#, snapshotted) and a trivial POJO conversion, with
  the anchor assignment shared with the pages via
  `Declarations.anchoredFunctionSections`. The test cross-checks that every sidebar
  fragment matches a rendered anchor. (raised phase 1, fixed after phase 6)

---

## Decisions log

Record choices made during the run so later phases do not relitigate them.

| Phase | Decision | Choice |
|---|---|---|
| 1 | Renderer snapshot harness (.NET vs Node) | .NET console project against the real fsproj; no split needed, no test framework, Verify file convention |
| 1 | Entity member ordering | kind, then name, then `XmlDocSig` - FCS order is not stable across processes |
| 2 | External types: plain text vs MS Learn links | Plain text for v1. Linking out needs a per-source URL scheme (MS Learn for BCL, nothing for arbitrary third-party assemblies) - that is an option-surface question, deferred to phase 6 |
| 2 | Link check location | Inside the .NET runner, not a separate Node script - one test entry point, and it can reuse the page set later |
| 3 | Final `TextNode` shape | Token stream, no HTML/urls/space-counts. `DeclarationName` carries text and anchor separately for overloaded constructors. (`Punctuation` was `of string` here; later typed as `Symbol` once it turned out the renderer decides escaping per symbol) |
| 4 | Colon spacing | Attached to the name, except in the aligned parameter block where padding forms a colon column. Alignment driven by parameter names only |
| 6 | Colon spacing, revised | A space before the colon after a declared name too, so the header row reads the same as the aligned rows below it |
| 4 | Layout as a plugin option? | No. 66 of 75 functions were affected, so this is the default's problem, not a preference. An option costs two layouts, two snapshot sets, and permanent API |
| 4 | Long signatures | Break the constraint clause at `when`/`and`, and scroll rather than wrap |
| 4 | Extension members placement | On the extended type's page when it has one, labelled with the declaring module; on the module's page otherwise |
| 4 | Explicit interface implementations | Omitted, not listed. The `interface` line already states it, and they are reachable only through the interface |
| 4 | `<inheritdoc>` | Out for v1: needs cross-assembly base-member resolution, and nothing uses it |
| 4 | `<see cref>` transport | Extractor emits a `fsharp-doc:` markdown link; the renderer resolves it. Keeps the IR free of URLs while still recording what was referenced |
| 5 | Slug collision disambiguation policy | Deterministic suffixes: sort the names, first keeps the plain slug, rest get `-2`, `-3`. Types and modules slugged separately so the intentional type+companion-module merge survives |
| 6 | Syntax colours source | `tree-sitter-fsharp`'s `highlights.scm` captures, mapped to TextMate scopes, rather than invented scope names |
| 7 | Distribution: prebuilt binaries vs dotnet tool vs SDK requirement | _tbd_ |
