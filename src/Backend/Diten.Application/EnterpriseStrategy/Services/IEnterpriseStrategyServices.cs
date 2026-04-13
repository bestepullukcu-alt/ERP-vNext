namespace Diten.Application.EnterpriseStrategy.Services;

public interface IMetricCatalogService
{
    Task<IReadOnlyList<string>> ListMetricDefinitionsAsync(CancellationToken cancellationToken = default);
}

public interface IAuditEvidenceService
{
    Task<IReadOnlyList<string>> ListEvidenceAsync(string objectType, string objectId, CancellationToken cancellationToken = default);
}

public sealed class MockMetricCatalogService : IMetricCatalogService
{
    public Task<IReadOnlyList<string>> ListMetricDefinitionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(new[] { "Revenue", "Margin", "NPS" });
}

public sealed class MockAuditEvidenceService : IAuditEvidenceService
{
    public Task<IReadOnlyList<string>> ListEvidenceAsync(string objectType, string objectId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(new[] { $"EVD-{objectType}-{objectId}" });
}
