using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class ApproveProductAbbreviationRetirementHandler
    : IRequestHandler<ApproveProductAbbreviationRetirementCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public ApproveProductAbbreviationRetirementHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Handle(
        ApproveProductAbbreviationRetirementCommand request,
        CancellationToken cancellationToken) => _workflow.ApproveRetirementAsync(request, cancellationToken);
}
