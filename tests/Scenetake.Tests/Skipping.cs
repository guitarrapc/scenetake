namespace Scenetake.Tests;

/// <summary>
/// Turns a missing font into a skip. A rendering test that finds no usable font on this machine has nothing to check.
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
    }
}
