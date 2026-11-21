namespace Scenetake;

/// <summary>
/// The editor schema of the scenario format, schema/scenario.schema.json in the repository. A scenario that
/// <c>init</c> or <c>capture --save-scenario</c> writes names it on its first line, which editors that use
/// yaml-language-server read: keys and values to complete, a description on hover, and a mark on a misspelled key or a
/// wrong value while the file is edited, before anything runs.
/// </summary>
static class ScenarioSchema
{
    /// <summary>
    /// The schema at this version's tag, which lists the keys this binary reads. A schema on main would offer keys
    /// that an older binary warns about. A version that has no tag yet, as on a development build, gives a URL that
    /// is not found, and an editor then checks nothing.
    /// </summary>
    internal static string Url { get; } =
        $"https://raw.githubusercontent.com/guitarrapc/scenetake/v{typeof(ScenarioSchema).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}/schema/scenario.schema.json";

    /// <summary>The comment that names the schema, as yaml-language-server reads it. It ends with a line break.</summary>
    internal static string ModeLine { get; } = $"# yaml-language-server: $schema={Url}\n";
}
