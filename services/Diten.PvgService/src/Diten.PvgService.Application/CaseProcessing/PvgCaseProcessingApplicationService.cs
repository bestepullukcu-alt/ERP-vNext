using Diten.PvgService.Domain.CaseProcessing;

namespace Diten.PvgService.Application.CaseProcessing;

public sealed class PvgCaseProcessingApplicationService
{
    private const string AcceptHandoffPermission = "pvg.mod0231.case-processing.accept-handoff";
    private const string UpdateAssessmentPermission = "pvg.mod0231.case-processing.update-assessment";
    private const string MarkReadyPermission = "pvg.mod0231.case-processing.mark-signal-minimum-ready";
    private const string ReadMetadataPermission = "pvg.mod0231.case-processing.read-metadata";

    private readonly Dictionary<string, SafetyCaseMaster> _caseMasters = new(StringComparer.Ordinal);

    public PvgCaseProcessingMutationResult AcceptMod0230Handoff(AcceptMod0230HandoffCommand command)
    {
        var validation = PvgCaseProcessingValidator.ValidateAcceptHandoff(command);
        if (!validation.IsValid)
        {
            return Blocked(validation);
        }

        var caseProcessingId = NewCaseProcessingId();
        var acceptedAt = DateTimeOffset.UtcNow;
        _caseMasters[caseProcessingId] = SafetyCaseMaster.AcceptHandoff(
            caseProcessingId,
            command.TenantContext.TenantId,
            command.HandoffReference,
            acceptedAt);

        return new PvgCaseProcessingMutationResult(
            PvgCaseProcessingResult.Accepted(Success("AcceptMod0230Handoff", AcceptHandoffPermission, command.ActorContext)),
            caseProcessingId);
    }

    public PvgCaseProcessingMutationResult UpdateSignalMinimumAssessment(UpdateSignalMinimumAssessmentCommand command)
    {
        var validation = PvgCaseProcessingValidator.ValidateUpdateAssessment(command);
        if (!validation.IsValid)
        {
            return Blocked(validation);
        }

        if (!TryGetCaseMaster(command.TenantContext, command.CaseProcessingId, out var caseMaster))
        {
            return NotFound();
        }

        _caseMasters[command.CaseProcessingId] = caseMaster.WithAssessment(command.Assessment, DateTimeOffset.UtcNow);

        return new PvgCaseProcessingMutationResult(
            PvgCaseProcessingResult.Accepted(Success("UpdateSignalMinimumAssessment", UpdateAssessmentPermission, command.ActorContext)),
            command.CaseProcessingId);
    }

    public PvgCaseProcessingMutationResult MarkSignalMinimumReady(MarkSignalMinimumReadyCommand command)
    {
        var validation = PvgCaseProcessingValidator.ValidateMarkSignalMinimumReady(command);
        if (!validation.IsValid)
        {
            return Blocked(validation);
        }

        if (!TryGetCaseMaster(command.TenantContext, command.CaseProcessingId, out var caseMaster))
        {
            return NotFound();
        }

        if (caseMaster.Assessment is null)
        {
            return new PvgCaseProcessingMutationResult(
                PvgCaseProcessingResult.Blocked(PvgCaseProcessingReasonCodes.AssessmentRequired),
                null);
        }

        _caseMasters[command.CaseProcessingId] = caseMaster.MarkSignalMinimumReady(DateTimeOffset.UtcNow);

        return new PvgCaseProcessingMutationResult(
            PvgCaseProcessingResult.Accepted(Success("MarkSignalMinimumReady", MarkReadyPermission, command.ActorContext)),
            command.CaseProcessingId);
    }

    public PvgCaseProcessingQueryResult GetByIdMetadata(GetCaseProcessingMetadataByIdQuery query)
    {
        var validation = PvgCaseProcessingValidator.ValidateGetById(query);
        if (!validation.IsValid)
        {
            return new PvgCaseProcessingQueryResult(
                PvgCaseProcessingResult.Blocked(validation.Failures.Select(failure => failure.ReasonCode).ToArray()),
                []);
        }

        if (!TryGetCaseMaster(query.TenantContext, query.CaseProcessingId, out var caseMaster))
        {
            return new PvgCaseProcessingQueryResult(PvgCaseProcessingResult.NotFound(), []);
        }

        return new PvgCaseProcessingQueryResult(
            PvgCaseProcessingResult.Accepted(Success("GetCaseProcessingMetadataById", ReadMetadataPermission, query.ActorContext)),
            [ToSummary(caseMaster)]);
    }

    public PvgCaseProcessingQueryResult ListMetadata(GetCaseProcessingMetadataListQuery query)
    {
        var validation = PvgCaseProcessingValidator.ValidateList(query);
        if (!validation.IsValid)
        {
            return new PvgCaseProcessingQueryResult(
                PvgCaseProcessingResult.Blocked(validation.Failures.Select(failure => failure.ReasonCode).ToArray()),
                []);
        }

        var items = _caseMasters
            .Values
            .Where(caseMaster => caseMaster.TenantId == query.TenantContext.TenantId)
            .Where(caseMaster => query.State is null || caseMaster.LifecycleState == query.State)
            .Skip(Math.Max(0, query.PageNumber - 1) * Math.Max(1, query.PageSize))
            .Take(Math.Max(1, query.PageSize))
            .Select(ToSummary)
            .ToArray();

        return new PvgCaseProcessingQueryResult(
            PvgCaseProcessingResult.Accepted(Success("ListCaseProcessingMetadata", ReadMetadataPermission, query.ActorContext)),
            items);
    }

    private static PvgCaseProcessingMutationResult Blocked(PvgCaseProcessingValidationResult validation) =>
        new(PvgCaseProcessingResult.Blocked(validation.Failures.Select(failure => failure.ReasonCode).ToArray()), null);

    private static PvgCaseProcessingMutationResult NotFound() =>
        new(PvgCaseProcessingResult.NotFound(), null);

    private bool TryGetCaseMaster(
        PvgCaseProcessingServerTenantContext tenantContext,
        string caseProcessingId,
        out SafetyCaseMaster caseMaster)
    {
        if (_caseMasters.TryGetValue(caseProcessingId, out var candidate) &&
            candidate.TenantId == tenantContext.TenantId)
        {
            caseMaster = candidate;
            return true;
        }

        caseMaster = null!;
        return false;
    }

    private static CaseProcessingMetadataSummary ToSummary(SafetyCaseMaster caseMaster) =>
        new(
            caseMaster.CaseProcessingId,
            caseMaster.Status,
            caseMaster.LifecycleState,
            caseMaster.Assessment is not null,
            caseMaster.LifecycleState == SignalMinimumLifecycleState.SignalMinimumReady);

    private static PvgCaseProcessingSuccessMetadata Success(
        string operation,
        string requiredPermission,
        PvgCaseProcessingActorContext actorContext) =>
        new(operation, requiredPermission, actorContext.ActorKind, true);

    private static string NewCaseProcessingId() => $"pvg-case-processing-{Guid.NewGuid():N}";
}
