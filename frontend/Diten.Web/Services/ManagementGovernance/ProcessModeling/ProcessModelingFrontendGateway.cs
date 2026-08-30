using System.Net.Http.Headers;
using Diten.Web.Models.ManagementGovernance.ProcessModeling;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.ManagementGovernance.ProcessModeling;

/// <summary>
/// Default-off MVC-to-Gateway seam for MOD-0355. The browser never receives a token or tenant header.
/// No downstream route is inferred: enabling requires a separately approved Gateway route and explicit configuration.
/// </summary>
public sealed class ProcessModelingFrontendGateway(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<ProcessModelingFrontendGateway> logger)
{
    public const string NotReadyReason = "process_modeling_frontend_gateway_not_ready";
    private readonly bool _enabled = configuration.GetValue<bool>("ManagementGovernance:ProcessModelingFrontend:Enabled");
    private readonly string? _readBasePath = configuration["ManagementGovernance:ProcessModelingFrontend:GatewayReadPath"];
    private readonly string? _gatewayUrl = configuration["GatewayUrl"];

    public bool IsReady => _enabled
        && !string.IsNullOrWhiteSpace(_gatewayUrl)
        && !string.IsNullOrWhiteSpace(_readBasePath)
        && _readBasePath.StartsWith("/", StringComparison.Ordinal)
        && !_readBasePath.Contains("5017", StringComparison.Ordinal);

    public Task<ProcessModelingFrontendProxyResult> GetAsync(
        HttpRequest incoming,
        string relativePath,
        CancellationToken cancellationToken) =>
        SendAsync(incoming, HttpMethod.Get, relativePath, cancellationToken);

    public Task<ProcessModelingFrontendProxyResult> SendAsync(
        HttpRequest incoming,
        HttpMethod method,
        string relativePath,
        CancellationToken cancellationToken) =>
        SendAsync(incoming, method, relativePath, method == HttpMethod.Get ? null : incoming.Body, cancellationToken);

    private async Task<ProcessModelingFrontendProxyResult> SendAsync(
        HttpRequest incoming,
        HttpMethod method,
        string relativePath,
        Stream? body,
        CancellationToken cancellationToken)
    {
        if (!IsReady)
            return ProcessModelingFrontendProxyResult.NotReady(incoming.HttpContext.TraceIdentifier);

        if (!TryResolveTenant(incoming.HttpContext.User, out var tenantId))
            return ProcessModelingFrontendProxyResult.Failure(404, "process_modeling_tenant_not_found", incoming.HttpContext.TraceIdentifier);

        var token = AuthTokenCookies.GetAccessToken(incoming);
        if (string.IsNullOrWhiteSpace(token))
            return ProcessModelingFrontendProxyResult.Failure(401, "process_modeling_unauthorized", incoming.HttpContext.TraceIdentifier);

        var basePath = _readBasePath!.TrimEnd('/');
        var suffix = relativePath.TrimStart('/');
        using var request = new HttpRequestMessage(method, $"{_gatewayUrl!.TrimEnd('/')}{basePath}/{suffix}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", incoming.HttpContext.TraceIdentifier);
        ForwardSafeHeader(incoming, request, "Idempotency-Key");
        ForwardSafeHeader(incoming, request, "If-Match");
        ForwardSafeHeader(incoming, request, "X-Expected-Version");
        if (body is not null)
        {
            request.Content = new StreamContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new((int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? "application/json", content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProcessModelingFrontendProxyResult.Failure(503, "process_modeling_frontend_gateway_timeout", incoming.HttpContext.TraceIdentifier);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "MOD-0355 frontend proxy is unavailable.");
            return ProcessModelingFrontendProxyResult.Failure(503, "process_modeling_frontend_gateway_unavailable", incoming.HttpContext.TraceIdentifier);
        }
    }

    private static void ForwardSafeHeader(HttpRequest incoming, HttpRequestMessage outgoing, string name)
    {
        if (incoming.Headers.TryGetValue(name, out var value) && value.Count == 1)
            outgoing.Headers.TryAddWithoutValidation(name, value[0]);
    }

    private static bool TryResolveTenant(System.Security.Claims.ClaimsPrincipal user, out Guid tenantId)
    {
        var raw = user.Claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, "tenantId", StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase)
            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(raw, out tenantId) && tenantId != Guid.Empty;
    }
}

public sealed record ProcessModelingFrontendProxyResult(int StatusCode, string ContentType, string Content)
{
    public static ProcessModelingFrontendProxyResult NotReady(string correlationId) =>
        Failure(503, ProcessModelingFrontendGateway.NotReadyReason, correlationId);

    public static ProcessModelingFrontendProxyResult Failure(int statusCode, string reasonCode, string correlationId) =>
        new(statusCode, "application/json", System.Text.Json.JsonSerializer.Serialize(new
        {
            data = (object?)null,
            isSuccessful = false,
            statusCode,
            errors = Array.Empty<string>(),
            reason_code = reasonCode,
            correlation_id = correlationId
        }));
}
