using System.Net.Http.Headers;
using Diten.Web.Services.Auth;

namespace Diten.Web.Services.ManagementGovernance.ProcessModeling;

public sealed class ProcessModelingFrontendGateway(HttpClient httpClient, IConfiguration configuration, ILogger<ProcessModelingFrontendGateway> logger)
{
    public const string NotReadyReason = "process_modeling_frontend_gateway_not_ready";
    private static readonly Uri AllowedOrigin = new("http://127.0.0.1:5017/");
    private readonly bool _enabled = configuration.GetValue<bool>("ManagementGovernance:ProcessModelingFrontend:LocalTestEnabled");
    private readonly string? _configuredOrigin = configuration["ManagementGovernance:ProcessModelingFrontend:LocalTestOrigin"];
    public bool IsReady => _enabled && TryGetOrigin(out _);
    public Task<ProcessModelingFrontendProxyResult> GetAsync(HttpRequest request, string path, CancellationToken ct) => SendAsync(request, HttpMethod.Get, path, null, ct);
    public Task<ProcessModelingFrontendProxyResult> PostAsync(HttpRequest request, string path, HttpContent content, CancellationToken ct) => SendAsync(request, HttpMethod.Post, path, content, ct);
    public Task<ProcessModelingFrontendProxyResult> PutAsync(HttpRequest request, string path, HttpContent content, CancellationToken ct) => SendAsync(request, HttpMethod.Put, path, content, ct);

    private async Task<ProcessModelingFrontendProxyResult> SendAsync(HttpRequest incoming, HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        if (!IsReady || !TryGetOrigin(out var origin) || !IsSafeRelativePath(path)) return ProcessModelingFrontendProxyResult.NotReady(incoming.HttpContext.TraceIdentifier);
        if (!TryResolveTenant(incoming.HttpContext.User, out var tenant)) return ProcessModelingFrontendProxyResult.Failure(404, "process_modeling_tenant_not_found", incoming.HttpContext.TraceIdentifier);
        var token = AuthTokenCookies.GetAccessToken(incoming);
        if (string.IsNullOrWhiteSpace(token)) return ProcessModelingFrontendProxyResult.Failure(401, "process_modeling_unauthorized", incoming.HttpContext.TraceIdentifier);
        using var downstream = new HttpRequestMessage(method, new Uri(origin, $"internal/local-test/v1/process-modeling/{path}")) { Content = content };
        downstream.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        downstream.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant.ToString("D"));
        downstream.Headers.TryAddWithoutValidation("X-Correlation-Id", incoming.HttpContext.TraceIdentifier);
        if (method != HttpMethod.Get)
        {
            if (!incoming.Headers.TryGetValue("Idempotency-Key", out var key) || key.Count != 1 || string.IsNullOrWhiteSpace(key[0])) return ProcessModelingFrontendProxyResult.Failure(400, "idempotency_key_required", incoming.HttpContext.TraceIdentifier);
            downstream.Headers.TryAddWithoutValidation("Idempotency-Key", key[0].ToString());
        }
        try
        {
            using var response = await httpClient.SendAsync(downstream, HttpCompletionOption.ResponseHeadersRead, ct);
            return new((int)response.StatusCode, response.Content.Headers.ContentType?.ToString() ?? "application/json", await response.Content.ReadAsStringAsync(ct));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return ProcessModelingFrontendProxyResult.Failure(503, "process_modeling_frontend_gateway_timeout", incoming.HttpContext.TraceIdentifier); }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "MOD-0355 local-test proxy is unavailable.");
            return ProcessModelingFrontendProxyResult.Failure(503, "process_modeling_frontend_gateway_unavailable", incoming.HttpContext.TraceIdentifier);
        }
    }

    private bool TryGetOrigin(out Uri origin) { origin = AllowedOrigin; return Uri.TryCreate(_configuredOrigin, UriKind.Absolute, out var configured) && configured == AllowedOrigin; }
    private static bool IsSafeRelativePath(string value) => !string.IsNullOrWhiteSpace(value) && !value.StartsWith('/') && !value.Contains("..", StringComparison.Ordinal) && !value.Contains('\\') && !Uri.TryCreate(value, UriKind.Absolute, out _);
    private static bool TryResolveTenant(System.Security.Claims.ClaimsPrincipal user, out Guid tenant)
    {
        var raw = user.Claims.FirstOrDefault(x => string.Equals(x.Type, "tenantId", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(raw, out tenant) && tenant != Guid.Empty;
    }
}

public sealed record ProcessModelingFrontendProxyResult(int StatusCode, string ContentType, string Content)
{
    public static ProcessModelingFrontendProxyResult NotReady(string id) => Failure(503, ProcessModelingFrontendGateway.NotReadyReason, id);
    public static ProcessModelingFrontendProxyResult Failure(int status, string reason, string id) => new(status, "application/json", System.Text.Json.JsonSerializer.Serialize(new { data = (object?)null, isSuccessful = false, statusCode = status, errors = Array.Empty<string>(), reason_code = reason, correlation_id = id }));
}
