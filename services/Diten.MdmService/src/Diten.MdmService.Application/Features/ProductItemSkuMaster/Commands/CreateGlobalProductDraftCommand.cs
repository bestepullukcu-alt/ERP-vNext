using Diten.MdmService.Application.Features.ProductItemSkuMaster;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

public sealed record CreateGlobalProductDraftCommand(
    ProductItemSkuMasterModels.CreateGlobalProductDraftRequest Request)
    : IRequest<Response<ProductItemSkuMasterModels.GlobalProductDraftDto>>;
