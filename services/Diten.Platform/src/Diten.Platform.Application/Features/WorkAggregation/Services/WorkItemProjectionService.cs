using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Application.Features.WorkAggregation.Services;

// WC-1 (DCP-004) — pure ApprovalTask → canonical work-item projection.
//
// Charter §10.1 status map, §10.2 single authoritative actions[] (native + permission + assignment/blocker),
// §10.3 source/lifecycleOwner split. No state is written; the same input always yields the same output.
public sealed class WorkItemProjectionService : IWorkItemProjectionService
{
    /*
     * WC-2 — the SAME SLA decision MOD-0024's provider makes, over the SAME working-time seam.
     *
     * Both providers answer, deliberately. Leaving this one silent would have left the browser deriving a state
     * for approval items — the exact split this slice exists to end — and would have quietly meant "no provider
     * speaks here, so the client still decides" for half the surface.
     */
    private readonly IWorkItemSlaCalculator _sla;

    public WorkItemProjectionService(IWorkItemSlaCalculator sla) => _sla = sla;

    // Localization resource keys (resource-key form; wiring is WC-1b).
    private const string TitleApprovalKey = "WorkAggregation_Title_Approval";
    // BL-437 — why the approval reached the reader. Two keys, not one with an optional slot: a sentence with its
    // subject missing is not a sentence in every language, so the nameless form is written whole.
    private const string ArrivalSentForApprovalKey = "WorkAggregation_ArrivalReason_SentForApproval";
    private const string ArrivalSentForApprovalUnnamedKey = "WorkAggregation_ArrivalReason_SentForApprovalUnnamed";
    private const string NativeStatusKeyPrefix = "WorkAggregation_NativeStatus_";
    private const string ActionApproveKey = "WorkAggregation_Action_Approve";
    private const string ActionRejectKey = "WorkAggregation_Action_Reject";
    private const string ActionRequestInfoKey = "WorkAggregation_Action_RequestInfo";
    private const string ActionDelegateKey = "WorkAggregation_Action_Delegate";
    private const string DisabledPermissionKey = "WorkAggregation_ActionDisabled_PermissionDenied";
    private const string DisabledEvidenceKey = "WorkAggregation_ActionDisabled_EvidenceRequired";
    private const string WaitingEvidenceType = "evidenceRequired";

    public WorkItemProjectionDto? Project(
        ApprovalTask task,
        WorkflowInstance? instance,
        WorkItemActor actor,
        string providerCode,
        string providerContractVersion,
        ApprovalSourceContext? sourceContext = null)
    {
        // Delegated → hidden from this actor (a disposition, not active work).
        if (task.Status == ApprovalTaskStatus.Delegated)
        {
            return null;
        }

        // A work item without its resolvable source object is not projectable (source is contract-required).
        if (instance is null)
        {
            return null;
        }

        var normalized = NormalizeStatus(task.Status);
        var isTerminal = normalized is WorkItemContract.StatusDone or WorkItemContract.StatusCancelled;
        var isWaiting = normalized == WorkItemContract.StatusWaiting;

        var source = new WorkItemSourceDto(
            ProviderCode: WorkItemContract.ProviderCodeWorkflow,
            ProviderContractVersion: providerContractVersion,
            ObjectType: instance.ObjectType,
            ObjectId: instance.ObjectId,
            // BL-437 — the source owner's address for the object, when it gave one. actionDepth stays inline: the
            // decision is still taken on the row; the link is the way to read what is being decided.
            DeepLink: string.IsNullOrWhiteSpace(sourceContext?.DeepLink) ? null : sourceContext.DeepLink);

        var nativeStatus = new WorkItemNativeStatusDto(
            Code: task.Status.ToString(),
            Label: WorkItemLabelDto.Resource(NativeStatusKeyPrefix + task.Status));

        /*
         * BL-437 — the title is WHAT IS BEING DECIDED, in its owner's words. The generic key below printed the
         * object type and its GUID ("Onay: task-review 3f2c…"), and the approver could not tell which of their
         * tasks had come back to them or why. The owner's title is a DISPLAY label: a person typed it, and routing
         * it through a resource key would put a raw key on screen for every title nobody translated.
         *
         * The generic key stays as the fallback for an object whose owner cannot answer — an approval over a
         * module with no resolver yet, or a source record that no longer exists.
         */
        var title = !string.IsNullOrWhiteSpace(sourceContext?.Title)
            ? WorkItemLabelDto.Display(sourceContext.Title.Trim())
            : WorkItemLabelDto.Resource(
                TitleApprovalKey,
                new Dictionary<string, string>
                {
                    ["objectType"] = instance.ObjectType,
                    ["objectId"] = instance.ObjectId
                });

        // Terminal items are read-only: no enabled inline state-changing action (contract invariant).
        var actions = isTerminal
            ? Array.Empty<WorkItemActionDto>()
            : BuildActionableActions(task, actor);

        var waitingContext = isWaiting
            ? new WorkItemWaitingContextDto(
                Type: WaitingEvidenceType,
                // The approver IS who we are waiting on, so it belongs in the typed identity field. The name is
                // left null rather than guessed — MOD-0023 has no directory seam — and the client renders nothing
                // instead of a raw id.
                WaitingOn: string.IsNullOrWhiteSpace(task.AssigneeRef)
                    ? null
                    : new WorkItemPersonDto(task.AssigneeRef),
                // No free-text reason on an approval wait: the reason IS the type (waiting for evidence).
                Reason: null,
                Since: task.EscalatedAt,
                // Unlike MOD-0024's case, this DueAt is the APPROVAL task's own deadline, so it genuinely is when
                // this wait is expected to end.
                ExpectedUntil: task.DueAt)
            : null;

        var escalation = task.Status == ApprovalTaskStatus.Escalated
            ? new WorkItemEscalationDto(Escalated: true, Level: task.EscalationLevel, Since: task.EscalatedAt)
            : null;

        return new WorkItemProjectionDto(
            FixtureKind: WorkItemContract.FixtureKindWorkItem,
            Id: task.Id.ToString(),
            WorkIntent: WorkItemContract.IntentApproval,
            AssignmentMode: WorkItemContract.AssignmentApproval,
            OwnershipState: WorkItemContract.NotApplicable,
            AdmissionState: WorkItemContract.NotApplicable,
            NormalizedStatus: normalized,
            TaskLifecycle: WorkItemContract.NotApplicable, // approval intent → non-task lifecycle
            ExecutionState: WorkItemContract.NotApplicable,
            TimerState: WorkItemContract.NotApplicable,
            SystemState: WorkItemContract.SystemFresh,
            ActionDepth: WorkItemContract.DepthInline,
            Title: title,
            NativeStatus: nativeStatus,
            Source: source,
            LifecycleOwner: WorkItemContract.LifecycleOwnerWorkflow,
            WorkItemCapabilities: Array.Empty<string>(),
            Actions: actions,
            // One projection-level concurrency token from the provider's technical Version (no per-action copy).
            Concurrency: new WorkItemConcurrencyDto("version", task.Version.ToString()),
            WaitingContext: waitingContext,
            Escalation: escalation,
            DueAt: task.DueAt,
            /*
             * BL-046 applies here too, and for the same reason it does in MOD-0024's provider: a decided
             * approval was still being measured against today, so it went on getting later every morning it sat
             * in History. The clock stops when the task closed.
             *
             * ApprovalTask records one closing timestamp (CompletedAt) for every terminal disposition —
             * approved, rejected, cancelled, timed out. When it is missing there is nothing honest to freeze, so
             * this falls back to now exactly as before rather than inventing an instant.
             */
            SlaState: _sla.Resolve(task.DueAt, (isTerminal ? task.CompletedAt : null) ?? DateTimeOffset.UtcNow),
            ClosedAt: isTerminal ? task.CompletedAt : null,
            Requester: sourceContext?.Requester,
            ArrivalReason: ArrivalReasonFor(sourceContext));
    }

    /*
     * BL-437 — "{name} sent this task for your approval". Only when the owner answered at all: an approval with
     * no source context says nothing rather than claiming someone sent it. A requester whose name the directory
     * could not resolve gets the whole nameless sentence — never their id in the name slot.
     */
    private static WorkItemLabelDto? ArrivalReasonFor(ApprovalSourceContext? sourceContext)
    {
        if (sourceContext is null)
        {
            return null;
        }

        var name = sourceContext.Requester?.DisplayName;
        return string.IsNullOrWhiteSpace(name)
            ? WorkItemLabelDto.Resource(ArrivalSentForApprovalUnnamedKey)
            : WorkItemLabelDto.Resource(
                ArrivalSentForApprovalKey,
                new Dictionary<string, string> { ["name"] = name.Trim() });
    }

    // Charter §10.1 — raw ApprovalTaskStatus is mapped by the enum, never by parsing status text.
    private static string NormalizeStatus(ApprovalTaskStatus status) => status switch
    {
        ApprovalTaskStatus.WaitingApproval => WorkItemContract.StatusPending,
        ApprovalTaskStatus.Escalated => WorkItemContract.StatusPending, // + escalation signal
        ApprovalTaskStatus.WaitingEvidence => WorkItemContract.StatusWaiting, // + waitingContext
        ApprovalTaskStatus.Approved => WorkItemContract.StatusDone,
        ApprovalTaskStatus.Rejected => WorkItemContract.StatusDone,
        ApprovalTaskStatus.Cancelled => WorkItemContract.StatusCancelled,
        ApprovalTaskStatus.TimedOut => WorkItemContract.StatusCancelled, // EA 2026-07-24, OD-WC-01
        // Delegated is handled (hidden) before this switch; any unmapped value fails safe.
        _ => throw new InvalidOperationException($"Unmapped ApprovalTaskStatus: {status}")
    };

    // The single authoritative actions[] for an actionable approval task. Each action's enabled state is
    // resolved here (permission + evidence blocker); the browser never invents or re-derives eligibility.
    private static IReadOnlyList<WorkItemActionDto> BuildActionableActions(ApprovalTask task, WorkItemActor actor)
    {
        var evidencePending = task.Status == ApprovalTaskStatus.WaitingEvidence;

        return
        [
            BuildApprove(task, actor, evidencePending),
            BuildDecision("reject", ActionRejectKey, WorkflowPermissions.TasksReject, actor,
                requiresConfirmation: true, requiresReason: true, supportsBulk: true, riskLevel: "elevated"),
            BuildDecision("requestInfo", ActionRequestInfoKey, WorkflowPermissions.TasksRequestInfo, actor,
                requiresConfirmation: false, requiresReason: true, supportsBulk: false, riskLevel: "normal"),
            BuildDecision("delegate", ActionDelegateKey, WorkflowPermissions.TasksDelegate, actor,
                requiresConfirmation: true, requiresReason: false, supportsBulk: false, riskLevel: "normal")
        ];
    }

    private static WorkItemActionDto BuildApprove(ApprovalTask task, WorkItemActor actor, bool evidencePending)
    {
        var permitted = actor.Has(WorkflowPermissions.TasksApprove);

        // Permission is the first gate; then a pending-evidence blocker keeps approve visible but disabled.
        if (!permitted)
        {
            return Disabled("approve", ActionApproveKey, WorkAggregationReasonCodes.PermissionDenied,
                DisabledPermissionKey, requiresConfirmation: true, requiresReason: task.CommentRequired,
                requiresEvidence: task.EvidenceRequired, supportsBulk: true, riskLevel: "normal");
        }

        if (evidencePending)
        {
            return Disabled("approve", ActionApproveKey, WorkAggregationReasonCodes.EvidenceRequired,
                DisabledEvidenceKey, requiresConfirmation: true, requiresReason: task.CommentRequired,
                requiresEvidence: true, supportsBulk: true, riskLevel: "normal");
        }

        return new WorkItemActionDto(
            Code: "approve",
            Label: WorkItemLabelDto.Resource(ActionApproveKey),
            SemanticType: "approve",
            Enabled: true,
            Source: WorkItemContract.ActionSourceProvider,
            DisabledReasonCode: null,
            DisabledReason: null,
            RequiresConfirmation: true,
            RequiresReason: task.CommentRequired,
            RequiresEvidence: task.EvidenceRequired,
            SupportsBulk: true,
            RiskLevel: "normal");
    }

    private static WorkItemActionDto BuildDecision(
        string code, string labelKey, string permissionKey, WorkItemActor actor,
        bool requiresConfirmation, bool requiresReason, bool supportsBulk, string riskLevel)
    {
        if (!actor.Has(permissionKey))
        {
            return Disabled(code, labelKey, WorkAggregationReasonCodes.PermissionDenied, DisabledPermissionKey,
                requiresConfirmation, requiresReason, requiresEvidence: false, supportsBulk, riskLevel);
        }

        return new WorkItemActionDto(
            Code: code,
            Label: WorkItemLabelDto.Resource(labelKey),
            SemanticType: code,
            Enabled: true,
            Source: WorkItemContract.ActionSourceProvider,
            DisabledReasonCode: null,
            DisabledReason: null,
            RequiresConfirmation: requiresConfirmation,
            RequiresReason: requiresReason,
            RequiresEvidence: false,
            SupportsBulk: supportsBulk,
            RiskLevel: riskLevel);
    }

    private static WorkItemActionDto Disabled(
        string code, string labelKey, string reasonCode, string reasonKey,
        bool requiresConfirmation, bool requiresReason, bool requiresEvidence, bool supportsBulk, string riskLevel)
        => new(
            Code: code,
            Label: WorkItemLabelDto.Resource(labelKey),
            SemanticType: code,
            Enabled: false,
            Source: WorkItemContract.ActionSourceProvider,
            DisabledReasonCode: reasonCode,
            DisabledReason: WorkItemLabelDto.Resource(reasonKey),
            RequiresConfirmation: requiresConfirmation,
            RequiresReason: requiresReason,
            RequiresEvidence: requiresEvidence,
            SupportsBulk: supportsBulk,
            RiskLevel: riskLevel);
}
