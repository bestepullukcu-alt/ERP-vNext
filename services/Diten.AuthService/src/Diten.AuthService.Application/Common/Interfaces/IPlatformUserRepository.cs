using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPlatformUserRepository
{
    Task<PlatformUser?> GetByEmailAsync(string email, CancellationToken ct);
    Task<PlatformUser> CreateAsync(PlatformUser user, CancellationToken ct);
}
