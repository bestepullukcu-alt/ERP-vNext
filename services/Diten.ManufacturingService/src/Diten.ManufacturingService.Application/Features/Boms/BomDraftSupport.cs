using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Interfaces;
using Diten.ManufacturingService.Domain.Entities;

namespace Diten.ManufacturingService.Application.Features.Boms;

/// <summary>Taslak yazan iki komutun (create / update) ortak adımları ve geçmiş kaydı üretimi.</summary>
internal static class BomDraftSupport
{
    public static List<BomComponentLine> ToLines(IEnumerable<BomComponentInput> components) =>
        components
            .OrderBy(c => c.Position)
            .Select(c => new BomComponentLine
            {
                ComponentItemId = c.ComponentItemId,
                Quantity = c.Quantity!,
                UomId = c.UomId!.Trim(),
                Position = c.Position,
                Alternates = c.Alternates is { Count: > 0 } ? c.Alternates.Select(a => a.ToString()).ToList() : null
            })
            .ToList();

    public static BomRouting? ToRouting(RoutingInput? routing, string? existingRoutingId) =>
        routing?.Steps is { Count: > 0 } steps
            ? new BomRouting
            {
                RoutingId = existingRoutingId ?? Guid.NewGuid().ToString(),
                Steps = steps.OrderBy(s => s.StepNo)
                    .Select(s => new RoutingStep { StepNo = s.StepNo, Operation = s.Operation!.Trim(), WorkCenter = string.IsNullOrWhiteSpace(s.WorkCenter) ? null : s.WorkCenter.Trim() })
                    .ToList()
            }
            : null;

    /// <summary>
    /// Ana ürün bileşen ya da alternatif olarak geçiyorsa <see cref="BomErrorCodes.SelfReference"/>; MOD-0290'da
    /// bilinmeyen kimlik varsa <see cref="BomErrorCodes.UnknownItem"/> (fail-closed). Sorun yoksa null.
    /// </summary>
    public static async Task<string?> CheckReferencesAsync(Guid itemId, IReadOnlyList<BomComponentLine> lines, IProductReferenceValidator products, CancellationToken ct)
    {
        var referenced = lines.Select(l => l.ComponentItemId)
            .Concat(lines.SelectMany(l => l.Alternates ?? []).Select(Guid.Parse))
            .ToHashSet();
        if (referenced.Contains(itemId))
        {
            return BomErrorCodes.SelfReference;
        }

        referenced.Add(itemId);
        var unknown = await products.GetUnknownItemIdsAsync(referenced, ct);
        return unknown.Count > 0 ? BomErrorCodes.UnknownItem : null;
    }

    public static BomHistoryEntry History(
        BomVersion bom,
        BomHistoryOperation operation,
        string? fromStatus,
        IEnumerable<string>? changedFields,
        ICurrentUserContext user,
        ICorrelationContext correlation,
        DateTimeOffset at) => new()
        {
            TenantId = bom.TenantId!.Value,
            LegalEntityId = bom.LegalEntityId!.Value,
            BomVersionId = bom.Id,
            ItemId = bom.ItemId,
            RevisionNo = bom.RevisionNo,
            Operation = operation,
            FromStatus = fromStatus,
            ToStatus = operation == BomHistoryOperation.Deleted ? null : bom.Status.ToString(),
            ChangedFields = changedFields?.ToList() ?? [],
            ChangeControlRef = bom.ChangeControlRef,
            ActorId = user.UserId,
            ActorDisplayName = user.UserName,
            CorrelationId = correlation.CorrelationId,
            OccurredAtUtc = at,
            Outcome = "Succeeded"
        };
}
