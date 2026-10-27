using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed class PvgIntakeDraftApplicationService
{
    private const string CreatePermission = "pvg.mod0230.intake.create";
    private const string UpdatePermission = "pvg.mod0230.intake.update";
    private const string ReadPermission = "pvg.mod0230.intake.read";
    private const string TriagePermission = "pvg.mod0230.intake.triage";
    private const string RoutePermission = "pvg.mod0230.intake.route";

    private static readonly PvgIntakeField[] FieldSecurityControlledFields = PvgIntakeFieldDefinition
        .ApprovedFields
        .Where(definition => definition.Sensitivity != PvgFieldSensitivity.PublicMetadata)
        .Select(definition => definition.Field)
        .ToArray();
    private static readonly PvgIntakeField[] TriageFieldSecurityControlledFields =
    [
        PvgIntakeField.TriageOutcome,
        PvgIntakeField.TriageReason
    ];

    private readonly IPvgFieldSecurityPolicy _fieldSecurityPolicy;
    private readonly IPvgWorkflowTransitionGate _workflowTransitionGate;
    private readonly IPvgEvidenceLinkPort _evidenceLinkPort;
    private readonly IPvgPermissionGate _permissionGate;
    private readonly IPvgIntakeDraftStore _draftStore;

    public PvgIntakeDraftApplicationService(
        IPvgFieldSecurityPolicy fieldSecurityPolicy,
        IPvgWorkflowTransitionGate workflowTransitionGate,
        IPvgEvidenceLinkPort evidenceLinkPort,
        IPvgPermissionGate permissionGate,
        IPvgIntakeDraftStore draftStore)
    {
        _fieldSecurityPolicy = fieldSecurityPolicy;
        _workflowTransitionGate = workflowTransitionGate;
        _evidenceLinkPort = evidenceLinkPort;
        _permissionGate = permissionGate;
        _draftStore = draftStore;
    }

    public ValueTask<PvgIntakeDraftMutationResult> CreateDraftAsync(
        CreateIntakeDraftCommand command,
        CancellationToken cancellationToken = default) =>
        CreateDraftAsync(command, [], cancellationToken);

    // Order (pack AC-7): tenant context, actor / correlation / permission, then field validation (request shape
    // failures first), then the ports. An unauthorized caller therefore never sees validation reason codes.
    public async ValueTask<PvgIntakeDraftMutationResult> CreateDraftAsync(
        CreateIntakeDraftCommand command,
        IReadOnlyList<PvgValidationFailure> requestShapeFailures,
        CancellationToken cancellationToken = default)
    {
        var tenantValidation = ValidateTenantContext(command.TenantContext);
        if (!tenantValidation.IsValid)
        {
            return InvalidMutation(tenantValidation);
        }

        var guardrailDecision = await EnsureContextPermissionGuardrailsAsync(
            command.TenantContext,
            command.ActorContext,
            command.CorrelationContext,
            PvgIntakeOperation.Create,
            CreatePermission,
            cancellationToken);
        if (guardrailDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(guardrailDecision, null, null);
        }

        var validation = WithRequestShapeFailures(
            requestShapeFailures,
            PvgIntakeDraftValidator.ValidateCreate(command.TenantContext, command.Draft));
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Create,
            "create",
            command.TenantContext.TenantId,
            command.ActorContext.ActorId,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null, null);
        }

        if (HasEvidenceReferences(command.Draft.EvidenceLinkReferences))
        {
            var evidenceDecision = await EnsureEvidenceAsync(
                PvgIntakeOperation.Create,
                command.TenantContext.TenantId,
                command.ActorContext.ActorId,
                null,
                cancellationToken);
            if (evidenceDecision is not null)
            {
                return new PvgIntakeDraftMutationResult(evidenceDecision, null, null);
            }
        }

        var draftId = await _draftStore.AddAsync(
            Scope(command.TenantContext),
            BuildIntake(
                command.TenantContext.TenantId,
                PvgIntakeStatus.IntakeCreated,
                command.Draft,
                PvgReporterSource.Manual,
                null,
                PvgReporterCrmSearchState.Pending),
            cancellationToken);

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Create, PvgIntakeStatus.IntakeCreated),
            draftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Create, PvgIntakeStatus.IntakeCreated, CreatePermission, []));
    }

    public ValueTask<PvgIntakeDraftMutationResult> UpdateDraftAsync(
        UpdateIntakeDraftCommand command,
        CancellationToken cancellationToken = default) =>
        UpdateDraftAsync(command, [], cancellationToken);

    public async ValueTask<PvgIntakeDraftMutationResult> UpdateDraftAsync(
        UpdateIntakeDraftCommand command,
        IReadOnlyList<PvgValidationFailure> requestShapeFailures,
        CancellationToken cancellationToken = default)
    {
        var tenantValidation = ValidateTenantContext(command.TenantContext);
        if (!tenantValidation.IsValid)
        {
            return InvalidMutation(tenantValidation);
        }

        var guardrailDecision = await EnsureContextPermissionGuardrailsAsync(
            command.TenantContext,
            command.ActorContext,
            command.CorrelationContext,
            PvgIntakeOperation.Update,
            UpdatePermission,
            cancellationToken);
        if (guardrailDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(guardrailDecision, null, null);
        }

        var validation = WithRequestShapeFailures(
            requestShapeFailures,
            PvgIntakeDraftValidator.ValidateUpdate(command.TenantContext, command.Draft));
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
        }

        var stored = await GetDraftAsync(command.TenantContext, command.IntakeDraftId, cancellationToken);
        if (stored is null)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        var isLinked = stored.ReporterSource is PvgReporterSource.Crm or PvgReporterSource.PersonDirectory;
        var removesLink = command.Draft.RemoveReporterLink == true;
        var hasSelection = !string.IsNullOrWhiteSpace(command.Draft.ReporterSelectionDirectory) ||
            !string.IsNullOrWhiteSpace(command.Draft.ReporterSelectionId);
        if (isLinked && !hasSelection && !removesLink)
        {
            return InvalidMutation(PvgIntakeField.RemoveReporterLink, PvgValidationReasonCodes.ReporterLinkRemovalRequired);
        }

        if (removesLink && !isLinked)
        {
            return InvalidMutation(PvgIntakeField.RemoveReporterLink, PvgValidationReasonCodes.ReporterLinkAbsent);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Update,
            "update",
            command.TenantContext.TenantId,
            command.ActorContext.ActorId,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null, null);
        }

        if (HasEvidenceReferences(command.Draft.EvidenceLinkReferences))
        {
            var evidenceDecision = await EnsureEvidenceAsync(
                PvgIntakeOperation.Update,
                command.TenantContext.TenantId,
                command.ActorContext.ActorId,
                command.IntakeDraftId,
                cancellationToken);
            if (evidenceDecision is not null)
            {
                return new PvgIntakeDraftMutationResult(evidenceDecision, null, null);
            }
        }

        // Server-set reporter fields are carried from the stored record; only RemoveReporterLink changes them here.
        var updated = removesLink
            ? BuildIntake(
                command.TenantContext.TenantId,
                PvgIntakeStatus.IntakeUpdated,
                command.Draft,
                PvgReporterSource.Manual,
                null,
                PvgReporterCrmSearchState.ClosedByUserUnlink)
            : BuildIntake(
                command.TenantContext.TenantId,
                PvgIntakeStatus.IntakeUpdated,
                command.Draft,
                stored.ReporterSource,
                stored.ReporterSourceId,
                stored.ReporterCrmSearchState);
        var changedFields = ChangedFields(stored, updated);
        var replaced = await _draftStore.ReplaceAsync(
            Scope(command.TenantContext),
            command.IntakeDraftId,
            command.ExpectedVersion,
            updated,
            cancellationToken);
        if (replaced.Outcome == PvgIntakeDraftReplaceOutcome.NotFound)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        if (replaced.Outcome != PvgIntakeDraftReplaceOutcome.Replaced)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftVersionConflict);
        }

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Update, PvgIntakeStatus.IntakeUpdated),
            command.IntakeDraftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Update, PvgIntakeStatus.IntakeUpdated, UpdatePermission, changedFields),
            replaced.Version);
    }

    public async ValueTask<PvgIntakeDraftMutationResult> TriageDraftAsync(
        TriageIntakeDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantValidation = ValidateTenantContext(command.TenantContext);
        if (!tenantValidation.IsValid)
        {
            return InvalidMutation(tenantValidation);
        }

        var guardrailDecision = await EnsureContextPermissionGuardrailsAsync(
            command.TenantContext,
            command.ActorContext,
            command.CorrelationContext,
            PvgIntakeOperation.Triage,
            TriagePermission,
            cancellationToken);
        if (guardrailDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(guardrailDecision, null, null);
        }

        var validation = PvgIntakeDraftValidator.ValidateTriage(command.TenantContext, command.Draft);
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
        }

        var draft = await GetDraftAsync(command.TenantContext, command.IntakeDraftId, cancellationToken);
        if (draft is null)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Triage,
            "triage",
            command.TenantContext.TenantId,
            command.ActorContext.ActorId,
            TriageFieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null, null);
        }

        var workflowDecision = await EnsureWorkflowAsync(
            PvgIntakeOperation.Triage,
            command.TenantContext.TenantId,
            command.IntakeDraftId,
            command.ActorContext.ActorId,
            draft.Status,
            PvgIntakeStatus.Triaged,
            null,
            cancellationToken);
        if (workflowDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(workflowDecision, null, null);
        }

        var evidenceDecision = await EnsureEvidenceAsync(
            PvgIntakeOperation.Triage,
            command.TenantContext.TenantId,
            command.ActorContext.ActorId,
            command.IntakeDraftId,
            cancellationToken);
        if (evidenceDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(evidenceDecision, null, null);
        }

        draft.MarkTriaged(command.Draft.TriageOutcome!.Value, command.Draft.TriageReason);
        var triageReplaced = await _draftStore.ReplaceAsync(
            Scope(command.TenantContext),
            command.IntakeDraftId,
            command.ExpectedVersion,
            draft,
            cancellationToken);
        if (triageReplaced.Outcome == PvgIntakeDraftReplaceOutcome.NotFound)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        if (triageReplaced.Outcome != PvgIntakeDraftReplaceOutcome.Replaced)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftVersionConflict);
        }

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Triage, PvgIntakeStatus.Triaged),
            command.IntakeDraftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Triage, PvgIntakeStatus.Triaged, TriagePermission, []),
            triageReplaced.Version);
    }

    public async ValueTask<PvgIntakeDraftMutationResult> RouteDraftAsync(
        RouteIntakeDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantValidation = ValidateTenantContext(command.TenantContext);
        if (!tenantValidation.IsValid)
        {
            return InvalidMutation(tenantValidation);
        }

        var guardrailDecision = await EnsureContextPermissionGuardrailsAsync(
            command.TenantContext,
            command.ActorContext,
            command.CorrelationContext,
            PvgIntakeOperation.Route,
            RoutePermission,
            cancellationToken);
        if (guardrailDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(guardrailDecision, null, null);
        }

        var validation = PvgIntakeDraftValidator.ValidateRoute(command.TenantContext, command.Draft);
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
        }

        var draft = await GetDraftAsync(command.TenantContext, command.IntakeDraftId, cancellationToken);
        if (draft is null)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Route,
            "route",
            command.TenantContext.TenantId,
            command.ActorContext.ActorId,
            [PvgIntakeField.RouteTargetQueue],
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null, null);
        }

        var workflowDecision = await EnsureWorkflowAsync(
            PvgIntakeOperation.Route,
            command.TenantContext.TenantId,
            command.IntakeDraftId,
            command.ActorContext.ActorId,
            draft.Status,
            PvgIntakeStatus.RoutePending,
            null,
            cancellationToken);
        if (workflowDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(workflowDecision, null, null);
        }

        var evidenceDecision = await EnsureEvidenceAsync(
            PvgIntakeOperation.Route,
            command.TenantContext.TenantId,
            command.ActorContext.ActorId,
            command.IntakeDraftId,
            cancellationToken);
        if (evidenceDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(evidenceDecision, null, null);
        }

        draft.MarkRoutePending(Required(command.Draft.RouteTargetQueue));
        var routeReplaced = await _draftStore.ReplaceAsync(
            Scope(command.TenantContext),
            command.IntakeDraftId,
            command.ExpectedVersion,
            draft,
            cancellationToken);
        if (routeReplaced.Outcome == PvgIntakeDraftReplaceOutcome.NotFound)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        if (routeReplaced.Outcome != PvgIntakeDraftReplaceOutcome.Replaced)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftVersionConflict);
        }

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Route, PvgIntakeStatus.RoutePending),
            command.IntakeDraftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Route, PvgIntakeStatus.RoutePending, RoutePermission, []),
            routeReplaced.Version);
    }

    public async ValueTask<PvgIntakeDraftQueryResult> GetDraftByIdAsync(
        GetIntakeDraftByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantValidation = ValidateTenantContext(query.TenantContext);
        if (!tenantValidation.IsValid)
        {
            return InvalidQuery(tenantValidation);
        }

        var guardrailDecision = await EnsureContextPermissionGuardrailsAsync(
            query.TenantContext,
            query.ActorContext,
            query.CorrelationContext,
            PvgIntakeOperation.GetById,
            ReadPermission,
            cancellationToken);
        if (guardrailDecision is not null)
        {
            return new PvgIntakeDraftQueryResult(guardrailDecision, []);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.GetById,
            "detail",
            query.TenantContext.TenantId,
            query.ActorContext.ActorId,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftQueryResult(fieldDecision, []);
        }

        var persisted = await _draftStore.FindByIdAsync(
            Scope(query.TenantContext),
            query.IntakeDraftId,
            cancellationToken);
        if (persisted is null)
        {
            return BlockedQuery(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        return new PvgIntakeDraftQueryResult(
            Succeeded(PvgIntakeOperation.GetById, persisted.Intake.Status),
            [new PvgIntakeDraftSummary(persisted.IntakeDraftId, persisted.Intake.Status, persisted.Version)],
            Intake: persisted.Intake);
    }

    public async ValueTask<PvgIntakeDraftQueryResult> ListDraftsAsync(
        GetIntakeDraftListQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantValidation = ValidateTenantContext(query.TenantContext);
        if (!tenantValidation.IsValid)
        {
            return InvalidQuery(tenantValidation);
        }

        var guardrailDecision = await EnsureContextPermissionGuardrailsAsync(
            query.TenantContext,
            query.ActorContext,
            query.CorrelationContext,
            PvgIntakeOperation.GetList,
            ReadPermission,
            cancellationToken);
        if (guardrailDecision is not null)
        {
            return new PvgIntakeDraftQueryResult(guardrailDecision, []);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.GetList,
            "list",
            query.TenantContext.TenantId,
            query.ActorContext.ActorId,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftQueryResult(fieldDecision, []);
        }

        var persistedItems = await _draftStore.ListAsync(
            new PvgPersistenceListScope(Scope(query.TenantContext), query.PageNumber, query.PageSize, query.Status),
            cancellationToken);
        var items = persistedItems
            .Select(item => new PvgIntakeDraftSummary(item.IntakeDraftId, item.Intake.Status, item.Version))
            .ToArray();
        var tenantScope = Scope(query.TenantContext);
        var totalCount = await _draftStore.CountAsync(tenantScope, null, cancellationToken);
        var filteredCount = query.Status is null
            ? totalCount
            : await _draftStore.CountAsync(tenantScope, query.Status, cancellationToken);

        return new PvgIntakeDraftQueryResult(
            Succeeded(PvgIntakeOperation.GetList, null),
            items,
            totalCount,
            filteredCount);
    }

    private async ValueTask<PvgApplicationResult?> EnsureFieldSecurityAsync(
        PvgIntakeOperation operation,
        string surface,
        string tenantId,
        string? actorId,
        IEnumerable<PvgIntakeField> fields,
        CancellationToken cancellationToken)
    {
        foreach (var field in fields.Distinct())
        {
            var decision = await _fieldSecurityPolicy.EvaluateAsync(
                new PvgFieldSecurityRequest(operation, surface, field.ToString(), tenantId, actorId, null, null),
                cancellationToken);
            if (!decision.IsAllowed || !decision.IsSatisfied)
            {
                return PvgApplicationResult.Blocked(decision.ReasonCode);
            }
        }

        return null;
    }

    private async ValueTask<PvgApplicationResult?> EnsureWorkflowAsync(
        PvgIntakeOperation operation,
        string tenantId,
        string intakeDraftId,
        string actorId,
        PvgIntakeStatus fromState,
        PvgIntakeStatus toState,
        string? routeTargetQueue,
        CancellationToken cancellationToken)
    {
        var decision = await _workflowTransitionGate.EvaluateAsync(
            new PvgWorkflowTransitionRequest(
                operation,
                tenantId,
                intakeDraftId,
                actorId,
                fromState.ToString(),
                toState.ToString(),
                routeTargetQueue,
                null),
            cancellationToken);

        return decision.IsAllowed && decision.IsSatisfied
            ? null
            : PvgApplicationResult.Blocked(decision.ReasonCode);
    }

    private async ValueTask<PvgApplicationResult?> EnsureEvidenceAsync(
        PvgIntakeOperation operation,
        string tenantId,
        string actorId,
        string? intakeDraftId,
        CancellationToken cancellationToken)
    {
        var decision = await _evidenceLinkPort.EvaluateAsync(
            new PvgEvidenceLinkRequest(operation, tenantId, intakeDraftId, actorId, null, null),
            cancellationToken);

        return decision.IsAllowed && decision.IsSatisfied
            ? null
            : PvgApplicationResult.Blocked(decision.ReasonCode);
    }

    private async ValueTask<SafetyCaseIntake?> GetDraftAsync(
        PvgServerTenantContext tenantContext,
        string intakeDraftId,
        CancellationToken cancellationToken)
    {
        var persisted = await _draftStore.FindByIdAsync(Scope(tenantContext), intakeDraftId, cancellationToken);
        return persisted?.Intake;
    }

    private static PvgValidationResult ValidateTenantContext(PvgServerTenantContext? tenantContext)
    {
        return tenantContext is null || string.IsNullOrWhiteSpace(tenantContext.TenantId)
            ? new PvgValidationResult([new PvgValidationFailure(null, PvgValidationReasonCodes.TenantContextRequired)])
            : PvgValidationResult.Valid;
    }

    private static PvgValidationResult WithRequestShapeFailures(
        IReadOnlyList<PvgValidationFailure> requestShapeFailures,
        PvgValidationResult validation) =>
        requestShapeFailures.Count == 0
            ? validation
            : new PvgValidationResult([.. requestShapeFailures, .. validation.Failures]);

    private static PvgIntakeDraftMutationResult InvalidMutation(PvgValidationResult validation) =>
        new(PvgApplicationResult.Invalid(validation.Failures), null, null);

    private static PvgIntakeDraftMutationResult InvalidMutation(PvgIntakeField field, string reasonCode) =>
        InvalidMutation(new PvgValidationResult([new PvgValidationFailure(field, reasonCode)]));

    private static PvgIntakeDraftQueryResult InvalidQuery(PvgValidationResult validation) =>
        new(PvgApplicationResult.Invalid(validation.Failures), []);

    private static PvgIntakeDraftMutationResult BlockedMutation(string reasonCode) =>
        new(PvgApplicationResult.Blocked(reasonCode), null, null);

    private static PvgIntakeDraftQueryResult BlockedQuery(string reasonCode) =>
        new(PvgApplicationResult.Blocked(reasonCode), []);

    private static PvgApplicationResult Succeeded(PvgIntakeOperation operation, PvgIntakeStatus? status) =>
        PvgApplicationResult.Succeeded(new PvgApplicationSuccessMetadata(operation, status, DateTimeOffset.UtcNow));

    private async ValueTask<PvgApplicationResult?> EnsureContextPermissionGuardrailsAsync(
        PvgServerTenantContext tenantContext,
        PvgActorContext? actorContext,
        PvgCorrelationContext? correlationContext,
        PvgIntakeOperation operation,
        string requiredPermission,
        CancellationToken cancellationToken)
    {
        if (actorContext is null ||
            string.IsNullOrWhiteSpace(actorContext.ActorId) ||
            string.IsNullOrWhiteSpace(actorContext.ActorKind))
        {
            return PvgApplicationResult.Blocked(PvgPermissionReasonCodes.ActorContextRequired);
        }

        var correlationValidation = ValidateCorrelationContext(correlationContext);
        if (correlationValidation is not null)
        {
            return correlationValidation;
        }

        var permissionDecision = await _permissionGate.EvaluateAsync(
            new PvgPermissionRequest(
                operation,
                requiredPermission,
                tenantContext.TenantId,
                actorContext.ActorId,
                correlationContext!.CorrelationId),
            cancellationToken);

        return permissionDecision.IsAllowed
            ? null
            : PvgApplicationResult.Blocked(permissionDecision.ReasonCode);
    }

    private static PvgApplicationResult? ValidateCorrelationContext(PvgCorrelationContext? correlationContext)
    {
        if (correlationContext is null || string.IsNullOrWhiteSpace(correlationContext.CorrelationId))
        {
            return PvgApplicationResult.Blocked(PvgPermissionReasonCodes.CorrelationContextRequired);
        }

        var correlationId = correlationContext.CorrelationId.Trim();
        return correlationId.Length > 128 || correlationId.Any(char.IsWhiteSpace)
            ? PvgApplicationResult.Blocked(PvgPermissionReasonCodes.CorrelationContextInvalid)
            : null;
    }

    private static PvgAuditIntent BuildAuditIntent(
        PvgActorContext actorContext,
        PvgIntakeOperation operation,
        PvgIntakeStatus status,
        string requiredPermission,
        IReadOnlyList<PvgIntakeField> changedFields) =>
        new(operation, status, requiredPermission, actorContext.ActorKind, true, DateTimeOffset.UtcNow, changedFields);

    // Field identifiers only, never values: every stored field whose value differs between the stored and the
    // written record. Instants are compared as instants; product lines column by column in order.
    private static IReadOnlyList<PvgIntakeField> ChangedFields(SafetyCaseIntake stored, SafetyCaseIntake updated)
    {
        var changed = new List<PvgIntakeField>();
        AddIfChanged(changed, PvgIntakeField.IntakeChannel, !SameText(stored.IntakeChannel, updated.IntakeChannel));
        AddIfChanged(changed, PvgIntakeField.SourceType, !SameText(stored.SourceType, updated.SourceType));
        AddIfChanged(changed, PvgIntakeField.SourceReference, !SameText(stored.SourceReference, updated.SourceReference));
        AddIfChanged(changed, PvgIntakeField.ReceivedAtUtc, stored.ReceivedAtUtc != updated.ReceivedAtUtc);
        AddIfChanged(changed, PvgIntakeField.ReporterType, !SameText(stored.ReporterType, updated.ReporterType));
        AddIfChanged(
            changed,
            PvgIntakeField.ReporterContactSummary,
            !SameText(stored.ReporterContactSummary, updated.ReporterContactSummary));
        AddIfChanged(changed, PvgIntakeField.PatientSubjectCode, !SameText(stored.PatientSubjectCode, updated.PatientSubjectCode));
        AddIfChanged(changed, PvgIntakeField.EventOnsetDate, stored.EventOnsetDate != updated.EventOnsetDate);
        AddIfChanged(
            changed,
            PvgIntakeField.AdverseEventNarrative,
            !SameText(stored.AdverseEventNarrative, updated.AdverseEventNarrative));
        AddIfChanged(changed, PvgIntakeField.Seriousness, !SameText(stored.Seriousness, updated.Seriousness));
        AddIfChanged(changed, PvgIntakeField.IntakePriority, !SameText(stored.IntakePriority, updated.IntakePriority));
        AddIfChanged(changed, PvgIntakeField.TriageOutcome, stored.TriageOutcome != updated.TriageOutcome);
        AddIfChanged(changed, PvgIntakeField.TriageReason, !SameText(stored.TriageReason, updated.TriageReason));
        AddIfChanged(changed, PvgIntakeField.RouteTargetQueue, !SameText(stored.RouteTargetQueue, updated.RouteTargetQueue));
        AddIfChanged(
            changed,
            PvgIntakeField.EvidenceLinkReferences,
            !stored.EvidenceLinkReferences.SequenceEqual(updated.EvidenceLinkReferences, StringComparer.Ordinal));
        AddIfChanged(changed, PvgIntakeField.ProductLines, !stored.ProductLines.SequenceEqual(updated.ProductLines));
        AddIfChanged(changed, PvgIntakeField.AwarenessAtUtc, stored.AwarenessAtUtc != updated.AwarenessAtUtc);
        AddIfChanged(
            changed,
            PvgIntakeField.CompanyRecipientPersonId,
            !SameText(stored.CompanyRecipientPersonId, updated.CompanyRecipientPersonId));
        AddIfChanged(changed, PvgIntakeField.EventCountry, !SameText(stored.EventCountry, updated.EventCountry));
        AddIfChanged(changed, PvgIntakeField.ReportType, !SameText(stored.ReportType, updated.ReportType));
        AddIfChanged(changed, PvgIntakeField.ReporterSource, stored.ReporterSource != updated.ReporterSource);
        AddIfChanged(changed, PvgIntakeField.ReporterSourceId, !SameText(stored.ReporterSourceId, updated.ReporterSourceId));
        AddIfChanged(
            changed,
            PvgIntakeField.ReporterCrmSearchState,
            stored.ReporterCrmSearchState != updated.ReporterCrmSearchState);
        AddIfChanged(
            changed,
            PvgIntakeField.ReporterNameOrInitials,
            !SameText(stored.ReporterNameOrInitials, updated.ReporterNameOrInitials));
        AddIfChanged(changed, PvgIntakeField.ReporterProfession, !SameText(stored.ReporterProfession, updated.ReporterProfession));
        AddIfChanged(changed, PvgIntakeField.ReporterCountry, !SameText(stored.ReporterCountry, updated.ReporterCountry));

        return changed;
    }

    private static void AddIfChanged(List<PvgIntakeField> changed, PvgIntakeField field, bool isChanged)
    {
        if (isChanged)
        {
            changed.Add(field);
        }
    }

    private static bool SameText(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);

    private static bool HasEvidenceReferences(IReadOnlyCollection<string>? evidenceLinkReferences) =>
        evidenceLinkReferences is not null && evidenceLinkReferences.Any(reference => !string.IsNullOrWhiteSpace(reference));

    private static PvgPersistenceTenantScope Scope(PvgServerTenantContext tenantContext) =>
        new(tenantContext.TenantId);

    private static string Required(string? value) => value!.Trim();

    private static string? OptionalTrimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Create and Update share one builder so normalization (UTC awareness, trimmed values) cannot differ by path.
    private static SafetyCaseIntake BuildIntake(
        string tenantId,
        PvgIntakeStatus status,
        PvgUpdateIntakeDraftRequest draft,
        PvgReporterSource reporterSource,
        string? reporterSourceId,
        PvgReporterCrmSearchState reporterCrmSearchState) =>
        BuildIntake(
            tenantId,
            status,
            new PvgCreateIntakeDraftRequest(
                draft.IntakeChannel,
                draft.SourceType,
                draft.SourceReference,
                draft.ReceivedAtUtc,
                draft.ReporterType,
                draft.ReporterContactSummary,
                draft.PatientSubjectCode,
                draft.EventOnsetDate,
                draft.AdverseEventNarrative,
                draft.ProductLines,
                draft.Seriousness,
                draft.IntakePriority,
                draft.EvidenceLinkReferences,
                draft.AwarenessAtUtc,
                draft.CompanyRecipientPersonId,
                draft.EventCountry,
                draft.ReportType,
                draft.ReporterSelectionDirectory,
                draft.ReporterSelectionId,
                draft.ReporterNameOrInitials,
                draft.ReporterProfession,
                draft.ReporterCountry),
            reporterSource,
            reporterSourceId,
            reporterCrmSearchState);

    private static SafetyCaseIntake BuildIntake(
        string tenantId,
        PvgIntakeStatus status,
        PvgCreateIntakeDraftRequest draft,
        PvgReporterSource reporterSource,
        string? reporterSourceId,
        PvgReporterCrmSearchState reporterCrmSearchState) =>
        new(
            tenantId,
            status,
            Required(draft.IntakeChannel),
            Required(draft.SourceType),
            draft.ReceivedAtUtc!.Value,
            Required(draft.ReporterType),
            Required(draft.AdverseEventNarrative),
            Required(draft.Seriousness),
            Required(draft.IntakePriority))
        {
            SourceReference = draft.SourceReference,
            ReporterContactSummary = draft.ReporterContactSummary,
            PatientSubjectCode = draft.PatientSubjectCode,
            EventOnsetDate = draft.EventOnsetDate,
            EvidenceLinkReferences = draft.EvidenceLinkReferences ?? [],
            ProductLines = (draft.ProductLines ?? [])
                .Select(line => new PvgIntakeProductLine(
                    Required(line.ProductRole),
                    OptionalTrimmed(line.GlobalProductId),
                    Required(line.ProductNameAsReported),
                    OptionalTrimmed(line.PresentationGskuId),
                    OptionalTrimmed(line.LotNumber)))
                .ToArray(),
            AwarenessAtUtc = draft.AwarenessAtUtc!.Value.ToUniversalTime(),
            CompanyRecipientPersonId = OptionalTrimmed(draft.CompanyRecipientPersonId),
            EventCountry = Required(draft.EventCountry),
            ReportType = Required(draft.ReportType),
            ReporterSource = reporterSource,
            ReporterSourceId = reporterSourceId,
            ReporterCrmSearchState = reporterCrmSearchState,
            ReporterNameOrInitials = OptionalTrimmed(draft.ReporterNameOrInitials),
            ReporterProfession = OptionalTrimmed(draft.ReporterProfession),
            ReporterCountry = OptionalTrimmed(draft.ReporterCountry)
        };
}
