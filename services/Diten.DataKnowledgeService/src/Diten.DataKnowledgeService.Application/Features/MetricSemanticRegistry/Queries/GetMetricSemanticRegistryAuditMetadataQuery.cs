using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Queries;

public sealed record GetMetricSemanticRegistryAuditMetadataQuery(Guid Id) : IRequest<Response<MetricSemanticRegistryAuditMetadataDto>>;
