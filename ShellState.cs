using System.Text;

/// <summary>
/// What each command leaves in its shell for the next one: the folder, the variables, the functions, and the
/// aliases (spec_scenario.md → Shell state across steps). Every command still runs in a shell of its own, as
/// pre, the steps, and post always have; this carries the state from one to the next, so a scenario reads like a
/// terminal session: <c>cd app</c>, then <c>npm test</c>.
/// </summary>
/// <remarks>
/// bash reads the state through <c>BASH_ENV</c>, and zsh through startup files that scenetake puts in
/// <c>ZDOTDIR</c>. Both save it from an EXIT trap, so the command's text, and the line numbers in its error
/// messages, stay as written. PowerShell gets the state on the command's first line and saves it in a
/// <c>finally</c> on its last. The sh family gets it on the command's first line too, without functions or aliases.
/// Other shells get none: <see cref="Create"/> returns null for them. Each command has a folder of its own under a
/// private temporary folder, which holds secrets a step exported, and which <see cref="Dispose"/> removes.
/// </remarks>
internal abstract class ShellState : IDisposable
{
    private readonly string _root;
    private int _count;
    private int _detached;
    private readonly List<string> _warnings = [];
    private readonly List<KeyValuePair<string, string?>> _exports = [];

    protected ShellState()
    {
        _root = Path.Combine(Path.GetTempPath(), "scenetake-state-" + Guid.NewGuid().ToString("N"));
        // The state holds what the steps exported, tokens included, so only the user may read it.
        if (OperatingSystem.IsWindows())
            Directory.CreateDirectory(_root);
        else
            Directory.CreateDirectory(_root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>The state for <paramref name="shell"/>, or null when scenetake cannot carry state in that shell.</summary>
    internal static ShellState? Create(string fileName, bool isPowerShell)
    {
        if (isPowerShell)
            return new PowerShellState();

        var name = Path.GetFileName(fileName);
        if (OperatingSystem.IsWindows())
        {
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];
            name = name.ToLowerInvariant();
        }

        return name switch
        {
            "bash" => new BashState(),
            "zsh" => new ZshState(),
            // A shell started as sh reads no BASH_ENV even when it is bash, as /bin/sh is on macOS.
            "sh" or "dash" or "ash" or "ksh" or "ksh93" or "mksh" => new PosixShellState(),
            _ => null,
        };
    }

    /// <summary>False for a shell that carries the folder and the variables only.</summary>
    internal virtual bool CarriesFunctionsAndAliases => true;

    /// <summary>
    /// The folder the last command that carried its state ended in, as the shell wrote it (Git Bash writes
    /// <c>/c/Users/...</c>). The next command starts there. Null until a command has carried its state.
    /// </summary>
    internal string? Folder { get; private protected set; }

    /// <summary>
    /// False when the last command removed <see cref="Folder"/> while in it: the next command cannot return there and
    /// starts where the first one did. The shell tells, since a Git Bash path such as <c>/tmp/x</c> has no Windows path
    /// to look up.
    /// </summary>
    internal bool FolderExists { get; private protected set; } = true;

    /// <summary>The command to start in place of <paramref name="command"/>, and the variables to add to its environment.</summary>
    internal PreparedCommand Prepare(string command)
    {
        var folder = Path.Combine(_root, (++_count).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return Prepare(command, folder);
    }

    /// <summary>
    /// Like <see cref="Prepare"/>, for a command that keeps running while the next ones start (<c>background: true</c>):
    /// it gets the state so far, and what it changes is never taken. Its folder stays until <see cref="Dispose"/>,
    /// because the shell reads its scripts when it starts and writes its state when it ends.
    /// </summary>
    internal PreparedCommand PrepareDetached(string command)
    {
        var folder = Path.Combine(_root, "background-" + (++_detached).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        return Prepare(command, folder);
    }

    /// <summary>
    /// Takes the state the command left. <paramref name="keep"/> is false for a command that scenetake stopped
    /// (<c>timeout</c>): its state is dropped, so a stopped step never changes what the next one sees.
    /// </summary>
    internal void Complete(bool keep)
    {
        var folder = Path.Combine(_root, _count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!Directory.Exists(folder))
            return;

        try
        {
            ReadWarnings(folder, _warnings);
            if (keep)
                Take(folder, _exports);
        }
        catch (IOException ex)
        {
            _warnings.Add($"could not read the shell state: {ex.Message}");
        }
        finally
        {
            TryDelete(folder);
        }
    }

    /// <summary>Environment variables that the last commands set or changed, for <see cref="SecretMasker.Learn"/>. Cleared by the call.</summary>
    internal List<KeyValuePair<string, string?>> TakeExports()
    {
        var exports = new List<KeyValuePair<string, string?>>(_exports);
        _exports.Clear();
        return exports;
    }

    /// <summary>Problems restoring the state, such as a folder that a step removed. Cleared by the call.</summary>
    internal List<string> TakeWarnings()
    {
        var warnings = new List<string>(_warnings);
        _warnings.Clear();
        return warnings;
    }

    public void Dispose() => TryDelete(_root);

    protected abstract PreparedCommand Prepare(string command, string folder);

    /// <summary>Reads what the command saved in <paramref name="folder"/> and makes it the state for the next command.</summary>
    protected abstract void Take(string folder, List<KeyValuePair<string, string?>> exports);

    /// <summary>Lines the restore wrote to <c>restore.err</c>: what it could not restore.</summary>
    private static void ReadWarnings(string folder, List<string> warnings)
    {
        var path = Path.Combine(folder, "restore.err");
        if (!File.Exists(path))
            return;

        foreach (var line in File.ReadAllLines(path))
        {
            if (!string.IsNullOrWhiteSpace(line))
                warnings.Add(line.Trim());
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A command that still runs may hold a file; the folder is in the temporary folder.
        }
    }

    /// <summary>A path for a shell script: Git Bash reads <c>C:/x</c>, and every shell reads forward slashes.</summary>
    protected static string ScriptPath(string path) => OperatingSystem.IsWindows() ? path.Replace('\\', '/') : path;
}

/// <summary>The command to start, with its text changed or not, and the variables to add to its environment.</summary>
internal readonly record struct PreparedCommand(string Command, Dictionary<string, string?>? Environment);

/// <summary>
/// bash: a script that bash reads through <c>BASH_ENV</c> after its login files, before the command. It restores
/// the state, notes the shell as the login files left it, and sets an EXIT trap that writes the shell's variables,
/// functions, aliases, and folder. The difference from the noted shell is the state for the next command, so the
/// state is what the steps changed, never what the login files set.
/// </summary>
internal sealed class BashState : ShellState
{
    // The restore statements for the next command, cumulative since pre's first command.
    private string _restore = "";
    private readonly string? _userBashEnv = Environment.GetEnvironmentVariable("BASH_ENV");

    protected override PreparedCommand Prepare(string command, string folder)
    {
        var script = Path.Combine(folder, "restore.sh");
        File.WriteAllText(script, BuildScript(_restore), new UTF8Encoding(false));
        return new PreparedCommand(command, new(StringComparer.Ordinal)
        {
            ["BASH_ENV"] = ScriptPath(script),
            ["__SCENETAKE_STATE_DIR"] = ScriptPath(folder),
            ["__SCENETAKE_USER_BASH_ENV"] = _userBashEnv ?? "",
        });
    }

    protected override void Take(string folder, List<KeyValuePair<string, string?>> exports)
    {
        string Read(string name) => File.ReadAllText(Path.Combine(folder, name));
        // No final.pwd: the EXIT trap did not run, because the command replaced the shell (exec) or set its own
        // EXIT trap. The state stays as it was.
        if (!File.Exists(Path.Combine(folder, "final.pwd")) || !File.Exists(Path.Combine(folder, "base.vars")))
            return;

        Folder = Read("final.pwd");
        FolderExists = !File.Exists(Path.Combine(folder, "final.gone"));
        _restore = BashDump.BuildRestore(
            Read("base.vars"), Read("final.vars"),
            Read("base.funcs"), Read("final.funcs"),
            Read("base.alias"), Read("final.alias"),
            Folder,
            exports);
    }

    /// <summary>The script bash reads through BASH_ENV. Every line keeps quiet: what bash prints here would be recorded.</summary>
    internal static string BuildScript(string restore)
    {
        var script = new StringBuilder();
        script.Append("# Written by scenetake: the state of the commands before this one (ShellState.cs).\n");
        script.Append("__scenetake_dir=$__SCENETAKE_STATE_DIR\n");
        // Children do not read this file: a bash script that the command runs gets the BASH_ENV of the caller, if any.
        script.Append("if [ -n \"$__SCENETAKE_USER_BASH_ENV\" ]; then BASH_ENV=$__SCENETAKE_USER_BASH_ENV; else unset BASH_ENV; fi\n");
        script.Append("unset __SCENETAKE_STATE_DIR __SCENETAKE_USER_BASH_ENV\n");
        script.Append("if [ -n \"${BASH_ENV-}\" ]; then . \"$BASH_ENV\"; fi\n");
        // bash 3, macOS's /bin/bash, writes `declare -p` without names as NAME=value, without the attributes, so it
        // gets each name. The loop runs in a subshell, which has every variable of the shell.
        script.Append("__scenetake_vars() { if (( BASH_VERSINFO[0] > 3 )); then builtin declare -p; else builtin compgen -v | while IFS= builtin read -r __scenetake_name; do builtin declare -p \"$__scenetake_name\"; done; fi; }\n");
        // The shell as the login files left it. The state is the difference from it.
        script.Append("{ __scenetake_vars >|\"$__scenetake_dir/base.vars\"; builtin declare -f >|\"$__scenetake_dir/base.funcs\"; builtin alias -p >|\"$__scenetake_dir/base.alias\"; } 2>/dev/null\n");
        // In a || list, set -e from the caller's BASH_ENV does not stop the shell at a statement that fails.
        script.Append("{\n").Append(restore).Append(":\n} 2>>\"$__scenetake_dir/restore.err\" || :\n");
        script.Append("__scenetake_save() { { __scenetake_vars >|\"$__scenetake_dir/final.vars\"; builtin declare -f >|\"$__scenetake_dir/final.funcs\"; builtin alias -p >|\"$__scenetake_dir/final.alias\"; builtin printf '%s' \"$PWD\" >|\"$__scenetake_dir/final.pwd\"; builtin test -d \"$PWD\" || : >|\"$__scenetake_dir/final.gone\"; } 2>/dev/null; }\n");
        script.Append("builtin trap __scenetake_save EXIT\n");
        return script.ToString();
    }
}

/// <summary>
/// Reads the dumps of <c>declare -p</c>, <c>declare -f</c>, and <c>alias -p</c>, and writes the statements that make
/// a fresh shell look like the saved one.
/// </summary>
internal static class BashDump
{
    // Variables that bash changes by itself, or that belong to the shell rather than to the steps. A step that sets
    // one of them does not pass it on.
    private static readonly HashSet<string> SkippedVariables = new(StringComparer.Ordinal)
    {
        "BASH", "BASHOPTS", "BASHPID", "BASH_ENV", "COLUMNS", "DIRSTACK", "EPOCHREALTIME", "EPOCHSECONDS", "EUID",
        "FUNCNAME", "GROUPS", "HISTCMD", "LINENO", "LINES", "OPTARG", "OPTIND", "PIPESTATUS", "PPID", "PWD", "RANDOM",
        "SECONDS", "SHELLOPTS", "SHLVL", "SRANDOM", "UID", "_",
    };

    internal static bool IsSkippedVariable(string name) =>
        SkippedVariables.Contains(name)
        || name.StartsWith("BASH_", StringComparison.Ordinal)
        || name.StartsWith("COMP_", StringComparison.Ordinal)
        || name.StartsWith("__scenetake", StringComparison.Ordinal);

    /// <summary>
    /// The statements that restore what changed between the base dumps (the shell after its login files) and the
    /// final ones (the shell at exit), in the order they must run: the folder, the variables, the functions, then
    /// the aliases. Exported variables that changed are added to <paramref name="exports"/>.
    /// </summary>
    internal static string BuildRestore(
        string baseVars, string finalVars,
        string baseFunctions, string finalFunctions,
        string baseAliases, string finalAliases,
        string folder,
        List<KeyValuePair<string, string?>> exports)
    {
        var restore = new StringBuilder();
        // An absolute folder, so CDPATH is not searched and cd prints nothing. A folder a later step removed is
        // reported instead of failing in silence.
        restore.Append("builtin cd -- ").Append(Quote(folder)).Append(" || builtin printf '%s\\n' ")
            .Append(Quote($"could not return to {folder}: it no longer exists or cannot be entered")).Append(" >&2\n");

        var before = SplitDeclarations(baseVars);
        var after = SplitDeclarations(finalVars);
        foreach (var (name, statement) in after)
        {
            if (IsSkippedVariable(name) || (before.TryGetValue(name, out var old) && old == statement))
                continue;
            restore.Append(statement).Append('\n');
            if (IsExported(statement))
                exports.Add(new(name, ScalarValue(statement)));
        }

        foreach (var name in before.Keys)
        {
            if (!after.ContainsKey(name) && !IsSkippedVariable(name))
                restore.Append("builtin unset -v ").Append(name).Append('\n');
        }

        var functionsBefore = SplitFunctions(baseFunctions);
        var functionsAfter = SplitFunctions(finalFunctions);
        foreach (var (name, body) in functionsAfter)
        {
            if (!name.StartsWith("__scenetake", StringComparison.Ordinal) && (!functionsBefore.TryGetValue(name, out var old) || old != body))
                restore.Append(body).Append('\n');
        }

        foreach (var name in functionsBefore.Keys)
        {
            if (!functionsAfter.ContainsKey(name))
                restore.Append("builtin unset -f ").Append(name).Append('\n');
        }

        var aliasesBefore = SplitAliases(baseAliases);
        var aliasesAfter = SplitAliases(finalAliases);
        var stepAlias = false;
        foreach (var (name, line) in aliasesAfter)
        {
            if (!aliasesBefore.TryGetValue(name, out var old) || old != line)
            {
                restore.Append(line).Append('\n');
                stepAlias = true;
            }
        }

        foreach (var name in aliasesBefore.Keys)
        {
            if (!aliasesAfter.ContainsKey(name))
                restore.Append("builtin unalias ").Append(Quote(name)).Append('\n');
        }

        // A shell that runs a command expands aliases only when told to. Only an alias a step defined turns that on,
        // and then only the steps' aliases expand: the login files' own are removed first. Git Bash's login files
        // define ls (ls -F --color=auto) and, in a terminal, node as winpty node.exe, which fails without a console
        // ("stdin is not a tty"). A command keeps running as it did before a step defined an alias. A login alias
        // that a step redefined is the step's, and stays.
        if (stepAlias)
        {
            foreach (var (name, line) in aliasesAfter)
            {
                if (aliasesBefore.TryGetValue(name, out var old) && old == line)
                    restore.Append("builtin unalias ").Append(Quote(name)).Append('\n');
            }

            restore.Append("builtin shopt -s expand_aliases\n");
        }

        return restore.ToString();
    }

    /// <summary>
    /// The statements of a <c>declare -p</c> dump by variable name. A statement ends at a line break outside quotes:
    /// bash 5.2 writes a value with a line break as <c>$'a\nb'</c>, but bash 3.2 writes the break inside double quotes.
    /// </summary>
    internal static Dictionary<string, string> SplitDeclarations(string dump)
    {
        var statements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var statement in ShellText.SplitStatements(dump))
        {
            if (DeclarationName(statement) is { } name)
                statements[name] = statement;
        }

        return statements;
    }

    /// <summary>The variable a <c>declare</c> statement sets: <c>declare -x PATH="..."</c> gives PATH. Null for another line.</summary>
    internal static string? DeclarationName(string statement)
    {
        if (!statement.StartsWith("declare ", StringComparison.Ordinal))
            return null;

        var rest = statement.AsSpan(8);
        // The options, such as -x, -ar, or --.
        while (rest.Length > 0 && rest[0] == '-')
        {
            var space = rest.IndexOf(' ');
            if (space < 0)
                return null;
            rest = rest[(space + 1)..];
        }

        var end = rest.IndexOfAny('=', ' ');
        var name = (end < 0 ? rest : rest[..end]).ToString();
        return name.Length > 0 ? name : null;
    }

    private static bool IsExported(string statement)
    {
        var options = statement.AsSpan(8);
        return options.Length > 1 && options[0] == '-' && options[..Math.Max(options.IndexOf(' '), 0)].Contains('x');
    }

    /// <summary>The value of a scalar <c>declare</c> statement, unquoted, or null for an array or no value.</summary>
    internal static string? ScalarValue(string statement)
    {
        var equals = statement.IndexOf('=');
        if (equals < 0 || equals + 1 >= statement.Length)
            return null;

        var value = statement.AsSpan(equals + 1);
        if (value[0] == '(')
            return null;

        var text = new StringBuilder(value.Length);
        if (value[0] == '"')
        {
            for (var i = 1; i < value.Length && value[i] != '"'; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length && value[i + 1] is '"' or '\\' or '$' or '`' or '\n')
                    i++;
                text.Append(value[i]);
            }

            return text.ToString();
        }

        if (value.StartsWith("$'"))
        {
            for (var i = 2; i < value.Length && value[i] != '\''; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    text.Append(value[i]);
                    continue;
                }

                i++;
                text.Append(value[i] switch
                {
                    'n' => '\n', 't' => '\t', 'r' => '\r', 'e' or 'E' => '\u001b', 'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v',
                    _ => value[i],
                });
            }

            return text.ToString();
        }

        if (value[0] == '\'')
        {
            var close = value[1..].IndexOf('\'');
            return close < 0 ? value[1..].ToString() : value[1..(close + 1)].ToString();
        }

        return value.ToString();
    }

    /// <summary>
    /// The functions of a <c>declare -f</c> dump by name. bash writes each as <c>name () </c>, then its body, which ends
    /// with <c>}</c> alone on a line, then <c>declare -fx name</c> when it is exported.
    /// </summary>
    internal static Dictionary<string, string> SplitFunctions(string dump)
    {
        var functions = new Dictionary<string, string>(StringComparer.Ordinal);
        string? name = null;
        var body = new StringBuilder();
        foreach (var raw in dump.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw;
            if (name is null)
            {
                if (line.StartsWith("declare -f", StringComparison.Ordinal))
                {
                    // `declare -fx name` after a body: the function is exported.
                    var exported = DeclarationName(line);
                    if (exported is not null && functions.TryGetValue(exported, out var text))
                        functions[exported] = text + "\n" + line;
                    continue;
                }

                // A name holds no space, so the header's only space is the one before "()".
                var header = line.TrimEnd();
                if (header.Length > 3 && header.EndsWith(" ()", StringComparison.Ordinal) && header.IndexOf(' ') == header.Length - 3)
                {
                    name = header[..^3];
                    body.Clear().Append(line);
                }

                continue;
            }

            body.Append('\n').Append(line);
            if (line == "}")
            {
                functions[name] = body.ToString();
                name = null;
            }
        }

        return functions;
    }

    /// <summary>The aliases of an <c>alias -p</c> dump by name. A value with a line break continues on the next lines.</summary>
    internal static Dictionary<string, string> SplitAliases(string dump)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        string? name = null;
        foreach (var raw in dump.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.StartsWith("alias ", StringComparison.Ordinal))
            {
                var equals = raw.IndexOf('=');
                name = equals > 6 ? raw[6..equals] : null;
                if (name is not null)
                    aliases[name] = raw;
                continue;
            }

            if (name is not null && raw.Length > 0)
                aliases[name] += "\n" + raw;
        }

        return aliases;
    }

    /// <summary>A bash word for <paramref name="text"/>: single quotes, with each quote in it written as <c>'\''</c>.</summary>
    internal static string Quote(string text) => "'" + text.Replace("'", "'\\''") + "'";
}

/// <summary>Reads what shells print for reinput: <c>declare -p</c>, <c>typeset -p</c>, <c>set</c>, <c>export -p</c>.</summary>
internal static class ShellText
{
    /// <summary>
    /// The statements of a dump. A statement ends at a line break outside quotes: dash writes a value with a line
    /// break inside single quotes, and bash 3.2 inside double quotes.
    /// </summary>
    internal static List<string> SplitStatements(string dump)
    {
        var statements = new List<string>();
        var start = 0;
        var quote = '\0';
        for (var i = 0; i <= dump.Length; i++)
        {
            if (i < dump.Length)
            {
                var c = dump[i];
                if (quote == '\0')
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }

                    if (c is '"' or '\'')
                    {
                        // $'...' takes backslash escapes, as "..." does; a plain '...' takes none.
                        quote = c == '\'' && i > 0 && dump[i - 1] == '$' ? '$' : c;
                        continue;
                    }

                    if (c != '\n')
                        continue;
                }
                else
                {
                    if (c == '\\' && quote is '"' or '$')
                    {
                        i++;
                        continue;
                    }

                    if ((quote == '"' && c == '"') || (quote is '\'' or '$' && c == '\''))
                        quote = '\0';
                    continue;
                }
            }

            var statement = dump[start..Math.Min(i, dump.Length)].TrimEnd('\r');
            start = i + 1;
            if (statement.Length > 0)
                statements.Add(statement);
        }

        return statements;
    }

    /// <summary>
    /// The text a shell word stands for. The word may join quoted parts, as the shells write a quote in a value:
    /// <c>'it'\''s'</c> (bash, mksh, zsh), <c>'it'"'"'s'</c> (dash), <c>$'it\'s'</c> (ksh93).
    /// </summary>
    internal static string Unquote(ReadOnlySpan<char> word)
    {
        var text = new StringBuilder(word.Length);
        for (var i = 0; i < word.Length; i++)
        {
            var c = word[i];
            if (c == '\'')
            {
                var close = word[(i + 1)..].IndexOf('\'');
                var end = close < 0 ? word.Length : i + 1 + close;
                text.Append(word[(i + 1)..end]);
                i = end;
            }
            else if (c == '$' && i + 1 < word.Length && word[i + 1] == '\'')
            {
                for (i += 2; i < word.Length && word[i] != '\''; i++)
                {
                    if (word[i] != '\\' || i + 1 >= word.Length)
                    {
                        text.Append(word[i]);
                        continue;
                    }

                    i++;
                    text.Append(word[i] switch
                    {
                        'n' => '\n', 't' => '\t', 'r' => '\r', 'e' or 'E' => '\u001b', 'a' => '\a', 'b' => '\b', 'f' => '\f', 'v' => '\v',
                        _ => word[i],
                    });
                }
            }
            else if (c == '"')
            {
                for (i++; i < word.Length && word[i] != '"'; i++)
                {
                    if (word[i] == '\\' && i + 1 < word.Length && word[i + 1] is '"' or '\\' or '$' or '`' or '\n')
                        i++;
                    text.Append(word[i]);
                }
            }
            else if (c == '\\' && i + 1 < word.Length)
            {
                i++;
                text.Append(word[i]);
            }
            else
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }

    /// <summary>The value of a <c>name=value</c> word or statement, unquoted, or null for none or an array.</summary>
    internal static string? AssignedValue(string statement)
    {
        var equals = statement.IndexOf('=');
        if (equals < 0)
            return null;

        var value = statement.AsSpan(equals + 1);
        return value.Length > 0 && value[0] == '(' ? null : Unquote(value);
    }

    /// <summary>True for a shell variable name: a letter or <c>_</c>, then letters, digits, and <c>_</c>.</summary>
    internal static bool IsName(ReadOnlySpan<char> name)
    {
        if (name.Length == 0 || !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
            return false;

        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
                return false;
        }

        return true;
    }
}

/// <summary>
/// zsh: scenetake points <c>ZDOTDIR</c> at the command's folder, which holds startup files of its own. Each one reads
/// the user's file of the same name, with the user's ZDOTDIR, then points ZDOTDIR back, so zsh comes back for the next
/// one. After the last, <c>finish.zsh</c> notes the shell, restores the state, and sets an EXIT trap that saves it, as
/// bash's script does. The restore comes after every login file because macOS's /etc/zprofile runs path_helper, which
/// puts the system folders first in PATH again.
/// </summary>
internal sealed class ZshState : ShellState
{
    // The restore statements for the next command, cumulative since pre's first command.
    private string _restore = "";
    private readonly string? _userZdotdir = Environment.GetEnvironmentVariable("ZDOTDIR");

    protected override PreparedCommand Prepare(string command, string folder)
    {
        foreach (var (name, text) in BuildFiles(ScriptPath(folder), _userZdotdir, _restore))
            File.WriteAllText(Path.Combine(folder, name), text, new UTF8Encoding(false));
        return new PreparedCommand(command, new(StringComparer.Ordinal) { ["ZDOTDIR"] = ScriptPath(folder) });
    }

    protected override void Take(string folder, List<KeyValuePair<string, string?>> exports)
    {
        string Read(string name) => File.ReadAllText(Path.Combine(folder, name));
        // No final.pwd: the EXIT trap did not run (exec, or the command's own EXIT trap). The state stays as it was.
        if (!File.Exists(Path.Combine(folder, "final.pwd")) || !File.Exists(Path.Combine(folder, "base.vars")))
            return;

        Folder = Read("final.pwd");
        FolderExists = !File.Exists(Path.Combine(folder, "final.gone"));
        _restore = ZshDump.BuildRestore(
            Read("base.vars"), Read("final.vars"),
            Read("base.funcs"), Read("final.funcs"),
            Read("base.alias"), Read("final.alias"),
            Folder,
            exports);
    }

    /// <summary>
    /// The startup files in the command's folder. zsh reads .zshenv, then for a login shell .zprofile and .zlogin,
    /// each from ZDOTDIR. A file that turns off the rcs option, or a shell that is not a login shell, as on Windows,
    /// ends the chain early: the state is restored there instead. Every line keeps quiet: what zsh prints here would
    /// be recorded.
    /// </summary>
    internal static List<(string Name, string Text)> BuildFiles(string folder, string? userZdotdir, string restore)
    {
        // A user file reads ZDOTDIR as zsh gives it: unset, or the value scenetake started with.
        var user = string.IsNullOrEmpty(userZdotdir) ? "builtin unset ZDOTDIR\n" : "ZDOTDIR=" + BashDump.Quote(userZdotdir) + "\n";
        const string Back = "if [[ -n $__scenetake_zdotdir ]]; then ZDOTDIR=$__scenetake_zdotdir; else builtin unset ZDOTDIR; fi\n";
        const string Away = "__scenetake_zdotdir=${ZDOTDIR-}; ZDOTDIR=$__scenetake_dir";
        const string Finish = "builtin source -- \"$__scenetake_dir/finish.zsh\"";
        static string ReadUser(string file) => $"if [[ -r ${{ZDOTDIR:-$HOME}}/{file} ]]; then builtin source -- \"${{ZDOTDIR:-$HOME}}/{file}\"; fi\n";

        var zshenv = "# Written by scenetake: zsh reads this in place of your .zshenv, and the next startup files from here too (ShellState.cs).\n" +
                     "__scenetake_dir=" + BashDump.Quote(folder) + "\n" +
                     user +
                     ReadUser(".zshenv") +
                     $"if [[ -o login && -o rcs ]]; then {Away}; else {Finish}; fi\n";
        var zprofile = "# Written by scenetake: reads your .zprofile (ShellState.cs).\n" +
                       Back +
                       ReadUser(".zprofile") +
                       $"if [[ -o rcs ]]; then {Away}; else {Finish}; fi\n";
        var zlogin = "# Written by scenetake: reads your .zlogin, then restores the state (ShellState.cs).\n" +
                     Back +
                     ReadUser(".zlogin") +
                     Finish + "\n";

        var finish = new StringBuilder();
        finish.Append("# Written by scenetake: the state of the commands before this one (ShellState.cs).\n");
        finish.Append("builtin unset __scenetake_zdotdir\n");
        // The dumps run with zsh's own options, whatever the steps set. Both run in this function, so both write
        // the global variables as typeset -g, and argv is the function's.
        finish.Append("__scenetake_dump() {\n");
        finish.Append("  builtin emulate -L zsh\n");
        finish.Append("  builtin typeset -p >|\"$__scenetake_dir/$1.vars\" 2>/dev/null\n");
        finish.Append("  builtin local __scenetake_k\n");
        finish.Append("  { for __scenetake_k in ${(ok)functions}; do builtin print -r -- \"functions[${(q)__scenetake_k}]=${(q)functions[$__scenetake_k]}\"; done } >|\"$__scenetake_dir/$1.funcs\" 2>/dev/null\n");
        finish.Append("  { for __scenetake_k in ${(ok)aliases}; do builtin print -r -- \"aliases[${(q)__scenetake_k}]=${(q)aliases[$__scenetake_k]}\"; done\n");
        finish.Append("    for __scenetake_k in ${(ok)galiases}; do builtin print -r -- \"galiases[${(q)__scenetake_k}]=${(q)galiases[$__scenetake_k]}\"; done\n");
        finish.Append("    for __scenetake_k in ${(ok)saliases}; do builtin print -r -- \"saliases[${(q)__scenetake_k}]=${(q)saliases[$__scenetake_k]}\"; done } >|\"$__scenetake_dir/$1.alias\" 2>/dev/null\n");
        finish.Append("}\n");
        finish.Append("__scenetake_dump base\n");
        finish.Append("{\n").Append(restore).Append(":\n} 2>>\"$__scenetake_dir/restore.err\" || :\n");
        finish.Append("__scenetake_save() { __scenetake_dump final; builtin print -rn -- \"$PWD\" >|\"$__scenetake_dir/final.pwd\" 2>/dev/null; [[ -d $PWD ]] || : >|\"$__scenetake_dir/final.gone\" 2>/dev/null; }\n");
        finish.Append("builtin trap __scenetake_save EXIT\n");

        return [(".zshenv", zshenv), (".zprofile", zprofile), (".zlogin", zlogin), ("finish.zsh", finish.ToString())];
    }
}

/// <summary>
/// Reads the dumps of zsh's <c>typeset -p</c> and of its <c>functions</c>, <c>aliases</c>, <c>galiases</c>, and
/// <c>saliases</c> tables, and writes the statements that make a fresh shell look like the saved one.
/// </summary>
internal static class ZshDump
{
    // Variables that zsh changes by itself, or that belong to the shell rather than to the steps. ZDOTDIR is
    // scenetake's own while zsh starts.
    private static readonly HashSet<string> SkippedVariables = new(StringComparer.Ordinal)
    {
        "0", "_", "ARGC", "COLUMNS", "EPOCHREALTIME", "EPOCHSECONDS", "ERRNO", "HISTCMD", "LINENO", "LINES", "OPTARG",
        "OPTIND", "PPID", "PWD", "RANDOM", "SECONDS", "SHLVL", "TRY_BLOCK_ERROR", "TRY_BLOCK_INTERRUPT", "TTY", "TTYIDLE",
        "ZDOTDIR", "ZSH_ARGZERO", "ZSH_EVAL_CONTEXT", "ZSH_EXECUTION_STRING", "ZSH_SUBSHELL", "argv", "epochtime",
        "funcfiletrace", "funcsourcetrace", "funcstack", "functrace", "pipestatus", "status", "zsh_eval_context",
    };

    internal static bool IsSkippedVariable(string name) =>
        SkippedVariables.Contains(name) || name.StartsWith("__scenetake", StringComparison.Ordinal);

    /// <summary>
    /// The statements that restore what changed between the base dumps (the shell after its login files) and the
    /// final ones (the shell at exit): the folder, the variables, the functions, then the aliases. Exported
    /// variables that changed are added to <paramref name="exports"/>.
    /// </summary>
    internal static string BuildRestore(
        string baseVars, string finalVars,
        string baseFunctions, string finalFunctions,
        string baseAliases, string finalAliases,
        string folder,
        List<KeyValuePair<string, string?>> exports)
    {
        var restore = new StringBuilder();
        // -q runs no chpwd hook, which could print.
        restore.Append("builtin cd -q -- ").Append(BashDump.Quote(folder)).Append(" || builtin print -r -- ")
            .Append(BashDump.Quote($"could not return to {folder}: it no longer exists or cannot be entered")).Append(" >&2\n");

        var before = SplitDeclarations(baseVars);
        var after = SplitDeclarations(finalVars);
        foreach (var (name, statement) in after)
        {
            if (IsSkippedVariable(name) || (before.TryGetValue(name, out var old) && old == statement))
                continue;
            restore.Append(statement).Append('\n');
            if (statement.StartsWith("export ", StringComparison.Ordinal) && !statement.Contains('\n'))
                exports.Add(new(name, ShellText.AssignedValue(statement)));
        }

        foreach (var name in before.Keys)
        {
            if (!after.ContainsKey(name) && !IsSkippedVariable(name))
                restore.Append("builtin unset -v -- ").Append(BashDump.Quote(name)).Append('\n');
        }

        AppendEntries(restore, SplitEntries(baseFunctions), SplitEntries(finalFunctions));
        AppendEntries(restore, SplitEntries(baseAliases), SplitEntries(finalAliases));
        return restore.ToString();
    }

    /// <summary>
    /// The statements of a <c>typeset -p</c> dump by variable name. A tied pair, such as PATH and path, is two
    /// statements under the array's name, kept together.
    /// </summary>
    internal static Dictionary<string, string> SplitDeclarations(string dump)
    {
        var statements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var statement in ShellText.SplitStatements(dump))
        {
            if (DeclarationName(statement) is not { } name)
                continue;
            statements[name] = statements.TryGetValue(name, out var first) ? first + "\n" + statement : statement;
        }

        return statements;
    }

    /// <summary>
    /// The variable a <c>typeset</c> or <c>export</c> statement sets: <c>typeset -g foo=bar</c> gives foo, and
    /// <c>export -T PATH path=( ... )</c> gives path. Null for another line.
    /// </summary>
    internal static string? DeclarationName(string statement)
    {
        ReadOnlySpan<char> rest;
        if (statement.StartsWith("typeset ", StringComparison.Ordinal))
            rest = statement.AsSpan(8);
        else if (statement.StartsWith("export ", StringComparison.Ordinal))
            rest = statement.AsSpan(7);
        else
            return null;

        // The options, such as -g, -a, -i10, or -aT.
        while (rest.Length > 0 && rest[0] == '-')
        {
            var space = rest.IndexOf(' ');
            if (space < 0)
                return null;
            rest = rest[(space + 1)..];
        }

        var end = rest.IndexOfAny('=', ' ');
        if (end < 0)
            return rest.Length > 0 ? rest.ToString() : null;
        if (rest[end] == '=')
            return end > 0 ? rest[..end].ToString() : null;

        // A tied pair: the scalar's name, then the array's, which takes the value.
        var next = rest[(end + 1)..];
        var equals = next.IndexOfAny('=', ' ');
        return equals > 0 && next[equals] == '=' ? next[..equals].ToString() : rest[..end].ToString();
    }

    /// <summary>The lines of a functions or aliases dump, <c>functions[name]=body</c>, by the part before <c>=</c>.</summary>
    internal static Dictionary<string, string> SplitEntries(string dump)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in dump.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var open = line.IndexOf('[');
            var close = line.IndexOf("]=", StringComparison.Ordinal);
            if (open > 0 && close > open)
                entries[line[..(close + 1)]] = line;
        }

        return entries;
    }

    private static void AppendEntries(StringBuilder restore, Dictionary<string, string> before, Dictionary<string, string> after)
    {
        foreach (var (key, line) in after)
        {
            if (!IsOwnEntry(key) && (!before.TryGetValue(key, out var old) || old != line))
                restore.Append(line).Append('\n');
        }

        foreach (var key in before.Keys)
        {
            if (!after.ContainsKey(key) && !IsOwnEntry(key) && RemoveEntry(key) is { } remove)
                restore.Append(remove).Append('\n');
        }
    }

    private static bool IsOwnEntry(string key) => key.StartsWith("functions[__scenetake", StringComparison.Ordinal);

    // The name is quoted as the dump quoted it, which unfunction and unalias read as zsh does.
    private static string? RemoveEntry(string key)
    {
        var open = key.IndexOf('[');
        var name = key[(open + 1)..^1];
        return key[..open] switch
        {
            "functions" => "builtin unfunction -- " + name,
            "aliases" or "galiases" => "builtin unalias -- " + name,
            "saliases" => "builtin unalias -s -- " + name,
            _ => null,
        };
    }
}

/// <summary>
/// The sh family (dash, ash, ksh, mksh, and bash started as sh): a shell that runs a command reads no startup file
/// after its login files, so the restore goes on the command's first line, as for PowerShell, and the line numbers
/// stay. It carries the folder and the variables only: dash and ash cannot list their functions, and an alias that
/// the first line defines is not used on that line.
/// </summary>
internal sealed class PosixShellState : ShellState
{
    // The restore statements for the next command, cumulative since pre's first command.
    private string _restore = "";

    internal override bool CarriesFunctionsAndAliases => false;

    protected override PreparedCommand Prepare(string command, string folder)
    {
        var script = Path.Combine(folder, "restore.sh");
        File.WriteAllText(script, BuildScript(ScriptPath(folder), _restore), new UTF8Encoding(false));
        return new PreparedCommand(". " + BashDump.Quote(ScriptPath(script)) + "; " + command, null);
    }

    protected override void Take(string folder, List<KeyValuePair<string, string?>> exports)
    {
        string Read(string name) => File.ReadAllText(Path.Combine(folder, name));
        // No final.pwd: the EXIT trap did not run (exec, or the command's own EXIT trap). The state stays as it was.
        if (!File.Exists(Path.Combine(folder, "final.pwd")) || !File.Exists(Path.Combine(folder, "base.vars")))
            return;

        Folder = Read("final.pwd");
        FolderExists = !File.Exists(Path.Combine(folder, "final.gone"));
        _restore = PosixShellDump.BuildRestore(
            Read("base.vars"), Read("final.vars"),
            Read("base.exports"), Read("final.exports"),
            Folder,
            exports);
    }

    /// <summary>The script the command's first line reads. Every line keeps quiet: what the shell prints here would be recorded.</summary>
    internal static string BuildScript(string folder, string restore)
    {
        // dash has no `builtin`. `command` passes over a function named cd or printf, and special built-ins, such as
        // set, export, unset, and trap, come before functions anyway.
        var script = new StringBuilder();
        script.Append("# Written by scenetake: the state of the commands before this one (ShellState.cs).\n");
        script.Append("__scenetake_dir=").Append(BashDump.Quote(folder)).Append('\n');
        script.Append("__scenetake_dump() { { set >|\"$__scenetake_dir/$1.vars\"; export -p >|\"$__scenetake_dir/$1.exports\"; } 2>/dev/null; }\n");
        script.Append("__scenetake_dump base\n");
        script.Append("{\n").Append(restore).Append(":\n} 2>>\"$__scenetake_dir/restore.err\" || :\n");
        script.Append("__scenetake_save() { __scenetake_dump final; command printf '%s' \"$PWD\" >|\"$__scenetake_dir/final.pwd\" 2>/dev/null; [ -d \"$PWD\" ] || : >|\"$__scenetake_dir/final.gone\" 2>/dev/null; }\n");
        script.Append("trap __scenetake_save EXIT\n");
        return script.ToString();
    }
}

/// <summary>Reads the dumps of <c>set</c> and <c>export -p</c>, and writes the statements that make a fresh shell look like the saved one.</summary>
internal static class PosixShellDump
{
    /// <summary>
    /// The statements that restore what changed between the base dumps (the shell after its login files) and the
    /// final ones (the shell at exit): the folder, the variables, then what is exported. Exported variables that
    /// changed are added to <paramref name="exports"/>.
    /// </summary>
    internal static string BuildRestore(
        string baseVars, string finalVars,
        string baseExports, string finalExports,
        string folder,
        List<KeyValuePair<string, string?>> exports)
    {
        var restore = new StringBuilder();
        restore.Append("command cd -- ").Append(BashDump.Quote(folder)).Append(" || command printf '%s\\n' ")
            .Append(BashDump.Quote($"could not return to {folder}: it no longer exists or cannot be entered")).Append(" >&2\n");

        // Each shell writes its values in quotes that it reads back, so a statement is restored as written.
        var before = SplitAssignments(baseVars);
        var after = SplitAssignments(finalVars);
        foreach (var (name, statement) in after)
        {
            if (!BashDump.IsSkippedVariable(name) && !(before.TryGetValue(name, out var old) && old == statement))
                restore.Append(statement).Append('\n');
        }

        foreach (var name in before.Keys)
        {
            if (!after.ContainsKey(name) && !BashDump.IsSkippedVariable(name))
                restore.Append("unset -v ").Append(name).Append('\n');
        }

        var exportedBefore = SplitExports(baseExports);
        foreach (var (name, statement) in SplitExports(finalExports))
        {
            if (BashDump.IsSkippedVariable(name) || (exportedBefore.TryGetValue(name, out var old) && old == statement))
                continue;
            restore.Append("export ").Append(name).Append('\n');
            exports.Add(new(name, ShellText.AssignedValue(statement) ?? (after.TryGetValue(name, out var assignment) ? ShellText.AssignedValue(assignment) : null)));
        }

        return restore.ToString();
    }

    /// <summary>The <c>name=value</c> statements of a <c>set</c> dump by name. Other lines, such as a function bash lists, are left out.</summary>
    internal static Dictionary<string, string> SplitAssignments(string dump)
    {
        var statements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var statement in ShellText.SplitStatements(dump))
        {
            var equals = statement.IndexOf('=');
            if (equals > 0 && ShellText.IsName(statement.AsSpan(0, equals)))
                statements[statement[..equals]] = statement;
        }

        return statements;
    }

    /// <summary>The statements of an <c>export -p</c> dump by name: <c>export NAME='value'</c>, or <c>export NAME</c> without a value.</summary>
    internal static Dictionary<string, string> SplitExports(string dump)
    {
        var statements = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var statement in ShellText.SplitStatements(dump))
        {
            if (!statement.StartsWith("export ", StringComparison.Ordinal))
                continue;

            var rest = statement.AsSpan(7);
            while (rest.Length > 0 && rest[0] == '-')
            {
                var space = rest.IndexOf(' ');
                rest = space < 0 ? [] : rest[(space + 1)..];
            }

            var end = rest.IndexOfAny('=', ' ');
            var name = end < 0 ? rest : rest[..end];
            if (ShellText.IsName(name))
                statements[name.ToString()] = statement;
        }

        return statements;
    }
}

/// <summary>
/// PowerShell: the command is wrapped on its first and last lines, so its line numbers stay as written. The first
/// line notes the shell as it started, then restores the state; a <c>finally</c> after the last line saves what
/// differs, and the exit code is made again from <c>$?</c>, as <c>-Command</c> makes it, because the save is the
/// last statement now. The state goes from one command to the next as a CliXml file, which PowerShell reads and
/// writes itself.
/// </summary>
internal sealed class PowerShellState : ShellState
{
    // The state the last command saved, or null before the first one.
    private string? _input;

    protected override PreparedCommand Prepare(string command, string folder)
    {
        var restore = Path.Combine(folder, "restore.ps1");
        var save = Path.Combine(folder, "save.ps1");
        File.WriteAllText(restore, BuildRestore(_input, Path.Combine(folder, "restore.err")), new UTF8Encoding(false));
        File.WriteAllText(save, BuildSave(Path.Combine(folder, "state.xml"), Path.Combine(folder, "exports.txt"), Path.Combine(folder, "restore.err"), Path.Combine(folder, "final.pwd")), new UTF8Encoding(false));
        // The scripts are read as text and run as script blocks: Windows PowerShell's default execution policy
        // refuses to run a .ps1 file, and a script block is not checked.
        var text = $". ([scriptblock]::Create([IO.File]::ReadAllText({Literal(restore)}))); $__scenetake_ok = $false; try {{ {command}\n" +
                   $"$__scenetake_ok = $? }} finally {{ . ([scriptblock]::Create([IO.File]::ReadAllText({Literal(save)}))) }}; if (-not $__scenetake_ok) {{ exit 1 }}";
        return new PreparedCommand(text, null);
    }

    protected override void Take(string folder, List<KeyValuePair<string, string?>> exports)
    {
        var state = Path.Combine(folder, "state.xml");
        if (!File.Exists(state))
            return;

        // The folder is removed after this, so the state moves to a file of its own.
        var kept = Path.Combine(Path.GetDirectoryName(folder)!, "powershell-state.xml");
        File.Copy(state, kept, overwrite: true);
        _input = kept;
        var location = Path.Combine(folder, "final.pwd");
        if (File.Exists(location))
            Folder = File.ReadAllText(location);

        var exportsPath = Path.Combine(folder, "exports.txt");
        if (!File.Exists(exportsPath))
            return;

        foreach (var line in File.ReadAllLines(exportsPath))
        {
            var space = line.IndexOf(' ');
            if (space <= 0)
                continue;
            try
            {
                exports.Add(new(
                    Encoding.UTF8.GetString(Convert.FromBase64String(line[..space])),
                    Encoding.UTF8.GetString(Convert.FromBase64String(line[(space + 1)..]))));
            }
            catch (FormatException)
            {
                // A line the save cut short.
            }
        }
    }

    /// <summary>A single-quoted PowerShell string.</summary>
    internal static string Literal(string text) => "'" + text.Replace("'", "''") + "'";

    // Variables that PowerShell sets by itself as commands run. The others that it starts with are in the noted
    // shell, so only the preference variables among them pass on, when a step changes them.
    private const string SkippedVariables = "'LASTEXITCODE','Matches','StackTrace','_','PSItem','PWD','args','input','MyInvocation','PSBoundParameters','PSCmdlet','PSCommandPath','PSScriptRoot','this','foreach','switch','sender','EventArgs','Event','EventSubscriber','SourceArgs','SourceEventArgs'";

    internal static string BuildRestore(string? input, string errors) =>
        $$"""
        # Written by scenetake: notes the shell as it started, then restores the state of the commands before this one (ShellState.cs).
        $global:__scenetake_base = @{ Variables = @{}; Functions = @{}; Aliases = @{}; Environment = @{} }
        foreach ($__scenetake_item in Get-Variable -Scope Global) { $global:__scenetake_base.Variables[$__scenetake_item.Name] = $(if ($__scenetake_item.Name -like '*Preference') { "$($__scenetake_item.Value)" } else { $null }) }
        foreach ($__scenetake_item in Get-ChildItem function:) { $global:__scenetake_base.Functions[$__scenetake_item.Name] = $__scenetake_item.Definition }
        foreach ($__scenetake_item in Get-Alias) { $global:__scenetake_base.Aliases[$__scenetake_item.Name] = $__scenetake_item.Definition }
        foreach ($__scenetake_item in Get-ChildItem env:) { $global:__scenetake_base.Environment[$__scenetake_item.Name] = $__scenetake_item.Value }
        Remove-Variable -Name __scenetake_item -ErrorAction SilentlyContinue
        {{(input is null ? "" : RestoreFrom(input, errors))}}
        """;

    private static string RestoreFrom(string input, string errors) =>
        $$"""
        try {
            $__scenetake_state = Import-Clixml -LiteralPath {{Literal(input)}}
            try { Set-Location -LiteralPath $__scenetake_state.Location -ErrorAction Stop } catch { [IO.File]::AppendAllText({{Literal(errors)}}, "could not return to $($__scenetake_state.Location): it no longer exists or cannot be entered`n") }
            foreach ($__scenetake_name in $__scenetake_state.Environment.Keys) { [Environment]::SetEnvironmentVariable($__scenetake_name, $__scenetake_state.Environment[$__scenetake_name]) }
            foreach ($__scenetake_name in $__scenetake_state.RemovedEnvironment) { [Environment]::SetEnvironmentVariable($__scenetake_name, $null) }
            foreach ($__scenetake_name in $__scenetake_state.Variables.Keys) { Set-Variable -Scope Global -Name $__scenetake_name -Value $__scenetake_state.Variables[$__scenetake_name] -Force -ErrorAction SilentlyContinue }
            foreach ($__scenetake_name in $__scenetake_state.Functions.Keys) { Set-Item -LiteralPath "function:global:$__scenetake_name" -Value ([scriptblock]::Create($__scenetake_state.Functions[$__scenetake_name])) -ErrorAction SilentlyContinue }
            foreach ($__scenetake_name in $__scenetake_state.Aliases.Keys) { Set-Alias -Scope Global -Name $__scenetake_name -Value $__scenetake_state.Aliases[$__scenetake_name] -Force -ErrorAction SilentlyContinue }
        } catch {
            [IO.File]::AppendAllText({{Literal(errors)}}, "could not restore the shell state: $($_.Exception.Message)`n")
        }
        Remove-Variable -Name __scenetake_state, __scenetake_name -ErrorAction SilentlyContinue
        """;

    internal static string BuildSave(string output, string exports, string errors, string location) =>
        $$"""
        # Written by scenetake: saves what this command changed in the shell, for the next command (ShellState.cs).
        try {
            $__scenetake_base = $global:__scenetake_base
            $__scenetake_state = @{ Location = (Get-Location).Path; Environment = @{}; RemovedEnvironment = @(); Variables = @{}; Functions = @{}; Aliases = @{} }
            foreach ($__scenetake_item in Get-ChildItem env:) {
                if (-not $__scenetake_base.Environment.ContainsKey($__scenetake_item.Name) -or $__scenetake_base.Environment[$__scenetake_item.Name] -cne $__scenetake_item.Value) { $__scenetake_state.Environment[$__scenetake_item.Name] = $__scenetake_item.Value }
            }
            foreach ($__scenetake_name in $__scenetake_base.Environment.Keys) {
                if (-not (Test-Path -LiteralPath "env:$__scenetake_name")) { $__scenetake_state.RemovedEnvironment += $__scenetake_name }
            }
            $__scenetake_skip = @({{SkippedVariables}})
            foreach ($__scenetake_item in Get-Variable -Scope Global) {
                $__scenetake_name = $__scenetake_item.Name
                if ($__scenetake_name -like '__scenetake*' -or $__scenetake_skip -contains $__scenetake_name) { continue }
                if ($__scenetake_item.Options -band [System.Management.Automation.ScopedItemOptions]::Constant) { continue }
                if ($__scenetake_base.Variables.ContainsKey($__scenetake_name)) {
                    if ($__scenetake_name -notlike '*Preference' -or $__scenetake_base.Variables[$__scenetake_name] -ceq "$($__scenetake_item.Value)") { continue }
                }
                $__scenetake_state.Variables[$__scenetake_name] = $__scenetake_item.Value
            }
            foreach ($__scenetake_item in Get-ChildItem function:) {
                if (-not $__scenetake_base.Functions.ContainsKey($__scenetake_item.Name) -or $__scenetake_base.Functions[$__scenetake_item.Name] -cne $__scenetake_item.Definition) { $__scenetake_state.Functions[$__scenetake_item.Name] = $__scenetake_item.Definition }
            }
            foreach ($__scenetake_item in Get-Alias) {
                if (-not $__scenetake_base.Aliases.ContainsKey($__scenetake_item.Name) -or $__scenetake_base.Aliases[$__scenetake_item.Name] -cne $__scenetake_item.Definition) { $__scenetake_state.Aliases[$__scenetake_item.Name] = $__scenetake_item.Definition }
            }
            try {
                $__scenetake_state | Export-Clixml -LiteralPath {{Literal(output)}} -Depth 4
            } catch {
                # A variable whose value cannot be written, such as a live connection: the rest still passes on.
                [IO.File]::AppendAllText({{Literal(errors)}}, "could not pass on the variables of this step: $($_.Exception.Message)`n")
                $__scenetake_state.Variables = @{}
                $__scenetake_state | Export-Clixml -LiteralPath {{Literal(output)}} -Depth 4
            }
            $__scenetake_lines = foreach ($__scenetake_name in $__scenetake_state.Environment.Keys) {
                [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($__scenetake_name)) + ' ' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$__scenetake_state.Environment[$__scenetake_name]))
            }
            [IO.File]::WriteAllLines({{Literal(exports)}}, [string[]]@($__scenetake_lines))
            # The folder, as text for the prompt's {dir}: the CLI reads no CliXml.
            [IO.File]::WriteAllText({{Literal(location)}}, [string]$__scenetake_state.Location)
        } catch {
            [IO.File]::AppendAllText({{Literal(errors)}}, "could not save the shell state: $($_.Exception.Message)`n")
        }
        """;
}
