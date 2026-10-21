using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Expiry.Commands;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Handlers;

/// <summary>
/// FEFO-BUNDLE §4.19 createDateOverride (KF-D, pack §8.3.3) — before a createLot / Draft updateLot whose expiry / retest date differs from the
/// suggestion (manufacturing date + getSku <c>shelfLifeDays</c> / <c>retestDays</c>): the caller prepares the override with a reason (10–1000);
/// it is usable for 24 h by ONE lot write of the same caller (consumed in that lot transaction). No suggestion → 422
/// <c>EXPIRY_NO_SUGGESTION</c>; the dates equal the suggestion → 422 <c>EXPIRY_DATE_EQUALS_SUGGESTION</c>. <c>signatureRequired</c> follows the
/// company's <c>dateOverrideMode</c> (CT-F6). The override is the F3 signable subject <c>expiry.date-override/{overrideId}</c>. Override + audit row
/// <c>expiry.date-override.prepared</c> in one transaction; the same key + body answers the stored override.
/// </summary>
public sealed class CreateDateOverrideHandler : IRequestHandler<CreateDateOverrideCommand, Response<ExpiryDateOverrideDto>>
{
    private readonly IExpiryRepository _repository;
    private readonly ITraceSkuDirectory _skus;
    private readonly ExpirySupport _support;

    public CreateDateOverrideHandler(IExpiryRepository repository, ITraceSkuDirectory skus, ExpirySupport support)
    {
        _repository = repository;
        _skus = skus;
        _support = support;
    }

    public async Task<Response<ExpiryDateOverrideDto>> Handle(CreateDateOverrideCommand request, CancellationToken ct)
    {
        var lotNumber = request.LotNumber is null ? null : LotIdentityPolicy.NormalizeLotNumber(request.LotNumber);
        var reason = request.Reason?.Trim();
        var fingerprint = ExpirySupport.Fingerprint(("itemId", request.ItemId.ToString("D")), ("skuId", request.SkuId.ToString("D")),
            ("skuLevel", WireNames.ToWire(request.SkuLevel)), ("lotNumber", lotNumber), ("manufacturingDate", Day(request.Manufacturing)),
            ("expiryDate", Day(request.Expiry)), ("retestDate", Day(request.Retest)), ("reason", reason));
        if (await _repository.FindDateOverrideByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return earlier.RequestFingerprint == fingerprint
                ? Response<ExpiryDateOverrideDto>.Success(Map(earlier))
                : ExpirySupport.KeyReused<ExpiryDateOverrideDto>();
        }

        if (!LotIdentityPolicy.IsValidLotNumber(lotNumber))
        {
            return ExpirySupport.Invalid<ExpiryDateOverrideDto>($"lotNumber is required: 1-{LotIdentityPolicy.MaxLotNumberLength} characters of A-Z 0-9 . / -.");
        }

        if (ExpiryExceptionRules.Reason(reason) is { } refusal) return ExpirySupport.Fail<ExpiryDateOverrideDto>(refusal);

        Traceability.Ports.TraceSku? sku;
        try
        {
            sku = await _skus.GetSkuAsync(request.SkuId, request.SkuLevel, ct);
        }
        catch (ProductMasterUnavailableException)
        {
            return ExpirySupport.Fail<ExpiryDateOverrideDto>(Common.InventoryErrorCodes.ProductMasterUnavailable,
                "PRODUCT-MASTER could not be reached; no override was prepared.", 503);
        }

        if (sku is not null && sku.ItemId != request.ItemId)
        {
            return ExpirySupport.Invalid<ExpiryDateOverrideDto>("The SKU does not belong to itemId (D-SKU-LINK).");
        }

        var suggestion = LotDateSuggestionRule.Suggest(request.Manufacturing, sku?.ShelfLifeDays, sku?.RetestDays);
        if (suggestion.IsEmpty)
        {
            return ExpirySupport.Fail<ExpiryDateOverrideDto>(ExpiryErrorCodes.NoSuggestion,
                "The SKU states no shelf life / retest days (or is unknown): there is no suggestion to override.", 422);
        }

        if (!LotDateSuggestionRule.Differs(suggestion, request.Expiry, request.Retest))
        {
            return ExpirySupport.Fail<ExpiryDateOverrideDto>(ExpiryErrorCodes.DateEqualsSuggestion,
                "The dates equal the suggestion: no override is needed.", 422);
        }

        var policy = await _repository.FindPolicyAsync(ct) ?? ExpiryPolicyRules.Defaults();
        var now = _support.Now();
        var prepared = new LotDateOverride
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            ItemId = request.ItemId,
            SkuId = request.SkuId,
            SkuLevel = request.SkuLevel,
            LotNumber = lotNumber!,
            ManufacturingDate = Midnight(request.Manufacturing),
            SuggestedExpiry = suggestion.Expiry is { } se ? Midnight(se) : null,
            EnteredExpiry = request.Expiry is { } ee ? Midnight(ee) : null,
            SuggestedRetest = suggestion.Retest is { } sr ? Midnight(sr) : null,
            EnteredRetest = request.Retest is { } er ? Midnight(er) : null,
            ShelfLifeDays = sku?.ShelfLifeDays,
            RetestDays = sku?.RetestDays,
            Reason = reason!,
            SignatureRequired = policy.DateOverrideMode == DateOverrideMode.ReasonAndSignature,
            Status = LotDateOverrideStatus.Pending,
            ExpiresAtUtc = now + ExpiryContract.DateOverrideLifetime,
            CreatedBy = _support.UserId,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            Version = 1
        };
        try
        {
            await _repository.InTransactionAsync<bool>(async (session, tx) =>
            {
                await session.InsertDateOverrideAsync(prepared, tx);
                await session.InsertAuditAsync(_support.Audit(ExpiryContract.AuditDateOverridePrepared, "expiry.date-override", prepared.Id, null, now,
                    request.IdempotencyKey, new Dictionary<string, string?>
                    {
                        ["lotNumber"] = prepared.LotNumber,
                        ["skuId"] = prepared.SkuId.ToString("D"),
                        ["signatureRequired"] = prepared.SignatureRequired ? "true" : "false",
                        ["fingerprint"] = fingerprint
                    }), tx);
                return true;
            }, ct);
        }
        catch (ExpiryDuplicateException)
        {
            return await _repository.FindDateOverrideByKeyAsync(_support.UserId, request.IdempotencyKey, ct) is { } twin && twin.RequestFingerprint == fingerprint
                ? Response<ExpiryDateOverrideDto>.Success(Map(twin))
                : ExpirySupport.KeyReused<ExpiryDateOverrideDto>();
        }

        return Response<ExpiryDateOverrideDto>.Success(Map(prepared), 201);
    }

    private static ExpiryDateOverrideDto Map(LotDateOverride o) => new(
        o.Id.ToString("D"), o.ItemId.ToString("D"), o.SkuId.ToString("D"), WireNames.ToWire(o.SkuLevel), o.LotNumber,
        ExpirySupport.WireDate(o.ManufacturingDate)!, ExpirySupport.WireDate(o.SuggestedExpiry), ExpirySupport.WireDate(o.EnteredExpiry),
        ExpirySupport.WireDate(o.SuggestedRetest), ExpirySupport.WireDate(o.EnteredRetest), o.Reason, o.SignatureRequired, o.Status.ToString(),
        ExpirySupport.Wire(o.ExpiresAtUtc), o.Version);

    private static DateTimeOffset Midnight(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static string? Day(DateOnly? day) => day?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
