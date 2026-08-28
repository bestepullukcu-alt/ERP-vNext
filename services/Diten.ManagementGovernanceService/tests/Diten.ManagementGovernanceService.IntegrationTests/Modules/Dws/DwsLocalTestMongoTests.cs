using System.Net;
using System.Net.Http.Json;
using Diten.ManagementGovernanceService.Api;
using Diten.ManagementGovernanceService.Api.LocalTest;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManagementGovernanceService.IntegrationTests.Modules.Dws;

[Collection(DwsMongoCollection.Name)]
public sealed class DwsLocalTestMongoTests(DisposableDwsMongo mongo)
{
    [Fact]
    public async Task Local_host_health_and_create_are_loopback_persistence_smoke()
    {
        var database="mod0354_local_host_"+Guid.NewGuid().ToString("N");
        var uri=$"mongodb://127.0.0.1:{mongo.Port}/?replicaSet={mongo.ReplicaSetName}&directConnection=true&serverSelectionTimeoutMS=1000";
        var app=Program.BuildLocalTestApp(uri,database);
        await app.Services.GetRequiredService<DwsMongoIndexInitializer>().InitializeAsync();
        var tenant=Guid.NewGuid();
        var subject=Guid.NewGuid();
        var actor=Guid.NewGuid();
        var delegated=Guid.NewGuid();
        var reference=new ExternalContextReference("ppm.external-context-reference","1.0",ExternalContextKind.Project,Guid.NewGuid());
        var actorContext=new DwsTrustedActorContext(tenant,subject,actor,delegated,"local-smoke-1");
        app.Services.GetRequiredService<DwsLocalTestIdentityFixture>().Configure(new(true,tenant,subject,actor,delegated));
        app.Services.GetRequiredService<DwsLocalMod0117Fixture>().Configure(new(actorContext,reference,1,DwsLocalContextDisposition.Accepted));
        app.Services.GetRequiredService<DwsLocalFu16Fixture>().Configure(new(
            DwsFunctionalAuthorizationBinding.ModuleCode,
            DwsFunctionalAuthorizationBinding.ModuleEntitlementCode,
            actorContext,
            "CreateStructureCommand",
            DwsAuthorizationManifest.RequireExact("CreateStructureCommand"),
            true,1,1,1,1,DwsLocalAuthorizationDisposition.Accepted));
        await app.StartAsync();
        try
        {
            using var client=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:5017")};
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health")).StatusCode);
            var message=new HttpRequestMessage(HttpMethod.Post,"/api/dws/structures")
            {
                Content=JsonContent.Create(new CreateStructureCommand(reference,"Local smoke",null))
            };
            message.Headers.Add("X-Diten-Test-Tenant",tenant.ToString("D"));
            message.Headers.Add("X-Diten-Test-Actor",actor.ToString("D"));
            message.Headers.Add("X-Diten-Test-Idempotency-Key","local-smoke-1");
            Assert.Equal(HttpStatusCode.Created,(await client.SendAsync(message)).StatusCode);
            var context=app.Services.GetRequiredService<DwsMongoContext>();
            var filter=Builders<BsonDocument>.Filter.Eq("TenantId",new BsonBinaryData(tenant,GuidRepresentation.Standard));
            long count=0;
            foreach(var alias in DwsMongoContext.CollectionAliases.Keys)count+=await context.Collection(alias).CountDocumentsAsync(filter);
            Assert.Equal(5,count);
            Assert.Equal(1,await context.Collection("audit-intents").CountDocumentsAsync(filter));
            Assert.Equal(1,await context.Collection("outbox").CountDocumentsAsync(filter));
            var audit=Assert.IsType<LocalTestDwsAuditSimulator>(app.Services.GetRequiredService<IDwsAuditSimulator>());
            Assert.Empty(audit.Records);

            var legacyTenant=Guid.NewGuid();
            var legacyActor=Guid.NewGuid();
            var legacyKey="legacy-smoke-1";
            using(var serviceScope=app.Services.CreateScope())
            {
                var legacy=serviceScope.ServiceProvider.GetRequiredService<IDwsLocalActionExecutor>();
                var legacyContract=new CreateStructureCommand(
                    new ExternalContextReference("ppm.external-context-reference","1.0",ExternalContextKind.Project,Guid.NewGuid()),
                    "Legacy smoke",
                    null);
                var legacyResult=await new DwsDispatchHandler(legacy).Handle(
                    new DwsDispatchRequest(nameof(CreateStructureCommand),legacyContract,new(legacyTenant,legacyActor,legacyKey)),
                    CancellationToken.None);
                Assert.Equal(201,legacyResult.StatusCode);
            }
            var legacyFilter=Builders<BsonDocument>.Filter.Eq("TenantId",new BsonBinaryData(legacyTenant,GuidRepresentation.Standard));
            long legacyCount=0;
            foreach(var alias in DwsMongoContext.CollectionAliases.Keys)legacyCount+=await context.Collection(alias).CountDocumentsAsync(legacyFilter);
            Assert.Equal(5,legacyCount);
            Assert.Single(audit.Records);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            await mongo.Client.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task Local_executor_rejects_operation_permission_drift_before_write()
    {
        var authorization=new LocalTestFu16AuthorizationAdapter();
        await Assert.ThrowsAsync<DwsValidationException>(()=>authorization.AuthorizeAsync(Guid.NewGuid(),Guid.NewGuid(),"CreateStructureCommand","management-governance.dws.read",CancellationToken.None));
    }
}
