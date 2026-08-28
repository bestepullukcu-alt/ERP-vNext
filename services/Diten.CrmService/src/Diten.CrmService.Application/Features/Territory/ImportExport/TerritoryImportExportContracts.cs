using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>MOD-0151 FU08 — read-only XLSX export of one territory model (pack §22.5 C).</summary>
public sealed record ExportTerritoryModelWorkbookQuery(Guid ModelId) : IRequest<Response<ExportFileDto>>;

/// <summary>MOD-0151 FU08 — the fillable multi-sheet import template for one territory model.</summary>
public sealed record BuildTerritoryImportTemplateQuery(Guid ModelId) : IRequest<Response<ExportFileDto>>;

/// <summary>
/// MOD-0151 FU08 — upload. <c>DryRun=true</c> validates and returns the preview WITHOUT writing anything (not even a
/// run-history row); <c>DryRun=false</c> runs the identical validation and then applies what passed.
/// </summary>
public sealed record TerritoryImportFileCommand(
    Guid ModelId,
    byte[] File,
    string FileName,
    bool DryRun,
    bool StrictMode,
    string? CorrelationId,
    string Actor) : IRequest<Response<TerritoryImportPreviewDto>>;

/// <summary>MOD-0151 FU08 — append-only import run history of one model.</summary>
public sealed record GetTerritoryImportRunsQuery(Guid ModelId) : IRequest<Response<TerritoryImportRunListDto>>;
