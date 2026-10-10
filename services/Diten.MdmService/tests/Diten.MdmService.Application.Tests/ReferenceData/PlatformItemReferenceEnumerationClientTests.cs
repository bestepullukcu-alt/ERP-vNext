using System.Net;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Infrastructure.ReferenceData;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

/// <summary>
/// MOD-0290-FU04 S5b (BL-600): the MDM client of Platform's item-universal ENUMERATION — the contract answer (the file
/// Platform's handler test produces) is trusted and mapped; the request is the contract's; an answer that does not list
/// exactly the asked sets is UNTRUSTED_ANSWER; every failure is an outage — never an empty list.
/// </summary>
public sealed partial class PlatformItemReferenceResolverClientTests
{
    private static readonly string[] EnumerationSets = ["item-uom", "storage-condition", "material-type"];

    [Fact]
    public async Task S5b_The_contract_enumeration_is_trusted_and_mapped()
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, EnumerationResponse()));

        var result = await Client(handler).EnumerateLatestAsync(EnumerationSets);

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal(EnumerationSets, result.Sets.Select(x => x.SetCode));
        var uoms = result.Sets.Single(x => x.SetCode == "item-uom").Values;
        Assert.Contains(uoms, x => x.ValueCode == "XBX");
        Assert.Equal("MASS", uoms.Single(x => x.ValueCode == "KGM").Attributes["DimensionCode"]);
        Assert.Equal("TEMPERATURE", result.Sets.Single(x => x.SetCode == "storage-condition").Values
            .Single(x => x.ValueCode == "COOL_2_8").Attributes["ConditionKind"]);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://platform.test/" + PlatformItemReferenceResolverClient.EnumerateRelativePath, request.Uri);
        Assert.True(JsonNode.DeepEquals(EnumerationFixture()["request"], JsonNode.Parse(request.Body)), request.Body);
        Assert.Equal("Bearer user-token", request.Authorization);
        Assert.Equal("resolver-secret", request.Headers[PlatformItemReferenceResolverClient.CredentialSecretHeader]);
        Assert.Equal("VERIFIED_GSKU_RESOLVE", request.Headers[PlatformItemReferenceResolverClient.AudienceHeader]);
    }

    [Theory]
    [InlineData("set-missing")]
    [InlineData("set-extra")]
    [InlineData("no-values")]
    [InlineData("code-repeated")]
    [InlineData("no-condition-kind")]
    [InlineData("no-dimension")]
    [InlineData("no-version")]
    public async Task S5b_An_enumeration_that_does_not_list_exactly_the_asked_sets_is_untrusted(string fault)
    {
        var body = EnumerationResponse();
        var sets = body["data"]!["sets"]!.AsArray();
        switch (fault)
        {
            case "set-missing": sets.RemoveAt(2); break;
            case "set-extra": sets.Add(sets[0]!.DeepClone()); break;
            case "no-values": sets[2]!["values"] = new JsonArray(); break;
            case "code-repeated": sets[0]!["values"]!.AsArray().Add(sets[0]!["values"]![0]!.DeepClone()); break;
            case "no-condition-kind": sets[1]!["values"]![0]!["attributes"]!.AsObject().Remove("ConditionKind"); break;
            case "no-dimension": sets[0]!["values"]![0]!["attributes"]!.AsObject().Remove("DimensionCode"); break;
            default: sets[0]!["catalog_version_id"] = Guid.Empty.ToString(); break;
        }

        var result = await Client(new Recording(_ => Json(HttpStatusCode.OK, body))).EnumerateLatestAsync(EnumerationSets);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(ItemReferenceCheck.UntrustedAnswer, result.FailureCode);
        Assert.Empty(result.Sets);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "REFERENCE_SET_NOT_SUPPORTED", "REFERENCE_SET_NOT_SUPPORTED")]
    [InlineData(HttpStatusCode.Forbidden, "REFERENCE_FORBIDDEN", "REFERENCE_FORBIDDEN")]
    [InlineData(HttpStatusCode.InternalServerError, "something", "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.GatewayTimeout, null, "REFERENCE_PROVIDER_TIMEOUT")]
    public async Task S5b_Every_enumeration_failure_is_an_outage_never_an_empty_list(HttpStatusCode status, string? reason, string code)
    {
        var result = await Client(new Recording(_ => Json(status,
            new JsonObject { ["isSuccessful"] = false, ["reason_code"] = reason }))).EnumerateLatestAsync(EnumerationSets);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(code, result.FailureCode);
        Assert.Empty(result.Sets);
    }

    [Fact]
    public async Task S5b_The_contract_bad_request_enumeration_is_an_outage_with_its_reason()
    {
        var result = await Client(new Recording(_ => Json(HttpStatusCode.BadRequest,
            EnumerationFixture()["bad_request"]!["response"]!.DeepClone()))).EnumerateLatestAsync(["uom"]);

        Assert.Equal("REFERENCE_SET_NOT_SUPPORTED", result.FailureCode);
    }

    [Fact]
    public async Task S5b_Without_the_callers_token_no_enumeration_is_sent()
    {
        var handler = new Recording(_ => Json(HttpStatusCode.OK, EnumerationResponse()));
        var result = await Client(handler, authenticated: false).EnumerateLatestAsync(EnumerationSets);
        Assert.Equal("REFERENCE_UNAUTHENTICATED", result.FailureCode);
        Assert.Empty(handler.Requests);
    }

    private static JsonNode EnumerationFixture() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(Path.GetDirectoryName(ContractFixturePath())!, "item-universal-enumerate.contract.json")))!;

    private static JsonObject EnumerationResponse() => EnumerationFixture()["response"]!.DeepClone().AsObject();
}
