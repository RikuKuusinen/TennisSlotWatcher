namespace TennisSlotWatcher;

/// <summary>
/// All settings bind from the "Watcher" configuration section
/// (app settings such as <c>Watcher__MinDurationMinutes</c>). Defaults match the original requirements.
/// </summary>
public sealed class WatcherOptions
{
    public const string SectionName = "Watcher";

    /// <summary>Venue root on play.fi.</summary>
    public string BaseUrl { get; set; } = "https://play.fi/laajasalo";

    /// <summary>play.fi "p_laji" (sport). 1 = Tennis.</summary>
    public int SportId { get; set; } = 1;

    /// <summary>play.fi "p_location".</summary>
    public int LocationId { get; set; } = 1;

    /// <summary>
    /// How many days ahead (inclusive of today) to look. play.fi only publishes the calendar up to today + 28 days;
    /// for later dates it silently returns today's calendar (the date check in <c>SlotCheckService</c> then flags the day as failed).
    /// </summary>
    public int DaysAhead { get; set; } = 28;

    /// <summary>
    /// Weekdays to watch, comma-separated English day names (e.g. "Monday,Tuesday,Thursday").
    /// Defaults to Monday, Tuesday and Thursday. A single string is used instead of an array because
    /// the configuration binder merges array settings with the defaults instead of replacing them.
    /// </summary>
    public string Weekdays { get; set; } = "Monday,Tuesday,Thursday";

    /// <summary>Earliest wanted time of day. The bookable part of a free block must lie inside the window.</summary>
    public TimeSpan WindowStart { get; set; } = TimeSpan.FromHours(18);

    /// <summary>Latest wanted end time of day.</summary>
    public TimeSpan WindowEnd { get; set; } = TimeSpan.FromHours(21);

    /// <summary>Minimum bookable minutes inside the window.</summary>
    public int MinDurationMinutes { get; set; } = 60;

    /// <summary>Court resource ids to include, comma-separated. Defaults to T1-T6 (resid 1-6).</summary>
    public string CourtIds { get; set; } = "1,2,3,4,5,6";

    /// <summary>Time zone used to decide "today" and to interpret the site's times.</summary>
    public string TimeZone { get; set; } = "Europe/Helsinki";

    /// <summary>Pause between page requests, to be polite to the site.</summary>
    public int RequestDelayMs { get; set; } = 750;

    public NtfyOptions Ntfy { get; set; } = new();

    public bool IsWatchedDay(DayOfWeek day) => ParseWeekdays().Contains(day);

    public bool IsWantedCourt(int courtId) => ParseCourtIds().Contains(courtId);

    internal IReadOnlyList<DayOfWeek> ParseWeekdays() => Split(Weekdays)
        .Select(s => Enum.TryParse<DayOfWeek>(s, ignoreCase: true, out var d) && !char.IsDigit(s[0])
            ? d
            : throw new InvalidOperationException($"Watcher:Weekdays contains '{s}', which is not an English day name."))
        .ToList();

    internal IReadOnlyList<int> ParseCourtIds() => Split(CourtIds)
        .Select(s => int.TryParse(s, out var id)
            ? id
            : throw new InvalidOperationException($"Watcher:CourtIds contains '{s}', which is not a number."))
        .ToList();

    private static IEnumerable<string> Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);}

public sealed class NtfyOptions
{
    public string Server { get; set; } = "https://ntfy.sh";

    /// <summary>Topic name. Treat it as a secret: anyone who knows it can read the alerts.</summary>
    public string Topic { get; set; } = "";

    /// <summary>Optional bearer token for protected topics / self-hosted servers.</summary>
    public string? Token { get; set; }
}
