using System.Text.Json;

namespace Diten.Web.Models.SupplyChain.Claims;

// MOD-0187 Claims tenant UI (pack §32). DRAFT overlay — not built, not writer-complete.
//
// These types carry NO business rule of their own. They exist so that the same-origin MVC adapter can
//   (1) read the two values it needs from the browser's body without rewriting that body (create: shipmentId,
//       transition: targetStatus), because the body text is forwarded to the Gateway exactly as received (§32.8
//       "same key and identical body text"), and
//   (2) project the Shipment detail down to number/status/carrierId and read the lifecycle root server-side
//       (§32.4 "root never returned to the browser").
// Field validation stays with the backend (ClaimModels.cs:29-39 ClaimWire.CreateValid/TransitionValid in the accepted
// archive normal-source.tar.gz edb759a0…): presence ≠ nonempty, no client tightening (§32.6).

/// <summary>Permission keys the Claims UI checks. Existing keys only — ClaimPermissions.cs:4-8 plus G-SHIPREAD.</summary>
public static class ClaimUiPermissions
{
    public const string Read = "supplychain.claims.read";            // ClaimPermissions.cs:4
    public const string Create = "supplychain.claims.create";        // ClaimPermissions.cs:5
    public const string Investigate = "supplychain.claims.investigate"; // ClaimPermissions.cs:6
    public const string Decide = "supplychain.claims.decide";        // ClaimPermissions.cs:7
    public const string Settle = "supplychain.claims.settle";        // ClaimPermissions.cs:8
    public const string ShipmentRead = "supplychain.shipments.read"; // G-SHIPREAD prerequisite (pack §32.4)

    /// <summary>
    /// Mirror of the backend target map ClaimModels.cs:45 <c>ClaimWire.Permission(ClaimStatus)</c>:
    /// Open→create, Investigating/Withdrawn→investigate, Settled→settle, every other target→decide.
    /// Unknown target text also maps to decide; the backend then rejects the body with 400.
    /// </summary>
    public static string ForTarget(string? targetStatus) => targetStatus switch
    {
        "Open" => Create,
        "Investigating" or "Withdrawn" => Investigate,
        "Settled" => Settle,
        _ => Decide
    };
}

/// <summary>Claim lifecycle display facts used by the page (display only; the backend stays authoritative).</summary>
public static class ClaimUiLifecycle
{
    /// <summary>Wire names of ClaimStatus in declaration order (Domain ClaimStatus enum).</summary>
    public static readonly IReadOnlyList<string> Statuses =
        ["Open", "Investigating", "Approved", "Rejected", "Settled", "Closed", "Withdrawn"];

    /// <summary>Allowed arrows, ClaimLifecycle.cs:4-7. Withdrawn and Closed have none.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Open"] = ["Investigating", "Withdrawn"],
            ["Investigating"] = ["Approved", "Rejected"],
            ["Approved"] = ["Settled"],
            ["Rejected"] = ["Closed"],
            ["Settled"] = ["Closed"],
            ["Closed"] = [],
            ["Withdrawn"] = []
        };

    /// <summary>Shipment statuses eligible for a new claim, ClaimLifecycle.cs:10 (display note only).</summary>
    public static readonly IReadOnlyList<string> EligibleShipmentStatuses =
        ["Dispatched", "InTransit", "Delivered", "Exception", "Closed"];
}

/// <summary>Values rendered into the page's permission JSON island (display decision only, UAS-001 §4).</summary>
public sealed record ClaimPagePermissions(bool CanCreate, bool CanInvestigate, bool CanDecide, bool CanSettle)
{
    /// <summary>Every mutation also needs supplychain.shipments.read, because the adapter must read the Shipment root.</summary>
    public static ClaimPagePermissions From(Func<string, bool> has)
    {
        var shipmentRead = has(ClaimUiPermissions.ShipmentRead);
        return new ClaimPagePermissions(
            CanCreate: shipmentRead && has(ClaimUiPermissions.Create),
            CanInvestigate: shipmentRead && has(ClaimUiPermissions.Investigate),
            CanDecide: shipmentRead && has(ClaimUiPermissions.Decide),
            CanSettle: shipmentRead && has(ClaimUiPermissions.Settle));
    }
}

/// <summary>The only Shipment fields the browser receives from the resolve adapter (pack §32.4).</summary>
public sealed record ClaimShipmentProjection(string? ShipmentNumber, string? Status, string? CarrierId)
{
    public static ClaimShipmentProjection From(JsonElement shipmentDetail) => new(
        ReadString(shipmentDetail, "shipmentNumber"),
        ReadString(shipmentDetail, "status"),
        ReadString(shipmentDetail, "carrierId"));

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public enum ClaimRootState
{
    Present,
    /// <summary>Missing, null or empty → 503 CLAIM_REFERENCE_INCOMPLETE (claims-semantics-v3.0.0.md line 8).</summary>
    Incomplete,
    /// <summary>Not a UUID → 502 CLAIM_REFERENCE_INVALID (claims-semantics-v3.0.0.md line 8).</summary>
    Invalid
}

/// <summary>Result of reading <c>lifecycleCorrelationId</c> from a ShipmentDetail. Never derived or back-filled.</summary>
public readonly record struct ClaimRootResolution(ClaimRootState State, Guid Root)
{
    public static ClaimRootResolution From(JsonElement shipmentDetail)
    {
        if (shipmentDetail.ValueKind != JsonValueKind.Object
            || !shipmentDetail.TryGetProperty("lifecycleCorrelationId", out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return new(ClaimRootState.Incomplete, Guid.Empty);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return new(ClaimRootState.Invalid, Guid.Empty);
        }

        var text = value.GetString();
        if (string.IsNullOrEmpty(text))
        {
            return new(ClaimRootState.Incomplete, Guid.Empty);
        }

        // The nil UUID is a valid UUID ("same nil is not automatically missing"); the backend decides on it.
        return Guid.TryParseExact(text, "D", out var root)
            ? new(ClaimRootState.Present, root)
            : new(ClaimRootState.Invalid, Guid.Empty);
    }
}

/// <summary>
/// Reads, without rewriting, the one routing value the adapter needs from a mutation body. Anything that is not a
/// JSON object with that value as a string is a local 400 INVALID_REQUEST; every other rule is the backend's.
/// </summary>
public static class ClaimRequestReader
{
    public static bool TryReadCreateShipmentId(string body, out Guid shipmentId)
    {
        shipmentId = Guid.Empty;
        return TryReadString(body, "shipmentId", out var text)
               && Guid.TryParseExact(text, "D", out shipmentId);
    }

    public static bool TryReadTransitionTarget(string body, out string targetStatus)
    {
        targetStatus = string.Empty;
        if (!TryReadString(body, "targetStatus", out var text))
        {
            return false;
        }

        targetStatus = text;
        return true;
    }

    private static bool TryReadString(string body, string name, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(name, out var element)
                || element.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = element.GetString() ?? string.Empty;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
