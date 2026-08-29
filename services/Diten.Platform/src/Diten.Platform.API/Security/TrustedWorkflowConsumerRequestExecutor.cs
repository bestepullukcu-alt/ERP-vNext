using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Diten.Platform.API.Security;

public sealed class TrustedWorkflowConsumerRequestExecutor : ITrustedWorkflowConsumerRequestExecutor
{
    public const int MaximumRequestBodyBytes = 32 * 1024;

    private readonly TrustedWorkflowConsumerRequestParser _parser;
    private readonly ITenantContext _tenantContext;

    public TrustedWorkflowConsumerRequestExecutor(
        TrustedWorkflowConsumerRequestParser parser,
        ITenantContext tenantContext)
    {
        _parser = parser;
        _tenantContext = tenantContext;
    }

    public Task<IActionResult> ExecuteStartAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowStartTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            TrustedWorkflowDelegatedUserIdentity, CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure) =>
        ExecuteWithBudgetAsync(
            cancellationToken,
            failure,
            token => ExecuteStartWithinBudgetAsync(httpContext, token, dispatch, failure));

    public Task<IActionResult> ExecuteEvidenceAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowTerminalDecisionEvidenceTransportRequest, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure) =>
        ExecuteWithBudgetAsync(
            cancellationToken,
            failure,
            token => ExecuteEvidenceWithinBudgetAsync(httpContext, token, dispatch, failure));

    public Task<IActionResult> ExecuteStartResultAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        Func<TrustedWorkflowStartResultTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure) =>
        ExecuteWithBudgetAsync(
            cancellationToken,
            failure,
            token => ExecuteStartResultWithinBudgetAsync(httpContext, token, dispatch, failure));

    private static async Task<IActionResult> ExecuteWithBudgetAsync(
        CancellationToken callerCancellation,
        Func<int, string, IActionResult> failure,
        Func<CancellationToken, Task<IActionResult>> operation)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(callerCancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            callerCancellation.ThrowIfCancellationRequested();
            return await operation(budget.Token);
        }
        catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
        {
            return failure(StatusCodes.Status504GatewayTimeout, "WORKFLOW_TRUSTED_CONSUMER_TIMEOUT");
        }
    }

    private async Task<IActionResult> ExecuteStartWithinBudgetAsync(
        HttpContext httpContext,
        CancellationToken budgetToken,
        Func<TrustedWorkflowStartTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            TrustedWorkflowDelegatedUserIdentity, CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure)
    {
        var preflight = Preflight(httpContext, requireDelegatedUser: true, requireIdempotencyKey: true, failure);
        if (preflight.Failure is not null)
        {
            return preflight.Failure;
        }

        var serviceAuthentication = await AuthenticateAsync(
            httpContext,
            TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme);
        budgetToken.ThrowIfCancellationRequested();
        if (serviceAuthentication.IsUnavailable)
        {
            return failure(StatusCodes.Status503ServiceUnavailable, "WORKFLOW_TRUSTED_CONSUMER_UNAVAILABLE");
        }

        if (!serviceAuthentication.Result.Succeeded)
        {
            return failure(StatusCodes.Status401Unauthorized, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED");
        }

        if (!TryResolveServiceIdentity(serviceAuthentication.Result.Principal, out var serviceIdentity))
        {
            return failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN");
        }

        var delegatedAuthentication = await AuthenticateAsync(
            httpContext,
            TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme);
        budgetToken.ThrowIfCancellationRequested();
        if (delegatedAuthentication.IsUnavailable)
        {
            return failure(StatusCodes.Status503ServiceUnavailable, "WORKFLOW_TRUSTED_CONSUMER_UNAVAILABLE");
        }

        if (!delegatedAuthentication.Result.Succeeded)
        {
            return failure(StatusCodes.Status401Unauthorized, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED");
        }

        if (!TryResolveDelegatedUser(delegatedAuthentication.Result.Principal, out var delegatedUser)
            || delegatedUser.TenantId != serviceIdentity.TenantId)
        {
            return failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH");
        }

        if (!PermissionClaimEvaluator.Evaluate(
                delegatedAuthentication.Result.Principal!.Claims,
                WorkflowPermissions.InstancesStart).IsSatisfied)
        {
            return failure(StatusCodes.Status403Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN");
        }

        var body = await ReadBoundedBodyAsync(httpContext.Request.Body, budgetToken);
        if (body is null)
        {
            return failure(StatusCodes.Status413PayloadTooLarge, "WORKFLOW_TRUSTED_CONSUMER_TOO_LARGE");
        }

        if (!_parser.TryParseStart(body.Value, out var request) || request is null)
        {
            return failure(StatusCodes.Status400BadRequest, "WORKFLOW_TRUSTED_START_INVALID");
        }

        var previousPrincipal = httpContext.User;
        try
        {
            httpContext.User = delegatedAuthentication.Result.Principal!;
            using (TenantScope.Begin(_tenantContext, serviceIdentity.TenantId))
            {
                return await dispatch(
                    request,
                    preflight.IdempotencyKey!,
                    serviceIdentity,
                    delegatedUser,
                    budgetToken);
            }
        }
        finally
        {
            httpContext.User = previousPrincipal;
        }
    }

    private async Task<IActionResult> ExecuteEvidenceWithinBudgetAsync(
        HttpContext httpContext,
        CancellationToken budgetToken,
        Func<TrustedWorkflowTerminalDecisionEvidenceTransportRequest, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure)
    {
        var preflight = Preflight(httpContext, requireDelegatedUser: false, requireIdempotencyKey: false, failure);
        if (preflight.Failure is not null)
        {
            return preflight.Failure;
        }

        var authentication = await AuthenticateAsync(
            httpContext,
            TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme);
        budgetToken.ThrowIfCancellationRequested();
        if (authentication.IsUnavailable)
        {
            return failure(StatusCodes.Status503ServiceUnavailable, "WORKFLOW_TRUSTED_CONSUMER_UNAVAILABLE");
        }

        if (!authentication.Result.Succeeded)
        {
            return failure(StatusCodes.Status401Unauthorized, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED");
        }

        if (!TryResolveServiceIdentity(authentication.Result.Principal, out var serviceIdentity))
        {
            return failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN");
        }

        var body = await ReadBoundedBodyAsync(httpContext.Request.Body, budgetToken);
        if (body is null)
        {
            return failure(StatusCodes.Status413PayloadTooLarge, "WORKFLOW_TRUSTED_CONSUMER_TOO_LARGE");
        }

        if (!_parser.TryParseEvidence(body.Value, out var request) || request is null)
        {
            return failure(StatusCodes.Status400BadRequest, "WORKFLOW_TERMINAL_EVIDENCE_INVALID");
        }

        var previousPrincipal = httpContext.User;
        try
        {
            httpContext.User = authentication.Result.Principal!;
            using (TenantScope.Begin(_tenantContext, serviceIdentity.TenantId))
            {
                return await dispatch(request, serviceIdentity, budgetToken);
            }
        }
        finally
        {
            httpContext.User = previousPrincipal;
        }
    }

    private async Task<IActionResult> ExecuteStartResultWithinBudgetAsync(
        HttpContext httpContext,
        CancellationToken budgetToken,
        Func<TrustedWorkflowStartResultTransportRequest, string, TrustedWorkflowConsumerServiceIdentity,
            CancellationToken, Task<IActionResult>> dispatch,
        Func<int, string, IActionResult> failure)
    {
        var preflight = Preflight(httpContext, requireDelegatedUser: false, requireIdempotencyKey: true, failure);
        if (preflight.Failure is not null)
        {
            return preflight.Failure;
        }

        var authentication = await AuthenticateAsync(
            httpContext,
            TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme);
        budgetToken.ThrowIfCancellationRequested();
        if (authentication.IsUnavailable)
        {
            return failure(StatusCodes.Status503ServiceUnavailable, "WORKFLOW_TRUSTED_CONSUMER_UNAVAILABLE");
        }

        if (!authentication.Result.Succeeded)
        {
            return failure(StatusCodes.Status401Unauthorized, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED");
        }

        if (!TryResolveServiceIdentity(authentication.Result.Principal, out var serviceIdentity))
        {
            return failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN");
        }

        var body = await ReadBoundedBodyAsync(httpContext.Request.Body, budgetToken);
        if (body is null)
        {
            return failure(StatusCodes.Status413PayloadTooLarge, "WORKFLOW_TRUSTED_CONSUMER_TOO_LARGE");
        }

        if (!_parser.TryParseStartResult(body.Value, out var request) || request is null)
        {
            return failure(StatusCodes.Status400BadRequest, "WORKFLOW_START_RESULT_INVALID");
        }

        var previousPrincipal = httpContext.User;
        try
        {
            httpContext.User = authentication.Result.Principal!;
            using (TenantScope.Begin(_tenantContext, serviceIdentity.TenantId))
            {
                return await dispatch(
                    request,
                    preflight.IdempotencyKey!,
                    serviceIdentity,
                    budgetToken);
            }
        }
        finally
        {
            httpContext.User = previousPrincipal;
        }
    }

    private static (IActionResult? Failure, string? IdempotencyKey) Preflight(
        HttpContext httpContext,
        bool requireDelegatedUser,
        bool requireIdempotencyKey,
        Func<int, string, IActionResult> failure)
    {
        if (httpContext.Request.ContentLength is > MaximumRequestBodyBytes)
        {
            return (failure(StatusCodes.Status413PayloadTooLarge, "WORKFLOW_TRUSTED_CONSUMER_TOO_LARGE"), null);
        }

        if (httpContext.Request.Headers.ContainsKey("X-Tenant-Id")
            || HasDuplicateValues(httpContext.Request.Headers.Authorization))
        {
            return (failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN"), null);
        }

        if (!HasOneExactBearer(httpContext.Request.Headers.Authorization))
        {
            return (failure(StatusCodes.Status401Unauthorized, "WORKFLOW_TRUSTED_CONSUMER_UNAUTHENTICATED"), null);
        }

        var delegated = httpContext.Request.Headers[
            TrustedServiceTokenValidationExtensions.DelegatedAuthorizationHeader];
        if (requireDelegatedUser)
        {
            if (HasDuplicateValues(delegated))
            {
                return (failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN"), null);
            }

            if (!HasOneExactBearer(delegated))
            {
                return (failure(StatusCodes.Status401Unauthorized, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED"), null);
            }
        }
        else if (delegated.Count != 0)
        {
            return (failure(StatusCodes.Status403Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN"), null);
        }

        var idempotency = httpContext.Request.Headers["Idempotency-Key"];
        if (requireIdempotencyKey)
        {
            if (idempotency.Count > 1)
            {
                return (failure(StatusCodes.Status400BadRequest, "WORKFLOW_START_IDEMPOTENCY_REQUIRED"), null);
            }

            var key = idempotency.Count == 1 ? idempotency[0]?.Trim() : null;
            if (!IsValidIdempotencyKey(key))
            {
                return (failure(StatusCodes.Status400BadRequest, "WORKFLOW_START_IDEMPOTENCY_REQUIRED"), null);
            }

            return (null, key);
        }

        if (idempotency.Count != 0)
        {
            return (failure(StatusCodes.Status400BadRequest, "WORKFLOW_TERMINAL_EVIDENCE_INVALID"), null);
        }

        return (null, null);
    }

    private static async Task<(AuthenticateResult Result, bool IsUnavailable)> AuthenticateAsync(
        HttpContext httpContext,
        string scheme)
    {
        try
        {
            return (await httpContext.AuthenticateAsync(scheme), false);
        }
        catch (InvalidOperationException)
        {
            return (AuthenticateResult.NoResult(), true);
        }
    }

    private static bool TryResolveServiceIdentity(
        ClaimsPrincipal? principal,
        out TrustedWorkflowConsumerServiceIdentity identity)
    {
        identity = null!;
        if (principal?.Identity?.IsAuthenticated != true
            || !TryOneGuid(principal, JwtRegisteredClaimNames.Sub, out var clientId)
            || !TryOneGuid(principal, "tenant_id", out var tenantId)
            || !TryOneGuid(principal, JwtRegisteredClaimNames.Jti, out var tokenId)
            || !TryOneExact(principal, "actor_type", "service")
            || !TryOneExact(principal, "service_name", TrustedServiceTokenValidationExtensions.RequiredServiceName)
            || !TryOneExact(principal, JwtRegisteredClaimNames.Aud,
                TrustedServiceTokenValidationExtensions.WorkflowRequiredAudience))
        {
            return false;
        }

        identity = new(
            clientId,
            tenantId,
            tokenId,
            TrustedServiceTokenValidationExtensions.RequiredServiceName,
            TrustedServiceTokenValidationExtensions.WorkflowRequiredAudience);
        return true;
    }

    private static bool TryResolveDelegatedUser(
        ClaimsPrincipal? principal,
        out TrustedWorkflowDelegatedUserIdentity identity)
    {
        identity = null!;
        if (principal?.Identity?.IsAuthenticated != true
            || !TryOneGuid(principal, JwtRegisteredClaimNames.Sub, out var userId)
            || !TryOneGuid(principal, "tenant_id", out var tenantId))
        {
            return false;
        }

        identity = new(userId, tenantId);
        return true;
    }

    private static bool TryOneGuid(ClaimsPrincipal principal, string type, out Guid value)
    {
        value = Guid.Empty;
        var claims = ClaimValues(principal, type);
        return claims.Count == 1
            && Guid.TryParseExact(claims[0], "D", out value)
            && value != Guid.Empty;
    }

    private static bool TryOneExact(ClaimsPrincipal principal, string type, string value)
    {
        var claims = ClaimValues(principal, type);
        return claims.Count == 1 && string.Equals(claims[0], value, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string type) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();

    private static bool HasDuplicateValues(StringValues values) => values.Count > 1;

    private static bool HasOneExactBearer(StringValues values) =>
        values.Count == 1
        && TrustedServiceTokenValidationExtensions.TryReadExactBearer(values[0], out _);

    private static bool IsValidIdempotencyKey(string? value) =>
        value is { Length: > 0 and <= 128 }
        && !value.Any(char.IsControl)
        && value.All(character => character is >= ' ' and <= '~');

    private static async Task<ReadOnlyMemory<byte>?> ReadBoundedBodyAsync(
        Stream body,
        CancellationToken cancellationToken)
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
