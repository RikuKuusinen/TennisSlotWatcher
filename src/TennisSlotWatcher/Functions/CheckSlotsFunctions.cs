using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TennisSlotWatcher.Services;

namespace TennisSlotWatcher.Functions;

internal sealed class CheckSlotsFunctions(SlotCheckService service, ILogger<CheckSlotsFunctions> log)
{
    /// <summary>
    /// Runs every 2 hours between 10:00 and 20:00 by default (override with the CheckSchedule app setting).
    /// The cron is evaluated in the Function App's time zone - set WEBSITE_TIME_ZONE (see README).
    /// Debug builds (local development) also run once immediately on startup; Release builds do not,
    /// so deployments and restarts in Azure don't trigger an extra check.
    /// </summary>
    [Function("CheckSlots")]
    public async Task Run(
#if DEBUG
        [TimerTrigger("%CheckSchedule%", RunOnStartup = true)] TimerInfo timer,
#else
        [TimerTrigger("%CheckSchedule%")] TimerInfo timer,
#endif
        CancellationToken ct)
    {
        log.LogInformation("=== CheckSlots started (past due: {PastDue}) ===", timer.IsPastDue);
        var result = await service.RunAsync(dryRun: false, ct);
        log.LogInformation("=== CheckSlots finished: {Matching} matching, {New} new, {Failed} failed days ===",
            result.Matching.Count, result.New.Count, result.FailedDates.Count);
    }

    /// <summary>
    /// Manual check. GET /api/check?dryRun=true lists matching slots without notifying or touching state
    /// (the default). Use dryRun=false to behave exactly like the timer. Requires a function key.
    /// </summary>
    [Function("CheckNow")]
    public async Task<IActionResult> CheckNow(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "check")] HttpRequest req,
        CancellationToken ct)
    {
        var dryRun = !string.Equals(req.Query["dryRun"], "false", StringComparison.OrdinalIgnoreCase);
        var result = await service.RunAsync(dryRun, ct);

        return new OkObjectResult(new
        {
            dryRun,
            failedDates = result.FailedDates.Select(d => d.ToString("yyyy-MM-dd")),
            newCount = result.New.Count,
            matching = result.Matching.Select(s => new
            {
                date = s.Block.Start.ToString("yyyy-MM-dd ddd"),
                court = s.Block.CourtName,
                bookable = $"{s.BookableStart:HH:mm}-{s.BookableEnd:HH:mm}",
                block = $"{s.Block.Start:HH:mm}+{s.Block.DurationMinutes}min",
                isNew = result.New.Contains(s),
            })
        });
    }
}
