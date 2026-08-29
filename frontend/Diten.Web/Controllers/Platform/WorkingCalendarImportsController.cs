using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/WorkingCalendarImports")]
public sealed class WorkingCalendarImportsController : Controller
{
    private const string ApiBase = "api/platform/working-calendars/imports";
    private readonly HttpClient _httpClient; private readonly string _gatewayUrl;
    public WorkingCalendarImportsController(HttpClient httpClient, IConfiguration configuration)
        => (_httpClient, _gatewayUrl) = (httpClient, configuration["GatewayUrl"] ?? "http://localhost:5000");

    [HttpGet("")] public IActionResult Index() => View("~/Views/Platform/WorkingCalendarImports/Index.cshtml");
    [HttpGet("Review/{id:guid}")] public IActionResult Review(Guid id) { ViewData["BatchId"] = id; return View("~/Views/Platform/WorkingCalendarImports/Review.cshtml"); }
    [HttpGet("api")] public Task<IActionResult> List(CancellationToken ct) => Forward(HttpMethod.Get, ApiBase + Request.QueryString, null, ct);
    [HttpGet("api/contract")] public Task<IActionResult> Contract(CancellationToken ct) => Forward(HttpMethod.Get, ApiBase + "/contract", null, ct);
    [HttpGet("api/provider-status")] public Task<IActionResult> Provider(CancellationToken ct) => Forward(HttpMethod.Get, ApiBase + "/provider-status", null, ct);
    [HttpGet("api/schedule")] public Task<IActionResult> Schedule(CancellationToken ct) => Forward(HttpMethod.Get, ApiBase + "/schedule", null, ct);
    [HttpGet("api/countries")] public Task<IActionResult> Countries(CancellationToken ct) => Forward(HttpMethod.Get, "api/lookups/countries", null, ct);
    [HttpGet("api/calendars")] public Task<IActionResult> Calendars(CancellationToken ct) => Forward(HttpMethod.Get, "api/platform/working-calendars" + Request.QueryString, null, ct);
    [HttpGet("api/calendars/{id:guid}")] public Task<IActionResult> Calendar(Guid id, CancellationToken ct) => Forward(HttpMethod.Get, $"api/platform/working-calendars/{id}", null, ct);
    [HttpGet("api/{id:guid}")] public Task<IActionResult> Get(Guid id, CancellationToken ct) => Forward(HttpMethod.Get, $"{ApiBase}/{id}", null, ct);
    [HttpPost("api")] public Task<IActionResult> Start([FromBody] object body, CancellationToken ct) => Forward(HttpMethod.Post, ApiBase, body, ct);
    [HttpPost("api/{id:guid}/candidates/{candidateId:guid}/decision")] public Task<IActionResult> Decide(Guid id, Guid candidateId, [FromBody] object body, CancellationToken ct) => Forward(HttpMethod.Post, $"{ApiBase}/{id}/candidates/{candidateId}/decision", body, ct);
    [HttpPost("api/{id:guid}/decisions")] public Task<IActionResult> DecideBatch(Guid id, [FromBody] object body, CancellationToken ct) => Forward(HttpMethod.Post, $"{ApiBase}/{id}/decisions", body, ct);
    [HttpPost("api/{id:guid}/apply")] public Task<IActionResult> Apply(Guid id, [FromBody] object body, CancellationToken ct) => Forward(HttpMethod.Post, $"{ApiBase}/{id}/apply", body, ct);
    [HttpPost("api/{id:guid}/discard")] public Task<IActionResult> Discard(Guid id, [FromBody] object body, CancellationToken ct) => Forward(HttpMethod.Post, $"{ApiBase}/{id}/discard", body, ct);

    private async Task<IActionResult> Forward(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        using var request = new HttpRequestMessage(method, $"{_gatewayUrl}/{path}");
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        try
        {
            using var response = await _httpClient.SendAsync(request, ct); var status = (int)response.StatusCode;
            if (status is 204 or 205 or 304) return StatusCode(status);
            var payload = await response.Content.ReadAsStringAsync(ct);
            return new ContentResult { StatusCode = status, Content = string.IsNullOrWhiteSpace(payload) ? "{}" : payload, ContentType = "application/json" };
        }
        catch { return StatusCode(502, "{\"message\":\"Gateway unavailable.\"}"); }
    }
}
