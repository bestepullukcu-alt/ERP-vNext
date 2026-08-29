using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;

public sealed class FirstGskuIdentityRetirementRecoveryRunner(
    IFirstGskuIdentityRetirementOperationRepository operations,
    FirstGskuIdentityRetirementProcessor processor)
{
    public async Task<GskuPairRetirementProcessingResult> RunAsync(
        Guid operationId,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty || string.IsNullOrWhiteSpace(leaseOwner)
            || leaseOwner.Length > 128 || leaseOwner != leaseOwner.Trim()
            || leaseOwner.Any(char.IsControl) || leaseDuration <= TimeSpan.Zero)
        {
            return new(false, null, "FIRST_GSKU_RETIREMENT_RECOVERY_REQUEST_INVALID", 400, false);
        }
        var operation = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        return operation is null
            ? new(false, null, "FIRST_GSKU_RETIREMENT_OPERATION_NOT_FOUND", 404, false)
            : await processor.RecoverAsync(
                operation, leaseOwner, leaseDuration, cancellationToken);
    }
}
