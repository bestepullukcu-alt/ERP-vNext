using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

public sealed record CreateFirstGskuDraftFacadeCommand(
    ProductItemSkuMasterModels.CreateFirstGskuDraftFacadeRequest Request,
    string OperationId)
    : IRequest<Response<ProductItemSkuMasterModels.GskuDraftResponse>>;
