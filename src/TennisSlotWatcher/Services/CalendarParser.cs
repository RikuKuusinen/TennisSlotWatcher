using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using TennisSlotWatcher.Models;

namespace TennisSlotWatcher.Services;

internal sealed record ParsedCalendar(DateOnly? PageDate, IReadOnlyList<FreeBlock> Blocks);

/// <summary>
/// Extracts free blocks from the play.fi booking calendar HTML. Only the "Varaa" anchors are read;
/// the table's rowspan layout is intentionally ignored.
/// </summary>
internal static partial class CalendarParser
{
    [GeneratedRegex("""<a\s+[^>]*href="(?<href>[^"]*create-booking\?[^"]*)"[^>]*>(?<label>.*?)</a>""",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BookingAnchor();

    [GeneratedRegex(@"name=""BookingCalForm\[p_pvm\]""\s+value=""(?<d>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex PageDateInput();

    /// <exception cref="FormatException">The HTML does not look like a booking calendar.</exception>
    public static ParsedCalendar Parse(string html)
    {
        // Guard against layout changes / error pages: a real calendar always has this container,
        // even when every slot is booked.
        if (!html.Contains("booking-table", StringComparison.Ordinal))
            throw new FormatException("Response does not contain the booking table.");

        DateOnly? pageDate = null;
        var dateMatch = PageDateInput().Match(html);
        if (dateMatch.Success && DateOnly.TryParseExact(dateMatch.Groups["d"].Value, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            pageDate = d;

        var blocks = new List<FreeBlock>();
        foreach (Match m in BookingAnchor().Matches(html))
        {
            var href = WebUtility.HtmlDecode(m.Groups["href"].Value);
            var query = HttpUtility.ParseQueryString(href[(href.IndexOf('?') + 1)..]);

            if (!DateTime.TryParseExact(query["alkuaika"], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var start)
                || !int.TryParse(query["kesto"], out var minutes)
                || !int.TryParse(query["resid"], out var courtId))
                continue;

            var label = m.Groups["label"].Value;
            var br = label.IndexOf("<br", StringComparison.OrdinalIgnoreCase);
            var name = WebUtility.HtmlDecode(br >= 0 ? label[..br] : label).Trim();

            blocks.Add(new FreeBlock(start, minutes, courtId, name, href));
        }

        return new ParsedCalendar(pageDate, blocks);
    }
}
