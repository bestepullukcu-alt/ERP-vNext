namespace Diten.AuthService.Domain.Entities;

public abstract class GlobalEntityBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsDeleted { get; set; } = false;

    /// <summary>UTC instant of the soft delete; null while live (and on rows deleted before this field existed).</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
