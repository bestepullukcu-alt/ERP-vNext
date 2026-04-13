namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class KpiTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string StrategicPerspective { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ObjectLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BusinessQuestion { get; set; } = string.Empty;
    public string Polarity { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public string ReportingFrequency { get; set; } = string.Empty;
    public string FormulaType { get; set; } = string.Empty;
    public string NumeratorDefinition { get; set; } = string.Empty;
    public string DenominatorDefinition { get; set; } = string.Empty;
    public string FormulaExpression { get; set; } = string.Empty;
    public string BaselineLogic { get; set; } = string.Empty;
    public string TargetLogic { get; set; } = string.Empty;
    public string ThresholdModelCode { get; set; } = string.Empty;
    public string DefaultOwnerRole { get; set; } = string.Empty;
    public string ReviewRole { get; set; } = string.Empty;
    public string DataSourcePattern { get; set; } = string.Empty;
    public string EvidenceRequirement { get; set; } = string.Empty;
    public string DecisionReferenceRequirement { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string VersionLabel { get; set; } = "v1.0";
    public DateTime? PublishDate { get; set; }
    public string Tags { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public string? LastUsedBy { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiThresholdModelDto
{
    public string Id { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string MetricUnit { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Polarity { get; set; } = string.Empty;
    public decimal? RedFloor { get; set; }
    public decimal? AmberFloor { get; set; }
    public decimal? GreenTarget { get; set; }
    public decimal? GreenStretch { get; set; }
    public decimal? UpperControlLimit { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public string Status { get; set; } = "Published";
    public string VersionLabel { get; set; } = "v1.0";
    public DateTime? PublishDate { get; set; }
}

public sealed class KpiScorecardPackDto
{
    public string Id { get; set; } = string.Empty;
    public string PackCode { get; set; } = string.Empty;
    public string PackName { get; set; } = string.Empty;
    public string PackLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string VersionLabel { get; set; } = "v1.0";
    public DateTime? PublishDate { get; set; }
    public string DefaultOwnerRole { get; set; } = string.Empty;
    public int KpiCount { get; set; }
    public int UsageCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiScorecardPackItemDto
{
    public string Id { get; set; } = string.Empty;
    public string PackId { get; set; } = string.Empty;
    public string PackCode { get; set; } = string.Empty;
    public string KpiTemplateId { get; set; } = string.Empty;
    public string KpiTemplateCode { get; set; } = string.Empty;
    public string KpiTemplateName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string PriorityClass { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
}

public sealed class KpiGovernanceSummaryDto
{
    public int TotalTemplates { get; set; }
    public int Draft { get; set; }
    public int InReview { get; set; }
    public int Approved { get; set; }
    public int Published { get; set; }
    public int Retired { get; set; }
    public int MissingOwner { get; set; }
    public int MissingThreshold { get; set; }
    public int MissingFormula { get; set; }
}

public sealed class KpiGovernanceExceptionDto
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class KpiInstantiateFromTemplateRequestDto
{
    public string TemplateId { get; set; } = string.Empty;
    public bool AllowDuplicates { get; set; }
}

public sealed class KpiLifecycleActionRequestDto
{
    public string Action { get; set; } = string.Empty; // submit-review|approve|publish|retire
}
