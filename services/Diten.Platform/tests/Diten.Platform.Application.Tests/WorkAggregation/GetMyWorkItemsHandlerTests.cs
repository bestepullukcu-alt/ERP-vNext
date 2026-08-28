using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// WC-1 (DCP-004) — the read-only aggregation handler over the MOD-0023 provider. Verifies
// actionable-scope + candidate filtering (parity with GetMyWorkflowTasks), the "no state written" invariant
// (write repository methods throw), tenant-scope pass-through, provider extensibility, and the OD-WC-04
// contract-version skip. End-to-end through the REAL provider + REAL projection service.
public sealed class GetMyWorkItemsHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Other = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task Aggregates_only_actionable_tasks_where_user_is_candidate_and_projects_them()
    {
        var instanceId = Guid.NewGuid();
        var instance = Instance(instanceId);

        // Candidate via snapshot, actionable.
        var s1 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Me.ToString(), Other.ToString()]);
        var t1 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s1.Id);
        // Direct assignee, actionable.
        var t2 = MakeTask(instanceId, ApprovalTaskStatus.WaitingEvidence, assigneeRef: Me.ToString());
        // Actionable but not my candidacy → excluded.
        var s3 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Other.ToString()]);
        var t3 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s3.Id);
        // My candidacy but terminal → not in the actionable inbox scope.
        var s4 = Snapshot(instanceId, resolved: Me.ToString(), candidates: [Me.ToString()]);
        var t4 = MakeTask(instanceId, ApprovalTaskStatus.Approved, snapshotId: s4.Id);

        var handler = Handler(
            new FakeApprovalTaskRepository(t1, t2, t3, t4),
            new FakeSnapshotRepository(s1, s3, s4),
            new FakeInstanceRepository(instance));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var ids = response.Data!.Items.Select(x => x.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(t1.Id.ToString(), ids);
        Assert.Contains(t2.Id.ToString(), ids);
        Assert.All(response.Data!.Items, item => Assert.Equal(WorkItemContract.FixtureKindWorkItem, item.FixtureKind));
    }

    [Fact]
    public async Task Writes_no_state_when_projecting()
    {
        // Every write method on these fakes throws; a green run proves the read/projection path performs no write.
        var instanceId = Guid.NewGuid();
        var handler = Handler(
            new FakeApprovalTaskRepository(MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, assigneeRef: Me.ToString())),
            new FakeSnapshotRepository(),
            new FakeInstanceRepository(Instance(instanceId)));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Items);
    }

    [Fact]
    public async Task Only_projects_tenant_scoped_tasks_returned_by_the_repository()
    {
        // The tenant-scoped repository returns only this tenant's task; a cross-tenant task never enters the
        // result because it is never returned by GetAllForTenantAsync (no client-supplied tenant is honored).
        var instanceId = Guid.NewGuid();
        var mine = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, assigneeRef: Me.ToString());

        var handler = Handler(
            new FakeApprovalTaskRepository(mine),
            new FakeSnapshotRepository(),
            new FakeInstanceRepository(Instance(instanceId)));

        var response = await handler.Handle(Query(), CancellationToken.None);

        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(mine.Id.ToString(), item.Id);
    }

    /// <summary>
    /// GUARD (c) — an unsupported contract version is still not PROJECTED, and is no longer SILENT.
    ///
    /// <para>The skip itself is charter OD-WC-04 and unchanged: a version this generation cannot map must not be
    /// mis-projected. What changed is that the source now appears in <c>UnavailableSources</c> instead of leaving
    /// the board looking complete — the small version of the very defect this slice closes.</para>
    /// </summary>
    [Fact]
    public async Task Reports_a_provider_with_an_unsupported_contract_version_instead_of_dropping_it_silently()
    {
        var handler = HandlerOver(new FutureVersionProvider());

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!.Items); // unmapped provider version skipped, not mis-projected
        var missing = Assert.Single(response.Data!.UnavailableSources);
        Assert.Equal("future", missing.ProviderCode);
        Assert.Equal(WorkAggregationUnavailableReasonCodes.UnsupportedVersion, missing.ReasonCode);
    }

    /// <summary>
    /// GUARD (a) — a provider that THROWS costs its own rows and nothing else.
    ///
    /// <para>This is the measured defect from DCP-004 §2 D3: the exception used to leave the handler, so the
    /// reader got an error page instead of the rows the healthy provider already had in hand.</para>
    /// </summary>
    [Fact]
    public async Task A_failing_provider_does_not_take_the_other_providers_items_with_it()
    {
        var healthy = new StubProvider("healthy", ProjectionFor("item-from-healthy"));
        var handler = HandlerOver(new ThrowingProvider("broken"), healthy);

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("item-from-healthy", item.Id);

        var missing = Assert.Single(response.Data!.UnavailableSources);
        Assert.Equal("broken", missing.ProviderCode);
        Assert.Equal(WorkAggregationUnavailableReasonCodes.Error, missing.ReasonCode);
    }

    /// <summary>
    /// GUARD (b) — a provider that never answers costs its own rows and nothing else, reported as TIMEOUT.
    ///
    /// <para>⚠ NO WALL-CLOCK WAIT. The budget is configured at <see cref="TimeSpan.Zero"/> — already spent — and
    /// the handler answers that exactly (it cancels the linked source rather than arming a 0 ms timer), so there
    /// is no race and no sleeping. A test that proved a timeout by waiting for one would be a slow test that
    /// still could not prove the isolation.</para>
    ///
    /// <para>The hanging provider waits on the token it was HANDED, which is the other half of the guard: an
    /// aggregation that passed the raw request token through would leave this provider waiting forever.</para>
    /// </summary>
    [Fact]
    public async Task A_provider_that_never_answers_is_reported_as_a_timeout_and_the_rest_of_the_board_still_arrives()
    {
        var healthy = new StubProvider("healthy", ProjectionFor("item-from-healthy"));
        var handler = HandlerOver(
            new[] { (IWorkItemProvider)new HangingProvider("slow"), healthy },
            budget: TimeSpan.Zero);

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("item-from-healthy", item.Id);

        var missing = Assert.Single(response.Data!.UnavailableSources);
        Assert.Equal("slow", missing.ProviderCode);
        Assert.Equal(WorkAggregationUnavailableReasonCodes.Timeout, missing.ReasonCode);
    }

    /// <summary>
    /// GUARD (d) — when every provider answers, the board says so by saying NOTHING. An empty
    /// <c>UnavailableSources</c> is the state the shell reads as "complete", so it must not be populated
    /// defensively; a banner on a healthy board teaches the reader to ignore the banner.
    /// </summary>
    [Fact]
    public async Task Reports_no_unavailable_source_when_every_provider_answers()
    {
        var handler = HandlerOver(
            new StubProvider("first", ProjectionFor("a")),
            new StubProvider("second", ProjectionFor("b")));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(2, response.Data!.Items.Count);
        Assert.Empty(response.Data!.UnavailableSources);
    }

    /// <summary>
    /// The CALLER's own cancellation is not a provider fault. A reader who navigates away must not produce a
    /// "the tasks source failed" report about a request nobody is waiting for any more.
    /// </summary>
    [Fact]
    public async Task Caller_cancellation_abandons_the_read_instead_of_reporting_a_partial_board()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var handler = HandlerOver(new HangingProvider("slow"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => handler.Handle(Query(), cancelled.Token));
    }

    // --- helpers ---------------------------------------------------------------------------------------

    private static GetMyWorkItemsQuery Query()
        => new(IsPlatformActor: true, new HashSet<string>(), "corr");

    private static GetMyWorkItemsHandler Handler(
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowInstanceRepository instances)
    {
        var provider = new WorkflowApprovalWorkItemProvider(
            tasks, snapshots, instances, new WorkItemProjectionService(Tasks.SlaForTests.Real()));
        return HandlerOver(provider);
    }

    private static GetMyWorkItemsHandler HandlerOver(params IWorkItemProvider[] providers)
        => HandlerOver(providers, budget: TimeSpan.FromSeconds(30));

    // The budget is passed in rather than left to the default, because a timeout that cannot be varied by a test
    // is a timeout no test can prove — which is how "no test covers failure or timeout" was true until now.
    private static GetMyWorkItemsHandler HandlerOver(IWorkItemProvider[] providers, TimeSpan budget)
        => new(
            providers,
            new FakeCurrentUserContext(Me),
            Options.Create(new WorkAggregationResilienceOptions { ProviderTimeout = budget }),
            NullLogger<GetMyWorkItemsHandler>.Instance);

    // A minimal contract-valid projection. Only Id is asserted on; the rest is the shape the DTO requires.
    private static WorkItemProjectionDto ProjectionFor(string id)
        => new(
            FixtureKind: WorkItemContract.FixtureKindWorkItem,
            Id: id,
            WorkIntent: WorkItemContract.IntentApproval,
            AssignmentMode: WorkItemContract.AssignmentApproval,
            OwnershipState: WorkItemContract.NotApplicable,
            AdmissionState: WorkItemContract.NotApplicable,
            NormalizedStatus: WorkItemContract.StatusPending,
            TaskLifecycle: WorkItemContract.NotApplicable,
            ExecutionState: WorkItemContract.NotApplicable,
            TimerState: WorkItemContract.NotApplicable,
            SystemState: WorkItemContract.SystemFresh,
            ActionDepth: WorkItemContract.DepthInline,
            Title: new WorkItemLabelDto(WorkItemContract.LabelResource, "WorkAggregation_Title_Approval"),
            NativeStatus: new WorkItemNativeStatusDto(
                "WaitingApproval",
                new WorkItemLabelDto(WorkItemContract.LabelResource, "WorkAggregation_NativeStatus_WaitingApproval")),
            Source: new WorkItemSourceDto(
                WorkItemContract.ProviderCodeWorkflow, "1.0", "invoice", "INV-1", null),
            LifecycleOwner: WorkItemContract.LifecycleOwnerWorkflow,
            WorkItemCapabilities: [],
            Actions: [],
            Concurrency: new WorkItemConcurrencyDto("version", "1"),
            WaitingContext: null,
            Escalation: null,
            DueAt: null);

    private sealed class StubProvider(string code, params WorkItemProjectionDto[] items) : IWorkItemProvider
    {
        public string ProviderCode => code;
        public string ProviderContractVersion => "1.0";
        public IReadOnlyCollection<string> RequiredActionPermissions => [];
        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItemProjectionDto>>(items.ToList());
    }

    private sealed class ThrowingProvider(string code) : IWorkItemProvider
    {
        public string ProviderCode => code;
        public string ProviderContractVersion => "1.0";
        public IReadOnlyCollection<string> RequiredActionPermissions => [];
        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default)
            => throw new InvalidOperationException("The source this provider reads is down.");
    }

    // Never completes on its own — the ONLY way out is the token it was handed. That is what makes this a test
    // of the handler's per-provider budget rather than a test of a sleep.
    private sealed class HangingProvider(string code) : IWorkItemProvider
    {
        public string ProviderCode => code;
        public string ProviderContractVersion => "1.0";
        public IReadOnlyCollection<string> RequiredActionPermissions => [];
        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default)
            => Task.Delay(Timeout.Infinite, ct).ContinueWith<IReadOnlyList<WorkItemProjectionDto>>(
                _ => [], TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private static ApprovalTask MakeTask(Guid instanceId, ApprovalTaskStatus status, Guid? snapshotId = null, string? assigneeRef = null)
        => new()
        {
            TenantId = Tenant,
            WorkflowInstanceId = instanceId,
            StageCode = "stage-1",
            StepCode = "step-1",
            Status = status,
            AssignmentSnapshotId = snapshotId,
            AssigneeRef = assigneeRef
        };

    private static RuntimeAssignmentSnapshot Snapshot(Guid instanceId, string resolved, List<string> candidates)
        => new()
        {
            TenantId = Tenant,
            WorkflowInstanceId = instanceId,
            ResolverSource = "TEST",
            ResolvedPrincipalId = resolved,
            CandidatePrincipalIds = candidates,
            TieBreakExplanation = "n/a"
        };

    private static WorkflowInstance Instance(Guid id) => new()
    {
        Id = id,
        TenantId = Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = "invoice",
        ObjectId = "INV-1",
        ObjectRef = "finance|invoice|INV-1"
    };

    private sealed class FutureVersionProvider : IWorkItemProvider
    {
        public string ProviderCode => "future";
        public string ProviderContractVersion => "9.9";
        public IReadOnlyCollection<string> RequiredActionPermissions => [];
        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default)
            => throw new InvalidOperationException("An unsupported-version provider must be skipped, never invoked.");
    }

    private sealed class FakeCurrentUserContext(Guid userId) : ICurrentUserContext
    {
        public Guid UserId { get; } = userId;
        public string? Email => "me@diten.local";
        public string? DisplayName => "Me";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeApprovalTaskRepository(params ApprovalTask[] tasks) : IApprovalTaskRepository
    {
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApprovalTask>>(tasks.ToList());

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeSnapshotRepository(params RuntimeAssignmentSnapshot[] snapshots) : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(snapshots.FirstOrDefault(s => s.Id == id));

        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeInstanceRepository(params WorkflowInstance[] instances) : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(instances.FirstOrDefault(i => i.Id == id));

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
