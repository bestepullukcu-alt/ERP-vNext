using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

public sealed record CreateFinishedGoodDraftCommand(
    ProductItemSkuMasterModels.CreateFinishedGoodDraftRequest Request)
    : IRequest<Response<ProductItemSkuMasterModels.FinishedGoodDraftDto>>;
