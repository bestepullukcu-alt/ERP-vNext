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

public sealed class WorkflowInstanceStartTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Correlation = "wf-start-corr-001";

    [Fact]
    public async Task Published_template_start_creates_instance_task_snapshot_and_start_log()
    {
        var fixture = Fixture(TenantA);
        var (template, version) = await fixture.AddPublishedTemplateAsync("WF-START");

        var response = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(Correlation, response.CorrelationId);
        Assert.Equal(Correlation, response.Data!.CorrelationId);
        Assert.Equal(template.Id, response.Data.TemplateId);
        Assert.Equal(version.Id, response.Data.TemplateVersionId);
        Assert.Equal("Active", response.Data.Status);
        Assert.Equal("stage-1", response.Data.CurrentStage);
        Assert.Equal("step-1", response.Data.CurrentStep);

        var instance = Assert.Single(fixture.Instances.Items);
        Assert.Equal(version.Id, instance.TemplateVersionId);
        Assert.Equal(WorkflowInstanceStatus.Active, instance.Status);

        var task = Assert.Single(fixture.Tasks.Items);
        Assert.Equal(instance.Id, task.WorkflowInstanceId);
        Assert.Equal("stage-1", task.StageCode);
        Assert.Equal("step-1", task.StepCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);

        var snapshot = Assert.Single(fixture.Snapshots.Items);
        Assert.Equal(task.Id, snapshot.ApprovalTaskId);
        Assert.Equal(snapshot.Id, task.AssignmentSnapshotId);

        var log = Assert.Single(fixture.Logs.Items);
        Assert.Equal(WorkflowTransitionAction.Start, log.Action);
        Assert.Equal(instance.Id, log.WorkflowInstanceId);
        Assert.Equal(task.Id, log.ApprovalTaskId);
        Assert.Equal(1, log.SequenceNo);

        var reload = await fixture.Instances.GetByIdAsync(instance.Id);
        Assert.Equal(instance.Id, reload!.Id);
    }

    [Fact]
    public async Task Missing_template_start_returns_not_found_non_leakage()
    {
        var fixture = Fixture(TenantA);

        var response = await fixture.Handler.Handle(Start(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Unpublished_active_version_blocks_start()
    {
        var fixture = Fixture(TenantA);
        var (template, version) = await fixture.AddPublishedTemplateAsync("WF-DRAFT-VERSION");
        version.Status = WorkflowTemplateVersionStatus.Draft;
        version.IsImmutable = false;

        var response = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTemplateNotPublished, response.ReasonCode);
    }

    [Fact]
    public async Task Template_without_active_version_blocks_start()
    {
        var fixture = Fixture(TenantA);
        var template = await fixture.Templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = fixture.TenantContext.TenantId,
            TemplateCode = "WF-NO-ACTIVE",
            Name = "No Active",
            Status = WorkflowTemplateStatus.Published
        });

        var response = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTemplateNoActiveVersion, response.ReasonCode);
    }

    [Fact]
    public async Task Active_published_version_missing_blocks_start()
    {
        var fixture = Fixture(TenantA);
        var template = await fixture.Templates.CreateAsync(new WorkflowTemplate
        {
            TenantId = fixture.TenantContext.TenantId,
            TemplateCode = "WF-MISSING-VERSION",
            Name = "Missing Version",
            Status = WorkflowTemplateStatus.Published,
            ActivePublishedVersionId = Guid.NewGuid()
        });

        var response = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTemplateVersionNotFound, response.ReasonCode);
    }

    [Fact]
    public async Task Cross_tenant_start_is_blocked()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-CROSS");

        fixture.TenantContext.SetTenant(TenantB);
        var response = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Empty(fixture.Instances.Items);
    }

    [Fact]
    public async Task Empty_candidate_principals_are_validation_failed()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-NO-CANDIDATE");

        var response = await fixture.Handler.Handle(
            Start(template.Id, candidates: []),
            CancellationToken.None);
        var validator = new StartWorkflowInstanceValidator();
        var validation = validator.Validate(Start(template.Id, candidates: []));

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowAssignmentCandidatesRequired, response.ReasonCode);
        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task Multiple_candidate_principals_use_lexicographic_first()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-MULTI-CANDIDATE");

        await fixture.Handler.Handle(
            Start(template.Id, candidates: ["user-c", "user-a", "user-b"]),
            CancellationToken.None);

        var snapshot = Assert.Single(fixture.Snapshots.Items);
        Assert.Equal("user-a", snapshot.ResolvedPrincipalId);
        Assert.Equal("lexicographic_first_principal", snapshot.TieBreakExplanation);
    }

    [Fact]
    public async Task Duplicate_candidate_principals_are_normalized()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-DUP-CANDIDATE");

        await fixture.Handler.Handle(
            Start(template.Id, candidates: [" user-b ", "user-a", "user-a", "user-b"]),
            CancellationToken.None);

        var snapshot = Assert.Single(fixture.Snapshots.Items);
        Assert.Equal(["user-a", "user-b"], snapshot.CandidatePrincipalIds);
        Assert.Equal("user-a", snapshot.ResolvedPrincipalId);
    }

    [Fact]
    public async Task Duplicate_idempotency_key_returns_existing_start_without_duplicates()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-IDEMPOTENT");

        var first = await fixture.Handler.Handle(Start(template.Id, idempotencyKey: "idem-001"), CancellationToken.None);
        var second = await fixture.Handler.Handle(Start(template.Id, idempotencyKey: "idem-001"), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.True(second.IsSuccessful);
        Assert.Equal(first.Data!.WorkflowInstanceId, second.Data!.WorkflowInstanceId);
        Assert.Single(fixture.Instances.Items);
        Assert.Single(fixture.Tasks.Items);
        Assert.Single(fixture.Snapshots.Items);
        Assert.Single(fixture.Logs.Items);
    }

    [Fact]
    public async Task Get_instance_by_id_same_tenant_returns_data()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-GET");
        var started = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        var response = await new GetWorkflowInstanceByIdHandler(fixture.Instances)
            .Handle(new GetWorkflowInstanceByIdQuery(started.Data!.WorkflowInstanceId, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(started.Data.WorkflowInstanceId, response.Data!.Id);
    }

    [Fact]
    public async Task Get_instance_by_id_cross_tenant_returns_not_found_non_leakage()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-GET-CROSS");
        var started = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        fixture.TenantContext.SetTenant(TenantB);
        var response = await new GetWorkflowInstanceByIdHandler(fixture.Instances)
            .Handle(new GetWorkflowInstanceByIdQuery(started.Data!.WorkflowInstanceId, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Get_tasks_list_returns_only_current_tenant_records()
    {
        var fixture = Fixture(TenantA);
        var (templateA, _) = await fixture.AddPublishedTemplateAsync("WF-TASK-A");
        await fixture.Handler.Handle(Start(templateA.Id), CancellationToken.None);

        fixture.TenantContext.SetTenant(TenantB);
        var (templateB, _) = await fixture.AddPublishedTemplateAsync("WF-TASK-B");
        await fixture.Handler.Handle(Start(templateB.Id), CancellationToken.None);

        var tenantBTasks = await new GetWorkflowTaskListHandler(fixture.Tasks)
            .Handle(new GetWorkflowTaskListQuery(Correlation), CancellationToken.None);

        Assert.True(tenantBTasks.IsSuccessful);
        Assert.Single(tenantBTasks.Data!);
        Assert.Equal(TenantB, fixture.Tasks.Items.Single(x => x.Id == tenantBTasks.Data![0].Id).TenantId);
    }

    [Fact]
    public async Task Start_response_carries_correlation_id()
    {
        var fixture = Fixture(TenantA);
        var (template, _) = await fixture.AddPublishedTemplateAsync("WF-CORR");

        var response = await fixture.Handler.Handle(Start(template.Id), CancellationToken.None);

        Assert.Equal(Correlation, response.CorrelationId);
        Assert.Equal(Correlation, response.Data!.CorrelationId);
    }

    private static StartWorkflowInstanceCommand Start(
        Guid templateId,
        IReadOnlyList<string>? candidates = null,
        string? idempotencyKey = null) =>
        new(new StartWorkflowInstanceRequest(
            templateId,
            null,
            "PurchaseOrder",
            "PO-100",
            "Purchasing|PurchaseOrder|PO-100",
            candidates ?? ["user-approver"],
            "SUBMIT_FOR_APPROVAL",
            idempotencyKey,
            CommentRequired: true,
            EvidenceRequired: false,
            DueAt: DateTimeOffset.UtcNow.AddDays(2)), Correlation);

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
        var handler = new StartWorkflowInstanceHandler(
            templates,
            versions,
            instances,
            tasks,
            snapshots,
            logs,
            tenantContext,
            new FakeCurrentUserContext());
        return new TestFixture(tenantContext, templates, versions, instances, tasks, snapshots, logs, handler);
    }

    private sealed record TestFixture(
        TenantContext TenantContext,
        FakeWorkflowTemplateRepository Templates,
        FakeWorkflowTemplateVersionRepository Versions,
        FakeWorkflowInstanceRepository Instances,
        FakeApprovalTaskRepository Tasks,
        FakeRuntimeAssignmentSnapshotRepository Snapshots,
        FakeWorkflowTransitionLogRepository Logs,
        StartWorkflowInstanceHandler Handler)
    {
        public async Task<(WorkflowTemplate Template, WorkflowTemplateVersion Version)> AddPublishedTemplateAsync(string code)
        {
            var template = await Templates.CreateAsync(new WorkflowTemplate
            {
                TenantId = TenantContext.TenantId,
                TemplateCode = code,
                Name = code,
                Status = WorkflowTemplateStatus.Published
            });
            var version = await Versions.CreateAsync(new WorkflowTemplateVersion
            {
                TenantId = TenantContext.TenantId,
                TemplateId = template.Id,
                VersionNumber = 1,
                DefinitionJson = "{}",
                SchemaVersion = "1.0",
                ExpressionVersion = "1.0",
                Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true,
                PublishedAt = DateTime.UtcNow,
                PublishedBy = "publisher"
            });
            template.ActivePublishedVersionId = version.Id;
            template.CurrentVersionId = version.Id;
            return (template, version);
        }
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "starter@diten.local";
        public string? DisplayName => "Starter";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeWorkflowTemplateRepository : IWorkflowTemplateRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTemplate> Items { get; } = [];

        public FakeWorkflowTemplateRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default)
        {
            typeof(WorkflowTemplate).GetProperty(nameof(WorkflowTemplate.TenantId))!.SetValue(template, _tenantContext.TenantId);
            Items.Add(template);
            return Task.FromResult(template);
        }

        public Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.TemplateCode == templateCode && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTemplate>>(Items.Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());

        public Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeWorkflowTemplateVersionRepository : IWorkflowTemplateVersionRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTemplateVersion> Items { get; } = [];

        public FakeWorkflowTemplateVersionRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion version, CancellationToken ct = default)
        {
            typeof(WorkflowTemplateVersion).GetProperty(nameof(WorkflowTemplateVersion.TenantId))!.SetValue(version, _tenantContext.TenantId);
            Items.Add(version);
            return Task.FromResult(version);
        }

        public Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.TemplateId == templateId && x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).OrderByDescending(x => x.VersionNumber).FirstOrDefault());

        public async Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default) =>
            (await GetLatestVersionAsync(templateId, ct))?.VersionNumber ?? 0;

        public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && x.Status == WorkflowTemplateVersionStatus.Published && x.IsImmutable && !x.IsDeleted));

        public Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(x => x.TemplateId == templateId && x.VersionNumber == versionNumber && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTemplateVersion>>(Items.Where(x => x.TemplateId == templateId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());

        public Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(WorkflowTemplateVersion version, int expectedVersion, CancellationToken ct = default) =>
            Task.FromResult(WorkflowTemplateVersionUpdateResult.Updated);
    }

    private sealed class FakeWorkflowInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowInstance> Items { get; } = [];

        public FakeWorkflowInstanceRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default)
        {
            typeof(WorkflowInstance).GetProperty(nameof(WorkflowInstance.TenantId))!.SetValue(instance, _tenantContext.TenantId);
            Items.Add(instance);
            return Task.FromResult(instance);
        }

        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(
            string objectRef,
            string objectType,
            string objectId,
            CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x =>
                    x.ObjectRef == objectRef &&
                    x.ObjectType == objectType &&
                    x.ObjectId == objectId &&
                    x.TenantId == _tenantContext.TenantId &&
                    !x.IsDeleted)
                .OrderByDescending(x => x.StartedAt)
                .ThenByDescending(x => x.CreatedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowInstance>>(Items.Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());

        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x => x.Id == instance.Id && x.TenantId == _tenantContext.TenantId && x.Version == expectedVersion && !x.IsDeleted);
            if (stored is null)
            {
                return Task.FromResult(false);
            }

            instance.Version = expectedVersion + 1;
            Items[Items.IndexOf(stored)] = instance;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeApprovalTaskRepository : IApprovalTaskRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<ApprovalTask> Items { get; } = [];

        public FakeApprovalTaskRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default)
        {
            typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.TenantId))!.SetValue(task, _tenantContext.TenantId);
            Items.Add(task);
            return Task.FromResult(task);
        }

        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x =>
                    x.WorkflowInstanceId == workflowInstanceId &&
                    x.TenantId == _tenantContext.TenantId &&
                    !x.IsDeleted &&
                    x.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ApprovalTask>>(Items.Where(x => x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());

        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
        {
            var stored = Items.FirstOrDefault(x => x.Id == task.Id && x.TenantId == _tenantContext.TenantId && x.Version == expectedVersion && !x.IsDeleted);
            if (stored is null)
            {
                return Task.FromResult(false);
            }

            task.Version = expectedVersion + 1;
            Items[Items.IndexOf(stored)] = task;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeRuntimeAssignmentSnapshotRepository : IRuntimeAssignmentSnapshotRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<RuntimeAssignmentSnapshot> Items { get; } = [];

        public FakeRuntimeAssignmentSnapshotRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default)
        {
            typeof(RuntimeAssignmentSnapshot).GetProperty(nameof(RuntimeAssignmentSnapshot.TenantId))!.SetValue(snapshot, _tenantContext.TenantId);
            Items.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RuntimeAssignmentSnapshot>>(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());
    }

    private sealed class FakeWorkflowTransitionLogRepository : IWorkflowTransitionLogRepository
    {
        private readonly ITenantContext _tenantContext;
        public List<WorkflowTransitionLog> Items { get; } = [];

        public FakeWorkflowTransitionLogRepository(ITenantContext tenantContext) => _tenantContext = tenantContext;

        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default)
        {
            typeof(WorkflowTransitionLog).GetProperty(nameof(WorkflowTransitionLog.TenantId))!.SetValue(log, _tenantContext.TenantId);
            Items.Add(log);
            return Task.FromResult(log);
        }

        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantContext.TenantId && !x.IsDeleted));

        public Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WorkflowTransitionLog>>(Items.Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted).ToList());

        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
            Guid approvalTaskId,
            WorkflowTransitionAction action,
            string idempotencyKey,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.ApprovalTaskId == approvalTaskId &&
                x.Action == action &&
                x.IdempotencyKey == idempotencyKey &&
                x.TenantId == _tenantContext.TenantId &&
                !x.IsDeleted));

        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(x => x.WorkflowInstanceId == workflowInstanceId && x.TenantId == _tenantContext.TenantId && !x.IsDeleted)
                .Select(x => x.SequenceNo)
                .DefaultIfEmpty(0)
                .Max());
    }
}
