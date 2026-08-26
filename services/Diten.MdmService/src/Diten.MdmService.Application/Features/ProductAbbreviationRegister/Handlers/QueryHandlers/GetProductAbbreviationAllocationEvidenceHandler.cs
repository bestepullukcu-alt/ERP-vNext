using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Queries;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Handlers.QueryHandlers;

public sealed class GetProductAbbreviationAllocationEvidenceHandler
    : IRequestHandler<GetProductAbbreviationAllocationEvidenceQuery, Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>>
{
    private readonly ProductAbbreviationWorkflow _workflow;
    public GetProductAbbreviationAllocationEvidenceHandler(ProductAbbreviationWorkflow workflow) => _workflow = workflow;
    public Task<Response<ProductAbbreviationRegisterModels.ProductAbbreviationAllocationEvidenceDto>> Handle(
        GetProductAbbreviationAllocationEvidenceQuery request,
        CancellationToken cancellationToken) => _workflow.GetEvidenceAsync(request.RegisterEntryId, cancellationToken);
}
