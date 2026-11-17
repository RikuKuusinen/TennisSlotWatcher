using TennisSlotWatcher.Models;
using TennisSlotWatcher.Services;

namespace TennisSlotWatcher.Tests;

public class CalendarParserTests
{
    private static string Html() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "calendar-sample.html"));

    [Fact]
    public void Parses_all_bookable_anchors_and_page_date()
    {
        var parsed = CalendarParser.Parse(Html());

        Assert.Equal(new DateOnly(2026, 11, 17), parsed.PageDate);
        // 7 anchors; the "T2 22:30" cell has no link and must be ignored.
        Assert.Equal(7, parsed.Blocks.Count);

        var t3 = parsed.Blocks.Single(b => b.CourtName == "T3");
        Assert.Equal(new DateTime(2026, 11, 17, 7, 0, 0), t3.Start);
        Assert.Equal(90, t3.DurationMinutes);
        Assert.Equal(3, t3.CourtId);
    }

    [Fact]
    public void Throws_when_page_is_not_a_calendar()
    {
        Assert.Throws<FormatException>(() => CalendarParser.Parse("<html>maintenance</html>"));
    }
}

public class SlotFilterTests
{
    private static readonly WatcherOptions Options = new();
    private static readonly DateTime Morning = new(2026, 11, 17, 8, 0, 0);

    private static IReadOnlyList<MatchedSlot> Matches(DateTime now)
    {
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "calendar-sample.html"));
        return SlotFilter.Match(CalendarParser.Parse(html).Blocks, Options, now).ToList();
    }

    [Fact]
    public void Keeps_only_courts_1_to_6_with_one_hour_inside_18_to_21()
    {
        var result = Matches(Morning);

        // T4 17:00 +150min -> 18:00-19:30 (90 min inside the window) and T5 19:00 +60 qualify.
        // T6 20:30 +60 only has 30 min inside the window; T1 19:00 +30 is too short;
        // T3 07:00 is outside the window; Pallotykki (resid 18) is not a wanted court.
        Assert.Equal(["T4", "T5"], result.Select(m => m.Block.CourtName).Order().ToArray());

        var t4 = result.Single(m => m.Block.CourtName == "T4");
        Assert.Equal(new DateTime(2026, 11, 17, 18, 0, 0), t4.BookableStart);
        Assert.Equal(new DateTime(2026, 11, 17, 19, 30, 0), t4.BookableEnd);
    }

    [Fact]
    public void Ignores_time_that_has_already_passed()
    {
        // At 19:00 the T4 block only has 30 min left in the window, so only T5 (19:00-20:00) remains.
        var result = Matches(new DateTime(2026, 11, 17, 19, 0, 0));
        Assert.Equal(["T5"], result.Select(m => m.Block.CourtName).ToArray());
    }

    [Fact]
    public void Key_is_stable_between_runs()
    {
        var a = Matches(Morning).Single(m => m.Block.CourtName == "T4");
        var b = Matches(Morning.AddHours(5)).Single(m => m.Block.CourtName == "T4");
        Assert.Equal(a.Key, b.Key);
        Assert.StartsWith("2026-11-17", a.Key);
    }
}

public class MessageFormatterTests
{
    [Fact]
    public void Body_groups_by_day()
    {
        var block = new FreeBlock(new DateTime(2026, 11, 17, 18, 0, 0), 120, 4, "T4", "/laajasalo/booking/create-booking?x=1");
        var slot = new MatchedSlot(block, block.Start, block.End);

        var body = MessageFormatter.BuildBody([slot]);

        Assert.Contains("Tue 17.11.", body);
        Assert.Contains("18:00-20:00  T4", body);
    }
}
