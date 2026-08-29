using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record ArchiveProcessArchitectureCommand(Guid Id,int ExpectedVersion,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<NoContent>>;
