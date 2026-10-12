using System.Text.Json;

namespace Diten.Web.Models.SupplyChain.Returns;

// MOD-0186 Reverse Logistics — Returns tenant UI (pack §32). DRAFT overlay — not built, not writer-complete.
//
// These types carry NO business rule of their own. They exist so that the same-origin MVC adapter can
//   (1) read the one routing value it needs from the browser's body without rewriting that body (create: shipmentId,
//       transition: targetStatus), because the body text is forwarded to the Gateway exactly as received (§32.8
//       "same key and identical body"), and
//   (2) project the Shipment detail down to number/status/lines and read the lifecycle root server-side
//       (§32.4 "root never returned to the browser").
// Field validation stays with the backend (ReturnModels.cs:15-24 ReturnWire.CreateValid/TransitionValid in the accepted
// source normal-source.tar.gz edb759a0…5a21): presence ≠ nonempty, no client tightening (§32.6).

/// <summary>Permission keys the Returns UI checks. Existing keys only — ReturnPermissions.cs (Read, Create, Transition
/// and the six values of <c>ForTarget</c>) plus the approved G-SHIPREAD prerequisite.</summary>
public static class ReturnUiPermissions
{
    public const string Read = "supplychain.returns.read";               // ReturnPermissions.Read
    public const string Create = "supplychain.returns.create";           // ReturnPermissions.Create
    public const string Transition = "supplychain.returns.transition";   // ReturnPermissions.Transition
    public const string Authorize = "supplychain.returns.authorize";     // ForTarget(Authorized|Rejected)
    public const string Transit = "supplychain.returns.transit";         // ForTarget(InTransit)
    public const string Cancel = "supplychain.returns.cancel";           // ForTarget(Cancelled)
    public const string Receive = "supplychain.returns.receive";         // ForTarget(Received)
    public const string Disposition = "supplychain.returns.disposition"; // ForTarget(Dispositioned)
    public const string Close = "supplychain.returns.close";             // ForTarget(Closed)
    public const string ShipmentRead = "supplychain.shipments.read";     // G-SHIPREAD prerequisite (pack §32.4)

    /// <summary>
    /// Mirror of the backend target map <c>ReturnPermissions.ForTarget</c> (ReturnPermissions.cs:7-16). Any other text
    /// (for example "Requested" or an unknown value) has no target key; the backend then answers 422 or 400.
    /// </summary>
    public static string? ForTarget(string? targetStatus) => targetStatus switch
    {
        "Authorized" or "Rejected" => Authorize,
        "InTransit" => Transit,
        "Cancelled" => Cancel,
        "Received" => Receive,
        "Dispositioned" => Disposition,
        "Closed" => Close,
        _ => null
    };
}

/// <summary>Return lifecycle display facts used by the page (display only; the backend stays authoritative).</summary>
public static class ReturnUiLifecycle
{
    /// <summary>Wire names of ReturnStatus in declaration order (ReturnStatus.cs:2).</summary>
    public static readonly IReadOnlyList<string> Statuses =
        ["Requested", "Authorized", "Rejected", "InTransit", "Received", "Dispositioned", "Closed", "Cancelled"];

    /// <summary>Allowed arrows, ReturnLifecycle.cs:4-9. InTransit→Cancelled does not exist (annex correction).</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Transitions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Requested"] = ["Authorized", "Rejected"],
            ["Authorized"] = ["InTransit", "Cancelled"],
            ["InTransit"] = ["Received"],
            ["Received"] = ["Dispositioned"],
            ["Dispositioned"] = ["Closed"],
            ["Rejected"] = [],
            ["Cancelled"] = [],
            ["Closed"] = []
        };

    /// <summary>Shipment statuses eligible for a new return, ReturnRepository.cs:57 (display note only).</summary>
    public static readonly IReadOnlyList<string> EligibleShipmentStatuses = ["Delivered", "Closed"];
}

/// <summary>Values rendered into the page's permission JSON island (display decision only, UAS-001 §4).</summary>
public sealed record ReturnPagePermissions(bool CanCreate, bool CanAuthorize, bool CanTransit, bool CanCancel,
    bool CanReceive, bool CanDisposition, bool CanClose)
{
    /// <summary>
    /// Every mutation also needs supplychain.shipments.read (the adapter reads the Shipment root, G-SHIPREAD); every
    /// transition also needs supplychain.returns.transition (conjunction key, pack §33 open gap 3).
    /// </summary>
    public static ReturnPagePermissions From(Func<string, bool> has)
    {
        var shipmentRead = has(ReturnUiPermissions.ShipmentRead);
        var transition = shipmentRead && has(ReturnUiPermissions.Transition);
        return new ReturnPagePermissions(
            CanCreate: shipmentRead && has(ReturnUiPermissions.Create),
            CanAuthorize: transition && has(ReturnUiPermissions.Authorize),
            CanTransit: transition && has(ReturnUiPermissions.Transit),
            CanCancel: transition && has(ReturnUiPermissions.Cancel),
            CanReceive: transition && has(ReturnUiPermissions.Receive),
            CanDisposition: transition && has(ReturnUiPermissions.Disposition),
            CanClose: transition && has(ReturnUiPermissions.Close));
    }

    public bool CanTransitionAny => CanAuthorize || CanTransit || CanCancel || CanReceive || CanDisposition || CanClose;
}

/// <summary>One Shipment line as the browser receives it: exact wire text, no conversion (pack §32.4/§32.6).</summary>
public sealed record ReturnShipmentLineProjection(string? LineNumber, string? Quantity, string? UomId);

/// <summary>The only Shipment fields the browser receives from the resolve adapter (pack §32.4): number, status and
/// lines (lineNumber, quantity, uomId). The lifecycle root and every other Shipment field stay server-side.</summary>
public sealed record ReturnShipmentProjection(string? ShipmentNumber, string? Status,
    IReadOnlyList<ReturnShipmentLineProjection> Lines)
{
    public static ReturnShipmentProjection From(JsonElement shipmentDetail)
    {
        var lines = new List<ReturnShipmentLineProjection>();
        if (shipmentDetail.ValueKind == JsonValueKind.Object
            && shipmentDetail.TryGetProperty("lines", out var array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in array.EnumerateArray())
            {
                lines.Add(new ReturnShipmentLineProjection(
                    ReadString(line, "lineNumber"), ReadString(line, "quantity"), ReadString(line, "uomId")));
            }
        }

        return new ReturnShipmentProjection(ReadString(shipmentDetail, "shipmentNumber"),
            ReadString(shipmentDetail, "status"), lines);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public enum ReturnRootState
{
    Present,
    /// <summary>Missing, null or empty → 503 RETURN_SHIPMENT_ROOT_UNAVAILABLE (returns-semantics-v3.0.0.md line 8
    /// "Missing/null503"; ReturnReferenceReader.cs:65).</summary>
    Unavailable,
    /// <summary>Not a UUID → 502 RETURN_SHIPMENT_ROOT_INVALID (annex line 8 "malformed502"; ReturnReferenceReader.cs:66).</summary>
    Invalid
}

/// <summary>Result of reading <c>lifecycleCorrelationId</c> from a ShipmentDetail. Never derived or back-filled.</summary>
public readonly record struct ReturnRootResolution(ReturnRootState State, Guid Root)
{
    public static ReturnRootResolution From(JsonElement shipmentDetail)
    {
        if (shipmentDetail.ValueKind != JsonValueKind.Object
            || !shipmentDetail.TryGetProperty("lifecycleCorrelationId", out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return new(ReturnRootState.Unavailable, Guid.Empty);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return new(ReturnRootState.Invalid, Guid.Empty);
        }

        var text = value.GetString();
        if (string.IsNullOrEmpty(text))
        {
            return new(ReturnRootState.Unavailable, Guid.Empty);
        }

        // The nil UUID is syntactically valid (annex "Nil … syntactically valid"); the backend decides on it.
        return Guid.TryParseExact(text, "D", out var root)
            ? new(ReturnRootState.Present, root)
            : new(ReturnRootState.Invalid, Guid.Empty);
    }
}

/// <summary>
/// Reads, without rewriting, the one routing value the adapter needs from a mutation body. Anything that is not a
/// JSON object with that value as a string is a local 400 INVALID_REQUEST; every other rule is the backend's.
/// </summary>
public static class ReturnRequestReader
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
