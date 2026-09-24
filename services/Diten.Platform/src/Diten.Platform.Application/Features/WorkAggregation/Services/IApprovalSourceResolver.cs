using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Application.Features.WorkAggregation.Services;

/// <summary>
/// BL-437 — what an approval is ABOUT, answered by the module that owns the object being approved.
///
/// <para><b>Why this exists.</b> MOD-0023 carries only an opaque reference to the object it decides on
/// (<c>ObjectType</c> + <c>ObjectId</c>), so the approval row used to read "Onay: task-review 3f2c…" — a type code
/// and a GUID where the approver needed the task's name, who sent it and a way to open it. The workflow engine
/// cannot know any of that and must not learn it: the source module owns its object's title, its people and its
/// address. So the provider asks the owner, through this seam, and the projection only places the answers.</para>
///
/// <para><b>Read only, and never a decision.</b> A resolver reads its own records. It does not touch the approval,
/// the workflow or any permission — an unanswered object simply keeps today's fallback title.</para>
///
/// <para>Batched: the provider hands over every instance of a page at once, so a resolver reads each of its
/// collections once rather than once per row.</para>
/// </summary>
public interface IApprovalSourceResolver
{
    /// <summary>True when this resolver owns objects of <paramref name="objectType"/> (the instance's own value).</summary>
    bool Handles(string objectType);

    /// <summary>
    /// Context for each instance this resolver can answer for, keyed by <see cref="WorkflowInstance"/> id. An
    /// instance whose object no longer exists (or is not readable in this tenant) is simply absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ApprovalSourceContext>> ResolveAsync(
        IReadOnlyCollection<WorkflowInstance> instances,
        WorkItemActor actor,
        CancellationToken ct = default);
}

/// <summary>
/// The owner's answer about one approval's source object.
/// </summary>
/// <param name="Title">The object's own title, as a person typed it. Null or blank → the projection keeps the
/// generic fallback title.</param>
/// <param name="Requester">Who sent the object for this decision. Null when the owner cannot say.</param>
/// <param name="DeepLink">The owner's address for the object — an app-relative path, never a service port.</param>
public sealed record ApprovalSourceContext(
    string? Title,
    WorkItemPersonDto? Requester,
    string? DeepLink);
