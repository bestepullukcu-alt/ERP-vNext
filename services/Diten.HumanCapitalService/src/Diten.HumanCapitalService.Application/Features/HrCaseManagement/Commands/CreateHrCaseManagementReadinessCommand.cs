using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement.Commands;

public sealed record CreateHrCaseManagementReadinessCommand(HrCaseManagementReadinessCreateRequest Request) : IRequest<Response<Guid>>;
