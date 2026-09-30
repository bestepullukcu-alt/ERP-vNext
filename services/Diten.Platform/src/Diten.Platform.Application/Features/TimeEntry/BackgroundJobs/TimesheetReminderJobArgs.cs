namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>MOD-0280-FU01 T3 — args for the Monday reminder. <paramref name="MaxPeoplePerTenant"/> caps one run per tenant.</summary>
public sealed record TimesheetReminderJobArgs(int MaxPeoplePerTenant);
