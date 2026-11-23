using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Scenetake.TestSupport;
using VYaml.Serialization;
using EvaluationOptions = Json.Schema.EvaluationOptions;
using JsonSchema = Json.Schema.JsonSchema;
using SchemaOutput = Json.Schema.OutputFormat;

namespace Scenetake.Tests;

// The editor schema, schema/scenario.schema.json (.github/docs/spec_scenario.md → Editor schema), against the code that
// reads a scenario: the same keys and names, no mark on a scenario that scenetake records, and a mark on the mistakes
// that scenetake stops at or warns about.

public sealed class ScenarioSchemaTests
{
    [Test]
    public async Task KeysMatchTheCode() => await Assert.That(ScenarioSchemaCases.KeysMatchTheCode()).IsTrue();

    [Test]
    public async Task NamesMatchTheCode() => await Assert.That(ScenarioSchemaCases.NamesMatchTheCode()).IsTrue();

    [Test]
    public async Task KeysEachKindTakesMatchTheCode() => await Assert.That(ScenarioSchemaCases.KeysEachKindTakesMatchTheCode()).IsTrue();

    [Test]
    public async Task SamplesAndFixturesPass() => await Assert.That(ScenarioSchemaCases.SamplesAndFixturesPass()).IsTrue();

    [Test]
    public async Task DocumentedExamplesPass() => await Assert.That(ScenarioSchemaCases.DocumentedExamplesPass()).IsTrue();

    [Test]
    public async Task MarksMistakes() => await Assert.That(ScenarioSchemaCases.MarksMistakes()).IsTrue();

    [Test]
    public async Task TakesWhatScenetakeReads() => await Assert.That(ScenarioSchemaCases.TakesWhatScenetakeReads()).IsTrue();

    [Test]
    public async Task SamplesNameTheSchema() => await Assert.That(ScenarioSchemaCases.SamplesNameTheSchema()).IsTrue();

    [Test]
    public async Task InitAndSavedScenariosNameTheSchema() => await Assert.That(ScenarioSchemaCases.InitAndSavedScenariosNameTheSchema(TestEnv.Binary)).IsTrue();

    [Test]
    public async Task RefusedOptionsMatchTheCli() => await Assert.That(ScenarioSchemaCases.RefusedOptionsMatchTheCli(TestEnv.Binary)).IsTrue();
}

internal static class ScenarioSchemaCases
{
    private static readonly string SchemaText = File.ReadAllText(TestEnv.RepoPath("schema", "scenario.schema.json"));
    private static readonly JsonNode Document = JsonNode.Parse(SchemaText)!;
    private static readonly JsonSchema Schema = JsonSchema.FromText(SchemaText);

    /// <summary>Fixtures for an error found before anything runs. The schema marks each of them too.</summary>
    private static readonly string[] InvalidFixtures =
    [
        "pty-interactive-normalize-without-interactive.yaml",
        "pty-interactive-without-pty.yaml",
        "step-input-keys-without-pty.yaml",
    ];

    /// <summary>Key names in the schema's list for completion only: one of each form that the pattern takes.</summary>
    private static readonly string[] KeyExamples = ["ctrl+c", "ctrl+d", "alt+x"];

    /// <summary>
    /// The options an outputs entry of each format refuses, as the CLI's checks before anything runs refuse them
    /// (<c>outputs entry 1 (svg): 'fps' requires format mp4 or gif</c>). <see cref="RefusedOptionsMatchTheCli"/> runs them.
    /// </summary>
    private static readonly Dictionary<string, string[]> RefusedOptions = new()
    {
        ["svg"] = ["fps", "crf", "max-size", "size", "font-file"],
        ["png"] = ["max-fps", "fps", "crf", "max-size", "size"],
        ["mp4"] = ["at", "max-fps"],
        ["gif"] = ["at", "max-fps", "crf"],
    };

    internal static bool KeysMatchTheCode()
    {
        var known = ScenarioCast.KnownSectionKeys();
        var sections = new (string Where, JsonNode? Properties, IEnumerable<string> Keys)[]
        {
            ("the top level", Document["properties"], known.TopLevel),
            ("settings", Definition("settings")["properties"], known.Settings),
            ("render", Definition("render")["properties"], known.Render),
            ("render.theme", Definition("theme")["properties"], known.Theme),
            ("a step", Definition("step")["properties"], ScenarioKeys.Step),
            ("a highlight entry", Definition("highlight")["properties"], ScenarioKeys.Highlight),
            ("a keys entry", Definition("keyEntry")["properties"], ScenarioKeys.KeyEntry),
            ("a prompt part", Definition("settings")["properties"]!["prompt"]!["items"]!["properties"], ScenarioKeys.PromptPart),
            ("an outputs entry", Definition("output")["properties"], ScenarioKeys.OutputEntry),
        };

        var ok = true;
        foreach (var (where, properties, keys) in sections)
        {
            var inSchema = properties!.AsObject().Select(property => property.Key).ToHashSet(StringComparer.Ordinal);
            var inCode = keys.ToHashSet(StringComparer.Ordinal);
            ok &= Check(inSchema.SetEquals(inCode),
                $"{where}: the code reads [{string.Join(", ", inCode.Except(inSchema))}] that the schema lacks, the schema has [{string.Join(", ", inSchema.Except(inCode))}] that the code does not read");

            // Every key shows a description on hover, its own or its definition's.
            var undescribed = properties.AsObject().Where(property => !Describes(property.Value)).Select(property => property.Key).ToList();
            ok &= Check(undescribed.Count == 0, $"{where}: no description for [{string.Join(", ", undescribed)}]");
        }

        return ok;
    }

    internal static bool NamesMatchTheCode()
    {
        var ok = CheckNames("themePreset", RenderSettingsResolver.PresetNames);
        ok &= CheckNames("windowName", [RenderSettingsResolver.NoneWindowName, RenderSettingsResolver.MacosWindowName, RenderSettingsResolver.WindowsWindowName]);
        ok &= CheckNames("fitMode", Enum.GetNames<FitMode>().Select(name => name.ToLowerInvariant()));
        ok &= CheckNames("showValue", Enum.GetNames<StepShow>().Select(name => name.ToLowerInvariant()));
        ok &= CheckNames("outputFormat", Enum.GetValues<OutputFormat>().Where(format => format != OutputFormat.Cast).Select(format => format.ToString().ToLowerInvariant()));

        // Key names: the table's names, then ctrl+ with a letter and alt+ with a printable character, around spaces,
        // as ScenarioCast.KeyText trims the name.
        var keyName = Definition("keyName")["anyOf"]!.AsArray();
        var listed = keyName[0]!["enum"]!.AsArray().Select(value => (string)value!).ToList();
        var named = listed.Except(KeyExamples).ToList();
        ok &= Check(named.ToHashSet().SetEquals(ScenarioCast.KeyNames) && KeyExamples.All(listed.Contains),
            $"keyName: the schema lists [{string.Join(", ", named)}], the code [{string.Join(", ", ScenarioCast.KeyNames)}]");
        var keyPattern = $"^\\s*(?:{string.Join("|", named.Select(AnyCase))}|{AnyCase("ctrl")}\\+[a-zA-Z]|{AnyCase("alt")}\\+[!-~])\\s*$";
        ok &= Check((string?)keyName[1]!["pattern"] == keyPattern, $"keyName: the pattern should be {keyPattern}");
        foreach (var name in new[] { "Enter", " page-DOWN ", "F12", "Ctrl+C", "alt+X" })
            ok &= Check(Regex.IsMatch(name, keyPattern) && ScenarioCast.KeyText(name) is not null, $"keyName: '{name}'");
        foreach (var name in new[] { "ctrl+1", "f13", "alt+xy", "enterr" })
            ok &= Check(!Regex.IsMatch(name, keyPattern) && ScenarioCast.KeyText(name) is null, $"keyName: '{name}' is not a key");

        // The conditions on a format and on show name one value each, in any case.
        var formats = Definition("output")["allOf"]!.AsArray().Select(entry => (string?)entry!["if"]!["properties"]!["format"]!["pattern"]).ToHashSet();
        ok &= Check(formats.SetEquals(new[] { "svg", "png", "mp4", "gif" }.Select(word => $"^{AnyCase(word)}$")), $"outputs entry conditions: {string.Join(", ", formats)}");
        var shows = Definition("step")["allOf"]!.AsArray()
            .Select(entry => (string?)entry!["if"]?["properties"]?["show"]?["pattern"])
            .OfType<string>()
            .ToHashSet();
        ok &= Check(shows.SetEquals(new[] { "none", "output" }.Select(word => $"^{AnyCase(word)}$")), $"step conditions on show: {string.Join(", ", shows)}");
        return ok;
    }

    internal static bool KeysEachKindTakesMatchTheCode()
    {
        // A card, clear, background, interactive, show: none, or show: output step takes fewer keys than other steps,
        // and an outputs entry fewer options than every format has. The schema lists what each takes, so an editor
        // shows the rule's own message.
        var rules = Definition("step")["allOf"]!.AsArray().Select(rule => rule!).ToList();
        var card = rules.Single(rule => Names(rule["if"]?["required"]) is ["card"]);
        var ok = CheckList("a card step", card["then"]!["propertyNames"]!["enum"], ScenarioCast.CardStepKeys);
        ok &= CheckList("a clear step", card["else"]!["then"]!["propertyNames"]!["enum"], ScenarioCast.ClearStepKeys);
        ok &= Check(Names(card["else"]!["else"]!["required"]) is ["run"], "a step that is neither a card nor a clear step needs run");
        var background = rules.Single(rule => Names(rule["if"]?["required"]) is ["background"]);
        ok &= CheckList("a background step", background["then"]!["propertyNames"]!["enum"], ["run", .. ScenarioCast.BackgroundStepKeys]);

        // The rules for keys that do not apply hold only when such a key is there, so a misspelled key gets the one
        // message of an unknown key.
        var interactive = rules.Single(rule => Names(rule["if"]?["required"]) is ["interactive"] && rule["then"]?["propertyNames"] is not null);
        ok &= CheckRefused("interactive: true", interactive, ScenarioKeys.Step, ["keys", "timeout"]);
        var hidden = rules.Single(rule => (string?)rule["if"]?["properties"]?["show"]?["pattern"] == $"^{AnyCase("none")}$");
        ok &= CheckRefused("show: none", hidden, ScenarioKeys.Step, ScenarioCast.ShownOnlyKeys);
        var output = rules.Single(rule => (string?)rule["if"]?["properties"]?["show"]?["pattern"] == $"^{AnyCase("output")}$");
        ok &= CheckList("show: output", output["then"]!["propertyNames"]!["enum"], ScenarioKeys.Step.Except(["run-highlight"]));
        ok &= Check(Names(output["if"]!["required"]) is ["show", "run-highlight"], "show: output: the rule should hold when run-highlight is there");
        var keyEntry = Definition("keyEntry");
        ok &= CheckList("a keys entry with type", keyEntry["then"]!["propertyNames"]!["enum"], ScenarioKeys.KeyEntry.Except(["key"]));
        ok &= Check(Names(keyEntry["if"]!["required"]) is ["type", "key"], "a keys entry: the rule should hold when both type and key are there");

        foreach (var (format, refused) in RefusedOptions)
        {
            var rule = Definition("output")["allOf"]!.AsArray().Single(entry => (string?)entry!["if"]!["properties"]!["format"]!["pattern"] == $"^{AnyCase(format)}$")!;
            ok &= CheckRefused($"a {format} entry", rule, ScenarioKeys.OutputEntry, refused);
        }

        return ok;
    }

    internal static bool SamplesAndFixturesPass()
    {
        var samples = Directory.EnumerateFiles(TestEnv.RepoPath("samples"), "*.yaml", SearchOption.AllDirectories).ToList();
        var fixtures = Directory.EnumerateFiles(TestEnv.RepoPath("tests", "fixtures"), "*.yaml").ToList();
        var ok = Check(samples.Count >= 20 && fixtures.Count >= 40, $"found {samples.Count} samples and {fixtures.Count} fixtures");
        foreach (var file in samples.Concat(fixtures))
        {
            var problems = Problems(ReadYaml(File.ReadAllText(file)));
            ok &= InvalidFixtures.Contains(Path.GetFileName(file))
                ? Check(problems.Count != 0, $"{Path.GetFileName(file)} tests an error before anything runs, and the schema does not mark it")
                : Check(problems.Count == 0, $"{Path.GetRelativePath(TestEnv.RepoRoot, file)}:{Environment.NewLine}    {string.Join(Environment.NewLine + "    ", problems)}");
        }

        return ok;
    }

    internal static bool DocumentedExamplesPass()
    {
        // The yaml blocks a reader copies from: the READMEs and the agent skill. A block that is part of a scenario,
        // such as a render or an outputs section alone, gets a step. A block of another file, such as a workflow, is
        // not a scenario and is skipped.
        var topLevel = ScenarioCast.KnownSectionKeys().TopLevel;
        var ok = true;
        var checkedBlocks = 0;
        foreach (var document in new[] { "README.md", "README-ja.md", Path.Combine("skills", "scenetake", "SKILL.md") })
        {
            var text = File.ReadAllText(TestEnv.RepoPath(document)).Replace("\r\n", "\n");
            foreach (Match block in Regex.Matches(text, "^```yaml\\n(.*?)^```", RegexOptions.Multiline | RegexOptions.Singleline))
            {
                var line = text[..block.Index].Count(c => c == '\n') + 1;
                object? root;
                try
                {
                    root = ReadYaml(block.Groups[1].Value);
                }
                catch (Exception ex)
                {
                    ok &= Check(false, $"{document}:{line}: not YAML: {ex.Message}");
                    continue;
                }

                if (root is not Dictionary<object, object?> map || !map.Keys.All(key => topLevel.Contains(key?.ToString() ?? "")))
                    continue;

                map.TryAdd("steps", new List<object?> { "echo" });
                var problems = Problems(map);
                checkedBlocks++;
                ok &= Check(problems.Count == 0, $"{document}:{line}:{Environment.NewLine}    {string.Join(Environment.NewLine + "    ", problems)}");
            }
        }

        return ok & Check(checkedBlocks >= 20, $"checked {checkedBlocks} yaml blocks");
    }

    internal static bool MarksMistakes()
    {
        // What scenetake stops at before anything runs, or warns about, the schema marks while the file is edited.
        var mistakes = new (string What, string Yaml)[]
        {
            ("no steps", "title: demo\n"),
            ("a misspelled step key", "steps:\n  - run: x\n    hightlight: []\n"),
            ("a step key under settings", "settings:\n  pty: true\nsteps:\n  - x\n"),
            ("a quoted number under settings", "settings:\n  typing-speed: \"0.05\"\nsteps:\n  - x\n"),
            ("a color YAML read as a comment", "render:\n  theme:\n    fg: #ffffff\nsteps:\n  - x\n"),
            ("a theme scenetake does not have", "render:\n  theme:\n    preset: monokai\nsteps:\n  - x\n"),
            ("a palette of 15 colors", "render:\n  theme:\n    palette: \"#000:#111:#222:#333:#444:#555:#666:#777:#888:#999:#aaa:#bbb:#ccc:#ddd:#eee\"\nsteps:\n  - x\n"),
            ("a window scenetake does not draw", "render:\n  window: mac\nsteps:\n  - x\n"),
            ("a terminal wider than a cast", "width: 1000\nsteps:\n  - x\n"),
            ("a speed above 4", "render:\n  speed: 10\nsteps:\n  - x\n"),
            ("a length below a second", "render:\n  length: 0.5\nsteps:\n  - x\n"),
            ("both speed and length", "render:\n  speed: 2\n  length: 15\nsteps:\n  - x\n"),
            ("both speed and length in an outputs entry", "outputs:\n  - format: svg\n    speed: 2\n    length: 15\nsteps:\n  - x\n"),
            ("a step without run", "steps:\n  - name: x\n"),
            ("keys without pty", "steps:\n  - run: x\n    keys:\n      - key: enter\n"),
            ("interactive without pty", "steps:\n  - run: x\n    interactive: true\n"),
            ("keys with interactive", "steps:\n  - run: x\n    pty: true\n    interactive: true\n    keys:\n      - key: q\n"),
            ("timeout with interactive", "steps:\n  - run: x\n    pty: true\n    interactive: true\n    timeout: 5\n"),
            ("normalize-typing without interactive", "steps:\n  - run: x\n    pty: true\n    normalize-typing: true\n"),
            ("type and key in one entry", "steps:\n  - run: x\n    pty: true\n    keys:\n      - type: q\n        key: enter\n"),
            ("an empty keys entry", "steps:\n  - run: x\n    pty: true\n    keys:\n      - {}\n"),
            ("a key name that is text to type", "steps:\n  - run: x\n    pty: true\n    keys:\n      - key: hello\n"),
            ("a card with run", "steps:\n  - card: hi\n    run: x\n"),
            ("a card with a comment line", "steps:\n  - card: hi\n    name: x\n"),
            ("clear that is false", "steps:\n  - clear: false\n"),
            ("clear with run", "steps:\n  - clear: true\n    run: x\n"),
            ("a background step with a comment line", "steps:\n  - run: x\n    background: true\n    name: server\n"),
            ("wait without background", "steps:\n  - run: x\n    wait: ready\n"),
            ("wait-timeout without wait", "steps:\n  - run: x\n    background: true\n    wait-timeout: 5\n"),
            ("show: none with a comment line", "steps:\n  - run: x\n    show: none\n    name: hidden\n"),
            ("show: none with post-delay", "steps:\n  - run: x\n    show: None\n    post-delay: 1\n"),
            ("show: output with run-highlight", "steps:\n  - run: x\n    show: output\n    run-highlight: red\n"),
            ("show that is no value", "steps:\n  - run: x\n    show: hidden\n"),
            ("a highlight range that is no range", "steps:\n  - run: x\n    highlight:\n      - color: red\n        at: a-b\n"),
            ("a highlight without at", "steps:\n  - run: x\n    highlight:\n      - color: red\n"),
            ("a prompt part without text", "settings:\n  prompt:\n    - color: red\nsteps:\n  - x\n"),
            ("an outputs entry without format", "outputs:\n  - file: a.svg\nsteps:\n  - x\n"),
            ("an outputs entry for the cast", "outputs:\n  - format: cast\nsteps:\n  - x\n"),
            ("crf on a GIF", "outputs:\n  - format: gif\n    crf: 30\nsteps:\n  - x\n"),
            ("at on an mp4", "outputs:\n  - format: mp4\n    at: 1\nsteps:\n  - x\n"),
            ("fps on an SVG", "outputs:\n  - format: svg\n    fps: 10\nsteps:\n  - x\n"),
            ("font-file on an SVG", "outputs:\n  - format: svg\n    font-file: a.ttf\nsteps:\n  - x\n"),
            ("max-fps on a PNG", "outputs:\n  - format: PNG\n    max-fps: 10\nsteps:\n  - x\n"),
            ("a size that is no size", "outputs:\n  - format: mp4\n    max-size: 1280\nsteps:\n  - x\n"),
            ("a shape for max-size", "outputs:\n  - format: mp4\n    max-size: 16:9\nsteps:\n  - x\n"),
            ("a shape in fractions", "outputs:\n  - format: mp4\n    size: 2.39:1\nsteps:\n  - x\n"),
            ("at that is neither seconds, end, nor an id", "outputs:\n  - format: png\n    at: 1st\nsteps:\n  - x\n"),
            ("an id that starts with a digit", "steps:\n  - run: x\n    id: 1st\n"),
            ("an id with a space", "steps:\n  - run: x\n    id: my step\n"),
            ("an id that --at reads as the final screen", "steps:\n  - run: x\n    id: End\n"),
            ("an id on a step that shows nothing", "steps:\n  - run: x\n    show: none\n    id: setup\n"),
            ("an id on a background step", "steps:\n  - run: x\n    background: true\n    id: server\n"),
            ("blur in fractions of a pixel", "outputs:\n  - format: svg\n    blur: 2.5\nsteps:\n  - x\n"),
        };

        var ok = true;
        foreach (var (what, yaml) in mistakes)
            ok &= Check(Problems(ReadYaml(yaml)).Count != 0, $"not marked: {what}");
        return ok;
    }

    internal static bool TakesWhatScenetakeReads()
    {
        // Values scenetake reads without a warning, which the schema must not mark: names in another case, numbers
        // where text goes, the forms of a highlight range, and an outputs entry's values as the command line takes them.
        var accepted = new (string What, string Yaml)[]
        {
            ("names in another case", """
                render:
                  window: MacOS
                  fit: Height
                  theme:
                    preset: Nord
                outputs:
                  - format: SVG
                    theme: GitHub-Light
                    window: Windows
                    file: light
                steps:
                  - run: x
                    show: Output
                  - run: y
                    pty: true
                    keys:
                      - key: Enter
                      - key: " ctrl+C "
                      - key: alt+X
                      - key: Page-Down
                """),
            ("numbers where text goes", """
                pre:
                  - 123
                settings:
                  prompt:
                    - text: 1
                      color: red
                    - " $ "
                  mask:
                    - 12345678
                steps:
                  - 123
                  - run: 2024
                  - card: 2024
                  - card: [1.10, ~, "x"]
                  - run: y
                    pty: true
                    keys:
                      - wait: 123
                        type: 42
                """),
            ("steps of every form", """
                steps:
                  -
                  - echo hi
                  - run: echo hi
                    name: "[bold]hi"
                  - clear: true
                    name: next
                    post-delay: 1
                  - card:
                      - "[bold fg:bright-cyan]my-app"
                      - ""
                    pre-delay: 0.5
                    post-delay: 2
                  - run: npm run dev
                    background: true
                    wait: "Local:"
                    wait-timeout: 60
                    continue-on-error: true
                  - run: cd app
                    show: none
                    pty: true
                    continue-on-error: true
                  - run: figlet hi
                    show: output
                    name: banner
                  - run: git diff
                    id: compare
                  - clear: true
                    id: _second-half
                  - card: bye
                    id: outro-1
                """),
            ("highlight ranges", """
                steps:
                  - run: x
                    highlight:
                      - color: red
                        at: 1
                      - color: green
                        at: [2, "3-", "4:2-", " 5:7-10 ", "6-9:1-3"]
                      - color: "fg:#ff8c00 bg:40,40,40"
                        at: "007"
                """),
            ("flags that are off need nothing else", """
                steps:
                  - run: x
                    interactive: false
                    normalize-typing: false
                    background: false
                    name: shown
                  - run: y
                    show: all
                    run-highlight: bold
                """),
            ("an outputs entry as the command line takes it", """
                outputs:
                  - format: svg
                    speed: 2x
                    at: 0
                    max-fps: 0
                    file: a
                  - format: png
                    at: End
                    font-file: font.ttf
                    file: b
                  - format: svg
                    at: compare
                    file: f
                  - format: gif
                    max-size: 400X300
                    length: 15s
                    fps: 50
                    loop: false
                    file: c
                  - format: mp4
                    size: 1920x1080
                    crf: 51
                    end-hold: 0
                    fade-out: 10
                    file: d
                  - format: mp4
                    size: 16:9
                    max-size: 1280x1024
                    file: e
                steps:
                  - x
                """),
            ("a palette with spaces", """
                render:
                  theme:
                    fg: " #fff "
                    palette: "#000 : #111:#222:#333:#444:#555:#666:#777:#888:#999:#aaa:#bbb:#ccc:#ddd:#eee:#fff:"
                steps:
                  - x
                """),
        };

        var ok = true;
        foreach (var (what, yaml) in accepted)
        {
            var problems = Problems(ReadYaml(yaml));
            ok &= Check(problems.Count == 0, $"{what}:{Environment.NewLine}    {string.Join(Environment.NewLine + "    ", problems)}");
        }

        return ok;
    }

    internal static bool SamplesNameTheSchema()
    {
        // The samples start with the line init writes, so a reader who copies one gets the editor's checks too.
        // scripts/set_version.cs moves the line to a new version's tag.
        var ok = true;
        foreach (var file in Directory.EnumerateFiles(TestEnv.RepoPath("samples"), "*.yaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file).Replace("\r\n", "\n");
            ok &= Check(text.StartsWith(ScenarioSchema.ModeLine, StringComparison.Ordinal),
                $"{Path.GetRelativePath(TestEnv.RepoRoot, file)} should start with {ScenarioSchema.ModeLine.TrimEnd()}, not {text.Split('\n')[0]}");
        }

        return ok;
    }

    internal static bool InitAndSavedScenariosNameTheSchema(string scenetake)
    {
        // init and capture --save-scenario name the schema of their own version on the first line, which editors that
        // use yaml-language-server read, and what they write passes it.
        var version = Regex.Match(File.ReadAllText(TestEnv.RepoPath("Directory.Build.props")), "<Version>([^<]+)</Version>").Groups[1].Value;
        var modeLine = $"# yaml-language-server: $schema=https://raw.githubusercontent.com/guitarrapc/scenetake/v{version}/schema/scenario.schema.json";
        var ok = Check(ScenarioSchema.ModeLine == modeLine + "\n", $"the schema line: {ScenarioSchema.ModeLine}");
        var folder = Directory.CreateTempSubdirectory("scenetake-schema-").FullName;
        try
        {
            var written = new List<(string What, string Path)>();
            foreach (var shell in new[] { "", "bash", "pwsh", "powershell" })
            {
                var path = Path.Combine(folder, $"init-{(shell.Length == 0 ? "default" : shell)}.yaml");
                var (exit, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, shell.Length == 0 ? ["init", path] : ["init", "--shell", shell, path]);
                ok &= Check(exit == 0, $"init {shell}: exit {exit}{Environment.NewLine}{stderr}");
                written.Add(($"init {shell}", path));
            }

            var saved = Path.Combine(folder, "saved.yaml");
            var (captureExit, captureStderr) = Mp4FfmpegCases.Exec(scenetake, folder,
                ["capture", "--pipe", "--format", "cast", "--width", "60", "--height", "10", "--timeout", "5", "--window", "macos", "--window-title", "demo",
                 "--theme", "dracula", "--background", "#112233 #445566", "--opacity", "0.9", "--blur", "2", "--margin", "20", "--padding", "4",
                 "--font-size", "14", "--fit", "none", "--crop-top", "1", "--no-loop", "--end-hold", "1", "--fade-out", "1", "--speed", "1.5",
                 "--save-scenario", saved, "--", "echo", "a: b"]);
            ok &= Check(captureExit == 0, $"capture --save-scenario: exit {captureExit}{Environment.NewLine}{captureStderr}");
            written.Add(("capture --save-scenario", saved));

            foreach (var (what, path) in written.Where(file => File.Exists(file.Path)))
            {
                var text = File.ReadAllText(path);
                ok &= Check(text.StartsWith(modeLine + "\n", StringComparison.Ordinal), $"{what}: first line {text.Split('\n')[0]}");
                var problems = Problems(ReadYaml(text));
                ok &= Check(problems.Count == 0, $"{what}:{Environment.NewLine}    {string.Join(Environment.NewLine + "    ", problems)}");
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }

        return ok;
    }

    internal static bool RefusedOptionsMatchTheCli(string scenetake)
    {
        // Each format's options that the schema refuses are the ones the CLI refuses before anything runs. A replay
        // runs no command, and an ffmpeg that does not exist stops an mp4 or a GIF entry after those checks.
        var values = new Dictionary<string, string>
        {
            ["at"] = "1", ["max-fps"] = "10", ["fps"] = "10", ["crf"] = "30", ["max-size"] = "400x300", ["size"] = "820x500", ["font-file"] = "font.ttf",
        };
        var folder = Directory.CreateTempSubdirectory("scenetake-schema-").FullName;
        try
        {
            const string steps = "settings:\n  typing-speed: 0.001\n  pre-delay: 0\n  post-delay: 0\nsteps:\n  - echo hi\n";
            File.WriteAllText(Path.Combine(folder, "base.yaml"), steps);
            var outputs = Path.Combine(folder, "base.outputs.json");
            var (recordExit, recordStderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--save-outputs", outputs, "base.yaml"]);
            var ok = Check(recordExit == 0, $"recording: exit {recordExit}{Environment.NewLine}{recordStderr}");
            var noFfmpeg = new Dictionary<string, string> { ["SCENETAKE_FFMPEG"] = Path.Combine(folder, "no-such-ffmpeg") };
            foreach (var (format, refused) in RefusedOptions)
            {
                foreach (var (key, value) in values)
                {
                    File.WriteAllText(Path.Combine(folder, "entry.yaml"), $"{steps}outputs:\n  - format: {format}\n    file: out-{format}-{key}\n    {key}: {value}\n");
                    var (_, stderr) = Mp4FfmpegCases.Exec(scenetake, folder, ["--replay", outputs, "entry.yaml"], noFfmpeg);
                    var cliRefuses = stderr.Contains($"'{key}' requires format", StringComparison.Ordinal) || stderr.Contains($"'{key}' applies to", StringComparison.Ordinal);
                    ok &= Check(cliRefuses == refused.Contains(key), $"a {format} entry with {key}: the CLI {(cliRefuses ? "refuses" : "takes")} it{Environment.NewLine}{stderr}");
                }
            }

            return ok;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>What the schema says is wrong with a scenario, one line each, or nothing when it passes.</summary>
    internal static List<string> Problems(object? scenario)
    {
        var element = JsonDocument.Parse(ToJson(scenario)?.ToJsonString() ?? "null").RootElement;
        var result = Schema.Evaluate(element, new EvaluationOptions { OutputFormat = SchemaOutput.List });
        if (result.IsValid)
            return [];

        // An "if" that does not hold is no problem: its "then" does not apply.
        var problems = (result.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 } && !detail.EvaluationPath.ToString().Contains("/if", StringComparison.Ordinal))
            .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation} {error.Key}: {error.Value}"))
            .Distinct()
            .ToList();
        return problems.Count == 0 ? ["invalid"] : problems;
    }

    /// <summary>A YAML document as scenetake reads it untyped: maps, lists, and scalars typed as YAML 1.2 types them.</summary>
    private static object? ReadYaml(string yaml) => YamlSerializer.Deserialize<object>(Encoding.UTF8.GetBytes(yaml));

    private static JsonNode? ToJson(object? value) => value switch
    {
        null => null,
        Dictionary<object, object?> map => new JsonObject(map.Select(pair => KeyValuePair.Create(pair.Key?.ToString() ?? "", ToJson(pair.Value)))),
        List<object?> list => new JsonArray([.. list.Select(ToJson)]),
        bool flag => JsonValue.Create(flag),
        string text => JsonValue.Create(text),
        int number => JsonValue.Create(number),
        long number => JsonValue.Create(number),
        double number => JsonValue.Create(number),
        _ => JsonValue.Create(value.ToString()),
    };

    private static JsonNode Definition(string name) => Document["definitions"]![name]!;

    /// <summary>Whether a key has a description of its own or through the definition it refers to.</summary>
    private static bool Describes(JsonNode? property) =>
        property?["description"] is not null
        || (property?["$ref"] is JsonNode reference && Describes(Definition(((string)reference!).Split('/')[^1])));

    /// <summary>
    /// A name's list and its pattern, which takes the name in any case, as scenetake reads it: the list gives an editor
    /// what to complete, the pattern what to accept.
    /// </summary>
    private static bool CheckNames(string definition, IEnumerable<string> names)
    {
        var anyOf = Definition(definition)["anyOf"]!.AsArray();
        var listed = anyOf[0]!["enum"]!.AsArray().Select(value => (string)value!).ToList();
        var expected = names.ToHashSet(StringComparer.Ordinal);
        var ok = Check(listed.ToHashSet().SetEquals(expected), $"{definition}: the schema lists [{string.Join(", ", listed)}], the code [{string.Join(", ", expected)}]");
        var pattern = $"^(?:{string.Join("|", listed.Select(AnyCase))})$";
        return ok & Check((string?)anyOf[1]!["pattern"] == pattern, $"{definition}: the pattern should be {pattern}");
    }

    /// <summary>
    /// A rule that lists the keys a kind takes, all but <paramref name="refused"/>, and holds only when one of those is
    /// there.
    /// </summary>
    private static bool CheckRefused(string what, JsonNode rule, IEnumerable<string> keys, IReadOnlyCollection<string> refused)
    {
        var ok = CheckList(what, rule["then"]!["propertyNames"]!["enum"], keys.Except(refused));
        var present = rule["if"]!["anyOf"]!.AsArray().SelectMany(entry => Names(entry!["required"])).ToHashSet();
        return ok & Check(present.SetEquals(refused), $"{what}: the rule should hold when one of [{string.Join(", ", refused)}] is there, not [{string.Join(", ", present)}]");
    }

    private static bool CheckList(string what, JsonNode? list, IEnumerable<string> expected)
    {
        var listed = Names(list).ToHashSet();
        var keys = expected.ToHashSet();
        return Check(listed.SetEquals(keys), $"{what}: the schema lists [{string.Join(", ", listed.Except(keys))}] that it should not, and lacks [{string.Join(", ", keys.Except(listed))}]");
    }

    private static List<string> Names(JsonNode? list) => list?.AsArray().Select(value => (string)value!).ToList() ?? [];

    /// <summary>A pattern for the word in any case: none becomes [nN][oO][nN][eE].</summary>
    private static string AnyCase(string word)
    {
        var builder = new StringBuilder();
        foreach (var c in word)
        {
            if (char.IsAsciiLetter(c))
                builder.Append('[').Append(char.ToLowerInvariant(c)).Append(char.ToUpperInvariant(c)).Append(']');
            else
                builder.Append(c is '+' or '.' ? "\\" + c : c.ToString());
        }

        return builder.ToString();
    }

    private static bool Check(bool passed, string message)
    {
        if (!passed)
            Console.Error.WriteLine($"  {message}");
        return passed;
    }
}
