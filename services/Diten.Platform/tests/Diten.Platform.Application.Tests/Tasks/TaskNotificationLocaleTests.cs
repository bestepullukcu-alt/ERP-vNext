using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Notifications.Validators;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.SelfRegistration;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-4 follow-up — <b>the locale, and the reason nothing was ever sent.</b>
///
/// <para><b>The defect, exactly.</b> <c>NotificationEventDispatchRequest.Locale</c> was declared
/// <c>string? Locale = null</c>, so MOD-0024 read it as optional and left it out. The adapter then forwarded
/// <c>request.Locale ?? string.Empty</c> into <c>QueueEmailNotificationCommand</c>, whose validator says
/// <c>RuleFor(x =&gt; x.Request.Locale).NotEmpty()</c>. Every one of the five task events threw a
/// ValidationException at the pipeline, no dispatch row was written, and the inbox stayed empty — with 44 green
/// WC-4 tests, because every one of them stopped at a fake adapter.</para>
///
/// <para><b>Why this file exists rather than one more assertion somewhere.</b> Filling one field closes this
/// defect. Running the chain through the REAL validator closes the class: the next producer that forgets a
/// required field fails here instead of in a customer's log. This is the same cure applied to
/// <c>FakeWorkflowMediator</c> when the review slice shipped an empty candidate list — a chain that ends in a fake
/// proves nothing about the request production actually sends.</para>
///
/// <para><b>What is real here.</b> The task service, the dispatch adapter, the locale resolver, the FluentValidation
/// validator and <see cref="ValidationBehavior{TRequest,TResponse}"/> are all production types. The event
/// definitions come from MOD-0024's own manifest. Only the two edges are doubles: the tenant registry (a Mongo
/// collection) and the terminal handler (which would need a template repository, renderer, provider resolver and
/// event bus — all of them covered by the notification suite's own end-to-end tests, and none of them the defect).
/// </para>
/// </summary>
public sealed class TaskNotificationLocaleTests
{
    // ── 1. The defect itself: RED before the fix ─────────────────────────────

    [Fact]
    public async Task A_task_event_survives_the_REAL_validator_and_produces_a_queued_command()
    {
        /*
         * The whole ticket in one assertion. Before the fix this threw:
         *   FluentValidation.ValidationException: 'Locale' must not be empty.
         * because ValidationBehavior's reflective Response<T>.Fail(IReadOnlyList<string>, int) lookup finds no such
         * overload (the real one takes four parameters), so it falls through to `throw`.
         */
        var harness = new Harness();

        var outcome = await harness.NotifyAssignedAsync();

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        var queued = Assert.Single(harness.Queued);
        Assert.False(string.IsNullOrWhiteSpace(queued.Request.Locale));
    }

    [Theory]
    [InlineData(TaskNotificationEvents.Assigned)]
    [InlineData(TaskNotificationEvents.Claimed)]
    [InlineData(TaskNotificationEvents.Completed)]
    [InlineData(TaskNotificationEvents.ApprovalRequested)]
    public async Task EVERY_dispatched_event_gets_through_not_just_the_one_that_was_debugged(string eventCode)
    {
        // The defect was in shared code, so it hit all four identically. A single-event test would have let a
        // per-event regression through, and "assigned works" was never the question.
        var harness = new Harness();

        var outcome = await harness.NotifyAsync(eventCode);

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        Assert.Single(harness.Queued);
    }

    // ── 2. The chain is real, and provably so ────────────────────────────────

    [Fact]
    public async Task The_chain_REJECTS_a_request_the_real_validator_would_reject()
    {
        /*
         * Non-vacuity for every test above, and the only assertion that proves the validator is actually in the
         * chain rather than merely referenced by it. A recipient with a malformed address violates
         * EmailRecipientDtoValidator; if this passes, the harness is a fake wearing a validator's name.
         */
        var harness = new Harness(recipientEmail: "not-an-email");

        var outcome = await harness.NotifyAssignedAsync();

        Assert.Equal(TaskNotificationOutcome.Failed, outcome);
        Assert.Empty(harness.Queued);
    }

    [Fact]
    public async Task A_dispatch_with_NO_TENANT_is_refused_by_that_same_validator_too()
    {
        /*
         * A second, independent rule of the same validator (RuleFor(x => x.TenantId).NotEmpty()), so the chain's
         * realness does not rest on one assertion. It is also a live scenario rather than a contrivance: a sweep
         * running outside tenant context hands the service Guid.Empty, and the whole point of WC-4 is that such a
         * dispatch must fail loudly here rather than write a tenant-less notification row.
         */
        var harness = new Harness(tenantId: Guid.Empty);

        /*
         * It THROWS rather than returning a refusal, and that is the shape worth pinning. ValidationBehavior looks
         * for Response<T>.Fail(IReadOnlyList<string>, int) by reflection; the real overload takes four parameters,
         * so the lookup misses and every FluentValidation failure falls through to `throw`. The adapter's own
         * refusals are controlled reason codes — a downstream validator's are not, which is exactly the escape that
         * surfaced as an unhandled ValidationException when the blank locale shipped, and exactly why the task call
         * sites are wrapped in TaskNotificationSafely.
         */
        await Assert.ThrowsAsync<ValidationException>(() => harness.DispatchDirectlyAsync(locale: null));

        Assert.Empty(harness.Queued);
    }

    [Fact]
    public async Task A_blank_locale_reaching_the_command_is_REFUSED_by_that_same_validator()
    {
        /*
         * The mutation, pinned as a test so it cannot be argued away: a producer that hands the adapter a locale of
         * whitespace does NOT get whitespace forwarded — the resolver treats "I said nothing" and "I said nothing
         * loudly" identically. Were the old `?? string.Empty` restored, this and the four above go red together.
         */
        var harness = new Harness();

        var response = await harness.DispatchDirectlyAsync(locale: "   ");

        Assert.True(response.IsSuccessful);
        Assert.Equal("en", Assert.Single(harness.Queued).Request.Locale);
    }

    // ── 3. The fallback chain, link by link ──────────────────────────────────

    [Fact]
    public async Task The_tenants_RUNTIME_language_wins_over_its_profile_default()
    {
        // Tenant.cs: "tenant profile defaults — TenantSettings holds runtime overrides". The settings screen is the
        // more recent statement of intent, so it is link 2 and DefaultLanguage is link 3.
        var harness = new Harness(tenantSettingsLanguage: "tr", tenantDefaultLanguage: "fr");

        await harness.NotifyAssignedAsync();

        Assert.Equal("tr", Assert.Single(harness.Queued).Request.Locale);
    }

    [Fact]
    public async Task The_profile_default_is_used_when_settings_were_never_touched()
    {
        var harness = new Harness(tenantSettingsLanguage: "  ", tenantDefaultLanguage: "es");

        await harness.NotifyAssignedAsync();

        Assert.Equal("es", Assert.Single(harness.Queued).Request.Locale);
    }

    [Fact]
    public async Task An_unreadable_tenant_registry_still_sends_in_English_rather_than_sending_nothing()
    {
        /*
         * The floor, and a deliberate ranking: a notification in the wrong language reaches somebody, a refused one
         * reaches nobody. This resolver sits in front of every dispatch in the platform — letting it throw would
         * turn one unreachable collection into total notification silence.
         */
        var harness = new Harness(tenantRegistryThrows: true);

        var outcome = await harness.NotifyAssignedAsync();

        Assert.Equal(TaskNotificationOutcome.Dispatched, outcome);
        Assert.Equal("en", Assert.Single(harness.Queued).Request.Locale);
    }

    [Fact]
    public async Task A_producer_that_KNOWS_the_language_still_wins_outright()
    {
        /*
         * The regression this ticket most had to avoid. TenantLifecycleNotificationConsumer already passes a locale
         * from the provisioning envelope; if the resolver overrode it with the tenant record, invitations would
         * start arriving in the wrong language — a working feature broken by a fix to a broken one.
         */
        var harness = new Harness(tenantSettingsLanguage: "tr");

        await harness.DispatchDirectlyAsync(locale: "RU");

        Assert.Equal("ru", Assert.Single(harness.Queued).Request.Locale);
    }

    // ── 4. The multi-language question, answered where it is decided ─────────

    [Fact]
    public async Task Recipients_in_one_tenant_share_ONE_dispatch_because_nothing_distinguishes_their_languages()
    {
        /*
         * The decision, made explicit and tested rather than left as a comment.
         *
         * Grouping recipients by language and dispatching per group is the right shape and it is NOT implemented,
         * because the input does not exist: TaskNotificationRecipient carries id, e-mail and display name, and the
         * AuthService User entity behind it has no Locale/Language/Culture field at all. Grouping on data nobody
         * supplies produces exactly one group every time — the same single dispatch as here, plus a fan-out loop
         * that is never exercised and rots.
         *
         * So: one dispatch, in the tenant's language. The seven seeded template languages are not wasted by this —
         * the tenant chooses which of them is used. What is still missing is a per-reader choice.
         */
        var harness = new Harness(
            tenantSettingsLanguage: "tr",
            recipients: [("ayse@diten.com", "Ayşe"), ("jean@diten.com", "Jean"), ("li@diten.com", "Li")]);

        await harness.NotifyAssignedAsync();

        var queued = Assert.Single(harness.Queued);
        Assert.Equal(3, queued.Request.To.Count);
        Assert.Equal("tr", queued.Request.Locale);
    }

    [Fact]
    public void The_recipient_contract_carries_no_language_which_is_WHY_grouping_is_absent()
    {
        /*
         * The structural half of the decision above. Stated as an assertion so that adding a language to
         * TaskNotificationRecipient is a visible, deliberate change that fails here and forces the grouping
         * question to be answered rather than a field being added and quietly ignored.
         */
        var carried = typeof(TaskNotificationRecipient)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(["UserId", "Email", "DisplayName"], carried);
    }

    // ── 5. Where the seven languages actually pay off ────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public async Task Each_supported_tenant_language_reaches_the_command_as_a_locale_that_HAS_a_seeded_template(
        string language)
    {
        /*
         * Ties the two halves together. TaskNotificationEndToEndTests proves a template exists for each of these
         * seven; this proves the locale that arrives at the lookup is one of them. Either half alone permits the
         * silent 404: a template nobody asks for, or a locale nobody seeded.
         */
        var harness = new Harness(tenantSettingsLanguage: language);

        await harness.NotifyAssignedAsync();

        Assert.Equal(language, Assert.Single(harness.Queued).Request.Locale);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Production types the whole way down, doubles only at the two edges (tenant registry, terminal handler).
    /// </summary>
    private sealed class Harness
    {
        private readonly TaskItem _task;
        private readonly TaskNotificationService _service;

        public Harness(
            string? tenantSettingsLanguage = null,
            string? tenantDefaultLanguage = null,
            bool tenantRegistryThrows = false,
            string recipientEmail = "ayse@diten.com",
            IReadOnlyList<(string Email, string Name)>? recipients = null,
            Guid? tenantId = null)
        {
            var tenant = tenantId ?? TaskTestData.Tenant;
            TenantId = tenant;
            var people = recipients ?? [(recipientEmail, "Ayşe")];

            var registry = new FakeTenantRegistry(tenantRegistryThrows)
            {
                Tenant = new Tenant
                {
                    Id = tenant,
                    Name = "Diten",
                    Code = "DITEN",
                    Slug = "diten",
                    DisplayName = "Diten",
                    Domain = "diten.local",
                    DefaultLanguage = tenantDefaultLanguage ?? "en",
                    Settings = new TenantSettings { Language = tenantSettingsLanguage ?? "en" }
                }
            };

            var localeResolver = new TenantNotificationLocaleResolver(
                registry,
                NullLogger<TenantNotificationLocaleResolver>.Instance);

            Mediator = new RealValidationMediator();

            Adapter = new NotificationEventDispatchAdapter(
                new ManifestEventDefinitionRepository(),
                Mediator,
                localeResolver,
                NullLogger<NotificationEventDispatchAdapter>.Instance);

            var resolver = new FixedRecipients(people);

            _service = new TaskNotificationService(
                Adapter,
                localeResolver,
                resolver,
                new FakePositionAssignmentRepository(),
                new FakeUserNotificationRepository(),
                new FakeTenantContext(tenant),
                NullLogger<TaskNotificationService>.Instance);

            _task = new TaskItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                Title = "Locale'i olan görev",
                Lifecycle = TaskLifecycle.InProgress,
                AssignmentTarget = TaskAssignmentTarget.Person,
                OrganizationUnitId = Guid.NewGuid(),
                EmailNotificationsEnabled = true,
                DueAt = DateTimeOffset.UtcNow.AddDays(2),
                Version = 1
            };

            Recipients = resolver;
        }

        public Guid TenantId { get; }

        public NotificationEventDispatchAdapter Adapter { get; }

        public RealValidationMediator Mediator { get; }

        public FixedRecipients Recipients { get; }

        public IReadOnlyList<QueueEmailNotificationCommand> Queued => Mediator.Queued;

        public Task<TaskNotificationOutcome> NotifyAssignedAsync()
            => NotifyAsync(TaskNotificationEvents.Assigned);

        public Task<TaskNotificationOutcome> NotifyAsync(string eventCode)
            => _service.NotifyAsync(_task, eventCode, Recipients.KnownIds, Guid.NewGuid(), CancellationToken.None);

        /// <summary>
        /// Skips the task service to exercise the adapter's own locale handling — used where the point is what an
        /// arbitrary producer supplies, not what MOD-0024 supplies.
        /// </summary>
        public Task<Response<NotificationDispatchDto>> DispatchDirectlyAsync(string? locale)
            => Adapter.DispatchByEventCodeAsync(
                new NotificationEventDispatchRequest(
                    TenantId: TenantId,
                    EventCode: TaskNotificationEvents.Assigned,
                    To: [new EmailRecipientDto("ayse@diten.com", "Ayşe")],
                    Variables: new Dictionary<string, object?>
                    {
                        ["TaskTitle"] = "Locale'i olan görev",
                        ["TaskId"] = Guid.NewGuid().ToString()
                    },
                    Locale: locale),
                CancellationToken.None);
    }

    /// <summary>
    /// The real <see cref="ValidationBehavior{TRequest,TResponse}"/> wrapped around the real
    /// <see cref="QueueEmailNotificationValidator"/>, with a recording terminal handler.
    ///
    /// <para>Nothing about the validation is simulated: the same behaviour class the API pipeline registers, running
    /// the same validator instance FluentValidation discovers, over the command the adapter actually built. The
    /// terminal handler is a double only because the real one needs a template repository, renderer, provider
    /// resolver and event bus — none of which were ever reached, which is the whole point.</para>
    /// </summary>
    internal sealed class RealValidationMediator : IMediator
    {
        private readonly List<QueueEmailNotificationCommand> _queued = [];

        public IReadOnlyList<QueueEmailNotificationCommand> Queued => _queued;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is QueueEmailNotificationCommand command)
            {
                var behavior = new ValidationBehavior<QueueEmailNotificationCommand, Response<NotificationDispatchDto>>(
                    [new QueueEmailNotificationValidator()]);

                var response = await behavior.Handle(
                    command,
                    () =>
                    {
                        _queued.Add(command);
                        return Task.FromResult(Response<NotificationDispatchDto>.Success(202));
                    },
                    ct);

                return (TResponse)(object)response;
            }

            throw new NotSupportedException($"Unexpected request {request.GetType().Name}.");
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    /// <summary>
    /// Event definitions read from MOD-0024's OWN manifest, not re-declared here. A hand-written definition would
    /// pass while the module shipped a manifest the catalogue rejects.
    /// </summary>
    private sealed class ManifestEventDefinitionRepository : INotificationEventDefinitionRepository
    {
        private readonly Dictionary<string, NotificationEventDefinition> _byCode;

        public ManifestEventDefinitionRepository()
        {
            var manifest = new TaskManifestProvider().GetManifest();
            _byCode = (manifest.NotificationEvents ?? [])
                .ToDictionary(
                    e => e.EventCode,
                    e => new NotificationEventDefinition
                    {
                        Id = Guid.NewGuid(),
                        EventCode = e.EventCode,
                        OwnerDomain = manifest.Domain,
                        OwnerModuleId = manifest.ModuleCode,
                        OwnerService = manifest.Service,
                        Channel = NotificationChannelCode.Email,
                        DefaultTemplateKey = e.DefaultTemplateKey,
                        Status = NotificationEventStatus.Active,
                        RequiredVariables = (e.RequiredVariables ?? [])
                            .Select(v => new TemplateVariableDefinition { Name = v.Name, IsRequired = true })
                            .ToList(),
                        OptionalVariables = (e.OptionalVariables ?? [])
                            .Select(v => new TemplateVariableDefinition { Name = v.Name, IsRequired = false })
                            .ToList()
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default)
            => Task.FromResult(_byCode.TryGetValue(eventCode, out var definition) ? definition : null);

        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition definition, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(NotificationEventDefinition definition, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<NotificationEventDefinition?>(null);

        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(
            string? ownerModuleId = null,
            NotificationChannelCode? channel = null,
            NotificationEventStatus? status = null,
            bool? canTenantOverride = null,
            NotificationEventUsageType? usageType = null,
            int skip = 0,
            int take = 50,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(_byCode.Values.ToList());

        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(
                _byCode.Values.Where(d => d.Status == NotificationEventStatus.Active).ToList());

        public Task<long> CountAsync(
            string? ownerModuleId = null,
            NotificationChannelCode? channel = null,
            NotificationEventStatus? status = null,
            bool? canTenantOverride = null,
            NotificationEventUsageType? usageType = null,
            CancellationToken ct = default)
            => Task.FromResult<long>(_byCode.Count);
    }

    private sealed class FakeTenantRegistry(bool throws) : ITenantRegistryRepository
    {
        public Tenant? Tenant { get; set; }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throws
                ? throw new InvalidOperationException("Tenant registry is unreachable.")
                : Task.FromResult(Tenant);

        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Tenant>, long)>(([], 0));
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    internal sealed class FixedRecipients : ITaskNotificationRecipientResolver
    {
        private readonly List<TaskNotificationRecipient> _people;

        public FixedRecipients(IReadOnlyList<(string Email, string Name)> people)
            => _people = people
                .Select(p => new TaskNotificationRecipient(Guid.NewGuid(), p.Email, p.Name))
                .ToList();

        public IReadOnlyCollection<Guid> KnownIds => _people.Select(p => p.UserId).ToList();

        public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(
            IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(
                _people.Where(p => userIds.Contains(p.UserId)).ToList());
    }
}
