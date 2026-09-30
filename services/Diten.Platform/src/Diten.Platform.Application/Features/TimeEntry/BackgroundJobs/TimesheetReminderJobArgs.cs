namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>MOD-0280-FU01 T3 — args for the Monday reminder. <paramref name="MaxPeoplePerTenant"/> caps the reminders one run
/// SENDS per tenant (M1): people already reminded do not count, and whoever is left is reached by the next run.</summary>
public sealed record TimesheetReminderJobArgs(int MaxPeoplePerTenant);
