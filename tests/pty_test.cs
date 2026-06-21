#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:package MiniPty@0.3.0
#:package MiniPty.Capture@0.3.0

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
failures += await RunAsync("PtyHasExitedPolls", () => Task.FromResult(PtyHasExitedPolls()));
failures += await RunAsync("PtyCancellationKill", PtyCancellationKill);
failures += await RunAsync("PtyCancellationWait", PtyCancellationWait);
failures += await RunAsync("PtyResizeUpdatesSize", () => Task.FromResult(PtyResizeUpdatesSize()));
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && TryResolvePwsh(out var pwshPath))
    failures += await RunAsync("PtyMatrixPwsh", () => PtyMatrixPwsh(pwshPath));
else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    failures += await RunAsync("PtyMatrixUnix", PtyMatrixUnix);

failures += await RunAsync("IntegrationPtyCmdFixture", () => Task.FromResult(IntegrationFixture(repoRoot, "pty-cmd.yaml", "pty-cmd-output")));
failures += await RunAsync("IntegrationPtyTtyFixture", () => Task.FromResult(IntegrationFixture(repoRoot, "pty-tty-check.yaml", "redirected=False")));
if (TryResolvePwsh(out var pwshForFixture))
    failures += await RunAsync("IntegrationMatrixFixture", () => Task.FromResult(IntegrationMatrixFixture(repoRoot, pwshForFixture)));

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

static async Task<bool> PtyEchoOutput()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe";
        var result = await PtyCapture.RunAsync(Spawn(cmd, ["/c", "echo pty-layer-echo"]));
        return result.ExitCode == 0 && result.Output.Contains("pty-layer-echo", StringComparison.Ordinal);
    }

    var shell = Environment.GetEnvironmentVariable("SHELL");
    if (string.IsNullOrWhiteSpace(shell))
        shell = "/bin/bash";

    var unix = await PtyCapture.RunAsync(Spawn(shell, ["-lc", "printf pty-layer-echo"]));
    return unix.ExitCode == 0 && unix.Output.Contains("pty-layer-echo", StringComparison.Ordinal);
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
        return result.Output.Contains(marker, StringComparison.Ordinal)
            && result.Output.Contains("aaa", StringComparison.Ordinal);
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    var unix = await PtyCapture.RunAsync(
        Spawn(shell, ["-lc", "cat"]),
        new PtyCaptureOptions { Completion = new() { Input = marker } });
    return unix.ExitCode == 0 && unix.Output.Contains(marker, StringComparison.Ordinal);
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
        return result.ExitCode == 0 && result.Output.Contains("redirected=False", StringComparison.OrdinalIgnoreCase);
    }

    var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash";
    var unix = await PtyCapture.RunAsync(Spawn(shell, ["-lc", "if [ -t 1 ]; then echo redirected=False; else echo redirected=True; fi"]));
    return unix.ExitCode == 0 && unix.Output.Contains("redirected=False", StringComparison.Ordinal);
}

static async Task<bool> PtyMatrixPwsh(string pwshPath)
{
    var result = await PtyCapture.RunAsync(Spawn(pwshPath, ["-NoLogo", "-Command", "matrix 3"], 80, 24));
    return result.ExitCode == 0
        && result.Chunks.Count >= 2
        && result.Output.Contains('\u001b')
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
