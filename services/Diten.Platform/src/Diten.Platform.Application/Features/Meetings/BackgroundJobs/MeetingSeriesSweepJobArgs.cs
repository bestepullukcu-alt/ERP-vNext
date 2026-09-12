namespace Diten.Platform.Application.Features.Meetings.BackgroundJobs;

/// <summary>MOD-0357 S11 — args for the recurring meeting-series generation sweep. <paramref name="MaxSeriesPerTenant"/>
/// caps how many series are evaluated per tenant per run, the same bound MOD-0024's own recurrence sweep puts
/// on its rules (<c>TaskRecurrenceSweepJobArgs</c>).</summary>
public sealed record MeetingSeriesSweepJobArgs(int MaxSeriesPerTenant);
