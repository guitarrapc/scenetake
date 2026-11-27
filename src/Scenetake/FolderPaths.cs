namespace Scenetake;

/// <summary>
/// The forms of a folder's path that a command may print, for the path to show as a stand-in (PathStandIn): the path
/// itself, and on Windows its long and its short (8.3) form, since TEMP and cmd write the user profile as
/// C:\Users\ADALOV~1 while Git Bash and .NET write it in full. On Unix, also the path with its links resolved, which
/// <c>pwd</c> prints when the shell reads the folder from the system, as <c>/private/tmp/demo</c> for
/// <c>/tmp/demo</c> on macOS.
/// </summary>
internal static class FolderPaths
{
    /// <summary>The full forms of <paramref name="folder"/>, none for a relative or empty path.</summary>
    internal static List<string> Forms(string? folder)
    {
        var forms = new List<string>();
        if (string.IsNullOrWhiteSpace(folder))
            return forms;

        // Git Bash writes a Windows folder as /c/Users/name.
        var native = PromptTemplate.NativePath(folder.Trim());
        if (!Path.IsPathFullyQualified(native))
            return forms;

        string full;
        try
        {
            full = Path.GetFullPath(native);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return forms;
        }

        AddNew(forms, full);
        if (OperatingSystem.IsWindows())
        {
            AddNew(forms, Kernel32.GetLongPathName(full));
            AddNew(forms, Kernel32.GetShortPathName(full));
        }
        else
        {
            AddNew(forms, Resolved(full));
        }

        return forms;
    }

    /// <summary>Adds <paramref name="form"/> unless the list has it, case ignored: the masker ignores case too.</summary>
    private static void AddNew(List<string> forms, string? form)
    {
        if (string.IsNullOrEmpty(form))
            return;
        foreach (var known in forms)
        {
            if (string.Equals(known, form, StringComparison.OrdinalIgnoreCase))
                return;
        }

        forms.Add(form);
    }

    /// <summary>The path with each link in it resolved, as the system gives a process's folder, or null.</summary>
    private static string? Resolved(string path)
    {
        try
        {
            var current = "/";
            foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var next = Path.Join(current, name);
                // The last folder that a chain of links leads to, resolved against the folder that holds the link.
                current = Directory.ResolveLinkTarget(next, returnFinalTarget: true)?.FullName ?? next;
            }

            return current;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
