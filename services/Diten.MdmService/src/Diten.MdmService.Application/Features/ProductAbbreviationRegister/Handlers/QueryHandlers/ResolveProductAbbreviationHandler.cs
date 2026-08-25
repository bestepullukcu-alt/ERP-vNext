using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;

public sealed class ResolveProductAbbreviationHandler
    : IRequestHandler<ResolveProductAbbreviationQuery, Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public ResolveProductAbbreviationHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationResolutionDto>> Handle(
        ResolveProductAbbreviationQuery request,
        CancellationToken cancellationToken) => _workflow.ResolveAsync(request.Abbreviation, cancellationToken);
}
