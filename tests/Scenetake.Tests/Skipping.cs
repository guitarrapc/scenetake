using Scenetake.TestSupport;
namespace Scenetake.Tests;

/// <summary>
/// Turns a missing prerequisite into a skip. A rendering test that finds no usable font, or a test that throws its
/// SkipTestException (no symbolic links, a user who reads every file, no pwsh), has nothing to check on this machine.
/// </summary>
internal static class Skipping
{
    internal static bool Run(Func<bool> test)
    {
        try
        {
            return test();
        }
        catch (FontNotFoundException ex)
        {
            Skip.Test(ex.Message);
            return true;
        }
        catch (Exception ex) when (ex.GetType().Name == "SkipTestException")
        {
            Skip.Test(ex.Message);
            return true;
        }
    }
}
