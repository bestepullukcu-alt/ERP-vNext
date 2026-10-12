using Diten.Platform.Domain.Entities.Workflow;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.Platform.Infrastructure.Persistence;

/// <summary>
/// WP-MVP1-SYNC1 (CT decision 3) — <see cref="WorkflowInstance.StartedByUserId"/> is read TOLERANTLY.
///
/// <para>Two lines added the same field independently: MVP-1 (WP-0023-FX) wrote it as a STRING (the user id in "D"
/// form), main (WP-WORKFLOW-APPROVAL-STATUS-01 B2) as a <see cref="Guid"/> (Standard binary). The merged entity keeps
/// main's <c>Guid?</c>. A database an MVP-1 build ran against can therefore hold the string form, and the default
/// serializer would throw <c>FormatException</c> on the whole instance — every task read of that workflow would fail.
/// This serializer reads a BSON string, a Guid (binary) or null, and always WRITES the Standard Guid, so a document is
/// normalised the next time it is saved. No bulk migration (owner decision).</para>
///
/// <para>A string that is not a Guid reads as null: the instance then falls back to main's pre-field comparison
/// (<c>StartedBy</c>), the same as an instance from before the field existed.</para>
/// </summary>
public static class WorkflowInstanceBsonMap
{
    private static int _registered;

    /// <summary>Registers the class map once per process. Called from <see cref="PlatformBsonConventions.Register"/>, after
    /// the convention pack, because a class map binds the conventions registered before it is built.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1 || BsonClassMap.IsClassMapRegistered(typeof(WorkflowInstance)))
        {
            return;
        }

        BsonClassMap.RegisterClassMap<WorkflowInstance>(map =>
        {
            map.AutoMap();
            map.MapMember(instance => instance.StartedByUserId).SetSerializer(new LenientNullableGuidSerializer());
        });
    }
}

/// <summary>A <c>Guid?</c> that reads a BSON string, a Guid or null, and writes the Standard Guid representation.</summary>
public sealed class LenientNullableGuidSerializer : SerializerBase<Guid?>
{
    private static readonly GuidSerializer Standard = new(GuidRepresentation.Standard);

    public override Guid? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        switch (reader.GetCurrentBsonType())
        {
            case BsonType.Null:
                reader.ReadNull();
                return null;
            case BsonType.String:
                return Guid.TryParse(reader.ReadString(), out var parsed) ? parsed : null;
            default:
                return Standard.Deserialize(context, args);
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid? value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        Standard.Serialize(context, args, value.Value);
    }
}
