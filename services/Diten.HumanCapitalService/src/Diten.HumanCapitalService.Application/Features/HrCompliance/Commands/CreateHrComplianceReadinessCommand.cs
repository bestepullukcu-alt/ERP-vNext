using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrCompliance.Commands;

public sealed record CreateHrComplianceReadinessCommand(HrComplianceReadinessCreateRequest Request) : IRequest<Response<Guid>>;
