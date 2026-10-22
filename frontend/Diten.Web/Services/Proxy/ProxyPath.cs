using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Services.Proxy;

/// <summary>
/// WP-MVP1-SEC-PROXY1 — THE path boundary of every same-origin catch-all proxy (<c>…/api/{**everything}</c> → Gateway <c>{prefix}/{everything}</c>).
/// The proxies carry the caller's own token, so a path that leaves the module prefix after the gateway URL is normalized (dot segments,
/// backslashes, encoded separators, a second authority) reaches another service's endpoints with the same session. <see cref="TryBuild"/>
/// builds the gateway path only when the caller's rest stays a plain path below the prefix; otherwise the proxy answers 400
/// <c>INVALID_PROXY_PATH</c> (<see cref="Refusal"/>) and NOTHING is sent to the gateway. The query string is not part of the rest: the
/// proxies forward <c>Request.QueryString</c> unchanged.
/// <list type="number">
/// <item>The rest is percent-decoded repeatedly until it no longer changes (double / triple encoding included), at most
/// <see cref="MaxDecodeRounds"/> rounds; every form (raw and each decoded one) is judged.</item>
/// <item>Refused: a control character / NUL; a backslash; a scheme or a second authority (<c>//</c> at the start, <c>://</c>, <c>@</c>,
/// <c>name:</c> as the first segment); a <c>.</c> or <c>..</c> segment; an encoded separator or dot (<c>%2F</c>, <c>%5C</c>, <c>%2E</c>) in
/// any form; an empty segment inside the path; any character outside <c>A-Z a-z 0-9 - _ ~ . /</c> (a dot only inside a segment).</item>
/// <item>Last check: <c>new Uri(gateway + prefix + "/" + rest).AbsolutePath</c> still starts with <c>prefix + "/"</c>.</item>
/// </list>
/// Empty / whitespace rest = the prefix itself (unchanged behaviour). A single leading <c>/</c> and a single trailing <c>/</c> are kept as before.
/// </summary>
public static class ProxyPath
{
    public const string InvalidProxyPath = "INVALID_PROXY_PATH";

    /// <summary>Decode rounds before a still-changing value is refused as too deeply encoded.</summary>
    public const int MaxDecodeRounds = 5;

    private static readonly Uri Probe = new("http://gateway.proxy-path.invalid");

    /// <summary>Why a rest was refused (the 400 body's <c>details.reason</c>; what the boundary detected).</summary>
    public static class Reasons
    {
        public const string ControlCharacter = "control-character";
        public const string Backslash = "backslash";
        public const string SchemeOrHost = "scheme-or-host";
        public const string DotSegment = "dot-segment";
        public const string EncodedSeparator = "encoded-separator";
        public const string EmptySegment = "empty-segment";
        public const string DisallowedCharacter = "disallowed-character";
        public const string TooDeeplyEncoded = "too-deeply-encoded";
        public const string EscapesPrefix = "escapes-prefix";
    }

    public static bool TryBuild(string prefix, string? everything, out string path) => TryBuild(prefix, everything, out path, out _);

    public static bool TryBuild(string prefix, string? everything, out string path, out string? reason)
    {
        path = prefix;
        reason = null;
        if (string.IsNullOrWhiteSpace(everything))
        {
            return true;
        }

        reason = Judge(everything);
        if (reason is not null)
        {
            return false;
        }

        var rest = everything.StartsWith('/') ? everything[1..] : everything;
        var candidate = $"{prefix}/{rest}";
        var absolute = new Uri(Probe, candidate).AbsolutePath;
        if (!absolute.StartsWith(prefix + "/", StringComparison.Ordinal) && absolute != prefix)
        {
            reason = Reasons.EscapesPrefix;
            return false;
        }

        path = candidate;
        return true;
    }

    /// <summary>400 <c>INVALID_PROXY_PATH</c> — the proxy's answer when <see cref="TryBuild"/> refused; the gateway is not called.</summary>
    public static IActionResult Refusal(string? reason)
        => new ObjectResult(new
        {
            error = new
            {
                code = InvalidProxyPath,
                message = "The proxied path must stay a plain path below the module prefix.",
                details = new { reason }
            }
        })
        { StatusCode = StatusCodes.Status400BadRequest };

    private static string? Judge(string raw)
    {
        if (raw.StartsWith("//", StringComparison.Ordinal)) return Reasons.SchemeOrHost;

        var forms = new List<string> { raw };
        var current = raw;
        for (var round = 1; round <= MaxDecodeRounds; round++)
        {
            var next = Uri.UnescapeDataString(current);
            if (next == current) break;
            forms.Add(next);
            current = next;
        }

        if (Uri.UnescapeDataString(current) != current) return Reasons.TooDeeplyEncoded;

        // structural checks on the most decoded form first: what the path MEANS once every layer is peeled
        var decoded = forms[^1];
        foreach (var form in Enumerable.Reverse(forms))
        {
            if (form.Any(char.IsControl)) return Reasons.ControlCharacter;
            if (form.Contains('\\')) return Reasons.Backslash;
            if (SchemeOrHost(form)) return Reasons.SchemeOrHost;
            if (DotSegment(form)) return Reasons.DotSegment;
        }

        foreach (var form in forms)
        {
            if (EncodedSeparator(form)) return Reasons.EncodedSeparator;
        }

        var body = decoded.StartsWith('/') ? decoded[1..] : decoded;
        if (body.TrimEnd('/').Split('/').Any(s => s.Length == 0) || body.EndsWith("//", StringComparison.Ordinal)) return Reasons.EmptySegment;
        return forms.All(f => f.All(Allowed)) ? null : Reasons.DisallowedCharacter;
    }

    private static bool Allowed(char c)
        => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or '~' or '.' or '/';

    private static bool SchemeOrHost(string form)
    {
        if (form.StartsWith("//", StringComparison.Ordinal) || form.Contains("://", StringComparison.Ordinal) || form.Contains('@')) return true;
        var first = form.TrimStart('/').Split('/')[0];
        return first.Contains(':');
    }

    private static bool DotSegment(string form)
        => form.Split('/').Any(segment => segment is "." or "..");

    private static bool EncodedSeparator(string form)
        => form.Contains("%2F", StringComparison.OrdinalIgnoreCase) || form.Contains("%5C", StringComparison.OrdinalIgnoreCase)
           || form.Contains("%2E", StringComparison.OrdinalIgnoreCase);
}
