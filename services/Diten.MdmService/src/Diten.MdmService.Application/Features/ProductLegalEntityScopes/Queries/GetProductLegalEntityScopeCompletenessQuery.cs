using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;

public sealed record GetProductLegalEntityScopeCompletenessQuery
    : IRequest<Response<ProductLegalEntityScopeModels.CompletenessDto>>;
