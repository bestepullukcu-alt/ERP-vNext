using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ITenantRegistryRepository
{
    /// <summary>
    /// BL-454 stage D FIX2 K7 — the outcome of a tenant administrator's invitation, written as a TARGETED update: the
    /// provisioning step <paramref name="stepKey"/> (status, detail, time; added when missing), the administrator's
    /// <c>UpdatedAt</c> (and, when <paramref name="stampInvitedAt"/>, <c>InvitedAt</c> and FIX3's
    /// <c>LastInvitationDispatchId</c> = <paramref name="invitationDispatchId"/>), one activity line. Nothing else of the
    /// tenant is written, so a change made meanwhile (a suspension) is never put back.
    /// <para>The in-memory doubles keep this default (read, change, write); the Mongo repository writes the targeted update.</para>
    /// </summary>
    async Task RecordAdminInvitationAsync(
        Guid tenantId, Guid? adminUserId, string stepKey, string stepStatus, string detail, DateTimeOffset at,
        bool stampInvitedAt, Guid? invitationDispatchId, TenantActivityEvent activity, CancellationToken ct = default)
    {
        var tenant = await GetByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            return;
        }

        ApplyAdminInvitation(tenant, adminUserId, stepKey, stepStatus, detail, at, stampInvitedAt, invitationDispatchId, activity);
        await UpdateAsync(tenant, ct);
    }

    /// <summary>
    /// BL-454 stage D FIX3 (1) — an invitation e-mail that can no longer be delivered marks the step Failed ONLY while it is
    /// still the administrator's current invitation: the administrator is still Invited and its
    /// <c>LastInvitationDispatchId</c> is this dispatch (or absent — a record from before the field, or an invitation that
    /// sent no e-mail: the safe side is to make a locked-out administrator visible). The condition is part of the write.
    /// </summary>
    /// <returns>Whether the step was written.</returns>
    async Task<bool> RecordUndeliveredInvitationAsync(
        Guid tenantId, Guid adminUserId, Guid dispatchId, string stepKey, string detail, DateTimeOffset at,
        TenantActivityEvent activity, CancellationToken ct = default)
    {
        var tenant = await GetByIdAsync(tenantId, ct);
        var admin = tenant?.AdminUsers.FirstOrDefault(user => user.Id == adminUserId);
        if (tenant is null || admin is null || admin.Status != TenantAdminUserStatus.Invited
            || (admin.LastInvitationDispatchId is { } current && current != dispatchId))
        {
            return false;
        }

        ApplyAdminInvitation(tenant, adminUserId, stepKey, "Failed", detail, at, stampInvitedAt: false, invitationDispatchId: null, activity);
        await UpdateAsync(tenant, ct);
        return true;
    }

    private static void ApplyAdminInvitation(
        Tenant tenant, Guid? adminUserId, string stepKey, string stepStatus, string detail, DateTimeOffset at,
        bool stampInvitedAt, Guid? invitationDispatchId, TenantActivityEvent activity)
    {
        var step = tenant.ProvisioningSteps.FirstOrDefault(s => s.Key == stepKey);
        if (step is null)
        {
            step = new TenantProvisioningStep { Key = stepKey, Label = "Initial Admin Invitation", CreatedAt = at };
            tenant.ProvisioningSteps.Add(step);
        }

        step.Status = stepStatus;
        step.Detail = detail;
        step.CompletedAt = at;
        var admin = adminUserId is { } id ? tenant.AdminUsers.FirstOrDefault(user => user.Id == id) : null;
        if (admin is not null)
        {
            admin.UpdatedAt = at;
            if (stampInvitedAt)
            {
                admin.InvitedAt = at;
                admin.LastInvitationDispatchId = invitationDispatchId;
            }
        }

        tenant.ActivityTimeline.Add(activity);
    }

    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>INTX FIX2 — the tenant as this transaction sees it (never a copy read before the transaction began).</summary>
    Task<Tenant?> GetByIdAsync(IPlatformTransactionSession session, Guid id, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("The tenant repository does not implement transaction-bound reads.");
    Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default);
    Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(IPlatformTransactionSession session, Tenant tenant, CancellationToken ct = default) =>
        throw new PlatformTransactionUnavailableException("The tenant repository does not implement transaction-bound updates.");
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default);
    Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default);
}

public sealed record TenantListQuery(
    string? Search,
    string? Status,
    string? Region,
    int Page,
    int PageSize,
    string Sort);

public sealed record TenantRegistryStats(
    long Total,
    long Active,
    long Provisioning,
    long Suspended,
    long Deactivated,
    long Trial,
    long OverQuota);
