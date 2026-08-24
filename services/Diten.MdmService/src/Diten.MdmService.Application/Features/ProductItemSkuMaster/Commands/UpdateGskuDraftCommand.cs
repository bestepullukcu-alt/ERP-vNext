using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

public sealed record UpdateGskuDraftCommand(ProductItemSkuMasterModels.UpdateGskuDraftRequest Request)
    : IRequest<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>>;
