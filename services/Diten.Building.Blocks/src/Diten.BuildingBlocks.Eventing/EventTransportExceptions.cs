namespace Diten.BuildingBlocks.Eventing;

public abstract class EventTransportTerminalExceptionBase : Exception
{
    protected EventTransportTerminalExceptionBase(
        EventOutboxTerminalFailureKind kind,
        string message,
        string reasonCode)
        : base(message)
    {
        Failure = new EventOutboxTerminalFailure(kind, reasonCode, message);
    }

    public EventOutboxTerminalFailure Failure { get; }
}

public sealed class EventContractException(string message, string reasonCode)
    : EventTransportTerminalExceptionBase(EventOutboxTerminalFailureKind.Contract, message, reasonCode);

public sealed class EventSecurityException(string message, string reasonCode)
    : EventTransportTerminalExceptionBase(EventOutboxTerminalFailureKind.Security, message, reasonCode);

public sealed class EventTerminalValidationException(string message, string reasonCode)
    : EventTransportTerminalExceptionBase(EventOutboxTerminalFailureKind.Validation, message, reasonCode);

public sealed class EventUnsupportedException(string message, string reasonCode)
    : EventTransportTerminalExceptionBase(EventOutboxTerminalFailureKind.Unsupported, message, reasonCode);

public sealed class EventDependencyException : Exception
{
    public EventDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
