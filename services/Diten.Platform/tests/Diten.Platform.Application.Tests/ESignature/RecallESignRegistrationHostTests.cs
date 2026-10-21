using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Tests.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.ESignature;

/// <summary>
/// WP-0177-R4 — the MOD-0177 registration body, BYTE-identical to the one InventoryService posts (the Inventory suite pins its SHA-256 in
/// <c>RecallESignTests.RecallRegistrationSha256</c>), accepted by the REAL Platform register endpoint in Platform's own test host: the five
/// <c>recall.</c> actions land in the catalog with seven names each and their defaults; the defaults are the registered ones (CT-R16:
/// <c>recall.settings.update</c> not required). The effective policy needs the recall subject provider, which this host does not have. Platform source is unchanged — only this test is added.
/// </summary>
[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class RecallESignRegistrationHostTests : IClassFixture<ESignatureTestHost>
{
    /// <summary>The body InventoryService sends for module recall (captured from its registration hosted service, WP-0177-R4).</summary>
    private const string RecallBody = """
{"module":"recall","actions":[{"actionCode":"recall.scope.approve","names":{"en":"Approve the recall scope","tr":"Geri \u00E7a\u011F\u0131rma kapsam\u0131n\u0131 onayla","fr":"Approuver le p\u00E9rim\u00E8tre du rappel","es":"Aprobar el alcance de la retirada","zh":"\u6279\u51C6\u53EC\u56DE\u8303\u56F4","ar":"\u0627\u0639\u062A\u0645\u0627\u062F \u0646\u0637\u0627\u0642 \u0627\u0644\u0627\u0633\u062A\u062F\u0639\u0627\u0621","ru":"\u0423\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u044C \u043E\u0431\u044A\u0451\u043C \u043E\u0442\u0437\u044B\u0432\u0430"},"defaultRequired":true,"defaultMeaningCode":"APPROVE","defaultVisualMode":"savedAccepted"},{"actionCode":"recall.effectiveness.complete","names":{"en":"Complete the effectiveness check","tr":"Etkinlik kontrol\u00FCn\u00FC tamamla","fr":"Terminer le contr\u00F4le d\u0027efficacit\u00E9","es":"Completar la verificaci\u00F3n de eficacia","zh":"\u5B8C\u6210\u6709\u6548\u6027\u6838\u67E5","ar":"\u0625\u0643\u0645\u0627\u0644 \u0641\u062D\u0635 \u0627\u0644\u0641\u0639\u0627\u0644\u064A\u0629","ru":"\u0417\u0430\u0432\u0435\u0440\u0448\u0438\u0442\u044C \u043F\u0440\u043E\u0432\u0435\u0440\u043A\u0443 \u044D\u0444\u0444\u0435\u043A\u0442\u0438\u0432\u043D\u043E\u0441\u0442\u0438"},"defaultRequired":true,"defaultMeaningCode":"APPROVE","defaultVisualMode":"savedAccepted"},{"actionCode":"recall.case.close","names":{"en":"Close the recall case","tr":"Geri \u00E7a\u011F\u0131rma vakas\u0131n\u0131 kapat","fr":"Cl\u00F4turer le dossier de rappel","es":"Cerrar el caso de retirada","zh":"\u5173\u95ED\u53EC\u56DE\u6848\u4F8B","ar":"\u0625\u063A\u0644\u0627\u0642 \u062D\u0627\u0644\u0629 \u0627\u0644\u0627\u0633\u062A\u062F\u0639\u0627\u0621","ru":"\u0417\u0430\u043A\u0440\u044B\u0442\u044C \u0434\u0435\u043B\u043E \u043E\u0431 \u043E\u0442\u0437\u044B\u0432\u0435"},"defaultRequired":true,"defaultMeaningCode":"APPROVE","defaultVisualMode":"savedAccepted"},{"actionCode":"recall.drill.result","names":{"en":"Sign the drill result","tr":"Tatbikat sonucunu imzala","fr":"Signer le r\u00E9sultat de l\u0027exercice","es":"Firmar el resultado del simulacro","zh":"\u7B7E\u7F72\u6F14\u7EC3\u7ED3\u679C","ar":"\u062A\u0648\u0642\u064A\u0639 \u0646\u062A\u064A\u062C\u0629 \u0627\u0644\u062A\u0645\u0631\u064A\u0646","ru":"\u041F\u043E\u0434\u043F\u0438\u0441\u0430\u0442\u044C \u0440\u0435\u0437\u0443\u043B\u044C\u0442\u0430\u0442 \u0443\u0447\u0435\u043D\u0438\u044F"},"defaultRequired":true,"defaultMeaningCode":"APPROVE","defaultVisualMode":"savedAccepted"},{"actionCode":"recall.settings.update","names":{"en":"Change the recall settings","tr":"Geri \u00E7a\u011F\u0131rma ayarlar\u0131n\u0131 de\u011Fi\u015Ftir","fr":"Modifier les param\u00E8tres de rappel","es":"Cambiar la configuraci\u00F3n de retirada","zh":"\u66F4\u6539\u53EC\u56DE\u8BBE\u7F6E","ar":"\u062A\u063A\u064A\u064A\u0631 \u0625\u0639\u062F\u0627\u062F\u0627\u062A \u0627\u0644\u0627\u0633\u062A\u062F\u0639\u0627\u0621","ru":"\u0418\u0437\u043C\u0435\u043D\u0438\u0442\u044C \u043D\u0430\u0441\u0442\u0440\u043E\u0439\u043A\u0438 \u043E\u0442\u0437\u044B\u0432\u0430"},"defaultRequired":false,"defaultMeaningCode":"APPROVE","defaultVisualMode":"savedAccepted"}]}
""";

    private const string RecallBodySha256 = "248e19eefc7b59e0f94216759da76146f040f74bbd7fd3f34954315c3ceddd35";

    private readonly ESignatureTestHost _host;

    public RecallESignRegistrationHostTests(ESignatureTestHost host) => _host = host;

    private static JsonElement Data(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task The_five_recall_actions_land_in_the_catalog_with_their_defaults()
    {
        Assert.Equal(RecallBodySha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(RecallBody))).ToLowerInvariant());

        using var internalClient = _host.Client();
        internalClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", ESignatureTestHost.InternalKey);
        var response = await internalClient.PostAsync("api/internal/esign/actions/register", new StringContent(RecallBody, Encoding.UTF8, "application/json"));
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        var result = Data(text);
        Assert.Equal(5, result.GetProperty("registered").GetInt32() + result.GetProperty("unchanged").GetInt32());

        var tenant = Guid.NewGuid();
        using var client = _host.Client(ESignatureTestHost.Token(tenant, Guid.NewGuid(), ESignPermissionsAll()));
        var catalog = Data(await client.GetStringAsync("api/v1/esign/actions")).EnumerateArray()
            .Where(a => a.GetProperty("actionCode").GetString()!.StartsWith("recall.", StringComparison.Ordinal)).ToList();
        Assert.Equal(["recall.case.close", "recall.drill.result", "recall.effectiveness.complete", "recall.scope.approve", "recall.settings.update"],
            catalog.Select(a => a.GetProperty("actionCode").GetString()!).OrderBy(c => c, StringComparer.Ordinal));
        Assert.All(catalog, a =>
        {
            Assert.Equal("recall", a.GetProperty("module").GetString());
            Assert.Equal(7, a.GetProperty("names").EnumerateObject().Count());
            Assert.Equal(a.GetProperty("actionCode").GetString() != "recall.settings.update", a.GetProperty("defaultRequired").GetBoolean());
        });
        Assert.Equal("Geri çağırma kapsamını onayla", catalog.Single(a => a.GetProperty("actionCode").GetString() == "recall.scope.approve").GetProperty("names").GetProperty("tr").GetString());
    }

    private static string[] ESignPermissionsAll() => Diten.Platform.Application.Features.ESignature.ESignPermissions.All.ToArray();
}
