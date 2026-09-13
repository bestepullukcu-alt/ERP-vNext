using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-374 — against a REAL MongoDB: (1) a dispatch document written before <c>Attachments</c>/
/// <c>TemplateSemanticVersion</c> existed is still readable, with both defaulting exactly as a schema-additive
/// field should; (2) a dispatch written WITH an attachment round-trips its bytes exactly.
/// </summary>
public sealed class NotificationDispatchRetryFidelityMongoTests : IAsyncLifetime
{
    private MongoIntegrationHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await MongoIntegrationHarness.CreateIsolatedAsync(
        "notification_dispatch_retry_fidelity",
        SchemaProfile.Notification);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task An_old_dispatch_document_written_before_the_new_fields_existed_is_read_without_error()
    {
        var repository = new NotificationDispatchRepository(_harness.DbContext);
        var dispatch = MakeDispatch(_harness.TenantId);
        await repository.CreateAsync(dispatch);

        // Simulate a document written before BL-374: strip whatever field names the driver persisted for the
        // two new properties, exactly as a pre-existing production document would look — measured from the
        // driver's OWN output rather than assumed, so this does not depend on guessing a naming convention.
        var raw = _harness.Database.GetCollection<BsonDocument>("notification_dispatches");
        var stored = await raw.Find(Builders<BsonDocument>.Filter.Eq("_id", dispatch.Id)).FirstAsync();
        var newFieldNames = stored.Names
            .Where(n => n.Contains("attachment", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("templatesemanticversion", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(newFieldNames); // sanity: the fields really were written, so removing them is a real test
        foreach (var name in newFieldNames)
        {
            stored.Remove(name);
        }

        await raw.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", dispatch.Id), stored);

        var reread = await repository.GetByIdForTenantAsync(_harness.TenantId, dispatch.Id);

        Assert.NotNull(reread);
        Assert.Empty(reread!.Attachments);
        Assert.Null(reread.TemplateSemanticVersion);
        // Everything ELSE on the old-shape document still reads correctly — this is not a partial recovery.
        Assert.Equal(dispatch.Subject, reread.Subject);
        Assert.Equal(dispatch.TemplateKey, reread.TemplateKey);
    }

    [Fact]
    public async Task A_dispatch_written_WITH_an_attachment_round_trips_the_exact_bytes()
    {
        var repository = new NotificationDispatchRepository(_harness.DbContext);
        var dispatch = MakeDispatch(_harness.TenantId);
        var icsBytes = System.Text.Encoding.UTF8.GetBytes("BEGIN:VCALENDAR\r\nUID:test@diten\r\nEND:VCALENDAR\r\n");
        dispatch.Attachments =
        [
            new NotificationDispatchAttachment
            {
                FileName = "invite.ics",
                ContentType = "text/calendar; charset=utf-8; method=REQUEST",
                Content = icsBytes
            }
        ];
        dispatch.TemplateSemanticVersion = "3.1.0";

        await repository.CreateAsync(dispatch);
        var reread = await repository.GetByIdForTenantAsync(_harness.TenantId, dispatch.Id);

        Assert.NotNull(reread);
        Assert.Equal("3.1.0", reread!.TemplateSemanticVersion);
        var attachment = Assert.Single(reread.Attachments);
        Assert.Equal("invite.ics", attachment.FileName);
        Assert.Equal("text/calendar; charset=utf-8; method=REQUEST", attachment.ContentType);
        Assert.Equal(icsBytes, attachment.Content);
    }

    private static NotificationDispatch MakeDispatch(Guid tenantId) => new()
    {
        TenantId = tenantId,
        TemplateKey = "tenant.invite.email",
        TemplateId = Guid.NewGuid(),
        Locale = "en",
        Channel = NotificationChannelCode.Email,
        ProviderCode = MessagingProviderCode.Fake,
        Status = NotificationDispatchStatus.Queued,
        To = [new EmailRecipient { Email = "user@example.com" }],
        Subject = "Subject",
        BodyHtmlPreview = "<p>preview</p>",
        BodyTextPreview = "preview",
        VariablesJson = "{}",
        QueuedAt = DateTimeOffset.UtcNow,
        CorrelationId = "corr-retry-fidelity-mongo"
    };
}
