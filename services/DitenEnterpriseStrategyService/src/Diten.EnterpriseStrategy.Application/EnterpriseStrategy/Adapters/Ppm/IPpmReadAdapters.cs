namespace Diten.Application.EnterpriseStrategy.Adapters.Ppm;

public sealed class PpmInitiativeReadModel
{
    public string InitiativeId { get; set; } = string.Empty;
    public string InitiativeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string WaveOrPhase { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string PrimaryKpi { get; set; } = string.Empty;
    public string BudgetEnvelope { get; set; } = string.Empty;
    public string Maturity { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = "ppm";
    public DateTime? SourceUpdatedAt { get; set; }
}

public sealed class PpmProjectReadModel
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string DeliveryType { get; set; } = string.Empty;
    public string SuccessMetric { get; set; } = string.Empty;
    public string RiskRating { get; set; } = string.Empty;
    public string ReadinessStatus { get; set; } = string.Empty;
    public string BudgetSummary { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = "ppm";
    public DateTime? SourceUpdatedAt { get; set; }
}

public interface IPpmInitiativeReadAdapter
{
    Task<IReadOnlyList<PpmInitiativeReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PpmInitiativeReadModel?> GetByIdAsync(string initiativeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PpmInitiativeReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default);
}

public interface IPpmProjectReadAdapter
{
    Task<IReadOnlyList<PpmProjectReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<PpmProjectReadModel?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PpmProjectReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default);
}

public sealed class MockPpmInitiativeReadAdapter : IPpmInitiativeReadAdapter
{
    private readonly List<PpmInitiativeReadModel> _data =
    [
        new() { InitiativeId = "init-001", InitiativeName = "Operational Excellence Wave 1", Description = "Workflow simplification", Owner = string.Empty, Status = "Active", Type = "Transformation", WaveOrPhase = "Wave 1", Priority = "High", Complexity = "High", PrimaryKpi = "Cycle Time", BudgetEnvelope = "$2.2M", Maturity = "Executing", SourceUpdatedAt = DateTime.UtcNow.AddDays(-1) },
        new() { InitiativeId = "init-002", InitiativeName = "Customer Insights Platform", Description = "CDP foundation", Owner = string.Empty, Status = "Active", Type = "Technology", WaveOrPhase = "Wave 2", Priority = "Medium", Complexity = "Medium", PrimaryKpi = "NPS", BudgetEnvelope = "$1.4M", Maturity = "Planning", SourceUpdatedAt = DateTime.UtcNow.AddDays(-2) }
    ];

    public Task<IReadOnlyList<PpmInitiativeReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var rows = _data.Skip(Math.Max(0, (page - 1) * pageSize)).Take(Math.Max(1, pageSize)).ToList();
        return Task.FromResult<IReadOnlyList<PpmInitiativeReadModel>>(rows);
    }

    public Task<PpmInitiativeReadModel?> GetByIdAsync(string initiativeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_data.FirstOrDefault(x => string.Equals(x.InitiativeId, initiativeId, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<PpmInitiativeReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PpmInitiativeReadModel>>(_data.ToList());
}

public sealed class MockPpmProjectReadAdapter : IPpmProjectReadAdapter
{
    private readonly List<PpmProjectReadModel> _data =
    [
        new() { ProjectId = "prj-001", ProjectName = "ERP Upgrade", Description = "Finance and operations upgrade", OwnerPm = string.Empty, Sponsor = "CFO", Status = "Active", Phase = "Execution", DeliveryType = "Program", SuccessMetric = "Close Cycle", RiskRating = "Medium", ReadinessStatus = "Ready", BudgetSummary = "$6.8M", SourceUpdatedAt = DateTime.UtcNow.AddDays(-1) },
        new() { ProjectId = "prj-002", ProjectName = "Sales Workflow Automation", Description = "CRM process automation", OwnerPm = string.Empty, Sponsor = "CRO", Status = "Active", Phase = "Execution", DeliveryType = "Project", SuccessMetric = "Lead Time", RiskRating = "Low", ReadinessStatus = "Ready", BudgetSummary = "$1.1M", SourceUpdatedAt = DateTime.UtcNow.AddDays(-3) }
    ];

    public Task<IReadOnlyList<PpmProjectReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var rows = _data.Skip(Math.Max(0, (page - 1) * pageSize)).Take(Math.Max(1, pageSize)).ToList();
        return Task.FromResult<IReadOnlyList<PpmProjectReadModel>>(rows);
    }

    public Task<PpmProjectReadModel?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_data.FirstOrDefault(x => string.Equals(x.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<PpmProjectReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PpmProjectReadModel>>(_data.ToList());
}
