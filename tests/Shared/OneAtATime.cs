using TUnit.Core.Interfaces;

// The product keeps state for the whole process: the font cache that rendering loads and releases, and
// Console.Error, which the playground's renderer swaps while it runs. The tests also time recordings, which other
// tests on the same CPU would slow. So each test project runs one test at a time.
[assembly: ParallelLimiter<Scenetake.TestSupport.OneAtATime>]

namespace Scenetake.TestSupport;

internal sealed class OneAtATime : IParallelLimit
{
    public int Limit => 1;
}
