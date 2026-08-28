using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.Http;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Auth;

/// <summary>
/// MOD-0288-FU01 — Platform-side <c>PositionAssignment.UserId</c> validation against the AuthService read-only
/// lookup-validation contract (GET /api/users/{userId}/lookup-validation). Mirrors the MDM Legal Entity reference
/// validator: typed HttpClient, the caller's own bearer forwarded, fail-closed on any failure.
///
/// <para><b>⚠ CORRECTION 2026-08-28 — this class used to claim "X-Tenant-Id via the shared
/// TenantPropagationHandler (registered in DI)". That was false for the whole life of the sentence.</b> The
/// handler WAS registered on this client and never added the header once. <c>IHttpClientFactory</c> builds and
/// CACHES its handler chain in its OWN scope, so a <c>DelegatingHandler</c> that injects the request-scoped
/// <see cref="ITenantContext"/> receives an instance belonging to no request; it answers
/// <c>IsResolved == false</c>, adds nothing, and says nothing anywhere. The unit tests never saw it because they
/// build the validator with a bare <c>HttpClient</c> and never go through the factory at all. The header is now
/// written HERE, from the request's own scope — the same conclusion <c>RemoteWorkItemGateway</c> reached by the
/// same measurement. Do NOT put it back in a handler; the next person's saved afternoon is the point of this
/// paragraph, and
/// <c>Tenant_header_is_written_by_the_validator_and_not_by_a_delegating_handler</c> fails if anyone tries.</para>
///
/// <para><b>What the header is worth against AuthService specifically.</b> AuthService resolves the tenant as
/// "JWT wins, header is a fallback" and — unlike MDM — issues NO mismatch 400 (measured in its own
/// <c>TenantResolutionMiddleware</c>; a differing header is only logged). So for a tenant caller the header is
/// belt-and-braces rather than the deciding input. It is still written, and the value still obeys
/// <see cref="TenantOnTheWire"/>, so that both reference validators state tenancy the same way and neither can
/// send the platform sentinel realm in place of a customer.</para>
/// </summary>
public sealed class AuthServiceUserReferenceValidator : IUserReferenceValidator
{
    private const string TenantHeader = "X-Tenant-Id";

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;

    public AuthServiceUserReferenceValidator(
        HttpClient httpClient,
        IOptions<AuthServiceOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            throw new InvalidOperationException("Configuration error: 'AuthService:BaseUrl' is missing.");
        }

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default)
    {
        // Fail-closed BEFORE the wire: with no tenant we cannot say who the answer would be about, and a user
        // confirmed against the wrong tenant is exactly the reference this contract exists to refuse.
        var tenantId = TenantOnTheWire.Resolve(_tenantContext);
        if (tenantId is null)
        {
            return FailClosed();
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/users/{userId:D}/lookup-validation");

            request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.Value.ToString());
            AttachCallerAuthorization(request);

            using var response = await _httpClient.SendAsync(request, ct);
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

    // The CALLER's own token, so AuthService authorises the human and not Platform.
    private void AttachCallerAuthorization(HttpRequestMessage request)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }
    }

    private static Response<UserReferenceDto> FailClosed() =>
        Response<UserReferenceDto>.Fail("User is not referenceable.", 404);

    private sealed record AuthServiceResponse<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string> Errors);
}
