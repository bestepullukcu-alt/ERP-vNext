namespace Diten.Application.Commands.EnterpriseStrategyCommands;

public sealed class CreateStrategyLinkedProjectPayloadDto
{
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned";
    public string Phase { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string? PriorityCode { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? SuccessMetric { get; set; }
    public string? MetricBaseline { get; set; }
    public string? MetricTarget { get; set; }
    public string? EntityScopeCode { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string? BudgetTypeCode { get; set; }
    public string? CurrencyCode { get; set; }
    public string? BudgetBasisCode { get; set; }
    public decimal? CapexAmount { get; set; }
    public decimal? OpexAmount { get; set; }
    public string? BudgetSummary { get; set; }
    public string? RiskRating { get; set; }
    public string? ReadinessStatus { get; set; }
    public string? DeliveryCompanyId { get; set; }
    public string? FundingCompanyId { get; set; }
    public string? ScopeModeCode { get; set; }
    public List<string> ApplicableCompanyIds { get; set; } = new();
    public string? Notes { get; set; }
}
