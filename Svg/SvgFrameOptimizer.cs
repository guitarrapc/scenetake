internal static class SvgFrameOptimizer
{
    internal const double DefaultMaxFps = 12d;

    internal static List<ReplayFrame> Optimize(IReadOnlyList<ReplayFrame> frames, double maxFps = DefaultMaxFps)
    {
        if (frames.Count <= 1)
            return frames.ToList();

        var normalized = NormalizeTiming(frames, maxFps);
        var reduced = ReduceFrames(normalized, maxFps);
        return SpreadCollapsedFrameTimes(reduced, maxFps);
    }

    internal static List<ReplayFrame> NormalizeTiming(IReadOnlyList<ReplayFrame> frames, double maxFps)
    {
        if (frames.Count == 0 || maxFps <= 0)
            return frames.ToList();

        var interval = 1d / maxFps;
        var normalized = new List<ReplayFrame>(frames.Count);
        var lastTime = 0d;

        for (var i = 0; i < frames.Count; i++)
        {
            var rawTime = Math.Max(0d, frames[i].Time);
            var quantizedTime = Math.Round(rawTime / interval, MidpointRounding.AwayFromZero) * interval;
            if (i > 0 && quantizedTime < lastTime)
                quantizedTime = lastTime;

            normalized.Add(CloneAtTime(frames[i], quantizedTime));
            lastTime = quantizedTime;
        }

        return normalized;
    }

    internal static List<ReplayFrame> ReduceFrames(IReadOnlyList<ReplayFrame> frames, double maxFps)
    {
        if (frames.Count <= 2 || maxFps <= 0)
            return frames.ToList();

        var minimumInterval = 1d / maxFps;
        var reduced = new List<ReplayFrame>(frames.Count) { frames[0] };
        var lastKeptTime = frames[0].Time;
        var lastKeptSignature = TerminalReplay.BuildVisualSignature(frames[0].Buffer);
        ReplayFrame? pending = null;
        ulong pendingSignature = 0;

        for (var i = 1; i < frames.Count - 1; i++)
        {
            var frame = frames[i];
            var signature = TerminalReplay.BuildVisualSignature(frame.Buffer);
            var visualChanged = signature != lastKeptSignature;
            var elapsed = frame.Time - lastKeptTime;

            if (elapsed < minimumInterval)
            {
                if (visualChanged)
                {
                    pending = frame;
                    pendingSignature = signature;
                }

                continue;
            }

            if (pending is not null)
            {
                reduced.Add(pending);
                lastKeptTime = pending.Time;
                lastKeptSignature = pendingSignature;
                pending = null;

                signature = TerminalReplay.BuildVisualSignature(frame.Buffer);
                visualChanged = signature != lastKeptSignature;
                elapsed = frame.Time - lastKeptTime;
                if (elapsed < minimumInterval)
                {
                    if (visualChanged)
                    {
                        pending = frame;
                        pendingSignature = signature;
                    }

                    continue;
                }

                if (!visualChanged)
                    continue;
            }

            reduced.Add(frame);
            lastKeptTime = frame.Time;
            lastKeptSignature = signature;
        }

        if (pending is not null && !ReferenceEquals(reduced[^1], pending))
            reduced.Add(pending);

        var last = frames[^1];
        if (TerminalReplay.BuildVisualSignature(reduced[^1].Buffer) != TerminalReplay.BuildVisualSignature(last.Buffer)
            || Math.Abs(reduced[^1].Time - last.Time) > 1e-9)
        {
            reduced.Add(last);
        }

        return reduced;
    }

    internal static List<ReplayFrame> SpreadCollapsedFrameTimes(IReadOnlyList<ReplayFrame> frames, double maxFps)
    {
        if (frames.Count <= 1)
            return frames.ToList();

        List<ReplayFrame>? adjusted = null;
        for (var runStart = 0; runStart < frames.Count;)
        {
            var runEnd = runStart;
            while (runEnd + 1 < frames.Count && HaveSameTime(frames[runEnd + 1].Time, frames[runStart].Time))
                runEnd++;

            if (runEnd == runStart)
            {
                adjusted?.Add(frames[runStart]);
                runStart++;
                continue;
            }

            adjusted ??= new List<ReplayFrame>(frames.Count);
            if (adjusted.Count == 0)
            {
                for (var copyIndex = 0; copyIndex < runStart; copyIndex++)
                    adjusted.Add(frames[copyIndex]);
            }

            var runCount = runEnd - runStart + 1;
            if (runStart == 0 && frames[runStart].Time <= 0d)
            {
                var upperBound = runEnd + 1 < frames.Count
                    ? frames[runEnd + 1].Time
                    : GetMinimumFrameInterval(maxFps);
                if (upperBound <= 0d)
                    upperBound = GetMinimumFrameInterval(maxFps);

                var step = upperBound / runCount;
                for (var offset = 0; offset < runCount; offset++)
                    adjusted.Add(CloneAtTime(frames[runStart + offset], step * offset));
            }
            else
            {
                var lowerBound = adjusted.Count > 0 ? adjusted[^1].Time : 0d;
                var upperBound = frames[runStart].Time;
                var step = (upperBound - lowerBound) / runCount;
                if (step <= 0d)
                    step = GetMinimumFrameInterval(maxFps) / runCount;

                for (var offset = 0; offset < runCount; offset++)
                    adjusted.Add(CloneAtTime(frames[runStart + offset], lowerBound + (step * (offset + 1))));
            }

            runStart = runEnd + 1;
        }

        return adjusted ?? frames.ToList();
    }

    private static ReplayFrame CloneAtTime(ReplayFrame frame, double time) =>
        new(time, frame.Buffer, frame.ViewportWidth, frame.ViewportHeight);

    private static bool HaveSameTime(double left, double right) =>
        Math.Abs(left - right) <= 1e-9;

    private static double GetMinimumFrameInterval(double maxFps) =>
        maxFps > 0d ? 1d / maxFps : 0.05d;
}
