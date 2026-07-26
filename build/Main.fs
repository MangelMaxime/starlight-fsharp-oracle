module EasyBuild.Main

open Spectre.Console.Cli
open EasyBuild.Commands.Docs
open EasyBuild.Commands.Serve
open EasyBuild.Commands.Test
open SimpleExec
open EasyBuild.Tools.Husky

[<EntryPoint>]
let main args =

    Husky.install ()

    Command.Run("npx", "pnpm install")

    let app = CommandApp()

    app.Configure(fun config ->
        config.Settings.ApplicationName <- "./build.sh"

        config.AddCommand<DocsCommand>("docs") |> ignore
        config.AddCommand<ServeCommand>("serve") |> ignore
        config.AddCommand<TestCommand>("test") |> ignore

    )

    app.Run(args)
