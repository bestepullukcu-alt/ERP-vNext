using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.Catalog;

public sealed class CatalogCommandContractTests
{
    [Fact]
    public void Twelve_commands_preserve_exact_entity_specific_code_and_parent_fields()
    {
        var context = new CatalogCommandContext(Guid.NewGuid(), Guid.NewGuid(), "key", "permission");
        var architecture = new CreateProcessArchitectureCommand(Guid.NewGuid(), "ARCH", "Architecture", null, 1, context).Mutation;
        var domainId = Guid.NewGuid();
        var domain = new CreateProcessDomainCommand(Guid.NewGuid(), domainId, "DOM", "Domain", null, 2, context).Mutation;
        var familyId = Guid.NewGuid();
        var definition = new CreateProcessDefinitionCommand(Guid.NewGuid(), familyId, "PROC", "Process", null, null, context).Mutation;

        Assert.Equal(CatalogMutationKind.CreateArchitecture, architecture.Kind);
        Assert.Equal("ARCH", architecture.Code);
        Assert.Null(architecture.ParentId);
        Assert.Equal(CatalogMutationKind.CreateDomain, domain.Kind);
        Assert.Equal(domainId, domain.ParentId);
        Assert.Equal("DOM", domain.Code);
        Assert.Equal(CatalogMutationKind.CreateDefinition, definition.Kind);
        Assert.Equal(familyId, definition.ParentId);
        Assert.Equal("PROC", definition.Code);
    }

}
