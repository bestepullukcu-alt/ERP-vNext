using Diten.BuildingBlocks.Auditing;
using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitQr;

// ── read models (§2.3) ───────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The institution's QR state (NO payload): the latest row (active, or the last revoked one; <c>none</c> when the
/// institution never had a code), the last scan, the successful scans of the last 30 days, and the parent's active code
/// a visit here also accepts (K-W3E-5). WP-VW-W3E (W3E-FIX, G-4): <see cref="IssuedByName"/> / <see cref="RevokedByName"/>
/// are the people's display names (the tenant's users only; null when unresolved — the screen then shows "—", never the id).</summary>
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
    VisitQrInheritedDto? InheritedFrom,
    string? IssuedByName = null,
    string? RevokedByName = null);

public sealed record VisitQrInheritedDto(Guid AccountId, string AccountName, int Version);

/// <summary>WP-VW-W3E (W3E-BE-c, D-4) — a <c>printed</c> event carries the paper and copies the print said (else null).
/// W3E-FIX (G-4): <see cref="ByName"/> is the person's display name (the tenant's users only; null when unresolved).</summary>
public sealed record VisitQrEventDto(
    Guid Id, string Kind, int Version, DateTimeOffset At, string? By, string? Reason, string? Paper = null, int? Copies = null, string? ByName = null);

/// <summary>The print payload (the ONLY place a payload leaves the server; <c>Cache-Control: no-store</c>).</summary>
public sealed record VisitQrPayloadDto(Guid AccountId, string AccountName, string? AccountCode, int Version, string Payload, DateTimeOffset IssuedAt);

public sealed record VisitQrBulkIssueItemDto(Guid AccountId, string Result, int? Version);

public sealed record VisitQrBulkIssueDto(IReadOnlyList<VisitQrBulkIssueItemDto> Items, int Issued, int SkippedActive, int NotFound);

/// <summary>A list row. WP-VW-W3E (W3E-BE-c, D-5): the parent institution (↳), the parent's active code a visit here
/// also accepts (<see cref="InheritedFrom"/>, K-W3E-5), the successful scans of the last 30 days.</summary>
public sealed record VisitQrListItemDto(
    Guid AccountId, string AccountName, string? AccountCode, string AccountType, string? CityRef, string Status, int? Version,
    DateTimeOffset? IssuedAt, DateTimeOffset? LastScannedAt,
    Guid? ParentAccountId = null, string? ParentAccountName = null, VisitQrInheritedDto? InheritedFrom = null, int RecentScanCount = 0);

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

/// <summary>The bulk body. WP-VW-W3E (W3E-BE-c, D-4): <paramref name="Paper"/> / <paramref name="Copies"/> are read by
/// <c>bulk-payload</c> only (the printed events); <c>bulk-issue</c> ignores them.</summary>
public sealed record VisitQrBulkInput(IReadOnlyList<Guid>? AccountIds, string? Paper = null, int? Copies = null);

// ── commands / queries ───────────────────────────────────────────────────────────────────────────────────────────────

public sealed record GetVisitQrAccountQuery(Guid AccountId) : IRequest<Response<VisitQrAccountDto>>;

public sealed record GetVisitQrHistoryQuery(Guid AccountId) : IRequest<Response<IReadOnlyList<VisitQrEventDto>>>;

public sealed record IssueVisitQrCommand(Guid AccountId, VisitQrIssueInput Input)
    : IRequest<Response<VisitQrAccountDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitQrAudit.Describe(AuditOperation.Create, AccountId);

    /// <summary>A REGENERATE (an expected version given) or a first issue; the reason is the QR history's, not copied here.</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Set("regenerate", Input?.ExpectedVersion is not null).Set("reasonGiven", !string.IsNullOrWhiteSpace(Input?.Reason));
        details.ObjectVersion = (response as Response<VisitQrAccountDto>)?.Data?.Version;
    }
}

public sealed record RevokeVisitQrCommand(Guid AccountId, VisitQrRevokeInput Input)
    : IRequest<Response<VisitQrAccountDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitQrAudit.Describe(AuditOperation.Revoke, AccountId);

    /// <summary>The revoked version; the reason text stays in the QR history (free text is not copied to the central log).</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Set("expectedVersion", Input?.ExpectedVersion).Set("reasonGiven", !string.IsNullOrWhiteSpace(Input?.Reason));
        details.ObjectVersion = (response as Response<VisitQrAccountDto>)?.Data?.Version;
    }
}

/// <summary>A command (it writes the <c>printed</c> event), although it is a GET on the wire. W3E-BE-c (D-4): the paper /
/// copies the print says (optional; recorded on the event).</summary>
public sealed record GetVisitQrPayloadCommand(Guid AccountId, string? Paper = null, int? Copies = null)
    : IRequest<Response<VisitQrPayloadDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitQrAudit.Describe(AuditOperation.Export, AccountId);

    /// <summary>The printed version, paper and copies — NEVER the payload (a signed code; AUD-001 §3).</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Set("paper", Paper).Set("copies", Copies);
        details.ObjectVersion = (response as Response<VisitQrPayloadDto>)?.Data?.Version;
    }
}

public sealed record BulkIssueVisitQrCommand(VisitQrBulkInput Input)
    : IRequest<Response<VisitQrBulkIssueDto>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitQrAudit.Describe(AuditOperation.Create, accountId: null);

    /// <summary>The institutions asked for (≤ <see cref="VisitQrLimits.MaxBulk"/>) and what happened to them.</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Set("accountIds", Input?.AccountIds?.ToArray()).Set("requested", Input?.AccountIds?.Count ?? 0);
        if ((response as Response<VisitQrBulkIssueDto>)?.Data is { } result)
        {
            details.Set("issued", result.Issued).Set("skippedActive", result.SkippedActive).Set("notFound", result.NotFound);
        }
    }
}

public sealed record BulkVisitQrPayloadCommand(VisitQrBulkInput Input)
    : IRequest<Response<IReadOnlyList<VisitQrPayloadDto>>>, IAuditableCommand, IAuditDetailsProvider
{
    public AuditDescriptor DescribeAudit() => VisitQrAudit.Describe(AuditOperation.Export, accountId: null);

    /// <summary>The institutions asked for, how many were printed, paper and copies — NEVER a payload.</summary>
    public void DescribeAuditDetails(AuditDetails details, object? response)
    {
        details.Set("accountIds", Input?.AccountIds?.ToArray()).Set("requested", Input?.AccountIds?.Count ?? 0)
            .Set("paper", Input?.Paper).Set("copies", Input?.Copies);
        if ((response as Response<IReadOnlyList<VisitQrPayloadDto>>)?.Data is { } printed)
        {
            details.Set("printed", printed.Count);
        }
    }
}

/// <summary>WP-VW-W6 F8-a (AUD-001 K4, path b) — the institution QR commands' central audit record (MOD-0155, W3E).</summary>
public static class VisitQrAudit
{
    public const string EntityType = "AccountVisitQr";
    public const string SourceModule = "MOD-0155";

    public static AuditDescriptor Describe(AuditOperation operation, Guid? accountId)
        => new(AuditCategory.Crm, operation, EntityType, SourceModule, accountId is { } id && id != Guid.Empty ? id : null);
}

public sealed record ListVisitQrAccountsQuery(
    string? Status, string? AccountType, string? City, string? TerritoryId, string? Search, int Page, int PageSize)
    : IRequest<Response<VisitQrListDto>>;
