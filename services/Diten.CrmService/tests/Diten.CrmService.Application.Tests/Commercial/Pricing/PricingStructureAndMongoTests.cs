using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Diten.CrmService.Api.Controllers.Commercial;
using Diten.CrmService.Application.Features.Commercial.Pricing;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;
using Diten.CrmService.Infrastructure.Authorization;
using Diten.CrmService.Persistence.Repositories.Commercial;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.Commercial.Pricing;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156: every endpoint carries a §14 key (the two dictionary reads check ANY read key and refuse
/// without one), the keys are exactly the §14 sixteen, the arithmetic lives in Evaluation/ only (AC-EVAL-3), and on a real
/// mongod the class maps round-trip (string Guids, "yyyy-MM-dd" dates) and the unique indexes hold.
/// </summary>
public sealed class PricingStructureAndMongoTests
{
    private static readonly Type[] Controllers =
        [typeof(PriceListsController), typeof(PriceListImportsController), typeof(PricingRulesController), typeof(FxRatesController), typeof(PricingHistoryController)];

    private static IEnumerable<MethodInfo> Actions(Type controller)
        => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any());

    [Fact]
    public void S1_every_pricing_action_carries_a_section_14_key_except_the_two_any_key_reads()
    {
        string[] anyKey = ["PricingHistoryController.History", "PricingHistoryController.Contract"];
        var actions = Controllers.SelectMany(c => Actions(c).Select(m => (Name: $"{c.Name}.{m.Name}", Method: m))).ToList();
        Assert.True(actions.Count >= 29, $"only {actions.Count} actions");

        foreach (var (name, method) in actions)
        {
            var keys = method.GetCustomAttributes<HasPermissionAttribute>().Select(a => a.Permission).ToList();
            if (anyKey.Contains(name))
            {
                Assert.Empty(keys);
                Assert.NotNull(method.DeclaringType!.GetCustomAttribute<AuthorizeAttribute>());   // still signed in
                continue;
            }

            var key = Assert.Single(keys);
            Assert.Contains(key, PricingPermissions.All);
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        // every key guards something
        var used = actions.SelectMany(a => a.Method.GetCustomAttributes<HasPermissionAttribute>()).Select(a => a.Permission).ToHashSet();
        Assert.Equal(PricingPermissions.All.OrderBy(k => k), used.OrderBy(k => k));
    }

    [Fact]
    public void S2_the_keys_are_the_section_14_sixteen()
    {
        Assert.Equal(new[]
        {
            "commercial.fx-rates.create", "commercial.fx-rates.import", "commercial.fx-rates.read", "commercial.fx-rates.update",
            "commercial.price-lists.create", "commercial.price-lists.export", "commercial.price-lists.import", "commercial.price-lists.publish",
            "commercial.price-lists.read", "commercial.price-lists.submit", "commercial.price-lists.update",
            "commercial.pricing-rules.create", "commercial.pricing-rules.publish", "commercial.pricing-rules.read", "commercial.pricing-rules.submit",
            "commercial.pricing-rules.update"
        }, PricingPermissions.All.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(new string[0], 403, 403, null)]
    [InlineData(new[] { "commercial.fx-rates.read" }, 200, 403, "fx-rate")]
    [InlineData(new[] { "commercial.pricing-rules.read" }, 200, 200, "pricing-rule")]
    public async Task S3_the_any_key_reads_refuse_without_a_read_key_and_the_history_shows_only_the_covered_types(string[] keys, int history, int contract, string? only)
    {
        var mediator = new RecordingMediator();
        var controller = new PricingHistoryController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(keys.Select(k => new Claim("permission", k)), "test")) }
            }
        };

        var h = await controller.History(null, null, null, null, null, null, null, null, default);
        var c = await controller.Contract(default);

        Assert.Equal(history, Status(h));
        Assert.Equal(contract, Status(c));
        if (only is not null)
        {
            var query = Assert.IsType<Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory.GetPricingChangeLogQuery>(mediator.Sent.First());
            Assert.Equal(new[] { only }, query.AllowedTypes);
        }
    }

    private static int Status(IActionResult result) => result is ObjectResult o ? o.StatusCode ?? 200 : result is StatusCodeResult s ? s.StatusCode : 200;

    [Fact]
    public void S4_the_guardrail_arithmetic_lives_only_in_Evaluation()
    {
        var root = FindPricingFolder();
        var arithmetic = new Regex(@"Math\.(Round|Floor|Ceiling|Truncate)\(|\b1m?\s*-\s*\(|\)\s*\*\s*\(1m?\s*-|/\s*\(\s*\w+\.Buy\s*\+", RegexOptions.Compiled);
        var offenders = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(root, f).StartsWith("Evaluation" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
            .Where(x => arithmetic.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
        Assert.True(File.Exists(Path.Combine(root, "Evaluation", "PricingMath.cs")));
    }

    private static string FindPricingFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Diten.CrmService.Application", "Diten.CrmService.Application.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Diten.CrmService.Application", "Features", "Commercial", "Pricing");
    }

    // ═══ the real mongod (the lane's own port; skipped without CRM_TEST_MONGO_URI) ═════════════════════════════

    [Fact]
    public async Task M1_on_a_real_mongod_the_maps_round_trip_and_the_unique_indexes_hold()
    {
        var uri = Environment.GetEnvironmentVariable("CRM_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri) || uri.Contains(":27017", StringComparison.Ordinal)) return; // never the dev database
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var client = new MongoClient(uri);
        var name = "W6_BEB_" + Guid.NewGuid().ToString("N");
        try
        {
            var db = client.GetDatabase(name);
            typeof(Diten.CrmService.Persistence.DependencyInjection)
                .GetMethod("TryEnsureIndexes", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [db]);
            var tenant = PricingWorld.Tenant;
            var lists = new PriceListRepository(db);
            var items = new PriceListItemRepository(db);
            var rules = new PricingRuleRepository(db);
            var fx = new FxRateRepository(db);
            var log = new PricingChangeLogRepository(db);

            // a published list round-trips: Guids as strings, the dates as yyyy-MM-dd
            var list = new PriceList
            {
                TenantId = tenant, Name = "TR Resmî", LegalEntityId = PricingWorld.LeTr, CountryCode = "TR", Currency = "TRY", Kind = PriceListKinds.Official,
                ValidFrom = new DateOnly(2026, 11, 1), ValidTo = new DateOnly(2026, 11, 30), Status = PriceListStatuses.Published, CreatedBy = "a", PublishedBy = "b"
            };
            list.FamilyId = list.Id;
            await lists.InsertAsync(list, default);
            var raw = await db.GetCollection<BsonDocument>(PriceListRepository.CollectionName).Find(new BsonDocument()).SingleAsync();
            Assert.Equal(BsonType.String, raw["TenantId"].BsonType);
            Assert.Equal(PricingWorld.LeTr.ToString("D"), raw["LegalEntityId"].AsString);
            Assert.Equal("2026-11-01", raw["ValidFrom"].AsString);
            var back = await lists.GetByIdAsync(tenant, list.Id, default);
            Assert.Equal((list.FamilyId, (DateOnly?)new DateOnly(2026, 11, 30)), (back!.FamilyId, back.ValidTo));
            Assert.Null(await lists.GetByIdAsync(PricingWorld.OtherTenant, list.Id, default));
            Assert.Single(await lists.ListPublishedAsync(tenant, PricingWorld.LeTr, "TR", default));
            Assert.Empty(await lists.ListPublishedAsync(PricingWorld.OtherTenant, PricingWorld.LeTr, "TR", default));

            // one row per (list, SKU)
            PriceListItem Item() => new() { TenantId = tenant, PriceListId = list.Id, SkuId = PricingWorld.Sku("TUT"), PharmacistPurchasePrice = 412.60m, VatRate = 10m };
            await items.InsertAsync(Item(), default);
            await Assert.ThrowsAnyAsync<MongoException>(() => items.InsertAsync(Item(), default));
            Assert.Equal(412.60m, (await items.GetBySkuAsync(tenant, list.Id, PricingWorld.Sku("TUT"), default))!.PharmacistPurchasePrice);

            // one document per (rule key, version); the active read filters the tenant
            var rule = new PricingRule
            {
                TenantId = tenant, RuleKey = Guid.NewGuid(), RuleVersion = 1, LegalEntityId = PricingWorld.LeTr, SkuId = PricingWorld.Sku("TUT"),
                Status = PricingRuleStatuses.Active, MaxDiscountPct = new PricingPctLimit { Soft = 10, Hard = 15 },
                MaxMf = new PricingMfLimit { Soft = new PricingMfTerm { Buy = 10, Free = 2 } }, ValidFrom = new DateOnly(2026, 11, 1)
            };
            await rules.InsertAsync(rule, default);
            await Assert.ThrowsAnyAsync<MongoException>(() => rules.InsertAsync(new PricingRule
            {
                TenantId = tenant, RuleKey = rule.RuleKey, RuleVersion = 1, LegalEntityId = PricingWorld.LeTr, SkuId = PricingWorld.Sku("TUT")
            }, default));
            var active = Assert.Single(await rules.ListActiveAsync(tenant, PricingWorld.LeTr, [PricingWorld.Sku("TUT")], default));
            Assert.Equal((2, (decimal?)15m), (active.MaxMf.Soft!.Free, active.MaxDiscountPct.Hard));
            Assert.Empty(await rules.ListActiveAsync(PricingWorld.OtherTenant, PricingWorld.LeTr, [PricingWorld.Sku("TUT")], default));

            // one rate per (legal entity, pair, date); the latest on or before asOf
            FxRate Rate(DateOnly day, decimal value) => new() { TenantId = tenant, LegalEntityId = PricingWorld.LeAe, FromCurrency = "USD", ToCurrency = "AED", RateDate = day, Rate = value, Source = "manual" };
            Assert.True(await fx.TryInsertAsync(Rate(new DateOnly(2026, 10, 1), 3.6725m), default));
            Assert.True(await fx.TryInsertAsync(Rate(new DateOnly(2026, 10, 15), 3.6730m), default));
            Assert.False(await fx.TryInsertAsync(Rate(new DateOnly(2026, 10, 15), 3.70m), default));
            Assert.Equal(3.6725m, (await fx.GetLatestAsync(tenant, PricingWorld.LeAe, "USD", "AED", new DateOnly(2026, 10, 14), default))!.Rate);
            Assert.Null(await fx.GetLatestAsync(PricingWorld.OtherTenant, PricingWorld.LeAe, "USD", "AED", new DateOnly(2026, 10, 20), default));

            // the change log reads back by entity, this tenant only
            await log.AppendAsync([new PricingChangeLog
            {
                TenantId = tenant, EntityType = PricingChangeEntityTypes.PriceList, EntityId = list.Id, EntityVersion = 1, Action = "published", ByUserId = "b",
                At = DateTimeOffset.UtcNow, Changes = [new PricingFieldChange { Field = "status", Old = "in_review", New = "published" }]
            }], default);
            Assert.Equal(1, (await log.ListAsync(tenant, new PricingChangeLogFilter(EntityId: list.Id), 0, 10, default)).Total);
            Assert.Equal(0, (await log.ListAsync(PricingWorld.OtherTenant, new PricingChangeLogFilter(EntityId: list.Id), 0, 10, default)).Total);
        }
        finally
        {
            await client.DropDatabaseAsync(name);
        }
    }

    private sealed class RecordingMediator : IMediator
    {
        public List<object> Sent { get; } = new();

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            object response = request switch
            {
                Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory.GetPricingChangeLogQuery =>
                    Diten.CrmService.Application.Common.Models.Response<Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory.PricingChangePageDto>
                        .Success(new([], 0, 1, 25)),
                _ => Diten.CrmService.Application.Common.Models.Response<Diten.CrmService.Application.Features.Commercial.Pricing.PricingHistory.PricingContractDto>
                    .Success(null!)
            };
            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotSupportedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}
