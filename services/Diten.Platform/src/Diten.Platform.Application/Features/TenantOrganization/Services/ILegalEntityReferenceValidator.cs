using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.TenantOrganization.Services;

public interface ILegalEntityReferenceValidator
{
    Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default);
}

public sealed record LegalEntityReferenceDto(
    Guid LegalEntityId,
    string LegalName,
    string? DisplayName,
    string LifecycleState,
    bool Referenceable);
