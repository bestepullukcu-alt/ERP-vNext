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

    [Fact]
    public void All_four_catalog_levels_trim_NFC_normalize_and_bound_text_before_persistence()
    {
        var context = new CatalogCommandContext(Guid.NewGuid(), Guid.NewGuid(), "key", "permission");
        var decomposed = "Cafe\u0301";
        var parent = Guid.NewGuid();
        ICatalogCommand[] commands =
        [
            new CreateProcessArchitectureCommand(Guid.NewGuid(), " order architecture ", $"  {decomposed}  ", "  description  ", 1, context),
            new CreateProcessDomainCommand(Guid.NewGuid(), parent, "sales_ops", $"  {decomposed}  ", "  description  ", 1, context),
            new CreateProcessFamilyCommand(Guid.NewGuid(), parent, "lead management", $"  {decomposed}  ", "  description  ", 1, context),
            new CreateProcessDefinitionCommand(Guid.NewGuid(), parent, "qualify_lead", $"  {decomposed}  ", "  purpose  ", "  description  ", context)
        ];

        var expectedCodes = new[] { "ORDER-ARCHITECTURE", "SALES-OPS", "LEAD-MANAGEMENT", "QUALIFY-LEAD" };
        for (var index = 0; index < commands.Length; index++)
        {
            Assert.Null(CatalogValidation.Command(commands[index], out var normalized));
            Assert.Equal(expectedCodes[index], normalized.Code);
            Assert.Equal("Café", normalized.Name);
            Assert.Equal("description", normalized.Description);
            if (index == 3) Assert.Equal("purpose", normalized.Purpose);
        }
    }

    [Fact]
    public void All_four_catalog_levels_reject_invalid_codes_and_text_bounds()
    {
        var context = new CatalogCommandContext(Guid.NewGuid(), Guid.NewGuid(), "key", "permission");
        var parent = Guid.NewGuid();
        ICatalogCommand[] invalid =
        [
            new CreateProcessArchitectureCommand(Guid.NewGuid(), "", "Name", null, 0, context),
            new CreateProcessArchitectureCommand(Guid.NewGuid(), new string('A', 101), "Name", null, 0, context),
            new CreateProcessArchitectureCommand(Guid.NewGuid(), "BAD@CODE", "Name", null, 0, context),
            new CreateProcessDomainCommand(Guid.NewGuid(), parent, "-DOMAIN", "Name", null, 0, context),
            new CreateProcessDomainCommand(Guid.NewGuid(), parent, "DOMAIN-", "Name", null, 0, context),
            new CreateProcessFamilyCommand(Guid.NewGuid(), parent, "FAMILY--CODE", "Name", null, 0, context),
            new CreateProcessFamilyCommand(Guid.NewGuid(), parent, "FAMILY", new string('N', 201), null, 0, context),
            new CreateProcessDefinitionCommand(Guid.NewGuid(), parent, "PROCESS", "Name", new string('P', 2001), null, context),
            new CreateProcessDefinitionCommand(Guid.NewGuid(), parent, "PROCESS", "Name", null, new string('D', 4001), context),
            new CreateProcessArchitectureCommand(Guid.NewGuid(), "ARCH", "Name", new string('D', 2001), 0, context)
        ];

        Assert.All(invalid, command => Assert.Equal(CatalogErrors.InvalidRequest, CatalogValidation.Command(command)));
    }

}
