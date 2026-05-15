namespace Diten.Platform.Infrastructure.Services.Audit;

public static class SafeAuditErrorFormatter
{
    public static string Format(Exception exception, int maxLength)
    {
        var message = exception is AuditOutboxPayloadMappingException mappingException
            ? mappingException.SafeMessage
            : $"Audit outbox processing failed. ErrorType={exception.GetType().Name}.";

        return Truncate(message, maxLength);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0 || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
