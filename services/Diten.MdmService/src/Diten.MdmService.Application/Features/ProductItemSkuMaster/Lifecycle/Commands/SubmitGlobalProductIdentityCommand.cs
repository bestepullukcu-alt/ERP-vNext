using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record SubmitGlobalProductIdentityCommand(SubmitGlobalProductIdentityRequest Request)
    : IRequest<Response<GlobalProductIdentityLifecycleResult>>;
