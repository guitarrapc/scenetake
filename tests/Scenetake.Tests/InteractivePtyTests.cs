using System.Diagnostics;
using Scenetake.TestSupport;

namespace Scenetake.Tests;

[Category("Smoke")]
public sealed class InteractivePtyTests
{
    [Test]
    public async Task InteractiveWithoutPtyRejected() => await Assert.That(await InteractivePtyCases.InteractiveWithoutPtyRejected()).IsTrue();

    [Test]
    public async Task NormalizeTypingRequiresInteractive() => await Assert.That(await InteractivePtyCases.NormalizeTypingRequiresInteractive()).IsTrue();

    [Test]
    public async Task InteractiveRequiresHostTty() => await Assert.That(await InteractivePtyCases.InteractiveRequiresHostTty()).IsTrue();
}

internal static class InteractivePtyCases
{

    internal static async Task<bool> InteractiveWithoutPtyRejected()
    {
        var env = TestEnv.Binary;

        var repoRoot = TestEnv.RepoRoot;
        var fixture = Path.Combine(repoRoot, "tests", "fixtures", "pty-interactive-without-pty.yaml");
        if (!File.Exists(fixture))
        {
            Console.Error.WriteLine($"InteractiveWithoutPtyRejected: missing fixture {fixture}");
            return false;
        }

        var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-interactive-invalid-{Guid.NewGuid():N}");
        var exitCode = await RunScenetake(env, fixture, outputStem);
        if (exitCode == 0)
            return false;

        return true;
    }

    internal static async Task<bool> NormalizeTypingRequiresInteractive()
    {
        var env = TestEnv.Binary;

        var repoRoot = TestEnv.RepoRoot;
        var fixture = Path.Combine(repoRoot, "tests", "fixtures", "pty-interactive-normalize-without-interactive.yaml");
        if (!File.Exists(fixture))
        {
            Console.Error.WriteLine($"NormalizeTypingRequiresInteractive: missing fixture {fixture}");
            return false;
        }

        var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-normalize-invalid-{Guid.NewGuid():N}");
        var exitCode = await RunScenetake(env, fixture, outputStem);
        return exitCode != 0;
    }

    internal static async Task<bool> InteractiveRequiresHostTty()
    {
        if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            Skip.Test("host is interactive (manual smoke covers attach path)");
            return true;
        }

        var env = TestEnv.Binary;

        var repoRoot = TestEnv.RepoRoot;
        var fixture = Path.Combine(repoRoot, "tests", "fixtures", "pty-interactive-tty.yaml");
        if (!File.Exists(fixture))
        {
            Console.Error.WriteLine($"InteractiveRequiresHostTty: missing fixture {fixture}");
            return false;
        }

        var outputStem = Path.Combine(Path.GetTempPath(), $"scenetake-interactive-tty-{Guid.NewGuid():N}");
        var exitCode = await RunScenetake(env, fixture, outputStem);
        return exitCode != 0;
    }

    internal static async Task<int> RunScenetake(string binaryPath, string scenarioPath, string outputStem)
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

}
