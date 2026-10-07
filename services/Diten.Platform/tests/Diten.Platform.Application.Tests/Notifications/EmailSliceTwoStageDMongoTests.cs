using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — WP-EMAIL-SHELL-01 slice 2, stage D, on a real MongoDB: the invitation's 1.1.0 rows (stage C, seven languages,
/// no link) are carried forward to 1.2.0, an operator's edit is kept — and counted ONCE although two previous seeds
/// (1.0.0 and 1.1.0) lead to the same 1.2.0. The seed is database-global: an isolated database of its own (DB-010).
/// </summary>
public sealed class EmailSliceTwoStageDMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync() =>
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("email_shell_stage_d", SchemaProfile.Notification);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    private IMongoCollection<NotificationTemplate> Templates =>
        _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);

    [Fact]
    public async Task The_invitations_untouched_1_1_0_rows_get_the_link_and_an_edited_one_is_kept_and_counted_once()
    {
        await Templates.InsertManyAsync(NotificationTemplateSeed.TenantLocales.Select(NotificationTemplateSeed.TenantInviteV11));
        var edited = await Templates.Find(t => t.TemplateKey == "tenant.invite.email" && t.Locale == "en" && t.TenantId == null).SingleAsync();
        await Templates.UpdateOneAsync(t => t.Id == edited.Id, Builders<NotificationTemplate>.Update
            .Set(t => t.BodyHtmlTemplate, "<p>Our own invitation to {{TenantDisplayName}} ({{TenantId}}).</p>")
            .Set(t => t.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(t => t.UpdatedBy, "operator"));

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(6, result.Upgraded);
        Assert.Equal(1, result.KeptModified);
        var rows = await Templates.Find(t => t.TemplateKey == "tenant.invite.email" && t.TenantId == null).ToListAsync();
        Assert.Equal(7, rows.Count);
        var kept = Assert.Single(rows, t => t.Id == edited.Id);
        Assert.Equal("1.1.0", kept.SemanticVersion);
        Assert.Equal("operator", kept.UpdatedBy);
        Assert.All(rows.Where(t => t.Id != edited.Id), t =>
        {
            Assert.Equal("1.2.0", t.SemanticVersion);
            Assert.Equal("SetPasswordUrl", t.Shell!.ActionUrlVariable);
        });

        var second = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });
        Assert.Equal(0, second.Upgraded);
        Assert.Equal(0, second.Inserted);
    }
}
