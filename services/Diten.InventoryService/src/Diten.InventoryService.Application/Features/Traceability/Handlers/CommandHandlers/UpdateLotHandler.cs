using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations;
using Diten.InventoryService.Application.Features.Traceability.Commands;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;

/// <summary>
/// updateLot (K-C, CT-D8; pack §12 rows 3–9, R, E, C). The key members (lot number, item, SKU, status) never reach this
/// handler — the request reader refuses them with 400 <c>LOT_KEY_IMMUTABLE</c>. Order:
/// <list type="number">
/// <item>Shape (400): <c>If-Match</c>, attribute lengths / country, date formats (<c>LOT_DATES_INVALID</c>).</item>
/// <item>In one transaction: the lot (404); the permission its status needs — Draft: <c>trace.lots.update</c>, any other
/// status: <c>trace.lots.update-released</c> (403); a non-Draft lot needs a reason of ≥ 10 characters (400
/// <c>REASON_REQUIRED</c>); the ETag (409 <c>LOT_CONFLICT</c>); at least one effective change (422 <c>LOT_NO_CHANGES</c>);
/// the date order of the RESULT (400 <c>LOT_DATES_INVALID</c>); replace + one <c>Updated</c> history row per changed field
/// (old → new + reason).</item>
/// </list>
/// <para><b>MOD-0175 CT-Q17 (WP-0175-Q4R).</b> On a non-Draft lot <c>retestDate</c> may only move EARLIER (or be set where none was):
/// later or null → 400 <c>LOT_KEY_IMMUTABLE</c> (the status the code already has, <c>TraceRequestReader</c>) — a later retest date
/// comes only from an approved MOD-0175 retest release (<c>ApplyApprovedLotStatusChange</c>, which does not pass through this
/// handler). A Draft lot stays free. An earlier date that makes the lot due blocks postings at once (the posting rule reads the date);
/// the stored retest hold (availability, new reservations) is set by the retest sweeper on its next run (CT-Q16).</para>
/// </summary>
public sealed class UpdateLotHandler : IRequestHandler<UpdateLotCommand, Response<LotRefDto>>
{
    private readonly ILotRepository _lots;
    private readonly TraceWriteSupport _support;

    public UpdateLotHandler(ILotRepository lots, TraceWriteSupport support)
    {
        _lots = lots;
        _support = support;
    }

    public async Task<Response<LotRefDto>> Handle(UpdateLotCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TraceMapping.Today(now);

        if (string.IsNullOrWhiteSpace(request.IfMatch))
            return TraceWriteSupport.Invalid<LotRefDto>("The If-Match header is required.");

        var attributes = LotAttributes.Read(Value(request.ManufacturerLot), Value(request.SupplierLot),
            Value(request.CountryOfOrigin), Value(request.CoaReference));
        if (attributes.Error is not null) return TraceWriteSupport.Invalid<LotRefDto>(attributes.Error);

        if (!LotAttributes.TryDate("manufacturingDate", Value(request.ManufacturingDate), out var manufacturing, out var dateError)
            || !LotAttributes.TryDate("expiryDate", Value(request.ExpiryDate), out var expiry, out dateError)
            || !LotAttributes.TryDate("retestDate", Value(request.RetestDate), out var retest, out dateError))
        {
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotDatesInvalid, dateError!, 400);
        }

        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId)) return TraceWriteSupport.UnknownLot<LotRefDto>();
        var expectedVersion = TraceWriteSupport.ExpectedVersion(request.IfMatch);
        var reason = TraceWriteSupport.Trimmed(request.Reason);

        return await _lots.InTransactionAsync(async (tx, ct) =>
        {
            var lot = await tx.FindAsync(lotId, ct);
            if (lot is null) return TraceWriteSupport.UnknownLot<LotRefDto>();

            var needsReleasedRight = LotStatusRules.EditNeedsReason(lot.Status);
            if (needsReleasedRight ? !request.CanUpdateReleased : !request.CanUpdateDraft)
            {
                return Response<LotRefDto>.Fail(needsReleasedRight
                    ? "Editing a lot that is not Draft needs the trace.lots.update-released permission."
                    : "Editing a Draft lot needs the trace.lots.update permission.", 403);
            }

            if (needsReleasedRight && !LotStatusRules.IsReasonValid(reason))
                return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.ReasonRequired,
                    $"A {lot.Status} lot is edited only with a reason of at least {LotStatusRules.MinReasonLength} characters.", 400);
            if (lot.Version != expectedVersion) return TraceWriteSupport.Conflict<LotRefDto>();
            if (lot.Status != LotStatus.Draft && request.RetestDate.IsPresent && LotDateRules.RetestMovesLater(TraceMapping.Day(lot.RetestDate), retest))
                return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotKeyImmutable,
                    "The retest date of a lot that is not Draft is moved later (or removed) only by an approved MOD-0175 retest decision; here it may only move earlier.",
                    400);

            var changes = new List<(string Field, string? Old, string? New)>();
            Apply(request.ManufacturerLot, "manufacturerLot", lot.ManufacturerLot, attributes.ManufacturerLot, v => lot.ManufacturerLot = v, changes);
            Apply(request.SupplierLot, "supplierLot", lot.SupplierLot, attributes.SupplierLot, v => lot.SupplierLot = v, changes);
            Apply(request.CountryOfOrigin, "countryOfOrigin", lot.CountryOfOrigin, attributes.Country, v => lot.CountryOfOrigin = v, changes);
            Apply(request.CoaReference, "coaReference", lot.CoaReference, attributes.Coa, v => lot.CoaReference = v, changes);
            ApplyDate(request.ManufacturingDate, "manufacturingDate", lot.ManufacturingDate, manufacturing, v => lot.ManufacturingDate = v, changes);
            ApplyDate(request.ExpiryDate, "expiryDate", lot.ExpiryDate, expiry, v => lot.ExpiryDate = v, changes);
            ApplyDate(request.RetestDate, "retestDate", lot.RetestDate, retest, v => lot.RetestDate = v, changes);

            if (changes.Count == 0)
                return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotNoChanges, "The request changes nothing.", 422);

            var orderError = LotDateRules.Validate(TraceMapping.Day(lot.ManufacturingDate), TraceMapping.Day(lot.ExpiryDate),
                TraceMapping.Day(lot.RetestDate), today);
            if (orderError is not null) return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotDatesInvalid, orderError, 400);

            lot.UpdatedAt = now;
            lot.UpdatedBy = _support.UserId;
            if (!await tx.ReplaceAsync(lot, expectedVersion, ct)) return TraceWriteSupport.Conflict<LotRefDto>();

            var entries = changes
                .Select(c => _support.History(lot.Id, LotChangeAction.Updated, c.Field, c.Old, c.New, reason, now))
                .ToList();
            await tx.AppendHistoryAsync(entries, ct);
            return Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 200);
        }, cancellationToken);
    }

    private static string? Value(Patch<string> patch) => patch.IsPresent ? patch.Value : null;

    private static void Apply(Patch<string> patch, string field, string? current, string? next, Action<string?> set,
        List<(string, string?, string?)> changes)
    {
        if (!patch.IsPresent || string.Equals(current, next, StringComparison.Ordinal)) return;
        changes.Add((field, current, next));
        set(next);
    }

    private static void ApplyDate(Patch<string> patch, string field, DateTimeOffset? current, DateOnly? next,
        Action<DateTimeOffset?> set, List<(string, string?, string?)> changes)
    {
        if (!patch.IsPresent) return;
        var nextValue = next is { } day ? TraceMapping.Midnight(day) : (DateTimeOffset?)null;
        if (current == nextValue) return;
        changes.Add((field, TraceMapping.Wire(current), TraceMapping.Wire(nextValue)));
        set(nextValue);
    }
}
