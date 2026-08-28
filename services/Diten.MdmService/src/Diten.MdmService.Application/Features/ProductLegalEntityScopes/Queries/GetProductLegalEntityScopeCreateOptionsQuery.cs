using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes.Queries;

public sealed record GetProductLegalEntityScopeCreateOptionsQuery(Guid GlobalProductId)
    : IRequest<Response<ProductLegalEntityScopeModels.CreateOptionsDto>>;
