namespace Diten.CrmService.Application.Features.ConsentPreference;

/// <summary>
/// Read-only Contact 360 consent/preference summary. This is a <b>seam projection owned by MOD-0164</b> — MOD-0150 never
/// authors, captures or defaults consent/preference state. Until MOD-0164 exists the summary is a controlled no-op
/// (<see cref="NotAvailable"/>); when the caller lacks the read permission it is masked (<see cref="NotAuthorized"/>).
/// State strings are opaque MOD-0164 values, never treated as MOD-0048 business reference values.
/// </summary>
public sealed record ContactConsentPreferenceSummaryDto(
    Guid ContactId,
    bool ConsentAvailable,
    bool PreferenceAvailable,
    string ConsentStatus,
    string PreferenceStatus,
    DateTimeOffset? LastConsentUpdatedAt,
    DateTimeOffset? LastPreferenceUpdatedAt,
    IReadOnlyList<ContactConsentPreferenceChannelDto> Channels,
    string Source,
    string Message)
{
    public const string OwnerModule = "MOD-0164";
    public const string StatusNotAvailable = "not-available";
    public const string StatusNotAuthorized = "not-authorized";

    /// <summary>MOD-0164 absent / unavailable — no data, business flow unaffected.</summary>
    public static ContactConsentPreferenceSummaryDto NotAvailable(Guid contactId) => new(
        contactId, ConsentAvailable: false, PreferenceAvailable: false,
        ConsentStatus: StatusNotAvailable, PreferenceStatus: StatusNotAvailable,
        LastConsentUpdatedAt: null, LastPreferenceUpdatedAt: null,
        Channels: Array.Empty<ContactConsentPreferenceChannelDto>(),
        Source: OwnerModule,
        Message: "Consent and preference data is not available yet.");

    /// <summary>Caller lacks consent/preference read permission — block masked, no data leak.</summary>
    public static ContactConsentPreferenceSummaryDto NotAuthorized(Guid contactId) => new(
        contactId, ConsentAvailable: false, PreferenceAvailable: false,
        ConsentStatus: StatusNotAuthorized, PreferenceStatus: StatusNotAuthorized,
        LastConsentUpdatedAt: null, LastPreferenceUpdatedAt: null,
        Channels: Array.Empty<ContactConsentPreferenceChannelDto>(),
        Source: OwnerModule,
        Message: "Not authorized to view consent and preference data.");
}

/// <summary>Per-channel consent/preference row (read-only). Channel codes and states originate in MOD-0164.</summary>
public sealed record ContactConsentPreferenceChannelDto(
    string ChannelCode,
    string ConsentState,
    string PreferenceState,
    DateTimeOffset? LastUpdatedAt);
