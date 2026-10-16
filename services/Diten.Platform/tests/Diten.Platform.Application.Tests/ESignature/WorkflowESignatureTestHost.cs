using System.Net.Http.Headers;
using System.Text;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Diten.Platform.API.Controllers.ESignature;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.ESignature.Providers;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Authorization;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.ESignature;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// MOD-0022 E4 — the ESign host of E2 plus MOD-0023 and the WorkCenter action endpoint, on one TEST-OWNED replica set.
/// Production: JWT validation, tenant middleware, <c>[HasPermission]</c>, <c>WorkflowDefinitionsController</c>,
/// <c>WorkItemsController</c> with the in-process workflow dispatcher, the ESign controllers, the MediatR pipeline, the
/// workflow + e-signature Mongo repositories, the in-process <c>workflow / workflow-task</c> subject provider and the
/// gate. Fakes only at the edges: AuthService's ticket consume, user / company lookups, the SEC-002 data scope. Tokens are
/// test-host tokens (this host's own key), never the services' keys.
/// </summary>
public sealed class WorkflowESignatureTestHost : IAsyncLifetime
{
    private const string Issuer = "diten-auth-esign-test";
    private const string Audience = "diten-platform-esign-test";
    private const string Secret = "esign e2 test host secret, test only, 0123456789abcdef";

    private DisposableMongoReplicaSet? _mongo;
    private TestServer? _server;

    public IMongoDatabase Database { get; private set; } = null!;
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "diten-esign-e4-tests", Path.GetRandomFileName());
    public FakeTicketConsumer Tickets { get; } = new();
    public FakeUserReferences Users { get; } = new();
    public FakeLegalEntities LegalEntities { get; } = new();
    public FakeDataScopes Scopes { get; } = new();
    public IServiceProvider Services => _server!.Services;

    public async Task InitializeAsync()
    {
        _mongo = await DisposableMongoReplicaSet.StartAsync();
        Database = _mongo.CreateDatabase();
        await PlatformSchemaManifest.ApplyAsync(Database, new[]
        {
            SchemaProfile.ESignature, SchemaProfile.AccessGovernance, SchemaProfile.WorkflowWorkCenter, SchemaProfile.Eventing, SchemaProfile.Organization
        });
        Directory.CreateDirectory(StorageRoot);

        var dbContext = new PlatformDbContext(_mongo.Client, Database);
        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthService:InternalApiKey"] = ESignatureTestHost.InternalKey
            }))
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                        ValidIssuer = Issuer, ValidAudience = Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                        ClockSkew = TimeSpan.Zero
                    };
                });
                services.AddAuthorization();
                services.AddHttpContextAccessor();

                services.AddApplication();
                services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                services.AddScoped<ITenantContext, TenantContext>();
                services.AddScoped<ICorrelationContext>(_ =>
                {
                    var correlation = new CorrelationContext();
                    correlation.SetCorrelationId("esign-e4-test");
                    return correlation;
                });

                services.AddSingleton<IPlatformDbContext>(dbContext);
                services.AddScoped<IPlatformTransactionExecutor>(_ => new PlatformTransactionExecutor(dbContext));
                // e-signature
                services.AddScoped<IESignatureRecordRepository, ESignatureRecordRepository>();
                services.AddScoped<IESignatureInvalidationRepository, ESignatureInvalidationRepository>();
                services.AddScoped<IESignatureExportRepository, ESignatureExportRepository>();
                services.AddScoped<IESignaturePolicyChangeRepository, ESignaturePolicyChangeRepository>();
                services.AddScoped<IESignRequestRepository, ESignRequestRepository>();
                services.AddScoped<IESignPolicyRepository, ESignPolicyRepository>();
                services.AddScoped<IESignMeaningRepository, ESignMeaningRepository>();
                services.AddScoped<IESignActionRepository, ESignActionRepository>();
                services.AddScoped<IESignNumberGenerator, ESignNumberGenerator>();
                services.AddScoped<IESignatureSampleRepository, ESignatureSampleRepository>();
                services.AddScoped<IESignatureSampleEventRepository, ESignatureSampleEventRepository>();
                services.AddScoped<IESignSampleOwnerStateRepository, ESignSampleOwnerStateRepository>();
                services.AddScoped<AuditOutboxRepository>();
                services.AddScoped<IAuditOutboxWriter>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                // Main sync 2026-10-13: the lane's audit door (WP-PLATFORM-AUDIT-INTX-01) — the repository is the STORE; the writer
                // is AddApplication's CanonicalTransactionalAuditOutboxWriter, exactly as in production.
                services.AddScoped<ITransactionalAuditOutboxStore>(sp => sp.GetRequiredService<AuditOutboxRepository>());
                services.Configure<ContentStorageOptions>(o => o.RootPath = StorageRoot);
                services.AddScoped<IContentStorageGateway, LocalFileSystemContentStorageGateway>();
                services.AddScoped<IESignRequestLanguage, HttpESignRequestLanguage>();
                // VIS1 — the audit export (createExport) runs on this host too (the E2 host registers the same renderer).
                services.AddSingleton<Diten.Platform.Application.Features.ESignature.Handlers.IESignaturePdfRenderer, MigraDocESignaturePdfRenderer>();
                // MOD-0023
                services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
                services.AddScoped<IWorkflowTemplateVersionRepository, WorkflowTemplateVersionRepository>();
                services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
                services.AddScoped<IApprovalTaskRepository, ApprovalTaskRepository>();
                services.AddScoped<IRuntimeAssignmentSnapshotRepository, RuntimeAssignmentSnapshotRepository>();
                services.AddScoped<IWorkflowTransitionLogRepository, WorkflowTransitionLogRepository>();
                services.AddScoped<ISlaEscalationRuleRepository, SlaEscalationRuleRepository>();
                services.AddScoped<IPositionRepository, PositionRepository>();
                services.AddScoped<IPositionAssignmentRepository, PositionAssignmentRepository>();
                services.AddScoped<IOutboxEventRepository, OutboxEventRepository>();
                services.AddScoped<ITransactionalOutboxEventWriter>(sp => (ITransactionalOutboxEventWriter)sp.GetRequiredService<IOutboxEventRepository>());
                services.AddSingleton<Diten.BuildingBlocks.Eventing.ITrustedTransportMetadataProvider, Diten.BuildingBlocks.Eventing.EmptyTrustedTransportMetadataProvider>();
                services.AddScoped<ITransactionalIntegrationEventWriter, TransactionalIntegrationEventWriter>();

                // Edges.
                services.AddSingleton<IESignTicketConsumer>(Tickets);
                services.RemoveAll<ISignableSubjectProvider>();
                services.AddScoped<ISignableSubjectProvider, WorkflowTaskSignableSubjectProvider>(); // the production E4 provider
                services.AddSingleton<IRemoteSignableSubjectClient>(new NoRemoteProviders());
                services.AddSingleton<IUserReferenceValidator>(Users);
                services.AddSingleton<ILegalEntityReferenceValidator>(LegalEntities);
                services.AddSingleton<IDataScopeResolver>(Scopes);
                // WorkCenter: only the workflow provider + its in-process dispatcher (the others need their own modules).
                services.RemoveAll<IWorkItemProvider>();
                services.RemoveAll<IWorkItemActionDispatcher>();
                services.AddScoped<IWorkItemProvider, WorkflowApprovalWorkItemProvider>();
                services.AddScoped<IWorkItemActionDispatcher, WorkflowApprovalWorkItemActionDispatcher>();
                // the approval-source resolvers of other modules (tasks, reviews) need their own stores; none applies here.
                services.RemoveAll<IApprovalSourceResolver>();

                services.AddControllers()
                    .AddApplicationPart(typeof(ESignatureSignaturesController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseTenantResolution();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        _server = new TestServer(builder);
    }

    public async Task DisposeAsync()
    {
        _server?.Dispose();
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }

        try
        {
            Directory.Delete(StorageRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public HttpClient Client(string token)
    {
        var client = _server!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("tr");
        return client;
    }
}
