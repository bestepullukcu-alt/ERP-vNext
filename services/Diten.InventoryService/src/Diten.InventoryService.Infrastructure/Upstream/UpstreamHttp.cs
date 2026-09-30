using System.Net;
using System.Text.Json;

namespace Diten.InventoryService.Infrastructure.Upstream;

/// <summary>
/// One fail-closed HTTP exchange with a frozen upstream contract. <c>200</c> → the parsed body; <c>404</c> (when the
/// operation declares it) → <c>default</c>; ANY other outcome — connection refused, timeout, 5xx, an undeclared
/// status, a body that does not parse into the contract shape — raises the caller-supplied "unavailable" exception.
/// A cancellation requested by the caller itself is not an upstream failure and propagates unchanged.
/// </summary>
internal static class UpstreamHttp
{
    public static async Task<T?> SendAsync<T>(
        HttpClient http,
        Func<HttpRequestMessage> requestFactory,
        bool notFoundIsAbsent,
        Func<JsonElement, T> parse,
        Func<string, Exception?, Exception> unavailable,
        CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage response;
        using var request = requestFactory();
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            throw unavailable($"{request.Method} {request.RequestUri} failed: {ex.GetType().Name}.", ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound && notFoundIsAbsent)
            {
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw unavailable($"{request.Method} {request.RequestUri} answered {(int)response.StatusCode}.", null);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return parse(document.RootElement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                throw unavailable($"{request.Method} {request.RequestUri} returned a body outside the contract shape.", ex);
            }
        }
    }

    public static string RequiredString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw new FormatException($"'{property}' is missing or not a string.");

    public static string? OptionalString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static bool OptionalBool(JsonElement element, string property, bool fallback = false)
        => element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;

    public static bool? NullableBool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    public static JsonElement RequiredArray(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value
            : throw new FormatException($"'{property}' is missing or not an array.");
}
