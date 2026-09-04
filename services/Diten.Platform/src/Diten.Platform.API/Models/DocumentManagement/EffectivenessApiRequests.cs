using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

namespace Diten.Platform.API.Models.DocumentManagement;

// DCP-005 (P-EFF-P2 Faz 3) — controlled-document effectiveness batch API payload (JSON from the TenantShell proxy).
// TenantId is never accepted from the client; it is server-side resolved. The endpoint is a thin screen over the
// single ResolveDocumentEffectivenessQuery resolver — it validates request shape only and adds no business logic.

public sealed class ResolveEffectivenessApiRequest
{
    /// <summary>Which register identity field to match against: "code" or "uid" (case-insensitive). No default (§1).</summary>
    public string? By { get; set; }

    public IReadOnlyList<string>? Identifiers { get; set; }
}

// MOD-0029 — maps the effectiveness API payload to the resolver's typed input (ApiRequestMapper pattern; no business logic).
internal static class EffectivenessApiMapper
{
    /// <summary>Reason code for a malformed effectiveness request (§4/§5: 400 only on a broken request).</summary>
    public const string InvalidRequestReasonCode = "invalid_request";

    /// <summary>
    /// Strict parse of the <c>by</c> discriminator: only "code" / "uid" (case-insensitive) are accepted, so an unknown
    /// or numeric value is rejected as a 400 rather than silently coerced. No silent default (contract §1).
    /// </summary>
    public static bool TryParseIdentifierKind(string? by, out DocumentIdentifierKind kind)
    {
        switch (by?.Trim().ToLowerInvariant())
        {
            case "code":
                kind = DocumentIdentifierKind.Code;
                return true;
            case "uid":
                kind = DocumentIdentifierKind.Uid;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>True when at least one non-empty/whitespace identifier is present (empty/all-blank ⇒ malformed request).</summary>
    public static bool HasResolvableIdentifier(IReadOnlyList<string>? identifiers) =>
        identifiers is not null && identifiers.Any(i => !string.IsNullOrWhiteSpace(i));
}
