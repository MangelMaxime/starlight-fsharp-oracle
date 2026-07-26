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

/// Extract the JSON IR for a single compiled .dll + optional .xml doc file.
/// The checker must have already been created with [allDllPaths] as references
/// so cross-assembly resolution works.
let extractAssembly
    (checker: FSharpChecker)
    (allDllPaths: string array)
    (dllPath: string)
    : Assembly
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

    // Load the assembly via FCS reflection
    let fsharpAssembly =
        checker.ParseAndCheckProject(projectOptions)
        |> Async.RunSynchronously
        // Walk the resolved assemblies to find the target dll
        |> fun results ->
            let targetName = System.IO.Path.GetFileNameWithoutExtension(dllPath)

            results.ProjectContext.GetReferencedAssemblies()
            |> List.tryFind (fun a -> a.SimpleName = targetName)

    match fsharpAssembly with
    | None -> failwithf "Could not load assembly: %s" dllPath
    | Some asm ->
        let docs = loadXmlDocFile dllPath

        // Recursively collect a module and all its nested sub-modules as
        // separate pages (each sub-module becomes its own page).
        let rec collectModulePages (entity: FSharpEntity) : Module list =
            let thisPage = extractModule docs entity

            let subPages =
                entity.NestedEntities
                |> Seq.filter (fun e -> e.IsFSharpModule)
                |> Seq.collect collectModulePages
                |> Seq.toList

            thisPage :: subPages

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
                    Entities = entities |> Seq.map (extractEntity docs) |> Seq.toList
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
