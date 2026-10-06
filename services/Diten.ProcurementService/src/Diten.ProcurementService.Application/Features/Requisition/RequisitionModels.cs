using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Requisition;

// ── Contract version sabiti (REQUISITION-PO owned contract; requisition-po.openapi.yaml) ──
public static class RequisitionContract
{
    public const string Version = "v1";
}

// ══ READ DTOs (requisition-po.openapi.yaml şekilleriyle uyumlu) ═══════════════════

/// <summary>RequisitionLine read DTO — contract RequisitionLine {itemId, skuId?, quantity, uomId, needBy?}.</summary>
public sealed record RequisitionLineDto(
    string ItemId,
    string? SkuId,
    string Quantity,
    string UomId,
    string? NeedBy);

/// <summary>Requisition read DTO — contract Requisition. (Id/Version house-style zarf alanı; concurrency için.)</summary>
public sealed record RequisitionDto(
    Guid Id,
    string RequisitionId,
    RequisitionStatus Status,
    IReadOnlyList<RequisitionLineDto> Lines,
    string? Justification,
    string? WorkflowInstanceId,
    int Version,
    string ContractVersion);

/// <summary>Sayfalı liste sonucu — contract listRequisitions {items, nextCursor, contractVersion}.</summary>
public sealed record RequisitionListResultDto(
    IReadOnlyList<RequisitionDto> Items,
    string? NextCursor,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract RequisitionUpsert) ══

/// <summary>contract RequisitionLine request satırı.</summary>
public sealed record RequisitionLineInput(
    string ItemId,
    string? SkuId,
    string Quantity,
    string UomId,
    string? NeedBy);

/// <summary>contract RequisitionUpsert request body.</summary>
public sealed record RequisitionUpsertRequest(
    List<RequisitionLineInput>? Lines,
    string? Justification);
