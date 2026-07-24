using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// WC-1 (DCP-004) — the pure ApprovalTask → canonical work-item projection. Verifies the
// charter §10.1 status map, single authoritative actions[] eligibility, terminal read-only, Delegated hidden,
// Waiting/waitingContext pairing, source join, and one projection-level concurrency token — all asserted
// against the executable contract's value sets (WorkItemContract) as the conformance oracle.
public sealed class WorkItemProjectionServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const string ProviderCode = "workflow";
    private const string ContractVersion = "1.0";

    private readonly WorkItemProjectionService _sut = new();

    [Theory]
    [InlineData(ApprovalTaskStatus.WaitingApproval, WorkItemContract.StatusPending)]
    [InlineData(ApprovalTaskStatus.Escalated, WorkItemContract.StatusPending)]
    [InlineData(ApprovalTaskStatus.WaitingEvidence, WorkItemContract.StatusWaiting)]
    [InlineData(ApprovalTaskStatus.Approved, WorkItemContract.StatusDone)]
    [InlineData(ApprovalTaskStatus.Rejected, WorkItemContract.StatusDone)]
    [InlineData(ApprovalTaskStatus.Cancelled, WorkItemContract.StatusCancelled)]
    [InlineData(ApprovalTaskStatus.TimedOut, WorkItemContract.StatusCancelled)] // EA 2026-07-24 / OD-WC-01
    public void Normalizes_each_status_per_charter_10_1(ApprovalTaskStatus status, string expected)
    {
        var dto = _sut.Project(MakeTask(status), Instance(), AllPermissions(), ProviderCode, ContractVersion);

        Assert.NotNull(dto);
        Assert.Equal(expected, dto!.NormalizedStatus);
        AssertContractConformant(dto);
    }

    [Fact]
    public void Delegated_is_hidden_from_the_actor()
    {
        var dto = _sut.Project(MakeTask(ApprovalTaskStatus.Delegated), Instance(), AllPermissions(), ProviderCode, ContractVersion);
        Assert.Null(dto);
    }

    [Fact]
    public void Missing_source_instance_is_not_projectable()
    {
        var dto = _sut.Project(MakeTask(ApprovalTaskStatus.WaitingApproval), instance: null, AllPermissions(), ProviderCode, ContractVersion);
        Assert.Null(dto);
    }

    [Theory]
    [InlineData(ApprovalTaskStatus.Approved)]
    [InlineData(ApprovalTaskStatus.Rejected)]
    [InlineData(ApprovalTaskStatus.Cancelled)]
    [InlineData(ApprovalTaskStatus.TimedOut)]
    public void Terminal_items_are_readonly_with_no_enabled_action(ApprovalTaskStatus status)
    {
        var dto = _sut.Project(MakeTask(status), Instance(), AllPermissions(), ProviderCode, ContractVersion);

        Assert.NotNull(dto);
        Assert.Empty(dto!.Actions); // no state-changing action on a terminal item
        AssertContractConformant(dto);
    }

    [Fact]
    public void WaitingEvidence_pairs_normalized_Waiting_with_a_waitingContext()
    {
        var dto = _sut.Project(MakeTask(ApprovalTaskStatus.WaitingEvidence), Instance(), AllPermissions(), ProviderCode, ContractVersion);

        Assert.NotNull(dto);
        Assert.Equal(WorkItemContract.StatusWaiting, dto!.NormalizedStatus);
        Assert.NotNull(dto.WaitingContext);
        AssertContractConformant(dto);
    }

    [Fact]
    public void Escalated_carries_the_escalation_signal_and_stays_Pending()
    {
        var task = MakeTask(ApprovalTaskStatus.Escalated);
        task.EscalationLevel = 2;

        var dto = _sut.Project(task, Instance(), AllPermissions(), ProviderCode, ContractVersion);

        Assert.NotNull(dto);
        Assert.Equal(WorkItemContract.StatusPending, dto!.NormalizedStatus);
        Assert.NotNull(dto.Escalation);
        Assert.True(dto.Escalation!.Escalated);
        Assert.Equal(2, dto.Escalation.Level);
        Assert.Null(dto.WaitingContext); // escalation is a signal, not a Waiting state
    }

    [Fact]
    public void Approve_is_enabled_with_permission_and_no_blocker()
    {
        var dto = _sut.Project(MakeTask(ApprovalTaskStatus.WaitingApproval), Instance(), AllPermissions(), ProviderCode, ContractVersion);

        var approve = Assert.Single(dto!.Actions, a => a.Code == "approve");
        Assert.True(approve.Enabled);
        Assert.Null(approve.DisabledReasonCode);
        AssertContractConformant(dto);
    }

    [Fact]
    public void Approve_is_disabled_without_permission()
    {
        var dto = _sut.Project(MakeTask(ApprovalTaskStatus.WaitingApproval), Instance(), NoPermissions(), ProviderCode, ContractVersion);

        var approve = Assert.Single(dto!.Actions, a => a.Code == "approve");
        Assert.False(approve.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, approve.DisabledReasonCode);
        Assert.NotNull(approve.DisabledReason);
        AssertContractConformant(dto);
    }

    [Fact]
    public void Approve_is_disabled_when_evidence_is_pending()
    {
        var dto = _sut.Project(MakeTask(ApprovalTaskStatus.WaitingEvidence), Instance(), AllPermissions(), ProviderCode, ContractVersion);

        var approve = Assert.Single(dto!.Actions, a => a.Code == "approve");
        Assert.False(approve.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.EvidenceRequired, approve.DisabledReasonCode);
        AssertContractConformant(dto);
    }

    [Fact]
    public void Projects_source_join_and_single_concurrency_token()
    {
        var task = MakeTask(ApprovalTaskStatus.WaitingApproval);
        task.Version = 17;

        var dto = _sut.Project(task, Instance(objectType: "invoice", objectId: "INV-42"), AllPermissions(), ProviderCode, ContractVersion);

        Assert.NotNull(dto);
        Assert.Equal(WorkItemContract.ProviderCodeWorkflow, dto!.Source.ProviderCode);
        Assert.Equal("invoice", dto.Source.ObjectType);
        Assert.Equal("INV-42", dto.Source.ObjectId);
        Assert.Equal(WorkItemContract.LifecycleOwnerWorkflow, dto.LifecycleOwner);
        Assert.Equal("version", dto.Concurrency.Kind);
        Assert.Equal("17", dto.Concurrency.Token);
        Assert.NotNull(dto.Title); // title resolved via deterministic resource-key fallback
        Assert.Equal(WorkItemContract.LabelResource, dto.Title.Kind);
    }

    // --- helpers ---------------------------------------------------------------------------------------

    private static WorkItemActor AllPermissions()
        => new(Me, IsPlatformActor: true, new HashSet<string>());

    private static WorkItemActor NoPermissions()
        => new(Me, IsPlatformActor: false, new HashSet<string>());

    private static ApprovalTask MakeTask(ApprovalTaskStatus status) => new()
    {
        TenantId = Tenant,
        WorkflowInstanceId = Guid.NewGuid(),
        StageCode = "stage-1",
        StepCode = "step-1",
        Status = status,
        AssigneeRef = Me.ToString(),
        EvidenceRequired = status == ApprovalTaskStatus.WaitingEvidence
    };

    private static WorkflowInstance Instance(string objectType = "invoice", string objectId = "INV-1") => new()
    {
        TenantId = Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = objectType,
        ObjectId = objectId,
        ObjectRef = $"finance|{objectType}|{objectId}"
    };

    // The executable-contract conformance oracle (fixture-contract.js invariants), replicated for the backend.
    private static void AssertContractConformant(WorkItemProjectionDto dto)
    {
        Assert.Equal(WorkItemContract.FixtureKindWorkItem, dto.FixtureKind);
        Assert.False(string.IsNullOrWhiteSpace(dto.Id));
        Assert.Equal(WorkItemContract.IntentApproval, dto.WorkIntent);
        Assert.Contains(dto.NormalizedStatus, WorkItemContract.NormalizedStatuses);
        Assert.Equal(WorkItemContract.NotApplicable, dto.TaskLifecycle); // non-task intent
        Assert.Equal(WorkItemContract.NotApplicable, dto.ExecutionState);
        Assert.Equal(WorkItemContract.NotApplicable, dto.TimerState);

        // Waiting ⇔ waitingContext (bidirectional pairing).
        var isWaiting = dto.NormalizedStatus == WorkItemContract.StatusWaiting;
        Assert.Equal(isWaiting, dto.WaitingContext is not null);

        // source is required and complete.
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ObjectType));
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ObjectId));
        Assert.False(string.IsNullOrWhiteSpace(dto.Source.ProviderContractVersion));

        // nativeStatus code + resource label.
        Assert.False(string.IsNullOrWhiteSpace(dto.NativeStatus.Code));
        Assert.Equal(WorkItemContract.LabelResource, dto.NativeStatus.Label.Kind);

        // actions: unique codes; disabled ⇒ reason code + reason label; every action has a source.
        Assert.Equal(dto.Actions.Select(a => a.Code).Distinct().Count(), dto.Actions.Count);
        foreach (var action in dto.Actions)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Source));
            Assert.Equal(WorkItemContract.LabelResource, action.Label.Kind);
            if (!action.Enabled)
            {
                Assert.False(string.IsNullOrWhiteSpace(action.DisabledReasonCode));
                Assert.NotNull(action.DisabledReason);
            }
        }

        // Terminal items expose no enabled inline action.
        var terminal = dto.NormalizedStatus is WorkItemContract.StatusDone or WorkItemContract.StatusCancelled;
        if (terminal)
        {
            Assert.DoesNotContain(dto.Actions, a => a.Enabled);
        }

        // Exactly one projection-level concurrency token (the DTO shape has no per-action token field).
        Assert.NotNull(dto.Concurrency);
        Assert.False(string.IsNullOrWhiteSpace(dto.Concurrency.Token));
    }
}
