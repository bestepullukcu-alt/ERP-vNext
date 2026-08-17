namespace Diten.Platform.Application.BackgroundJobs;

public sealed record SchedulerSmokeTestJobArgs(bool ShouldFail, string Message = "scheduler-smoke");
