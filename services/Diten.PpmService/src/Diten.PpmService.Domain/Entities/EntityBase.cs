using System.Text;

namespace Diten.PpmService.Domain.Entities;

public abstract class EntityBase
{
    public Guid Id { get; protected set; }
    public Guid TenantId { get; protected set; }
    public DateTime CreatedAtUtc { get; protected set; }
    public DateTime? UpdatedAtUtc { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAtUtc { get; protected set; }
    public int Version { get; protected set; }
    public Guid CreatedBy { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    protected EntityBase() { }

    protected EntityBase(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (actorId == Guid.Empty) throw new ArgumentException("ActorId is required.", nameof(actorId));
        Id = Guid.NewGuid();
        TenantId = tenantId;
        CreatedBy = actorId;
        CreatedAtUtc = DateTime.UtcNow;
        Version = 1;
    }

    public void MarkUpdated(Guid actorId)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("ActorId is required.", nameof(actorId));
        UpdatedBy = actorId;
        UpdatedAtUtc = DateTime.UtcNow;
        Version++;
    }

    public void SoftDelete(Guid actorId)
    {
        if (IsDeleted) return;
        MarkUpdated(actorId);
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
    }

    protected static string Required(string value, int maxLength, string name)
    {
        var normalized = value?.Trim().Normalize(NormalizationForm.FormC);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
            throw new ArgumentException($"{name} is required and must be at most {maxLength} characters.", name);
        return normalized;
    }

    protected static string? Optional(string? value, int maxLength, string name)
    {
        var normalized = value?.Trim().Normalize(NormalizationForm.FormC);
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maxLength) throw new ArgumentException($"{name} must be at most {maxLength} characters.", name);
        return normalized;
    }
}
