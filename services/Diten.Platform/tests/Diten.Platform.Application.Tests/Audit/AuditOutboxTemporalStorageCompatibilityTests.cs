using System.Text.RegularExpressions;
using Diten.Platform.API.Configuration;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Models;
using MongoDB.Bson;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditOutboxTemporalStorageCompatibilityTests
{
    [Theory]
    [InlineData(14)]
    [InlineData(0)]
    [InlineData(-12)]
    public void ToUtcTicks_SameInstantWithDifferentOffsets_ReturnsSameScalar(int offsetHours)
    {
        var instant = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

        var ticks = AuditOutboxTemporalStorageCompatibility.ToUtcTicks(
            instant.ToOffset(TimeSpan.FromHours(offsetHours)));

        Assert.Equal(instant.UtcTicks, ticks);
    }

    [Fact]
    public void ApplyCurrentVersion_ValidMessage_DualWritesCompletePair()
    {
        var message = Message(
            nextAttempt: new DateTimeOffset(2026, 8, 28, 23, 0, 0, TimeSpan.FromHours(11)),
            createdAt: new DateTimeOffset(2026, 8, 28, 1, 0, 0, TimeSpan.FromHours(-11)));

        AuditOutboxTemporalStorageCompatibility.ApplyCurrentVersion(message);
        AuditOutboxTemporalStorageCompatibility.ValidateForPersistence(message);

        Assert.Equal(message.NextAttemptAtUtc.UtcTicks, message.NextAttemptAtUtcTicksV1);
        Assert.Equal(message.CreatedAtUtc.UtcTicks, message.CreatedAtUtcTicksV1);
        Assert.Equal(AuditOutboxTemporalStorageCompatibility.CurrentVersion, message.TemporalStorageVersion);
    }

    [Fact]
    public void ValidateForPersistence_LegacyOnly_RemainsRollbackCompatible()
    {
        var message = Message();

        AuditOutboxTemporalStorageCompatibility.ValidateForPersistence(message);

        Assert.Null(message.NextAttemptAtUtcTicksV1);
        Assert.Null(message.CreatedAtUtcTicksV1);
        Assert.Null(message.TemporalStorageVersion);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    public void ValidateForPersistence_HalfShadow_FailsClosed(
        bool hasNext,
        bool hasCreated,
        bool hasVersion)
    {
        var message = Message();
        message.NextAttemptAtUtcTicksV1 = hasNext ? message.NextAttemptAtUtc.UtcTicks : null;
        message.CreatedAtUtcTicksV1 = hasCreated ? message.CreatedAtUtc.UtcTicks : null;
        message.TemporalStorageVersion = hasVersion ? 1 : null;

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuditOutboxTemporalStorageCompatibility.ValidateForPersistence(message));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_SHADOW_INCOMPLETE", exception.Message);
    }

    [Fact]
    public void ValidateForPersistence_UnknownVersion_FailsClosed()
    {
        var message = CurrentMessage();
        message.TemporalStorageVersion = 2;

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuditOutboxTemporalStorageCompatibility.ValidateForPersistence(message));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_VERSION_UNSUPPORTED", exception.Message);
    }

    [Fact]
    public void ValidateForPersistence_ShadowMismatch_FailsClosed()
    {
        var message = CurrentMessage();
        message.NextAttemptAtUtcTicksV1++;

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuditOutboxTemporalStorageCompatibility.ValidateForPersistence(message));

        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_SHADOW_MISMATCH", exception.Message);
    }

    [Fact]
    public void Inspect_LegacyArray_ReturnsExactUtcInstantsWithoutMutation()
    {
        var next = new DateTimeOffset(2026, 8, 28, 23, 0, 0, TimeSpan.FromHours(11));
        var created = new DateTimeOffset(2026, 8, 28, 1, 0, 0, TimeSpan.FromHours(-11));
        var document = LegacyDocument(next, created);
        var before = document.DeepClone().AsBsonDocument;

        var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(document);

        Assert.Equal(AuditOutboxTemporalStorageCompatibility.InspectionKind.Legacy, inspection.Kind);
        Assert.Equal(next.UtcTicks, inspection.NextAttemptAtUtcTicks);
        Assert.Equal(created.UtcTicks, inspection.CreatedAtUtcTicks);
        Assert.Equal(before, document);
    }

    [Fact]
    public void Inspect_ExactV1_ReturnsCurrent()
    {
        var next = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(14));
        var created = DateTimeOffset.UtcNow.AddHours(-1).ToOffset(TimeSpan.FromHours(-12));
        var document = LegacyDocument(next, created);
        document["NextAttemptAtUtcTicksV1"] = next.UtcTicks;
        document["CreatedAtUtcTicksV1"] = created.UtcTicks;
        document["TemporalStorageVersion"] = 1;

        var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(document);

        Assert.Equal(AuditOutboxTemporalStorageCompatibility.InspectionKind.Current, inspection.Kind);
        Assert.Null(inspection.FailureCode);
    }

    [Theory]
    [MemberData(nameof(MalformedDocuments))]
    public void Inspect_MalformedOrConflictingDocument_FailsClosed(BsonDocument document, string expectedCode)
    {
        var inspection = AuditOutboxTemporalStorageCompatibility.Inspect(document);

        Assert.Equal(AuditOutboxTemporalStorageCompatibility.InspectionKind.Malformed, inspection.Kind);
        Assert.Equal(expectedCode, inspection.FailureCode);
    }

    [Fact]
    public void BsonSerialization_CurrentMessage_RetainsLegacyArraysAndAddsScalarInt64Shadows()
    {
        var message = CurrentMessage();

        var document = message.ToBsonDocument();

        Assert.Equal(BsonType.Array, document["NextAttemptAtUtc"].BsonType);
        Assert.Equal(BsonType.Array, document["CreatedAtUtc"].BsonType);
        Assert.Equal(BsonType.Int64, document["NextAttemptAtUtcTicksV1"].BsonType);
        Assert.Equal(BsonType.Int64, document["CreatedAtUtcTicksV1"].BsonType);
        Assert.Equal(BsonType.Int32, document["TemporalStorageVersion"].BsonType);
    }

    [Fact]
    public void MigrationSources_DoNotUseWholeDocumentReplaceOrRegisterDateTimeOffsetSerializer()
    {
        var root = RepoRoot();
        var migrations = Path.Combine(
            root,
            "services",
            "Diten.Platform",
            "src",
            "Diten.Platform.Infrastructure",
            "Persistence",
            "Migrations");
        var platform = Path.Combine(root, "services", "Diten.Platform", "src");

        var migrationSource = string.Join(
            "\n",
            Directory.EnumerateFiles(migrations, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var platformSource = string.Join(
            "\n",
            Directory.EnumerateFiles(platform, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("ReplaceOne", migrationSource, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new Regex(@"RegisterSerializer\s*<\s*DateTimeOffset\s*>", RegexOptions.CultureInvariant),
            platformSource);
        Assert.DoesNotContain("new DateTimeOffsetSerializer", platformSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationOptions_DefaultState_IsDisabledAndFailsClosed()
    {
        var options = new AuditOutboxTemporalStorageMigrationOptions();

        Assert.False(options.Enabled);
        var error = Assert.Throws<InvalidOperationException>(() => options.Validate("Development"));
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_MIGRATION_DISABLED", error.Message);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("development")]
    public void MigrationOptions_EnabledOutsideExactDevelopment_FailsClosed(string environment)
    {
        var options = new AuditOutboxTemporalStorageMigrationOptions
        {
            Enabled = true,
            LeaseOwner = "fu02-test"
        };

        var error = Assert.Throws<InvalidOperationException>(() => options.Validate(environment));
        Assert.Equal("AUDIT_OUTBOX_TEMPORAL_MIGRATION_ENVIRONMENT_NOT_ALLOWED", error.Message);
    }

    public static IEnumerable<object[]> MalformedDocuments()
    {
        var valid = LegacyDocument(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-1));

        var missingLegacy = valid.DeepClone().AsBsonDocument;
        missingLegacy.Remove("CreatedAtUtc");
        yield return new object[] { missingLegacy, "AUDIT_OUTBOX_TEMPORAL_LEGACY_SHAPE_INVALID" };

        var invalidOffset = valid.DeepClone().AsBsonDocument;
        invalidOffset["NextAttemptAtUtc"] = new BsonArray { DateTimeOffset.UtcNow.Ticks, 841 };
        yield return new object[] { invalidOffset, "AUDIT_OUTBOX_TEMPORAL_LEGACY_SHAPE_INVALID" };

        var halfShadow = valid.DeepClone().AsBsonDocument;
        halfShadow["NextAttemptAtUtcTicksV1"] = DateTimeOffset.UtcNow.UtcTicks;
        yield return new object[] { halfShadow, "AUDIT_OUTBOX_TEMPORAL_SHADOW_INCOMPLETE" };

        var wrongType = valid.DeepClone().AsBsonDocument;
        wrongType["NextAttemptAtUtcTicksV1"] = "ticks";
        wrongType["CreatedAtUtcTicksV1"] = DateTimeOffset.UtcNow.UtcTicks;
        wrongType["TemporalStorageVersion"] = 1;
        yield return new object[] { wrongType, "AUDIT_OUTBOX_TEMPORAL_SHADOW_INCOMPLETE" };

        var unknownVersion = valid.DeepClone().AsBsonDocument;
        unknownVersion["NextAttemptAtUtcTicksV1"] = valid["NextAttemptAtUtc"].AsBsonArray[0].AsInt64;
        unknownVersion["CreatedAtUtcTicksV1"] = valid["CreatedAtUtc"].AsBsonArray[0].AsInt64;
        unknownVersion["TemporalStorageVersion"] = 2;
        yield return new object[] { unknownVersion, "AUDIT_OUTBOX_TEMPORAL_VERSION_UNSUPPORTED" };

        var mismatch = valid.DeepClone().AsBsonDocument;
        var next = DateTimeOffset.UtcNow;
        var created = next.AddMinutes(-1);
        mismatch = LegacyDocument(next, created);
        mismatch["NextAttemptAtUtcTicksV1"] = next.UtcTicks + 1;
        mismatch["CreatedAtUtcTicksV1"] = created.UtcTicks;
        mismatch["TemporalStorageVersion"] = 1;
        yield return new object[] { mismatch, "AUDIT_OUTBOX_TEMPORAL_SHADOW_MISMATCH" };
    }

    private static AuditOutboxMessage Message(
        DateTimeOffset? nextAttempt = null,
        DateTimeOffset? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        RequestType = "FU02.Test",
        Operation = Diten.Platform.Domain.Enums.AuditOperation.Create,
        EntityType = "AuditOutboxTemporalTest",
        NextAttemptAtUtc = nextAttempt ?? DateTimeOffset.UtcNow,
        CreatedAtUtc = createdAt ?? DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    private static AuditOutboxMessage CurrentMessage()
    {
        var message = Message();
        AuditOutboxTemporalStorageCompatibility.ApplyCurrentVersion(message);
        return message;
    }

    private static BsonDocument LegacyDocument(DateTimeOffset next, DateTimeOffset created) => new()
    {
        ["NextAttemptAtUtc"] = new BsonArray { next.Ticks, (int)next.Offset.TotalMinutes },
        ["CreatedAtUtc"] = new BsonArray { created.Ticks, (int)created.Offset.TotalMinutes }
    };

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
