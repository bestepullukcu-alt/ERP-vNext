using System.Text.Json;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Application.Features.BrandProductContract.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.BrandProductContract.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.MdmService.Application.Tests.BrandProduct;

// MOD-0290-FU02 — contract + surface-shape gates (pack §22.1 items 26, 28 and the forbidden-flag guard).
public sealed class BrandProductContractTests
{
    private static async Task<BrandProductContractDto> LoadAsync()
    {
        var response = await new GetBrandProductContractHandler()
            .Handle(new GetBrandProductContractQuery(), CancellationToken.None);
        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    // Gate 26 — all eight published capabilities are true.
    [Fact]
    public async Task Contract_publishes_the_eight_authorized_flags_as_true()
    {
        var contract = await LoadAsync();

        Assert.True(contract.IsReady);
        Assert.True(contract.Features.SupportsBrandManagement);
        Assert.True(contract.Features.SupportsProductManagement);
        Assert.True(contract.Features.SupportsBrandProductReference);
        Assert.True(contract.Features.SupportsBrandProductHierarchy);
        Assert.True(contract.Features.SupportsExternalReferences);
        Assert.True(contract.Features.SupportsArchiveLifecycle);
        Assert.True(contract.Features.SupportsEffectiveDating);
        Assert.True(contract.Features.SupportsContractDrivenUi);
        Assert.Equal(8, typeof(BrandProductFeaturesDto).GetProperties().Length);
    }

    // Gate 27 — forbidden flags must be ABSENT from the serialized payload, not merely false. A consumer must
    // not be able to discover a capability this module does not own.
    [Fact]
    public async Task Contract_never_mentions_a_forbidden_flag()
    {
        var contract = await LoadAsync();
        var json = JsonSerializer.Serialize(contract, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        string[] forbidden =
        [
            "supportsCampaignRuntime", "supportsCampaignEngine", "supportsKnowledgeRuntime",
            "supportsVisitPlanning", "supportsRoutePlanning", "supportsFrequencyRuntime",
            "supportsRecommendationEngine", "supportsWorkflowApproval", "supportsDigitalDetailing",
            "supportsSegmentation", "supportsAtcLocalMaster", "supportsTherapeuticAreaFlatReferenceSet",
            "supportsIndicationMaster", "supportsItemSku", "supportsUomMapping", "supportsImportExport",
            "supportsHardDelete", "supportsMultiBrand"
        ];

        foreach (var flag in forbidden)
        {
            Assert.DoesNotContain(flag, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Contract_publishes_vocabulary_reason_codes_and_permissions()
    {
        var contract = await LoadAsync();

        Assert.Equal(["draft", "active", "inactive", "archived"], contract.Vocabulary.BrandStatuses);
        Assert.Equal(["draft", "active", "inactive", "archived"], contract.Vocabulary.ProductStatuses);
        Assert.DoesNotContain("discontinued", contract.Vocabulary.ProductStatuses);
        Assert.Contains("medicine", contract.Vocabulary.ProductTypes);
        Assert.NotEmpty(contract.Vocabulary.DosageForms);
        Assert.NotEmpty(contract.Vocabulary.UnitsOfMeasure);

        Assert.Contains(BrandProductReasonCodes.BrandCodeDuplicate, contract.ReasonCodes);
        Assert.Contains(BrandProductReasonCodes.BrandArchived, contract.ReasonCodes);

        Assert.Equal(
            ["mdm.brands.read", "mdm.brands.create", "mdm.brands.update", "mdm.brands.archive",
             "mdm.products.read", "mdm.products.create", "mdm.products.update", "mdm.products.archive"],
            contract.Permissions);

        // Limitations are stated so the UI can disable rather than fake.
        Assert.Contains("hard-delete-not-supported", contract.Limitations);
        Assert.Contains("atc-code-is-external-taxonomy-pointer-no-local-master", contract.Limitations);
    }

    // Gate 28 — no HTTP DELETE exists on either controller, at any route.
    [Theory]
    [InlineData(typeof(BrandsController))]
    [InlineData(typeof(ProductsController))]
    public void Controllers_expose_no_delete_verb(Type controller)
    {
        var deleteActions = controller.GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(HttpDeleteAttribute), inherit: true).Length > 0)
            .ToList();

        Assert.Empty(deleteActions);
    }

    // Every action is permission-guarded with a canonical PKS-001 key in the mdm.* namespace.
    [Theory]
    [InlineData(typeof(BrandsController), "mdm.brands.")]
    [InlineData(typeof(ProductsController), "mdm.products.")]
    public void Every_action_is_guarded_by_a_canonical_permission(Type controller, string expectedPrefix)
    {
        var actions = controller.GetMethods()
            .Where(m => m.DeclaringType == controller && m.IsPublic && !m.IsSpecialName)
            .ToList();

        Assert.NotEmpty(actions);
        foreach (var action in actions)
        {
            var permission = action
                .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true)
                .Cast<HasPermissionAttribute>()
                .SingleOrDefault();

            // HasPermissionAttribute stores the key as the policy name: "Permission:{key}".
            Assert.NotNull(permission);
            Assert.StartsWith($"Permission:{expectedPrefix}", permission!.Policy, StringComparison.Ordinal);
        }
    }
}
