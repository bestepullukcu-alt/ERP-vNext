using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Diten.Platform.Domain.Common;

namespace Diten.Platform.Domain.Entities;

public sealed class SavedView : BaseEntity
{
    [BsonRepresentation(BsonType.String)]
    public Guid TenantId { get; set; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    public string ModuleKey { get; set; } = string.Empty;

    public string PageKey { get; set; } = string.Empty;

    public string ViewName { get; set; } = string.Empty;

    public string ViewDefinitionJson { get; set; } = "{}";

    public bool IsDefault { get; set; }

    public string Visibility { get; set; } = "private";
}
