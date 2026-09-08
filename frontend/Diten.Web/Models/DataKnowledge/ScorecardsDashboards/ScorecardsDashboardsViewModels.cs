using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.DataKnowledge.ScorecardsDashboards;

// Enum field values map 1:1 to the backend ScorecardsDashboardsReadinessState ordinals:
// Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5.
// Enums are serialized as integers by the service (no JsonStringEnumConverter), so the
// save payload carries integers and the gateway binds them to the enum.

public sealed class ScorecardsDashboardsEditViewModel
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
    public long ScorecardsDashboardsReadinessVersion { get; set; } = 1;

    // Readiness & workflow
    public int ScorecardsDashboardsReadinessState { get; set; } = 0;                     // Draft
    public int ScorecardCatalogBoundaryState { get; set; } = 3;                  // Blocked
    public int WidgetBindingIntakeBoundaryState { get; set; } = 3;     // Blocked
    public int LayoutScopeBoundaryState { get; set; } = 3;              // Blocked
    public int PublicationControlBoundaryState { get; set; } = 3;            // Blocked
    public int DashboardReviewBoundaryState { get; set; } = 3;                   // Blocked
    public int AutomatedDecisionBoundaryState { get; set; } = 3;              // Blocked

    // Dependencies
    public int MetricSemanticRegistrySourceDependencyState { get; set; } = 2;               // Deferred
    public int DataWarehouseSourceDependencyState { get; set; } = 2;           // Deferred
    public int DataContractRegistryDependencyState { get; set; } = 2;                    // Deferred
    public int NotificationDependencyState { get; set; } = 2;                // Deferred

    // Governance / policy
    public int StewardshipPreconditionState { get; set; } = 2; // Deferred
    public int DataMinimizationState { get; set; } = 2;    // Deferred
    public int RetentionPolicyState { get; set; } = 2;     // Deferred
    public int PublicationPolicyState { get; set; } = 2;      // Deferred

    public string? DeferredReason { get; set; }
}

public sealed class ScorecardsDashboardsDetailViewModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ScorecardsDashboardsReadinessState { get; set; }
    public int ScorecardCatalogBoundaryState { get; set; }
    public int WidgetBindingIntakeBoundaryState { get; set; }
    public int LayoutScopeBoundaryState { get; set; }
    public int PublicationControlBoundaryState { get; set; }
    public int DashboardReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int MetricSemanticRegistrySourceDependencyState { get; set; }
    public int DataWarehouseSourceDependencyState { get; set; }
    public int DataContractRegistryDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int StewardshipPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long ScorecardsDashboardsReadinessVersion { get; set; }
    public DateTimeOffset? LastEvaluatedAt { get; set; }
    public string? DeferredReason { get; set; }

    // Ordinal → canonical state name, mirroring ScorecardsDashboardsReadinessState.
    private static readonly string[] StateNames =
        ["Draft", "Ready", "Deferred", "Blocked", "NotRequired", "Archived"];

    public static string StateName(int value) =>
        value >= 0 && value < StateNames.Length ? StateNames[value] : value.ToString();
}

// Sent to the gateway create endpoint. Property names match ScorecardsDashboardsReadinessCreateRequest.
// This module has NO manager/employee assessment UX fields; every scalar request field is included.
public sealed class ScorecardsDashboardsSavePayload
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int ScorecardsDashboardsReadinessState { get; set; }
    public int ScorecardCatalogBoundaryState { get; set; }
    public int WidgetBindingIntakeBoundaryState { get; set; }
    public int LayoutScopeBoundaryState { get; set; }
    public int PublicationControlBoundaryState { get; set; }
    public int DashboardReviewBoundaryState { get; set; }
    public int AutomatedDecisionBoundaryState { get; set; }
    public int MetricSemanticRegistrySourceDependencyState { get; set; }
    public int DataWarehouseSourceDependencyState { get; set; }
    public int DataContractRegistryDependencyState { get; set; }
    public int NotificationDependencyState { get; set; }
    public int StewardshipPreconditionState { get; set; }
    public int DataMinimizationState { get; set; }
    public int RetentionPolicyState { get; set; }
    public int PublicationPolicyState { get; set; }
    public string SourceContractVersion { get; set; } = string.Empty;
    public long ScorecardsDashboardsReadinessVersion { get; set; } = 1;
    public string? DeferredReason { get; set; }
}

public sealed class GatewayResponse<T>
{
    public T? Data { get; set; }
    public bool IsSuccessful { get; set; }
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = [];
}
