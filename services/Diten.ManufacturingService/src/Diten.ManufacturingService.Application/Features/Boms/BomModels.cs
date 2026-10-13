using Diten.ManufacturingService.Domain.Entities;

namespace Diten.ManufacturingService.Application.Features.Boms;

// ── Wire shapes: bom.openapi.yaml. v1.0.0 (FROZEN) shapes are BomView / BomComponentView / RoutingView /
// RoutingStepView / ExplodeResponse; everything else is the additive v1.1.0 surface. TenantId / LegalEntityId never
// appear in a request: they are server-resolved (TenantResolutionMiddleware).

public sealed record BomComponentInput(
    Guid ComponentItemId,
    string? Quantity,
    string? UomId,
    int Position,
    List<Guid>? Alternates);

public sealed record RoutingStepInput(int StepNo, string? Operation, string? WorkCenter);

public sealed record RoutingInput(List<RoutingStepInput>? Steps);

/// <summary>Taslak içeriği — create ve update gövdelerinin ortak kısmı (tek doğrulayıcı).</summary>
public interface IBomDraftContent
{
    string? Description { get; }
    List<BomComponentInput>? Components { get; }
    RoutingInput? Routing { get; }
}

/// <summary>POST /api/bom/versions — yeni taslak (v1.1.0 additive).</summary>
public sealed record CreateBomDraftRequest(
    Guid ItemId,
    string? Description,
    List<BomComponentInput>? Components,
    RoutingInput? Routing) : IBomDraftContent;

/// <summary>PUT /api/bom/version/{bomVersionId} — taslak düzenleme; <c>rowVersion</c> eşzamanlılık belirteci.</summary>
public sealed record UpdateBomDraftRequest(
    string? Description,
    List<BomComponentInput>? Components,
    RoutingInput? Routing,
    int RowVersion) : IBomDraftContent;

/// <summary>POST /api/bom/version/{bomVersionId}/release.</summary>
public sealed record ReleaseBomVersionRequest(string? ChangeControlRef, int RowVersion);

/// <summary>POST /api/bom/explode (v1.0.0 FROZEN).</summary>
public sealed record ExplodeBomRequest(Guid ItemId, string? Quantity, DateOnly? AsOfDate);

public sealed record BomComponentView(Guid ComponentItemId, string Quantity, string UomId, int Position, IReadOnlyList<string>? Alternates);

public sealed record RoutingStepView(int StepNo, string Operation, string? WorkCenter);

public sealed record RoutingView(string RoutingId, IReadOnlyList<RoutingStepView> Steps);

/// <summary>
/// Frozen <c>BomView</c>. v1.0.0 alanları birebir; <c>description</c>, <c>changeControlRef</c>, <c>rowVersion</c>
/// v1.1.0'da eklenen isteğe bağlı alanlardır (mevcut tüketiciler yok sayar).
/// </summary>
public sealed record BomView(
    string BomVersionId,
    Guid ItemId,
    int Version,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<BomComponentView> Components,
    RoutingView? Routing,
    string? Description,
    string? ChangeControlRef,
    int RowVersion,
    Guid LegalEntityId,
    string ContractVersion = BomContract.Version);

public sealed record BomListItem(
    string BomVersionId,
    Guid ItemId,
    int Version,
    string Status,
    string? Description,
    int ComponentCount,
    int StepCount,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset UpdatedAt,
    int RowVersion,
    Guid LegalEntityId);

public sealed record BomListResponse(IReadOnlyList<BomListItem> Items, long Total, long FilteredTotal);

public sealed record BomRequirementView(Guid ComponentItemId, string RequiredQuantity, string UomId);

public sealed record ExplodeBomResponse(
    Guid ItemId,
    string RequestedQuantity,
    IReadOnlyList<BomRequirementView> Requirements,
    string BomVersionId,
    string ContractVersion = BomContract.Version);

public sealed record BomHistoryView(
    string Operation,
    string? FromStatus,
    string? ToStatus,
    IReadOnlyList<string> ChangedFields,
    string? ChangeControlRef,
    Guid ActorId,
    string ActorDisplayName,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc,
    string Outcome);

public sealed record BomHistoryResponse(string BomVersionId, IReadOnlyList<BomHistoryView> Entries, string ContractVersion = BomContract.Version);

public static class BomContract
{
    public const string Version = "v1";
}

internal static class BomMapping
{
    public static BomView ToView(this BomVersion bom) => new(
        bom.Id.ToString(),
        bom.ItemId,
        bom.RevisionNo,
        bom.Status.ToString(),
        bom.EffectiveFrom,
        bom.EffectiveTo,
        bom.Components
            .OrderBy(c => c.Position)
            .Select(c => new BomComponentView(c.ComponentItemId, c.Quantity, c.UomId, c.Position, c.Alternates))
            .ToList(),
        bom.Routing is null
            ? null
            : new RoutingView(bom.Routing.RoutingId, bom.Routing.Steps.OrderBy(s => s.StepNo).Select(s => new RoutingStepView(s.StepNo, s.Operation, s.WorkCenter)).ToList()),
        bom.Description,
        bom.ChangeControlRef,
        bom.Version,
        bom.LegalEntityId ?? Guid.Empty);

    public static BomListItem ToListItem(this BomVersion bom) => new(
        bom.Id.ToString(),
        bom.ItemId,
        bom.RevisionNo,
        bom.Status.ToString(),
        bom.Description,
        bom.Components.Count,
        bom.Routing?.Steps.Count ?? 0,
        bom.EffectiveFrom,
        bom.EffectiveTo,
        bom.UpdatedAt ?? bom.CreatedAt,
        bom.Version,
        bom.LegalEntityId ?? Guid.Empty);

    public static BomHistoryView ToView(this BomHistoryEntry entry) => new(
        entry.Operation.ToString(),
        entry.FromStatus,
        entry.ToStatus,
        entry.ChangedFields,
        entry.ChangeControlRef,
        entry.ActorId,
        entry.ActorDisplayName,
        entry.CorrelationId,
        entry.OccurredAtUtc,
        entry.Outcome);
}
