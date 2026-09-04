using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;

namespace Diten.Platform.Infrastructure.Services.WorkAggregation;

/// <summary>
/// WC-D1 — THE ONE PLACE PLATFORM TALKS TO A MODULE IN ANOTHER SERVICE. Read and write both come through here.
///
/// <para><b>Why one class for two directions.</b> The read ("what work have you got?") and the write ("the user
/// pressed approve") are the same conversation with the same module, and if they were designed apart they would
/// grow two identity models, two error dictionaries and two ideas of how long is too long — which is what DCP-004
/// warned about on 2026-08-26 in the sentence this round is paying off. They share this file instead: one place
/// that decides what a JWT does, what a tenant header does, what a dead socket means, and what a body that is not
/// a projection means.</para>
///
/// <para><b>What travels, and what does not.</b> The caller's own bearer token is forwarded, so the module
/// authorises the HUMAN and not Platform — a service-to-service key here would make every remote module see one
/// omnipotent caller and lose the actor entirely. The tenant header is written here, from the request-scoped
/// <see cref="ITenantContext"/>. The correlation id is forwarded so one reader's click can be followed across two
/// services' logs. NOTHING ELSE is copied from the inbound request: a blanket header copy would forward cookies
/// and internal API keys to a third party.</para>
///
/// <para><b>⚠ Why the tenant header is NOT left to a <c>DelegatingHandler</c>, MEASURED 2026-08-28.</b> The
/// intent was to reuse the shared tenant-propagation handler the MDM and Auth clients then carried, so tenancy on
/// the wire would have one implementation. It does not work, and the failure is silent: <c>IHttpClientFactory</c>
/// builds and CACHES a
/// handler chain in its OWN scope, so a <c>DelegatingHandler</c> resolving a request-scoped
/// <see cref="ITenantContext"/> gets an instance that belongs to no request and answers
/// <c>IsResolved == false</c> — the header is then simply not added and nothing anywhere says so. It was caught
/// by calling a real module and reading the tenant it received back on the screen: "(no tenant header)". A unit
/// test could not have caught it, and did not: the test's container registers the context as a singleton, so it
/// proved the wiring and not the lifetime. The two clients that carried the same defect were moved off it
/// (BL-311) and the handler class was then deleted from all three services (BL-316); <c>TenantOnTheWire</c> is
/// where the rule lives now.</para>
///
/// <para><b>Timeouts have ONE source.</b> The named client's own <c>Timeout</c> is disabled on purpose
/// (<c>InfiniteTimeSpan</c>) so that the only deadline in play is
/// <c>WorkAggregation:Resilience:ProviderTimeout</c> — the budget WC-D3 already built and the aggregation loop
/// already applies per provider. On the READ path this class therefore imposes no deadline of its own: it honours
/// the token the loop hands it, and lets an exceeded budget surface as the cancellation the loop is written to
/// catch and report as <c>TIMEOUT</c>. On the WRITE path there is no such loop, so the dispatcher applies the
/// SAME option before calling in. A second timeout constant would be a second answer to one operator question.</para>
/// </summary>
public sealed class RemoteWorkItemGateway
{
    /// <summary>
    /// The named client every remote provider row shares. Named rather than typed because the type is registered
    /// once per configuration row and a typed client would bind one handler chain per row — the same tenancy
    /// handler, configured N times.
    /// </summary>
    public const string HttpClientName = "WorkItemBridge";

    private const string CorrelationHeader = "X-Correlation-Id";

    /// <summary>
    /// Matches how Platform's own API serialises: camelCase, so a module echoing the canonical DTO shape needs no
    /// bespoke naming policy to be read back.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string TenantHeader = "X-Tenant-Id";

    private readonly IHttpClientFactory _clients;
    private readonly IHttpContextAccessor _http;
    private readonly ITenantContext _tenant;

    public RemoteWorkItemGateway(IHttpClientFactory clients, IHttpContextAccessor http, ITenantContext tenant)
    {
        _clients = clients;
        _http = http;
        _tenant = tenant;
    }

    /// <summary>
    /// Issue one call and report what came back — WITHOUT deciding what it means. Read and write want different
    /// things from a failure (the read must let cancellation reach the aggregation loop; the write must turn
    /// everything into a refusal), so the interpretation belongs to them and the transport belongs here.
    /// </summary>
    /// <remarks>
    /// <para><b>Cancellation is never swallowed.</b> It is the aggregation loop's timeout signal and the caller's
    /// abandon signal, and both are the loop's to interpret. Catching it here would turn an exceeded budget into
    /// "the module answered badly", which reports the wrong fact to the wrong person.</para>
    /// </remarks>
    public async Task<RemoteCallOutcome> SendAsync(
        RemoteWorkItemProviderOptions row,
        HttpMethod method,
        string path,
        object? body,
        string? correlationId,
        CancellationToken ct)
    {
        var client = _clients.CreateClient(HttpClientName);
        var uri = new Uri(new Uri(row.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/'));

        using var request = new HttpRequestMessage(method, uri);

        // The HUMAN's token, not a service key: the module must authorise the person who pressed the button.
        var authorization = _http.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization)
            && AuthenticationHeaderValue.TryParse(authorization, out var parsed))
        {
            request.Headers.Authorization = parsed;
        }

        // From the REQUEST's own scope, which is the whole reason this is here and not in a shared handler.
        if (_tenant.IsResolved)
        {
            request.Headers.TryAddWithoutValidation(TenantHeader, _tenant.TenantId.ToString());
        }

        var correlation = string.IsNullOrWhiteSpace(correlationId)
            ? _http.HttpContext?.Request.Headers[CorrelationHeader].ToString()
            : correlationId;
        if (!string.IsNullOrWhiteSpace(correlation))
        {
            request.Headers.TryAddWithoutValidation(CorrelationHeader, correlation);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException)
        {
            return RemoteCallOutcome.NotReached($"{row.ProviderCode}: {ex.Message}");
        }

        using (response)
        {
            RemoteEnvelope? envelope = null;
            string? parseFailure = null;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<RemoteEnvelope>(Json, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException or IOException)
            {
                parseFailure = ex.Message;
            }

            if (envelope is null)
            {
                // A body that is not the shared envelope. Treated as NOT REACHED rather than as a refusal with an
                // unknown code, because a module answering HTML (a login page, a proxy error) has not answered the
                // question at all, and calling that a business refusal would put a nonsense sentence on a screen.
                return RemoteCallOutcome.NotReached(
                    $"{row.ProviderCode}: {(int)response.StatusCode} carried no work-item envelope"
                    + (parseFailure is null ? "." : $" ({parseFailure})"));
            }

            return new RemoteCallOutcome(
                Reached: true,
                // The module's own verdict wins over the transport status when they disagree; a module that
                // answers 200 with isSuccessful=false has refused, and saying otherwise would be the silent
                // success this whole capability forbids.
                Succeeded: envelope.IsSuccessful && response.IsSuccessStatusCode,
                StatusCode: envelope.StatusCode > 0 ? envelope.StatusCode : (int)response.StatusCode,
                ReasonCode: envelope.ReasonCode,
                Errors: envelope.Errors ?? [],
                Data: envelope.Data,
                FailureDetail: null);
        }
    }
}

/// <summary>What one call to a remote module produced, before anybody decides what it means.</summary>
/// <param name="Reached">
/// FALSE means no usable answer arrived — refused socket, unreadable body, a page instead of an envelope. It is
/// deliberately distinct from a refusal: "the module said no" and "the module said nothing" are different facts
/// and the second one is the only one where the outcome of a write is genuinely unknown.
/// </param>
public sealed record RemoteCallOutcome(
    bool Reached,
    bool Succeeded,
    int StatusCode,
    string? ReasonCode,
    IReadOnlyList<string> Errors,
    JsonElement? Data,
    string? FailureDetail)
{
    public static RemoteCallOutcome NotReached(string detail)
        => new(false, false, 0, null, [], null, detail);
}

/// <summary>
/// The shared response envelope every Diten service already returns (<c>Response&lt;T&gt;</c>) — read here as raw
/// JSON so the bridge is not coupled to the payload type of either direction.
/// </summary>
/// <remarks>
/// <c>reason_code</c> is snake_case on the wire because <c>Response&lt;T&gt;</c> declares it so. Getting that
/// wrong would silently drop every module's refusal code and leave the reader with a generic error — the exact
/// failure the error-code bridge exists to prevent.
/// </remarks>
public sealed record RemoteEnvelope(
    JsonElement? Data,
    int StatusCode,
    bool IsSuccessful,
    IReadOnlyList<string>? Errors,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason_code")]
    string? ReasonCode);
