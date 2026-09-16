using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess.Commands;

public sealed record ValidateSensitiveAccessPolicyCommand(SensitiveAccessPolicyValidationRequest Request)
    : IRequest<Response<SensitiveAccessPolicyValidationDto>>;
