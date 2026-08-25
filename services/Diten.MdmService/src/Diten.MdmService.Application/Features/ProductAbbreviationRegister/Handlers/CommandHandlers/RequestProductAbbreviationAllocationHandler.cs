using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.CommandHandlers;

public sealed class RequestProductAbbreviationAllocationHandler
    : IRequestHandler<RequestProductAbbreviationAllocationCommand, Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public RequestProductAbbreviationAllocationHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationResultDto>> Handle(
        RequestProductAbbreviationAllocationCommand request,
        CancellationToken cancellationToken) => _workflow.RequestAsync(request, cancellationToken);
}
