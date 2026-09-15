using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Queries;

public sealed record GetHrDocumentationReadinessByIdQuery(Guid Id) : IRequest<Response<HrDocumentationReadinessDto>>;
