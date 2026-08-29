using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record CreateProcessArchitectureCommand(string ArchitectureCode,string Name,string? Description,int SortOrder,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<Guid>>;
