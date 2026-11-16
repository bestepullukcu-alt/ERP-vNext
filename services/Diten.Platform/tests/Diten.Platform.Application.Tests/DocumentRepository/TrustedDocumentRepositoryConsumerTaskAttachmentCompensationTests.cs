using System.Text;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Application.Features.Tasks.Attachments;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentRepository;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentRepository;

/// <summary>
/// MOD-0262-FU01 DL-2 — inceleme r8 F-1 / F-2. The ONE in-process caller of <c>DocumentRepositoryService.StoreAsync</c> /
/// <c>CompensateAsync</c> is <c>TaskAttachmentHandlers</c> (<c>AddTaskAttachmentHandler</c>: store, then — when its own
/// <c>TaskAttachment</c> row cannot be written — <c>CompensateAsync(…, CancellationToken.None)</c>). DL-2 ties the provider
/// delete to <c>MarkCompensatedAsync</c>'s answer in the shared <c>CompensateRowAsync</c>, so this caller is touched too.
/// <list type="bullet">
/// <item>The first test pins today's behaviour of that caller and is green before AND after DL-2 (r8 D-1: the in-process
/// path passes the token it was given; here that token is <c>None</c>, so nothing changes).</item>
/// <item>The second test is the one behaviour DL-2 changes on this path (r5 F-3 race rule, both paths): a mark that loses
/// (<c>false</c>) no longer issues a second provider delete.</item>
/// </list>
/// The real local-filesystem provider sits behind a recorder, so the binary on disk is what is measured.
/// </summary>
public sealed class TrustedDocumentRepositoryConsumerTaskAttachmentCompensationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DitenDl2TaskAttachment", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TaskAttachmentHandlers_row_failure_compensates_in_process_marking_once_and_deleting_the_binary_once()
    {
        var harness = Build(markResult: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Handler.Handle(Upload(harness.Task.Id), CancellationToken.None));

        var stored = Assert.Single(harness.Objects.Items);
        var mark = Assert.Single(harness.Objects.MarkTokens);
        Assert.False(mark.CanBeCanceled); // TaskAttachmentHandlers passes CancellationToken.None and the service forwards it
        var delete = Assert.Single(harness.Storage.DeleteTokens);
        Assert.False(delete.CanBeCanceled);
        Assert.True(harness.Storage.DeleteResults.Single());
        Assert.False(File.Exists(Path.Combine(_root, stored.ObjectKey.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task TaskAttachmentHandlers_compensation_that_loses_the_mark_does_not_delete_the_binary_again()
    {
        var harness = Build(markResult: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Handler.Handle(Upload(harness.Task.Id), CancellationToken.None));

        Assert.Single(harness.Objects.MarkTokens);
        Assert.Empty(harness.Storage.DeleteTokens);
    }

    private Harness Build(bool markResult)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TaskTestData.Tenant);
        var currentUser = new FakeCurrentUserContext(TaskTestData.Me);
        var storage = new RecordingStorage(new LocalFileSystemContentStorageGateway(
            Options.Create(new ContentStorageOptions
            {
                RootPath = _root,
                MaxFileSizeBytes = 1_048_576,
                AllowedExtensions = [".pdf"],
                AllowedMediaTypes = []
            }),
            NullLogger<LocalFileSystemContentStorageGateway>.Instance));
        var objects = new RecordingObjects(markResult);
        var service = new DocumentRepositoryService(storage, objects, tenant, currentUser, NullLogger<DocumentRepositoryService>.Instance);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            TenantId = TaskTestData.Tenant,
            Title = "Attach me",
            Lifecycle = TaskLifecycle.Open,
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = TaskTestData.Me,
            CreatedByUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            CreatedBy = "tester"
        };

        var attachments = new Mock<ITaskAttachmentRepository>(MockBehavior.Strict);
        attachments
            .Setup(x => x.CreateAsync(It.IsAny<TaskAttachment>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("task attachment row write failed"));

        var handler = new AddTaskAttachmentHandler(
            new FakeTaskItemRepository(task),
            new FakeChecklistRunRepository(),
            attachments.Object,
            service,
            tenant,
            currentUser,
            writeGate: TaskWriteGateDoubles.Open);

        return new Harness(handler, task, storage, objects);
    }

    private static AddTaskAttachmentCommand Upload(Guid taskId) =>
        new(taskId, new MemoryStream(Encoding.UTF8.GetBytes("in-process body")), "evidence.pdf", "application/pdf",
            TaskAttachmentKind.Attachment, null, null, "dl2-f2-corr");

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) { Directory.Delete(_root, recursive: true); } } catch { /* best effort */ }
    }

    private sealed record Harness(AddTaskAttachmentHandler Handler, TaskItem Task, RecordingStorage Storage, RecordingObjects Objects);

    private sealed class RecordingStorage(IContentStorageGateway inner) : IContentStorageGateway
    {
        public List<CancellationToken> DeleteTokens { get; } = [];
        public List<bool> DeleteResults { get; } = [];

        public Task<Diten.Platform.Application.Common.Response<ContentStoreResult>> StoreAsync(ContentStoreRequest request, CancellationToken ct = default) =>
            inner.StoreAsync(request, ct);

        public Task<Diten.Platform.Application.Common.Response<ContentStreamResult>> OpenReadAsync(string storageProvider, string objectKey, CancellationToken ct = default) =>
            inner.OpenReadAsync(storageProvider, objectKey, ct);

        public async Task<bool> TryDeleteAsync(string storageProvider, string objectKey, CancellationToken ct = default)
        {
            DeleteTokens.Add(ct);
            var deleted = await inner.TryDeleteAsync(storageProvider, objectKey, ct);
            DeleteResults.Add(deleted);
            return deleted;
        }
    }

    private sealed class RecordingObjects(bool markResult) : IRepositoryObjectRepository
    {
        public List<RepositoryObject> Items { get; } = [];
        public List<CancellationToken> MarkTokens { get; } = [];

        public Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default)
        {
            Items.Add(repositoryObject);
            return Task.FromResult(repositoryObject);
        }

        public Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.ContentId == contentId && !x.IsDeleted));

        public Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RepositoryObject>>(Items.Where(x => x.OwningItemId == owningItemId).ToList());

        public Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default)
        {
            MarkTokens.Add(ct);
            return Task.FromResult(markResult);
        }
    }
}
