using System.Globalization;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// MOD-0177-FU01 (pack §8.6–§8.8) — the ONE builder of a customer's display (<c>CustomerDisplay</c>, RECALL 1.10.0) and of the name
/// resolution behind it. A CRM key never becomes display text: it is shown as "{name} · {code}" or as a state (Unresolved / NotFound /
/// Forbidden, with the distinguisher; Deferred outside the requested customer page). A key without a CRM id keeps its label: Legacy (written
/// before FU01) or NoCrm (ShipTo / Document / Manual).
/// <list type="bullet">
/// <item><b>Customer page</b> (screen reads): the response's customer keys, ordinal, cut into slices of <see cref="PageSize"/>; only the CRM keys
/// of the requested slice are resolved (Page mode, one call).</item>
/// <item><b>Export</b> (evidence, trace CSV): every CRM key, Export mode (breaker).</item>
/// </list>
/// </summary>
public sealed class RecallCustomerDisplay
{
    public const int PageSize = 25;

    private readonly IRecallCustomerDirectory _directory;

    public RecallCustomerDisplay(IRecallCustomerDirectory directory) => _directory = directory;

    /// <summary>What one key is: its source (Crm wins when any item of the key is Crm) and whether a kept CRM key carries the warning.</summary>
    public sealed record KeyFacts(string Key, RecallCustomerRefSource? Source, bool NoCrmWarning)
    {
        public bool IsCrm => Source == RecallCustomerRefSource.Crm;
    }

    /// <summary>The resolved names by key; a CRM key absent from it was not asked (Deferred).</summary>
    public sealed record Resolution(IReadOnlyDictionary<string, RecallCustomerName> Names)
    {
        public static readonly Resolution None = new(new Dictionary<string, RecallCustomerName>(StringComparer.Ordinal));

        public bool HasForbidden => Names.Values.Any(n => n.Outcome == RecallCustomerOutcome.Forbidden);
    }

    /// <summary>The optional <c>customerPage</c> query parameter: absent → 1; an integer ≥ 1; anything else is refused (400).</summary>
    public static bool TryPage(string? raw, out int page)
    {
        page = 1;
        if (raw is null) return true;
        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1;
    }

    public const string PageError = "customerPage must be an integer of at least 1.";

    /// <summary>The facts of the keys of these scope items (Shipment items with a key), one entry per key.</summary>
    public static IReadOnlyDictionary<string, KeyFacts> Facts(IEnumerable<RecallScopeItem> items)
        => items.Where(i => i.Kind == RecallScopeItemKind.Shipment && i.CustomerRef is not null)
            .GroupBy(i => i.CustomerRef!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => new KeyFacts(g.Key,
                g.Any(i => i.CustomerRefSource == RecallCustomerRefSource.Crm) ? RecallCustomerRefSource.Crm : g.First().CustomerRefSource,
                g.Any(i => i.CustomerRefSource == RecallCustomerRefSource.Crm && i.NoCrmWarning)), StringComparer.Ordinal);

    /// <summary>The facts of a key not met in the items (it never is for a record written through the scope check): a key without a source.</summary>
    public static KeyFacts Of(IReadOnlyDictionary<string, KeyFacts> facts, string key)
        => facts.TryGetValue(key, out var f) ? f : new KeyFacts(key, null, false);

    /// <summary>The requested slice of the response's customer keys (all sources), in ordinal order.</summary>
    public static IReadOnlyList<string> Slice(IEnumerable<string> keys, int page)
        => keys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Skip((page - 1) * PageSize).Take(PageSize).ToList();

    /// <summary>Screen read: resolves the CRM keys of the requested customer page only (Page mode, ≤ 25 accounts, ≤ 3 s).</summary>
    public Task<Resolution> PageAsync(IEnumerable<KeyFacts> keys, int page, CancellationToken ct)
    {
        var all = keys.ToList();
        var slice = Slice(all.Select(k => k.Key), page).ToHashSet(StringComparer.Ordinal);
        return ResolveAsync(all.Where(k => k.IsCrm && slice.Contains(k.Key)), RecallLookupMode.Page, ct);
    }

    /// <summary>Export: every CRM key (Export mode — no total budget, breaker).</summary>
    public Task<Resolution> ExportAsync(IEnumerable<KeyFacts> keys, CancellationToken ct) => ResolveAsync(keys.Where(k => k.IsCrm), RecallLookupMode.Export, ct);

    /// <summary>One CRM key (a record command's resolution check, Page mode).</summary>
    public async Task<RecallCustomerName> OneAsync(string crmKey, CancellationToken ct)
        => (await ResolveAsync([new KeyFacts(crmKey, RecallCustomerRefSource.Crm, false)], RecallLookupMode.Page, ct)).Names.GetValueOrDefault(crmKey)
           ?? RecallCustomerName.Unresolved;

    private async Task<Resolution> ResolveAsync(IEnumerable<KeyFacts> crm, RecallLookupMode mode, CancellationToken ct)
    {
        var accounts = new Dictionary<Guid, string>();
        foreach (var key in crm.Select(k => k.Key).Distinct(StringComparer.Ordinal))
        {
            if (RecallCustomerKeyRules.TryCanonical(key, out var canonical)) accounts[Guid.ParseExact(canonical, "D")] = key;
        }

        if (accounts.Count == 0) return Resolution.None;
        var answers = await _directory.ResolveAsync(accounts.Keys.ToList(), mode, ct);
        return new Resolution(accounts.ToDictionary(a => a.Value, a => answers.GetValueOrDefault(a.Key) ?? RecallCustomerName.Unresolved, StringComparer.Ordinal));
    }

    /// <summary>
    /// The display of one key. <paramref name="label"/> is today's label (shown for a key without a CRM id); <paramref name="shipTo"/> /
    /// <paramref name="shipmentNumber"/> distinguish an unresolved / not found / forbidden CRM row (a "D" GUID is never shown).
    /// </summary>
    public static RecallCustomerDisplayDto Display(KeyFacts facts, Resolution resolution, string? label, string? shipTo = null, string? shipmentNumber = null)
    {
        switch (facts.Source)
        {
            case null:
                return new RecallCustomerDisplayDto("Unknown", "Legacy", Label: label ?? facts.Key);
            case RecallCustomerRefSource.ShipTo or RecallCustomerRefSource.Document or RecallCustomerRefSource.Manual:
                return new RecallCustomerDisplayDto(facts.Source.Value.ToString(), "NoCrm", Label: label ?? facts.Key);
        }

        var warning = facts.NoCrmWarning ? true : (bool?)null;
        if (!resolution.Names.TryGetValue(facts.Key, out var name)) return new RecallCustomerDisplayDto("Crm", "Deferred", NoCrmWarning: warning);
        if (name.Outcome == RecallCustomerOutcome.Resolved) return new RecallCustomerDisplayDto("Crm", "Resolved", name.Name, name.Code, NoCrmWarning: warning);
        return new RecallCustomerDisplayDto("Crm", name.Outcome.ToString(), ShipTo: Visible(shipTo), ShipmentNumber: Visible(shipmentNumber), NoCrmWarning: warning);
    }

    /// <summary>A distinguisher value is shown unless it is empty or a "D" GUID.</summary>
    public static string? Visible(string? value) => string.IsNullOrWhiteSpace(value) || RecallCustomerKeyRules.IsDShaped(value) ? null : value;
}
