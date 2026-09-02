using MediatR;
namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record GetCatalogTreeQuery(CatalogQueryContext Context) : IRequest<CatalogResponse<CatalogTreeDto>>;
