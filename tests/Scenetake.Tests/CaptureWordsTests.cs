namespace Scenetake.Tests;

// scenetake capture -- <words> (.github/docs/spec_cli.md → capture): the caller's shell has split the words, and they
// are quoted again for the shell that runs the step, so each reaches the command as it was given.

public sealed class CaptureWordsTests
{
    [Test]
    public async Task PlainWordsStayAsTheyAre() => await Assert.That(CaptureWordsCases.PlainWordsStayAsTheyAre()).IsTrue();

    [Test]
    public async Task WordsOneShellReadsAreQuotedForIt() => await Assert.That(CaptureWordsCases.WordsOneShellReadsAreQuotedForIt()).IsTrue();

    [Test]
    public async Task HelpDescriptionsStartInOneColumn() => await Assert.That(CaptureWordsCases.HelpDescriptionsStartInOneColumn()).IsTrue();
}

internal static class CaptureWordsCases
{
    private static bool Same(string what, string expected, string actual)
    {
        if (expected == actual)
            return true;

        Console.Error.WriteLine($"  {what}: expected <{expected}>, got <{actual}>");
        return false;
    }

    internal static bool PlainWordsStayAsTheyAre()
    {
        string[] words = ["npx", "@scope/tool", "--format=%h,%s", "a^b", "key=value"];
        return Same("bash", "npx @scope/tool --format=%h,%s a^b key=value", CaptureRequest.JoinWords(words, "bash"))
            & Same("pwsh", "npx @scope/tool --format=%h,%s a^b key=value", CaptureRequest.JoinWords(words, "pwsh"))
            & Same("a space and a quote", "echo 'a b' 'it'\\''s'", CaptureRequest.JoinWords(["echo", "a b", "it's"], "/bin/bash"));
    }

    internal static bool WordsOneShellReadsAreQuotedForIt()
    {
        // PowerShell passes @args as the values of $args, and --% ends its parsing. cmd reads ^ as an escape. zsh reads
        // =name at the start as the path of a command. Each is quoted for that shell only.
        return Same("pwsh splat", "echo '@args' @scope/x", CaptureRequest.JoinWords(["echo", "@args", "@scope/x"], "pwsh"))
            & Same("pwsh stop-parsing", "echo '--%' x", CaptureRequest.JoinWords(["echo", "--%", "x"], "powershell.exe"))
            & Same("bash keeps them", "echo @args --% =ls a^b", CaptureRequest.JoinWords(["echo", "@args", "--%", "=ls", "a^b"], "bash"))
            & Same("cmd caret", "echo \"a^b\"", CaptureRequest.JoinWords(["echo", "a^b"], "cmd.exe"))
            & Same("zsh equals", "echo '=ls' a=b", CaptureRequest.JoinWords(["echo", "=ls", "a=b"], "/bin/zsh"));
    }

    internal static bool HelpDescriptionsStartInOneColumn()
    {
        // Every option's description in --help starts in the same column, the one its next lines start in.
        string[] texts =
        [
            HelpText.Recording(), HelpText.Recording(capture: true), HelpText.Render(scenario: true), HelpText.Render(scenario: false),
            HelpText.Render(scenario: false, animation: false), HelpText.CaptureOptions, HelpText.SvgOptions, HelpText.StillOptions,
            HelpText.PngOptions, HelpText.Mp4Options, HelpText.GifOptions,
        ];
        var ok = true;
        foreach (var text in texts)
        {
            foreach (var line in text.Split('\n'))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"^  (--?\S+(?: \S+)?) {2,}(\S)");
                if (match.Success && match.Groups[2].Index != 27)
                {
                    Console.Error.WriteLine($"  starts in column {match.Groups[2].Index}, not 27: {line}");
                    ok = false;
                }
            }
        }

        return ok;
    }
}
