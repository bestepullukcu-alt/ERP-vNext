using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 FIX4 — a QueueEmailNotificationHandler built WITHOUT the server's job settings now fails closed: no retry,
/// no variables kept. A test about retries therefore says, in so many words, that this server runs the retry sweep.
/// </summary>
internal static class NotificationTestJobs
{
    public static IOptions<BackgroundJobSchedulerOptions> RetriesOn() => Options.Create(new BackgroundJobSchedulerOptions
    {
        Enabled = true,
        RegisterStandardJobs = true,
        EnabledJobs = new(StringComparer.OrdinalIgnoreCase) { [EmailDispatchSweepJob.JobId] = true }
    });
}
