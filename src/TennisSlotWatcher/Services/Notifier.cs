using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using TennisSlotWatcher.Models;

namespace TennisSlotWatcher.Services;

internal interface INotifier
{
    Task NotifyAsync(IReadOnlyList<MatchedSlot> newSlots, CancellationToken ct);
}

/// <summary>Sends one push notification per run through ntfy (https://ntfy.sh).</summary>
internal sealed class NtfyNotifier(HttpClient http, IOptions<WatcherOptions> options) : INotifier
{
    private readonly WatcherOptions _options = options.Value;

    public async Task NotifyAsync(IReadOnlyList<MatchedSlot> newSlots, CancellationToken ct)
    {
        if (newSlots.Count == 0) return;

        var ntfy = _options.Ntfy;
        if (string.IsNullOrWhiteSpace(ntfy.Topic))
            throw new InvalidOperationException("Watcher:Ntfy:Topic is not configured.");

        var origin = new Uri(_options.BaseUrl).GetLeftPart(UriPartial.Authority);
        var first = newSlots.OrderBy(s => s.BookableStart).ThenBy(s => s.Block.CourtId).First();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ntfy.Server.TrimEnd('/')}/{Uri.EscapeDataString(ntfy.Topic)}")
        {
            Content = new StringContent(MessageFormatter.BuildBody(newSlots), Encoding.UTF8, "text/plain")
        };
        request.Headers.Add("Title", MessageFormatter.BuildTitle(newSlots.Count));
        request.Headers.Add("Tags", "tennis");
        request.Headers.Add("Click", origin + first.Block.BookingPath);
        if (!string.IsNullOrEmpty(ntfy.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ntfy.Token);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}

internal static class MessageFormatter
{
    public static string BuildTitle(int count) =>
        count == 1 ? "New tennis slot at Laajasalo" : $"{count} new tennis slots at Laajasalo";

    // ntfy turns messages over 4096 bytes into attachments, so cap the list (~25 bytes per line).
    private const int MaxLines = 60;

    public static string BuildBody(IEnumerable<MatchedSlot> slots)
    {
        var ordered = slots.OrderBy(s => s.BookableStart).ThenBy(s => s.Block.CourtId).ToList();
        var shown = ordered.Take(MaxLines).ToList();

        var sb = new StringBuilder();
        foreach (var day in shown.GroupBy(s => s.Block.Start.Date))
        {
            sb.AppendLine(day.Key.ToString("ddd d.M.", CultureInfo.InvariantCulture));
            foreach (var s in day)
                sb.AppendLine($"  {s.BookableStart:HH\\:mm}-{s.BookableEnd:HH\\:mm}  {s.Block.CourtName}");
        }
        if (ordered.Count > shown.Count)
            sb.AppendLine($"...and {ordered.Count - shown.Count} more");
        sb.AppendLine().Append("Tap to open the earliest slot.");
        return sb.ToString();
    }
}
