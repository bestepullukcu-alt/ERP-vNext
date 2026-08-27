using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;

public sealed record CreateProcessFamilyCommand(Guid ProcessDomainId,string FamilyCode,string Name,string? Description,int SortOrder,ProcessModelingLocalExecutionRequest Execution) : IRequest<Response<Guid>>;
