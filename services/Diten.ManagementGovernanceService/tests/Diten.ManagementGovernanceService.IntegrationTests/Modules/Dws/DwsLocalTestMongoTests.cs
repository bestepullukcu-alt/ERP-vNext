using System.Net;
using System.Net.Http.Json;
using Diten.ManagementGovernanceService.Api;
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
        await app.StartAsync();
        try
        {
            using var client=new HttpClient{BaseAddress=new Uri("http://127.0.0.1:5017")};
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health")).StatusCode);
            var tenant=Guid.NewGuid();
            var actor=Guid.NewGuid();
            var message=new HttpRequestMessage(HttpMethod.Post,"/api/dws/structures")
            {
                Content=JsonContent.Create(new CreateStructureCommand(new ExternalContextReference("ppm.external-context-reference","1.0",ExternalContextKind.Project,Guid.NewGuid()),"Local smoke",null))
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
            var audit=Assert.IsType<LocalTestDwsAuditSimulator>(app.Services.GetRequiredService<IDwsAuditSimulator>());
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
