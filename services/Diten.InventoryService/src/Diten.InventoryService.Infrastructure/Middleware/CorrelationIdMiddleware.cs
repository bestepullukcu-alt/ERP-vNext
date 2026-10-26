using System.Text;
using Diten.InventoryService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Middleware;

/// <summary>
/// First in the pipeline (pack §8 correlation). <c>X-Correlation-Id</c> is not declared by the frozen contract, so it is optional: absent →
/// the server mints a UUID; exactly one UUID → it is used as is. FU-INT1-02 (owner decision G1-D3 = A, 2026-10-20 — WP-MVP1-FIX-LIVE3): any
/// other value (not a UUID, the empty UUID, several values) is NOT refused any more — the server mints a fresh UUID and the request goes on
/// with it; the supplied text appears ONLY in one warning log line (at most <see cref="LoggedLength"/> characters, every character outside
/// printable ASCII escaped) and is never used as an id in any record. The id is echoed in the response header of EVERY response, including
/// refusals, and is what the posting persists on its transaction, audit record and outbox event.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>The most characters of a replaced value that reach the log.</summary>
    public const int LoggedLength = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation, ILogger<CorrelationIdMiddleware> logger)
    {
        var supplied = context.Request.Headers[InventoryContract.CorrelationHeader];
        var valid = supplied.Count == 1 && Guid.TryParse(supplied[0], out var fromCaller) && fromCaller != Guid.Empty;
        var correlationId = valid ? Guid.Parse(supplied[0]!) : Guid.NewGuid();
        if (supplied.Count > 0 && !valid)
        {
            logger.LogWarning("inventory.correlation.replaced {Header} {Supplied} -> {CorrelationId}", InventoryContract.CorrelationHeader,
                ForLog(string.Join(",", supplied.ToArray())), correlationId);
        }

        correlation.Set(correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[InventoryContract.CorrelationHeader] = correlationId.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }

    /// <summary>The supplied text as it may be logged: at most <see cref="LoggedLength"/> characters, quoted, non-printable-ASCII as <c>\uXXXX</c>.</summary>
    public static string ForLog(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        var cut = value.Length > LoggedLength;
        var text = new StringBuilder();
        foreach (var c in cut ? value[..LoggedLength] : value)
            text.Append(c is >= ' ' and <= '~' && c != '"' && c != '\\' ? c.ToString() : $"\\u{(int)c:x4}");
        return "\"" + text + (cut ? $"\"...(+{value.Length - LoggedLength})" : "\"");
    }
}
