using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IFirstGskuIdentityRetirementOperationRepository
{
    Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(
        FirstGskuIdentityRetirementOperation operation, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(
        Guid operationId, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(
        FirstGskuIdentityRetirementClaimRequest request, CancellationToken cancellationToken = default);
    Task<bool> AdvanceAsync(
        FirstGskuIdentityRetirementClaim claim, FirstGskuIdentityRetirementMutation mutation,
        CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, FirstGskuIdentityRetirementRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);
}
