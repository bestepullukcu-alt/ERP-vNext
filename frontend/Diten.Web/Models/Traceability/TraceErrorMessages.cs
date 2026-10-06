using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Models.Traceability;

/// <summary>
/// TRACE refusal → the resx key of its sentence (pack §11.6 <c>Error.&lt;code&gt;</c>, §13: never "403" / "Forbidden"
/// text, the correlation id goes in a details disclosure). The code is kept in the answer so support can trace it.
/// </summary>
public static class TraceErrorMessages
{
    /// <summary>Every TRACE / scope code the P3 screens can receive (the P4 migration codes come with P4).</summary>
    public static readonly IReadOnlyList<string> Codes =
    [
        "UNKNOWN_LOT", "LOT_ALREADY_EXISTS", "LOT_SKU_REQUIRED", "LOT_SKU_MISMATCH", "LOT_NUMBER_INVALID", "LOT_DATES_INVALID",
        "LOT_KEY_IMMUTABLE", "LOT_NO_CHANGES", "LOT_CONFLICT", "LOT_STATUS_TRANSITION_INVALID", "REASON_REQUIRED",
        "ACKNOWLEDGEMENT_REQUIRED", "PRODUCT_NOT_LOT_CONTROLLED", "GS1_UNREADABLE", "UNKNOWN_SERIAL", "INVALID_REQUEST",
        "IDEMPOTENCY_KEY_REUSED", "UNKNOWN_ITEM", "PRODUCT_MASTER_UNAVAILABLE", "CROSS_LE_FORBIDDEN",
        "LEGAL_ENTITY_MASTER_UNAVAILABLE", InventoryGatewayClient.CompanyNotSelected
    ];

    public const string Generic = "Error.Generic";
    public const string NoPermission = "Error.NoPermission";
    public const string SessionExpired = "Error.SessionExpired";
    public const string ServiceUnavailable = "Error.ServiceUnavailable";
    public const string Correlation = "Error.Correlation";

    public static IEnumerable<string> AllKeys =>
        Codes.Select(code => "Error." + code).Concat([Generic, NoPermission, SessionExpired, ServiceUnavailable, Correlation]);

    public static string KeyFor(string? code, int status)
    {
        if (code is not null && Codes.Contains(code, StringComparer.Ordinal)) return "Error." + code;
        return status switch
        {
            0 or 502 or 503 or 504 => ServiceUnavailable,
            401 => SessionExpired,
            403 => NoPermission,
            409 => "Error.LOT_CONFLICT",
            404 => "Error.UNKNOWN_LOT",
            _ => Generic
        };
    }

    /// <summary>The contract-shaped refusal (<see cref="InventoryProxy.Error"/>), its message already localized.</summary>
    public static IActionResult Refuse(int status, string? code, string message, string? correlationId) =>
        InventoryProxy.Error(status, code, message, correlationId);
}
