namespace Diten.ProcurementService.Application.Features.Clause;

// ══ READ DTOs (contracting.openapi.yaml Clause şekliyle uyumlu) ═══════════════════════

/// <summary>Clause read DTO — contract Clause {clauseId, category, title, body, contractVersion}. (Id/Version
/// house-style zarf alanı.)</summary>
public sealed record ClauseDto(
    Guid Id,
    string ClauseId,
    string Category,
    string Title,
    string Body,
    int Version,
    string ContractVersion);

/// <summary>Sayfalı liste sonucu (contract listClauseLibrary {items, nextCursor, contractVersion}).</summary>
public sealed record ClauseListResultDto(
    IReadOnlyList<ClauseDto> Items,
    string? NextCursor,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract ClauseUpsert) ══

/// <summary>contract ClauseUpsert body. TenantId/LegalEntityId server-resolved (payload'da YOK); Idempotency-Key header.</summary>
public sealed record ClauseUpsertBody(
    string Category,
    string Title,
    string Body);
