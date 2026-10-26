using System.Net;
using System.Text.Json;
using Diten.SafetyStockService.Application;
using Microsoft.AspNetCore.Http;

namespace Diten.SafetyStockService.Infrastructure;

public sealed class MdmLegalEntityReferenceValidator : ILegalEntityReferenceValidator
{
    private readonly HttpClient _client;
    private readonly IHttpContextAccessor _httpContext;

    public MdmLegalEntityReferenceValidator(HttpClient client, IHttpContextAccessor httpContext)
    {
        _client = client;
        _httpContext = httpContext;
    }

    public async Task<ReferenceCheck> ValidateAsync(Guid legalEntityId, CancellationToken cancellationToken)
    {
        var bearer = _httpContext.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer) || !bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return ReferenceCheck.Unavailable;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{legalEntityId:D}/lookup-validation");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await _client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return ReferenceCheck.NotFound;
            if (response.StatusCode != HttpStatusCode.OK) return ReferenceCheck.Unavailable;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var data = json.RootElement.TryGetProperty("data", out var envelope) ? envelope : json.RootElement;
            if (!data.TryGetProperty("legalEntityId", out var id) || !Guid.TryParse(id.GetString(), out var returnedId)
                || !data.TryGetProperty("referenceable", out var referenceable)
                || referenceable.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return ReferenceCheck.Unavailable;
            return returnedId == legalEntityId && referenceable.GetBoolean()
                ? ReferenceCheck.Valid : ReferenceCheck.NotFound;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or UriFormatException)
        {
            return ReferenceCheck.Unavailable;
        }
    }
}
