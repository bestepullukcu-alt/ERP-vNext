using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Audit;

public sealed class AuditIdempotencyKeyBuilder : IAuditIdempotencyKeyBuilder
{
    public string Build(Guid correlationId, string requestType, string entityType, Guid? entityId, AuditOperation operation, int sequence = 0)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Audit idempotency correlation id is required.", nameof(correlationId));
        }

        if (string.IsNullOrWhiteSpace(requestType))
        {
            throw new ArgumentException("Audit idempotency request type is required.", nameof(requestType));
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Audit idempotency entity type is required.", nameof(entityType));
        }

        if (operation == AuditOperation.Unknown)
        {
            throw new ArgumentException("Audit idempotency operation is required.", nameof(operation));
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Audit idempotency sequence cannot be negative.");
        }

        var material = string.Join(
            "|",
            correlationId.ToString("N"),
            Normalize(requestType),
            Normalize(entityType),
            entityId?.ToString("N") ?? "none",
            ((int)operation).ToString(),
            sequence.ToString());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return $"audit:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
