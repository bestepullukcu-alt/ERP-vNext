using MediatR;
namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
internal sealed class GetCatalogTreeHandler(ICatalogStore store) : IRequestHandler<GetCatalogTreeQuery, CatalogResponse<CatalogTreeDto>>
{
    public Task<CatalogResponse<CatalogTreeDto>> Handle(GetCatalogTreeQuery request, CancellationToken cancellationToken)
    {
        var error = CatalogValidation.Query(request.Context);
        return error is null ? store.GetTreeAsync(request.Context, cancellationToken) : Task.FromResult(CatalogResponse<CatalogTreeDto>.Fail(error, 400));
    }
}
