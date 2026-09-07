using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;

public sealed record GetProductLegalEntityScopeHistoryQuery(Guid GlobalProductId)
    : IRequest<Response<IReadOnlyList<ProductLegalEntityScopeModels.PeriodDto>>>;
