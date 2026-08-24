using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;

public sealed record GetGskuByIdQuery(Guid Id)
    : IRequest<Response<ProductItemSkuMasterModels.GskuDetailDto>>;
