using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.LegalEntity.Queries;

/// <summary>Tenant-scoped lookup list of Active (referenceable) legal entities for dropdown/Select2 surfaces.</summary>
public sealed record GetLegalEntitiesLookupQuery() : IRequest<Response<IReadOnlyList<LegalEntityLookupDto>>>;
