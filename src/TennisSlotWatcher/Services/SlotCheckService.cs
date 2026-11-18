using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TennisSlotWatcher.Models;

namespace TennisSlotWatcher.Services;

internal sealed record CheckResult(
    IReadOnlyList<MatchedSlot> Matching,
    IReadOnlyList<MatchedSlot> New,
    IReadOnlyList<DateOnly> FailedDates);

internal sealed class SlotCheckService(
    PlayFiClient client,
    ISlotStateStore store,
    INotifier notifier,
    IOptions<WatcherOptions> options,
    TimeProvider time,
    ILogger<SlotCheckService> log)
{
    private readonly WatcherOptions _options = options.Value;

    public async Task<CheckResult> RunAsync(bool dryRun, CancellationToken ct)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_options.TimeZone);
        var now = TimeZoneInfo.ConvertTime(time.GetUtcNow(), tz).DateTime;
        var today = DateOnly.FromDateTime(now);

        var dates = Enumerable.Range(0, _options.DaysAhead + 1)
            .Select(today.AddDays)
            .Where(d => _options.IsWatchedDay(d.DayOfWeek))
            .ToList();

        var matching = new List<MatchedSlot>();
        var failed = new List<DateOnly>();

        for (var i = 0; i < dates.Count; i++)
        {
            var date = dates[i];
            if (i > 0) await Task.Delay(_options.RequestDelayMs, ct);

            try
            {
                var html = await client.GetCalendarHtmlAsync(date, ct);
                var calendar = CalendarParser.Parse(html);

                if (calendar.PageDate is { } pageDate && pageDate != date)
                    throw new FormatException($"Requested {date:yyyy-MM-dd} but the site returned {pageDate:yyyy-MM-dd}.");

                var blocks = calendar.Blocks.Where(b => DateOnly.FromDateTime(b.Start) == date);
                matching.AddRange(SlotFilter.Match(blocks, _options, now));
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogError(ex, "Could not read calendar for {Date}; keeping previous state for that day.", date);
                failed.Add(date);
            }
        }

        var previous = await store.LoadAsync(ct);
        var newSlots = matching.Where(s => !previous.Contains(s.Key)).ToList();

        log.LogInformation("Checked {Days} days: {Matching} matching slots, {New} new, {Failed} failed days.",
            dates.Count, matching.Count, newSlots.Count, failed.Count);

        foreach (var s in matching.OrderBy(s => s.BookableStart).ThenBy(s => s.Block.CourtId))
            log.LogInformation("  {Day:ddd dd.MM.} {From:HH\\:mm}-{To:HH\\:mm} {Court}{New}",
                s.Block.Start, s.BookableStart, s.BookableEnd, s.Block.CourtName, newSlots.Contains(s) ? "  [NEW]" : "");

        if (dryRun) return new CheckResult(matching, newSlots, failed);

        // Notify first: if sending fails the state is not saved, so the next run retries.
        if (newSlots.Count > 0)
        {
            await notifier.NotifyAsync(newSlots, ct);
            log.LogInformation("Notification sent for {Count} new slot(s).", newSlots.Count);
        }
        else
        {
            log.LogInformation("No new slots - nothing to send.");
        }

        // New state = what is available now. Days we could not read keep their previous entries,
        // so a flaky page does not cause duplicate alerts later. Slots that disappeared (booked or past)
        // are dropped, which means a later cancellation alerts again.
        var failedPrefixes = failed.Select(d => d.ToString("yyyy-MM-dd")).ToList();
        var state = new HashSet<string>(matching.Select(s => s.Key), StringComparer.Ordinal);
        foreach (var key in previous)
            if (failedPrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)))
                state.Add(key);

        await store.SaveAsync(state, ct);
        return new CheckResult(matching, newSlots, failed);
    }
}
