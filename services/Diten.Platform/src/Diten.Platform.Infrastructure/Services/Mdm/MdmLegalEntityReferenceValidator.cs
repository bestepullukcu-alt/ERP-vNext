using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.Http;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.Mdm;

/// <summary>
/// MOD-0220 read-only reference check against MDM (GET /api/legal-entities/{id}/lookup-validation).
/// Fail-closed on every failure: a Legal Entity is referenceable only when MDM says so, out loud.
///
/// <para><b>⚠ Tenancy is written HERE, not by a <c>DelegatingHandler</c>, MEASURED 2026-08-28.</b>
/// A shared tenant-propagation handler was registered on this client and never did anything (it was detached in
/// BL-311 and the class deleted in BL-316): <c>IHttpClientFactory</c> builds and CACHES
/// the handler chain in its OWN scope, so a <c>DelegatingHandler</c> resolving the request-scoped
/// <see cref="ITenantContext"/> gets an instance belonging to no request, answers <c>IsResolved == false</c>, and
/// adds no header — silently. The unit tests could not see it because they never went through the factory. The
/// same failure was measured and rejected once before, on the work-item bridge; see
/// <c>RemoteWorkItemGateway</c>'s class comment, which is the precedent this class follows. Do NOT move this back
/// into a handler — <c>Tenant_header_is_written_by_the_validator_and_not_by_a_delegating_handler</c> pins it.</para>
///
/// <para><b>⚠ Which tenant goes on the wire.</b> MDM resolves <c>jwtTenant ?? headerTenant</c> and answers 400
/// "Tenant mismatch" when both are present and DIFFER — it has no notion of <c>actor_type</c> and grants a
/// platform actor no exception (measured in MDM's own <c>TenantResolutionMiddleware</c>). So the header must
/// carry the tenant the caller is really acting for, never the platform sentinel
/// (<c>00000000-0000-0000-0000-000000000001</c>) that a platform token's <c>tenant_id</c> claim carries. In a
/// tenant context that is <see cref="ITenantContext.TenantId"/>; in a platform context it is
/// <see cref="ITenantContext.TargetTenantId"/>, and when no target tenant has been declared there is no honest
/// value to send, so the call is NOT made and the reference fails closed rather than being answered about the
/// wrong tenant.</para>
/// </summary>
public sealed class MdmLegalEntityReferenceValidator : ILegalEntityReferenceValidator
{
    private const string TenantHeader = "X-Tenant-Id";

    /*
     * ⚠ THIS CALL SITS IN THE TASK CENTER'S CREATE PATH, so an MDM that is merely SLOW freezes a screen.
     *
     * MEASURED 2026-09-02, live: MDM answered /lookup-validation in 30 021 ms (its Mongo driver could not
     * select a server). This client had no budget of its own and none in DI, so it waited on the .NET
     * default of 100 s; Platform's assignable-people query waited on it; the browser's wire() awaited that
     * and never bound the date pickers or the person selects. Four separate "the UI is broken" reports,
     * one unbounded dependency.
     *
     * The sibling registered two lines below in DependencyInjection already owns a linked budget
     * (WorkingCalendarLegalEntityValidator, 3 s). This is that pattern, applied where it was missing.
     * A budget that expires is NOT the caller's cancellation, so it lands in the TaskCanceledException
     * catch and fails closed -- the same answer this validator already gives for an unreachable MDM.
     */
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<MdmLegalEntityReferenceValidator> _logger;

    public MdmLegalEntityReferenceValidator(
        HttpClient httpClient,
        IOptions<MdmServiceOptions> options,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext,
        ILogger<MdmLegalEntityReferenceValidator> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            throw new InvalidOperationException("Configuration error: 'MdmService:BaseUrl' is missing.");
        }

        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default)
    {
        // Fail-closed BEFORE the wire: without a tenant we cannot state who the answer would be about, and an
        // answer about the wrong tenant is the silent wrong "not found" this class exists to prevent.
        var tenantId = TenantOnTheWire.Resolve(_tenantContext, out var skipReason);
        if (tenantId is null)
        {
            // Say WHICH fail-closed this is. Before 2026-08-28 this collapsed into the same 404 as "MDM did not
            // answer", so "why was it not found?" had no answer in any log. The reader's sentence is unchanged;
            // this line is for the operator.
            _logger.LogWarning(
                "Legal Entity reference check not attempted: no tenant could be named. Reason={SkipReason} LegalEntityId={LegalEntityId}",
                skipReason,
                legalEntityId);

            return FailClosed();
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/legal-entities/{legalEntityId:D}/lookup-validation");

            request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.Value.ToString());
            AttachCallerAuthorization(request);

            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TotalTimeout);

            using var response = await _httpClient.SendAsync(request, budget.Token);
            if (!response.IsSuccessStatusCode)
            {
                return FailClosed();
            }

            var envelope = await response.Content.ReadFromJsonAsync<MdmResponse<LegalEntityReferenceDto>>(cancellationToken: budget.Token);
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

    // The CALLER's own token, so MDM authorises the human and not Platform. Written on the request rather than on
    // DefaultRequestHeaders: the tenant header already has to be per-request, and one place is easier to read.
    private void AttachCallerAuthorization(HttpRequestMessage request)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }
    }

    private static Response<LegalEntityReferenceDto> FailClosed() =>
        Response<LegalEntityReferenceDto>.Fail("Legal Entity is not referenceable.", 404);

    private sealed record MdmResponse<T>(T? Data, int StatusCode, bool IsSuccessful, IReadOnlyList<string> Errors);
}
