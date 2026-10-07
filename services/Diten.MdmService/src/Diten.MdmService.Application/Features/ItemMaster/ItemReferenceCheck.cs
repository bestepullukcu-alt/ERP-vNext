using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ItemMaster;

// MOD-0290-FU04 S1 (§4.3, §12): every code the item stores is an active value of its MOD-0048 list, with the selection
// proof kept on the row. A code the row already holds keeps its proof (an edit of the name does not re-resolve the
// base unit); only new codes are resolved. O-2: at most one TEMPERATURE storage condition, read from the list's
// ConditionKind attribute, never a hard-coded list.
public sealed record ItemReferenceOutcome(ItemReferenceProof? Proof, int StatusCode, string? ErrorCode, string? Detail)
{
    public static ItemReferenceOutcome Ok(ItemReferenceProof proof) => new(proof, 200, null, null);
    public static ItemReferenceOutcome Fail(int statusCode, string errorCode, string? detail = null) =>
        new(null, statusCode, errorCode, detail);
}

public static class ItemReferenceCheck
{
    public static async Task<ItemReferenceOutcome> ResolveAsync(
        IItemReferenceResolver resolver,
        ItemFieldValues values,
        Item? current,
        CancellationToken cancellationToken)
    {
        if (values.StorageConditionCodes.Distinct(StringComparer.Ordinal).Count() != values.StorageConditionCodes.Count)
            return ItemReferenceOutcome.Fail(422, ItemErrors.StorageConditionConflict, "REPEATED");

        var known = new Dictionary<(string Set, string Value), ReferenceCatalogSelection>();
        if (current is not null)
        {
            Keep(known, current.MaterialTypeSelection);
            Keep(known, current.BaseUomSelection);
            foreach (var selection in current.StorageConditionSelections) Keep(known, selection);
        }

        var wanted = new List<ItemReferenceRequest> { new(ItemReferenceSets.MaterialType, ItemRules.Code(values.MaterialType)) };
        wanted.AddRange(ItemRules.UomCodes(values).Select(code => new ItemReferenceRequest(ItemReferenceSets.ItemUom, code)));
        wanted.AddRange(values.StorageConditionCodes.Select(code =>
            new ItemReferenceRequest(ItemReferenceSets.StorageCondition, code)));
        // The temperature rule needs ConditionKind for every storage code, kept or new: storage codes are always asked.
        var ask = wanted.Where(request => request.SetCode == ItemReferenceSets.StorageCondition
                                          || !known.ContainsKey((request.SetCode, request.ValueCode)))
            .Distinct()
            .ToList();

        var resolved = new Dictionary<(string Set, string Value), ItemReferenceSelection>();
        if (ask.Count > 0)
        {
            ItemReferenceResolveResult result;
            try
            {
                result = await resolver.ResolveLatestAsync(ask, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return ItemReferenceOutcome.Fail(503, ItemErrors.ReferenceListUnavailable);
            }

            if (!result.IsSuccessful)
            {
                return result.FailureCode == ItemReferenceResolveResult.ValueNotFound
                    ? ItemReferenceOutcome.Fail(422, ItemErrors.ReferenceValueInvalid,
                        string.Join(",", result.NotFound.Select(missing => $"{missing.SetCode}:{missing.ValueCode}")))
                    : ItemReferenceOutcome.Fail(503, ItemErrors.ReferenceListUnavailable, result.FailureCode);
            }

            foreach (var selection in result.Selections)
                resolved[(selection.SetCode, selection.ValueCode)] = selection;
            foreach (var request in ask)
            {
                if (!resolved.TryGetValue((request.SetCode, request.ValueCode), out var selection))
                    return ItemReferenceOutcome.Fail(503, ItemErrors.ReferenceListUnavailable, "ANSWER_INCOMPLETE");
                if (selection.IsRetired || !selection.SelectableForNew)
                {
                    // A value the record already holds stays valid (FU01: retirement never strips stored evidence).
                    if (known.ContainsKey((request.SetCode, request.ValueCode))) continue;
                    return ItemReferenceOutcome.Fail(422, ItemErrors.ReferenceValueInvalid,
                        $"{request.SetCode}:{request.ValueCode}:NOT_SELECTABLE");
                }
                if (ItemRules.ToSelection(selection) is not { } proof)
                    return ItemReferenceOutcome.Fail(503, ItemErrors.ReferenceListUnavailable, "UNTRUSTED_ANSWER");
                known.TryAdd((request.SetCode, request.ValueCode), proof);
            }
        }

        var temperatures = values.StorageConditionCodes.Count(code =>
            resolved.TryGetValue((ItemReferenceSets.StorageCondition, code), out var selection)
            && selection.Attributes.TryGetValue(ItemReferenceSets.ConditionKindAttribute, out var kind)
            && string.Equals(kind, ItemReferenceSets.TemperatureKind, StringComparison.Ordinal));
        if (temperatures > 1)
            return ItemReferenceOutcome.Fail(422, ItemErrors.StorageConditionConflict, "MORE_THAN_ONE_TEMPERATURE");

        return ItemReferenceOutcome.Ok(new ItemReferenceProof(
            known[(ItemReferenceSets.MaterialType, ItemRules.Code(values.MaterialType))],
            known[(ItemReferenceSets.ItemUom, values.BaseUomCode)],
            values.StorageConditionCodes.Select(code => known[(ItemReferenceSets.StorageCondition, code)]).ToList()));
    }

    private static void Keep(Dictionary<(string Set, string Value), ReferenceCatalogSelection> known,
        ReferenceCatalogSelection? selection)
    {
        if (selection is not null) known.TryAdd((selection.SetCode, selection.ValueCode), selection);
    }
}
