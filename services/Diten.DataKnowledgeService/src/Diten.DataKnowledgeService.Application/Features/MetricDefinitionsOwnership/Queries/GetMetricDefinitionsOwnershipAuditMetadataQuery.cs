using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Queries;

public sealed record GetMetricDefinitionsOwnershipAuditMetadataQuery(Guid Id) : IRequest<Response<MetricDefinitionsOwnershipAuditMetadataDto>>;
