#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:package MiniPty@1.0.1
#:package MiniPty.Capture@1.0.1

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using MiniPty;
using MiniPty.Capture;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var failures = 0;

failures += await RunAsync("PtyEchoOutput", PtyEchoOutput);
failures += await RunAsync("PtyTtyCheck", PtyTtyCheck);
failures += await RunAsync("PtyStdinEof", PtyStdinEof);
failures += await RunAsync("PtyEmptyInputSignalsEof", PtyEmptyInputSignalsEof);
failures += await RunAsync("PtyStdinReadCompletesAfterInputEof", PtyStdinReadCompletesAfterInputEof);
failures += await RunAsync("PtyHasExitedPolls", () => Task.FromResult(PtyHasExitedPolls()));
failures += await RunAsync("PtyCancellationKill", PtyCancellationKill);
failures += await RunAsync("PtyCancellationWait", PtyCancellationWait);
failures += await RunAsync("PtyResizeUpdatesSize", () => Task.FromResult(PtyResizeUpdatesSize()));
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TryResolvePwsh(out var pwshPath))
    failures += await RunAsync("PtyMatrixPwsh", () => PtyMatrixPwsh(pwshPath));
else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    failures += await RunAsync("PtyMatrixUnix", PtyMatrixUnix);

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    failures += await RunAsync("IntegrationPtyCmdFixture", () => Task.FromResult(IntegrationFixture(repoRoot, "pty-cmd.yaml", "pty-cmd-output")));
    failures += await RunAsync("IntegrationPtyTtyFixture", () => Task.FromResult(IntegrationFixture(repoRoot, "pty-tty-check.yaml", "redirected=False")));
    failures += await RunAsync("IntegrationPtyScreenStateFixture", () => Task.FromResult(IntegrationPtyScreenStateFixture(repoRoot, "pty-screen-state-cmd.yaml")));
    failures += await RunAsync("IntegrationPtyDefaultTypingFixture", () => Task.FromResult(IntegrationPtyDefaultTypingFixture(repoRoot, "pty-default-typing-cmd.yaml", "echo pty-typed-output")));
    if (TryResolvePwsh(out var pwshForFixture))
        failures += await RunAsync("IntegrationMatrixFixture", () => Task.FromResult(IntegrationMatrixFixture(repoRoot, "matrix-pwsh-pty.yaml")));
}
else
{
    failures += await RunAsync("IntegrationPtyCmdFixture", () => Task.FromResult(IntegrationFixture(repoRoot, "pty-unix.yaml", "pty-cmd-output")));
    failures += await RunAsync("IntegrationPtyTtyFixture", () => Task.FromResult(IntegrationFixture(repoRoot, "pty-tty-check-unix.yaml", "redirected=False")));
    failures += await RunAsync("IntegrationPtyScreenStateFixture", () => Task.FromResult(IntegrationPtyScreenStateFixture(repoRoot, "pty-screen-state.yaml")));
    failures += await RunAsync("IntegrationPtyDefaultTypingFixture", () => Task.FromResult(IntegrationPtyDefaultTypingFixture(repoRoot, "pty-default-typing.yaml", "printf 'pty-typed-output\\n'")));
    if (!TryFindExecutable("matrix", out _))
        Console.Error.WriteLine("skip IntegrationMatrixFixture: matrix not found");
    else
        failures += await RunAsync("IntegrationMatrixFixture", () => Task.FromResult(IntegrationMatrixFixture(repoRoot, "matrix-unix-pty.yaml")));
}

return failures == 0 ? 0 : 1;

static bool IntegrationTestsEnabled(string repoRoot)
{
    var env = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        return true;

    Console.Error.WriteLine("skip integration PTY tests: set SCENETAKE_BIN to a published scenetake binary");
    return false;
}

static async Task<int> RunAsync(string name, Func<Task<bool>> test)
{
    try
    {
        if (await test())
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

static PtyStartInfo Spawn(string fileName, IReadOnlyList<string> arguments, int columns = 40, int rows = 8) =>
    new() { FileName = fileName, Arguments = arguments, Size = new(columns, rows) };

static PtyStartInfo UnixShell(string command) => Spawn("sh", ["-c", command]);

static PtyStartInfo WindowsCommand(string command)
{
    var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
    return Spawn(cmd, ["/c", command]);
}

static async Task<bool> PtyEchoOutput()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var result = await PtyCapture.RunAsync(WindowsCommand("echo pty-layer-echo"));
        return result.ExitCode == 0 && result.Contains("pty-layer-echo", StringComparison.Ordinal);
    }

    var unix = await PtyCapture.RunAsync(UnixShell("printf pty-layer-echo"));
    return unix.ExitCode == 0 && unix.Contains("pty-layer-echo", StringComparison.Ordinal);
}

static async Task<bool> PtyStdinEof()
{
    const string marker = "pty-stdin-eof";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var sort = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sort.exe");
        var result = await PtyCapture.RunAsync(
            Spawn(sort, []),
            new PtyCaptureOptions { Completion = new() { Input = $"zzz\r\n{marker}\r\naaa\r\n" } });
        return result.Contains(marker, StringComparison.Ordinal)
            && result.Contains("aaa", StringComparison.Ordinal);
    }

    // Canonical PTY line discipline needs a submitted line before EOT signals EOF; run cat directly (not a login shell).
    var unix = await PtyCapture.RunAsync(
        Spawn("cat", []),
        new PtyCaptureOptions { Completion = new() { Input = $"{marker}\n" } });
    return unix.ExitCode == 0 && unix.Contains(marker, StringComparison.Ordinal);
}

static async Task<bool> PtyEmptyInputSignalsEof()
{
    const string marker = "pty-empty-eof-complete";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var result = await PtyCapture.RunAsync(
            WindowsCommand($"find /v \"\" >nul & echo {marker}"),
            new PtyCaptureOptions { Completion = new() { Input = string.Empty } });
        return result.ExitCode == 0 && result.Contains(marker, StringComparison.Ordinal);
    }

    var unix = await PtyCapture.RunAsync(
        UnixShell($"cat >/dev/null; printf {marker}"),
        new PtyCaptureOptions { Completion = new() { Input = string.Empty } });
    return unix.ExitCode == 0 && unix.Contains(marker, StringComparison.Ordinal);
}

static async Task<bool> PtyStdinReadCompletesAfterInputEof()
{
    const string marker = "pty-stdin-read-complete";

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var result = await PtyCapture.RunAsync(
            WindowsCommand($"find /v \"\" >nul & echo {marker}"),
            new PtyCaptureOptions { Completion = new() { Input = "line 1\r\nline 2\r\n" } });
        return result.ExitCode == 0 && result.Contains(marker, StringComparison.Ordinal);
    }

    var unix = await PtyCapture.RunAsync(
        UnixShell($"cat >/dev/null; printf {marker}"),
        new PtyCaptureOptions { Completion = new() { Input = "line 1\nline 2\n" } });
    return unix.ExitCode == 0 && unix.Contains(marker, StringComparison.Ordinal);
}

static bool PtyHasExitedPolls()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        using var session = Pty.Start(Spawn(cmd, ["/c", "exit 0"]));
        for (var i = 0; i < 50 && !session.HasExited; i++)
            Thread.Sleep(20);
        return session.HasExited;
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    using var unixSession = Pty.Start(Spawn(shell, ["-lc", "exit 0"]));
    for (var i = 0; i < 50 && !unixSession.HasExited; i++)
        Thread.Sleep(20);
    return unixSession.HasExited;
}

static async Task<bool> PtyCancellationKill()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        await using var session = Pty.Start(Spawn(cmd, ["/c", "ping -n 30 127.0.0.1 >nul"]));
        try
        {
            await session.CompleteAsync(new PtyCompleteOptions { KillOnCancellation = true }, cts.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            await Task.Delay(200);
            return session.HasExited;
        }
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    await using var unixSession = Pty.Start(Spawn(shell, ["-lc", "sleep 30"]));
    try
    {
        await unixSession.CompleteAsync(new PtyCompleteOptions { KillOnCancellation = true }, cts.Token);
        return false;
    }
    catch (OperationCanceledException)
    {
        await Task.Delay(200);
        return unixSession.HasExited;
    }
}

static async Task<bool> PtyCancellationWait()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        await using var session = Pty.Start(Spawn(cmd, ["/c", "ping -n 30 127.0.0.1 >nul"]));
        try
        {
            await session.WaitForExitAsync(cts.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return !session.HasExited;
        }
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    await using var unixSession = Pty.Start(Spawn(shell, ["-lc", "sleep 30"]));
    try
    {
        await unixSession.WaitForExitAsync(cts.Token);
        return false;
    }
    catch (OperationCanceledException)
    {
        return !unixSession.HasExited;
    }
}

static bool PtyResizeUpdatesSize()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        using var session = Pty.Start(Spawn(cmd, ["/c", "exit 0"]));
        session.Resize(new(100, 30));
        return session.Size.Columns == 100 && session.Size.Rows == 30;
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    using var unixSession = Pty.Start(Spawn(shell, ["-lc", "exit 0"]));
    unixSession.Resize(new(100, 30));
    return unixSession.Size.Columns == 100 && unixSession.Size.Rows == 30;
}

static async Task<bool> PtyTtyCheck()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        if (!TryResolvePwsh(out var pwsh))
        {
            Console.Error.WriteLine("skip PtyTtyCheck: pwsh not found");
            return true;
        }

        var result = await PtyCapture.RunAsync(Spawn(pwsh, ["-NoLogo", "-NoProfile", "-Command", "Write-Output (\"redirected=$([Console]::IsOutputRedirected)\")"]));
        return result.ExitCode == 0 && result.Contains("redirected=False", StringComparison.OrdinalIgnoreCase);
    }

    var unix = await PtyCapture.RunAsync(UnixShell("test -t 1 && printf redirected=False || printf redirected=True"));
    return unix.ExitCode == 0 && unix.Contains("redirected=False", StringComparison.Ordinal);
}

static async Task<bool> PtyMatrixPwsh(string pwshPath)
{
    if (!TryFindExecutable("matrix", out _))
    {
        Console.Error.WriteLine("skip PtyMatrixPwsh: matrix not found");
        return true;
    }

    var result = await PtyCapture.RunAsync(Spawn(pwshPath, ["-NoLogo", "-Command", "matrix 3"], 80, 24));
    return result.ExitCode == 0
        && result.Chunks.Count >= 2
        && result.Contains("\u001b")
        && result.Output.Length > 100;
}

static async Task<bool> PtyMatrixUnix()
{
    if (!TryFindExecutable("cmatrix", out var cmatrix))
        return true;

    var result = await PtyCapture.RunAsync(Spawn(cmatrix, ["-C", "-s", "-l", "3"], 80, 24));
    return result.ExitCode == 0
        && result.Chunks.Count >= 2
        && result.Output.Length > 50;
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

static bool IntegrationMatrixFixture(string repoRoot, string fixtureName)
{
    if (!IntegrationTestsEnabled(repoRoot))
        return true;

    if (!TryFindExecutable("matrix", out _))
    {
        Console.Error.WriteLine("skip IntegrationMatrixFixture: matrix not found");
        return true;
    }

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", fixtureName);
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
        return events.Count >= 1
            && combined.Contains('\u001b')
            && LooksLikeMatrixOutput(combined);
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

static bool IntegrationPtyScreenStateFixture(string repoRoot, string fixtureName)
{
    if (!IntegrationTestsEnabled(repoRoot))
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", fixtureName);
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-pty-screen-state-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(repoRoot, fixturePath, outputStem, out var exitCode))
            return false;
        if (exitCode != 0)
            return false;

        var castPath = outputStem + ".cast";
        var combinedOutput = ReadCastOutput(castPath);
        return combinedOutput.Contains("before pty", StringComparison.Ordinal)
            && combinedOutput.Contains("pty-cmd-output", StringComparison.Ordinal)
            && combinedOutput.Contains("after pty", StringComparison.Ordinal)
            && !combinedOutput.Contains("\u001b[2J", StringComparison.Ordinal);
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

static bool IntegrationPtyDefaultTypingFixture(string repoRoot, string fixtureName, string typedCommand)
{
    if (!IntegrationTestsEnabled(repoRoot))
        return true;

    var fixturePath = Path.Combine(repoRoot, "tests", "fixtures", fixtureName);
    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-pty-default-typing-{Guid.NewGuid():N}");
    try
    {
        if (!RunScenetake(repoRoot, fixturePath, outputStem, out var exitCode))
            return false;
        if (exitCode != 0)
            return false;

        var castPath = outputStem + ".cast";
        var events = ReadCastOutputEvents(castPath);
        var combinedOutput = string.Concat(events);
        return combinedOutput.Contains($"$ {typedCommand}\r\n", StringComparison.Ordinal)
            && combinedOutput.Contains("pty-typed-output", StringComparison.Ordinal);
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

static bool LooksLikeMatrixOutput(string text) =>
    text.Length > 100
    && (text.Contains("\u001b[?1049h", StringComparison.Ordinal)
        || text.Contains("\u001b[2J", StringComparison.Ordinal)
        || text.Contains("\u001b[H", StringComparison.Ordinal));

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
