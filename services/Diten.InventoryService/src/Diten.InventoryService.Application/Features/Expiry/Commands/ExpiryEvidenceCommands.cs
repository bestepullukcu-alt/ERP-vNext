using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Commands;

// WP-0176-F5 — FEFO-BUNDLE 1.2.0 createEvidenceExport, the one write of the Kanıt Paketi. A command, so it stays OUT of the .Queries namespace
// (AuditBehavior.IsQueryRequest would count it as a read); the handler writes expiry.evidence.exported itself, like the F1 commands.

/// <summary>createEvidenceExport — <paramref name="Language"/> is the reader's language for the PDF labels (tr | en; anything else → en).</summary>
public sealed record CreateExpiryEvidenceExportCommand(
    string? Preset,
    string? From,
    string? To,
    IReadOnlyList<string> WarehouseIds,
    IReadOnlyList<string> Types,
    string? Format,
    string IdempotencyKey,
    string Language) : IRequest<Response<ExpiryEvidenceExportDto>>;
