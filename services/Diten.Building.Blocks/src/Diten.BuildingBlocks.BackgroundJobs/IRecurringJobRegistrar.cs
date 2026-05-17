namespace Diten.BuildingBlocks.BackgroundJobs;

public interface IRecurringJobRegistrar
{
    IReadOnlyCollection<RecurringJobRegistration> GetRecurringJobs();
}
