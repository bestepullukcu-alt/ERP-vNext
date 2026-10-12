using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitEvidence;

// WP-VW-W3 (W3-BE-c, §2.3) — the time-boxed evidence exceptions: a manager writes one for a rep (draft), sends it to the
// sales-operations approval (MOD-0023 template CRM-VISIT-EVIDENCE-EXC, the Work Center), and while it is APPROVED and
// ACTIVE the rep's visit start runs with its rules relaxed (VisitEvidencePolicyResolver). A planned one can be cancelled,
// an active one ended early (its end becomes today). Dates are UTC days.

public sealed record VisitEvidenceExceptionInput(
    string? ResourceId,
    IReadOnlyList<string>? Rules,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Reason,
    int? ExpectedVersion = null);

public sealed record VisitEvidenceExceptionAbilities(
    bool CanEdit, bool CanSubmit, bool CanWithdraw, bool CanDecide, bool CanCancel, bool CanEndEarly);

public sealed record VisitEvidenceExceptionDto(
    Guid Id,
    string ResourceId,
    string? RepDisplayName,
    IReadOnlyList<string> Rules,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    string Reason,
    string Status,
    string? Phase,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt,
    string? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? CancelledBy,
    DateTimeOffset? CancelledAt,
    string? CancelReason,
    IReadOnlyList<ApprovalRoundDto> ReviewRounds,
    IReadOnlyList<ApprovalDecisionDto> Decisions,
    VisitEvidenceExceptionAbilities Abilities,
    int Version)
{
    public static VisitEvidenceExceptionDto From(VisitEvidenceException e, DateOnly today, VisitEvidenceExceptionAbilities abilities)
    {
        var open = e.OpenRound();
        return new VisitEvidenceExceptionDto(
            e.Id, e.ResourceId, e.RepDisplayName, e.Rules.ToList(), e.StartDate, e.EndDate,
            e.EndDate.DayNumber - e.StartDate.DayNumber + 1, e.Reason, e.Status, e.PhaseOn(today),
            e.CreatedBy, e.CreatedAt, e.UpdatedBy, e.UpdatedAt, open?.SubmittedBy, open?.SubmittedAt,
            e.ApprovedBy, e.ApprovedAt, e.CancelledBy, e.CancelledAt, e.CancelReason,
            ApprovalRoundDto.From(e), ApprovalDecisionDto.From(e), abilities, e.Version);
    }
}

/// <summary>The list: the rows after the status / phase filter, and the counts of EVERY status and phase (the tab badges).</summary>
public sealed record VisitEvidenceExceptionListDto(
    IReadOnlyList<VisitEvidenceExceptionDto> Items,
    IReadOnlyDictionary<string, int> StatusCounts,
    IReadOnlyDictionary<string, int> PhaseCounts);

public sealed record ListVisitEvidenceExceptionsQuery(string? Status = null, string? Phase = null)
    : IRequest<Response<VisitEvidenceExceptionListDto>>;

public sealed record GetVisitEvidenceExceptionQuery(Guid Id) : IRequest<Response<VisitEvidenceExceptionDto>>;

public sealed record CreateVisitEvidenceExceptionCommand(VisitEvidenceExceptionInput Input) : IRequest<Response<VisitEvidenceExceptionDto>>;

public sealed record UpdateVisitEvidenceExceptionCommand(Guid Id, VisitEvidenceExceptionInput Input) : IRequest<Response<VisitEvidenceExceptionDto>>;

public sealed record SubmitVisitEvidenceExceptionCommand(Guid Id) : IRequest<Response<VisitEvidenceExceptionDto>>;

public sealed record WithdrawVisitEvidenceExceptionCommand(Guid Id) : IRequest<Response<VisitEvidenceExceptionDto>>;

public sealed record DecideVisitEvidenceExceptionCommand(Guid Id, string? Decision, string? Comment)
    : IRequest<Response<VisitEvidenceExceptionDto>>;

/// <summary>A PLANNED approved exception → <c>cancelled</c> (a reason is required).</summary>
public sealed record CancelVisitEvidenceExceptionCommand(Guid Id, string? Reason) : IRequest<Response<VisitEvidenceExceptionDto>>;

/// <summary>An ACTIVE approved exception → <c>ended_early</c>, its end = today (a reason is required).</summary>
public sealed record EndVisitEvidenceExceptionEarlyCommand(Guid Id, string? Reason) : IRequest<Response<VisitEvidenceExceptionDto>>;

/// <summary>WP-VW-W3 (W3-BE-c) — the pure checks of an exception (dates, rules, reason, overlap).</summary>
public static class VisitEvidenceExceptionChecks
{
    /// <summary>
    /// ≥ 1 known rule (no duplicate); start and end required, UTC today ≤ start ≤ end, at most 30 days both ends included;
    /// a reason ≤ 500. Null = valid.
    /// </summary>
    public static string? Validate(IReadOnlyList<string>? rules, DateOnly? start, DateOnly? end, string? reason, DateOnly today)
    {
        var list = (rules ?? Array.Empty<string>()).Select(r => r?.Trim().ToLowerInvariant() ?? string.Empty).ToList();
        if (list.Count == 0 || list.Any(r => !VisitEvidenceExceptionRules.All.Contains(r)) || list.Distinct().Count() != list.Count)
        {
            return $"At least one rule, each one of: {string.Join(", ", VisitEvidenceExceptionRules.All)} (no repeats).";
        }

        if (start is not { } s || end is not { } e)
        {
            return "A start and an end date are required.";
        }

        if (s < today)
        {
            return "The start date cannot be in the past (UTC today or later).";
        }

        if (e < s)
        {
            return "The end date cannot be before the start date.";
        }

        if (e.DayNumber - s.DayNumber + 1 > VisitEvidenceExceptionLimits.MaxDays)
        {
            return $"An exception lasts at most {VisitEvidenceExceptionLimits.MaxDays} days (both ends included).";
        }

        var text = reason?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > VisitEvidenceExceptionLimits.MaxReasonLength
            ? $"A reason (at most {VisitEvidenceExceptionLimits.MaxReasonLength} characters) is required."
            : null;
    }

    /// <summary>Another in-review / approved exception of the SAME rep relaxing a SAME rule on an overlapping day, or null.</summary>
    public static VisitEvidenceException? Overlapping(VisitEvidenceException candidate, IEnumerable<VisitEvidenceException> others)
        => others.FirstOrDefault(o => o.Id != candidate.Id && !o.IsDeleted && o.Holds()
                                      && string.Equals(o.ResourceId, candidate.ResourceId, StringComparison.Ordinal)
                                      && o.Rules.Intersect(candidate.Rules, StringComparer.Ordinal).Any()
                                      && o.StartDate <= candidate.EndDate && candidate.StartDate <= o.EndDate);

    public static List<string> NormalizeRules(IReadOnlyList<string>? rules)
        => (rules ?? Array.Empty<string>()).Select(r => r.Trim().ToLowerInvariant()).ToList();

    public static Response<T> Invalid<T>(string message) => WorkflowApprovalRules.Fail<T>(VisitEvidenceExceptionErrors.Invalid, message, 400);

    public static Response<T> OverlapFailure<T>(VisitEvidenceException other) => WorkflowApprovalRules.Fail<T>(
        VisitEvidenceExceptionErrors.Overlap,
        $"This rep already has an exception relaxing the same rule between {other.StartDate:yyyy-MM-dd} and {other.EndDate:yyyy-MM-dd} ({other.Status}).",
        409);
}

/// <summary>WP-VW-W3 (W3-BE-c) — the exceptions' shared plumbing (tenant, load + reconcile, abilities, the DTO).</summary>
public sealed class VisitEvidenceExceptionService
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitEvidenceExceptionRepository _exceptions;
    private readonly WorkflowApprovalFlow _flow;
    private readonly WorkflowApprovalReconciler? _reconciler;
    private readonly TimeProvider _clock;

    public VisitEvidenceExceptionService(ITenantContext tenant, IActorContext actor, IVisitEvidenceExceptionRepository exceptions,
        WorkflowApprovalFlow flow, WorkflowApprovalReconciler? reconciler = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _exceptions = exceptions;
        _flow = flow;
        _reconciler = reconciler;
        _clock = clock ?? TimeProvider.System;
    }

    public Guid? TenantId => _tenant.TenantId;
    public string? Actor => _actor.ActorName;
    public WorkflowApprovalFlow Flow => _flow;
    public DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
    public IVisitEvidenceExceptionRepository Repository => _exceptions;

    public async Task<IReadOnlyList<VisitEvidenceException>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var rows = await _exceptions.ListAsync(tenantId, ct);
        if (_reconciler is not null && rows.Any(r => r.OpenRound() is not null)
            && await _reconciler.ReconcileAsync(tenantId, WorkflowApprovalKind.VisitEvidenceException, rows, ct) > 0)
        {
            rows = await _exceptions.ListAsync(tenantId, ct);
        }

        return rows;
    }

    public async Task<(VisitEvidenceException? Record, Response<T>? Error)> LoadAsync<T>(Guid id, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return (null, Response<T>.Fail("Tenant context is required.", 400));
        }

        var record = await _exceptions.GetByIdAsync(tenantId, id, ct);
        if (record is not null && record.OpenRound() is not null && _reconciler is not null
            && await _reconciler.ReconcileAsync(tenantId, WorkflowApprovalKind.VisitEvidenceException, new[] { record }, ct) > 0)
        {
            record = await _exceptions.GetByIdAsync(tenantId, id, ct);
        }

        return record is null
            ? (null, WorkflowApprovalRules.Fail<T>(VisitEvidenceExceptionErrors.NotFound, "Exception not found.", 404))
            : (record, null);
    }

    public async Task<Func<VisitEvidenceException, VisitEvidenceExceptionAbilities>> AbilitiesAsync(
        IEnumerable<VisitEvidenceException> rows, CancellationToken ct)
    {
        var mine = await _flow.MyOpenInstancesAsync(rows, ct);
        var actor = _actor.ActorName;
        var today = Today;
        return e => new VisitEvidenceExceptionAbilities(
            CanEdit: e.IsDraft(),
            CanSubmit: e.IsDraft(),
            CanWithdraw: e.OpenRound() is { } r && Knowledge.Path.Review.KnowledgePathReviewRules.SamePerson(actor, r.SubmittedBy),
            CanDecide: e.OpenRound() is { } open && mine.Contains(open.WorkflowInstanceId) && !WorkflowApprovalRules.IsOwn(e, actor),
            CanCancel: e.PhaseOn(today) == VisitEvidenceExceptionPhases.Planned,
            CanEndEarly: e.PhaseOn(today) == VisitEvidenceExceptionPhases.Active);
    }

    public async Task<Response<VisitEvidenceExceptionDto>> DtoAsync(Guid tenantId, Guid id, int status, CancellationToken ct)
    {
        var record = await _exceptions.GetByIdAsync(tenantId, id, ct);
        if (record is null)
        {
            return WorkflowApprovalRules.Fail<VisitEvidenceExceptionDto>(VisitEvidenceExceptionErrors.NotFound, "Exception not found.", 404);
        }

        await _flow.FillReturnCommentsAsync(record, ct);
        var abilities = await AbilitiesAsync(new[] { record }, ct);
        return Response<VisitEvidenceExceptionDto>.Success(VisitEvidenceExceptionDto.From(record, Today, abilities(record)), status);
    }
}

public sealed class VisitEvidenceExceptionQueryHandlers :
    IRequestHandler<ListVisitEvidenceExceptionsQuery, Response<VisitEvidenceExceptionListDto>>,
    IRequestHandler<GetVisitEvidenceExceptionQuery, Response<VisitEvidenceExceptionDto>>
{
    private readonly VisitEvidenceExceptionService _service;

    public VisitEvidenceExceptionQueryHandlers(VisitEvidenceExceptionService service) => _service = service;

    public async Task<Response<VisitEvidenceExceptionListDto>> Handle(ListVisitEvidenceExceptionsQuery request, CancellationToken ct)
    {
        if (_service.TenantId is not { } tenantId)
        {
            return Response<VisitEvidenceExceptionListDto>.Fail("Tenant context is required.", 400);
        }

        var status = request.Status?.Trim().ToLowerInvariant();
        var phase = request.Phase?.Trim().ToLowerInvariant();
        if ((!string.IsNullOrEmpty(status) && !VisitEvidenceExceptionStatuses.All.Contains(status))
            || (!string.IsNullOrEmpty(phase) && !VisitEvidenceExceptionPhases.All.Contains(phase)))
        {
            return VisitEvidenceExceptionChecks.Invalid<VisitEvidenceExceptionListDto>(
                $"status ∈ {string.Join(", ", VisitEvidenceExceptionStatuses.All)}; phase ∈ {string.Join(", ", VisitEvidenceExceptionPhases.All)}.");
        }

        var rows = await _service.ListAsync(tenantId, ct);
        var today = _service.Today;
        var statusCounts = VisitEvidenceExceptionStatuses.All.ToDictionary(s => s, s => rows.Count(r => r.Status == s));
        var phaseCounts = VisitEvidenceExceptionPhases.All.ToDictionary(p => p, p => rows.Count(r => r.PhaseOn(today) == p));
        var filtered = rows
            .Where(r => string.IsNullOrEmpty(status) || r.Status == status)
            .Where(r => string.IsNullOrEmpty(phase) || r.PhaseOn(today) == phase)
            .ToList();
        var abilities = await _service.AbilitiesAsync(filtered, ct);
        return Response<VisitEvidenceExceptionListDto>.Success(new VisitEvidenceExceptionListDto(
            filtered.Select(r => VisitEvidenceExceptionDto.From(r, today, abilities(r))).ToList(), statusCounts, phaseCounts));
    }

    public async Task<Response<VisitEvidenceExceptionDto>> Handle(GetVisitEvidenceExceptionQuery request, CancellationToken ct)
    {
        var (record, error) = await _service.LoadAsync<VisitEvidenceExceptionDto>(request.Id, ct);
        return error ?? await _service.DtoAsync(record!.TenantId, record.Id, 200, ct);
    }
}

public sealed class VisitEvidenceExceptionCommandHandlers :
    IRequestHandler<CreateVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>,
    IRequestHandler<UpdateVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>,
    IRequestHandler<SubmitVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>,
    IRequestHandler<WithdrawVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>,
    IRequestHandler<DecideVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>,
    IRequestHandler<CancelVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>,
    IRequestHandler<EndVisitEvidenceExceptionEarlyCommand, Response<VisitEvidenceExceptionDto>>
{
    private readonly VisitEvidenceExceptionService _service;
    private readonly IVisitEvidenceRepRoster _reps;
    private readonly WorkflowApprovalOutcomeApplier _applier;
    private readonly IAuthoringApprovalSettings? _settings;
    private readonly TimeProvider _clock;

    public VisitEvidenceExceptionCommandHandlers(VisitEvidenceExceptionService service, IVisitEvidenceRepRoster reps,
        WorkflowApprovalOutcomeApplier applier, IAuthoringApprovalSettings? settings = null, TimeProvider? clock = null)
    {
        _service = service;
        _reps = reps;
        _applier = applier;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
    }

    private IVisitEvidenceExceptionRepository Repository => _service.Repository;

    public async Task<Response<VisitEvidenceExceptionDto>> Handle(CreateVisitEvidenceExceptionCommand request, CancellationToken ct)
    {
        if (_service.TenantId is not { } tenantId)
        {
            return Response<VisitEvidenceExceptionDto>.Fail("Tenant context is required.", 400);
        }

        var record = new VisitEvidenceException
        {
            TenantId = tenantId, Status = VisitEvidenceExceptionStatuses.Draft, CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _service.Actor, Version = 1
        };
        if (await ApplyInputAsync(record, request.Input, ct) is { } failure)
        {
            return failure;
        }

        await Repository.InsertAsync(record, ct);
        return await _service.DtoAsync(tenantId, record.Id, 201, ct);
    }

    public async Task<Response<VisitEvidenceExceptionDto>> Handle(UpdateVisitEvidenceExceptionCommand request, CancellationToken ct)
    {
        var (record, error) = await _service.LoadAsync<VisitEvidenceExceptionDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        if (!record!.IsDraft())
        {
            return WorkflowApprovalRules.Fail<VisitEvidenceExceptionDto>(VisitEvidenceExceptionErrors.NotEditable,
                $"Only a draft exception is edited (status: {record.Status}).", 409);
        }

        if (request.Input.ExpectedVersion is { } ev && ev != record.Version)
        {
            return WorkflowApprovalRules.Conflict<VisitEvidenceExceptionDto>();
        }

        if (await ApplyInputAsync(record, request.Input, ct) is { } failure)
        {
            return failure;
        }

        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.UpdatedBy = _service.Actor;
        return await Repository.ReplaceAsync(record, record.Version, ct)
            ? await _service.DtoAsync(record.TenantId, record.Id, 200, ct)
            : WorkflowApprovalRules.Conflict<VisitEvidenceExceptionDto>();
    }

    public async Task<Response<VisitEvidenceExceptionDto>> Handle(SubmitVisitEvidenceExceptionCommand request, CancellationToken ct)
    {
        var (record, error) = await _service.LoadAsync<VisitEvidenceExceptionDto>(request.Id, ct);
        if (error is not null)
        {
            return error;
        }

        if (!record!.IsDraft())
        {
            return WorkflowApprovalRules.Fail<VisitEvidenceExceptionDto>(VisitEvidenceExceptionErrors.NotEditable,
                $"Only a draft exception can be sent for approval (status: {record.Status}).", 409);
        }

        // The dates are checked again on submit (a draft written yesterday may start in the past today).
        if (VisitEvidenceExceptionChecks.Validate(record.Rules, record.StartDate, record.EndDate, record.Reason, _service.Today) is { } invalid)
        {
            return VisitEvidenceExceptionChecks.Invalid<VisitEvidenceExceptionDto>(invalid);
        }

        if (VisitEvidenceExceptionChecks.Overlapping(record, await Repository.ListByResourceAsync(record.TenantId, record.ResourceId, ct)) is { } other)
        {
            return VisitEvidenceExceptionChecks.OverlapFailure<VisitEvidenceExceptionDto>(other);
        }

        var rules = string.Join(", ", record.Rules);
        var display = new ClaimWorkflowDisplayContext(
            ClaimReviewRules.Truncate($"Ziyaret kanıtı istisnası · {record.RepDisplayName ?? record.ResourceId}", 200),
            ClaimReviewRules.Truncate($"{rules} · {record.StartDate:yyyy-MM-dd} – {record.EndDate:yyyy-MM-dd} · {record.Reason}", 300),
            "crm",
            WorkflowApprovalKind.VisitEvidenceException.Link(record.Id),
            record.Rules.Select(r => ClaimReviewRules.Truncate(r, 32))
                .Append($"{record.EndDate.DayNumber - record.StartDate.DayNumber + 1} gün").ToList());
        var templateCode = _settings?.VisitEvidenceExceptionTemplateCode ?? AuthoringApprovalDefaults.VisitEvidenceExceptionTemplateCode;
        return await _service.Flow.SubmitAsync<VisitEvidenceException, VisitEvidenceExceptionDto>(
                   _applier.Exceptions, record, WorkflowApprovalKind.VisitEvidenceException, templateCode, display,
                   VisitEvidenceExceptionStatuses.InReview, ct)
               ?? await _service.DtoAsync(record.TenantId, record.Id, 200, ct);
    }

    public async Task<Response<VisitEvidenceExceptionDto>> Handle(WithdrawVisitEvidenceExceptionCommand request, CancellationToken ct)
    {
        var (record, error) = await _service.LoadAsync<VisitEvidenceExceptionDto>(request.Id, ct);
        return error
               ?? await _service.Flow.WithdrawAsync<VisitEvidenceException, VisitEvidenceExceptionDto>(
                   _applier.Exceptions, record!, WorkflowApprovalKind.VisitEvidenceException, ct)
               ?? await _service.DtoAsync(record!.TenantId, record.Id, 200, ct);
    }

    public async Task<Response<VisitEvidenceExceptionDto>> Handle(DecideVisitEvidenceExceptionCommand request, CancellationToken ct)
    {
        var (record, error) = await _service.LoadAsync<VisitEvidenceExceptionDto>(request.Id, ct);
        return error
               ?? await _service.Flow.DecideAsync<VisitEvidenceException, VisitEvidenceExceptionDto>(
                   _applier.Exceptions, record!, WorkflowApprovalKind.VisitEvidenceException, request.Decision, request.Comment,
                   VisitEvidenceExceptionErrors.SeparationOfDuties, ct)
               ?? await _service.DtoAsync(record!.TenantId, record.Id, 200, ct);
    }

    public Task<Response<VisitEvidenceExceptionDto>> Handle(CancelVisitEvidenceExceptionCommand request, CancellationToken ct)
        => CloseAsync(request.Id, request.Reason, VisitEvidenceExceptionPhases.Planned, VisitEvidenceExceptionStatuses.Cancelled, ct);

    public Task<Response<VisitEvidenceExceptionDto>> Handle(EndVisitEvidenceExceptionEarlyCommand request, CancellationToken ct)
        => CloseAsync(request.Id, request.Reason, VisitEvidenceExceptionPhases.Active, VisitEvidenceExceptionStatuses.EndedEarly, ct);

    private async Task<Response<VisitEvidenceExceptionDto>> CloseAsync(
        Guid id, string? reason, string requiredPhase, string newStatus, CancellationToken ct)
    {
        var text = reason?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > VisitEvidenceExceptionLimits.MaxReasonLength)
        {
            return VisitEvidenceExceptionChecks.Invalid<VisitEvidenceExceptionDto>(
                $"A reason (at most {VisitEvidenceExceptionLimits.MaxReasonLength} characters) is required.");
        }

        var (record, error) = await _service.LoadAsync<VisitEvidenceExceptionDto>(id, ct);
        if (error is not null)
        {
            return error;
        }

        var today = _service.Today;
        if (record!.PhaseOn(today) != requiredPhase)
        {
            return WorkflowApprovalRules.Fail<VisitEvidenceExceptionDto>(VisitEvidenceExceptionErrors.NotEditable,
                requiredPhase == VisitEvidenceExceptionPhases.Planned
                    ? "Only an approved exception that has not started yet can be cancelled."
                    : "Only an approved exception that is active today can be ended early.", 409);
        }

        var now = _clock.GetUtcNow();
        record.Status = newStatus;
        if (newStatus == VisitEvidenceExceptionStatuses.EndedEarly)
        {
            record.EndDate = today;
        }

        record.CancelledAt = now;
        record.CancelledBy = _service.Actor;
        record.CancelReason = text;
        record.UpdatedAt = now;
        record.UpdatedBy = _service.Actor;
        return await Repository.ReplaceAsync(record, record.Version, ct)
            ? await _service.DtoAsync(record.TenantId, record.Id, 200, ct)
            : WorkflowApprovalRules.Conflict<VisitEvidenceExceptionDto>();
    }

    /// <summary>Checks the input (rules, dates, reason; the rep is an active primary territory resource; no overlap with an
    /// in-review / approved exception) and writes it onto <paramref name="record"/>. Null = written.</summary>
    private async Task<Response<VisitEvidenceExceptionDto>?> ApplyInputAsync(
        VisitEvidenceException record, VisitEvidenceExceptionInput input, CancellationToken ct)
    {
        if (VisitEvidenceExceptionChecks.Validate(input.Rules, input.StartDate, input.EndDate, input.Reason, _service.Today) is { } invalid)
        {
            return VisitEvidenceExceptionChecks.Invalid<VisitEvidenceExceptionDto>(invalid);
        }

        var resourceId = input.ResourceId?.Trim();
        var rep = string.IsNullOrEmpty(resourceId)
            ? null
            : (await _reps.ListAsync(record.TenantId, _clock.GetUtcNow(), ct)).FirstOrDefault(r => r.ResourceId == resourceId);
        if (rep is null)
        {
            return WorkflowApprovalRules.Fail<VisitEvidenceExceptionDto>(VisitEvidenceExceptionErrors.RepUnknown,
                "The rep has no active primary territory assignment.", 400);
        }

        record.ResourceId = rep.ResourceId;
        record.RepDisplayName = rep.DisplayName;
        record.Rules = VisitEvidenceExceptionChecks.NormalizeRules(input.Rules);
        record.StartDate = input.StartDate!.Value;
        record.EndDate = input.EndDate!.Value;
        record.Reason = input.Reason!.Trim();

        return VisitEvidenceExceptionChecks.Overlapping(record, await Repository.ListByResourceAsync(record.TenantId, record.ResourceId, ct)) is { } other
            ? VisitEvidenceExceptionChecks.OverlapFailure<VisitEvidenceExceptionDto>(other)
            : null;
    }
}
