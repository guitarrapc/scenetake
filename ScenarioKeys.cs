/// <summary>
/// Finds keys in a scenario that scenetake does not read, so a misspelled key (`hightlight`, `ptty`,
/// `typing_speed`, `setting`) is reported instead of silently turning its feature off. Each finding says
/// where the key is and, when one fits, which key was meant or where the key belongs.
/// </summary>
/// <remarks>
/// The checks read the YAML as plain maps and lists, after the typed parse succeeded. There is no
/// exemption such as compose's <c>x-</c> keys: VYaml's typed parse does not register an anchor under a key
/// it skips, so an alias to it fails to parse, and such a key could only hold text scenetake ignores.
/// </remarks>
internal static class ScenarioKeys
{
    /// <summary>The keys a map-form step reads. Kept in step with the step key table in spec_scenario.md.</summary>
    internal static readonly string[] Step =
    [
        "run", "name",
        "typing-speed", "typing-jitter", "pre-delay", "post-delay", "execution-duration",
        "stream-output", "max-duration", "silence-threshold", "max-idle",
        "run-highlight", "highlight", "stderr-color",
        "pty", "interactive", "normalize-typing", "allow-failure",
    ];

    /// <summary>The keys a <c>highlight</c> entry reads.</summary>
    internal static readonly string[] Highlight = ["color", "at"];

    /// <summary>The keys of the typed sections, taken from the classes that read them.</summary>
    internal sealed record Known(
        IReadOnlyList<string> TopLevel,
        IReadOnlyList<string> Settings,
        IReadOnlyList<string> Render,
        IReadOnlyList<string> Theme);

    internal static List<string> FindUnknown(Dictionary<object, object?> root, Known known)
    {
        var levels = new (string Where, IReadOnlyList<string> Keys)[]
        {
            ("at the top level", known.TopLevel),
            ("under settings", known.Settings),
            ("under render", known.Render),
            ("under render.theme", known.Theme),
            ("in a step", Step),
            ("in a highlight entry", Highlight),
        };
        var findings = new List<string>();
        Check(root, known.TopLevel, "at the top level", levels, findings);

        if (root.GetValueOrDefault("settings") is Dictionary<object, object?> settings)
            Check(settings, known.Settings, "in settings", levels, findings);

        if (root.GetValueOrDefault("render") is Dictionary<object, object?> render)
        {
            Check(render, known.Render, "in render", levels, findings);
            if (render.GetValueOrDefault("theme") is Dictionary<object, object?> theme)
                Check(theme, known.Theme, "in render.theme", levels, findings);
        }

        if (root.GetValueOrDefault("steps") is List<object?> steps)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i] is not Dictionary<object, object?> step)
                    continue;

                Check(step, Step, $"in step {i + 1}", levels, findings);
                if (step.GetValueOrDefault("highlight") is not List<object?> highlights)
                    continue;

                for (var j = 0; j < highlights.Count; j++)
                {
                    if (highlights[j] is Dictionary<object, object?> entry)
                        Check(entry, Highlight, $"in step {i + 1} highlight {j + 1}", levels, findings);
                }
            }
        }

        return findings;
    }

    private static void Check(
        Dictionary<object, object?> map,
        IReadOnlyList<string> keys,
        string where,
        (string Where, IReadOnlyList<string> Keys)[] levels,
        List<string> findings)
    {
        foreach (var rawKey in map.Keys)
        {
            var key = rawKey?.ToString() ?? "";
            if (keys.Contains(key))
                continue;

            var hint = Hint(key, keys, where, levels);
            findings.Add(hint is null ? $"unknown key '{key}' {where}" : $"unknown key '{key}' {where} ({hint})");
        }
    }

    private static string? Hint(string key, IReadOnlyList<string> keys, string where, (string Where, IReadOnlyList<string> Keys)[] levels)
    {
        if (key == "<<")
            return "YAML merge keys are not supported, so nothing is merged";

        // Another spelling of a key of this map: typing_speed, TypingSpeed, Settings.
        var normalized = Normalize(key);
        foreach (var known in keys)
        {
            if (Normalize(known) == normalized)
                return $"did you mean '{known}'?";
        }

        // A key of another map, such as pty under settings or font-size at the top level.
        var elsewhere = levels
            .Where(level => !ReferenceEquals(level.Keys, keys) && level.Keys.Any(known => Normalize(known) == normalized))
            .Select(level => level.Where)
            .Distinct()
            .ToList();
        if (elsewhere.Count > 0)
            return $"it belongs {string.Join(" or ", elsewhere)}";

        // A typo of a key of this map: hightlight, ptty, setting, preest.
        var limit = key.Length >= 5 ? 2 : 1;
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var known in keys)
        {
            var distance = Distance(normalized, Normalize(known));
            if (distance <= limit && distance < bestDistance)
            {
                best = known;
                bestDistance = distance;
            }
        }

        return best is null ? null : $"did you mean '{best}'?";
    }

    /// <summary>Lowercase kebab-case: TypingSpeed, typingSpeed, typing_speed and "typing speed" all become typing-speed.</summary>
    private static string Normalize(string key)
    {
        var builder = new System.Text.StringBuilder(key.Length + 4);
        for (var i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && key[i - 1] is not ('-' or '_' or ' ') && !char.IsUpper(key[i - 1]))
                    builder.Append('-');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c is '_' or ' ' ? '-' : c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Edit distance where swapping two neighbors counts as one edit (optimal string alignment).</summary>
    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++)
            d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++)
            d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }

        return d[a.Length, b.Length];
    }
}
