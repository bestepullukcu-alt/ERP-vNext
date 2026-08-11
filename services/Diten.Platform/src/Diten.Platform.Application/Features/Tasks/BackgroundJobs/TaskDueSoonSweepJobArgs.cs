namespace Diten.Platform.Application.Features.Tasks.BackgroundJobs;

/// <summary>
/// BL-065 — args for the due-soon reminder sweep. <paramref name="MaxTasksPerTenant"/> caps how many tasks are
/// considered per tenant per run, the same bound the recurrence and escalation sweeps put on their own work.
/// </summary>
public sealed record TaskDueSoonSweepJobArgs(int MaxTasksPerTenant);
