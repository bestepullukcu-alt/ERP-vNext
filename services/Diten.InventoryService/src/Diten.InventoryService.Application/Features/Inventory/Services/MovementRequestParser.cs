using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// Structural rules of pack §8 / §12 for a movement request (everything decidable WITHOUT an upstream call).
/// Used by the validator (to refuse) and by the handler (to read typed values) so the two can never disagree.
/// Errors are returned in rule order; the first one's code becomes <c>error.code</c>.
/// </summary>
public static class MovementRequestParser
{
    /// <summary>Upper bound for free-text identifiers (warehouse, location, lot, source refs, uom).</summary>
    public const int MaxTextLength = 200;

    /// <summary>INVENTORY 1.3.0 <c>fefoBypassReason</c> maximum length.</summary>
    public const int MaxFefoBypassReasonLength = 1000;

    public static (ParsedMovement? Movement, IReadOnlyList<FieldError> Errors) Parse(PostMovementCommand command)
    {
        var errors = new List<FieldError>();

        // movementType first, so an unknown type is reported as INVALID_MOVEMENT_TYPE (pack §12).
        MovementType movementType = default;
        var movementTypeOk = false;
        if (string.IsNullOrWhiteSpace(command.MovementType))
        {
            errors.Add(Invalid("movementType is required."));
        }
        else if (!WireNames.TryParseMovementType(command.MovementType, out movementType))
        {
            errors.Add(new FieldError(InventoryErrorCodes.InvalidMovementType,
                $"movementType '{command.MovementType}' is not one of the 15 canonical movement types."));
        }
        else
        {
            movementTypeOk = true;
        }

        // Idempotency-Key header AND body idempotencyKey: both present, equal after trim, 1–128 chars.
        var headerKey = command.HeaderIdempotencyKey?.Trim();
        var bodyKey = command.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(headerKey) || string.IsNullOrEmpty(bodyKey))
        {
            errors.Add(Invalid("Both the Idempotency-Key header and body idempotencyKey are required."));
        }
        else if (!string.Equals(headerKey, bodyKey, StringComparison.Ordinal))
        {
            errors.Add(Invalid("The Idempotency-Key header must equal body idempotencyKey."));
        }
        else if (headerKey.Length > InventoryContract.MaxIdempotencyKeyLength)
        {
            errors.Add(Invalid($"idempotencyKey must be 1-{InventoryContract.MaxIdempotencyKeyLength} characters."));
        }

        if (!WireParsing.TryParseUuid(command.ItemId, out var itemId))
            errors.Add(Invalid("itemId is required and must be a UUID."));
        if (!WireParsing.TryParseUuid(command.SkuId, out var skuId))
            errors.Add(Invalid("skuId is required and must be a UUID."));
        if (!WireNames.TryParseSkuLevel(command.SkuLevel, out var skuLevel))
            errors.Add(Invalid("skuLevel is required and must be one of Gsku, Lsku, FinishedGood."));

        if (!InventoryQuantity.TryParsePositive(command.Quantity, out var quantity))
            errors.Add(Invalid($"quantity must be a decimal string greater than 0 with at most {InventoryQuantity.MaxFractionDigits} fraction digits."));

        RequireText(command.UomId, "uomId", errors);
        RequireText(command.SourceModule, "sourceModule", errors);
        RequireText(command.SourceType, "sourceType", errors);

        foreach (var (value, name) in new[]
                 {
                     (command.DocumentGroupId, "documentGroupId"), (command.MovementReason, "movementReason"),
                     (command.FromWarehouseId, "fromWarehouseId"), (command.FromLocationId, "fromLocationId"),
                     (command.ToWarehouseId, "toWarehouseId"), (command.ToLocationId, "toLocationId"),
                     (command.LotId, "lotId"), (command.SourceDocumentId, "sourceDocumentId"),
                     (command.SourceLineId, "sourceLineId"), (command.SourceSystem, "sourceSystem")
                 })
        {
            if (value is not null && value.Length > MaxTextLength)
                errors.Add(Invalid($"{name} must be at most {MaxTextLength} characters."));
        }

        StockStatus? fromStatus = null;
        StockStatus? toStatus = null;
        var statusesOk = true;
        if (command.FromStockStatus is not null)
        {
            if (WireNames.TryParseStockStatus(command.FromStockStatus, out var parsed)) fromStatus = parsed;
            else { statusesOk = false; errors.Add(Invalid("fromStockStatus is not a StockStatus value.")); }
        }

        if (command.ToStockStatus is not null)
        {
            if (WireNames.TryParseStockStatus(command.ToStockStatus, out var parsed)) toStatus = parsed;
            else { statusesOk = false; errors.Add(Invalid("toStockStatus is not a StockStatus value.")); }
        }

        IReadOnlyList<string>? serialIds = null;
        if (command.SerialIds is not null)
        {
            if (command.SerialIds.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > MaxTextLength))
                errors.Add(Invalid("serialIds must not contain blank or over-long entries."));
            else
                serialIds = command.SerialIds.ToArray();
        }

        if (!WireParsing.TryParseUtcTimestamp(command.TransactionDate, out var transactionDate))
            errors.Add(Invalid("transactionDate is required and must be a UTC ISO-8601 date-time."));
        if (!WireParsing.TryParseUtcTimestamp(command.PostingDate, out var postingDate))
            errors.Add(Invalid("postingDate is required and must be a UTC ISO-8601 date-time."));

        MovementDirection? direction = null;
        if (movementTypeOk && statusesOk)
        {
            var (resolved, directionError) = MovementDirectionRules.Resolve(
                movementType,
                command.FromWarehouseId, command.FromLocationId, fromStatus,
                command.ToWarehouseId, command.ToLocationId, toStatus);
            if (directionError is not null)
                errors.Add(Invalid(directionError));
            direction = resolved;

            if (movementType == MovementType.Reversal && (!string.IsNullOrWhiteSpace(command.LotId) || command.SerialIds is not null))
                errors.Add(Invalid("REVERSAL derives lotId and serialIds from the original transaction: they must be omitted."));
        }

        // INVENTORY 1.3.0 (MOD-0176 KF-B): fefoBypassReason is optional; blank = absent; the 10-character floor is the hook's 422.
        var fefoBypassReason = Blank(command.FefoBypassReason)?.Trim();
        if (fefoBypassReason is { Length: > MaxFefoBypassReasonLength })
            errors.Add(Invalid($"fefoBypassReason must be at most {MaxFefoBypassReasonLength} characters."));

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ParsedMovement(
            headerKey!,
            Blank(command.DocumentGroupId),
            movementType,
            Blank(command.MovementReason),
            itemId,
            skuId,
            skuLevel,
            quantity,
            command.UomId!.Trim(),
            Blank(command.LotId),
            serialIds,
            direction,
            command.SourceModule!.Trim(),
            command.SourceType!.Trim(),
            Blank(command.SourceDocumentId),
            Blank(command.SourceLineId),
            Blank(command.SourceSystem),
            transactionDate,
            postingDate,
            fefoBypassReason), errors);
    }

    private static void RequireText(string? value, string name, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(Invalid($"{name} is required."));
        else if (value.Length > MaxTextLength)
            errors.Add(Invalid($"{name} must be at most {MaxTextLength} characters."));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static FieldError Invalid(string message) => new(InventoryErrorCodes.InvalidRequest, message);
}
