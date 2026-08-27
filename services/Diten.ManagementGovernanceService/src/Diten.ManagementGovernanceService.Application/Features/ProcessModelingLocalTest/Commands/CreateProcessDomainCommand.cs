using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record CreateProcessDomainCommand(Guid ProcessArchitectureId,string DomainCode,string Name,string? Description,int SortOrder,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<Guid>>;
