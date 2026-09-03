using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

public sealed class WorkflowProductIdentityWorkItemContractTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Assignee = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Maker = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly HashSet<string> MappedProperties = new(StringComparer.Ordinal)
    {
        nameof(WorkItemProjectionDto.FixtureKind),
        nameof(WorkItemProjectionDto.Id),
        nameof(WorkItemProjectionDto.WorkIntent),
        nameof(WorkItemProjectionDto.AssignmentMode),
        nameof(WorkItemProjectionDto.OwnershipState),
        nameof(WorkItemProjectionDto.AdmissionState),
        nameof(WorkItemProjectionDto.NormalizedStatus),
        nameof(WorkItemProjectionDto.TaskLifecycle),
        nameof(WorkItemProjectionDto.ExecutionState),
        nameof(WorkItemProjectionDto.TimerState),
        nameof(WorkItemProjectionDto.SystemState),
        nameof(WorkItemProjectionDto.ActionDepth),
        nameof(WorkItemProjectionDto.Title),
        nameof(WorkItemProjectionDto.NativeStatus),
        nameof(WorkItemProjectionDto.Source),
        nameof(WorkItemProjectionDto.LifecycleOwner),
        nameof(WorkItemProjectionDto.WorkItemCapabilities),
        nameof(WorkItemProjectionDto.Actions),
        nameof(WorkItemProjectionDto.Concurrency),
        nameof(WorkItemProjectionDto.WaitingContext),
        nameof(WorkItemProjectionDto.Escalation),
        nameof(WorkItemProjectionDto.DueAt),
        nameof(WorkItemProjectionDto.Assignee),
        nameof(WorkItemProjectionDto.Requester),
        nameof(WorkItemProjectionDto.SlaState),
        nameof(WorkItemProjectionDto.ClosedAt)
    };

    private static readonly HashSet<string> ExplicitlyAbsentProperties = new(StringComparer.Ordinal)
    {
        nameof(WorkItemProjectionDto.PrimaryActionCode),
        nameof(WorkItemProjectionDto.OverflowActionCodes),
        nameof(WorkItemProjectionDto.Checklist),
        nameof(WorkItemProjectionDto.Subtasks),
        nameof(WorkItemProjectionDto.ParentTaskItemId),
        nameof(WorkItemProjectionDto.Gates),
        nameof(WorkItemProjectionDto.Priority),
        nameof(WorkItemProjectionDto.Dependencies),
        nameof(WorkItemProjectionDto.BlockedState),
        nameof(WorkItemProjectionDto.Activity),
        nameof(WorkItemProjectionDto.PlannedDate),
        nameof(WorkItemProjectionDto.TaskType),
        nameof(WorkItemProjectionDto.Pool),
        nameof(WorkItemProjectionDto.BusinessContext),
        nameof(WorkItemProjectionDto.Summary),
        nameof(WorkItemProjectionDto.StartAt),
        nameof(WorkItemProjectionDto.EstimateHours),
        nameof(WorkItemProjectionDto.SpentHours),
        nameof(WorkItemProjectionDto.Tags),
        nameof(WorkItemProjectionDto.Personal),
        nameof(WorkItemProjectionDto.Watchers),
        nameof(WorkItemProjectionDto.DelegationAllowed),
        nameof(WorkItemProjectionDto.Notifications),
        nameof(WorkItemProjectionDto.ReminderLeadDays),
        // Closure is the Task provider's typed TaskItem.ClosureReasonCode/outcome contract. Native Workflow
        // approvals persist transition reason and terminal status, but no equivalent closure-outcome dictionary;
        // translating ActionReasonCode would invent Task semantics and an outcome label the provider cannot prove.
        nameof(WorkItemProjectionDto.Closure),
        // Returned describes the Task provider's explicit return-to-requester transition history. Workflow's
        // request-info transition moves an approval to WaitingEvidence; it does not persist the equivalent
        // return event/count contract, so the native Workflow projection must remain silent.
        nameof(WorkItemProjectionDto.Returned),
        // ViewerRelation classifies Task-provider initiator-only reads. This provider enumerates assigned
        // ApprovalTask records and has no separate initiator/outbox query path to classify truthfully.
        nameof(WorkItemProjectionDto.ViewerRelation)
    };

    [Fact]
    public void Every_current_projection_property_is_mapped_or_explicitly_absent()
    {
        var classified = MappedProperties.Concat(ExplicitlyAbsentProperties).ToHashSet(StringComparer.Ordinal);
        var current = typeof(WorkItemProjectionDto).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(current.OrderBy(name => name), classified.OrderBy(name => name));
        Assert.Empty(MappedProperties.Intersect(ExplicitlyAbsentProperties));
    }

    [Theory]
    [InlineData("GlobalProduct")]
    [InlineData("gsku")]
    [InlineData("lsku")]
    [InlineData("finished-good")]
    public void Product_identity_work_items_keep_native_workflow_source_people_actions_and_version(string objectType)
    {
        var task = new ApprovalTask
        {
            TenantId = Tenant,
            WorkflowInstanceId = Guid.NewGuid(),
            StageCode = "approval",
            StepCode = "decision",
            Status = ApprovalTaskStatus.WaitingApproval,
            AssigneeRef = Assignee.ToString("D")
        };
        var instance = new WorkflowInstance
        {
            TenantId = Tenant,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            ObjectType = objectType,
            ObjectId = Guid.NewGuid().ToString("D"),
            ObjectRef = $"product-item-sku-master|{objectType}|object",
            DelegatedMakerUserId = Maker,
            StartedBy = "trusted-service-client"
        };
        var actor = new WorkItemActor(
            Assignee,
            IsPlatformActor: true,
            new HashSet<string>(StringComparer.Ordinal));

        var projection = new WorkItemProjectionService(Tasks.SlaForTests.Real())
            .Project(task, instance, actor, WorkItemContract.ProviderCodeWorkflow, "1.0");

        Assert.NotNull(projection);
        Assert.Equal(WorkItemContract.ProviderCodeWorkflow, projection!.Source.ProviderCode);
        Assert.Equal(objectType, projection.Source.ObjectType);
        Assert.Equal(instance.ObjectId, projection.Source.ObjectId);
        Assert.Equal(WorkItemContract.LifecycleOwnerWorkflow, projection.LifecycleOwner);
        Assert.Equal(task.Version.ToString(), projection.Concurrency.Token);
        Assert.Equal(Assignee.ToString("D"), projection.Assignee!.Id);
        Assert.True(projection.Assignee.IsCurrentUser);
        Assert.Equal(Maker.ToString("D"), projection.Requester!.Id);
        Assert.False(projection.Requester.IsCurrentUser);
        Assert.Equal(
            new[] { "approve", "delegate", "reject", "requestInfo" },
            projection.Actions.Select(action => action.Code).OrderBy(code => code));

        foreach (var propertyName in ExplicitlyAbsentProperties)
        {
            Assert.Null(typeof(WorkItemProjectionDto).GetProperty(propertyName)!.GetValue(projection));
        }
    }

    [Fact]
    public void Requester_falls_back_to_human_started_by_and_never_exposes_trusted_client_identity()
    {
        var clientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var task = Task();
        var actor = new WorkItemActor(Assignee, true, new HashSet<string>());
        var service = new WorkItemProjectionService(Tasks.SlaForTests.Real());

        var human = service.Project(task, Instance(startedBy: "human-subject"), actor, "workflow", "1.0");
        var serviceStarted = service.Project(
            task,
            Instance(startedBy: clientId.ToString("D").ToUpperInvariant(), trustedClientId: clientId),
            actor,
            "workflow",
            "1.0");

        Assert.Equal("human-subject", human!.Requester!.Id);
        Assert.Null(serviceStarted!.Requester);
    }

    private static ApprovalTask Task() => new()
    {
        TenantId = Tenant,
        WorkflowInstanceId = Guid.NewGuid(),
        StageCode = "approval",
        StepCode = "decision",
        Status = ApprovalTaskStatus.WaitingApproval,
        AssigneeRef = Assignee.ToString("D")
    };

    private static WorkflowInstance Instance(string? startedBy, Guid? trustedClientId = null) => new()
    {
        TenantId = Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = "GlobalProduct",
        ObjectId = Guid.NewGuid().ToString("D"),
        ObjectRef = "product-item-sku-master|GlobalProduct|object",
        TrustedConsumerClientId = trustedClientId,
        StartedBy = startedBy
    };
}
