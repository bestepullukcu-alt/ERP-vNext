using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Infrastructure;

/// <summary>
/// MOD-0029-FU04C — shared translation of an upstream (Gateway/Platform) proxy response into an MVC result.
/// Authorization failures (401/403) reach the browser in two shapes that need different UX:
/// <list type="bullet">
/// <item>AJAX/fetch (DataTables, action buttons): keep the JSON <c>Response&lt;T&gt;</c> envelope verbatim so the
/// page JS can show a toast/modal — the body (incl. <c>reason_code</c>/<c>correlation_id</c>) is preserved.</item>
/// <item>Full-page navigation (download/preview/direct link): redirect to the friendly Not Authorized page so the
/// raw JSON envelope is never dumped into the browser.</item>
/// </list>
/// Backend enforcement is untouched — the upstream already decided 401/403; this only changes presentation.
/// </summary>
public static class TenantShellProxyResponse
{
    public static async Task<IActionResult> PassthroughAsync(HttpResponseMessage response, HttpRequest request, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        if (status is 204 or 205 or 304)
        {
            return new StatusCodeResult(status);
        }

        if (status is 401 or 403 && IsNavigation(request))
        {
            return new RedirectResult($"/Error/NotAuthorized?code={status}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            Content = string.IsNullOrWhiteSpace(body) ? "{}" : body,
            ContentType = "application/json",
            StatusCode = status
        };
    }

    /// <summary>True for a top-level browser navigation (download/preview/link), false for fetch/XHR. Uses the
    /// <c>Sec-Fetch-Mode</c> request header (sent by all modern browsers) with an Accept-header fallback.</summary>
    public static bool IsNavigation(HttpRequest request)
    {
        var secFetchMode = request.Headers["Sec-Fetch-Mode"].ToString();
        if (!string.IsNullOrEmpty(secFetchMode))
        {
            return string.Equals(secFetchMode, "navigate", StringComparison.OrdinalIgnoreCase);
        }

        // Fallback (older clients): XHR marker wins; otherwise an HTML-accepting request is a navigation.
        if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
