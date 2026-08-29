using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record CreateProcessDefinitionCommand(Guid ProcessFamilyId,string ProcessCode,string Name,string? Purpose,string? Description,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<Guid>>;
