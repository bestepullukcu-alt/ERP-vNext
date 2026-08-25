using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands;

public sealed record CreateFirstGskuDraftCommand(ProductItemSkuMasterModels.CreateFirstGskuDraftRequest Request)
    : IRequest<Response<ProductItemSkuMasterModels.FirstGskuDraftDto>>;
