using Diten.ManufacturingService.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManufacturingService.Tests.Api;

/// <summary>
/// M-3 (MVP-1 legal-entity pattern) over HTTP against the real Program: the caller names its legal entity, MDM proves it,
/// nothing is read or written otherwise. The token carries NO legal-entity claim and no dev bypass is configured.
/// </summary>
public sealed class LegalEntityScopeTests : IClassFixture<BomApiFixture>
{
    private readonly BomApiFactory _factory;

    public LegalEntityScopeTests(BomApiFixture fixture)
    {
        Skip.If(BomApiFactory.Connection is null, $"{BomApiFactory.EnvironmentVariable} is not set — no isolated Mongo replica set.");
        _factory = fixture.Factory!;
    }

    [SkippableFact]
    public async Task The_proven_legal_entity_creates_and_lists_and_its_id_is_on_the_record()
    {
        var caller = new BomCaller(_factory);
        var draft = await caller.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10));

        var list = await caller.Send(HttpMethod.Get, "/api/bom/versions");
        var byQuery = await caller.Send(HttpMethod.Get, $"/api/bom/versions?legalEntityId={caller.LegalEntity}", withLegalEntity: false);

        Assert.Equal(caller.LegalEntity, (Guid)draft["legalEntityId"]!);
        Assert.Equal(200, list.Status);
        Assert.Equal(1, (long)list.Body!["data"]!["total"]!);
        Assert.Equal(caller.LegalEntity, (Guid)list.Body["data"]!["items"]![0]!["legalEntityId"]!);
        Assert.Equal(200, byQuery.Status);
        Assert.Equal(1, (long)byQuery.Body!["data"]!["total"]!);
    }

    [SkippableFact]
    public async Task Another_tenants_legal_entity_is_refused_and_nothing_is_written()
    {
        var caller = new BomCaller(_factory);
        _factory.LegalEntities.Foreign.Add(caller.LegalEntity);

        var create = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10)));
        var list = await caller.Send(HttpMethod.Get, "/api/bom/versions");

        foreach (var r in new[] { create, list })
        {
            Assert.Equal(422, r.Status);
            Assert.Equal("LEGAL_ENTITY_NOT_REFERENCEABLE", (string)r.Body!["error"]!["code"]!);
        }

        Assert.Equal(0, await _factory.Db().GetCollection<BomVersion>("mfg_boms").CountDocumentsAsync(b => b.TenantId == caller.Tenant));
        Assert.Equal(0, await _factory.Db().GetCollection<BomHistoryEntry>("mfg_bom_history").CountDocumentsAsync(h => h.TenantId == caller.Tenant));
    }

    [SkippableFact]
    public async Task A_missing_malformed_or_contradicting_legal_entity_is_a_validation_error()
    {
        var caller = new BomCaller(_factory);
        var before = _factory.LegalEntities.Calls;

        var missing = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10)), withLegalEntity: false);
        var missingRead = await caller.Send(HttpMethod.Get, "/api/bom/versions", withLegalEntity: false);
        var malformed = await caller.Send(HttpMethod.Get, "/api/bom/versions?legalEntityId=not-a-uuid", withLegalEntity: false);
        var contradicting = await caller.Send(HttpMethod.Get, $"/api/bom/versions?legalEntityId={Guid.NewGuid()}");

        Assert.Equal((400, "LEGAL_ENTITY_REQUIRED"), (missing.Status, (string)missing.Body!["error"]!["code"]!));
        Assert.Equal((400, "LEGAL_ENTITY_REQUIRED"), (missingRead.Status, (string)missingRead.Body!["error"]!["code"]!));
        Assert.Equal((400, "LEGAL_ENTITY_REQUIRED"), (malformed.Status, (string)malformed.Body!["error"]!["code"]!));
        Assert.Equal(400, contradicting.Status);
        Assert.Equal(before, _factory.LegalEntities.Calls);
    }

    [SkippableFact]
    public async Task A_legal_entity_in_the_query_is_accepted_on_GET_only()
    {
        var caller = new BomCaller(_factory);
        var post = await caller.Send(HttpMethod.Post, $"/api/bom/versions?legalEntityId={caller.LegalEntity}",
            BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10)), withLegalEntity: false);

        Assert.Equal((400, "LEGAL_ENTITY_REQUIRED"), (post.Status, (string)post.Body!["error"]!["code"]!));
    }
}

/// <summary>The REAL MDM validator with MDM unreachable: every scoped request is 503 and writes nothing.</summary>
public sealed class UnreachableMdmTests : IClassFixture<UnreachableMdmFixture>
{
    private readonly BomApiFactory _factory;

    public UnreachableMdmTests(UnreachableMdmFixture fixture)
    {
        Skip.If(BomApiFactory.Connection is null, $"{BomApiFactory.EnvironmentVariable} is not set — no isolated Mongo replica set.");
        _factory = fixture.Factory!;
    }

    [SkippableFact]
    public async Task Mdm_down_is_503_dependency_unavailable_for_reads_and_writes()
    {
        var caller = new BomCaller(_factory);
        var create = await caller.Send(HttpMethod.Post, "/api/bom/versions", BomCaller.Draft(Guid.NewGuid(), (Guid.NewGuid(), "1", "EA", 10)));
        var list = await caller.Send(HttpMethod.Get, "/api/bom/versions");

        foreach (var r in new[] { create, list })
        {
            Assert.Equal(503, r.Status);
            Assert.Equal("DEPENDENCY_UNAVAILABLE", (string)r.Body!["error"]!["code"]!);
        }

        Assert.Equal(0, await _factory.Db().GetCollection<BomVersion>("mfg_boms").CountDocumentsAsync(b => b.TenantId == caller.Tenant));
    }

    [SkippableFact]
    public async Task Control_health_does_not_ask_mdm()
    {
        using var client = _factory.CreateClient();
        Assert.Equal(200, (int)(await client.GetAsync("/health")).StatusCode);
    }
}

public sealed class UnreachableMdmFixture : IDisposable
{
    public BomApiFactory? Factory { get; } = BomApiFactory.Connection is null ? null : new UnreachableMdmFactory();

    public void Dispose() => Factory?.Dispose();
}
