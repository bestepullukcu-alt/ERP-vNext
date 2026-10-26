using Diten.SupplyChainService.Api.Features.Carriers;
using Xunit;

namespace Diten.SupplyChainService.Tests.Carriers;

/// <summary>
/// BL-652 — a dependency failure must not reach the caller as a schema complaint.
///
/// Measured on a live stack (docs/records/audits/2026-10/mvp6-golden-flows-http-01): a token holding all 25
/// supplychain.* keys but not mdm.legal-entities.read made the legal-entity validator answer 503
/// DEPENDENCY_UNAVAILABLE, and because that code was in neither of this file's switches the 503 fell through to the
/// final default — so a VALID payload was reported as "Request schema validation failed." and sent the reader to
/// check the request instead of the dependency.
///
/// These assertions hold the production mapping, not a copy of it, and the negative half keeps the real schema
/// message reachable so the fix cannot be a blanket rename.
/// </summary>
public sealed class CarrierContractErrorMessageTests
{
    private const string SchemaMessage = "Request schema validation failed.";

    [Fact]
    public void A_dependency_failure_does_not_read_as_a_schema_failure()
    {
        var byCode = CarrierContractError.Message("DEPENDENCY_UNAVAILABLE", 503);
        var byStatus = CarrierContractError.Message("SOME_FUTURE_CODE", 503);

        Assert.NotEqual(SchemaMessage, byCode);
        Assert.NotEqual(SchemaMessage, byStatus);
        Assert.Contains("dependency", byCode, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dependency", byStatus, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_schema_message_is_still_reachable_so_the_fix_is_not_a_blanket_rename()
    {
        // A 400 with no known code is the case the schema message exists for.
        Assert.Equal(SchemaMessage, CarrierContractError.Message("SOME_FUTURE_CODE", 400));
    }

    [Theory]
    [InlineData("CARRIER_NOT_FOUND", 404, "Carrier not found.")]
    [InlineData("CARRIER_CODE_CONFLICT", 409, "Carrier code is already reserved.")]
    [InlineData("IDEMPOTENCY_KEY_REUSED", 409, "Idempotency-Key was used with a different payload.")]
    [InlineData("INVALID_CARRIER_TRANSITION", 422, "Carrier lifecycle transition is invalid.")]
    public void The_codes_that_already_had_messages_are_unchanged(string code, int status, string expected)
        => Assert.Equal(expected, CarrierContractError.Message(code, status));

    [Theory]
    [InlineData(401, "Authentication required.")]
    [InlineData(403, "Required authorization context or permission is missing.")]
    [InlineData(415, "Request content type is not supported.")]
    [InlineData(500, "An unexpected internal error occurred.")]
    public void The_status_arms_that_already_had_messages_are_unchanged(int status, string expected)
        => Assert.Equal(expected, CarrierContractError.Message("SOME_FUTURE_CODE", status));

    [Fact]
    public void Create_carries_the_code_the_correlation_and_the_contract_version()
    {
        var correlation = System.Guid.NewGuid();
        var envelope = CarrierContractError.Create("DEPENDENCY_UNAVAILABLE", 503, correlation);
        var json = System.Text.Json.JsonSerializer.Serialize(envelope);

        Assert.Contains("\"code\":\"DEPENDENCY_UNAVAILABLE\"", json, System.StringComparison.Ordinal);
        Assert.Contains(correlation.ToString(), json, System.StringComparison.Ordinal);
        Assert.Contains("\"contractVersion\":\"v1\"", json, System.StringComparison.Ordinal);
        Assert.DoesNotContain(SchemaMessage, json, System.StringComparison.Ordinal);
    }
}
