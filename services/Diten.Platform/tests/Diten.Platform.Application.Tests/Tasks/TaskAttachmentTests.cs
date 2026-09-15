using System.Reflection;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Attachments;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 Slice ATT-1 — task attachments: upload/list/download/delete, holder/requester authorization, the
/// closed-task gate, the checklist evidence gate, and the "ObjectKey never leaves the boundary" guarantee.
/// Uses the REAL local-filesystem storage gateway against a temp root (same reasoning as
/// <c>DocumentBinaryStoreTests</c>): the defects that matter here are invisible to a fake gateway.
/// </summary>
public sealed class TaskAttachmentTests : IDisposable
{
    // FakeTaskItemRepository.GetByIdAsync filters on TaskTestData.Tenant, hardcoded — every task built here must
    // carry that exact tenant id, not one of this file's own choosing, or every lookup 404s before anything else
    // in the handler runs.
    private static readonly Guid TenantId = TaskTestData.Tenant;
    private static readonly Guid Holder = TaskTestData.Me;
    private static readonly Guid Requester = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");
    private static readonly Guid Stranger = Guid.Parse("cccccccc-1111-2222-3333-444444444444");
    private const string Corr = "att1-corr";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "DitenAttachmentTests", Guid.NewGuid().ToString("N"));

    private sealed class FakeRepositoryObjectRepository : IRepositoryObjectRepository
    {
        public List<RepositoryObject> Items { get; } = [];
        public List<Guid> Compensated { get; } = [];

        public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
        {
            Items.Add(repositoryObject);
            return Task.FromResult(repositoryObject);
        }

        public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ContentId == contentId));

        public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryObject>>(Items.Where(x => x.OwningItemId == owningItemId).ToList());

        public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
        {
            Compensated.Add(contentId);
            return Task.FromResult(true);
        }
    }

    private sealed class Harness
    {
        public required FakeTaskItemRepository Tasks { get; init; }
        public required FakeChecklistRunRepository ChecklistRuns { get; init; }
        public required FakeTaskAttachmentRepository Attachments { get; init; }
        public required DocumentRepositoryService DocumentRepository { get; init; }
        public required AddTaskAttachmentHandler Add { get; init; }
        public required RemoveTaskAttachmentHandler Remove { get; init; }
        public required ListTaskAttachmentsHandler List { get; init; }
        public required OpenTaskAttachmentHandler Open { get; init; }
        public required SetChecklistItemStateHandler SetChecklistState { get; init; }
    }

    /// <summary>BL-349 — a resolver that has nothing to say, for a suite about attachment mechanics rather than
    /// the scope leg (that leg is TaskReadAccessPolicyTests's own territory).</summary>
    private sealed class NoScopeResolver : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }

    private Harness Build(TaskItem task, ChecklistRun? run = null, Guid? actor = null)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var currentUser = new FakeCurrentUserContext(actor ?? Holder);

        var gateway = new LocalFileSystemContentStorageGateway(
            Options.Create(new ContentStorageOptions
            {
                RootPath = _root,
                MaxFileSizeBytes = 1_048_576,
                AllowedExtensions = [".pdf", ".xlsx", ".txt"],
                AllowedMediaTypes = []
            }),
            NullLogger<LocalFileSystemContentStorageGateway>.Instance);
        var objects = new FakeRepositoryObjectRepository();
        var documentRepository = new DocumentRepositoryService(
            gateway, objects, tenant, currentUser, NullLogger<DocumentRepositoryService>.Instance);

        var tasksRepo = new FakeTaskItemRepository(task);
        var runs = run is null ? new FakeChecklistRunRepository() : new FakeChecklistRunRepository(run);
        var attachments = new FakeTaskAttachmentRepository();

        // BL-349 — the REAL policy, over fakes with nothing granted beyond the task's own fields: every test in
        // this file acts as the task's assignee (Holder, the default), which the assignee leg admits on its own.
        // Non-relationship access is TaskReadAccessPolicyTests's own territory, not re-proven here.
        var readAccess = new TaskReadAccessPolicy(
            tasksRepo, new FakeTaskWatcherRepository(), new FakeTaskNotificationService(),
            new FakeOrganizationUnitRepository(), new NoScopeResolver(), new FakeTaskTeamResolver(), TaskActors.None(), currentUser);

        return new Harness
        {
            Tasks = tasksRepo,
            ChecklistRuns = runs,
            Attachments = attachments,
            DocumentRepository = documentRepository,
            Add = new AddTaskAttachmentHandler(tasksRepo, runs, attachments, documentRepository, tenant, currentUser),
            Remove = new RemoveTaskAttachmentHandler(tasksRepo, attachments, currentUser),
            List = new ListTaskAttachmentsHandler(tasksRepo, attachments, readAccess, currentUser),
            Open = new OpenTaskAttachmentHandler(tasksRepo, attachments, documentRepository, readAccess, currentUser),
            SetChecklistState = new SetChecklistItemStateHandler(
                tasksRepo, runs, new TaskChecklistService(), currentUser, attachments)
        };
    }

    private static TaskItem NewTask(Guid? assignee = null, Guid? createdBy = null, TaskLifecycle lifecycle = TaskLifecycle.Open) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantId,
        Title = "Attach me",
        Lifecycle = lifecycle,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = assignee ?? Holder,
        CreatedByUserId = createdBy ?? Requester,
        OrganizationUnitId = Guid.NewGuid(),
        CreatedBy = "tester"
    };

    private static AddTaskAttachmentCommand UploadCmd(
        Guid taskId, string fileName = "report.pdf", TaskAttachmentKind kind = TaskAttachmentKind.Attachment,
        string? checklistItemCode = null, string? note = null, string content = "hello") =>
        new(taskId, new MemoryStream(Encoding.UTF8.GetBytes(content)), fileName, "application/pdf",
            kind, checklistItemCode, note, Corr);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) { Directory.Delete(_root, recursive: true); } } catch { /* best effort */ }
    }

    // ── AC1: upload, list, download, delete round trip ────────────────────

    [Fact]
    public async Task A_pdf_is_uploaded_listed_downloaded_by_name_and_soft_deleted()
    {
        var task = NewTask();
        var h = Build(task);

        var uploaded = await h.Add.Handle(UploadCmd(task.Id, "report.pdf", content: "PDF-BYTES"), CancellationToken.None);
        Assert.True(uploaded.IsSuccessful);
        Assert.Equal(201, uploaded.StatusCode);
        var attachmentId = uploaded.Data!.Id;

        var listed = await h.List.Handle(new ListTaskAttachmentsQuery(task.Id, Corr), CancellationToken.None);
        Assert.Single(listed.Data!);
        Assert.Equal("report.pdf", listed.Data!.Single().FileName);

        var opened = await h.Open.Handle(new OpenTaskAttachmentQuery(task.Id, attachmentId, Corr), CancellationToken.None);
        Assert.True(opened.IsSuccessful);
        Assert.Equal("report.pdf", opened.Data!.FileName);
        using var reader = new StreamReader(opened.Data.Content);
        Assert.Equal("PDF-BYTES", await reader.ReadToEndAsync());

        var removed = await h.Remove.Handle(new RemoveTaskAttachmentCommand(task.Id, attachmentId, Corr), CancellationToken.None);
        Assert.True(removed.IsSuccessful);

        var listedAfter = await h.List.Handle(new ListTaskAttachmentsQuery(task.Id, Corr), CancellationToken.None);
        Assert.Empty(listedAfter.Data!);

        // The metadata row is gone from every normal read (the same soft-delete convention every tenant-scoped
        // repository in this codebase follows) — a deleted attachment 404s through the task's own API, same as
        // any other soft-deleted row. AD-6 is about the underlying FILE, not this row: nothing on the remove path
        // ever calls TryDeleteAsync/CompensateAsync, so the object on disk is untouched — verified directly
        // against the filesystem below, not through this API (which has no reason to serve a deleted attachment).
        var afterDelete = await h.Open.Handle(new OpenTaskAttachmentQuery(task.Id, attachmentId, Corr), CancellationToken.None);
        Assert.False(afterDelete.IsSuccessful);
        Assert.Equal(404, afterDelete.StatusCode);

        // MUTATION GUARD (AD-6): the physical file on disk is untouched by a remove — a purge implementation
        // would fail this by deleting it.
        var objectPath = Directory.GetFiles(_root, "report.pdf", SearchOption.AllDirectories);
        Assert.Single(objectPath);
    }

    [Fact]
    public async Task An_xlsx_is_uploaded_and_downloaded_with_its_file_name_preserved()
    {
        var task = NewTask();
        var h = Build(task);

        var uploaded = await h.Add.Handle(
            UploadCmd(task.Id, "workbook.xlsx", content: "XLSX-BYTES"), CancellationToken.None);
        Assert.True(uploaded.IsSuccessful);

        var opened = await h.Open.Handle(
            new OpenTaskAttachmentQuery(task.Id, uploaded.Data!.Id, Corr), CancellationToken.None);
        Assert.Equal("workbook.xlsx", opened.Data!.FileName);
    }

    // ── AC2: checklist evidence enforcement ────────────────────────────────

    [Fact]
    public async Task An_evidence_required_item_cannot_be_completed_without_evidence()
    {
        var task = NewTask();
        var run = new ChecklistRun
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TaskItemId = task.Id, Version = 1,
            Items = [new ChecklistRunItem { Code = "C1", EvidenceRequired = true, Requirement = ChecklistItemRequirement.Blocking }]
        };
        var h = Build(task, run);

        var result = await h.SetChecklistState.Handle(
            new SetChecklistItemStateCommand(
                task.Id, new SetChecklistItemStateRequest("C1", true, 1), Corr),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistEvidenceRequired, result.ReasonCode);
    }

    [Fact]
    public async Task Adding_evidence_then_completing_the_item_succeeds()
    {
        var task = NewTask();
        var run = new ChecklistRun
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TaskItemId = task.Id, Version = 1,
            Items = [new ChecklistRunItem { Code = "C1", EvidenceRequired = true, Requirement = ChecklistItemRequirement.Blocking }]
        };
        var h = Build(task, run);

        var uploaded = await h.Add.Handle(
            UploadCmd(task.Id, "evidence.pdf", TaskAttachmentKind.Evidence, checklistItemCode: "C1"),
            CancellationToken.None);
        Assert.True(uploaded.IsSuccessful);

        var result = await h.SetChecklistState.Handle(
            new SetChecklistItemStateCommand(
                task.Id, new SetChecklistItemStateRequest("C1", true, 1), Corr),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task A_non_evidence_kind_attachment_does_not_satisfy_the_evidence_gate()
    {
        /*
         * MUTATION GUARD: match on ChecklistRunItemCode alone (ignore Kind) and this goes red — a Deliverable
         * filed against the same item would wrongly satisfy an Evidence-only gate.
         */
        var task = NewTask();
        var run = new ChecklistRun
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TaskItemId = task.Id, Version = 1,
            Items = [new ChecklistRunItem { Code = "C1", EvidenceRequired = true, Requirement = ChecklistItemRequirement.Blocking }]
        };
        var h = Build(task, run);

        await h.Add.Handle(
            UploadCmd(task.Id, "deliverable.pdf", TaskAttachmentKind.Deliverable, checklistItemCode: "C1"),
            CancellationToken.None);

        var result = await h.SetChecklistState.Handle(
            new SetChecklistItemStateCommand(
                task.Id, new SetChecklistItemStateRequest("C1", true, 1), Corr),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ChecklistEvidenceRequired, result.ReasonCode);
    }

    // ── AC3: authorization, closed task, cross-tenant/cross-task 404 ──────

    [Fact]
    public async Task A_stranger_is_refused_403()
    {
        var task = NewTask();
        var h = Build(task, actor: Stranger);

        var result = await h.Add.Handle(UploadCmd(task.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(TaskReasonCodes.AttachmentNotAuthorized, result.ReasonCode);
    }

    [Fact]
    public async Task The_requester_may_upload_even_when_not_the_holder()
    {
        var task = NewTask();
        var h = Build(task, actor: Requester);

        var result = await h.Add.Handle(UploadCmd(task.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task A_closed_task_refuses_a_new_attachment_409()
    {
        var task = NewTask(lifecycle: TaskLifecycle.Done);
        var h = Build(task);

        var result = await h.Add.Handle(UploadCmd(task.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.AttachmentTaskClosed, result.ReasonCode);
    }

    [Fact]
    public async Task A_closed_task_also_refuses_removal_409()
    {
        var task = NewTask();
        var h = Build(task);
        var uploaded = await h.Add.Handle(UploadCmd(task.Id), CancellationToken.None);

        task.Lifecycle = TaskLifecycle.Done; // close it after the upload

        var result = await h.Remove.Handle(
            new RemoveTaskAttachmentCommand(task.Id, uploaded.Data!.Id, Corr), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Anothers_tasks_attachment_id_resolves_to_404_not_403()
    {
        /*
         * Non-leakage (MOD-0262-FU01's own rule, applied here): an attachment id from a DIFFERENT task the same
         * actor owns must not resolve through the wrong task id — 404, exactly like a genuinely missing id.
         */
        var taskA = NewTask();
        var taskB = NewTask();
        var h = Build(taskA);
        h.Tasks.CreateAsync(taskB, CancellationToken.None).GetAwaiter().GetResult();

        var uploadedOnA = await h.Add.Handle(UploadCmd(taskA.Id), CancellationToken.None);

        var result = await h.Open.Handle(
            new OpenTaskAttachmentQuery(taskB.Id, uploadedOnA.Data!.Id, Corr), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(TaskReasonCodes.AttachmentNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task A_deleted_attachments_id_resolves_to_404()
    {
        var task = NewTask();
        var h = Build(task);
        var uploaded = await h.Add.Handle(UploadCmd(task.Id), CancellationToken.None);
        await h.Remove.Handle(new RemoveTaskAttachmentCommand(task.Id, uploaded.Data!.Id, Corr), CancellationToken.None);

        var result = await h.List.Handle(new ListTaskAttachmentsQuery(task.Id, Corr), CancellationToken.None);
        Assert.Empty(result.Data!);
    }

    // ── ObjectKey never serialized ─────────────────────────────────────────

    [Fact]
    public void TaskAttachmentDto_has_no_ObjectKey_or_StorageProvider_member()
    {
        /*
         * MUTATION GUARD (AC6-b): add either field back to the DTO and this goes red. Reflection, not a source
         * grep — the same reasoning DCP-005's freezer registration test gives: a real property is what a real
         * client would receive, a string search is not.
         */
        var properties = typeof(TaskAttachmentDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ObjectKey", properties);
        Assert.DoesNotContain("StorageProvider", properties);
    }

    [Fact]
    public async Task A_serialized_attachment_dto_contains_no_object_key_string()
    {
        var task = NewTask();
        var h = Build(task);
        var uploaded = await h.Add.Handle(UploadCmd(task.Id), CancellationToken.None);

        var json = JsonSerializer.Serialize(uploaded.Data);

        Assert.DoesNotContain("objectKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageProvider", json, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC4: repository validation passes through ─────────────────────────

    [Fact]
    public async Task A_disallowed_extension_is_refused_400_by_the_repositorys_own_rule()
    {
        var task = NewTask();
        var h = Build(task);

        var result = await h.Add.Handle(UploadCmd(task.Id, "malware.exe"), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        // MUTATION GUARD: nothing was written to our own collection on a rejected upload.
        Assert.Empty(h.Attachments.Items);
    }

    // ── AC5: auditable ──────────────────────────────────────────────────────

    [Fact]
    public void Add_and_remove_commands_are_auditable()
    {
        Assert.True(typeof(IAuditableCommand).IsAssignableFrom(typeof(AddTaskAttachmentCommand)));
        Assert.True(typeof(IAuditableCommand).IsAssignableFrom(typeof(RemoveTaskAttachmentCommand)));
        Assert.True(typeof(IAuditMetadataProvider).IsAssignableFrom(typeof(AddTaskAttachmentCommand)));
        Assert.True(typeof(IAuditMetadataProvider).IsAssignableFrom(typeof(RemoveTaskAttachmentCommand)));
    }

    [Fact]
    public void The_add_commands_audit_metadata_names_the_task_as_the_entity()
    {
        var task = NewTask();
        var command = UploadCmd(task.Id);
        var metadata = ((IAuditMetadataProvider)command).GetAuditMetadata();

        Assert.Equal(AuditCategory.Tasks, metadata.Category);
        Assert.Equal(AuditOperation.Create, metadata.Operation);
        Assert.Equal((Guid?)task.Id, metadata.EntityId);
    }
}
