using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// WP-LANE-MAIN-SYNC-02 P1 (owner decision 2026-10-14, BL-634) — the in-transaction audit door's named exemption. Only
/// an EXACT event name on <see cref="TransactionOwnedAuditPayloadExemptions"/>, with no intent and a payload naming the
/// same event, is stored as written; everything else without an intent is still refused before the store is touched.
/// </summary>
public sealed class TransactionOwnedAuditPayloadExemptionTests
{
    private static readonly string[] TheEighteenEvents =
    [
        "esign.export.created",
        "esign.meaning.activated",
        "esign.meaning.created",
        "esign.meaning.deactivated",
        "esign.meaning.updated",
        "esign.policy.changed",
        "esign.policy.override-reverted",
        "esign.request.cancelled",
        "esign.request.created",
        "esign.request.expired",
        "esign.request.rejected",
        "esign.sample.approved",
        "esign.sample.deactivated",
        "esign.sample.rejected",
        "esign.sample.submitted",
        "esign.sample.withdrawn",
        "esign.signature.invalidated",
        "esign.signature.signed"
    ];

    [Fact]
    public void The_exemption_is_exactly_the_eighteen_named_events_with_owner_reason_and_expiry()
    {
        Assert.Equal(TheEighteenEvents, TransactionOwnedAuditPayloadExemptions.All.Order(StringComparer.Ordinal));
        Assert.Equal("MOD-0022 e-imza geliştiricisi", TransactionOwnedAuditPayloadExemptions.Owner);
        Assert.Equal("main-sync 2026-10-14", TransactionOwnedAuditPayloadExemptions.Reason);
        Assert.Equal("BL-634", TransactionOwnedAuditPayloadExemptions.Backlog);
        Assert.Equal("2026-11-13", TransactionOwnedAuditPayloadExemptions.ExpiresOn);
    }

    [Theory]
    [InlineData("esign.signature.verified")]     // an e-signature event, but not on the list
    [InlineData("esign.signature")]              // a prefix of a listed name
    [InlineData("esign.signature.signed.extra")] // a listed name as a prefix
    [InlineData("ESIGN.SIGNATURE.SIGNED")]       // another spelling
    [InlineData("tenant.subscription.created")]  // anything else
    public async Task An_event_that_is_not_named_and_carries_no_intent_is_still_refused(string eventName)
    {
        var (writer, store) = Door();

        await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(
            () => writer.TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(), HandWritten(eventName, eventName)));

        store.Verify(s => s.TryInsertAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<AuditOutboxWriteRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_named_event_whose_payload_names_another_event_is_refused()
    {
        var (writer, store) = Door();

        await Assert.ThrowsAsync<TransactionOwnedAuditIntentInvalidException>(
            () => writer.TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(),
                HandWritten("esign.signature.signed", payloadEvent: "esign.signature.verified")));

        store.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(NamedEvents))]
    public async Task A_named_event_with_its_own_payload_is_stored_as_written(string eventName)
    {
        var (writer, store) = Door();
        var request = HandWritten(eventName, eventName);

        Assert.True(await writer.TryEnqueueAsync(Mock.Of<IPlatformTransactionSession>(), request));

        store.Verify(s => s.TryInsertAsync(It.IsAny<IPlatformTransactionSession>(), request, It.IsAny<CancellationToken>()), Times.Once);
    }

    public static TheoryData<string> NamedEvents()
    {
        var data = new TheoryData<string>();
        foreach (var name in TheEighteenEvents)
        {
            data.Add(name);
        }

        return data;
    }

    private static (CanonicalTransactionalAuditOutboxWriter Writer, Mock<ITransactionalAuditOutboxStore> Store) Door()
    {
        var store = new Mock<ITransactionalAuditOutboxStore>(MockBehavior.Strict);
        store.Setup(s => s.TryInsertAsync(It.IsAny<IPlatformTransactionSession>(), It.IsAny<AuditOutboxWriteRequest>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var writer = new CanonicalTransactionalAuditOutboxWriter(
            store.Object, Mock.Of<ITenantAuthorizationContext>(), Mock.Of<ICurrentUserContext>(), Mock.Of<ISensitiveFieldRedactor>());
        return (writer, store);
    }

    private static AuditOutboxWriteRequest HandWritten(string eventName, string payloadEvent) => new()
    {
        TenantId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        IdempotencyKey = "esign:" + eventName,
        RequestType = eventName,
        Operation = AuditOperation.Create,
        EntityType = "ESignatureRecord",
        EntityId = Guid.NewGuid(),
        Payload = new Dictionary<string, object?> { ["RequestType"] = payloadEvent, ["Category"] = "ESignature" }
    };
}
