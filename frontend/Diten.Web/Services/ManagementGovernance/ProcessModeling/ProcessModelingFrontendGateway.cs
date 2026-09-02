using System.Text.Json;

namespace Diten.Web.Services.ManagementGovernance.ProcessModeling;

/// <summary>
/// Default-off boundary for the bounded MOD-0355 local-test frontend.
/// No credential, tenant header, service address or transport is owned by this slice.
/// </summary>
public sealed class ProcessModelingFrontendGateway
{
    public const string NotReadyReason = "process_modeling_frontend_gateway_not_ready";

    public ProcessModelingFrontendGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ProcessModelingFrontendGateway> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
    }

    public bool IsReady => false;

    public Task<ProcessModelingFrontendProxyResult> GetAsync(
        HttpRequest request,
        string path,
        CancellationToken cancellationToken) =>
        NotReadyAsync(request, path, cancellationToken);

    public Task<ProcessModelingFrontendProxyResult> PostAsync(
        HttpRequest request,
        string path,
        HttpContent content,
        CancellationToken cancellationToken) =>
        NotReadyAsync(request, path, cancellationToken);

    public Task<ProcessModelingFrontendProxyResult> PutAsync(
        HttpRequest request,
        string path,
        HttpContent content,
        CancellationToken cancellationToken) =>
        NotReadyAsync(request, path, cancellationToken);

    private static Task<ProcessModelingFrontendProxyResult> NotReadyAsync(
        HttpRequest request,
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(ProcessModelingFrontendProxyResult.Failure(
                StatusCodes.Status400BadRequest,
                "process_modeling_bad_request",
                request.HttpContext.TraceIdentifier));

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ProcessModelingFrontendProxyResult.NotReady(
            request.HttpContext.TraceIdentifier));
    }
}

public sealed record ProcessModelingFrontendProxyResult(
    int StatusCode,
    string ContentType,
    string Content)
{
    public static ProcessModelingFrontendProxyResult NotReady(string correlationId) =>
        Failure(StatusCodes.Status503ServiceUnavailable, ProcessModelingFrontendGateway.NotReadyReason, correlationId);

    public static ProcessModelingFrontendProxyResult Failure(
        int statusCode,
        string reasonCode,
        string correlationId) =>
        new(
            statusCode,
            "application/json",
            JsonSerializer.Serialize(new
            {
                data = (object?)null,
                isSuccessful = false,
                statusCode,
                errors = Array.Empty<string>(),
                reason_code = reasonCode,
                correlation_id = correlationId
            }));
}
