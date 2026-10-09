using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Bson;

namespace Diten.Platform.Infrastructure.Persistence.Migrations;

internal static class AuditOutboxTemporalStorageCompatibility
{
    internal const int CurrentVersion = 1;
    private const long TicksPerMinute = TimeSpan.TicksPerMinute;

    internal static long ToUtcTicks(DateTimeOffset value) => value.UtcTicks;

    internal static BsonArray ToLegacyBsonArray(DateTimeOffset value) =>
        [value.Ticks, checked((int)value.Offset.TotalMinutes)];

    internal static void ApplyCurrentVersion(AuditOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        message.NextAttemptAtUtcTicksV1 = ToUtcTicks(message.NextAttemptAtUtc);
        message.CreatedAtUtcTicksV1 = ToUtcTicks(message.CreatedAtUtc);
        message.TemporalStorageVersion = CurrentVersion;
    }

    internal static void ValidateForPersistence(AuditOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var hasNext = message.NextAttemptAtUtcTicksV1.HasValue;
        var hasCreated = message.CreatedAtUtcTicksV1.HasValue;
        var hasVersion = message.TemporalStorageVersion.HasValue;

        if (!hasNext && !hasCreated && !hasVersion)
        {
            return;
        }

        if (!hasNext || !hasCreated || !hasVersion)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SHADOW_INCOMPLETE");
        }

        if (message.TemporalStorageVersion != CurrentVersion)
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_VERSION_UNSUPPORTED");
        }

        if (message.NextAttemptAtUtcTicksV1 != ToUtcTicks(message.NextAttemptAtUtc)
            || message.CreatedAtUtcTicksV1 != ToUtcTicks(message.CreatedAtUtc))
        {
            throw new InvalidOperationException("AUDIT_OUTBOX_TEMPORAL_SHADOW_MISMATCH");
        }
    }

    internal static Inspection Inspect(BsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!TryReadLegacyUtcTicks(document, nameof(AuditOutboxMessage.NextAttemptAtUtc), out var nextTicks)
            || !TryReadLegacyUtcTicks(document, nameof(AuditOutboxMessage.CreatedAtUtc), out var createdTicks))
        {
            return Inspection.Malformed("AUDIT_OUTBOX_TEMPORAL_LEGACY_SHAPE_INVALID");
        }

        var hasNext = document.TryGetValue(nameof(AuditOutboxMessage.NextAttemptAtUtcTicksV1), out var nextShadow);
        var hasCreated = document.TryGetValue(nameof(AuditOutboxMessage.CreatedAtUtcTicksV1), out var createdShadow);
        var hasVersion = document.TryGetValue(nameof(AuditOutboxMessage.TemporalStorageVersion), out var version);

        if (!hasNext && !hasCreated && !hasVersion)
        {
            return Inspection.Legacy(nextTicks, createdTicks);
        }

        if (!hasNext || !hasCreated || !hasVersion
            || !nextShadow.IsInt64
            || !createdShadow.IsInt64
            || !version.IsInt32)
        {
            return Inspection.Malformed("AUDIT_OUTBOX_TEMPORAL_SHADOW_INCOMPLETE");
        }

        if (version.AsInt32 != CurrentVersion)
        {
            return Inspection.Malformed("AUDIT_OUTBOX_TEMPORAL_VERSION_UNSUPPORTED");
        }

        if (nextShadow.AsInt64 != nextTicks || createdShadow.AsInt64 != createdTicks)
        {
            return Inspection.Malformed("AUDIT_OUTBOX_TEMPORAL_SHADOW_MISMATCH");
        }

        return Inspection.Current(nextTicks, createdTicks);
    }

    private static bool TryReadLegacyUtcTicks(BsonDocument document, string fieldName, out long utcTicks)
    {
        utcTicks = default;
        if (!document.TryGetValue(fieldName, out var value)
            || !value.IsBsonArray
            || value.AsBsonArray.Count != 2
            || !value.AsBsonArray[0].IsInt64
            || !value.AsBsonArray[1].IsInt32)
        {
            return false;
        }

        var localTicks = value.AsBsonArray[0].AsInt64;
        var offsetMinutes = value.AsBsonArray[1].AsInt32;
        if (offsetMinutes is < -840 or > 840)
        {
            return false;
        }

        try
        {
            var offset = TimeSpan.FromMinutes(offsetMinutes);
            var legacyValue = new DateTimeOffset(localTicks, offset);
            utcTicks = checked(localTicks - (offsetMinutes * TicksPerMinute));
            return utcTicks == legacyValue.UtcTicks
                   && utcTicks >= DateTimeOffset.MinValue.UtcTicks
                   && utcTicks <= DateTimeOffset.MaxValue.UtcTicks;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    internal sealed record Inspection(
        InspectionKind Kind,
        long NextAttemptAtUtcTicks,
        long CreatedAtUtcTicks,
        string? FailureCode)
    {
        internal static Inspection Legacy(long nextTicks, long createdTicks) =>
            new(InspectionKind.Legacy, nextTicks, createdTicks, null);

        internal static Inspection Current(long nextTicks, long createdTicks) =>
            new(InspectionKind.Current, nextTicks, createdTicks, null);

        internal static Inspection Malformed(string failureCode) =>
            new(InspectionKind.Malformed, default, default, failureCode);
    }

    internal enum InspectionKind
    {
        Legacy = 0,
        Current = 1,
        Malformed = 2
    }
}
