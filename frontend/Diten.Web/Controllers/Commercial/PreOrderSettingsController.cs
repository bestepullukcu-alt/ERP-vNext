using System.Text.Json;
using Diten.Web.Controllers.CRM;
using Diten.Web.Models.Commercial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Commercial;

/// <summary>
/// WP-VW-W6 (W6-WEB-a) — <c>/Commercial/PreOrderSettings</c> (manifest page <c>PREORDERSETTINGS</c>): the tenant's pre-order
/// settings (MOD-0168 §4.3) on one Golden form — the approval mode (D1), the approval link's lifetime (12–168 h, D3), the
/// price tier per country (D11), the automatic transmission retries (0–10) and the ORD number prefix of each selling company
/// (a prefix that numbered an order is locked — the CRM answers 409 <c>pre_order_prefix_locked</c>). Not in the mockup: the
/// Golden standard form (W6-WEB sırası). Read with <c>.read</c>, saved with <c>.update</c> — checked HERE before the CRM.
/// </summary>
[Authorize]
[Route("Commercial/PreOrderSettings")]
public sealed class PreOrderSettingsController : CrmGatewayProxy
{
    public const string SettingsRead = "commercial.pre-order-settings.read";
    public const string SettingsUpdate = "commercial.pre-order-settings.update";

    private const string Settings = "/api/commercial/pre-order-settings";

    public PreOrderSettingsController(HttpClient httpClient, IConfiguration configuration, ILogger<PreOrderSettingsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(SettingsRead, SettingsUpdate);

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Commercial/PreOrderSettings/Index.cshtml", new PreOrderSettingsViewModel
    {
        CanRead = MayRead,
        CanUpdate = HasAny(SettingsUpdate)
    });

    [HttpGet("api/settings")]
    public Task<IActionResult> Get(CancellationToken ct) => ProxyAsync(HttpMethod.Get, Settings, null, MayRead, ct);

    /// <summary>The save (<c>.update</c>, checked here: the form is read only without it and the endpoint does not trust that);
    /// the CRM answers 409 when the version moved or a used prefix was changed, 400 for a value out of range.</summary>
    [HttpPut("api/settings")]
    public async Task<IActionResult> Put(CancellationToken ct)
        => HasAny(SettingsUpdate) ? await ProxyAsync(HttpMethod.Put, Settings, await ReadBodyAsync(ct) ?? "{}", true, ct) : Denied();

    /// <summary>The selling companies for the prefix rows: MDM's referenceable legal entities by name + country (the price
    /// tier rows are per country). An MDM that does not answer = <c>available: false</c> (the page then shows the ids it has).</summary>
    [HttpGet("api/legal-entities")]
    public async Task<IActionResult> LegalEntities(CancellationToken ct)
    {
        if (!MayRead) return Denied();
        using var response = await SendGatewayAsync(HttpMethod.Get, "/api/legal-entities/lookup", null, ct);
        var items = response is { IsSuccessStatusCode: true } ? CompaniesOf(await response.Content.ReadAsStringAsync(ct)) : null;
        return Ok(new { data = new { available = items is not null, items = (IEnumerable<Company>?)items ?? [] } });
    }

    /// <summary>One selling company: its id, name and country.</summary>
    public sealed record Company(string Id, string Name, string? CountryCode);

    /// <summary>Static, so MVC never treats it as an action: MDM's lookup → the referenceable companies (display name, else
    /// legal name, else code; the country upper case); null when the body is not the lookup.</summary>
    public static List<Company>? CompaniesOf(string body)
    {
        var options = VisitEvidenceSettingsController.LegalEntityOptionsOf(body);
        if (options is null) return null;
        var countries = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            foreach (var item in data.EnumerateArray())
            {
                var id = Str(item, "legalEntityId") ?? Str(item, "id");
                if (Guid.TryParse(id, out var g)) countries[g.ToString("D")] = Str(item, "countryCode")?.Trim().ToUpperInvariant();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return options.Select(o => new Company(o.Ref, o.Name, countries.GetValueOrDefault(o.Ref))).ToList();
    }
}
