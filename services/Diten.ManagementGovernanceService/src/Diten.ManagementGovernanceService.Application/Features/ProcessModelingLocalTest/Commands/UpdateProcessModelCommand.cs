using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record UpdateProcessModelCommand(Guid Id,string Name,string? Description,int ExpectedVersion,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<NoContent>>;
