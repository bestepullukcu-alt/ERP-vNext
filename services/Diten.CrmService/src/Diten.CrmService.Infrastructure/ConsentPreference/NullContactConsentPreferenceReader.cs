using Diten.CrmService.Application.Features.ConsentPreference;

namespace Diten.CrmService.Infrastructure.ConsentPreference;

/// <summary>
/// Default MOD-0164 consent/preference reader used while MOD-0164 does not exist. Always returns the controlled
/// <see cref="ContactConsentPreferenceSummaryDto.NotAvailable"/> no-op — it fabricates no consent/preference state, no
/// granted/denied defaults and never calls a network endpoint. When MOD-0164 ships, a config-gated HTTP reader replaces
/// this registration; there is no fake/placeholder HTTP call here (no real endpoint exists yet).
/// </summary>
public sealed class NullContactConsentPreferenceReader : IContactConsentPreferenceReader
{
    public Task<ContactConsentPreferenceSummaryDto> GetSummaryAsync(Guid tenantId, Guid contactId, CancellationToken cancellationToken)
        => Task.FromResult(ContactConsentPreferenceSummaryDto.NotAvailable(contactId));
}
