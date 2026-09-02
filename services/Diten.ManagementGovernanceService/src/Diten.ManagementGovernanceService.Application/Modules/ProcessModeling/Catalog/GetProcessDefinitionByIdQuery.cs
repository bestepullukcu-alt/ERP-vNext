using MediatR;
namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record GetProcessDefinitionByIdQuery(Guid Id, CatalogQueryContext Context) : IRequest<CatalogResponse<ProcessDefinitionDto>>;
