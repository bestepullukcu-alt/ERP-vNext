using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ImportExport;

public sealed record ImportContactsCommand(bool DryRun, IReadOnlyList<ContactImportRow> Rows) : IRequest<Response<ImportResultDto>>;

public sealed record ImportAccountContactsCommand(bool DryRun, IReadOnlyList<AccountContactImportRow> Rows) : IRequest<Response<ImportResultDto>>;

public sealed record ImportAccountRelationshipsCommand(bool DryRun, IReadOnlyList<AccountRelationshipImportRow> Rows) : IRequest<Response<ImportResultDto>>;

/// <summary>Exports return CSV text (Response&lt;string&gt;); the controller streams it as text/csv.</summary>
public sealed record ExportContactsQuery : IRequest<Response<string>>;

public sealed record ExportAccountContactsQuery : IRequest<Response<string>>;

public sealed record ExportAccountRelationshipsQuery : IRequest<Response<string>>;

// ---- MOD-0150 Import/Export Task 1 — XLSX workbook (template + existing-data export) ----
// The CSV queries above are untouched: `format=xlsx` is an additive option, not a replacement.

/// <summary>Empty XLSX import template (Instructions / Contacts / AccountLinks / ReferenceData [/ Accounts]).</summary>
public sealed record BuildContactTemplateWorkbookQuery(bool IncludeAccountsSheet) : IRequest<Response<Xlsx.ExportFileDto>>;

/// <summary>Existing Contact (+ optionally AccountContactLink) data as an XLSX workbook, round-trip shaped.</summary>
public sealed record ExportContactsWorkbookQuery(Xlsx.ContactWorkbookOptions Options) : IRequest<Response<Xlsx.ExportFileDto>>;
