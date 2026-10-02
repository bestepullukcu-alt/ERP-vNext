extern alias PlatformApi;
extern alias MdmInfrastructure;
extern alias MdmApplication;

using System.Reflection;
using System.Text.Json;
using Diten.AuthService.Api.Controllers.Internal;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.DevTrustBootstrap;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Infrastructure;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using MongoDB.Driver;
using MdmApplication::Diten.MdmService.Application.Contracts.Workflow;
using MdmInfrastructure::Diten.MdmService.Infrastructure.Workflow;
using PlatformApi::Diten.Platform.API.Configuration;
using PlatformApi::Diten.Platform.API.Security;
using TrustedWorkflowStartAuthorizationRequest = Diten.Platform.Application.Features.Workflow.Services.TrustedWorkflowStartAuthorizationRequest;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// The whole chain with production code on every link and disposable keys:
/// bootstrap (generator inside) → Auth settings carry the HASH → Auth startup creates identity and grant →
/// MDM's own token client gets a token from Auth's token endpoint with the RAW secret → Platform's validation
/// parameters and claim contract accept that token, and Platform's start-authorization policy (with the TRACKED
/// Development entry) authorizes it. A second Auth startup writes nothing.
///
/// <para>What is NOT in this path: Auth's tenant-resolution middleware (the host below maps only the token
/// endpoint), the network, and the three services' full startup. Those belong to the live bring-up.</para>
/// </summary>
[Collection(OwnedMongoCollection.Name)]
public sealed class TrustChainEndToEndTests(OwnedMongo mongo)
{
    [Fact]
    public async Task Chain_FromBootstrapToPlatformAcceptance_WorksWithProductionCodeOnEveryLink()
    {
        using var targets = new DevTrustBootstrapTests.Targets();
        var clientCode = RegistryStartup.NewClientCode();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        Assert.Equal(DevTrustBootstrapCommand.Success, DevTrustBootstrapCommand.Run(
        [
            "--auth-dir", targets.Auth, "--platform-dir", targets.Platform, "--mdm-dir", targets.Mdm,
            "--tenant-id", tenant.ToString("D"), "--approver-user-id", Guid.NewGuid().ToString("D"),
            "--client-code", clientCode
        ], new StringWriter(), new StringWriter()));
        var authSettings = Settings(targets.Files[0]);
        var platformSettings = Settings(targets.Files[1]);
        var mdmSettings = Settings(targets.Files[2]);

        // Auth's settings hold the hash and never the raw secret.
        var rawSecret = mdmSettings["AuthProductIdentityWorkflowServiceIdentityProvider:ActiveClientSecret"]!;
        Assert.False(File.ReadAllText(targets.Files[0]).Contains(rawSecret, StringComparison.Ordinal),
            "Auth's settings must not contain the raw client secret");
        Assert.False(File.ReadAllText(targets.Files[1]).Contains("PRIVATE KEY", StringComparison.Ordinal),
            "Platform's settings must not contain a private key");

        // ── Auth starts: the registry creates the identity and the grant ──────────────────────────────────────
        await using var auth = await StartAuthAsync(authSettings);
        var identity = await mongo.Database.GetCollection<ServiceClientIdentity>("serviceClientIdentities")
            .Find(x => x.ClientCode == clientCode).SingleAsync();
        Assert.Equal(ServiceClientIdentityIds.FromClientCode(clientCode), identity.Id);
        Assert.Equal(1, await mongo.Database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants")
            .CountDocumentsAsync(x => x.ServiceClientIdentityId == identity.Id && x.TenantId == tenant && x.IsEnabled));

        // ── MDM's own client asks Auth's token endpoint, with the raw secret from MDM's settings ───────────────
        var provider = new AuthProductIdentityWorkflowServiceIdentityProvider(
            new TestServerClients(auth.GetTestServer()),
            Options.Create(mdmSettings.GetSection(AuthProductIdentityWorkflowServiceIdentityProviderOptions.SectionName)
                .Get<AuthProductIdentityWorkflowServiceIdentityProviderOptions>()!),
            TimeProvider.System);
        var token = (await provider.GetAsync(tenant, forceRefresh: false)).AccessToken;

        // A tenant that was not declared gets nothing.
        var rejected = await Assert.ThrowsAsync<ProductIdentityWorkflowServiceIdentityException>(
            () => provider.GetAsync(otherTenant, forceRefresh: false));
        Assert.Equal("PRODUCT_WORKFLOW_IDENTITY_REJECTED", rejected.ErrorCode);

        // ── Platform's validation scheme accepts that token ────────────────────────────────────────────────────
        var validation = platformSettings.GetSection(TrustedServiceTokenValidationOptions.SectionName)
            .Get<TrustedServiceTokenValidationOptions>()!;
        var parameters = TrustedServiceTokenValidationExtensions.CreateTokenValidationParameters(
            validation, TimeProvider.System, TrustedServiceTokenValidationExtensions.WorkflowRequiredAudience);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid, "Platform's trusted workflow scheme must accept the Auth-issued token");
        Assert.True(TrustedServiceTokenValidationExtensions.HasExactWorkflowServiceClaims(result.SecurityToken, validation.Issuer),
            "the token must satisfy Platform's exact service-claim contract");
        var subject = Guid.Parse(((JsonWebToken)result.SecurityToken).Subject);
        Assert.Equal(identity.Id, subject);
        Assert.Equal(tenant.ToString("D"), ((JsonWebToken)result.SecurityToken).GetClaim("tenant_id").Value);

        // The audit-ingest scheme (another audience) must NOT accept a workflow token.
        var auditParameters = TrustedServiceTokenValidationExtensions.CreateTokenValidationParameters(validation, TimeProvider.System);
        Assert.False((await new JsonWebTokenHandler().ValidateTokenAsync(token, auditParameters)).IsValid,
            "a workflow token must not pass as an audit-ingest token");

        // ── A second Auth startup writes nothing ───────────────────────────────────────────────────────────────
        var before = await RegistryStartup.SnapshotAsync(mongo.Database, [clientCode], [tenant, otherTenant]);
        await using (await StartAuthAsync(authSettings))
        {
        }

        Assert.Equal(before, await RegistryStartup.SnapshotAsync(mongo.Database, [clientCode], [tenant, otherTenant]));
    }

    /// <summary>
    /// The TRACKED Development settings line up with what the bootstrap and Auth produce: the Platform entry names
    /// the id Auth derives from the default client code, and the template code is the one MDM sends.
    /// </summary>
    [Fact]
    public void TrackedDevelopmentSettings_AuthorizeTheDefaultDevelopmentClientForGlobalProduct()
    {
        var root = DevTrustBootstrapTests.RepositoryRoot();
        var platform = Settings(Path.Combine(root, "services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json"));
        var mdm = Settings(Path.Combine(root, "services/Diten.MdmService/src/Diten.MdmService.Api/appsettings.Development.json"));
        var authorization = platform.GetSection(TrustedWorkflowStartAuthorizationOptions.SectionName)
            .Get<TrustedWorkflowStartAuthorizationOptions>()!;
        var policy = new ConfiguredTrustedWorkflowStartAuthorizationPolicy(Options.Create(authorization));

        Assert.True(policy.IsAuthorized(new TrustedWorkflowStartAuthorizationRequest(
            ServiceClientIdentityIds.FromClientCode(DevTrustBootstrapCommand.DefaultClientCode),
            DevTrustBootstrapCommand.ServiceName,
            DevTrustBootstrapCommand.Audience,
            "GlobalProduct",
            null,
            mdm["ProductIdentityWorkflow:GlobalProductTemplateCode"])));
        Assert.True(PlatformProductIdentityWorkflowClient.IsValidConfiguration(
            mdm.GetSection(ProductIdentityWorkflowClientOptions.SectionName).Get<ProductIdentityWorkflowClientOptions>()!));
        Assert.False(string.IsNullOrWhiteSpace(mdm["ProductIdentityWorkflow:ReasonCode"]));
        Assert.False(string.IsNullOrWhiteSpace(mdm["ProductIdentityWorkflowWorker:LeaseOwner"]));

        // Tracked files carry no switch and no secret of this chain: a checkout that was never bootstrapped stays off.
        Assert.Null(mdm["ProductIdentityWorkflow:Enabled"]);
        Assert.Null(mdm["ProductIdentityWorkflowWorker:Enabled"]);
        Assert.Null(mdm["AuthProductIdentityWorkflowServiceIdentityProvider:ActiveClientSecret"]);
        Assert.Null(platform["TrustedServiceTokenValidation:CurrentPublicKeyPem"]);
        var auth = Settings(Path.Combine(root, "services/Diten.AuthService/src/Diten.AuthService.Api/appsettings.Development.json"));
        Assert.Null(auth["ServiceIdentityTokenIssuer:ActivePrivateKeyPem"]);
        Assert.Empty(auth.GetSection("ServiceClientRegistry").GetChildren());
    }

    /// <summary>
    /// A non-default client code (the way out when the first credential set is lost): Platform's local file then
    /// carries the authorization entry for THAT client, and over the tracked file it is the one entry in force.
    /// </summary>
    [Fact]
    public void Bootstrap_WithAnotherClientCode_WritesPlatformsAuthorizationEntryForThatClient()
    {
        using var targets = new DevTrustBootstrapTests.Targets();
        const string clientCode = "diten-mdm-workflow-second";
        Assert.Equal(DevTrustBootstrapCommand.Success, DevTrustBootstrapCommand.Run(
            [.. targets.Arguments(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")), "--client-code", clientCode],
            new StringWriter(), new StringWriter()));
        var root = DevTrustBootstrapTests.RepositoryRoot();
        var mdmTemplateCode = Settings(Path.Combine(root, "services/Diten.MdmService/src/Diten.MdmService.Api/appsettings.Development.json"))
            ["ProductIdentityWorkflow:GlobalProductTemplateCode"];
        Assert.Equal(DevTrustBootstrapCommand.DefaultTemplateCode, mdmTemplateCode);

        // Tracked file first, local file over it — the order Platform's Program.cs loads them in Development.
        var platform = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(root, "services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json"), optional: false)
            .AddJsonFile(targets.Files[1], optional: false)
            .Build();
        var authorization = platform.GetSection(TrustedWorkflowStartAuthorizationOptions.SectionName)
            .Get<TrustedWorkflowStartAuthorizationOptions>()!;
        var policy = new ConfiguredTrustedWorkflowStartAuthorizationPolicy(Options.Create(authorization));

        Assert.Single(authorization.Entries);
        Assert.True(policy.IsAuthorized(Request(clientCode, mdmTemplateCode)));
        Assert.False(policy.IsAuthorized(Request(DevTrustBootstrapCommand.DefaultClientCode, mdmTemplateCode)));

        static TrustedWorkflowStartAuthorizationRequest Request(string code, string? templateCode) => new(
            ServiceClientIdentityIds.FromClientCode(code), DevTrustBootstrapCommand.ServiceName,
            DevTrustBootstrapCommand.Audience, DevTrustBootstrapCommand.GlobalProductObjectType, null, templateCode);
    }

    /// <summary>The bootstrap's MDM file is a configuration MDM's own validators accept — including a non-empty candidate list.</summary>
    [Fact]
    public void BootstrapOutput_IsAcceptedByTheConsumersOwnValidators()
    {
        using var targets = new DevTrustBootstrapTests.Targets();
        var approver = Guid.NewGuid();
        Assert.Equal(DevTrustBootstrapCommand.Success, DevTrustBootstrapCommand.Run(
            targets.Arguments(Guid.NewGuid().ToString("D"), approver.ToString("D")), new StringWriter(), new StringWriter()));
        var mdm = Settings(targets.Files[2]);

        AuthProductIdentityWorkflowServiceIdentityProvider.EnsureValidConfiguration(
            mdm.GetSection(AuthProductIdentityWorkflowServiceIdentityProviderOptions.SectionName)
                .Get<AuthProductIdentityWorkflowServiceIdentityProviderOptions>()!);
        Assert.Equal("True", mdm["ProductIdentityWorkflow:Enabled"]);
        Assert.Equal(approver.ToString("D"), mdm["ProductIdentityWorkflow:CandidatePrincipalIds:0"]);
        Assert.Equal("True", mdm["ProductIdentityWorkflowWorker:Enabled"]);

        // And Auth's own registry validation accepts the Auth file.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceClientRegistry(Settings(targets.Files[0]));
        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetRequiredService<ServiceClientRegistryDeclarations>().Clients);
    }

    private static IConfigurationRoot Settings(string path) =>
        new ConfigurationBuilder().AddJsonFile(path, optional: false, reloadOnChange: false).Build();

    /// <summary>
    /// A host that holds exactly what token issuance is in production — the real controller, MediatR pipeline,
    /// handler, verifier, RS256 issuer, repositories — plus the production registry wiring, over the owned mongod.
    /// An empty builder: no appsettings file, no environment variables, no user secrets reach it.
    /// </summary>
    private async Task<WebApplication> StartAuthAsync(IConfiguration authLocalSettings)
    {
        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions { EnvironmentName = "ServiceTrustChainTest" });
        builder.Configuration.AddConfiguration(authLocalSettings);
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton(mongo.Database);
        builder.Services.AddScoped<IServiceClientIdentityRepository, ServiceClientIdentityRepository>();
        builder.Services.AddScoped<IServiceClientTenantGrantRepository, ServiceClientTenantGrantRepository>();
        builder.Services.AddScoped<IAuthAuditService, AuthAuditService>();
        builder.Services.AddServiceClientRegistry(builder.Configuration);
        builder.Services.AddApplication();
        builder.Services.AddServiceIdentityTokenIssuance(builder.Configuration);
        builder.Services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalServiceIdentityTokensController).Assembly));
            foreach (var provider in manager.FeatureProviders.OfType<ControllerFeatureProvider>().ToArray())
                manager.FeatureProviders.Remove(provider);
            manager.FeatureProviders.Add(new OnlyTheTokenEndpoint());
        });

        var app = builder.Build();
        app.UseRouting();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class OnlyTheTokenEndpoint : ControllerFeatureProvider
    {
        protected override bool IsController(TypeInfo typeInfo) =>
            typeInfo.AsType() == typeof(InternalServiceIdentityTokensController);
    }

    private sealed class TestServerClients(TestServer server) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => server.CreateClient();
    }
}
