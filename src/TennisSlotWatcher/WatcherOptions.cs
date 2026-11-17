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

    /// <summary>How many days ahead (inclusive of today) to look.</summary>
    public int DaysAhead { get; set; } = 14;

    /// <summary>Weekdays to watch. Defaults to Monday-Thursday (no Fri/Sat/Sun).</summary>
    public DayOfWeek[] Weekdays { get; set; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday];

    /// <summary>Earliest wanted time of day. The bookable part of a free block must lie inside the window.</summary>
    public TimeSpan WindowStart { get; set; } = TimeSpan.FromHours(18);

    /// <summary>Latest wanted end time of day.</summary>
    public TimeSpan WindowEnd { get; set; } = TimeSpan.FromHours(21);

    /// <summary>Minimum bookable minutes inside the window.</summary>
    public int MinDurationMinutes { get; set; } = 60;

    /// <summary>Court resource ids to include. Defaults to T1-T6 (resid 1-6).</summary>
    public int[] CourtIds { get; set; } = [1, 2, 3, 4, 5, 6];

    /// <summary>Time zone used to decide "today" and to interpret the site's times.</summary>
    public string TimeZone { get; set; } = "Europe/Helsinki";

    /// <summary>Pause between page requests, to be polite to the site.</summary>
    public int RequestDelayMs { get; set; } = 750;

    public NtfyOptions Ntfy { get; set; } = new();
}

public sealed class NtfyOptions
{
    public string Server { get; set; } = "https://ntfy.sh";

    /// <summary>Topic name. Treat it as a secret: anyone who knows it can read the alerts.</summary>
    public string Topic { get; set; } = "";

    /// <summary>Optional bearer token for protected topics / self-hosted servers.</summary>
    public string? Token { get; set; }
}
