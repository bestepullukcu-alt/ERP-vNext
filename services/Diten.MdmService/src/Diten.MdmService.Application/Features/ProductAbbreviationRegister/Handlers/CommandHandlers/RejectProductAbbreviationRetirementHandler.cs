using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class RejectProductAbbreviationRetirementHandler
    : IRequestHandler<RejectProductAbbreviationRetirementCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public RejectProductAbbreviationRetirementHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Handle(
        RejectProductAbbreviationRetirementCommand request,
        CancellationToken cancellationToken) => _workflow.RejectRetirementAsync(request, cancellationToken);
}
