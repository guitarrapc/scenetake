#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:property AllowUnsafeBlocks=true
#:package VYaml@1.3.0
#:package MiniPty@1.0.4-local
#:package MiniPty.Capture@1.0.4-local
#:package MiniPty.Console@1.0.4-local

using System.Diagnostics;
using System.Text;
using VYaml.Serialization;

var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
var failures = 0;

failures += await RunAsync("InteractiveWithoutPtyRejected", InteractiveWithoutPtyRejected);
failures += await RunAsync("InteractiveRequiresHostTty", InteractiveRequiresHostTty);

return failures == 0 ? 0 : 1;

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

static async Task<bool> InteractiveWithoutPtyRejected()
{
    var env = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (string.IsNullOrWhiteSpace(env) || !File.Exists(env))
    {
        Console.Error.WriteLine("skip InteractiveWithoutPtyRejected: set SCENETAKE_BIN to a published scenetake binary");
        return true;
    }

    var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
    var fixture = Path.Combine(repoRoot, "tests", "fixtures", "pty-interactive-without-pty.yaml");
    if (!File.Exists(fixture))
    {
        Console.Error.WriteLine($"skip InteractiveWithoutPtyRejected: missing fixture {fixture}");
        return true;
    }

    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-interactive-invalid-{Guid.NewGuid():N}");
    var exitCode = await RunScenetake(env, fixture, outputStem);
    if (exitCode == 0)
        return false;

    return true;
}

static async Task<bool> InteractiveRequiresHostTty()
{
    if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
    {
        Console.Error.WriteLine("skip InteractiveRequiresHostTty: host is interactive (manual smoke covers attach path)");
        return true;
    }

    var env = Environment.GetEnvironmentVariable("SCENETAKE_BIN");
    if (string.IsNullOrWhiteSpace(env) || !File.Exists(env))
    {
        Console.Error.WriteLine("skip InteractiveRequiresHostTty: set SCENETAKE_BIN to a published scenetake binary");
        return true;
    }

    var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
    var fixture = Path.Combine(repoRoot, "tests", "fixtures", "pty-interactive-tty.yaml");
    if (!File.Exists(fixture))
    {
        Console.Error.WriteLine($"skip InteractiveRequiresHostTty: missing fixture {fixture}");
        return true;
    }

    var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-interactive-tty-{Guid.NewGuid():N}");
    var exitCode = await RunScenetake(env, fixture, outputStem);
    return exitCode != 0;
}

static async Task<int> RunScenetake(string binaryPath, string scenarioPath, string outputStem)
{
    var psi = new ProcessStartInfo(binaryPath)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    psi.ArgumentList.Add(scenarioPath);
    psi.ArgumentList.Add(outputStem);

    using var proc = Process.Start(psi)!;
    var stderr = await proc.StandardError.ReadToEndAsync();
    await proc.WaitForExitAsync();
    if (proc.ExitCode != 0)
        Console.Error.WriteLine(stderr.TrimEnd());
    return proc.ExitCode;
}

static string FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "scenetake.cs")))
            return dir.FullName;
        dir = dir.Parent;
    }

    throw new InvalidOperationException("Could not find scenetake repo root");
}
