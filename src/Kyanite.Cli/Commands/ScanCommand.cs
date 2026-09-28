// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

using System.CommandLine;
using System.IO;

using Spectre.Console;

using Kyanite.Cli.Utils;

namespace Kyanite.Cli.Commands;

internal static class ScanCommand
{
    public static Command Build()
    {
        var command = new Command("scan", "Scan all specified files and directories.");

        var recurseOption = new Option<bool>("--all", "-a")
        {
            Description = "Recursively scan every file git sees, plus untracked files."
        };

        var includeOption = new Option<string[]?>("--include", "-i")
        {
            Description = """
                Glob patterns to include directories and files.
                Respects .gitignore, use -I to bypass.
                Shell must expand globs (explicit paths on Windows).
                """,
            HelpName = "GLOB",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => null
        };

        var includeForceOption = new Option<string[]?>("--include-force", "-I")
        {
            Description = """
                Like --include, but bypasses .gitignore and built-in exclusions.
                """,
            HelpName = "GLOB",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => null
        };

        var excludeOption = new Option<string[]?>("--exclude", "-e")
        {
            Description = """
                Glob patterns to exclude directories and files.
                  ✔  '**/bin/**'   every bin/ directory and its contents
                  ✘  'bin/'        matches only a file literally named 'bin'
                """,
            HelpName = "GLOB",
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => null
        };

        var ignoreGitOption = new Option<bool>("--ignore-git", "-g")
        {
            Description = "Ignore .gitignore, scan files it would normally skip."
        };

        command.Options.Add(recurseOption);
        command.Options.Add(includeOption);
        command.Options.Add(includeForceOption);
        command.Options.Add(excludeOption);
        command.Options.Add(ignoreGitOption);

        command.SetAction(parseResult =>
        {
            string root = Directory.GetCurrentDirectory();
            bool scanAll = parseResult.GetValue(recurseOption);
            string[] includes = parseResult.GetValue(includeOption) ?? [];
            string[] forceIncludes = parseResult.GetValue(includeForceOption) ?? [];
            string[] excludes = parseResult.GetValue(excludeOption) ?? [];
            bool ignoreGit = parseResult.GetValue(ignoreGitOption);

            return Execute(root, scanAll, includes, forceIncludes, excludes, ignoreGit);
        });

        return command;
    }

    private static int Execute(
        string root,
        bool scanAll,
        string[] includes,
        string[] forceIncludes,
        string[] excludes,
        bool ignoreGit)
    {
        if (!scanAll && includes.Length == 0 && forceIncludes.Length == 0)
        {
            AnsiConsole.MarkupLine("Nothing to scan. Use [bold yellow]--all[/] or [bold yellow]--include[/] to specify what to scan.");
            return 1;
        }

        PathResult result = PathResolver.Resolve(
            root, scanAll, includes, forceIncludes, excludes, ignoreGit);

        if (result.Paths.Count == 0)
        {
            ReportEmptyResult(root, scanAll, ignoreGit, result);
            return 1;
        }

        foreach (string path in result.Paths)
            Console.WriteLine(path);

        // TODO: hand `result.Paths` off to the scanner.

        return 0;
    }

    #region Utils

    private static void ReportEmptyResult(
        string root,
        bool scanAll,
        bool ignoreGit,
        PathResult result)
    {
        AnsiConsole.MarkupLine("[yellow]No files to scan.[/]");
        var inc = result.Includes;

        if (inc.Requested > 0)
        {
            int total = inc.Requested;

            if (inc.Excluded.Count > 0)
            {
                AnsiConsole.MarkupLine($"[grey]{inc.Excluded.Count} of {total} specified paths were excluded by ignore rules.[/]");
                AnsiConsole.MarkupLine("[grey]Use [bold]-I[/] to force include them.[/]");
            }
            else if (inc.Missing.Count > 0)
            {
                AnsiConsole.MarkupLine($"[grey]{inc.Missing.Count} of {total} specified paths are missing.[/]");
            }
            else if (inc.Directories.Count > 0)
            {
                AnsiConsole.MarkupLine($"[grey]{inc.Directories.Count} of {total} specified paths were directories, not files.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]None of the specified paths resolved to files.[/]");
            }
        }

        if (scanAll)
        {
            if (result.Recursion.CandidateCount > 0)
                AnsiConsole.MarkupLine("[grey]All files under the working directory were excluded.[/]");
            else
                AnsiConsole.MarkupLine("[grey]No files were found under the working directory.[/]");
        }

        if (!ignoreGit && File.Exists(Path.Combine(root, ".gitignore")))
            AnsiConsole.MarkupLine("[grey]Tip: rerun with [bold]-g[/] to ignore [bold].gitignore[/].[/]");
    }

    #endregion
}
