using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-454 — the task-assignment e-mail's CONTENT: what <see cref="TaskNotificationService"/> supplies, what the seeded
/// template does with it in seven languages, and that nothing travels that the reader has no claim to.
/// </summary>
public sealed class TaskAssignedEmailTests
{
    private static readonly Guid Assignee = Guid.NewGuid();
    private static readonly Guid Assigner = Guid.NewGuid();
    private static readonly string[] Seven = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public async Task An_assignment_carries_the_tasks_address_its_priority_in_the_mails_language_and_the_assigners_name()
    {
        var adapter = new RecordingNotificationDispatchAdapter();
        var task = NewTask(TaskPriority.High);

        await Service(adapter, "tr").NotifyAsync(task, TaskNotificationEvents.Assigned, [Assignee], Assigner, CancellationToken.None);

        var variables = Assert.Single(adapter.Requests).Variables;
        Assert.Equal($"https://di10.example/WorkCenterNext/Details/{task.Id}", variables["TaskUrl"]);
        Assert.Equal("Yüksek", variables["Priority"]);
        Assert.Equal("Burak Şen", variables["AssignerName"]);
        // The three the event always carried are unchanged.
        Assert.Equal("Parti kaydı incelemesi", variables["TaskTitle"]);
        Assert.Equal(task.Id.ToString(), variables["TaskId"]);
    }

    [Fact]
    public async Task The_assigners_address_never_travels()
    {
        var adapter = new RecordingNotificationDispatchAdapter();

        await Service(adapter, "en").NotifyAsync(NewTask(TaskPriority.Low), TaskNotificationEvents.Assigned, [Assignee], Assigner, CancellationToken.None);

        var request = Assert.Single(adapter.Requests);
        Assert.DoesNotContain(request.Variables.Values, value => (value?.ToString() ?? string.Empty).Contains("burak@", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(request.To, recipient => recipient.Email.Contains("burak@", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task An_event_other_than_assignment_names_no_assigner()
    {
        var adapter = new RecordingNotificationDispatchAdapter();

        await Service(adapter, "en").NotifyAsync(NewTask(TaskPriority.Medium), TaskNotificationEvents.Completed, [Assignee], Assigner, CancellationToken.None);

        Assert.Equal(string.Empty, Assert.Single(adapter.Requests).Variables["AssignerName"]);
    }

    [Fact]
    public async Task A_failing_assigner_lookup_does_not_cost_the_notification()
    {
        var adapter = new RecordingNotificationDispatchAdapter();
        var people = new People { ThrowFor = Assigner };

        var outcome = await Service(adapter, "en", people).NotifyAsync(
            NewTask(TaskPriority.Medium), TaskNotificationEvents.Assigned, [Assignee], Assigner, CancellationToken.None);

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        Assert.Equal(string.Empty, Assert.Single(adapter.Requests).Variables["AssignerName"]);
    }

    [Fact]
    public void Without_a_configured_web_origin_there_is_no_address_rather_than_a_relative_one()
    {
        Assert.Equal(string.Empty, new TaskWebLinks(Options.Create(new AuthServiceOptions { FrontendBaseUrl = " " })).Detail(Guid.NewGuid()));

        var id = Guid.NewGuid();
        Assert.Equal(
            "https://di10.example" + TaskLinks.Detail(id),
            new TaskWebLinks(Options.Create(new AuthServiceOptions { FrontendBaseUrl = "https://di10.example/" })).Detail(id));
    }

    [Fact]
    public void A_priority_has_its_own_word_in_each_of_the_seven_languages()
    {
        foreach (var priority in Enum.GetValues<TaskPriority>())
        {
            var words = Seven.Select(language => TaskEmailContent.PriorityLabel(priority, language)).ToList();
            Assert.Equal(7, words.Distinct().Count());
            Assert.All(words, word => Assert.False(string.IsNullOrWhiteSpace(word)));
        }

        Assert.Equal("High", TaskEmailContent.PriorityLabel(TaskPriority.High, "de"));
        Assert.Equal("Yüksek", TaskEmailContent.PriorityLabel(TaskPriority.High, "tr-TR"));
    }

    [Fact]
    public void The_seeded_template_fills_the_shell_in_seven_languages_and_none_is_a_copy_of_English()
    {
        var english = NotificationTemplateSeed.TaskAssigned("en");

        foreach (var language in Seven)
        {
            var template = NotificationTemplateSeed.TaskAssigned(language);
            var shell = template.Shell!;

            Assert.Equal("1.1.0", template.SemanticVersion);
            Assert.False(string.IsNullOrWhiteSpace(shell.HeadingTemplate));
            Assert.Equal(
                ["{{TaskTitle}}", "{{DueAt}}", "{{Priority}}", "{{AssignerName}}"],
                shell.InfoRows.Select(row => row.ValueTemplate));
            Assert.All(shell.InfoRows, row => Assert.False(string.IsNullOrWhiteSpace(row.Label)));
            Assert.False(string.IsNullOrWhiteSpace(shell.ActionLabel));
            Assert.Equal("TaskUrl", shell.ActionUrlVariable);
            Assert.False(string.IsNullOrWhiteSpace(shell.FootnoteTemplate));
            // The subject and the required variables are what version 1.0.0 had: the event's contract did not move.
            Assert.Equal(NotificationTemplateSeed.TaskAssignedV1(language).SubjectTemplate, template.SubjectTemplate);
            Assert.Equal(["TaskTitle", "TaskId"], template.Variables.Where(v => v.IsRequired).Select(v => v.Name));
            // There is no "preferences" link: that screen does not exist.
            Assert.DoesNotContain("<a", template.BodyHtmlTemplate);

            if (language == "en")
            {
                continue;
            }

            Assert.NotEqual(english.Shell!.HeadingTemplate, shell.HeadingTemplate);
            Assert.NotEqual(english.BodyHtmlTemplate, template.BodyHtmlTemplate);
            Assert.NotEqual(english.Shell.ActionLabel, shell.ActionLabel);
            Assert.NotEqual(english.Shell.FootnoteTemplate, shell.FootnoteTemplate);
            Assert.Empty(english.Shell.InfoRows.Select(r => r.Label).Intersect(shell.InfoRows.Select(r => r.Label)));
        }
    }

    [Fact]
    public void Every_upgrade_pair_is_the_same_key_and_language_and_really_differs()
    {
        var pairs = NotificationTemplateSeed.SeedUpgrades();

        Assert.Equal(7, pairs.Count);
        Assert.All(pairs, pair =>
        {
            Assert.Equal(pair.Previous.TemplateKey, pair.Current.TemplateKey);
            Assert.Equal(pair.Previous.Locale, pair.Current.Locale);
            Assert.Equal("1.0.0", pair.Previous.SemanticVersion);
            Assert.Null(pair.Previous.Shell);
            Assert.NotEqual(pair.Previous.SemanticVersion, pair.Current.SemanticVersion);
        });
    }

    private static TaskNotificationService Service(RecordingNotificationDispatchAdapter adapter, string locale, People? people = null) => new(
        adapter,
        new FakeNotificationLocaleResolver(locale),
        people ?? new People(),
        new FakePositionAssignmentRepository(),
        new FakeUserNotificationRepository(),
        new FakeTenantContext(TaskTestData.Tenant),
        NullLogger<TaskNotificationService>.Instance,
        new TaskWebLinks(Options.Create(new AuthServiceOptions { FrontendBaseUrl = "https://di10.example" })));

    private static TaskItem NewTask(TaskPriority priority) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Parti kaydı incelemesi",
        Priority = priority,
        AssigneeUserId = Assignee,
        AssignmentTarget = TaskAssignmentTarget.Person,
        OrganizationUnitId = Guid.NewGuid(),
        EmailNotificationsEnabled = true,
        Version = 1
    };

    /// <summary>What the AuthService contact lookup returns: id, address, display name.</summary>
    private sealed class People : ITaskNotificationRecipientResolver
    {
        public Guid? ThrowFor { get; init; }

        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            if (ThrowFor is { } id && userIds.Contains(id))
            {
                throw new InvalidOperationException("AuthService is unreachable.");
            }

            return Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(userIds.Select(userId =>
                userId == Assigner
                    ? new TaskNotificationRecipient(userId, "burak@ditenpharma.test", "Burak Şen")
                    : new TaskNotificationRecipient(userId, "ayse@ditenpharma.test", "Ayşe Kaya")).ToList());
        }
    }
}
