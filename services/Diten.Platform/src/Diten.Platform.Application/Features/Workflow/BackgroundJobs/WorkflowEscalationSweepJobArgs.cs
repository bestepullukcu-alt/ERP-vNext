namespace Diten.Platform.Application.Features.Workflow.BackgroundJobs;

/// <summary>
/// MOD-0023 — args for the recurring workflow-escalation sweep. <paramref name="MaxItemsPerTenant"/> caps how
/// many overdue tasks are evaluated per tenant per run (matches RunWorkflowEscalationsRequest.MaxItems).
/// </summary>
public sealed record WorkflowEscalationSweepJobArgs(int MaxItemsPerTenant);
