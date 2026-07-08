using Diten.HcmService.Application.Common.Models;
using MediatR;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;

public sealed record CleanupEmployeeSmokeFixtureCommand(bool IsLocalFixtureEnabled)
    : IRequest<Response<EmployeeSmokeFixtureCleanupResponse>>;
