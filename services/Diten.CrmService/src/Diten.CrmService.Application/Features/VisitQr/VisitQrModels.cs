using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitQr;

// ── read models (§2.3) ───────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The institution's QR state (NO payload): the latest row (active, or the last revoked one; <c>none</c> when the
/// institution never had a code), the last scan, the successful scans of the last 30 days, and the parent's active code
/// a visit here also accepts (K-W3E-5).</summary>
public sealed record VisitQrAccountDto(
    Guid AccountId,
    string AccountName,
    string? AccountCode,
    string Status,
    int? Version,
    DateTimeOffset? IssuedAt,
    string? IssuedBy,
    DateTimeOffset? RevokedAt,
    string? RevokedBy,
    string? RevokeReason,
    DateTimeOffset? LastScannedAt,
    int ScanCount,
    int RecentScanCount,
    VisitQrInheritedDto? InheritedFrom);

public sealed record VisitQrInheritedDto(Guid AccountId, string AccountName, int Version);

public sealed record VisitQrEventDto(Guid Id, string Kind, int Version, DateTimeOffset At, string? By, string? Reason);

/// <summary>The print payload (the ONLY place a payload leaves the server; <c>Cache-Control: no-store</c>).</summary>
public sealed record VisitQrPayloadDto(Guid AccountId, string AccountName, string? AccountCode, int Version, string Payload, DateTimeOffset IssuedAt);

public sealed record VisitQrBulkIssueItemDto(Guid AccountId, string Result, int? Version);

public sealed record VisitQrBulkIssueDto(IReadOnlyList<VisitQrBulkIssueItemDto> Items, int Issued, int SkippedActive, int NotFound);

public sealed record VisitQrListItemDto(
    Guid AccountId, string AccountName, string? AccountCode, string AccountType, string? CityRef, string Status, int? Version,
    DateTimeOffset? IssuedAt, DateTimeOffset? LastScannedAt);

public sealed record VisitQrListDto(IReadOnlyList<VisitQrListItemDto> Items, long Total, int Page, int PageSize);

public static class VisitQrBulkResults
{
    public const string Issued = "issued";
    public const string SkippedActive = "skipped_active";
    public const string NotFound = "not_found";
}

// ── inputs ───────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary><c>issue</c>: no active code → the next version; an active code → REGENERATE (<paramref name="ExpectedVersion"/> required).</summary>
public sealed record VisitQrIssueInput(int? ExpectedVersion, string? Reason);

public sealed record VisitQrRevokeInput(int? ExpectedVersion, string? Reason);

public sealed record VisitQrBulkInput(IReadOnlyList<Guid>? AccountIds);

// ── commands / queries ───────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetVisitQrAccountQuery(Guid AccountId) : IRequest<Response<VisitQrAccountDto>>;

public sealed record GetVisitQrHistoryQuery(Guid AccountId) : IRequest<Response<IReadOnlyList<VisitQrEventDto>>>;

public sealed record IssueVisitQrCommand(Guid AccountId, VisitQrIssueInput Input) : IRequest<Response<VisitQrAccountDto>>;

public sealed record RevokeVisitQrCommand(Guid AccountId, VisitQrRevokeInput Input) : IRequest<Response<VisitQrAccountDto>>;

/// <summary>A command (it writes the <c>printed</c> event), although it is a GET on the wire.</summary>
public sealed record GetVisitQrPayloadCommand(Guid AccountId) : IRequest<Response<VisitQrPayloadDto>>;

public sealed record BulkIssueVisitQrCommand(VisitQrBulkInput Input) : IRequest<Response<VisitQrBulkIssueDto>>;

public sealed record BulkVisitQrPayloadCommand(VisitQrBulkInput Input) : IRequest<Response<IReadOnlyList<VisitQrPayloadDto>>>;

public sealed record ListVisitQrAccountsQuery(
    string? Status, string? AccountType, string? City, string? TerritoryId, string? Search, int Page, int PageSize)
    : IRequest<Response<VisitQrListDto>>;
