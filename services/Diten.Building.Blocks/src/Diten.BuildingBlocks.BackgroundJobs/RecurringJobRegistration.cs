namespace Diten.BuildingBlocks.BackgroundJobs;

public sealed record RecurringJobRegistration(
    BackgroundJobDescriptor Descriptor,
    Type HandlerType,
    Type ArgsType,
    object Args,
    BackgroundJobContext? Context = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Descriptor);
        ArgumentNullException.ThrowIfNull(HandlerType);
        ArgumentNullException.ThrowIfNull(ArgsType);
        ArgumentNullException.ThrowIfNull(Args);

        Descriptor.Validate();

        if (!string.IsNullOrWhiteSpace(Descriptor.CronExpression) && Descriptor.CronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 5)
        {
            throw new BackgroundJobValidationException("Recurring background job cron expression is invalid.");
        }

        if (!ArgsType.IsInstanceOfType(Args))
        {
            throw new BackgroundJobValidationException("Recurring background job args object does not match ArgsType.");
        }

        var expectedHandlerType = typeof(IBackgroundJobHandler<>).MakeGenericType(ArgsType);
        if (!expectedHandlerType.IsAssignableFrom(HandlerType))
        {
            throw new BackgroundJobValidationException("Recurring background job handler does not implement IBackgroundJobHandler<TArgs>.");
        }
    }
}
