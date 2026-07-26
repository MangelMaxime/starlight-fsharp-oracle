module FSharp.Oracle.Extractor

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Oracle.Schema
open Oracle.XmlDoc
open FSharp.Oracle.Helpers
open FSharp.Oracle.EntityExtractor
open FSharp.Oracle.ModuleExtractor

// ---------------------------------------------------------------------------
// Public entry point
// ---------------------------------------------------------------------------

/// Resolve every referenced assembly, once.
///
/// This used to happen per target dll, so N assemblies meant N full FCS checks of the
/// same reference set. The result is shared by every `extractAssembly` call.
let resolveAssemblies
    (checker: FSharpChecker)
    (allDllPaths: string array)
    : FSharpAssembly list
    =
    let baseOptions, _ =
        checker.GetProjectOptionsFromScript(
            "dummy.fsx",
            FSharp.Compiler.Text.SourceText.ofString ""
        )
        |> Async.RunSynchronously

    // Reference every DLL so FCS can resolve cross-assembly dependencies.
    let projectOptions =
        { baseOptions with
            OtherOptions =
                [|
                    yield! baseOptions.OtherOptions
                    for path in allDllPaths do
                        $"-r:{path}"
                |]
        }

    checker.ParseAndCheckProject(projectOptions)
    |> Async.RunSynchronously
    |> fun results -> results.ProjectContext.GetReferencedAssemblies()

/// Extract the IR for a single compiled .dll plus its .xml doc file, from assemblies
/// already resolved by `resolveAssemblies`.
let extractAssembly (resolved: FSharpAssembly list) (dllPath: string) : Assembly =
    let targetName = System.IO.Path.GetFileNameWithoutExtension(dllPath)

    let fsharpAssembly = resolved |> List.tryFind (fun a -> a.SimpleName = targetName)

    match fsharpAssembly with
    | None ->
        // Say what was looked for and what was found: "could not load" on its own
        // leaves no way to tell a typo from a missing reference.
        let available =
            resolved
            |> List.map (fun a -> a.SimpleName)
            |> List.sort
            |> String.concat ", "

        failwith
            $"Could not load assembly '%s{targetName}' from %s{dllPath}.\nResolved assemblies were: %s{available}"
    | Some asm ->
        let docs = loadXmlDocFile dllPath

        // Recursively collect a module and all its nested sub-modules as
        // separate pages (each sub-module becomes its own page).
        let rec collectModulePages (entity: FSharpEntity) : Module list =
            let thisPage =
                tryExtract $"module {entity.FullName}" (fun () -> extractModule docs entity)

            let subPages =
                entity.NestedEntities
                |> Seq.filter (fun e -> e.IsFSharpModule)
                |> Seq.collect collectModulePages
                |> Seq.toList

            match thisPage with
            | Some page -> page :: subPages
            | None -> subPages

        // All pages that come from module entities (including sub-modules).
        let modulePages =
            asm.Contents.Entities
            |> Seq.filter (fun e -> e.IsFSharpModule)
            |> Seq.collect collectModulePages
            |> Seq.toList

        // Bare types at the assembly root that live in a namespace but are not
        // inside any module (e.g. record/union types declared directly in a
        // namespace declaration).  Group them by their namespace and emit one
        // synthetic module page per namespace.
        let syntheticPages =
            asm.Contents.Entities
            |> Seq.filter (fun e ->
                not e.IsFSharpModule && not e.IsNamespace && not e.IsArrayType && not e.IsByRef
            )
            |> Seq.groupBy (fun e -> namespaceOf (safeFullName e))
            |> Seq.map (fun (ns, entities) ->
                {
                    Name = "global"
                    FullName = ns + ".global"
                    Namespace = ns
                    XmlDoc = None
                    Entities =
                        entities
                        |> Seq.choose (fun e ->
                            tryExtract $"type {safeFullName e}" (fun () -> extractEntity docs e)
                        )
                        |> Seq.toList
                    Functions = []
                    Values = []
                    ExtensionMembers = []
                    IsSynthetic = true
                    ObsoleteInfo = ObsoleteInfo.Active
                }
            )
            |> Seq.toList

        let rec collectFcsNamespaces (entity: FSharpEntity) : string list =
            if entity.IsNamespace then
                let nested = entity.NestedEntities |> Seq.collect collectFcsNamespaces |> Seq.toList
                entity.FullName :: nested
            else
                []

        let allModules = modulePages @ syntheticPages

        let namespaceNames =
            [
                yield! asm.Contents.Entities |> Seq.collect collectFcsNamespaces

                yield!
                    allModules
                    |> List.map (fun m -> m.Namespace)
                    |> List.filter (fun ns -> ns <> "")
            ]
            |> List.distinct
            |> List.sort

        let namespaces =
            [
                // "global" if any module has no namespace
                if allModules |> List.exists (fun m -> m.Namespace = "") then
                    {
                        Name = "global"
                        FullName = ""
                    }

                for ns in namespaceNames do
                    let name =
                        let lastDot = ns.LastIndexOf('.')

                        if lastDot < 0 then
                            ns
                        else
                            ns.[lastDot + 1 ..]

                    {
                        Name = name
                        FullName = ns
                    }
            ]

        {
            Name = asm.SimpleName
            Namespaces = namespaces
            Modules = allModules
        }
