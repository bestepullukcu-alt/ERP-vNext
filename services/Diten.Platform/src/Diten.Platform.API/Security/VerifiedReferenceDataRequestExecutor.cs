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
    private readonly ITenantContext _tenantContext;

    public VerifiedReferenceDataRequestExecutor(
        IVerifiedGskuResolverCredentialAuthenticator credentialAuthenticator,
        IVerifiedGskuResolverJwtTenantContext jwtTenantContext,
        ITenantContext tenantContext)
    {
        _credentialAuthenticator = credentialAuthenticator;
        _jwtTenantContext = jwtTenantContext;
        _tenantContext = tenantContext;
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<Guid, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure)
    {
        var credential = _credentialAuthenticator.Authenticate(
            httpContext.Request.Headers[CredentialIdHeader].FirstOrDefault(),
            httpContext.Request.Headers[CredentialSecretHeader].FirstOrDefault(),
            httpContext.Request.Headers[AudienceHeader].FirstOrDefault());
        if (!credential.IsAuthenticated)
        {
            return failure(credential.IsForbidden ? 403 : 401,
                credential.IsForbidden ? "REFERENCE_FORBIDDEN" : "REFERENCE_UNAUTHENTICATED");
        }

        var jwt = await _jwtTenantContext.ResolveAsync(httpContext);
        if (!jwt.IsAuthenticated || !jwt.IsAuthorized || !jwt.TenantId.HasValue)
        {
            return failure(jwt.IsAuthenticated ? 403 : 401,
                jwt.IsAuthenticated ? "REFERENCE_FORBIDDEN" : "REFERENCE_UNAUTHENTICATED");
        }

        using (TenantScope.Begin(_tenantContext, jwt.TenantId.Value))
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                return await action(jwt.TenantId.Value, budget.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return failure(504, "REFERENCE_PROVIDER_TIMEOUT");
            }
        }
    }
}
