using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Mdm;

public sealed class MdmLegalEntityReferenceValidator : ILegalEntityReferenceValidator
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MdmLegalEntityReferenceValidator(
        HttpClient httpClient,
        IOptions<MdmServiceOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            throw new InvalidOperationException("Configuration error: 'MdmService:BaseUrl' is missing.");
        }

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
    {
        PropagateAuthorizationHeader();

        try
        {
            using var response = await _httpClient.GetAsync($"api/legal-entities/{legalEntityId:D}/lookup-validation", ct);
            if (!response.IsSuccessStatusCode)
            {
                return FailClosed();
            }

            var envelope = await response.Content.ReadFromJsonAsync<MdmResponse<LegalEntityReferenceDto>>(cancellationToken: ct);
            if (envelope?.IsSuccessful != true
                || envelope.Data is null
                || envelope.Data.LegalEntityId != legalEntityId
                || !string.Equals(envelope.Data.LifecycleState, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                || envelope.Data.Referenceable != true)
            {
                return FailClosed();
            }

            return Response<LegalEntityReferenceDto>.Success(envelope.Data);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return FailClosed();
        }
    }

    private void PropagateAuthorizationHeader()
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            _httpClient.DefaultRequestHeaders.Authorization = parsed;
        }
    }

    private static Response<LegalEntityReferenceDto> FailClosed() =>
        Response<LegalEntityReferenceDto>.Fail("Legal Entity is not referenceable.", 404);

    private sealed record MdmResponse<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string> Errors);
}
