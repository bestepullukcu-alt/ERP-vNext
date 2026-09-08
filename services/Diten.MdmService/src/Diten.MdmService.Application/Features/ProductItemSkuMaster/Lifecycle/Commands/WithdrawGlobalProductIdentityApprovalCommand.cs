using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

public sealed record WithdrawGlobalProductIdentityApprovalCommand(
    WithdrawGlobalProductIdentityApprovalRequest Request)
    : IRequest<Response<GlobalProductIdentityWorkflowResult>>;
