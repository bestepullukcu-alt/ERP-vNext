using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Expiry.Commands;
using Diten.InventoryService.Application.Features.Expiry.Queries;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>§4.7 getPolicy — the company's row, or the KF-A…KF-D defaults at version 0 (<c>isDefault: true</c>).</summary>
public sealed class GetExpiryPolicyHandler : IRequestHandler<GetExpiryPolicyQuery, Response<ExpiryPolicyDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ExpirySupport _support;

    public GetExpiryPolicyHandler(IExpiryRepository repository, ExpirySupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<ExpiryPolicyDto>> Handle(GetExpiryPolicyQuery request, CancellationToken ct)
    {
        var stored = await _repository.FindPolicyAsync(ct);
        return Response<ExpiryPolicyDto>.Success(ExpiryMapping.Policy(stored ?? ExpiryPolicyRules.Defaults(), _support.LegalEntityId, stored is null));
    }
}

/// <summary>§4.8 getPolicyHistory — one row per changed field of every save, newest first.</summary>
public sealed class GetExpiryPolicyHistoryHandler : IRequestHandler<GetExpiryPolicyHistoryQuery, Response<ExpiryPolicyHistoryPageDto>>
{
    private readonly IExpiryRepository _repository;

    public GetExpiryPolicyHistoryHandler(IExpiryRepository repository) => _repository = repository;

    public async Task<Response<ExpiryPolicyHistoryPageDto>> Handle(GetExpiryPolicyHistoryQuery request, CancellationToken ct)
    {
        if (ExpiryPaging.Parse(request.Page, request.PageSize) is not { } paging)
        {
            return ExpirySupport.Invalid<ExpiryPolicyHistoryPageDto>($"page must be ≥ 1 and pageSize 1-{ExpiryContract.MaxPageSize}.");
        }

        var (page, size) = paging;
        var result = await _repository.PolicyHistoryAsync(page, size, ct);
        return Response<ExpiryPolicyHistoryPageDto>.Success(new ExpiryPolicyHistoryPageDto(
            result.Items.Select(ExpiryMapping.History).ToList(), result.Total, page, size));
    }
}

/// <summary>
/// §4.18 updatePolicy — PUT, full replacement (pack §4.1 / §12): ranges, critical &lt; approaching, the four SLA types, reason ≥ 10,
/// If-Match (<c>"0"</c> creates the company's first row). Nothing changed → 409 <c>EXPIRY_NO_CHANGES</c>. In ONE transaction: the
/// row, one history row per changed field, one audit row <c>expiry.policy.updated</c>. The same key + the same body answers the
/// current policy (200); the same key + another body → 400 <c>IDEMPOTENCY_KEY_REUSED</c>. The hook modes are only stored here —
/// the F2 hooks read them.
/// </summary>
public sealed class UpdateExpiryPolicyHandler : IRequestHandler<UpdateExpiryPolicyCommand, Response<ExpiryPolicyDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ExpirySupport _support;

    public UpdateExpiryPolicyHandler(IExpiryRepository repository, ExpirySupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<Response<ExpiryPolicyDto>> Handle(UpdateExpiryPolicyCommand request, CancellationToken ct)
    {
        var input = request.Policy;
        var fingerprint = ExpirySupport.Fingerprint(
            ("approachingDays", input.ApproachingDays.ToString()), ("criticalDays", input.CriticalDays.ToString()),
            ("fefoOutboundMode", input.FefoOutboundMode.ToString()), ("belowMinRemainingMode", input.BelowMinRemainingMode.ToString()),
            ("dateOverrideMode", input.DateOverrideMode.ToString()),
            ("slaHours", string.Join(",", input.SlaHours.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}:{p.Value}"))),
            ("expiredOnHandThresholdDays", input.ExpiredOnHandThresholdDays.ToString()), ("reason", request.Reason?.Trim()));

        if (await _repository.FindPolicySaveAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.Detail.GetValueOrDefault("fingerprint") == fingerprint
                ? await CurrentAsync(ct)
                : ExpirySupport.KeyReused<ExpiryPolicyDto>();
        }

        if (ExpiryExceptionRules.Reason(request.Reason) is { } reasonRefusal)
        {
            return ExpirySupport.Fail<ExpiryPolicyDto>(reasonRefusal);
        }

        var stored = await _repository.FindPolicyAsync(ct);
        var before = stored ?? ExpiryPolicyRules.Defaults();
        var after = before.Copy();
        after.ApproachingDays = input.ApproachingDays;
        after.CriticalDays = input.CriticalDays;
        after.FefoOutboundMode = input.FefoOutboundMode;
        after.BelowMinRemainingMode = input.BelowMinRemainingMode;
        after.DateOverrideMode = input.DateOverrideMode;
        after.SlaHours = new Dictionary<string, int>(input.SlaHours, StringComparer.Ordinal);
        after.ExpiredOnHandThresholdDays = input.ExpiredOnHandThresholdDays;

        if (ExpiryPolicyRules.Validate(after) is { } invalid)
        {
            return ExpirySupport.Fail<ExpiryPolicyDto>(invalid);
        }

        if (before.Version != request.ExpectedVersion)
        {
            return ExpirySupport.Conflict<ExpiryPolicyDto>();
        }

        var changes = ExpiryPolicyRules.Changes(before, after);
        if (changes.Count == 0)
        {
            return ExpirySupport.Fail<ExpiryPolicyDto>(ExpiryErrorCodes.NoChanges, "The policy is unchanged.", 409);
        }

        var now = _support.Now();
        var reason = request.Reason!.Trim();
        if (stored is null)
        {
            after.Id = Guid.NewGuid();
            after.CreatedAt = now;
        }

        after.UpdatedAt = now;
        after.UpdatedBy = _support.UserId;
        after.Version = request.ExpectedVersion + 1;

        var history = changes.Select(c => new ExpiryPolicyHistoryEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Field = c.Field,
            OldValue = c.Old,
            NewValue = c.New,
            Reason = reason,
            ChangedBy = _support.UserId,
            ChangedAtUtc = now,
            PolicyVersion = after.Version,
            CorrelationId = _support.CorrelationId
        }).ToList();
        var audit = _support.Audit(ExpiryContract.AuditPolicyUpdated, ExpiryContract.SubjectPolicy, after.Id, null, now, request.IdempotencyKey,
            new Dictionary<string, string?>
            {
                ["fields"] = string.Join(",", changes.Select(c => c.Field)),
                ["reason"] = reason,
                ["version"] = after.Version.ToString(),
                ["fingerprint"] = fingerprint
            });

        bool saved;
        try
        {
            saved = await _repository.InTransactionAsync(async (session, c) =>
            {
                if (!await session.SavePolicyAsync(after, request.ExpectedVersion, c)) return false;
                await session.InsertPolicyHistoryAsync(history, c);
                await session.InsertAuditAsync(audit, c);
                return true;
            }, ct);
        }
        catch (ExpiryDuplicateException)
        {
            // two concurrent first saves of the company: the unique (tenant, company) row admits one
            saved = false;
        }

        return saved
            ? Response<ExpiryPolicyDto>.Success(ExpiryMapping.Policy(after, _support.LegalEntityId, false))
            : ExpirySupport.Conflict<ExpiryPolicyDto>();
    }

    private async Task<Response<ExpiryPolicyDto>> CurrentAsync(CancellationToken ct)
    {
        var stored = await _repository.FindPolicyAsync(ct);
        return Response<ExpiryPolicyDto>.Success(ExpiryMapping.Policy(stored ?? ExpiryPolicyRules.Defaults(), _support.LegalEntityId, stored is null));
    }
}

/// <summary>Paging of the FEFO-BUNDLE lists: page ≥ 1 (default 1), pageSize 1–200 (default 25). Not numbers → null.</summary>
public static class ExpiryPaging
{
    public static (int Page, int PageSize)? Parse(string? page, string? pageSize)
    {
        var p = 1;
        var s = ExpiryContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out p) || p < 1))
            return null;
        if (pageSize is not null && (!int.TryParse(pageSize, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out s)
                                     || s is < 1 or > ExpiryContract.MaxPageSize))
            return null;
        return (p, s);
    }
}
