using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed class PvgIntakeDraftApplicationService
{
    private const string CreatePermission = "pvg.mod0230.intake.create";
    private const string UpdatePermission = "pvg.mod0230.intake.update";
    private const string TriagePermission = "pvg.mod0230.intake.triage";
    private const string RoutePermission = "pvg.mod0230.intake.route";

    private static readonly PvgIntakeField[] FieldSecurityControlledFields = PvgIntakeFieldDefinition
        .ApprovedFields
        .Where(definition => definition.Sensitivity != PvgFieldSensitivity.PublicMetadata)
        .Select(definition => definition.Field)
        .ToArray();

    private readonly IPvgFieldSecurityPolicy _fieldSecurityPolicy;
    private readonly IPvgWorkflowTransitionGate _workflowTransitionGate;
    private readonly IPvgEvidenceLinkPort _evidenceLinkPort;
    private readonly IPvgPermissionGate _permissionGate;
    private readonly Dictionary<string, SafetyCaseIntake> _drafts = new(StringComparer.Ordinal);

    public PvgIntakeDraftApplicationService(
        IPvgFieldSecurityPolicy fieldSecurityPolicy,
        IPvgWorkflowTransitionGate workflowTransitionGate,
        IPvgEvidenceLinkPort evidenceLinkPort,
        IPvgPermissionGate permissionGate)
    {
        _fieldSecurityPolicy = fieldSecurityPolicy;
        _workflowTransitionGate = workflowTransitionGate;
        _evidenceLinkPort = evidenceLinkPort;
        _permissionGate = permissionGate;
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

        var guardrailDecision = await EnsureMutationGuardrailsAsync(
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

        var draftId = NewDraftId();
        _drafts[draftId] = new SafetyCaseIntake(
            command.TenantContext.TenantId,
            PvgIntakeStatus.IntakeCreated,
            Required(command.Draft.IntakeChannel),
            Required(command.Draft.SourceType),
            command.Draft.ReceivedAtUtc!.Value,
            Required(command.Draft.ReporterType),
            Required(command.Draft.AdverseEventNarrative),
            Required(command.Draft.Seriousness),
            Required(command.Draft.IntakePriority))
        {
            SourceReference = command.Draft.SourceReference,
            ReporterContactSummary = command.Draft.ReporterContactSummary,
            PatientSubjectCode = command.Draft.PatientSubjectCode,
            EventOnsetDate = command.Draft.EventOnsetDate,
            SuspectProductText = command.Draft.SuspectProductText,
            EvidenceLinkReferences = command.Draft.EvidenceLinkReferences ?? []
        };

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

        var guardrailDecision = await EnsureMutationGuardrailsAsync(
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

        if (!TryGetDraft(command.TenantContext, command.IntakeDraftId, out _))
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

        var updated = new SafetyCaseIntake(
            command.TenantContext.TenantId,
            PvgIntakeStatus.IntakeUpdated,
            Required(command.Draft.IntakeChannel),
            Required(command.Draft.SourceType),
            command.Draft.ReceivedAtUtc!.Value,
            Required(command.Draft.ReporterType),
            Required(command.Draft.AdverseEventNarrative),
            Required(command.Draft.Seriousness),
            Required(command.Draft.IntakePriority))
        {
            SourceReference = command.Draft.SourceReference,
            ReporterContactSummary = command.Draft.ReporterContactSummary,
            PatientSubjectCode = command.Draft.PatientSubjectCode,
            EventOnsetDate = command.Draft.EventOnsetDate,
            SuspectProductText = command.Draft.SuspectProductText,
            EvidenceLinkReferences = command.Draft.EvidenceLinkReferences ?? []
        };

        _drafts[command.IntakeDraftId] = updated;

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

        var guardrailDecision = await EnsureMutationGuardrailsAsync(
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

        if (!TryGetDraft(command.TenantContext, command.IntakeDraftId, out var draft))
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
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

        var guardrailDecision = await EnsureMutationGuardrailsAsync(
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

        if (!TryGetDraft(command.TenantContext, command.IntakeDraftId, out var draft))
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

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.GetById,
            "detail",
            query.TenantContext.TenantId,
            null,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftQueryResult(fieldDecision, []);
        }

        if (!TryGetDraft(query.TenantContext, query.IntakeDraftId, out var draft))
        {
            return BlockedQuery(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        return new PvgIntakeDraftQueryResult(
            Succeeded(PvgIntakeOperation.GetById, draft.Status),
            [new PvgIntakeDraftSummary(query.IntakeDraftId, draft.Status)]);
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

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.GetList,
            "list",
            query.TenantContext.TenantId,
            null,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftQueryResult(fieldDecision, []);
        }

        var items = _drafts
            .Where(pair => pair.Value.TenantId == query.TenantContext.TenantId)
            .Where(pair => query.Status is null || pair.Value.Status == query.Status)
            .Skip(Math.Max(0, query.PageNumber - 1) * Math.Max(1, query.PageSize))
            .Take(Math.Max(1, query.PageSize))
            .Select(pair => new PvgIntakeDraftSummary(pair.Key, pair.Value.Status))
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

    private bool TryGetDraft(PvgServerTenantContext tenantContext, string intakeDraftId, out SafetyCaseIntake draft)
    {
        if (_drafts.TryGetValue(intakeDraftId, out var candidate) &&
            candidate.TenantId == tenantContext.TenantId)
        {
            draft = candidate;
            return true;
        }

        draft = null!;
        return false;
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

    private async ValueTask<PvgApplicationResult?> EnsureMutationGuardrailsAsync(
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

    private static string NewDraftId() => $"pvg-draft-{Guid.NewGuid():N}";

    private static string Required(string? value) => value!.Trim();
}
