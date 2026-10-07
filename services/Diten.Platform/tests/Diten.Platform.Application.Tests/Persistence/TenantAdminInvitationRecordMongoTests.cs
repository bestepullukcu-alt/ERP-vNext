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
        var dispatchId = Guid.NewGuid();

        // … then the writer's write — through the interface, as every production writer calls it.
        await ((Diten.Platform.Domain.Repositories.ITenantRegistryRepository)_tenants).RecordAdminInvitationAsync(
            tenant.Id, admin.Id, TenantProvisioningStep.AdminInvitationKey, "Completed", "sent", at, stampInvitedAt: true, dispatchId,
            new TenantActivityEvent { EventType = "tenant.admin_user.invited", Message = "sent", At = at, Actor = "test" });

        var stored = (await _tenants.GetByIdAsync(tenant.Id))!;
        Assert.Equal(TenantStatus.Suspended, stored.Status);     // not put back
        var storedRaw = await raw.Find(new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(tenant.Id, MongoDB.Bson.GuidRepresentation.Standard))).SingleAsync();
        Assert.Equal("keep me", storedRaw["WrittenByANewerVersion"].AsString); // nothing else of the document was rewritten
        var step = Assert.Single(stored.ProvisioningSteps, s => s.Key == TenantProvisioningStep.AdminInvitationKey);
        Assert.Equal("Completed", step.Status);
        Assert.Equal("sent", step.Detail);
        Assert.NotNull(stored.AdminUsers.Single().InvitedAt);
        Assert.Equal(dispatchId, stored.AdminUsers.Single().LastInvitationDispatchId); // FIX3 (1): written with InvitedAt
        Assert.Contains(stored.ActivityTimeline, e => e.EventType == "tenant.admin_user.invited");
    }

    [Theory]
    [InlineData("same", TenantAdminUserStatus.Invited, true)]
    [InlineData("other", TenantAdminUserStatus.Invited, false)] // a newer "Invite" is the current state
    [InlineData("absent", TenantAdminUserStatus.Invited, true)] // a record from before the field: the safe side marks
    [InlineData("null", TenantAdminUserStatus.Invited, true)]   // the current invitation sent no e-mail
    [InlineData("same", TenantAdminUserStatus.Active, false)]   // the administrator got in after all
    public async Task An_undeliverable_invitation_is_written_only_while_it_is_the_current_one(string current, TenantAdminUserStatus status, bool written)
    {
        // BL-454 stage D FIX3 (1) — the condition is part of the write, on a real MongoDB.
        var tenant = NewTenant();
        var failing = Guid.NewGuid();
        var admin = new TenantAdminUser
        {
            Name = "First Admin", Email = "first@record.test", Status = status, InvitedAt = DateTimeOffset.UtcNow,
            LastInvitationDispatchId = current switch { "same" => failing, "other" => Guid.NewGuid(), _ => null }
        };
        tenant.AdminUsers.Add(admin);
        tenant.ProvisioningSteps.Add(new TenantProvisioningStep { Key = TenantProvisioningStep.AdminInvitationKey, Label = "Initial Admin Invitation", Status = "Completed" });
        await _tenants.CreateAsync(tenant);
        var raw = _harness.Database.GetCollection<MongoDB.Bson.BsonDocument>(PlatformCollections.Tenants);
        var id = new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(tenant.Id, MongoDB.Bson.GuidRepresentation.Standard));
        if (current == "absent")
        {
            await raw.UpdateOneAsync(id, new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument("AdminUsers.0.LastInvitationDispatchId", "")));
            Assert.False((await raw.Find(id).SingleAsync())["AdminUsers"][0].AsBsonDocument.Contains("LastInvitationDispatchId"));
        }

        var at = DateTimeOffset.UtcNow;
        var marked = await ((Diten.Platform.Domain.Repositories.ITenantRegistryRepository)_tenants).RecordUndeliveredInvitationAsync(
            tenant.Id, admin.Id, failing, TenantProvisioningStep.AdminInvitationKey, "undelivered", at,
            new TenantActivityEvent { EventType = "tenant.admin_user.invitation_undelivered", Message = "undelivered", At = at, Actor = "test" });

        var stored = (await _tenants.GetByIdAsync(tenant.Id))!;
        Assert.Equal(written, marked);
        Assert.Equal(written ? "Failed" : "Completed", Assert.Single(stored.ProvisioningSteps).Status);
        Assert.Equal(written, stored.ActivityTimeline.Any(e => e.EventType == "tenant.admin_user.invitation_undelivered"));
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
