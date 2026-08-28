using Diten.Platform.API.Models.Audit;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Diten.Platform.API.Security;

public sealed class TrustedSourceAuditIntentRequestExecutor : ITrustedSourceAuditIntentRequestExecutor
{
    public const string CredentialIdHeader = "X-Audit-Source-Credential-Id";
    public const string CredentialSecretHeader = "X-Audit-Source-Credential";
    public const string AudienceHeader = "X-Audit-Source-Audience";
    public const int MaximumRequestBodyBytes = 32 * 1024;

    private readonly ITrustedSourceAuditIntentCredentialAuthenticator _credentialAuthenticator;
    private readonly ITrustedSourceAuditIntentServiceIdentity _serviceIdentity;
    private readonly TrustedSourceAuditIntentRequestParser _parser;
    private readonly ITrustedSourceAuditIntentAcceptanceService _acceptanceService;
    private readonly ITenantContext _tenantContext;

    public TrustedSourceAuditIntentRequestExecutor(
        ITrustedSourceAuditIntentCredentialAuthenticator credentialAuthenticator,
        ITrustedSourceAuditIntentServiceIdentity serviceIdentity,
        TrustedSourceAuditIntentRequestParser parser,
        ITrustedSourceAuditIntentAcceptanceService acceptanceService,
        ITenantContext tenantContext)
    {
        _credentialAuthenticator = credentialAuthenticator;
        _serviceIdentity = serviceIdentity;
        _parser = parser;
        _acceptanceService = acceptanceService;
        _tenantContext = tenantContext;
    }

    public async Task<IActionResult> ExecuteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedSourceAuditIntentAcceptanceResult, IActionResult> result,
        Func<int, string, IActionResult> failure)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            budget.Token.ThrowIfCancellationRequested();
            return await ExecuteWithinBudgetAsync(httpContext, budget.Token, result, failure);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return failure(StatusCodes.Status504GatewayTimeout, "AUDIT_SOURCE_INTENT_TIMEOUT");
        }
    }

    private async Task<IActionResult> ExecuteWithinBudgetAsync(
        HttpContext httpContext,
        CancellationToken budgetToken,
        Func<TrustedSourceAuditIntentAcceptanceResult, IActionResult> result,
        Func<int, string, IActionResult> failure)
    {
        if (httpContext.Request.ContentLength is > MaximumRequestBodyBytes)
        {
            return failure(StatusCodes.Status413PayloadTooLarge, "AUDIT_SOURCE_INTENT_TOO_LARGE");
        }

        if (HasDuplicateValues(httpContext.Request.Headers.Authorization)
            || HasDuplicateValues(httpContext.Request.Headers[CredentialIdHeader])
            || HasDuplicateValues(httpContext.Request.Headers[CredentialSecretHeader])
            || HasDuplicateValues(httpContext.Request.Headers[AudienceHeader])
            || httpContext.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            return failure(StatusCodes.Status403Forbidden, "AUDIT_SOURCE_INTENT_FORBIDDEN");
        }

        if (!HasExactlyOneNonEmptyValue(httpContext.Request.Headers.Authorization)
            || !HasExactlyOneNonEmptyValue(httpContext.Request.Headers[CredentialIdHeader])
            || !HasExactlyOneNonEmptyValue(httpContext.Request.Headers[CredentialSecretHeader])
            || !HasExactlyOneNonEmptyValue(httpContext.Request.Headers[AudienceHeader]))
        {
            return failure(StatusCodes.Status401Unauthorized, "AUDIT_SOURCE_INTENT_UNAUTHENTICATED");
        }

        var credential = _credentialAuthenticator.Authenticate(
            httpContext.Request.Headers[CredentialIdHeader][0],
            httpContext.Request.Headers[CredentialSecretHeader][0],
            httpContext.Request.Headers[AudienceHeader][0]);
        if (!credential.IsAuthenticated)
        {
            return failure(
                credential.IsForbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized,
                credential.IsForbidden ? "AUDIT_SOURCE_INTENT_FORBIDDEN" : "AUDIT_SOURCE_INTENT_UNAUTHENTICATED");
        }

        var identity = await _serviceIdentity.ResolveAsync(httpContext, credential.AllowedTenantIds);
        budgetToken.ThrowIfCancellationRequested();
        if (!identity.IsAuthenticated || !identity.IsAuthorized || !identity.TenantId.HasValue)
        {
            return failure(
                identity.IsAuthenticated ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized,
                identity.IsAuthenticated ? "AUDIT_SOURCE_INTENT_FORBIDDEN" : "AUDIT_SOURCE_INTENT_UNAUTHENTICATED");
        }

        var body = await ReadBoundedBodyAsync(httpContext.Request.Body, budgetToken);
        if (body is null)
        {
            return failure(StatusCodes.Status413PayloadTooLarge, "AUDIT_SOURCE_INTENT_TOO_LARGE");
        }

        if (!_parser.TryParse(body.Value, out var request) || request is null)
        {
            return failure(StatusCodes.Status400BadRequest, "AUDIT_SOURCE_INTENT_INVALID");
        }

        if (request.TenantId != identity.TenantId.Value)
        {
            return failure(StatusCodes.Status403Forbidden, "AUDIT_SOURCE_INTENT_FORBIDDEN");
        }

        using (TenantScope.Begin(_tenantContext, identity.TenantId.Value))
        {
            return result(await _acceptanceService.AcceptAsync(request.ToEnvelope(), budgetToken));
        }
    }

    private static bool HasDuplicateValues(StringValues values) => values.Count > 1;

    private static bool HasExactlyOneNonEmptyValue(StringValues values) =>
        values.Count == 1 && !string.IsNullOrEmpty(values[0]);

    private static async Task<ReadOnlyMemory<byte>?> ReadBoundedBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(MaximumRequestBodyBytes);
        var chunk = new byte[4096];
        while (true)
        {
            var read = await body.ReadAsync(chunk.AsMemory(), cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaximumRequestBodyBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }
}
