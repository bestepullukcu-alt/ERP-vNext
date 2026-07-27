using Diten.Web;
using Diten.BuildingBlocks.Security.Secrets;
using Diten.Web.Filters;
using Diten.Web.Services.Auth;
using Diten.Web.Services.EnterpriseStrategy;
using Diten.Web.Services.ManagementGovernance;
using Diten.Web.Services.WorkCenter;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    })
    .AddRazorOptions(options =>
    {
        options.ViewLocationFormats.Add("/Views/MDM/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/Platform/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/Organization/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/MasterData/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/Governance/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/{1}/{0}.cshtml");
        options.ViewLocationFormats.Add("/Views/Archive/{1}/{0}.cshtml");
    });

builder.Services.Configure<MvcOptions>(options =>
{
    options.Filters.Add<ShellAccessFilter>();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "auth_ticket"; // Use a separate cookie for ASP.NET state if needed
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
    });

var authServiceUrl = builder.Configuration["GatewayUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient<IAuthGateway, AuthGateway>(client =>
{
    client.BaseAddress = new Uri(authServiceUrl);
});
// Pre-auth tenant branding lookup for the login screen. Targets the Platform service DIRECTLY
// (the internal branding endpoint is not exposed through the gateway), authenticated with the
// shared internal API key. Best-effort: failures fall back to platform default branding.
var platformServiceUrl = builder.Configuration["PlatformServiceUrl"] ?? "http://localhost:5057";
builder.Services.AddHttpClient<Diten.Web.Services.Branding.IBrandingGateway, Diten.Web.Services.Branding.BrandingGateway>(client =>
{
    client.BaseAddress = new Uri(platformServiceUrl);
});
// FIX-4: per-request tenant liveness lookup for the shell session guard (deleted/suspended tenant → sign-out).
// Same Platform target + shared internal API key; best-effort/fail-open and short-cached (~30s) in the gateway.
builder.Services.AddHttpClient<Diten.Web.Services.TenantStatus.ITenantStatusGateway, Diten.Web.Services.TenantStatus.TenantStatusGateway>(client =>
{
    client.BaseAddress = new Uri(platformServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
// Vanity slug → tenant login redirect (e.g. http://<host>/gmg → /account/login?tenantId=...).
// Targets the Platform service DIRECTLY with the shared internal API key (same pattern as branding).
builder.Services.AddHttpClient<Diten.Web.Services.TenantResolution.ITenantSlugResolver, Diten.Web.Services.TenantResolution.TenantSlugResolver>(client =>
{
    client.BaseAddress = new Uri(platformServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
// FE-A-harden (A5): the default HttpClient is registered TRANSIENT — each scoped (per-request)
// controller resolves its own instance and makes only sequential calls, so a controller that mutates
// HttpClient.DefaultRequestHeaders.Authorization affects only its own request and cannot bleed a token
// across requests. Keep this registration transient; do NOT make HttpClient a singleton. New code
// should prefer the per-request HttpRequestMessage pattern (see GoldenReferenceSlimController /
// PlatformAuditController) so the guarantee holds even if this ever changes.
builder.Services.AddHttpClient();
builder.Services.AddScoped<Diten.Web.Services.IPlatformProfileSnapshotProvider, Diten.Web.Services.PlatformProfileSnapshotProvider>();
// FE-B (MOD-0018-FU9): UX-only permission snapshot for tenant RBAC screens. Not enforcement.
builder.Services.AddScoped<Diten.Web.Services.IPermissionSnapshot, Diten.Web.Services.PermissionSnapshot>();
// FEAT-NAV-L10N — generic code→resx localization for the tenant nav (sidebar + Ctrl+K).
builder.Services.AddScoped<Diten.Web.Services.Navigation.INavNameLocalizer, Diten.Web.Services.Navigation.NavNameLocalizer>();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddSingleton<ITaskDetailService, TaskDetailService>();
builder.Services.AddScoped<IManagementGovernanceFrontendAdapter, MockManagementGovernanceFrontendAdapter>();
builder.Services.AddScoped<IEnterpriseStrategyFrontendAdapter, MockEnterpriseStrategyFrontendAdapter>();
// FE-A-harden (A4): trust forwarded scheme/host/ip from the TLS-terminating reverse proxy.
// KnownNetworks/Proxies cleared: the proxy topology is deployment-managed (containerized) and not a
// fixed IP, so the forwarded headers from the trusted edge are honoured.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSecretsProvider(builder.Configuration, builder.Environment, options => options.ServiceName = "Diten.Web");
builder.Services.ValidateRequiredSecrets(builder.Configuration, builder.Environment, "Diten.Web", [
    new("JwtSettings:Secret", "Diten.Web", SecretRequirementKind.JwtCurrent),
    new("JwtSettings:PreviousSecrets", "Diten.Web", SecretRequirementKind.JwtPreviousCollection, Required: false),
    new("ConnectionStrings:MongoDb", "Diten.Web", SecretRequirementKind.ConnectionString, Required: false)
]);

var app = builder.Build();

// FE-A-harden (A4): honour X-Forwarded-For / X-Forwarded-Proto from the TLS-terminating proxy so the
// request scheme (https) and client IP resolve correctly — required for Secure cookies to be emitted
// behind a reverse proxy. Must run before any middleware that reads the scheme / sets cookies.
app.UseForwardedHeaders();

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "Data", "uploads"));

var supportedCultures = new[] { "en", "fr", "es", "zh", "ar", "ru", "tr" };
var supportedCultureSet = new HashSet<string>(supportedCultures, StringComparer.OrdinalIgnoreCase);
var platformSupportedCultures = new[] { "en", "tr" };
var platformCultureSet = new HashSet<string>(platformSupportedCultures, StringComparer.OrdinalIgnoreCase);
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.RequestCultureProviders.Insert(0, new CustomRequestCultureProvider(context =>
{
    var requestHost = context.Request.Host.Host ?? string.Empty;
    var isPlatformContext = requestHost.StartsWith("admin.", StringComparison.OrdinalIgnoreCase) ||
                            context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase);
    var cultureSet = IsReferenceDataTenantPath(context.Request.Path) ? supportedCultureSet : platformCultureSet;
    if (!isPlatformContext)
    {
        return Task.FromResult<ProviderCultureResult?>(null);
    }

    var requestedCulture = context.Request.Query["culture"].ToString();
    if (!string.IsNullOrWhiteSpace(requestedCulture))
    {
        var normalizedCulture = cultureSet.Contains(requestedCulture) ? requestedCulture : "en";
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(normalizedCulture, normalizedCulture));
    }

    var cultureCookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
    var cookieCulture = !string.IsNullOrWhiteSpace(cultureCookie)
        ? CookieRequestCultureProvider.ParseCookieValue(cultureCookie)?.Cultures.FirstOrDefault().Value
        : null;
    var resolvedCulture = !string.IsNullOrWhiteSpace(cookieCulture) && cultureSet.Contains(cookieCulture)
        ? cookieCulture
        : "en";

    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(resolvedCulture, resolvedCulture));
}));

app.UseRequestLocalization(localizationOptions);

// Persist culture in session/cookie for subsequent requests (e.g., POST)
app.Use(async (context, next) =>
{
    var culture = context.Request.Query["culture"];
    if (!string.IsNullOrEmpty(culture))
    {
        var requestedCulture = culture.ToString();
        var requestHost = context.Request.Host.Host ?? string.Empty;
        var isPlatformContext = requestHost.StartsWith("admin.", StringComparison.OrdinalIgnoreCase) ||
                                context.Request.Path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase);
        var allowedSet = isPlatformContext && !IsReferenceDataTenantPath(context.Request.Path)
            ? platformCultureSet
            : supportedCultureSet;
        var normalizedCulture = allowedSet.Contains(requestedCulture) ? requestedCulture : "en";
        var cookieValue = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture));
        context.Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookieValue, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
    }
    await next();
});

var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? string.Empty;
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? string.Empty;
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? string.Empty;
var jwtRotationResolver = new JwtSecretRotationResolver(builder.Configuration);

var validatedTokenParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtIssuer,
    ValidAudience = jwtAudience,
    IssuerSigningKeys = jwtRotationResolver.GetValidationKeys(),
    ClockSkew = TimeSpan.FromSeconds(30)
};

// FE-A-harden (A3): single-flight the eager refresh, and MOD-0014's token→User bridge. Both live in
// Services/Auth/TokenBridge.cs so they can be tested — see that file for why the second pass must never
// re-read the request token (it undid the refresh and logged the session out).
// One instance per app: the in-flight refresh map has to be shared across requests to be single-flight at all.
// The logger is diagnostic only: the three refresh outcomes were all silent, so a session that dropped left no
// evidence behind. Nothing about the bridge's behaviour depends on it.
var tokenBridge = new TokenBridge(logger: app.Services.GetRequiredService<ILogger<TokenBridge>>());

// MOD-0014 pass 1: validate, and refresh an expired token. Owns every cookie decision.
app.Use(async (context, next) =>
{
    if (!ShouldSkipTokenBridgeRefresh(context.Request.Path))
    {
        await tokenBridge.AuthenticateAsync(
            context,
            validatedTokenParameters,
            context.RequestServices.GetRequiredService<IAuthGateway>(),
            context.RequestServices.GetRequiredService<IAuthCookieService>(),
            TryReadTenantId);
    }

    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

// FE-A-core: friendly 401/403/404 pages, re-executed through HomeController.Status ([AllowAnonymous]).
// Only triggers for empty-body error responses (JSON API/proxy responses carry a body → unaffected).
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

// MOD-0014 pass 2: cookie auth may have replaced context.User, so re-apply the principal pass 1 computed.
// It re-applies ONLY — no re-validation, no cookie clearing. The request still carries the PRE-refresh token,
// so any decision taken from it here would undo a refresh that just succeeded (which is exactly what happened:
// the page rendered while every following API call was logged out).
app.Use(async (context, next) =>
{
    tokenBridge.ReapplyPrincipal(context);
    await next();
});

app.UseAuthorization();

// FIX-4: tenant-shell session guard. A JWT stays cryptographically valid until it expires, so a tenant that was
// deleted or suspended after the token was issued would keep an open session. On top-level tenant-shell document
// navigations (tenant_user only) we check Platform for the tenant's liveness (short-cached, S2S); a DEFINITIVE
// missing/inactive answer clears the auth cookies and redirects to login. FAIL-OPEN: a null (unverifiable)
// result — Platform down/slow/no key — never signs anyone out. Ajax/DataTable/API/static calls are skipped so a
// JSON consumer never gets an HTML redirect; the next document navigation enforces it.
app.Use(async (context, next) =>
{
    if (ShouldCheckTenantStatus(context) &&
        TryReadTenantIdFromClaims(context.User, out var guardedTenantId))
    {
        var statusGateway = context.RequestServices.GetRequiredService<Diten.Web.Services.TenantStatus.ITenantStatusGateway>();
        var liveness = await statusGateway.GetTenantLivenessAsync(guardedTenantId, context.RequestAborted);
        if (liveness is not null && (!liveness.Exists || !liveness.IsActive))
        {
            var authCookieService = context.RequestServices.GetRequiredService<IAuthCookieService>();
            authCookieService.ClearTokens(context.Response);
            context.Response.Redirect("/account/login?reason=tenant-unavailable");
            return;
        }
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (RequiresPlatformPasswordChange(context) && !IsPasswordChangeAllowedPath(context.Request.Path))
    {
        context.Response.Redirect("/platform/change-password");
        return;
    }

    await next();
});

// FIX-TENANT-MUSTCHANGEPW — tenant counterpart of the platform forced-change gate: a tenant_user carrying
// pwd_change_required=true is pinned to /account/change-password until they set a real password.
app.Use(async (context, next) =>
{
    if (RequiresTenantPasswordChange(context) && !IsPasswordChangeAllowedPath(context.Request.Path))
    {
        context.Response.Redirect("/account/change-password");
        return;
    }

    await next();
});

app.MapGet("/", (HttpContext context) =>
{
    var host = context.Request.Host.Host;
    var isAdminHost = host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
    return Results.Redirect(isAdminHost ? "/Platform/Tenants" : "/WorkCenter");
});

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=WorkCenter}/{action=Index}/{id?}");

// Vanity tenant slug entry point. Runs ONLY when no controller/route matched (lowest priority),
// so it never shadows real routes/static files. A single-segment GET like "/gmg" is treated as a
// tenant slug: if it resolves to an ACTIVE tenant, redirect to that tenant's login; otherwise fall
// through to a normal 404 (re-executed as the friendly /Home/Status/404 page).
app.MapFallback(async (HttpContext context) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) || !TryGetSlugCandidate(context.Request.Path, out var slug))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var resolver = context.RequestServices.GetRequiredService<Diten.Web.Services.TenantResolution.ITenantSlugResolver>();
    var tenantId = await resolver.ResolveActiveTenantIdAsync(slug, context.RequestAborted);
    if (tenantId is { } id)
    {
        context.Response.Redirect($"/account/login?tenantId={id}");
        return;
    }

    context.Response.StatusCode = StatusCodes.Status404NotFound;
});

app.Run();

// A vanity-slug candidate is a single path segment of slug-safe characters (letters, digits, dash)
// with no file extension — e.g. "/gmg". Anything multi-segment, dotted (static file), or oversized
// is rejected so we never make a Platform lookup for junk or asset paths.
static bool TryGetSlugCandidate(PathString path, out string slug)
{
    slug = string.Empty;
    var value = path.Value;
    if (string.IsNullOrEmpty(value) || value.Length < 2 || value[0] != '/')
    {
        return false;
    }

    var segment = value[1..];
    if (segment.Length is < 1 or > 63 || segment.Contains('/') || segment.Contains('.'))
    {
        return false;
    }

    foreach (var c in segment)
    {
        var ok = c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' || c == '-';
        if (!ok)
        {
            return false;
        }
    }

    slug = segment;
    return true;
}

static Guid? TryReadTenantId(string accessToken)
{
    try
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(accessToken))
        {
            return null;
        }

        var token = handler.ReadJwtToken(accessToken);
        var claimValue = token.Claims.FirstOrDefault(c =>
            string.Equals(c.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Type, "tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : null;
    }
    catch
    {
        return null;
    }
}

static bool IsReferenceDataTenantPath(PathString path)
{
    return path.StartsWithSegments("/Platform/ReferenceData", StringComparison.OrdinalIgnoreCase);
}

// FIX-4: gate the tenant-shell session guard to top-level HTML document navigations by a tenant_user. Excludes
// login/account, platform-admin, API and static paths, and any ajax/non-HTML request, so DataTable/JSON callers
// never receive an HTML login redirect — the next page navigation enforces the sign-out instead.
static bool ShouldCheckTenantStatus(HttpContext context)
{
    if (!HttpMethods.IsGet(context.Request.Method))
    {
        return false;
    }

    var actorType = context.User.FindFirst("actor_type")?.Value;
    if (!string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var path = context.Request.Path;
    if (path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/platform", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (string.Equals(context.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var accept = context.Request.Headers.Accept.ToString();
    return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
}

static bool TryReadTenantIdFromClaims(System.Security.Claims.ClaimsPrincipal user, out Guid tenantId)
{
    var raw = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenantId")?.Value;
    return Guid.TryParse(raw, out tenantId);
}

static bool ShouldSkipTokenBridgeRefresh(PathString path)
{
    return path.StartsWithSegments("/account/refresh", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/logout", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/forgot-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/reset-password", StringComparison.OrdinalIgnoreCase);
}

static bool RequiresPlatformPasswordChange(HttpContext context)
{
    var actorType = context.User.FindFirst("actor_type")?.Value;
    var requiresChange = context.User.FindFirst("pwd_change_required")?.Value;
    return (string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase)) &&
           string.Equals(requiresChange, "true", StringComparison.OrdinalIgnoreCase);
}

// FIX-TENANT-MUSTCHANGEPW — tenant equivalent of RequiresPlatformPasswordChange.
static bool RequiresTenantPasswordChange(HttpContext context)
{
    var actorType = context.User.FindFirst("actor_type")?.Value;
    var requiresChange = context.User.FindFirst("pwd_change_required")?.Value;
    return string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase)
           && string.Equals(requiresChange, "true", StringComparison.OrdinalIgnoreCase);
}

static bool IsPasswordChangeAllowedPath(PathString path)
{
    return path.StartsWithSegments("/platform/change-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/forgot-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/platform/reset-password", StringComparison.OrdinalIgnoreCase)
           // FIX-TENANT-MUSTCHANGEPW — tenant change-password page/POST + login must be reachable (no redirect loop).
           || path.StartsWithSegments("/account/change-password", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/login", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/account/logout", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase);
}
