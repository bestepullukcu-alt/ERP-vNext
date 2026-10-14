using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Territory.AccountAssignments;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitQr;

/// <summary>WP-VW-W3E (W3E-BE-a, §2.3) — what every QR handler shares: the tenant, the institution (404 when it is not the
/// tenant's), the coded failures (<c>[message, code]</c>), the state DTO.</summary>
public sealed class VisitQrService
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IAccountRepository _accounts;
    private readonly IInstitutionQrCodeRepository _codes;
    private readonly IInstitutionQrEventRepository _events;
    private readonly IVisitQrKeyRing _keys;
    private readonly TimeProvider _clock;

    public VisitQrService(
        ITenantContext tenant, IActorContext actor, IAccountRepository accounts, IInstitutionQrCodeRepository codes,
        IInstitutionQrEventRepository events, IVisitQrKeyRing keys, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _accounts = accounts;
        _codes = codes;
        _events = events;
        _keys = keys;
        _clock = clock ?? TimeProvider.System;
    }

    public Guid? TenantId => _tenant.TenantId;
    public string? Actor => _actor.ActorName;
    public DateTimeOffset Now => _clock.GetUtcNow();
    public IAccountRepository Accounts => _accounts;
    public IInstitutionQrCodeRepository Codes => _codes;
    public IInstitutionQrEventRepository Events => _events;
    public Task<VisitQrKeys> KeysAsync(CancellationToken ct) => _keys.GetAsync(ct);

    public static Response<T> Fail<T>(string message, string code, int status) => Response<T>.Fail(new[] { message, code }, status);

    public static Response<T> NoTenant<T>() => Response<T>.Fail("Tenant context is required.", 400);

    public static Response<T> AccountNotFound<T>()
        => Fail<T>("The institution does not exist.", VisitExecutionErrorCodes.QrAccountNotFound, 404);

    public static Response<T> SigningUnavailable<T>()
        => Fail<T>("The QR signing key is not available. Nothing was issued.", VisitExecutionErrorCodes.QrSigningUnavailable, 503);

    public static Response<T> Conflict<T>()
        => Fail<T>("The institution's QR code was changed by someone else. Reload and try again.", VisitExecutionErrorCodes.ConcurrencyConflict, 409);

    public static Response<T> NotActive<T>()
        => Fail<T>("The institution has no active QR code.", VisitExecutionErrorCodes.QrNotActive, 409);

    public static Response<T> Invalid<T>(string message) => Fail<T>(message, VisitExecutionErrorCodes.QrRequestInvalid, 400);

    public Task<Domain.Entities.Account?> AccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        => accountId == Guid.Empty ? Task.FromResult<Domain.Entities.Account?>(null) : _accounts.GetByIdAsync(tenantId, accountId, ct);

    /// <summary>The institution's state: the latest row, the last-30-days scans, the parent's active code (K-W3E-5).</summary>
    public async Task<VisitQrAccountDto> StateAsync(Guid tenantId, Domain.Entities.Account account, CancellationToken ct)
    {
        var rows = await _codes.ListByAccountAsync(tenantId, account.Id, ct);
        var latest = rows.FirstOrDefault(r => r.IsActive()) ?? rows.OrderByDescending(r => r.CodeVersion).FirstOrDefault();
        var recent = latest is null ? 0 : await _codes.CountScansSinceAsync(tenantId, account.Id, Now.AddDays(-VisitQrLimits.RecentScanDays), ct);

        VisitQrInheritedDto? inherited = null;
        var parentId = account.ParentAccountId;
        var seen = new HashSet<Guid> { account.Id };
        for (var depth = 0; depth < VisitQrLimits.MaxParentDepth && parentId is { } pid && pid != Guid.Empty && seen.Add(pid); depth++)
        {
            var parent = await _accounts.GetByIdAsync(tenantId, pid, ct);
            if (parent is null)
            {
                break;
            }

            if (await _codes.GetActiveAsync(tenantId, parent.Id, ct) is { } parentCode)
            {
                inherited = new VisitQrInheritedDto(parent.Id, parent.AccountName, parentCode.CodeVersion);
                break;
            }

            parentId = parent.ParentAccountId;
        }

        return new VisitQrAccountDto(
            account.Id, account.AccountName, account.AccountCode,
            latest?.Status ?? InstitutionQrCodeStatuses.None,
            latest?.CodeVersion, latest?.IssuedAt, latest?.IssuedBy, latest?.RevokedAt, latest?.RevokedBy, latest?.RevokeReason,
            rows.Where(r => r.LastScannedAt is not null).Select(r => r.LastScannedAt).Max(),
            rows.Sum(r => r.ScanCount), recent, inherited);
    }

    public InstitutionQrCode NewCode(Guid tenantId, Guid accountId, int version, string keyId) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AccountId = accountId, CodeVersion = version, Status = InstitutionQrCodeStatuses.Active,
        KeyId = keyId, IssuedAt = Now, IssuedBy = Actor, CreatedAt = Now
    };

    public InstitutionQrEvent Event(Guid tenantId, InstitutionQrCode code, string kind, string? reason, string? paper = null, int? copies = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AccountId = code.AccountId, QrCodeId = code.Id, Kind = kind,
        CodeVersion = code.CodeVersion, At = Now, By = Actor, Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), CreatedAt = Now,
        Paper = paper, Copies = copies
    };

    public static int NextVersion(IReadOnlyList<InstitutionQrCode> rows) => rows.Count == 0 ? 1 : rows.Max(r => r.CodeVersion) + 1;

    public static string? CheckReason(string? reason, bool required)
    {
        var text = reason?.Trim();
        if (required && string.IsNullOrEmpty(text))
        {
            return "A reason is required.";
        }

        return text is { Length: > VisitQrLimits.MaxReasonLength } ? $"The reason must be at most {VisitQrLimits.MaxReasonLength} characters." : null;
    }

    public static (IReadOnlyList<Guid>? Ids, string? Error) CheckBulk(VisitQrBulkInput? input)
    {
        var ids = (input?.AccountIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return (null, "accountIds must name at least one institution.");
        }

        return (input?.AccountIds?.Count ?? 0) > VisitQrLimits.MaxBulk
            ? (null, $"At most {VisitQrLimits.MaxBulk} institutions per request.")
            : (ids, null);
    }
}

public sealed class GetVisitQrAccountHandler(VisitQrService qr) : IRequestHandler<GetVisitQrAccountQuery, Response<VisitQrAccountDto>>
{
    public async Task<Response<VisitQrAccountDto>> Handle(GetVisitQrAccountQuery request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<VisitQrAccountDto>();
        var account = await qr.AccountAsync(tenantId, request.AccountId, ct);
        return account is null
            ? VisitQrService.AccountNotFound<VisitQrAccountDto>()
            : Response<VisitQrAccountDto>.Success(await qr.StateAsync(tenantId, account, ct));
    }
}

public sealed class GetVisitQrHistoryHandler(VisitQrService qr) : IRequestHandler<GetVisitQrHistoryQuery, Response<IReadOnlyList<VisitQrEventDto>>>
{
    public async Task<Response<IReadOnlyList<VisitQrEventDto>>> Handle(GetVisitQrHistoryQuery request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<IReadOnlyList<VisitQrEventDto>>();
        if (await qr.AccountAsync(tenantId, request.AccountId, ct) is null) return VisitQrService.AccountNotFound<IReadOnlyList<VisitQrEventDto>>();
        var events = await qr.Events.ListByAccountAsync(tenantId, request.AccountId, ct);
        return Response<IReadOnlyList<VisitQrEventDto>>.Success(events
            .Select(e => new VisitQrEventDto(e.Id, e.Kind, e.CodeVersion, e.At, e.By, e.Reason, e.Paper, e.Copies)).ToList());
    }
}

/// <summary><c>POST accounts/{id}/issue</c>: no active code → version+1 (<c>issued</c>); an active code → REGENERATE: the
/// active row is revoked at <c>expectedVersion</c> (else 409) and version+1 is inserted (<c>regenerated</c>), so every older
/// sticker answers <c>revoked</c> from now on. Two concurrent issues: the partial unique index keeps ONE active code, the
/// loser gets 409.</summary>
public sealed class IssueVisitQrHandler(VisitQrService qr) : IRequestHandler<IssueVisitQrCommand, Response<VisitQrAccountDto>>
{
    public async Task<Response<VisitQrAccountDto>> Handle(IssueVisitQrCommand request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<VisitQrAccountDto>();
        var account = await qr.AccountAsync(tenantId, request.AccountId, ct);
        if (account is null) return VisitQrService.AccountNotFound<VisitQrAccountDto>();
        if (VisitQrService.CheckReason(request.Input?.Reason, required: false) is { } bad) return VisitQrService.Invalid<VisitQrAccountDto>(bad);
        var keys = await qr.KeysAsync(ct);
        if (!keys.Available) return VisitQrService.SigningUnavailable<VisitQrAccountDto>();

        var rows = await qr.Codes.ListByAccountAsync(tenantId, account.Id, ct);
        var active = rows.FirstOrDefault(r => r.IsActive());
        var kind = InstitutionQrEventKinds.Issued;
        if (active is not null)
        {
            if (request.Input?.ExpectedVersion is not { } expected || expected != active.CodeVersion)
            {
                return VisitQrService.Conflict<VisitQrAccountDto>();
            }

            if (!await qr.Codes.RevokeAsync(tenantId, active.Id, expected, qr.Actor, qr.Now, "regenerated", ct))
            {
                return VisitQrService.Conflict<VisitQrAccountDto>();
            }

            kind = InstitutionQrEventKinds.Regenerated;
        }

        var code = qr.NewCode(tenantId, account.Id, VisitQrService.NextVersion(rows), keys.ActiveKeyId!);
        if (!await qr.Codes.InsertAsync(code, ct))
        {
            return VisitQrService.Conflict<VisitQrAccountDto>();
        }

        await qr.Events.InsertManyAsync(new[] { qr.Event(tenantId, code, kind, request.Input?.Reason) }, ct);
        return Response<VisitQrAccountDto>.Success(await qr.StateAsync(tenantId, account, ct), 201);
    }
}

/// <summary><c>POST accounts/{id}/revoke</c>: the active code at <c>expectedVersion</c>, a reason (≤ 500) required.</summary>
public sealed class RevokeVisitQrHandler(VisitQrService qr) : IRequestHandler<RevokeVisitQrCommand, Response<VisitQrAccountDto>>
{
    public async Task<Response<VisitQrAccountDto>> Handle(RevokeVisitQrCommand request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<VisitQrAccountDto>();
        var account = await qr.AccountAsync(tenantId, request.AccountId, ct);
        if (account is null) return VisitQrService.AccountNotFound<VisitQrAccountDto>();
        if (VisitQrService.CheckReason(request.Input?.Reason, required: true) is { } bad) return VisitQrService.Invalid<VisitQrAccountDto>(bad);
        if (request.Input?.ExpectedVersion is not { } expected) return VisitQrService.Invalid<VisitQrAccountDto>("expectedVersion is required.");

        var active = await qr.Codes.GetActiveAsync(tenantId, account.Id, ct);
        if (active is null) return VisitQrService.NotActive<VisitQrAccountDto>();
        if (active.CodeVersion != expected
            || !await qr.Codes.RevokeAsync(tenantId, active.Id, expected, qr.Actor, qr.Now, request.Input.Reason!.Trim(), ct))
        {
            return VisitQrService.Conflict<VisitQrAccountDto>();
        }

        await qr.Events.InsertManyAsync(new[] { qr.Event(tenantId, active, InstitutionQrEventKinds.Revoked, request.Input.Reason) }, ct);
        return Response<VisitQrAccountDto>.Success(await qr.StateAsync(tenantId, account, ct));
    }
}

/// <summary><c>GET accounts/{id}/payload</c> — the print: the ACTIVE code's payload re-derived with the key it was issued
/// with (a reprint is the same payload), one <c>printed</c> event. The payload is never logged.</summary>
public sealed class GetVisitQrPayloadHandler(VisitQrService qr) : IRequestHandler<GetVisitQrPayloadCommand, Response<VisitQrPayloadDto>>
{
    public async Task<Response<VisitQrPayloadDto>> Handle(GetVisitQrPayloadCommand request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<VisitQrPayloadDto>();
        var (paper, printError) = VisitQrPrint.Check(request.Paper, request.Copies);
        if (printError is not null) return VisitQrService.Invalid<VisitQrPayloadDto>(printError);
        var account = await qr.AccountAsync(tenantId, request.AccountId, ct);
        if (account is null) return VisitQrService.AccountNotFound<VisitQrPayloadDto>();
        var active = await qr.Codes.GetActiveAsync(tenantId, account.Id, ct);
        if (active is null) return VisitQrService.NotActive<VisitQrPayloadDto>();
        var keys = await qr.KeysAsync(ct);
        if (!keys.Keys.ContainsKey(active.KeyId) || keys.Keys[active.KeyId].Length < VisitQrSigner.MinKeyBytes)
        {
            return VisitQrService.SigningUnavailable<VisitQrPayloadDto>();
        }

        var payload = VisitQrSigner.Sign(keys, active.KeyId, tenantId, account.Id, active.CodeVersion);
        await qr.Events.InsertManyAsync(new[] { qr.Event(tenantId, active, InstitutionQrEventKinds.Printed, null, paper, request.Copies) }, ct);
        return Response<VisitQrPayloadDto>.Success(new VisitQrPayloadDto(account.Id, account.AccountName, account.AccountCode, active.CodeVersion, payload, active.IssuedAt));
    }
}

/// <summary><c>POST bulk-issue</c> (≤ 200): an institution with an active code is skipped; not the tenant's → not_found.</summary>
public sealed class BulkIssueVisitQrHandler(VisitQrService qr) : IRequestHandler<BulkIssueVisitQrCommand, Response<VisitQrBulkIssueDto>>
{
    public async Task<Response<VisitQrBulkIssueDto>> Handle(BulkIssueVisitQrCommand request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<VisitQrBulkIssueDto>();
        var (ids, error) = VisitQrService.CheckBulk(request.Input);
        if (error is not null) return VisitQrService.Invalid<VisitQrBulkIssueDto>(error);
        var keys = await qr.KeysAsync(ct);
        if (!keys.Available) return VisitQrService.SigningUnavailable<VisitQrBulkIssueDto>();

        var accounts = (await qr.Accounts.ListByIdsAsync(tenantId, ids!, ct)).Where(a => a.TenantId == tenantId && !a.IsDeleted).ToDictionary(a => a.Id);
        var latest = (await qr.Codes.ListLatestByAccountsAsync(tenantId, accounts.Keys.ToList(), ct)).ToDictionary(c => c.AccountId);
        var active = (await qr.Codes.ListActiveByAccountsAsync(tenantId, accounts.Keys.ToList(), ct)).Select(c => c.AccountId).ToHashSet();
        var items = new List<VisitQrBulkIssueItemDto>();
        var events = new List<InstitutionQrEvent>();
        foreach (var id in ids!)
        {
            if (!accounts.ContainsKey(id))
            {
                items.Add(new VisitQrBulkIssueItemDto(id, VisitQrBulkResults.NotFound, null));
                continue;
            }

            if (active.Contains(id))
            {
                items.Add(new VisitQrBulkIssueItemDto(id, VisitQrBulkResults.SkippedActive, null));
                continue;
            }

            var code = qr.NewCode(tenantId, id, latest.TryGetValue(id, out var last) ? last.CodeVersion + 1 : 1, keys.ActiveKeyId!);
            if (await qr.Codes.InsertAsync(code, ct))
            {
                items.Add(new VisitQrBulkIssueItemDto(id, VisitQrBulkResults.Issued, code.CodeVersion));
                events.Add(qr.Event(tenantId, code, InstitutionQrEventKinds.BulkIssued, null));
            }
            else
            {
                items.Add(new VisitQrBulkIssueItemDto(id, VisitQrBulkResults.SkippedActive, null)); // issued meanwhile
            }
        }

        await qr.Events.InsertManyAsync(events, ct);
        return Response<VisitQrBulkIssueDto>.Success(new VisitQrBulkIssueDto(items,
            items.Count(i => i.Result == VisitQrBulkResults.Issued), items.Count(i => i.Result == VisitQrBulkResults.SkippedActive),
            items.Count(i => i.Result == VisitQrBulkResults.NotFound)));
    }
}

/// <summary><c>POST bulk-payload</c> (≤ 200): the print payloads of the ACTIVE codes only, a <c>printed</c> event each.</summary>
public sealed class BulkVisitQrPayloadHandler(VisitQrService qr) : IRequestHandler<BulkVisitQrPayloadCommand, Response<IReadOnlyList<VisitQrPayloadDto>>>
{
    public async Task<Response<IReadOnlyList<VisitQrPayloadDto>>> Handle(BulkVisitQrPayloadCommand request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<IReadOnlyList<VisitQrPayloadDto>>();
        var (ids, error) = VisitQrService.CheckBulk(request.Input);
        if (error is not null) return VisitQrService.Invalid<IReadOnlyList<VisitQrPayloadDto>>(error);
        var (paper, printError) = VisitQrPrint.Check(request.Input?.Paper, request.Input?.Copies);
        if (printError is not null) return VisitQrService.Invalid<IReadOnlyList<VisitQrPayloadDto>>(printError);
        var keys = await qr.KeysAsync(ct);
        var accounts = (await qr.Accounts.ListByIdsAsync(tenantId, ids!, ct)).Where(a => a.TenantId == tenantId && !a.IsDeleted).ToDictionary(a => a.Id);
        var active = (await qr.Codes.ListActiveByAccountsAsync(tenantId, accounts.Keys.ToList(), ct)).ToDictionary(c => c.AccountId);
        if (active.Values.Any(c => !keys.Keys.TryGetValue(c.KeyId, out var k) || k.Length < VisitQrSigner.MinKeyBytes))
        {
            return VisitQrService.SigningUnavailable<IReadOnlyList<VisitQrPayloadDto>>();
        }

        var result = new List<VisitQrPayloadDto>();
        var events = new List<InstitutionQrEvent>();
        foreach (var id in ids!.Where(active.ContainsKey))
        {
            var code = active[id];
            var account = accounts[id];
            result.Add(new VisitQrPayloadDto(id, account.AccountName, account.AccountCode, code.CodeVersion,
                VisitQrSigner.Sign(keys, code.KeyId, tenantId, id, code.CodeVersion), code.IssuedAt));
            events.Add(qr.Event(tenantId, code, InstitutionQrEventKinds.Printed, null, paper, request.Input?.Copies));
        }

        await qr.Events.InsertManyAsync(events, ct);
        return Response<IReadOnlyList<VisitQrPayloadDto>>.Success(result);
    }
}

/// <summary>
/// <c>GET accounts</c> — the bulk page: institutions (the existing Accounts query: search, type, paging on the server —
/// 43K rows never leave Mongo) with their QR state; an institution with no code is listed as <c>none</c>. The QR status /
/// territory (subtree, the Accounts chip rule) / city filters become an account-id scope or exclusion of the same query.
/// </summary>
public sealed class ListVisitQrAccountsHandler(
    VisitQrService qr, IAccountTerritoryAssignmentRepository assignments, ITerritoryModelRepository models, ITerritoryNodeRepository nodes)
    : IRequestHandler<ListVisitQrAccountsQuery, Response<VisitQrListDto>>
{
    public async Task<Response<VisitQrListDto>> Handle(ListVisitQrAccountsQuery request, CancellationToken ct)
    {
        if (qr.TenantId is not { } tenantId) return VisitQrService.NoTenant<VisitQrListDto>();
        var status = VisitExecutionRules.Normalize(request.Status);
        if (status is not null && status != InstitutionQrCodeStatuses.None && !InstitutionQrCodeStatuses.All.Contains(status))
        {
            return VisitQrService.Invalid<VisitQrListDto>("status must be one of: active, revoked, none.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = request.PageSize <= 0 ? 25 : request.PageSize;
        if (pageSize > VisitQrLimits.MaxPageSize) return VisitQrService.Invalid<VisitQrListDto>($"pageSize must be at most {VisitQrLimits.MaxPageSize}.");

        HashSet<Guid>? scope = null;
        void Narrow(IEnumerable<Guid> ids) => scope = scope is null ? ids.ToHashSet() : scope.Intersect(ids).ToHashSet();

        if (!string.IsNullOrWhiteSpace(request.TerritoryId))
        {
            if (!Guid.TryParse(request.TerritoryId, out var nodeId)) return VisitQrService.Invalid<VisitQrListDto>("territoryId must be a territory node id.");
            Narrow(await AccountCurrentCoverageResolver.ResolveCoveredAccountIdsBySubtreesAsync(assignments, models, nodes, tenantId, new[] { nodeId }, qr.Now, ct));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            Narrow(await qr.Accounts.ListIdsByCityAsync(tenantId, request.City.Trim(), ct));
        }

        IReadOnlyCollection<Guid>? excluded = null;
        if (status == InstitutionQrCodeStatuses.Active)
        {
            Narrow(await qr.Codes.ListAccountIdsByStatusAsync(tenantId, InstitutionQrCodeStatuses.Active, ct));
        }
        else if (status == InstitutionQrCodeStatuses.Revoked)
        {
            var active = (await qr.Codes.ListAccountIdsByStatusAsync(tenantId, InstitutionQrCodeStatuses.Active, ct)).ToHashSet();
            Narrow((await qr.Codes.ListAccountIdsByStatusAsync(tenantId, InstitutionQrCodeStatuses.Revoked, ct)).Where(id => !active.Contains(id)));
        }
        else if (status == InstitutionQrCodeStatuses.None)
        {
            excluded = (await qr.Codes.ListAccountIdsByStatusAsync(tenantId, InstitutionQrCodeStatuses.Active, ct))
                .Concat(await qr.Codes.ListAccountIdsByStatusAsync(tenantId, InstitutionQrCodeStatuses.Revoked, ct)).Distinct().ToList();
        }

        var types = string.IsNullOrWhiteSpace(request.AccountType) ? null : new[] { request.AccountType.Trim() };
        var (items, total, _) = await qr.Accounts.ListAsync(tenantId, request.Search, page, pageSize, "accountName", "asc",
            null, types, scope, excluded, ct);
        var pageIds = items.Select(a => a.Id).ToList();
        var latest = (await qr.Codes.ListLatestByAccountsAsync(tenantId, pageIds, ct)).ToDictionary(c => c.AccountId);
        var lineage = await VisitQrLineage.ReadAsync(qr, tenantId, items, ct);
        var recent = await qr.Codes.CountScansSinceByAccountsAsync(tenantId, pageIds, qr.Now.AddDays(-VisitQrLimits.RecentScanDays), ct);
        return Response<VisitQrListDto>.Success(new VisitQrListDto(items.Select(a =>
        {
            latest.TryGetValue(a.Id, out var code);
            var parentId = a.ParentAccountId is { } p && p != Guid.Empty ? p : (Guid?)null;
            return new VisitQrListItemDto(a.Id, a.AccountName, a.AccountCode, a.AccountType, a.CityRef,
                code?.Status ?? InstitutionQrCodeStatuses.None, code?.CodeVersion, code?.IssuedAt, code?.LastScannedAt,
                parentId, parentId is { } pid && lineage.Names.TryGetValue(pid, out var pn) ? pn : null,
                lineage.Inherited.GetValueOrDefault(a.Id), recent.GetValueOrDefault(a.Id));
        }).ToList(), total, page, pageSize));
    }
}

/// <summary>
/// WP-VW-W3E (W3E-BE-c, D-5) — a list page's parents and inherited codes, level by level: each level is ONE institution
/// read + ONE active-code read for the whole page (≤ <see cref="VisitQrLimits.MaxParentDepth"/> levels), so the query count
/// does not grow with the page. The walk is the institution read's (<see cref="VisitQrService.StateAsync"/>): the nearest
/// parent with an ACTIVE code, at most 5 levels up, cycle-guarded; only this tenant's institutions are read and named.
/// </summary>
internal static class VisitQrLineage
{
    public sealed record Result(IReadOnlyDictionary<Guid, string> Names, IReadOnlyDictionary<Guid, VisitQrInheritedDto> Inherited);

    public static async Task<Result> ReadAsync(VisitQrService qr, Guid tenantId, IReadOnlyList<Domain.Entities.Account> page, CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        var parentOf = new Dictionary<Guid, Guid?>();
        var inherited = new Dictionary<Guid, VisitQrInheritedDto>();

        // per row: the institution whose parent is checked next, and what the row has walked (cycle guard)
        var cursor = page.Where(a => a.ParentAccountId is { } p && p != Guid.Empty)
            .ToDictionary(a => a.Id, a => a.ParentAccountId!.Value);
        var seen = page.ToDictionary(a => a.Id, a => new HashSet<Guid> { a.Id });

        for (var depth = 0; depth < VisitQrLimits.MaxParentDepth && cursor.Count > 0; depth++)
        {
            var level = cursor.Values.Distinct().ToList();
            var unread = level.Where(id => !parentOf.ContainsKey(id)).ToList();
            if (unread.Count > 0)
            {
                foreach (var a in (await qr.Accounts.ListByIdsAsync(tenantId, unread, ct)).Where(a => a.TenantId == tenantId && !a.IsDeleted))
                {
                    names[a.Id] = a.AccountName;
                    parentOf[a.Id] = a.ParentAccountId is { } p && p != Guid.Empty ? p : null;
                }

                foreach (var id in unread.Where(id => !parentOf.ContainsKey(id)))
                {
                    parentOf[id] = null; // missing / another tenant's: the walk stops there
                }
            }

            var active = (await qr.Codes.ListActiveByAccountsAsync(tenantId, level, ct))
                .Where(c => c.IsActive() && !c.IsDeleted).GroupBy(c => c.AccountId).ToDictionary(g => g.Key, g => g.First());

            var next = new Dictionary<Guid, Guid>();
            foreach (var (row, at) in cursor)
            {
                if (!names.ContainsKey(at) || !seen[row].Add(at))
                {
                    continue; // not this tenant's / missing, or a cycle
                }

                if (active.TryGetValue(at, out var code))
                {
                    inherited[row] = new VisitQrInheritedDto(at, names[at], code.CodeVersion);
                    continue;
                }

                if (parentOf[at] is { } up)
                {
                    next[row] = up;
                }
            }

            cursor = next;
        }

        return new Result(names, inherited);
    }
}
