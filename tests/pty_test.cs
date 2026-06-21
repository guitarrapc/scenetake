#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:include ../PseudoTerminal.cs

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var failures = 0;

failures += Run("PtyEchoOutput", PtyEchoOutput);
failures += Run("PtyTtyCheck", PtyTtyCheck);
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TryResolvePwsh(out var pwshPath))
    failures += Run("PtyMatrixPwsh", () => PtyMatrixPwsh(pwshPath));
else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    failures += Run("PtyMatrixUnix", PtyMatrixUnix);

failures += Run("IntegrationPtyCmdFixture", () => IntegrationFixture(repoRoot, "pty-cmd.yaml", "pty-cmd-output"));
failures += Run("IntegrationPtyTtyFixture", () => IntegrationFixture(repoRoot, "pty-tty-check.yaml", "redirected=False"));
if (TryResolvePwsh(out var pwshForFixture))
    failures += Run("IntegrationMatrixFixture", () => IntegrationMatrixFixture(repoRoot, pwshForFixture));

return failures == 0 ? 0 : 1;

static bool IntegrationTestsEnabled(string repoRoot)
{
    var env = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        return true;

    Console.Error.WriteLine("skip integration PTY tests: set SCENETAKE_BIN to a published scenetake binary");
    return false;
}

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }

        Console.Error.WriteLine($"FAIL {name}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        return 1;
    }
}

static PtyLaunchContext TestContext(string shell) => new(shell, 40, 8, null);

static bool PtyEchoOutput()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        var output = PseudoTerminal.Run(
            cmd,
            ["/c", "echo pty-layer-echo"],
            null,
            40,
            8,
            TestContext("cmd"));
        return output.ExitCode == 0
            && output.IsTerminalOutput
            && output.Stdout.Contains("pty-layer-echo", StringComparison.Ordinal);
    }

    var shell = Environment.GetEnvironmentVariable("SHELL");
    if (string.IsNullOrWhiteSpace(shell))
        shell = "/bin/bash";

    var unixOutput = PseudoTerminal.Run(
        shell,
        ["-lc", "printf pty-layer-echo"],
        null,
        40,
        8,
        TestContext(shell));
    return unixOutput.ExitCode == 0
        && unixOutput.IsTerminalOutput
        && unixOutput.Stdout.Contains("pty-layer-echo", StringComparison.Ordinal);
}

static bool PtyTtyCheck()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        if (!TryResolvePwsh(out var pwsh))
        {
            Console.Error.WriteLine("skip PtyTtyCheck: pwsh not found");
            return true;
        }

        var output = PseudoTerminal.Run(
            pwsh,
            ["-NoLogo", "-NoProfile", "-Command", "Write-Output (\"redirected=$([Console]::IsOutputRedirected)\")"],
            null,
            40,
            8,
            TestContext("pwsh"));
        return output.ExitCode == 0 && output.Stdout.Contains("redirected=False", StringComparison.OrdinalIgnoreCase);
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    var unixOutput = PseudoTerminal.Run(
        shell,
        ["-lc", "if [ -t 1 ]; then echo redirected=False; else echo redirected=True; fi"],
        null,
        40,
        8,
        TestContext(shell));
    return unixOutput.ExitCode == 0 && unixOutput.Stdout.Contains("redirected=False", StringComparison.Ordinal);
}

static bool PtyMatrixPwsh(string pwshPath)
{
    var output = PseudoTerminal.Run(
        pwshPath,
        ["-NoLogo", "-Command", "matrix 3"],
        null,
        80,
        24,
        new PtyLaunchContext("pwsh", 80, 24, null));
    return output.ExitCode == 0
        && output.Chunks.Count >= 2
        && output.Stdout.Contains('\u001b')
        && output.Stdout.Length > 100;
}

static bool PtyMatrixUnix()
{
    if (!TryFindExecutable("cmatrix", out var cmatrix))
        return true;

    var output = PseudoTerminal.Run(
        cmatrix,
        ["-C", "-s", "-l", "3"],
        null,
        80,
        24,
        new PtyLaunchContext("cmatrix", 80, 24, null));
    return output.ExitCode == 0
        && output.Chunks.Count >= 2
        && output.Stdout.Length > 50;
}

static bool IntegrationFixture(string repoRoot, string fixtureName, string expectedSubstring)
{
    if (!IntegrationTestsEnabled(repoRoot))
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", fixtureName);
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-pty-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(repoRoot, fixturePath, outputStem, out var exitCode))
            return false;
        if (exitCode != 0)
            return false;

        var castPath = outputStem + ".cast";
        if (!File.Exists(castPath))
            return false;

        var combinedOutput = ReadCastOutput(castPath);
        return combinedOutput.Contains(expectedSubstring, StringComparison.Ordinal);
    }
    finally
    {
        foreach (var path in new[] { outputStem + ".cast", outputStem + ".svg" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

static bool IntegrationMatrixFixture(string repoRoot, string pwshPath)
{
    if (!IntegrationTestsEnabled(repoRoot))
        return true;

    if (!File.Exists(pwshPath))
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", "matrix-pwsh-pty.yaml");
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-pty-matrix-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(repoRoot, fixturePath, outputStem, out var exitCode))
            return false;
        if (exitCode != 0)
            return false;

        var castPath = outputStem + ".cast";
        var events = ReadCastOutputEvents(castPath);
        var combined = string.Concat(events);
        return events.Count >= 2
            && combined.Contains('\u001b')
            && combined.Length > 100;
    }
    finally
    {
        foreach (var path in new[] { outputStem + ".cast", outputStem + ".svg" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

static bool RunScenetake(string repoRoot, string fixturePath, string outputStem, out int exitCode)
{
    exitCode = -1;
    var scenetakePath = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (string.IsNullOrWhiteSpace(scenetakePath) || !File.Exists(scenetakePath))
        return false;

    var psi = new ProcessStartInfo(scenetakePath, [fixturePath, outputStem])
    {
        WorkingDirectory = repoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };

    using var process = Process.Start(psi);
    if (process is null)
        return false;

    process.WaitForExit();
    exitCode = process.ExitCode;
    return true;
}

static string ReadCastOutput(string castPath)
{
    return string.Concat(ReadCastOutputEvents(castPath));
}

static List<string> ReadCastOutputEvents(string castPath)
{
    var outputs = new List<string>();
    foreach (var line in File.ReadAllLines(castPath))
    {
        if (line.Length == 0 || line[0] == '#')
            continue;
        if (line[0] == '{')
            continue;

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        if (root.GetArrayLength() < 3)
            continue;
        if (root[1].GetString() != "o")
            continue;
        outputs.Add(root[2].GetString() ?? "");
    }

    return outputs;
}

static bool TryResolvePwsh(out string path)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return TryFindExecutable("pwsh", out path) || TryFindExecutable("powershell", out path);

    return TryFindExecutable("pwsh", out path);
}

static bool TryFindExecutable(string name, out string resolved)
{
    resolved = "";
    if (File.Exists(name))
    {
        resolved = Path.GetFullPath(name);
        return true;
    }

    var pathEnv = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(pathEnv))
        return false;

    var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        : [string.Empty];

    foreach (var directory in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (Path.HasExtension(name))
        {
            var exact = Path.Combine(directory, name);
            if (File.Exists(exact))
            {
                resolved = exact;
                return true;
            }
            continue;
        }

        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(directory, name + extension);
            if (File.Exists(candidate))
            {
                resolved = candidate;
                return true;
            }
        }
    }

    return false;
}

static string FindRepoRoot(string start)
{
    var current = new DirectoryInfo(start);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "scenetake.cs")))
            return current.FullName;
        current = current.Parent;
    }

    throw new InvalidOperationException("repository root not found");
}
