using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.ObjectionResponses;
using Diten.CrmService.Application.Features.VisitEvidence;
using Diten.CrmService.Application.Features.VisitExecution;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitReport.V2;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;
using VisitReportFollowUp = Diten.CrmService.Domain.Entities.VisitReportFollowUp;
using VisitReportGeneral = Diten.CrmService.Domain.Entities.VisitReportGeneral;
using VisitReportSafety = Diten.CrmService.Domain.Entities.VisitReportSafety;

namespace Diten.CrmService.Application.Tests.CentralAudit;

/*
 * CRM central audit (b)(c) — THE REAL CRM HOST (Program.cs: AddApplication + AddCentralAudit<CrmCentralAuditScope>, the real
 * MediatR pipeline with CRM's Validation / Logging / ExceptionHandling / Performance behaviors) with only two things swapped:
 * the outbox store (in memory — no Mongo; the Mongo store is the shared package's, measured by its own tests) and the
 * command's handler (a stub that answers what the test asks). What is read back is what the shared recorder wrote:
 *   success                  → exactly one event: caller's tenant (token), user (sub), entity id, Crm / CRM / Diten.CrmService
 *   create                   → the entity id from the response
 *   failed envelope          → none
 *   exception                → none (CRM's ExceptionHandlingBehavior rethrows; the central behavior is innermost)
 *   two tenants              → each event under its own tenant
 *   token ≠ resolved tenant  → none (never filed under a guessed tenant)
 *   unconfigured Platform    → /health/central-audit Degraded (AUD-001 §4.3 "not silent")
 */
public sealed class CrmCentralAuditPipelineTests : IClassFixture<CrmCentralAuditApiFactory>
{
    private readonly CrmCentralAuditApiFactory _factory;

    public CrmCentralAuditPipelineTests(CrmCentralAuditApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_succeeded_command_leaves_one_event_with_the_callers_tenant_user_and_entity()
    {
        var caller = Caller.New();
        var id = Guid.NewGuid();
        _factory.Outcome = _ => Response<bool>.Success(true);

        await _factory.SendAsync(caller, new UpdateObjectionResponseCommand(id, Guid.NewGuid(), "cat", "Secret question?", "Secret answer.", null));

        var row = Assert.Single(_factory.Events(caller.Tenant));
        Assert.Equal(caller.Tenant, row.TenantId);
        Assert.Equal(caller.User, row.ActorId);
        Assert.Equal(CentralAuditActorTypes.TenantUser, row.ActorType);
        Assert.Equal(id, row.EntityId);
        Assert.Equal("Crm", row.Category);
        Assert.Equal("Update", row.Operation);
        Assert.Equal("ObjectionResponse", row.EntityType);
        Assert.Equal("CRM", row.SourceModule);
        Assert.Equal("Diten.CrmService", row.SourceService);
        Assert.Equal(nameof(UpdateObjectionResponseCommand), row.RequestType);
        Assert.False(row.WrittenInCommandTransaction);

        // AUD-001 §3: the command names the entity, never its data.
        var stored = JsonSerializer.Serialize(row);
        Assert.DoesNotContain("Secret", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_create_names_the_id_its_response_returns()
    {
        var caller = Caller.New();
        var created = Guid.NewGuid();
        _factory.CreateOutcome = _ => Response<Guid>.Success(created, 201);

        await _factory.SendAsync(caller, new CreateObjectionResponseCommand(Guid.NewGuid(), null, null, null, null));

        var row = Assert.Single(_factory.Events(caller.Tenant));
        Assert.Equal("Create", row.Operation);
        Assert.Equal(created, row.EntityId);
    }

    [Fact]
    public async Task A_failed_envelope_leaves_no_event()
    {
        var caller = Caller.New();
        _factory.Outcome = _ => Response<bool>.Fail("objection_response_not_found", 404);

        var response = await _factory.SendAsync(caller, new UpdateObjectionResponseCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null));

        Assert.False(response.IsSuccessful);
        Assert.Empty(_factory.Events(caller.Tenant));
    }

    [Fact]
    public async Task A_thrown_exception_leaves_no_event()
    {
        var caller = Caller.New();
        _factory.Outcome = _ => throw new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _factory.SendAsync(caller, new UpdateObjectionResponseCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null)));

        Assert.Empty(_factory.Events(caller.Tenant));
    }

    /// <summary>(c) Each tenant's command is filed under the tenant its own token names, never another's.</summary>
    [Fact]
    public async Task Each_tenants_event_is_filed_under_the_tenant_of_its_token()
    {
        var a = Caller.New();
        var b = Caller.New();
        _factory.Outcome = _ => Response<bool>.Success(true);

        await _factory.SendAsync(a, new UpdateObjectionResponseCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null));
        await _factory.SendAsync(b, new UpdateObjectionResponseCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null));

        Assert.Equal(a.User, Assert.Single(_factory.Events(a.Tenant)).ActorId);
        Assert.Equal(b.User, Assert.Single(_factory.Events(b.Tenant)).ActorId);
    }

    [Fact]
    public async Task A_token_tenant_that_contradicts_the_resolved_tenant_files_nothing()
    {
        var caller = Caller.New();
        var other = Guid.NewGuid();
        _factory.Outcome = _ => Response<bool>.Success(true);

        await _factory.SendAsync(caller with { ResolvedTenant = other },
            new UpdateObjectionResponseCommand(Guid.NewGuid(), Guid.NewGuid(), null, null, null, null));

        Assert.Empty(_factory.Events(caller.Tenant));
        Assert.Empty(_factory.Events(other));
    }

    [Fact]
    public async Task Visit_payload_reason_comment_photo_and_signature_never_enter_the_serialized_events()
    {
        var caller = Caller.New();
        var exceptionId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var planningSessionId = Guid.NewGuid();
        var plannedVisitId = Guid.NewGuid();
        var exceptionDto = (VisitEvidenceExceptionDto)RuntimeHelpers.GetUninitializedObject(typeof(VisitEvidenceExceptionDto)) with { Id = exceptionId };
        var executionDto = (VisitExecutionDto)RuntimeHelpers.GetUninitializedObject(typeof(VisitExecutionDto)) with { Id = executionId };
        _factory.DecideExceptionOutcome = _ => Response<VisitEvidenceExceptionDto>.Success(exceptionDto);
        _factory.SignExecutionOutcome = _ => Response<VisitExecutionDto>.Success(executionDto);
        _factory.ReopenWeekOutcome = _ => Response<PlanningWeekReopenResult>.Success(
            new PlanningWeekReopenResult(planningSessionId, "2026-11-02", "reopened", [], []));
        _factory.ShelfPhotoOutcome = _ => Response<ShelfPhotoDto>.Success(new ShelfPhotoDto(Guid.NewGuid(), "image/jpeg", 20));

        await _factory.SendAsync(caller, new DecideVisitEvidenceExceptionCommand(exceptionId, "approved", "PRIVATE-COMMENT"));
        await _factory.SendAsync(caller, new ReopenPlanningWeekCommand(planningSessionId, "2026-11-02", "PRIVATE-REASON", 1));
        await _factory.SendAsync(caller, new UploadShelfPhotoCommand(plannedVisitId,
            new VisitEvidenceFile("PRIVATE-PHOTO.jpg", "image/jpeg", Encoding.UTF8.GetBytes("PRIVATE-PHOTO-CONTENT"))));
        await _factory.SendAsync(caller, new SignVisitExecutionCommand(executionId,
            new VisitEvidenceFile("PRIVATE-SIGNATURE.png", "image/png", Encoding.UTF8.GetBytes("PRIVATE-SIGNATURE-CONTENT")),
            true, "PRIVATE-SIGNATURE-CONTEXT"));

        var rows = _factory.Events(caller.Tenant);
        Assert.Equal(4, rows.Count);
        var stored = JsonSerializer.Serialize(rows);
        Assert.DoesNotContain("PRIVATE-COMMENT", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-REASON", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-PHOTO", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-SIGNATURE", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Visit_report_v2_amend_success_records_the_report_without_serializing_body_reason_or_reporter()
    {
        var caller = Caller.New();
        var reportId = Guid.NewGuid();
        _factory.AmendVisitReportOutcome = _ => Response<VisitReportV2SaveResultDto>.Success(
            new VisitReportV2SaveResultDto(reportId, 2, "amended", [], []));
        var body = new VisitReportV2Body(
            General: new VisitReportGeneral { GeneralNote = "PRIVATE-REPORT-BODY" },
            Safety: new VisitReportSafety { AdverseEvent = true, AdverseEventNote = "PRIVATE-SAFETY-TEXT" },
            FollowUp: new VisitReportFollowUp { Required = true, Note = "PRIVATE-FOLLOW-UP" });

        await _factory.SendAsync(caller, new AmendVisitReportV2Command(
            reportId, "PRIVATE-AMENDMENT-REASON", body, 1, "PRIVATE-REPORTED-BY"));

        var row = Assert.Single(_factory.Events(caller.Tenant));
        Assert.Equal(reportId, row.EntityId);
        Assert.Equal("VisitReport", row.EntityType);
        Assert.Equal("Update", row.Operation);
        Assert.Equal(nameof(AmendVisitReportV2Command), row.RequestType);
        var stored = JsonSerializer.Serialize(row);
        Assert.DoesNotContain("PRIVATE-REPORT-BODY", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-SAFETY-TEXT", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-FOLLOW-UP", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-AMENDMENT-REASON", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE-REPORTED-BY", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Visit_report_v2_amend_failed_envelope_records_nothing()
    {
        var caller = Caller.New();
        _factory.AmendVisitReportOutcome = _ => Response<VisitReportV2SaveResultDto>.Fail("report_not_found", 404);

        var response = await _factory.SendAsync(caller, new AmendVisitReportV2Command(
            Guid.NewGuid(), "PRIVATE-AMENDMENT-REASON", new VisitReportV2Body(), 1, "PRIVATE-REPORTED-BY"));

        Assert.False(response.IsSuccessful);
        Assert.Empty(_factory.Events(caller.Tenant));
    }

    [Fact]
    public async Task Without_platform_settings_the_health_check_says_degraded()
    {
        using var client = _factory.CreateClient();
        using var health = await client.GetAsync("/health/central-audit");

        Assert.Equal("Degraded", await health.Content.ReadAsStringAsync());
    }
}

public sealed record Caller(Guid Tenant, Guid User, Guid ResolvedTenant)
{
    public static Caller New()
    {
        var tenant = Guid.NewGuid();
        return new Caller(tenant, Guid.NewGuid(), tenant);
    }
}

/// <summary>
/// Hosts the real CRM Api. Program.cs builds its configuration from appsettings + ENVIRONMENT VARIABLES itself
/// (CreateEmptyBuilder), so the test settings go in as process variables while the host is built and are put back after
/// (the MDM composition-root tests' pattern). Mongo is a test-owned mongod (<see cref="DisposableCrmMongod"/>) — the host
/// creates its indexes at registration; the outbox store is replaced and no stubbed handler touches a repository.
/// </summary>
public sealed class CrmCentralAuditApiFactory : WebApplicationFactory<Program>
{
    private static readonly object EnvironmentGate = new();

    private readonly DisposableCrmMongod _mongod = new();

    private Dictionary<string, string?> Settings => new(StringComparer.Ordinal)
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Testing",
        ["Mongo__ConnectionString"] = _mongod.ConnectionString,
        ["Mongo__DatabaseName"] = "crm_central_audit_tests",
        ["JwtSettings__Secret"] = "crm-central-audit-tests-only-signing-key-" + Guid.NewGuid().ToString("N"),
        ["CentralAudit__DispatcherEnabled"] = "false",
        ["PlatformRegistration__BaseUrl"] = string.Empty,
        ["PlatformRegistration__InternalApiKey"] = string.Empty
    };

    public InMemoryCentralAuditOutboxStore Store { get; } = new();

    public Func<UpdateObjectionResponseCommand, Response<bool>> Outcome { get; set; } = _ => Response<bool>.Success(true);

    public Func<CreateObjectionResponseCommand, Response<Guid>> CreateOutcome { get; set; } = _ => Response<Guid>.Success(Guid.NewGuid());

    public Func<DecideVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>> DecideExceptionOutcome { get; set; } =
        _ => throw new InvalidOperationException("Set the visit-exception outcome in the test.");

    public Func<SignVisitExecutionCommand, Response<VisitExecutionDto>> SignExecutionOutcome { get; set; } =
        _ => throw new InvalidOperationException("Set the visit-execution outcome in the test.");

    public Func<ReopenPlanningWeekCommand, Response<PlanningWeekReopenResult>> ReopenWeekOutcome { get; set; } =
        _ => throw new InvalidOperationException("Set the planning-week outcome in the test.");

    public Func<UploadShelfPhotoCommand, Response<ShelfPhotoDto>> ShelfPhotoOutcome { get; set; } =
        _ => throw new InvalidOperationException("Set the shelf-photo outcome in the test.");

    public Func<AmendVisitReportV2Command, Response<VisitReportV2SaveResultDto>> AmendVisitReportOutcome { get; set; } =
        _ => throw new InvalidOperationException("Set the visit-report amendment outcome in the test.");

    public IReadOnlyList<CentralAuditOutboxRecord> Events(Guid tenant) => Store.Rows.Where(r => r.TenantId == tenant).ToList();

    /// <summary>One request scope as the middleware leaves it: the signed principal, and CRM's resolved tenant.</summary>
    public async Task<TResponse> SendAsync<TResponse>(Caller caller, IRequest<TResponse> command)
    {
        using var scope = Services.CreateScope();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        http.HttpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", caller.User.ToString()), new Claim("tenant_id", caller.Tenant.ToString()), new Claim("name", "Test User")],
                "Test"))
        };
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(caller.ResolvedTenant);

        try
        {
            return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
        }
        finally
        {
            http.HttpContext = null;
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (EnvironmentGate)
        {
            var settings = Settings;
            var previous = settings.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            try
            {
                return base.CreateHost(builder);
            }
            finally
            {
                foreach (var (key, value) in previous)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _mongod.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _mongod.Dispose();
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Store);
            services.AddScoped<ICentralAuditOutboxStore>(sp => sp.GetRequiredService<InMemoryCentralAuditOutboxStore>());
            services.AddTransient<IRequestHandler<UpdateObjectionResponseCommand, Response<bool>>>(_ => new StubHandler<UpdateObjectionResponseCommand, bool>(c => Outcome(c)));
            services.AddTransient<IRequestHandler<CreateObjectionResponseCommand, Response<Guid>>>(_ => new StubHandler<CreateObjectionResponseCommand, Guid>(c => CreateOutcome(c)));
            services.AddTransient<IRequestHandler<DecideVisitEvidenceExceptionCommand, Response<VisitEvidenceExceptionDto>>>(
                _ => new StubHandler<DecideVisitEvidenceExceptionCommand, VisitEvidenceExceptionDto>(c => DecideExceptionOutcome(c)));
            services.AddTransient<IRequestHandler<SignVisitExecutionCommand, Response<VisitExecutionDto>>>(
                _ => new StubHandler<SignVisitExecutionCommand, VisitExecutionDto>(c => SignExecutionOutcome(c)));
            services.AddTransient<IRequestHandler<ReopenPlanningWeekCommand, Response<PlanningWeekReopenResult>>>(
                _ => new StubHandler<ReopenPlanningWeekCommand, PlanningWeekReopenResult>(c => ReopenWeekOutcome(c)));
            services.AddTransient<IRequestHandler<UploadShelfPhotoCommand, Response<ShelfPhotoDto>>>(
                _ => new StubHandler<UploadShelfPhotoCommand, ShelfPhotoDto>(c => ShelfPhotoOutcome(c)));
            services.AddTransient<IRequestHandler<AmendVisitReportV2Command, Response<VisitReportV2SaveResultDto>>>(
                _ => new StubHandler<AmendVisitReportV2Command, VisitReportV2SaveResultDto>(c => AmendVisitReportOutcome(c)));
        });
    }

    private sealed class StubHandler<TCommand, T>(Func<TCommand, Response<T>> answer) : IRequestHandler<TCommand, Response<T>>
        where TCommand : IRequest<Response<T>>
    {
        public Task<Response<T>> Handle(TCommand request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }
}

/// <summary>The shared package's outbox contract, in memory (append-only: no member removes a row).</summary>
public sealed class InMemoryCentralAuditOutboxStore : ICentralAuditOutboxStore
{
    private readonly ConcurrentQueue<CentralAuditOutboxRecord> _rows = new();

    public IReadOnlyCollection<CentralAuditOutboxRecord> Rows => _rows.ToArray();

    public Task InsertInTransactionAsync(IClientSessionHandle session, CentralAuditOutboxRecord record, CancellationToken ct) =>
        throw new InvalidOperationException("These CRM commands own no transaction; nothing should enlist.");

    public Task InsertAsync(CentralAuditOutboxRecord record, CancellationToken ct)
    {
        _rows.Enqueue(record);
        return Task.CompletedTask;
    }

    public Task SetEntityIdIfMissingAsync(Guid id, Guid entityId, CancellationToken ct) => Task.CompletedTask;

    public Task ConfirmAsync(Guid id, CancellationToken ct) => Task.CompletedTask;

    public Task AbandonAsync(Guid id, CancellationToken ct) => Task.CompletedTask;

    public Task<long> ReleaseOrphansAsync(DateTime createdBeforeUtc, CancellationToken ct) => Task.FromResult(0L);

    public Task<IReadOnlyList<CentralAuditOutboxRecord>> GetDueAsync(DateTime nowUtc, int batchSize, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CentralAuditOutboxRecord>>([]);

    public Task MarkDeliveredAsync(Guid id, DateTime nowUtc, CancellationToken ct) => Task.CompletedTask;

    public Task MarkRetryAsync(Guid id, int attempts, DateTime nextAttemptAtUtc, string error, CancellationToken ct) => Task.CompletedTask;

    public Task MarkRejectedAsync(Guid id, int attempts, string error, CancellationToken ct) => Task.CompletedTask;

    public Task<CentralAuditBacklog> GetBacklogAsync(CancellationToken ct) =>
        Task.FromResult(new CentralAuditBacklog(_rows.Count, _rows.IsEmpty ? null : _rows.Min(r => r.CreatedAtUtc), 0));

    public Task EnsureCreatedAsync(CancellationToken ct) => Task.CompletedTask;

    public Task CreateIndexesAsync(CancellationToken ct) => Task.CompletedTask;
}
