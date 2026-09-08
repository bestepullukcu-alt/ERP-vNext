using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Commands;

public sealed record DeleteKpiCatalogReadinessCommand(Guid Id) : IRequest<Response<bool>>;
