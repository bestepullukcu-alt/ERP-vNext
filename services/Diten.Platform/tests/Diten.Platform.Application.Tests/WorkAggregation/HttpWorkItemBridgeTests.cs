using System.Net;
using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.WorkAggregation;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// WC-D1 (DCP-004 §2 D1) — THE GUARDS FOR "A MODULE IN ANOTHER SERVICE REACHES THE TASK CENTER".
///
/// <para>Each test below is one of the promises the round was scoped around, written as a measurement rather than
/// a sentence in a document. The load-bearing one is the first: TWO configuration rows produce TWO working
/// providers from ONE class. If that ever stops being true — if a module gets its own bridge class — the
/// repository grows N teams' timeouts and N teams' error handling, one slow module slows the whole board, and
/// nobody can say which. That is the failure DCP-004 warned about on 2026-08-26, and the last test here refuses
/// it in the compiler rather than in a review.</para>
/// </summary>
public sealed class HttpWorkItemBridgeTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    // ── (a) TWO CONFIGURATION ROWS → TWO PROVIDERS, ONE CLASS ─────────────────

    [Fact]
    public void Two_configuration_rows_bind_two_providers_and_two_dispatchers_from_one_class_each()
    {
        using var scope = Container(
            Row("alpha", "http://alpha.local", ("approve", "alpha.approve")),
            Row("beta", "http://beta.local", ("complete", "beta.complete")));

        var providers = scope.ServiceProvider.GetServices<IWorkItemProvider>().ToList();
        var dispatchers = scope.ServiceProvider.GetServices<IWorkItemActionDispatcher>().ToList();

        Assert.Equal(2, providers.Count);
        Assert.Equal(2, dispatchers.Count);

        // ONE class. Adding a module must cost a configuration row and nothing else.
        Assert.All(providers, p => Assert.IsType<HttpWorkItemProvider>(p));
        Assert.All(dispatchers, d => Assert.IsType<HttpWorkItemActionDispatcher>(d));

        Assert.Equal(
            new[] { "alpha", "beta" },
            providers.Select(p => p.ProviderCode).OrderBy(c => c, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "alpha", "beta" },
            dispatchers.Select(d => d.ProviderCode).OrderBy(c => c, StringComparer.Ordinal));
    }

    /// <summary>
    /// The permission trap the onboarding note §3 records — a key consulted but never declared, so
    /// <c>actor.Has</c> silently answers false and a caller who HOLDS the permission is shown PERMISSION_DENIED —
    /// is not merely avoided here; there is only one list, so it is unreachable.
    /// </summary>
    [Fact]
    public void A_row_declares_its_permissions_once_for_both_halves()
    {
        using var scope = Container(Row("alpha", "http://alpha.local",
            ("approve", "alpha.approve"), ("reject", "alpha.reject")));

        var provider = Assert.Single(scope.ServiceProvider.GetServices<IWorkItemProvider>());
        var dispatcher = Assert.Single(scope.ServiceProvider.GetServices<IWorkItemActionDispatcher>());

        foreach (var code in dispatcher.SupportedActionCodes)
        {
            var key = dispatcher.RequiredPermission(code);
            Assert.False(string.IsNullOrWhiteSpace(key), $"'{code}' names no permission.");
            Assert.Contains(key!, provider.RequiredActionPermissions, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_row_with_no_address_stops_the_service_rather_than_becoming_a_permanently_dead_source()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Container(Row("alpha", baseUrl: "not-a-url", ("approve", "alpha.approve"))));

        Assert.Contains("BaseUrl", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_provider_code_twice_stops_the_service()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Container(
                Row("alpha", "http://one.local", ("approve", "alpha.approve")),
                Row("alpha", "http://two.local", ("approve", "alpha.approve"))));

        Assert.Contains("more than once", ex.Message, StringComparison.Ordinal);
    }

    // ── (b) A REMOTE 500 LEAVES THE BOARD DRAWN, AND NAMES THE SOURCE ─────────

    /// <summary>
    /// WC-D3's first real customer. D3 said in its own words that "the first network-backed provider is the first
    /// one that can be slow or absent"; this is that provider, and the machinery D3 built is not re-invented here
    /// — it is measured through the real aggregation handler.
    /// </summary>
    [Fact]
    public async Task A_remote_module_answering_500_leaves_the_board_drawn_and_reports_the_source()
    {
        var board = await Board(
            Reply(HttpStatusCode.InternalServerError, """{"data":null,"statusCode":500,"isSuccessful":false,"errors":["boom"]}"""),
            budget: TimeSpan.FromSeconds(30));

        // The healthy in-process provider's row is still there. An error page instead of the board is the defect.
        Assert.Single(board.Items);
        Assert.Equal("local-1", board.Items[0].Id);

        var missing = Assert.Single(board.UnavailableSources);
        Assert.Equal("alpha", missing.ProviderCode);
        Assert.Equal(WorkAggregationUnavailableReasonCodes.Error, missing.ReasonCode);
    }

    [Fact]
    public async Task A_remote_module_that_refuses_the_connection_is_reported_the_same_way()
    {
        var board = await Board(
            (_, _) => throw new HttpRequestException("connection refused"),
            budget: TimeSpan.FromSeconds(30));

        Assert.Single(board.Items);
        Assert.Equal(
            WorkAggregationUnavailableReasonCodes.Error,
            Assert.Single(board.UnavailableSources).ReasonCode);
    }

    /// <summary>
    /// A body that is not the shared envelope — a proxy error page, a login redirect — is "no answer", not a
    /// business refusal. Calling it a refusal would put a nonsense sentence in front of a reader.
    /// </summary>
    [Fact]
    public async Task A_remote_module_answering_HTML_is_reported_rather_than_parsed()
    {
        var board = await Board(
            Reply(HttpStatusCode.OK, "<html><body>Sign in</body></html>", "text/html"),
            budget: TimeSpan.FromSeconds(30));

        Assert.Single(board.Items);
        Assert.Equal(
            WorkAggregationUnavailableReasonCodes.Error,
            Assert.Single(board.UnavailableSources).ReasonCode);
    }

    // ── (c) A REMOTE TIMEOUT IS THE SAME, WITH ITS OWN REASON ─────────────────

    /// <summary>
    /// Proved with an ALREADY-SPENT budget and a token-respecting far end, so it costs no wall-clock time — the
    /// same technique the in-process timeout guard uses.
    /// </summary>
    [Fact]
    public async Task A_remote_module_that_exceeds_its_budget_is_reported_as_TIMEOUT()
    {
        var board = await Board(
            async (request, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new UnreachableException();
            },
            budget: TimeSpan.Zero);

        Assert.Single(board.Items);
        var missing = Assert.Single(board.UnavailableSources);
        Assert.Equal("alpha", missing.ProviderCode);
        Assert.Equal(WorkAggregationUnavailableReasonCodes.Timeout, missing.ReasonCode);
    }

    // ── (d) A WRITE THAT WAS NOT ANSWERED IS REFUSED, NEVER ASSUMED SUCCESSFUL ─

    [Fact]
    public async Task A_write_to_a_module_that_does_not_answer_is_REFUSED()
    {
        var dispatcher = Dispatcher(
            (_, _) => throw new HttpRequestException("connection refused"),
            budget: TimeSpan.FromSeconds(30));

        var response = await dispatcher.DispatchAsync(DispatchRequest("approve"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(WorkItemActionReasonCodes.RemoteUnavailable, response.ReasonCode);
        Assert.Equal(504, response.StatusCode);
    }

    [Fact]
    public async Task A_write_that_exceeds_its_budget_is_REFUSED_on_the_same_terms()
    {
        var dispatcher = Dispatcher(
            async (request, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new UnreachableException();
            },
            budget: TimeSpan.Zero);

        var response = await dispatcher.DispatchAsync(DispatchRequest("approve"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(WorkItemActionReasonCodes.RemoteUnavailable, response.ReasonCode);
    }

    /// <summary>
    /// The module's own refusal must arrive INTACT. The Task Center resolves its sentences from stable codes in
    /// seven languages, so a bridge that flattened a 409 into its own shape would make every remote refusal read
    /// "an error occurred" — the exact defect the error-code bridge exists to prevent, now one network hop away.
    /// </summary>
    [Fact]
    public async Task A_remote_modules_refusal_code_survives_the_bridge()
    {
        var dispatcher = Dispatcher(
            Reply(HttpStatusCode.Conflict,
                """{"data":null,"statusCode":409,"isSuccessful":false,"errors":["stale"],"reason_code":"REFERENCE_CONCURRENCY_CONFLICT"}"""),
            budget: TimeSpan.FromSeconds(30));

        var response = await dispatcher.DispatchAsync(DispatchRequest("approve"), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal("REFERENCE_CONCURRENCY_CONFLICT", response.ReasonCode);
    }

    [Fact]
    public async Task A_write_the_module_accepted_is_reported_as_success()
    {
        var dispatcher = Dispatcher(
            Reply(HttpStatusCode.OK, """{"data":{"ok":true},"statusCode":200,"isSuccessful":true,"errors":[]}"""),
            budget: TimeSpan.FromSeconds(30));

        var response = await dispatcher.DispatchAsync(DispatchRequest("approve"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("approve", response.Data!.ActionCode);
        Assert.Equal("alpha", response.Data!.ProviderCode);
    }

    [Fact]
    public async Task An_action_the_row_does_not_configure_is_never_forwarded()
    {
        HttpRequestMessage? seen = null;
        var dispatcher = Dispatcher(
            (request, _) =>
            {
                seen = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            },
            budget: TimeSpan.FromSeconds(30));

        var response = await dispatcher.DispatchAsync(DispatchRequest("obliterate"), CancellationToken.None);

        Assert.Equal(WorkItemActionReasonCodes.ActionUnknown, response.ReasonCode);
        Assert.Null(seen);
    }

    // ── (e) THE TENANT AND THE CALLER'S OWN IDENTITY ACTUALLY TRAVEL ──────────

    /// <summary>
    /// ⚠ THIS TEST IS NOT SUFFICIENT ON ITS OWN, and saying so is the point. It resolves the container's real
    /// named client and its real gateway, but the tenant context it registers is a SINGLETON — so it proves the
    /// header is written, and cannot prove it is written from the right SCOPE. An earlier version of this bridge
    /// passed this exact test while sending no tenant header at all in production, because the shared
    /// tenant-propagation <c>DelegatingHandler</c> it then used resolved its context from the HttpClientFactory's
    /// cached handler scope rather than the request's (that handler was deleted in BL-316). That was found by
    /// calling a real module and reading the tenant it received back
    /// on the screen. The fix moved the header into the request-scoped gateway; the live read stays the proof.
    /// </summary>
    [Fact]
    public async Task The_tenant_header_and_the_callers_own_bearer_token_reach_the_module()
    {
        HttpRequestMessage? seen = null;

        using var scope = Container(
            rows: [Row("alpha", "http://alpha.local", ("approve", "alpha.approve"))],
            handler: (request, _) =>
            {
                seen = request;
                return Task.FromResult(Json(HttpStatusCode.OK, Projection("alpha", "remote-1")));
            },
            bearer: "Bearer caller-token");

        var provider = Assert.Single(scope.ServiceProvider.GetServices<IWorkItemProvider>());
        await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.NotNull(seen);
        Assert.Equal(Tenant.ToString(), Assert.Single(seen!.Headers.GetValues("X-Tenant-Id")));

        // The HUMAN's token, not a service key: the module must authorise the person who pressed the button.
        Assert.Equal("Bearer", seen.Headers.Authorization?.Scheme);
        Assert.Equal("caller-token", seen.Headers.Authorization?.Parameter);
    }

    // ── The bridge decides permission and dispatchability, not the module ─────

    /// <summary>
    /// A remote module can claim an action is enabled; it cannot decide whether the CALLER may use it. The granted
    /// set is evaluated from claims on this side, and the downgrade uses the same PERMISSION_DENIED code an
    /// in-process provider's action gets — one vocabulary, so the screen needs no special case for remote items.
    /// </summary>
    [Fact]
    public async Task An_action_the_caller_lacks_the_permission_for_is_disabled_whatever_the_module_said()
    {
        var provider = Provider(
            Reply(HttpStatusCode.OK, Projection("alpha", "remote-1", actions: [("approve", true)])),
            ("approve", "alpha.approve"));

        var item = Assert.Single(await provider.GetWorkItemsAsync(
            Actor(granted: new HashSet<string>()), CancellationToken.None));

        var action = Assert.Single(item.Actions);
        Assert.False(action.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, action.DisabledReasonCode);
    }

    /// <summary>
    /// An action with no configured permission has no dispatch path behind it, so it is NOT DRAWN. A drawn button
    /// that reaches nothing is precisely the defect DCP-004 §2 D2 records, and a network hop is no reason to
    /// re-ship it.
    /// </summary>
    [Fact]
    public async Task An_action_the_row_does_not_configure_is_not_offered_to_the_reader()
    {
        var provider = Provider(
            Reply(HttpStatusCode.OK, Projection("alpha", "remote-1", actions: [("approve", true), ("nuke", true)])),
            ("approve", "alpha.approve"));

        var item = Assert.Single(await provider.GetWorkItemsAsync(
            Actor(granted: new HashSet<string> { "alpha.approve" }), CancellationToken.None));

        Assert.Equal(["approve"], item.Actions.Select(a => a.Code));
    }

    /// <summary>
    /// <c>source.providerCode</c> is the address the browser posts the next click to, so an item claiming another
    /// module's code would route a write at a module the operator never configured — the manifest-address hazard
    /// (D1) one level down, refused for the same reason.
    /// </summary>
    [Fact]
    public async Task An_item_claiming_another_modules_provider_code_is_dropped()
    {
        var provider = Provider(
            Reply(HttpStatusCode.OK, Projection("beta", "stolen-1")),
            ("approve", "alpha.approve"));

        Assert.Empty(await provider.GetWorkItemsAsync(Actor(), CancellationToken.None));
    }

    /// <summary>
    /// The version handshake runs in BOTH directions. The handler decided to call this provider from the ROW's
    /// declared version — the only one available before a call exists — so a module answering a different
    /// generation means the row is stale, and projecting anyway would map a shape nobody agreed to.
    /// </summary>
    [Fact]
    public async Task A_module_answering_a_different_contract_version_is_reported_rather_than_projected()
    {
        var board = await Board(
            Reply(HttpStatusCode.OK, Projection("alpha", "remote-1", contractVersion: "2.0")),
            budget: TimeSpan.FromSeconds(30));

        Assert.Single(board.Items);
        Assert.Equal(
            WorkAggregationUnavailableReasonCodes.Error,
            Assert.Single(board.UnavailableSources).ReasonCode);
    }

    // ── (f) NOBODY MAY ADD A CLASS WITH A MODULE BAKED INTO IT ────────────────

    /// <summary>
    /// THE RULE OF THE ROUND, in a test rather than a review comment.
    ///
    /// <para>Exactly ONE implementation of each seam may exist in the Infrastructure assembly, and its name may
    /// not carry a module's identity. A second one — <c>PvgWorkItemProvider</c>, <c>SkuWorkItemDispatcher</c> —
    /// is how the repository ends up holding N teams' error handling and N teams' timeouts, and it is exactly what
    /// DCP-004 asked not to happen. Adding a module is adding a ROW.</para>
    /// </summary>
    [Fact]
    public void The_network_seam_has_exactly_one_implementation_and_it_names_no_module()
    {
        var assembly = typeof(HttpWorkItemProvider).Assembly;

        var providers = Concrete<IWorkItemProvider>(assembly);
        var dispatchers = Concrete<IWorkItemActionDispatcher>(assembly);

        Assert.Equal([typeof(HttpWorkItemProvider)], providers);
        Assert.Equal([typeof(HttpWorkItemActionDispatcher)], dispatchers);

        // Non-vacuity: this must not pass because the seam was renamed and nothing was found.
        Assert.Contains(typeof(HttpWorkItemProvider), providers);

        // "Http" is the transport, which is what these classes ARE. A name that says anything else about WHICH
        // module it serves is the thing being refused.
        foreach (var type in providers.Concat(dispatchers))
        {
            Assert.StartsWith("Http", type.Name, StringComparison.Ordinal);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<Type> Concrete<T>(System.Reflection.Assembly assembly)
        => assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(T).IsAssignableFrom(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static Dictionary<string, string?> Row(
        string code,
        string baseUrl,
        params (string Action, string Permission)[] actions)
    {
        var row = new Dictionary<string, string?>
        {
            ["ProviderCode"] = code,
            ["ContractVersion"] = "1.0",
            ["BaseUrl"] = baseUrl
        };

        foreach (var (action, permission) in actions)
        {
            row[$"Actions:{action}"] = permission;
        }

        return row;
    }

    /// <summary>
    /// A REAL container built from REAL configuration through the REAL registration path — the only way the
    /// "two rows, one class" promise can be measured rather than asserted. Only the socket is replaced.
    /// </summary>
    private static IServiceScope Container(params Dictionary<string, string?>[] rows)
        => Container(rows, handler: (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

    private static IServiceScope Container(
        Dictionary<string, string?>[] rows,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        string? bearer = null)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < rows.Length; i++)
        {
            foreach (var (key, value) in rows[i])
            {
                settings[$"{RemoteWorkItemProviderOptions.SectionName}:{i}:{key}"] = value;
            }
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(new FakeTenantContext(Tenant));
        services.AddSingleton<IHttpContextAccessor>(new FakeHttpContextAccessor(bearer));
        services.Configure<WorkAggregationResilienceOptions>(_ => { });

        services.AddRemoteWorkItemProviders(configuration);

        // Only the SOCKET is stubbed. Everything above it — the named client, the tenancy handler, the JSON — is
        // the code that runs in production.
        services.AddHttpClient(RemoteWorkItemGateway.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(handler));

        return services.BuildServiceProvider().CreateScope();
    }

    private static HttpWorkItemProvider Provider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        params (string Action, string Permission)[] actions)
        => new(
            OptionsRow(actions),
            Gateway(handler),
            NullLogger<HttpWorkItemProvider>.Instance);

    private static HttpWorkItemActionDispatcher Dispatcher(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        TimeSpan budget)
        => new(
            OptionsRow([("approve", "alpha.approve")]),
            Gateway(handler),
            Options.Create(new WorkAggregationResilienceOptions { ProviderTimeout = budget }),
            NullLogger<HttpWorkItemActionDispatcher>.Instance);

    private static RemoteWorkItemProviderOptions OptionsRow((string Action, string Permission)[] actions)
    {
        var row = new RemoteWorkItemProviderOptions
        {
            ProviderCode = "alpha",
            ContractVersion = "1.0",
            BaseUrl = "http://alpha.local"
        };

        foreach (var (action, permission) in actions)
        {
            row.Actions[action] = permission;
        }

        return row;
    }

    private static RemoteWorkItemGateway Gateway(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new StubClientFactory(handler), new FakeHttpContextAccessor(null), new FakeTenantContext(Tenant));

    /// <summary>
    /// The board as the READER gets it: the real aggregation handler over one healthy in-process provider and one
    /// remote provider behind the given far end. Nothing about failure is asserted at the bridge — it is asserted
    /// on what the screen receives.
    /// </summary>
    private static async Task<WorkItemBoardDto> Board(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> remote,
        TimeSpan budget)
    {
        var handler = new GetMyWorkItemsHandler(
            [
                new HealthyProvider("local", ProjectionFor("local-1")),
                new HttpWorkItemProvider(
                    OptionsRow([("approve", "alpha.approve")]),
                    Gateway(remote),
                    NullLogger<HttpWorkItemProvider>.Instance)
            ],
            new FakeCurrentUser(Me),
            Options.Create(new WorkAggregationResilienceOptions { ProviderTimeout = budget }),
            NullLogger<GetMyWorkItemsHandler>.Instance);

        var response = await handler.Handle(
            new GetMyWorkItemsQuery(IsPlatformActor: true, new HashSet<string>(), "corr"),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        return response.Data!;
    }

    private static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Reply(
        HttpStatusCode status, string body, string mediaType = "application/json")
        => (_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType)
        });

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// One canonical projection, serialised the way a module would send it, so what the test exercises is the
    /// WIRE and not a C# object handed straight back.
    /// </summary>
    private static string Projection(
        string providerCode,
        string id,
        (string Code, bool Enabled)[]? actions = null,
        string contractVersion = "1.0")
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            data = new { contractVersion, items = new[] { Item(providerCode, id, actions) } },
            statusCode = 200,
            isSuccessful = true,
            errors = Array.Empty<string>()
        });

    private static object Item(string providerCode, string id, (string Code, bool Enabled)[]? actions = null)
        => new
        {
            fixtureKind = "workItem",
            id,
            workIntent = "task",
            assignmentMode = "direct",
            ownershipState = "assigned",
            admissionState = "admitted",
            normalizedStatus = "Pending",
            taskLifecycle = "Open",
            executionState = "notStarted",
            timerState = "inactive",
            systemState = "fresh",
            actionDepth = "inline",
            title = new { kind = "display", text = "Remote", locale = "und" },
            nativeStatus = new
            {
                code = "Pending",
                label = new { kind = "display", text = "Pending", locale = "und" }
            },
            source = new
            {
                providerCode,
                providerContractVersion = "1.0",
                objectType = "referenceWorkItem",
                objectId = id
            },
            lifecycleOwner = providerCode,
            workItemCapabilities = Array.Empty<string>(),
            actions = (actions ?? []).Select(a => new
            {
                code = a.Code,
                label = new { kind = "display", text = a.Code, locale = "und" },
                semanticType = "primary",
                enabled = a.Enabled,
                source = "provider",
                requiresConfirmation = false,
                requiresReason = false,
                requiresEvidence = false,
                supportsBulk = false,
                riskLevel = "low"
            }).ToArray(),
            concurrency = new { kind = "version", token = "1" }
        };

    private static WorkItemActor Actor(IReadOnlySet<string>? granted = null)
        => new(Me, IsPlatformActor: false, granted ?? new HashSet<string>());

    private static WorkItemActionDispatchRequest DispatchRequest(string actionCode)
        => new(Guid.NewGuid(), actionCode, new WorkItemActionPayloadDto(ExpectedVersion: 1), Actor(), "corr");

    private static WorkItemProjectionDto ProjectionFor(string id)
        => new(
            FixtureKind: WorkItemContract.FixtureKindWorkItem,
            Id: id,
            WorkIntent: WorkItemContract.IntentApproval,
            AssignmentMode: WorkItemContract.AssignmentApproval,
            OwnershipState: WorkItemContract.NotApplicable,
            AdmissionState: WorkItemContract.NotApplicable,
            NormalizedStatus: WorkItemContract.StatusPending,
            TaskLifecycle: WorkItemContract.NotApplicable,
            ExecutionState: WorkItemContract.NotApplicable,
            TimerState: WorkItemContract.NotApplicable,
            SystemState: WorkItemContract.SystemFresh,
            ActionDepth: WorkItemContract.DepthInline,
            Title: new WorkItemLabelDto(WorkItemContract.LabelResource, "WorkAggregation_Title_Approval"),
            NativeStatus: new WorkItemNativeStatusDto(
                "WaitingApproval",
                new WorkItemLabelDto(WorkItemContract.LabelResource, "WorkAggregation_NativeStatus_WaitingApproval")),
            Source: new WorkItemSourceDto(WorkItemContract.ProviderCodeWorkflow, "1.0", "invoice", "INV-1", null),
            LifecycleOwner: WorkItemContract.LifecycleOwnerWorkflow,
            WorkItemCapabilities: [],
            Actions: [],
            Concurrency: new WorkItemConcurrencyDto("version", "1"),
            WaitingContext: null,
            Escalation: null,
            DueAt: null);

    private sealed class HealthyProvider(string code, params WorkItemProjectionDto[] items) : IWorkItemProvider
    {
        public string ProviderCode => code;
        public string ProviderContractVersion => "1.0";
        public IReadOnlyCollection<string> RequiredActionPermissions => [];
        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
            WorkItemActor actor, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItemProjectionDto>>(items.ToList());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => reply(request, ct);
    }

    private sealed class StubClientFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new StubHandler(reply)) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private sealed class FakeHttpContextAccessor(string? bearer) : IHttpContextAccessor
    {
        private readonly DefaultHttpContext _context = Build(bearer);

        public HttpContext? HttpContext { get => _context; set => throw new NotSupportedException(); }

        private static DefaultHttpContext Build(string? bearer)
        {
            var context = new DefaultHttpContext();
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                context.Request.Headers.Authorization = bearer;
            }

            return context;
        }
    }

    private sealed class FakeTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public bool IsResolved => true;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid id) => throw new NotSupportedException();
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
        public void ClearTenant() => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid UserId { get; } = userId;
        public string? Email => "me@diten.local";
        public string? DisplayName => "Me";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    private sealed class UnreachableException() : Exception("unreachable");
}
