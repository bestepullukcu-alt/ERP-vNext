using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ItemMaster.Activation;

/// <summary>
/// MOD-0290-FU04 S2 — the activation gate (R-04, contract draft.11 x-note "Etkinleştirme kuralı"; O-25; S1 carry-over a /
/// R2). Run at the submit (the request is refused) AND when the approval is applied (the decision is not applied: the
/// item goes back with the reason). On an Active item the stock fields that apply at its level are never unknown:
/// <list type="bullet">
/// <item><c>lotControlled</c> filled (null refused);</item>
/// <item><c>serialControlled</c> — always false for an item (does not apply; the item has no such field to be wrong);</item>
/// <item>the base unit filled;</item>
/// <item>at least one storage condition, unless the item is packaging (O-25);</item>
/// <item>the unit conversions pass the record-level chain rules (<see cref="StockBehaviourRules.ValidateConversions"/>:
/// positive exact decimals, no pair twice, every unit within 4 edges of the base unit). The units were each checked
/// against <c>item-uom</c> when they were written; the gate checks their structure, not the list again.</item>
/// </list>
/// </summary>
public static class ItemActivationGate
{
    public const string StockFieldsRequired = "ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION";

    // The names the refusal carries (the contract's field names), in this order.
    public const string LotControlledField = "lotControlled";
    public const string BaseUomField = "baseUomCode";
    public const string StorageConditionsField = "storageConditionCodes";
    public const string UomConversionsField = "uomConversions";

    public static readonly IReadOnlyList<string> FieldNames =
        [LotControlledField, BaseUomField, StorageConditionsField, UomConversionsField];

    /// <summary>The fields that keep the item from activation, in <see cref="FieldNames"/> order; empty = it may activate.</summary>
    public static IReadOnlyList<string> MissingFields(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var missing = new List<string>();
        if (item.LotControlled is null) missing.Add(LotControlledField);
        if (string.IsNullOrWhiteSpace(item.BaseUomCode)) missing.Add(BaseUomField);
        if (!IsPackaging(item.MaterialType) && (item.StorageConditionCodes is null || item.StorageConditionCodes.Count == 0))
            missing.Add(StorageConditionsField);
        if (!string.IsNullOrWhiteSpace(item.BaseUomCode) && ChainFailure(item) is not null) missing.Add(UomConversionsField);
        return missing;
    }

    /// <summary>The conversion chain's own refusal code (null = valid).</summary>
    public static string? ChainFailure(Item item)
    {
        var conversions = item.UomConversions ?? [];
        var units = new HashSet<string>(StringComparer.Ordinal) { item.BaseUomCode };
        foreach (var conversion in conversions)
        {
            if (conversion is null) return StockBehaviourRules.ConversionInvalid;
            if (!string.IsNullOrWhiteSpace(conversion.FromUom)) units.Add(conversion.FromUom);
            if (!string.IsNullOrWhiteSpace(conversion.ToUom)) units.Add(conversion.ToUom);
        }
        return StockBehaviourRules.ValidateConversions(item.BaseUomCode, conversions, units);
    }

    private static bool IsPackaging(ItemMaterialType type) =>
        type is ItemMaterialType.PackagingPrimary or ItemMaterialType.PackagingSecondary;
}

/// <summary>
/// MOD-0290-FU04 S2 — "S2 kanıt karşılaştırma kuralı" (CT, from the IU review: item lists ITEM-UNIVERSAL-V1 → V2). When the
/// approval is applied, the item's codes are compared with the evidence FROZEN when they were written (the item is locked
/// from the submit on, so that is the submit's evidence) — never with a fresh LATEST's version:
/// <list type="bullet">
/// <item>same list, the code still in it, and (storage conditions) the same <c>ConditionKind</c> → held; a catalog version
/// change alone (1 → 2) is NEVER a refusal (the GSKU precedent compared versions — copied, a Platform deployment during an
/// approval, or any V1 item, would be refused);</item>
/// <item>a code the list no longer has → <c>ITEM_REFERENCE_VALUE_REMOVED</c>; a storage condition whose kind changed →
/// <c>ITEM_REFERENCE_VALUE_CHANGED</c> — the maker's to put right (the decision is not applied, the item goes back);</item>
/// <item>the provider cannot answer now (or refuses this service) → wait, the approver's decision is kept; an answer that
/// does not fit the request → stuck, for the requester's repeat / a person.</item>
/// </list>
/// A retired value is still a value of the list: retired after the item stored it, it stands (the GSKU rule
/// InfoRetiredAfterApproval) — it only cannot be chosen for new records.
/// </summary>
public static class ItemReferenceEvidence
{
    public const string ValueRemoved = "ITEM_REFERENCE_VALUE_REMOVED";
    public const string ValueChanged = "ITEM_REFERENCE_VALUE_CHANGED";
    public const string ProviderUnavailable = "ITEM_REFERENCE_LIST_UNAVAILABLE";
    public const string ContractConflict = "ITEM_REFERENCE_CONTRACT_CONFLICT";
    // S2 FIX1 item 1: the list provider refused MDM itself (its service identity, credential or configuration). Waiting
    // never ends it — stuck, counted by needing_a_person, the item stays pending (fail closed) until a person fixes it and
    // the requester's same submit reads again.
    public const string ProviderConfigurationError = "ITEM_REFERENCE_PROVIDER_CONFIGURATION_ERROR";

    public enum Verdict { Held, Wait, NotApplicable, Stuck }

    public sealed record Outcome(Verdict Verdict, string? Code)
    {
        public static readonly Outcome Held = new(Verdict.Held, null);
    }

    /// <summary>Every code the item holds, by list: material type, base unit and conversion units, storage conditions.</summary>
    public static IReadOnlyList<Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceRequest> Requests(Item item)
    {
        var requests = new List<Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceRequest>
        {
            new(Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.MaterialType, ItemRules.Code(item.MaterialType)),
            new(Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.ItemUom, item.BaseUomCode)
        };
        foreach (var unit in (item.UomConversions ?? []).SelectMany(c => new[] { c.FromUom, c.ToUom }))
            requests.Add(new(Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.ItemUom, unit));
        foreach (var code in item.StorageConditionCodes ?? [])
            requests.Add(new(Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.StorageCondition, code));
        return requests.Where(request => !string.IsNullOrWhiteSpace(request.ValueCode)).Distinct().ToList();
    }

    /// <summary>
    /// S2 FIX1 (minor) — at the submit: what the list answers now for every code the item holds, frozen for the approval
    /// (code + kind + catalog version). An S1 row that never stored a storage condition's kind gets it from this answer. A
    /// list that no longer holds a code, or that gives a storage condition another kind than the item stored, refuses the
    /// submit by name (the maker saves the item again); a list that cannot answer refuses it as an outage (fail closed).
    /// </summary>
    public static async Task<(Outcome? Refusal, List<ItemReferenceEvidenceEntry>? Evidence)> FreezeAsync(
        Diten.MdmService.Application.Contracts.ReferenceData.IItemReferenceResolver resolver, Item item,
        CancellationToken cancellationToken)
    {
        var requests = Requests(item);
        Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult? result;
        try { result = await resolver.ResolveLatestAsync(requests, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return (new(Verdict.Wait, ProviderUnavailable), null); }
        var verdict = Judge(item, requests, result);
        if (verdict.Verdict != Verdict.Held) return (verdict, null);
        return (null, requests.Select(request =>
        {
            var selection = result!.Selections.First(s => s is not null && s.SetCode == request.SetCode
                && s.ValueCode == request.ValueCode);
            return new ItemReferenceEvidenceEntry
            {
                SetCode = request.SetCode,
                ValueCode = request.ValueCode,
                Kind = request.SetCode == Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.StorageCondition
                    ? selection.Attributes?.GetValueOrDefault(
                        Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.ConditionKindAttribute)
                    : null,
                CatalogVersionNumber = selection.CatalogVersionNumber
            };
        }).ToList());
    }

    public static async Task<Outcome> CheckAsync(
        Diten.MdmService.Application.Contracts.ReferenceData.IItemReferenceResolver resolver, Item item,
        CancellationToken cancellationToken, IReadOnlyList<ItemReferenceEvidenceEntry>? frozen = null)
    {
        // The codes asked are the ones frozen at the submit (an operation reserved before FIX1 has none: the item's).
        var requests = frozen is { Count: > 0 }
            ? frozen.Select(entry => new Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceRequest(
                entry.SetCode, entry.ValueCode)).Distinct().ToList()
            : Requests(item);
        Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult? result;
        try { result = await resolver.ResolveLatestAsync(requests, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return new(Verdict.Wait, ProviderUnavailable); }
        return Judge(item, requests, result, frozen);
    }

    // The rule alone (tested): what the list answered, against the evidence frozen at the submit (or, without it, what the
    // item holds).
    public static Outcome Judge(Item item,
        IReadOnlyList<Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceRequest> requests,
        Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult? result,
        IReadOnlyList<ItemReferenceEvidenceEntry>? frozen = null)
    {
        if (result is null) return new(Verdict.Wait, ProviderUnavailable);
        if (!result.IsSuccessful)
        {
            if (result.FailureCode == Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult.ValueNotFound)
                return (result.NotFound ?? []).Count > 0 && (result.NotFound ?? []).All(missing => missing is not null
                           && requests.Contains(missing))
                    ? new(Verdict.NotApplicable, ValueRemoved)
                    : new(Verdict.Stuck, ContractConflict);
            if (result.IsConfigurationRefusal) return new(Verdict.Stuck, ProviderConfigurationError);
            return result.StatusCode >= 500
                ? new(Verdict.Wait, ProviderUnavailable)
                : new(Verdict.Stuck, ContractConflict);
        }
        foreach (var request in requests)
        {
            var selection = (result.Selections ?? []).FirstOrDefault(s => s is not null
                && string.Equals(s.SetCode, request.SetCode, StringComparison.Ordinal)
                && string.Equals(s.ValueCode, request.ValueCode, StringComparison.Ordinal));
            if (selection is null) return new(Verdict.Stuck, ContractConflict);
            if (request.SetCode == Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.StorageCondition
                && StoredKind(item, frozen, request.ValueCode) is { } stored
                && !string.Equals(stored, selection.Attributes?.GetValueOrDefault(
                    Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.ConditionKindAttribute),
                    StringComparison.Ordinal))
                return new(Verdict.NotApplicable, ValueChanged);
        }
        return Outcome.Held;
    }

    // The kind to hold the list to: the frozen one, else the item's own (an S1 row may have none).
    private static string? StoredKind(Item item, IReadOnlyList<ItemReferenceEvidenceEntry>? frozen, string code) =>
        frozen?.FirstOrDefault(entry => entry.SetCode
            == Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSets.StorageCondition
            && entry.ValueCode == code)?.Kind
        ?? (item.StorageConditionKinds is { } kinds && kinds.TryGetValue(code, out var stored) ? stored : null);
}

