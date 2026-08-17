using Diten.BuildingBlocks.BackgroundJobs;

namespace Diten.Platform.Application.BackgroundJobs;

public sealed class DeferredPlatformJobHandler : IBackgroundJobHandler<DeferredPlatformJobArgs>
{
    public Task HandleAsync(
        DeferredPlatformJobArgs args,
        BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Background job execution is deferred to owning module {args.OwnerModule}: {args.Reason}");
    }
}
