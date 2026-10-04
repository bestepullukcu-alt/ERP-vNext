using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// BL-454 — the versioned seed step, against a real MongoDB.
///
/// <para>The subject is DATABASE-GLOBAL — platform-default rows, <c>TenantId == null</c> — so it cannot be isolated
/// by tenant; it gets the harness's fixed-name isolated database (DB-010), emptied before each test.</para>
/// </summary>
public sealed class NotificationTemplateSeedUpgradeMongoTests : IAsyncLifetime
{
    private const string Key = "platform.tasks.assigned";

    private MongoIntegrationHarness _harness = null!;
    private IMongoCollection<NotificationTemplate> _templates = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateIsolatedAsync("email_shell_seed", SchemaProfile.Notification);
        _templates = _harness.Database.GetCollection<NotificationTemplate>(PlatformCollections.NotificationTemplates);
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_new_database_gets_the_current_template_in_seven_languages_and_nothing_to_upgrade()
    {
        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(0, result.Upgraded);
        Assert.Equal(0, result.KeptModified);
        Assert.True(result.Inserted >= 167, $"Inserted {result.Inserted}");

        var rows = await AssignedRowsAsync();
        Assert.Equal(NotificationTemplateSeed.TenantLocales.OrderBy(x => x), rows.Select(r => r.Locale).OrderBy(x => x));
        Assert.All(rows, row =>
        {
            Assert.Equal("1.1.0", row.SemanticVersion);
            Assert.NotNull(row.Shell);
            Assert.Equal("TaskUrl", row.Shell!.ActionUrlVariable);
        });
    }

    [Fact]
    public async Task An_untouched_previous_seed_row_is_carried_forward_and_a_second_start_writes_nothing()
    {
        await InsertPreviousSeedAsync();

        var first = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(7, first.Upgraded);
        Assert.Equal(0, first.KeptModified);
        var afterFirst = await AssignedRowsAsync();
        Assert.Equal(7, afterFirst.Count);
        Assert.All(afterFirst, row =>
        {
            var current = NotificationTemplateSeed.TaskAssigned(row.Locale);
            Assert.Equal("1.1.0", row.SemanticVersion);
            Assert.Equal(current.BodyHtmlTemplate, row.BodyHtmlTemplate);
            Assert.Equal(current.Shell!.HeadingTemplate, row.Shell!.HeadingTemplate);
            Assert.Equal(current.Shell.InfoRows.Count, row.Shell.InfoRows.Count);
            Assert.Equal("system.seed", row.UpdatedBy);
        });

        var second = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(0, second.Inserted);
        Assert.Equal(0, second.Upgraded);
        Assert.Equal(0, second.KeptModified);
        var afterSecond = await AssignedRowsAsync();
        // Zero writes: not one row's UpdatedAt moved.
        Assert.Equal(
            afterFirst.OrderBy(r => r.Locale).Select(r => (r.Id, r.UpdatedAt)),
            afterSecond.OrderBy(r => r.Locale).Select(r => (r.Id, r.UpdatedAt)));
    }

    [Fact]
    public async Task A_row_an_operator_changed_is_left_exactly_as_it_is()
    {
        await InsertPreviousSeedAsync();
        // Two different operator edits, neither of which moved SemanticVersion off "1.0.0":
        // one changed the body through the screen (UpdatedAt set), one was changed directly (UpdatedAt still null).
        await _templates.UpdateOneAsync(
            x => x.TemplateKey == Key && x.Locale == "tr" && x.TenantId == null,
            Builders<NotificationTemplate>.Update
                .Set(x => x.BodyHtmlTemplate, "<p>Operatörün kendi metni {{TaskTitle}}</p>")
                .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow));
        await _templates.UpdateOneAsync(
            x => x.TemplateKey == Key && x.Locale == "fr" && x.TenantId == null,
            Builders<NotificationTemplate>.Update.Set(x => x.SubjectTemplate, "Sujet de l'opérateur : {{TaskTitle}}"));

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(5, result.Upgraded);
        Assert.Equal(2, result.KeptModified);
        var rows = await AssignedRowsAsync();
        var turkish = rows.Single(r => r.Locale == "tr");
        Assert.Equal("<p>Operatörün kendi metni {{TaskTitle}}</p>", turkish.BodyHtmlTemplate);
        Assert.Equal("1.0.0", turkish.SemanticVersion);
        Assert.Null(turkish.Shell);
        var french = rows.Single(r => r.Locale == "fr");
        Assert.Equal("Sujet de l'opérateur : {{TaskTitle}}", french.SubjectTemplate);
        Assert.Equal(NotificationTemplateSeed.TaskAssignedV1("fr").BodyHtmlTemplate, french.BodyHtmlTemplate);
        Assert.Null(french.Shell);
        // …and no second row was inserted beside the kept one.
        Assert.Equal(7, rows.Count);
    }

    [Fact]
    public async Task A_tenants_own_override_is_never_touched_even_when_it_is_identical_to_the_old_seed()
    {
        await InsertPreviousSeedAsync();
        var tenant = Guid.NewGuid();
        var overrideRow = NotificationTemplateSeed.TaskAssignedV1("en");
        overrideRow.TenantId = tenant;
        overrideRow.IsPlatformDefault = false;
        await _templates.InsertOneAsync(overrideRow);

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(7, result.Upgraded);
        var kept = await _templates.Find(x => x.TenantId == tenant).SingleAsync();
        Assert.Equal("1.0.0", kept.SemanticVersion);
        Assert.Null(kept.Shell);
        Assert.Null(kept.UpdatedAt);
        Assert.Equal(NotificationTemplateSeed.TaskAssignedV1("en").BodyHtmlTemplate, kept.BodyHtmlTemplate);
    }

    [Fact]
    public async Task Two_instances_starting_together_upgrade_each_row_exactly_once()
    {
        // Everything else already present, so the only work either instance has is the upgrade itself.
        await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });
        await _templates.DeleteManyAsync(x => x.TemplateKey == Key);
        await InsertPreviousSeedAsync();

        var results = await Task.WhenAll(
            Task.Run(() => NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { })),
            Task.Run(() => NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { })));

        Assert.Equal(7, results.Sum(r => r.Upgraded));
        Assert.Equal(0, results.Sum(r => r.Inserted));
        var rows = await AssignedRowsAsync();
        Assert.Equal(7, rows.Count);
        Assert.All(rows, row => Assert.Equal("1.1.0", row.SemanticVersion));
    }

    [Fact]
    public async Task What_the_step_did_is_written_to_the_log()
    {
        await InsertPreviousSeedAsync();
        var lines = new List<string>();

        await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: lines.Add);

        var line = Assert.Single(lines);
        Assert.StartsWith("notification.template.seed ", line);
        Assert.Contains("Upgraded=7", line);
        Assert.Contains("KeptModified=0", line);
    }

    [Fact]
    public async Task A_row_the_seed_itself_carried_forward_is_carried_forward_again_by_the_next_version()
    {
        // 1.0.0 → 1.1.0 stamps the row (UpdatedAt, UpdatedBy = system.seed). A later 1.1.0 → 1.2.0 must not read that
        // stamp as "an operator changed this" and skip the row.
        await InsertPreviousSeedAsync();
        await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        var nextVersion = NotificationTemplateSeed.TenantLocales.Select(locale =>
        {
            var current = NotificationTemplateSeed.TaskAssigned(locale);
            var next = NotificationTemplateSeed.TaskAssigned(locale);
            next.SemanticVersion = "1.2.0";
            next.BodyHtmlTemplate = current.BodyHtmlTemplate + "<p>1.2.0</p>";
            return (current, next);
        }).ToList();

        var (upgraded, kept) = await NotificationTemplateSeed.UpgradeUntouchedSeedsAsync(_templates, nextVersion, CancellationToken.None);

        Assert.Equal(7, upgraded);
        Assert.Equal(0, kept);
        Assert.All(await AssignedRowsAsync(), row => Assert.Equal("1.2.0", row.SemanticVersion));
    }

    [Fact]
    public async Task A_row_whose_only_difference_from_the_old_seed_is_its_variables_is_left_as_it_is()
    {
        await InsertPreviousSeedAsync();
        await _templates.UpdateOneAsync(
            x => x.TemplateKey == Key && x.Locale == "es" && x.TenantId == null,
            Builders<NotificationTemplate>.Update.Set(x => x.Variables, new List<TemplateVariableDefinition>
            {
                new() { Name = "TaskTitle", Type = Diten.Platform.Domain.Enums.TemplateVariableType.String, IsRequired = true }
            }));

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(6, result.Upgraded);
        Assert.Equal(1, result.KeptModified);
        Assert.Equal("1.0.0", (await AssignedRowsAsync()).Single(r => r.Locale == "es").SemanticVersion);
    }

    [Fact]
    public async Task A_row_an_operator_saved_with_identical_content_is_theirs_and_is_not_carried_forward()
    {
        await InsertPreviousSeedAsync();
        var french = (await AssignedRowsAsync()).Single(r => r.Locale == "fr");

        // The operator's save through the real handler: same content, but now signed by the operator.
        var handler = new Diten.Platform.Application.Features.Notifications.Handlers.CommandHandlers.UpdateNotificationTemplateHandler(
            new Diten.Platform.Infrastructure.Persistence.Repositories.NotificationTemplateRepository(_harness.DbContext));
        var saved = await handler.Handle(new Diten.Platform.Application.Features.Notifications.Commands.UpdateNotificationTemplateCommand(
            french.Id,
            null,
            new Diten.Platform.Application.Features.Notifications.NotificationTemplateUpsertRequest(
                true, french.TemplateKey, "Email", french.Locale, french.SubjectTemplate, french.BodyHtmlTemplate,
                french.BodyTextTemplate,
                french.Variables.Select(v => new Diten.Platform.Application.Features.Notifications.TemplateVariableDefinitionDto(v.Name, v.Type.ToString(), v.IsRequired)).ToList(),
                "Active", french.SemanticVersion)), CancellationToken.None);
        Assert.True(saved.IsSuccessful, string.Join(" | ", saved.Errors));

        var result = await NotificationTemplateSeed.EnsureSeededAsync(_harness.Database, log: _ => { });

        Assert.Equal(6, result.Upgraded);
        Assert.Equal(1, result.KeptModified);
        var kept = (await AssignedRowsAsync()).Single(r => r.Locale == "fr");
        Assert.Equal("1.0.0", kept.SemanticVersion);
        Assert.Equal("operator", kept.UpdatedBy);
    }

    [Fact]
    public async Task The_upgrade_write_matches_nothing_once_the_row_was_signed_by_someone_else()
    {
        await InsertPreviousSeedAsync();
        var row = (await AssignedRowsAsync()).Single(r => r.Locale == "ru");
        var previous = NotificationTemplateSeed.TaskAssignedV1("ru");
        var scope = Builders<NotificationTemplate>.Filter.Eq(x => x.Id, row.Id);

        Assert.Equal(1, await _templates.CountDocumentsAsync(NotificationTemplateSeed.UpgradeWriteFilter(scope, row, previous)));

        // Signed in between the read and the write — same time stamp, different signature.
        await _templates.UpdateOneAsync(x => x.Id == row.Id, Builders<NotificationTemplate>.Update.Set(x => x.UpdatedBy, "operator"));

        Assert.Equal(0, await _templates.CountDocumentsAsync(NotificationTemplateSeed.UpgradeWriteFilter(scope, row, previous)));
    }

    private Task InsertPreviousSeedAsync() =>
        _templates.InsertManyAsync(NotificationTemplateSeed.TenantLocales.Select(NotificationTemplateSeed.TaskAssignedV1));

    private async Task<List<NotificationTemplate>> AssignedRowsAsync() =>
        await _templates.Find(x => x.TemplateKey == Key && x.TenantId == null && !x.IsDeleted).ToListAsync();
}
