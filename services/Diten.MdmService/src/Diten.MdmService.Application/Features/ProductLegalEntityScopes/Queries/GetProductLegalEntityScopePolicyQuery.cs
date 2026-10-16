using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;

public sealed record GetProductLegalEntityScopePolicyQuery(Guid GlobalProductId)
    : IRequest<Response<ProductLegalEntityScopeModels.PolicyDto>>;
