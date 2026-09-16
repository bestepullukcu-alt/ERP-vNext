using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Queries;

public sealed record GetHrDocumentationReadinessListQuery : IRequest<Response<IReadOnlyList<HrDocumentationReadinessListItemDto>>>;
