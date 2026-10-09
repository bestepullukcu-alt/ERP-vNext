using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Contract;

/// <summary>
/// K3 for the AC15 guard itself: a schema validator that accepts everything would make every API test "conformant".
/// Each case pairs a body the frozen contract accepts with a mutation it must reject.
/// </summary>
public sealed class ContractSchemaValidatorTests
{
    private const string ValidMovementResponse = """
        {"transactionId":"3f1c1f7e-0000-4000-8000-000000000001","transactionNumber":"INV-2026-0000001","postedAt":"2026-09-30T10:00:00.000Z",
         "idempotentReplay":false,"contractVersion":"v1",
         "balanceSnapshot":{"itemId":"b1f2c3d4-0000-0000-0000-000000000001","skuId":"c3d4e5f6-0000-0000-0000-000000000002","skuLevel":"Lsku",
           "legalEntityId":"le","warehouseId":"wh-01","locationId":"loc","stockStatus":"AVAILABLE","onHand":"100.000","reserved":"0.000",
           "available":"100.000","inTransit":"0.000","blocked":"0.000","qualityInspection":"0.000","baseUomId":"EA","baseQuantity":"100.000",
           "lastMovementId":"x","lastPostingDate":"2026-09-30T10:00:00.000Z"}}
        """;

    [Fact]
    public void The_contract_file_is_the_frozen_v1_bundle()
    {
        var text = File.ReadAllText(ContractSchemas.ContractPath);

        Assert.Contains("x-status: FROZEN", text);
        Assert.Contains("x-owner: MOD-0173", text);
        // WP-0174-P2 (MOD-0174 CT-D1 (a), owner decision 2026-10-05): the description-only single-writer step 1.0.0 → 1.0.1.
        // Still the frozen v1 bundle — InventoryBundleDescriptionNoteTests pins 1.0.0 and proves only descriptions changed.
        // WP-0175-Q4 (MOD-0175 CT-Q3 (a), owner decision 2026-10-07): the description-only step 1.0.1 → 1.0.2 (LOT_RETEST_DUE).
        // WP-0173-PM2 (MOD-0290 R-12, proposal accepted 2026-10-09): the ADDITIVE step 1.0.2 → 1.1.0 (SkuLevel Item,
        // getStockPresence) — InventoryBundleDescriptionNoteTests proves nothing of 1.0.0 was removed or changed but the allowed keys.
        // WP-0173-PM1 (CT-PM1-1): the additive step 1.1.0 → 1.2.0 (three 422 codes on postMovement + its description).
        Assert.Contains("version: 1.2.1", text);
    }

    [Fact]
    public void A_conformant_movement_response_validates()
        => Assert.Empty(ContractSchemas.Validate(ContractSchemas.MovementResponse, ValidMovementResponse));

    [Theory]
    [InlineData("\"onHand\":\"100.000\"", "\"onHand\":100.0", "decimal must be a string")]
    [InlineData("\"onHand\":\"100.000\"", "\"onHand\":\"1e2\"", "decimal pattern")]
    [InlineData("\"stockStatus\":\"AVAILABLE\"", "\"stockStatus\":\"GOOD\"", "enum")]
    [InlineData("\"skuLevel\":\"Lsku\"", "\"skuLevel\":\"lsku\"", "enum is case-sensitive")]
    [InlineData("\"postedAt\":\"2026-09-30T10:00:00.000Z\"", "\"postedAt\":\"yesterday\"", "date-time format")]
    [InlineData("\"idempotentReplay\":false", "\"idempotentReplay\":\"false\"", "boolean type")]
    [InlineData("\"contractVersion\":\"v1\"", "\"contractVersion\":\"v1\",\"tenantId\":\"t\"", "no invented field")]
    [InlineData("\"lotId\":null", "\"lotId\":null", "placeholder")]
    public void A_mutated_movement_response_is_rejected(string from, string to, string because)
    {
        if (from == to)
        {
            // nullable in the contract → null is acceptable after the 3.0 → 2020-12 translation.
            var withNullLot = ValidMovementResponse.Replace("\"locationId\":\"loc\"", "\"locationId\":\"loc\",\"lotId\":null");
            Assert.Empty(ContractSchemas.Validate(ContractSchemas.MovementResponse, withNullLot));
            return;
        }

        var mutated = ValidMovementResponse.Replace(from, to);
        Assert.NotEqual(ValidMovementResponse, mutated);

        Assert.True(ContractSchemas.Validate(ContractSchemas.MovementResponse, mutated).Count > 0, because);
    }

    [Fact]
    public void Error_schema_accepts_the_contract_example_and_rejects_a_wrapped_response()
    {
        Assert.Empty(ContractSchemas.Validate(ContractSchemas.Error,
            """{"error":{"code":"INSUFFICIENT_STOCK","message":"m","details":{"available":"80.000","requested":"100.000"},"correlationId":"c"},"contractVersion":"v1"}"""));

        // The repo's Response<T> envelope must never reach this wire (pack §8).
        Assert.NotEmpty(ContractSchemas.Validate(ContractSchemas.Error,
            """{"data":null,"statusCode":400,"isSuccessful":false,"errors":["x"]}"""));
    }

    [Fact]
    public void Page_schemas_come_from_the_inline_path_definitions()
    {
        Assert.Empty(ContractSchemas.Validate(ContractSchemas.BalancePage, """{"rows":[],"total":0,"contractVersion":"v1"}"""));
        Assert.NotEmpty(ContractSchemas.Validate(ContractSchemas.BalancePage, """{"rows":[],"total":"0","contractVersion":"v1"}"""));
        Assert.Empty(ContractSchemas.Validate(ContractSchemas.TransactionPage, """{"items":[],"page":1,"pageSize":50,"total":0,"contractVersion":"v1"}"""));
        Assert.NotEmpty(ContractSchemas.Validate(ContractSchemas.TransactionPage, """{"items":[{"movementType":"TELEPORT"}],"page":1,"pageSize":50,"total":1}"""));
    }

    [Fact]
    public void Availability_by_status_values_must_be_decimals()
    {
        Assert.Empty(ContractSchemas.Validate(ContractSchemas.AvailabilityView,
            """{"legalEntityId":"le","scope":{},"onHand":"1.000","reserved":"0.000","available":"1.000","byStatus":{"AVAILABLE":"1.000"},"asOf":"2026-09-30T10:00:00.000Z","contractVersion":"v1"}"""));
        Assert.NotEmpty(ContractSchemas.Validate(ContractSchemas.AvailabilityView,
            """{"legalEntityId":"le","scope":{},"onHand":"1.000","reserved":"0.000","available":"1.000","byStatus":{"AVAILABLE":1},"asOf":"2026-09-30T10:00:00.000Z","contractVersion":"v1"}"""));
    }
}
