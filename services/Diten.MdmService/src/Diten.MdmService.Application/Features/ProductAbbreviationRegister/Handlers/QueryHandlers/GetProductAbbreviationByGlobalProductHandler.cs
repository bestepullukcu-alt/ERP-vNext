using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;

public sealed class GetProductAbbreviationByGlobalProductHandler
    : IRequestHandler<GetProductAbbreviationByGlobalProductQuery, Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public GetProductAbbreviationByGlobalProductHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationRegisterEntryDto>> Handle(
        GetProductAbbreviationByGlobalProductQuery request,
        CancellationToken cancellationToken) => _workflow.GetByGlobalProductAsync(request.GlobalProductId, cancellationToken);
}
