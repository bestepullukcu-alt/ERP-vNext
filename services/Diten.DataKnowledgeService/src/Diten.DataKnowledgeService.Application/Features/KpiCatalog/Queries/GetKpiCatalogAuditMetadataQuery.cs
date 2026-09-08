using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Queries;

public sealed record GetKpiCatalogAuditMetadataQuery(Guid Id) : IRequest<Response<KpiCatalogAuditMetadataDto>>;
