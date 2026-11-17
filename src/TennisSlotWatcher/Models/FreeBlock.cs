namespace TennisSlotWatcher.Models;

/// <summary>
/// A contiguous free block on a court, exactly as published by the booking calendar
/// (one "Varaa" link: start time, maximum duration, court).
/// </summary>
/// <param name="Start">Local (Helsinki) start time.</param>
/// <param name="DurationMinutes">Free minutes starting at <paramref name="Start"/>.</param>
/// <param name="CourtId">The site's resource id (resid).</param>
/// <param name="CourtName">Label shown on the site, e.g. "T4".</param>
/// <param name="BookingPath">Site-relative link to the create-booking page.</param>
internal sealed record FreeBlock(
    DateTime Start,
    int DurationMinutes,
    int CourtId,
    string CourtName,
    string BookingPath)
{
    public DateTime End => Start.AddMinutes(DurationMinutes);

    /// <summary>Stable identity used for de-duplicating alerts. Starts with the date (yyyy-MM-dd).</summary>
    public string Key => $"{Start:yyyy-MM-dd'T'HH:mm}|{DurationMinutes}|{CourtId}";
}

/// <summary>A free block that satisfies the user's filters, plus the part of it inside the wanted window.</summary>
internal sealed record MatchedSlot(FreeBlock Block, DateTime BookableStart, DateTime BookableEnd)
{
    public string Key => Block.Key;
    public int BookableMinutes => (int)(BookableEnd - BookableStart).TotalMinutes;
}
