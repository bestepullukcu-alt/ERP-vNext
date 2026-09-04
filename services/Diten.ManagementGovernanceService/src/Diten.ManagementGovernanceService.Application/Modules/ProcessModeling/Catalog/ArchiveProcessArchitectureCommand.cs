namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record ArchiveProcessArchitectureCommand(Guid Id, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.ArchiveArchitecture, Id, null, null, null, null, null, null, ExpectedVersion); }
