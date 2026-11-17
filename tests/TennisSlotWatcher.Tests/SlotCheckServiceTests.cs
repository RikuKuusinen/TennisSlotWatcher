using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TennisSlotWatcher.Models;
using TennisSlotWatcher.Services;

namespace TennisSlotWatcher.Tests;

public class SlotCheckServiceTests
{
    // Monday 2026-11-16 10:00 Helsinki (UTC+2) -> 08:00 UTC.
    private sealed class FixedTime(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
    }

    private sealed class FakeStore : ISlotStateStore
    {
        public HashSet<string> Keys { get; private set; } = [];
        public Task<HashSet<string>> LoadAsync(CancellationToken ct) => Task.FromResult(new HashSet<string>(Keys));
        public Task SaveAsync(IReadOnlyCollection<string> keys, CancellationToken ct)
        {
            Keys = new HashSet<string>(keys);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNotifier : INotifier
    {
        public List<IReadOnlyList<MatchedSlot>> Sent { get; } = [];
        public Task NotifyAsync(IReadOnlyList<MatchedSlot> newSlots, CancellationToken ct)
        {
            Sent.Add(newSlots);
            return Task.CompletedTask;
        }
    }

    /// <summary>Serves the sample calendar for whatever date is requested, except dates listed in <see cref="FailDates"/>.</summary>
    private sealed class FakeSite : HttpMessageHandler
    {
        public HashSet<string> FailDates { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            var date = query["BookingCalForm[p_pvm]"]!;
            if (FailDates.Contains(date))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "calendar-sample.html"))
                .Replace("2026-11-17", date);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }

    private static (SlotCheckService Service, FakeStore Store, FakeNotifier Notifier, FakeSite Site) Create()
    {
        var options = Options.Create(new WatcherOptions { RequestDelayMs = 0 });
        var site = new FakeSite();
        var client = new PlayFiClient(new HttpClient(site), options);
        var store = new FakeStore();
        var notifier = new FakeNotifier();
        var time = new FixedTime(new DateTimeOffset(2026, 11, 16, 8, 0, 0, TimeSpan.Zero));
        return (new SlotCheckService(client, store, notifier, options, time, NullLogger<SlotCheckService>.Instance),
            store, notifier, site);
    }

    [Fact]
    public async Task Only_watched_weekdays_in_two_week_horizon_are_checked_and_alert_once()
    {
        var (service, store, notifier, _) = Create();

        var first = await service.RunAsync(dryRun: false, CancellationToken.None);

        // Mon 16 .. Mon 30 Nov, Mon-Thu only = 16,17,18,19,23,24,25,26,30 -> 9 days x (T4 + T5)
        Assert.Equal(18, first.New.Count);
        Assert.DoesNotContain(first.Matching, s => s.Block.Start.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Single(notifier.Sent);
        Assert.Equal(18, store.Keys.Count);

        var second = await service.RunAsync(dryRun: false, CancellationToken.None);
        Assert.Empty(second.New);
        Assert.Single(notifier.Sent); // no second notification
    }

    [Fact]
    public async Task Dry_run_neither_notifies_nor_saves()
    {
        var (service, store, notifier, _) = Create();

        var result = await service.RunAsync(dryRun: true, CancellationToken.None);

        Assert.NotEmpty(result.New);
        Assert.Empty(notifier.Sent);
        Assert.Empty(store.Keys);
    }

    [Fact]
    public async Task Failed_day_keeps_previous_state_and_does_not_realert()
    {
        var (service, store, notifier, site) = Create();
        await service.RunAsync(dryRun: false, CancellationToken.None);

        site.FailDates.Add("2026-11-18");
        var result = await service.RunAsync(dryRun: false, CancellationToken.None);
        Assert.Equal([new DateOnly(2026, 11, 18)], result.FailedDates);
        Assert.Equal(18, store.Keys.Count); // 2026-11-18 entries preserved

        site.FailDates.Clear();
        var recovered = await service.RunAsync(dryRun: false, CancellationToken.None);
        Assert.Empty(recovered.New);
        Assert.Single(notifier.Sent);
    }

    [Fact]
    public async Task Slot_that_disappears_and_returns_alerts_again()
    {
        var (service, store, notifier, _) = Create();
        await service.RunAsync(dryRun: false, CancellationToken.None);

        // Simulate the slot having been booked in between: forget one key as the state would after it vanished.
        var removed = store.Keys.First(k => k.StartsWith("2026-11-17"));
        store.Keys.Remove(removed);

        var result = await service.RunAsync(dryRun: false, CancellationToken.None);
        Assert.Single(result.New);
        Assert.Equal(removed, result.New[0].Key);
        Assert.Equal(2, notifier.Sent.Count);
    }
}
