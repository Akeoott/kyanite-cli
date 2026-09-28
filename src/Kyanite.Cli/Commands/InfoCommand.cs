// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

using System.CommandLine;

using Spectre.Console;

namespace Kyanite.Cli.Commands;

internal static class InfoCommand
{
    public static Command Build()
    {
        var command = new Command("info", "Show tool information.");

        command.SetAction(_ =>
        {
            AnsiConsole.MarkupLine("""
                [bold green]kyanite-cli[/] — [dim]Identify, inspect and fix.[/]

                Come back later!
                """);
            return 0;
        });

        return command;
    }
}
