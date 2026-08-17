using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Diten.Platform.API.Observability;

public sealed class RabbitMqReadinessHealthCheck : IHealthCheck
{
    private const string SectionName = "Eventing";
    private readonly IConfiguration _configuration;

    public RabbitMqReadinessHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var eventing = _configuration.GetSection(SectionName);
        var transport = eventing["Transport"];
        if (!string.Equals(transport, "RabbitMQ", StringComparison.OrdinalIgnoreCase))
        {
            return HealthCheckResult.Healthy("RabbitMQ transport is not enabled.");
        }

        var host = eventing["Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            return HealthCheckResult.Unhealthy("RabbitMQ host is not configured.");
        }

        var port = eventing.GetValue("Port", 5672);
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port, cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ TCP endpoint is reachable.");
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ TCP endpoint is not reachable.");
        }
    }
}
