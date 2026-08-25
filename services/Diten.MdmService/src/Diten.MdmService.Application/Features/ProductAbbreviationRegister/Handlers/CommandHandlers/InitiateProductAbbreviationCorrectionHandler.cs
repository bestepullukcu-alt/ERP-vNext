using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class InitiateProductAbbreviationCorrectionHandler
    : IRequestHandler<InitiateProductAbbreviationCorrectionCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public InitiateProductAbbreviationCorrectionHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>> Handle(
        InitiateProductAbbreviationCorrectionCommand request,
        CancellationToken cancellationToken) => _workflow.InitiateCorrectionAsync(request, cancellationToken);
}
