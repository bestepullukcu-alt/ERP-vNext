using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.MdmService.Application.Tests;

// Uses the built owner-side Platform implementation, without adding a production or project-reference dependency.
// HTTP transport is in-process; credential, JWT, strict parser, exact-pair executor and MDM client are real.
public sealed class FirstGskuIdentityLifecycleAuthorizationTests
{
    [Theory]
    [InlineData("mdm.gskus.update")]
    [InlineData("mdm.gskus.submit")]
    [InlineData("mdm.gskus.withdraw")]
    [InlineData("mdm.gskus.request-correction")]
    [InlineData("mdm.gskus.request-retirement")]
    [InlineData("mdm.gskus.retire")]
    public async Task Enforced_scope_uses_real_client_and_owner_security_with_exact_permission(string permission)
    {
        var tenant = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = "gsku-scope-test",
                ValidateAudience = true, ValidAudience = "gsku-scope-test",
                ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        });
        await using var serviceProvider = services.BuildServiceProvider();
        using var transport = new OwnerTransport(serviceProvider, secret, permission);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject.ToString("D"))], "Bearer"));
        string Token(Guid tokenTenant, string tokenPermission, string actor = "tenant_user") =>
            new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer: "gsku-scope-test", audience: "gsku-scope-test",
                claims: [new("sub", subject.ToString("D")), new("tenant_id", tokenTenant.ToString("D")),
                    new("actor_type", actor), new("permission", tokenPermission)],
                notBefore: DateTime.UtcNow.AddSeconds(-5), expires: DateTime.UtcNow.AddMinutes(1),
                signingCredentials: new(key, SecurityAlgorithms.HmacSha256)));
        context.Request.Headers.Authorization = "Bearer " + Token(tenant, permission);
        using var http = new HttpClient(transport);
        var client = new PlatformTrustedLegalEntityScopeProviderClient(http,
            new HttpContextAccessor { HttpContext = context },
            Options.Create(new TrustedLegalEntityScopeProviderOptions
            {
                PlatformBaseAddress = new Uri("https://platform.test/"),
                Timeout = TimeSpan.FromSeconds(2), CredentialIdentifier = "test-mdm",
                CredentialSecret = secret
            }), TimeProvider.System);
        var tc = new Tenant(tenant);
        var prepared = ProductLegalEntityScopeTestFixture.Preparation(tc);
        var facade = new ProductLegalEntityScopeCandidateFacade(client,
            new InMemoryLegalEntityRepository(tenant, []), tc, new Actor(subject));
        var guard = new ProductLegalEntityScopeConsumerGuard(new Rollout(tenant),
            prepared.Policies, facade, tc);
        var accepted = await guard.ResolveContextAsync(permission);
        Assert.True(accepted.IsSuccessful, accepted.FailureCode + "; HTTP status=" + transport.LastStatus);
        Assert.Equal(ProductLegalEntityScopeRolloutMode.Enforced, accepted.Context!.RolloutMode);
        Assert.Empty(accepted.Context.EffectiveCandidateLegalEntityIds);
        Assert.Equal(permission, transport.LastPermission);
        Assert.Equal(1, transport.CandidateCalls);

        context.Request.Headers.Authorization = "Bearer " + Token(tenant, "mdm.gskus.read");
        var wrongPermission = await client.ResolveAsync(tenant, subject, "product-item-sku-master", permission);
        Assert.Equal(403, wrongPermission.StatusCode);
        context.Request.Headers.Authorization = "Bearer " + Token(tenant, permission, "service");
        Assert.Equal(403, (await client.ResolveAsync(tenant, subject, "product-item-sku-master", permission)).StatusCode);
        context.Request.Headers.Authorization = "Bearer " + Token(Guid.NewGuid(), permission);
        Assert.Equal(503, (await client.ResolveAsync(tenant, subject, "product-item-sku-master", permission)).StatusCode);
        Assert.Equal(2, transport.CandidateCalls); // Only the authenticated other tenant reached its own candidate stage.
    }

    private sealed class OwnerTransport : HttpMessageHandler
    {
        private readonly IServiceProvider services;
        private readonly object executor;
        private readonly MethodInfo execute;
        private readonly Delegate action;
        private readonly Func<AssemblyLoadContext, AssemblyName, Assembly?> resolver;
        public string? LastPermission { get; private set; }
        public int CandidateCalls { get; private set; }
        public int LastStatus { get; private set; }

        public OwnerTransport(IServiceProvider services, string secret, string permission)
        {
            this.services = services;
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
            Assert.NotNull(root);
            var bin = Path.Combine(root!.FullName, "services", "Diten.Platform", "src", "Diten.Platform.API",
                "bin", "Release", "net8.0");
            resolver = (context, name) =>
            {
                var path = Path.Combine(bin, name.Name + ".dll");
                return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            AssemblyLoadContext.Default.Resolving += resolver;
            var api = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "Diten.Platform.API.dll"));
            var optionsType = api.GetType("Diten.Platform.API.Configuration.TrustedLegalEntityScopeCredentialOptions", true)!;
            var options = Activator.CreateInstance(optionsType)!;
            var binding = optionsType.GetProperty("Mdm")!.GetValue(options)!;
            Set(binding, "Identifier", "test-mdm"); Set(binding, "ActiveSecret", secret);
            Set(binding, "ConsumerService", "DITENMDMSERVICE");
            Set(binding, "AllowedAudience", "TRUSTED_LEGAL_ENTITY_SCOPE_RESOLVE");
            var pairs = (System.Collections.IList)binding.GetType().GetProperty("AllowedPairs")!.GetValue(binding)!;
            pairs.Clear();
            var pair = Activator.CreateInstance(pairs.GetType().GetGenericArguments()[0])!;
            Set(pair, "ModuleCode", "product-item-sku-master"); Set(pair, "PermissionKey", permission); pairs.Add(pair);
            var wrapper = Activator.CreateInstance(typeof(OptionsWrapper<>).MakeGenericType(optionsType), options)!;
            var auth = Activator.CreateInstance(api.GetType("Diten.Platform.API.Security.TrustedLegalEntityScopeCredentialAuthenticator", true)!,
                wrapper, TimeProvider.System)!;
            var jwt = Activator.CreateInstance(api.GetType("Diten.Platform.API.Security.TrustedLegalEntityScopeJwtContext", true)!)!;
            var executorType = api.GetType("Diten.Platform.API.Security.TrustedLegalEntityScopeRequestExecutor", true)!;
            var tenantInterface = executorType.GetConstructors().Single().GetParameters()[2].ParameterType;
            var tenantType = tenantInterface.Assembly.GetType("Diten.Platform.Common.Tenancy.TenantContext", true)!;
            executor = Activator.CreateInstance(executorType, auth, jwt, Activator.CreateInstance(tenantType)!)!;
            execute = executorType.GetMethod("ExecuteAsync")!;
            var delegateType = execute.GetParameters()[2].ParameterType;
            action = GetType().GetMethod(nameof(Candidates), BindingFlags.Instance | BindingFlags.NonPublic)!
                .MakeGenericMethod(delegateType.GetGenericArguments()[2]).CreateDelegate(delegateType, this);
        }

        private Task<IActionResult> Candidates<T>(Guid tenant, Guid subject, T request, CancellationToken ct)
        {
            CandidateCalls++;
            LastPermission = (string)typeof(T).GetProperty("PermissionKey")!.GetValue(request)!;
            return Task.FromResult<IActionResult>(new ObjectResult(new
            {
                data = new { tenantId = tenant, subjectId = subject, moduleCode = "product-item-sku-master",
                    permissionKey = LastPermission, evaluatedAtUtc = DateTimeOffset.UtcNow, legalEntityIds = Array.Empty<Guid>() },
                statusCode = 200, isSuccessful = true, errors = Array.Empty<string>(),
                reason_code = (string?)null, correlation_id = "scope-contract-test"
            }) { StatusCode = 200 });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            try { return await SendCoreAsync(request, ct); }
            catch (Exception error) { throw new Xunit.Sdk.XunitException("Transport failure type: " + error.GetType().FullName + " stack: " + error.StackTrace); }
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("/api/internal/v1/access-governance/legal-entity-scope/resolve", request.RequestUri!.AbsolutePath);
            Assert.False(request.Headers.Contains("X-Tenant-Id"));
            using var scope = services.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Method = request.Method.Method;
            foreach (var header in request.Headers) context.Request.Headers[header.Key] = header.Value.ToArray();
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct);
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
            context.Request.Body = new MemoryStream(bytes);
            Func<int, string, IActionResult> failure = (status, code) =>
                new ObjectResult(new { data = (object?)null, statusCode = status, isSuccessful = false, errors = new[] { code },
                    reason_code = code, correlation_id = "scope-contract-test" })
                { StatusCode = status };
            var task = (Task<IActionResult>)execute.Invoke(executor, [context, ct, action, failure])!;
            IActionResult executed;
            try { executed = await task; }
            catch (Exception error) { throw new Xunit.Sdk.XunitException("Owner transport exception: " + error.GetType().FullName + " at " + error.StackTrace); }
            var result = Assert.IsType<ObjectResult>(executed);
            LastStatus = result.StatusCode!.Value;
            return new HttpResponseMessage((HttpStatusCode)result.StatusCode!)
            { Content = new StringContent(JsonSerializer.Serialize(result.Value), Encoding.UTF8, "application/json") };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) AssemblyLoadContext.Default.Resolving -= resolver;
            base.Dispose(disposing);
        }
        private static void Set(object instance, string name, object value) =>
            instance.GetType().GetProperty(name)!.SetValue(instance, value);
    }

    private sealed class Tenant(Guid id) : ITenantContext
    { public Guid TenantId { get; private set; } = id; public bool IsResolved => true; public void SetTenant(Guid value) => TenantId = value; }
    private sealed class Actor(Guid id) : IProductIdentityActorContext
    { public string ActorId => id.ToString("D"); }
    private sealed class Rollout(Guid tenant) : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken ct = default)
        {
            var state = ProductLegalEntityScopeRolloutState.CreatePreparation(tenant, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
            state.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
            return Task.FromResult<ProductLegalEntityScopeRolloutState?>(state);
        }
        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(ProductLegalEntityScopeRolloutState state, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(ProductLegalEntityScopeRolloutState state, int version, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
