using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Attachments;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-349 — WIRING, not the rule (that is <c>TaskReadAccessPolicyTests</c>'s territory): each of the three
/// task-id-keyed GET endpoints (detail, attachment list, attachment content) asks <see cref="ITaskReadAccessPolicy"/>
/// before returning anything, and a denial is byte-identical to a genuinely missing task — never a 403, never a
/// different reason code. Driven through a policy DOUBLE (admit/deny by task id) so a wiring defect cannot hide
/// behind the real policy's own logic, mirroring how <c>TaskAssignmentGuardDoubles</c> isolates guard wiring from
/// the guard's rule.
/// </summary>
public sealed class TaskReadAccessWiringTests
{
    private const string Corr = "read-access-wiring";
    private static readonly Guid Actor = TaskTestData.Me;

    /// <summary>Admits or denies by task id — never consults a single global switch, so a test can prove the
    /// handler passed the RIGHT task through, not merely that some check ran.</summary>
    private sealed class StubReadAccessPolicy(params Guid[] admittedTaskIds) : ITaskReadAccessPolicy
    {
        private readonly HashSet<Guid> _admitted = [.. admittedTaskIds];
        public Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct)
            => Task.FromResult(_admitted.Contains(task.Id));
    }

    private static TaskItem NewTask() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Wiring probe",
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Rival,
        CreatedByUserId = TaskTestData.Other,
        OrganizationUnitId = Guid.NewGuid(),
        CreatedBy = "tester"
    };

    // ── GET api/v1/tasks/{id} ──────────────────────────────────────────

    [Fact]
    public async Task Detail_An_authorized_reader_gets_the_task()
    {
        var task = NewTask();
        var tasks = new FakeTaskItemRepository(task);
        var handler = new GetTaskItemByIdHandler(
            tasks, new FakeTaskWatcherRepository(), new FakeTaskDependencyRepository(),
            new TaskLifecycleService(), new FakeTaskApprovalService(), new FakeTaskFieldDefinitionRepository(),
            TaskActors.PermitAll(), new StubReadAccessPolicy(task.Id), new FakeCurrentUserContext(Actor));

        var result = await handler.Handle(new GetTaskItemByIdQuery(task.Id, Corr), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Detail_An_unauthorized_reader_of_a_REAL_task_gets_the_byte_identical_not_found_a_missing_task_gets()
    {
        var realTask = NewTask();
        var tasks = new FakeTaskItemRepository(realTask);
        var handler = new GetTaskItemByIdHandler(
            tasks, new FakeTaskWatcherRepository(), new FakeTaskDependencyRepository(),
            new TaskLifecycleService(), new FakeTaskApprovalService(), new FakeTaskFieldDefinitionRepository(),
            TaskActors.PermitAll(), new StubReadAccessPolicy(/* nobody admitted */), new FakeCurrentUserContext(Actor));

        var deniedReal = await handler.Handle(new GetTaskItemByIdQuery(realTask.Id, Corr), CancellationToken.None);
        var missing = await handler.Handle(new GetTaskItemByIdQuery(Guid.NewGuid(), Corr), CancellationToken.None);

        Assert.False(deniedReal.IsSuccessful);
        Assert.Equal(404, deniedReal.StatusCode);
        Assert.Equal(TaskReasonCodes.NotFound, deniedReal.ReasonCode);
        AssertByteIdenticalFailures(missing, deniedReal);
    }

    // ── GET {id}/attachments ────────────────────────────────────────────

    [Fact]
    public async Task AttachmentList_An_authorized_reader_gets_the_list()
    {
        var task = NewTask();
        var tasks = new FakeTaskItemRepository(task);
        var handler = new ListTaskAttachmentsHandler(
            tasks, new FakeTaskAttachmentRepository(), new StubReadAccessPolicy(task.Id), new FakeCurrentUserContext(Actor));

        var result = await handler.Handle(new ListTaskAttachmentsQuery(task.Id, Corr), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task AttachmentList_An_unauthorized_reader_of_a_REAL_task_gets_the_byte_identical_not_found_a_missing_task_gets()
    {
        var realTask = NewTask();
        var tasks = new FakeTaskItemRepository(realTask);
        var handler = new ListTaskAttachmentsHandler(
            tasks, new FakeTaskAttachmentRepository(), new StubReadAccessPolicy(), new FakeCurrentUserContext(Actor));

        var deniedReal = await handler.Handle(new ListTaskAttachmentsQuery(realTask.Id, Corr), CancellationToken.None);
        var missing = await handler.Handle(new ListTaskAttachmentsQuery(Guid.NewGuid(), Corr), CancellationToken.None);

        Assert.False(deniedReal.IsSuccessful);
        Assert.Equal(404, deniedReal.StatusCode);
        Assert.Equal(TaskReasonCodes.NotFound, deniedReal.ReasonCode);
        AssertByteIdenticalFailures(missing, deniedReal);
    }

    // ── GET {id}/attachments/{attachmentId}/content ────────────────────

    [Fact]
    public async Task AttachmentContent_An_unauthorized_reader_gets_the_TASK_not_found_shape_not_AttachmentNotFound()
    {
        // The read-access check runs BEFORE the attachment lookup, and on the SAME code a missing task answers
        // with — never AttachmentNotFound, which would tell an unauthorized caller "wrong attachment id" instead
        // of the task simply not existing for them.
        var realTask = NewTask();
        var attachments = new FakeTaskAttachmentRepository();
        var attachment = new TaskAttachment
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, TaskId = realTask.Id, Kind = TaskAttachmentKind.Attachment,
            ContentId = Guid.NewGuid(), FileName = "x.pdf", MediaType = "application/pdf", ByteSize = 1,
            Checksum = "x", UploadedByUserId = TaskTestData.Rival, UploadedAt = DateTimeOffset.UtcNow, CreatedBy = "tester"
        };
        attachments.CreateAsync(attachment, CancellationToken.None).GetAwaiter().GetResult();
        var tasks = new FakeTaskItemRepository(realTask);
        var handler = new OpenTaskAttachmentHandler(
            tasks, attachments, DocumentRepositoryOf(), new StubReadAccessPolicy(), new FakeCurrentUserContext(Actor));

        var deniedReal = await handler.Handle(
            new OpenTaskAttachmentQuery(realTask.Id, attachment.Id, Corr), CancellationToken.None);
        var missing = await handler.Handle(
            new OpenTaskAttachmentQuery(Guid.NewGuid(), attachment.Id, Corr), CancellationToken.None);

        Assert.False(deniedReal.IsSuccessful);
        Assert.Equal(404, deniedReal.StatusCode);
        Assert.Equal(TaskReasonCodes.NotFound, deniedReal.ReasonCode); // NOT AttachmentNotFound
        AssertByteIdenticalFailures(missing, deniedReal);
    }

    private static Diten.Platform.Application.Features.DocumentRepository.Services.DocumentRepositoryService DocumentRepositoryOf()
    {
        var tenant = new Diten.Platform.Common.Tenancy.TenantContext();
        tenant.SetTenant(TaskTestData.Tenant);
        var gateway = new Diten.Platform.Infrastructure.Services.DocumentManagement.LocalFileSystemContentStorageGateway(
            Microsoft.Extensions.Options.Options.Create(new Diten.Platform.Application.Contracts.DocumentRepository.ContentStorageOptions
            {
                RootPath = Path.Combine(Path.GetTempPath(), "DitenReadAccessWiringTests", Guid.NewGuid().ToString("N")),
                MaxFileSizeBytes = 1_048_576,
                AllowedExtensions = [".pdf"],
                AllowedMediaTypes = []
            }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Infrastructure.Services.DocumentManagement.LocalFileSystemContentStorageGateway>.Instance);
        return new Diten.Platform.Application.Features.DocumentRepository.Services.DocumentRepositoryService(
            gateway, new FakeRepositoryObjectRepository(), tenant, new FakeCurrentUserContext(Actor),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Application.Features.DocumentRepository.Services.DocumentRepositoryService>.Instance);
    }

    private sealed class FakeRepositoryObjectRepository : Diten.Platform.Domain.Repositories.IRepositoryObjectRepository
    {
        public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
            => Task.FromResult(repositoryObject);
        public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default)
            => Task.FromResult<RepositoryObject?>(null);
        public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RepositoryObject>>([]);
        public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default) => Task.FromResult(true);
    }

    /// <summary>
    /// Every wire-visible field of a failure response compared — Message/StatusCode/ReasonCode/Errors — so
    /// "the same shape" is measured, not asserted by eye. IsSuccessful is false for both by construction (Fail).
    /// </summary>
    private static void AssertByteIdenticalFailures<T>(Response<T> expectedShape, Response<T> actual)
    {
        Assert.Equal(expectedShape.StatusCode, actual.StatusCode);
        Assert.Equal(expectedShape.ReasonCode, actual.ReasonCode);
        Assert.Equal(expectedShape.Errors, actual.Errors);
    }
}
