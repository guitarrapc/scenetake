using System.Text;
using static Scenetake.Core.ScenarioCast;
using static Scenetake.Core.Tests.SecretMaskCases;

namespace Scenetake.Core.Tests;

// The path that shows as a stand-in (.github/docs/spec_scenario.md → Paths in the recording): cwd as cwd-as gives it.
// The folders are a made-up person's, as the CLI would collect them. The home folder has no stand-in, and its path
// shows as it is.

public sealed class PathStandInTests
{
    [Test]
    public async Task EachSpellingShowsAsTheStandIn() => await Assert.That(PathStandInCases.EachSpellingShowsAsTheStandIn()).IsTrue();

    [Test]
    public async Task OnlyAWholePathIsReplaced() => await Assert.That(PathStandInCases.OnlyAWholePathIsReplaced()).IsTrue();

    [Test]
    public async Task PathsInAPtyStream() => await Assert.That(PathStandInCases.PathsInAPtyStream()).IsTrue();

    [Test]
    public async Task CwdAsIsNoPersonalDetail() => await Assert.That(PathStandInCases.CwdAsIsNoPersonalDetail()).IsTrue();

    [Test]
    public async Task RecordingShowsStandIns() => await Assert.That(PathStandInCases.RecordingShowsStandIns()).IsTrue();

    [Test]
    public async Task CwdAsKey() => await Assert.That(PathStandInCases.CwdAsKey()).IsTrue();
}

internal static class PathStandInCases
{
    private const string Settings = "  prompt: \"$ \"";

    private static PathStandIn Cwd(string folder) => new(folder, "~/my-app");

    private static SecretMasker Masker(string yamlSettings, params PathStandIn[] paths) =>
        SecretMasker.ForScenario(ParseScenario($"settings:\n{yamlSettings}\nsteps:\n  - echo hi\n"), null, AdaDetails(), paths)
            ?? throw new InvalidOperationException("no masker");

    internal static bool EachSpellingShowsAsTheStandIn()
    {
        var unix = Masker(Settings, Cwd("/home/adal/demo"));
        // The CLI gives the folder's 8.3 form too, which Windows writes in TEMP.
        var windows = Masker(Settings, Cwd(@"C:\Users\adal\demo\"), Cwd(@"C:\Users\ADAL~1\demo"));
        return Same("pwd", "~/my-app/src\n", unix.MaskText("/home/adal/demo/src\n"))
            & Same("the folder itself", "cd ~/my-app && ls ~/my-app/", unix.MaskText("cd /home/adal/demo && ls /home/adal/demo/"))
            & Same("PATH", "PATH=~/my-app/bin:/usr/bin", unix.MaskText("PATH=/home/adal/demo/bin:/usr/bin"))
            & Same("quoted and at the end of a sentence", "'~/my-app/x' \"~/my-app\" in ~/my-app.", unix.MaskText("'/home/adal/demo/x' \"/home/adal/demo\" in /home/adal/demo."))
            & Same("the home folder above it", "/home/adal and /home/adal/other", unix.MaskText("/home/adal and /home/adal/other"))
            & Same("Windows, other case", @"~\my-app\src", windows.MaskText(@"c:\users\Adal\Demo\src"))
            & Same("forward slashes", "~/my-app/src", windows.MaskText("C:/Users/adal/demo/src"))
            & Same("Git Bash", "~/my-app/src", windows.MaskText("/c/Users/ADAL/demo/src"))
            & Same("JSON", @"{""dir"":""~\\my-app\\x""}", windows.MaskText(@"{""dir"":""C:\\Users\\adal\\demo\\x""}"))
            & Same("8.3 name", @"~\my-app\src", windows.MaskText(@"C:\Users\ADAL~1\demo\src"))
            & Same("masked twice", "~/my-app/x", unix.MaskText(unix.MaskText("/home/adal/demo/x")))
            & Expect(!unix.Hides("~/my-app/src") && unix.Hides("/home/adal/demo"), "text with stand-ins has nothing left to replace");
    }

    internal static bool OnlyAWholePathIsReplaced()
    {
        var unix = Masker(Settings, Cwd("/home/adal/demo"));
        var windows = Masker(Settings, Cwd(@"C:\Users\adal\demo"));
        // A root folder starts every path, so it has no stand-in.
        var root = Masker(Settings, Cwd("/"), Cwd(@"C:\"), Cwd("relative/demo"));
        return Same("another folder", "/home/adal/demo2/x /home/adal/demo-work /home/adal/demo_x", unix.MaskText("/home/adal/demo2/x /home/adal/demo-work /home/adal/demo_x"))
            & Same("a folder with a dot in its name", "/home/adal/demo.old", unix.MaskText("/home/adal/demo.old"))
            & Same("inside another path", "/mnt/home/adal/demo x/home/adal/demo ./home/adal/demo", unix.MaskText("/mnt/home/adal/demo x/home/adal/demo ./home/adal/demo"))
            & Same("WSL's path to the Windows folder", "/mnt/c/Users/adal/demo", windows.MaskText("/mnt/c/Users/adal/demo"))
            & Same("a long path", @"\\?\C:\Users\adal\demo", windows.MaskText(@"\\?\C:\Users\adal\demo"))
            & Same("a file URL", "file:///C:/Users/adal/demo/x", windows.MaskText("file:///C:/Users/adal/demo/x"))
            & Same("root folders", @"/usr/bin C:\Windows relative/demo/x", root.MaskText(@"/usr/bin C:\Windows relative/demo/x"));
    }

    internal static bool PathsInAPtyStream()
    {
        var masker = Masker(Settings, Cwd("/home/adal/demo"));
        return Same("split across reads", "~/my-app|/x\r\n", MaskPty(masker, "/home/adal/de", "mo/x\r\n"))
            // The colors inside the path stay, so the text after it keeps its own.
            & Same("colored in parts", "\u001b[34m~/my-app\u001b[1m\u001b[0m/x", MaskPty(masker, "\u001b[34m/home/adal/\u001b[1mdemo\u001b[0m/x"))
            // A full-screen program places text by column: there the path keeps its width, and it shows as it is.
            & Same("alternate screen", "\u001b[?1049h/home/adal/demo/x\u001b[?1049l~/my-app/y", MaskPty(masker, "\u001b[?1049h/home/adal/demo/x\u001b[?1049l/home/adal/demo/y"))
            & Same("alternate screen, mode 47 among others", "\u001b[?1;47h/home/adal/demo\u001b[?47l~/my-app", MaskPty(masker, "\u001b[?1;47h/home/adal/demo\u001b[?47l/home/adal/demo"))
            // A path that a cursor move cuts was drawn in parts.
            & Same("cut by a cursor move", "/home/adal/\u001b[12Gdemo", MaskPty(masker, "/home/adal/\u001b[12Gdemo"))
            // ConPTY writes a run of spaces as a move to the right.
            & Same("followed by a move to the right", "~/my-app\u001b[1Cx", MaskPty(masker, "/home/adal/demo\u001b[1Cx"));
    }

    internal static bool CwdAsIsNoPersonalDetail()
    {
        var off = Masker("  mask-personal: false", Cwd("/home/adal/demo"));
        var alone = SecretMasker.ForScenario(ParseScenario("settings:\n  mask-personal: false\n  mask-auto: false\nsteps:\n  - echo hi\n"), null, AdaDetails(), [Cwd("/home/adal/demo")]);
        var counted = Masker(Settings, Cwd("/home/adal/demo"));
        counted.MaskText("/home/adal/demo /home/adal/x");
        return Same("cwd-as stays on", "~/my-app /home/adal/x", off.MaskText("/home/adal/demo /home/adal/x"))
            & Expect(alone is not null && alone.MaskText("/home/adal/demo") == "~/my-app", "cwd-as alone gives a masker")
            & Expect(counted.PersonalSummary() is null && counted.Summary() is null, $"cwd-as names no detail: {counted.PersonalSummary()}, {counted.Summary()}");
    }

    internal static bool RecordingShowsStandIns()
    {
        var scenario = ParseScenario("settings:\n  prompt: \"$ \"\nsteps:\n  - name: read /home/adal/demo/notes.txt\n    run: wc -l /home/adal/demo/notes.txt /home/adal/todo.txt\n");
        var masker = SecretMasker.ForScenario(scenario, null, AdaDetails(), [Cwd("/home/adal/demo")]);
        var (events, _) = GenerateAsync(scenario, new GenerateOptions { LogRunning = false, Masker = masker }, step => Task.FromResult(CommandExecution.FromPipeStream(
            "3 /home/adal/demo/notes.txt\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "3 /home/adal/demo/notes.txt\n")]))).GetAwaiter().GetResult();
        var shown = string.Concat(events.Select(static ev => ev.Kind == CastEventKind.Marker ? $"[{ev.Data}]" : ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
        return Expect(!shown.Contains("/home/adal/demo", StringComparison.Ordinal), $"the cast shows cwd: {Show(shown)}")
            & Expect(shown.Contains("[read ~/my-app/notes.txt]", StringComparison.Ordinal), $"the marker: {Show(shown)}")
            & Expect(shown.Contains("wc -l ~/my-app/notes.txt /home/adal/todo.txt\r\n", StringComparison.Ordinal), $"the typed command, with the home folder as it is: {Show(shown)}")
            & Expect(shown.Contains("3 ~/my-app/notes.txt", StringComparison.Ordinal), $"the output: {Show(shown)}");
    }

    internal static bool CwdAsKey()
    {
        var scenario = ParseScenario("cwd: ./demo\ncwd-as: ~/my-app\nsteps:\n  - pwd\n");
        var unknown = FindUnknownScenarioKeys("cwd-as: ~/my-app\nsteps:\n  - pwd\n");
        string Error(string value) => FindStepKeyError(ParseScenario($"cwd-as: {value}\nsteps:\n  - pwd\n")) ?? "(none)";
        return Same("read", "~/my-app", scenario.CwdAs ?? "(none)")
            & Expect(unknown.Count == 0, $"cwd-as is no unknown key: {string.Join("; ", unknown)}")
            & Same("blank", "cwd-as cannot be empty: give the path the recording shows for cwd, such as ~/my-app.", Error("\"  \""))
            & Same("line break", "cwd-as cannot hold a line break or another control character: give one path, such as ~/my-app.", Error("\"a\\nb\""))
            & Same("a path", "(none)", Error("/work/my-app"))
            // The prompt's {dir} at cwd shows the stand-in's last name.
            & Same("{dir} of a stand-in", "my-app", PromptTemplate.FolderName("~/my-app/", "~"))
            & Same("{dir} of ~", "~", PromptTemplate.FolderName("~", "~"))
            & Same("{dir} of a Windows stand-in", "demo", PromptTemplate.FolderName(@"C:\demo", "~"))
            & Expect(PromptTemplate.SameFolder("/home/adal/", "/home/adal") && !PromptTemplate.SameFolder("/home/adal", "/home/adal2"), "SameFolder")
            // Windows and macOS ignore case in a path, so /users/adal is the home folder there. Linux does not.
            & Expect(PromptTemplate.SameFolder("/users/adal", "/Users/adal") == (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()), "SameFolder and case");
    }
}
