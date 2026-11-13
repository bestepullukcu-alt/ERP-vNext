using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// Save View tenant scope — WHO sends X-Tenant-Id. Since 8e497c6f5 (2026-08-24) the browser's personalizationClient
/// calls the same-origin MVC proxy and sends NO tenant header (a header built from window.CurrentUser is
/// client-controlled); <see cref="PersonalizationProxyController"/> adds it from the authenticated tenant_id claim,
/// only for tenant_user actors. The DataTable verifier's rule still demanded the old browser header, so every list
/// page that ran it inherited one red. Measured on the production files: (1) the real personalization-client.js in
/// Node for both actor types, (2) the real proxy controller with a captured outbound request, (3) the verifier itself.
/// </summary>
public sealed class PersonalizationTenantScopeWebTests
{
    private const string TenantId = "74355e70-4c7d-410c-8cf6-db5fe3b9547f";

    [Theory]
    [InlineData("tenant_user")]
    [InlineData("platform_admin")]
    public void Client_never_sends_a_browser_tenant_header_and_keeps_scope_in_the_query(string actorType)
    {
        var js = Path.Combine(WebRoot(), "wwwroot", "assets", "js").Replace("\\", "/");
        var node = RunNode($$"""
            const fs = require('fs'), vm = require('vm');
            globalThis.window = { location: { hostname: 'app.local', pathname: '/CRM/Accounts', search: '', origin: 'http://app.local' },
                                  CurrentUser: { tenantId: '{{TenantId}}', actorType: '{{actorType}}' } };
            const calls = [];
            globalThis.fetch = async (url, options) => { calls.push({ url, headers: options.headers || {} });
              return { ok: true, status: 200, headers: { get: () => 'application/json' }, json: async () => [] }; };
            vm.runInThisContext(fs.readFileSync('{{js}}/shared/http-media-type.js', 'utf8'));
            vm.runInThisContext(fs.readFileSync('{{js}}/personalization-client.js', 'utf8'));
            (async () => {
              const c = window.personalizationClient;
              await c.getViews('CRM', 'Accounts');
              await c.saveView({ moduleKey: 'CRM', pageKey: 'Accounts', viewName: 'Default' });
              await c.updateView('v1', { moduleKey: 'CRM', pageKey: 'Accounts' });
              await c.deleteView('v1', 'CRM', 'Accounts');
              console.log(JSON.stringify(calls));
            })();
            """);
        if (node is null) return;

        var calls = JsonDocument.Parse(node).RootElement.EnumerateArray().ToArray();
        Assert.Equal(4, calls.Length);
        foreach (var call in calls)
        {
            var headers = call.GetProperty("headers").EnumerateObject().Select(h => h.Name).ToArray();
            Assert.DoesNotContain(headers, h => h.Equals("X-Tenant-Id", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("moduleKey=CRM", call.GetProperty("url").GetString());   // the gateway reads scope here
        }
    }

    [Fact]
    public async Task Proxy_adds_the_claimed_tenant_for_a_tenant_user()
    {
        var (result, sent) = await ForwardAsync("tenant_user", TenantId);

        Assert.IsType<ContentResult>(result);
        Assert.NotNull(sent);
        Assert.Equal(TenantId, Assert.Single(sent!.Headers.GetValues("X-Tenant-Id")));
        Assert.Equal("Bearer", sent.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task Proxy_omits_the_tenant_header_for_a_platform_actor()
    {
        var (result, sent) = await ForwardAsync("platform_admin", TenantId);

        Assert.IsType<ContentResult>(result);
        Assert.NotNull(sent);
        Assert.False(sent!.Headers.Contains("X-Tenant-Id"));
    }

    [Fact]
    public async Task Proxy_refuses_a_tenant_user_without_a_tenant_claim_and_calls_nothing()
    {
        var (result, sent) = await ForwardAsync("tenant_user", tenantClaim: null);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(sent);
    }

    [Fact]
    public void Verifier_tenant_scope_rule_agrees_with_the_code()
    {
        // SYNC-144 (main 3912d568b): the verifier's Save View tenant rule now lives in check_personalization_proxy_contract
        // and applies to the proxy profile — the same-origin MVC proxy every page's Save View goes through. The
        // direct-gateway profile keeps main's historical "client sends X-Tenant-Id" check (follow-up for the shared owner).
        var output = RunPython(".antigravity/scripts/verify_datatable_page.py", ".",
            "--area", "DevEnablement", "--module", "GoldenReferenceCompact", "--reference", "compact", "--format", "gaps",
            "--api-profile", "proxy");
        if (output is null) return;

        Assert.Contains("[factory]", output);   // the verifier ran to the end (a crash would print no gaps either)
        // no gap of main's personalization contract (its wording: "keeps tenant and bearer authority out of the browser",
        // "uses same-origin MVC proxy endpoint", the proxy's tenant_user header …) — any of them names personalization
        Assert.DoesNotContain("personalization", output, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(IActionResult Result, HttpRequestMessage? Sent)> ForwardAsync(string actorType, string? tenantClaim)
    {
        var handler = new CapturingHandler();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" })
            .Build();
        var controller = new PersonalizationProxyController(new HttpClient(handler), configuration,
            NullLogger<PersonalizationProxyController>.Instance);

        var claims = new List<Claim> { new("sub", "u1"), new("actor_type", actorType) };
        if (tenantClaim is not null) claims.Add(new Claim("tenant_id", tenantClaim));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        context.Request.Headers.Cookie = "access_token=token-1";
        context.Request.QueryString = new QueryString("?moduleKey=CRM&pageKey=Accounts");
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Get(CancellationToken.None);
        return (result, handler.Sent);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Sent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }

    private static string? RunNode(string code)
    {
        var temp = Path.Combine(Path.GetTempPath(), "pts-" + Guid.NewGuid().ToString("N") + ".js");
        File.WriteAllText(temp, code, new UTF8Encoding(false));
        try
        {
            return Run("node", "\"" + temp + "\"", workingDirectory: null);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static string? RunPython(params string[] args)
    {
        var repo = Path.GetDirectoryName(Path.GetDirectoryName(WebRoot()))!;
        var quoted = string.Join(' ', args.Select(a => "\"" + a + "\""));
        return Run("py", "-3 " + quoted, repo) ?? Run("python3", quoted, repo);
    }

    private static string? Run(string fileName, string arguments, string? workingDirectory)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8, WorkingDirectory = workingDirectory ?? ""
            });
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(60000);
            if (fileName == "node") Assert.True(process.ExitCode == 0, error);
            return output;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
