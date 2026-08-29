using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Diten.Web.Models.ManagementGovernance.Dws;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Services.ManagementGovernance.Dws;

public sealed class DwsFrontendGatewayProxy(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DwsFrontendGatewayProxy> logger)
{
    public const string FeatureFlag = "Features:ManagementGovernance:DwsFrontendGatewayEnabled";
    public const string NotReadyCode = "dws_frontend_gateway_not_ready";
    private const string ApiBase = "/api/dws/structures";
    private const string CorrelationHeader = "X-Correlation-Id";

    public bool IsEnabled => configuration.GetValue<bool>(FeatureFlag);

    public bool HasPermission(ClaimsPrincipal principal, string permission) =>
        principal.Claims.Any(claim =>
            (string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(claim.Value, permission, StringComparison.Ordinal));

    public async Task<IActionResult> GetAsync(
        HttpContext context,
        string relativePath,
        string requiredPermission,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return Failure(context, StatusCodes.Status503ServiceUnavailable, NotReadyCode);
        }

        if (!DwsFrontendPermissions.Exact.Contains(requiredPermission))
        {
            return Failure(context, StatusCodes.Status403Forbidden, "dws_permission_denied");
        }

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(context.Request);
        var tenantId = ResolveClaim(context.User, token, "tenantId", "tenant_id");
        if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(tenantId, out var tenant) || tenant == Guid.Empty)
        {
            return Failure(context, StatusCodes.Status401Unauthorized, "dws_unauthorized");
        }

        if (!HasPermission(context.User, requiredPermission) &&
            !TokenHasPermission(token, requiredPermission))
        {
            return Failure(context, StatusCodes.Status403Forbidden, "dws_permission_denied");
        }

        var gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(gatewayUrl))
        {
            return Failure(context, StatusCodes.Status503ServiceUnavailable, NotReadyCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{gatewayUrl}{ApiBase}/{relativePath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeader, ResolveCorrelationId(context));
        if (context.Request.Headers.TryGetValue("Accept-Language", out var language))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", language.ToString());
        }

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            return await Diten.Web.Infrastructure.TenantShellProxyResponse.PassthroughAsync(
                response,
                context.Request,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "DWS frontend Gateway read failed. CorrelationId={CorrelationId}", context.TraceIdentifier);
            return Failure(context, StatusCodes.Status503ServiceUnavailable, "dws_frontend_gateway_unavailable");
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationHeader].FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 128 &&
               incoming.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or ':')
            ? incoming
            : context.TraceIdentifier;
    }

    private static string? ResolveClaim(ClaimsPrincipal principal, string? token, params string[] names)
    {
        var claim = principal.Claims.FirstOrDefault(candidate => names.Any(name =>
            string.Equals(candidate.Type, name, StringComparison.OrdinalIgnoreCase) ||
            candidate.Type.EndsWith($"/{name}", StringComparison.OrdinalIgnoreCase)))?.Value;
        if (!string.IsNullOrWhiteSpace(claim) || string.IsNullOrWhiteSpace(token))
        {
            return claim;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return null;
            return handler.ReadJwtToken(token).Claims.FirstOrDefault(candidate => names.Any(name =>
                string.Equals(candidate.Type, name, StringComparison.OrdinalIgnoreCase) ||
                candidate.Type.EndsWith($"/{name}", StringComparison.OrdinalIgnoreCase)))?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static bool TokenHasPermission(string token, string permission)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.CanReadToken(token) && handler.ReadJwtToken(token).Claims.Any(claim =>
                (string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(claim.Value, permission, StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    private static ContentResult Failure(HttpContext context, int status, string reasonCode)
    {
        var json = JsonSerializer.Serialize(new
        {
            data = (object?)null,
            isSuccessful = false,
            statusCode = status,
            errors = new[] { reasonCode },
            reason_code = reasonCode,
            correlation_id = context.TraceIdentifier
        });
        return new ContentResult { Content = json, ContentType = "application/json", StatusCode = status };
    }
}
