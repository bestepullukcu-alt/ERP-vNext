namespace Diten.CrmService.Application.Features.ImportExport;

/// <summary>Common import result summary (Contact / AccountContactLink / AccountRelationship).</summary>
public sealed record ImportResultDto(
    string CorrelationId,
    bool DryRun,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int CreatedRows,
    int UpdatedRows,
    int SkippedRows,
    int ConflictRows,
    IReadOnlyList<ImportRowErrorDto> Errors);

public sealed record ImportRowErrorDto(int RowNumber, string Field, string Code, string Message, string Severity);

// ---- Contact ----

public sealed record ContactImportRow(
    string? ExternalSourceSystem,
    string? ExternalId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? ContactType,
    string? ProfessionalTitle,
    string? Specialty,
    string? Department,
    string? Phone,
    string? Email,
    string? Status,
    string? Notes);

// ---- AccountContactLink ----

public sealed record AccountContactImportRow(
    string? AccountCode,
    Guid? AccountId,
    string? ContactExternalSourceSystem,
    string? ContactExternalId,
    Guid? ContactId,
    string? RoleCode,
    bool IsPrimary,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes);

// ---- AccountRelationship ----

public sealed record AccountRelationshipImportRow(
    string? SourceAccountCode,
    Guid? SourceAccountId,
    string? TargetAccountCode,
    Guid? TargetAccountId,
    string? RelationshipType,
    string? Status,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string? Notes);
