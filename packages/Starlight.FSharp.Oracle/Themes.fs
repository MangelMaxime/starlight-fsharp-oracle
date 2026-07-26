module Starlight.FSharp.Themes

open Fable.Core
open Fable.Core.JsInterop
open System.Text
open StringBuilder.Extensions

[<Emit("Array.isArray($0)")>]
let private isArray (x: obj) : bool = jsNative

let private scopesOf (rule: obj) : string array =
    let scope: obj = rule?scope

    if isNull (box scope) then
        [||]
    elif isArray scope then
        scope :?> string array
    else
        [| scope :?> string |]

let private colorFor (theme: obj) (targetScopes: string list) : string =
    let settings: obj array = theme?settings
    let fg: string = theme?fg

    // A rule applies to a token when the token's scope starts with the rule's scope.
    // We look for the first rule whose scope is a prefix of any of our target scopes.
    settings
    |> Array.tryPick (fun rule ->
        let ruleScopes = scopesOf rule

        let matches =
            targetScopes
            |> List.exists (fun target ->
                ruleScopes
                |> Array.exists (fun rs -> target = rs || target.StartsWith(rs + "."))
            )

        if not matches then
            None
        else
            let color: obj = rule?settings?foreground

            if isNull (box color) then
                None
            else
                Some(color :?> string)
    )
    |> Option.defaultValue fg

/// Generates CSS custom-property declarations for each syntax-highlighting
/// theme extracted from the Expressive Code renderer. The static class rules
/// (colour assignments, layout, sidebar badges, etc.) now live in
/// components/fsharp-doc.css and are imported by FSharpDocPage.astro.
let generateCss (ecRenderer: obj) : string =
    let themes: obj array = ecRenderer?ec?themes
    let sb = StringBuilder()

    for theme in themes do
        let themeType: string = theme?``type``

        let selector =
            if themeType = "dark" then
                ":root[data-theme='dark']"
            else
                ":root"

        // Scope chains follow the captures in tree-sitter-fsharp's highlights.scm,
        // most specific first. Anything a theme does not define falls through to the
        // editor foreground, which is the right answer for punctuation: an IDE leaves
        // it neutral rather than colouring it like a keyword.
        let colors =
            [
                "kw", [ "keyword"; "keyword.control"; "storage.type"; "storage.modifier" ]
                "op", [ "keyword.operator"; "operator" ]
                "punct", [ "punctuation.separator"; "punctuation" ]
                "bracket", [ "punctuation.section"; "meta.brace"; "punctuation" ]
                "type", [ "entity.name.type"; "support.type"; "support.class"; "entity.name.class" ]
                // Most themes have no separate colour for type parameters. Falling back
                // to the type colour says so honestly, rather than inventing one.
                "typevar",
                [
                    "entity.name.type.parameter"
                    "support.type.parameter"
                    "entity.name.type"
                    "support.type"
                ]
                "param", [ "variable.parameter"; "variable.other"; "variable" ]
                "fn", [ "entity.name.function"; "support.function"; "variable.function" ]
                "literal", [ "constant"; "constant.numeric"; "string" ]
                "member",
                [
                    "variable.other.property"
                    "variable.other.member"
                    "support.variable.property"
                    "variable.other"
                    "variable"
                ]
                "attr",
                [
                    "storage.type.attribute"
                    "entity.other.attribute-name"
                    "meta.attribute"
                    "entity.name.tag"
                ]
            ]

        sb.WriteLine($"{selector} {{")

        for name, scopes in colors do
            sb.WriteLine($"    --fsharp-doc-{name}: {colorFor theme scopes};")

        sb.WriteLine("}")

    sb.ToString()
