namespace Diten.CrmService.Application.Features.Account.Validators;

/// <summary>
/// Shared validation constants for the optional inline account logo (base64 data URI). Kept in one place so the
/// Create and Update validators stay in lock-step. The logo is stored on the Account document itself (no external
/// file storage), so the size cap is deliberately small; the frontend enforces a matching ~256&#160;KB file cap.
/// </summary>
public static class AccountLogoRules
{
    /// <summary>Accepted raster/vector image data URIs only. Must be base64-encoded.</summary>
    public const string DataUriPattern = @"^data:image/(png|jpe?g|gif|webp|svg\+xml);base64,[A-Za-z0-9+/=\r\n]+$";

    /// <summary>Max encoded length (~512&#160;KB of base64) — headroom over the frontend's ~256&#160;KB file cap.</summary>
    public const int MaxLength = 700_000;

    public const string FormatMessage = "Logo must be a base64 image data URI (png, jpg, gif, webp or svg).";
    public const string SizeMessage = "Logo image is too large.";
}
