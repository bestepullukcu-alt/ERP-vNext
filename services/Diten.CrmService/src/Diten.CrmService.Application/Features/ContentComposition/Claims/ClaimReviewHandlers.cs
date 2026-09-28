using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

// WP-CL-BE-4 — submit / withdraw a review round and read its history.

/// <summary>One step row of a round, from MOD-0023's task list. Platform exposes no approver comment on this read.</summary>
public sealed record ClaimReviewStepDto(
    string StageCode,
    string StepCode,
    string Status,
    string? AssigneeRef,
    string? ActionedBy,
    string? ActionReasonCode,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt);

public sealed record ClaimReviewRoundDto(
    int RoundNo,
    Guid WorkflowInstanceId,
    string? TemplateCode,
    DateTimeOffset SubmittedAt,
    string? SubmittedBy,
    string? Outcome,
    DateTimeOffset? ClosedAt,
    string? CompletedBy,
    string? ReasonCode,
    bool IsOpen,
    IReadOnlyList<ClaimReviewStepDto>? Steps = null,
    bool StepsAvailable = false);

public sealed record GetClaimReviewHistoryQuery(Guid ClaimId) : IRequest<Response<IReadOnlyList<ClaimReviewRoundDto>>>;

public sealed record GetClaimCountryVersionReviewHistoryQuery(Guid CountryVersionId)
    : IRequest<Response<IReadOnlyList<ClaimReviewRoundDto>>>;

internal static class ClaimReviewMapping
{
    public static ClaimReviewRoundDto ToDto(ClaimReviewRound r, IReadOnlyList<ClaimReviewStepDto>? steps = null,
        bool stepsAvailable = false) => new(
        r.RoundNo, r.WorkflowInstanceId, r.TemplateCode, r.SubmittedAt, r.SubmittedBy, r.Outcome, r.ClosedAt,
        r.CompletedBy, r.ReasonCode, r.IsOpen(), steps, stepsAvailable);

    public static Response<T> FromStart<T>(ClaimWorkflowStartResult result) => result.Outcome switch
    {
        ClaimWorkflowCallOutcome.TemplateMissing => Fail<T>(ClaimErrorCodes.ApprovalTemplateMissing,
            "The approval workflow template is not available (not created or not published).", 409),
        ClaimWorkflowCallOutcome.Forbidden => Fail<T>(ClaimErrorCodes.ApprovalForbidden,
            "You are not allowed to start the approval workflow.", 403),
        ClaimWorkflowCallOutcome.Rejected => Fail<T>(ClaimErrorCodes.WorkflowRequestRejected,
            $"The approval workflow refused the request: {result.Detail}", 400),
        _ => Fail<T>(ClaimErrorCodes.WorkflowUnavailable,
            "The approval workflow service cannot be reached. Nothing was changed.", 503)
    };

    public static Response<T> Fail<T>(string code, string message, int status) =>
        Response<T>.Fail(new[] { code, message }, status);
}

/// <summary>
/// Sends a DRAFT core/local claim to MOD-0023. The round is written only after MOD-0023 accepted the start: any failure
/// leaves the claim exactly as it was. Re-sending after a lost answer reuses the same idempotency key, so MOD-0023
/// returns the same instance instead of starting a second one.
/// </summary>
public sealed class SubmitClaimReviewHandler : IRequestHandler<SubmitClaimReviewCommand, Response<ClaimReviewRoundDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimWorkflowClient _workflow;
    private readonly IClaimWorkflowSettings? _settings;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimEvidenceClient? _evidence;

    public SubmitClaimReviewHandler(ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimWorkflowClient workflow, IClaimWorkflowSettings? settings = null,
        IContentCompositionAuditPublisher? audit = null, IClaimEvidenceClient? evidence = null)
    {
        _evidence = evidence;
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _workflow = workflow;
        _settings = settings;
        _audit = audit;
    }

    public async Task<Response<ClaimReviewRoundDto>> Handle(SubmitClaimReviewCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimReviewRoundDto>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, ct);
        if (claim is null)
        {
            return Response<ClaimReviewRoundDto>.Fail("Claim not found.", 404);
        }

        if (claim.IsArchived() || claim.Status != ClaimStatuses.Draft)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.InvalidStatus,
                $"Only a draft claim can be sent for review (status: {claim.Status}).", 409);
        }

        if (string.IsNullOrWhiteSpace(claim.ClaimText))
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.TextsRequired, "The claim text is empty.", 400);
        }

        if (claim.ProductId is null)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.ProductRequired,
                "A claim needs its product before it is sent for review.", 400);
        }

        // WP-CL-BE-5 — at least one active evidence link (after the BE-4 rules, before MOD-0023 is called).
        if (await ClaimEvidenceGate.RequireAsync<ClaimReviewRoundDto>(_evidence,
                [ClaimEvidenceRules.ReadKey(ClaimEvidenceRules.ClaimObjectType, claim.Id)], ct) is { } noEvidence)
        {
            return noEvidence;
        }

        var templateCode = claim.IsLocal()
            ? string.Format(_settings?.LocalTemplateCodeFormat ?? ClaimWorkflowDefaults.LocalTemplateCodeFormat,
                claim.LocalCountryCode)
            : _settings?.CoreTemplateCode ?? ClaimWorkflowDefaults.CoreTemplateCode;
        var roundNo = ClaimReviewRules.NextRoundNo(claim.ReviewRounds);
        var kind = string.IsNullOrWhiteSpace(claim.Kind) ? ClaimKinds.Core : claim.Kind;
        var start = await _workflow.StartAsync(new ClaimWorkflowStartRequest(
            templateCode,
            ClaimReviewRules.ClaimObjectType,
            claim.Id.ToString("D"),
            ClaimReviewRules.ObjectRef(ClaimReviewRules.ClaimObjectType, claim.Id),
            ClaimReviewRules.IdempotencyKey(ClaimReviewRules.ClaimObjectType, claim.Id, roundNo),
            new ClaimWorkflowDisplayContext(
                ClaimReviewRules.Truncate($"İddia onayı · {claim.ClaimCode} v{claim.ClaimVersion}", 200),
                ClaimReviewRules.Truncate(claim.ClaimName, 300),
                "crm",
                $"/CRM/Claims/Details/{claim.Id:D}",
                [ClaimReviewRules.Truncate(kind, 32), ClaimReviewRules.Truncate($"v{claim.ClaimVersion}", 32)])), ct);
        if (start.Outcome != ClaimWorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return ClaimReviewMapping.FromStart<ClaimReviewRoundDto>(start);
        }

        var now = DateTimeOffset.UtcNow;
        var round = new ClaimReviewRound
        {
            WorkflowInstanceId = instanceId, SubmittedAt = now, SubmittedBy = _actor.ActorName, RoundNo = roundNo,
            TemplateCode = templateCode
        };
        claim.ReviewRounds.Add(round);
        claim.Status = ClaimStatuses.InReview;
        claim.UpdatedAt = now;
        claim.UpdatedBy = _actor.ActorName;
        await _claims.UpdateAsync(claim, ct);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.ReviewSubmitted, tenantId, ContentCompositionAuditEntities.Claim,
                claim.Id, claim.Version, $"{claim.ClaimCode}|r{roundNo}", ct);
        }

        return Response<ClaimReviewRoundDto>.Success(ClaimReviewMapping.ToDto(round), 201);
    }
}

/// <summary>
/// Sends a DRAFT country version to its country's MOD-0023 workflow. The WP-CL-BE-1 temporary approve checks live
/// here now: the bound core must be approved (core kind), every content language of the country needs a text, and the
/// claim × country cell must not be closed.
/// </summary>
public sealed class SubmitClaimCountryVersionReviewHandler
    : IRequestHandler<SubmitClaimCountryVersionReviewCommand, Response<ClaimReviewRoundDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IClaimWorkflowClient _workflow;
    private readonly IReferenceMetadataReader? _metadata;
    private readonly IClaimWorkflowSettings? _settings;
    private readonly IContentCompositionAuditPublisher? _audit;
    private readonly IClaimEvidenceClient? _evidence;

    public SubmitClaimCountryVersionReviewHandler(ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimCountryVersionRepository countryVersions, IClaimWorkflowClient workflow,
        IReferenceMetadataReader? metadata = null, IClaimWorkflowSettings? settings = null,
        IContentCompositionAuditPublisher? audit = null, IClaimEvidenceClient? evidence = null)
    {
        _evidence = evidence;
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _countryVersions = countryVersions;
        _workflow = workflow;
        _metadata = metadata;
        _settings = settings;
        _audit = audit;
    }

    public async Task<Response<ClaimReviewRoundDto>> Handle(SubmitClaimCountryVersionReviewCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimReviewRoundDto>.Fail("Tenant context is required.", 400);
        }

        var version = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, ct);
        if (version is null)
        {
            return Response<ClaimReviewRoundDto>.Fail("Country version not found.", 404);
        }

        if (version.IsArchived() || version.Status != ClaimStatuses.Draft)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.InvalidStatus,
                $"Only a draft country version can be sent for review (status: {version.Status}).", 409);
        }

        var claim = await _claims.GetByIdAsync(tenantId, version.ClaimId, ct);
        if (claim is null)
        {
            return Response<ClaimReviewRoundDto>.Fail("Bound claim not found.", 404);
        }

        if (!claim.IsLocal() && !claim.IsApproved())
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.CoreNotApproved,
                $"The bound core version {version.BoundCoreVersion} is not approved.", 409);
        }

        var records = await _claims.ListByCodeAsync(tenantId, claim.ClaimCode, ct);
        var current = ClaimLines.Current(records) ?? claim;
        if (current.ActiveClosureFor(version.CountryCode) is not null)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.CountryClosed,
                $"'{version.CountryCode}' is closed for this claim; reopen it first.", 409);
        }

        var (languages, languageFailure) = await ClaimV2Checks.GetCountryLanguagesAsync(_metadata, version.CountryCode, ct);
        if (languageFailure is not null)
        {
            return languageFailure.To<ClaimReviewRoundDto>();
        }

        var missing = languages.Where(l => version.Texts.All(t => t.LanguageCode != l)).ToList();
        if (missing.Count > 0)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.LanguagesIncomplete,
                $"Every content language of '{version.CountryCode}' needs a text; missing: {string.Join(", ", missing)}.",
                400);
        }

        // WP-CL-BE-5 — at least one active effective evidence link: inherited (bound claim record) + own.
        if (await ClaimEvidenceGate.RequireAsync<ClaimReviewRoundDto>(_evidence,
            [
                ClaimEvidenceRules.ReadKey(ClaimEvidenceRules.CountryVersionObjectType, version.Id),
                ClaimEvidenceRules.ReadKey(ClaimEvidenceRules.ClaimObjectType, version.ClaimId)
            ], ct) is { } noEvidence)
        {
            return noEvidence;
        }

        var templateCode = string.Format(
            _settings?.LocalTemplateCodeFormat ?? ClaimWorkflowDefaults.LocalTemplateCodeFormat, version.CountryCode);
        var roundNo = ClaimReviewRules.NextRoundNo(version.ReviewRounds);
        var start = await _workflow.StartAsync(new ClaimWorkflowStartRequest(
            templateCode,
            ClaimReviewRules.CountryVersionObjectType,
            version.Id.ToString("D"),
            ClaimReviewRules.ObjectRef(ClaimReviewRules.CountryVersionObjectType, version.Id),
            ClaimReviewRules.IdempotencyKey(ClaimReviewRules.CountryVersionObjectType, version.Id, roundNo),
            new ClaimWorkflowDisplayContext(
                ClaimReviewRules.Truncate(
                    $"İddia ülke onayı · {version.ClaimCode} · {version.CountryCode} v{version.CountryVersion}", 200),
                ClaimReviewRules.Truncate(claim.ClaimName, 300),
                "crm",
                $"/CRM/Claims/Details/{claim.Id:D}?country={version.CountryCode}",
                [version.CountryCode, ClaimReviewRules.Truncate($"v{version.CountryVersion}", 32)])), ct);
        if (start.Outcome != ClaimWorkflowCallOutcome.Ok || start.WorkflowInstanceId is not { } instanceId)
        {
            return ClaimReviewMapping.FromStart<ClaimReviewRoundDto>(start);
        }

        var now = DateTimeOffset.UtcNow;
        var round = new ClaimReviewRound
        {
            WorkflowInstanceId = instanceId, SubmittedAt = now, SubmittedBy = _actor.ActorName, RoundNo = roundNo,
            TemplateCode = templateCode
        };
        version.ReviewRounds.Add(round);
        version.Status = ClaimStatuses.InReview;
        version.UpdatedAt = now;
        version.UpdatedBy = _actor.ActorName;
        await _countryVersions.UpdateAsync(version, ct);
        if (_audit is not null)
        {
            await _audit.PublishAsync(ClaimReasonCodes.ReviewSubmitted, tenantId,
                ContentCompositionAuditEntities.ClaimCountryVersion, version.Id, version.Version,
                $"{version.ClaimCode}|{version.CountryCode}|r{roundNo}", ct);
        }

        return Response<ClaimReviewRoundDto>.Success(ClaimReviewMapping.ToDto(round), 201);
    }
}

/// <summary>Shared withdraw flow: find the open round's waiting task in MOD-0023, cancel it as the caller, then apply
/// <c>cancelled</c> through the applier (the completion event that follows finds no open round and is ignored).</summary>
internal static class ClaimReviewWithdrawal
{
    public static async Task<Response<ClaimReviewRoundDto>> WithdrawAsync(
        Guid tenantId, string objectType, Guid objectId, ClaimReviewRound? round, string? actor,
        IClaimWorkflowClient workflow, ClaimReviewOutcomeApplier applier, IContentCompositionAuditPublisher? audit,
        string auditEntity, int entityVersion, string auditDetail, CancellationToken ct)
    {
        if (round is null)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.NoOpenReview,
                "There is no open review round to withdraw.", 409);
        }

        var tasks = await workflow.GetTasksAsync([round.WorkflowInstanceId], ct);
        if (tasks is null)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.WorkflowUnavailable,
                "The approval workflow service cannot be reached. Nothing was changed.", 503);
        }

        var open = tasks.Where(t => t.WorkflowInstanceId == round.WorkflowInstanceId && t.IsOpen)
            .OrderByDescending(t => t.DueAt ?? DateTimeOffset.MinValue).FirstOrDefault();
        if (open is null)
        {
            return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.WithdrawNotPossible,
                "The workflow has no waiting step to cancel (it may have just finished).", 409);
        }

        var cancel = await workflow.CancelTaskAsync(open.TaskId, string.IsNullOrWhiteSpace(actor) ? "crm-user" : actor,
            ClaimReviewRules.WithdrawReasonCode,
            $"{ClaimReviewRules.IdempotencyKey(objectType, objectId, round.RoundNo)}:withdraw", ct);
        switch (cancel)
        {
            case ClaimWorkflowCallOutcome.Ok:
                break;
            case ClaimWorkflowCallOutcome.Forbidden:
                return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.ApprovalForbidden,
                    "You are not allowed to cancel this approval.", 403);
            case ClaimWorkflowCallOutcome.Unavailable:
                return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.WorkflowUnavailable,
                    "The approval workflow service cannot be reached. Nothing was changed.", 503);
            default:
                return ClaimReviewMapping.Fail<ClaimReviewRoundDto>(ClaimErrorCodes.WithdrawNotPossible,
                    "The approval workflow refused to cancel the waiting step.", 409);
        }

        await applier.ApplyAsync(tenantId, objectType, objectId, round.WorkflowInstanceId, ClaimReviewOutcomes.Cancelled,
            actor, ClaimReviewRules.WithdrawReasonCode, DateTimeOffset.UtcNow, ct);
        if (audit is not null)
        {
            await audit.PublishAsync(ClaimReasonCodes.ReviewWithdrawn, tenantId, auditEntity, objectId, entityVersion,
                auditDetail, ct);
        }

        return Response<ClaimReviewRoundDto>.Success(ClaimReviewMapping.ToDto(round));
    }
}

public sealed class WithdrawClaimReviewHandler : IRequestHandler<WithdrawClaimReviewCommand, Response<ClaimReviewRoundDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimRepository _claims;
    private readonly IClaimWorkflowClient _workflow;
    private readonly ClaimReviewOutcomeApplier _applier;
    private readonly IContentCompositionAuditPublisher? _audit;

    public WithdrawClaimReviewHandler(ITenantContext tenant, IActorContext actor, IClaimRepository claims,
        IClaimWorkflowClient workflow, ClaimReviewOutcomeApplier applier, IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _claims = claims;
        _workflow = workflow;
        _applier = applier;
        _audit = audit;
    }

    public async Task<Response<ClaimReviewRoundDto>> Handle(WithdrawClaimReviewCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimReviewRoundDto>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, ct);
        if (claim is null)
        {
            return Response<ClaimReviewRoundDto>.Fail("Claim not found.", 404);
        }

        var round = claim.Status == ClaimStatuses.InReview ? ClaimReviewRules.OpenRound(claim.ReviewRounds) : null;
        return await ClaimReviewWithdrawal.WithdrawAsync(tenantId, ClaimReviewRules.ClaimObjectType, claim.Id, round,
            _actor.ActorName, _workflow, _applier, _audit, ContentCompositionAuditEntities.Claim, claim.Version,
            $"{claim.ClaimCode}|r{round?.RoundNo}", ct);
    }
}

public sealed class WithdrawClaimCountryVersionReviewHandler
    : IRequestHandler<WithdrawClaimCountryVersionReviewCommand, Response<ClaimReviewRoundDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IClaimWorkflowClient _workflow;
    private readonly ClaimReviewOutcomeApplier _applier;
    private readonly IContentCompositionAuditPublisher? _audit;

    public WithdrawClaimCountryVersionReviewHandler(ITenantContext tenant, IActorContext actor,
        IClaimCountryVersionRepository countryVersions, IClaimWorkflowClient workflow, ClaimReviewOutcomeApplier applier,
        IContentCompositionAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _countryVersions = countryVersions;
        _workflow = workflow;
        _applier = applier;
        _audit = audit;
    }

    public async Task<Response<ClaimReviewRoundDto>> Handle(WithdrawClaimCountryVersionReviewCommand request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimReviewRoundDto>.Fail("Tenant context is required.", 400);
        }

        var version = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, ct);
        if (version is null)
        {
            return Response<ClaimReviewRoundDto>.Fail("Country version not found.", 404);
        }

        var round = version.Status == ClaimStatuses.InReview ? ClaimReviewRules.OpenRound(version.ReviewRounds) : null;
        return await ClaimReviewWithdrawal.WithdrawAsync(tenantId, ClaimReviewRules.CountryVersionObjectType,
            version.Id, round, _actor.ActorName, _workflow, _applier, _audit,
            ContentCompositionAuditEntities.ClaimCountryVersion, version.Version,
            $"{version.ClaimCode}|{version.CountryCode}|r{round?.RoundNo}", ct);
    }
}

/// <summary>Rounds (newest first) with their MOD-0023 step rows. When MOD-0023 cannot be read the rounds are still
/// returned, with <c>StepsAvailable = false</c>.</summary>
internal static class ClaimReviewHistory
{
    public static async Task<IReadOnlyList<ClaimReviewRoundDto>> BuildAsync(
        IReadOnlyList<ClaimReviewRound> rounds, IClaimWorkflowClient? workflow, CancellationToken ct)
    {
        IReadOnlyList<ClaimWorkflowTaskState>? tasks = null;
        if (workflow is not null && rounds.Count > 0)
        {
            try
            {
                tasks = await workflow.GetTasksAsync(rounds.Select(r => r.WorkflowInstanceId).ToList(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                tasks = null;
            }
        }

        return rounds.OrderByDescending(r => r.RoundNo).Select(r => tasks is null
                ? ClaimReviewMapping.ToDto(r)
                : ClaimReviewMapping.ToDto(r, tasks.Where(t => t.WorkflowInstanceId == r.WorkflowInstanceId)
                    .OrderBy(t => t.StageCode, StringComparer.Ordinal).ThenBy(t => t.StepCode, StringComparer.Ordinal)
                    .Select(t => new ClaimReviewStepDto(t.StageCode, t.StepCode, t.Status, t.AssigneeRef, t.ActionedBy,
                        t.ActionReasonCode, t.DueAt, t.CompletedAt))
                    .ToList(), stepsAvailable: true))
            .ToList();
    }
}

public sealed class GetClaimReviewHistoryHandler
    : IRequestHandler<GetClaimReviewHistoryQuery, Response<IReadOnlyList<ClaimReviewRoundDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimWorkflowClient? _workflow;

    public GetClaimReviewHistoryHandler(ITenantContext tenant, IClaimRepository claims, IClaimWorkflowClient? workflow = null)
    {
        _tenant = tenant;
        _claims = claims;
        _workflow = workflow;
    }

    public async Task<Response<IReadOnlyList<ClaimReviewRoundDto>>> Handle(GetClaimReviewHistoryQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<ClaimReviewRoundDto>>.Fail("Tenant context is required.", 400);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, ct);
        return claim is null
            ? Response<IReadOnlyList<ClaimReviewRoundDto>>.Fail("Claim not found.", 404)
            : Response<IReadOnlyList<ClaimReviewRoundDto>>.Success(
                await ClaimReviewHistory.BuildAsync(claim.ReviewRounds, _workflow, ct));
    }
}

public sealed class GetClaimCountryVersionReviewHistoryHandler
    : IRequestHandler<GetClaimCountryVersionReviewHistoryQuery, Response<IReadOnlyList<ClaimReviewRoundDto>>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimCountryVersionRepository _countryVersions;
    private readonly IClaimWorkflowClient? _workflow;

    public GetClaimCountryVersionReviewHistoryHandler(ITenantContext tenant,
        IClaimCountryVersionRepository countryVersions, IClaimWorkflowClient? workflow = null)
    {
        _tenant = tenant;
        _countryVersions = countryVersions;
        _workflow = workflow;
    }

    public async Task<Response<IReadOnlyList<ClaimReviewRoundDto>>> Handle(
        GetClaimCountryVersionReviewHistoryQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<IReadOnlyList<ClaimReviewRoundDto>>.Fail("Tenant context is required.", 400);
        }

        var version = await _countryVersions.GetByIdAsync(tenantId, request.CountryVersionId, ct);
        return version is null
            ? Response<IReadOnlyList<ClaimReviewRoundDto>>.Fail("Country version not found.", 404)
            : Response<IReadOnlyList<ClaimReviewRoundDto>>.Success(
                await ClaimReviewHistory.BuildAsync(version.ReviewRounds, _workflow, ct));
    }
}
