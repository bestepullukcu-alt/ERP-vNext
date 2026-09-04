namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
internal sealed class ArchiveProcessDomainHandler(ICatalogStore store) : CatalogCommandHandler<ArchiveProcessDomainCommand>(store);
