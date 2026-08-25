using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

public sealed record CreateLskuDraftCommand(
    ProductItemSkuMasterModels.CreateLskuDraftRequest Request)
    : IRequest<Response<ProductItemSkuMasterModels.LskuDraftDto>>;
