using System.Text;
using static Scenetake.Core.ScenarioCast;
using static Scenetake.Core.Tests.SecretMaskCases;

namespace Scenetake.Core.Tests;

// Paths that show as a stand-in (.github/docs/spec_scenario.md → Paths in the recording): the home folder as ~ with
// mask-personal, and cwd as cwd-as gives it. The folders are a made-up person's, as the CLI would collect them.

public sealed class PathStandInTests
{
    [Test]
    public async Task EachSpellingShowsAsTheStandIn() => await Assert.That(PathStandInCases.EachSpellingShowsAsTheStandIn()).IsTrue();

    [Test]
    public async Task OnlyAWholePathIsReplaced() => await Assert.That(PathStandInCases.OnlyAWholePathIsReplaced()).IsTrue();

    [Test]
    public async Task PathsInAPtyStream() => await Assert.That(PathStandInCases.PathsInAPtyStream()).IsTrue();

    [Test]
    public async Task MaskPersonalTurnsOffTheHomeOnly() => await Assert.That(PathStandInCases.MaskPersonalTurnsOffTheHomeOnly()).IsTrue();

    [Test]
    public async Task RecordingShowsStandIns() => await Assert.That(PathStandInCases.RecordingShowsStandIns()).IsTrue();

    [Test]
    public async Task CwdAsKey() => await Assert.That(PathStandInCases.CwdAsKey()).IsTrue();
}

internal static class PathStandInCases
{
    private const string Settings = "  prompt: \"$ \"";

    private static PathStandIn Home(string folder) => new(folder, "~", PersonalDetail.HomeFolder);

    private static SecretMasker Masker(string yamlSettings, params PathStandIn[] paths) =>
        SecretMasker.ForScenario(ParseScenario($"settings:\n{yamlSettings}\nsteps:\n  - echo hi\n"), null, AdaDetails(), paths)
            ?? throw new InvalidOperationException("no masker");

    internal static bool EachSpellingShowsAsTheStandIn()
    {
        var unix = Masker(Settings, Home("/home/adal"));
        // The CLI gives the profile's 8.3 form too, which Windows writes in TEMP.
        var windows = Masker(Settings, Home(@"C:\Users\adal"), Home(@"C:\Users\ADAL~1"));
        // cwd under the home folder: its own stand-in wins, written with each spelling's separators.
        var cwd = Masker(Settings, new PathStandIn(@"C:\Users\adal\src\demo\", "~/my-app", null), Home(@"C:\Users\adal"));
        return Same("pwd", "~/src/app\n", unix.MaskText("/home/adal/src/app\n"))
            & Same("the home folder itself", "cd ~ && ls ~/", unix.MaskText("cd /home/adal && ls /home/adal/"))
            & Same("PATH", "PATH=~/bin:~/.local/bin:/usr/bin", unix.MaskText("PATH=/home/adal/bin:/home/adal/.local/bin:/usr/bin"))
            & Same("quoted and at the end of a sentence", "'~/x' \"~\" in ~.", unix.MaskText("'/home/adal/x' \"/home/adal\" in /home/adal."))
            & Same("Windows, other case", @"~\AppData", windows.MaskText(@"c:\users\Adal\AppData"))
            & Same("forward slashes", "~/AppData", windows.MaskText("C:/Users/adal/AppData"))
            & Same("Git Bash", "~/src", windows.MaskText("/c/Users/ADAL/src"))
            & Same("JSON", @"{""dir"":""~\\x""}", windows.MaskText(@"{""dir"":""C:\\Users\\adal\\x""}"))
            & Same("8.3 name", @"~\AppData\Local\Temp", windows.MaskText(@"C:\Users\ADAL~1\AppData\Local\Temp"))
            & Same("cwd-as under the home folder", @"~\my-app\src and ~\other", cwd.MaskText(@"C:\Users\adal\src\demo\src and C:\Users\adal\other"))
            & Same("cwd-as in Git Bash", "~/my-app", cwd.MaskText("/c/Users/adal/src/demo"))
            & Same("cwd-as in JSON", @"""~\\my-app""", cwd.MaskText(@"""C:\\Users\\adal\\src\\demo"""))
            & Same("masked twice", "~/x", unix.MaskText(unix.MaskText("/home/adal/x")))
            & Expect(!unix.Hides("~/src") && unix.Hides("/home/adal"), "text with stand-ins has nothing left to replace");
    }

    internal static bool OnlyAWholePathIsReplaced()
    {
        var unix = Masker(Settings, Home("/home/adal"));
        var windows = Masker(Settings, Home(@"C:\Users\adal"));
        // A root folder starts every path, so it has no stand-in.
        var root = Masker(Settings, Home("/"), Home(@"C:\"), Home("relative/home"));
        // Where the path is not replaced, the user name is starred as a word, as before.
        return Same("another folder", "/home/adal2/x /home/adal-work /home/adal_x", unix.MaskText("/home/adal2/x /home/adal-work /home/adal_x"))
            & Same("a folder with a dot in its name", "/home/****.old", unix.MaskText("/home/adal.old"))
            & Same("inside another path", "/mnt/home/**** x/home/**** ./home/****", unix.MaskText("/mnt/home/adal x/home/adal ./home/adal"))
            & Same("WSL's path to the Windows home", "/mnt/c/Users/****", windows.MaskText("/mnt/c/Users/adal"))
            & Same("a long path", @"\\?\C:\Users\****", windows.MaskText(@"\\?\C:\Users\adal"))
            // A file URL is a path, not a web address where a published name stays.
            & Same("a file URL", "file:///C:/Users/****/x file:///home/****", windows.MaskText("file:///C:/Users/adal/x file:///home/adal"))
            & Same("root folders", @"/usr/bin C:\Windows relative/home/x", root.MaskText(@"/usr/bin C:\Windows relative/home/x"));
    }

    internal static bool PathsInAPtyStream()
    {
        var masker = Masker(Settings, Home("/home/adal"));
        return Same("split across reads", "~|/x\r\n", MaskPty(masker, "/home/ad", "al/x\r\n"))
            // The colors inside the path stay, so the text after it keeps its own.
            & Same("colored in parts", "\u001b[34m~\u001b[1m\u001b[0m/x", MaskPty(masker, "\u001b[34m/home/\u001b[1madal\u001b[0m/x"))
            // A full-screen program places text by column: there the width stays, and the name is starred.
            & Same("alternate screen", "\u001b[?1049h/home/****/x\u001b[?1049l~/y", MaskPty(masker, "\u001b[?1049h/home/adal/x\u001b[?1049l/home/adal/y"))
            & Same("alternate screen, mode 47 among others", "\u001b[?1;47h/home/****\u001b[?47l~", MaskPty(masker, "\u001b[?1;47h/home/adal\u001b[?47l/home/adal"))
            // A path that a cursor move cuts was drawn in parts.
            & Same("cut by a cursor move", "/home/\u001b[7G****", MaskPty(masker, "/home/\u001b[7Gadal"))
            // ConPTY writes a run of spaces as a move to the right.
            & Same("followed by a move to the right", "~\u001b[1Cx", MaskPty(masker, "/home/adal\u001b[1Cx"));
    }

    internal static bool MaskPersonalTurnsOffTheHomeOnly()
    {
        var off = Masker("  mask-personal: false", new PathStandIn("/home/adal/demo", "~/my-app", null), Home("/home/adal"));
        var none = SecretMasker.ForScenario(ParseScenario("settings:\n  mask-personal: false\n  mask-auto: false\nsteps:\n  - echo hi\n"), null, AdaDetails(), [Home("/home/adal")]);
        var counted = Masker(Settings, new PathStandIn("/home/adal/demo", "~/my-app", null), Home("/home/adal"));
        counted.MaskText("/home/adal/demo");
        var cwdOnly = counted.PersonalSummary();
        counted.MaskUncounted("/home/adal/x");
        var uncounted = counted.PersonalSummary();
        counted.MaskText("/home/adal/x");
        return Same("cwd-as stays on", "~/my-app /home/adal/x", off.MaskText("/home/adal/demo /home/adal/x"))
            & Expect(none is null, "the home folder alone gives no masker with mask-personal: false")
            & Expect(cwdOnly is null && uncounted is null, $"cwd-as and uncounted text name no personal detail: {cwdOnly}, {uncounted}")
            & Same("summary", "Masked personal details: home folder as ~", counted.PersonalSummary() ?? "(none)");
    }

    internal static bool RecordingShowsStandIns()
    {
        var scenario = ParseScenario("settings:\n  prompt: \"$ \"\nsteps:\n  - name: read /home/adal/notes.txt\n    run: wc -l /home/adal/notes.txt\n");
        var masker = SecretMasker.ForScenario(scenario, null, AdaDetails(), [Home("/home/adal")]);
        var (events, _) = GenerateAsync(scenario, new GenerateOptions { LogRunning = false, Masker = masker }, step => Task.FromResult(CommandExecution.FromPipeStream(
            "3 /home/adal/notes.txt\n", "", 0, [new PipeStreamChunk(TimeSpan.Zero, false, "3 /home/adal/notes.txt\n")]))).GetAwaiter().GetResult();
        var shown = string.Concat(events.Select(static ev => ev.Kind == CastEventKind.Marker ? $"[{ev.Data}]" : ev.HasUtf8Output ? Encoding.UTF8.GetString(ev.Utf8Output.Span) : ev.Data));
        return Expect(!shown.Contains("adal", StringComparison.Ordinal), $"the cast shows the home folder: {Show(shown)}")
            & Expect(shown.Contains("[read ~/notes.txt]", StringComparison.Ordinal), $"the marker: {Show(shown)}")
            & Expect(shown.Contains("wc -l ~/notes.txt\r\n", StringComparison.Ordinal), $"the typed command: {Show(shown)}")
            & Expect(shown.Contains("3 ~/notes.txt", StringComparison.Ordinal), $"the output: {Show(shown)}");
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
            & Expect(PromptTemplate.SameFolder("/home/adal/", "/home/adal") && !PromptTemplate.SameFolder("/home/adal", "/home/adal2"), "SameFolder");
    }
}
