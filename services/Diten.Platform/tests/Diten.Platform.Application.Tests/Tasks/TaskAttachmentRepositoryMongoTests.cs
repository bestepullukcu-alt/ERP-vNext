using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 Slice ATT-1 — the attachment repository against a REAL MongoDB.
///
/// <para>Every other attachment test drives <c>FakeTaskAttachmentRepository</c>, and that double filters
/// <c>!IsDeleted</c> in its own LINQ. So the suite proves the HANDLERS honour soft deletion while proving nothing
/// about the query that actually runs in production: the three reads compose <c>ExecutionFilter</c> from
/// <c>TenantRepository&lt;T&gt;</c>, and if that composition were ever dropped from one of them, a removed piece of
/// evidence would keep satisfying <c>ChecklistRunItem.EvidenceRequired</c> and a deleted file would keep showing in
/// the task's list — with every existing test still green. Measured by the Control Tower, 2026-09-12.</para>
/// </summary>
public sealed class TaskAttachmentRepositoryMongoTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private static readonly Guid TaskId = Guid.NewGuid();
    private const string ItemCode = "CHK-1";

    private MongoIntegrationHarness _harness = null!;
    private TaskAttachmentRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
            "task_attachments", SchemaProfile.WorkflowWorkCenter);
        _repository = new TaskAttachmentRepository(_harness.DbContext, new FakeTenantContext(TenantId));
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private IMongoCollection<TaskAttachment> Collection =>
        _harness.Database.GetCollection<TaskAttachment>(PlatformCollections.TaskAttachments);

    private static TaskAttachment Row(
        Guid tenantId, TaskAttachmentKind kind = TaskAttachmentKind.Evidence, string? itemCode = ItemCode) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        TaskId = TaskId,
        ChecklistRunItemCode = itemCode,
        Kind = kind,
        ContentId = Guid.NewGuid(),
        FileName = "evidence.pdf",
        MediaType = "application/pdf",
        ByteSize = 1024,
        UploadedByUserId = Guid.NewGuid(),
        UploadedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task A_soft_deleted_attachment_leaves_the_task_list_and_stops_satisfying_the_evidence_gate()
    {
        var kept = Row(TenantId);
        var removed = Row(TenantId);
        await Collection.InsertManyAsync([kept, removed]);

        Assert.Equal(2, (await _repository.ListByTaskIdAsync(TaskId)).Count);
        Assert.Equal(2, await _repository.CountEvidenceForChecklistItemAsync(TaskId, ItemCode));

        Assert.True(await _repository.SoftDeleteAsync(removed.Id, "ct"));

        var listed = await _repository.ListByTaskIdAsync(TaskId);
        Assert.Equal(kept.Id, Assert.Single(listed).Id);
        Assert.Equal(1, await _repository.CountEvidenceForChecklistItemAsync(TaskId, ItemCode));

        // The row is still THERE — soft, not gone: the audit trail of what was once cited must survive.
        Assert.NotNull(await Collection.Find(Builders<TaskAttachment>.Filter.Eq(x => x.Id, removed.Id)).FirstOrDefaultAsync());
    }

    [Fact]
    public async Task Another_tenants_attachment_is_invisible_to_every_read()
    {
        var mine = Row(TenantId);
        var theirs = Row(OtherTenantId);
        await Collection.InsertManyAsync([mine, theirs]);

        Assert.Equal(mine.Id, Assert.Single(await _repository.ListByTaskIdAsync(TaskId)).Id);
        Assert.Equal(1, await _repository.CountEvidenceForChecklistItemAsync(TaskId, ItemCode));
        Assert.Single(await _repository.ListByTaskIdsAsync([TaskId]));
        Assert.Null(await _repository.GetByIdAsync(theirs.Id));
        // And it cannot be removed across the boundary either.
        Assert.False(await _repository.SoftDeleteAsync(theirs.Id, "ct"));
    }

    [Fact]
    public async Task A_non_evidence_kind_is_not_counted_by_the_gate()
    {
        await Collection.InsertManyAsync(
        [
            Row(TenantId, TaskAttachmentKind.Attachment),
            Row(TenantId, TaskAttachmentKind.Deliverable),
            Row(TenantId, TaskAttachmentKind.Evidence, itemCode: "CHK-OTHER")
        ]);

        Assert.Equal(0, await _repository.CountEvidenceForChecklistItemAsync(TaskId, ItemCode));
    }

    /// <summary>
    /// CT 2026-09-13 — TaskType.RequiresDeliverableOnCompletion gates "complete" on CountDeliverablesAsync, and every
    /// gate test drove the fake repository, which filters in its own LINQ. This runs the production query: only this
    /// task's live Deliverable rows in this tenant count — not Evidence, not a plain Attachment, not a removed file,
    /// not another task's deliverable, not another tenant's.
    /// </summary>
    [Fact]
    public async Task The_deliverable_gate_counts_only_live_deliverables_of_this_task_in_this_tenant()
    {
        var live = Row(TenantId, TaskAttachmentKind.Deliverable, itemCode: null);
        var removed = Row(TenantId, TaskAttachmentKind.Deliverable, itemCode: null);
        var otherTenant = Row(OtherTenantId, TaskAttachmentKind.Deliverable, itemCode: null);
        var otherTask = Row(TenantId, TaskAttachmentKind.Deliverable, itemCode: null);
        otherTask.TaskId = Guid.NewGuid();
        var evidence = Row(TenantId, TaskAttachmentKind.Evidence);
        var plain = Row(TenantId, TaskAttachmentKind.Attachment, itemCode: null);
        await Collection.InsertManyAsync([live, removed, otherTenant, otherTask, evidence, plain]);

        Assert.True(await _repository.SoftDeleteAsync(removed.Id, "ct"));

        Assert.Equal(1, await _repository.CountDeliverablesAsync(TaskId));
    }

    [Fact]
    public async Task The_two_declared_indexes_exist_on_the_collection()
    {
        var names = (await (await Collection.Indexes.ListAsync()).ToListAsync())
            .Select(i => i["name"].AsString)
            .ToList();

        Assert.Contains("ix_task_attachments_tenant_task", names);
        Assert.Contains("ix_task_attachments_tenant_task_checklist_item", names);
    }
}
