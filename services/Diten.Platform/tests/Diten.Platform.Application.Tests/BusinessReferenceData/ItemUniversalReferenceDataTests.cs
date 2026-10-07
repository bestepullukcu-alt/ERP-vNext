using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 (MOD-0048-FU01 §4): ITEM-UNIVERSAL-V1, its resolve handler and its controller's trust gate.
/// </summary>
public sealed class ItemUniversalReferenceDataTests
{
    // ------------------------------------------------------------------------------------------- catalog

    [Fact]
    public void The_catalog_holds_exactly_the_three_item_sets_each_with_its_own_version()
    {
        Assert.Equal(new[] { "item-uom", "storage-condition", "material-type" }, ItemUniversalCatalog.SetCodes);
        Assert.Equal(ItemUniversalCatalog.SetCodes.Order(StringComparer.Ordinal),
            ItemUniversalCatalog.Values.Select(x => x.SetCode).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(3, ItemUniversalCatalog.SetCodes.Select(ItemUniversalCatalog.GetVersionId).Distinct().Count());
        Assert.DoesNotContain(ItemUniversalCatalog.SetCodes.Select(ItemUniversalCatalog.GetVersionId),
            id => id == VerifiedGskuUniversalCatalog.UomCatalogVersionId
                  || id == VerifiedGskuUniversalCatalog.PackApplicabilityCatalogVersionId);
        Assert.Equal(ItemUniversalCatalog.Values.Count,
            ItemUniversalCatalog.Values.Select(x => (x.SetCode, x.ValueCode)).Distinct().Count());
    }

    // O-2: every storage condition names its kind; MDM reads it, never a code list.
    [Fact]
    public void Every_storage_condition_carries_its_condition_kind()
    {
        var storage = ItemUniversalCatalog.Values.Where(x => x.SetCode == "storage-condition").ToList();
        Assert.Equal(new[] { "AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE" },
            storage.Select(x => x.ValueCode));
        Assert.All(storage, value => Assert.Contains(value.Attributes[ItemUniversalCatalog.ConditionKind],
            new[] { ItemUniversalCatalog.Temperature, ItemUniversalCatalog.Protection }));
        Assert.Equal(3, storage.Count(x => x.Attributes[ItemUniversalCatalog.ConditionKind] == ItemUniversalCatalog.Temperature));
    }

    // MOD-0290-FU04 O-21 (UN/ECE Rec 20 Rev 11e, measured): roll = XRO; no single-unit "international unit" in V1.
    [Fact]
    public void The_item_units_are_the_agreed_rec_20_codes_with_dimension_precision_and_factor()
    {
        var units = ItemUniversalCatalog.Values.Where(x => x.SetCode == "item-uom").ToList();
        Assert.Equal(new[] { "C62", "MC", "MGM", "GRM", "KGM", "MLT", "LTR", "MTR", "MTK", "XBX", "XCS", "XRO" },
            units.Select(x => x.ValueCode));
        Assert.All(units, unit =>
        {
            var dimension = unit.Attributes[ItemUniversalCatalog.DimensionCode];
            Assert.Contains(dimension, new[] { "COUNT", "MASS", "VOLUME", "LENGTH", "AREA", "PACKAGE" });
            Assert.True(int.Parse(unit.Attributes[ItemUniversalCatalog.MaximumDecimalPrecision]!) >= 0);
            Assert.Equal(dimension == "PACKAGE", unit.Attributes[ItemUniversalCatalog.FactorToDimensionBase] is null);
        });
        Assert.DoesNotContain(units, x => x.ValueCode is "RO" or "HIU" or "IU");
    }

    // FU01 §4: the five `uom` values appear in `item-uom` with the same meaning — one code never carries two.
    [Fact]
    public void The_five_gsku_units_mean_the_same_in_item_uom()
    {
        var dimensions = new Dictionary<string, string>
            { ["C62"] = "COUNT", ["GRM"] = "MASS", ["KGM"] = "MASS", ["MLT"] = "VOLUME", ["LTR"] = "VOLUME" };
        foreach (var uom in VerifiedGskuUniversalCatalog.Uoms)
        {
            var item = Assert.Single(ItemUniversalCatalog.Values, x => x.SetCode == "item-uom" && x.ValueCode == uom.Code);
            Assert.Equal(uom.MaximumDecimalPrecision.ToString(), item.Attributes[ItemUniversalCatalog.MaximumDecimalPrecision]);
            Assert.Equal(dimensions[uom.Code], item.Attributes[ItemUniversalCatalog.DimensionCode]);
        }
        Assert.Equal("1000", Ratio("KGM", "GRM"));
        Assert.Equal("1000", Ratio("LTR", "MLT"));
    }

    // product-master-bundle v1.1 MaterialType (1.1.0-draft.4, measured on the CT line at 0318dc453 — v1.1 is not on this
    // branch yet; when it is, this reads the contract file). One for one; a new value needs a new contract version.
    [Fact]
    public void The_material_types_are_the_contracts_six_values()
    {
        string[] contract = ["ACTIVE_INGREDIENT", "EXCIPIENT", "RAW_MATERIAL", "PACKAGING_PRIMARY", "PACKAGING_SECONDARY", "INTERMEDIATE"];
        Assert.Equal(contract, ItemUniversalCatalog.Values.Where(x => x.SetCode == "material-type").Select(x => x.ValueCode));
    }

    // ------------------------------------------------------------------------------------------- handler

    [Fact]
    public async Task One_call_resolves_every_set_with_its_attributes()
    {
        var result = await Handle(Sel("item-uom", "KGM"), Sel("storage-condition", "COOL_2_8"), Sel("material-type", "EXCIPIENT"));

        Assert.True(result.IsSuccessful);
        Assert.Equal(3, result.Data!.Selections.Count);
        Assert.All(result.Data.Selections, x =>
        {
            Assert.Equal("ITEM-UNIVERSAL-V1", x.CatalogVersion);
            Assert.Equal(ItemUniversalCatalog.GetVersionId(x.SetCode), x.CatalogVersionId);
            Assert.Equal(1, x.CatalogVersionNumber);
            Assert.Equal("LATEST", x.ResolutionMode);
            Assert.False(x.IsRetired);
            Assert.True(x.SelectableForNew);
        });
        Assert.Equal("TEMPERATURE", result.Data.Selections[1].Attributes["ConditionKind"]);
        Assert.Equal("MASS", result.Data.Selections[0].Attributes["DimensionCode"]);
    }

    // 200 is the ceiling: 200 distinct (unknown) codes reach the value check (404), the 201st is a batch refusal (400).
    [Fact]
    public async Task Two_hundred_selections_are_allowed_and_the_201st_is_refused()
    {
        var two = Enumerable.Range(1, ItemUniversalCatalog.MaximumSelections).Select(i => Sel("item-uom", $"U{i:000}")).ToArray();
        var allowed = await Handle(two);
        var refused = await Handle([.. two, Sel("item-uom", "U201")]);

        Assert.Equal(404, allowed.StatusCode);
        Assert.Equal(ItemUniversalCatalog.MaximumSelections, allowed.Data!.NotFound.Count);
        Assert.Equal(400, refused.StatusCode);
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.BatchLimitExceeded, refused.ReasonCode);
    }

    [Fact]
    public async Task An_unknown_value_is_named_with_every_missing_pair()
    {
        var result = await Handle(Sel("item-uom", "KGM"), Sel("item-uom", "LBR"), Sel("storage-condition", "BELOW_30"));

        Assert.Equal(404, result.StatusCode);
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.ValueNotFound, result.ReasonCode);
        Assert.Equal(new[] { ("item-uom", "LBR"), ("storage-condition", "BELOW_30") },
            result.Data!.NotFound.Select(x => (x.SetCode, x.ValueCode)));
        Assert.Empty(result.Data.Selections);
    }

    [Theory]
    [InlineData("set", ResolveItemUniversalReferenceDataHandler.SetNotSupported)]
    [InlineData("gsku-set", ResolveItemUniversalReferenceDataHandler.SetNotSupported)]
    [InlineData("mode", ResolveItemUniversalReferenceDataHandler.ContractInvalid)]
    [InlineData("duplicate", ResolveItemUniversalReferenceDataHandler.ContractInvalid)]
    [InlineData("null", ResolveItemUniversalReferenceDataHandler.ContractInvalid)]
    [InlineData("empty", ResolveItemUniversalReferenceDataHandler.BatchLimitExceeded)]
    public async Task A_malformed_request_is_400_by_name(string fault, string code)
    {
        ItemUniversalResolveSelectionInput?[] selections = fault switch
        {
            "set" => [Sel("market", "TR")],
            "gsku-set" => [Sel("uom", "KGM")],
            "mode" => [new("item-uom", "KGM", "PINNED")],
            "duplicate" => [Sel("item-uom", "KGM"), Sel("item-uom", "KGM")],
            "null" => [Sel("item-uom", "KGM"), null],
            _ => []
        };
        var result = await Handle(selections);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(code, result.ReasonCode);
    }

    [Fact]
    public async Task Without_a_tenant_nothing_resolves()
    {
        var result = await new ResolveItemUniversalReferenceDataHandler(new TenantContext(), TimeProvider.System)
            .Handle(new ResolveItemUniversalReferenceDataQuery([Sel("item-uom", "KGM")]), CancellationToken.None);
        Assert.Equal(403, result.StatusCode);
    }

    // ------------------------------------------------------------------------------------------- controller trust gate

    // No new trust path: the controller is the verified-reference executor's — a request it does not authenticate is
    // refused by name and never reaches the handler (strict mediator).
    [Theory]
    [InlineData("no-credential", 401)]
    [InlineData("forbidden-credential", 403)]
    [InlineData("no-token", 401)]
    [InlineData("both-tokens", 403)]
    public async Task An_unauthenticated_request_never_reaches_the_lists(string fault, int status)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var (controller, _) = Controller(mediator, fault);

        var result = await controller.Resolve(new ItemUniversalResolveRequest { Selections = [Body("item-uom", "KGM")] },
            CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_authenticated_service_resolves_under_its_tenant_and_restores_the_prior_scope()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var (controller, tenants) = Controller(mediator, "service");
        mediator.Setup(x => x.Send(It.IsAny<ResolveItemUniversalReferenceDataQuery>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(ServiceTenant, tenants.TenantId))
            .ReturnsAsync(Response<ItemUniversalResolveResult>.Success(new ItemUniversalResolveResult([], [])));

        var result = await controller.Resolve(new ItemUniversalResolveRequest { Selections = [Body("item-uom", "KGM")] },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PriorTenant, tenants.TenantId);
    }

    [Theory]
    [InlineData("query")]
    [InlineData("extra-field")]
    [InlineData("extra-selection-field")]
    [InlineData("no-selections")]
    public async Task A_body_outside_the_contract_is_400_before_dispatch(string fault)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var (controller, _) = Controller(mediator, "service");
        if (fault == "query") controller.HttpContext.Request.QueryString = new QueryString("?tenantId=x");
        var extra = new Dictionary<string, System.Text.Json.JsonElement> { ["tenant_id"] = default };
        var request = fault switch
        {
            "extra-field" => new ItemUniversalResolveRequest { Selections = [Body("item-uom", "KGM")], AdditionalFields = extra },
            "extra-selection-field" => new ItemUniversalResolveRequest
            {
                Selections = [new ItemUniversalResolveSelectionRequest
                    { SetCode = "item-uom", ValueCode = "KGM", ResolutionMode = "LATEST", AdditionalFields = extra }]
            },
            "no-selections" => new ItemUniversalResolveRequest(),
            _ => new ItemUniversalResolveRequest { Selections = [Body("item-uom", "KGM")] }
        };

        var result = await controller.Resolve(request, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    // ------------------------------------------------------------------------------------------- contract (MDM ↔ Platform)

    // The answer MDM's tests feed to the real MDM client is exactly what this handler produces (web JSON, as MVC writes it).
    [Fact]
    public async Task The_handler_produces_the_contract_answer_mdm_reads()
    {
        var fixture = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ContractFixturePath()))!;
        var selections = fixture["request"]!["selections"]!.AsArray()
            .Select(x => new ItemUniversalResolveSelectionInput(
                (string)x!["set_code"]!, (string)x["value_code"]!, (string)x["resolution_mode"]!))
            .ToArray();
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());

        var response = await new ResolveItemUniversalReferenceDataHandler(tenant, new FixedTime())
            .Handle(new ResolveItemUniversalReferenceDataQuery(selections), CancellationToken.None);
        var actual = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));

        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(fixture["response"], actual),
            "contract drift:\n" + actual!.ToJsonString());
    }

    internal static string ContractFixturePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "services", "Diten.Platform", "tests", "Diten.Platform.Application.Tests",
            "BusinessReferenceData", "Fixtures", "item-universal-resolve.contract.json");
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    }

    // ------------------------------------------------------------------------------------------- helpers

    private static readonly Guid PriorTenant = Guid.NewGuid();
    private static readonly Guid ServiceTenant = Guid.NewGuid();

    private static ItemUniversalResolveSelectionInput Sel(string set, string value) => new(set, value, "LATEST");

    private static ItemUniversalResolveSelectionRequest Body(string set, string value) =>
        new() { SetCode = set, ValueCode = value, ResolutionMode = "LATEST" };

    private static Task<Response<ItemUniversalResolveResult>> Handle(params ItemUniversalResolveSelectionInput?[] selections)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        return new ResolveItemUniversalReferenceDataHandler(tenant, TimeProvider.System)
            .Handle(new ResolveItemUniversalReferenceDataQuery(selections), CancellationToken.None);
    }

    private static (InternalItemUniversalReferenceDataController Controller, TenantContext Tenants) Controller(
        Mock<IMediator> mediator, string mode)
    {
        var tenants = new TenantContext();
        tenants.SetTenant(PriorTenant);
        var credential = new Mock<IVerifiedGskuResolverCredentialAuthenticator>(MockBehavior.Strict);
        credential.Setup(x => x.Authenticate("resolver-id", "resolver-secret", "VERIFIED_GSKU_RESOLVE"))
            .Returns(mode == "forbidden-credential"
                ? new VerifiedGskuResolverCredentialAuthenticationResult(false, true, null, null)
                : new VerifiedGskuResolverCredentialAuthenticationResult(true, false, "DITENMDMSERVICE", "VERIFIED_GSKU_RESOLVE"));
        var interactive = new Mock<IVerifiedGskuResolverJwtTenantContext>(MockBehavior.Strict);
        interactive.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>())).ReturnsAsync(mode == "both-tokens"
            ? new VerifiedGskuResolverJwtTenantResult(true, true, Guid.NewGuid())
            : VerifiedGskuResolverJwtTenantResult.Unauthenticated);
        var service = new Mock<IVerifiedReferenceDataServiceTenantContext>(MockBehavior.Strict);
        service.Setup(x => x.ResolveAsync(It.IsAny<HttpContext>())).ReturnsAsync(mode is "no-token"
            ? VerifiedGskuResolverJwtTenantResult.Unauthenticated
            : new VerifiedGskuResolverJwtTenantResult(true, true, ServiceTenant));
        var controller = new InternalItemUniversalReferenceDataController(
            mediator.Object, credential.Object, interactive.Object, tenants, service.Object);
        var http = new DefaultHttpContext();
        if (mode != "no-credential")
        {
            http.Request.Headers[InternalBusinessReferenceDataController.CredentialIdHeader] = "resolver-id";
            http.Request.Headers[InternalBusinessReferenceDataController.CredentialSecretHeader] = "resolver-secret";
            http.Request.Headers[InternalBusinessReferenceDataController.AudienceHeader] = "VERIFIED_GSKU_RESOLVE";
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, tenants);
    }

    private static string Ratio(string bigger, string smaller) =>
        (decimal.Parse(Factor(bigger), System.Globalization.CultureInfo.InvariantCulture)
         / decimal.Parse(Factor(smaller), System.Globalization.CultureInfo.InvariantCulture))
        .ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture);

    private static string Factor(string code) =>
        ItemUniversalCatalog.Values.Single(x => x.SetCode == "item-uom" && x.ValueCode == code)
            .Attributes[ItemUniversalCatalog.FactorToDimensionBase]!;
}
