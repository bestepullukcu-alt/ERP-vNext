using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed class PortfolioService(
    IPortfolioRepository repository, IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork,
    ITenantContext tenant, ICurrentActorContext actor, ICorrelationContext correlation, IPpmAccessAuthorizer access,
    IInvestmentCaseRepository? investmentCases = null,
    IPortfolioRecordAccessAuthority? recordAuthority = null,
    IPortfolioOwnerActionAuthority? ownerAuthority = null)
{
    public async Task<Response<PortfolioDto>> Create(CreatePortfolioCommand request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosCreate, ct);
        if (permission != PpmAccessDecision.Allowed) return permission.Failure<PortfolioDto>();
        var scope = Scope(null, "create") with { Code = request.Code, Name = request.Name,
            Description = request.Description, CapacityAllocationDescription = request.CapacityAllocationDescription };
        var evidence = await RecordEvidence(scope, ct);
        var status = Status(evidence, scope);
        if (status != 200) return Failure<PortfolioDto>(status);
        if (request.VisibilityPolicyKey is not null) return Failure<PortfolioDto>(400);
        if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(request.Code), null, ct))
            return Response<PortfolioDto>.Fail("Portfolio code already exists.", 409);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var transactionEvidence = await RecordEvidence(scope, token);
            if (Status(transactionEvidence, scope) != 200 ||
                recordAuthority is not PortfolioTemporaryNonProductionRecordAccessAuthority localAuthority)
                return Failure<PortfolioDto>(503);
            var entity = new Portfolio(tenant.TenantId, actor.ActorId, request.Code, request.Name,
                request.Description, null, request.CapacityAllocationDescription);
            try { entity.BindTemporaryNonProductionAccess(localAuthority.CreateBinding(entity.Id)); }
            catch (InvalidOperationException) { return Failure<PortfolioDto>(503); }
            await repository.AddAsync(entity, token);
            await audit.AddAsync(Intent(entity, "created"), token);
            return Response<PortfolioDto>.Success(entity.ToDto(), 201);
        }, ct);
    }

    public async Task<Response<PortfolioDto>> Update(UpdatePortfolioCommand request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosUpdate, ct);
        if (permission != PpmAccessDecision.Allowed) return permission.Failure<PortfolioDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Failure<PortfolioDto>(404);
        var scope = Scope(entity, "edit") with { ExpectedVersion = request.ExpectedVersion, Code = request.Code,
            Name = request.Name, Description = request.Description, CapacityAllocationDescription = request.CapacityAllocationDescription };
        var evidence = await RecordEvidence(scope, ct);
        var status = Status(evidence, scope);
        if (status != 200) return Failure<PortfolioDto>(status);
        if (request.VisibilityPolicyKey is not null) return Failure<PortfolioDto>(400);
        if (entity.LifecycleState != PortfolioLifecycleState.Draft || entity.Version != request.ExpectedVersion)
            return Failure<PortfolioDto>(409);
        if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(request.Code), request.Id, ct))
            return Response<PortfolioDto>.Fail("Portfolio code already exists.", 409);
        entity.Update(actor.ActorId, request.Code, request.Name, request.Description,
            entity.VisibilityPolicyKey, request.CapacityAllocationDescription);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (Status(evidence, scope) != 200) return Failure<PortfolioDto>(503);
            await repository.ReplaceAsync(entity, request.ExpectedVersion, token);
            await audit.AddAsync(Intent(entity, "updated"), token);
            return Response<PortfolioDto>.Success(entity.ToDto());
        }, ct);
    }

    // Keep the existing routes fail-closed, including direct API bypass attempts.
    public async Task<Response<PortfolioDto>> Transition(TransitionPortfolioLifecycleCommand request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosLifecycle, ct);
        return permission != PpmAccessDecision.Allowed ? permission.Failure<PortfolioDto>()
            : Response<PortfolioDto>.Fail("Portfolio lifecycle is unavailable in this delivery.", 409);
    }
    public async Task<Response<NoContent>> SoftDelete(SoftDeletePortfolioCommand request, CancellationToken ct)
    {
        // Preserve constructor compatibility with the existing dependency-fence composition.
        _ = investmentCases;
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosLifecycle, ct);
        return permission != PpmAccessDecision.Allowed ? permission.Failure<NoContent>()
            : Response<NoContent>.Fail("Portfolio deletion is unavailable in this delivery.", 409);
    }

    public async Task<Response<PortfolioPageAccess>> PageAccess(GetPortfolioPageAccessQuery request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosRead, ct);
        if (permission != PpmAccessDecision.Allowed) return permission.Failure<PortfolioPageAccess>();
        var scope = Scope(null, "page");
        var status = Status(await RecordEvidence(scope, ct), scope);
        if (status != 200) return Failure<PortfolioPageAccess>(status);
        var createScope = Scope(null, "create-availability");
        var canCreate = await access.AuthorizeAsync(PpmPermissions.PortfoliosCreate, ct) == PpmAccessDecision.Allowed &&
            Status(await RecordEvidence(createScope, ct), createScope) == 200;
        return Response<PortfolioPageAccess>.Success(new(true, canCreate));
    }

    public async Task<Response<PortfolioDto>> GetById(GetPortfolioByIdQuery request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosRead, ct);
        if (permission != PpmAccessDecision.Allowed) return permission.Failure<PortfolioDto>();
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Failure<PortfolioDto>(404);
        var scope = Scope(entity, "read");
        var status = Status(await RecordEvidence(scope, ct), scope);
        if (status != 200) return Failure<PortfolioDto>(status == 403 ? 404 : status);
        return Response<PortfolioDto>.Success(await Project(entity, true, ct));
    }

    public async Task<Response<IReadOnlyList<PortfolioDto>>> List(ListPortfoliosQuery request, CancellationToken ct)
    {
        var page = await PageAccess(new(), ct);
        if (!page.IsSuccessful) return Failure<IReadOnlyList<PortfolioDto>>(page.StatusCode);
        var items = await repository.ListAsync(tenant.TenantId, ct);
        var visible = new List<PortfolioDto>();
        foreach (var entity in items)
        {
            var scope = Scope(entity, "read");
            var status = Status(await RecordEvidence(scope, ct), scope);
            if (status == 503) return Failure<IReadOnlyList<PortfolioDto>>(503);
            if (status == 200) visible.Add(await Project(entity, false, ct));
        }
        return Response<IReadOnlyList<PortfolioDto>>.Success(visible);
    }

    public async Task<Response<IReadOnlyList<PortfolioOwnerCandidate>>> OwnerCandidates(GetPortfolioOwnerCandidatesQuery request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosAssignOwner, ct);
        if (permission != PpmAccessDecision.Allowed) return permission.Failure<IReadOnlyList<PortfolioOwnerCandidate>>();
        if (request.Id == Guid.Empty || request.Search is null || request.Search.Length > 100 || request.Limit is < 1 or > 20)
            return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(400);
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(404);
        var scope = Scope(entity, "owner-candidates") with { Search = request.Search, Limit = request.Limit };
        var status = Status(await RecordEvidence(scope, ct), scope);
        if (status != 200) return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(status);
        if (entity.LifecycleState != PortfolioLifecycleState.Draft) return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(409);
        if (ownerAuthority is null) return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(503);
        PortfolioOwnerCandidatesEvidence? result;
        try { result = await ownerAuthority.CandidatesAsync(scope, ct); }
        catch (Exception ex) when (IsUnavailable(ex, ct)) { return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(503); }
        status = Status(result?.Authority, scope);
        if (status != 200) return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(status);
        if (result!.Candidates is null || result.Candidates.Count > request.Limit ||
            result.Candidates.Any(x => x.UserId == Guid.Empty || string.IsNullOrWhiteSpace(x.DisplayLabel) || x.DisplayLabel.Length > 200) ||
            result.Candidates.Select(x => x.UserId).Distinct().Count() != result.Candidates.Count)
            return Failure<IReadOnlyList<PortfolioOwnerCandidate>>(503);
        return Response<IReadOnlyList<PortfolioOwnerCandidate>>.Success(result.Candidates);
    }

    public async Task<Response<PortfolioOwnerReceipt>> ChangeOwner(ChangePortfolioOwnerCommand request, CancellationToken ct)
    {
        var permission = await access.AuthorizeAsync(PpmPermissions.PortfoliosAssignOwner, ct);
        if (permission != PpmAccessDecision.Allowed) return permission.Failure<PortfolioOwnerReceipt>();
        var validation = await new ChangePortfolioOwnerValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Failure<PortfolioOwnerReceipt>(400);
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Failure<PortfolioOwnerReceipt>(404);
        var scope = Scope(entity, request.Operation == PortfolioOwnerOperation.Assign ? "assign-owner" : "transfer-owner") with
        { TargetUserId = request.TargetUserId, RequestId = request.RequestId, ExpectedVersion = request.ExpectedVersion,
          ExpectedAssignmentId = request.ExpectedAssignmentId, Reason = request.Reason };
        var recordEvidence = await RecordEvidence(scope, ct);
        var status = Status(recordEvidence, scope);
        if (status != 200) return Failure<PortfolioOwnerReceipt>(status);
        if (entity.LifecycleState != PortfolioLifecycleState.Draft) return Failure<PortfolioOwnerReceipt>(409);
        if (ownerAuthority is null) return Failure<PortfolioOwnerReceipt>(503);
        PortfolioOwnerEvidence? evidence;
        try { evidence = await ownerAuthority.EvaluateAsync(scope, ct); }
        catch (Exception ex) when (IsUnavailable(ex, ct)) { return Failure<PortfolioOwnerReceipt>(503); }
        status = Status(evidence?.ActorAuthority, scope);
        if (status != 200) return Failure<PortfolioOwnerReceipt>(status);
        status = Status(evidence?.TargetEligibility, scope);
        if (status != 200) return Failure<PortfolioOwnerReceipt>(status);
        if (!evidence!.SameTenant || !evidence.Active || !evidence.NamedHuman || !evidence.Assignable)
            return Failure<PortfolioOwnerReceipt>(403);
        var labelFailure = LabelFailure<PortfolioOwnerReceipt>(evidence);
        if (labelFailure is not null) return labelFailure;
        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async token =>
            {
                var transactionRecordEvidence = await RecordEvidence(scope, token);
                if (Status(transactionRecordEvidence, scope) != 200) return Failure<PortfolioOwnerReceipt>(503);
                PortfolioOwnerEvidence? transactionEvidence;
                try { transactionEvidence = await ownerAuthority.EvaluateAsync(scope, token); }
                catch (Exception ex) when (IsUnavailable(ex, token)) { return Failure<PortfolioOwnerReceipt>(503); }
                // Reassertion is a second authorization decision, not a transport failure. Preserve definitive
                // denials and non-disclosure while keeping malformed, unavailable, or scope-mismatched evidence closed.
                status = Status(transactionEvidence?.ActorAuthority, scope);
                if (status != 200) return Failure<PortfolioOwnerReceipt>(status);
                status = Status(transactionEvidence?.TargetEligibility, scope);
                if (status != 200) return Failure<PortfolioOwnerReceipt>(status);
                if (!transactionEvidence!.SameTenant || !transactionEvidence.Active || !transactionEvidence.NamedHuman || !transactionEvidence.Assignable)
                    return Failure<PortfolioOwnerReceipt>(403);
                var transactionLabelFailure = LabelFailure<PortfolioOwnerReceipt>(transactionEvidence);
                if (transactionLabelFailure is not null) return transactionLabelFailure;
                var previousVersion = entity.Version;
                PortfolioOwnerAssignment assignment;
                try
                {
                    assignment = entity.ChangeOwner(actor.ActorId, request.TargetUserId, transactionEvidence.DisplayLabel!,
                        request.Reason, request.Operation, request.ExpectedAssignmentId, request.ExpectedVersion,
                        request.RequestId, Guid.NewGuid(), correlation.CorrelationId);
                }
                catch (ArgumentException) { return Failure<PortfolioOwnerReceipt>(400); }
                catch (InvalidOperationException) { return Failure<PortfolioOwnerReceipt>(409); }
                var receipt = new PortfolioOwnerReceipt(assignment.Id, assignment.RequestId, assignment.ResultVersion);
                if (entity.Version == previousVersion) return Response<PortfolioOwnerReceipt>.Success(receipt);
                await repository.ReplaceAsync(entity, request.ExpectedVersion, token);
                await audit.AddAsync(new(assignment.AuditIntentId, tenant.TenantId, actor.ActorId,
                    assignment.CorrelationId, nameof(Portfolio), entity.Id, "updated", assignment.OccurredAtUtc), token);
                return Response<PortfolioOwnerReceipt>.Success(receipt);
            }, ct);
        }
        catch (OptimisticConcurrencyException) { return Failure<PortfolioOwnerReceipt>(409); }
    }

    private async Task<PortfolioDto> Project(Portfolio entity, bool detail, CancellationToken ct)
    {
        var editScope = Scope(entity, "edit-availability");
        var ownerScope = Scope(entity, "manage-owner");
        var canEdit = entity.LifecycleState == PortfolioLifecycleState.Draft &&
            await access.AuthorizeAsync(PpmPermissions.PortfoliosUpdate, ct) == PpmAccessDecision.Allowed &&
            Status(await RecordEvidence(editScope, ct), editScope) == 200;
        var canAssign = entity.LifecycleState == PortfolioLifecycleState.Draft &&
            await access.AuthorizeAsync(PpmPermissions.PortfoliosAssignOwner, ct) == PpmAccessDecision.Allowed &&
            Status(await RecordEvidence(ownerScope, ct), ownerScope) == 200 &&
            Status(await ManageEvidence(ownerScope, ct), ownerScope) == 200;
        var ownerReadScope = Scope(entity, "owner-read");
        var canReadOwner = Status(await RecordEvidence(ownerReadScope, ct), ownerReadScope) == 200;
        var historyScope = Scope(entity, "history-read");
        var canReadHistory = detail && Status(await RecordEvidence(historyScope, ct), historyScope) == 200;
        return entity.ToDto() with
        {
            Actions = new(canEdit, canAssign && canReadOwner),
            Owner = canReadOwner && entity.CurrentOwnerAssignment is { } current
                ? new(current.Id, current.DisplayLabel) : null,
            OwnerVisible = canReadOwner,
            OwnerHistory = canReadHistory ? entity.OwnerAssignments.Select(x =>
                new PortfolioOwnerHistoryItem(x.Id, x.DisplayLabel, x.Reason, x.OccurredAtUtc, x.PreviousAssignmentId, x.Operation)).ToArray() : null
        };
    }

    private PortfolioAuthorityScope Scope(Portfolio? entity, string operation) =>
        new(tenant.TenantId, actor.ActorId, entity?.Id, operation, entity?.Version,
            VisibilityPolicyKey: entity?.VisibilityPolicyKey,
            RecordTenantId: entity?.TenantId,
            CreatorId: entity?.CreatedBy,
            CurrentOwnerUserId: entity?.CurrentOwnerAssignment?.UserId,
            LifecycleState: entity?.LifecycleState,
            TemporaryNonProductionAccessBinding: entity?.TemporaryNonProductionAccessBinding);
    private async Task<PortfolioAuthorityEvidence?> RecordEvidence(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        if (recordAuthority is null || scope.TenantId == Guid.Empty || scope.ActorId == Guid.Empty) return null;
        try { return await recordAuthority.EvaluateAsync(scope, ct); }
        catch (Exception ex) when (IsUnavailable(ex, ct)) { return null; }
    }
    private async Task<PortfolioAuthorityEvidence?> ManageEvidence(PortfolioAuthorityScope scope, CancellationToken ct)
    {
        if (ownerAuthority is null) return null;
        try { return await ownerAuthority.CanManageAsync(scope, ct); }
        catch (Exception ex) when (IsUnavailable(ex, ct)) { return null; }
    }
    private static bool IsUnavailable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or TimeoutException || ex is OperationCanceledException && !ct.IsCancellationRequested;
    private static int Status(PortfolioAuthorityEvidence? evidence, PortfolioAuthorityScope scope) =>
        evidence is null || !evidence.IsBoundTo(scope) ? 503 : evidence.Outcome switch
        { PortfolioAuthorityOutcome.Allowed => 200, PortfolioAuthorityOutcome.Denied => 403,
          PortfolioAuthorityOutcome.NotFound => 404, _ => 503 };
    private static Response<T> Failure<T>(int status) => Response<T>.Fail(status switch
    { 400 => "Invalid Portfolio request.", 403 => "Portfolio operation is not permitted.",
      404 => "Portfolio was not found.", 409 => "Portfolio state or request has changed.",
      _ => "Portfolio authority is unavailable." }, status);
    private static Response<T>? LabelFailure<T>(PortfolioOwnerEvidence evidence) => evidence.LabelState switch
    {
        PortfolioOwnerLabelState.Missing => Response<T>.Fail("PORTFOLIO_OWNER_LABEL_MISSING", 409),
        PortfolioOwnerLabelState.TooLong => Response<T>.Fail("PORTFOLIO_OWNER_LABEL_TOO_LONG", 409),
        PortfolioOwnerLabelState.Available when !string.IsNullOrWhiteSpace(evidence.DisplayLabel) && evidence.DisplayLabel.Length <= 200 => null,
        _ => Failure<T>(503)
    };
    private AuditIntent Intent(Portfolio x, string mutation) => new(Guid.NewGuid(), tenant.TenantId,
        actor.ActorId, correlation.CorrelationId, nameof(Portfolio), x.Id, mutation, DateTime.UtcNow);
}
