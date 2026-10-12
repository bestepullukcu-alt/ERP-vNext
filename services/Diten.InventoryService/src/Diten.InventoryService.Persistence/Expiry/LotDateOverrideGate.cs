using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Traceability;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>
/// MOD-0176 F2 — the KF-D gate of createLot / Draft updateLot (pack §8.3.3; CT-F6, CT-F7). Replaces MOD-0174's <see cref="NoLotDateOverrideGate"/>.
/// <list type="number">
/// <item>Suggestion (<see cref="LotDateSuggestionRule"/>): manufacturing date + getSku <c>shelfLifeDays</c> / <c>retestDays</c>. None → no rule.</item>
/// <item>The dates equal the suggestion → no override (the suggestion is stored on the lot).</item>
/// <item>Otherwise: <c>dateOverride.overrideId</c> required (422 <c>LOT_DATE_OVERRIDE_REQUIRED</c>); the override must be the caller's, pending and
/// unexpired (409 <c>EXPIRY_DATE_OVERRIDE_NOT_USABLE</c>) and name exactly these SKU / lot number / dates (409 <c>LOT_DATE_OVERRIDE_MISMATCH</c>);
/// mode ReasonAndSignature (default, CT-F6): a signature id is required (400) and must verify (409 — fail-closed until F3).</item>
/// <item>In the lot transaction: the override is consumed (a lost race → abort, 409) and EXPIRY_DATE_OVERRIDE opened — the user who set the lot
/// dates is its origin user (CT-F10).</item>
/// </list>
/// </summary>
public sealed class LotDateOverrideGate : ILotDateOverrideGate
{
    private readonly IMongoCollection<LotDateOverride> _overrides;
    private readonly ExpiryHookWriter _writer;
    private readonly ILotDateOverrideSignature _signature;
    private readonly ITenantContext _tenant;

    public LotDateOverrideGate(IMongoDatabase database, ExpiryHookWriter writer, ILotDateOverrideSignature signature, ITenantContext tenant)
    {
        _overrides = database.GetCollection<LotDateOverride>(ExpiryCollections.DateOverrides);
        _writer = writer;
        _signature = signature;
        _tenant = tenant;
    }

    private FilterDefinition<LotDateOverride> Scope(Guid id)
        => Builders<LotDateOverride>.Filter.Eq(x => x.TenantId, _tenant.TenantId) & Builders<LotDateOverride>.Filter.Eq(x => x.LegalEntityId, _tenant.LegalEntityId)
           & Builders<LotDateOverride>.Filter.Eq(x => x.IsDeleted, false) & Builders<LotDateOverride>.Filter.Eq(x => x.Id, id);

    public async Task<LotDateGateDecision> CheckAsync(LotDateGateRequest request, CancellationToken cancellationToken = default)
    {
        var suggestion = LotDateSuggestionRule.Suggest(request.Manufacturing, request.ShelfLifeDays, request.RetestDays);
        if (suggestion.IsEmpty) return LotDateGateDecision.Free;
        if (!LotDateSuggestionRule.Differs(suggestion, request.Expiry, request.Retest))
        {
            return new LotDateGateDecision(null, suggestion.Expiry, suggestion.Retest, null, null, null);
        }

        if (request.Override?.OverrideId is not { } raw)
        {
            // TRACE's Error carries no details member (TRACE 1.4.0 leaves it unchanged): the suggestion is in the message; the FEFO
            // createDateOverride answer carries it as data (DateOverride.suggestedExpiry / suggestedRetest).
            return Refused(TraceErrorCodes.LotDateOverrideRequired,
                $"The expiry / retest date differs from the suggestion (expiry {Wire(suggestion.Expiry)}, retest {Wire(suggestion.Retest)}; manufacturing date + the SKU's shelf life / retest days): prepare an override (POST /api/expiry/date-overrides) and send dateOverride.",
                422);
        }

        var stored = Guid.TryParseExact(raw, "D", out var id) ? await _overrides.Find(Scope(id)).FirstOrDefaultAsync(cancellationToken) : null;
        if (stored is null || stored.Status != LotDateOverrideStatus.Pending || stored.ExpiresAtUtc <= _writer.Now() || stored.CreatedBy != _tenant.UserId)
        {
            return Refused(TraceErrorCodes.ExpiryDateOverrideNotUsable,
                "The date override is unknown, already used, expired or another user's; prepare a new one.", 409);
        }

        if (stored.ItemId != request.ItemId || stored.SkuId != request.SkuId || stored.SkuLevel != request.SkuLevel
            || !string.Equals(stored.LotNumber, request.LotNumber, StringComparison.Ordinal)
            || Day(stored.ManufacturingDate) != request.Manufacturing
            || Day(stored.EnteredExpiry) != request.Expiry || Day(stored.EnteredRetest) != request.Retest)
        {
            return Refused(TraceErrorCodes.LotDateOverrideMismatch,
                "The date override was prepared for another SKU, lot number or other dates than this request.", 409);
        }

        var policy = await _writer.PolicyAsync(null, cancellationToken);
        var signatureId = string.IsNullOrWhiteSpace(request.Override.SignatureId) ? null : request.Override.SignatureId.Trim();
        if (policy.DateOverrideMode == DateOverrideMode.ReasonAndSignature)
        {
            if (signatureId is null)
            {
                return Refused(TraceErrorCodes.LotDateOverrideSignatureRequired,
                    "This company requires an e-signature on a lot date apart from the suggestion (KF-D).", 400);
            }

            if (!await _signature.VerifyAsync(signatureId, stored.Id, _tenant.UserId, cancellationToken))
            {
                return Refused(TraceErrorCodes.LotDateOverrideSignatureMismatch,
                    "The signature does not belong to this date override (signer, action or subject).", 409);
            }
        }

        return new LotDateGateDecision(null, suggestion.Expiry, suggestion.Retest, stored.Id, signatureId, stored.Reason);
    }

    public async Task ApplyAsync(ILotTransaction transaction, Lot lot, LotDateGateDecision decision, CancellationToken cancellationToken = default)
    {
        if (decision.OverrideId is not { } overrideId) return;
        var session = transaction.Session as IClientSessionHandle
                      ?? throw new InvalidOperationException("The lot store lends no transaction session; the date override cannot be consumed with the lot.");

        var now = _writer.Now();
        var consumed = await _overrides.FindOneAndUpdateAsync(session,
            Scope(overrideId) & Builders<LotDateOverride>.Filter.Eq(x => x.Status, LotDateOverrideStatus.Pending)
            & Builders<LotDateOverride>.Filter.Gt(x => x.ExpiresAtUtc, now),
            Builders<LotDateOverride>.Update.Set(x => x.Status, LotDateOverrideStatus.Consumed).Set(x => x.ConsumedByLotId, lot.Id)
                .Set(x => x.ConsumedAtUtc, now).Set(x => x.UpdatedAt, now).Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<LotDateOverride> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (consumed is null)
        {
            throw new LotDateOverrideConflictException(TraceErrorCodes.ExpiryDateOverrideNotUsable, "The date override was used or expired meanwhile.");
        }

        var policy = await _writer.PolicyAsync(session, cancellationToken);
        var caseId = await _writer.OpenAsync(session, new HookCase(
            ExpiryExceptionType.ExpiryDateOverride, $"EXPIRY_DATE_OVERRIDE:{lot.Id:D}:{overrideId:D}", lot, null, null, null, null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["overrideId"] = overrideId.ToString("D"),
                ["suggestedExpiry"] = ExpiryHookWriter.Day(consumed.SuggestedExpiry),
                ["enteredExpiry"] = ExpiryHookWriter.Day(consumed.EnteredExpiry),
                ["suggestedRetest"] = ExpiryHookWriter.Day(consumed.SuggestedRetest),
                ["enteredRetest"] = ExpiryHookWriter.Day(consumed.EnteredRetest),
                ["reason"] = consumed.Reason,
                ["signatureId"] = decision.SignatureId ?? "",
                ["mode"] = policy.DateOverrideMode.ToString()
            },
            OriginUserId: _tenant.UserId, // CT-F10: the user who set the lot dates may not resolve it
            Description: consumed.Reason), policy, cancellationToken);
        await _writer.AuditAsync(session, ExpiryContract.AuditDateOverrideConsumed, "expiry.date-override", overrideId, null, _tenant.UserId,
            new Dictionary<string, string?> { ["lotId"] = lot.Id.ToString("D"), ["exceptionId"] = caseId.ToString("D"), ["signatureId"] = decision.SignatureId },
            cancellationToken);
    }

    private static LotDateGateDecision Refused(string code, string message, int status, IReadOnlyDictionary<string, object?>? details = null)
        => new(new LotDateGateRefusal(code, message, status, details), null, null, null, null, null);

    private static DateOnly? Day(DateTimeOffset? value) => value is { } v ? DateOnly.FromDateTime(v.UtcDateTime) : null;

    private static string Wire(DateOnly? day) => day?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "—";
}
