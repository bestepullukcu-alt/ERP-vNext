namespace Diten.Platform.Application.Features.Tasks.BackgroundJobs;

/// <summary>
/// MOD-0024 Phase 4 — args for the recurring task-generation sweep. <paramref name="MaxRulesPerTenant"/> caps how
/// many rules are evaluated per tenant per run, the same bound the escalation sweep puts on its own work.
/// </summary>
public sealed record TaskRecurrenceSweepJobArgs(int MaxRulesPerTenant);
