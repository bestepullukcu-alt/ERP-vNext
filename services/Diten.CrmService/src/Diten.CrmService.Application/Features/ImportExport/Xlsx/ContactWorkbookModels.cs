using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.ImportExport.Xlsx;

/// <summary>A produced file (workbook bytes + download name + content type). Used by the XLSX template/export handlers.</summary>
public sealed record ExportFileDto(byte[] Content, string FileName, string ContentType)
{
    public const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

/// <summary>
/// What the caller asked for. Drives both the sheets that get written and the option/filter summary printed on the
/// Instructions sheet, so the file itself documents how it was produced.
/// </summary>
public sealed record ContactWorkbookOptions(
    bool IncludeLinks = false,
    bool IncludeHistorical = false,
    bool IncludeNotes = false,
    bool IncludeAccountsSheet = false,
    string? ContactType = null,
    string? Status = null,
    string? Country = null,
    DateTimeOffset? UpdatedAfter = null)
{
    public static readonly ContactWorkbookOptions Template = new();

    /// <summary>Names (never values) of the filters that were applied — safe for the audit detail string.</summary>
    public IReadOnlyList<string> AppliedFilterFields()
    {
        var fields = new List<string>();
        if (!string.IsNullOrWhiteSpace(ContactType)) fields.Add(nameof(ContactType));
        if (!string.IsNullOrWhiteSpace(Status)) fields.Add(nameof(Status));
        if (!string.IsNullOrWhiteSpace(Country)) fields.Add(nameof(Country));
        if (UpdatedAfter is not null) fields.Add(nameof(UpdatedAfter));
        return fields;
    }
}

/// <summary>Everything the workbook writer needs. Rows are pre-shaped as strings by the handler (schema order).</summary>
public sealed record ContactWorkbookRequest(
    bool IsTemplate,
    ContactWorkbookOptions Options,
    IReadOnlyList<IReadOnlyList<string?>> ContactRows,
    IReadOnlyList<IReadOnlyList<string?>> AccountLinkRows,
    IReadOnlyList<ReferenceSetSnapshot> ReferenceSets,
    IReadOnlyList<IReadOnlyList<string?>>? AccountRows,
    string GeneratedAtUtc,
    string CorrelationId);
