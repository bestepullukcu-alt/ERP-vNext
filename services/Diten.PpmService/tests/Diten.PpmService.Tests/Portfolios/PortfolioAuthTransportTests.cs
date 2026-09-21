using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.Portfolios;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.PpmService.Tests.Portfolios;

public sealed class PortfolioAuthTransportTests
{
    [Fact]
    public async Task Factory_concurrent_request_scopes_reuse_handler_without_mixing_saved_credentials()
    {
        var seen = new ConcurrentBag<(string? Token, string Tenant)>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;
        var handlers = 0;
        var handler = new CallbackHandler(async request =>
        {
            seen.Add((request.Headers.Authorization?.Parameter, request.Headers.GetValues("X-Tenant-Id").Single()));
            if (Interlocked.Increment(ref arrivals) == 2) release.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return EmptyCandidates();
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
        services.AddInfrastructure();
        services.AddSingleton<IOptions<PortfolioAuthorityOptions>>(Options.Create(TestOptions()));
        services.AddScoped<IAuthenticationService, TicketAuthentication>();
        services.AddHttpClient<PortfolioAuthorityClient>().ConfigurePrimaryHttpMessageHandler(() =>
        { Interlocked.Increment(ref handlers); return handler; });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var actors = Enumerable.Range(0, 2).Select(_ => (Tenant: Guid.NewGuid(), Actor: Guid.NewGuid(), Token: Guid.NewGuid().ToString("N"))).ToArray();

        await Task.WhenAll(actors.Select(async actor =>
        {
            using var scope = provider.CreateScope();
            var context = Context(actor.Tenant, actor.Actor, actor.Token, scope.ServiceProvider);
            var accessor = provider.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext = context;
            try
            {
                var client = scope.ServiceProvider.GetRequiredService<PortfolioAuthorityClient>();
                var result = await client.CandidatesAsync(Scope(actor.Tenant, actor.Actor), default);
                Assert.Equal(PortfolioAuthorityOutcome.Allowed, result.Authority.Outcome);
            }
            finally { accessor.HttpContext = null; }
        }));

        Assert.Equal(1, handlers);
        Assert.Equal(2, seen.Count);
        foreach (var actor in actors)
            Assert.Single(seen, x => x.Token == actor.Token && x.Tenant == actor.Tenant.ToString("D"));
        using var factoryClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(PortfolioAuthorityClient));
        Assert.Null(factoryClient.DefaultRequestHeaders.Authorization);
        Assert.False(factoryClient.DefaultRequestHeaders.Contains("X-Tenant-Id"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("failed")]
    [InlineData("cookie")]
    [InlineData("saved-token-missing")]
    [InlineData("actor")]
    [InlineData("tenant")]
    [InlineData("record-tenant")]
    [InlineData("header")]
    [InlineData("conflicting-actor")]
    [InlineData("conflicting-tenant")]
    [InlineData("ticket-principal")]
    [InlineData("trusted-actor")]
    [InlineData("trusted-tenant")]
    [InlineData("missing-header")]
    [InlineData("multiple-headers")]
    [InlineData("unauthenticated-user")]
    [InlineData("platform-actor")]
    [InlineData("platform-tenant")]
    public async Task Missing_authentication_or_mismatched_scope_never_sends(string mode)
    {
        var tenant = mode == "platform-tenant" ? Guid.Parse("00000000-0000-0000-0000-000000000001") : Guid.NewGuid();
        var actor = Guid.NewGuid(); var calls = 0;
        var context = Context(tenant, actor, mode == "saved-token-missing" ? null : "saved-test-token");
        context.Request.Headers.Authorization = "Bearer untrusted-raw-test-token";
        var scope = Scope(tenant, actor);
        if (mode is "missing" or "failed") context.Items["result"] = mode == "missing" ? AuthenticateResult.NoResult() : AuthenticateResult.Fail("test failure");
        if (mode == "cookie") context.Items["scheme"] = "Cookies";
        if (mode == "actor") scope = scope with { ActorId = Guid.NewGuid(), CreatorId = Guid.NewGuid() };
        if (mode == "tenant") scope = scope with { TenantId = Guid.NewGuid() };
        if (mode == "record-tenant") scope = scope with { RecordTenantId = Guid.NewGuid() };
        if (mode == "header") context.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        if (mode == "conflicting-actor") ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString("D")));
        if (mode == "conflicting-tenant") ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim("tenant_id", Guid.NewGuid().ToString("D")));
        if (mode == "ticket-principal") context.Items["ticket-user"] = Principal(Guid.NewGuid(), Guid.NewGuid());
        if (mode == "missing-header") context.Request.Headers.Remove("X-Tenant-Id");
        if (mode == "multiple-headers") context.Request.Headers.Append("X-Tenant-Id", tenant.ToString("D"));
        if (mode == "unauthenticated-user") context.User = new ClaimsPrincipal(new ClaimsIdentity(context.User.Claims));
        if (mode == "platform-actor") ((ClaimsIdentity)context.User.Identity!).AddClaim(new Claim("actor_type", "platform_admin"));
        var trustedContext = new TrustedContext(mode == "trusted-tenant" ? Guid.NewGuid() : tenant, mode == "trusted-actor" ? Guid.NewGuid() : actor);
        var options = Options.Create(TestOptions());
        var client = new PortfolioAuthorityClient(new HttpClient(new CallbackHandler(_ => { calls++; return Task.FromResult(EmptyCandidates()); })), options,
            new PortfolioAuthRequestContext(new FixedHttpContextAccessor(context), trustedContext, trustedContext),
            new PortfolioAuthTrustedTarget(options, new TestEnvironment()));

        var result = await client.CandidatesAsync(scope, default);

        Assert.NotEqual(PortfolioAuthorityOutcome.Allowed, result.Authority.Outcome);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("http://auth.test")]
    [InlineData("https://user:password@auth.test")]
    [InlineData("https://auth.test/path")]
    [InlineData("https://auth.test?target=other")]
    [InlineData("https://auth.test#fragment")]
    public void Untrusted_origin_is_rejected(string origin)
    {
        var options = TestOptions(origin: origin);
        Assert.False(PortfolioAuthTrustedTarget.IsConfigurationValid(options));
    }

    [Fact]
    public void Default_disabled_production_disabled_and_only_three_routes_are_allowed()
    {
        Assert.False(new PortfolioAuthTrustedTarget(Options.Create(new PortfolioAuthorityOptions()), new TestEnvironment()).IsEnabled);
        Assert.False(new PortfolioAuthTrustedTarget(Options.Create(TestOptions()), new TestEnvironment { EnvironmentName = "Production" }).IsEnabled);
        var target = new PortfolioAuthTrustedTarget(Options.Create(TestOptions()), new TestEnvironment());
        Assert.True(target.TryCreateRequestUri("api/users/lookup?search=A&limit=5", out _));
        Assert.True(target.TryCreateRequestUri($"api/users/{Guid.NewGuid():D}/account-assertion", out _));
        Assert.True(target.TryCreateRequestUri($"api/users/{Guid.NewGuid():D}/display-label", out _));
        foreach (var path in new[] { "https://evil.test/api/users/lookup", "//evil.test/api/users/lookup", "api/users/login", "api/users/../login", "api/users/lookup#fragment" })
            Assert.False(target.TryCreateRequestUri(path, out _));
        using var handler = PortfolioAuthTrustedTarget.CreateHttpHandler();
        Assert.False(handler.AllowAutoRedirect); Assert.False(handler.UseProxy); Assert.False(handler.UseCookies);
        Assert.True(handler.CheckCertificateRevocationList);
        Assert.Null(handler.ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public async Task Successful_ticket_saved_token_is_forwarded_instead_of_contradictory_raw_header()
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var calls = 0;
        var context = Context(tenant, actor, "authenticated-saved-test-token");
        context.Request.Headers.Authorization = "Bearer poisoned-raw-test-token";
        var trusted = new TrustedContext(tenant, actor);
        var options = Options.Create(TestOptions());
        using var http = new HttpClient(new CallbackHandler(request =>
        {
            calls++;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("authenticated-saved-test-token", request.Headers.Authorization?.Parameter);
            Assert.Equal(tenant.ToString("D"), Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
            Assert.Equal("https://auth.test/api/users/lookup?search=&limit=5", request.RequestUri!.AbsoluteUri);
            return Task.FromResult(EmptyCandidates());
        }));
        var client = new PortfolioAuthorityClient(http, options,
            new PortfolioAuthRequestContext(new FixedHttpContextAccessor(context), trusted, trusted),
            new PortfolioAuthTrustedTarget(options, new TestEnvironment()));

        var result = await client.CandidatesAsync(Scope(tenant, actor), default);

        Assert.Equal(PortfolioAuthorityOutcome.Allowed, result.Authority.Outcome);
        Assert.Equal(1, calls);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
        Assert.False(http.DefaultRequestHeaders.Contains("X-Tenant-Id"));
    }

    [Theory]
    [InlineData("default-off")]
    [InlineData("production")]
    [InlineData("unknown-environment")]
    [InlineData("missing-environment")]
    [InlineData("missing-approval")]
    [InlineData("missing-owner")]
    [InlineData("missing-origin")]
    [InlineData("http-origin")]
    [InlineData("userinfo-origin")]
    [InlineData("path-origin")]
    public async Task Disabled_or_unapproved_transport_never_sends_even_with_valid_ticket(string mode)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid(); var calls = 0;
        var options = Options.Create(mode switch
        {
            "default-off" => new PortfolioAuthorityOptions(),
            "missing-approval" => TestOptions(approval: null),
            "missing-owner" => TestOptions(owner: null),
            "missing-origin" => TestOptions(origin: null),
            "http-origin" => TestOptions(origin: "http://auth.test"),
            "userinfo-origin" => TestOptions(origin: "https://user:password@auth.test"),
            "path-origin" => TestOptions(origin: "https://auth.test/other"),
            _ => TestOptions()
        });
        var environment = mode == "missing-environment" ? null : new TestEnvironment
        {
            EnvironmentName = mode switch { "production" => "Production", "unknown-environment" => "Unknown", _ => "TransportTest" }
        };
        var trusted = new TrustedContext(tenant, actor);
        using var http = new HttpClient(new CallbackHandler(_ => { calls++; return Task.FromResult(EmptyCandidates()); }));
        var client = new PortfolioAuthorityClient(http, options,
            new PortfolioAuthRequestContext(new FixedHttpContextAccessor(Context(tenant, actor, "saved-test-token")), trusted, trusted),
            new PortfolioAuthTrustedTarget(options, environment));

        var result = await client.CandidatesAsync(Scope(tenant, actor), default);

        Assert.Equal(PortfolioAuthorityOutcome.Unavailable, result.Authority.Outcome);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Enabled_invalid_profile_is_rejected_by_startup_validation()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{PortfolioAuthorityOptions.SectionName}:Enabled"] = "true",
            [$"{PortfolioAuthorityOptions.SectionName}:ApprovedAuthOrigin"] = "https://auth.test",
            [$"{PortfolioAuthorityOptions.SectionName}:NonProductionEnvironmentName"] = "TransportTest"
            // The trust profile owner and approval reference are deliberately absent.
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    internal static PortfolioAuthorityClient CreateClient(HttpMessageHandler handler, Guid tenant, Guid actor)
    {
        var options = Options.Create(TestOptions());
        return new(new HttpClient(handler), options,
            new PortfolioAuthRequestContext(new FixedHttpContextAccessor(Context(tenant, actor, "saved-test-token")), new TrustedContext(tenant, actor), new TrustedContext(tenant, actor)),
            new PortfolioAuthTrustedTarget(options, new TestEnvironment()));
    }

    private static PortfolioAuthorityOptions TestOptions(string? origin = "https://auth.test", string? owner = "isolated-test",
        string? approval = "run-owned-test-profile") => new()
    {
        Enabled = true,
        TimeoutSeconds = 10,
        ApprovedAuthOrigin = origin,
        NonProductionEnvironmentName = "TransportTest",
        TrustProfileOwner = owner,
        TrustProfileApprovalReference = approval
    };
    internal static PortfolioAuthorityScope Scope(Guid tenant, Guid actor)
    {
        var id = Guid.NewGuid();
        return new(tenant, actor, id, "owner-candidates", 1, Search: "", Limit: 5, RecordTenantId: tenant, CreatorId: actor,
            LifecycleState: Diten.PpmService.Domain.Entities.PortfolioLifecycleState.Draft,
            TemporaryNonProductionAccessBinding: new PortfolioTemporaryNonProductionRecordAccessAuthority(true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction).CreateBinding(id));
    }
    private static ClaimsPrincipal Principal(Guid tenant, Guid actor) => new(new ClaimsIdentity(new[] { new Claim("sub", actor.ToString("D")), new Claim("tenant_id", tenant.ToString("D")) }, "Bearer"));
    private static DefaultHttpContext Context(Guid tenant, Guid actor, string? token, IServiceProvider? services = null)
    {
        var context = new DefaultHttpContext { User = Principal(tenant, actor), RequestServices = services ?? new ServiceCollection().AddSingleton<IAuthenticationService, TicketAuthentication>().BuildServiceProvider() };
        context.Request.Headers["X-Tenant-Id"] = tenant.ToString("D"); context.Items["token"] = token;
        return context;
    }
    private static HttpResponseMessage EmptyCandidates() => new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[],\"statusCode\":200,\"isSuccessful\":true}") };
    private sealed class FixedHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = context;
    }
    private sealed record TrustedContext(Guid TenantId, Guid ActorId) : ITenantContext, ICurrentActorContext;
    private sealed class CallbackHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => callback(request); }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "TransportTest";
        public string ApplicationName { get; set; } = "PPM isolated transport tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    // Deliberate test ticket seam, not proof of the deployed PPM Bearer pipeline.
    private sealed class TicketAuthentication : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            Assert.Equal("Bearer", scheme);
            if (context.Items["result"] is AuthenticateResult result) return Task.FromResult(result);
            var properties = new AuthenticationProperties();
            if (context.Items["token"] is string token) properties.StoreTokens(new[] { new AuthenticationToken { Name = "access_token", Value = token } });
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(context.Items["ticket-user"] as ClaimsPrincipal ?? context.User, properties, context.Items["scheme"] as string ?? "Bearer")));
        }
        public Task ChallengeAsync(HttpContext c, string? s, AuthenticationProperties? p) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext c, string? s, AuthenticationProperties? p) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext c, string? s, ClaimsPrincipal u, AuthenticationProperties? p) => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext c, string? s, AuthenticationProperties? p) => throw new NotSupportedException();
    }
}
