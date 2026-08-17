using System.Diagnostics.CodeAnalysis;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities.Audit;

public sealed class TenantAuditPreference : TenantScopedEntity
{
    private const int MaxReasonLength = 500;

    [SetsRequiredMembers]
    public TenantAuditPreference()
    {
        TenantId = Guid.Empty;
    }

    public AuditCategory Category { get; set; }
    public int RetentionDays { get; set; }
    public DateTimeOffset EffectiveFromUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedByActorId { get; set; }
    public string? Reason { get; set; }

    public void ValidateAgainst(AuditEventRetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        if (TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant audit preference tenant id must be server-resolved.");
        }

        if (Category == AuditCategory.Unknown)
        {
            throw new InvalidOperationException("Tenant audit preference category is required.");
        }

        if (Category != policy.Category)
        {
            throw new InvalidOperationException("Tenant audit preference category must match its retention policy.");
        }

        if (RetentionDays <= 0)
        {
            throw new InvalidOperationException("Tenant audit preference retention days must be greater than zero.");
        }

        if (RetentionDays < policy.MinimumRetentionDays || RetentionDays > policy.MaximumRetentionDays)
        {
            throw new InvalidOperationException("Tenant audit preference retention days must be within the policy floor and ceiling.");
        }

        if (UpdatedByActorId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant audit preference updated-by actor id is required.");
        }

        if (Reason is { Length: > MaxReasonLength })
        {
            throw new InvalidOperationException($"Tenant audit preference reason cannot exceed {MaxReasonLength} characters.");
        }
    }
}
