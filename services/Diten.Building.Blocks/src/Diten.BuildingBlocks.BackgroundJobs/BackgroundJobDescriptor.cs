namespace Diten.BuildingBlocks.BackgroundJobs;

public sealed record BackgroundJobDescriptor(
    string Id,
    string ServiceName,
    string JobName,
    string Owner,
    string? CronExpression = null,
    string TimeZoneId = "UTC",
    bool IsEnabled = true,
    string Queue = "default",
    int MaxRetryAttempts = 5,
    string TriggerType = BackgroundJobTriggerTypes.Recurring)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new BackgroundJobValidationException("Background job descriptor id is required.");
        }

        if (string.IsNullOrWhiteSpace(ServiceName))
        {
            throw new BackgroundJobValidationException("Background job service name is required.");
        }

        if (string.IsNullOrWhiteSpace(JobName))
        {
            throw new BackgroundJobValidationException("Background job name is required.");
        }

        if (string.IsNullOrWhiteSpace(Owner))
        {
            throw new BackgroundJobValidationException("Background job owner is required.");
        }

        if (!string.Equals(TimeZoneId, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            throw new BackgroundJobValidationException("Background job schedules must use UTC timezone.");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new BackgroundJobValidationException("Background job max retry attempts cannot be negative.");
        }
    }
}
