using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.API.Controllers.Internal;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Diten.Platform.Application.Features.Meetings.SelfRegistration;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.OrganizationSeats;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Notifications;

/// <summary>
/// WP-VW-W5 PLT-NOTIFY — <c>POST /api/internal/notifications/events/dispatch</c> over real HTTP. Real: the controller,
/// MVC's binder and serializer, <see cref="InternalEventDispatchService"/>, the real <see cref="TenantContext"/> it pins,
/// <see cref="LineManagerResolver"/> over the real <see cref="TaskSeatDirectory"/>, and the event catalog as the manifest
/// sync writes it from the CRM and Meetings manifests. Doubled: the org repositories, the AuthService address lookup and
/// the dispatch adapter (recording) — each double reads the AMBIENT tenant, exactly as the tenant execution filter and
/// AuthService's own tenant scoping do, so a call that did not pin the body's tenant would see nobody.
/// </summary>
public sealed partial class InternalNotificationEventDispatchTests
{
    private const string ApiKey = "test-internal-api-key";
    private const string Crm = "MOD-0155";

    private static readonly Guid TenantA = Guid.Parse("a5a5a5a5-0000-0000-0000-0000000000a5");
    internal static readonly Guid TenantB = Guid.Parse("b6b6b6b6-0000-0000-0000-0000000000b6");

    private static readonly Guid Rep = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Manager = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid Director = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid EnglishColleague = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid Loner = Guid.Parse("10000000-0000-0000-0000-000000000005");
    private static readonly Guid ForeignRep = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ForeignManager = Guid.Parse("20000000-0000-0000-0000-000000000002");

    // ── The key ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Without_the_key_or_with_a_wrong_one_the_answer_is_401_and_nothing_is_read_or_sent()
    {
        using var host = new Host();

        var missing = await host.PostAsync(Body(to: [LineManagerOf(Rep)]), apiKey: null);
        var wrong = await host.PostAsync(Body(to: [LineManagerOf(Rep)]), apiKey: "not-the-key");
        var sameLength = await host.PostAsync(Body(to: [LineManagerOf(Rep)]), apiKey: new string('x', ApiKey.Length));
        // …and for a tenant that does not exist: without the key there is no telling which is which.
        var unknown = await host.PostAsync(Body(tenantId: Guid.NewGuid(), to: [LineManagerOf(Rep)]), apiKey: null);

        Assert.All(new[] { missing, wrong, sameLength, unknown }, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(0, host.Tenants.Reads);
        Assert.Empty(host.Adapter.Requests);
    }

    // ── The tenant ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unknown_or_inactive_tenant_is_404_naming_nothing_and_nothing_is_sent()
    {
        using var host = new Host();
        var unknownId = Guid.NewGuid();

        var unknown = await host.PostAsync(Body(tenantId: unknownId, to: [LineManagerOf(Rep)]));
        host.Tenants.Statuses[TenantA] = TenantStatus.Suspended;
        var suspended = await host.PostAsync(Body(to: [LineManagerOf(Rep)]));

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, suspended.StatusCode);
        var body = await unknown.Content.ReadAsStringAsync();
        Assert.DoesNotContain(unknownId.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(host.Adapter.Requests);
    }

    // ── The event and its owner ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_event_not_in_the_catalog_another_modules_event_or_one_that_is_not_active_is_403()
    {
        using var host = new Host();

        var notCatalogued = await host.PostAsync(Body(eventCode: "crm.visitreport.unknown", to: [LineManagerOf(Rep)]));
        // A Meetings event, asked for by CRM: a module sends its own events only.
        var foreign = await host.PostAsync(Body(eventCode: "platform.meetings.invite", to: [LineManagerOf(Rep)]));
        // CRM's own event, asked for under some other module's name.
        var wrongSource = await host.PostAsync(Body(sourceModule: "MOD-0024", to: [LineManagerOf(Rep)]));
        host.Events.Single(CrmManifestProvider.VisitReportOverdueEvent).Status = NotificationEventStatus.Deprecated;
        var inactive = await host.PostAsync(Body(eventCode: CrmManifestProvider.VisitReportOverdueEvent, to: [LineManagerOf(Rep)]));

        Assert.All(new[] { notCatalogued, foreign, wrongSource, inactive }, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Empty(host.Adapter.Requests);
        Assert.Equal(0, host.Recipients.Calls);
    }

    [Fact]
    public async Task The_catalog_carries_the_four_crm_events_active_owned_by_the_crm_manifest_and_mod_0155_speaks_for_it()
    {
        using var host = new Host();

        foreach (var code in CrmEvents)
        {
            var definition = host.Events.Single(code);
            Assert.Equal(NotificationEventStatus.Active, definition.Status);
            Assert.Equal("CRM", definition.OwnerModuleId);
            Assert.Equal(code, definition.DefaultTemplateKey);
            Assert.Equal(CrmManifestProvider.VisitReportRequiredVariables,
                definition.RequiredVariables.Select(v => v.Name).Take(CrmManifestProvider.VisitReportRequiredVariables.Count));
            Assert.DoesNotContain(definition.RequiredVariables.Concat(definition.OptionalVariables),
                v => v.Name.Contains("Patient", StringComparison.OrdinalIgnoreCase));
        }

        Assert.True(InternalEventDispatchService.IsOwnedBy("CRM", "MOD-0155"));
        Assert.True(InternalEventDispatchService.IsOwnedBy("CRM", "crm"));
        Assert.False(InternalEventDispatchService.IsOwnedBy("MEETINGS", "MOD-0155"));
        Assert.False(InternalEventDispatchService.IsOwnedBy("CRM", "MOD-0156"));
    }

    // ── Line manager resolution ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Line_manager_of_the_rep_is_the_holder_of_the_seat_the_rep_reports_to()
    {
        using var host = new Host();

        var response = await host.PostAsync(Body(to: [LineManagerOf(Rep)]));
        var result = await host.ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("sent", result.Status);
        var reader = Assert.Single(result.Resolved.To);
        Assert.Equal(Manager, reader.UserId);
        Assert.Equal("line-manager", reader.Via);
        var dispatched = Assert.Single(host.Adapter.Requests);
        Assert.Equal("manager@a.test", Assert.Single(dispatched.To).Email);
        Assert.Equal(TenantA, dispatched.TenantId);
        Assert.Equal(CrmManifestProvider.VisitReportSubmittedEvent, dispatched.EventCode);
        Assert.Equal(Crm, dispatched.SourceModule);
        Assert.Equal("corr-1", dispatched.CorrelationId);
    }

    [Fact]
    public async Task A_vacant_manager_seat_moves_up_the_chain()
    {
        using var host = new Host();
        host.Org.Vacate(Manager);

        var result = await host.ReadAsync(await host.PostAsync(Body(to: [LineManagerOf(Rep)])));

        var reader = Assert.Single(result.Resolved.To);
        Assert.Equal(Director, reader.UserId);
        Assert.Equal("manager-chain", reader.Via);
        Assert.Equal("director@a.test", Assert.Single(Assert.Single(host.Adapter.Requests).To).Email);
    }

    [Fact]
    public async Task Nobody_to_report_to_means_no_mail_and_the_answer_says_no_recipient()
    {
        using var host = new Host();

        var response = await host.PostAsync(Body(eventCode: CrmManifestProvider.VisitReportOverdueEvent, to: [LineManagerOf(Loner)]));
        var result = await host.ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-recipient", result.Status);
        Assert.Empty(result.Dispatches);
        Assert.Empty(result.Resolved.To);
        Assert.Empty(host.Adapter.Requests);
    }

    [Fact]
    public async Task Another_tenants_user_is_never_resolved_neither_directly_nor_as_a_line_manager()
    {
        using var host = new Host();

        // Tenant B's rep and manager exist, with addresses — but the call is for tenant A.
        var asUser = await host.ReadAsync(await host.PostAsync(Body(to: [new InternalEventRecipient(UserId: ForeignManager)])));
        var asManagerOf = await host.ReadAsync(await host.PostAsync(Body(to: [LineManagerOf(ForeignRep)])));

        Assert.Equal("no-recipient", asUser.Status);
        Assert.Equal("no-recipient", asManagerOf.Status);
        Assert.Empty(host.Adapter.Requests);

        // The same people ARE resolved in their own tenant: the refusal above is the tenant, not the people.
        var own = await host.ReadAsync(await host.PostAsync(Body(tenantId: TenantB, to: [LineManagerOf(ForeignRep)])));
        Assert.Equal(ForeignManager, Assert.Single(own.Resolved.To).UserId);
        Assert.Equal(TenantB, Assert.Single(host.Adapter.Requests).TenantId);
    }

    // ── Languages, copies, addresses ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_turkish_and_an_english_reader_get_two_dispatches_one_per_language_and_the_copy_rides_once()
    {
        using var host = new Host();

        var response = await host.PostAsync(Body(
            to: [LineManagerOf(Rep), new InternalEventRecipient(UserId: EnglishColleague)],
            cc: [new InternalEventRecipient(Email: "pv@a.test", DisplayName: "PV")]));
        var result = await host.ReadAsync(response);

        Assert.Equal(["tr", "en"], result.Dispatches.Select(d => d.Locale));
        Assert.All(result.Dispatches, d => Assert.NotNull(d.DispatchId));
        Assert.Equal(2, host.Adapter.Requests.Count);
        Assert.Equal(["manager@a.test"], host.Adapter.Requests[0].To.Select(r => r.Email));
        Assert.Equal("tr", host.Adapter.Requests[0].Locale);
        Assert.Equal(["colleague@a.test"], host.Adapter.Requests[1].To.Select(r => r.Email));
        Assert.Equal("en", host.Adapter.Requests[1].Locale);
        Assert.Equal(["pv@a.test"], host.Adapter.Requests[0].Cc!.Select(r => r.Email));
        Assert.Null(host.Adapter.Requests[1].Cc);
        Assert.Equal("sent", result.Status);
    }

    // ── W5-FIX (3) — one address, one mail: a reader in To is not copied in Cc as well ─────────────────────────────

    [Fact]
    public async Task The_same_user_in_to_and_in_copy_is_written_to_once_and_left_out_of_the_copy()
    {
        using var host = new Host();

        var result = await host.ReadAsync(await host.PostAsync(Body(
            to: [new InternalEventRecipient(UserId: Manager)],
            cc: [LineManagerOf(Rep), new InternalEventRecipient(UserId: EnglishColleague)])));

        var sent = Assert.Single(host.Adapter.Requests);
        Assert.Equal(["manager@a.test"], sent.To.Select(r => r.Email));
        Assert.Equal(["colleague@a.test"], sent.Cc!.Select(r => r.Email));
        Assert.Equal(Manager, Assert.Single(result.Resolved.To).UserId);
        Assert.Equal([EnglishColleague], result.Resolved.Cc.Select(c => c.UserId!.Value));
    }

    [Fact]
    public async Task The_same_address_in_another_letter_case_in_to_and_in_copy_is_one_reader()
    {
        using var host = new Host();

        await host.PostAsync(Body(
            to: [new InternalEventRecipient(Email: "PV-Safety@A.test", DisplayName: "PV", Locale: "tr")],
            cc: [new InternalEventRecipient(Email: "pv-safety@a.test"), LineManagerOf(Rep)]));

        var sent = Assert.Single(host.Adapter.Requests);
        Assert.Equal(["PV-Safety@A.test"], sent.To.Select(r => r.Email));
        Assert.Equal(["manager@a.test"], sent.Cc!.Select(r => r.Email));
    }

    [Fact]
    public async Task The_answer_carries_no_e_mail_address_at_all()
    {
        using var host = new Host();

        var response = await host.PostAsync(Body(
            eventCode: CrmManifestProvider.VisitReportAdverseEventEvent,
            to: [new InternalEventRecipient(Email: "pv-safety@a.test", DisplayName: "PV", Locale: "en")],
            cc: [LineManagerOf(Rep), new InternalEventRecipient(UserId: EnglishColleague)]));
        var body = await response.Content.ReadAsStringAsync();
        var result = await host.ReadAsync(response, body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("@", body);
        Assert.Equal("email", Assert.Single(result.Resolved.To).Via);
        Assert.Null(result.Resolved.To[0].UserId);
        Assert.Equal([(Manager, "line-manager"), (EnglishColleague, "direct")],
            result.Resolved.Cc.Select(c => (c.UserId!.Value, c.Via)));
        var sent = Assert.Single(host.Adapter.Requests);
        Assert.Equal(["manager@a.test", "colleague@a.test"], sent.Cc!.Select(r => r.Email));
    }

    [Fact]
    public async Task Nobody_in_to_but_somebody_in_copy_is_written_to_rather_than_left_out()
    {
        using var host = new Host();

        var result = await host.ReadAsync(await host.PostAsync(Body(
            eventCode: CrmManifestProvider.VisitReportOffLabelRequestEvent,
            to: [LineManagerOf(Loner)],
            cc: [LineManagerOf(Rep)])));

        Assert.Equal(Manager, Assert.Single(result.Resolved.To).UserId);
        Assert.Empty(result.Resolved.Cc);
        Assert.Equal(["manager@a.test"], Assert.Single(host.Adapter.Requests).To.Select(r => r.Email));
    }

    // ── The reply address (contract annex) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_reply_goes_to_the_named_user_of_the_same_tenant_and_the_answer_names_no_address()
    {
        using var host = new Host();

        var response = await host.PostAsync(Body(to: [LineManagerOf(Rep)], replyTo: new InternalEventReplyTo(Rep)));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rep@a.test", Assert.Single(host.Adapter.Requests).ReplyToEmail);
        Assert.DoesNotContain("@", body);
    }

    [Fact]
    public async Task A_reply_user_of_another_tenant_is_not_resolved_the_tenants_own_reply_address_applies()
    {
        using var host = new Host();

        await host.PostAsync(Body(to: [LineManagerOf(Rep)], replyTo: new InternalEventReplyTo(ForeignRep)));

        Assert.Null(Assert.Single(host.Adapter.Requests).ReplyToEmail);
    }

    [Theory]
    [InlineData("reply-empty")]
    [InlineData("both")]
    [InlineData("none")]
    [InlineData("empty-user")]
    [InlineData("no-recipients")]
    [InlineData("no-source")]
    public async Task A_malformed_body_is_400_and_nothing_is_sent(string shape)
    {
        using var host = new Host();
        var body = shape switch
        {
            "both" => Body(to: [new InternalEventRecipient(UserId: Manager, Email: "x@a.test")]),
            "none" => Body(to: [new InternalEventRecipient()]),
            "empty-user" => Body(to: [new InternalEventRecipient(LineManagerOf: Guid.Empty)]),
            "no-recipients" => Body(to: []),
            "reply-empty" => Body(to: [LineManagerOf(Rep)], replyTo: new InternalEventReplyTo(Guid.Empty)),
            _ => Body(sourceModule: " ", to: [LineManagerOf(Rep)])
        };

        var response = await host.PostAsync(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.Adapter.Requests);
    }

    [Fact]
    public async Task A_refusal_of_every_group_is_the_answer_with_its_own_status()
    {
        using var host = new Host();
        host.Adapter.FailWith = (422, NotificationEventDispatchAdapter.ReasonRequiredVariableMissing);

        var response = await host.PostAsync(Body(to: [LineManagerOf(Rep)]));

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains(NotificationEventDispatchAdapter.ReasonRequiredVariableMissing, await response.Content.ReadAsStringAsync());
    }

    // ── The test world ──────────────────────────────────────────────────────────────────────────────────────────

    private static readonly string[] CrmEvents =
    [
        CrmManifestProvider.VisitReportSubmittedEvent,
        CrmManifestProvider.VisitReportAdverseEventEvent,
        CrmManifestProvider.VisitReportOffLabelRequestEvent,
        CrmManifestProvider.VisitReportOverdueEvent
    ];

    private static InternalEventRecipient LineManagerOf(Guid user) => new(LineManagerOf: user);

    internal static InternalEventDispatchRequest Body(
        Guid? tenantId = null,
        string eventCode = CrmManifestProvider.VisitReportSubmittedEvent,
        string sourceModule = Crm,
        IReadOnlyList<InternalEventRecipient>? to = null,
        IReadOnlyList<InternalEventRecipient>? cc = null,
        InternalEventReplyTo? replyTo = null,
        IReadOnlyList<InternalEventAttachment>? attachments = null) =>
        new(
            tenantId ?? TenantA,
            eventCode,
            sourceModule,
            new InternalEventRecipients(to, cc),
            new Dictionary<string, string?>
            {
                ["doctorName"] = "Doç. Dr. Mehmet Yılmaz",
                ["institutionName"] = "Şişli Hamidiye Etfal EAH",
                ["visitDate"] = "2026-10-08",
                ["repName"] = "Ayşe Demir",
                ["reportUrl"] = "https://crm.test/CRM/VisitReport?plannedVisitId=v26",
                ["deadline"] = "2026-10-10T11:31:00Z"
            },
            "corr-1",
            ReplyTo: replyTo,
            Attachments: attachments);

    internal sealed class Host : IDisposable
    {
        private readonly TestServer _server;

        public Tenants Tenants { get; } = new();
        public Catalog Events { get; } = Catalog.FromManifests(new CrmManifestProvider(), new MeetingManifestProvider());
        public OrgChart Org { get; } = new();
        public Contacts Recipients { get; }
        public RecordingAdapter Adapter { get; } = new();

        /// <summary>WP-VW-W6 PLT-W6 — the MOD-0262 rows + their bytes (tenant-filtered by the ambient tenant).</summary>
        public Documents Files { get; } = new();

        public Host()
        {
            Recipients = new Contacts(Org);
            _server = new TestServer(new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AuthService:InternalApiKey"] = ApiKey
                }))
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddSingleton<ITenantRegistryRepository>(Tenants);
                    services.AddSingleton<INotificationEventDefinitionRepository>(Events);
                    services.AddSingleton(Org);
                    services.AddScoped<IPositionAssignmentRepository, TenantAssignments>();
                    services.AddScoped<IPositionRepository, TenantPositions>();
                    services.AddScoped<ITaskSeatDirectory, TaskSeatDirectory>();
                    services.AddScoped<ILineManagerResolver, LineManagerResolver>();
                    services.AddScoped<ITaskNotificationRecipientResolver>(sp => Recipients.For(sp.GetRequiredService<ITenantContext>()));
                    services.AddSingleton<INotificationLocaleResolver>(new FakeNotificationLocaleResolver("tr"));
                    services.AddSingleton<INotificationEventDispatchAdapter>(Adapter);
                    services.AddScoped<IRepositoryObjectRepository>(sp => Files.For(sp.GetRequiredService<ITenantContext>()));
                    services.AddSingleton<Diten.Platform.Application.Contracts.DocumentRepository.IContentStorageGateway>(Files);
                    // W5-FIX2 — the idempotency keys: one store for the host, read in each request's own tenant.
                    services.AddSingleton(new List<InternalDispatchKey>());
                    services.AddScoped<IInternalDispatchKeyRepository>(sp => new InMemoryInternalDispatchKeys(
                        sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<List<InternalDispatchKey>>()));
                    services.AddScoped<IInternalEventDispatchService, InternalEventDispatchService>();
                    services.AddControllers()
                        .ConfigureApplicationPartManager(manager =>
                        {
                            manager.ApplicationParts.Clear();
                            manager.ApplicationParts.Add(new AssemblyPart(typeof(InternalNotificationEventDispatchController).Assembly));
                            manager.FeatureProviders.Add(new OnlyController(typeof(InternalNotificationEventDispatchController)));
                        });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }));
        }

        public Task<HttpResponseMessage> PostAsync(InternalEventDispatchRequest body, string? apiKey = ApiKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/notifications/events/dispatch")
            {
                Content = JsonContent.Create(body)
            };
            if (apiKey is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", apiKey);
            }

            return _server.CreateClient().SendAsync(request);
        }

        public async Task<InternalEventDispatchResultDto> ReadAsync(HttpResponseMessage response, string? body = null)
        {
            body ??= await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, body);
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("data").Deserialize<InternalEventDispatchResultDto>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        }

        public void Dispose() => _server.Dispose();
    }

    private sealed class OnlyController(Type controller) : ControllerFeatureProvider
    {
        protected override bool IsController(System.Reflection.TypeInfo typeInfo) => typeInfo.AsType() == controller;
    }

    /// <summary>Tenant A: rep → manager → director (Rep reports to Manager's seat, Manager's to Director's), an English
    /// colleague, and a loner with no reporting line. Tenant B: its own rep → manager. Every person has an address.</summary>
    internal sealed class OrgChart
    {
        public List<Position> Positions { get; } = [];
        public List<PositionAssignment> Assignments { get; } = [];
        public Dictionary<(Guid Tenant, Guid User), (string Email, string? Language)> Addresses { get; } = [];

        public OrgChart()
        {
            var director = Seat(TenantA, "director", reportsTo: null);
            var manager = Seat(TenantA, "manager", reportsTo: director.Id);
            var rep = Seat(TenantA, "rep", reportsTo: manager.Id);
            var colleague = Seat(TenantA, "colleague", reportsTo: director.Id);
            var loner = Seat(TenantA, "loner", reportsTo: null);
            Hold(TenantA, Director, director, "director@a.test", "tr");
            Hold(TenantA, Manager, manager, "manager@a.test", "tr");
            Hold(TenantA, Rep, rep, "rep@a.test", "tr");
            Hold(TenantA, EnglishColleague, colleague, "colleague@a.test", "en-GB");
            Hold(TenantA, Loner, loner, "loner@a.test", null);

            var foreignManager = Seat(TenantB, "manager", reportsTo: null);
            var foreignRep = Seat(TenantB, "rep", reportsTo: foreignManager.Id);
            Hold(TenantB, ForeignManager, foreignManager, "manager@b.test", "tr");
            Hold(TenantB, ForeignRep, foreignRep, "rep@b.test", "tr");
        }

        public void Vacate(Guid user) => Assignments.RemoveAll(a => a.UserId == user);

        private Position Seat(Guid tenant, string code, Guid? reportsTo)
        {
            var position = new Position
            {
                Id = Guid.NewGuid(), TenantId = tenant, Code = code, Name = code, OrganizationUnitId = Guid.NewGuid(),
                ReportsToPositionId = reportsTo
            };
            Positions.Add(position);
            return position;
        }

        private void Hold(Guid tenant, Guid user, Position position, string email, string? language)
        {
            Assignments.Add(new PositionAssignment
            {
                Id = Guid.NewGuid(), TenantId = tenant, UserId = user, PositionId = position.Id,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30), AssignmentType = AssignmentType.Primary
            });
            Addresses[(tenant, user)] = (email, language);
        }
    }

    /// <summary>The tenant execution filter: the AMBIENT tenant's rows only — and an unresolved context throws, as
    /// <see cref="TenantContext.TenantId"/> does in production.</summary>
    private sealed class TenantAssignments(OrgChart org, ITenantContext tenant) : IPositionAssignmentRepository
    {
        public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PositionAssignment>>(org.Assignments.Where(a => a.TenantId == tenant.TenantId && !a.IsDeleted).ToList());

        public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(org.Assignments.FirstOrDefault(a => a.Id == id && a.TenantId == tenant.TenantId));

        public Task<PositionAssignment> CreateAsync(PositionAssignment assignment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(PositionAssignment assignment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class TenantPositions(OrgChart org, ITenantContext tenant) : IPositionRepository
    {
        public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(org.Positions.FirstOrDefault(p => p.Id == id && p.TenantId == tenant.TenantId && !p.IsDeleted));

        public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Position>>(org.Positions.Where(p => p.TenantId == tenant.TenantId && !p.IsDeleted).ToList());

        public Task<Position> CreateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Position position, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>AuthService's contact lookup: scoped by the ambient tenant, as the real client sends it; unknown ids are
    /// omitted.</summary>
    internal sealed class Contacts(OrgChart org)
    {
        public int Calls { get; private set; }

        public ITaskNotificationRecipientResolver For(ITenantContext tenant) => new Scoped(this, org, tenant);

        private sealed class Scoped(Contacts owner, OrgChart org, ITenantContext tenant) : ITaskNotificationRecipientResolver
        {
            public Task<IReadOnlyList<TaskNotificationRecipient>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            {
                owner.Calls++;
                var t = tenant.TenantId;
                return Task.FromResult<IReadOnlyList<TaskNotificationRecipient>>(userIds
                    .Where(id => org.Addresses.ContainsKey((t, id)))
                    .Select(id => new TaskNotificationRecipient(id, org.Addresses[(t, id)].Email, null, org.Addresses[(t, id)].Language))
                    .ToList());
            }
        }
    }

    internal sealed class Tenants : ITenantRegistryRepository
    {
        public Dictionary<Guid, TenantStatus> Statuses { get; } = new() { [TenantA] = TenantStatus.Active, [TenantB] = TenantStatus.Active };
        public int Reads { get; private set; }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult(Statuses.TryGetValue(id, out var status)
                ? new Tenant { Id = id, Code = "T", Slug = "t", Name = "t", DisplayName = "T", Domain = "t.test", Status = status }
                : null);
        }

        public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByDomainAsync(string domain, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> GetActiveTenantsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<Tenant> CreateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateStatusAsync(Guid id, TenantStatus status, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<(IReadOnlyList<Tenant> Items, long TotalCount)> QueryAsync(TenantListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TenantRegistryStats> GetStatsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>The event catalog as the REAL manifest sync writes it.</summary>
    internal sealed class Catalog : INotificationEventDefinitionRepository
    {
        private readonly List<NotificationEventDefinition> _items = [];

        public static Catalog FromManifests(params IModuleManifestProvider[] providers)
        {
            var catalog = new Catalog();
            var result = new NotificationEventManifestSyncService(providers, catalog).SyncAsync().GetAwaiter().GetResult();
            // The CRM events sync clean. (Meetings' three organizer-* codes carry a hyphen the catalogue refuses — a defect
            // of their own, outside this test; the Meetings event used below, platform.meetings.invite, is valid.)
            var crm = result.Items.Where(i => i.EventCode.StartsWith("crm.", StringComparison.Ordinal)).ToList();
            Assert.Equal(4, crm.Count);
            Assert.True(crm.All(i => i.Issues.Count == 0), string.Join("; ", crm.SelectMany(i => i.Issues)));
            return catalog;
        }

        public NotificationEventDefinition Single(string code) => _items.Single(e => e.EventCode == code);

        public Task<NotificationEventDefinition> CreateAsync(NotificationEventDefinition definition, CancellationToken ct = default)
        {
            _items.Add(definition);
            return Task.FromResult(definition);
        }

        public Task UpdateAsync(NotificationEventDefinition definition, CancellationToken ct = default) => Task.CompletedTask;
        public Task<NotificationEventDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_items.FirstOrDefault(e => e.Id == id));
        public Task<NotificationEventDefinition?> GetByEventCodeAsync(string eventCode, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(e => string.Equals(e.EventCode, eventCode, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyList<NotificationEventDefinition>> ListAsync(string? ownerModuleId = null, NotificationChannelCode? channel = null,
            NotificationEventStatus? status = null, bool? canTenantOverride = null, NotificationEventUsageType? usageType = null,
            int skip = 0, int take = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(_items);
        public Task<IReadOnlyList<NotificationEventDefinition>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationEventDefinition>>(_items.Where(e => e.Status == NotificationEventStatus.Active).ToList());
    }

    internal sealed class RecordingAdapter : INotificationEventDispatchAdapter
    {
        public List<NotificationEventDispatchRequest> Requests { get; } = [];
        public (int Status, string Reason)? FailWith { get; set; }

        public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(NotificationEventDispatchRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            if (FailWith is { } failure)
            {
                return Task.FromResult(Response<NotificationDispatchDto>.Fail("refused", failure.Status, failure.Reason));
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(Response<NotificationDispatchDto>.Success(new NotificationDispatchDto(
                Guid.NewGuid(), request.TenantId, request.EventCode, null, request.Locale ?? "en", "Email", "Fake", null, "Sent",
                [], request.Cc?.Count ?? 0, 0, "subject", null, null, "{}", now, now, null, 0, null, null, request.CorrelationId)));
        }
    }
}
