using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Queries;

public sealed record GetDataWarehouseLakehouseReadinessListQuery : IRequest<Response<IReadOnlyList<DataWarehouseLakehouseReadinessListItemDto>>>;
