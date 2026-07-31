using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// MOD-0027-FU04B — EventCode Dispatch Adapter. Unit "proof": eventCode → Active event → DefaultTemplateKey resolution +
/// validation + delegation to the existing QueueEmailNotificationCommand (fake IMediator). Real template render/provider
/// send is NOT proven here (that is the existing handler's job; optional live smoke covers it).
/// </summary>
public sealed class NotificationEventDispatchAdapterTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // --- failure paths: controlled Response, mediator NEVER called ---

    [Fact]
    public async Task Invalid_event_code_returns_400_and_does_not_dispatch()
    {
        var (adapter, mediator, _) = Build();
        var result = await adapter.DispatchByEventCodeAsync(Request("Not A Valid Code!"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonInvalidEventCode, result.ReasonCode);
        VerifyNeverDispatched(mediator);
    }

    [Fact]
    public async Task Unknown_event_returns_404_and_does_not_dispatch()
    {
        var (adapter, mediator, _) = Build(); // repo empty
        var result = await adapter.DispatchByEventCodeAsync(Request("tenant.user.invited"));

        Assert.Equal(404, result.StatusCode);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonEventNotFound, result.ReasonCode);
        VerifyNeverDispatched(mediator);
    }

    [Theory]
    [InlineData(NotificationEventStatus.Draft)]
    [InlineData(NotificationEventStatus.Deprecated)]
    [InlineData(NotificationEventStatus.Archived)]
    public async Task Non_active_event_returns_409_and_does_not_dispatch(NotificationEventStatus status)
    {
        var (adapter, mediator, repo) = Build();
        repo.Add(Event("tenant.user.invited", "tenant.invite.email", status, "TenantDisplayName"));

        var result = await adapter.DispatchByEventCodeAsync(Request("tenant.user.invited"));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonEventNotActive, result.ReasonCode);
        VerifyNeverDispatched(mediator);
    }

    [Fact]
    public async Task Missing_default_template_key_returns_422_and_does_not_dispatch()
    {
        var (adapter, mediator, repo) = Build();
        repo.Add(Event("tenant.user.invited", defaultTemplateKey: "", NotificationEventStatus.Active, "TenantDisplayName"));

        var result = await adapter.DispatchByEventCodeAsync(Request("tenant.user.invited"));

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonTemplateKeyMissing, result.ReasonCode);
        VerifyNeverDispatched(mediator);
    }

    [Fact]
    public async Task Missing_required_variable_returns_422_with_names_and_does_not_dispatch()
    {
        var (adapter, mediator, repo) = Build();
        repo.Add(Event("tenant.lifecycle.suspended", "tenant.suspended.email", NotificationEventStatus.Active,
            "TenantDisplayName", "Reason", "SuspendedAtUtc"));

        // Supply only TenantDisplayName; Reason empty, SuspendedAtUtc missing.
        var vars = new Dictionary<string, object?> { ["TenantDisplayName"] = "Acme", ["Reason"] = "  " };
        var result = await adapter.DispatchByEventCodeAsync(Request("tenant.lifecycle.suspended", vars));

        Assert.Equal(422, result.StatusCode);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonRequiredVariableMissing, result.ReasonCode);
        Assert.Contains("Reason", result.Errors[0]);
        Assert.Contains("SuspendedAtUtc", result.Errors[0]);
        VerifyNeverDispatched(mediator);
    }

    [Fact]
    public async Task Missing_recipient_returns_400_and_does_not_dispatch()
    {
        var (adapter, mediator, repo) = Build();
        repo.Add(Event("tenant.user.invited", "tenant.invite.email", NotificationEventStatus.Active, "TenantDisplayName"));

        var result = await adapter.DispatchByEventCodeAsync(
            Request("tenant.user.invited", to: Array.Empty<EmailRecipientDto>()));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(NotificationEventDispatchAdapter.ReasonRecipientMissing, result.ReasonCode);
        VerifyNeverDispatched(mediator);
    }

    // --- valid path: delegates to QueueEmailNotificationCommand, returns its result unchanged ---

    [Fact]
    public async Task Valid_event_delegates_to_queue_command_and_returns_result_unchanged()
    {
        var sentinel = Response<NotificationDispatchDto>.Success(Dispatch("tenant.invite.email"), 201);
        var (adapter, mediator, repo) = Build(sentinel);
        repo.Add(Event("tenant.user.invited", "tenant.invite.email", NotificationEventStatus.Active, "TenantDisplayName"));

        var vars = new Dictionary<string, object?> { ["TenantDisplayName"] = "Acme", ["Extra"] = "optional-passthrough" };
        var result = await adapter.DispatchByEventCodeAsync(Request("tenant.user.invited", vars));

        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        Assert.Same(sentinel.Data, result.Data);   // handler result returned unchanged

        mediator.Verify(m => m.Send(
            It.Is<QueueEmailNotificationCommand>(c =>
                c.TenantId == Tenant &&
                c.Request.TemplateKey == "tenant.invite.email" &&
                c.Request.To.Count == 1 &&
                c.Request.Variables.ContainsKey("Extra")),         // optional variables pass through
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Provider_failure_from_handler_is_passed_through()
    {
        var sentinel = Response<NotificationDispatchDto>.Fail("Messaging provider rejected the message.", 400, "PROVIDER_FAILURE");
        var (adapter, mediator, repo) = Build(sentinel);
        repo.Add(Event("tenant.user.invited", "tenant.invite.email", NotificationEventStatus.Active, "TenantDisplayName"));

        var result = await adapter.DispatchByEventCodeAsync(Request("tenant.user.invited"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("PROVIDER_FAILURE", result.ReasonCode);
        mediator.Verify(m => m.Send(It.IsAny<QueueEmailNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- 3 tenant event proof: correct eventCode → templateKey resolution ---

    [Theory]
    [InlineData("tenant.user.invited", "tenant.invite.email")]
    [InlineData("tenant.lifecycle.suspended", "tenant.suspended.email")]
    [InlineData("tenant.lifecycle.reactivated", "tenant.reactivated.email")]
    public async Task Tenant_event_resolves_to_expected_template_key(string eventCode, string expectedTemplateKey)
    {
        var sentinel = Response<NotificationDispatchDto>.Success(Dispatch(expectedTemplateKey), 201);
        var (adapter, mediator, repo) = Build(sentinel);
        repo.Add(Event(eventCode, expectedTemplateKey, NotificationEventStatus.Active,
            "TenantDisplayName", "Reason", "SuspendedAtUtc", "ReactivatedAtUtc")); // superset of required vars

        var vars = new Dictionary<string, object?>
        {
            ["TenantDisplayName"] = "Acme",
            ["Reason"] = "policy",
            ["SuspendedAtUtc"] = "2026-07-08",
            ["ReactivatedAtUtc"] = "2026-07-08"
        };
        var result = await adapter.DispatchByEventCodeAsync(Request(eventCode, vars));

        Assert.True(result.IsSuccessful);
        mediator.Verify(m => m.Send(
            It.Is<QueueEmailNotificationCommand>(c => c.Request.TemplateKey == expectedTemplateKey),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- helpers ---

    private static (NotificationEventDispatchAdapter adapter, Mock<IMediator> mediator, FakeEventRepo repo) Build(
        Response<NotificationDispatchDto>? sendResult = null)
    {
        var mediator = new Mock<IMediator>();
        if (sendResult is not null)
        {
            mediator
                .Setup(m => m.Send(It.IsAny<QueueEmailNotificationCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sendResult);
        }
        var repo = new FakeEventRepo();
        return (new NotificationEventDispatchAdapter(repo, mediator.Object, new PassThroughLocaleResolver()), mediator, repo);
    }

    private static void VerifyNeverDispatched(Mock<IMediator> mediator) =>
        mediator.Verify(m => m.Send(It.IsAny<QueueEmailNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Never);

    private static NotificationEventDispatchRequest Request(
        string eventCode,
        IReadOnlyDictionary<string, object?>? variables = null,
        IReadOnlyList<EmailRecipientDto>? to = null) =>
        new(
            TenantId: Tenant,
            EventCode: eventCode,
            To: to ?? new[] { new EmailRecipientDto("owner@acme.test", "Owner") },
            Variables: variables ?? new Dictionary<string, object?> { ["TenantDisplayName"] = "Acme" });

    private static NotificationEventDefinition Event(
        string eventCode, string defaultTemplateKey, NotificationEventStatus status, params string[] requiredVars) => new()
    {
        EventCode = eventCode,
        SourceType = NotificationEventSourceType.PlatformSeed,
        OwnerModuleId = "MOD-0009",
        Channel = NotificationChannelCode.Email,
        DefaultTemplateKey = defaultTemplateKey,
        FallbackDisplayName = eventCode,
        Status = status,
        RequiredVariables = requiredVars
            .Select(n => new TemplateVariableDefinition { Name = n, Type = TemplateVariableType.String, IsRequired = true })
            .ToList()
    };

    private static NotificationDispatchDto Dispatch(string templateKey) => new(
        Guid.NewGuid(), Tenant, templateKey, null, "en", "Email", "Smtp", null, "Sent",
        new[] { new EmailRecipientDto("owner@acme.test", "Owner") }, 0, 0, "Subject",
        null, null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, 0, null, null, null);

    private sealed class FakeEventRepo : INotificationEventDefinitionRepository
    {
        private readonly List<NotificationEventDefinition> _items = new();
        public void Add(NotificationEventDefinition e) => _items.Add(e);

        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x =>
                string.Equals(x.EventCode, (eventCode ?? "").Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)));

        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition d, CancellationToken ct = default)
        { _items.Add(d); return Task.FromResult(d); }
        public Task UpdateAsync(NotificationEventDefinition d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
            string? ownerModuleId = null, NotificationChannelCode? channel = null, NotificationEventStatus? status = null,
            bool? canTenantOverride = null, NotificationEventUsageType? usageType = null,
            int skip = 0, int take = 100, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(_items);
        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(
                _items.Where(x => x.Status == NotificationEventStatus.Active).ToList());
    }
}

/// <summary>
/// Keeps these tests about the adapter's own rules: it answers what the caller asked for, and "en" when the caller
/// asked for nothing. The real chain (tenant settings → profile default → "en") is covered by
/// TenantNotificationLocaleResolverTests, and the two together are covered end-to-end by TaskNotificationLocaleTests.
/// </summary>
internal sealed class PassThroughLocaleResolver
    : Diten.Platform.Application.Features.Notifications.Services.INotificationLocaleResolver
{
    public Task<string> ResolveAsync(Guid tenantId, string? requested, CancellationToken ct = default)
        => Task.FromResult(string.IsNullOrWhiteSpace(requested) ? "en" : requested.Trim().ToLowerInvariant());
}
