using System.Net.Http.Headers;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Infrastructure.ESign;
using Diten.InventoryService.Tests.CentralAudit;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-MVP1-FIX-LIVE2 (owner step MOD-0176, kişi adları) — the two READ paths of a person's name in the FEFO evidence pack: the signer's
/// name MOD-0022 recorded at signing (<c>signer.name</c> = <c>signerNameAtSign</c>), and the name the central audit queue recorded for the user
/// of a command (correlation id AND actor id). No Expiry entity gains a field.
/// </summary>
public sealed class ExpiryPersonNameReadTests : IClassFixture<ExpiryApiFactory>
{
    private readonly ExpiryApiFactory _factory;

    public ExpiryPersonNameReadTests(ExpiryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task The_signature_read_carries_the_name_and_meaning_text_recorded_at_signing()
    {
        var gateway = new FakeESignGateway();
        var tenant = Guid.NewGuid();
        var signer = Guid.NewGuid();
        var id = gateway.Sign(tenant, signer, "FX-2026-0001", "APPROVE", new string('a', 64), "expiry", "expiry.exception", "expiry.exception.resolve",
            signerName: "Ayşe Demir", meaningText: "Onaylıyorum");
        var unnamed = gateway.Sign(tenant, signer, "FX-2026-0002", "APPROVE", new string('b', 64), "expiry", "expiry.exception");

        var client = Client(gateway, tenant);
        var read = await client.SignatureAsync(id);
        Assert.True(read.IsOk);
        Assert.Equal(("Ayşe Demir", "Onaylıyorum", signer.ToString("D")), (read.Value!.SignerNameAtSign, read.Value.MeaningText, read.Value.SignerUserId));

        // every field read before is read as before (the record grew at its end only)
        Assert.Equal(("expiry", "expiry.exception", "FX-2026-0001", "APPROVE", new string('a', 64), false),
            (read.Value.Module, read.Value.SubjectType, read.Value.SubjectId, read.Value.MeaningCode, read.Value.SubjectHash, read.Value.Invalidated));
        Assert.Null((await client.SignatureAsync(unnamed)).Value!.MeaningText);
    }

    [Fact]
    public async Task An_audit_name_is_found_only_for_its_own_correlation_and_its_own_user()
    {
        var tenant = Guid.NewGuid();
        var (ayse, mehmet) = (Guid.NewGuid(), Guid.NewGuid());
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var outbox = CentralAuditInventoryTests.Outbox(_factory);
        await outbox.InsertManyAsync(
        [
            Row(tenant, first, ayse, "Ayşe Demir"),
            Row(tenant, second, mehmet, "Mehmet Kaya"),
            Row(tenant, third, mehmet, null),                     // a command whose name was not recorded
            Row(Guid.NewGuid(), first, ayse, "Another tenant")      // the same ids in another tenant never answer
        ]);

        var names = await NamesAsync(tenant, [(first, ayse), (second, mehmet), (first, mehmet), (second, ayse), (third, mehmet), (Guid.NewGuid(), ayse)]);

        Assert.Equal("Ayşe Demir", names[(first, ayse)]);
        Assert.Equal("Mehmet Kaya", names[(second, mehmet)]);
        Assert.False(names.ContainsKey((first, mehmet)));          // two people, two events: the other event's name is never borrowed
        Assert.False(names.ContainsKey((second, ayse)));
        Assert.False(names.ContainsKey((third, mehmet)));          // no recorded name → absent ("ad okunamadı"), nothing invented
        Assert.Equal(2, names.Count);
    }

    private async Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(Guid tenant, IReadOnlyCollection<(Guid, Guid)> keys)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(tenant, Guid.NewGuid(), Guid.NewGuid());
        return await scope.ServiceProvider.GetRequiredService<IExpiryAuditNames>().NamesAsync(keys);
    }

    private static CentralAuditOutboxRecord Row(Guid tenant, Guid correlation, Guid actor, string? name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenant,
        ActorId = actor,
        ActorDisplayName = name,
        SourceService = "inventory",
        SourceModule = "expiry",
        Category = "Inventory",
        Operation = "UpdateExpiryPolicyCommand",
        EntityType = "ExpiryPolicy",
        RequestType = "UpdateExpiryPolicyCommand",
        CorrelationId = correlation,
        OccurredAtUtc = DateTime.UtcNow,
        NextAttemptAtUtc = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static GatewayExpiryESignClient Client(FakeESignGateway gateway, Guid tenant)
    {
        var http = new HttpClient(new FakeESignHandler(gateway)) { BaseAddress = new Uri("http://gateway.test/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Mint(tenant, null, Guid.NewGuid(), ["esign.signatures.read"]));
        var context = new TenantContext();
        context.SetScope(tenant, Guid.NewGuid(), Guid.NewGuid());
        return new GatewayExpiryESignClient(http, context, NullLogger<GatewayExpiryESignClient>.Instance);
    }
}
