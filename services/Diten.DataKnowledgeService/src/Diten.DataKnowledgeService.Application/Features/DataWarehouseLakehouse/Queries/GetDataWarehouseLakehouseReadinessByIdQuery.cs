using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Queries;

public sealed record GetDataWarehouseLakehouseReadinessByIdQuery(Guid Id) : IRequest<Response<DataWarehouseLakehouseReadinessDto>>;
