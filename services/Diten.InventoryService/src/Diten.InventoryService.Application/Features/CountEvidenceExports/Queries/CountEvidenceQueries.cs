using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountEvidenceExports.Queries;

// DL-5 (draft §4.4) — the Evidence Pack reads. Raw text in, validated by the handler (400 INVALID_REQUEST) — the DL-1…DL-3 pattern.

/// <summary><c>listCountEvidenceExports</c> — the company's export log, newest first; <c>countId</c> narrows it to one count.</summary>
public sealed record GetCountEvidenceExportListQuery(string? CountId, string? Page, string? PageSize) : IRequest<Response<CountEvidenceExportPageDto>>;

/// <summary><c>getCountEvidenceExportFile</c> — the stored bytes of one export (what was hashed).</summary>
public sealed record GetCountEvidenceExportFileQuery(Guid ExportId) : IRequest<Response<CountEvidenceFileDto>>;
