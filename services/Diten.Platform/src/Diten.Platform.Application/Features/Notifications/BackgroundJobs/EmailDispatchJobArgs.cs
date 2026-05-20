namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

public sealed record EmailDispatchJobArgs(Guid TenantId, Guid DispatchId);
