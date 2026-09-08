using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Queries;

public sealed record GetDataWarehouseLakehouseAuditMetadataQuery(Guid Id) : IRequest<Response<DataWarehouseLakehouseAuditMetadataDto>>;
