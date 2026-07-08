using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record EnsureEmployeeSmokeFixtureCommand(bool IsLocalFixtureEnabled)
    : IRequest<Response<EmployeeSmokeFixtureResponse>>;
