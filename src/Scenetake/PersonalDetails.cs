using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Scenetake;

/// <summary>
/// Collects what names the person who records, for <c>settings.mask-personal</c> (spec_scenario.md → Masking personal
/// details): the user name, the home folder's name, the computer's name, and the git name and email. Commands print
/// them without being asked: <c>pwd</c> under the home folder, <c>ls -l</c>, <c>git log</c>, a stack trace.
/// </summary>
internal static class PersonalDetails
{
    // git config answers at once. A wait this long means something is wrong, and the recording goes on without it.
    private const int GitTimeoutMilliseconds = 5000;

    /// <summary>
    /// The details of this machine and its user. <paramref name="folder"/> is where git reads its config, so a
    /// conditional include for that folder (includeIf "gitdir:~/work/") gives the email used there.
    /// </summary>
    internal static List<PersonalDetail> Collect(string? folder)
    {
        var details = new List<PersonalDetail>();
        Add(details, Environment.UserName, PersonalDetail.UserName);

        // The home folder's name is usually the user name, but not always: Windows can cut it or add the domain.
        // Git Bash may set HOME to another folder than the user profile. Its 8.3 name, such as ADALOV~1, is in TEMP.
        foreach (var home in HomeFolders())
            Add(details, LastName(home), PersonalDetail.HomeFolderName);

        try
        {
            Add(details, Environment.MachineName, PersonalDetail.ComputerName);
        }
        catch (InvalidOperationException)
        {
            // The name could not be read. The DNS name below may still be.
        }

        if (OperatingSystem.IsWindows())
            Add(details, Kernel32.GetDnsHostName(), PersonalDetail.ComputerName);

        // The identity git commits with when these are set, whatever its config says.
        Add(details, Environment.GetEnvironmentVariable("GIT_AUTHOR_NAME"), PersonalDetail.GitName);
        Add(details, Environment.GetEnvironmentVariable("GIT_COMMITTER_NAME"), PersonalDetail.GitName);
        Add(details, Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL"), PersonalDetail.GitEmail);
        Add(details, Environment.GetEnvironmentVariable("GIT_COMMITTER_EMAIL"), PersonalDetail.GitEmail);

        ReadGitIdentity(details, folder is not null && Path.IsPathRooted(folder) && Directory.Exists(folder) ? folder : Environment.CurrentDirectory);
        return details;
    }

    /// <summary>
    /// The home folder in each form a command may print it, for its path to show as ~ (PathStandIn): the user profile,
    /// which PowerShell on Windows takes as home, and HOME, which Git Bash and Unix shells take, each in its forms
    /// (<see cref="FolderPaths.Forms"/>).
    /// </summary>
    internal static List<string> HomeFolders()
    {
        var folders = FolderPaths.Forms(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        foreach (var form in FolderPaths.Forms(Environment.GetEnvironmentVariable("HOME")))
            FolderPaths.AddNew(folders, form);
        return folders;
    }

    private static void Add(List<PersonalDetail> details, string? value, string kind)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details.Add(new PersonalDetail(value, kind));
    }

    private static string? LastName(string? path)
    {
        var trimmed = path?.TrimEnd('/', '\\');
        if (string.IsNullOrEmpty(trimmed))
            return null;

        var slash = trimmed.LastIndexOfAny(['/', '\\']);
        return slash < 0 ? trimmed : trimmed[(slash + 1)..];
    }

    /// <summary>
    /// user.name and user.email from the global and system git config, and from the command scope: GIT_CONFIG_COUNT
    /// and GIT_CONFIG_PARAMETERS in the environment, which the steps get too. A repository's own config is left out:
    /// a demo repository may set an identity on purpose, and it is that repository's.
    /// </summary>
    private static void ReadGitIdentity(List<PersonalDetail> details, string folder)
    {
        var git = FindOnPath("git");
        if (git is null)
            return;

        // --show-scope (git 2.26) tells the global and system values from the repository's. An older git refuses it,
        // and then the global config is read alone.
        if (RunGitConfig(git, folder, ["--show-scope", "--get-regexp", @"^user\.(name|email)$"]) is { } scoped)
        {
            ParseGitLines(details, scoped, scoped: true);
            return;
        }

        if (RunGitConfig(git, folder, ["--global", "--includes", "--get-regexp", @"^user\.(name|email)$"]) is { } global)
            ParseGitLines(details, global, scoped: false);
    }

    // Lines such as "global\tuser.name Ada Lovelace", or "user.name Ada Lovelace" without the scope.
    private static void ParseGitLines(List<PersonalDetail> details, string output, bool scoped)
    {
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (scoped)
            {
                var tab = line.IndexOf('\t');
                if (tab < 0 || line[..tab] is not ("global" or "system" or "command"))
                    continue;
                line = line[(tab + 1)..];
            }

            var space = line.IndexOf(' ');
            if (space < 0)
                continue;

            var kind = line[..space].Equals("user.email", StringComparison.OrdinalIgnoreCase) ? PersonalDetail.GitEmail : PersonalDetail.GitName;
            Add(details, line[(space + 1)..], kind);
        }
    }

    /// <summary>git config's output, or null when git failed, refused the options, or took too long. No match is empty.</summary>
    private static string? RunGitConfig(string git, string folder, string[] arguments)
    {
        var start = new ProcessStartInfo(git)
        {
            WorkingDirectory = folder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("config");
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return null;

            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(GitTimeoutMilliseconds))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // It ended in the meantime.
                }

                return null;
            }

            // 1 is "no value matched", as when no identity is set.
            return process.ExitCode switch
            {
                0 => output.GetAwaiter().GetResult(),
                1 => "",
                _ => null,
            };
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// The full path of <paramref name="name"/> in a PATH folder, or null. Never the current directory, from which .NET
    /// would start a name first: a cloned repository could hold its own git.
    /// </summary>
    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        string[] extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [""];
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // A relative PATH entry names the current directory or a folder under it.
            if (!Path.IsPathFullyQualified(folder))
                continue;

            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(folder, name + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
