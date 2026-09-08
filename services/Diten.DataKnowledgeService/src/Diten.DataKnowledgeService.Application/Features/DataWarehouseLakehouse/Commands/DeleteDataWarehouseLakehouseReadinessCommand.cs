using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;

public sealed record DeleteDataWarehouseLakehouseReadinessCommand(Guid Id) : IRequest<Response<bool>>;
