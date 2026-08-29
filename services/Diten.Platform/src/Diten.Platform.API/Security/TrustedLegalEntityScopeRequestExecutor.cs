using Diten.Platform.API.Models.AccessGovernance;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Diten.Platform.Application.Authorization;
using MongoDB.Driver;

namespace Diten.Platform.API.Security;

public sealed class TrustedLegalEntityScopeRequestExecutor : ITrustedLegalEntityScopeRequestExecutor
{
    public const string CredentialIdHeader = "X-Legal-Entity-Scope-Credential-Id";
    public const string CredentialSecretHeader = "X-Legal-Entity-Scope-Credential";
    public const string AudienceHeader = "X-Legal-Entity-Scope-Audience";

    private readonly ITrustedLegalEntityScopeCredentialAuthenticator _credentialAuthenticator;
    private readonly ITrustedLegalEntityScopeJwtContext _jwtContext;
    private readonly ITenantContext _tenantContext;

    public TrustedLegalEntityScopeRequestExecutor(
        ITrustedLegalEntityScopeCredentialAuthenticator credentialAuthenticator,
        ITrustedLegalEntityScopeJwtContext jwtContext,
        ITenantContext tenantContext)
    {
        _credentialAuthenticator = credentialAuthenticator ?? throw new ArgumentNullException(nameof(credentialAuthenticator));
        _jwtContext = jwtContext ?? throw new ArgumentNullException(nameof(jwtContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext context,
        CancellationToken cancellationToken,
        Func<Guid, Guid, TrustedLegalEntityScopeResolveRequest, CancellationToken, Task<IActionResult>> action,
        Func<int, string, IActionResult> failure)
    {
        if (context.Request.Headers[CredentialIdHeader].Count != 1
            || context.Request.Headers[CredentialSecretHeader].Count != 1
            || context.Request.Headers[AudienceHeader].Count != 1)
        {
            return failure(401, "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED");
        }

        var credential = _credentialAuthenticator.Authenticate(
            context.Request.Headers[CredentialIdHeader][0],
            context.Request.Headers[CredentialSecretHeader][0],
            context.Request.Headers[AudienceHeader][0]);
        if (!credential.Authenticated)
        {
            return failure(
                credential.Forbidden ? 403 : 401,
                credential.Forbidden ? "LEGAL_ENTITY_SCOPE_FORBIDDEN" : "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED");
        }

        var jwt = await _jwtContext.ResolveAsync(context);
        if (!jwt.Authenticated || !jwt.Authorized || !jwt.TenantId.HasValue || !jwt.SubjectId.HasValue)
        {
            return failure(
                jwt.Authenticated ? 403 : 401,
                jwt.Authenticated ? "LEGAL_ENTITY_SCOPE_FORBIDDEN" : "LEGAL_ENTITY_SCOPE_UNAUTHENTICATED");
        }

        var parsed = await TrustedLegalEntityScopeResolveRequestParser.ParseAsync(context.Request, cancellationToken);
        if (parsed.Request is null)
        {
            return failure(400, parsed.Error!);
        }

        if (!_credentialAuthenticator.AllowsPair(parsed.Request.ModuleCode, parsed.Request.PermissionKey)
            || !jwt.HasExactPermission(parsed.Request.PermissionKey))
        {
            return failure(403, "LEGAL_ENTITY_SCOPE_FORBIDDEN");
        }

        using (TenantScope.Begin(_tenantContext, jwt.TenantId.Value))
        using (var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                return await action(
                    jwt.TenantId.Value,
                    jwt.SubjectId.Value,
                    parsed.Request,
                    budget.Token);
            }
            catch (OperationCanceledException) when (
                budget.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return failure(504, "LEGAL_ENTITY_SCOPE_TIMEOUT");
            }
        }
    }
}

public sealed class MongoOrgDataScopeCandidateAvailabilityClassifier : IOrgDataScopeCandidateAvailabilityClassifier
{
    public bool IsUnavailable(Exception exception) =>
        exception is MongoConnectionException
            or MongoExecutionTimeoutException
            or TimeoutException;
}
