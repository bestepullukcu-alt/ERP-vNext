using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGlobalProductByIdQuery(Guid Id)
    : IRequest<Response<ProductItemSkuMasterModels.GlobalProductDetailDto>>;
