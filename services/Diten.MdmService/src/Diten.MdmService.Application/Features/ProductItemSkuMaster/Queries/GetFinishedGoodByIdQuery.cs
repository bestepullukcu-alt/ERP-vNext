using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetFinishedGoodByIdQuery(Guid Id)
    : IRequest<Response<ProductItemSkuMasterModels.FinishedGoodDetailDto>>;
