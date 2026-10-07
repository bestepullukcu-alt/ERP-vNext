namespace Diten.MdmService.Domain.Entities;

// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — the Brand's own, append-only trail. Every brand write (create, update, archive)
// leaves exactly one entry, written in the SAME transaction as the brand itself: the central audit forwarder
// (IAuditableCommand → AuditForwardingBehavior) is best effort and swallows a failed hand-over, so it cannot be the only
// record of who changed a master record and how. Entries are never updated or deleted.
public sealed class BrandHistoryEntry : EntityBase
{
    public const string EventCreated = "created";
    public const string EventUpdated = "updated";
    public const string EventArchived = "archived";

    public Guid BrandId { get; set; }

    /// <summary><see cref="EventCreated"/> · <see cref="EventUpdated"/> · <see cref="EventArchived"/>.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The JWT subject of whoever made the change (audit identity only, never an authorization input).</summary>
    public string? Actor { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>The brand as it was before the write; null for a create.</summary>
    public BrandAuditSnapshot? Before { get; set; }

    /// <summary>The brand as the write left it.</summary>
    public BrandAuditSnapshot After { get; set; } = new();

    /// <summary>The brand's version after the write — pairs the entry with exactly one stored state.</summary>
    public int ResultVersion { get; set; }

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX3 (M-3) — "released on archive": the external references whose reservations the archive gave
    /// up (an archived brand no longer holds a pair; another record may take it). Empty on every other event and on entries
    /// written before FIX3 (the field is absent there and reads as empty).
    /// </summary>
    public List<BrandProductExternalReference> ReleasedExternalReferences { get; set; } = [];

    public DateTimeOffset OccurredAtUtc { get; set; }
}

/// <summary>The business fields of a brand, as one point-in-time picture for <see cref="BrandHistoryEntry"/>.</summary>
public sealed class BrandAuditSnapshot
{
    public string BrandCode { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string BrandStatus { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OwnerCompanyId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? TherapeuticAreaId { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public List<BrandProductExternalReference> ExternalReferences { get; set; } = [];
    public bool IsArchived { get; set; }

    public static BrandAuditSnapshot Of(Brand brand) => new()
    {
        BrandCode = brand.BrandCode,
        BrandName = brand.BrandName,
        BrandStatus = brand.BrandStatus,
        Description = brand.Description,
        OwnerCompanyId = brand.OwnerCompanyId,
        BusinessUnitId = brand.BusinessUnitId,
        TherapeuticAreaId = brand.TherapeuticAreaId,
        EffectiveFrom = brand.EffectiveFrom,
        EffectiveTo = brand.EffectiveTo,
        ExternalReferences = brand.ExternalReferences
            .Select(x => new BrandProductExternalReference
            {
                SourceSystem = x.SourceSystem,
                ExternalId = x.ExternalId,
                ExternalCode = x.ExternalCode,
                ExternalName = x.ExternalName,
                ImportedAt = x.ImportedAt,
                IsPrimary = x.IsPrimary
            })
            .ToList(),
        IsArchived = brand.IsArchived
    };
}
