using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// MOD-0357 S5c — <c>MeetingAttendeeRepository.ListPendingByUserIdAsync</c> against a REAL MongoDB.
///
/// <para>K5's promise — an answered invitation never comes back as work — is kept in production by ONE query:
/// the tenant execution filter plus <c>InvitationResponse == Pending</c>. Every test that asserts K5 today drives
/// <c>FakeMeetingAttendeeRepository</c>, which applies that filter itself in LINQ, so the suite proves the PROVIDER
/// honours whatever the repository returns while proving nothing about what the repository returns. Drop the clause
/// from the real query and an accepted invitation reappears in the inbox with every existing test still green —
/// measured by the Control Tower, 2026-09-12.</para>
/// </summary>
public sealed class MeetingAttendeePendingQueryMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;
    private MeetingAttendeeRepository _repository = null!;
    private readonly Guid _meetingId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.Meetings);
        _repository = new MeetingAttendeeRepository(_harness.DbContext, _harness.TenantContext);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private IMongoCollection<MeetingAttendee> Collection =>
        _harness.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees);

    private MeetingAttendee Row(InvitationResponse response, Guid? tenantId = null, Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId ?? _harness.TenantId,
        MeetingId = Guid.NewGuid(),
        UserId = userId ?? _userId,
        InvitationResponse = response
    };

    [Fact]
    public async Task Only_a_pending_invitation_of_this_tenants_user_comes_back()
    {
        var pending = Row(InvitationResponse.Pending);
        var accepted = Row(InvitationResponse.Accepted);
        var declined = Row(InvitationResponse.Declined);
        var otherTenant = Row(InvitationResponse.Pending, tenantId: _otherTenantId);
        var otherUser = Row(InvitationResponse.Pending, userId: Guid.NewGuid());
        await Collection.InsertManyAsync([pending, accepted, declined, otherTenant, otherUser]);

        var rows = await _repository.ListPendingByUserIdAsync(_userId);

        Assert.Equal(pending.Id, Assert.Single(rows).Id);
    }

    [Fact]
    public async Task A_soft_deleted_pending_invitation_does_not_come_back()
    {
        var removed = Row(InvitationResponse.Pending);
        removed.IsDeleted = true;
        await Collection.InsertOneAsync(removed);

        Assert.Empty(await _repository.ListPendingByUserIdAsync(_userId));
    }

    [Fact]
    public async Task Another_tenants_context_cannot_read_this_tenants_pending_invitation()
    {
        await Collection.InsertOneAsync(Row(InvitationResponse.Pending));

        _harness.TenantContext.SetTenant(_otherTenantId);
        try
        {
            Assert.Empty(await _repository.ListPendingByUserIdAsync(_userId));
        }
        finally
        {
            _harness.TenantContext.SetTenant(_harness.TenantId);
        }
    }
}
