using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record CreateProcessModelCommand(Guid ProcessDefinitionId,string ModelCode,string Name,string? Description,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<Guid>>;
