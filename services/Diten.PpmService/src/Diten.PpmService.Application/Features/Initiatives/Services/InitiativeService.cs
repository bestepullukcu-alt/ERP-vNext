using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed class InitiativeService(
    IInitiativeRepository repository, IPortfolioRepository portfolios, IAuditIntentRepository audit,
    IPpmUnitOfWork unitOfWork, ITenantContext tenant, ICurrentActorContext actor, ICorrelationContext correlation,
    IPpmAccessAuthorizer access, IInitiativeClassificationAuthority? classifications = null,
    IInitiativeClosureReferenceAuthority? closureReferences = null,
    IInitiativeLifecycleContractAuthority? lifecycleContracts = null)
{
    private IInitiativeClassificationAuthority Classifications { get; } = classifications ?? UnavailableAuthorities.Instance;
    private IInitiativeClosureReferenceAuthority ClosureReferences { get; } = closureReferences ?? UnavailableAuthorities.Instance;
    private IInitiativeLifecycleContractAuthority? LifecycleContracts { get; } = lifecycleContracts;
    private IInitiativeV2Repository? V2Repository => repository as IInitiativeV2Repository;

    public async Task<Response<InitiativeLifecycleContractsV2>> GetLifecycleContracts(CancellationToken ct)
    {
        var denied = await Authorize<InitiativeLifecycleContractsV2>(PpmPermissions.InitiativesRead, ct);
        if (denied is not null) return denied;

        try
        {
            var contract = LifecycleContracts is null
                ? BuildLifecycleContracts()
                : await LifecycleContracts.GetLifecycleContractsAsync(ct);
            ValidateLifecycleContracts(contract);
            return Response<InitiativeLifecycleContractsV2>.Success(contract);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Response<InitiativeLifecycleContractsV2>.Fail(
                "Initiative lifecycle contract authority is unavailable.", 503);
        }
    }

    public async Task<Response<InitiativeContractsV2>> GetContracts(CancellationToken ct)
    {
        var denied = await Authorize<InitiativeContractsV2>(PpmPermissions.InitiativesRead, ct);
        if (denied is not null) return denied;
        var types = await Classifications.GetTypesAsync(ct);
        var priorities = await Classifications.GetPrioritiesAsync(ct);
        if (!IsUsableClassificationResult(types) || !IsUsableClassificationResult(priorities))
            return Response<InitiativeContractsV2>.Fail("Initiative classification authority is unavailable.", 503);
        return Response<InitiativeContractsV2>.Success(new(types.Options, priorities.Options,
            InitiativeVocabularies.CancellationReasons, InitiativeVocabularies.HoldReasons,
            InitiativeVocabularies.CompletionOutcomes, InitiativeVocabularies.ClosureReasons,
            InitiativeVocabularies.BenefitDispositions));
    }

    public async Task<Response<InitiativeV2Dto>> Create(CreateInitiativeCommand request, CancellationToken ct)
    {
        var denied = await Authorize<InitiativeV2Dto>(PpmPermissions.InitiativesCreate, ct);
        if (denied is not null) return denied;
        var invalid = await ValidateClassifications(request.InitiativeTypeCode, request.PriorityCode, ct);
        if (invalid is not null) return invalid;
        return await CreateCore(request.Code, request.Name, request.Description, request.PortfolioId,
            request.InitiativeTypeCode, request.PriorityCode, request.PlannedStartDate, request.PlannedEndDate, null, ct);
    }

    public async Task<Response<InitiativeV2Dto>> CreateSuccessor(CreateInitiativeSuccessorCommand request, CancellationToken ct)
    {
        var denied = await Authorize<InitiativeV2Dto>(PpmPermissions.InitiativesCreate, ct);
        if (denied is not null) return denied;
        var invalid = await ValidateClassifications(request.InitiativeTypeCode, request.PriorityCode, ct);
        if (invalid is not null) return invalid;
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var old = await repository.GetByIdAsync(tenant.TenantId, request.TerminalId, token);
            if (old is null) return Response<InitiativeV2Dto>.Fail("Initiative was not found.", 404);
            if (!old.IsTerminal || old.Version != request.ExpectedTerminalVersion)
                return Response<InitiativeV2Dto>.Fail("Terminal Initiative or version is invalid.", 409);
            if (V2Repository is null) return Response<InitiativeV2Dto>.Fail("Initiative v2 persistence is unavailable.", 503);
            if (await V2Repository.GetActiveSuccessorAsync(tenant.TenantId, old.Id, token) is not null)
                return Response<InitiativeV2Dto>.Fail("Initiative already has a successor.", 409);
            if (!await PortfolioExists(request.PortfolioId, token))
                return Response<InitiativeV2Dto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(request.Code), null, token))
                return Response<InitiativeV2Dto>.Fail("Initiative code already exists.", 409);
            var successor = New(request.Code, request.Name, request.Description, request.PortfolioId,
                request.InitiativeTypeCode, request.PriorityCode, request.PlannedStartDate, request.PlannedEndDate, old.Id);
            if (successor.Id == old.Id || await WouldCreateCycle(successor, old, token))
                return Response<InitiativeV2Dto>.Fail("Initiative supersession cycle is not allowed.", 409);
            await V2Repository.ClaimTerminalForSuccessorAsync(
                tenant.TenantId, old.Id, successor.Id, request.ExpectedTerminalVersion, token);
            await repository.AddAsync(successor, token);
            await audit.AddAsync(Intent(successor, "successor-created"), token);
            return Response<InitiativeV2Dto>.Success(
                successor.ToV2Dto(await GetAvailableActions(successor, token)), 201);
        }, ct);
    }

    public async Task<Response<InitiativeV2Dto>> Update(UpdateInitiativeCommand request, CancellationToken ct)
    {
        var denied = await Authorize<InitiativeV2Dto>(PpmPermissions.InitiativesUpdate, ct);
        if (denied is not null) return denied;
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Response<InitiativeV2Dto>.Fail("Initiative was not found.", 404);
        if (entity.IsTerminal) return Response<InitiativeV2Dto>.Fail("Terminal Initiative records are immutable.", 409);
        var invalid = await ValidateClassifications(request.InitiativeTypeCode, request.PriorityCode, ct);
        if (invalid is not null) return invalid;
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await PortfolioExists(request.PortfolioId, token)) return Response<InitiativeV2Dto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(request.Code), request.Id, token))
                return Response<InitiativeV2Dto>.Fail("Initiative code already exists.", 409);
            entity.Update(actor.ActorId, request.Code, request.Name, request.Description, request.PortfolioId,
                request.InitiativeTypeCode, request.PriorityCode, request.PlannedStartDate, request.PlannedEndDate);
            await repository.ReplaceAsync(entity, request.ExpectedVersion, token);
            await audit.AddAsync(Intent(entity, "updated"), token);
            return Response<InitiativeV2Dto>.Success(
                entity.ToV2Dto(await GetAvailableActions(entity, token)));
        }, ct);
    }

    public async Task<Response<InitiativeLifecycleResult>> Transition(TransitionInitiativeLifecycleCommand request, CancellationToken ct)
    {
        var denied = await Authorize<InitiativeLifecycleResult>(PpmPermissions.InitiativesLifecycle, ct);
        if (denied is not null) return denied;
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Response<InitiativeLifecycleResult>.Fail("Initiative was not found.", 404);
        if (entity.IsTerminal) return Response<InitiativeLifecycleResult>.Fail("Terminal Initiative records are immutable.", 409);
        if (!entity.CanTransitionTo(request.TargetState)) return Response<InitiativeLifecycleResult>.Fail("Invalid lifecycle transition.", 409);
        var companionError = ValidateCompanionData(request);
        if (companionError is not null) return companionError;

        if (request.TargetState == InitiativeLifecycleState.Active)
        {
            if (!entity.IsActivationReady)
                return Response<InitiativeLifecycleResult>.Fail("Type, priority and both planning dates are required before Active.", 400);
            var invalid = await ValidateClassifications(entity.InitiativeTypeCode, entity.PriorityCode, ct);
            if (invalid is not null) return Response<InitiativeLifecycleResult>.Fail(invalid.Errors, invalid.StatusCode);
            return Response<InitiativeLifecycleResult>.Fail("Initiative activation approval policy authority is unavailable.", 503);
        }
        if (request.TargetState == InitiativeLifecycleState.Cancelled
            && entity.LifecycleState is InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold)
            return Response<InitiativeLifecycleResult>.Fail("Approval outcome authority is unavailable.", 503);

        InitiativeClosure? closure = null;
        if (request.TargetState == InitiativeLifecycleState.Completed)
        {
            if (V2Repository is null)
                return Response<InitiativeLifecycleResult>.Fail("Initiative closure persistence is unavailable.", 503);
            var evidence = request.Closure!.EvidenceReferences ?? [];
            var tasks = request.Closure.FollowUpTaskReferences ?? [];
            if (await ClosureReferences.ValidateEvidenceAsync(evidence, ct) != InitiativeAuthorityDisposition.Valid
                || await ClosureReferences.ValidateFollowUpTasksAsync(tasks, ct) != InitiativeAuthorityDisposition.Valid)
                return Response<InitiativeLifecycleResult>.Fail("Closure reference authority is unavailable.", 503);
            closure = new(tenant.TenantId, actor.ActorId, entity.Id, request.Closure.OutcomeCode,
                request.Closure.ClosureReasonCode, DateTime.UtcNow, request.Closure.CompletionSummary,
                evidence, tasks, request.Closure.BenefitDisposition, entity.CreatedAtUtc);
        }

        IReadOnlyList<string> warnings = request.TargetState == InitiativeLifecycleState.OnHold
            ? [InitiativeWarnings.RecipientUnresolved] : [];
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            entity.Transition(actor.ActorId, request.TargetState);
            await repository.ReplaceAsync(entity, request.ExpectedVersion, token);
            if (closure is not null)
                await V2Repository!.AddClosureAsync(closure, token);
            await audit.AddAsync(Intent(entity, warnings.Count != 0 ? InitiativeWarnings.RecipientUnresolved : "lifecycle-changed"), token);
            return Response<InitiativeLifecycleResult>.Success(new(
                entity.ToV2Dto(await GetAvailableActions(entity, token)), closure?.ToDto(), warnings));
        }, ct);
    }

    public async Task<Response<NoContent>> SoftDelete(SoftDeleteInitiativeCommand request, CancellationToken ct)
    {
        var denied = await Authorize<NoContent>(PpmPermissions.InitiativesLifecycle, ct);
        if (denied is not null) return denied;
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        if (entity is null) return Response<NoContent>.Fail("Initiative was not found.", 404);
        if (entity.IsTerminal) return Response<NoContent>.Fail("Terminal Initiative records are immutable.", 409);
        entity.SoftDelete(actor.ActorId);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await repository.ReplaceAsync(entity, request.ExpectedVersion, token);
            await audit.AddAsync(Intent(entity, "soft-deleted"), token);
            return Response<NoContent>.SuccessWithoutData();
        }, ct);
    }

    public async Task<Response<InitiativeV2Dto>> GetById(GetInitiativeByIdQuery request, CancellationToken ct)
    {
        var denied = await Authorize<InitiativeV2Dto>(PpmPermissions.InitiativesRead, ct);
        if (denied is not null) return denied;
        var entity = await repository.GetByIdAsync(tenant.TenantId, request.Id, ct);
        return entity is null
            ? Response<InitiativeV2Dto>.Fail("Initiative was not found.", 404)
            : Response<InitiativeV2Dto>.Success(
                entity.ToV2Dto(await GetAvailableActions(entity, ct)));
    }

    public async Task<Response<InitiativeDetailLinks>> GetDetailLinks(GetInitiativeDetailLinksQuery request, CancellationToken ct)
    {
        var denied = await Authorize<InitiativeDetailLinks>(PpmPermissions.InitiativesRead, ct);
        if (denied is not null) return denied;
        if (await repository.GetByIdAsync(tenant.TenantId, request.Id, ct) is null)
            return Response<InitiativeDetailLinks>.Fail("Initiative was not found.", 404);
        return Response<InitiativeDetailLinks>.Fail("Authoritative Initiative detail-link owner contracts are unavailable.", 503);
    }

    public async Task<Response<IReadOnlyList<InitiativeV2Dto>>> List(ListInitiativesQuery request, CancellationToken ct)
    {
        var denied = await Authorize<IReadOnlyList<InitiativeV2Dto>>(PpmPermissions.InitiativesRead, ct);
        if (denied is not null) return denied;
        var entities = await repository.ListAsync(tenant.TenantId, ct);
        var lifecycleAccess = await access.AuthorizeAsync(PpmPermissions.InitiativesLifecycle, ct);
        return Response<IReadOnlyList<InitiativeV2Dto>>.Success(entities
            .Select(x => x.ToV2Dto(GetAvailableActions(x, lifecycleAccess)))
            .ToArray());
    }

    private async Task<Response<InitiativeV2Dto>> CreateCore(string code, string name, string? description,
        Guid? portfolioId, string? type, string? priority, DateOnly? start, DateOnly? end, Guid? supersedes, CancellationToken ct) =>
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await PortfolioExists(portfolioId, token)) return Response<InitiativeV2Dto>.Fail("Portfolio was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(code), null, token))
                return Response<InitiativeV2Dto>.Fail("Initiative code already exists.", 409);
            var entity = New(code, name, description, portfolioId, type, priority, start, end, supersedes);
            await repository.AddAsync(entity, token);
            await audit.AddAsync(Intent(entity, "created"), token);
            return Response<InitiativeV2Dto>.Success(
                entity.ToV2Dto(await GetAvailableActions(entity, token)), 201);
        }, ct);

    private async Task<IReadOnlyList<InitiativeActionAvailability>> GetAvailableActions(
        Initiative entity, CancellationToken ct) =>
        GetAvailableActions(entity,
            await access.AuthorizeAsync(PpmPermissions.InitiativesLifecycle, ct));

    private static IReadOnlyList<InitiativeActionAvailability> GetAvailableActions(
        Initiative entity, PpmAccessDecision lifecycleAccess)
    {
        if (entity.IsTerminal) return [];

        return Enum.GetValues<InitiativeLifecycleState>()
            .Where(entity.CanTransitionTo)
            .Select(target => BuildActionAvailability(entity, target, lifecycleAccess))
            .ToArray();
    }

    private static InitiativeActionAvailability BuildActionAvailability(
        Initiative entity, InitiativeLifecycleState target, PpmAccessDecision lifecycleAccess)
    {
        var companionData = CompanionDataFor(target);
        if (lifecycleAccess == PpmAccessDecision.Forbidden)
            return new(target, InitiativeActionAvailability.Forbidden,
                InitiativeActionAvailability.LifecyclePermissionDeniedReason, companionData);
        if (target == InitiativeLifecycleState.Active && !entity.IsActivationReady)
            return new(target, InitiativeActionAvailability.RecordNotReady,
                InitiativeActionAvailability.ActivationDataIncompleteReason, companionData);
        if (lifecycleAccess == PpmAccessDecision.DependencyUnavailable)
            return new(target, InitiativeActionAvailability.DependencyUnavailable,
                InitiativeActionAvailability.EntitlementAuthorityUnavailableReason, companionData);
        if (RequiresApprovalAuthority(entity.LifecycleState, target))
            return new(target, InitiativeActionAvailability.DependencyUnavailable,
                InitiativeActionAvailability.ApprovalAuthorityUnavailableReason, companionData);
        return new(target, InitiativeActionAvailability.Available,
            InitiativeActionAvailability.Available, companionData);
    }

    private static InitiativeLifecycleContractsV2 BuildLifecycleContracts()
    {
        var states = Enum.GetValues<InitiativeLifecycleState>();
        var transitions = states
            .SelectMany(source => states
                .Where(CreateLifecycleProbe(source).CanTransitionTo)
                .Select(target => new InitiativeLifecycleTransitionContract(
                    source, target, CompanionDataFor(target),
                    RequiresApprovalAuthority(source, target)
                        ? InitiativeLifecycleTransitionContract.ApprovalAuthorityRequiredDisposition
                        : InitiativeLifecycleTransitionContract.DirectApprovalDisposition)))
            .ToArray();
        var allowedTargetsBySource = states.ToDictionary(
            source => source,
            source => (IReadOnlyList<InitiativeLifecycleState>)transitions
                .Where(transition => transition.SourceState == source)
                .Select(transition => transition.TargetState)
                .ToArray());

        return new(InitiativeLifecycleContractsV2.CurrentContractVersion, allowedTargetsBySource, transitions,
            InitiativeVocabularies.CancellationReasons,
            InitiativeVocabularies.HoldReasons,
            InitiativeVocabularies.CompletionOutcomes,
            InitiativeVocabularies.ClosureReasons,
            InitiativeVocabularies.BenefitDispositions);
    }

    private static Initiative CreateLifecycleProbe(InitiativeLifecycleState state)
    {
        var probe = new Initiative(Guid.NewGuid(), Guid.NewGuid(), "CONTRACT-PROBE", "Contract probe",
            null, null, "type", "priority", DateOnly.MinValue, DateOnly.MinValue);
        if (state is InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold
            or InitiativeLifecycleState.Completed)
            probe.Transition(Guid.NewGuid(), InitiativeLifecycleState.Active);
        if (state is InitiativeLifecycleState.OnHold)
            probe.Transition(Guid.NewGuid(), InitiativeLifecycleState.OnHold);
        if (state is InitiativeLifecycleState.Completed)
            probe.Transition(Guid.NewGuid(), InitiativeLifecycleState.Completed);
        if (state is InitiativeLifecycleState.Cancelled)
            probe.Transition(Guid.NewGuid(), InitiativeLifecycleState.Cancelled);
        return probe;
    }

    private static void ValidateLifecycleContracts(InitiativeLifecycleContractsV2 contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var states = Enum.GetValues<InitiativeLifecycleState>();
        var allowedTargetsBySource = contract.AllowedTargetStatesBySource;
        var transitions = contract.Transitions;
        if (!string.Equals(contract.ContractVersion, InitiativeLifecycleContractsV2.CurrentContractVersion,
                StringComparison.Ordinal)
            || allowedTargetsBySource is null
            || transitions is null
            || contract.CancellationReasons is null
            || contract.HoldReasons is null
            || contract.CompletionOutcomes is null
            || contract.ClosureReasons is null
            || contract.BenefitDispositions is null)
            throw new InvalidOperationException("Initiative lifecycle contract is malformed.");

        if (allowedTargetsBySource.Count != states.Length
            || states.Any(state => !allowedTargetsBySource.ContainsKey(state))
            || allowedTargetsBySource.Keys.Any(state => !Enum.IsDefined(state))
            || allowedTargetsBySource.Values.Any(targets => targets is null)
            || allowedTargetsBySource.Any(entry => entry.Value.Any(target => !Enum.IsDefined(target)))
            || allowedTargetsBySource.Any(entry => entry.Value.Distinct().Count() != entry.Value.Count)
            || allowedTargetsBySource.Any(entry => !entry.Value.SequenceEqual(transitions
                .Where(transition => transition.SourceState == entry.Key)
                .Select(transition => transition.TargetState)))
            || transitions.Any(x => x is null)
            || transitions.Any(x => !Enum.IsDefined(x.SourceState) || !Enum.IsDefined(x.TargetState))
            || transitions.Any(x => x.SourceState == x.TargetState)
            || transitions.DistinctBy(x => (x.SourceState, x.TargetState)).Count() != transitions.Count
            || transitions.Any(transition => transition.RequiredCompanionDataKind is not (
                InitiativeLifecycleTransitionContract.NoCompanionData
                or InitiativeLifecycleTransitionContract.CancellationReasonCompanionData
                or InitiativeLifecycleTransitionContract.HoldReasonCompanionData
                or InitiativeLifecycleTransitionContract.ClosureCompanionData))
            || transitions.Any(transition => transition.ApprovalDependencyDisposition is not (
                InitiativeLifecycleTransitionContract.DirectApprovalDisposition
                or InitiativeLifecycleTransitionContract.ApprovalAuthorityRequiredDisposition))
            || transitions.Any(transition =>
                transition.RequiredCompanionDataKind != CompanionDataFor(transition.TargetState))
            || transitions.Any(transition => transition.ApprovalDependencyDisposition
                != (RequiresApprovalAuthority(transition.SourceState, transition.TargetState)
                    ? InitiativeLifecycleTransitionContract.ApprovalAuthorityRequiredDisposition
                    : InitiativeLifecycleTransitionContract.DirectApprovalDisposition)))
            throw new InvalidOperationException("Initiative lifecycle transition contract is inconsistent.");

        ValidateVocabulary(contract.CancellationReasons, InitiativeVocabularies.CancellationReasons);
        ValidateVocabulary(contract.HoldReasons, InitiativeVocabularies.HoldReasons);
        ValidateVocabulary(contract.CompletionOutcomes, InitiativeVocabularies.CompletionOutcomes);
        ValidateVocabulary(contract.ClosureReasons, InitiativeVocabularies.ClosureReasons);
        ValidateVocabulary(contract.BenefitDispositions, InitiativeVocabularies.BenefitDispositions);
    }

    private static void ValidateVocabulary(IReadOnlyList<string> values, IReadOnlyList<string> canonicalValues)
    {
        if (values.Count == 0 || values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Count
            || !values.SequenceEqual(canonicalValues, StringComparer.Ordinal))
            throw new InvalidOperationException("Initiative lifecycle vocabulary is inconsistent.");
    }

    private static string CompanionDataFor(InitiativeLifecycleState target) => target switch
    {
        InitiativeLifecycleState.Cancelled => InitiativeLifecycleTransitionContract.CancellationReasonCompanionData,
        InitiativeLifecycleState.OnHold => InitiativeLifecycleTransitionContract.HoldReasonCompanionData,
        InitiativeLifecycleState.Completed => InitiativeLifecycleTransitionContract.ClosureCompanionData,
        _ => InitiativeLifecycleTransitionContract.NoCompanionData
    };

    private static bool RequiresApprovalAuthority(
        InitiativeLifecycleState source, InitiativeLifecycleState target) =>
        target == InitiativeLifecycleState.Active
        || target == InitiativeLifecycleState.Cancelled
            && source is InitiativeLifecycleState.Active or InitiativeLifecycleState.OnHold;

    private Initiative New(string code, string name, string? description, Guid? portfolioId, string? type,
        string? priority, DateOnly? start, DateOnly? end, Guid? supersedes) =>
        new(tenant.TenantId, actor.ActorId, code, name, description, portfolioId, type, priority, start, end, supersedes);

    private async Task<Response<InitiativeV2Dto>?> ValidateClassifications(string? type, string? priority, CancellationToken ct)
    {
        if (type is not null)
        {
            var result = await Classifications.GetTypesAsync(ct);
            if (!IsUsableClassificationResult(result)) return Response<InitiativeV2Dto>.Fail("Initiative type authority is unavailable.", 503);
            if (!result.Options.Any(x => string.Equals(x.Code, type, StringComparison.Ordinal))) return Response<InitiativeV2Dto>.Fail("Unknown InitiativeTypeCode.", 400);
        }
        if (priority is not null)
        {
            var result = await Classifications.GetPrioritiesAsync(ct);
            if (!IsUsableClassificationResult(result)) return Response<InitiativeV2Dto>.Fail("Initiative priority authority is unavailable.", 503);
            if (!result.Options.Any(x => string.Equals(x.Code, priority, StringComparison.Ordinal))) return Response<InitiativeV2Dto>.Fail("Unknown PriorityCode.", 400);
        }
        return null;
    }

    private static bool IsUsableClassificationResult(InitiativeClassificationResult result)
    {
        if (result.Disposition != InitiativeAuthorityDisposition.Valid || result.Options is null || result.Options.Count == 0)
            return false;

        var codes = new HashSet<string>(StringComparer.Ordinal);
        return result.Options.All(option => option is not null
            && !string.IsNullOrWhiteSpace(option.Code)
            && !string.IsNullOrWhiteSpace(option.Label)
            && codes.Add(option.Code));
    }

    private static Response<InitiativeLifecycleResult>? ValidateCompanionData(TransitionInitiativeLifecycleCommand request)
    {
        try
        {
            if (request.TargetState == InitiativeLifecycleState.Cancelled) InitiativeVocabularies.RequireCancellationReason(request.CancellationReasonCode!);
            if (request.TargetState == InitiativeLifecycleState.OnHold) InitiativeVocabularies.RequireHoldReason(request.HoldReasonCode!);
        }
        catch (ArgumentException) { return Response<InitiativeLifecycleResult>.Fail("A valid lifecycle reason is required.", 400); }
        if (request.TargetState == InitiativeLifecycleState.Completed && request.Closure is null)
            return Response<InitiativeLifecycleResult>.Fail("InitiativeClosure is required for completion.", 400);
        if (request.TargetState != InitiativeLifecycleState.Completed && request.Closure is not null)
            return Response<InitiativeLifecycleResult>.Fail("InitiativeClosure is accepted only for completion.", 400);
        return null;
    }

    private async Task<bool> WouldCreateCycle(Initiative successor, Initiative old, CancellationToken ct)
    {
        var seen = new HashSet<Guid> { successor.Id };
        Initiative? cursor = old;
        while (cursor is not null)
        {
            if (!seen.Add(cursor.Id)) return true;
            cursor = cursor.SupersedesInitiativeId is Guid parentId
                ? await repository.GetByIdAsync(tenant.TenantId, parentId, ct) : null;
        }
        return false;
    }

    private async Task<Response<T>?> Authorize<T>(string permission, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(permission, ct);
        return decision == PpmAccessDecision.Allowed ? null : decision.Failure<T>();
    }
    private async Task<bool> PortfolioExists(Guid? id, CancellationToken ct) => id is null || await portfolios.GetByIdAsync(tenant.TenantId, id.Value, ct) is not null;
    private AuditIntent Intent(Initiative entity, string mutation) => new(Guid.NewGuid(), tenant.TenantId,
        actor.ActorId, correlation.CorrelationId, nameof(Initiative), entity.Id, mutation, DateTime.UtcNow);

    private sealed class UnavailableAuthorities : IInitiativeClassificationAuthority,
        IInitiativeClosureReferenceAuthority
    {
        public static UnavailableAuthorities Instance { get; } = new();
        public Task<InitiativeClassificationResult> GetTypesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new InitiativeClassificationResult(InitiativeAuthorityDisposition.Unavailable, []));
        public Task<InitiativeClassificationResult> GetPrioritiesAsync(CancellationToken cancellationToken) => GetTypesAsync(cancellationToken);
        public Task<InitiativeAuthorityDisposition> ValidateEvidenceAsync(IReadOnlyList<InitiativeTypedReference> references, CancellationToken cancellationToken) =>
            Task.FromResult(references.Count == 0 ? InitiativeAuthorityDisposition.Valid : InitiativeAuthorityDisposition.Unavailable);
        public Task<InitiativeAuthorityDisposition> ValidateFollowUpTasksAsync(IReadOnlyList<InitiativeTypedReference> references, CancellationToken cancellationToken) =>
            Task.FromResult(references.Count == 0 ? InitiativeAuthorityDisposition.Valid : InitiativeAuthorityDisposition.Unavailable);
    }
}
