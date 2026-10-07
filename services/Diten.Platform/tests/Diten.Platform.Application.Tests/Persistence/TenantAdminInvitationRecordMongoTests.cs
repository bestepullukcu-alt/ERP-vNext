using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-454 stage D FIX2 K7 — the outcome of a tenant administrator's invitation is a TARGETED write on a real MongoDB: the
/// step, the administrator's times and one activity line, nothing else — so a change made between the writer's read and its
/// write (here: a suspension) is never put back.
/// </summary>
public sealed class TenantAdminInvitationRecordMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private TenantRegistryRepository _tenants = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Core);
        var context = new TenantContext();
        context.SetPlatformContext(_harness.TenantId);
        _tenants = new TenantRegistryRepository(_harness.DbContext, context);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Theory]
    [InlineData(true)]  // the step already exists (Pending, as registration writes it)
    [InlineData(false)] // a tenant made before the step existed
    public async Task The_invitation_outcome_is_written_without_undoing_a_suspension_made_meanwhile(bool stepExists)
    {
        var tenant = NewTenant();
        var admin = new TenantAdminUser { Name = "First Admin", Email = "first@record.test", Status = TenantAdminUserStatus.Invited };
        tenant.AdminUsers.Add(admin);
        if (stepExists)
        {
            tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = TenantProvisioningStep.AdminInvitationKey, Label = "Initial Admin Invitation" });
        }

        await _tenants.CreateAsync(tenant);
        _ = await _tenants.GetByIdAsync(tenant.Id);              // the writer's read …
        await _tenants.UpdateStatusAsync(tenant.Id, TenantStatus.Suspended); // … a suspension lands …
        // … and the stored document carries something this code's model does not know (a field a newer version wrote).
        // A targeted write leaves it; a whole-document replace — even one that re-reads first — drops it. This is what
        // tells the two apart: a re-read would keep the suspension either way.
        var raw = _harness.Database.GetCollection<MongoDB.Bson.BsonDocument>(PlatformCollections.Tenants);
        await raw.UpdateOneAsync(new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(tenant.Id, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument("WrittenByANewerVersion", "keep me")));
        var at = DateTimeOffset.UtcNow;

        // … then the writer's write — through the interface, as every production writer calls it.
        await ((Diten.Platform.Domain.Repositories.ITenantRegistryRepository)_tenants).RecordAdminInvitationAsync(
            tenant.Id, admin.Id, TenantProvisioningStep.AdminInvitationKey, "Completed", "sent", at, stampInvitedAt: true,
            new TenantActivityEvent { EventType = "tenant.admin_user.invited", Message = "sent", At = at, Actor = "test" });

        var stored = (await _tenants.GetByIdAsync(tenant.Id))!;
        Assert.Equal(TenantStatus.Suspended, stored.Status);     // not put back
        var storedRaw = await raw.Find(new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(tenant.Id, MongoDB.Bson.GuidRepresentation.Standard))).SingleAsync();
        Assert.Equal("keep me", storedRaw["WrittenByANewerVersion"].AsString); // nothing else of the document was rewritten
        var step = Assert.Single(stored.ProvisioningSteps, s => s.Key == TenantProvisioningStep.AdminInvitationKey);
        Assert.Equal("Completed", step.Status);
        Assert.Equal("sent", step.Detail);
        Assert.NotNull(stored.AdminUsers.Single().InvitedAt);
        Assert.Contains(stored.ActivityTimeline, e => e.EventType == "tenant.admin_user.invited");
    }

    private Tenant NewTenant()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new Tenant
        {
            Id = Guid.NewGuid(), Code = "REC" + suffix.ToUpperInvariant(), Slug = "rec-" + suffix, Name = "rec-" + suffix,
            DisplayName = "Record " + suffix, Domain = "rec-" + suffix + ".test", Region = "EU", Environment = "Production"
        };
    }
}
