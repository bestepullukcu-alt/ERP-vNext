using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — the O-8 change rule, field by field, on the record as read:
/// <list type="bullet">
/// <item>not sent → left as it is;</item>
/// <item>sent and equal to the stored value → nothing to write;</item>
/// <item>stored value empty (missing on an old document, or null) → written; on a non-Draft record it is a FIRST FILL —
/// no approval, audited, and written only while the store still has it empty (R2 / R3);</item>
/// <item>stored value different → written on a Draft; on any other record 409 <c>STOCK_BEHAVIOUR_FIELD_LOCKED</c>
/// (a change of a filled value is the critical-field correction, MOD-0023, a later slice) — except a list that only
/// GAINS items (S3 FIX1 M5, pack §4.4 rule 8): written, audited, every stored item kept as it was.</item>
/// </list>
/// </summary>
public sealed class StockBehaviourPlan
{
    private readonly bool _draft;

    public StockBehaviourPlan(ProductIdentityLifecycleStatus status) =>
        _draft = status == ProductIdentityLifecycleStatus.Draft;

    public Dictionary<string, object?> Sets { get; } = new(StringComparer.Ordinal);
    public HashSet<string> FirstFills { get; } = new(StringComparer.Ordinal);
    public string? LockedField { get; private set; }

    public void Field(string name, string? requested, string? current) =>
        Apply(name, requested, current, (a, b) => string.Equals((string)a, (string)b, StringComparison.Ordinal));

    public void Field(string name, int? requested, int? current) =>
        Apply(name, requested, current, (a, b) => (int)a == (int)b);

    public void Field(string name, bool? requested, bool? current) =>
        Apply(name, requested, current, (a, b) => (bool)a == (bool)b);

    // A set of codes: the order a screen sends them in is not a change.
    // S3 FIX1 M5 (CT decision, pack §4.4 rule 8): on a non-Draft record a code ADDED to the stored ones is free (audited,
    // like a first fill); one removed is a change of the filled value — locked.
    public void Field(string name, IReadOnlyList<string>? requested, IReadOnlyList<string>? current) =>
        Apply(name, requested?.ToList(), current, (a, b) =>
            ((IEnumerable<string>)a).Order(StringComparer.Ordinal)
            .SequenceEqual(((IEnumerable<string>)b).Order(StringComparer.Ordinal), StringComparer.Ordinal),
            (a, b) => ((IEnumerable<string>)b).All(code => ((IEnumerable<string>)a).Contains(code, StringComparer.Ordinal)));

    // Conversions compare as entered text (MDM never rounds: "2" and "2.0" are two values), order-free.
    // S3 FIX1 M5: a conversion ADDED is free on a non-Draft record; one changed (another ratio for the same units) or
    // removed is locked.
    public void Field(string name, IReadOnlyList<ItemUomConversion>? requested, IReadOnlyList<ItemUomConversion>? current) =>
        Apply(name, requested?.Select(Copy).ToList(), current, (a, b) =>
            Keys((IEnumerable<ItemUomConversion>)a).SequenceEqual(Keys((IEnumerable<ItemUomConversion>)b)),
            (a, b) => Keys((IEnumerable<ItemUomConversion>)b).All(Keys((IEnumerable<ItemUomConversion>)a).Contains));

    private void Apply(string name, object? requested, object? current, Func<object, object, bool> equal,
        Func<object, object, bool>? keepsEveryStored = null)
    {
        if (requested is null) return;
        if (current is null)
        {
            Sets[name] = requested;
            if (!_draft) FirstFills.Add(name);
            return;
        }
        if (equal(requested, current)) return;
        if (_draft)
        {
            Sets[name] = requested;
            return;
        }
        if (keepsEveryStored is not null && keepsEveryStored(requested, current))
        {
            // Every stored item is still there, unchanged, and new ones are added: not a change of a filled value.
            // The version CAS covers it (no first-fill condition: the field is not empty).
            Sets[name] = requested;
            return;
        }
        LockedField ??= name;
    }

    /// <summary>The value the record has after this plan (sent and written, else stored).</summary>
    public T? After<T>(string name, T? current) => Sets.TryGetValue(name, out var value) ? (T?)value : current;

    private static ItemUomConversion Copy(ItemUomConversion source) => new()
    {
        FromUom = source.FromUom, ToUom = source.ToUom, Numerator = source.Numerator, Denominator = source.Denominator
    };

    private static IEnumerable<string> Keys(IEnumerable<ItemUomConversion> conversions) =>
        conversions.Select(c => $"{c.FromUom}\u001f{c.ToUom}\u001f{c.Numerator}\u001f{c.Denominator}")
            .Order(StringComparer.Ordinal);

    /// <summary>The command's fingerprint: the same operation id with other content is a reused key, not a replay.</summary>
    public static string Fingerprint(string kind, Guid recordId, int expectedVersion, object values)
    {
        var text = $"{kind}|{recordId:D}|{expectedVersion}|{JsonSerializer.Serialize(values)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    public static (int Status, string Code) StoreFailure(string? code) => code switch
    {
        StockBehaviourStoreCodes.NotFound => (404, code),
        StockBehaviourStoreCodes.ContractInvalid => (400, code),
        null => (409, StockBehaviourStoreCodes.ConcurrencyConflict),
        _ => (409, code)
    };
}

/// <summary>
/// The stock fields' codes are MOD-0048 <c>item-uom</c> / <c>storage-condition</c> values (S1's resolver; fail-closed
/// until Platform publishes the lists — a named 503, never a hard-coded list). A code the record (or, for a Finished
/// Good, its GSKU) already holds stays valid when the list retires it; a new one must be selectable.
/// </summary>
public sealed record StockReferenceAnswer(
    IReadOnlySet<string> Uoms,
    IReadOnlySet<string> StorageConditions,
    IReadOnlySet<string> Temperatures,
    IReadOnlyDictionary<(string Set, string Value), ReferenceCatalogSelection> Proofs);

public static class StockReferenceCheck
{
    public static async Task<(StockReferenceAnswer? Answer, int Status, string? Code)> ResolveAsync(
        IItemReferenceResolver resolver,
        IReadOnlyCollection<string> uoms,
        IReadOnlyCollection<string> storageConditions,
        IReadOnlyCollection<ReferenceCatalogSelection> held,
        string? packUomCode,
        CancellationToken cancellationToken)
    {
        // Unlike the item master's check (ItemReferenceCheck keeps a held code's proof and never asks again), every code
        // here — held ones too — is resolved again on each write (WP-PLT-ITEM-LISTS-02 FIX1, see the re-stamp below).
        var requests = uoms.Distinct(StringComparer.Ordinal)
            .Select(code => new ItemReferenceRequest(ItemReferenceSets.ItemUom, code))
            .Concat(storageConditions.Distinct(StringComparer.Ordinal)
                .Select(code => new ItemReferenceRequest(ItemReferenceSets.StorageCondition, code)))
            .ToList();
        ItemReferenceResolveResult result;
        try
        {
            result = await resolver.ResolveLatestAsync(requests, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (null, 503, StockBehaviourErrors.ReferenceListUnavailable);
        }
        if (!result.IsSuccessful)
        {
            if (result.FailureCode != ItemReferenceResolveResult.ValueNotFound)
                return (null, 503, StockBehaviourErrors.ReferenceListUnavailable);
            // CT decision (a): the GSKU's pack unit must be in item-uom — named apart.
            if (packUomCode is not null && result.NotFound.Any(missing =>
                    missing.SetCode == ItemReferenceSets.ItemUom && missing.ValueCode == packUomCode))
                return (null, 422, StockBehaviourRules.PackUomUnknown);
            return (null, 422, result.NotFound.Any(missing => missing.SetCode == ItemReferenceSets.ItemUom)
                ? StockBehaviourRules.UomInvalid
                : StockBehaviourRules.StorageConditionInvalid);
        }

        var heldKeys = held.Select(selection => (selection.SetCode, selection.ValueCode)).ToHashSet();
        var proofs = held.GroupBy(selection => (selection.SetCode, selection.ValueCode))
            .ToDictionary(group => group.Key, group => group.First());
        var listedUoms = new HashSet<string>(StringComparer.Ordinal);
        var listedStorage = new HashSet<string>(StringComparer.Ordinal);
        var temperatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in requests)
        {
            var selection = result.Selections.FirstOrDefault(answer =>
                answer.SetCode == request.SetCode && answer.ValueCode == request.ValueCode);
            if (selection is null) return (null, 503, StockBehaviourErrors.ReferenceListUnavailable);
            var key = (request.SetCode, request.ValueCode);
            if (selection.IsRetired || !selection.SelectableForNew)
            {
                if (!heldKeys.Contains(key))
                    return (null, 422, request.SetCode == ItemReferenceSets.ItemUom
                        ? request.ValueCode == packUomCode ? StockBehaviourRules.PackUomUnknown : StockBehaviourRules.UomInvalid
                        : StockBehaviourRules.StorageConditionInvalid);
            }
            else if (ToSelection(selection) is { } proof)
            {
                // RE-STAMPS: a held code that is still selectable gets the CURRENT version's proof (an ITEM-UNIVERSAL-V1
                // item-uom proof becomes V2's); a retired held code keeps its old proof (branch above). Harmless by the
                // catalog's rule — a code is retired, never redefined — so the new proof vouches for the same meaning.
                proofs[key] = proof;
            }
            else
            {
                return (null, 503, StockBehaviourErrors.ReferenceListUnavailable);
            }
            (request.SetCode == ItemReferenceSets.ItemUom ? listedUoms : listedStorage).Add(request.ValueCode);
            if (request.SetCode == ItemReferenceSets.StorageCondition
                && selection.Attributes.TryGetValue(ItemReferenceSets.ConditionKindAttribute, out var kind)
                && string.Equals(kind, ItemReferenceSets.TemperatureKind, StringComparison.Ordinal))
                temperatures.Add(request.ValueCode);
        }
        return (new StockReferenceAnswer(listedUoms, listedStorage, temperatures, proofs), 200, null);
    }

    private static ReferenceCatalogSelection? ToSelection(ItemReferenceSelection source) =>
        source.CatalogVersionId == Guid.Empty || source.CatalogVersionNumber <= 0
        || source.ResolutionMode != "LATEST" || source.ResolvedAtUtc == default
            ? null
            : new ReferenceCatalogSelection
            {
                SetCode = source.SetCode,
                ValueCode = source.ValueCode,
                CatalogVersionId = source.CatalogVersionId,
                CatalogVersionNumber = source.CatalogVersionNumber,
                ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = source.ResolvedAtUtc
            };

    /// <summary>The proofs of exactly the codes a record's stock fields hold after the edit.</summary>
    public static List<ReferenceCatalogSelection> ProofsFor(
        StockReferenceAnswer answer, IEnumerable<string> uoms, IEnumerable<string> storageConditions) =>
        uoms.Distinct(StringComparer.Ordinal).Select(code => (ItemReferenceSets.ItemUom, code))
            .Concat(storageConditions.Distinct(StringComparer.Ordinal).Select(code => (ItemReferenceSets.StorageCondition, code)))
            .Where(answer.Proofs.ContainsKey)
            .Select(key => answer.Proofs[key])
            .ToList();
}
