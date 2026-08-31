using System.Net;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Application.Features.Programs;
using Diten.PpmService.Application.Features.Projects;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Infrastructure.Audit;
using Diten.PpmService.Infrastructure.Correlation;
using Diten.PpmService.Infrastructure.Entitlements;
using Diten.Shared.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class PpmEntitlementAuthorizationTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Access_order_is_tenant_then_entitlement_then_exact_permission()
    {
        var calls = new List<string>();
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            new Entitlement(true, calls),
            new Permission(true, calls));

        var result = await authorizer.AuthorizeAsync(PpmPermissions.PortfoliosRead, default);

        Assert.Equal(PpmAccessDecision.Allowed, result);
        Assert.Equal(["entitlement", "permission"], calls);
    }

    [Fact]
    public async Task Entitlement_deny_returns_403_decision_without_permission_evaluation()
    {
        var calls = new List<string>();
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            new Entitlement(false, calls),
            new Permission(true, calls));

        var result = await authorizer.AuthorizeAsync(PpmPermissions.PortfoliosRead, default);

        Assert.Equal(PpmAccessDecision.Forbidden, result);
        Assert.Equal(["entitlement"], calls);
    }

    [Fact]
    public async Task Invalid_tenant_fails_before_entitlement_and_permission()
    {
        var calls = new List<string>();
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(Guid.Empty),
            new ActorContext(Guid.NewGuid()),
            new Entitlement(true, calls),
            new Permission(true, calls));

        Assert.Equal(
            PpmAccessDecision.Forbidden,
            await authorizer.AuthorizeAsync(PpmPermissions.PortfoliosRead, default));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Provider_failure_maps_only_to_dependency_unavailable()
    {
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            new ThrowingEntitlement(),
            new Permission(true, []));

        var result = await authorizer.AuthorizeAsync(PpmPermissions.PortfoliosRead, default);

        Assert.Equal(PpmAccessDecision.DependencyUnavailable, result);
        Assert.Equal(503, result.Failure<object>().StatusCode);
    }

    [Fact]
    public void All_four_aggregate_services_depend_on_the_single_access_seam()
    {
        var serviceTypes = new[]
        {
            typeof(PortfolioService), typeof(InitiativeService),
            typeof(ProgramService), typeof(ProjectService)
        };

        foreach (var serviceType in serviceTypes)
        {
            var parameters = Assert.Single(serviceType.GetConstructors()).GetParameters();
            Assert.Contains(parameters, p => p.ParameterType == typeof(IPpmAccessAuthorizer));
            Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(IEffectivePermissionEvaluator));
            Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(IPpmEntitlementDecisionClient));
        }
    }

    [Fact]
    public async Task Client_sends_dedicated_key_and_correlation_and_accepts_exact_contract()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, ValidJson()));
        var client = Client(handler, enabled: true);

        Assert.True(await client.IsAllowedAsync(TenantId, default));
        Assert.Equal("dedicated-ppm-service-key-123", handler.Request!.Headers
            .GetValues(PpmEntitlementDecisionClient.ServiceCredentialHeader).Single());
        Assert.Equal("55555555-5555-5555-5555-555555555555", handler.Request.Headers
            .GetValues(PpmEntitlementDecisionClient.CorrelationIdHeader).Single());
        Assert.EndsWith(
            $"/api/internal/ppm/tenants/{TenantId:D}/entitlement-decision",
            handler.Request.RequestUri!.AbsoluteUri,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("55555555-5555-5555-5555-555555555555")]
    [InlineData("55555555555555555555555555555555")]
    [InlineData("malformed-client-value")]
    public async Task One_scoped_correlation_flows_through_entitlement_mutation_and_dispatch(
        string incoming)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CanonicalCorrelationContext.HeaderName] = incoming;
        var correlation = new CanonicalCorrelationContext(
            new HttpContextAccessor { HttpContext = httpContext });
        var expected = correlation.CorrelationId;
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, ValidJson()));
        var entitlement = Client(handler, enabled: true, correlation);
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            entitlement,
            new Permission(true, []));
        var audit = new CorrelationAuditRepository();
        var service = new PortfolioService(
            new CountingPortfolioRepository(),
            audit,
            new NoOpUnitOfWork(),
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            correlation,
            authorizer);

        var response = await service.Create(
            new("P-CORR", "Correlation", null, null),
            default);
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(
            expected.ToString("D"),
            handler.Request!.Headers
                .GetValues(PpmEntitlementDecisionClient.CorrelationIdHeader)
                .Single());
        Assert.Equal(expected, Assert.IsType<AuditIntent>(audit.Intent).CorrelationId);

        var bus = new CorrelationRecordingEventBus();
        var dispatcher = new PpmAuditIntentDispatcher(
            audit,
            bus,
            Options.Create(new PpmAuditProducerOptions
            {
                Enabled = true,
                WorkerEnabled = true,
                BatchSize = 1
            }),
            NullLogger<PpmAuditIntentDispatcher>.Instance);

        Assert.Equal(1, await dispatcher.DispatchPendingAsync(default));
        Assert.Equal(expected, bus.Options!.CorrelationId);
        Assert.Equal(audit.Intent.Id, bus.Options.EventId);
        if (incoming == "malformed-client-value")
        {
            Assert.NotEqual(
                incoming,
                handler.Request.Headers
                    .GetValues(PpmEntitlementDecisionClient.CorrelationIdHeader)
                    .Single());
        }
    }

    [Fact]
    public async Task Client_preserves_authoritative_deny()
    {
        var handler = new RecordingHandler(_ => Json(
            HttpStatusCode.OK,
            ValidJson(isAllowed: false)));

        Assert.False(await Client(handler, enabled: true).IsAllowedAsync(TenantId, default));
    }

    [Fact]
    public async Task Expired_authoritative_deny_is_403_without_permission_or_repository_access()
    {
        var calls = new List<string>();
        var client = Client(
            new RecordingHandler(_ => Json(
                HttpStatusCode.OK,
                ValidJson(isAllowed: false, expiresAtUtc: "2000-01-01T00:00:00+00:00"))),
            enabled: true);
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            client,
            new Permission(true, calls));
        var repository = new CountingPortfolioRepository();
        var service = new PortfolioService(
            repository,
            new NoOpAuditRepository(),
            new NoOpUnitOfWork(),
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            new CorrelationContext(),
            authorizer);

        var response = await service.Create(
            new("P-1", "Portfolio", null, null),
            default);

        Assert.Equal(403, response.StatusCode);
        Assert.Empty(calls);
        Assert.Equal(0, repository.TotalCalls);
    }

    [Fact]
    public async Task Expired_authoritative_allow_is_dependency_unavailable_503()
    {
        var client = Client(
            new RecordingHandler(_ => Json(
                HttpStatusCode.OK,
                ValidJson(isAllowed: true, expiresAtUtc: "2000-01-01T00:00:00+00:00"))),
            enabled: true);
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            client,
            new Permission(true, []));

        var result = await authorizer.AuthorizeAsync(PpmPermissions.PortfoliosRead, default);

        Assert.Equal(PpmAccessDecision.DependencyUnavailable, result);
        Assert.Equal(503, result.Failure<object>().StatusCode);
    }

    [Fact]
    public async Task Authoritative_deny_with_null_expiry_is_403()
    {
        var client = Client(
            new RecordingHandler(_ => Json(
                HttpStatusCode.OK,
                ValidJson(isAllowed: false, expiresAtUtc: null))),
            enabled: true);
        var authorizer = new PpmAccessAuthorizer(
            new TenantContext(TenantId),
            new ActorContext(Guid.NewGuid()),
            client,
            new Permission(true, []));

        Assert.Equal(
            PpmAccessDecision.Forbidden,
            await authorizer.AuthorizeAsync(PpmPermissions.PortfoliosRead, default));
    }

    [Fact]
    public async Task Exact_4096_byte_contract_is_accepted()
    {
        var json = PadToByteLength(ValidJson(), 4096);
        Assert.Equal(4096, Encoding.UTF8.GetByteCount(json));

        Assert.True(await Client(
            new RecordingHandler(_ => Json(HttpStatusCode.OK, json)),
            enabled: true).IsAllowedAsync(TenantId, default));
    }

    [Fact]
    public async Task Response_larger_than_4096_bytes_is_rejected_without_full_buffering()
    {
        var json = PadToByteLength(ValidJson(), 4097);
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(() =>
            Client(
                new RecordingHandler(_ => Json(HttpStatusCode.OK, json)),
                enabled: true).IsAllowedAsync(TenantId, default));
    }

    [Fact]
    public async Task Unknown_length_chunked_oversized_response_is_rejected_at_4097_bytes()
    {
        var bytes = Encoding.UTF8.GetBytes(PadToByteLength(ValidJson(), 8192));
        var content = new StreamContent(new ChunkedReadStream(bytes, 17));
        Assert.Null(content.Headers.ContentLength);
        var client = Client(
            new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            }),
            enabled: true);

        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(
            () => client.IsAllowedAsync(TenantId, default));
    }

    [Fact]
    public async Task Caller_cancellation_during_bounded_stream_read_is_propagated()
    {
        var client = Client(
            new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CancellationOnlyStream())
            }),
            enabled: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.IsAllowedAsync(TenantId, cancellation.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Non_success_provider_status_is_dependency_unavailable(HttpStatusCode status)
    {
        var client = Client(new RecordingHandler(_ => new HttpResponseMessage(status)), enabled: true);
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(
            () => client.IsAllowedAsync(TenantId, default));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"tenantId":"11111111-1111-1111-1111-111111111111"}""")]
    [InlineData("""{"tenantId":"11111111-1111-1111-1111-111111111111","moduleCode":"PPM","isAllowed":true,"resolvedAtUtc":"2026-07-30T10:00:00+00:00","expiresAtUtc":null,"extra":1}""")]
    public async Task Malformed_or_non_exact_response_is_dependency_unavailable(string json)
    {
        var client = Client(
            new RecordingHandler(_ => Json(HttpStatusCode.OK, json)),
            enabled: true);
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(
            () => client.IsAllowedAsync(TenantId, default));
    }

    [Fact]
    public async Task Tenant_or_module_mismatch_is_dependency_unavailable()
    {
        var wrongTenant = ValidJson().Replace(TenantId.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal);
        var wrongModule = ValidJson().Replace("\"PPM\"", "\"MDM\"", StringComparison.Ordinal);

        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(() =>
            Client(new RecordingHandler(_ => Json(HttpStatusCode.OK, wrongTenant)), true)
                .IsAllowedAsync(TenantId, default));
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(() =>
            Client(new RecordingHandler(_ => Json(HttpStatusCode.OK, wrongModule)), true)
                .IsAllowedAsync(TenantId, default));
    }

    [Fact]
    public async Task Disabled_or_missing_configuration_never_calls_provider()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("must not call"));
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(
            () => Client(handler, enabled: false).IsAllowedAsync(TenantId, default));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Connection_and_timeout_fail_closed_but_caller_cancellation_is_preserved()
    {
        var connection = Client(new ThrowingHandler(new HttpRequestException("offline")), true);
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(
            () => connection.IsAllowedAsync(TenantId, default));

        var timeout = Client(new ThrowingHandler(new OperationCanceledException("timeout")), true);
        await Assert.ThrowsAsync<PpmEntitlementDependencyException>(
            () => timeout.IsAllowedAsync(TenantId, default));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => timeout.IsAllowedAsync(TenantId, cancelled.Token));
    }

    private static PpmEntitlementDecisionClient Client(
        HttpMessageHandler handler,
        bool enabled,
        ICorrelationContext? correlation = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["PpmEntitlementDecision:Enabled"] = enabled.ToString(),
            ["PpmEntitlementDecision:BaseUrl"] = "http://platform.internal",
            ["PpmEntitlementDecision:ServiceCredential"] = "dedicated-ppm-service-key-123",
            ["PpmEntitlementDecision:TimeoutSeconds"] = "5"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var context = new DefaultHttpContext { TraceIdentifier = "trace-fallback" };
        context.Request.Headers[PpmEntitlementDecisionClient.CorrelationIdHeader] = "corr-1";
        return new PpmEntitlementDecisionClient(
            new HttpClient(handler),
            configuration,
            correlation ?? new CorrelationContext());
    }

    private static string ValidJson(
        bool isAllowed = true,
        string? expiresAtUtc = "2099-07-30T10:05:00+00:00")
    {
        var expiry = expiresAtUtc is null ? "null" : $"\"{expiresAtUtc}\"";
        return $$"""{"tenantId":"{{TenantId:D}}","moduleCode":"PPM","isAllowed":{{isAllowed.ToString().ToLowerInvariant()}},"resolvedAtUtc":"2026-07-30T10:00:00+00:00","expiresAtUtc":{{expiry}}}""";
    }

    private static string PadToByteLength(string json, int byteLength)
    {
        var currentLength = Encoding.UTF8.GetByteCount(json);
        Assert.True(currentLength <= byteLength);
        return json + new string(' ', byteLength - currentLength);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record TenantContext(Guid TenantId) : ITenantContext;
    private sealed record ActorContext(Guid ActorId) : ICurrentActorContext;

    private sealed class Entitlement(bool allowed, List<string> calls) : IPpmEntitlementDecisionClient
    {
        public Task<bool> IsAllowedAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            calls.Add("entitlement");
            return Task.FromResult(allowed);
        }
    }

    private sealed class ThrowingEntitlement : IPpmEntitlementDecisionClient
    {
        public Task<bool> IsAllowedAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new PpmEntitlementDependencyException("unavailable");
    }

    private sealed class CorrelationContext : ICorrelationContext
    {
        public Guid CorrelationId { get; } = Guid.Parse("55555555-5555-5555-5555-555555555555");
    }

    private sealed class Permission(bool allowed, List<string> calls) : IEffectivePermissionEvaluator
    {
        public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken)
        {
            calls.Add("permission");
            return Task.FromResult(allowed);
        }
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response(request));
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class CountingPortfolioRepository : IPortfolioRepository
    {
        public int TotalCalls { get; private set; }
        public Task<Portfolio?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult<Portfolio?>(null);
        }
        public Task<IReadOnlyList<Portfolio>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult<IReadOnlyList<Portfolio>>([]);
        }
        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludingId, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.FromResult(false);
        }
        public Task AddAsync(Portfolio entity, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.CompletedTask;
        }
        public Task ReplaceAsync(Portfolio entity, int expectedVersion, CancellationToken cancellationToken)
        {
            TotalCalls++;
            return Task.CompletedTask;
        }
        public Task AdvanceInvestmentCaseCollectionFenceAsync(Portfolio entity, CancellationToken cancellationToken)
        { TotalCalls++; entity.AdvanceInvestmentCaseCollectionFence(); return Task.CompletedTask; }
    }

    private sealed class NoOpAuditRepository : IAuditIntentRepository
    {
        public Task AddAsync(AuditIntent intent, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class CorrelationAuditRepository : IAuditIntentRepository
    {
        public AuditIntent Intent { get; private set; } = null!;

        public Task AddAsync(AuditIntent intent, CancellationToken cancellationToken)
        {
            Intent = intent;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditIntentDispatchCandidate>> GetDispatchCandidatesAsync(
            int batchSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AuditIntentDispatchCandidate>>(
            [
                new(
                    Intent.Id,
                    Intent.TenantId,
                    Intent.ActorId,
                    Intent.CorrelationId,
                    Intent.EntityType,
                    Intent.EntityId,
                    Intent.Mutation,
                    Intent.OccurredAtUtc,
                    null)
            ]);

        public Task<bool> MarkOutboxEnqueuedAsync(
            Guid intentId,
            DateTime enqueuedAtUtc,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<bool> MarkDispatchQuarantinedAsync(
            Guid intentId,
            string failureCode,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class CorrelationRecordingEventBus : IEventBus
    {
        public EventPublishOptions? Options { get; private set; }

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
            TEvent @event,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent =>
            throw new NotSupportedException();

        public Task<EventEnvelope<TEvent>> PublishAsync<TEvent>(
            TEvent @event,
            EventPublishOptions options,
            CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Options = options;
            return Task.FromResult(new EventEnvelope<TEvent>(
                new EventMetadata(
                    options.EventId!.Value,
                    @event.EventName,
                    @event.EventVersion,
                    options.CorrelationId!.Value,
                    options.CausationId,
                    options.TenantId,
                    options.Producer!,
                    options.OccurredAtUtc!.Value),
                @event));
        }
    }

    private sealed class NoOpUnitOfWork : IPpmUnitOfWork
    {
        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);
    }

    private sealed class ChunkedReadStream(byte[] bytes, int chunkSize) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(Math.Min(count, chunkSize), bytes.Length - _position);
            if (length <= 0) return 0;
            bytes.AsSpan(_position, length).CopyTo(buffer.AsSpan(offset, length));
            _position += length;
            return length;
        }
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(Math.Min(buffer.Length, chunkSize), bytes.Length - _position);
            if (length <= 0) return ValueTask.FromResult(0);
            bytes.AsMemory(_position, length).CopyTo(buffer);
            _position += length;
            return ValueTask.FromResult(length);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CancellationOnlyStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
