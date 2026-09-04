namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record ArchiveProcessDomainCommand(Guid Id, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.ArchiveDomain, Id, null, null, null, null, null, null, ExpectedVersion); }
