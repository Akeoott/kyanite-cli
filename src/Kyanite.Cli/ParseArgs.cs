// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

using System.Collections.Generic;
using Kyanite.Cli.Assets;
using Mono.Options;
using Spectre.Console;

namespace Kyanite.Cli;

internal static class ParseArgs
{
    public static Settings settings = new();

    public static Settings Init(string[] args)
    {
        var shouldScan = false;
        string? scanPath = null;

        var showInfo = false;
        var showHelp = false;

        var options = new OptionSet
        {
            { "s=|scan=", v => {shouldScan = true; scanPath = v;} },
            { "i|info", _ => showInfo  = true },
            { "h|help", _ => showHelp  = true },
        };

        List<string> positional;
        try
        {
            positional = options.Parse(args);
        }
        catch (OptionException e)
        {
            Fail(e.Message);
            return null!; // unreachable
        }

        if (showHelp || args.Length == 0)
            ShowHelpAndExit();
        if (showInfo)
            ShowInfoAndExit();

        if (positional.Count > 0)
            Fail($"Unexpected argument \"{string.Join(" ", positional)}\"");

        settings = new Settings(
            Scan: new Scan(
                ShouldScan: shouldScan,
                ScanPath: scanPath
            )
        );

        return settings;
    }

    private static void ShowHelpAndExit()
    {
        AnsiConsole.MarkupLine(Const.Help);
        Environment.Exit(0);
    }

    private static void ShowInfoAndExit()
    {
        AnsiConsole.MarkupLine(Const.Info);
        Environment.Exit(0);
    }

    private static void Fail(string message)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(message)}");
        AnsiConsole.MarkupLine("Use [Yellow2]--help[/] to see all commands.");
        Environment.Exit(1);
    }
}
