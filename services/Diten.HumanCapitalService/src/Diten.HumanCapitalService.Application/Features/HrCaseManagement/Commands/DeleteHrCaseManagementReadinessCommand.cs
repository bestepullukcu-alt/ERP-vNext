using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement.Commands;

public sealed record DeleteHrCaseManagementReadinessCommand(Guid Id) : IRequest<Response<bool>>;
