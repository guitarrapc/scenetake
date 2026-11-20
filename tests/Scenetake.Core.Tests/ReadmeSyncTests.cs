using Scenetake.TestSupport;

namespace Scenetake.Core.Tests;

// The Scenario Format yaml block in each README is a copy of samples/scenario_format.yaml.
public sealed class ReadmeSyncTests
{
    [Test]
    [Arguments("README.md", "## Scenario Format")]
    [Arguments("README-ja.md", "## シナリオファイル形式")]
    public async Task ScenarioFormatBlockMatchesSample(string readme, string heading)
    {
        var expected = NormalizeNewlines(File.ReadAllText(TestEnv.RepoPath("samples", "scenario_format.yaml")));
        await Assert.That(YamlBlockAfter(File.ReadAllText(TestEnv.RepoPath(readme)), heading)).IsEqualTo(expected);
    }

    /// <summary>The first yaml code block after the heading, or null when the heading or the block is missing.</summary>
    private static string? YamlBlockAfter(string readme, string heading)
    {
        var section = readme.IndexOf(heading, StringComparison.Ordinal);
        if (section < 0)
            return null;

        var fence = readme.IndexOf("```yaml", section, StringComparison.Ordinal);
        if (fence < 0)
            return null;

        var start = fence + "```yaml".Length;
        if (start < readme.Length && readme[start] == '\r')
            start++;
        if (start < readme.Length && readme[start] == '\n')
            start++;

        var end = readme.IndexOf("```", start, StringComparison.Ordinal);
        return end < 0 ? null : NormalizeNewlines(readme[start..end]);
    }

    private static string NormalizeNewlines(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n') + '\n';
}
