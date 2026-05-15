using System.Reflection;
using System.Text;
using Diten.Platform.API.Controllers.Platform;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.Audit.Commands;
using Diten.Platform.Application.Features.Audit.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Audit.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Audit.Queries;
using Diten.Platform.Application.Features.Audit.Services;
using Diten.Platform.Application.Features.Audit.Validators;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.Audit;

public sealed class AuditPhase5ApiSurfaceTests
{
    [Fact]
    public async Task Export_ShouldDefensivelyRedactSensitiveStateAndMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new FakeAuditEventRepository(CreateAuditEvent(now));
        var metaAudit = new CapturingMetaAuditWriter();
        var handler = new ExportAuditEventsHandler(repository, CreateRedactor(), metaAudit);

        var response = await handler.Handle(new ExportAuditEventsQuery(new AuditExportRequest
        {
            Format = AuditExportFormats.Json,
            FromUtc = now.AddDays(-1),
            ToUtc = now,
            Limit = 100
        }), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(response.Data);
        var json = Encoding.UTF8.GetString(response.Data!.Content);
        Assert.DoesNotContain("raw-password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-api-key", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-token", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
        Assert.Equal("application/json", response.Data.ContentType);
        Assert.Single(metaAudit.Requests);
        Assert.Equal(AuditOperation.Export, metaAudit.Requests[0].Operation);
    }

    [Fact]
    public async Task Export_ShouldRejectDateRangesLongerThan365Days()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new FakeAuditEventRepository();
        var handler = new ExportAuditEventsHandler(repository, CreateRedactor(), new CapturingMetaAuditWriter());

        var response = await handler.Handle(new ExportAuditEventsQuery(new AuditExportRequest
        {
            Format = AuditExportFormats.Csv,
            FromUtc = now.AddDays(-(AuditExportLimits.MaxDays + 1)),
            ToUtc = now,
            Limit = 100
        }), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(repository.SearchRequests);
    }

    [Fact]
    public async Task RedactActor_ShouldCallRepositoryWithoutDeletingEventsAndMetaAuditOperation()
    {
        var actorId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repository = new FakeAuditEventRepository { RedactionResult = 3 };
        var metaAudit = new CapturingMetaAuditWriter();
        var handler = new RedactAuditActorHandler(
            repository,
            metaAudit,
            new TestCurrentUserContext { UserId = userId });

        var response = await handler.Handle(new RedactAuditActorCommand(new RedactAuditActorRequest
        {
            ActorId = actorId,
            Reason = "DSAR actor erasure request"
        }), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(3, response.Data!.RedactedEventCount);
        Assert.NotNull(repository.RedactionRequest);
        Assert.Equal(actorId, repository.RedactionRequest!.ActorId);
        Assert.Equal(userId, repository.RedactionRequest.RedactedByActorId);
        Assert.Empty(repository.DeletedIds);
        Assert.Single(metaAudit.Requests);
        Assert.Equal(AuditOperation.Redact, metaAudit.Requests[0].Operation);
        Assert.Equal(AuditCategory.DataPrivacy, metaAudit.Requests[0].Category);
    }

    [Fact]
    public void RetentionValidator_ShouldRejectZeroRetention()
    {
        var validator = new UpdateAuditRetentionValidator();

        var result = validator.Validate(new UpdateAuditRetentionCommand(new UpdateAuditRetentionRequest
        {
            Category = AuditCategory.Security.ToString(),
            PlanTierCode = AuditEventRetentionPolicy.DefaultPlanTierCode,
            MinimumRetentionDays = 0,
            DefaultRetentionDays = 30,
            MaximumRetentionDays = 365,
            HotStorageDays = 30
        }));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("greater than zero", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlatformAuditController_ShouldExposeOnlyExpectedPermissionMappedEndpoints()
    {
        var controllerType = typeof(PlatformAuditController);

        Assert.Equal("api/platform/audit", controllerType.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("PlatformActor", controllerType.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Equal("Platform.Audit.Read", PermissionFor(nameof(PlatformAuditController.GetEvents)));
        Assert.Equal("Platform.Audit.Read", PermissionFor(nameof(PlatformAuditController.GetEventById)));
        Assert.Equal("Platform.Audit.Export", PermissionFor(nameof(PlatformAuditController.Export)));
        Assert.Equal("Platform.Audit.Retention.Update", PermissionFor(nameof(PlatformAuditController.UpdateRetention)));
        Assert.Equal("Platform.Audit.RedactActor", PermissionFor(nameof(PlatformAuditController.RedactActor)));
    }

    private static string? PermissionFor(string methodName)
    {
        var attribute = typeof(PlatformAuditController)
            .GetMethod(methodName)!
            .GetCustomAttribute<HasPermissionAttribute>();
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(attribute) as string;
    }

    private static SensitiveFieldRedactor CreateRedactor()
    {
        return new SensitiveFieldRedactor(new SensitiveFieldRedactionRegistry());
    }

    private static AuditEvent CreateAuditEvent(DateTimeOffset occurredAtUtc)
    {
        return new AuditEvent
        {
            TenantId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            ActorType = AuditActorType.PlatformAdministrator,
            ActorId = Guid.NewGuid(),
            ActorEmailMasked = "a***@diten.com",
            ActorDisplayNameMasked = "A***n",
            Category = AuditCategory.Security,
            EntityType = "Tenant",
            EntityId = Guid.NewGuid(),
            Operation = AuditOperation.Update,
            Outcome = AuditOutcome.Succeeded,
            BeforeState = new Dictionary<string, object?>
            {
                ["credentials"] = new Dictionary<string, object?>
                {
                    ["password"] = "raw-password",
                    ["apiKey"] = "raw-api-key"
                }
            },
            Metadata = new Dictionary<string, object?>
            {
                ["headers"] = new Dictionary<string, object?>
                {
                    ["authorization"] = "Bearer raw-token"
                }
            },
            OccurredAtUtc = occurredAtUtc,
            SourceService = "Diten.Platform",
            SourceModule = "AuditPhase5Tests"
        };
    }

    private sealed class CapturingMetaAuditWriter : IAuditMetaAuditWriter
    {
        public List<AuditMetaAuditRequest> Requests { get; } = [];

        public Task<AuditAppendResult> WriteAsync(AuditMetaAuditRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(AuditAppendResult.Queued($"meta:{Requests.Count}"));
        }
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; init; } = Guid.NewGuid();
        public string? Email { get; init; } = "admin@diten.com";
        public string? DisplayName { get; init; } = "Platform Admin";
        public string ActorName => Email ?? DisplayName ?? "system";
        public bool IsAuthenticated { get; init; } = true;
    }

    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        private readonly List<AuditEvent> _events;

        public FakeAuditEventRepository(params AuditEvent[] events)
        {
            _events = events.ToList();
        }

        public int RedactionResult { get; init; }
        public List<AuditEventSearchRequest> SearchRequests { get; } = [];
        public List<Guid> DeletedIds { get; } = [];
        public AuditActorPiiRedactionRequest? RedactionRequest { get; private set; }

        public Task AppendAsync(AuditEvent auditEvent, CancellationToken ct = default)
        {
            _events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<AuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_events.FirstOrDefault(item => item.Id == id));
        }

        public Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_events.FirstOrDefault(item => item.TenantId == tenantId && item.Id == id));
        }

        public Task<AuditEvent?> GetByIdForPlatformCrossTenantAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(_events.FirstOrDefault(item => item.Id == id));
        }

        public Task<AuditEventSearchResult> SearchForPlatformCrossTenantAsync(
            AuditEventSearchRequest request,
            CancellationToken ct = default)
        {
            SearchRequests.Add(request);
            var filtered = _events
                .Where(item => !request.FromUtc.HasValue || item.OccurredAtUtc >= request.FromUtc.Value)
                .Where(item => !request.ToUtc.HasValue || item.OccurredAtUtc <= request.ToUtc.Value)
                .Take(request.Take)
                .ToList();

            return Task.FromResult(new AuditEventSearchResult(filtered, filtered.Count));
        }

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken ct = default)
        {
            var items = _events.Where(item => item.CorrelationId == correlationId).ToList();
            return Task.FromResult(items as IReadOnlyList<AuditEvent>);
        }

        public Task<IReadOnlyList<AuditEvent>> GetByCorrelationIdForPlatformCrossTenantAsync(Guid correlationId, CancellationToken ct = default)
        {
            return GetByCorrelationIdAsync(correlationId, ct);
        }

        public Task<int> RedactActorPiiForPlatformCrossTenantAsync(
            AuditActorPiiRedactionRequest request,
            CancellationToken ct = default)
        {
            RedactionRequest = request;
            return Task.FromResult(RedactionResult);
        }
    }
}
