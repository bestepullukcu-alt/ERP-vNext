namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// F3 subject provider — the company of one signable subject of a tenant, read WITHOUT a company scope (MOD-0022 calls the provider with
/// the signer's token + <c>X-Tenant-Id</c> only; the MOD-0175 <c>IQuarantineEventInbox.DecisionCompanyAsync</c> pattern). Null = no such
/// subject in this tenant. For a date override the creator is returned too: only its creator may see (and sign) it.
/// </summary>
public interface IExpirySubjectDirectory
{
    Task<(Guid LegalEntityId, Guid? Owner)?> SubjectAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken = default);
}
