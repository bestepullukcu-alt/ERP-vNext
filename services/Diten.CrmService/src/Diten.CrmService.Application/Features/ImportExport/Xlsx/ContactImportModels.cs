namespace Diten.CrmService.Application.Features.ImportExport.Xlsx;

/// <summary>Row outcome categories shown in the preview and the result report.</summary>
public static class ImportRowStatuses
{
    public const string Create = "create";
    public const string Update = "update";
    public const string End = "end";
    public const string Skip = "skip";
    public const string Error = "error";
    public const string Conflict = "conflict";
    public const string SkippedDependency = "skipped_dependency";
}

/// <summary>Allowed values of the sheet's <c>Operation</c> column.</summary>
public static class ImportOperations
{
    public const string Add = "add";
    public const string Update = "update";
    public const string End = "end";
    public const string Skip = "skip";

    /// <summary>Accepted as a synonym of <see cref="Add"/> (the Instructions sheet says "add/create").</summary>
    public const string Create = "create";

    /// <summary>Explicitly NOT supported — a Contact/AccountLink is never destroyed by an import.</summary>
    public const string Delete = "delete";

    public static string? Normalize(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant();
        return value switch
        {
            null or "" => null,
            Create => Add,
            _ => value
        };
    }
}

/// <summary>
/// One preview/apply row outcome. Everything here is PII-safe: <see cref="Message"/> states the rule, never the
/// offending value, and <see cref="DisplayLabel"/> is masked (e.g. "A** Y****"), so the preview can be rendered,
/// downloaded and logged without leaking a name, phone number, e-mail or note.
/// </summary>
public sealed record ImportRowResultDto(
    string Sheet,
    int RowNumber,
    string? Operation,
    string EntityType,
    string? ResolvedKey,
    string Status,
    string? Code,
    string Message,
    IReadOnlyList<string> ChangedFields,
    string? DisplayLabel,
    string Severity);

public sealed record ImportSummaryDto(
    int TotalRows,
    int Creates,
    int Updates,
    int Ends,
    int Skips,
    int Errors,
    int Warnings,
    int Conflicts);

/// <summary>Preview (dry-run) or apply outcome for a whole workbook.</summary>
public sealed record ImportPreviewDto(
    string CorrelationId,
    bool DryRun,
    bool Applied,
    bool CanApply,
    string? BlockedReason,
    string Strategy,
    ImportSummaryDto Summary,
    IReadOnlyList<string> FileErrors,
    IReadOnlyList<string> FileWarnings,
    IReadOnlyList<ImportRowResultDto> Rows);

/// <summary>
/// What the caller is allowed to do, resolved from MOD-0018 claims by the API layer. Row-level rather than
/// endpoint-level so a user who may import contacts but not manage account links gets a precise, fail-closed message
/// on exactly the rows they cannot execute — instead of a blanket 403 or a silently trimmed import.
/// </summary>
public sealed record ImportCapabilities(bool CanCreateContact, bool CanUpdateContact, bool CanManageLinks)
{
    public static readonly ImportCapabilities Full = new(true, true, true);
}

/// <summary>Masks personal data for preview/report display. Never produces a readable name, e-mail or phone.</summary>
public static class ImportDisplayLabel
{
    public static string? ForContact(string? firstName, string? lastName, string? displayName)
    {
        var source = !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : $"{firstName} {lastName}".Trim();

        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts.Select(Mask));
    }

    private static string Mask(string word)
        => word.Length <= 1 ? word + "*" : word[0] + new string('*', Math.Min(word.Length - 1, 4));
}
