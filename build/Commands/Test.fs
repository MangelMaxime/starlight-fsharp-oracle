module EasyBuild.Commands.Test

open Spectre.Console.Cli
open SimpleExec
open BlackFox.CommandLine
open System.ComponentModel
open EasyBuild.Workspace

type TestSettings() =

    inherit CommandSettings()

    [<CommandOption("-u|--update")>]
    [<Description("Accept the current output as the new snapshots")>]
    member val IsUpdate = false with get, set

type TestCommand() =
    inherit Command<TestSettings>()
    interface ICommandLimiter<TestSettings>

    override __.Execute(_, settings, _) =

        // The snapshot tests read the compiled fixture, so it must be current.
        Command.Run(
            "dotnet",
            CmdLine.empty
            |> CmdLine.appendRaw "build"
            |> CmdLine.appendRaw Workspace.tests.Reference.``Reference.fsproj``
            |> CmdLine.appendPrefix "-v" "quiet"
            |> CmdLine.appendRaw "--nologo"
            |> CmdLine.toString
        )

        Command.Run(
            "dotnet",
            CmdLine.empty
            |> CmdLine.appendRaw "run"
            |> CmdLine.appendPrefix "--project" Workspace.tests.``Oracle.Tests``.``Oracle.Tests.fsproj``
            |> CmdLine.appendPrefix "-v" "quiet"
            |> CmdLine.appendRaw "--nologo"
            |> CmdLine.appendRaw "--"
            |> CmdLine.appendIf settings.IsUpdate "--update"
            |> CmdLine.toString
        )

        0
