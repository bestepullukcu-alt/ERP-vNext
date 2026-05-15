namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed class AuditOutboxPayloadMappingException : Exception
{
    public AuditOutboxPayloadMappingException(string safeMessage)
        : base(safeMessage)
    {
        SafeMessage = safeMessage;
    }

    public string SafeMessage { get; }
}
