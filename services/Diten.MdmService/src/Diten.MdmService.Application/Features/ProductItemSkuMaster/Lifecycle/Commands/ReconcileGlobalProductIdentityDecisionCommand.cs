using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record ReconcileGlobalProductIdentityDecisionCommand(
    ReconcileGlobalProductIdentityDecisionRequest Request)
    : IRequest<Response<GlobalProductIdentityLifecycleResult>>;
