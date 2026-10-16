using Diten.MdmService.Domain.Entities;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class AuditIntentTemporalStorageTests
{
    private static readonly string[] ProducerPaths =
    [
        "services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/CodeReservationRepository.cs",
        "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateGlobalProductDraftHandler.cs",
        "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFirstGskuDraftHandler.cs",
        "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/UpdateGskuDraftHandler.cs",
        "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateFinishedGoodDraftHandler.cs",
        "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductItemSkuMaster/Handlers/CommandHandlers/CreateLskuDraftHandler.cs",
        "services/Diten.MdmService/src/Diten.MdmService.Application/Features/ProductLegalEntityScopes/ProductLegalEntityScopeAuditIntentFactory.cs"
    ];

    [Fact]
    public void Legacy_intent_is_recognized_without_defaulting_missing_version_to_current()
    {
        var intent = Intent(new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(AuditIntentTemporalStorageKind.Legacy, AuditIntentTemporalStorage.Validate(intent));
        Assert.Null(intent.TemporalStorageVersion);
    }

    [Theory]
    [InlineData(-12)]
    [InlineData(0)]
    [InlineData(14)]
    public void Current_version_uses_exact_UTC_ticks_for_all_supported_offsets(int offsetHours)
    {
        var timestamp = new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.FromHours(offsetHours));
        var intent = Intent(timestamp);
        intent.NextRetryAt = timestamp.AddMinutes(5);
        intent.LeaseUntil = timestamp.AddMinutes(10);

        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);

        Assert.Equal(AuditIntentTemporalStorageKind.Current, AuditIntentTemporalStorage.Validate(intent));
        Assert.Equal(timestamp.UtcTicks, intent.TimestampUtcTicksV1);
        Assert.Equal(intent.NextRetryAt.Value.UtcTicks, intent.NextRetryAtUtcTicksV1);
        Assert.Equal(intent.LeaseUntil.Value.UtcTicks, intent.LeaseUntilUtcTicksV1);
    }

    [Fact]
    public void Current_null_retry_and_lease_are_exact_dual_written_state()
    {
        var intent = Intent(DateTimeOffset.UtcNow);
        AuditIntentTemporalStorage.ApplyCurrentVersion(intent);

        Assert.Equal(AuditIntentTemporalStorageKind.Current, AuditIntentTemporalStorage.Validate(intent));
        Assert.Null(intent.NextRetryAtUtcTicksV1);
        Assert.Null(intent.LeaseUntilUtcTicksV1);
    }

    [Fact]
    public void Half_shadow_unknown_version_and_mismatch_fail_closed()
    {
        var half = Intent(DateTimeOffset.UtcNow);
        half.TimestampUtcTicksV1 = half.TimestampUtc.UtcTicks;
        Assert.Equal(
            "AUDIT_INTENT_TEMPORAL_SHADOW_INCOMPLETE",
            Assert.Throws<InvalidOperationException>(() => AuditIntentTemporalStorage.Validate(half)).Message);

        var newer = Intent(DateTimeOffset.UtcNow);
        AuditIntentTemporalStorage.ApplyCurrentVersion(newer);
        newer.TemporalStorageVersion = 2;
        Assert.Equal(
            "AUDIT_INTENT_TEMPORAL_VERSION_UNSUPPORTED",
            Assert.Throws<InvalidOperationException>(() => AuditIntentTemporalStorage.Validate(newer)).Message);

        var mismatch = Intent(DateTimeOffset.UtcNow);
        AuditIntentTemporalStorage.ApplyCurrentVersion(mismatch);
        mismatch.TimestampUtcTicksV1++;
        Assert.Equal(
            "AUDIT_INTENT_TEMPORAL_SHADOW_MISMATCH",
            Assert.Throws<InvalidOperationException>(() => AuditIntentTemporalStorage.Validate(mismatch)).Message);
    }

    [Fact]
    public void Every_embedded_intent_producer_explicitly_stamps_current_timestamp_and_version()
    {
        var root = FindRepoRoot();
        foreach (var relativePath in ProducerPaths)
        {
            var source = File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains("TimestampUtcTicksV1", source, StringComparison.Ordinal);
            Assert.Contains("TemporalStorageVersion", source, StringComparison.Ordinal);
        }
    }

    private static LocalAuditIntent Intent(DateTimeOffset timestamp) => new()
    {
        TimestampUtc = timestamp
    };

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
