using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record UpdateGlobalProductDraftCommand(
    Guid GlobalProductId,
    ProductItemSkuMasterModels.UpdateGlobalProductDraftRequest Request,
    Guid OperationId)
    : IRequest<Response<ProductItemSkuMasterModels.GlobalProductDraftUpdateDto>>;
