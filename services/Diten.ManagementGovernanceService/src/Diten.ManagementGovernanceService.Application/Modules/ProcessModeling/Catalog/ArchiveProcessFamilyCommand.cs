namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record ArchiveProcessFamilyCommand(Guid Id, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.ArchiveFamily, Id, null, null, null, null, null, null, ExpectedVersion); }
