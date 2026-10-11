using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.API.Models.BusinessReferenceData;
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
/// MOD-0290-FU04 S5b (BL-600): the item-universal ENUMERATION — every value of the asked sets (only the three item sets),
/// in order, with attributes and the set's catalog version; the resolve's trust gate (same executor, credential, audience);
/// the contract MDM's client reads (Fixtures/item-universal-enumerate.contract.json).
/// </summary>
public sealed partial class ItemUniversalReferenceDataTests
{
    [Fact]
    public async Task S5b_The_enumeration_lists_every_value_of_the_asked_sets_in_order_with_its_version()
    {
        var response = await Enumerate("item-uom", "storage-condition", "material-type");

        Assert.True(response.IsSuccessful);
        Assert.Equal(["item-uom", "storage-condition", "material-type"], response.Data!.Sets.Select(x => x.SetCode));
        foreach (var set in response.Data.Sets)
        {
            var catalog = ItemUniversalCatalog.Values.Where(x => x.SetCode == set.SetCode).OrderBy(x => x.SortOrder).ToList();
            Assert.Equal(catalog.Select(x => x.ValueCode), set.Values.Select(x => x.ValueCode));
            Assert.Equal(catalog.Select(x => x.Attributes), set.Values.Select(x => x.Attributes));
            Assert.Equal(ItemUniversalCatalog.GetVersionId(set.SetCode), set.CatalogVersionId);
            Assert.Equal((ItemUniversalCatalog.CatalogVersion, ItemUniversalCatalog.GetVersionNumber(set.SetCode)),
                (set.CatalogVersion, set.CatalogVersionNumber));
        }
        Assert.All(response.Data.Sets.Single(x => x.SetCode == "storage-condition").Values,
            value => Assert.Contains(value.Attributes[ItemUniversalCatalog.ConditionKind], new[] { "TEMPERATURE", "PROTECTION" }));
    }

    // The sabotage target "a fourth set is accepted": only the three item sets; anything else is 400 by name.
    [Theory]
    [InlineData("uom")]
    [InlineData("country")]
    [InlineData("ITEM-UOM")]
    public async Task S5b_A_set_outside_the_three_is_400_by_name(string setCode)
    {
        var response = await Enumerate("item-uom", setCode);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.SetNotSupported, response.Errors.Single());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("repeat")]
    [InlineData("blank")]
    [InlineData("four")]
    public async Task S5b_A_malformed_ask_is_400(string shape)
    {
        string?[] codes = shape switch
        {
            "empty" => [],
            "repeat" => ["item-uom", "item-uom"],
            "blank" => [""],
            _ => ["item-uom", "storage-condition", "material-type", "item-uom"]
        };
        var response = await Enumerate(codes);

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.ContractInvalid, response.Errors.Single());
    }

    [Fact]
    public async Task S5b_Without_a_tenant_nothing_is_listed()
    {
        var response = await new EnumerateItemUniversalReferenceDataHandler(new TenantContext())
            .Handle(new EnumerateItemUniversalReferenceDataQuery(["item-uom"]), CancellationToken.None);

        Assert.Equal(403, response.StatusCode);
        Assert.Null(response.Data);
    }

    // The resolve's trust gate, unchanged: refused by name before the handler (strict mediator).
    [Theory]
    [InlineData("no-credential", 401)]
    [InlineData("forbidden-credential", 403)]
    [InlineData("no-token", 401)]
    [InlineData("both-tokens", 403)]
    public async Task S5b_An_unauthenticated_enumeration_never_reaches_the_lists(string fault, int status)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var (controller, _) = Controller(mediator, fault);

        var result = await controller.Enumerate(new ItemUniversalEnumerateRequest { SetCodes = ["item-uom"] }, CancellationToken.None);

        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("query")]
    [InlineData("extra-field")]
    [InlineData("no-sets")]
    public async Task S5b_An_enumeration_body_outside_the_contract_is_400_before_dispatch(string fault)
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var (controller, _) = Controller(mediator, "service");
        if (fault == "query") controller.HttpContext.Request.QueryString = new QueryString("?tenantId=x");
        var request = fault switch
        {
            "extra-field" => new ItemUniversalEnumerateRequest
            {
                SetCodes = ["item-uom"], AdditionalFields = new() { ["tenant_id"] = default }
            },
            "no-sets" => new ItemUniversalEnumerateRequest(),
            _ => new ItemUniversalEnumerateRequest { SetCodes = ["item-uom"] }
        };

        var result = await controller.Enumerate(request, CancellationToken.None);

        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task S5b_An_authenticated_service_enumerates_under_its_tenant_and_restores_the_prior_scope()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var (controller, tenants) = Controller(mediator, "service");
        mediator.Setup(x => x.Send(It.IsAny<EnumerateItemUniversalReferenceDataQuery>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(ServiceTenant, tenants.TenantId))
            .ReturnsAsync(Response<ItemUniversalEnumerationResult>.Success(new ItemUniversalEnumerationResult([])));

        var result = await controller.Enumerate(new ItemUniversalEnumerateRequest { SetCodes = ["item-uom"] }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(PriorTenant, tenants.TenantId);
    }

    // The contract MDM's client reads: the handler's web JSON is exactly the fixture (success and the named 400), and the
    // request MDM sends binds to the real body.
    [Theory]
    [InlineData(null)]
    [InlineData("bad_request")]
    public async Task S5b_The_enumeration_handler_produces_the_contract_answer_mdm_reads(string? contractCase)
    {
        var fixture = EnumerationContract(contractCase);
        var codes = fixture["request"]!["set_codes"]!.AsArray().Select(x => (string?)x).ToArray();

        var response = await Enumerate(codes);
        var actual = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));

        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(fixture["response"], actual),
            "contract drift:\n" + actual!.ToJsonString());
        var body = System.Text.Json.JsonSerializer.Deserialize<ItemUniversalEnumerateRequest>(fixture["request"]!.ToJsonString(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Null(body.AdditionalFields);
        Assert.Equal(codes, body.SetCodes);
    }

    // S5b-FIX2: a set code longer than 64 is a malformed ask (CONTRACT_INVALID) — never "not supported", whatever it
    // spells; 64 itself reaches the set check.
    [Fact]
    public async Task S5b_FIX2_A_set_code_longer_than_64_is_contract_invalid_not_unsupported()
    {
        var refused = await Enumerate("item-uom", new string('x', 65));
        Assert.Equal(400, refused.StatusCode);
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.ContractInvalid, refused.Errors.Single());

        var unsupported = await Enumerate("item-uom", new string('x', 64));
        Assert.Equal(ResolveItemUniversalReferenceDataHandler.SetNotSupported, unsupported.Errors.Single());
    }

    // S5b-FIX2: the enumeration body is capped like the resolve's (sabotage target V4).
    [Fact]
    public void S5b_FIX2_The_enumeration_body_is_capped()
    {
        var limit = Assert.Single(typeof(InternalItemUniversalReferenceDataController)
            .GetMethod(nameof(InternalItemUniversalReferenceDataController.Enumerate))!
            .GetCustomAttributes(typeof(RequestSizeLimitAttribute), false).Cast<RequestSizeLimitAttribute>());
        // FIX3: the cap's VALUE, not only its presence.
        Assert.Equal(InternalItemUniversalReferenceDataController.MaximumRequestBytes,
            ((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)limit).MaxRequestBodySize);
        Assert.Equal(65536, InternalItemUniversalReferenceDataController.MaximumRequestBytes);
    }

    // S5b-FIX2: an unexpected failure inside the enumeration is a named 503 (a timeout a named 504), never a bare 500
    // (sabotage target V6).
    [Theory]
    [InlineData(false, 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(true, 504, "REFERENCE_PROVIDER_TIMEOUT")]
    public async Task S5b_FIX2_An_unexpected_failure_in_the_enumeration_is_answered_by_name(bool cancelled, int status, string code)
    {
        var behavior = new Diten.Platform.Application.Features.BusinessReferenceData.BusinessReferenceDataExceptionBehavior<
            EnumerateItemUniversalReferenceDataQuery, Response<ItemUniversalEnumerationResult>>(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Diten.Platform.Application.Features.BusinessReferenceData
                .BusinessReferenceDataExceptionBehavior<EnumerateItemUniversalReferenceDataQuery,
                    Response<ItemUniversalEnumerationResult>>>.Instance);

        var response = await behavior.Handle(new EnumerateItemUniversalReferenceDataQuery(["item-uom"]),
            () => Task.FromException<Response<ItemUniversalEnumerationResult>>(
                cancelled ? new OperationCanceledException() : new NullReferenceException()),
            CancellationToken.None);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, response.Errors.Single());
    }

    private static Task<Response<ItemUniversalEnumerationResult>> Enumerate(params string?[] setCodes)
    {
        var tenant = new TenantContext();
        tenant.SetTenant(Guid.NewGuid());
        return new EnumerateItemUniversalReferenceDataHandler(tenant)
            .Handle(new EnumerateItemUniversalReferenceDataQuery(setCodes), CancellationToken.None);
    }

    private static System.Text.Json.Nodes.JsonNode EnumerationContract(string? contractCase)
    {
        var path = Path.Combine(Path.GetDirectoryName(ContractFixturePath())!, "item-universal-enumerate.contract.json");
        var fixture = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        return contractCase is null ? fixture : fixture[contractCase]!;
    }
}
