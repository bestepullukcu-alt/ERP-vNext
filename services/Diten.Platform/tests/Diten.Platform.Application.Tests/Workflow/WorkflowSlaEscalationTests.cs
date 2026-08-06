using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Validators;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class WorkflowSlaEscalationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Correlation = "wf-sla-corr-001";

    [Fact]
    public async Task Create_sla_rule_for_same_tenant_template_persists_with_normalized_principals()
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-SLA"));

        var response = await f.CreateRule.Handle(CreateRule(template.Id, principals: [" user-b ", "user-a", "user-a"]), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(["user-a", "user-b"], response.Data!.EscalationPrincipalIds);
        Assert.Single(f.Rules.Items);
    }

    [Fact]
    public async Task Cross_tenant_template_rule_create_returns_not_found_non_leakage()
    {
        var f = Fixture(TenantA);
        var template = await f.Templates.CreateAsync(Template("WF-CROSS-SLA"));

        f.TenantContext.SetTenant(TenantB);
        var response = await f.CreateRule.Handle(CreateRule(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Empty(f.Rules.Items);
    }

    [Fact]
    public void Invalid_minutes_are_validation_failed()
    {
        var validation = new CreateSlaEscalationRuleValidator().Validate(
            CreateRule(Guid.NewGuid(), dueInMinutes: 10, escalateAfterMinutes: 5));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task List_rules_returns_only_current_tenant_records()
    {
        var f = Fixture(TenantA);
        var templateA = await f.Templates.CreateAsync(Template("WF-LIST-A"));
        await f.CreateRule.Handle(CreateRule(templateA.Id), CancellationToken.None);

        f.TenantContext.SetTenant(TenantB);
        var templateB = await f.Templates.CreateAsync(Template("WF-LIST-B"));
        await f.CreateRule.Handle(CreateRule(templateB.Id), CancellationToken.None);

        var response = await f.ListRules.Handle(new GetSlaEscalationRulesQuery(null, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!);
        Assert.Equal(templateB.Id, response.Data![0].TemplateId);
    }

    [Fact]
    public async Task Start_with_matching_sla_rule_sets_due_at_and_no_rule_leaves_due_at_null()
    {
        var f = Fixture(TenantA);
        var templateId = await f.AddPublishedTemplateAsync("WF-DUE");
        await f.CreateRule.Handle(CreateRule(templateId, dueInMinutes: 30), CancellationToken.None);

        var withRule = await f.Start.Handle(Start(templateId, dueAt: null), CancellationToken.None);
        var noRuleTemplateId = await f.AddPublishedTemplateAsync("WF-NO-DUE");
        var noRule = await f.Start.Handle(Start(noRuleTemplateId, dueAt: null), CancellationToken.None);

        Assert.True(withRule.IsSuccessful);
        Assert.NotNull(f.Tasks.Items.Single(x => x.Id == withRule.Data!.ApprovalTaskId).DueAt);
        Assert.Null(f.Tasks.Items.Single(x => x.Id == noRule.Data!.ApprovalTaskId).DueAt);
    }

    [Theory]
    [InlineData(ApprovalTaskStatus.WaitingApproval)]
    [InlineData(ApprovalTaskStatus.WaitingEvidence)]
    public async Task Overdue_open_task_escalates_and_writes_one_log(ApprovalTaskStatus status)
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeWithRuleAsync(status, dueAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await f.Run.Handle(Run(DateTimeOffset.UtcNow, "run-escalate"), CancellationToken.None);
        var duplicate = await f.Run.Handle(Run(DateTimeOffset.UtcNow, "run-escalate"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, response.Data!.EscalatedCount);
        Assert.Equal(ApprovalTaskStatus.Escalated, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.Escalated, runtime.Instance.Status);
        Assert.Equal(1, runtime.Task.EscalationLevel);
        Assert.NotNull(runtime.Task.EscalatedAt);
        Assert.Equal(WorkflowReasonCodes.WorkflowEscalationProcessed, runtime.Task.LastEscalationReasonCode);
        Assert.Equal(2, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Escalate).SequenceNo);
        Assert.Equal(2, f.Logs.Items.Count);
        Assert.True(duplicate.Data!.Results.Single().IsIdempotent);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Non_overdue_task_is_skipped()
    {
        var f = Fixture(TenantA);
        await f.SeedRuntimeWithRuleAsync(ApprovalTaskStatus.WaitingApproval, dueAt: DateTimeOffset.UtcNow.AddMinutes(5));

        var response = await f.Run.Handle(Run(DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Equal(0, response.Data!.EvaluatedCount);
        Assert.Equal(0, response.Data.EscalatedCount);
        Assert.Single(f.Logs.Items);
    }

    [Theory]
    [InlineData(ApprovalTaskStatus.Approved)]
    [InlineData(ApprovalTaskStatus.Rejected)]
    [InlineData(ApprovalTaskStatus.Cancelled)]
    public async Task Terminal_task_is_not_processed(ApprovalTaskStatus status)
    {
        var f = Fixture(TenantA);
        await f.SeedRuntimeWithRuleAsync(status, dueAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await f.Run.Handle(Run(DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.Equal(0, response.Data!.EvaluatedCount);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public async Task Task_past_timeout_times_out_instance_and_gate_blocks()
    {
        var f = Fixture(TenantA);
        var runtime = await f.SeedRuntimeWithRuleAsync(
            ApprovalTaskStatus.WaitingApproval,
            dueAt: DateTimeOffset.UtcNow.AddMinutes(-45),
            timeoutAfterMinutes: 40);

        var response = await f.Run.Handle(Run(DateTimeOffset.UtcNow, "run-timeout"), CancellationToken.None);
        var gate = await new EvaluateWorkflowTransitionGateHandler(f.Instances, f.Tasks, f.TenantContext)
            .Handle(new EvaluateWorkflowTransitionGateQuery(
                new EvaluateWorkflowTransitionGateRequest(
                    runtime.Instance.ObjectType,
                    runtime.Instance.ObjectId,
                    runtime.Instance.ObjectRef,
                    "submit",
                    "Submitted",
                    "actor",
                    "SUBMIT"),
                Correlation), CancellationToken.None);

        Assert.Equal(1, response.Data!.TimedOutCount);
        Assert.Equal(ApprovalTaskStatus.TimedOut, runtime.Task.Status);
        Assert.Equal(WorkflowInstanceStatus.TimedOut, runtime.Instance.Status);
        Assert.NotNull(runtime.Task.TimedOutAt);
        Assert.Equal(WorkflowTransitionAction.Timeout, f.Logs.Items.Single(x => x.Action == WorkflowTransitionAction.Timeout).Action);
        Assert.Equal(WorkflowTransitionGateDecision.Blocked, gate.Data!.Decision);
        Assert.Equal(WorkflowReasonCodes.WorkflowNotTerminalApproved, gate.Data.BlockingReasonCode);

        var duplicate = await f.Run.Handle(Run(DateTimeOffset.UtcNow, "run-timeout"), CancellationToken.None);
        Assert.True(duplicate.Data!.Results.Single().IsIdempotent);
        Assert.Equal(2, f.Logs.Items.Count);
    }

    [Fact]
    public async Task Cross_tenant_overdue_task_is_not_processed_and_response_carries_correlation()
    {
        var f = Fixture(TenantA);
        await f.SeedRuntimeWithRuleAsync(ApprovalTaskStatus.WaitingApproval, dueAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        f.TenantContext.SetTenant(TenantB);
        var response = await f.Run.Handle(Run(DateTimeOffset.UtcNow), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(Correlation, response.Data!.CorrelationId);
        Assert.Equal(0, response.Data.EvaluatedCount);
        Assert.Single(f.Logs.Items);
    }

    [Fact]
    public void Escalation_permission_constants_are_defined_without_auth_seed_side_effect()
    {
        Assert.Equal("platform.workflow.escalations.manage", WorkflowPermissions.EscalationsManage);
        Assert.Equal("platform.workflow.escalations.run", WorkflowPermissions.EscalationsRun);
    }

    private static CreateSlaEscalationRuleCommand CreateRule(
        Guid templateId,
        int dueInMinutes = 5,
        int escalateAfterMinutes = 5,
        int? timeoutAfterMinutes = null,
        IReadOnlyList<string>? principals = null) =>
        new(new CreateSlaEscalationRuleRequest(
            templateId,
            "stage-1",
            "step-1",
            dueInMinutes,
            escalateAfterMinutes,
            timeoutAfterMinutes,
            principals ?? ["manager-001"]), Correlation);

    private static RunWorkflowEscalationsCommand Run(DateTimeOffset now, string? idempotencyKey = null) =>
        new(new RunWorkflowEscalationsRequest(now, 100, idempotencyKey), Correlation);

    private static StartWorkflowInstanceCommand Start(Guid templateId, DateTimeOffset? dueAt) =>
        new(new StartWorkflowInstanceRequest(
            templateId,
            null,
            "PurchaseOrder",
            Guid.NewGuid().ToString("N"),
            null,
            ["approver-001"],
            "SUBMIT",
            null,
            false,
            false,
            dueAt), Correlation);

    private static WorkflowTemplate Template(string code) => new()
    {
        TenantId = Guid.Empty,
        TemplateCode = code,
        Name = code,
        Status = WorkflowTemplateStatus.Published
    };

    private static TestFixture Fixture(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        var templates = new FakeWorkflowTemplateRepository(tenantContext);
        var versions = new FakeWorkflowTemplateVersionRepository(tenantContext);
        var instances = new FakeWorkflowInstanceRepository(tenantContext);
        var tasks = new FakeApprovalTaskRepository(tenantContext);
        var snapshots = new FakeRuntimeAssignmentSnapshotRepository(tenantContext);
        var logs = new FakeWorkflowTransitionLogRepository(tenantContext);
        var rules = new FakeSlaEscalationRuleRepository(tenantContext);
        return new TestFixture(
            tenantContext,
            templates,
            versions,
            instances,
            tasks,
            snapshots,
            logs,
            rules,
            new CreateSlaEscalationRuleHandler(templates, rules, tenantContext),
            new GetSlaEscalationRulesHandler(rules),
            new RunWorkflowEscalationsHandler(tasks, instances, rules, logs, null, snapshots),
            new StartWorkflowInstanceHandler(
                templates,
                versions,
                instances,
                tasks,
                snapshots,
                logs,
                tenantContext,
                new FakeCurrentUserContext(),
                slaRules: rules));
    }

    private sealed record RuntimeSeed(WorkflowInstance Instance, ApprovalTask Task);

    private sealed record TestFixture(
        TenantContext TenantContext,
        FakeWorkflowTemplateRepository Templates,
        FakeWorkflowTemplateVersionRepository Versions,
        FakeWorkflowInstanceRepository Instances,
        FakeApprovalTaskRepository Tasks,
        FakeRuntimeAssignmentSnapshotRepository Snapshots,
        FakeWorkflowTransitionLogRepository Logs,
        FakeSlaEscalationRuleRepository Rules,
        CreateSlaEscalationRuleHandler CreateRule,
        GetSlaEscalationRulesHandler ListRules,
        RunWorkflowEscalationsHandler Run,
        StartWorkflowInstanceHandler Start)
    {
        public async Task<Guid> AddPublishedTemplateAsync(string code)
        {
            var template = await Templates.CreateAsync(Template(code));
            var version = await Versions.CreateAsync(new WorkflowTemplateVersion
            {
                TenantId = TenantContext.TenantId,
                TemplateId = template.Id,
                VersionNumber = 1,
                DefinitionJson = "{}",
                SchemaVersion = "1.0",
                ExpressionVersion = "1.0",
                Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true
            });
            template.ActivePublishedVersionId = version.Id;
            return template.Id;
        }

        public async Task<RuntimeSeed> SeedRuntimeWithRuleAsync(
            ApprovalTaskStatus status,
            DateTimeOffset dueAt,
            int? timeoutAfterMinutes = null)
        {
            var templateId = await AddPublishedTemplateAsync($"WF-RUN-{Guid.NewGuid():N}");
            await CreateRule.Handle(CreateRuleCommand(templateId, timeoutAfterMinutes), CancellationToken.None);
            var instance = await Instances.CreateAsync(new WorkflowInstance
            {
                TenantId = TenantContext.TenantId,
                TemplateId = templateId,
                WorkflowTemplateId = templateId,
                TemplateVersionId = Guid.NewGuid(),
                ObjectType = "PurchaseOrder",
                ObjectId = Guid.NewGuid().ToString("N"),
                ObjectRef = $"Purchasing|PurchaseOrder|{Guid.NewGuid():N}",
                CurrentStage = "stage-1",
                CurrentStep = "step-1",
                Status = WorkflowInstanceStatus.Active,
                StartedAt = dueAt.AddMinutes(-5),
                DueAt = dueAt,
                LastTransitionAt = dueAt.AddMinutes(-5)
            });
            var task = await Tasks.CreateAsync(new ApprovalTask
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = instance.Id,
                StageCode = "stage-1",
                StepCode = "step-1",
                Status = status,
                DueAt = dueAt
            });
            await Logs.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = instance.Id,
                ApprovalTaskId = task.Id,
                Action = WorkflowTransitionAction.Start,
                SequenceNo = 1
            });
            return new RuntimeSeed(instance, task);
        }

        private static CreateSlaEscalationRuleCommand CreateRuleCommand(Guid templateId, int? timeoutAfterMinutes) =>
            WorkflowSlaEscalationTests.CreateRule(
                templateId,
                dueInMinutes: 5,
                escalateAfterMinutes: 5,
                timeoutAfterMinutes: timeoutAfterMinutes);
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "starter@diten.local";
        public string? DisplayName => "Starter";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    private abstract class TenantFakeRepository<T> where T : Diten.Platform.Common.Persistence.TenantScopedEntity
    {
        protected readonly ITenantContext TenantContext;
        public List<T> Items { get; } = [];
        protected TenantFakeRepository(ITenantContext tenantContext) => TenantContext = tenantContext;
        protected T Add(T item)
        {
            typeof(T).GetProperty(nameof(Diten.Platform.Common.Persistence.TenantScopedEntity.TenantId))!
                .SetValue(item, TenantContext.TenantId);
            Items.Add(item);
            return item;
        }
        protected T? Find(Guid id) => Items.FirstOrDefault(x => x.Id == id && x.TenantId == TenantContext.TenantId && !x.IsDeleted);
        protected IReadOnlyList<T> CurrentTenantItems() => Items.Where(x => x.TenantId == TenantContext.TenantId && !x.IsDeleted).ToList();
    }

    private sealed class FakeWorkflowTemplateRepository(ITenantContext tenantContext)
        : TenantFakeRepository<WorkflowTemplate>(tenantContext), IWorkflowTemplateRepository
    {
        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default) => Task.FromResult(Add(template));
        public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.TemplateCode == templateCode));
        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult(CurrentTenantItems());
        public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeWorkflowTemplateVersionRepository(ITenantContext tenantContext)
        : TenantFakeRepository<WorkflowTemplateVersion>(tenantContext), IWorkflowTemplateVersionRepository
    {
        public Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion version, CancellationToken ct = default) => Task.FromResult(Add(version));
        public Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.TemplateId == templateId && x.Id == id));
        public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().Where(x => x.TemplateId == templateId).OrderByDescending(x => x.VersionNumber).FirstOrDefault());
        public async Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default) => (await GetLatestVersionAsync(templateId, ct))?.VersionNumber ?? 0;
        public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.TemplateId == templateId && x.Status == WorkflowTemplateVersionStatus.Published && x.IsImmutable));
        public Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTemplateVersion>>(CurrentTenantItems().Where(x => x.TemplateId == templateId).ToList());
        public Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(WorkflowTemplateVersion version, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(WorkflowTemplateVersionUpdateResult.Updated);
    }

    private sealed class FakeWorkflowInstanceRepository(ITenantContext tenantContext)
        : TenantFakeRepository<WorkflowInstance>(tenantContext), IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => Task.FromResult(Add(instance));
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().Where(x => x.ObjectRef == objectRef && x.ObjectType == objectType && x.ObjectId == objectId).OrderByDescending(x => x.StartedAt).FirstOrDefault());
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult(CurrentTenantItems());
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        {
            instance.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeApprovalTaskRepository(ITenantContext tenantContext)
        : TenantFakeRepository<ApprovalTask>(tenantContext), IApprovalTaskRepository
    {
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => Task.FromResult(Add(task));
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.WorkflowInstanceId == workflowInstanceId));
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.WorkflowInstanceId == workflowInstanceId && x.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence));
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(CurrentTenantItems().Where(x => x.WorkflowInstanceId == workflowInstanceId).ToList());
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult(CurrentTenantItems());
        public Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(CurrentTenantItems()
                .Where(x => x.DueAt <= nowUtc && x.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence or ApprovalTaskStatus.Escalated or ApprovalTaskStatus.TimedOut)
                .Take(maxItems)
                .ToList());
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
        {
            task.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRuntimeAssignmentSnapshotRepository(ITenantContext tenantContext)
        : TenantFakeRepository<RuntimeAssignmentSnapshot>(tenantContext), IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) => Task.FromResult(Add(snapshot));
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RuntimeAssignmentSnapshot>>(CurrentTenantItems().Where(x => x.WorkflowInstanceId == workflowInstanceId).ToList());
    }

    private sealed class FakeWorkflowTransitionLogRepository(ITenantContext tenantContext)
        : TenantFakeRepository<WorkflowTransitionLog>(tenantContext), IWorkflowTransitionLogRepository
    {
        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default) => Task.FromResult(Add(log));
        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.ApprovalTaskId == approvalTaskId && x.Action == action && x.IdempotencyKey == idempotencyKey));
        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().Where(x => x.WorkflowInstanceId == workflowInstanceId).Select(x => x.SequenceNo).DefaultIfEmpty(0).Max());
        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionLog>>(CurrentTenantItems().Where(x => x.WorkflowInstanceId == workflowInstanceId).ToList());
    }

    private sealed class FakeSlaEscalationRuleRepository(ITenantContext tenantContext)
        : TenantFakeRepository<SlaEscalationRule>(tenantContext), ISlaEscalationRuleRepository
    {
        public Task<SlaEscalationRule> CreateAsync(SlaEscalationRule rule, CancellationToken ct = default) => Task.FromResult(Add(rule));
        public Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Find(id));
        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlaEscalationRule>>(CurrentTenantItems().Where(x => x.TemplateId == templateId && x.IsActive).ToList());
        public Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SlaEscalationRule>>(CurrentTenantItems().Where(x => x.IsActive).ToList());
        public Task<SlaEscalationRule?> FindForStepAsync(Guid templateId, string stageCode, string stepCode, CancellationToken ct = default) =>
            Task.FromResult(CurrentTenantItems().FirstOrDefault(x => x.TemplateId == templateId && x.StageCode == stageCode && x.StepCode == stepCode && x.IsActive));
        public Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default)
        {
            foreach (var rule in Items.Where(x => x.TemplateId == templateId && x.TenantId == TenantContext.TenantId && x.IsActive))
            {
                rule.IsActive = false;
                rule.DeletedAt = DateTimeOffset.UtcNow;
                rule.IsDeleted = true;
                rule.UpdatedAt = DateTimeOffset.UtcNow;
            }
            return Task.CompletedTask;
        }
    }
}
