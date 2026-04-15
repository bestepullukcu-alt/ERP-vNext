using Diten.MdmService.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// Composition header entity.
/// Inherits Id, TenantId, IsDeleted, DeletedAt, CreatedAt, UpdatedAt from EntityBase.
/// </summary>
public sealed class Composition : EntityBase
{
    public string FormulationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public Guid? CurrentVersionId { get; set; }

    public CompositionLifecycleState LifecycleState { get; set; } = CompositionLifecycleState.Draft;

    [BsonRepresentation(BsonType.String)]
    public Guid LifecycleStateId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? CreatedBy { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid? UpdatedBy { get; set; }
}
