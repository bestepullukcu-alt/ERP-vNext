using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;

public sealed record EvaluateDataWarehouseLakehouseReadinessCommand(Guid Id) : IRequest<Response<DataWarehouseLakehouseReadinessDto>>;
