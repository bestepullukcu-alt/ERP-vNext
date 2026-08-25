using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class RequestProductAbbreviationRetirementHandler
    : IRequestHandler<RequestProductAbbreviationRetirementCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public RequestProductAbbreviationRetirementHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Handle(
        RequestProductAbbreviationRetirementCommand request,
        CancellationToken cancellationToken) => _workflow.RequestRetirementAsync(request, cancellationToken);
}
