namespace Scenetake.TestSupport;

/// <summary>What the tests share: the repository they read samples and fixtures from, and the binary they run.</summary>
internal static class TestEnv
{
    /// <summary>
    /// The repository root, found above the test's own folder: a test runs from its bin folder, and reads
    /// samples/ and tests/fixtures/ from the repository.
    /// </summary>
    internal static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>
    /// The published scenetake binary that the integration tests run, from SCENETAKE_BIN. Without it the test is
    /// skipped: these tests check the binary that ships, which a test run does not build.
    /// </summary>
    internal static string Binary
    {
        get
        {
            if (Environment.GetEnvironmentVariable("SCENETAKE_BIN") is { Length: > 0 } bin && File.Exists(bin))
                return bin;
            Skip.Test("set SCENETAKE_BIN to a published scenetake binary");
            return "";
        }
    }

    /// <summary>A path under the repository root, from its parts.</summary>
    internal static string RepoPath(params string[] parts) => Path.Combine([RepoRoot, .. parts]);

    /// <summary>The full path of a program on PATH, or null when no PATH folder has it.</summary>
    internal static string? FindOnPath(string name)
    {
        var fileName = OperatingSystem.IsWindows() ? name + ".exe" : name;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>Skips the test when one of the programs is not on PATH.</summary>
    internal static void RequireOnPath(params string[] names)
    {
        foreach (var name in names)
        {
            if (FindOnPath(name) is null)
                Skip.Test($"{name} not found on PATH");
        }
    }

    private static string FindRepoRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "scenetake.slnx")))
                return folder.FullName;
        }

        throw new InvalidOperationException($"no scenetake.slnx above {AppContext.BaseDirectory}");
    }
}
