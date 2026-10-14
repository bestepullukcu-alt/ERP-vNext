using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Diten.Web.Models.Inventory;

/// <summary>
/// The company (legal entity) the inventory screens work in — pack §19 waiver W-0173-02. AuthService issues no
/// <c>legal_entity_id</c> claim today, so a claim-less user picks a company; the choice is kept in a same-origin cookie
/// (the legal entity id only — no PII) whose NAME is derived from the user, so a second user on the same browser does
/// not inherit it. A token that DOES carry the claim always wins: no selector, no header (FU-LE-01 retires all this).
/// One isolated component: this class, <c>Views/Inventory/Shared/_CompanySelector.cshtml</c> and
/// <c>js/Inventory/company-selector.js</c>.
/// </summary>
public static class CompanySelection
{
    public const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string CookiePrefix = "diten-inv-company-";

    /// <summary>The legal entity the signed token names, if any (the Web principal keeps the raw claim type).</summary>
    public static Guid? TokenLegalEntity(ClaimsPrincipal user)
    {
        var values = user.Claims
            .Where(c => c.Type == "legal_entity_id" || c.Type.EndsWith("/legal_entity_id", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value)
            .ToList();
        return values.Count == 1 && Guid.TryParse(values[0], out var id) && id != Guid.Empty ? id : null;
    }

    /// <summary>Per-user cookie name: prefix + the first 16 hex of SHA-256(sub). Null when the principal has no subject.</summary>
    public static string? CookieName(ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject)))[..16].ToLowerInvariant();
        return CookiePrefix + hash;
    }

    /// <summary>The company in the user's cookie — a UUID or nothing. MOD-0173 still verifies it against MDM.</summary>
    public static Guid? SelectedCompany(HttpRequest request, ClaimsPrincipal user)
    {
        var name = CookieName(user);
        return name is not null && request.Cookies.TryGetValue(name, out var raw) && Guid.TryParse(raw, out var id) && id != Guid.Empty
            ? id
            : null;
    }

    /// <summary>
    /// Builds the selector's state for one page render: the token's company (selector hidden), or the tenant's active
    /// companies from MDM <c>GET /api/legal-entities/lookup</c> through the Gateway (server-side; the browser never calls
    /// the Gateway) with the cookie's choice when it is still one of them.
    /// </summary>
    public static async Task<CompanySelectorViewModel> ResolveAsync(
        HttpClient http, string gatewayUrl, HttpRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (TokenLegalEntity(user) is not null)
            return new CompanySelectorViewModel { State = CompanySelectorState.FromToken };

        var cookieName = CookieName(user);
        var gateway = InventoryProxy.Gateway(http, gatewayUrl, request, user);
        if (gateway is null || cookieName is null)
            return new CompanySelectorViewModel { State = CompanySelectorState.Unavailable };

        var reply = await gateway.GetAsync("/api/legal-entities/lookup", cancellationToken);
        var companies = reply.IsSuccess ? ParseCompanies(reply.Body) : null;
        if (companies is null)
            return new CompanySelectorViewModel { State = CompanySelectorState.Unavailable, CookieName = cookieName };

        var selected = SelectedCompany(request, user);
        var valid = selected is not null && companies.Any(c => c.Id == selected.Value.ToString("D"));
        return new CompanySelectorViewModel
        {
            State = valid ? CompanySelectorState.Selected : CompanySelectorState.NotSelected,
            Companies = companies,
            SelectedId = valid ? selected!.Value.ToString("D") : null,
            CookieName = cookieName
        };
    }

    /// <summary>MDM answers <c>Response&lt;IReadOnlyList&lt;LegalEntityLookupDto&gt;&gt;</c>; only referenceable (active) entities are offered.</summary>
    public static List<CompanyOption>? ParseCompanies(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Array) return null;

            var options = new List<CompanyOption>();
            foreach (var item in data.EnumerateArray())
            {
                var id = Text(item, "legalEntityId");
                if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty) continue;
                if (item.TryGetProperty("referenceable", out var r) && r.ValueKind == JsonValueKind.False) continue;
                var code = Text(item, "code");
                var name = Text(item, "displayName") ?? Text(item, "legalName");
                var label = string.IsNullOrWhiteSpace(code) ? name : string.IsNullOrWhiteSpace(name) ? code : $"{code} · {name}";
                options.Add(new CompanyOption(parsed.ToString("D"), string.IsNullOrWhiteSpace(label) ? parsed.ToString("D") : label));
            }

            return options;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public enum CompanySelectorState
{
    /// <summary>The token names the company: no selector, the screens load.</summary>
    FromToken,

    /// <summary>A company of the tenant is selected: the screens load with it.</summary>
    Selected,

    /// <summary>Companies are listed, none chosen yet: "select a company", no data call.</summary>
    NotSelected,

    /// <summary>The company list could not be read (no MDM permission, MDM down): no data call.</summary>
    Unavailable
}

public sealed record CompanyOption(string Id, string Name);

/// <summary>What <c>_CompanySelector.cshtml</c> draws; the page hands in its own localized words.</summary>
public sealed class CompanySelectorViewModel
{
    public CompanySelectorState State { get; init; }
    public IReadOnlyList<CompanyOption> Companies { get; init; } = [];
    public string? SelectedId { get; init; }
    public string? CookieName { get; init; }
    public bool Ready => State is CompanySelectorState.FromToken or CompanySelectorState.Selected;

    public string Label { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string SelectHint { get; set; } = string.Empty;
    public string UnavailableText { get; set; } = string.Empty;
}
