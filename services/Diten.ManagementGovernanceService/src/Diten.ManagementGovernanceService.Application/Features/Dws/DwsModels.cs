using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws;

public sealed record CreateStructureRequest
{
    public ExternalContextReference ExternalContextReference { get; }
    public string Name { get; }
    public string? Description { get; }
    public CreateStructureRequest(ExternalContextReference externalContextReference, string name, string? description)
    {
        ExternalContextReference = externalContextReference ?? throw new DwsValidationException(DwsErrors.InvalidContextReference);
        var metadata = new StructuralMetadata(name, description);
        Name = metadata.Name; Description = metadata.Description;
    }
}

public sealed record UpdateStructureMetadataRequest
{
    public Guid StructureDefinitionId { get; }
    public string Name { get; }
    public string? Description { get; }
    public int ExpectedRevisionVersion { get; }
    public UpdateStructureMetadataRequest(Guid structureDefinitionId, string name, string? description, int expectedRevisionVersion)
    {
        StructureDefinitionId = structureDefinitionId; ExpectedRevisionVersion = expectedRevisionVersion;
        var metadata = new StructuralMetadata(name, description);
        Name = metadata.Name; Description = metadata.Description;
    }
}

public sealed record AddStructureNodeRequest
{
    public Guid StructureDefinitionId { get; }
    public Guid? ParentLogicalNodeId { get; }
    public string Code { get; }
    public string Title { get; }
    public string? Description { get; }
    public int SiblingOrder { get; }
    public int ExpectedRevisionVersion { get; }
    public AddStructureNodeRequest(Guid structureDefinitionId, Guid? parentLogicalNodeId, string code, string title, string? description, int siblingOrder, int expectedRevisionVersion)
    {
        StructureDefinitionId = structureDefinitionId; ParentLogicalNodeId = parentLogicalNodeId;
        Code = DwsText.Required(code, 100); Title = DwsText.Required(title, 300); Description = DwsText.Optional(description, 4000);
        SiblingOrder = siblingOrder; ExpectedRevisionVersion = expectedRevisionVersion;
    }
}
public sealed record MoveStructureNodeRequest(Guid StructureDefinitionId, Guid LogicalNodeId, Guid? NewParentLogicalNodeId, int NewSiblingOrder, int ExpectedRevisionVersion);
public sealed record ReorderStructureNodeRequest(Guid StructureDefinitionId, Guid LogicalNodeId, int SiblingOrder, int ExpectedRevisionVersion);
public sealed record RemoveStructureNodeRequest(Guid StructureDefinitionId, Guid LogicalNodeId, int ExpectedRevisionVersion);
public sealed record AddStructuralDependencyRequest(Guid StructureDefinitionId, Guid FromLogicalNodeId, Guid ToLogicalNodeId, int ExpectedRevisionVersion);
public sealed record RemoveStructuralDependencyRequest(Guid StructureDefinitionId, Guid FromLogicalNodeId, Guid ToLogicalNodeId, int ExpectedRevisionVersion);
public sealed record CreateStructureBaselineRequest(Guid StructureDefinitionId, int ExpectedRevisionVersion);
public sealed record CreateNextStructureRevisionRequest(Guid StructureDefinitionId, int? SourceRevisionNumber, int? SourceBaselineNumber, int ExpectedDefinitionVersion);

public sealed record CreateStructureResult(Guid StructureDefinitionId, int RevisionNumber, int DefinitionVersion, int RevisionVersion);
public sealed record UpdateStructureMetadataResult
{
    public Guid StructureDefinitionId { get; }
    public int RevisionNumber { get; }
    public int RevisionVersion { get; }
    public string OutcomeKind { get; }
    public UpdateStructureMetadataResult(Guid structureDefinitionId, int revisionNumber, int revisionVersion, DwsOutcomeKind outcomeKind)
    {
        StructureDefinitionId = structureDefinitionId; RevisionNumber = revisionNumber; RevisionVersion = revisionVersion;
        OutcomeKind = DwsClosedValues.Outcome(outcomeKind);
    }
}
public sealed record AddStructureNodeResult(Guid StructureDefinitionId, int RevisionNumber, Guid LogicalNodeId, int RevisionVersion);
public sealed record MoveStructureNodeResult
{
    public Guid StructureDefinitionId { get; }
    public int RevisionNumber { get; }
    public Guid LogicalNodeId { get; }
    public Guid? ParentLogicalNodeId { get; }
    public int SiblingOrder { get; }
    public int RevisionVersion { get; }
    public string OutcomeKind { get; }
    public MoveStructureNodeResult(Guid structureDefinitionId, int revisionNumber, Guid logicalNodeId, Guid? parentLogicalNodeId, int siblingOrder, int revisionVersion, DwsOutcomeKind outcomeKind)
    {
        StructureDefinitionId = structureDefinitionId; RevisionNumber = revisionNumber; LogicalNodeId = logicalNodeId;
        ParentLogicalNodeId = parentLogicalNodeId; SiblingOrder = siblingOrder; RevisionVersion = revisionVersion;
        OutcomeKind = DwsClosedValues.Outcome(outcomeKind);
    }
}
public sealed record ReorderStructureNodeResult
{
    public Guid StructureDefinitionId { get; }
    public int RevisionNumber { get; }
    public Guid LogicalNodeId { get; }
    public int SiblingOrder { get; }
    public int RevisionVersion { get; }
    public string OutcomeKind { get; }
    public ReorderStructureNodeResult(Guid structureDefinitionId, int revisionNumber, Guid logicalNodeId, int siblingOrder, int revisionVersion, DwsOutcomeKind outcomeKind)
    {
        StructureDefinitionId = structureDefinitionId; RevisionNumber = revisionNumber; LogicalNodeId = logicalNodeId;
        SiblingOrder = siblingOrder; RevisionVersion = revisionVersion; OutcomeKind = DwsClosedValues.Outcome(outcomeKind);
    }
}
public sealed record RemoveStructureNodeResult(Guid StructureDefinitionId, int RevisionNumber, Guid LogicalNodeId, bool Removed, int RevisionVersion);
public sealed record AddStructuralDependencyResult(Guid StructureDefinitionId, int RevisionNumber, Guid FromLogicalNodeId, Guid ToLogicalNodeId, int RevisionVersion);
public sealed record RemoveStructuralDependencyResult(Guid StructureDefinitionId, int RevisionNumber, Guid FromLogicalNodeId, Guid ToLogicalNodeId, bool Removed, int RevisionVersion);
public sealed record CreateStructureBaselineResult(Guid StructureDefinitionId, int SourceRevisionNumber, int BaselineNumber, string ContentHash, string CanonicalizationVersion, int DefinitionVersion);
public sealed record CreateNextStructureRevisionResult(Guid StructureDefinitionId, int NewRevisionNumber, int DefinitionVersion, int RevisionVersion);

public sealed record StructureSummaryDto(
    Guid StructureDefinitionId,
    ExternalContextReference ExternalContextReference,
    int? CurrentWorkingRevisionNumber,
    int LatestRevisionNumber,
    int DefinitionVersion);

public sealed record StructuralMetadataDto(string Name, string? Description);
public sealed record StructureNodeDto(Guid LogicalNodeId, Guid? ParentLogicalNodeId, string Code, string Title, string? Description, int SiblingOrder);
public sealed record StructuralDependencyDto(Guid FromLogicalNodeId, Guid ToLogicalNodeId);
public sealed record StructureTreeDto(
    StructureSummaryDto Summary,
    int RevisionNumber,
    StructuralMetadataDto Metadata,
    bool IsSealed,
    int RevisionVersion,
    IReadOnlyList<StructureNodeDto> Nodes,
    IReadOnlyList<StructuralDependencyDto> Dependencies);

public enum StructureValidationIssueCode
{
    SelfParent,
    MissingParent,
    HierarchyCycle,
    DuplicateSiblingOrder,
    DuplicateNodeCode,
    MissingDependencyEndpoint,
    DuplicateDependency,
    DependencyCycle
}

public sealed record StructureValidationIssueDto(
    StructureValidationIssueCode Code,
    Guid? LogicalNodeId,
    Guid? RelatedLogicalNodeId);

public sealed record StructureValidationDto(
    Guid StructureDefinitionId,
    int RevisionNumber,
    bool IsValid,
    IReadOnlyList<StructureValidationIssueDto> Issues);

public sealed record StructureNodeDifferenceDto(Guid LogicalNodeId, string Kind);
public sealed record StructuralDependencyDifferenceDto(Guid FromLogicalNodeId, Guid ToLogicalNodeId, string Kind);
public sealed record StructureComparisonDto(
    Guid StructureDefinitionId,
    int LeftRevisionNumber,
    int RightRevisionNumber,
    IReadOnlyList<StructureNodeDifferenceDto> Nodes,
    IReadOnlyList<StructuralDependencyDifferenceDto> Dependencies);

public sealed record BaselineComparisonDto(
    Guid StructureDefinitionId,
    int LeftBaselineNumber,
    string LeftContentHash,
    int RightBaselineNumber,
    string RightContentHash,
    IReadOnlyList<StructureNodeDifferenceDto> Nodes,
    IReadOnlyList<StructuralDependencyDifferenceDto> Dependencies);
