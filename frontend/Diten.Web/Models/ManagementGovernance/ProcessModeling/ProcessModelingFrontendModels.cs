using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.ManagementGovernance.ProcessModeling;

public static class ProcessModelingFrontendPermissions
{
    public const string Read = "management-governance.process-modeling.models.read";
    public const string Create = "management-governance.process-modeling.models.create";
    public const string Update = "management-governance.process-modeling.models.update";
    public const string RequestReview = "management-governance.process-modeling.models.request-review";
    public const string ReturnToDraft = "management-governance.process-modeling.models.return-to-draft";
    public const string Publish = "management-governance.process-modeling.models.publish";
    public const string Retire = "management-governance.process-modeling.models.retire";
    public const string CreateRevision = "management-governance.process-modeling.models.create-revision";

    public static IReadOnlyList<string> ExactVisibleActions { get; } =
    [
        Create,
        Update,
        RequestReview,
        ReturnToDraft,
        Publish,
        Retire,
        CreateRevision
    ];
}

public sealed class ProcessModelingIndexViewModel
{
    public required IReadOnlySet<string> Permissions { get; init; }
    public bool GatewayReady { get; init; }
}

public sealed class ProcessModelingEditorViewModel
{
    public Guid ProcessModelId { get; init; }
    public required IReadOnlySet<string> Permissions { get; init; }
    public bool GatewayReady { get; init; }
}

public sealed class ProcessModelIdentityInput
{
    [Required]
    public Guid? ProcessDefinitionId { get; set; }

    [Required, StringLength(64)]
    public string ModelCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }
}

public sealed record ProcessModelingFrontendFailure(
    int StatusCode,
    string ReasonCode,
    string CorrelationId);
