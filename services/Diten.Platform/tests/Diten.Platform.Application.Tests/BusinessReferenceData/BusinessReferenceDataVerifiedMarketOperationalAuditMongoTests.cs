using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Application.Tests.Persistence;
using System.Diagnostics;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

[CollectionDefinition(Name)]
public sealed class VerifiedMarketReplicaSetCollection : ICollectionFixture<VerifiedMarketReplicaSetFixture>
{
    public const string Name = "VerifiedMarketReplicaSet";
}

public sealed class VerifiedMarketReplicaSetFixture : IAsyncLifetime
{
    private DisposableMongoReplicaSet? _replica;

    public DisposableMongoReplicaSet Replica =>
        _replica ?? throw new InvalidOperationException("Disposable replica-set fixture was not initialized.");

    public async Task InitializeAsync()
    {
        _replica = await DisposableMongoReplicaSet.StartAsync(
            new DynamicReplicaSetPortAllocator(),
            new WindowsCompatibleMongodProcessStarter(),
            new TempReplicaSetWorkspaceFactory());
    }

    public async Task DisposeAsync()
    {
        if (_replica is not null)
        {
            await _replica.DisposeAsync();
        }
    }
}

internal sealed class WindowsCompatibleMongodProcessStarter : IMongodProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo)
    {
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Remove("--nounixsocket");
        }

        return Process.Start(startInfo);
    }
}

public sealed class BusinessReferenceDataVerifiedMarketOperationalAuditMongoTests
{
    [Fact]
    public async Task UnboundAdapter_RejectsOverrideBeforeAuditAppend()
    {
        var (adapter, audit, _) = Adapter();
        var version = Version();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitOverrideAsync(version, Facts().ActorId, version.LastCorrelationId!, version.OverrideReason!));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION", exception.Message);
        audit.Verify(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("foreign-tenant")]
    [InlineData("wrong-version")]
    [InlineData("wrong-fingerprint")]
    [InlineData("wrong-actor")]
    [InlineData("blank-actor")]
    public void Bind_RejectsFactsOutsideLockedOperationalScope(string mutation)
    {
        var (adapter, _, _) = Adapter();
        var facts = Facts();
        facts = mutation switch
        {
            "foreign-tenant" => facts with { ReferenceTenantId = Guid.NewGuid() },
            "wrong-version" => facts with { CatalogVersion = "wrong" },
            "wrong-fingerprint" => facts with { CatalogFingerprint = new string('0', 64) },
            "wrong-actor" => facts with { ActorId = "42b66b40-f47f-4d7b-9a90-15a654333d48" },
            "blank-actor" => facts with { ActorId = " " },
            _ => facts
        };

        var exception = Assert.Throws<InvalidOperationException>(() => adapter.Bind(facts));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION", exception.Message);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("actor")]
    [InlineData("correlation")]
    [InlineData("values")]
    public async Task OverrideTupleMismatch_IsRejectedWithoutAuditAppend(string mutation)
    {
        var facts = Facts();
        var (adapter, audit, _) = Adapter();
        adapter.Bind(facts);
        var version = Version();
        var actor = facts.ActorId;
        var correlation = version.LastCorrelationId!;
        var reason = version.OverrideReason!;
        if (mutation == "reason") reason = "different";
        if (mutation == "actor") actor = "different";
        if (mutation == "correlation") correlation = string.Empty;
        if (mutation == "values") version.Values.RemoveAt(0);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitOverrideAsync(version, actor, correlation, reason));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION", exception.Message);
        audit.Verify(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishWithoutPendingExactOverride_IsRejected()
    {
        var facts = Facts();
        var (adapter, audit, _) = Adapter();
        adapter.Bind(facts);
        var version = Version();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate"));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_SCOPE_VIOLATION", exception.Message);
        audit.Verify(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactOverrideAndPublish_AcceptsQueuedOrDuplicateOnlyAfterExactProof(bool duplicate)
    {
        var facts = Facts();
        var auditKey = "audit-key";
        var (adapter, audit, preflight) = Adapter();
        adapter.Bind(facts);
        var version = Version();
        audit.Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicate ? AuditAppendResult.Duplicate(auditKey) : AuditAppendResult.Queued(auditKey));
        preflight.Setup(x => x.VerifyAuditOutboxAsync(
                It.Is<VerifiedMarketOperationalAuditProofRequest>(request =>
                    request.TenantId == version.TenantId
                    && request.EntityId == version.BusinessReferenceDataVersionId
                    && request.PublicationCorrelationId == version.LastCorrelationId
                    && request.AuditIdempotencyKey == auditKey
                    && request.PublicationOperationKey == version.LastPublishIdempotencyKey
                    && request.CatalogVersion == VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion
                    && request.SetCode == "market"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalAuditProofResult(
                true,
                "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_EXACT",
                1));

        await adapter.EmitOverrideAsync(version, facts.ActorId, version.LastCorrelationId!, version.OverrideReason!);
        audit.Verify(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        await adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate");

        audit.Verify(x => x.AppendAsync(
            It.Is<AuditAppendRequest>(request =>
                request.TargetTenantId == version.TenantId
                && request.EntityId == version.BusinessReferenceDataVersionId
                && request.RequestType == "BusinessReferenceData.publish"
                && request.Operation == AuditOperation.Activate
                && request.ActorType == AuditActorType.PlatformAdministrator
                && request.ActorId == Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedActorId)
                && request.Metadata["actor"]!.ToString() == VerifiedMarketOperationalProvisioningOptions.LockedActorId
                && request.Metadata["actorType"]!.ToString() == AuditActorType.PlatformAdministrator.ToString()
                && request.Metadata["publicationOperationKey"]!.ToString() == version.LastPublishIdempotencyKey),
            It.IsAny<CancellationToken>()), Times.Once);
        preflight.VerifyAll();
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("enqueue-failed")]
    [InlineData("skipped")]
    [InlineData("empty-key")]
    public async Task NonDurableAppendResult_FailsClosed(string result)
    {
        var facts = Facts();
        var (adapter, audit, preflight) = Adapter();
        adapter.Bind(facts);
        var version = Version();
        audit.Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result switch
            {
                "rejected" => AuditAppendResult.Rejected("rejected"),
                "enqueue-failed" => AuditAppendResult.EnqueueFailed("audit-key", "failed"),
                "skipped" => AuditAppendResult.SkippedRecursion("recursion"),
                _ => AuditAppendResult.Queued(string.Empty)
            });
        await adapter.EmitOverrideAsync(version, facts.ActorId, version.LastCorrelationId!, version.OverrideReason!);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate"));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_AUDIT_APPEND_FAILED", exception.Message);
        preflight.Verify(x => x.VerifyAuditOutboxAsync(
            It.IsAny<VerifiedMarketOperationalAuditProofRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, 0, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_AMBIGUOUS")]
    [InlineData(false, 1, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_MISMATCH")]
    [InlineData(true, 2, "VERIFIED_MARKET_OPERATIONAL_AUDIT_PROOF_AMBIGUOUS")]
    public async Task MissingMismatchedOrAmbiguousProof_FailsClosed(
        bool isExact,
        int matchCount,
        string reason)
    {
        var facts = Facts();
        var (adapter, audit, preflight) = Adapter();
        adapter.Bind(facts);
        var version = Version();
        audit.Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuditAppendResult.Queued("audit-key"));
        preflight.Setup(x => x.VerifyAuditOutboxAsync(
                It.IsAny<VerifiedMarketOperationalAuditProofRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalAuditProofResult(isExact, reason, matchCount));
        await adapter.EmitOverrideAsync(version, facts.ActorId, version.LastCorrelationId!, version.OverrideReason!);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate"));

        Assert.Equal(reason, exception.Message);
    }

    [Fact]
    public async Task AuditAppendException_PropagatesAndDoesNotClaimSuccess()
    {
        var facts = Facts();
        var (adapter, audit, _) = Adapter();
        adapter.Bind(facts);
        var version = Version();
        audit.Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("outbox unavailable"));
        await adapter.EmitOverrideAsync(version, facts.ActorId, version.LastCorrelationId!, version.OverrideReason!);

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate"));

        Assert.Equal("outbox unavailable", exception.Message);
    }

    [Fact]
    public async Task PendingOverrideTuple_CannotBeReusedOrPublishedTwice()
    {
        var facts = Facts();
        var (adapter, audit, preflight) = Adapter();
        adapter.Bind(facts);
        var version = Version();
        audit.Setup(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuditAppendResult.Queued("audit-key"));
        preflight.Setup(x => x.VerifyAuditOutboxAsync(
                It.IsAny<VerifiedMarketOperationalAuditProofRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedMarketOperationalAuditProofResult(true, "exact", 1));

        await adapter.EmitOverrideAsync(version, facts.ActorId, version.LastCorrelationId!, version.OverrideReason!);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitOverrideAsync(version, facts.ActorId, version.LastCorrelationId!, version.OverrideReason!));
        await adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.EmitPublishAsync(version, facts.ActorId, version.LastCorrelationId!, true, null, "Immediate"));

        audit.Verify(x => x.AppendAsync(It.IsAny<AuditAppendRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (VerifiedMarketOperationalGovernanceAuditAdapter Adapter, Mock<IAuditService> Audit, Mock<IVerifiedMarketOperationalPreflight> Preflight) Adapter()
    {
        var audit = new Mock<IAuditService>(MockBehavior.Strict);
        var preflight = new Mock<IVerifiedMarketOperationalPreflight>(MockBehavior.Strict);
        return (new VerifiedMarketOperationalGovernanceAuditAdapter(audit.Object, preflight.Object), audit, preflight);
    }

    private static VerifiedMarketOperationalFacts Facts() => new(
        BusinessReferenceDataTestHarness.GetSeedPath(VerifiedMarketOperationalProvisioningOptions.LockedCatalogFileName),
        VerifiedMarketOperationalProvisioningOptions.LockedCatalogVersion,
        VerifiedMarketOperationalProvisioningOptions.LockedCatalogFingerprint,
        Guid.Parse(VerifiedMarketOperationalProvisioningOptions.LockedReferenceTenantId),
        VerifiedMarketOperationalProvisioningOptions.LockedActorId,
        "market-operational-test");

    private static BusinessReferenceDataVersion Version()
    {
        var facts = Facts();
        return new BusinessReferenceDataVersion
        {
            TenantId = facts.ReferenceTenantId,
            BusinessReferenceDataSetId = Guid.NewGuid(),
            BusinessReferenceDataVersionId = Guid.NewGuid(),
            LastCorrelationId = "publication-correlation",
            LastPublishIdempotencyKey = $"{facts.IdempotencyNamespace}:businessreferencedata-catalog-v{facts.CatalogVersion}:market".ToLowerInvariant(),
            OverrideReason = "BusinessReferenceData catalog load override",
            Values = Enumerable.Range(0, 249)
                .Select(index => new BusinessReferenceDataValue
                {
                    ValueCode = index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                    DisplayName = $"Market {index}"
                })
                .ToList()
        };
    }
}
