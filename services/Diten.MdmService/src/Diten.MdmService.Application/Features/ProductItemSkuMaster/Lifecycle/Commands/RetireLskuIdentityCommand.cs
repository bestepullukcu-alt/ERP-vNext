using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record RetireLskuIdentityCommand(
    RetireLskuIdentityRequest Request)
    : IRequest<Response<LskuIdentityLifecycleResult>>;
