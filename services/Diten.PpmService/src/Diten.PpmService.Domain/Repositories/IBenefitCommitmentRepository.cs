using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Domain.Repositories;

public interface IBenefitCommitmentRepository : IRepository<BenefitCommitment>
{
    Task<bool> ExistsForInvestmentCaseAsync(Guid tenantId, Guid investmentCaseId, CancellationToken cancellationToken);
}
