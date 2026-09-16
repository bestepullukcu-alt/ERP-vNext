using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCaseManagement.Queries;

public sealed record GetHrCaseManagementReadinessListQuery : IRequest<Response<IReadOnlyList<HrCaseManagementReadinessListItemDto>>>;
