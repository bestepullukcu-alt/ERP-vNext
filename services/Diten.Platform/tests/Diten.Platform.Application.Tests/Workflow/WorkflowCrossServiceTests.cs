using System.Reflection;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Events;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Validators;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-CL-BE-3 — MOD-0023 opened for cross-service use: display context, the completion event, the candidate fix and
/// the batch status read. In-memory, tenant-filtering repositories; a capturing outbox writer that also runs the
/// production <see cref="EventPayloadContractValidator"/>; an immediate transaction executor.
/// </summary>
public sealed class WorkflowCrossServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    private const string Approver = "approver-001";
    private const string Submitter = "submitter-001";
    private const string Correlation = "3f0f1a1e-5b7e-4c55-9d41-0000000000cc";

    // ============================================================ display context

    [Fact]
    public async Task Display_context_is_snapshotted_on_start_and_exposed_on_the_instance()
    {
        var f = new Fx(TenantA);
        var template = f.SeedTemplate("CLAIM-APPROVAL");
        var display = new WorkflowDisplayContext("İddia onayı · CL-1 v1.0", "Ürün X · TR", "crm",
            "/Crm/Claims/Detail/abc", ["TR", "v1.0"]);

        var r = await f.Start().Handle(new StartWorkflowInstanceCommand(
            StartRequest(template.Id, ["user:" + Approver], display), Correlation), default);

        Assert.True(r.IsSuccessful, string.Join(",", r.Errors));
        var instance = f.Store.Instances.Single();
        Assert.Equal("CLAIM-APPROVAL", instance.TemplateCode);
        Assert.Equal("İddia onayı · CL-1 v1.0", instance.DisplayContext!.Title);
        Assert.Equal(["TR", "v1.0"], instance.DisplayContext.Chips);
        var dto = WorkflowDefinitionMapper.ToInstance(instance);
        Assert.Equal("/Crm/Claims/Detail/abc", dto.DisplayContext!.DeepLinkUrl);
        Assert.Equal("CLAIM-APPROVAL", dto.TemplateCode);
        Assert.Null(dto.Outcome);
    }

    [Theory]
    [InlineData("https://evil.example/x")]
    [InlineData("//evil.example/x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/x/javascript:alert(1)")]
    [InlineData("Crm/Claims/1")]
    [InlineData("/Crm\\Claims")]
    public void Non_relative_deep_link_is_a_validation_failure(string url)
    {
        var result = new StartWorkflowInstanceValidator().Validate(new StartWorkflowInstanceCommand(
            StartRequest(Guid.NewGuid(), ["user:x"], new WorkflowDisplayContext("t", DeepLinkUrl: url)), Correlation));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Display_context_limits_are_enforced_and_a_relative_link_passes()
    {
        var ok = new StartWorkflowInstanceValidator().Validate(new StartWorkflowInstanceCommand(
            StartRequest(Guid.NewGuid(), ["user:x"], new WorkflowDisplayContext("t", "s", "crm", "/Crm/Claims/1?tab=2",
                ["a", "b", "c", "d", "e"])), Correlation));
        Assert.True(ok.IsValid);

        Assert.False(Validate(new WorkflowDisplayContext(new string('t', 201))));
        Assert.False(Validate(new WorkflowDisplayContext("t", Chips: ["1", "2", "3", "4", "5", "6"])));
        Assert.False(Validate(new WorkflowDisplayContext("t", Chips: [new string('c', 33)])));
        Assert.True(new StartWorkflowInstanceValidator().Validate(new StartWorkflowInstanceCommand(
            StartRequest(Guid.NewGuid(), ["user:x"]), Correlation)).IsValid); // pre-existing callers unaffected

        static bool Validate(WorkflowDisplayContext d) => new StartWorkflowInstanceValidator()
            .Validate(new StartWorkflowInstanceCommand(StartRequest(Guid.NewGuid(), ["user:x"], d), Correlation)).IsValid;
    }

    [Fact]
    public async Task Snapshot_resolver_gives_title_subtitle_link_and_chips_for_a_cross_service_object()
    {
        var instance = Instance("crm.claim", "claim-1", new WorkflowDisplayContextSnapshot
        {
            Title = "İddia onayı · CL-1", Subtitle = "Ürün X · TR", DeepLinkUrl = "/Crm/Claims/Detail/1", Chips = ["TR"]
        });
        var item = (await Provider(instance, new SnapshotApprovalSourceResolver()).GetWorkItemsAsync(Actor(), default)).Single();

        Assert.Equal(WorkItemContract.LabelDisplay, item.Title.Kind);
        Assert.Equal("İddia onayı · CL-1", item.Title.Text);
        Assert.Equal("Ürün X · TR", item.Summary!.Text);
        Assert.Equal(["TR"], item.Tags);
        Assert.Equal("/Crm/Claims/Detail/1", item.Source.DeepLink);
    }

    [Fact]
    public async Task Without_display_context_the_generic_title_stays()
    {
        var instance = Instance("crm.claim", "claim-2", null);
        var item = (await Provider(instance, new SnapshotApprovalSourceResolver()).GetWorkItemsAsync(Actor(), default)).Single();
        Assert.Equal(WorkItemContract.LabelResource, item.Title.Kind);
        Assert.Null(item.Summary);
        Assert.Null(item.Tags);
    }

    [Fact]
    public async Task Tasks_owner_resolver_keeps_priority_over_the_snapshot_whatever_the_registration_order()
    {
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant, Title = "Q3 bütçe revizyonu", OrganizationUnitId = Guid.NewGuid(),
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned, CreatedByUserId = Guid.NewGuid()
        };
        var snapshot = new WorkflowDisplayContextSnapshot { Title = "SNAPSHOT TITLE", Subtitle = "SNAPSHOT SUB", Chips = ["X"] };
        var owned = Instance(TaskReviewService.ReviewObjectType, task.Id.ToString(), snapshot);
        var tasksResolver = new TaskApprovalSourceResolver(
            new FakeTaskItemRepository(task), new FakeTaskTransitionRepository(), new FakeUserDisplayNameResolver());

        // Fallback registered FIRST on purpose: the provider must still ask the owner first.
        var item = (await Provider(owned, new SnapshotApprovalSourceResolver(), tasksResolver)
            .GetWorkItemsAsync(Actor(), default)).Single();
        Assert.Equal("Q3 bütçe revizyonu", item.Title.Text);
        Assert.Null(item.Summary); // the owner says nothing more, and the snapshot does not fill in
        Assert.Null(item.Tags);

        // Owner cannot answer (its object is gone) → the generic title, never the snapshot, for an OWNED type.
        var orphan = Instance(TaskReviewService.ReviewObjectType, Guid.NewGuid().ToString(), snapshot);
        var orphanItem = (await Provider(orphan, new SnapshotApprovalSourceResolver(), tasksResolver)
            .GetWorkItemsAsync(Actor(), default)).Single();
        Assert.Equal(WorkItemContract.LabelResource, orphanItem.Title.Kind);
    }

    // ============================================================ completion event

    [Fact]
    public async Task Final_approve_writes_exactly_one_completion_event_with_the_payload()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime();
        var r = await f.Approve().Handle(ApproveCmd(run.Task.Id), default);

        Assert.True(r.IsSuccessful, string.Join(",", r.Errors));
        var e = Assert.IsType<WorkflowInstanceCompletedV1>(Assert.Single(f.Events.Events));
        Assert.Equal("platform.workflow.instance.completed.v1", e.EventName);
        Assert.Equal(WorkflowOutcomes.Approved, e.Outcome);
        Assert.Equal(TenantA, e.TenantId);
        Assert.Equal(run.Instance.Id, e.WorkflowInstanceId);
        Assert.Equal("CLAIM-APPROVAL", e.TemplateCode);
        Assert.Equal(run.Instance.TemplateVersionId, e.TemplateVersionId);
        Assert.Equal("crm.claim", e.ObjectType);
        Assert.Equal("claim-1", e.ObjectId);
        Assert.Equal("crm|crm.claim|claim-1", e.ObjectRef);
        Assert.Equal(Approver, e.CompletedBy);
        Assert.Equal("stage-1", e.FinalStageCode);
        Assert.Equal("step-1", e.FinalStepCode);
        Assert.Equal("APPROVED", e.ReasonCode);
        Assert.Equal(Guid.Parse(Correlation), e.CorrelationId);
        Assert.Equal(WorkflowTerminalTransitionWriter.CompletionEventId(run.Instance.Id), e.EventId);
        Assert.Equal(1, f.Transactions.Calls);
        Assert.DoesNotContain("SECRET-COMMENT", JsonSerializer.Serialize(e));

        // Idempotent replay of the same approve: no second event.
        var again = await f.Approve().Handle(ApproveCmd(run.Task.Id), default);
        Assert.True(again.Data!.IsIdempotent);
        Assert.Single(f.Events.Events);
    }

    [Fact]
    public async Task Reject_writes_exactly_one_rejected_event()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime();
        var r = await f.Reject().Handle(new RejectWorkflowTaskCommand(run.Task.Id,
            new RejectWorkflowTaskRequest(Approver, "REJECTED", "reject-1", "SECRET-COMMENT", null), Correlation), default);
        Assert.True(r.IsSuccessful);
        var e = Assert.IsType<WorkflowInstanceCompletedV1>(Assert.Single(f.Events.Events));
        Assert.Equal(WorkflowOutcomes.Rejected, e.Outcome);
        Assert.Equal("REJECTED", e.ReasonCode);
    }

    [Fact]
    public async Task Cancel_writes_exactly_one_cancelled_event()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime();
        var r = await f.Cancel().Handle(new CancelWorkflowTaskCommand(run.Task.Id,
            new CancelWorkflowTaskRequest("workflow-admin", "CANCELLED", "cancel-1", "SECRET-COMMENT"), Correlation), default);
        Assert.True(r.IsSuccessful);
        var e = Assert.IsType<WorkflowInstanceCompletedV1>(Assert.Single(f.Events.Events));
        Assert.Equal(WorkflowOutcomes.Cancelled, e.Outcome);
        Assert.Equal("workflow-admin", e.CompletedBy);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, run.Instance.Status);
    }

    [Fact]
    public async Task Timeout_writes_exactly_one_timed_out_event_and_escalation_writes_none()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime(dueAt: f.Clock.GetUtcNow().AddMinutes(-120));
        f.Store.Rules.Add(new SlaEscalationRule
        {
            TenantId = TenantA, TemplateId = run.Instance.TemplateId, DueInMinutes = 10, EscalateAfterMinutes = 20,
            TimeoutAfterMinutes = 60, EscalationPrincipalIds = ["user:escalation-001"]
        });

        var r = await f.Escalations().Handle(new RunWorkflowEscalationsCommand(
            new RunWorkflowEscalationsRequest(null, 10, "run-1"), Correlation), default);

        Assert.True(r.IsSuccessful, string.Join(",", r.Errors));
        Assert.Equal(1, r.Data!.TimedOutCount);
        var e = Assert.IsType<WorkflowInstanceCompletedV1>(Assert.Single(f.Events.Events));
        Assert.Equal(WorkflowOutcomes.TimedOut, e.Outcome);
        Assert.Null(e.CompletedBy); // the system, not a person
        Assert.Equal(WorkflowReasonCodes.WorkflowTimeoutProcessed, e.ReasonCode);

        // An escalation (non-terminal) writes nothing.
        var g = new Fx(TenantA);
        var run2 = g.SeedRuntime(dueAt: g.Clock.GetUtcNow().AddMinutes(-30));
        g.Store.Rules.Add(new SlaEscalationRule
        {
            TenantId = TenantA, TemplateId = run2.Instance.TemplateId, DueInMinutes = 10, EscalateAfterMinutes = 20,
            TimeoutAfterMinutes = 600, EscalationPrincipalIds = ["user:escalation-001"]
        });
        var escalated = await g.Escalations().Handle(new RunWorkflowEscalationsCommand(
            new RunWorkflowEscalationsRequest(null, 10, "run-2"), Correlation), default);
        Assert.Equal(1, escalated.Data!.EscalatedCount);
        Assert.Empty(g.Events.Events);
    }

    [Fact]
    public async Task Non_terminal_approve_to_a_next_step_writes_no_event()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime(twoSteps: true);
        var r = await f.Approve().Handle(ApproveCmd(run.Task.Id), default);
        Assert.True(r.IsSuccessful, string.Join(",", r.Errors));
        Assert.Equal("Active", r.Data!.NewInstanceStatus);
        Assert.Empty(f.Events.Events);
        Assert.Equal(0, f.Transactions.Calls);
    }

    [Fact]
    public async Task A_conflict_inside_the_transaction_writes_nothing_and_returns_409()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime();
        f.Store.FailInstanceUpdate = true;
        var r = await f.Approve().Handle(ApproveCmd(run.Task.Id), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, r.ReasonCode);
        Assert.Empty(f.Events.Events);
        Assert.DoesNotContain(f.Store.Logs, l => l.Action == WorkflowTransitionAction.Approve);
    }

    [Fact]
    public async Task No_transaction_support_is_503_not_a_silent_write_without_event()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime();
        f.Transactions.Unavailable = true;
        var r = await f.Approve().Handle(ApproveCmd(run.Task.Id), default);
        Assert.Equal(503, r.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransactionUnavailable, r.ReasonCode);
        Assert.Empty(f.Events.Events);
    }

    [Fact]
    public async Task Sod_submitter_still_cannot_approve_their_own_workflow()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime(startedBy: Approver);
        var r = await f.Approve().Handle(ApproveCmd(run.Task.Id), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Equal(WorkflowReasonCodes.SodViolation, r.ReasonCode);
        Assert.Empty(f.Events.Events);
    }

    [Fact]
    public void Completion_event_passes_the_production_contract_validator()
        => new EventPayloadContractValidator().Validate(new WorkflowInstanceCompletedV1(Guid.NewGuid(),
            DateTimeOffset.UtcNow, TenantA, Guid.NewGuid(), Guid.NewGuid(), "T", Guid.NewGuid(), "crm.claim", "1",
            "crm|crm.claim|1", "approved", DateTimeOffset.UtcNow, "u", "stage-1", "step-1", "R"));

    // ============================================================ candidate fix

    [Fact]
    public async Task Cancelled_deleted_ended_assignments_and_non_active_positions_are_not_candidates()
    {
        var f = new Fx(TenantA);
        var now = DateTimeOffset.UtcNow;
        Guid Pos(PositionStatus status, bool archived = false)
        {
            var p = new Position
            {
                TenantId = TenantA, Code = Guid.NewGuid().ToString("N")[..6], Name = "P",
                OrganizationUnitId = Guid.NewGuid(), Status = status, IsArchived = archived
            };
            f.Store.Positions.Add(p);
            return p.Id;
        }

        Guid Assign(Guid positionId, AssignmentType type = AssignmentType.Primary, bool cancelled = false,
            bool deleted = false, DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            var user = Guid.NewGuid();
            f.Store.Assignments.Add(new PositionAssignment
            {
                TenantId = TenantA, PositionId = positionId, UserId = user, AssignmentType = type,
                EffectiveFrom = from ?? now.AddDays(-10), EffectiveTo = to, IsCancelled = cancelled, IsDeleted = deleted
            });
            return user;
        }

        var active = Pos(PositionStatus.Active);
        var primary = Assign(active);
        var secondary = Assign(active, AssignmentType.Secondary);
        var acting = Assign(active, AssignmentType.Acting);
        var delegated = Assign(active, AssignmentType.Delegated);
        var cancelled = Assign(active, cancelled: true);
        var deleted = Assign(active, deleted: true);
        var ended = Assign(active, to: now.AddDays(-1));
        var planned = Assign(active, from: now.AddDays(5));
        var frozenHolder = Assign(Pos(PositionStatus.Frozen));
        var closedHolder = Assign(Pos(PositionStatus.Closed));
        var draftHolder = Assign(Pos(PositionStatus.Draft));
        var archivedHolder = Assign(Pos(PositionStatus.Active, archived: true));

        var all = f.Store.Positions.Select(p => "position:" + p.Id).ToList();
        var resolved = await WorkflowCandidateResolver.ResolveAsync(all, f.AssignmentRepo, f.PositionRepo, default);

        Assert.Equal(new[] { primary, secondary, acting, delegated }.Select(x => x.ToString()).OrderBy(x => x, StringComparer.Ordinal),
            resolved);
        foreach (var excluded in new[] { cancelled, deleted, ended, planned, frozenHolder, closedHolder, draftHolder, archivedHolder })
        {
            Assert.DoesNotContain(excluded.ToString(), resolved);
        }

        Assert.Equal(0, f.AssignmentRepo.GetAllCalls); // narrow read, never the whole table

        // Fail-closed: a position cannot be verified without the position repository.
        Assert.Empty(await WorkflowCandidateResolver.ResolveAsync(all, f.AssignmentRepo, null, default));
        // user: and bare principals are untouched.
        Assert.Equal(["u-1", "u-2"], await WorkflowCandidateResolver.ResolveAsync(["user:u-2", "u-1"], null, null, default));
    }

    [Fact]
    public async Task Other_tenants_positions_do_not_resolve()
    {
        var a = new Fx(TenantA);
        var position = new Position
        {
            TenantId = TenantA, Code = "P1", Name = "P1", OrganizationUnitId = Guid.NewGuid(), Status = PositionStatus.Active
        };
        a.Store.Positions.Add(position);
        a.Store.Assignments.Add(new PositionAssignment
        {
            TenantId = TenantA, PositionId = position.Id, UserId = Guid.NewGuid(), EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var b = new Fx(TenantB, a.Store);
        Assert.Empty(await WorkflowCandidateResolver.ResolveAsync(["position:" + position.Id], b.AssignmentRepo, b.PositionRepo, default));
        Assert.Single(await WorkflowCandidateResolver.ResolveAsync(["position:" + position.Id], a.AssignmentRepo, a.PositionRepo, default));
    }

    [Fact]
    public async Task Start_uses_the_fixed_resolver_for_position_candidates()
    {
        var f = new Fx(TenantA);
        var frozen = new Position
        {
            TenantId = TenantA, Code = "FZ", Name = "FZ", OrganizationUnitId = Guid.NewGuid(), Status = PositionStatus.Frozen
        };
        f.Store.Positions.Add(frozen);
        f.Store.Assignments.Add(new PositionAssignment
        {
            TenantId = TenantA, PositionId = frozen.Id, UserId = Guid.NewGuid(), EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var template = f.SeedTemplate("T-FROZEN");
        var r = await f.Start().Handle(new StartWorkflowInstanceCommand(
            StartRequest(template.Id, ["position:" + frozen.Id]), Correlation), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, r.ReasonCode);
    }

    // ============================================================ batch read

    [Fact]
    public async Task Batch_read_returns_newest_first_per_object_with_outcome_and_isolates_tenants()
    {
        var a = new Fx(TenantA);
        var now = DateTimeOffset.UtcNow;
        a.Store.Instances.Add(InstanceRow(TenantA, "c1", WorkflowInstanceStatus.Rejected, now.AddDays(-2), now.AddDays(-1)));
        a.Store.Instances.Add(InstanceRow(TenantA, "c1", WorkflowInstanceStatus.Active, now, null));
        a.Store.Instances.Add(InstanceRow(TenantA, "c2", WorkflowInstanceStatus.Completed, now.AddDays(-3), now.AddDays(-2)));
        a.Store.Instances.Add(InstanceRow(TenantB, "c3", WorkflowInstanceStatus.Active, now, null));
        a.Store.Instances.Add(InstanceRow(TenantA, "c1", WorkflowInstanceStatus.Active, now, null, objectType: "other"));

        var r = await new GetWorkflowInstancesByObjectsHandler(a.InstanceRepo).Handle(
            new GetWorkflowInstancesByObjectsQuery("crm.claim", ["c1,c2", "c3", "c4"], Correlation), default);

        Assert.True(r.IsSuccessful);
        var rows = r.Data!.ToDictionary(x => x.ObjectId);
        Assert.Equal(["Active", "Rejected"], rows["c1"].Instances.Select(x => x.Status));
        Assert.Equal([null, WorkflowOutcomes.Rejected], rows["c1"].Instances.Select(x => x.Outcome));
        Assert.Equal(WorkflowOutcomes.Approved, rows["c2"].Instances.Single().Outcome);
        Assert.Empty(rows["c3"].Instances); // tenant B's instance is invisible
        Assert.Empty(rows["c4"].Instances);
    }

    [Fact]
    public async Task Batch_read_limits_to_100_ids_and_requires_type_and_ids()
    {
        var f = new Fx(TenantA);
        var handler = new GetWorkflowInstancesByObjectsHandler(f.InstanceRepo);
        var hundred = Enumerable.Range(0, 100).Select(i => "o" + i).ToList();
        Assert.True((await handler.Handle(new GetWorkflowInstancesByObjectsQuery("crm.claim", hundred, Correlation), default)).IsSuccessful);

        var tooMany = await handler.Handle(new GetWorkflowInstancesByObjectsQuery("crm.claim",
            [string.Join(",", Enumerable.Range(0, 101).Select(i => "o" + i))], Correlation), default);
        Assert.Equal(400, tooMany.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowBatchLimitExceeded, tooMany.ReasonCode);
        Assert.Equal(400, (await handler.Handle(new GetWorkflowInstancesByObjectsQuery(" ", ["o1"], Correlation), default)).StatusCode);
        Assert.Equal(400, (await handler.Handle(new GetWorkflowInstancesByObjectsQuery("crm.claim", [], Correlation), default)).StatusCode);
    }

    [Fact]
    public void Batch_endpoint_uses_the_existing_view_permission()
    {
        var method = typeof(WorkflowDefinitionsController).GetMethod(nameof(WorkflowDefinitionsController.GetInstancesByObjects))!;
        Assert.Equal("instances/by-objects", method.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(WorkflowPermissions.InstancesView, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    // ============================================================ WP-CL-BE-3a — empty request candidates

    [Fact]
    public void Validator_accepts_an_empty_candidate_list_but_still_rejects_a_blank_entry()
    {
        var template = Guid.NewGuid();
        Assert.True(new StartWorkflowInstanceValidator().Validate(
            new StartWorkflowInstanceCommand(StartRequest(template, []), Correlation)).IsValid);
        Assert.False(new StartWorkflowInstanceValidator().Validate(
            new StartWorkflowInstanceCommand(StartRequest(template, [" "]), Correlation)).IsValid);
        Assert.False(new StartWorkflowInstanceValidator().Validate(
            new StartWorkflowInstanceCommand(StartRequest(template, [new string('x', 257)]), Correlation)).IsValid);
    }

    [Fact]
    public async Task Template_position_candidates_start_the_instance_when_the_request_list_is_empty()
    {
        var f = new Fx(TenantA);
        var position = new Position
        {
            TenantId = TenantA, Code = "MLR", Name = "MLR", OrganizationUnitId = Guid.NewGuid(), Status = PositionStatus.Active
        };
        f.Store.Positions.Add(position);
        var holder = Guid.NewGuid();
        f.Store.Assignments.Add(new PositionAssignment
        {
            TenantId = TenantA, PositionId = position.Id, UserId = holder, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var template = f.SeedTemplateJson("T-POS",
            "{\"stages\":[{\"code\":\"medical\",\"steps\":[{\"code\":\"review\",\"name\":\"Medical review\","
            + "\"assignment\":{\"candidatePrincipalIds\":[\"position:" + position.Id + "\"]}}]}]}");

        var command = new StartWorkflowInstanceCommand(StartRequest(template.Id, []), Correlation);
        Assert.True(new StartWorkflowInstanceValidator().Validate(command).IsValid); // the pipeline lets it through
        var r = await f.Start().Handle(command, default);

        Assert.True(r.IsSuccessful, string.Join(",", r.Errors));
        Assert.Equal("medical", r.Data!.CurrentStage);
        var task = f.Store.Tasks.Single(t => t.WorkflowInstanceId == r.Data.WorkflowInstanceId);
        Assert.Equal(holder.ToString(), task.AssigneeRef);
    }

    [Fact]
    public async Task No_template_and_no_request_candidates_is_still_400_candidates_required()
    {
        var f = new Fx(TenantA);
        var template = f.SeedTemplate("T-NONE");
        var r = await f.Start().Handle(new StartWorkflowInstanceCommand(StartRequest(template.Id, []), Correlation), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, r.ReasonCode);
        Assert.Empty(f.Store.Instances);
        Assert.Empty(f.Store.Tasks);
    }

    [Fact]
    public async Task Request_candidates_are_used_when_the_template_has_none()
    {
        var f = new Fx(TenantA);
        var template = f.SeedTemplate("T-REQ");
        var r = await f.Start().Handle(new StartWorkflowInstanceCommand(
            StartRequest(template.Id, ["user:request-approver"]), Correlation), default);
        Assert.True(r.IsSuccessful, string.Join(",", r.Errors));
        Assert.Equal("request-approver", f.Store.Tasks.Single().AssigneeRef);
    }

    // ============================================================ WP-CL-BE-3a — instance history

    [Fact]
    public async Task History_lists_start_approve_reject_in_sequence_with_comments_and_step_names()
    {
        var f = new Fx(TenantA);
        var run = f.SeedRuntime(twoSteps: true);
        Assert.True((await f.Approve().Handle(ApproveCmd(run.Task.Id), default)).IsSuccessful);
        var second = f.Store.Tasks.Single(t => t.StageCode == "stage-2");
        var rejected = await f.Reject().Handle(new RejectWorkflowTaskCommand(second.Id,
            new RejectWorkflowTaskRequest("second-approver", "NOT_SUBSTANTIATED", "reject-2", "Evidence is outdated", null),
            Correlation), default);
        Assert.True(rejected.IsSuccessful, string.Join(",", rejected.Errors));

        var r = await f.History().Handle(new GetWorkflowInstanceHistoryQuery(run.Instance.Id, Correlation), default);

        Assert.True(r.IsSuccessful);
        var rows = r.Data!;
        Assert.Equal([1L, 2L, 3L, 4L], rows.Select(x => x.SequenceNo));
        Assert.Equal(["start", "approve", "start", "reject"], rows.Select(x => x.Action));

        Assert.Equal(("stage-1", "step-1"), (rows[0].ToStageCode, rows[0].ToStepCode));
        Assert.Null(rows[0].FromStageCode);

        Assert.Equal(Approver, rows[1].ActorId);
        Assert.Equal("SECRET-COMMENT", rows[1].Comment);
        Assert.Equal("APPROVED", rows[1].ReasonCode);
        Assert.Equal(("stage-1", "step-1", "stage-2", "step-2"),
            (rows[1].FromStageCode, rows[1].FromStepCode, rows[1].ToStageCode, rows[1].ToStepCode));

        Assert.Null(rows[2].Comment); // the engine's internal next-step marker is not user text

        Assert.Equal("second-approver", rows[3].ActorId);
        Assert.Equal("Evidence is outdated", rows[3].Comment);
        Assert.Equal("NOT_SUBSTANTIATED", rows[3].ReasonCode);
        Assert.Equal(("stage-2", "step-2"), (rows[3].FromStageCode, rows[3].FromStepCode));
        Assert.Null(rows[3].ToStageCode); // terminal

        // The template has no step names → StepName falls back to the step code (template-version resolution).
        Assert.Equal("step-2", rows[3].StepName);
        Assert.Null(rows[0].ActorDisplay); // no resolver wired → no name, never the id
    }

    [Fact]
    public async Task History_resolves_step_name_from_the_template_version_and_actor_display_through_the_resolver()
    {
        var f = new Fx(TenantA);
        var actor = Guid.NewGuid();
        var template = f.SeedTemplateJson("T-NAMED",
            "{\"stages\":[{\"code\":\"medical\",\"steps\":[{\"code\":\"review\",\"name\":\"Medical review\","
            + "\"assignment\":{\"candidatePrincipalIds\":[\"user:" + actor + "\"]}}]}]}");
        var start = await f.Start().Handle(new StartWorkflowInstanceCommand(StartRequest(template.Id, []), Correlation), default);
        Assert.True(start.IsSuccessful, string.Join(",", start.Errors));

        var names = new FakeNames(new Dictionary<Guid, string> { [Guid.Parse("5b000000-0000-0000-0000-000000000001")] = "Ayşe Yılmaz" });
        var row = Assert.Single((await f.History(names).Handle(
            new GetWorkflowInstanceHistoryQuery(start.Data!.WorkflowInstanceId, Correlation), default)).Data!);
        Assert.Equal("Medical review", row.StepName);
        Assert.Equal(("medical", "review"), (row.ToStageCode, row.ToStepCode));
        Assert.Equal("Ayşe Yılmaz", row.ActorDisplay);

        var down = new FakeNames(null); // resolver throws → rows still come back, name absent
        Assert.Null(Assert.Single((await f.History(down).Handle(
            new GetWorkflowInstanceHistoryQuery(start.Data.WorkflowInstanceId, Correlation), default)).Data!).ActorDisplay);
    }

    [Fact]
    public async Task History_of_another_tenants_instance_is_a_non_leaking_404()
    {
        var a = new Fx(TenantA);
        var run = a.SeedRuntime();
        var b = new Fx(TenantB, a.Store);
        var r = await b.History().Handle(new GetWorkflowInstanceHistoryQuery(run.Instance.Id, Correlation), default);
        Assert.Equal(404, r.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, r.ReasonCode);
        Assert.Null(r.Data);
    }

    [Fact]
    public void History_endpoint_requires_the_existing_instances_view_permission()
    {
        var method = typeof(WorkflowDefinitionsController).GetMethod(nameof(WorkflowDefinitionsController.GetInstanceHistory))!;
        Assert.Equal("instances/{id:guid}/history", method.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(WorkflowPermissions.InstancesView, method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
        Assert.NotNull(typeof(WorkflowDefinitionsController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
    }

    private sealed class FakeNames(IReadOnlyDictionary<Guid, string>? names) : IUserDisplayNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => names is null
                ? throw new HttpRequestException("auth unreachable")
                : Task.FromResult<IReadOnlyDictionary<Guid, string>>(names.Where(kv => userIds.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value));
    }

    // ============================================================ helpers

    private static StartWorkflowInstanceRequest StartRequest(Guid templateId, IReadOnlyList<string> candidates,
        WorkflowDisplayContext? display = null) =>
        new(templateId, null, "crm.claim", "claim-1", "crm|crm.claim|claim-1", candidates, "SUBMITTED", null, false, false,
            null, display);

    private static ApproveWorkflowTaskCommand ApproveCmd(Guid taskId) =>
        new(taskId, new ApproveWorkflowTaskRequest(Approver, "APPROVED", "approve-1", "SECRET-COMMENT", null), Correlation);

    private static WorkItemActor Actor() => new(Guid.Parse("99999999-0000-0000-0000-000000000001"), IsPlatformActor: true, new HashSet<string>());

    private static WorkflowInstance Instance(string objectType, string objectId, WorkflowDisplayContextSnapshot? display) => new()
    {
        TenantId = TaskTestData.Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = objectType,
        ObjectId = objectId,
        ObjectRef = $"x|{objectType}|{objectId}",
        Status = WorkflowInstanceStatus.Active,
        DisplayContext = display
    };

    private static WorkflowInstance InstanceRow(Guid tenant, string objectId, WorkflowInstanceStatus status,
        DateTimeOffset startedAt, DateTimeOffset? completedAt, string objectType = "crm.claim") => new()
    {
        TenantId = tenant, TemplateId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(), ObjectType = objectType,
        ObjectId = objectId, ObjectRef = $"crm|{objectType}|{objectId}", Status = status, StartedAt = startedAt,
        CompletedAt = completedAt
    };

    private static WorkflowApprovalWorkItemProvider Provider(WorkflowInstance instance, params IApprovalSourceResolver[] resolvers)
    {
        var task = new ApprovalTask
        {
            TenantId = instance.TenantId, WorkflowInstanceId = instance.Id, StageCode = "stage-1", StepCode = "step-1",
            Status = ApprovalTaskStatus.WaitingApproval, AssigneeRef = Actor().UserId.ToString()
        };
        var store = new Store();
        store.Instances.Add(instance);
        store.Tasks.Add(task);
        var tenant = new TenantContext();
        tenant.SetTenant(instance.TenantId);
        return new WorkflowApprovalWorkItemProvider(new TaskRepo(store, tenant), new SnapshotRepo(store, tenant),
            new InstanceRepo(store, tenant), new WorkItemProjectionService(SlaForTests.Real()), resolvers);
    }

    private sealed class Fx
    {
        public Store Store { get; }
        public TenantContext Tenant { get; } = new();
        public CapturingEvents Events { get; } = new();
        public ImmediateExecutor Transactions { get; } = new();
        public FixedClock Clock { get; } = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

        public Fx(Guid tenant, Store? shared = null)
        {
            Store = shared ?? new Store();
            Tenant.SetTenant(tenant);
        }

        public TaskRepo TaskRepo => new(Store, Tenant);
        public InstanceRepo InstanceRepo => new(Store, Tenant);
        public SnapshotRepo SnapshotRepo => new(Store, Tenant);
        public LogRepo LogRepo => new(Store, Tenant);
        public TemplateRepo TemplateRepo => new(Store, Tenant);
        public VersionRepo VersionRepo => new(Store, Tenant);
        public AssignmentRepo AssignmentRepo { get => _assignments ??= new AssignmentRepo(Store, Tenant); }
        public PositionRepo PositionRepo => new(Store, Tenant);
        private AssignmentRepo? _assignments;

        public StartWorkflowInstanceHandler Start() => new(TemplateRepo, VersionRepo, InstanceRepo, TaskRepo, SnapshotRepo,
            LogRepo, Tenant, new User(), AssignmentRepo, null, PositionRepo);

        public ApproveWorkflowTaskHandler Approve() => new(TaskRepo, InstanceRepo, SnapshotRepo, LogRepo, VersionRepo,
            AssignmentRepo, PositionRepo, Transactions, Events, TemplateRepo);

        public RejectWorkflowTaskHandler Reject() => new(TaskRepo, InstanceRepo, SnapshotRepo, LogRepo, Transactions, Events, TemplateRepo);
        public CancelWorkflowTaskHandler Cancel() => new(TaskRepo, InstanceRepo, SnapshotRepo, LogRepo, Transactions, Events, TemplateRepo);

        public RunWorkflowEscalationsHandler Escalations() => new(TaskRepo, InstanceRepo, new RuleRepo(Store, Tenant), LogRepo,
            Clock, AssignmentRepo, SnapshotRepo, PositionRepo, Transactions, Events, TemplateRepo);

        public GetWorkflowInstanceHistoryHandler History(IUserDisplayNameResolver? names = null)
            => new(InstanceRepo, LogRepo, TaskRepo, VersionRepo, names);

        public WorkflowTemplate SeedTemplateJson(string code, string definitionJson)
        {
            var template = SeedTemplate(code);
            Store.Versions.Single(v => v.TemplateId == template.Id).DefinitionJson = definitionJson;
            return template;
        }

        public WorkflowTemplate SeedTemplate(string code, bool twoSteps = false)
        {
            var template = new WorkflowTemplate
            {
                TenantId = Tenant.TenantId, TemplateCode = code, Name = code, Status = WorkflowTemplateStatus.Published
            };
            var version = new WorkflowTemplateVersion
            {
                TenantId = Tenant.TenantId, TemplateId = template.Id, VersionNumber = 1,
                DefinitionJson = twoSteps
                    ? "{\"stages\":[{\"code\":\"stage-1\",\"steps\":[{\"code\":\"step-1\",\"assignment\":{\"candidatePrincipalIds\":[\"user:"
                      + Approver + "\"]}}]},{\"code\":\"stage-2\",\"steps\":[{\"code\":\"step-2\",\"assignment\":"
                      + "{\"candidatePrincipalIds\":[\"user:second-approver\"]}}]}]}"
                    : "{}",
                SchemaVersion = "1.0", ExpressionVersion = "1.0", Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true, PublishedAt = DateTime.UtcNow, PublishedBy = "publisher"
            };
            template.ActivePublishedVersionId = version.Id;
            template.CurrentVersionId = version.Id;
            Store.Templates.Add(template);
            Store.Versions.Add(version);
            return template;
        }

        public (WorkflowInstance Instance, ApprovalTask Task) SeedRuntime(string startedBy = Submitter,
            DateTimeOffset? dueAt = null, bool twoSteps = false)
        {
            var template = SeedTemplate("CLAIM-APPROVAL", twoSteps);
            var instance = new WorkflowInstance
            {
                TenantId = Tenant.TenantId, TemplateId = template.Id, WorkflowTemplateId = template.Id,
                TemplateVersionId = template.ActivePublishedVersionId, ObjectType = "crm.claim", ObjectId = "claim-1",
                ObjectRef = "crm|crm.claim|claim-1", Status = WorkflowInstanceStatus.Active, StartedBy = startedBy,
                StartedAt = DateTimeOffset.UtcNow
            };
            var task = new ApprovalTask
            {
                TenantId = Tenant.TenantId, WorkflowInstanceId = instance.Id, StageCode = "stage-1", StepCode = "step-1",
                Status = ApprovalTaskStatus.WaitingApproval, AssigneeRef = Approver, DueAt = dueAt
            };
            var snapshot = new RuntimeAssignmentSnapshot
            {
                TenantId = Tenant.TenantId, WorkflowInstanceId = instance.Id, ApprovalTaskId = task.Id, ResolverSource = "test",
                ResolvedPrincipalId = Approver, CandidatePrincipalIds = [Approver], ResolvedAt = DateTime.UtcNow,
                TieBreakExplanation = "single_candidate"
            };
            task.AssignmentSnapshotId = snapshot.Id;
            Store.Instances.Add(instance);
            Store.Tasks.Add(task);
            Store.Snapshots.Add(snapshot);
            Store.Logs.Add(new WorkflowTransitionLog
            {
                TenantId = Tenant.TenantId, WorkflowInstanceId = instance.Id, ApprovalTaskId = task.Id,
                Action = WorkflowTransitionAction.Start, SequenceNo = 1
            });
            return (instance, task);
        }
    }

    private sealed class Store
    {
        public List<WorkflowTemplate> Templates { get; } = [];
        public List<WorkflowTemplateVersion> Versions { get; } = [];
        public List<WorkflowInstance> Instances { get; } = [];
        public List<ApprovalTask> Tasks { get; } = [];
        public List<RuntimeAssignmentSnapshot> Snapshots { get; } = [];
        public List<WorkflowTransitionLog> Logs { get; } = [];
        public List<SlaEscalationRule> Rules { get; } = [];
        public List<Position> Positions { get; } = [];
        public List<PositionAssignment> Assignments { get; } = [];
        public bool FailInstanceUpdate { get; set; }
    }

    private sealed class User : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("5b000000-0000-0000-0000-000000000001");
        public string? Email => "submitter@example.test";
        public string? DisplayName => "Submitter";
        public string ActorName => Submitter;
        public bool IsAuthenticated => true;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Session : IPlatformTransactionSession
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
    }

    private sealed class ImmediateExecutor : IPlatformTransactionExecutor
    {
        public int Calls { get; private set; }
        public bool Unavailable { get; set; }

        public Task<T> ExecuteAsync<T>(Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Unavailable)
            {
                throw new PlatformTransactionUnavailableException("no transactions", new InvalidOperationException());
            }

            return body(new Session(), cancellationToken);
        }
    }

    private sealed class CapturingEvents : ITransactionalIntegrationEventWriter
    {
        private readonly EventPayloadContractValidator _validator = new();
        private readonly HashSet<Guid> _ids = [];
        public List<IIntegrationEvent> Events { get; } = [];

        public Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent
        {
            Assert.IsType<Session>(session); // written inside the transaction
            _validator.Validate(@event);
            if (!_ids.Add(options.EventId!.Value))
            {
                throw new InvalidOperationException("Transactional integration-event intent was not inserted exactly once.");
            }

            Events.Add(@event);
            return Task.FromResult(new EventEnvelope<TEvent>(new EventMetadata(options.EventId.Value, @event.EventName,
                @event.EventVersion, options.CorrelationId ?? Guid.NewGuid(), null, options.TenantId,
                options.Producer ?? "test", DateTimeOffset.UtcNow), @event));
        }
    }

    // ---- tenant-filtering in-memory repositories (same scoping as TenantRepository) ----

    private sealed class TemplateRepo(Store s, ITenantContext t) : IWorkflowTemplateRepository
    {
        private IEnumerable<WorkflowTemplate> Scoped => s.Templates.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate x, CancellationToken ct = default) { s.Templates.Add(x); return Task.FromResult(x); }
        public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.TemplateCode == code));
        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowTemplate>>(Scoped.ToList());
        public Task<bool> UpdateAsync(WorkflowTemplate x, int v, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class VersionRepo(Store s, ITenantContext t) : IWorkflowTemplateVersionRepository
    {
        private IEnumerable<WorkflowTemplateVersion> Scoped => s.Versions.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion x, CancellationToken ct = default) { s.Versions.Add(x); return Task.FromResult(x); }
        public Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id && x.TemplateId == templateId));
        public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default) => Task.FromResult(Scoped.Where(x => x.TemplateId == templateId).MaxBy(x => x.VersionNumber));
        public Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default) => Task.FromResult(Scoped.Where(x => x.TemplateId == templateId).Select(x => x.VersionNumber).DefaultIfEmpty(0).Max());
        public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.TemplateId == templateId));
        public Task<bool> ExistsVersionNumberAsync(Guid templateId, int n, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowTemplateVersion>>(Scoped.Where(x => x.TemplateId == templateId).ToList());
        public Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(WorkflowTemplateVersion x, int v, CancellationToken ct = default) => Task.FromResult(WorkflowTemplateVersionUpdateResult.Updated);
    }

    private sealed class InstanceRepo(Store s, ITenantContext t) : IWorkflowInstanceRepository
    {
        private IEnumerable<WorkflowInstance> Scoped => s.Instances.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance x, CancellationToken ct = default) { s.Instances.Add(x); return Task.FromResult(x); }
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.IdempotencyKey == key));
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string r, string ty, string id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ObjectRef == r));
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowInstance>>(Scoped.ToList());
        public Task<bool> UpdateAsync(WorkflowInstance x, int v, CancellationToken ct = default)
        {
            if (s.FailInstanceUpdate || !Scoped.Any(i => i.Id == x.Id && i.Version == v)) return Task.FromResult(false);
            x.Version = v + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class TaskRepo(Store s, ITenantContext t) : IApprovalTaskRepository
    {
        private IEnumerable<ApprovalTask> Scoped => s.Tasks.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<ApprovalTask> CreateAsync(ApprovalTask x, CancellationToken ct = default) { s.Tasks.Add(x); return Task.FromResult(x); }
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.WorkflowInstanceId == id));
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.WorkflowInstanceId == id));
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApprovalTask>>(Scoped.Where(x => x.WorkflowInstanceId == id).ToList());
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApprovalTask>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ApprovalTask x, int v, CancellationToken ct = default)
        {
            if (!Scoped.Any(i => i.Id == x.Id && i.Version == v)) return Task.FromResult(false);
            x.Version = v + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class SnapshotRepo(Store s, ITenantContext t) : IRuntimeAssignmentSnapshotRepository
    {
        private IEnumerable<RuntimeAssignmentSnapshot> Scoped => s.Snapshots.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot x, CancellationToken ct = default) { s.Snapshots.Add(x); return Task.FromResult(x); }
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<RuntimeAssignmentSnapshot>>(Scoped.Where(x => x.WorkflowInstanceId == id).ToList());
    }

    private sealed class LogRepo(Store s, ITenantContext t) : IWorkflowTransitionLogRepository
    {
        private IEnumerable<WorkflowTransitionLog> Scoped => s.Logs.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog x, CancellationToken ct = default) { s.Logs.Add(x); return Task.FromResult(x); }
        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(Guid taskId, WorkflowTransitionAction a, string key, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.ApprovalTaskId == taskId && x.Action == a && x.IdempotencyKey == key));
        public Task<long> GetLatestSequenceNoAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.Where(x => x.WorkflowInstanceId == id).Select(x => x.SequenceNo).DefaultIfEmpty(0).Max());
        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowTransitionLog>>(Scoped.Where(x => x.WorkflowInstanceId == id).ToList());
    }

    private sealed class RuleRepo(Store s, ITenantContext t) : ISlaEscalationRuleRepository
    {
        private IEnumerable<SlaEscalationRule> Scoped => s.Rules.Where(x => x.TenantId == t.TenantId && x.IsActive);
        public Task<SlaEscalationRule> CreateAsync(SlaEscalationRule x, CancellationToken ct = default) { s.Rules.Add(x); return Task.FromResult(x); }
        public Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SlaEscalationRule>>(Scoped.Where(x => x.TemplateId == id).ToList());
        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SlaEscalationRule>>(Scoped.ToList());
        public Task<SlaEscalationRule?> FindForStepAsync(Guid templateId, string stage, string step, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.TemplateId == templateId && x.StageCode == stage && x.StepCode == step));
        public Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class PositionRepo(Store s, ITenantContext t) : IPositionRepository
    {
        private IEnumerable<Position> Scoped => s.Positions.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<Position> CreateAsync(Position x, CancellationToken ct = default) { s.Positions.Add(x); return Task.FromResult(x); }
        public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Position>>(Scoped.ToList());
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateAsync(Position x, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class AssignmentRepo(Store s, ITenantContext t) : IPositionAssignmentRepository
    {
        public int GetAllCalls { get; private set; }
        private IEnumerable<PositionAssignment> Scoped => s.Assignments.Where(x => x.TenantId == t.TenantId && !x.IsDeleted);
        public Task<PositionAssignment> CreateAsync(PositionAssignment x, CancellationToken ct = default) { s.Assignments.Add(x); return Task.FromResult(x); }
        public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default) { GetAllCalls++; return Task.FromResult<IReadOnlyList<PositionAssignment>>(Scoped.ToList()); }
        public Task<IReadOnlyList<PositionAssignment>> GetByPositionIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PositionAssignment>>(Scoped.Where(x => ids.Contains(x.PositionId)).ToList());
        public Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset from, DateTimeOffset? to, Guid? excludeId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task UpdateAsync(PositionAssignment x, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
