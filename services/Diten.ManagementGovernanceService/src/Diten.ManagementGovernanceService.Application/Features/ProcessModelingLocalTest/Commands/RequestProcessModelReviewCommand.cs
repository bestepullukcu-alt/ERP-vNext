using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record RequestProcessModelReviewCommand(Guid Id,int ExpectedVersion,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<NoContent>>;
