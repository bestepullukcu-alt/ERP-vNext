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

    public async ValueTask<PvgIntakeDraftMutationResult> CreateDraftAsync(
        CreateIntakeDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = PvgIntakeDraftValidator.ValidateCreate(command.TenantContext, command.Draft);
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
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
            BuildIntake(command.TenantContext.TenantId, PvgIntakeStatus.IntakeCreated, command.Draft),
            cancellationToken);

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Create, PvgIntakeStatus.IntakeCreated),
            draftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Create, PvgIntakeStatus.IntakeCreated, CreatePermission));
    }

    public async ValueTask<PvgIntakeDraftMutationResult> UpdateDraftAsync(
        UpdateIntakeDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = PvgIntakeDraftValidator.ValidateUpdate(command.TenantContext, command.Draft);
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
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

        if (!await TryGetDraftAsync(command.TenantContext, command.IntakeDraftId, cancellationToken))
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
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

        var updated = BuildIntake(command.TenantContext.TenantId, PvgIntakeStatus.IntakeUpdated, command.Draft);
        var replaced = await _draftStore.ReplaceAsync(
            Scope(command.TenantContext),
            command.IntakeDraftId,
            updated,
            cancellationToken);
        if (!replaced)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Update, PvgIntakeStatus.IntakeUpdated),
            command.IntakeDraftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Update, PvgIntakeStatus.IntakeUpdated, UpdatePermission));
    }

    public async ValueTask<PvgIntakeDraftMutationResult> TriageDraftAsync(
        TriageIntakeDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = PvgIntakeDraftValidator.ValidateTriage(command.TenantContext, command.Draft);
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
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
            draft,
            cancellationToken);
        if (!triageReplaced)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Triage, PvgIntakeStatus.Triaged),
            command.IntakeDraftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Triage, PvgIntakeStatus.Triaged, TriagePermission));
    }

    public async ValueTask<PvgIntakeDraftMutationResult> RouteDraftAsync(
        RouteIntakeDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = PvgIntakeDraftValidator.ValidateRoute(command.TenantContext, command.Draft);
        if (!validation.IsValid)
        {
            return InvalidMutation(validation);
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
            draft,
            cancellationToken);
        if (!routeReplaced)
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Route, PvgIntakeStatus.RoutePending),
            command.IntakeDraftId,
            BuildAuditIntent(command.ActorContext, PvgIntakeOperation.Route, PvgIntakeStatus.RoutePending, RoutePermission));
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
            [new PvgIntakeDraftSummary(persisted.IntakeDraftId, persisted.Intake.Status)]);
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
            .Select(item => new PvgIntakeDraftSummary(item.IntakeDraftId, item.Intake.Status))
            .ToArray();

        return new PvgIntakeDraftQueryResult(
            Succeeded(PvgIntakeOperation.GetList, null),
            items);
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

    private async ValueTask<bool> TryGetDraftAsync(
        PvgServerTenantContext tenantContext,
        string intakeDraftId,
        CancellationToken cancellationToken) =>
        await _draftStore.FindByIdAsync(Scope(tenantContext), intakeDraftId, cancellationToken) is not null;

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

    private static PvgIntakeDraftMutationResult InvalidMutation(PvgValidationResult validation) =>
        new(PvgApplicationResult.Invalid(validation.Failures), null, null);

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
        string requiredPermission) =>
        new(operation, status, requiredPermission, actorContext.ActorKind, true, DateTimeOffset.UtcNow);

    private static bool HasEvidenceReferences(IReadOnlyCollection<string>? evidenceLinkReferences) =>
        evidenceLinkReferences is not null && evidenceLinkReferences.Any(reference => !string.IsNullOrWhiteSpace(reference));

    private static PvgPersistenceTenantScope Scope(PvgServerTenantContext tenantContext) =>
        new(tenantContext.TenantId);

    private static string Required(string? value) => value!.Trim();

    private static SafetyCaseIntake BuildIntake(
        string tenantId,
        PvgIntakeStatus status,
        PvgCreateIntakeDraftRequest draft) =>
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
            SuspectProductText = draft.SuspectProductText,
            EvidenceLinkReferences = draft.EvidenceLinkReferences ?? []
        };

    private static SafetyCaseIntake BuildIntake(
        string tenantId,
        PvgIntakeStatus status,
        PvgUpdateIntakeDraftRequest draft) =>
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
            SuspectProductText = draft.SuspectProductText,
            EvidenceLinkReferences = draft.EvidenceLinkReferences ?? []
        };
}
