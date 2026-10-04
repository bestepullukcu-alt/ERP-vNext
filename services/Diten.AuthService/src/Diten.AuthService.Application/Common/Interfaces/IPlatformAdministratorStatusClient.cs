namespace Diten.AuthService.Application.Common.Interfaces;

public interface IPlatformAdministratorStatusClient
{
    Task<bool> IsActiveAsync(string email, CancellationToken ct);
    Task MarkLoginAcceptedAsync(string email, CancellationToken ct);

    /// <summary>
    /// BL-521 FIX1 F1(b) — the token actor type ("platform_admin" / "partner_admin") the administrator's Platform record
    /// names, or null when the record is not active, does not exist, names no known actor type or cannot be read. A
    /// refresh issues its new token with THIS value, never the old token's: a demoted administrator does not keep
    /// platform_admin by refreshing.
    /// </summary>
    Task<string?> GetActiveActorTypeAsync(string email, CancellationToken ct);
}
