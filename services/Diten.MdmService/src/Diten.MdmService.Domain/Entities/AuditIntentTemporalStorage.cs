namespace Diten.MdmService.Domain.Entities;

public static class AuditIntentTemporalStorage
{
    public const int CurrentVersion = 1;

    public static void ApplyCurrentVersion(LocalAuditIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.TimestampUtcTicksV1 = ToUtcTicks(intent.TimestampUtc);
        intent.NextRetryAtUtcTicksV1 = intent.NextRetryAt is null ? null : ToUtcTicks(intent.NextRetryAt.Value);
        intent.LeaseUntilUtcTicksV1 = intent.LeaseUntil is null ? null : ToUtcTicks(intent.LeaseUntil.Value);
        intent.TemporalStorageVersion = CurrentVersion;
    }

    public static AuditIntentTemporalStorageKind Validate(LocalAuditIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var hasTimestamp = intent.TimestampUtcTicksV1.HasValue;
        var hasVersion = intent.TemporalStorageVersion.HasValue;
        if (!hasTimestamp
            && !hasVersion
            && !intent.NextRetryAtUtcTicksV1.HasValue
            && !intent.LeaseUntilUtcTicksV1.HasValue)
        {
            return AuditIntentTemporalStorageKind.Legacy;
        }

        if (!hasTimestamp || !hasVersion)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_SHADOW_INCOMPLETE");
        }

        if (intent.TemporalStorageVersion != CurrentVersion)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_VERSION_UNSUPPORTED");
        }

        if (intent.TimestampUtcTicksV1 != ToUtcTicks(intent.TimestampUtc)
            || intent.NextRetryAtUtcTicksV1 != NullableUtcTicks(intent.NextRetryAt)
            || intent.LeaseUntilUtcTicksV1 != NullableUtcTicks(intent.LeaseUntil))
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_SHADOW_MISMATCH");
        }

        return AuditIntentTemporalStorageKind.Current;
    }

    public static long ToUtcTicks(DateTimeOffset value)
    {
        var ticks = value.UtcTicks;
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            throw new InvalidOperationException("AUDIT_INTENT_TEMPORAL_TICKS_INVALID");
        }

        return ticks;
    }

    private static long? NullableUtcTicks(DateTimeOffset? value)
        => value is null ? null : ToUtcTicks(value.Value);
}

public enum AuditIntentTemporalStorageKind
{
    Legacy = 0,
    Current = 1
}
