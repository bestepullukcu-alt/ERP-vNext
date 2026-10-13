using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Queries;

// WP-0176-F5 — FEFO-BUNDLE 1.2.0 Kanıt Paketi reads (previewEvidence, listEvidenceExports, getEvidenceExportFile); the write is in Commands/ExpiryEvidenceCommands.cs. Raw text in,
// validated by the handler (400 INVALID_REQUEST) — the F1 / F4 pattern.

public sealed record PreviewExpiryEvidenceQuery(
    string? Preset,
    string? From,
    string? To,
    IReadOnlyList<string> WarehouseIds,
    IReadOnlyList<string> Types) : IRequest<Response<ExpiryEvidencePreviewDto>>;

public sealed record ListExpiryEvidenceExportsQuery(string? Page, string? PageSize) : IRequest<Response<ExpiryEvidenceExportPageDto>>;

public sealed record GetExpiryEvidenceFileQuery(string? ExportId) : IRequest<Response<ExpiryEvidenceFileDto>>;
