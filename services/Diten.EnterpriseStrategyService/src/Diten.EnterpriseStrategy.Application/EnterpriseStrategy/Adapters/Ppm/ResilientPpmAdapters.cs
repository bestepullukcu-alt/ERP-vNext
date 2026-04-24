using Diten.Application.Common.Models;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.Extensions.Logging;

namespace Diten.Application.EnterpriseStrategy.Adapters.Ppm;

public interface IPpmAdapterHealth
{
    AdapterSyncStatus Status { get; }
}

public sealed class ResilientPpmInitiativeReadAdapter : IPpmInitiativeReadAdapter, IPpmAdapterHealth
{
    private readonly IPpmInitiativeReadAdapter _inner;
    private readonly ILogger<ResilientPpmInitiativeReadAdapter> _logger;
    private readonly IEnterpriseStrategyObservability _observability;
    private IReadOnlyList<PpmInitiativeReadModel> _cached = Array.Empty<PpmInitiativeReadModel>();

    public ResilientPpmInitiativeReadAdapter(
        MockPpmInitiativeReadAdapter inner,
        ILogger<ResilientPpmInitiativeReadAdapter> logger,
        IEnterpriseStrategyObservability observability)
    {
        _inner = inner;
        _logger = logger;
        _observability = observability;
    }

    public AdapterSyncStatus Status { get; } = new() { Dependency = "ppm-initiatives" };

    public async Task<IReadOnlyList<PpmInitiativeReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
            var records = await _inner.ListAsync(page, pageSize, linked.Token);
            _cached = records;
            Status.LastSuccessUtc = DateTime.UtcNow;
            Status.DegradedMode = false;
            _observability.TrackAdapterSync("ppm-initiatives", true);
            return records;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PPM initiative read degraded. Falling back to cache.");
            Status.DegradedMode = true;
            Status.LastErrorCode = EnterpriseStrategyErrorCodes.DependencyUnavailable;
            Status.LastErrorMessage = ex.Message;
            _observability.TrackAdapterSync("ppm-initiatives", false);

            if (_cached.Count > 0)
                return _cached;

            throw new InvalidOperationException("DEPENDENCY_UNAVAILABLE: ppm-initiatives");
        }
    }

    public async Task<PpmInitiativeReadModel?> GetByIdAsync(string initiativeId, CancellationToken cancellationToken = default)
    {
        var rows = await ListAsync(1, 1000, cancellationToken);
        return rows.FirstOrDefault(x => string.Equals(x.InitiativeId, initiativeId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<PpmInitiativeReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ppm_initiative_sync_started correlation_id={CorrelationId}", correlationId);
        var rows = await _inner.SyncAsync(correlationId, cancellationToken);
        _cached = rows;
        Status.LastSuccessUtc = DateTime.UtcNow;
        Status.DegradedMode = false;
        _observability.TrackAdapterSync("ppm-initiatives-sync", true);
        _logger.LogInformation("ppm_initiative_sync_completed correlation_id={CorrelationId} count={Count}", correlationId, rows.Count);
        return rows;
    }
}

public sealed class ResilientPpmProjectReadAdapter : IPpmProjectReadAdapter, IPpmAdapterHealth
{
    private readonly IPpmProjectReadAdapter _inner;
    private readonly ILogger<ResilientPpmProjectReadAdapter> _logger;
    private readonly IEnterpriseStrategyObservability _observability;
    private IReadOnlyList<PpmProjectReadModel> _cached = Array.Empty<PpmProjectReadModel>();

    public ResilientPpmProjectReadAdapter(
        MockPpmProjectReadAdapter inner,
        ILogger<ResilientPpmProjectReadAdapter> logger,
        IEnterpriseStrategyObservability observability)
    {
        _inner = inner;
        _logger = logger;
        _observability = observability;
    }

    public AdapterSyncStatus Status { get; } = new() { Dependency = "ppm-projects" };

    public async Task<IReadOnlyList<PpmProjectReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
            var rows = await _inner.ListAsync(page, pageSize, linked.Token);
            _cached = rows;
            Status.LastSuccessUtc = DateTime.UtcNow;
            Status.DegradedMode = false;
            _observability.TrackAdapterSync("ppm-projects", true);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PPM project read degraded. Falling back to cache.");
            Status.DegradedMode = true;
            Status.LastErrorCode = EnterpriseStrategyErrorCodes.DependencyUnavailable;
            Status.LastErrorMessage = ex.Message;
            _observability.TrackAdapterSync("ppm-projects", false);
            if (_cached.Count > 0)
                return _cached;
            throw new InvalidOperationException("DEPENDENCY_UNAVAILABLE: ppm-projects");
        }
    }

    public async Task<PpmProjectReadModel?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var rows = await ListAsync(1, 1000, cancellationToken);
        return rows.FirstOrDefault(x => string.Equals(x.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<PpmProjectReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ppm_project_sync_started correlation_id={CorrelationId}", correlationId);
        var rows = await _inner.SyncAsync(correlationId, cancellationToken);
        _cached = rows;
        Status.LastSuccessUtc = DateTime.UtcNow;
        Status.DegradedMode = false;
        _observability.TrackAdapterSync("ppm-projects-sync", true);
        _logger.LogInformation("ppm_project_sync_completed correlation_id={CorrelationId} count={Count}", correlationId, rows.Count);
        return rows;
    }
}
