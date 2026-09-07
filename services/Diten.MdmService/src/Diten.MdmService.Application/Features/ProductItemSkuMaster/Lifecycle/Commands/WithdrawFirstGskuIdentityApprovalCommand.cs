using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record WithdrawFirstGskuIdentityApprovalCommand(
    WithdrawFirstGskuIdentityApprovalRequest Request)
    : IRequest<Response<FirstGskuIdentityLifecycleResult>>;
