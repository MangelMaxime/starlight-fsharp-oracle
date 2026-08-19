---
last_commit_released: f3695b2f9e2f106e1688855ee54cda3fd6cb3672
name: starlight-fsharp-oracle
updaters:
  - package.json:
      file: package.json
---

# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This changelog is generated using [EasyBuild.ShipIt](https://github.com/easybuild-org/EasyBuild.ShipIt).

⚠ Only edit the front matter metadata at the top of this file. All other changes will be overwritten when a new release is created.

## 0.5.0 - 2026-08-19

### 🚀 Features

* *(render)* Break a member signature that would not fit on one line ([806211d](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/806211d5a66272f6f65fb8b663a8b53ae160f363))

### 🐞 Bug Fixes

* *(extractor)* Stop parenthesising the range of a function type ([f3695b2](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/f3695b2f9e2f106e1688855ee54cda3fd6cb3672))

<strong><small>[View changes on Github](https://github.com/MangelMaxime/starlight-fsharp-oracle/compare/567877550ac3b53142a7c678879ab94817f87743..f3695b2f9e2f106e1688855ee54cda3fd6cb3672)</small></strong>

## 0.4.0 - 2026-08-18

### 🚀 Features

* *(oracle)* Surface inheritance, attributes, literals and parameter modifiers ([8a42add](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/8a42add1a5a4e34de25acc0489ebf3b043dc9cad))
* *(oracle)* Make XML doc comments carry their full content ([cc947ad](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/cc947ad98d816171ac3ad04d33d7961cb486ac41))
* *(oracle)* Document events and optional type extensions ([847f659](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/847f659a1ba44f29c01c7d3a18d15671e0b0cf18))
* *(render)* Colour signatures from the tree-sitter grammar's captures ([1d968ed](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/1d968edf6dacca834393e179a969de136490e36b))

### 🐞 Bug Fixes

* *(oracle)* Make slug collisions visible and extraction resilient ([23e51de](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/23e51de0f694f47e965ab9c969a973f01c643c18))
* *(plugin)* Derive sidebar links from the same slugs and anchors as pages ([3789146](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/3789146a89168b1ec4f5a161a821c1b4b6584597))
* *(render)* Only link types that have a page ([5b9fdf0](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/5b9fdf03c927e4133a79a0cdada7cd1d2f1cd444))
* *(render)* Correct anchors, accessors and constraints ([3270906](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/3270906c62509de67dca5d7788f1db404059de2e))
* *(render)* Remove attribute double-spacing, show extension type blocks ([868723a](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/868723a91561da24b10473192157a8ea1d946529))
* *(render)* Colour declared names, not just referenced ones ([b4b0ae3](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/b4b0ae3ab37230bd6cdb91852e51eb30dd2fbaa7))
* *(render)* Collapse non-identifier characters in page slugs ([ba3bfa9](https://github.com/MangelMaxime/starlight-fsharp-oracle/commit/ba3bfa9c289667ff0fb22159fe1eb9ba23dc05a8))

<strong><small>[View changes on Github](https://github.com/MangelMaxime/starlight-fsharp-oracle/compare/cec2951b273604c3a65edbdbd9874b007aaabbab..567877550ac3b53142a7c678879ab94817f87743)</small></strong>

## 0.3.0 - 2026-07-26

### 🚀 Features

* Bump astro v7 and starlight 0.41 ([7bfb354](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/7bfb354a69169aaa8967d3a82c2709db0ffc2d75))

### 🐞 Bug Fixes

* Prefix in-content links with the Astro site base ([5095622](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/5095622ebb885a31f5aeb89384047e91012ce27a))
* Merge companion module into its same-slug type page ([2b4dec1](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/2b4dec1b6393ecfddc2ebebe303a72e38cc41650))
* *(render)* Preserve Markdown code spans in XML doc summaries ([cb677c0](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/cb677c0167ef76214af0b81f5bd77c5afe2a1248))
* *(render)* Render enum cases with docs and colored names ([cec2951](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/cec2951b273604c3a65edbdbd9874b007aaabbab))

<strong><small>[View changes on Github](https://github.com/MangelMaxime/starlight-fsharp-doc/compare/004b2222784b8ccac32bf46e91832b4f92d55505..cec2951b273604c3a65edbdbd9874b007aaabbab)</small></strong>

## 0.1.2 - 2026-07-24

### 🐞 Bug Fixes

* Tolerate unescaped angle brackets in F# XML doc member IDs ([004b222](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/004b2222784b8ccac32bf46e91832b4f92d55505))
* *(render)* Escape MDX-hostile constructs in signatures and inline summaries ([1b716c6](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/1b716c695b7f73a88a6ebd13af6637880f9c0325))

<strong><small>[View changes on Github](https://github.com/MangelMaxime/starlight-fsharp-doc/compare/f566ab34e39f9ea2fbeb2aeddda2428d5d27dfb0..004b2222784b8ccac32bf46e91832b4f92d55505)</small></strong>

## 0.1.1 - 2026-05-14

### 🐞 Bug Fixes

* Don't include oracle-bin ([c3a5412](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/c3a5412c53b2e249d2230f95d486187ab5c4792a))
* Include fable_modules files ([f566ab3](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/f566ab34e39f9ea2fbeb2aeddda2428d5d27dfb0))

<strong><small>[View changes on Github](https://github.com/MangelMaxime/starlight-fsharp-doc/compare/f8a4ff5673ff7b2386988c021bce6ddafd75349c..f566ab34e39f9ea2fbeb2aeddda2428d5d27dfb0)</small></strong>

## 0.1.0 - 2026-05-14

### 🚀 Features

* Initial commit ([f8a4ff5](https://github.com/MangelMaxime/starlight-fsharp-doc/commit/f8a4ff5673ff7b2386988c021bce6ddafd75349c))

## 0.0.0
