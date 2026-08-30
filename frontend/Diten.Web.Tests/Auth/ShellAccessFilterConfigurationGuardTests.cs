using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Diten.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// <c>ShellAccessFilter</c> is a GLOBAL MVC authorization filter (Program.cs) and the only thing in Diten.Web
/// that puts a VERIFIED principal on <c>HttpContext.User</c>. It used to answer a missing JWT configuration with
/// a bare <c>return;</c> — leaving <c>User</c> untouched, writing nothing anywhere, and skipping token
/// verification entirely. The app happened to stay closed (nothing else signs a principal in, so <c>User</c>
/// stayed anonymous and /Platform/* redirected to login) but that was an ACCIDENT of the surrounding code, not a
/// decision, and it left no trace for anyone to notice.
///
/// <para>These tests pin the replacement: configuration is checked ONCE at startup (<see
/// cref="ShellAccessFilter.ValidateConfiguration"/>, called from Program.cs before <c>builder.Build()</c>) so a
/// deployment missing a key never boots, and the residual per-request branch — reachable only if configuration
/// is emptied under a running process, since appsettings.json reloads on change — fails LOUD and CLOSED.</para>
/// </summary>
public sealed class ShellAccessFilterConfigurationGuardTests
{
    private const string Secret = "shell-access-filter-configuration-guard-signing-secret-hs256-ok";
    private const string OtherSecret = "a-completely-different-signing-secret-that-must-not-verify-hs256";
    private const string Issuer = "diten-auth-service";
    private const string Audience = "diten-erp";

    // ── (a) complete configuration → the token is VERIFIED and User is filled ────────────────────────────

    [Fact]
    public void A_complete_configuration_verifies_the_token_and_fills_User()
    {
        var result = RunFilter(Configuration(), TokenSignedWith(Secret));

        Assert.True(result.User.Identity?.IsAuthenticated);
        Assert.Equal("tenant_user", result.User.FindFirst("actor_type")?.Value);
        Assert.Equal("platform.tenants.view", result.User.FindFirst("permission")?.Value);
    }

    // ── (c) CONTROL: (a) is not green because the filter accepts everything ──────────────────────────────

    [Fact]
    public void CONTROL_a_token_signed_with_a_DIFFERENT_key_leaves_User_anonymous()
    {
        /*
         * Without this, (a) would pass just as happily against a filter that skipped verification and trusted
         * the payload. Same complete configuration, same claims — only the signature is wrong.
         */
        var result = RunFilter(Configuration(), TokenSignedWith(OtherSecret));

        Assert.NotEqual(true, result.User.Identity?.IsAuthenticated);
        Assert.Null(result.User.FindFirst("actor_type"));
    }

    // ── (b) missing configuration → LOUD and CLOSED, never a silent skip ─────────────────────────────────

    [Theory]
    [InlineData("JwtSettings:Secret")]
    [InlineData("JwtSettings:Issuer")]
    [InlineData("JwtSettings:Audience")]
    public void A_missing_configuration_key_blanks_User_and_LOGS_which_key_is_missing(string missingKey)
    {
        /*
         * ⚠ RED BEFORE THE FIX, on both assertions.
         *
         * The seeded principal is the shape the old `return;` was invisible in: authenticated, but WITHOUT the
         * actor_type claim, so the early-exit at the top of EnsureJwtCookiePrincipal does not fire and the
         * config branch really is reached. Old code returned and left this principal standing — a principal the
         * filter had not verified. New code blanks it, because a filter that cannot verify must not vouch.
         *
         * The log is the other half: the old skip wrote nothing at all, so a misconfigured deployment looked
         * exactly like a healthy one.
         */
        var configuration = Configuration(omit: missingKey);
        var logger = new RecordingLogger();

        var result = RunFilter(
            configuration,
            TokenSignedWith(Secret),
            logger,
            seedUser: new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "u1") }, "SeededCookie")));

        Assert.NotEqual(true, result.User.Identity?.IsAuthenticated);

        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains(missingKey, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_missing_key_log_never_carries_the_secret_or_its_length()
    {
        // The whole point of removing the debug line: nothing about the secret's VALUE — including how long it
        // is — may reach a log sink at any level. Only key NAMES are reportable.
        var logger = new RecordingLogger();

        RunFilter(Configuration(omit: "JwtSettings:Issuer"), TokenSignedWith(Secret), logger);

        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain(Secret, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret.Length.ToString(), entry.Message, StringComparison.Ordinal);
        }
    }

    // ── validation failure is EXPECTED traffic, so it is a warning, not an error ─────────────────────────

    [Fact]
    public void A_rejected_token_is_logged_as_a_WARNING_not_an_error()
    {
        // An expired or foreign token is normal for a public-facing shell. Only a broken DEPLOYMENT is an error.
        var logger = new RecordingLogger();

        RunFilter(Configuration(), TokenSignedWith(OtherSecret), logger);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    // ── the startup gate ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Program_calls_the_guard_BEFORE_it_builds_the_host()
    {
        /*
         * ⚠ RED BEFORE THE FIX — the call did not exist. This is what makes the startup path real rather than a
         * validator nobody invokes, and "before builder.Build()" is the part that matters: after Build the host
         * is already alive and the point of refusing to start is gone.
         *
         * ⚠ MEASURED, not assumed: booting the real host through WebApplicationFactory with a blanked setting
         * does NOT work here. Diten.Web uses minimal hosting (WebApplication.CreateBuilder), and the factory's
         * ConfigureAppConfiguration callbacks are applied during builder.Build() — too late for anything read
         * before it. Blanking JwtSettings:Secret, which the pre-existing ValidateRequiredSecrets check would
         * certainly reject, produced no exception at all, which is how that limitation was confirmed.
         *
         * The live control that the guard does not wrongly refuse a HEALTHY configuration is already in this
         * assembly: TaskDependencyProxyRouteTests and TaskRecurrenceRuleScreenTests boot the real host through
         * WebApplicationFactory<Program> with real settings, so a guard that threw unconditionally would take
         * both classes down with it.
         */
        var program = File.ReadAllText(SourcePath("Program.cs"));

        var guardCall = program.IndexOf(
            $"{nameof(ShellAccessFilter)}.{nameof(ShellAccessFilter.ValidateConfiguration)}(builder.Configuration)",
            StringComparison.Ordinal);
        var hostBuild = program.IndexOf("builder.Build()", StringComparison.Ordinal);

        Assert.True(guardCall >= 0, "Program.cs must call ShellAccessFilter.ValidateConfiguration(builder.Configuration).");
        Assert.True(hostBuild >= 0, "Program.cs no longer calls builder.Build() — this guard needs rewriting.");
        Assert.True(guardCall < hostBuild, "ShellAccessFilter.ValidateConfiguration must run BEFORE builder.Build().");
    }

    [Theory]
    [InlineData("JwtSettings:Secret")]
    [InlineData("JwtSettings:Issuer")]
    [InlineData("JwtSettings:Audience")]
    public void ValidateConfiguration_names_the_key_that_is_missing(string missingKey)
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => ShellAccessFilter.ValidateConfiguration(Configuration(omit: missingKey)));

        Assert.Contains(missingKey, thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateConfiguration_names_EVERY_missing_key_not_just_the_first()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => ShellAccessFilter.ValidateConfiguration(new ConfigurationBuilder().Build()));

        Assert.Contains("JwtSettings:Secret", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("JwtSettings:Issuer", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("JwtSettings:Audience", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateConfiguration_never_puts_the_secret_in_the_message()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => ShellAccessFilter.ValidateConfiguration(Configuration(omit: "JwtSettings:Issuer")));

        Assert.DoesNotContain(Secret, thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret.Length.ToString(), thrown.Message, StringComparison.Ordinal);
    }

    // ── (d) the debug line stays deleted ─────────────────────────────────────────────────────────────────

    [Fact]
    public void The_filter_source_never_writes_the_secret_length_and_never_uses_Console()
    {
        /*
         * ⚠ RED BEFORE THE FIX. The old line ran on EVERY request:
         *     Console.WriteLine($"[SHELL_FILTER_DEBUG] Secret length: {jwtSecret.Length}, Issuer: '{...}' ...")
         * A source guard rather than a behavioural one on purpose — Console.WriteLine goes to stdout, not to any
         * ILogger a test can observe, so the only way to keep it from coming back is to look at the file.
         */
        var source = File.ReadAllText(SourcePath("Filters", "ShellAccessFilter.cs"));

        Assert.DoesNotContain("Secret length", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Console.Write", source, StringComparison.Ordinal);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────────────────

    private static string TokenSignedWith(string signingSecret)
    {
        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            new[]
            {
                new Claim("actor_type", "tenant_user"),
                new Claim("permission", "platform.tenants.view")
            },
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IConfiguration Configuration(string? omit = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = Secret,
            ["JwtSettings:Issuer"] = Issuer,
            ["JwtSettings:Audience"] = Audience
        };

        if (omit is not null)
        {
            // Empty, not absent: this is exactly how appsettings.json ships the Secret slot.
            values[omit] = string.Empty;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>
    /// Runs the REAL filter over a request carrying <paramref name="token"/> and hands back the HttpContext it
    /// leaves behind. Nothing here re-implements validation.
    /// </summary>
    private static HttpContext RunFilter(
        IConfiguration configuration,
        string token,
        RecordingLogger? logger = null,
        ClaimsPrincipal? seedUser = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/Platform/Tenants";
        http.Request.Cookies = new FakeCookies(new Dictionary<string, string> { ["access_token"] = token });
        if (seedUser is not null)
        {
            http.User = seedUser;
        }

        new ShellAccessFilter(configuration, logger ?? new RecordingLogger()).OnAuthorization(
            new AuthorizationFilterContext(
                new ActionContext(http, new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>()));

        return http;
    }

    /// <summary>Resolves a file under frontend/Diten.Web by walking up from the test output directory.</summary>
    private static string SourcePath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                new[] { dir.FullName, "frontend", "Diten.Web" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate frontend/Diten.Web/{string.Join('/', relativeParts)} from the test output directory.");
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class RecordingLogger : ILogger<ShellAccessFilter>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed class FakeCookies : IRequestCookieCollection
    {
        private readonly IDictionary<string, string> _values;

        public FakeCookies(IDictionary<string, string> values) => _values = values;

        public string? this[string key] => _values.TryGetValue(key, out var value) ? value : null;

        public int Count => _values.Count;

        public ICollection<string> Keys => _values.Keys;

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public bool TryGetValue(string key, out string? value)
        {
            var found = _values.TryGetValue(key, out var raw);
            value = raw;
            return found;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
