using MediatR;
namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
internal sealed class GetProcessDefinitionByIdHandler(ICatalogStore store) : IRequestHandler<GetProcessDefinitionByIdQuery, CatalogResponse<ProcessDefinitionDto>>
{
    public Task<CatalogResponse<ProcessDefinitionDto>> Handle(GetProcessDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var error = CatalogValidation.Query(request.Context, request.Id);
        return error is null ? store.GetDefinitionAsync(request.Id, request.Context, cancellationToken) : Task.FromResult(CatalogResponse<ProcessDefinitionDto>.Fail(error, 400));
    }
}
