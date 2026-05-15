namespace Diten.BuildingBlocks.Eventing;

public sealed class EventValidationException : Exception
{
    public EventValidationException(string message) : base(message)
    {
    }
}
