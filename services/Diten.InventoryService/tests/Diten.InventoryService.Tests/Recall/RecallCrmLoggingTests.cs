using System.Text.Json.Nodes;
using System.Xml.Linq;
using Diten.InventoryService.Infrastructure.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// MOD-0177-FU01 AC-14 (pack §8.6) — no personal data in the log: a failed CRM read logs the account id and the reason (status code, Budget,
/// Breaker); never a body, a name or a code; an unusable customerRef or a GUID-shaped ship-to is logged without its value.
/// </summary>
public sealed class RecallCrmLoggingTests : IClassFixture<RecallApiFactory>
{
    private readonly RecallApiFactory _factory;

    public RecallCrmLoggingTests(RecallApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Failures_log_the_account_and_the_reason_never_a_name_a_code_or_a_body()
    {
        var w = await CrmWorld.CreateAsync(_factory);
        var (c, d, e) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        w.Account(c, "Gizli Eczane Ltd", "ACC-SECRET-C");
        w.Account(d, "Gizli Klinik AS", "ACC-SECRET-D");
        w.FirstSaleIs(CrmWorld.A);
        await w.SellAsync(c);
        await w.SellAsync(d);
        await w.SellAsync(e);
        var (id, _) = await w.ActiveAsync();
        _factory.Crm.Behaviour[c] = CrmBehaviour.Forbidden;
        _factory.Crm.Behaviour[d] = CrmBehaviour.Raw;
        _factory.Crm.RawBody[d] = CrmDirectoryDouble.Body(d, "ACC-SECRET-D", "Gizli Klinik AS", version: "crm-account-lookup/2.0"); // an invalid contract answer
        _factory.Crm.Behaviour[e] = CrmBehaviour.ServiceUnavailable;
        try
        {
            await w.CustomersAsync(id);
            await w.GetAsync($"/api/recall/cases/{id}/effectiveness");
            var lines = _factory.Logs.Lines.ToList();
            foreach (var secret in new[] { CrmWorld.NameA, CrmWorld.CodeA, "Gizli Eczane Ltd", "ACC-SECRET-C", "Gizli Klinik AS", "ACC-SECRET-D", "crm-account-lookup/2.0" })
            {
                Assert.DoesNotContain(lines, l => l.Contains(secret, StringComparison.Ordinal));
            }

            Assert.Contains(lines, l => l.Contains($"recall.customer.lookup_unavailable {c}: 403", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.Contains($"recall.customer.lookup_unavailable {d}: InvalidBody", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.Contains($"recall.customer.lookup_unavailable {e}: 503", StringComparison.Ordinal));
        }
        finally
        {
            _factory.Crm.Behaviour.Clear();
        }
    }
}

/// <summary>
/// MOD-0177-FU01 AC-17 — the acceptance gate D-1: the reader's one field name is the frozen SHIPMENT-BUNDLE 3.5.0 getShipment member
/// (string, uuid). SHIPMENT-BUNDLE is 3.4.0 today: the test is skipped with its named reason until MVP-6 freezes 3.5.0; DL-1 is not accepted
/// before the Skip is removed and the test is green.
/// </summary>
public sealed class ShipmentBundle350AlignmentTests
{
    // The reason starts with its CI skip code (scripts/ci-skip-allowlist.tsv, inventory-tests SHIPMENT_BUNDLE_350, max 1);
    // the line and this Skip go together when MVP-6 freezes 3.5.0.
    [Fact(Skip = "SHIPMENT_BUNDLE_350: MOD-0177-FU01 D-1 — SHIPMENT-BUNDLE 3.5.0 not frozen")]
    public void CustomerRef_field_matches()
    {
        var path = RepoPaths.Contract("shipment-bundle.openapi.yaml");
        var doc = (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;
        var version = Version.Parse(doc["info"]!["version"]!.GetValue<string>());
        Assert.True(version >= new Version(3, 5, 0), version.ToString());
        var getShipment = doc["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject()).Single(m => m.Value?["operationId"]?.GetValue<string>() == "getShipment").Value!;
        var schemaRef = getShipment["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>();
        var schema = doc["components"]!["schemas"]![schemaRef.Split('/')[^1]]!;
        var member = schema["properties"]![ShipmentBundleHttpReader.CustomerRefField]!;
        Assert.Equal(("string", "uuid"), (member["type"]!.GetValue<string>(), member["format"]!.GetValue<string>()));
    }
}

/// <summary>
/// MOD-0177-FU01 AC-19 (pack §11) — the texts in seven languages: every key of the pack's table in its family (Web; the shared ones in the
/// service family too), not English copies in zh / ar / ru (the two "{0} · {1}" formats excepted), and the two error texts distinct from the
/// generic SERVICE_UNAVAILABLE / FORBIDDEN texts they would fall back to.
/// </summary>
public sealed class RecallCustomerTextTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static readonly string[] Shared =
    [
        "Recall.Customer.NameCode", "Recall.Customer.Unresolved", "Recall.Customer.NotFound", "Recall.Customer.Distinguisher", "Recall.Customer.UnresolvedWith",
        "Recall.Customer.NotFoundWith", "Recall.Customer.LegacyMarked", "Recall.Customer.NoCrmMarked", "Recall.Ev.W.customerNames", "Recall.Ev.W.customerNamesSnapshot"
    ];

    private static readonly string[] WebOnly =
    [
        "Recall.Customer.LegacyBadge", "Recall.Customer.NoCrmBadge", "Recall.Customer.LegacyHint", "Recall.Customer.NoCrmHint", "Recall.Customer.ManualHint",
        "Recall.Customer.CrmUnavailable", "Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE", "Recall.Customer.CrmForbidden", "Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN",
        "Recall.Customer.Deferred", "Recall.Customer.NamesPage", "Recall.Customer.PagePrev", "Recall.Customer.PageNext", "Recall.Customer.Forbidden",
        "Recall.Customer.ForbiddenWith", "Recall.Customer.ExportForbidden"
    ];

    private static readonly string[] SameInEveryLanguage = ["Recall.Customer.NameCode", "Recall.Customer.Distinguisher"];

    private static Dictionary<string, string> Resx(string path)
        => XDocument.Load(path).Root!.Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static string Web(string lang) => Path.Combine(RepoPaths.Root, "frontend", "Diten.Web", "Resources", "Views", "Recall", $"Recall.{lang}.resx");

    private static string Service(string lang) => Path.Combine(RepoPaths.ServiceRoot, "src", "Diten.InventoryService.Application", "Features", "Recall", "Evidence", $"RecallEvidenceText_{lang}.resx");

    [Fact]
    public void Every_customer_text_is_in_its_family_in_seven_languages_and_the_errors_are_their_own()
    {
        var webEn = Resx(Web("en"));
        foreach (var lang in Languages)
        {
            var web = Resx(Web(lang));
            var service = Resx(Service(lang));
            foreach (var key in Shared.Concat(WebOnly))
            {
                Assert.True(web.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"{lang} {key} missing in the Web family");
                if (lang is "zh" or "ar" or "ru" && !SameInEveryLanguage.Contains(key)) Assert.True(value != webEn[key], $"{lang} {key} copies English");
            }

            foreach (var key in Shared) Assert.Equal(web[key], service[key]);
            Assert.All(WebOnly, key => Assert.False(service.ContainsKey(key), $"{lang} {key} is a screen-only text"));
            Assert.NotEqual(web["Recall.Errors.SERVICE_UNAVAILABLE"], web["Recall.Errors.RECALL_CUSTOMER_DIRECTORY_UNAVAILABLE"]);
            Assert.NotEqual(web["Recall.Errors.FORBIDDEN"], web["Recall.Errors.RECALL_CUSTOMER_DIRECTORY_FORBIDDEN"]);
        }

        // the page vocabulary is the Keys list (Views/Recall/_RecallL10n.cshtml delivers it) — not any other list of the file
        var source = File.ReadAllText(Path.Combine(RepoPaths.Root, "frontend", "Diten.Web", "Views", "Recall", "Recall.cs"));
        var keysList = System.Text.RegularExpressions.Regex.Match(source, @"IReadOnlyList<string> Keys =\s*\[(?<list>[\s\S]*?)\];").Groups["list"].Value;
        Assert.All(Shared.Concat(WebOnly), key => Assert.Contains($"\"{key}\"", keysList, StringComparison.Ordinal));
    }
}
