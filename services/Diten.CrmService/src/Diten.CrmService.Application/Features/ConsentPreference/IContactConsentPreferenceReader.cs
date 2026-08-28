namespace Diten.CrmService.Application.Features.ConsentPreference;

/// <summary>
/// Read-only seam to the MOD-0164 consent/preference store. MOD-0150 depends on this <b>softly</b>: there is no hard
/// dependency, no consent engine and no capture here. Implementations MUST be fail-soft — if MOD-0164 is unavailable or
/// errors, they return <see cref="ContactConsentPreferenceSummaryDto.NotAvailable"/> and never throw into the Contact 360
/// flow. The default registration is <c>NullContactConsentPreferenceReader</c> (no-op) until MOD-0164 ships.
/// </summary>
public interface IContactConsentPreferenceReader
{
    Task<ContactConsentPreferenceSummaryDto> GetSummaryAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken);
}
