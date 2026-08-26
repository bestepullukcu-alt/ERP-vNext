using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0024 Phase 2 — checklist semantics (pack §12 E1). One owner for the question "may this task be
/// completed?", so the API and the Task Center can never disagree about it.
/// </summary>
public interface ITaskChecklistService
{
    /// <summary>
    /// True when an incomplete <see cref="ChecklistItemRequirement.Blocking"/> item stands in the way of
    /// completion. <c>Required</c> is deliberately NOT blocking — it means "you are expected to do this",
    /// not "you may not finish without it".
    /// </summary>
    bool BlocksCompletion(ChecklistRun? run);

    /// <summary>Materialize a template onto a task, preserving each item's label form and requirement.</summary>
    ChecklistRun Instantiate(Guid tenantId, Guid taskItemId, ChecklistTemplate template);

    /// <summary>Recompute the run's rolled-up status from its items.</summary>
    ChecklistRunStatus ResolveStatus(ChecklistRun run);
}

public sealed class TaskChecklistService : ITaskChecklistService
{
    public bool BlocksCompletion(ChecklistRun? run)
        => run is not null
           && run.Items.Any(item => item.Requirement == ChecklistItemRequirement.Blocking && !item.Completed);

    public ChecklistRun Instantiate(Guid tenantId, Guid taskItemId, ChecklistTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new ChecklistRun
        {
            TenantId = tenantId,
            TaskItemId = taskItemId,
            ChecklistTemplateId = template.Id,
            Status = ChecklistRunStatus.NotStarted,
            Items = template.Items
                .OrderBy(item => item.SortOrder)
                .Select(item => new ChecklistRunItem
                {
                    Code = item.Code,
                    // The label FORM is carried over as-is: a template item keeps its resource key so it
                    // localizes, and an author-typed one keeps its literal text. Collapsing the two here is how
                    // a raw resource key ends up on screen.
                    LabelResourceKey = item.LabelResourceKey,
                    LabelText = item.LabelText,
                    Requirement = item.Requirement,
                    SortOrder = item.SortOrder,
                    EvidenceRequired = item.EvidenceRequired,
                    Completed = false
                })
                .ToList()
        };
    }

    public ChecklistRunStatus ResolveStatus(ChecklistRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (run.Items.Count == 0 || run.Items.All(item => !item.Completed))
        {
            return ChecklistRunStatus.NotStarted;
        }

        return run.Items.All(item => item.Completed)
            ? ChecklistRunStatus.Completed
            : ChecklistRunStatus.InProgress;
    }
}
