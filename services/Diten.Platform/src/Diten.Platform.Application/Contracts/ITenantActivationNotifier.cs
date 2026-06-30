namespace Diten.Platform.Application.Contracts;

/// <summary>
/// FIX-ONBOARDING (B1) — notifies AuthService that a tenant is active (S2S) so it provisions default roles and
/// syncs entitled-module permissions (FIX-2) automatically, with no manual trigger. Implementations are
/// best-effort: a failure must never block tenant creation, and the call is idempotent (AuthService dedups by
/// event id).
/// </summary>
public interface ITenantActivationNotifier
{
    Task NotifyActivatedAsync(Guid tenantId, CancellationToken ct = default);
}
