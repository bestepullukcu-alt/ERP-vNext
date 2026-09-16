using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.Succession.Queries;

public sealed record GetSuccessionAuditMetadataQuery(Guid Id) : IRequest<Response<SuccessionAuditMetadataDto>>;
