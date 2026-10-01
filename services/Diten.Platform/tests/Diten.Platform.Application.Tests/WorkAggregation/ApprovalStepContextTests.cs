using System.Text.Json;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// REQ-WCN-01 (WP-WCN-APPROVAL-UX-01) — an approval says WHICH STEP it is (W-1), WHO it is waiting on when the step
/// names positions rather than a person (W-3), and whether its confirmation accepts an optional note (W-2).
///
/// <para>All three are OPTIONAL on the wire: an approval whose step says nothing serializes byte for byte as it did
/// before, so no other provider's output and no older client is disturbed.</para>
/// </summary>
public sealed class ApprovalStepContextTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly Guid OtherTenant = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkItemProjectionService _projection = new(Tasks.SlaForTests.Real());

    // ── W-1 — step name ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_step_name_of_the_pinned_version_travels_as_a_display_label()
    {
        var version = Version(Step("step-1", "Finans Onayı", $"user:{Me}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(await Provider(version, [], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.NotNull(item.StepName);
        Assert.Equal(WorkItemContract.LabelDisplay, item.StepName!.Kind);
        Assert.Equal("Finans Onayı", item.StepName.Text);
        Assert.Null(item.StepName.Key);
    }

    [Fact]
    public async Task The_step_name_is_never_added_to_the_title()
    {
        var version = Version(Step("step-1", "Finans Onayı", $"user:{Me}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(await Provider(version, [], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.DoesNotContain("Finans Onayı", JsonSerializer.Serialize(item.Title, WebOptions));
    }

    [Fact]
    public async Task A_step_without_a_name_of_its_own_writes_no_step_name()
    {
        // The runtime plan falls back to the step CODE; a code is not a name and must not reach the badge.
        var version = Version(Step("step-1", null, $"user:{Me}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(await Provider(version, [], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Null(item.StepName);
        Assert.False(Json(item).TryGetProperty("stepName", out _), "an absent step name must be omitted, not null");
    }

    [Fact]
    public async Task An_instance_with_no_pinned_version_keeps_todays_shape()
    {
        var instance = Instance(versionId: null);

        var item = Assert.Single(await Provider(null, [], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Null(item.StepName);
        Assert.Null(item.CandidatePositions);
    }

    // ── W-3 — candidate positions ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Candidate_positions_are_named_when_the_step_names_no_person()
    {
        var finance = Position(Tenant, "Finans Müdürü");
        var legal = Position(Tenant, "Hukuk Müşaviri");
        var version = Version(Step("step-1", "Onay", $"position:{finance.Id}", $"position:{legal.Id}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(
            await Provider(version, [finance, legal], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Equal(["Finans Müdürü", "Hukuk Müşaviri"], item.CandidatePositions!.Select(p => p.Text));
        Assert.All(item.CandidatePositions!, p => Assert.Equal(WorkItemContract.LabelDisplay, p.Kind));
    }

    [Fact]
    public async Task A_position_that_cannot_be_read_is_left_out_and_never_as_its_id()
    {
        var finance = Position(Tenant, "Finans Müdürü");
        var gone = Guid.NewGuid();
        var version = Version(Step("step-1", "Onay", $"position:{finance.Id}", $"position:{gone}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(
            await Provider(version, [finance], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Equal(["Finans Müdürü"], item.CandidatePositions!.Select(p => p.Text));
        Assert.DoesNotContain(gone.ToString(), Json(item).GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(finance.Id.ToString(), Json(item).GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_no_candidate_position_can_be_read_the_field_is_omitted()
    {
        var version = Version(Step("step-1", "Onay", $"position:{Guid.NewGuid()}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(await Provider(version, [], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Null(item.CandidatePositions);
        Assert.False(Json(item).TryGetProperty("candidatePositions", out _));
    }

    [Fact]
    public async Task Another_tenants_position_is_never_named()
    {
        // The repository double hands the foreign row back on purpose — the provider's own tenant lock must hold
        // even when the scoped read fails to.
        var foreign = Position(OtherTenant, "Rakip Şirket CFO");
        var version = Version(Step("step-1", "Onay", $"position:{foreign.Id}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(
            await Provider(version, [foreign], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Null(item.CandidatePositions);
        Assert.DoesNotContain("Rakip Şirket CFO", Json(item).GetRawText());
    }

    [Fact]
    public async Task A_closed_or_archived_position_is_not_named()
    {
        var closed = Position(Tenant, "Kapalı Pozisyon", PositionStatus.Closed);
        var archived = Position(Tenant, "Arşivli Pozisyon");
        archived.IsArchived = true;
        var version = Version(Step("step-1", "Onay", $"position:{closed.Id}", $"position:{archived.Id}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(
            await Provider(version, [closed, archived], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Null(item.CandidatePositions);
    }

    [Fact]
    public async Task A_step_that_names_a_person_directly_carries_no_candidate_positions()
    {
        var finance = Position(Tenant, "Finans Müdürü");
        var version = Version(Step("step-1", "Onay", $"user:{Me}", $"position:{finance.Id}"));
        var instance = Instance(version.Id);

        var item = Assert.Single(
            await Provider(version, [finance], Approval(instance)).With(instance).GetWorkItemsAsync(Actor()));

        Assert.Null(item.CandidatePositions);
    }

    [Fact]
    public async Task A_page_of_approvals_reads_positions_exactly_once()
    {
        var finance = Position(Tenant, "Finans Müdürü");
        var legal = Position(Tenant, "Hukuk Müşaviri");
        var version = Version(
            Step("step-1", "Finans", $"position:{finance.Id}"),
            Step("step-2", "Hukuk", $"position:{legal.Id}"));
        var i1 = Instance(version.Id);
        var i2 = Instance(version.Id);
        var i3 = Instance(version.Id);
        var positions = new CountingPositions(finance, legal);
        var versions = new CountingVersions(version);
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(i1), Approval(i2, "step-2"), Approval(i3)),
            new NoSnapshots(),
            new InstanceStore(i1, i2, i3),
            _projection,
            versions: versions,
            positions: positions);

        var items = await provider.GetWorkItemsAsync(Actor());

        Assert.Equal(3, items.Count);
        Assert.Equal(1, positions.Reads);
        // The pinned version is shared by the page's instances and is read once, not once per item.
        Assert.Equal(1, versions.Reads);
        Assert.Contains(items, i => i.CandidatePositions!.Single().Text == "Hukuk Müşaviri");
    }

    [Fact]
    public async Task A_failing_version_read_costs_the_badge_not_the_approval()
    {
        var instance = Instance(Guid.NewGuid());
        var provider = new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(Approval(instance)),
            new NoSnapshots(),
            new InstanceStore(instance),
            _projection,
            versions: new ThrowingVersions(),
            positions: new CountingPositions());

        var item = Assert.Single(await provider.GetWorkItemsAsync(Actor()));

        Assert.Null(item.StepName);
        Assert.NotEmpty(item.Actions);
    }

    // ── W-2 — the optional note flag ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_accepts_an_optional_note_when_no_comment_is_required()
    {
        var instance = Instance(null);

        var dto = _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0")!;

        var approve = dto.Actions.Single(a => a.Code == "approve");
        Assert.False(approve.RequiresReason);
        Assert.True(approve.AcceptsNote);
        Assert.True(dto.Actions.Single(a => a.Code == "delegate").AcceptsNote);
    }

    [Fact]
    public void A_required_comment_keeps_its_mandatory_window_and_sets_no_note_flag()
    {
        var instance = Instance(null);
        var task = Approval(instance);
        task.CommentRequired = true;

        var dto = _projection.Project(task, instance, Actor(), "workflow", "1.0")!;

        var approve = dto.Actions.Single(a => a.Code == "approve");
        Assert.True(approve.RequiresReason);
        Assert.Null(approve.AcceptsNote);
        // reject / requestInfo always demand a reason — the note flag is never set beside it.
        Assert.All(dto.Actions.Where(a => a.RequiresReason), a => Assert.Null(a.AcceptsNote));
    }

    // ── serialization — absent means absent ───────────────────────────────────────────────────────────────

    [Fact]
    public void Without_a_step_context_the_item_serializes_exactly_as_before()
    {
        var instance = Instance(null);
        var json = JsonSerializer.Serialize(
            _projection.Project(Approval(instance), instance, Actor(), "workflow", "1.0"), WebOptions);

        Assert.DoesNotContain("stepName", json, StringComparison.Ordinal);
        Assert.DoesNotContain("candidatePositions", json, StringComparison.Ordinal);
    }

    [Fact]
    public void An_action_of_a_provider_that_says_nothing_about_notes_omits_the_flag()
    {
        var action = new WorkItemActionDto("start", WorkItemLabelDto.Resource("K"), "start", true,
            WorkItemContract.ActionSourceProvider, null, null, false, false, false, false, "normal");

        Assert.DoesNotContain("acceptsNote", JsonSerializer.Serialize(action, WebOptions), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_new_fields_serialize_in_the_shape_the_executable_contract_reads()
    {
        var finance = Position(Tenant, "Finans Müdürü");
        var version = Version(Step("step-1", "Finans Onayı", $"position:{finance.Id}"));
        var instance = Instance(version.Id);

        var root = Json(Assert.Single(
            await Provider(version, [finance], Approval(instance)).With(instance).GetWorkItemsAsync(Actor())));

        var step = root.GetProperty("stepName");
        Assert.Equal("display", step.GetProperty("kind").GetString());
        Assert.Equal("Finans Onayı", step.GetProperty("text").GetString());
        Assert.False(step.TryGetProperty("key", out _)); // isLabel: a display label must have NO `key` member
        var position = Assert.Single(root.GetProperty("candidatePositions").EnumerateArray());
        Assert.Equal("Finans Müdürü", position.GetProperty("text").GetString());
        Assert.False(position.TryGetProperty("key", out _));
        var approve = root.GetProperty("actions").EnumerateArray().Single(a => a.GetProperty("code").GetString() == "approve");
        Assert.Equal(JsonValueKind.True, approve.GetProperty("acceptsNote").ValueKind);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Json(WorkItemProjectionDto dto)
        => JsonDocument.Parse(JsonSerializer.Serialize(dto, WebOptions)).RootElement.Clone();

    private static WorkItemActor Actor() => new(Me, IsPlatformActor: true, new HashSet<string>());

    private sealed record Builder(WorkflowApprovalWorkItemProvider Provider)
    {
        public WorkflowApprovalWorkItemProvider With(WorkflowInstance _) => Provider;
    }

    // One approval over one instance; the instance store is filled from the approval's own instance.
    private Builder Provider(WorkflowTemplateVersion? version, Position[] positions, ApprovalTask approval)
        => new(new WorkflowApprovalWorkItemProvider(
            new ApprovalStore(approval),
            new NoSnapshots(),
            new InstanceStore(InstancesById[approval.WorkflowInstanceId]),
            _projection,
            versions: new CountingVersions(version is null ? [] : [version]),
            positions: new CountingPositions(positions)));

    private static readonly Dictionary<Guid, WorkflowInstance> InstancesById = new();

    private static WorkflowInstance Instance(Guid? versionId)
    {
        var instance = new WorkflowInstance
        {
            TenantId = Tenant,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            TemplateVersionId = versionId,
            ObjectType = "claim",
            ObjectId = "CLM-1",
            ObjectRef = "crm|claim|CLM-1"
        };
        lock (InstancesById) { InstancesById[instance.Id] = instance; }
        return instance;
    }

    private static ApprovalTask Approval(WorkflowInstance instance, string stepCode = "step-1") => new()
    {
        TenantId = Tenant,
        WorkflowInstanceId = instance.Id,
        StageCode = "stage-1",
        StepCode = stepCode,
        Status = ApprovalTaskStatus.WaitingApproval,
        AssigneeRef = Me.ToString()
    };

    private static object Step(string code, string? name, params string[] candidates)
        => name is null
            ? new { code, type = "approval", assignment = new { candidatePrincipalIds = candidates } }
            : new { code, name, type = "approval", assignment = new { candidatePrincipalIds = candidates } };

    private static WorkflowTemplateVersion Version(params object[] steps) => new()
    {
        TenantId = Tenant,
        TemplateId = Guid.NewGuid(),
        VersionNumber = 1,
        DefinitionJson = JsonSerializer.Serialize(new { stages = new[] { new { code = "stage-1", steps } } }),
        SchemaVersion = "1",
        ExpressionVersion = "1",
        Status = WorkflowTemplateVersionStatus.Published,
        IsImmutable = true
    };

    private static Position Position(Guid tenant, string name, PositionStatus status = PositionStatus.Active) => new()
    {
        TenantId = tenant,
        Code = "P-" + Guid.NewGuid().ToString("N")[..6],
        Name = name,
        OrganizationUnitId = Guid.NewGuid(),
        Status = status
    };

    private sealed class CountingPositions(params Position[] seed) : IPositionRepository
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<Position>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<Position>>(seed.Where(p => ids.Contains(p.Id)).ToList());
        }

        public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<Position>>(seed.ToList());
        }

        public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(seed.FirstOrDefault(p => p.Id == id));
        }

        public Task<Position> CreateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class CountingVersions(params WorkflowTemplateVersion[] seed) : IWorkflowTemplateVersionRepository
    {
        public int Reads { get; private set; }

        public virtual Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(seed.FirstOrDefault(v => v.Id == id));
        }

        public Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion version, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(WorkflowTemplateVersion version, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ThrowingVersions : CountingVersions
    {
        public override Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new InvalidOperationException("version store unavailable");
    }

    private sealed class ApprovalStore(params ApprovalTask[] tasks) : IApprovalTaskRepository
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

    private sealed class NoSnapshots : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RuntimeAssignmentSnapshot?>(null);

        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class InstanceStore(params WorkflowInstance[] instances) : IWorkflowInstanceRepository
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
