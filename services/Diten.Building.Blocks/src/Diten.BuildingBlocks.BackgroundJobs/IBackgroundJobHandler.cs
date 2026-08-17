namespace Diten.BuildingBlocks.BackgroundJobs;

public interface IBackgroundJobHandler<in TArgs>
{
    Task HandleAsync(TArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default);
}
