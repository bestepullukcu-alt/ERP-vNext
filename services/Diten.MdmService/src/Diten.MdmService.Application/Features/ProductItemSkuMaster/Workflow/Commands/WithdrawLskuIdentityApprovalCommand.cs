using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;

public sealed record WithdrawLskuIdentityApprovalCommand(WithdrawLskuIdentityApprovalRequest Request)
    : IRequest<Response<LskuIdentityWorkflowResult>>;
