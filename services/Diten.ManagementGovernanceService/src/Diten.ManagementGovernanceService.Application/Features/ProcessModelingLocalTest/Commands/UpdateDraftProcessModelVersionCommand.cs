using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record UpdateDraftProcessModelVersionCommand(Guid Id,string Title,string? Description,IReadOnlyList<ProcessModelActivityInput> Activities,IReadOnlyList<ProcessModelControlPointInput> ControlPoints,IReadOnlyList<ProcessModelRelationshipInput> Relationships,int ExpectedVersion,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<NoContent>>;
