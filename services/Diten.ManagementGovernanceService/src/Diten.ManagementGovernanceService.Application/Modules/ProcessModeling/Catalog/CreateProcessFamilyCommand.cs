namespace Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
public sealed record CreateProcessFamilyCommand(Guid Id, Guid ProcessDomainId, string FamilyCode, string Name, string? Description, int SortOrder, CatalogCommandContext Context) : ICatalogCommand
{ public CatalogMutation Mutation => new(CatalogMutationKind.CreateFamily, Id, ProcessDomainId, FamilyCode, Name, null, Description, SortOrder, null); }
