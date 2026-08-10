using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed class PvgIntakeDraftApplicationService
{
    private static readonly PvgIntakeField[] FieldSecurityControlledFields = PvgIntakeFieldDefinition
        .ApprovedFields
        .Where(definition => definition.Sensitivity != PvgFieldSensitivity.PublicMetadata)
        .Select(definition => definition.Field)
        .ToArray();

    private readonly IPvgFieldSecurityPolicy _fieldSecurityPolicy;
    private readonly IPvgWorkflowTransitionGate _workflowTransitionGate;
    private readonly IPvgEvidenceLinkPort _evidenceLinkPort;
    private readonly Dictionary<string, SafetyCaseIntake> _drafts = new(StringComparer.Ordinal);

    public PvgIntakeDraftApplicationService(
        IPvgFieldSecurityPolicy fieldSecurityPolicy,
        IPvgWorkflowTransitionGate workflowTransitionGate,
        IPvgEvidenceLinkPort evidenceLinkPort)
    {
        _fieldSecurityPolicy = fieldSecurityPolicy;
        _workflowTransitionGate = workflowTransitionGate;
        _evidenceLinkPort = evidenceLinkPort;
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

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Create,
            "create",
            command.TenantContext.TenantId,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null);
        }

        if (HasEvidenceReferences(command.Draft.EvidenceLinkReferences))
        {
            var evidenceDecision = await EnsureEvidenceAsync(
                PvgIntakeOperation.Create,
                command.TenantContext.TenantId,
                null,
                cancellationToken);
            if (evidenceDecision is not null)
            {
                return new PvgIntakeDraftMutationResult(evidenceDecision, null);
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

        return new PvgIntakeDraftMutationResult(Succeeded(PvgIntakeOperation.Create, PvgIntakeStatus.IntakeCreated), draftId);
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

        if (!TryGetDraft(command.TenantContext, command.IntakeDraftId, out _))
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Update,
            "update",
            command.TenantContext.TenantId,
            FieldSecurityControlledFields,
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null);
        }

        if (HasEvidenceReferences(command.Draft.EvidenceLinkReferences))
        {
            var evidenceDecision = await EnsureEvidenceAsync(
                PvgIntakeOperation.Update,
                command.TenantContext.TenantId,
                command.IntakeDraftId,
                cancellationToken);
            if (evidenceDecision is not null)
            {
                return new PvgIntakeDraftMutationResult(evidenceDecision, null);
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
            command.IntakeDraftId);
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

        if (!TryGetDraft(command.TenantContext, command.IntakeDraftId, out var draft))
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        var workflowDecision = await EnsureWorkflowAsync(
            PvgIntakeOperation.Triage,
            command.TenantContext.TenantId,
            command.IntakeDraftId,
            draft.Status,
            PvgIntakeStatus.Triaged,
            null,
            cancellationToken);
        if (workflowDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(workflowDecision, null);
        }

        var evidenceDecision = await EnsureEvidenceAsync(
            PvgIntakeOperation.Triage,
            command.TenantContext.TenantId,
            command.IntakeDraftId,
            cancellationToken);
        if (evidenceDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(evidenceDecision, null);
        }

        draft.MarkTriaged(command.Draft.TriageOutcome!.Value, command.Draft.TriageReason);

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Triage, PvgIntakeStatus.Triaged),
            command.IntakeDraftId);
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

        if (!TryGetDraft(command.TenantContext, command.IntakeDraftId, out var draft))
        {
            return BlockedMutation(PvgApplicationReasonCodes.IntakeDraftNotFound);
        }

        var fieldDecision = await EnsureFieldSecurityAsync(
            PvgIntakeOperation.Route,
            "route",
            command.TenantContext.TenantId,
            [PvgIntakeField.RouteTargetQueue],
            cancellationToken);
        if (fieldDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(fieldDecision, null);
        }

        var workflowDecision = await EnsureWorkflowAsync(
            PvgIntakeOperation.Route,
            command.TenantContext.TenantId,
            command.IntakeDraftId,
            draft.Status,
            PvgIntakeStatus.RoutePending,
            null,
            cancellationToken);
        if (workflowDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(workflowDecision, null);
        }

        var evidenceDecision = await EnsureEvidenceAsync(
            PvgIntakeOperation.Route,
            command.TenantContext.TenantId,
            command.IntakeDraftId,
            cancellationToken);
        if (evidenceDecision is not null)
        {
            return new PvgIntakeDraftMutationResult(evidenceDecision, null);
        }

        draft.MarkRoutePending(Required(command.Draft.RouteTargetQueue));

        return new PvgIntakeDraftMutationResult(
            Succeeded(PvgIntakeOperation.Route, PvgIntakeStatus.RoutePending),
            command.IntakeDraftId);
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
        IEnumerable<PvgIntakeField> fields,
        CancellationToken cancellationToken)
    {
        foreach (var field in fields.Distinct())
        {
            var decision = await _fieldSecurityPolicy.EvaluateAsync(
                new PvgFieldSecurityRequest(operation, surface, field.ToString(), tenantId, null, null, null),
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
                null,
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
        string? intakeDraftId,
        CancellationToken cancellationToken)
    {
        var decision = await _evidenceLinkPort.EvaluateAsync(
            new PvgEvidenceLinkRequest(operation, tenantId, intakeDraftId, null, null, null),
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
        new(PvgApplicationResult.Invalid(validation.Failures), null);

    private static PvgIntakeDraftQueryResult InvalidQuery(PvgValidationResult validation) =>
        new(PvgApplicationResult.Invalid(validation.Failures), []);

    private static PvgIntakeDraftMutationResult BlockedMutation(string reasonCode) =>
        new(PvgApplicationResult.Blocked(reasonCode), null);

    private static PvgIntakeDraftQueryResult BlockedQuery(string reasonCode) =>
        new(PvgApplicationResult.Blocked(reasonCode), []);

    private static PvgApplicationResult Succeeded(PvgIntakeOperation operation, PvgIntakeStatus? status) =>
        PvgApplicationResult.Succeeded(new PvgApplicationSuccessMetadata(operation, status, DateTimeOffset.UtcNow));

    private static bool HasEvidenceReferences(IReadOnlyCollection<string>? evidenceLinkReferences) =>
        evidenceLinkReferences is not null && evidenceLinkReferences.Any(reference => !string.IsNullOrWhiteSpace(reference));

    private static string NewDraftId() => $"pvg-draft-{Guid.NewGuid():N}";

    private static string Required(string? value) => value!.Trim();
}
