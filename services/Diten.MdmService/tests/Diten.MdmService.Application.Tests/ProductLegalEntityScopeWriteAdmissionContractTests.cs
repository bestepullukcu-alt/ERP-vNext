using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using MediatR;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductLegalEntityScopeWriteAdmissionContractTests
{
    private static readonly string[] ExactMutationCommands =
    [
        "ReserveCanonicalCodeCommand", "CreateGlobalProductDraftCommand",
        "CreateFirstGskuDraftFacadeCommand", "CreateFirstGskuDraftCommand", "UpdateGskuDraftCommand",
        "CreateLskuDraftCommand", "CreateFinishedGoodDraftCommand",
        "RequestProductAbbreviationAllocationCommand", "ApproveProductAbbreviationAllocationCommand",
        "RejectProductAbbreviationAllocationCommand", "CancelProductAbbreviationAllocationCommand",
        "InitiateProductAbbreviationCorrectionCommand", "RequestProductAbbreviationRetirementCommand",
        "ApproveProductAbbreviationRetirementCommand", "RejectProductAbbreviationRetirementCommand",
        "CreateProductLegalEntityScopePolicyCommand", "ReplaceProductLegalEntityScopePolicyCommand",
        "EndProductLegalEntityScopePolicyCommand"
    ];

    [Fact]
    public void Mutation_inventory_is_exactly_the_frozen_18_commands_and_has_no_unclassified_nineteenth_writer()
    {
        var marker = typeof(IProductLegalEntityScopeInventoryMutation);
        var applicationTypes = marker.Assembly.GetTypes();
        var commandNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands",
            "Diten.MdmService.Application.Features.ProductAbbreviationRegister.Commands",
            "Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands"
        };
        var discoveredCommands = applicationTypes
            .Where(type => type.IsClass && !type.IsAbstract
                && type.Namespace is not null && commandNamespaces.Contains(type.Namespace)
                && typeof(IBaseRequest).IsAssignableFrom(type))
            .ToArray();
        var unclassified = discoveredCommands
            .Where(type => !marker.IsAssignableFrom(type))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var actual = discoveredCommands
            .Where(marker.IsAssignableFrom)
            .Where(type => type.IsClass && !type.IsAbstract && marker.IsAssignableFrom(type))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(unclassified.Length == 0,
            "Unclassified product inventory command(s): " + string.Join(", ", unclassified));
        Assert.Equal(18, actual.Length);
        Assert.Equal(ExactMutationCommands.OrderBy(name => name, StringComparer.Ordinal), actual);
        Assert.Equal(18, ExactMutationCommands.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Write_fence_behavior_is_registered_after_validation_and_before_audit_forwarding()
    {
        var source = ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs");
        var validation = source.IndexOf("ValidationBehavior", StringComparison.Ordinal);
        var fence = source.IndexOf("ProductLegalEntityScopeWriteFenceBehavior", StringComparison.Ordinal);
        var audit = source.IndexOf("AuditForwardingBehavior", StringComparison.Ordinal);

        Assert.True(validation >= 0 && fence > validation && audit > fence,
            $"Unexpected pipeline order: validation={validation}, fence={fence}, audit={audit}");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return File.ReadAllText(Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
