using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.Platform.Application.Features.WorkingCalendarImport;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.WorkingCalendarImport;

public sealed class NagerDateHolidayProvider : IHolidayProvider
{
    private readonly HttpClient _httpClient;
    private readonly WorkingCalendarImportOptions _options;
    public string ProviderKey => "nager-date";

    public NagerDateHolidayProvider(HttpClient httpClient, IOptions<WorkingCalendarImportOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<HolidayFetchResult> FetchAsync(string countryCode, int year, CancellationToken ct = default)
    {
        var endpoint = $"api/v3/PublicHolidays/{year}/{Uri.EscapeDataString(countryCode.ToUpperInvariant())}";
        using var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(ct);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var rows = JsonSerializer.Deserialize<List<NagerHolidayRow>>(payload, JsonOptions) ?? new();
        if (rows.Count > _options.MaxResponseItems) throw new InvalidOperationException("Holiday provider response exceeded MaxResponseItems.");
        var holidays = rows.Select(x => new ProviderHoliday(
            x.Date, x.Name ?? x.LocalName ?? string.Empty, x.LocalName, x.Types ?? new List<string>(),
            x.Global, x.Counties,
            $"{countryCode.ToUpperInvariant()}:{x.Date:yyyy-MM-dd}:{(x.Name ?? x.LocalName ?? string.Empty).Trim().ToUpperInvariant()}")).ToList();
        return new HolidayFetchResult(HolidayProviderOutcome.Succeeded, holidays, ProviderKey, endpoint,
            DateTimeOffset.UtcNow, hash);
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed class NagerHolidayRow
    {
        public DateOnly Date { get; set; }
        public string? LocalName { get; set; }
        public string? Name { get; set; }
        public bool Global { get; set; }
        public List<string>? Counties { get; set; }
        public List<string>? Types { get; set; }
    }
}

public sealed class OfflineHolidayProvider : IHolidayProvider
{
    public string ProviderKey => "offline-stub";

    public Task<HolidayFetchResult> FetchAsync(string countryCode, int year, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var row = new ProviderHoliday(new DateOnly(year, 1, 1), "New Year's Day", "New Year's Day",
            new[] { "Public" }, true, null, $"offline:{countryCode.ToUpperInvariant()}:{year}:01-01");
        var payload = $"{countryCode.ToUpperInvariant()}|{year}|01-01|Public";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return Task.FromResult(new HolidayFetchResult(HolidayProviderOutcome.Succeeded, new[] { row }, ProviderKey,
            "offline://built-in", DateTimeOffset.UtcNow, hash));
    }
}
