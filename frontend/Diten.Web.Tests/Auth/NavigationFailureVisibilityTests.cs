using System.Net;
using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Auth;

/*
 * WHEN THE NAVIGATION ENDPOINT FAILS, BOTH SURFACES EMPTY OUT — AND BOTH MUST SAY SO.
 *
 * Ctrl+K and the sidebar menu resolve from the SAME endpoint, /api/platform/navigation/menu. When the token
 * is stale the call 401s, the menu vanishes and the command palette goes blank at the same instant. The menu
 * logged that at Warning ("a disappearing menu is not a debug-level event", says the comment there); Ctrl+K
 * logged it at Debug, so in every environment where Debug is off the second half of the symptom left no
 * trace at all. The palette looked unhelpful rather than broken, which is why it went unreported for months.
 *
 * ⚠ WHAT THIS FILE DOES NOT DECIDE: what the USER sees. An empty palette with no explanation is a product
 * question — a message, a retry, a silent degrade — and it is open with CONTROL TOWER. This pins only that
 * the system says the same thing about the same event, at the same volume.
 */
public class NavigationFailureVisibilityTests
{
    [Fact]
    public async Task A_failed_navigation_call_is_logged_at_Warning_not_Debug()
    {
        /*
         * MUTATION GUARD: put LogDebug back in TenantSearchController's failure path and this goes red.
         */
        var logger = new CapturingLogger<TenantSearchController>();
        var controller = ControllerWith(HttpStatusCode.Unauthorized, logger);

        await controller.Data();

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Debug && e.Message.Contains("resolve"));

        // The message has to name the shared cause, or the reader has two unrelated-looking incidents.
        Assert.Contains(logger.Entries, e => e.Message.Contains("sidebar menu"));
    }

    [Fact]
    public async Task A_healthy_navigation_call_logs_no_warning_at_all()
    {
        // Non-vacuity: a guard that warns on every request teaches the reader to ignore the warning.
        var logger = new CapturingLogger<TenantSearchController>();
        var controller = ControllerWith(HttpStatusCode.OK, logger, body: """{"data":[]}""");

        await controller.Data();

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }

    // ── plumbing ───────────────────────────────────────────────────────────────────────────────────────────

    private static TenantSearchController ControllerWith(
        HttpStatusCode status,
        ILogger<TenantSearchController> logger,
        string body = "")
    {
        var httpClient = new HttpClient(new StubHandler(status, body))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:5000" })
            .Build();

        var controller = new TenantSearchController(
            httpClient,
            configuration,
            new PermitEverything(),
            new PassThroughNavLocalizer(),
            logger);

        var context = new DefaultHttpContext();
        // A token has to be present, or the controller refuses before it ever reaches the call being tested.
        context.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}=A-TOKEN";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class PermitEverything : Diten.Web.Services.IPermissionSnapshot
    {
        public bool Has(string permissionKey) => true;
        public IReadOnlyCollection<string> Keys => Array.Empty<string>();
    }

    private sealed class PassThroughNavLocalizer : Diten.Web.Services.Navigation.INavNameLocalizer
    {
        public string Domain(string? domainCode, string serverName, bool isOverride) => serverName;
        public string Module(string? moduleCode, string serverName, bool isOverride) => serverName;
        public string Page(string? pageCode, string serverName) => serverName;
    }
}
