using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Common.Observability;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteSanitizedAsync(HttpContext context, HealthReport report)
    {
        var options = context.RequestServices.GetService(typeof(IOptions<ObservabilityOptions>)) as IOptions<ObservabilityOptions>;
        var serviceName = options?.Value.ServiceName ?? "Diten.Service";

        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            service = serviceName,
            timestampUtc = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = SensitiveDataRedactor.Redact(entry.Value.Description)
            })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
