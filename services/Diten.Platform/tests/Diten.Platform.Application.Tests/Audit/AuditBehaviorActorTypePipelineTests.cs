using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Audit;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static Diten.Platform.Application.Tests.Audit.DataExportAuditTestKit;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// BL-409 — WHO MADE AN AUDITED COMMAND, THROUGH THE REAL PIPELINE.
///
/// <para><b>⚠ NOTHING ON THE PATH IS A DOUBLE EXCEPT THE OUTBOX STORE.</b> The MediatR pipeline is the one
/// <c>AddApplication()</c> registers (Validation → Logging → Exception → Audit → Performance, in production order),
/// the audit service is the production <see cref="AuditService"/> with its own validation, the principal is real
/// claims read by the production <see cref="JwtTenantAuthorizationContext"/> and <see cref="CurrentUserContext"/>,
/// and what <c>audit_events</c> would hold is read back through the production
/// <see cref="AuditOutboxPayloadMapper"/>. Only the Mongo outbox collection is in memory. A test that handed the
/// behaviour a double answering "tenant user" would prove the behaviour copies a value, not that it is the token's.</para>
///
/// <para>The mapping itself is guarded once, in <c>DataExportAuditWriterTests</c> and here, against the ONE shared
/// <see cref="AuditActorTypeResolver"/>: breaking it turns both suites red (sabotage S1).</para>
/// </summary>
public sealed class AuditBehaviorActorTypePipelineTests
{
    private static readonly Guid Tenant = Guid.Parse("40940940-0000-4000-8000-000000000001");
    private static readonly Guid User = Guid.Parse("40940940-0000-4000-8000-000000000002");

    // ── A PERSON: THE TOKEN'S ACTOR TYPE ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("tenant_user", AuditActorType.TenantUser, false)]
    [InlineData("TENANT_USER", AuditActorType.TenantUser, false)]
    [InlineData("platform_admin", AuditActorType.PlatformAdministrator, true)]
    [InlineData("partner_admin", AuditActorType.PartnerAdministrator, true)]
    public async Task An_authenticated_principal_is_recorded_with_its_tokens_actor_type_not_System(
        string claim,
        AuditActorType expected,
        bool platformRequest)
    {
        // ⚠ SABOTAGE S1 and S2's GUARD: a resolver that maps wrongly, or a behaviour that ignores it, turns this red.
        await using var pipeline = AuditPipeline.Build(Principal(claim, User, tenantClaim: Tenant), platformRequest);

        var response = await pipeline.SendAsync(new AuditActorProbeCommand(Tenant));

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, pipeline.Calls.Count);

        var request = Assert.Single(pipeline.Audit.Requests);
        Assert.Equal(expected, request.ActorType);
        Assert.Equal(AuditAppendStatus.Queued, Assert.Single(pipeline.Audit.Results).Status);

        var stored = pipeline.StoredEvent();
        Assert.Equal(expected, stored.ActorType);
        Assert.Equal(User, stored.ActorId);
        Assert.Equal(Tenant, stored.TenantId);
        Assert.Equal(nameof(AuditActorProbeCommand), stored.RequestType);
    }

    [Fact]
    public async Task A_failing_command_is_recorded_as_failed_under_the_same_actor()
    {
        // The runtime-exception append is a second call site of the same request builder; it must not fall back.
        await using var pipeline = AuditPipeline.Build(Principal("tenant_user", User, tenantClaim: Tenant), platformRequest: false);

        try
        {
            await pipeline.SendAsync(new AuditActorProbeCommand(Tenant, Throw: true));
        }
        catch (InvalidOperationException)
        {
            // Rethrown by design; what matters here is the record.
        }

        var request = Assert.Single(pipeline.Audit.Requests);
        Assert.Equal(AuditOutcome.Failed, request.Outcome);
        Assert.Equal(AuditActorType.TenantUser, request.ActorType);
        Assert.Equal(AuditActorType.TenantUser, pipeline.StoredEvent().ActorType);
    }

    // ── NO PRINCIPAL: THE SYSTEM, AND ONLY THEN ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_background_job_with_no_http_request_at_all_is_recorded_as_System()
    {
        // A Hangfire job or an event consumer: a scope of its own, and no HttpContext anywhere on the flow.
        var noRequest = new HttpContextAccessor();
        Assert.Null(noRequest.HttpContext);
        await using var pipeline = AuditPipeline.Build(noRequest, platformRequest: false);

        var response = await pipeline.SendAsync(new AuditActorProbeCommand(Tenant));

        Assert.True(response.IsSuccessful);
        Assert.Equal(1, pipeline.Calls.Count);
        Assert.Equal(AuditActorType.System, Assert.Single(pipeline.Audit.Requests).ActorType);
        Assert.Equal(AuditAppendStatus.Queued, Assert.Single(pipeline.Audit.Results).Status);

        var stored = pipeline.StoredEvent();
        Assert.Equal(AuditActorType.System, stored.ActorType);
        Assert.Null(stored.ActorId);
    }

    [Fact]
    public async Task An_unauthenticated_request_is_recorded_as_System_even_when_it_carries_actor_type_claims()
    {
        // The X-Internal-Api-Key controllers are [AllowAnonymous]: an HTTP request with no authenticated principal.
        // Claims on an unauthenticated identity were never validated, so they must not name anybody.
        await using var pipeline = AuditPipeline.Build(
            Principal("platform_admin", User, authenticated: false, tenantClaim: Tenant),
            platformRequest: false);

        var response = await pipeline.SendAsync(new AuditActorProbeCommand(Tenant));

        Assert.True(response.IsSuccessful);
        Assert.Equal(AuditActorType.System, Assert.Single(pipeline.Audit.Requests).ActorType);
        Assert.Equal(AuditActorType.System, pipeline.StoredEvent().ActorType);
    }

    // ── A PRINCIPAL THAT NAMES NOBODY: UNKNOWN, AND THE COMMAND STILL RUNS ──────────────────────────────────

    [Theory]
    [InlineData((string?)null)]
    [InlineData("")]
    [InlineData("service")]
    [InlineData("system")]
    [InlineData("admin")]
    public async Task An_authenticated_principal_without_a_recognised_actor_type_is_Unknown_and_the_command_is_not_blocked(
        string? claim)
    {
        await using var pipeline = AuditPipeline.Build(Principal(claim, User, tenantClaim: Tenant), platformRequest: false);

        var response = await pipeline.SendAsync(new AuditActorProbeCommand(Tenant));

        // The command: ran once and succeeded — BL-409 does not change command availability.
        Assert.True(response.IsSuccessful);
        Assert.Equal(1, pipeline.Calls.Count);

        // The behaviour: asked for an Unknown actor, never guessed System or TenantUser.
        var request = Assert.Single(pipeline.Audit.Requests);
        Assert.Equal(AuditActorType.Unknown, request.ActorType);

        // ⚠ THE CONSEQUENCE, MEASURED ON THE PRODUCTION AUDIT SERVICE: it refuses an Unknown actor
        // ("Audit actor type is required."), so NO record reaches the outbox. That refusal is pre-existing and
        // outside BL-409's scope (the outbox mapper would dead-letter Unknown too); the warning is the only trace.
        Assert.Equal(AuditAppendStatus.Rejected, Assert.Single(pipeline.Audit.Results).Status);
        Assert.Empty(pipeline.Outbox.Writes);
        Assert.Contains(pipeline.Logs.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Category.Contains("AuditBehavior", StringComparison.Ordinal)
            && entry.Message.Contains("BL-409", StringComparison.Ordinal));
    }
}

/// <summary>
/// The production MediatR pipeline around one audited probe command, in a request scope of its own. The only
/// in-memory piece is the outbox store; see <see cref="AuditBehaviorActorTypePipelineTests"/>.
/// </summary>
internal sealed class AuditPipeline : IAsyncDisposable
{
    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    private AuditPipeline(
        ServiceProvider root,
        InMemoryAuditOutbox outbox,
        AuditActorProbeCalls calls,
        CapturingLoggerProvider logs)
    {
        _root = root;
        _scope = root.CreateAsyncScope();
        Outbox = outbox;
        Calls = calls;
        Logs = logs;
    }

    public InMemoryAuditOutbox Outbox { get; }

    public AuditActorProbeCalls Calls { get; }

    public CapturingLoggerProvider Logs { get; }

    /// <summary>The scope's audit service — the very instance the behaviour appended through.</summary>
    public RecordingAuditService Audit => (RecordingAuditService)_scope.ServiceProvider.GetRequiredService<IAuditService>();

    public static AuditPipeline Build(IHttpContextAccessor httpContext, bool platformRequest)
    {
        var outbox = new InMemoryAuditOutbox();
        var calls = new AuditActorProbeCalls();
        var logs = new CapturingLoggerProvider();
        var root = Compose(new ServiceCollection(), httpContext, platformRequest, outbox, calls, logs);
        return new AuditPipeline(root, outbox, calls, logs);
    }

    private static ServiceProvider Compose(
        ServiceCollection services,
        IHttpContextAccessor httpContext,
        bool platformRequest,
        InMemoryAuditOutbox outbox,
        AuditActorProbeCalls calls,
        CapturingLoggerProvider logs)
    {
        services.AddLogging(logging => logging.AddProvider(logs));

        // Production registrations: the MediatR pipeline, AuditBehaviorOptions, AuditService and its collaborators.
        services.AddApplication();

        // Production readers over the request's principal (Infrastructure registers exactly these).
        services.AddSingleton(httpContext);
        services.AddScoped<IDataScopeResolver>(_ => new FakeDataScopeResolver());
        services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        // What TenantResolutionMiddleware (or TenantScope in a job) leaves behind.
        services.AddScoped<ITenantContext>(_ =>
        {
            var tenant = new TenantContext();
            if (platformRequest)
            {
                tenant.SetPlatformContext(Guid.Empty);
            }
            else
            {
                tenant.SetTenant(Guid.Parse("40940940-0000-4000-8000-000000000001"));
            }

            return tenant;
        });

        services.AddSingleton<IAuditOutboxWriter>(outbox);
        services.AddScoped<AuditService>();
        services.AddScoped<IAuditService>(provider => new RecordingAuditService(provider.GetRequiredService<AuditService>()));

        services.AddSingleton(calls);
        services.AddTransient<IRequestHandler<AuditActorProbeCommand, Response<NoContent>>, AuditActorProbeHandler>();

        return services.BuildServiceProvider();
    }

    public Task<Response<NoContent>> SendAsync(AuditActorProbeCommand command)
        => _scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, CancellationToken.None);

    /// <summary>The single outbox write, read by the production mapper exactly as the audit worker reads it.</summary>
    public AuditEvent StoredEvent() => InMemoryAuditOutbox.ToAuditEvent(Assert.Single(Outbox.Writes));

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
    }
}

/// <summary>An audited command with nothing in it but the target tenant — the subject is the pipeline, not a feature.</summary>
internal sealed record AuditActorProbeCommand(Guid TargetTenantId, bool Throw = false)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.Security,
        AuditOperation.Update,
        "AuditActorProbe",
        EntityId: Guid.Parse("40940940-0000-4000-8000-0000000000ee"),
        SourceModule: "BL-409",
        TargetTenantId: TargetTenantId);
}

internal sealed class AuditActorProbeCalls
{
    private int _count;

    public int Count => _count;

    public void Hit() => Interlocked.Increment(ref _count);
}

internal sealed class AuditActorProbeHandler(AuditActorProbeCalls calls)
    : IRequestHandler<AuditActorProbeCommand, Response<NoContent>>
{
    public Task<Response<NoContent>> Handle(AuditActorProbeCommand request, CancellationToken cancellationToken)
    {
        calls.Hit();
        if (request.Throw)
        {
            throw new InvalidOperationException("BL-409 probe failure");
        }

        return Task.FromResult(Response<NoContent>.Success());
    }
}

/// <summary>The audit outbox, in memory: accepts every write and keeps it.</summary>
internal sealed class InMemoryAuditOutbox : IAuditOutboxWriter
{
    private readonly List<AuditOutboxWriteRequest> _writes = [];

    public IReadOnlyList<AuditOutboxWriteRequest> Writes
    {
        get
        {
            lock (_writes)
            {
                return _writes.ToList();
            }
        }
    }

    public Task<bool> TryEnqueueAsync(AuditOutboxWriteRequest request, CancellationToken ct = default)
    {
        lock (_writes)
        {
            _writes.Add(request);
        }

        return Task.FromResult(true);
    }

    public static AuditEvent ToAuditEvent(AuditOutboxWriteRequest write)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuditOutboxPayloadMapper().Map(
            new AuditOutboxProcessingItem(
                Guid.NewGuid(),
                write.TenantId,
                write.CorrelationId,
                write.IdempotencyKey,
                write.RequestType,
                write.Operation,
                write.EntityType,
                write.EntityId,
                write.Payload,
                default,
                0,
                now,
                now),
            now);
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(LogLevel Level, string Category, string Message)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Category, string Message)> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToList();
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (owner._entries)
            {
                owner._entries.Add((logLevel, category, formatter(state, exception)));
            }
        }
    }
}
