using Diten.MdmService.Application.Common;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record RetireGskuIdentityPairCommand(RetireGskuIdentityPairRequest Request)
    : IRequest<Response<GskuPairRetirementResult>>;
