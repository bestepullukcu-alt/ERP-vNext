using System.Text.Json;

namespace Diten.AuthService.Api.Security;

public static class ServiceIdentityTokenRequestParser
{
    public const long MaxBodyBytes = 1024;

    public static async Task<(Guid TenantId, string Audience)> ParseAsync(Stream body, CancellationToken ct)
    {
        await using var bounded = new MemoryStream();
        var buffer = new byte[256];
        while (true)
        {
            var read = await body.ReadAsync(buffer, ct);
            if (read == 0) break;
            if (bounded.Length + read > MaxBodyBytes) throw new ServiceIdentityRequestTooLargeException();
            await bounded.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        bounded.Position = 0;
        using var document = await JsonDocument.ParseAsync(bounded, cancellationToken: ct);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Object body required.");

        Guid tenantId = Guid.Empty;
        string? audience = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new JsonException("Duplicate field.");
            switch (property.Name)
            {
                case "tenantId" when property.Value.ValueKind == JsonValueKind.String:
                    if (!Guid.TryParse(property.Value.GetString(), out tenantId)) throw new JsonException("Invalid tenantId.");
                    break;
                case "audience" when property.Value.ValueKind == JsonValueKind.String:
                    audience = property.Value.GetString();
                    break;
                default: throw new JsonException("Unknown or invalid field.");
            }
        }
        if (seen.Count != 2 || tenantId == Guid.Empty || !ServiceIdentityTokenTransportContract.IsExactValue(audience, 128))
            throw new JsonException("Exact fields required.");
        return (tenantId, audience!);
    }
}

public sealed class ServiceIdentityRequestTooLargeException : Exception;

public static class ServiceIdentityTokenTransportContract
{
    public static bool IsExactValue(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl)
        && !value.Contains(',', StringComparison.Ordinal);

    public static bool IsSupportedContentType(string? contentType) =>
        string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/json; charset=utf-8", StringComparison.OrdinalIgnoreCase);

    public static bool HasNoQuery(string? queryString) => string.IsNullOrEmpty(queryString);
}
