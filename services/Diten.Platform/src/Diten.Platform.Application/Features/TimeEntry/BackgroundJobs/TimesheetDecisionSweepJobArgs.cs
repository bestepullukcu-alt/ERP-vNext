namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>MOD-0280-FU01 — args for the decision sweep. <paramref name="MaxWeeksPerTenant"/> caps how many submitted
/// weeks one run looks at per tenant.</summary>
public sealed record TimesheetDecisionSweepJobArgs(int MaxWeeksPerTenant);
