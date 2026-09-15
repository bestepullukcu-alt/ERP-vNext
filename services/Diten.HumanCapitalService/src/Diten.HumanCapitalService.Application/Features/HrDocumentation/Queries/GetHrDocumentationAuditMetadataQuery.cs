using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.HrDocumentation.Queries;

public sealed record GetHrDocumentationAuditMetadataQuery(Guid Id) : IRequest<Response<HrDocumentationAuditMetadataDto>>;
