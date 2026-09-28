// SPDX-FileCopyrightText: 2026-present Akeoot <akeoot@pm.me>
// SPDX-License-Identifier: FSL-1.1-ALv2

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Kyanite.Cli.Utils;

internal sealed record PathResult(
    IReadOnlyList<string> Paths,
    IncludeClassification Includes,
    RecursionSummary Recursion);

internal sealed record IncludeClassification(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Directories,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Excluded,
    int Requested);

internal sealed record RecursionSummary(
    bool Requested,
    bool UsedGit,
    int CandidateCount);

/// <summary>
/// Resolves the set of files a scan is allowed to touch.
/// Precedence, highest to lowest: --exclude, --include-force, --include, --all
/// </summary>
internal sealed class PathResolver
{
    // CLI config, set once per Resolve() call.
    private readonly string _root;
    private readonly bool _all;
    private readonly string[] _includes;
    private readonly string[] _forceIncludes;
    private readonly IReadOnlyList<string> _excludes;
    private readonly bool _ignoreGit;

    // Populated during Run(). Null means git was skipped or unavailable.
    private HashSet<string>? _gitVisible;

    private PathResolver(
        string root, bool all,
        string[] includes, string[] forceIncludes,
        IReadOnlyList<string> excludes, bool ignoreGit)
    {
        _root = root;
        _all = all;
        _includes = includes;
        _forceIncludes = forceIncludes;
        _excludes = excludes;
        _ignoreGit = ignoreGit;
    }

    /// <summary>
    /// Resolves the final scan set from the given CLI options.
    /// </summary>
    public static PathResult Resolve(
        string root, bool all,
        string[] includes, string[] forceIncludes,
        string[] excludes, bool ignoreGit)
        => new PathResolver(root, all, includes, forceIncludes, excludes, ignoreGit).Run();

    private PathResult Run()
    {
        _gitVisible = LoadGitVisibleIfNeeded();

        (IReadOnlyList<string> recursiveFiles, int candidates) = _all
            ? CollectRecursive()
            : ([], 0);

        var acc = new IncludeAccumulator();
        foreach (string raw in _includes) ClassifyInclude(raw, force: false, acc);
        foreach (string raw in _forceIncludes) ClassifyInclude(raw, force: true, acc);

        var set = new HashSet<string>(_pathComparer);
        foreach (string p in recursiveFiles) set.Add(p);
        foreach (string p in acc.Files) set.Add(p);

        var paths = set.ToArray();
        Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

        return new PathResult(
            paths,
            new IncludeClassification(
                acc.Files, acc.Directories, acc.Missing, acc.Excluded,
                _includes.Length + _forceIncludes.Length),
            new RecursionSummary(_all, _gitVisible is not null, candidates));
    }

    // --all

    private (IReadOnlyList<string> Files, int Candidates) CollectRecursive()
    {
        string[] candidates = _gitVisible is not null
            ? [.. _gitVisible
                .Where(rel => !rel.StartsWith(".."))
                .Select(rel => Path.GetFullPath(Path.Combine(_root, rel)))]
            : [.. WalkFallback()];

        var kept = new List<string>(candidates.Length);
        foreach (string abs in candidates)
        {
            string rel = Relative(abs);
            if (MatchesAny(rel, _alwaysIgnoredGlobs)) continue;
            if (MatchesAny(rel, _excludes)) continue;
            kept.Add(abs);
        }

        return (kept, candidates.Length);
    }

    private IEnumerable<string> WalkFallback()
    {
        var stack = new Stack<string>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            string dir = stack.Pop();

            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch { continue; }

            foreach (string entry in entries)
            {
                bool isDir, isFile;
                try { isDir = Directory.Exists(entry); isFile = !isDir && File.Exists(entry); }
                catch { continue; }

                if (isDir)
                {
                    if (_alwaysIgnoredDirectoryNames.Contains(Path.GetFileName(entry))) continue;
                    stack.Push(entry);
                }
                else if (isFile)
                {
                    yield return entry;
                }
            }
        }
    }

    // --include and --include-force

    private void ClassifyInclude(string raw, bool force, IncludeAccumulator acc)
    {
        if (!TryAbsolute(raw, out string abs) || (!File.Exists(abs) && !Directory.Exists(abs)))
        {
            acc.Missing.Add(abs ?? raw);
            return;
        }

        string rel = Relative(abs);

        // --exclude wins over everything. --include-force skips the other two.
        if (MatchesAny(rel, _excludes)
            || (!force && MatchesAny(rel, _alwaysIgnoredGlobs))
            || (!force && _gitVisible is not null && IsUnderRoot(abs) && !_gitVisible.Contains(rel)))
        {
            acc.Excluded.Add(abs);
            return;
        }

        if (Directory.Exists(abs)) acc.Directories.Add(abs);
        else acc.Files.Add(abs);
    }

    private bool TryAbsolute(string raw, out string abs)
    {
        try
        {
            abs = Path.IsPathRooted(raw)
                ? Path.GetFullPath(raw)
                : Path.GetFullPath(raw, _root);
            return true;
        }
        catch { abs = raw; return false; }
    }

    // git

    private HashSet<string>? LoadGitVisibleIfNeeded()
    {
        if (_ignoreGit || (!_all && _includes.Length == 0)) return null;
        return TryGitLsFiles();
    }

    private HashSet<string>? TryGitLsFiles()
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("ls-files");
            psi.ArgumentList.Add("--cached");
            psi.ArgumentList.Add("--others");
            psi.ArgumentList.Add("--exclude-standard");
            psi.ArgumentList.Add("-z");

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            if (proc.ExitCode != 0) return null;

            var rels = new HashSet<string>(_pathComparer);
            foreach (string rel in stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(rel)) continue;
                rels.Add(rel.Replace('\\', '/'));
            }
            return rels;
        }
        catch { return null; }
    }

    // Paths

    private string Relative(string abs)
        => Path.GetRelativePath(_root, abs).Replace('\\', '/');

    private bool IsUnderRoot(string abs)
    {
        string fullRoot = Path.GetFullPath(_root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(abs).StartsWith(fullRoot, _pathComparison);
    }

    // Globs

    private static bool MatchesAny(string relativePath, IReadOnlyList<string> globs)
    {
        if (globs.Count == 0) return false;

        string path = Normalize(relativePath);
        foreach (string glob in globs)
            if (MatchesGlob(path, glob)) return true;
        return false;
    }

    private static bool MatchesGlob(string path, string glob)
    {
        string[] globParts = Normalize(glob).Split('/', StringSplitOptions.RemoveEmptyEntries);
        string[] pathParts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return MatchParts(pathParts, 0, globParts, 0);
    }

    private static bool MatchParts(string[] path, int pi, string[] glob, int gi)
    {
        if (gi == glob.Length) return pi == path.Length;

        if (glob[gi] == "**")
        {
            for (int skip = pi; skip <= path.Length; skip++)
                if (MatchParts(path, skip, glob, gi + 1)) return true;
            return false;
        }

        if (pi == path.Length) return false;
        if (!string.Equals(glob[gi], path[pi], _pathComparison)) return false;
        return MatchParts(path, pi + 1, glob, gi + 1);
    }

    private static string Normalize(string value)
        => value.Replace('\\', '/').TrimStart('/');

    // Static config

    private static readonly StringComparison _pathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly StringComparer _pathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Directory names that are always skipped. The walk prunes these by name;
    /// the glob list is derived from them so the two can never drift.
    /// </summary>
    private static readonly HashSet<string> _alwaysIgnoredDirectoryNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".env", ".venv", ".vs", ".idea", "target", "node_modules"
        };

    /// <summary>
    /// Glob equivalents of <see cref="_alwaysIgnoredDirectoryNames"/>.
    /// Each name yields two patterns so both the directory itself and anything
    /// inside it are matched. Built once at type initialization.
    /// </summary>
    private static readonly IReadOnlyList<string> _alwaysIgnoredGlobs =
        BuildAlwaysIgnoredGlobs(_alwaysIgnoredDirectoryNames);

    private static string[] BuildAlwaysIgnoredGlobs(IEnumerable<string> names)
    {
        var globs = new List<string>(names.Count() * 2);
        foreach (string name in names)
        {
            globs.Add($"**/{name}");
            globs.Add($"**/{name}/**");
        }
        return [.. globs];
    }

    private sealed class IncludeAccumulator
    {
        public List<string> Files { get; } = [];
        public List<string> Directories { get; } = [];
        public List<string> Missing { get; } = [];
        public List<string> Excluded { get; } = [];
    }
}
