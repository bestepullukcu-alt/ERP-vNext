using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

/// <summary>Lists all legal entities for the current tenant (frontend selector + auth assignment source).</summary>
public sealed record GetLegalEntitiesQuery() : IRequest<Response<IReadOnlyList<LegalEntityListItemDto>>>;
