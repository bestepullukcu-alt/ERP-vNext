using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Auth;

// MOD-0288-FU01: Platform-side PositionAssignment.UserId validation against the AuthService
// read-only lookup-validation contract (GET /api/users/{userId}/lookup-validation).
// Mirrors the MDM Legal Entity reference validator pattern: typed HttpClient, bearer forwarding,
// X-Tenant-Id via the shared TenantPropagationHandler (registered in DI), fail-closed on any failure.
public sealed class AuthServiceUserReferenceValidator : IUserReferenceValidator
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthServiceUserReferenceValidator(
        HttpClient httpClient,
        IOptions<AuthServiceOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            throw new InvalidOperationException("Configuration error: 'AuthService:BaseUrl' is missing.");
        }

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default)
    {
        PropagateAuthorizationHeader();

        try
        {
            using var response = await _httpClient.GetAsync($"api/users/{userId:D}/lookup-validation", ct);
            if (!response.IsSuccessStatusCode)
            {
                return FailClosed();
            }

            var envelope = await response.Content.ReadFromJsonAsync<AuthServiceResponse<UserReferenceDto>>(cancellationToken: ct);
            if (envelope?.IsSuccessful != true
                || envelope.Data is null
                || envelope.Data.UserId != userId
                || envelope.Data.Referenceable != true)
            {
                return FailClosed();
            }

            return Response<UserReferenceDto>.Success(envelope.Data);
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

    private static Response<UserReferenceDto> FailClosed() =>
        Response<UserReferenceDto>.Fail("User is not referenceable.", 404);

    private sealed record AuthServiceResponse<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string> Errors);
}
