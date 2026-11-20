using System.Diagnostics;
using System.IO.Enumeration;
using System.Text;

/// <summary>
/// Several scenarios in one run, such as <c>scenetake docs/*.yaml</c> (spec_cli.md → Several scenarios). Each one
/// is recorded by this same program in a process of its own, with the other options of the command line, so what
/// one scenario leaves behind (a background server, the shell state folder, fonts that pre installed) never
/// reaches the next. A scenario that fails does not stop the others.
/// </summary>
internal static class ScenarioBatch
{
    /// <summary>
    /// Whether a positional argument after the first names scenarios: a <c>.yaml</c> or <c>.yml</c> file, or a
    /// wildcard. Any other one is the output name, which only one scenario takes.
    /// </summary>
    internal static bool IsScenarioArgument(string arg) =>
        arg.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
        || arg.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
        || HasWildcard(arg);

    internal static bool HasWildcard(string arg) => arg.AsSpan().IndexOfAny('*', '?') >= 0;

    /// <summary>
    /// The scenario files that <paramref name="patterns"/> name, as full paths, in the order given, each once. A
    /// wildcard (<c>*</c>, <c>?</c>) in the file name lists that folder, in ordinal order, as a shell does: PowerShell
    /// and cmd pass <c>docs/*.yaml</c> on as it is. A name that starts with a dot is left out unless the pattern
    /// starts with one too. Every file is found before anything is recorded.
    /// </summary>
    internal static bool TryExpand(List<string> patterns, string currentFolder, out List<string> paths, out string error)
    {
        paths = [];
        error = "";
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var pattern in patterns)
        {
            if (!HasWildcard(pattern))
            {
                var file = Path.GetFullPath(pattern, currentFolder);
                if (!File.Exists(file))
                {
                    error = $"{pattern} not found";
                    return false;
                }

                Add(paths, file, comparison);
                continue;
            }

            var folderPart = Path.GetDirectoryName(pattern) ?? "";
            var namePattern = Path.GetFileName(pattern);
            if (pattern.Contains("**", StringComparison.Ordinal))
            {
                error = $"'**' does not look into folders below: list each folder, such as docs/*.yaml docs/more/*.yaml ({pattern})";
                return false;
            }

            if (HasWildcard(folderPart))
            {
                error = $"a wildcard works in the file name only, such as docs/*.yaml: {pattern}";
                return false;
            }

            var folder = Path.GetFullPath(folderPart.Length == 0 ? "." : folderPart, currentFolder);
            var matches = new List<string>();
            if (Directory.Exists(folder))
            {
                // The pattern is matched here, not by the file system, so it means the same on every OS.
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    var name = Path.GetFileName(file);
                    if (name.StartsWith('.') && !namePattern.StartsWith('.'))
                        continue;
                    if (!FileSystemName.MatchesSimpleExpression(namePattern, name, ignoreCase: OperatingSystem.IsWindows()))
                        continue;

                    // Each match goes in its ordinal place. List.Sort with a comparer would add comparer types for
                    // every list type of the program to the AOT binary (about 37 KB).
                    var at = matches.Count;
                    while (at > 0 && string.CompareOrdinal(matches[at - 1], file) > 0)
                        at--;
                    matches.Insert(at, file);
                }
            }

            if (matches.Count == 0)
            {
                error = $"no file matches {pattern}";
                return false;
            }

            foreach (var match in matches)
                Add(paths, match, comparison);
        }

        return true;

        static void Add(List<string> paths, string path, StringComparison comparison)
        {
            foreach (var existing in paths)
            {
                if (string.Equals(existing, path, comparison))
                    return;
            }

            paths.Add(path);
        }
    }

    /// <summary>
    /// The command line that records <paramref name="scenario"/> alone: <paramref name="args"/> without the scenario
    /// arguments, then the scenario's full path. A full path never starts with a dash, so it is never read as an option.
    /// </summary>
    internal static List<string> ChildArguments(string[] args, List<int> scenarioIndexes, string scenario)
    {
        var child = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (!scenarioIndexes.Contains(i))
                child.Add(args[i]);
        }

        child.Add(scenario);
        return child;
    }

    /// <summary>
    /// Records each scenario in turn, then says how many were recorded and names the ones that failed or were not
    /// recorded. Returns 1 when one of them was not recorded. Ctrl+C stops the scenario being recorded, which gets
    /// it too, and no other one starts.
    /// </summary>
    internal static int Run(string[] args, List<int> scenarioIndexes, List<string> scenarios, Func<string, string> display)
    {
        var program = SelfCommand(out var programArgument);
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            // This process waits for the scenario being recorded, which stops on the same Ctrl+C.
            e.Cancel = true;
            stop.Cancel();
        };
        Console.CancelKeyPress += onCancel;

        var failed = new List<string>();
        var notRecorded = new List<string>();
        var recorded = 0;
        try
        {
            for (var index = 0; index < scenarios.Count; index++)
            {
                var scenario = scenarios[index];
                if (stop.IsCancellationRequested)
                {
                    notRecorded.Add(scenario);
                    continue;
                }

                if (index > 0)
                    Console.Error.WriteLine();

                var psi = new ProcessStartInfo(program) { UseShellExecute = false };
                if (programArgument is not null)
                    psi.ArgumentList.Add(programArgument);
                foreach (var arg in ChildArguments(args, scenarioIndexes, scenario))
                    psi.ArgumentList.Add(arg);

                int exitCode;
                try
                {
                    using var process = Process.Start(psi);
                    if (process is null)
                    {
                        Console.Error.WriteLine($"Error: cannot start {program} for {display(scenario)}");
                        failed.Add(scenario);
                        continue;
                    }

                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
                {
                    Console.Error.WriteLine($"Error: cannot start {program} for {display(scenario)}: {ex.Message}");
                    failed.Add(scenario);
                    continue;
                }

                if (stop.IsCancellationRequested)
                    notRecorded.Add(scenario);
                else if (exitCode == 0)
                    recorded++;
                else
                    failed.Add(scenario);
            }
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine(Summary(scenarios.Count, recorded, failed, notRecorded, stop.IsCancellationRequested, display));
        return recorded == scenarios.Count ? 0 : 1;
    }

    /// <summary>The last line of a run of several scenarios.</summary>
    internal static string Summary(int total, int recorded, List<string> failed, List<string> notRecorded, bool stopped, Func<string, string> display)
    {
        var summary = new StringBuilder();
        if (stopped)
            summary.Append("Stopped by Ctrl+C: recorded ").Append(recorded).Append(" of ").Append(total).Append(" scenarios.");
        else if (recorded == total)
            summary.Append("Recorded ").Append(total).Append(" scenarios.");
        else
            summary.Append("Recorded ").Append(recorded).Append(" of ").Append(total).Append(" scenarios.");

        Append(summary, " Failed: ", failed, display);
        Append(summary, " Not recorded: ", notRecorded, display);
        return summary.ToString();

        static void Append(StringBuilder summary, string label, List<string> paths, Func<string, string> display)
        {
            if (paths.Count == 0)
                return;
            summary.Append(label);
            for (var i = 0; i < paths.Count; i++)
                summary.Append(i == 0 ? "" : ", ").Append(display(paths[i]));
        }
    }

    /// <summary>
    /// This program, to start again for each scenario: the native binary, the app host of `dotnet run` or of the
    /// dotnet tool, or for `dotnet scenetake.dll` the dotnet host, with the dll as <paramref name="argument"/>.
    /// </summary>
    private static string SelfCommand(out string? argument)
    {
        argument = null;
        var program = Environment.ProcessPath ?? "scenetake";
        if (Path.GetFileNameWithoutExtension(program).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            argument = Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName + ".dll");
        return program;
    }
}
