namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>MOD-0280-FU01 — args for the midnight close. <paramref name="MaxSegmentsPerTenant"/> caps one run per tenant.</summary>
public sealed record TimerMidnightCloseJobArgs(int MaxSegmentsPerTenant);
