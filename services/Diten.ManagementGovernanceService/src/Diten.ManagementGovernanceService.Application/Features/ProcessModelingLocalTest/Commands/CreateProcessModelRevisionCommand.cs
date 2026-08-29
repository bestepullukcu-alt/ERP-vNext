using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record CreateProcessModelRevisionCommand(Guid Id,string Title,string? Description,int ExpectedVersion,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<Guid>>;
