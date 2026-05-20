namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

public sealed record EmailDispatchSweepJobArgs(int BatchSize = 50, int MaxRetryCount = 5);
