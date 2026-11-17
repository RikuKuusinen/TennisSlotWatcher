using TennisSlotWatcher.Models;

namespace TennisSlotWatcher.Services;

internal static class SlotFilter
{
    /// <summary>
    /// Keeps blocks on wanted courts whose part inside [WindowStart, WindowEnd] (and not already in the past)
    /// is at least <see cref="WatcherOptions.MinDurationMinutes"/> long.
    /// </summary>
    public static IEnumerable<MatchedSlot> Match(IEnumerable<FreeBlock> blocks, WatcherOptions options, DateTime now)
    {
        foreach (var block in blocks)
        {
            if (!options.CourtIds.Contains(block.CourtId)) continue;

            var date = block.Start.Date;
            var from = Max(Max(block.Start, date + options.WindowStart), now);
            var to = Min(block.End, date + options.WindowEnd);

            if ((to - from).TotalMinutes >= options.MinDurationMinutes)
                yield return new MatchedSlot(block, from, to);
        }
    }

    private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}
