namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record UpdateProcessDomainCommand(Guid Id, string Name, string? Description, int SortOrder, int ExpectedVersion, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.UpdateDomain, Id, null, null, Name, null, Description, SortOrder, ExpectedVersion); }
