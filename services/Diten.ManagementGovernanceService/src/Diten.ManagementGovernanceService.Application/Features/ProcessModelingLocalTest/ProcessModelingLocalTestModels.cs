namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;

public sealed record ProcessModelActivityInput(Guid LogicalActivityId,string ActivityCode,string Name,string? Description,int SortOrder);
public sealed record ProcessModelControlPointInput(Guid LogicalControlPointId,string ControlCode,string Name,string? Description,Guid? LogicalActivityId,int SortOrder);
public sealed record ProcessModelRelationshipInput(Guid FromActivityId,Guid ToActivityId,string? ConditionLabel,int SortOrder);
