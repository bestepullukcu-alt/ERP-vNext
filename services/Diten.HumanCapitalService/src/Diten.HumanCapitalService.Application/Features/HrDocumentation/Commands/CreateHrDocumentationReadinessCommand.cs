using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;

public sealed record CreateHrDocumentationReadinessCommand(HrDocumentationReadinessCreateRequest Request) : IRequest<Response<Guid>>;
