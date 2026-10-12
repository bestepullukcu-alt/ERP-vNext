using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Artifacts;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using ReadAll = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions;

namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>WP-VW-W3 (BE-a) — the ownership + loading rules every execution handler shares, so they answer alike.</summary>
internal static class VisitExecutionAccess
{
    public static Response<T> Fail<T>(VisitExecutionRules.Failure failure) => Response<T>.Fail(failure.Errors, failure.StatusCode);

    public static VisitExecutionRules.Failure PlanNotFound()
        => new("The planned visit does not exist.", Features.VisitReport.Contract.VisitReportErrorCodes.PlannedVisitNotFound, 404);

    public static VisitExecutionRules.Failure ExecutionNotFound()
        => new("The visit execution does not exist.", VisitExecutionErrorCodes.NotFound, 404);

    /// <summary>A READ: the caller's own visit, or any visit with <c>crm.planned-visit.read-all</c>; else 404.</summary>
    public static bool MayRead(ICallerScope caller, PlanAtom plan)
        => caller.MayAccess(ReadAll.ReadAll, plan.Resource.ResourceId);

    /// <summary>A WRITE (start / capture / finish / signature): only the visit's own rep. Another rep's visit is 404
    /// (as absent) for a rep, and 403 <c>resource_not_caller</c> for a read-all holder — evidence is never recorded on
    /// someone else's behalf.</summary>
    public static VisitExecutionRules.Failure? WriteRefusal(ICallerScope caller, PlanAtom plan)
    {
        if (!MayRead(caller, plan))
        {
            return PlanNotFound();
        }

        return caller.CallerResourceId is { } me && string.Equals(me, plan.Resource.ResourceId, StringComparison.Ordinal)
            ? null
            : new VisitExecutionRules.Failure(
                "Only the visit's own rep records its execution.", VisitOwnership.ResourceNotCaller, 403);
    }

    public static async Task<(VisitEvidencePolicy Policy, bool IsDefault)> PolicyAsync(
        IVisitEvidencePolicyRepository policies, Guid tenantId, CancellationToken cancellationToken)
    {
        var stored = await policies.GetAsync(tenantId, cancellationToken);
        return stored is null ? (VisitExecutionRules.Defaults(tenantId), true) : (stored, false);
    }

    /// <summary>The report's state against a new start: a submitted report, or a recorded missed / rescheduled
    /// outcome, means the visit is already decided.</summary>
    public static bool IsReported(Domain.Entities.VisitReport? report)
        => report is not null && (report.IsFinalised() || !report.IsCompleted());
}

/// <summary>
/// WP-VW-W3 (BE-a) — <c>POST api/crm/visit-execution/start</c>. Rules in order: the rep's own visit; an active plan
/// (<c>planned</c> | <c>confirmed</c>); its day is TODAY (the W1 UTC day); not started; not reported; the 48 h lock;
/// the location rule (<see cref="VisitExecutionRules.EvaluateLocation"/>); the photo rule. Then the photo is stored
/// FIRST (fail-closed: a store failure → 503, nothing written), the record second; a record that cannot be written
/// compensates the stored photo. <c>StartedAt</c> is the SERVER clock — no client instant is accepted.
/// <para>WP-VW-W3 (W3-BE-c) — the rules are the rep's EFFECTIVE ones (<see cref="IVisitEvidencePolicyResolver"/>: the
/// tenant policy ⊕ an approved exception active today). A start under an exception records it: flag
/// <c>started_with_exception</c>, <c>ExceptionId</c>, <c>RelaxedRules</c>.</para>
/// </summary>
public sealed class StartVisitExecutionHandler : IRequestHandler<StartVisitExecutionCommand, Response<VisitExecutionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IVisitExecutionRepository _executions;
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly IAccountRepository _accounts;
    private readonly IVisitEvidenceStore _store;
    private readonly TimeProvider _clock;
    private readonly IVisitEvidencePolicyResolver _resolver;

    public StartVisitExecutionHandler(
        ITenantContext tenant, IActorContext actor, ICallerScope caller,
        IPlannedVisitRepository plans, IVisitReportRepository reports, IVisitExecutionRepository executions,
        IVisitEvidencePolicyRepository policies, IAccountRepository accounts, IVisitEvidenceStore store,
        TimeProvider? clock = null, IVisitEvidencePolicyResolver? resolver = null)
    {
        _resolver = resolver ?? new VisitEvidencePolicyResolver(policies);
        _tenant = tenant;
        _actor = actor;
        _caller = caller;
        _plans = plans;
        _reports = reports;
        _executions = executions;
        _policies = policies;
        _accounts = accounts;
        _store = store;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<VisitExecutionDto>> Handle(StartVisitExecutionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitExecutionDto>.Fail("Tenant context is required.", 400);
        }

        var plan = request.PlannedVisitId == Guid.Empty
            ? null
            : await _plans.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(VisitExecutionAccess.PlanNotFound());
        }

        if (VisitExecutionAccess.WriteRefusal(_caller, plan) is { } ownership)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(ownership);
        }

        if (request.Kind is not null && !VisitExecutionKinds.IsKnown(request.Kind))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(
                VisitExecutionRules.Capture($"kind must be one of: {string.Join(", ", VisitExecutionKinds.All)}."));
        }

        if (VisitExecutionRules.ValidateCoordinates(request.Latitude, request.Longitude, request.AccuracyMeters, request.LocationError)
            is { } coordinateFailure)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(coordinateFailure);
        }

        // An approved week writes its visits as `planned` (the engine, unplanned and reschedule do too); `confirmed` is
        // the optional manual confirm. A draft or cancelled plan is not startable.
        if (!plan.IsActivePlan())
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "Only an approved visit can be started.", VisitExecutionErrorCodes.NotConfirmed, 409));
        }

        var now = _clock.GetUtcNow();
        if (plan.PlannedDate != VisitReportValidation.Today(_clock))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "A visit is started on its own day only.", VisitExecutionErrorCodes.NotToday, 409));
        }

        if (await _executions.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken) is not null)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(AlreadyStarted());
        }

        var report = await _reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken);
        if (VisitExecutionAccess.IsReported(report))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "This visit already has its outcome.", VisitExecutionErrorCodes.AlreadyReported, 409));
        }

        if (VisitReportDeadline.IsPassed(plan.PlannedDate, now))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(DeadlinePassed());
        }

        var effective = await _resolver.ResolveAsync(tenantId, plan.Resource.ResourceId, now, cancellationToken);
        var policy = effective.Policy;

        var account = VisitExecutionRules.AccountOf(plan) is { } accountId
            ? await _accounts.GetByIdAsync(tenantId, accountId, cancellationToken)
            : null;
        var verdict = VisitExecutionRules.EvaluateLocation(
            policy, account?.Latitude, account?.Longitude,
            request.Latitude, request.Longitude, request.AccuracyMeters, request.LocationError);
        if (verdict.Refusal is { } locationFailure)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(locationFailure);
        }

        if (request.Photo is null
            && string.Equals(policy.PhotoMode, VisitPhotoModes.Required, StringComparison.Ordinal))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "A photo is required to start the visit.", VisitExecutionErrorCodes.PhotoRequired, 400));
        }

        if (request.Photo is { } file && VisitExecutionRules.ValidatePhoto(file) is { } photoFailure)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(photoFailure);
        }

        var execution = new ExecutionEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlannedVisitId = plan.Id,
            ResourceId = plan.Resource.ResourceId,
            Status = VisitExecutionStatus.Started,
            Kind = VisitExecutionRules.Normalize(request.Kind) ?? VisitExecutionKinds.InPerson,
            StartedAt = now, // the SERVER clock — never a client instant
            Location = verdict.Location,
            Flags = verdict.Flags.ToList(),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };
        if (effective.PrimaryException is { } exception)
        {
            execution.ExceptionId = exception.Id;
            execution.RelaxedRules = effective.RelaxedRules.ToList();
            execution.Flags.Add(VisitExecutionFlags.StartedWithException);
        }

        // 1) the photo FIRST, in the document repository (fail-closed).
        if (request.Photo is { } photo)
        {
            ContentArtifactStoreResult stored;
            try
            {
                stored = await _store.StoreAsync(
                    new VisitEvidenceStoreRequest(plan.Id, execution.Id, photo.FileName, photo.MediaType, photo.Content),
                    cancellationToken);
            }
            catch (ContentArtifactStoreException ex)
            {
                return VisitExecutionAccess.Fail<VisitExecutionDto>(StoreFailure(ex, VisitExecutionErrorCodes.PhotoInvalid));
            }

            execution.Photo = new VisitExecutionEvidence
            {
                ContentId = stored.ContentId,
                MediaType = stored.MediaType,
                ByteSize = stored.ByteSize,
                CapturedAt = VisitReportValidation.ParseInstant(request.PhotoCapturedAt)
            };
        }

        // 2) the record; a record that cannot be written takes the stored photo back.
        bool inserted;
        try
        {
            inserted = await _executions.InsertAsync(execution, cancellationToken);
        }
        catch
        {
            await CompensateAsync(execution);
            throw;
        }

        if (!inserted)
        {
            await CompensateAsync(execution);
            return VisitExecutionAccess.Fail<VisitExecutionDto>(AlreadyStarted());
        }

        return Response<VisitExecutionDto>.Success(
            VisitExecutionRules.ToDto(execution, plan.PlannedDate, _caller.CallerResourceId), 201);
    }

    private async Task CompensateAsync(ExecutionEntity execution)
    {
        if (execution.Photo is { } photo)
        {
            await _store.CompensateAsync(photo.ContentId, CancellationToken.None);
        }
    }

    private static VisitExecutionRules.Failure AlreadyStarted()
        => new("This visit has already been started.", VisitExecutionErrorCodes.AlreadyStarted, 409);

    internal static VisitExecutionRules.Failure DeadlinePassed()
        => new("The visit's 48-hour window has closed.", VisitExecutionErrorCodes.ReportDeadlinePassed, 409);

    /// <summary>The store answered 400 (its file rule) → the file was invalid; anything else → 503.</summary>
    internal static VisitExecutionRules.Failure StoreFailure(ContentArtifactStoreException ex, string invalidCode)
        => ex.StatusCode == 400
            ? new VisitExecutionRules.Failure("The evidence file was refused by the document repository.", invalidCode, 400)
            : new VisitExecutionRules.Failure(
                "The evidence store is unavailable. Nothing was saved — please try again.",
                VisitExecutionErrorCodes.EvidenceStoreUnavailable, 503);
}

/// <summary>
/// WP-VW-W3 (BE-a) — <c>PUT {id}/capture</c> and <c>POST {id}/finish</c>: a FULL replacement of the live capture while
/// the execution is <c>started</c>; the finish writes the last capture, the SERVER <c>finishedAt</c>, the duration and
/// the short-visit flag. Checks: own visit, started (else 409 <c>visit_execution_finished</c>), the 48 h lock, the
/// capture shape, the objection category against the <c>visit-objection-category</c> reference set (a set that is not
/// published refuses any category), the co-visitors (≤ 3 tenant users resolved by Auth, never the rep).
/// </summary>
public sealed class VisitExecutionCaptureHandler :
    IRequestHandler<CaptureVisitExecutionCommand, Response<VisitExecutionDto>>,
    IRequestHandler<FinishVisitExecutionCommand, Response<VisitExecutionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitExecutionRepository _executions;
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly IUserDisplayNameResolver _users;
    private readonly IReferenceDataValidator? _referenceValues;
    private readonly TimeProvider _clock;

    public VisitExecutionCaptureHandler(
        ITenantContext tenant, IActorContext actor, ICallerScope caller,
        IPlannedVisitRepository plans, IVisitExecutionRepository executions, IVisitEvidencePolicyRepository policies,
        IUserDisplayNameResolver users, IReferenceDataValidator? referenceValues = null, TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _caller = caller;
        _plans = plans;
        _executions = executions;
        _policies = policies;
        _users = users;
        _referenceValues = referenceValues;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<Response<VisitExecutionDto>> Handle(CaptureVisitExecutionCommand request, CancellationToken cancellationToken)
        => WriteAsync(request.ExecutionId, request.Capture, finish: false, cancellationToken);

    public Task<Response<VisitExecutionDto>> Handle(FinishVisitExecutionCommand request, CancellationToken cancellationToken)
        => WriteAsync(request.ExecutionId, request.Capture, finish: true, cancellationToken);

    private async Task<Response<VisitExecutionDto>> WriteAsync(
        Guid executionId, VisitCaptureInput? capture, bool finish, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitExecutionDto>.Fail("Tenant context is required.", 400);
        }

        var execution = await _executions.GetByIdAsync(tenantId, executionId, cancellationToken);
        var plan = execution is null ? null : await _plans.GetByIdAsync(tenantId, execution.PlannedVisitId, cancellationToken);
        if (execution is null || plan is null)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(VisitExecutionAccess.ExecutionNotFound());
        }

        if (VisitExecutionAccess.WriteRefusal(_caller, plan) is { } ownership)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(ownership.StatusCode == 404 ? VisitExecutionAccess.ExecutionNotFound() : ownership);
        }

        if (!execution.IsStarted())
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "The visit is finished; its capture can no longer change.", VisitExecutionErrorCodes.ExecutionFinished, 409));
        }

        var now = _clock.GetUtcNow();
        if (VisitReportDeadline.IsPassed(plan.PlannedDate, now))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(StartVisitExecutionHandler.DeadlinePassed());
        }

        if (capture is null && !finish)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(VisitExecutionRules.Capture("A capture body is required."));
        }

        IReadOnlyDictionary<Guid, string>? names = null;
        if (capture is not null)
        {
            if (VisitExecutionRules.ValidateCapture(plan, capture) is { } shapeFailure)
            {
                return VisitExecutionAccess.Fail<VisitExecutionDto>(shapeFailure);
            }

            if (await ValidateCategoriesAsync(capture, cancellationToken) is { } categoryFailure)
            {
                return VisitExecutionAccess.Fail<VisitExecutionDto>(categoryFailure);
            }

            var (coFailure, coIds) = VisitExecutionRules.ParseCoVisitors(capture.CoVisitorUserIds, execution.ResourceId);
            if (coFailure is not null)
            {
                return VisitExecutionAccess.Fail<VisitExecutionDto>(coFailure);
            }

            if (coIds.Count > 0)
            {
                names = await _users.ResolveAsync(coIds, cancellationToken);
                if (coIds.Any(id => !names.ContainsKey(id)))
                {
                    return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                        "A co-visitor is not a user of this tenant.", VisitExecutionErrorCodes.CoVisitorInvalid, 400));
                }
            }

            VisitExecutionRules.ApplyCapture(execution, capture, coIds);
        }

        if (finish)
        {
            var (policy, _) = await VisitExecutionAccess.PolicyAsync(_policies, tenantId, cancellationToken);
            VisitExecutionRules.ApplyFinish(execution, policy, now); // the SERVER clock
        }

        execution.UpdatedAt = now;
        execution.UpdatedBy = _actor.ActorName;
        if (!await _executions.ReplaceAsync(execution, execution.Version, cancellationToken))
        {
            return Response<VisitExecutionDto>.Fail(
                new[] { "The visit changed since it was loaded. Reload and try again.", VisitExecutionErrorCodes.ConcurrencyConflict },
                409);
        }

        return Response<VisitExecutionDto>.Success(
            VisitExecutionRules.ToDto(execution, plan.PlannedDate, _caller.CallerResourceId, names));
    }

    private async Task<VisitExecutionRules.Failure?> ValidateCategoriesAsync(VisitCaptureInput capture, CancellationToken cancellationToken)
    {
        foreach (var category in VisitExecutionRules.ObjectionCategories(capture))
        {
            if (_referenceValues is null)
            {
                return VisitExecutionRules.Capture(
                    $"The '{VisitExecutionLimits.ObjectionCategorySet}' set cannot be read; an objection category cannot be saved.");
            }

            var verdict = await _referenceValues.ValidateAsync(VisitExecutionLimits.ObjectionCategorySet, category, cancellationToken);
            switch (verdict.Status)
            {
                case ReferenceValidationStatus.SetMissing:
                    return VisitExecutionRules.Capture(
                        $"The '{VisitExecutionLimits.ObjectionCategorySet}' set is not published; an objection category cannot be saved.");
                case ReferenceValidationStatus.InvalidValue:
                    return VisitExecutionRules.Capture(
                        $"'{category}' is not an active '{VisitExecutionLimits.ObjectionCategorySet}' value.");
            }
        }

        return null;
    }
}

/// <summary>
/// WP-VW-W3 (BE-a) — <c>POST {id}/signature</c>: the doctor's PNG signature, ONCE, after the finish, before the report
/// is submitted and inside the 48 h window. Stored first in the repository (fail-closed), the record second (compensated
/// when it cannot be written). The context is DERIVED: <c>sample</c> when any product was marked <c>sample_request</c>.
/// </summary>
public sealed class SignVisitExecutionHandler : IRequestHandler<SignVisitExecutionCommand, Response<VisitExecutionDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICallerScope _caller;
    private readonly IPlannedVisitRepository _plans;
    private readonly IVisitReportRepository _reports;
    private readonly IVisitExecutionRepository _executions;
    private readonly IVisitEvidenceStore _store;
    private readonly TimeProvider _clock;

    public SignVisitExecutionHandler(
        ITenantContext tenant, IActorContext actor, ICallerScope caller, IPlannedVisitRepository plans,
        IVisitReportRepository reports, IVisitExecutionRepository executions, IVisitEvidenceStore store,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _actor = actor;
        _caller = caller;
        _plans = plans;
        _reports = reports;
        _executions = executions;
        _store = store;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<VisitExecutionDto>> Handle(SignVisitExecutionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitExecutionDto>.Fail("Tenant context is required.", 400);
        }

        var execution = await _executions.GetByIdAsync(tenantId, request.ExecutionId, cancellationToken);
        var plan = execution is null ? null : await _plans.GetByIdAsync(tenantId, execution.PlannedVisitId, cancellationToken);
        if (execution is null || plan is null)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(VisitExecutionAccess.ExecutionNotFound());
        }

        if (VisitExecutionAccess.WriteRefusal(_caller, plan) is { } ownership)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(ownership.StatusCode == 404 ? VisitExecutionAccess.ExecutionNotFound() : ownership);
        }

        if (!execution.IsFinished())
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "The visit is signed after it is finished.", VisitExecutionErrorCodes.ExecutionNotFinished, 409));
        }

        if (execution.Signature is not null)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "The visit is already signed.", VisitExecutionErrorCodes.SignatureExists, 409));
        }

        var report = await _reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken);
        if (report?.IsFinalised() == true)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(new VisitExecutionRules.Failure(
                "The visit's report is already submitted.", VisitExecutionErrorCodes.AlreadyReported, 409));
        }

        var now = _clock.GetUtcNow();
        if (VisitReportDeadline.IsPassed(plan.PlannedDate, now))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(StartVisitExecutionHandler.DeadlinePassed());
        }

        if (VisitExecutionRules.Normalize(request.Context) is { } asked
            && !VisitSignatureContexts.All.Contains(asked, StringComparer.Ordinal))
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(
                VisitExecutionRules.Capture($"context must be one of: {string.Join(", ", VisitSignatureContexts.All)}."));
        }

        if (!request.Confirmed)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(VisitExecutionRules.Capture("The doctor's confirmation is required."));
        }

        if (VisitExecutionRules.ValidateSignatureFile(request.File) is { } fileFailure)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(fileFailure);
        }

        ContentArtifactStoreResult stored;
        try
        {
            stored = await _store.StoreAsync(
                new VisitEvidenceStoreRequest(plan.Id, execution.Id, request.File!.FileName, request.File.MediaType, request.File.Content),
                cancellationToken);
        }
        catch (ContentArtifactStoreException ex)
        {
            return VisitExecutionAccess.Fail<VisitExecutionDto>(
                StartVisitExecutionHandler.StoreFailure(ex, VisitExecutionErrorCodes.CaptureInvalid));
        }

        execution.Signature = new VisitExecutionSignature
        {
            ContentId = stored.ContentId,
            MediaType = stored.MediaType,
            ByteSize = stored.ByteSize,
            Confirmed = true,
            Context = VisitExecutionRules.SignatureContextOf(execution),
            SignedAt = now
        };
        execution.UpdatedAt = now;
        execution.UpdatedBy = _actor.ActorName;

        bool replaced;
        try
        {
            replaced = await _executions.ReplaceAsync(execution, execution.Version, cancellationToken);
        }
        catch
        {
            await _store.CompensateAsync(stored.ContentId, CancellationToken.None);
            throw;
        }

        if (!replaced)
        {
            await _store.CompensateAsync(stored.ContentId, CancellationToken.None);
            return Response<VisitExecutionDto>.Fail(
                new[] { "The visit changed since it was loaded. Reload and try again.", VisitExecutionErrorCodes.ConcurrencyConflict },
                409);
        }

        return Response<VisitExecutionDto>.Success(
            VisitExecutionRules.ToDto(execution, plan.PlannedDate, _caller.CallerResourceId));
    }
}

/// <summary>WP-VW-W3 (BE-a, K-W2) — <c>PUT api/crm/visit-evidence-policy</c>: the tenant's evidence rules (absent
/// fields keep their value; bounds §2.2). The first PUT creates the tenant's single document.</summary>
public sealed class UpdateVisitEvidencePolicyHandler : IRequestHandler<UpdateVisitEvidencePolicyCommand, Response<VisitEvidencePolicyDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitEvidencePolicyRepository _policies;
    private readonly IVisitEvidencePolicyChangeRepository? _changes;

    public UpdateVisitEvidencePolicyHandler(ITenantContext tenant, IActorContext actor, IVisitEvidencePolicyRepository policies,
        IVisitEvidencePolicyChangeRepository? changes = null)
    {
        _tenant = tenant;
        _actor = actor;
        _policies = policies;
        _changes = changes;
    }

    public async Task<Response<VisitEvidencePolicyDto>> Handle(UpdateVisitEvidencePolicyCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitEvidencePolicyDto>.Fail("Tenant context is required.", 400);
        }

        var stored = await _policies.GetAsync(tenantId, cancellationToken);
        // WP-VW-W3 (W3-BE-c) — the before-image of the history rows (the defaults when the tenant had no document).
        var before = VisitEvidencePolicyResolver.Copy(stored ?? VisitExecutionRules.Defaults(tenantId));
        var policy = stored ?? new VisitEvidencePolicy { Id = Guid.NewGuid(), TenantId = tenantId };
        if (stored is not null && request.Input.ExpectedVersion is { } expected && expected != stored.Version)
        {
            return Conflict();
        }

        if (VisitExecutionRules.ApplyPolicy(policy, request.Input) is { } failure)
        {
            return VisitExecutionAccess.Fail<VisitEvidencePolicyDto>(failure);
        }

        policy.UpdatedAt = DateTimeOffset.UtcNow;
        policy.UpdatedBy = _actor.ActorName;
        var written = stored is null
            ? await _policies.InsertAsync(policy, cancellationToken)
            : await _policies.ReplaceAsync(policy, stored.Version, cancellationToken);
        if (!written)
        {
            return Conflict();
        }

        // WP-VW-W3 (W3-BE-c) — one append-only history row per changed field (+ "created" for the first document).
        if (_changes is not null)
        {
            await _changes.InsertManyAsync(VisitEvidencePolicyHistory.Rows(
                tenantId, before, policy, stored is null, _actor.ActorName, policy.UpdatedAt ?? DateTimeOffset.UtcNow), cancellationToken);
        }

        return Response<VisitEvidencePolicyDto>.Success(VisitExecutionRules.ToDto(policy, isDefault: false));
    }

    private static Response<VisitEvidencePolicyDto> Conflict()
        => Response<VisitEvidencePolicyDto>.Fail(
            new[] { "The policy changed since it was loaded. Reload and try again.", VisitExecutionErrorCodes.ConcurrencyConflict },
            409);
}
