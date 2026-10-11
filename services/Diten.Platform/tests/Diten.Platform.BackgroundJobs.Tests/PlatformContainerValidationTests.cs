using Diten.Platform.Tests.Shared;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.Platform.API.Configuration;
using Diten.Platform.API.Security;
using Diten.Platform.Application;
using Diten.Platform.Application.Authorization;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.BackgroundJobs;
using Diten.Platform.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

/// <summary>
/// Does the container this service composes actually build?
///
/// ⚠ WHY THIS EXISTS. Measured 2026-08-31: Platform would not start at all — not slowly, not degraded, not
/// at all. <c>SubscriptionPlanStartupInitializer</c> is an <see cref="IHostedService"/>, and a hosted service
/// is registered as a SINGLETON. It took <c>IMongoDatabase</c> as a constructor argument, and
/// <c>IMongoDatabase</c> is registered SCOPED (Infrastructure/DependencyInjection.cs). The container refused
/// to build and the process died at <c>builder.Build()</c>:
///
///     Cannot consume scoped service 'MongoDB.Driver.IMongoDatabase'
///     from singleton 'Microsoft.Extensions.Hosting.IHostedService'.
///
/// NOT ONE of roughly 1500 passing tests saw it, and no additional test ABOUT SUBSCRIPTION PLANS would have.
/// The check that catches this runs when the CONTAINER IS BUILT, and a unit test never builds the container —
/// it news up the class under test and hands it fakes, which is exactly the step that skips the lifetime
/// rule. The only thing exercising it was a human starting the app by hand.
///
/// Program.cs asks for the check explicitly, and unconditionally — not only in Development:
///
///     builder.Host.UseDefaultServiceProvider((_, options) =>
///     {
///         options.ValidateOnBuild = true;
///         options.ValidateScopes = true;
///     });
///
/// This test performs that same validation, with the same two flags, over the same composition, so the next
/// captive dependency is a red test instead of a service that will not boot.
///
/// ⚠ WHY NOT <c>WebApplicationFactory&lt;Program&gt;</c>, which would cover strictly more. Measured this
/// session: it cannot work here. Program.cs calls <c>AddInfrastructure(builder.Configuration, …)</c> at line
/// 76 and <c>builder.Build()</c> at line 221, and under minimal hosting a factory's
/// <c>ConfigureAppConfiguration</c> is applied during <c>Build()</c> — i.e. AFTER the configuration under
/// test has already been read and acted upon. A WAF-based guard therefore cannot influence what
/// <c>AddInfrastructure</c> does. The cost of the choice made here is worth naming plainly: registrations made
/// in Program.cs itself are NOT covered by this file. The API-layer startup-maintenance registrations have their
/// own production-helper test in <c>PlatformApiStartupExecutionModeTests</c>. Everything <c>AddApplication</c>
/// and <c>AddInfrastructure</c> register is covered here.
///
/// ⚠ WHY IT NEEDS A REAL MONGODB, which a composition test has no business needing. Measured 2026-08-31:
/// <c>AddInfrastructure</c> does not merely REGISTER things in its default mode. It runs the migration and seed
/// suites inline and then runs the guarded startup initialization chain. The Development-only API-serving mode
/// can now omit those operations explicitly, but this test deliberately composes the DEFAULT mode: it is the
/// regression guard that an argument-free normal startup still builds under the production validation rules.
///
/// Requiring Mongo is the established convention here rather than a new burden — see
/// <c>MongoIntegrationHarness</c>, whose own comment states the position: "Tests built on this harness
/// deliberately have no skip-if-unavailable escape hatch: a missing Mongo is a broken dev environment, and
/// silently skipping is what let the bug ship." This test instead starts a dynamic-loopback, test-owned Mongo
/// process and deletes its whole data directory, so the default maintenance regression never reaches the
/// application's Local Development database.
///
/// ⚠ AND WHY THIS FILE LIVES IN THE BACKGROUND-JOBS TEST PROJECT rather than beside the other Platform
/// tests. <c>AddInfrastructure</c> calls <c>BsonSerializer.RegisterSerializer</c>, which writes to a
/// PROCESS-GLOBAL registry and THROWS if the type is already registered. Diten.Platform.Application.Tests
/// registers those same serializers from a <c>[ModuleInitializer]</c> before its first test runs, so
/// composing there fails with "There is already a serializer registered for type Guid" — red, but about the
/// wrong thing. This project registers none, and hosted-service lifetime is its subject anyway.
///
/// ⚠ TWO COMPOSITIONS IN ONE PROCESS (2026-10-01). The default and the scheduler-off case each call
/// <c>AddInfrastructure</c>, and the second call used to die on that same global registry — "There is already a
/// serializer registered for type Guid" — whichever of the two ran second. <c>AddInfrastructure</c> now registers
/// through <c>PlatformBsonSerializers.EnsureRegistered</c>: a repeat of the SAME serializers is accepted, a
/// different one already in place is still refused (<c>PlatformBsonSerializersTests</c>).
///
/// ⚠ THIS TEST WAS PROVED TO FAIL, not merely observed to pass. With the constructor parameter put back, it
/// reports the production message verbatim — "Cannot consume scoped service 'MongoDB.Driver.IMongoDatabase'
/// from singleton 'Microsoft.Extensions.Hosting.IHostedService'" — and it passes with the parameter removed.
/// A guard that has only ever been green is not known to guard anything.
/// </summary>
public sealed class PlatformContainerValidationTests : IClassFixture<PlatformContainerValidationTests.ContainerFixture>
{
    [Fact]
    public async Task Platform_container_builds_under_the_validation_the_app_boots_with()
        => await ValidateContainerAsync(runStartupMaintenance: true, schedulerEnabled: true);

    [Fact]
    public async Task Api_serving_container_builds_with_scheduler_and_dashboard_disabled()
        => await ValidateContainerAsync(runStartupMaintenance: false, schedulerEnabled: false);

    private static async Task ValidateContainerAsync(bool runStartupMaintenance, bool schedulerEnabled)
    {
        await using var mongo = await OwnedMongoProcess.StartAsync();
        var configuration = TestConfiguration(
            mongo.ConnectionString,
            "diten_platform_itest_container_validation",
            schedulerEnabled);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        services.AddApplication();
        services.AddInfrastructure(
            configuration,
            new ContainerValidationHostEnvironment(),
            runStartupMaintenance: runStartupMaintenance);
        services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
        services.AddScoped<Diten.Platform.Application.Contracts.IActorPermissionContext,
            Diten.Platform.API.Security.ClaimsActorPermissionContext>();
        // BL-521 — the partner tenant scope, through THE registration method Program.cs calls (FIX1 F8-7), not a copy.
        Diten.Platform.API.Security.PlatformActorTenantScopeRegistration.AddPlatformActorTenantScope(services);
        services.Configure<TrustedWorkflowStartAuthorizationOptions>(
            configuration.GetSection(TrustedWorkflowStartAuthorizationOptions.SectionName));
        services.AddSingleton<IOrgDataScopeCandidateAvailabilityClassifier,
            MongoOrgDataScopeCandidateAvailabilityClassifier>();
        services.AddSingleton<ConfiguredTrustedWorkflowStartAuthorizationPolicy>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<TrustedWorkflowStartAuthorizationOptions>,
            TrustedWorkflowStartAuthorizationOptionsValidator>();
        services.AddSingleton<ITrustedWorkflowStartAuthorizationPolicy>(provider =>
            provider.GetRequiredService<ConfiguredTrustedWorkflowStartAuthorizationPolicy>());

        // ⚠ WHY ValidateOnBuild AND NOT JUST ValidateScopes, MEASURED RATHER THAN ASSUMED. Resolving
        // IEnumerable<IHostedService> from the root provider with ValidateScopes alone was tried against the
        // reverted (broken) code, and it DOES catch this particular defect — same message. ValidateOnBuild is
        // kept because it is the wider net: it validates EVERY registered descriptor rather than only the
        // ones reachable from a hosted service, so a captive dependency in an ordinary singleton is caught
        // too. It is also exactly what Program.cs configures, which makes this test and the running service
        // ask the same question rather than two similar ones.
        ServiceProvider? provider = null;
        var failure = Record.Exception(() =>
            provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            }));

        // DisposeAsync, not Dispose: MassTransitHostedService implements only IAsyncDisposable, and the
        // synchronous path throws over it — which would mask the assertion below with an unrelated failure.
        if (provider is not null)
        {
            await provider.DisposeAsync();
        }

        Assert.True(
            failure is null,
            "The Platform DI container does not build. This is the same validation the service performs at "
            + "startup, so the service will not start either.\n\n" + failure);

        Assert.Equal(schedulerEnabled, services.Any(descriptor =>
            descriptor.ServiceType == typeof(IBackgroundJobScheduler)));
        Assert.Equal(schedulerEnabled, services.Any(descriptor =>
            descriptor.ServiceType == typeof(EmailDispatchSweepJob)));
        if (schedulerEnabled)
        {
            Assert.Equal(ServiceLifetime.Scoped, services.Single(descriptor =>
                descriptor.ServiceType == typeof(EmailDispatchSweepJob)).Lifetime);
        }
    }

    [Theory]
    [InlineData(Diten.Platform.Infrastructure.Services.InternalHttpClients.AuthInternal)]
    [InlineData("IUserReferenceValidator")]
    [InlineData("IApprovalRoleDirectory")]
    public async Task The_client_that_carries_the_internal_key_to_AuthService_never_follows_a_redirect(string clientName)
    {
        // BL-454 — measured on the PRODUCTION composition (AddInfrastructure), not on a copy of its registration: if the
        // line in DependencyInjection.cs changes, this goes red. A redirect would hand X-Internal-Api-Key (or the
        // caller's bearer) to another host. The named client serves every factory-built AuthService caller; the two
        // typed clients are registered under their interface names.
        await using var provider = Composition.Value.BuildServiceProvider();
        var factory = provider.GetRequiredService<System.Net.Http.IHttpMessageHandlerFactory>();

        HttpMessageHandler handler = factory.CreateHandler(clientName);
        while (handler is DelegatingHandler delegating && delegating.InnerHandler is not null)
        {
            handler = delegating.InnerHandler;
        }

        var primary = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(primary.AllowAutoRedirect);
    }

    [Fact]
    public async Task The_queue_handler_is_composed_with_the_servers_job_settings_and_the_shared_permanent_failure_path()
    {
        // BL-454 — the handler fails closed without the settings (no retry, no variables kept), so it must be measured
        // that production actually hands them over, and the counter / organizer path with them.
        await using var provider = Composition.Value.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<MediatR.IRequestHandler<
            Diten.Platform.Application.Features.Notifications.Commands.QueueEmailNotificationCommand,
            Diten.Platform.Application.Common.Response<Diten.Platform.Application.Features.Notifications.NotificationDispatchDto>>>();
        var type = handler.GetType();

        var jobOptions = type.GetField("_jobOptions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(handler);
        var effects = type.GetField("_permanentFailure", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(handler);

        Assert.Same(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Diten.BuildingBlocks.BackgroundJobs.BackgroundJobSchedulerOptions>>(), jobOptions);
        Assert.NotNull(effects);
    }

    [Fact]
    public async Task A_transition_command_sent_through_the_production_mediator_is_validated_before_its_handler()
    {
        // BL-454 — the sweep and the retry job send their commands through THIS mediator: every behaviour AddApplication
        // registers and every validator AddValidatorsFromAssembly finds. Round 4's close was refused here in production
        // while its tests, which skipped validation, were green.
        await using var provider = Composition.Value.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
        var tenant = Guid.NewGuid();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => mediator.Send(
            new Diten.Platform.Application.Features.Notifications.Commands.MarkNotificationDispatchFailedCommand(
                tenant, Guid.NewGuid(), "RetryWindowExpired", "The retry window passed before the message could be sent.",
                IsPermanentFailure: true)));

        // The sweep's own message passes validation and reaches the handler, which answers for an unknown dispatch.
        var response = await mediator.Send(
            new Diten.Platform.Application.Features.Notifications.Commands.MarkNotificationDispatchFailedCommand(
                tenant, Guid.NewGuid(), "RetryWindowExpired",
                Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob.ClosingMessage(null),
                IsPermanentFailure: true));
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task The_retry_window_is_read_from_configuration()
    {
        await using var provider = Composition.Value.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<
            Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchRetentionOptions>>().Value;

        Assert.Equal(48, options.RetryWindowHours);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob>());
    }

    [Fact]
    public async Task Both_email_jobs_from_the_production_container_work_each_row_in_its_own_tenant()
    {
        // BL-454 slice 2 — the sweep and the retry job as production composes them, over two tenants' rows at once and
        // in ONE scope (the worst case: a tenant left behind by one row would be read by the next). Each retry must be
        // sent with ITS tenant's sender name and rendered from ITS tenant's template. Only the transport is replaced
        // (nothing leaves the machine) and the scheduler, which records what the sweep enqueues instead of starting
        // Hangfire. The rows, tenants, settings and templates this test writes are its own and are removed at the end.
        var recorder = new RecordingProvider();
        var scheduler = new RecordingScheduler();
        var services = new ServiceCollection();
        foreach (var descriptor in Composition.Value)
        {
            if (descriptor.ServiceType != typeof(Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider)
                && descriptor.ServiceType != typeof(Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler))
            {
                ((ICollection<ServiceDescriptor>)services).Add(descriptor);
            }
        }

        services.AddSingleton<Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider>(recorder);
        services.AddSingleton<Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler>(scheduler);
        await using var provider = services.BuildServiceProvider();

        // BL-454 FIX1 10 — the database is shared by every run of this test: a killed run's tenants are removed first,
        // this run's rows are the most overdue (the sweep's batch is ordered by NextRetryAt) and only they are counted.
        await RemoveOrphansAsync(provider);
        SeededRow? alpha = null, beta = null;
        try
        {
            alpha = await SeedTenantRowAsync(provider, "Alpha");
            beta = await SeedTenantRowAsync(provider, "Beta");
            using var scope = provider.CreateScope();
            var sweep = scope.ServiceProvider.GetRequiredService<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob>();
            await sweep.HandleAsync(
                new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJobArgs(BatchSize: 500),
                new Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext());

            var mine = scheduler.Enqueued
                .Where(args => args.DispatchId == alpha.DispatchId || args.DispatchId == beta.DispatchId)
                .ToList();
            Assert.Equal(2, mine.Count);

            var job = scope.ServiceProvider.GetRequiredService<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchJob>();
            foreach (var args in mine)
            {
                await job.HandleAsync(args, new Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext(TenantId: args.TenantId));
            }

            foreach (var row in new[] { alpha, beta })
            {
                var sent = Assert.Single(recorder.Requests, request => request.DispatchId == row.DispatchId);
                Assert.Equal(row.TenantId, sent.TenantId);
                Assert.Contains(row.SenderName, sent.SenderName);
                Assert.Contains(row.BodyMark, sent.BodyHtml);
                Assert.DoesNotContain(row == alpha ? beta.BodyMark : alpha.BodyMark, sent.BodyHtml);
            }

            Assert.False(scope.ServiceProvider.GetRequiredService<Diten.Platform.Common.Tenancy.ITenantContext>().IsResolved);
        }
        finally
        {
            if (alpha is not null) await RemoveAsync(provider, alpha);
            if (beta is not null) await RemoveAsync(provider, beta);
        }
    }

    [Fact]
    public async Task The_resolved_sweep_holds_the_scopes_tenant_context_and_the_shared_permanent_failure_effects()
    {
        // BL-454 FIX1 5b — both are required constructor arguments now; measured on what the container hands over.
        await using var provider = Composition.Value.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var sweep = scope.ServiceProvider.GetRequiredService<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob>();
        var type = sweep.GetType();
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<Diten.Platform.Common.Tenancy.ITenantContext>(),
            type.GetField("_tenantContext", Private)!.GetValue(sweep));
        Assert.IsType<Diten.Platform.Application.Features.Notifications.Services.NotificationPermanentFailureEffects>(
            type.GetField("_permanentFailure", Private)!.GetValue(sweep));
    }

    [Fact]
    public async Task Pending_effects_of_two_tenants_are_re_driven_by_the_production_sweep_each_inside_its_own_tenant()
    {
        // BL-454 FIX1 5b — the failing-provider case: two tenants' meeting mails whose last send failed and whose
        // effects are pending. The production effects write the attendee badge through a TENANT repository, which reads
        // the ambient tenant: the spy records which tenant each read saw. Without the sweep's scope the read throws,
        // the effects fail and the rows stay pending.
        var spy = new SpyTenantContext();
        var services = new ServiceCollection();
        foreach (var descriptor in Composition.Value)
        {
            if (descriptor.ServiceType != typeof(Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider)
                && descriptor.ServiceType != typeof(Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler)
                && descriptor.ServiceType != typeof(Diten.Platform.Common.Tenancy.ITenantContext))
            {
                ((ICollection<ServiceDescriptor>)services).Add(descriptor);
            }
        }

        services.AddSingleton<Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider>(new FailingProvider());
        services.AddSingleton<Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler>(new RecordingScheduler());
        services.AddSingleton<Diten.Platform.Common.Tenancy.ITenantContext>(spy);
        await using var provider = services.BuildServiceProvider();

        await RemoveOrphansAsync(provider);
        SeededRow? alpha = null, beta = null;
        try
        {
            alpha = await SeedTenantRowAsync(provider, "Alpha", pendingMeetingEffects: true);
            beta = await SeedTenantRowAsync(provider, "Beta", pendingMeetingEffects: true);
            spy.Reads.Clear();
            using var scope = provider.CreateScope();
            var sweep = scope.ServiceProvider.GetRequiredService<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJob>();

            await sweep.HandleAsync(
                new Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchSweepJobArgs(BatchSize: 500),
                new Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext());

            var dispatches = scope.ServiceProvider.GetRequiredService<Diten.Platform.Domain.Repositories.INotificationDispatchRepository>();
            foreach (var row in new[] { alpha, beta })
            {
                var stored = (await dispatches.GetByIdForTenantAsync(row.TenantId, row.DispatchId))!;
                Assert.False(Diten.Platform.Domain.Entities.Notifications.NotificationDispatch.IsPermanentFailurePending(stored));
                Assert.Contains(row.TenantId, spy.Reads);
            }

            Assert.False(spy.IsResolved);
        }
        finally
        {
            if (alpha is not null) await RemoveAsync(provider, alpha);
            if (beta is not null) await RemoveAsync(provider, beta);
        }
    }

    /// <summary>Removes what a killed run of these tests left: every tenant under this file's own "s2-" slug prefix.</summary>
    private static async Task RemoveOrphansAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var tenants = await scope.ServiceProvider.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantRegistryRepository>().GetAllAsync();
        foreach (var orphan in tenants.Where(t => t.Slug.StartsWith("s2-", StringComparison.Ordinal)))
        {
            await RemoveAsync(provider, new SeededRow(orphan.Id, Guid.Empty, Guid.Empty, string.Empty, string.Empty));
        }
    }

    /// <summary>The real tenant context, recording the tenant every resolved read saw.</summary>
    private sealed class SpyTenantContext : Diten.Platform.Common.Tenancy.ITenantContext
    {
        private readonly Diten.Platform.Common.Tenancy.TenantContext _inner = new();
        public List<Guid> Reads { get; } = [];

        public Guid TenantId
        {
            get
            {
                var id = _inner.TenantId; // throws when unresolved, as the real one does
                lock (Reads) Reads.Add(id);
                return id;
            }
        }

        public bool IsResolved => _inner.IsResolved;
        public bool IsPlatformContext => _inner.IsPlatformContext;
        public Guid? TargetTenantId => _inner.TargetTenantId;
        public void SetTenant(Guid tenantId) => _inner.SetTenant(tenantId);
        public void SetPlatformContext(Guid targetTenantId) => _inner.SetPlatformContext(targetTenantId);
        public void ClearTenant() => _inner.ClearTenant();
    }

    private sealed class FailingProvider : Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider
    {
        public Diten.Platform.Domain.Enums.MessagingProviderCode ProviderCode => Diten.Platform.Domain.Enums.MessagingProviderCode.Fake;

        public Task<Diten.Platform.Application.Features.Notifications.Services.MessagingProviderResult> SendEmailAsync(
            Diten.Platform.Application.Features.Notifications.Services.MessagingProviderEmailRequest request, CancellationToken ct = default) =>
            Task.FromResult(Diten.Platform.Application.Features.Notifications.Services.MessagingProviderResult.Fail("SMTP_REJECTED", "rejected"));
    }

    /// <summary>
    /// Removes the platform default templates (<c>TenantId == null</c>) of THIS test's database, so the seed writes them
    /// afresh. Refuses — throws, before anything is touched — any database that is not exactly
    /// <see cref="ContainerDatabaseName"/>: a shared test database, a developer's database, production. Tenant templates and
    /// every other collection are never touched.
    /// </summary>
    internal static void ResetPlatformDefaultTemplates(MongoDB.Driver.IMongoDatabase database)
    {
        var name = database.DatabaseNamespace.DatabaseName;
        if (!string.Equals(name, ContainerDatabaseName, StringComparison.Ordinal) || !name.Contains("_itest_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to reset platform default templates in '{name}': only the container test's own database ('{ContainerDatabaseName}') may be reset.");
        }

        // Untyped on purpose: this runs BEFORE AddInfrastructure registers the production serializers (Guid among them),
        // and a typed filter would make the driver register its default Guid serializer first — the composition then
        // refuses to register its own.
        database.GetCollection<MongoDB.Bson.BsonDocument>(Diten.Platform.Infrastructure.Persistence.Schema.PlatformCollections.NotificationTemplates)
            .DeleteMany(new MongoDB.Bson.BsonDocument { ["TenantId"] = MongoDB.Bson.BsonNull.Value, ["IsPlatformDefault"] = true });
    }

    [Theory]
    [InlineData("DitenERP_Dev")]
    [InlineData("diten_platform_itest")]
    [InlineData("diten_platform_itest_container_validation_x")]
    [InlineData("diten_platform")]
    public void Only_the_container_tests_own_database_may_have_its_platform_templates_reset(string name)
    {
        // A client that is never contacted: the guard throws before any command is sent.
        var database = new MongoDB.Driver.MongoClient("mongodb://localhost:1").GetDatabase(name);

        var refused = Assert.Throws<InvalidOperationException>(() => ResetPlatformDefaultTemplates(database));
        Assert.Contains(name, refused.Message);
    }

    public static TheoryData<string> TenantLanguages() => new() { "en", "tr", "fr", "es", "zh", "ar", "ru" };

    [Theory]
    [MemberData(nameof(TenantLanguages))]
    public async Task A_new_tenants_first_administrator_gets_the_set_password_link_from_the_production_container(string language)
    {
        // BL-454 slice 2 stage D — the measured defect: a tenant opened with an initial administrator sent tenant.invite.email
        // with the tenant's name and id only, to an account nobody had created in AuthService. Here, through the production
        // composition: the created event → the consumer → the invitation service → AuthService (only its HTTP answer is
        // faked: a token and an expiry) → the notification pipeline → the transport (recorded). Once per language.
        var recorder = new RecordingProvider();
        var auth = new FakeAuthInvitations();
        var services = new ServiceCollection();
        foreach (var descriptor in Composition.Value)
        {
            if (descriptor.ServiceType != typeof(Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider)
                && descriptor.ServiceType != typeof(Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler))
            {
                ((ICollection<ServiceDescriptor>)services).Add(descriptor);
            }
        }

        services.AddSingleton<Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider>(recorder);
        services.AddSingleton<Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler>(new RecordingScheduler());
        services.AddHttpClient(Diten.Platform.Infrastructure.Services.InternalHttpClients.AuthInternal)
            .ConfigurePrimaryHttpMessageHandler(() => auth);
        await using var provider = services.BuildServiceProvider();

        await RemoveOrphansAsync(provider);
        SeededRow? row = null;
        try
        {
            var adminId = Guid.NewGuid();
            row = await SeedInvitationTenantAsync(provider, language, adminId);
            using (var scope = provider.CreateScope())
            {
                var consumer = ActivatorUtilities.CreateInstance<Diten.Platform.Infrastructure.Eventing.TenantLifecycleNotificationConsumer>(scope.ServiceProvider);
                var message = new Diten.Platform.Application.Contracts.Eventing.EventTransportMessage(
                    Guid.NewGuid(), Diten.Platform.Contracts.Events.TenantCreatedV1.Name, Diten.Platform.Contracts.Events.TenantCreatedV1.Version,
                    Guid.NewGuid(), Guid.NewGuid(), row.TenantId, "Diten.Platform.Tests", DateTimeOffset.UtcNow,
                    System.Text.Json.JsonSerializer.Serialize(new Diten.Platform.Contracts.Events.TenantCreatedV1(
                        row.TenantId, DateTimeOffset.UtcNow, null, null, row.SenderName, language, adminId)));
                await consumer.ConsumeAsync(message);
            }

            var token = Assert.Single(auth.IssuedTokens);
            var sent = Assert.Single(recorder.Requests, request => request.TenantId == row.TenantId);
            var encoded = Uri.EscapeDataString(token);
            Assert.Contains("/account/set-password?email=", sent.BodyText);
            Assert.Contains(encoded, sent.BodyText);                          // the link
            Assert.Contains(encoded, sent.BodyHtml);
            Assert.Contains(auth.ExpiresAt.ToString("yyyy-MM-dd HH:mm"), sent.BodyText); // until when
            Assert.DoesNotContain("TemporaryPassword", sent.BodyHtml, StringComparison.OrdinalIgnoreCase);

            using var check = provider.CreateScope();
            var template = (await check.ServiceProvider.GetRequiredService<Diten.Platform.Domain.Repositories.INotificationTemplateRepository>()
                .GetActiveByKeyAsync(null, true, "tenant.invite.email", language, Diten.Platform.Domain.Enums.NotificationChannelCode.Email))!;
            Assert.Equal("1.2.0", template.SemanticVersion);
            Assert.Contains(template.Shell!.HeadingTemplate!, sent.BodyText);  // in the tenant's language
            Assert.Contains(template.Shell.ActionLabel!, sent.BodyText);

            var dispatch = Assert.Single(await check.ServiceProvider.GetRequiredService<Diten.Platform.Domain.Repositories.INotificationDispatchRepository>()
                .ListByTenantAsync(row.TenantId, take: 10));
            Assert.Equal(Diten.Platform.Domain.Enums.NotificationDispatchStatus.Sent, dispatch.Status);
            Assert.DoesNotContain(token, dispatch.VariablesJson);             // the row never holds the token
            Assert.DoesNotContain(token, dispatch.BodyHtmlPreview ?? string.Empty);
            Assert.DoesNotContain(token, dispatch.BodyTextPreview ?? string.Empty);
        }
        finally
        {
            if (row is not null) await RemoveAsync(provider, row);
        }
    }

    private static async Task<SeededRow> SeedInvitationTenantAsync(IServiceProvider provider, string language, Guid adminId)
    {
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var tenantId = Guid.NewGuid();
        var displayName = "Stage D " + language + " " + suffix;
        var tenant = new Diten.Platform.Domain.Entities.Tenant
        {
            Id = tenantId, Code = "S2" + suffix.ToUpperInvariant(), Slug = "s2-" + suffix, Name = "s2-" + suffix,
            DisplayName = displayName, Domain = "s2-" + suffix + ".test", Region = "EU", Environment = "Production",
            DefaultLanguage = language
        };
        tenant.AdminUsers.Add(new Diten.Platform.Domain.Entities.TenantAdminUser
        {
            Id = adminId, Name = "First Admin", Email = "first.admin@s2.test",
            Status = Diten.Platform.Domain.Entities.TenantAdminUserStatus.Invited
        });
        await sp.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantRegistryRepository>().CreateAsync(tenant);
        await sp.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantMessagingSettingsRepository>().CreateAsync(
            new Diten.Platform.Domain.Entities.Notifications.TenantMessagingSettings
            {
                TenantId = tenantId,
                IsPlatformDefault = false,
                ProviderCode = Diten.Platform.Domain.Enums.MessagingProviderCode.Fake,
                SenderEmail = "bildirim@s2.test",
                SenderName = displayName,
                IsEnabled = true
            });
        return new SeededRow(tenantId, Guid.Empty, Guid.Empty, displayName, string.Empty);
    }

    /// <summary>AuthService's tenant-admin-invited door, answered: a fresh token and its expiry — never a password.</summary>
    private sealed class FakeAuthInvitations : HttpMessageHandler
    {
        public List<string> IssuedTokens { get; } = [];
        public DateTime ExpiresAt { get; } = new(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // BL-454 stage D FIX2 (3) — the tenant-created event reaches AuthService through its own create-only door.
            Assert.EndsWith("/internal/events/tenant-admin-created", request.RequestUri!.AbsolutePath);
            var token = "stage-d-" + Guid.NewGuid().ToString("N");
            lock (IssuedTokens)
            {
                IssuedTokens.Add(token);
            }

            var body = System.Text.Json.JsonSerializer.Serialize(new { userProvisioned = true, setupToken = token, setupExpiresAtUtc = ExpiresAt, message = "processed" });
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed record SeededRow(Guid TenantId, Guid DispatchId, Guid TemplateId, string SenderName, string BodyMark);

    private static async Task<SeededRow> SeedTenantRowAsync(IServiceProvider provider, string name, bool pendingMeetingEffects = false)
    {
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var tenantId = Guid.NewGuid();
        await sp.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantRegistryRepository>().CreateAsync(new Diten.Platform.Domain.Entities.Tenant
        {
            Id = tenantId, Code = "S2" + suffix.ToUpperInvariant(), Slug = "s2-" + suffix, Name = "s2-" + suffix,
            DisplayName = name + " Pharma " + suffix, Domain = "s2-" + suffix + ".test", Region = "EU", Environment = "Production"
        });
        var senderName = name + " Sender " + suffix;
        await sp.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantMessagingSettingsRepository>().CreateAsync(
            new Diten.Platform.Domain.Entities.Notifications.TenantMessagingSettings
            {
                TenantId = tenantId,
                IsPlatformDefault = false,
                ProviderCode = Diten.Platform.Domain.Enums.MessagingProviderCode.Fake,
                SenderEmail = "bildirim@s2.test",
                SenderName = senderName,
                IsEnabled = true
            });
        var bodyMark = name + " body " + suffix;
        var templateKey = "platform.slice2.t" + suffix;
        var template = await sp.GetRequiredService<Diten.Platform.Domain.Repositories.INotificationTemplateRepository>().CreateAsync(
            new Diten.Platform.Domain.Entities.Notifications.NotificationTemplate
            {
                TenantId = tenantId,
                IsPlatformDefault = false,
                TemplateKey = templateKey,
                Channel = Diten.Platform.Domain.Enums.NotificationChannelCode.Email,
                Locale = "en",
                SubjectTemplate = "Subject",
                BodyHtmlTemplate = "<p>" + bodyMark + "</p>",
                BodyTextTemplate = bodyMark,
                Status = Diten.Platform.Domain.Enums.NotificationTemplateStatus.Active,
                SemanticVersion = "1.0.0"
            });
        var dispatch = new Diten.Platform.Domain.Entities.Notifications.NotificationDispatch
        {
            TenantId = tenantId,
            TemplateKey = templateKey,
            TemplateId = template.Id,
            TemplateSemanticVersion = "1.0.0",
            Locale = "en",
            Channel = Diten.Platform.Domain.Enums.NotificationChannelCode.Email,
            ProviderCode = Diten.Platform.Domain.Enums.MessagingProviderCode.Fake,
            Status = Diten.Platform.Domain.Enums.NotificationDispatchStatus.Failed,
            To = [new Diten.Platform.Domain.Entities.Notifications.EmailRecipient { Email = "user@s2.test" }],
            Subject = "Subject",
            BodyHtmlPreview = "<p>stored preview</p>",
            BodyTextPreview = "stored preview",
            VariablesJson = "{}",
            QueuedAt = DateTimeOffset.UtcNow,
            RetryCount = 1,
            NextRetryAt = DateTimeOffset.UtcNow.AddDays(-7) // the most overdue: first in the sweep's batch
        };
        if (pendingMeetingEffects)
        {
            // The last send failed and the publish after it threw: permanent, effects pending, idle for an hour.
            dispatch.TemplateKey = "platform.meetings.invite";
            dispatch.CausationId = Guid.NewGuid();
            dispatch.MeetingAttendeeUserId = Guid.NewGuid();
            dispatch.RetryCount = 5;
            dispatch.NextRetryAt = null;
            dispatch.PermanentlyFailedNotifiedAt = Diten.Platform.Domain.Entities.Notifications.NotificationDispatch.PermanentFailurePending;
            dispatch.UpdatedAt = DateTimeOffset.UtcNow.AddHours(-1);
        }
        await sp.GetRequiredService<Diten.Platform.Domain.Repositories.INotificationDispatchRepository>().CreateAsync(dispatch);
        return new SeededRow(tenantId, dispatch.Id, template.Id, senderName, bodyMark);
    }

    private static async Task RemoveAsync(IServiceProvider provider, SeededRow row)
    {
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<MongoDB.Driver.IMongoDatabase>();
        await database.GetCollection<Diten.Platform.Domain.Entities.Notifications.NotificationDispatch>(
                Diten.Platform.Infrastructure.Persistence.Schema.PlatformCollections.NotificationDispatches)
            .DeleteManyAsync(MongoDB.Driver.Builders<Diten.Platform.Domain.Entities.Notifications.NotificationDispatch>.Filter.Eq(x => x.TenantId, row.TenantId));
        await database.GetCollection<Diten.Platform.Domain.Entities.Notifications.NotificationTemplate>(
                Diten.Platform.Infrastructure.Persistence.Schema.PlatformCollections.NotificationTemplates)
            .DeleteManyAsync(MongoDB.Driver.Builders<Diten.Platform.Domain.Entities.Notifications.NotificationTemplate>.Filter.Eq(x => x.TenantId, (Guid?)row.TenantId));
        await database.GetCollection<Diten.Platform.Domain.Entities.Notifications.TenantMessagingSettings>(
                Diten.Platform.Infrastructure.Persistence.Schema.PlatformCollections.TenantMessagingSettings)
            .DeleteManyAsync(MongoDB.Driver.Builders<Diten.Platform.Domain.Entities.Notifications.TenantMessagingSettings>.Filter.Eq(x => x.TenantId, (Guid?)row.TenantId));
        await scope.ServiceProvider.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantRegistryRepository>().DeleteAsync(row.TenantId);
    }

    private sealed class RecordingProvider : Diten.Platform.Application.Features.Notifications.Services.IMessagingProvider
    {
        public List<Diten.Platform.Application.Features.Notifications.Services.MessagingProviderEmailRequest> Requests { get; } = [];
        public Diten.Platform.Domain.Enums.MessagingProviderCode ProviderCode => Diten.Platform.Domain.Enums.MessagingProviderCode.Fake;

        public Task<Diten.Platform.Application.Features.Notifications.Services.MessagingProviderResult> SendEmailAsync(
            Diten.Platform.Application.Features.Notifications.Services.MessagingProviderEmailRequest request, CancellationToken ct = default)
        {
            lock (Requests)
            {
                Requests.Add(request);
            }

            return Task.FromResult(Diten.Platform.Application.Features.Notifications.Services.MessagingProviderResult.Success("s2-" + request.DispatchId.ToString("N")));
        }
    }

    private sealed class RecordingScheduler : Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobScheduler
    {
        public List<Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchJobArgs> Enqueued { get; } = [];

        public Task<string> EnqueueAsync<TArgs, THandler>(TArgs args, Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobHandler<TArgs>
        {
            if (args is Diten.Platform.Application.Features.Notifications.BackgroundJobs.EmailDispatchJobArgs email)
            {
                Enqueued.Add(email);
            }

            return Task.FromResult("job");
        }

        public Task<string> ScheduleAsync<TArgs, THandler>(TArgs args, DateTimeOffset enqueueAtUtc, Diten.BuildingBlocks.BackgroundJobs.BackgroundJobContext? context = null, CancellationToken cancellationToken = default)
            where THandler : Diten.BuildingBlocks.BackgroundJobs.IBackgroundJobHandler<TArgs> => Task.FromResult("job");

        public Task RegisterRecurringAsync(Diten.BuildingBlocks.BackgroundJobs.RecurringJobRegistration registration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>This test's own database — fixed name, inside the class's own mongod (see <see cref="ContainerFixture"/>).</summary>
    internal const string ContainerDatabaseName = "diten_platform_itest_container_validation";

    /// <summary>
    /// Merge WP-CT-LANE-TKO-MERGE-01 — THE COMPOSITION THE LANE'S TESTS RESOLVE FROM, ON A TEST-OWNED MONGOD. The lane built it
    /// once, lazily, against the shared localhost:27017; the takeover replaced the shared Mongo with a mongod each test owns. Both
    /// kept: one owned mongod for this class (started once, its data directory removed afterwards), and on it the lane's
    /// composition, unchanged.
    /// </summary>
    public sealed class ContainerFixture : IAsyncLifetime
    {
        private OwnedMongoProcess? _mongo;

        public Lazy<IServiceCollection> Composition { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _mongo = await OwnedMongoProcess.StartAsync();
            var configuration = TestConfiguration(_mongo.ConnectionString, ContainerDatabaseName, schedulerEnabled: true);
            Composition = new Lazy<IServiceCollection>(() =>
            {
                // BL-454 slice 2 stage D — the seed below (inside AddInfrastructure) writes the platform default templates, and
                // it rightly never overwrites a row it does not recognise. The test sets up its own state: before the seed runs,
                // this database's platform default templates are removed (on an owned, fresh mongod there are none to remove;
                // kept so the rule holds wherever the database comes from).
                ResetPlatformDefaultTemplates(new MongoDB.Driver.MongoClient(configuration["MongoDbSettings:ConnectionString"])
                    .GetDatabase(configuration["MongoDbSettings:DatabaseName"]));
                var services = new ServiceCollection();

                // What the HOST always supplies, and therefore not a copy of Program.cs: WebApplicationBuilder
                // registers the configuration and the logging services before any AddX of ours runs.
                services.AddLogging();
                services.AddSingleton(configuration);

                services.AddApplication();
                services.AddInfrastructure(configuration, new ContainerValidationHostEnvironment());

                // ⚠ THE TWO THINGS THE WEB LAYER SUPPLIES THAT INFRASTRUCTURE SERVICES DEPEND ON (see the lane's note:
                // IActorPermissionContext is Program.cs's own registration, reproduced; EndpointDataSource is an EMPTY data
                // source rather than AddControllers(), which drags in the whole MVC subsystem).
                services.AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource());
                services.AddScoped<Diten.Platform.Application.Contracts.IActorPermissionContext,
                    Diten.Platform.API.Security.ClaimsActorPermissionContext>();
                // BL-521 — the partner tenant scope, through THE registration method Program.cs calls (FIX1 F8-7), not a copy.
                Diten.Platform.API.Security.PlatformActorTenantScopeRegistration.AddPlatformActorTenantScope(services);
                // …and what the takeover's composition adds from Program.cs (the org data-scope classifier, the trusted
                // workflow start policy): the same set ValidateContainerAsync registers, so both compositions are the app's.
                services.Configure<TrustedWorkflowStartAuthorizationOptions>(
                    configuration.GetSection(TrustedWorkflowStartAuthorizationOptions.SectionName));
                services.AddSingleton<IOrgDataScopeCandidateAvailabilityClassifier,
                    MongoOrgDataScopeCandidateAvailabilityClassifier>();
                services.AddSingleton<ConfiguredTrustedWorkflowStartAuthorizationPolicy>();
                services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<TrustedWorkflowStartAuthorizationOptions>,
                    TrustedWorkflowStartAuthorizationOptionsValidator>();
                services.AddSingleton<ITrustedWorkflowStartAuthorizationPolicy>(provider =>
                    provider.GetRequiredService<ConfiguredTrustedWorkflowStartAuthorizationPolicy>());

                return services;
            });
        }

        public async Task DisposeAsync()
        {
            if (_mongo is not null)
            {
                await _mongo.DisposeAsync();
            }
        }
    }

    private readonly ContainerFixture _fixture;

    public PlatformContainerValidationTests(ContainerFixture fixture) => _fixture = fixture;

    private Lazy<IServiceCollection> Composition => _fixture.Composition;

    /// <summary>
    /// D2-FIX5 item 5 — the REAL composition, read for what the SLA sweep would be given. Until now the only thing standing between
    /// <c>Workflow:Escalation:StandingRest</c> and the sweep was a test that read <c>DependencyInjection.cs</c> as text and looked
    /// for the registration's line: it passed with the line commented out of the method it names, or moved into a method nothing
    /// calls. Here <c>AddInfrastructure</c> itself runs (without the startup maintenance — nothing is written), and the option the
    /// sweep's handler is handed is resolved from the container it built: an operator's six hours arrive as six hours, and with
    /// nothing configured the sweep keeps its day.
    /// </summary>
    [Fact]
    public async Task AddInfrastructure_binds_the_escalation_options_from_configuration()
    {
        await using var mongo = await OwnedMongoProcess.StartAsync();

        var configured = await ResolveEscalationOptionsAsync(
            mongo, new Dictionary<string, string?> { ["Workflow:Escalation:StandingRest"] = "06:00:00" });
        var unset = await ResolveEscalationOptionsAsync(mongo, new Dictionary<string, string?>());

        Assert.Equal(TimeSpan.FromHours(6), configured.StandingRest);
        Assert.Equal(WorkflowEscalationOptions.DefaultStandingRest, unset.StandingRest);
    }

    private static async Task<WorkflowEscalationOptions> ResolveEscalationOptionsAsync(
        OwnedMongoProcess mongo, Dictionary<string, string?> overrides)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(TestConfiguration(mongo.ConnectionString, "diten_platform_itest_escalation_options", schedulerEnabled: false))
            .AddInMemoryCollection(overrides)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration, new ContainerValidationHostEnvironment(), runStartupMaintenance: false);

        await using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<WorkflowEscalationOptions>>().Value;
    }

    [Fact]
    public void AddInfrastructure_defaults_to_running_startup_maintenance()
    {
        var overload = typeof(Diten.Platform.Infrastructure.DependencyInjection).GetMethods()
            .Single(method =>
                method.Name == nameof(Diten.Platform.Infrastructure.DependencyInjection.AddInfrastructure)
                && method.GetParameters().Length == 4);
        var maintenanceParameter = overload.GetParameters()[3];

        Assert.True(maintenanceParameter.HasDefaultValue);
        Assert.True(Assert.IsType<bool>(maintenanceParameter.DefaultValue));
    }

    /// <summary>
    /// The service's OWN configuration files, with only what this test must pin layered on top.
    ///
    /// ⚠ IT READS THE REAL FILES ON PURPOSE. <c>AddInfrastructure</c> throws outright on a missing section —
    /// <c>AuditRetentionSeed</c>, <c>WorkAggregation</c>, <c>MessagingProviders</c> and others — so a
    /// hand-written configuration here would be a second copy of appsettings.json that nobody updates: every
    /// section a future change adds would be missing from it, and this guard would then fail for a
    /// configuration reason having nothing to do with lifetimes — noise that teaches people to ignore it.
    /// Reading the shipped files means this test sees the same configuration surface the service does.
    /// </summary>
    private static IConfiguration TestConfiguration(
        string mongoConnectionString, string databaseName, bool schedulerEnabled) =>
        new ConfigurationBuilder()
            .AddJsonFile(ApiSettingsPath("appsettings.json"), optional: false)
            .AddJsonFile(ApiSettingsPath("appsettings.Development.json"), optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A database inside the dynamic-loopback, test-owned mongod. The whole data directory is
                // removed after the test; the Local Development Mongo service is never contacted.
                ["MongoDbSettings:ConnectionString"] = mongoConnectionString,
                ["MongoDbSettings:DatabaseName"] = databaseName,
                ["MongoDbSettings:AllowStartupWithoutDatabase"] = "true",
                // BL-454 — a value the defaults never produce, to prove the window is read from configuration.
                ["Notifications:EmailDispatch:RetryWindowHours"] = "48",

                // Secrets the infrastructure layer refuses to compose without. Local-only literals: nothing
                // is signed or authenticated with them, because nothing is started.
                ["JwtSettings:Secret"] = "container-validation-only-jwt-signing-secret-0123456789",
                ["JwtSettings:Issuer"] = "diten-platform-tests",
                ["JwtSettings:Audience"] = "diten-platform-tests",
                ["AuthService:BaseUrl"] = "http://localhost:5001",
                ["AuthService:InternalApiKey"] = "container-validation-only-internal-api-key",
                ["ModuleRegistrationCredentials:Mdm:Identifier"] = "ditenmdmservice",
                ["ModuleRegistrationCredentials:Mdm:ActiveSecret"] =
                    "container-validation-only-module-registration-secret",

                ["BackgroundJobs:Enabled"] = schedulerEnabled.ToString(),
                ["BackgroundJobs:DashboardEnabled"] = "false",
                ["Smtp:Enabled"] = "false"
            })
            .Build();

    private sealed class OwnedMongoProcess : IAsyncDisposable
    {
        private readonly Process? _process;
        private readonly string? _root;

        private OwnedMongoProcess(Process process, string root, int port)
        {
            _process = process;
            _root = root;
            ConnectionString = $"mongodb://127.0.0.1:{port}/?directConnection=true";
        }

        // A given lane URI: nothing was started here, so nothing is stopped or removed.
        private OwnedMongoProcess(string laneUri) => ConnectionString = laneUri;

        public string ConnectionString { get; }

        /// <summary>
        /// Main sync (CT decision C): the ONE Platform test Mongo rule (PlatformTestMongo, linked). A lane URI
        /// (DITEN_PLATFORM_TEST_MONGO_URI, main's Q131a check) is used as given — nothing to stop, the database name is
        /// fixed and reused; otherwise a mongod THIS test starts from DITEN_TEST_MONGOD (an empty / relative / missing
        /// value is red); with neither, red by name. Never the application MongoDB, never 27017.
        /// </summary>
        public static async Task<OwnedMongoProcess> StartAsync()
        {
            var choice = PlatformTestMongo.ChooseFromEnvironment();
            if (choice.Failure is { } failure)
            {
                throw new InvalidOperationException(failure);
            }

            if (choice.Uri is { } laneUri)
            {
                return new OwnedMongoProcess(laneUri);
            }

            var binary = choice.Executable!;
            var port = ReservePort();
            var root = Path.Combine(
                Path.GetTempPath(),
                "diten-platform-container-validation-" + Guid.NewGuid().ToString("N"));
            var data = Path.Combine(root, "data");
            Directory.CreateDirectory(data);
            var start = new ProcessStartInfo
            {
                FileName = binary,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var arguments = new List<string>
            {
                "--dbpath", data,
                "--port", port.ToString(),
                "--bind_ip", "127.0.0.1",
                "--quiet"
            };
            if (!OperatingSystem.IsWindows())
            {
                arguments.Add("--nounixsocket");
            }

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            var process = Process.Start(start)
                          ?? throw new InvalidOperationException("Failed to start the test-owned mongod process.");
            var settings = MongoClientSettings.FromConnectionString(
                $"mongodb://127.0.0.1:{port}/?directConnection=true");
            settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(500);
            var client = new MongoClient(settings);
            try
            {
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException(
                            $"Test-owned mongod exited with code {process.ExitCode}.");
                    }

                    try
                    {
                        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                            new BsonDocument("ping", 1));
                        return new OwnedMongoProcess(process, root, port);
                    }
                    catch (Exception exception) when (exception is MongoException or TimeoutException)
                    {
                        await Task.Delay(100);
                    }
                }

                throw new TimeoutException("Test-owned mongod did not become ready.");
            }
            catch
            {
                Stop(process);
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }

                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_process is null || _root is null)
            {
                return ValueTask.CompletedTask;
            }

            Stop(_process);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }

        private static int ReservePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        private static void Stop(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// A settings file inside Diten.Platform.API, found by WALKING UP to the AGENTS.md marker rather than by
    /// counting directories out of the build output. Same reasoning as
    /// Diten.Platform.Application.Tests.RepoPaths: a fixed number of "../" is right in exactly one checkout
    /// shape, and AGENTS.md is a tracked FILE, so it is found in a git worktree too — where <c>.git</c> is a
    /// file rather than a directory and a <c>Directory.Exists</c> probe walks off the top of the filesystem.
    /// </summary>
    private static string ApiSettingsPath(string fileName)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        if (current is null)
        {
            throw new InvalidOperationException(
                $"Repo root not found above '{AppContext.BaseDirectory}' — no AGENTS.md on any parent.");
        }

        return Path.Combine(
            current.FullName, "services", "Diten.Platform", "src", "Diten.Platform.API", fileName);
    }

    private sealed class ContainerValidationHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Diten.Platform.API";
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
