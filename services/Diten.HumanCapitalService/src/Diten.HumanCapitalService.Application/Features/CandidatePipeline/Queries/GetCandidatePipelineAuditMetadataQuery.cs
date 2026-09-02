using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Queries;

public sealed record GetCandidatePipelineAuditMetadataQuery(Guid Id) : IRequest<Response<CandidatePipelineAuditMetadataDto>>;
