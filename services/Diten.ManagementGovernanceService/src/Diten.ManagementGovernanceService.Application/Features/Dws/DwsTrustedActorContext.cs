using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws;

public sealed record DwsTrustedActorContext(
    Guid TenantId,
    Guid SecuritySubjectId,
    Guid EffectiveActorId,
    Guid? DelegatedActorId,
    string? IdempotencyKey)
{
    public void RequireCommand()
    {
        RequireIdentities();
        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new DwsValidationException(DwsErrors.InvalidRequest);
    }

    public void RequireQuery()
    {
        RequireIdentities();
        if (IdempotencyKey is not null)
            throw new DwsValidationException(DwsErrors.InvalidRequest);
    }

    private void RequireIdentities()
    {
        if (TenantId == Guid.Empty || SecuritySubjectId == Guid.Empty || EffectiveActorId == Guid.Empty)
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);
        if (DelegatedActorId == Guid.Empty)
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);
        if (DelegatedActorId == SecuritySubjectId || DelegatedActorId == EffectiveActorId)
            throw new DwsValidationException(DwsErrors.AuthenticationRequired);
    }
}
