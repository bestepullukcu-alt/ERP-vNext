using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Expiry;
using Diten.InventoryService.Api.Expiry;
using Diten.InventoryService.Application.Features.Expiry;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Repositories;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Infrastructure.Traceability;
using Diten.InventoryService.Infrastructure.Upstream;
using Diten.InventoryService.Tests.Architecture;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>WP-0176-F1 — the pure rules (pack §4.1, §8.4, §12; CT-F10) measured on the production functions.</summary>
public sealed class ExpiryRulesTests
{
    [Fact]
    public void The_defaults_are_the_owner_decisions_and_the_CT_accepted_assumptions()
    {
        var d = ExpiryPolicyRules.Defaults();
        Assert.Equal((90, 30), (d.ApproachingDays, d.CriticalDays));
        Assert.Equal((FefoOutboundMode.RequireReason, BelowMinRemainingMode.AutoQuarantine, DateOverrideMode.ReasonAndSignature),
            (d.FefoOutboundMode, d.BelowMinRemainingMode, d.DateOverrideMode));
        Assert.Equal(new Dictionary<string, int> { ["FEFO_BYPASS"] = 24, ["BELOW_MIN_REMAINING"] = 72, ["EXPIRY_DATE_OVERRIDE"] = 48, ["EXPIRED_ON_HAND"] = 120 },
            d.SlaHours);
        Assert.Equal(14, d.ExpiredOnHandThresholdDays);
        Assert.Null(ExpiryPolicyRules.Validate(d));
    }

    // Every (status, command) pair of pack §8.4 — the whole matrix, so a widened transition is caught.
    [Theory]
    [InlineData(ExpiryExceptionStatus.Open, true, true, false, true)]
    [InlineData(ExpiryExceptionStatus.InReview, false, true, false, true)]
    [InlineData(ExpiryExceptionStatus.Accepted, false, false, true, true)]
    [InlineData(ExpiryExceptionStatus.Fixed, false, false, true, true)]
    [InlineData(ExpiryExceptionStatus.Closed, false, false, false, false)]
    [InlineData(ExpiryExceptionStatus.Cancelled, false, false, false, false)]
    public void The_state_machine_matrix(ExpiryExceptionStatus status, bool take, bool resolve, bool close, bool comment)
    {
        Assert.Equal(take, ExpiryExceptionRules.CanTake(status));
        Assert.Equal(resolve, ExpiryExceptionRules.CanResolve(status));
        Assert.Equal(close, ExpiryExceptionRules.CanClose(status));
        Assert.Equal(comment, ExpiryExceptionRules.CanComment(status));
        Assert.Equal(status is ExpiryExceptionStatus.Closed or ExpiryExceptionStatus.Cancelled, ExpiryExceptionRules.IsTerminal(status));
    }

    [Theory]
    [InlineData(ExpiryResolution.AcceptedWithReason, ExpiryExceptionStatus.Accepted, "expiry.exception.accept")]
    [InlineData(ExpiryResolution.Fixed, ExpiryExceptionStatus.Fixed, "expiry.exception.fix")]
    [InlineData(ExpiryResolution.Cancelled, ExpiryExceptionStatus.Cancelled, "expiry.exception.cancel")]
    public void A_resolution_lands_in_its_status_with_its_signable_action(ExpiryResolution resolution, ExpiryExceptionStatus status, string action)
    {
        Assert.Equal(status, ExpiryExceptionRules.StatusAfter(resolution));
        Assert.Equal(action, ExpiryExceptionRules.ActionCode(resolution));
    }

    [Fact]
    public void SoD_refuses_only_the_origin_user()
    {
        var origin = Guid.NewGuid();
        Assert.Equal("EXPIRY_SOD_VIOLATION", ExpiryExceptionRules.SeparationOfDuties(new FefoExceptionCase { OriginUserId = origin }, origin)?.Code);
        Assert.Null(ExpiryExceptionRules.SeparationOfDuties(new FefoExceptionCase { OriginUserId = origin }, Guid.NewGuid()));
        Assert.Null(ExpiryExceptionRules.SeparationOfDuties(new FefoExceptionCase { OriginUserId = null }, origin)); // system case
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("123456789", false)]
    [InlineData("  123456789  ", false)]
    [InlineData("1234567890", true)]
    public void Reason_is_10_to_1000_after_trim(string? reason, bool ok)
        => Assert.Equal(ok, ExpiryExceptionRules.Reason(reason) is null);

    [Fact]
    public void Late_and_due_soon_follow_the_due_time_while_awaiting_a_decision()
    {
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
        var c = new FefoExceptionCase { Status = ExpiryExceptionStatus.Open, DueAtUtc = now.AddHours(5) };
        Assert.True(ExpiryExceptionRules.IsDueSoon(c, now));
        Assert.False(ExpiryExceptionRules.IsLate(c, now));
        Assert.True(ExpiryExceptionRules.IsLate(c, now.AddHours(6)));
        c.Status = ExpiryExceptionStatus.Accepted;
        Assert.False(ExpiryExceptionRules.IsLate(c, now.AddHours(6)));
        Assert.Equal(now.AddHours(24), ExpiryExceptionRules.DueAt(now, ExpiryPolicyRules.Defaults(), ExpiryExceptionType.FefoBypass));
    }

    [Fact]
    public void Changes_lists_each_changed_field_once()
    {
        var before = ExpiryPolicyRules.Defaults();
        var after = before.Copy();
        after.CriticalDays = 21;
        after.SlaHours["EXPIRED_ON_HAND"] = 96;
        Assert.Equal([("criticalDays", "30", "21"), ("slaHours.EXPIRED_ON_HAND", "120", "96")], ExpiryPolicyRules.Changes(before, after));
        Assert.Equal(120, before.SlaHours["EXPIRED_ON_HAND"]); // Copy does not share the map
    }
}

/// <summary>
/// WP-0176-F1 §7 — the PRODUCT-MASTER 1.1 shelf-life days are read nullable by both production clients (MOD-0174 TraceSku, MOD-0173
/// SkuReference): a stated integer is kept, 0 included; absent, null, text, a fraction or a negative number is null ("Tanımsız") — never
/// 0. K3 sabotage (d): read a null minRemainingShelfLifeDays as 0 → red.
/// </summary>
public sealed class ExpiryProductReadTests
{
    private sealed class Answer(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static HttpClient Http(string json) => new(new Answer(json)) { BaseAddress = new Uri("http://product-master.test/") };

    private static string Sku(Guid sku, Guid item, string days)
        => $$"""{ "skuId": "{{sku}}", "skuLevel": "LSKU", "itemId": "{{item}}", "lifecycleStatus": "Active", "lotControlled": true {{days}} }""";

    public static TheoryData<string, int?, int?, int?> Cases => new()
    {
        { """, "shelfLifeDays": 730, "retestDays": 365, "minRemainingShelfLifeDays": 180""", 730, 365, 180 },
        { """, "shelfLifeDays": 730, "retestDays": null, "minRemainingShelfLifeDays": null""", 730, null, null },
        { "", null, null, null },
        { """, "shelfLifeDays": 0, "minRemainingShelfLifeDays": 0""", 0, null, 0 },
        { """, "shelfLifeDays": "730", "minRemainingShelfLifeDays": 12.5""", null, null, null },
        { """, "shelfLifeDays": -1, "retestDays": true""", null, null, null }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Tanimsiz_stays_null_in_both_clients(string days, int? shelf, int? retest, int? minimum)
    {
        var sku = Guid.NewGuid();
        var item = Guid.NewGuid();

        var trace = await new TraceSkuDirectoryHttpClient(Http(Sku(sku, item, days))).GetSkuAsync(sku, SkuLevel.Lsku);
        Assert.NotNull(trace);
        Assert.Equal((shelf, retest, minimum), (trace!.ShelfLifeDays, trace.RetestDays, trace.MinRemainingShelfLifeDays));

        var ledger = await new ProductMasterHttpClient(Http(Sku(sku, item, days))).GetSkuAsync(sku, SkuLevel.Lsku);
        Assert.NotNull(ledger);
        Assert.Equal((shelf, retest, minimum), (ledger!.ShelfLifeDays, ledger.RetestDays, ledger.MinRemainingShelfLifeDays));
    }
}

/// <summary>
/// WP-0176-F1 §9 — <c>fefo.openapi.yaml</c> 1.0.0 = the CT-accepted proposal + pack §19.7, measured against the PROPOSAL FILE and the
/// production controller: every proposal operation is in the yaml; the F1 controller serves exactly the F1 operations (path, method,
/// permission) and the rest are listed as not yet implemented; every owned error code (proposal §4.21 + CT-F10) is declared.
/// </summary>
public sealed class ExpiryContractTests
{
    private static readonly Lazy<JsonObject> Yaml = new(() =>
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(ContractSchemas.FefoContractPath));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    });

    private static readonly Lazy<string> Proposal = new(() => File.ReadAllText(RepoPaths.Contract("fefo-bundle-v1-proposal.md")));

    private static readonly string[] Methods = ["get", "post", "put", "patch", "delete"];

    private static IEnumerable<(string Path, string Method, JsonObject Op)> Operations()
        => Yaml.Value["paths"]!.AsObject().SelectMany(p => Methods.Where(m => p.Value![m] is not null).Select(m => (p.Key, m, p.Value![m]!.AsObject())));

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_contract_is_frozen_1_0_0_owned_by_MOD_0176_with_the_CT_additions()
    {
        var info = Yaml.Value["info"]!;
        Assert.Equal("1.0.1", info["version"]!.GetValue<string>()); // WP-0176-F2: description-only step (x-implemented-by-slice)
        Assert.Equal("FROZEN", info["x-status"]!.GetValue<string>());
        Assert.Equal("MOD-0176", info["x-owner"]!.GetValue<string>());
        Assert.Equal("/api/expiry", Yaml.Value["servers"]![0]!["url"]!.GetValue<string>());
        var changelog = string.Join(" ", Strings(info["x-changelog"]));
        Assert.All(new[] { "CT-F10", "EXPIRY_SOD_VIOLATION", "CT-F8", "APPROVE", "CT-F14" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        Assert.Equal("APPROVE", Yaml.Value["x-signable-actions"]!["defaultMeaningCode"]!.GetValue<string>());
        Assert.Equal(ExpiryPermissions.All.OrderBy(k => k), Strings(Yaml.Value["x-permissions"]!["owned"]).OrderBy(k => k));
    }

    [Fact]
    public void Every_proposal_operation_is_in_the_contract()
    {
        var proposed = Regex.Matches(Proposal.Value, @"^\| 4\.\d+ \| `(?<op>[a-zA-Z]+)` \|", RegexOptions.Multiline).Select(m => m.Groups["op"].Value).ToList();
        Assert.Equal(20, proposed.Count);
        var frozen = Operations().Select(o => o.Op["operationId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        Assert.All(proposed, op => Assert.Contains(op, frozen));
    }

    [Fact]
    public void The_F1_controller_serves_exactly_the_F1_operations_with_their_permission()
    {
        var served = typeof(ExpiryController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods.Select(v =>
                (Path: "/" + a.Template, Method: v.ToLowerInvariant(), Permission: m.GetCustomAttribute<HasPermissionAttribute>()!.Policy!["Permission:".Length..]))))
            .ToList();
        Assert.Equal(12, served.Count); // 11 F1 + createDateOverride (F2)

        var byRoute = Operations().ToDictionary(o => (o.Path, o.Method));
        var f1 = Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["F1"]).Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["F2"])).ToList();
        foreach (var (path, method, permission) in served)
        {
            Assert.True(byRoute.TryGetValue((path, method), out var op), $"{method} {path} is not in fefo.openapi.yaml");
            Assert.Equal(permission, op.Op["x-permission"]!.GetValue<string>());
            Assert.Contains(op.Op["operationId"]!.GetValue<string>(), f1);
        }

        Assert.Equal(f1.Count, served.Count);
        var notYet = Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]);
        Assert.Equal(Operations().Select(o => o.Op["operationId"]!.GetValue<string>()).Except(f1).OrderBy(x => x), notYet.OrderBy(x => x));
        Assert.Equal("api/expiry", typeof(ExpiryController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()!.Template);
    }

    [Fact]
    public void Every_owned_error_code_is_declared_and_SoD_is_on_prepare_and_resolve()
    {
        var declared = Yaml.Value["x-error-codes"]!.AsObject().Where(p => p.Key != "shared").Select(p => p.Key)
            .Concat(Strings(Yaml.Value["x-error-codes"]!["shared"])).ToHashSet(StringComparer.Ordinal);
        var owned = typeof(ExpiryErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!).ToList();
        Assert.All(owned, code => Assert.Contains(code, declared));

        var proposed = Regex.Matches(Proposal.Value, @"^\| `(?<code>EXPIRY_[A-Z_]+)` \|", RegexOptions.Multiline).Select(m => m.Groups["code"].Value).ToList();
        Assert.True(proposed.Count >= 11);
        Assert.All(proposed, code => Assert.Contains(code, declared));

        var ops = Operations().ToDictionary(o => o.Op["operationId"]!.GetValue<string>());
        Assert.Contains("EXPIRY_SOD_VIOLATION", Strings(ops["prepareResolution"].Op["x-error-codes"]));
        Assert.Contains("EXPIRY_SOD_VIOLATION", Strings(ops["resolveException"].Op["x-error-codes"]));
        Assert.Equal("409", Yaml.Value["x-error-codes"]!["EXPIRY_SOD_VIOLATION"]!.GetValue<string>()); // YamlDotNet reads scalars as text
    }
}

/// <summary>WP-0176-F1 — structure: scope, insert-only records, no signature record, keys on every action.</summary>
public sealed class ExpiryArchitectureTests
{
    [Fact]
    public void The_expiry_base_path_is_scoped_by_the_tenant_and_company_middleware()
        => Assert.Contains(ExpiryContract.BasePath, InventoryScopeResolutionMiddleware.ScopedBasePaths);

    [Fact]
    public void Every_expiry_action_carries_a_permission_in_the_expiry_namespace()
    {
        var actions = typeof(ExpiryController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToList();
        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.StartsWith("Permission:expiry.", a.GetCustomAttribute<HasPermissionAttribute>()?.Policy ?? "", StringComparison.Ordinal));
        Assert.All(ExpiryPermissions.F1, k => Assert.Contains(k, ExpiryPermissions.All));

        // completeness (FU-RSV-01 rule): every key the expiry controller enforces reaches the catalog through ITS manifest, and the
        // manifest declares nothing the controller does not enforce (F1: API permissions only, no page yet)
        var enforced = actions.Select(a => a.GetCustomAttribute<HasPermissionAttribute>()!.Policy!["Permission:".Length..]).ToHashSet(StringComparer.Ordinal);
        var manifest = new ExpiryFefoManifestProvider().GetManifest();
        Assert.Empty(manifest.Pages);
        Assert.Equal(enforced.OrderBy(k => k, StringComparer.Ordinal), (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(manifest.ApiPermissions!, p => Assert.Equal("Tenant", p.Scope));
    }

    [Fact]
    public void History_events_and_audit_rows_are_insert_only()
    {
        var mutating = typeof(IExpiryWriteSession).GetMethods().Select(m => m.Name)
            .Where(n => Regex.IsMatch(n, "Update|Replace|Delete|Remove|Save|Upsert")).OrderBy(n => n).ToList();
        Assert.Equal(["ReplaceCaseAsync", "SavePolicyAsync"], mutating); // the two mutable heads, both version-guarded

        foreach (var file in SourceScan.Production("Diten.InventoryService.Persistence").Where(f => f.RelativePath.Contains("Expiry", StringComparison.Ordinal)))
        {
            Assert.DoesNotMatch(@"_(history|events|audit)\.(Update|Replace|Delete|FindOneAndUpdate|FindOneAndReplace|FindOneAndDelete|BulkWrite)", file.Body);
            Assert.DoesNotMatch(@"\.(DeleteOne|DeleteMany)Async", file.Body);
        }
    }

    [Fact]
    public void Expiry_keeps_no_signature_record_KF_E()
    {
        var entities = typeof(EntityBase).Assembly.GetTypes()
            .Where(t => typeof(EntityBase).IsAssignableFrom(t) && t.Namespace?.Contains(".Expiry", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(7, entities.Count); // F1: 5; F2: + FefoIssueSnapshot, LotDateOverride
        Assert.DoesNotContain(entities, t => t.Name.Contains("Signature", StringComparison.Ordinal));
        Assert.Contains(typeof(IExpiryResolutionSignature), typeof(FailClosedExpiryResolutionSignature).GetInterfaces());
    }
}
