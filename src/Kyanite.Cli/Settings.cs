// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

namespace Kyanite.Cli;

internal record Settings(
    Scan Scan = default!
)
{
    public Settings() : this(
        new Scan()
    )
    { }
}

internal record Scan(
    bool ShouldScan = false,
    string? ScanPath = null
);
