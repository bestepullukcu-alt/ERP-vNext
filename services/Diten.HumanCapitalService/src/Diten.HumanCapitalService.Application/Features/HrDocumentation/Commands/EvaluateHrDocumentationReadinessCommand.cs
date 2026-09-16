using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;

public sealed record EvaluateHrDocumentationReadinessCommand(Guid Id) : IRequest<Response<HrDocumentationReadinessDto>>;
