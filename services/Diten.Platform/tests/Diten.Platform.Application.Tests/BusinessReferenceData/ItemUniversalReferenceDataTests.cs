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
public sealed partial class ItemUniversalReferenceDataTests
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

    // MOD-0290-FU04 O-21 (UN/ECE Rec 20 Rev 11e, measured): roll = XRO. V2 (WP-PLT-ITEM-LISTS-02): the local IU; Rec 20's
    // HIU (hundred international units) is NOT used — the quality unit's decision.
    [Fact]
    public void The_item_units_are_the_agreed_codes_with_dimension_precision_and_factor()
    {
        var units = ItemUniversalCatalog.Values.Where(x => x.SetCode == "item-uom").ToList();
        Assert.Equal(new[] { "C62", "MC", "MGM", "GRM", "KGM", "IU", "MLT", "LTR", "MTR", "MTK", "XBX", "XCS", "XRO" },
            units.Select(x => x.ValueCode));
        Assert.All(units, unit =>
        {
            var dimension = unit.Attributes[ItemUniversalCatalog.DimensionCode];
            Assert.Contains(dimension, new[] { "COUNT", "MASS", "VOLUME", "LENGTH", "AREA", "PACKAGE", "BIOLOGICAL_ACTIVITY" });
            Assert.True(int.Parse(unit.Attributes[ItemUniversalCatalog.MaximumDecimalPrecision]!) >= 0);
            Assert.Equal(dimension == "PACKAGE", unit.Attributes[ItemUniversalCatalog.FactorToDimensionBase] is null);
        });
        Assert.DoesNotContain(units, x => x.ValueCode is "RO" or "HIU");
        var iu = Assert.Single(units, x => x.ValueCode == "IU");
        Assert.Equal("International unit", iu.DisplayName);
        Assert.Equal("1", iu.Attributes[ItemUniversalCatalog.FactorToDimensionBase]);
        Assert.Equal("0", iu.Attributes[ItemUniversalCatalog.MaximumDecimalPrecision]);
    }

    // OD-7: IU is marked as outside UN/ECE Rec 20; every other unit says it is a Rec 20 code — the attribute is on every
    // unit, so "no attribute" never reads as either.
    [Fact]
    public void IU_is_marked_as_a_local_code_outside_rec_20_and_every_other_unit_as_rec_20()
    {
        var units = ItemUniversalCatalog.Values.Where(x => x.SetCode == "item-uom").ToList();
        Assert.All(units, unit => Assert.True(unit.Attributes.ContainsKey(ItemUniversalCatalog.CodeStandard), unit.ValueCode));
        Assert.Equal(new[] { "IU" },
            units.Where(x => x.Attributes[ItemUniversalCatalog.CodeStandard] == "LOCAL").Select(x => x.ValueCode));
        Assert.All(units.Where(x => x.ValueCode != "IU"),
            unit => Assert.Equal("UNECE_REC20", unit.Attributes[ItemUniversalCatalog.CodeStandard]));
    }

    // IU ↔ mg is substance-specific: no factor may derive it. Two units are derivable from each other only inside one
    // dimension with both factors known; IU shares its dimension with no other unit, so it derives with nothing — an item
    // states IU ↔ MGM as an explicit conversion.
    [Fact]
    public void IU_has_its_own_dimension_so_no_factor_derives_it_from_a_mass_or_any_other_unit()
    {
        var units = ItemUniversalCatalog.Values.Where(x => x.SetCode == "item-uom").ToList();
        var derivable = (from a in units
                         from b in units
                         where a.ValueCode != b.ValueCode
                               && a.Attributes[ItemUniversalCatalog.DimensionCode] == b.Attributes[ItemUniversalCatalog.DimensionCode]
                               && a.Attributes[ItemUniversalCatalog.FactorToDimensionBase] is not null
                               && b.Attributes[ItemUniversalCatalog.FactorToDimensionBase] is not null
                         select (a.ValueCode, b.ValueCode)).ToList();

        Assert.Equal("BIOLOGICAL_ACTIVITY", units.Single(x => x.ValueCode == "IU").Attributes[ItemUniversalCatalog.DimensionCode]);
        Assert.DoesNotContain(derivable, pair => pair.Item1 == "IU" || pair.Item2 == "IU");
        Assert.Contains(("MGM", "GRM"), derivable);
    }

    // FIX1 R1: what ITEM-UNIVERSAL-V1 vouched for, FROZEN — written out here, never read from the catalog. An item that
    // stored a V1 proof keeps it forever (ItemReferenceCheck never re-resolves a held code), so every V1 code must stay,
    // with the same meaning. CodeStandard (V2) is excluded: it says where a code comes from, not what it means.
    private static readonly (string Set, string Code, (string Key, string? Value)[] Attributes)[] V1Snapshot =
    [
        ("item-uom", "C62", [("DimensionCode", "COUNT"), ("MaximumDecimalPrecision", "0"), ("FactorToDimensionBase", "1")]),
        ("item-uom", "MC", [("DimensionCode", "MASS"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "0.000000001")]),
        ("item-uom", "MGM", [("DimensionCode", "MASS"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "0.000001")]),
        ("item-uom", "GRM", [("DimensionCode", "MASS"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "0.001")]),
        ("item-uom", "KGM", [("DimensionCode", "MASS"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "1")]),
        ("item-uom", "MLT", [("DimensionCode", "VOLUME"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "0.001")]),
        ("item-uom", "LTR", [("DimensionCode", "VOLUME"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "1")]),
        ("item-uom", "MTR", [("DimensionCode", "LENGTH"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "1")]),
        ("item-uom", "MTK", [("DimensionCode", "AREA"), ("MaximumDecimalPrecision", "3"), ("FactorToDimensionBase", "1")]),
        ("item-uom", "XBX", [("DimensionCode", "PACKAGE"), ("MaximumDecimalPrecision", "0"), ("FactorToDimensionBase", null)]),
        ("item-uom", "XCS", [("DimensionCode", "PACKAGE"), ("MaximumDecimalPrecision", "0"), ("FactorToDimensionBase", null)]),
        ("item-uom", "XRO", [("DimensionCode", "PACKAGE"), ("MaximumDecimalPrecision", "0"), ("FactorToDimensionBase", null)]),
        ("storage-condition", "AMBIENT_15_25", [("ConditionKind", "TEMPERATURE"), ("TemperatureMinC", "15"), ("TemperatureMaxC", "25"), ("ProtectFromLight", "false"), ("ProtectFromMoisture", "false")]),
        ("storage-condition", "COOL_2_8", [("ConditionKind", "TEMPERATURE"), ("TemperatureMinC", "2"), ("TemperatureMaxC", "8"), ("ProtectFromLight", "false"), ("ProtectFromMoisture", "false")]),
        ("storage-condition", "FROZEN_BELOW_MINUS_20", [("ConditionKind", "TEMPERATURE"), ("TemperatureMinC", null), ("TemperatureMaxC", "-20"), ("ProtectFromLight", "false"), ("ProtectFromMoisture", "false")]),
        ("storage-condition", "PROTECT_FROM_LIGHT", [("ConditionKind", "PROTECTION"), ("TemperatureMinC", null), ("TemperatureMaxC", null), ("ProtectFromLight", "true"), ("ProtectFromMoisture", "false")]),
        ("storage-condition", "PROTECT_FROM_MOISTURE", [("ConditionKind", "PROTECTION"), ("TemperatureMinC", null), ("TemperatureMaxC", null), ("ProtectFromLight", "false"), ("ProtectFromMoisture", "true")]),
        ("material-type", "ACTIVE_INGREDIENT", []),
        ("material-type", "EXCIPIENT", []),
        ("material-type", "RAW_MATERIAL", []),
        ("material-type", "PACKAGING_PRIMARY", []),
        ("material-type", "PACKAGING_SECONDARY", []),
        ("material-type", "INTERMEDIATE", []),
    ];

    // FIX1 R1 (catalog rule: a code is retired, never removed or redefined). Every V1 value is still in the catalog, with
    // exactly the attributes V1 gave it — none removed, none changed, none added except CodeStandard.
    [Fact]
    public void Every_v1_value_is_still_there_with_the_meaning_v1_vouched_for()
    {
        Assert.Equal(23, V1Snapshot.Length);
        foreach (var (set, code, attributes) in V1Snapshot)
        {
            var value = ItemUniversalCatalog.Find(set, code);
            Assert.True(value is not null, $"V1 code {set}:{code} is gone — a code is retired, never removed");
            var now = value!.Attributes.Where(pair => pair.Key != ItemUniversalCatalog.CodeStandard)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (pair.Key, pair.Value)).ToArray();
            var then = attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
            Assert.True(then.SequenceEqual(now),
                $"V1 code {set}:{code} changed meaning — V1: [{string.Join(", ", then)}], now: [{string.Join(", ", now)}]");
        }
    }

    // The rule "a value change is a new deployment version": only item-uom changed in V2 — it has a new version id and
    // number; storage-condition and material-type keep V1's (literal: the ids items stored under V1 carry).
    [Fact]
    public void Only_the_changed_list_has_a_new_version_and_the_unchanged_lists_keep_theirs()
    {
        Assert.Equal("ITEM-UNIVERSAL-V2", ItemUniversalCatalog.CatalogVersion);
        Assert.Equal(Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0004"), ItemUniversalCatalog.GetVersionId("item-uom"));
        Assert.Equal(2, ItemUniversalCatalog.GetVersionNumber("item-uom"));
        Assert.Equal(Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0001"), ItemUniversalCatalog.ItemUomCatalogVersionIdV1);
        Assert.NotEqual(ItemUniversalCatalog.ItemUomCatalogVersionIdV1, ItemUniversalCatalog.GetVersionId("item-uom"));
        Assert.Equal(Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0002"), ItemUniversalCatalog.GetVersionId("storage-condition"));
        Assert.Equal(1, ItemUniversalCatalog.GetVersionNumber("storage-condition"));
        Assert.Equal(Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0003"), ItemUniversalCatalog.GetVersionId("material-type"));
        Assert.Equal(1, ItemUniversalCatalog.GetVersionNumber("material-type"));
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
            Assert.Equal("ITEM-UNIVERSAL-V2", x.CatalogVersion);
            Assert.Equal(ItemUniversalCatalog.GetVersionId(x.SetCode), x.CatalogVersionId);
            Assert.Equal(ItemUniversalCatalog.GetVersionNumber(x.SetCode), x.CatalogVersionNumber);
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

    // The answers MDM's tests feed to the real MDM client are exactly what this handler produces (web JSON, as MVC writes
    // it) — the success, the named 404 and the 400 (FIX1 R2).
    [Theory]
    [InlineData(null)]
    [InlineData("not_found")]
    [InlineData("bad_request")]
    public async Task The_handler_produces_the_contract_answer_mdm_reads(string? contractCase)
    {
        var fixture = Contract(contractCase);
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());

        var response = await new ResolveItemUniversalReferenceDataHandler(tenant, new FixedTime())
            .Handle(new ResolveItemUniversalReferenceDataQuery(Inputs(fixture["request"]!)), CancellationToken.None);
        var actual = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));

        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(fixture["response"], actual),
            "contract drift:\n" + actual!.ToJsonString());
    }

    // FIX1 R2: the request MDM sends (the same file) binds field by field to the real API body — a renamed JSON property on
    // either side turns this or MDM's body test red.
    [Theory]
    [InlineData(null)]
    [InlineData("not_found")]
    [InlineData("bad_request")]
    public void The_contract_request_binds_to_the_real_body_field_by_field(string? contractCase)
    {
        var source = Contract(contractCase)["request"]!;
        var body = System.Text.Json.JsonSerializer.Deserialize<ItemUniversalResolveRequest>(source.ToJsonString(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.Null(body.AdditionalFields);
        var expected = source["selections"]!.AsArray();
        Assert.Equal(expected.Count, body.Selections!.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Null(body.Selections[i]!.AdditionalFields);
            Assert.Equal((string)expected[i]!["set_code"]!, body.Selections[i]!.SetCode);
            Assert.Equal((string)expected[i]!["value_code"]!, body.Selections[i]!.ValueCode);
            Assert.Equal((string)expected[i]!["resolution_mode"]!, body.Selections[i]!.ResolutionMode);
        }
    }

    // FIX1 M6 / M5: the 200 limit and the six material types — the same file binds MDM's client constant and ItemRules.
    [Fact]
    public void The_limit_and_the_material_types_are_the_contracts()
    {
        var fixture = Contract(null);
        Assert.Equal((int)fixture["maximum_selections"]!, ItemUniversalCatalog.MaximumSelections);
        Assert.Equal(fixture["material_types"]!.AsArray().Select(x => (string)x!),
            ItemUniversalCatalog.Values.Where(x => x.SetCode == "material-type").Select(x => x.ValueCode));
        // WP-PLT-ITEM-LISTS-02: the unit list and item-uom's V1 proof — the same file binds MDM's fakes and its V1 test.
        Assert.Equal(fixture["item_uoms"]!.AsArray().Select(x => (string)x!),
            ItemUniversalCatalog.Values.Where(x => x.SetCode == "item-uom").Select(x => x.ValueCode));
        Assert.Equal(
            fixture["storage_conditions"]!.AsObject().Select(pair => (pair.Key, (string?)pair.Value)),
            ItemUniversalCatalog.Values.Where(x => x.SetCode == "storage-condition")
                .Select(x => (x.ValueCode, x.Attributes[ItemUniversalCatalog.ConditionKind])));
        var v1 = Assert.Single(fixture["previous_versions"]!["item-uom"]!.AsArray());
        Assert.Equal(ItemUniversalCatalog.ItemUomCatalogVersionIdV1, Guid.Parse((string)v1!["catalog_version_id"]!));
        Assert.Equal(1, (int)v1["catalog_version_number"]!);
    }

    // FIX1 M1: a code longer than 64 is a malformed request (never echoed back as "not found"); the body is capped at 64 KB.
    [Fact]
    public async Task A_code_longer_than_64_is_400_and_the_body_is_capped()
    {
        var refused = await Handle(Sel("item-uom", new string('X', 65)));
        Assert.Equal(400, refused.StatusCode);
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.ContractInvalid, refused.ReasonCode);
        var allowed = await Handle(Sel("item-uom", new string('X', 64)));
        Assert.Equal(404, allowed.StatusCode);

        var limit = Assert.Single(typeof(InternalItemUniversalReferenceDataController).GetMethod(nameof(
                InternalItemUniversalReferenceDataController.Resolve))!
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Cast<RequestSizeLimitAttribute>());
        Assert.Equal(65536, InternalItemUniversalReferenceDataController.MaximumRequestBytes);
        Assert.NotNull(limit);
    }

    // FIX1 M6: an unexpected failure inside the resolve is a named 503 (a timeout a named 504), never a bare 500.
    [Theory]
    [InlineData(false, 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(true, 504, "REFERENCE_PROVIDER_TIMEOUT")]
    public async Task An_unexpected_failure_in_the_resolve_is_answered_by_name(bool cancelled, int status, string code)
    {
        var behavior = new Diten.Platform.Application.Features.BusinessReferenceData.BusinessReferenceDataExceptionBehavior<
            ResolveItemUniversalReferenceDataQuery, Response<ItemUniversalResolveResult>>(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Application.Features.BusinessReferenceData
                .BusinessReferenceDataExceptionBehavior<ResolveItemUniversalReferenceDataQuery, Response<ItemUniversalResolveResult>>>.Instance);

        var response = await behavior.Handle(new ResolveItemUniversalReferenceDataQuery([Sel("item-uom", "KGM")]),
            () => Task.FromException<Response<ItemUniversalResolveResult>>(
                // Not InvalidOperationException: that is the module's coded-error channel (mapped by message).
                cancelled ? new OperationCanceledException() : new NullReferenceException()),
            CancellationToken.None);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, response.Errors.Single());
    }

    private static System.Text.Json.Nodes.JsonNode Contract(string? contractCase)
    {
        var fixture = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(ContractFixturePath()))!;
        return contractCase is null ? fixture : fixture[contractCase]!;
    }

    private static ItemUniversalResolveSelectionInput?[] Inputs(System.Text.Json.Nodes.JsonNode request) =>
        request["selections"]!.AsArray()
            .Select(x => (ItemUniversalResolveSelectionInput?)new ItemUniversalResolveSelectionInput(
                (string)x!["set_code"]!, (string)x["value_code"]!, (string)x["resolution_mode"]!))
            .ToArray();

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
