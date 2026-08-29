using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record RetireFinishedGoodIdentityCommand(
    RetireFinishedGoodIdentityRequest Request)
    : IRequest<Response<FinishedGoodIdentityLifecycleResult>>;
