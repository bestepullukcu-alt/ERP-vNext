using System.Net.Http.Headers;
using Diten.Web.Tests.Contracts;

namespace Diten.Web.Tests.Recall;

/// <summary>
/// WP-MVP1-FU-CLEAN2 (FU-WEB-CONTRACT-FAKES-RECALL, O-NAMES1-3) — the Recall Web fakes (<see cref="RecallDataGateTests.FakeGateway"/>,
/// <see cref="RecallCaseDetailWebTests.Recorder"/>) answer through here: every 2xx JSON body a fake hands to the Web must be a valid response of
/// the RECALL 1.9.0 operation it stands for (<see cref="ContractRoutes"/>: CLOSED objects, formats, enums). The one tolerance is the bare
/// placeholder <c>{}</c> of a fake that does not care about the answer: it may MISS required members (a "required" error) and nothing else — it
/// can never carry an off-contract member. A deliberate off-contract body is registered by name (<see cref="Off"/>), never silently.
/// </summary>
internal static class RecallContractFakes
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HttpResponseMessage, string[]> Allowed = new();

    /// <summary>A DELIBERATE off-contract answer (a negative test of the Web's own guard): it must break the contract by exactly these violations.</summary>
    public static HttpResponseMessage Off(HttpResponseMessage response, params string[] allowed)
    {
        Allowed.AddOrUpdate(response, allowed);
        return response;
    }

    /// <summary>Throws <see cref="ContractViolationException"/> when the fake's answer is not a response of the contract that owns the path.</summary>
    public static HttpResponseMessage Bound(HttpRequestMessage request, HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (status is < 200 or >= 300 || response.Content is null) return response;
        if (response.Content.Headers.ContentType is not MediaTypeHeaderValue { MediaType: "application/json" }) return response; // CSV / PDF files
        var path = request.RequestUri!.AbsolutePath;
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (Allowed.TryGetValue(response, out var allowed))
        {
            ContractRoutes.RequireOff(path, request.Method.Method, status, body, allowed);
            return response;
        }

        var errors = Placeholder(ContractRoutes.Check(path, status, body, request.Method.Method), body);
        if (errors.Count > 0) throw new ContractViolationException("recall.openapi.yaml", $"{request.Method} {path}", errors);
        return response;
    }

    /// <summary>
    /// The placeholder answer of a fake that does not care about the body, SHAPED BY THE CONTRACT: <c>{}</c> for an object answer, <c>[]</c>
    /// for an array answer, an empty file for an operation that answers no JSON (a download). Never an off-contract member.
    /// </summary>
    public static HttpResponseMessage Placeholder(HttpRequestMessage request, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK)
    {
        var path = request.RequestUri!.AbsolutePath;
        foreach (var body in new[] { "{}", "[]" })
            if (Placeholder(ContractRoutes.Check(path, (int)status, body, request.Method.Method), body).Count == 0)
                return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        return new HttpResponseMessage(status) { Content = new ByteArrayContent([]) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } } };
    }

    /// <summary>The bare <c>{}</c> may miss required members and nothing else; any other body keeps every violation.</summary>
    private static List<string> Placeholder(IReadOnlyList<string> errors, string body)
        => body.Trim() == "{}" ? errors.Where(e => !e.Contains(": required —", StringComparison.Ordinal)).ToList() : errors.ToList();
}
