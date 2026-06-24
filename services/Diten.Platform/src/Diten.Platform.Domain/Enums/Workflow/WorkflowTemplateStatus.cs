namespace Diten.Platform.Domain.Enums.Workflow;

// MOD-0023 Batch 01: status lifecycle for a workflow definition. Transition rules (publish/supersede)
// are implemented in later batches; this batch only persists the value.
public enum WorkflowTemplateStatus
{
    Draft = 0,
    Reviewed = 1,
    Published = 2,
    Superseded = 3,
    Archived = 4
}
