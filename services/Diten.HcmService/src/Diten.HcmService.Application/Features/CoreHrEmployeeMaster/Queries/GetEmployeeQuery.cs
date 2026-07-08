using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;

public sealed record GetEmployeeQuery(Guid EmployeeId) : IRequest<Response<EmployeeDetailResponse>>;
