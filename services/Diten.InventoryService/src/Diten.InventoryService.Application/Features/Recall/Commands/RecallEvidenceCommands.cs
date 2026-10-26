using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Recall.Commands;

// WP-0177-R7b — RECALL 1.7.0 createEvidenceExport, the one write of the Kanıt Paketi. A command, so it stays OUT of the .Queries namespace
// (AuditBehavior.IsQueryRequest would count it as a read); BL-501: it declares its central-audit record (the handler's transaction joins it).

/// <summary>
/// createEvidenceExport — exactly one subject (caseId | runId | from + to); <paramref name="Format"/> Pdf | Csv; <paramref name="Language"/> is the
/// reader's language for the PDF / CSV labels (tr | en; anything else → en — the FU-0176-PDF-L10N limit).
/// </summary>
public sealed record CreateRecallEvidenceExportCommand(
    string? CaseId,
    string? RunId,
    string? From,
    string? To,
    string? Format,
    string IdempotencyKey,
    string Language) : IRequest<Response<RecallEvidenceExportDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Export", "RecallEvidenceExport");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallEvidenceExportDto>(response)?.ExportId);
}
