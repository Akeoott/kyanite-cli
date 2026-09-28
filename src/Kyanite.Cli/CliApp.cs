// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

using System.CommandLine;
using System.CommandLine.Help;
using System.Threading.Tasks;

using Kyanite.Cli.Commands;

namespace Kyanite.Cli;

internal static class CliApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        var rootCommand = new RootCommand()
        {
            ScanCommand.Build(),
            InfoCommand.Build()
        };

        rootCommand.SetAction(parseResult
            => new HelpAction().Invoke(parseResult));

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}
