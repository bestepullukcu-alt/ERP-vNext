namespace Diten.BuildingBlocks.BackgroundJobs;

public static class BackgroundJobTriggerTypes
{
    public const string FireAndForget = "FireAndForget";
    public const string Scheduled = "Scheduled";
    public const string Recurring = "Recurring";
    public const string Manual = "Manual";
    public const string EventDriven = "EventDriven";
}
