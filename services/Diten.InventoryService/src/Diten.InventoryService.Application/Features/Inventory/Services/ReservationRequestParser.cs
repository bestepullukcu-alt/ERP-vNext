using System.Globalization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// Structural rules of a reservation request (contract <c>ReservationRequest</c> + pack §8 idempotency). Shared by the
/// validator and the handler. All failures are 400 <c>INVALID_REQUEST</c>.
/// </summary>
public static class ReservationRequestParser
{
    public static (ParsedReservation? Reservation, IReadOnlyList<FieldError> Errors) Parse(CreateReservationCommand command, DateTimeOffset now)
    {
        var errors = new List<FieldError>();

        var headerKey = command.HeaderIdempotencyKey?.Trim();
        var bodyKey = command.IdempotencyKey?.Trim();
        if (string.IsNullOrEmpty(headerKey) || string.IsNullOrEmpty(bodyKey))
            errors.Add(Invalid("Both the Idempotency-Key header and body idempotencyKey are required."));
        else if (!string.Equals(headerKey, bodyKey, StringComparison.Ordinal))
            errors.Add(Invalid("The Idempotency-Key header must equal body idempotencyKey."));
        else if (headerKey.Length > InventoryContract.MaxIdempotencyKeyLength)
            errors.Add(Invalid($"idempotencyKey must be 1-{InventoryContract.MaxIdempotencyKeyLength} characters."));

        if (!WireParsing.TryParseUuid(command.ItemId, out var itemId))
            errors.Add(Invalid("itemId is required and must be a UUID."));
        if (!WireParsing.TryParseUuid(command.SkuId, out var skuId))
            errors.Add(Invalid("skuId is required and must be a UUID."));
        if (!WireNames.TryParseSkuLevel(command.SkuLevel, out var skuLevel))
            errors.Add(Invalid("skuLevel is required and must be one of Gsku, Lsku, FinishedGood."));

        if (!InventoryQuantity.TryParsePositive(command.Quantity, out var quantity))
            errors.Add(Invalid($"quantity must be a decimal string greater than 0 with at most {InventoryQuantity.MaxFractionDigits} fraction digits."));
        else if (command.BaseQuantity is not null
                 && (!InventoryQuantity.TryParsePositive(command.BaseQuantity, out var baseQuantity) || baseQuantity != quantity))
            errors.Add(Invalid("baseQuantity, when sent, must equal quantity: reservations are held in the SKU base unit."));

        RequireText(command.ReservationType, "reservationType", errors);
        RequireText(command.SourceModule, "sourceModule", errors);
        foreach (var (value, name) in new[] { (command.SourceDocumentId, "sourceDocumentId"), (command.SourceLineId, "sourceLineId") })
        {
            if (value is not null && value.Length > MovementRequestParser.MaxTextLength)
                errors.Add(Invalid($"{name} must be at most {MovementRequestParser.MaxTextLength} characters."));
        }

        int? priority = null;
        if (command.Priority is not null)
        {
            if (int.TryParse(command.Priority, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var p))
                priority = p;
            else
                errors.Add(Invalid("priority must be an integer."));
        }

        DateTimeOffset? expiresAt = null;
        if (command.ExpiresAt is not null)
        {
            if (!WireParsing.TryParseUtcTimestamp(command.ExpiresAt, out var e))
                errors.Add(Invalid("expiresAt must be a UTC ISO-8601 date-time."));
            else if (e <= now)
                errors.Add(Invalid("expiresAt must be in the future."));
            else
                expiresAt = e;
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ParsedReservation(
            headerKey!, itemId, skuId, skuLevel, quantity,
            command.ReservationType!.Trim(), command.SourceModule!.Trim(),
            Blank(command.SourceDocumentId), Blank(command.SourceLineId), priority, expiresAt), errors);
    }

    private static void RequireText(string? value, string name, List<FieldError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add(Invalid($"{name} is required."));
        else if (value.Length > MovementRequestParser.MaxTextLength)
            errors.Add(Invalid($"{name} must be at most {MovementRequestParser.MaxTextLength} characters."));
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static FieldError Invalid(string message) => new(InventoryErrorCodes.InvalidRequest, message);
}
