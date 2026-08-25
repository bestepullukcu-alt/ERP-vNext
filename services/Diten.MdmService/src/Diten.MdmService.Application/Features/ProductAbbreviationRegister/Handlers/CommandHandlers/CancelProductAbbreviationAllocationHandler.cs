using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class CancelProductAbbreviationAllocationHandler
    : IRequestHandler<CancelProductAbbreviationAllocationCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public CancelProductAbbreviationAllocationHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Handle(
        CancelProductAbbreviationAllocationCommand request,
        CancellationToken cancellationToken) => _workflow.CancelAsync(request, cancellationToken);
}
