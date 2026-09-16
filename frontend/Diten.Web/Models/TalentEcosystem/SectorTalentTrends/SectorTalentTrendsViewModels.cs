using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.TalentEcosystem.SectorTalentTrends;

// Enum field values map 1:1 to the backend SectorTalentTrendsReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class SectorTalentTrendsEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string SourceContractVersion { get; set; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long SectorTalentTrendsReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int SectorTalentTrendsReadinessState { get; set; } = 0;                     // Draft
    public int TrendCatalogBoundaryState { get; set; } = 3;                  // Blocked
    public int SignalBindingIntakeBoundaryState { get; set; } = 3;     // Blocked
    public int AggregationScopeBoundaryState { get; set; } = 3;              // Blocked
    public int VisibilityControlBoundaryState { get; set; } = 3;            // Blocked
    public int TrendReviewBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int TalentDataSourceDependencyState { get; set; } = 2;               // Deferred
    public int WorkforceAnalyticsSourceDependencyState { get; set; } = 2;           // Deferred
    public int DataGovernancePolicyDependencyState { get; set; } = 2;                    // Deferred
    public int NotificationDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int ConsentPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int PublicationPolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class SectorTalentTrendsDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SectorTalentTrendsReadinessState { get; set; }
    public int TrendCatalogBoundaryState { get; set; }
    public int SignalBindingIntakeBoundaryState { get; set; }
    public int AggregationScopeBoundaryState { get; set; }
    public int VisibilityControlBoundaryState { get; set; }
    public int TrendReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int TalentDataSourceDependencyState { get; set; }
    public int WorkforceAnalyticsSourceDependencyState { get; set; }
    public int DataGovernancePolicyDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long SectorTalentTrendsReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring SectorTalentTrendsReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match SectorTalentTrendsReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class SectorTalentTrendsSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SectorTalentTrendsReadinessState { get; set; }
    public int TrendCatalogBoundaryState { get; set; }
    public int SignalBindingIntakeBoundaryState { get; set; }
    public int AggregationScopeBoundaryState { get; set; }
    public int VisibilityControlBoundaryState { get; set; }
    public int TrendReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int TalentDataSourceDependencyState { get; set; }
    public int WorkforceAnalyticsSourceDependencyState { get; set; }
    public int DataGovernancePolicyDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int ConsentPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long SectorTalentTrendsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
