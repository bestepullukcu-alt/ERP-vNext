namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record CreateProcessDomainCommand(Guid Id, Guid ProcessArchitectureId, string DomainCode, string Name, string? Description, int SortOrder, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.CreateDomain, Id, ProcessArchitectureId, DomainCode, Name, null, Description, SortOrder, null); }
