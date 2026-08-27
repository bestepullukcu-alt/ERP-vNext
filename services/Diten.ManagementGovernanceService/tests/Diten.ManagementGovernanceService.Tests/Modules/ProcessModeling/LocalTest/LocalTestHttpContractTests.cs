using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.ManagementGovernanceService.Api.Controllers;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest.Commands;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.LocalTest;

public sealed class LocalTestHttpContractTests
{
    private static readonly Guid TenantId=Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ActorId=Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task Health_is_the_only_anonymous_success()
    {
        await using var host=await LocalHost.StartAsync(null,new SuccessfulExecutor());
        Assert.Equal(HttpStatusCode.OK,(await host.Client.GetAsync("/internal/local-test/v1/process-modeling/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await host.Client.PostAsync(
            "/internal/local-test/v1/process-modeling/architectures",
            Json("""{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}"""))).StatusCode);
    }

    [Fact]
    public async Task Tenant_permission_and_idempotency_boundaries_fail_closed()
    {
        var permission="management-governance.process-modeling.architectures.create";
        await using var host=await LocalHost.StartAsync(new LocalTestIdentity(ActorId,TenantId,[permission]),new SuccessfulExecutor());

        using var wrongTenant=Request(HttpMethod.Post,"/internal/local-test/v1/process-modeling/architectures","""{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}""",Guid.NewGuid(),permission,true);
        Assert.Equal(HttpStatusCode.NotFound,(await host.Client.SendAsync(wrongTenant)).StatusCode);

        using var missingPermission=Request(HttpMethod.Post,"/internal/local-test/v1/process-modeling/architectures","""{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}""",TenantId,permission,false);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.SendAsync(missingPermission)).StatusCode);

        await using var noPermission=await LocalHost.StartAsync(new LocalTestIdentity(ActorId,TenantId,[ProcessModelingPermissions.ExactPermissions.First(x=>x.EndsWith(".read",StringComparison.Ordinal))]),new SuccessfulExecutor());
        using var denied=Request(HttpMethod.Post,"/internal/local-test/v1/process-modeling/architectures","""{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}""",TenantId,permission,true);
        Assert.Equal(HttpStatusCode.Forbidden,(await noPermission.Client.SendAsync(denied)).StatusCode);
    }

    [Theory]
    [InlineData("""{"ArchitectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}""")]
    [InlineData("""{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1,"extra":true}""")]
    [InlineData("""{"architectureCode":"EA","architectureCode":"OTHER","name":"Enterprise Architecture","sortOrder":1}""")]
    [InlineData("""{"name":"Enterprise Architecture","sortOrder":1}""")]
    public async Task Strict_json_rejects_case_extra_duplicate_and_missing_fields(string body)
    {
        var permission="management-governance.process-modeling.architectures.create";
        await using var host=await LocalHost.StartAsync(new LocalTestIdentity(ActorId,TenantId,[permission]),new SuccessfulExecutor());
        using var request=Request(HttpMethod.Post,"/internal/local-test/v1/process-modeling/architectures",body,TenantId,permission,true);
        Assert.Equal(HttpStatusCode.BadRequest,(await host.Client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Create_returns_typed_201_envelope_and_preserves_raw_binding()
    {
        var permission="management-governance.process-modeling.architectures.create";
        var executor=new SuccessfulExecutor();
        await using var host=await LocalHost.StartAsync(new LocalTestIdentity(ActorId,TenantId,[permission]),executor);
        const string body="""{"architectureCode":"EA","name":"Enterprise Architecture","sortOrder":1}""";
        using var request=Request(HttpMethod.Post,"/internal/local-test/v1/process-modeling/architectures",body,TenantId,permission,true);
        var response=await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        using var envelope=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(envelope.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.Equal(executor.CreatedId,envelope.RootElement.GetProperty("data").GetGuid());
        var captured=Assert.Single(executor.Requests);
        Assert.Equal(nameof(CreateProcessArchitectureCommand),captured.CommandName);
        Assert.Equal(Encoding.UTF8.GetBytes(body),captured.RawBody);
        Assert.Equal(TenantId,captured.Context.TenantId);
        Assert.Equal(ActorId,captured.Context.ActorId);
        Assert.Equal(permission,captured.Context.Permission);
        Assert.Equal("idem-1",captured.Context.IdempotencyKey);
    }

    [Fact]
    public async Task Update_returns_empty_204()
    {
        var permission="management-governance.process-modeling.architectures.update";
        await using var host=await LocalHost.StartAsync(new LocalTestIdentity(ActorId,TenantId,[permission]),new SuccessfulExecutor());
        var id=Guid.NewGuid();
        using var request=Request(HttpMethod.Put,$"/internal/local-test/v1/process-modeling/architectures/{id:D}","""{"name":"Updated","sortOrder":2,"expectedVersion":0}""",TenantId,permission,true);
        var response=await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
        Assert.Equal(string.Empty,await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Exact_twenty_commands_have_typed_MediatR_response_contracts()
    {
        var commands=typeof(CreateProcessArchitectureCommand).Assembly.GetTypes()
            .Where(x=>x.Namespace==typeof(CreateProcessArchitectureCommand).Namespace&&x.Name.EndsWith("Command",StringComparison.Ordinal))
            .OrderBy(x=>x.Name,StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(20,commands.Length);
        foreach(var command in commands)
        {
            var request=Assert.Single(command.GetInterfaces(),x=>x.IsGenericType&&x.GetGenericTypeDefinition()==typeof(MediatR.IRequest<>));
            var response=request.GetGenericArguments()[0];
            Assert.Equal(typeof(Response<>),response.GetGenericTypeDefinition());
            Assert.Contains(response.GetGenericArguments()[0],new[]{typeof(Guid),typeof(NoContent)});
        }
        Assert.Equal(6,commands.Count(x=>x.GetInterfaces().Any(i=>i.IsGenericType&&i.GetGenericArguments().Contains(typeof(Response<Guid>)))));
    }

    private static HttpRequestMessage Request(HttpMethod method,string path,string body,Guid tenant,string permission,bool includeIdempotency)
    {
        var request=new HttpRequestMessage(method,path){Content=Json(body)};
        request.Headers.Add("X-Tenant-Id",tenant.ToString("D"));
        if(includeIdempotency)request.Headers.Add("Idempotency-Key","idem-1");
        request.Headers.Authorization=new AuthenticationHeaderValue(LocalTestAuthenticationHandler.SchemeName);
        return request;
    }

    private static StringContent Json(string body)=>new(body,Encoding.UTF8,"application/json");

    private sealed class SuccessfulExecutor:IProcessModelingLocalCommandExecutor
    {
        internal Guid CreatedId { get; }=Guid.Parse("33333333-3333-4333-8333-333333333333");
        internal List<ProcessModelingLocalExecutionRequest> Requests { get; }=[];
        public Task<ProcessModelingLocalExecutionResult> ExecuteAsync(ProcessModelingLocalExecutionRequest command,CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(command);
            return Task.FromResult(command.CommandName.StartsWith("Create",StringComparison.Ordinal)
                ? ProcessModelingLocalExecutionResult.Created(CreatedId)
                : ProcessModelingLocalExecutionResult.NoContent());
        }
    }

    private sealed class IdentitySource(LocalTestIdentity? identity):ILocalTestIdentitySource
    {
        public LocalTestIdentity? Resolve()=>identity;
    }

    private sealed class LocalHost(WebApplication application,HttpClient client):IAsyncDisposable
    {
        internal HttpClient Client { get; }=client;
        internal static async Task<LocalHost> StartAsync(LocalTestIdentity? identity,IProcessModelingLocalCommandExecutor executor)
        {
            var builder=WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddManagementGovernanceApplication();
            builder.Services.AddSingleton(executor);
            builder.Services.AddSingleton<IProcessModelingLocalCommandExecutor>(executor);
            if(identity is not null)builder.Services.AddSingleton<ILocalTestIdentitySource>(new IdentitySource(identity));
            builder.Services.AddAuthentication(LocalTestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions,LocalTestAuthenticationHandler>(LocalTestAuthenticationHandler.SchemeName,_=>{});
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddApplicationPart(typeof(ProcessModelingLocalTestController).Assembly);
            var app=builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return new LocalHost(app,app.GetTestClient());
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.StopAsync();
            await application.DisposeAsync();
        }
    }
}
