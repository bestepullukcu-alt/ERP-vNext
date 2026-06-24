namespace Diten.Platform.API.Models.DocumentManagement;

public sealed class InstantiationScopeApiRequest
{
    public Guid CompanyId { get; set; }
    public Guid? PlantId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public string? InstanceToken { get; set; }
}

public sealed class InstantiationActionRequest
{
    public Guid BaselineReleaseId { get; set; }
    public InstantiationScopeApiRequest Scope { get; set; } = new();
    public string? SelectionMode { get; set; }
    public IReadOnlyList<string> SelectedCanonicalIds { get; set; } = [];
    public bool IncludeDescendants { get; set; } = true;
    public bool IncludeRequiredAncestors { get; set; } = true;
}

public sealed class RetryInstantiationRequest
{
    public IReadOnlyList<string> NodeKeys { get; set; } = [];
}
