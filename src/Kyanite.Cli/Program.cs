// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

using System.IO;

namespace Kyanite.Cli;

internal class Program()
{
    internal static int Main(string[] args)
    {
        var settings = ParseArgs.Init(args);
        Console.WriteLine(settings);

        // Test
        if (settings.Scan.ShouldScan)
        {
            Console.WriteLine(
                File.ReadAllText(settings.Scan.ScanPath!)
            );
        }

        return 0;
    }
}
