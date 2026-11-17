using Microsoft.Extensions.Options;

namespace TennisSlotWatcher.Services;

internal sealed class PlayFiClient(HttpClient http, IOptions<WatcherOptions> options)
{
    private readonly WatcherOptions _options = options.Value;

    public Task<string> GetCalendarHtmlAsync(DateOnly date, CancellationToken ct) =>
        http.GetStringAsync(BuildCalendarUrl(date), ct);

    public string BuildCalendarUrl(DateOnly date)
    {
        // p_pvm_interval is deliberately omitted: a non-empty interval shifts the date by that many days.
        var query = string.Join('&',
            Pair("BookingCalForm[p_laji]", _options.SportId.ToString()),
            Pair("BookingCalForm[p_location]", _options.LocationId.ToString()),
            Pair("BookingCalForm[p_pvm]", date.ToString("yyyy-MM-dd")),
            Pair("BookingCalForm[p_calmode]", "2"));

        return $"{_options.BaseUrl.TrimEnd('/')}/booking/booking-calendar?{query}";

        static string Pair(string k, string v) => $"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}";
    }
}
