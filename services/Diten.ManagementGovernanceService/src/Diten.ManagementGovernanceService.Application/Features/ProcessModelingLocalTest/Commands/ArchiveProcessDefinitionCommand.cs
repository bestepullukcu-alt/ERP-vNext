using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record ArchiveProcessDefinitionCommand(Guid Id,int ExpectedVersion,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<NoContent>>;
