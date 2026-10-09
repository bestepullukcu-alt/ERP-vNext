using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record RetireGlobalProductIdentityCommand(RetireGlobalProductIdentityRequest Request)
    : IRequest<Response<GlobalProductIdentityLifecycleResult>>;
