namespace Diten.BuildingBlocks.BackgroundJobs;

public sealed class BackgroundJobValidationException : Exception
{
    public BackgroundJobValidationException(string message)
        : base(message)
    {
    }
}
