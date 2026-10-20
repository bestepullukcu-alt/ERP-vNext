using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.CrmService.Api.Controllers.Commercial;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Application.Tests.Commercial.Pricing;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Domain.Repositories.Commercial;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Infrastructure.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.CrmService.Infrastructure.Workflow;
using Diten.CrmService.Persistence.Repositories.Commercial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168: every endpoint carries one §14 key and every key guards something (AC-SEC-1); no MF /
/// discount / combined / VAT arithmetic in the module (AC-PRICE-2) and no state change outside the state machine (§10);
/// the Work Center event route; the runtime candidates on the MOD-0023 start body (the shared client stays unchanged for
/// every other caller); and on a real mongod the maps round-trip and the counter / link / indexes hold under concurrency.
/// </summary>
public sealed class PreOrderStructureAndMongoTests : PreOrderTestBase
{
    private static readonly Type[] Controllers = [typeof(PreOrdersController), typeof(PreOrderSettingsController), typeof(PreOrderApprovalLinksController)];

    [Fact]
    public void S1_every_pre_order_action_carries_exactly_one_section_14_key_and_every_key_guards_something()
    {
        var actions = Controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any())).ToList();
        Assert.Equal(19, actions.Count);                                          // §15 minus the document (W6-BE-c)
        foreach (var action in actions)
        {
            var key = Assert.Single(action.GetCustomAttributes<HasPermissionAttribute>()).Permission;
            Assert.Contains(key, PreOrderPermissions.All);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        Assert.All(Controllers, c => Assert.NotNull(c.GetCustomAttribute<AuthorizeAttribute>()));
        // read-all is not an endpoint key: it widens the read scope inside the handlers (§4.8)
        Assert.Equal(PreOrderPermissions.All.Where(k => k != PreOrderPermissions.ReadAll).OrderBy(k => k),
            actions.Select(a => a.GetCustomAttribute<HasPermissionAttribute>()!.Permission).Distinct().OrderBy(k => k));
        Assert.Contains("PreOrderPermissions.ReadAll", File.ReadAllText(Path.Combine(CrmSourceTree.ApplicationRoot(), "Features", "Commercial", "PreOrders", "PreOrderContext.cs")));
        Assert.Equal(new[]
        {
            "commercial.pre-order-settings.read", "commercial.pre-order-settings.update", "commercial.pre-orders.approve", "commercial.pre-orders.cancel",
            "commercial.pre-orders.create", "commercial.pre-orders.export", "commercial.pre-orders.read", "commercial.pre-orders.read-all",
            "commercial.pre-orders.record-depot-response", "commercial.pre-orders.retry-transmission", "commercial.pre-orders.submit"
        }, PreOrderPermissions.All.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void S2_the_module_holds_no_pricing_arithmetic_and_changes_state_only_in_the_state_machine()
    {
        var root = Path.Combine(CrmSourceTree.ApplicationRoot(), "Features", "Commercial", "PreOrders");
        var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.True(files.Count >= 10, root);
        var arithmetic = new Regex(@"Math\.(Round|Floor|Ceiling|Truncate)\(|\b1m?\s*-\s*\(|\)\s*\*\s*\(1m?\s*-|/\s*\(\s*\w+\.Buy\s*\+|/\s*100m?\b|\*\s*100m?\b|\.VatRate\s*\*|\*\s*\w*\.?VatRate",
            RegexOptions.Compiled);
        var statusWrite = new Regex(@"\.Status\s*=(?!=)", RegexOptions.Compiled);
        var offenders = files.SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
            .Where(x => arithmetic.IsMatch(x.line)
                        || (statusWrite.IsMatch(x.line) && !x.f.EndsWith("PreOrderStateMachine.cs", StringComparison.Ordinal) && !x.line.Contains("Status = status")
                            && !x.line.Contains("Status = sent ?") && !x.line.Contains("LastStatus")))
            .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.i + 1}: {x.line.Trim()}").ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        Assert.Contains(files, f => File.ReadAllText(f).Contains("IPricingEvaluator"));                  // the ONE source of the numbers
        Assert.DoesNotContain(files, f => Regex.IsMatch(File.ReadAllText(f), @"Features\.Commercial\.Pricing\.(?!Contracts)"));
    }

    [Fact]
    public async Task S3_the_Work_Center_completion_event_is_routed_to_the_pre_order_and_other_kinds_are_untouched()
    {
        var world = W;
        var pending = await Pending();
        var consumer = new ClaimWorkflowOutcomeConsumer(
            new ClaimReviewOutcomeApplier(System.Reflection.DispatchProxy.Create<IClaimRepository, ThrowingProxy>(), new EmptyCountryVersionRepo()),
            new Inbox(), NullLogger<ClaimWorkflowOutcomeConsumer>.Instance, preOrderApproval: world.Get<PreOrderApprovalService>());

        var message = Completed("commercial.pre-order", pending.Id, pending.Approval!.WorkflowInstanceId!.Value, ClaimReviewOutcomes.Approved,
            PreOrderWorld.Manager2.ToString("D"));
        await consumer.ConsumeAsync(message);
        await consumer.ConsumeAsync(message);                                        // the inbox drops the replay

        var stored = world.Stored(pending.Id);
        Assert.Equal((PreOrderStatuses.Transmitted, PreOrderWorld.Manager2), (stored.Status, stored.Approval!.DecidedByUserId!.Value));
        Assert.Single(stored.Events, e => e.Type == PreOrderEventTypes.Approved);
    }

    [Fact]
    public async Task S4_the_start_body_carries_the_runtime_candidates_only_when_a_caller_names_them()
    {
        var bodies = new List<string>();
        var client = new GatewayClaimWorkflowClient(new HttpClient(new StartHandler(bodies)) { BaseAddress = new Uri("http://gw.test") },
            new ConfigurationBuilder().Build(), new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<GatewayClaimWorkflowClient>.Instance);
        var display = new ClaimWorkflowDisplayContext("t", null, "MOD-0168", "/x", []);

        await client.StartAsync(new ClaimWorkflowStartRequest("T", "crm.claim", "1", "r", "k1", display), default);
        await client.StartAsync(new ClaimWorkflowStartRequest("T", "commercial.pre-order", "2", "r", "k2", display, "R",
            ["b2000000-0000-4000-8000-000000000001", "b2000000-0000-4000-8000-000000000001", " "]), default);

        Assert.Equal(0, JsonDocument.Parse(bodies[0]).RootElement.GetProperty("candidatePrincipalIds").GetArrayLength());
        Assert.Equal(new[] { "b2000000-0000-4000-8000-000000000001" },
            JsonDocument.Parse(bodies[1]).RootElement.GetProperty("candidatePrincipalIds").EnumerateArray().Select(e => e.GetString()));
    }

    // ═══ the real mongod (the lane's own port; skipped without CRM_TEST_MONGO_URI) ═════════════════════════════

    [Fact]
    public async Task M1_on_a_real_mongod_the_maps_round_trip_the_counter_is_atomic_one_link_consumer_wins_and_the_scope_filters_hold()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W6_BEA_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            typeof(Diten.CrmService.Persistence.DependencyInjection).GetMethod("TryEnsureIndexes", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [db]);
            var tenant = PreOrderWorld.Tenant;
            var orders = new PreOrderRepository(db);
            var links = new PreOrderApprovalLinkRepository(db);
            var sequences = new PreOrderCodeSequenceRepository(db);
            var settings = new PreOrderSettingsRepository(db);

            // the ORD counter: 40 concurrent numbers are 1..40, another prefix / tenant counts on its own
            var numbers = await Task.WhenAll(Enumerable.Range(0, 40).Select(_ => sequences.NextAsync(tenant, "MG", default)));
            Assert.Equal(Enumerable.Range(1, 40).Select(i => (long)i), numbers.OrderBy(n => n));
            Assert.Equal(1, await sequences.NextAsync(tenant, "GMAE", default));
            Assert.Equal(1, await sequences.NextAsync(PreOrderWorld.OtherTenant, "MG", default));
            Assert.True(await sequences.ExistsAsync(tenant, "MG", default));
            Assert.False(await sequences.ExistsAsync(tenant, "ZZ", default));

            // the order round-trips: Guids (lists too) as strings, dates as yyyy-MM-dd
            var order = new PreOrder
            {
                TenantId = tenant, Code = "MG-ORD-000041", Status = PreOrderStatuses.PendingApproval, PriceMode = PreOrderPriceModes.Priced,
                Seller = new PreOrderSeller { LegalEntityId = PreOrderWorld.LeTr, Prefix = "MG", Code = "GM-TR", Name = "GM" }, CountryCode = "TR",
                LocalCurrencyCode = "TRY", Currency = "TRY", Pharmacy = new PreOrderParty { AccountId = PreOrderWorld.Pharmacy, Name = "Eczane Şifa" },
                Depot = new PreOrderParty { AccountId = PreOrderWorld.Depot, Name = "Selçuk" }, Rep = new PreOrderRep { UserId = PreOrderWorld.Rep },
                RequestedDeliveryDate = new DateOnly(2026, 11, 2),
                Lines = [new PreOrderLine { LineNo = 1, SkuId = PricingWorld.Sku("TUT"), Quantity = 20, Mf = new PreOrderMf { Buy = 10, Free = 2 }, UnitPrice = 412.60m,
                    Findings = [new PreOrderFinding { Code = "mf.soft-exceeded", Severity = "soft", Params = new() { ["limit"] = "10+2" } }] }],
                Price = new PreOrderPriceSnapshot { PriceListId = Guid.NewGuid(), PriceListVersion = 1, ValidFrom = new DateOnly(2026, 10, 1), Tier = "pharmacist-purchase" },
                Fx = new PreOrderFxSnapshot { LocalCurrency = "AED", Rate = 3.6725m, RateDate = new DateOnly(2026, 10, 19), Source = "m", LocalGross = 1m },
                ApprovalRequirement = new PreOrderApprovalRequirement { Required = true, ApproverUserIds = [PreOrderWorld.Manager, PreOrderWorld.Manager2] },
                Approval = new PreOrderApproval { WorkflowInstanceId = Guid.NewGuid(), SubmissionNo = 1 }
            };
            await orders.InsertAsync(order, default);
            var raw = await db.GetCollection<BsonDocument>(PreOrderRepository.CollectionName).Find(new BsonDocument()).SingleAsync();
            Assert.Equal(PreOrderWorld.Manager.ToString("D"), raw["ApprovalRequirement"]["ApproverUserIds"][0].AsString);
            Assert.Equal(PreOrderWorld.Pharmacy.ToString("D"), raw["Pharmacy"]["AccountId"].AsString);
            Assert.Equal("2026-11-02", raw["RequestedDeliveryDate"].AsString);
            Assert.Equal("2026-10-19", raw["Fx"]["RateDate"].AsString);
            var back = await orders.GetByIdAsync(tenant, order.Id, default);
            Assert.Equal((412.60m, "10+2", 2), (back!.Lines[0].UnitPrice, back.Lines[0].Findings[0].Params["limit"], back.ApprovalRequirement!.ApproverUserIds.Count));
            Assert.Null(await orders.GetByIdAsync(PreOrderWorld.OtherTenant, order.Id, default));
            await Assert.ThrowsAnyAsync<MongoException>(() => orders.InsertAsync(new PreOrder { TenantId = tenant, Code = order.Code }, default));

            // optimistic replace
            back.Note = "x";
            Assert.True(await orders.ReplaceAsync(back, back.Version, default));
            Assert.False(await orders.ReplaceAsync(back, back.Version - 1, default));

            // the scope filters: rep set OR approver; awaiting me; by SKU; another tenant never
            var mine = await orders.ListAsync(tenant, new PreOrderFilter(RepUserIds: [PreOrderWorld.OtherRep], OrApproverUserId: PreOrderWorld.Manager2), 0, 10, default);
            var awaiting = await orders.ListAsync(tenant, new PreOrderFilter(AwaitingApproverUserId: PreOrderWorld.Manager), 0, 10, default);
            var bySku = await orders.ListAsync(tenant, new PreOrderFilter(SkuId: PricingWorld.Sku("ALM")), 0, 10, default);
            var foreign = await orders.ListAsync(PreOrderWorld.OtherTenant, new PreOrderFilter(), 0, 10, default);
            Assert.Equal((1L, 1L, 0L, 0L), (mine.Total, awaiting.Total, bySku.Total, foreign.Total));

            // the link: one consumer among many; superseded links cannot be consumed
            var link = new PreOrderApprovalLink { TenantId = tenant, PreOrderId = order.Id, SubmissionNo = 1, TokenHash = new string('a', 64),
                ApproverUserIds = [PreOrderWorld.Manager], ExpiresAt = DateTimeOffset.UtcNow.AddHours(48) };
            await links.InsertAsync(link, default);
            await Assert.ThrowsAnyAsync<MongoException>(() => links.InsertAsync(new PreOrderApprovalLink { TenantId = tenant, TokenHash = link.TokenHash }, default));
            var wins = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => links.TryConsumeAsync(tenant, link.Id, PreOrderWorld.Manager, DateTimeOffset.UtcNow, default)));
            Assert.Single(wins, w => w);
            Assert.Equal(PreOrderWorld.Manager, (await links.GetByHashAsync(tenant, link.TokenHash, default))!.ConsumedByUserId);
            var other = new PreOrderApprovalLink { TenantId = tenant, PreOrderId = order.Id, SubmissionNo = 1, TokenHash = new string('b', 64), ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) };
            await links.InsertAsync(other, default);
            await links.SupersedeAsync(tenant, order.Id, DateTimeOffset.UtcNow, default);
            Assert.False(await links.TryConsumeAsync(tenant, other.Id, PreOrderWorld.Manager, DateTimeOffset.UtcNow, default));
            Assert.Null(await links.GetByHashAsync(PreOrderWorld.OtherTenant, link.TokenHash, default));

            // settings: one document per tenant (a concurrent first save loses), then optimistic
            Assert.True(await settings.SaveAsync(new PreOrderSettings { TenantId = tenant, CompanyPrefixes = [new() { LegalEntityId = PreOrderWorld.LeTr, Prefix = "MG" }] }, null, default));
            Assert.False(await settings.SaveAsync(new PreOrderSettings { TenantId = tenant }, null, default));
            var stored = await settings.GetAsync(tenant, default);
            Assert.Equal((1, "MG", PreOrderWorld.LeTr), (stored!.Version, stored.CompanyPrefixes[0].Prefix, stored.CompanyPrefixes[0].LegalEntityId));
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    private static EventTransportMessage Completed(string objectType, Guid objectId, Guid instanceId, string outcome, string completedBy)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid(), tenantId = PreOrderWorld.Tenant, workflowInstanceId = instanceId, templateCode = "COM-PREORDER-APPROVAL", objectType,
            objectId = objectId.ToString("D"), objectRef = "x", outcome, completedAt = DateTimeOffset.UtcNow, completedBy, reasonCode = "COMMERCIAL_PRE_ORDER_APPROVED"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new EventTransportMessage(Guid.NewGuid(), ClaimWorkflowOutcomeConsumer.CompletedEventName, 1, Guid.NewGuid(), null, PreOrderWorld.Tenant,
            "Diten.Platform", DateTimeOffset.UtcNow, payload);
    }

    private sealed class Inbox : ICrmEventInboxRepository
    {
        private readonly HashSet<Guid> _seen = new();
        public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default) => Task.FromResult(_seen.Add(eventId));
    }

    private sealed class StartHandler(List<string> bodies) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            var json = JsonSerializer.Serialize(new { data = new { workflowInstanceId = Guid.NewGuid() }, isSuccessful = true, statusCode = 201 });
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

}
