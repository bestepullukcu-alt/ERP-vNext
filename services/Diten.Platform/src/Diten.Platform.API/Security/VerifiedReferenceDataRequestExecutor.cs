using Diten.Platform.Common.Tenancy;
using Diten.Platform.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Security;

public sealed class VerifiedReferenceDataRequestExecutor : IVerifiedReferenceDataRequestExecutor
{
    public const string CredentialIdHeader = "X-Verified-Gsku-Credential-Id";
    public const string CredentialSecretHeader = "X-Verified-Gsku-Credential";
    public const string AudienceHeader = "X-Verified-Gsku-Audience";
    private readonly IVerifiedGskuResolverCredentialAuthenticator _credentialAuthenticator;
    private readonly IVerifiedGskuResolverJwtTenantContext _jwtTenantContext;
    private readonly IVerifiedReferenceDataServiceTenantContext? _serviceTenantContext;
    private readonly ITenantContext _tenantContext;

    public VerifiedReferenceDataRequestExecutor(
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        ITenantContext tenantContext)
        : this(credentialAuthenticator, jwtTenantContext, null, tenantContext)
    {
    }

    public VerifiedReferenceDataRequestExecutor(
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        IVerifiedReferenceDataServiceTenantContext? serviceTenantContext,
        ITenantContext tenantContext)
    {
        _credentialAuthenticator = credentialAuthenticator;
        _jwtTenantContext = jwtTenantContext;
        _serviceTenantContext = serviceTenantContext;
        _tenantContext = tenantContext;
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<Guid, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure) =>
        await ExecuteAsync(httpContext, cancellationToken, action, failure, allowServiceIdentity: true);

    public async Task<IActionResult> ExecuteInteractiveOnlyAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<Guid, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure) =>
        await ExecuteAsync(httpContext, cancellationToken, action, failure, allowServiceIdentity: false);

    private async Task<IActionResult> ExecuteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<Guid, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure,
        bool allowServiceIdentity)
    {
        var credentialIds = httpContext.Request.Headers[CredentialIdHeader];
        var credentialSecrets = httpContext.Request.Headers[CredentialSecretHeader];
        var audiences = httpContext.Request.Headers[AudienceHeader];
        if (credentialIds.Count != 1 || credentialSecrets.Count != 1 || audiences.Count != 1)
        {
            return failure(401, "REFERENCE_UNAUTHENTICATED");
        }

        var credential = _credentialAuthenticator.Authenticate(
            credentialIds[0],
            credentialSecrets[0],
            audiences[0]);
        if (!credential.IsAuthenticated)
        {
            return failure(credential.IsForbidden ? 403 : 401,
                credential.IsForbidden ? "REFERENCE_FORBIDDEN" : "REFERENCE_UNAUTHENTICATED");
        }

        var interactive = await _jwtTenantContext.ResolveAsync(httpContext);
        var service = _serviceTenantContext is null
            ? VerifiedGskuResolverJwtTenantResult.Unauthenticated
            : await _serviceTenantContext.ResolveAsync(httpContext);
        if (interactive.IsAuthenticated && service.IsAuthenticated)
        {
            return failure(403, "REFERENCE_FORBIDDEN");
        }

        if (!allowServiceIdentity && service.IsAuthenticated)
        {
            return failure(403, "REFERENCE_FORBIDDEN");
        }

        var tenant = interactive.IsAuthenticated ? interactive : service;
        if (!tenant.IsAuthenticated || !tenant.IsAuthorized || !tenant.TenantId.HasValue)
        {
            var forbidden = interactive.IsAuthenticated || service.IsAuthenticated;
            return failure(forbidden ? 403 : 401,
                forbidden ? "REFERENCE_FORBIDDEN" : "REFERENCE_UNAUTHENTICATED");
        }

        using (TenantScope.Begin(_tenantContext, tenant.TenantId.Value))
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                return await action(tenant.TenantId.Value, budget.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return failure(504, "REFERENCE_PROVIDER_TIMEOUT");
            }
        }
    }
}
