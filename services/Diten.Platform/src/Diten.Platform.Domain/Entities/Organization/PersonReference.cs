using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public enum PersonReferenceStatus
{
    Active = 1,
    Inactive = 2,
    Deprecated = 3,
    Deleted = 4
}

public sealed class PersonReference : TenantScopedEntity
{
    public required string DisplayName { get; set; }
    public string? ReferenceCode { get; set; }
    public PersonReferenceStatus Status { get; set; } = PersonReferenceStatus.Active;
    public string? ProfilePointer { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsReferenceable => !IsDeleted && Status == PersonReferenceStatus.Active;
}
