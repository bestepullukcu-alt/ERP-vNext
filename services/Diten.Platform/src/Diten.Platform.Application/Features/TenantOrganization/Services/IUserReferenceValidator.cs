using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.TenantOrganization.Services;

public interface IUserReferenceValidator
{
    Task<Response<UserReferenceDto>> ValidateAsync(Guid userId, CancellationToken ct = default);
}

public sealed record UserReferenceDto(
    Guid UserId,
    bool Referenceable);
