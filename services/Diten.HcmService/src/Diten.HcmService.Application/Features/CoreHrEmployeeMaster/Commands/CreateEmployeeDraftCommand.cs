using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record CreateEmployeeDraftCommand(EmployeeDraftCreateRequest Request)
    : IRequest<Response<EmployeeDraftCreateResponse>>;
