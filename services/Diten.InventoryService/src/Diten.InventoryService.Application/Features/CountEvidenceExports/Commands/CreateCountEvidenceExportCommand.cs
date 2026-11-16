using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CountEvidenceExports.Commands;

/// <summary>
/// DL-5 <c>createCountEvidenceExport</c> (draft §4.4; pack §4.5, §21) — the one Evidence Pack write: draws the count's file (PDF | CSV), stores the bytes
/// and their SHA-256 in an insert-only record. <paramref name="Language"/> is the reader's language (the primary subtag of Accept-Language, one of the
/// 7, else en — <see cref="CycleCountEvidenceText.Normal"/>). A command, so it stays OUT of the <c>.Queries</c> namespace; the central record is
/// <c>CreateCountEvidenceExportCommand + Export</c> on <c>CountEvidenceExport</c> (pack §21).
/// </summary>
public sealed record CreateCountEvidenceExportCommand(Guid CountId, string? Format, string IdempotencyKey, string Language)
    : IRequest<Response<CountEvidenceExportDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Export", "CountEvidenceExport");

    public Guid? CentralAuditEntityId(object? response) => Id(Data<CountEvidenceExportDto>(response)?.ExportId);
}
