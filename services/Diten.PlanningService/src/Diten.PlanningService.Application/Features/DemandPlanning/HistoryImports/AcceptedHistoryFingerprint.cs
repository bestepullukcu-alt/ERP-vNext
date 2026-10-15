using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public static class AcceptedHistoryFingerprint
{
    public static string Compute(AcceptedDemandHistoryRow row)
    {
        if (row.UnitVerificationPending)
        {
            var interimContent = JsonSerializer.Serialize(new
            {
                row.TenantId, row.LegalEntityId, row.SourceSystem, row.SourceRecordKey,
                row.SkuId, row.SkuLevel, row.WarehouseId, row.OccurredOn,
                OriginalQuantity = row.OriginalQuantity.ToString("G29", CultureInfo.InvariantCulture),
                row.OriginalUomId,
                AcceptedQuantity = row.AcceptedQuantity.ToString("G29", CultureInfo.InvariantCulture),
                row.AcceptedUomId, row.UnitVerificationPending,
                row.StockoutEvidenceState
            });
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(interimContent)));
        }
        var content = row.ConversionNumerator is not null && row.ConversionDenominator is not null ?
            JsonSerializer.Serialize(new
            {
                row.TenantId, row.LegalEntityId, row.SourceSystem, row.SourceRecordKey,
                row.SkuId, row.SkuLevel, row.WarehouseId, row.OccurredOn,
                OriginalQuantity = row.OriginalQuantity.ToString("G29", CultureInfo.InvariantCulture),
                row.OriginalUomId,
                BaseQuantity = row.BaseQuantity.ToString("G29", CultureInfo.InvariantCulture),
                row.BaseUomId,
                ConversionFactor = row.ConversionFactor.ToString("G29", CultureInfo.InvariantCulture),
                ConversionNumerator = row.ConversionNumerator.Value.ToString("G29", CultureInfo.InvariantCulture),
                ConversionDenominator = row.ConversionDenominator.Value.ToString("G29", CultureInfo.InvariantCulture),
                row.ConversionContractId, row.ConversionContractVersion,
                row.StockoutEvidenceState
            }) : JsonSerializer.Serialize(new
        {
            row.TenantId, row.LegalEntityId, row.SourceSystem, row.SourceRecordKey,
            row.SkuId, row.SkuLevel, row.WarehouseId, row.OccurredOn,
            OriginalQuantity = row.OriginalQuantity.ToString("G29", CultureInfo.InvariantCulture),
            row.OriginalUomId,
            BaseQuantity = row.BaseQuantity.ToString("G29", CultureInfo.InvariantCulture),
            row.BaseUomId,
            ConversionFactor = row.ConversionFactor.ToString("G29", CultureInfo.InvariantCulture),
            row.ConversionContractId, row.ConversionContractVersion,
            row.StockoutEvidenceState
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}
