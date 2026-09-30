using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Application.Features.WorkAggregation.Services;

/// <summary>
/// WP-CL-BE-3 — the approval's title / subtitle / link / chips from the <see cref="WorkflowInstance.DisplayContext"/>
/// snapshot its starter wrote. For approvals over objects whose owner lives in ANOTHER service (CRM claims), where no
/// in-process owner resolver can exist.
/// <para><b>Priority:</b> owner resolver &gt; this snapshot &gt; the generic fallback title. That order is enforced by the
/// provider (<see cref="IFallbackApprovalSourceResolver"/>): this resolver never answers for an object type an owner
/// claims, and an instance without a display context is left to the generic title.</para>
/// <para>Display only, read only: the link was validated as app-relative when the instance started.</para>
/// </summary>
public sealed class SnapshotApprovalSourceResolver : IFallbackApprovalSourceResolver
{
    public bool Handles(string objectType) => true;

    public Task<IReadOnlyDictionary<Guid, ApprovalSourceContext>> ResolveAsync(
        IReadOnlyCollection<WorkflowInstance> instances,
        WorkItemActor actor,
        CancellationToken ct = default)
    {
        var answers = new Dictionary<Guid, ApprovalSourceContext>();
        foreach (var instance in instances)
        {
            var display = instance.DisplayContext;
            if (display is null || (string.IsNullOrWhiteSpace(display.Title)
                    && string.IsNullOrWhiteSpace(display.Subtitle)
                    && string.IsNullOrWhiteSpace(display.DeepLinkUrl)))
            {
                continue;
            }

            answers[instance.Id] = new ApprovalSourceContext(
                string.IsNullOrWhiteSpace(display.Title) ? null : display.Title,
                // The starter is the requester, but the snapshot carries no directory name; say nothing rather than
                // print a raw id.
                Requester: null,
                string.IsNullOrWhiteSpace(display.DeepLinkUrl) ? null : display.DeepLinkUrl,
                string.IsNullOrWhiteSpace(display.Subtitle) ? null : display.Subtitle,
                display.Chips is { Count: > 0 } ? display.Chips.ToList() : null);
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, ApprovalSourceContext>>(answers);
    }
}
