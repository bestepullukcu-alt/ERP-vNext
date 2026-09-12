using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 Slice ATT-1 — a real (not stubbed-true) in-memory <see cref="ITaskAttachmentRepository"/> for tests
/// that are not themselves about attachments. Starts empty: a checklist item whose test never uploads evidence
/// genuinely has zero, so the evidence gate behaves exactly as it would in production, not as a bypass.
/// </summary>
internal sealed class FakeTaskAttachmentRepository : ITaskAttachmentRepository
{
    public List<TaskAttachment> Items { get; } = [];

    public Task<TaskAttachment> CreateAsync(TaskAttachment attachment, CancellationToken ct = default)
    {
        Items.Add(attachment);
        return Task.FromResult(attachment);
    }

    public Task<TaskAttachment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id && !a.IsDeleted));

    public Task<IReadOnlyList<TaskAttachment>> ListByTaskIdAsync(Guid taskId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TaskAttachment>>(
            Items.Where(a => a.TaskId == taskId && !a.IsDeleted).OrderByDescending(a => a.UploadedAt).ToList());

    public Task<IReadOnlyList<TaskAttachment>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TaskAttachment>>(
            Items.Where(a => taskIds.Contains(a.TaskId) && !a.IsDeleted).ToList());

    public Task<int> CountEvidenceForChecklistItemAsync(
        Guid taskId, string checklistItemCode, CancellationToken ct = default) =>
        Task.FromResult(Items.Count(a =>
            !a.IsDeleted && a.TaskId == taskId && a.ChecklistRunItemCode == checklistItemCode
            && a.Kind == TaskAttachmentKind.Evidence));

    public Task<bool> SoftDeleteAsync(Guid id, string deletedBy, CancellationToken ct = default)
    {
        var item = Items.FirstOrDefault(a => a.Id == id && !a.IsDeleted);
        if (item is null) { return Task.FromResult(false); }
        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = deletedBy;
        return Task.FromResult(true);
    }
}
