namespace Diten.Platform.Application.Features.Workflow.BackgroundJobs;

/// <summary>BL-549 — args for the recurring abandoned-start recovery. <paramref name="MaxClosesPerTenant"/> ≤ 0 takes
/// <see cref="WorkflowStartRecoveryOptions.MaxClosesPerTenant"/>.</summary>
public sealed record WorkflowStartRecoverySweepJobArgs(int MaxClosesPerTenant);
