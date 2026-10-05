using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>
/// checkGs1Code (K-B, proposal §4.6) — read-only. The scanned code is read by <see cref="Gs1CodeParser"/> and compared
/// with the SELECTED lot and ITS SKU:
/// <list type="bullet">
/// <item><b>Format</b> — (21) present and a valid serial (3–24 of A–Z 0–9 -);</item>
/// <item><b>Gtin</b> — (01) equals the SKU's GTIN from PRODUCT-MASTER; when PRODUCT-MASTER answers <c>gtin: null</c>
/// (MOD-0290 D-1) — and ONLY then — this one check is <c>Skipped</c> and <c>gtinSkippedReason</c> tells the UI to warn;</item>
/// <item><b>Lot</b> — (10) equals the selected lot's number: ALWAYS checked;</item>
/// <item><b>Expiry</b> — (17) equals the selected lot's expiry date: ALWAYS checked.</item>
/// </list>
/// <c>accepted</c> = Format, Lot and Expiry Ok, and Gtin Ok or Skipped. The code is written as if the GTIN were always
/// present, so nothing changes when MDM starts returning it.
/// </summary>
public sealed class CheckGs1CodeHandler : IRequestHandler<CheckGs1CodeQuery, Response<Gs1CheckResultDto>>
{
    public const string GtinNullReason = "PRODUCT_MASTER_GTIN_NULL";

    private readonly ILotRepository _lots;
    private readonly ITraceSkuDirectory _skus;

    public CheckGs1CodeHandler(ILotRepository lots, ITraceSkuDirectory skus)
    {
        _lots = lots;
        _skus = skus;
    }

    public async Task<Response<Gs1CheckResultDto>> Handle(CheckGs1CodeQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > TraceContract.MaxGs1CodeLength)
            return TraceWriteSupport.Invalid<Gs1CheckResultDto>($"code is required: 1-{TraceContract.MaxGs1CodeLength} characters.");
        if (!TraceWriteSupport.TryParseId(request.SkuId, out var skuId))
            return TraceWriteSupport.Invalid<Gs1CheckResultDto>("skuId is required and must be a UUID.");
        if (!WireNames.TryParseSkuLevel(request.SkuLevel, out var skuLevel))
            return TraceWriteSupport.Invalid<Gs1CheckResultDto>("skuLevel is required: Gsku, Lsku or FinishedGood.");
        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId)) return TraceWriteSupport.UnknownLot<Gs1CheckResultDto>();

        var lot = await _lots.FindAsync(lotId, cancellationToken);
        if (lot is null) return TraceWriteSupport.UnknownLot<Gs1CheckResultDto>();
        if (lot.SkuId != skuId || lot.SkuLevel != skuLevel)
            return TraceWriteSupport.Fail<Gs1CheckResultDto>(TraceErrorCodes.LotSkuMismatch, "The selected lot belongs to another SKU.", 422);

        var today = TraceMapping.Today(DateTimeOffset.UtcNow);
        if (!Gs1CodeParser.TryParse(request.Code, today, out var code))
            return TraceWriteSupport.Fail<Gs1CheckResultDto>(TraceErrorCodes.Gs1Unreadable,
                "The code is not a readable GS1 element string ((01) GTIN, (21) serial, (17) expiry, (10) lot).", 400);

        TraceSku? sku;
        try
        {
            sku = await _skus.GetSkuAsync(skuId, skuLevel, cancellationToken);
        }
        catch (ProductMasterUnavailableException)
        {
            return TraceWriteSupport.Fail<Gs1CheckResultDto>(TraceErrorCodes.ProductMasterUnavailable,
                "PRODUCT-MASTER could not confirm the SKU's GTIN; the code was not checked.", 503);
        }

        if (sku is null)
            return TraceWriteSupport.Fail<Gs1CheckResultDto>(TraceErrorCodes.UnknownItem, "The SKU does not exist in PRODUCT-MASTER.", 422);

        var serial = code.Serial is null ? null : LotIdentityPolicy.NormalizeSerial(code.Serial);
        var format = serial is null
            ? Check("Format", "Missing", null, null)
            : LotIdentityPolicy.IsValidSerial(serial) ? Check("Format", "Ok", null, serial) : Check("Format", "Mismatch", null, serial);

        var expectedGtin = Gs1CodeParser.Gtin14(sku.Gtin);
        var gtin = sku.Gtin is null
            ? Check("Gtin", "Skipped", null, code.Gtin)
            : code.Gtin is null ? Check("Gtin", "Missing", expectedGtin ?? sku.Gtin, null)
            : string.Equals(expectedGtin, Gs1CodeParser.Gtin14(code.Gtin), StringComparison.Ordinal)
                ? Check("Gtin", "Ok", expectedGtin, code.Gtin)
                : Check("Gtin", "Mismatch", expectedGtin ?? sku.Gtin, code.Gtin);

        var scannedLot = code.Lot is null ? null : LotIdentityPolicy.NormalizeLotNumber(code.Lot);
        var lotCheck = scannedLot is null
            ? Check("Lot", "Missing", lot.LotNumber, null)
            : string.Equals(scannedLot, lot.LotNumber, StringComparison.Ordinal)
                ? Check("Lot", "Ok", lot.LotNumber, scannedLot)
                : Check("Lot", "Mismatch", lot.LotNumber, scannedLot);

        var lotExpiry = TraceMapping.Day(lot.ExpiryDate);
        var expectedExpiry = lotExpiry?.ToString(TraceContract.DateFormat, CultureInfo.InvariantCulture);
        var scannedExpiry = code.Expiry?.ToString(TraceContract.DateFormat, CultureInfo.InvariantCulture) ?? code.ExpiryRaw;
        var expiry = code.ExpiryRaw is null
            ? lotExpiry is null ? Check("Expiry", "Ok", null, null) : Check("Expiry", "Missing", expectedExpiry, null)
            : code.Expiry is { } scanned && lotExpiry == scanned
                ? Check("Expiry", "Ok", expectedExpiry, scannedExpiry)
                : Check("Expiry", "Mismatch", expectedExpiry, scannedExpiry);

        var checks = new[] { format, gtin, lotCheck, expiry };
        var accepted = format.Result == "Ok" && lotCheck.Result == "Ok" && expiry.Result == "Ok" && gtin.Result is "Ok" or "Skipped";
        return Response<Gs1CheckResultDto>.Success(new Gs1CheckResultDto
        {
            SerialNumber = serial,
            Gtin = code.Gtin,
            LotNumber = scannedLot,
            Expiry = code.Expiry?.ToString(TraceContract.DateFormat, CultureInfo.InvariantCulture),
            Checks = checks,
            Accepted = accepted,
            GtinSkippedReason = sku.Gtin is null ? GtinNullReason : null
        });
    }

    private static Gs1CheckDto Check(string field, string result, string? expected, string? actual)
        => new() { Field = field, Result = result, Expected = expected, Actual = actual };
}
