namespace Diten.AuthService.Application.Common.Interfaces;

public interface IAuthAuditService
{
    Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default);
}
