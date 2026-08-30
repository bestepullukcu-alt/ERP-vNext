using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

public sealed record ProcessModelUpdateInput([property: Required, StringLength(200)] string Name, [property: StringLength(4000)] string? Description, [property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record ExpectedVersionInput([property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record ProcessModelRevisionInput([property: Required, StringLength(200)] string Title, [property: StringLength(4000)] string? Description, [property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record ProcessModelDraftInput([property: Required, StringLength(200)] string Title, [property: StringLength(4000)] string? Description,
    [property: JsonRequired] IReadOnlyList<ProcessModelActivityInput> Activities, [property: JsonRequired] IReadOnlyList<ProcessModelControlPointInput> ControlPoints,
    [property: JsonRequired] IReadOnlyList<ProcessModelRelationshipInput> Relationships, [property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion);
public sealed record ProcessModelActivityInput(Guid LogicalActivityId, [property: Required] string ActivityCode, [property: Required] string Name, string? Description, int SortOrder);
public sealed record ProcessModelControlPointInput(Guid LogicalControlPointId, [property: Required] string ControlCode, [property: Required] string Name, string? Description, Guid? LogicalActivityId, int SortOrder);
public sealed record ProcessModelRelationshipInput(Guid FromLogicalActivityId, Guid ToLogicalActivityId, string? ConditionLabel, int SortOrder);
