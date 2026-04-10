using Diten.MdmService.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// CompositionVersion entity for versioned formulation data.
/// Inherits Id, TenantId, IsDeleted, DeletedAt, CreatedAt, UpdatedAt from EntityBase.
/// </summary>
public sealed class CompositionVersion : EntityBase
{
    [BsonRepresentation(BsonType.String)]
    public Guid CompositionId { get; set; }

    public int VersionNo { get; set; } = 1;

    public CompositionVersionStatus Status { get; set; } = CompositionVersionStatus.Draft;

    public bool IsCurrent { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid DosageFormId { get; set; }

    public decimal StrengthValue { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid StrengthUnitId { get; set; }

    public decimal TechnicalFillAmount { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? TechnicalFillUnitId { get; set; }

    public List<CompositionComponent> Components { get; set; } = [];

    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? CreatedBy { get; set; }
    
    public string DisplayName => $"v{VersionNo}";
}

public sealed class CompositionComponent
{
    public int Sequence { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid ComponentId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public CompositionComponentType ComponentType { get; set; } = CompositionComponentType.Api;

    public decimal Quantity { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UnitId { get; set; }
}
