using System.Net;
using System.Text;

namespace Diten.Web.Tests.Contracts;

/// <summary>
/// WP-MVP1-FIX-LIVE1 — a fake Gateway that will not answer with a body its contract does not allow. Every 2xx route is registered
/// WITH the contract schema it stands for (<see cref="OnContract"/>); the body is validated against the contract file when it is
/// registered and the registration throws <see cref="ContractViolationException"/> if the body breaks the contract. So a test
/// can no longer "pass" by feeding the Web a shape the service never sends (the <c>id</c> vs <c>locationId</c> live finding).
/// Requests are recorded; the longest matching prefix answers (so <c>/api/locations/search</c> wins over <c>/api/locations</c>).
/// </summary>
public sealed class ContractGateway : HttpMessageHandler
{
    private readonly List<(string Prefix, int Status, string Body)> _routes = [];

    public List<(string Method, string PathAndQuery)> Requests { get; } = [];

    /// <summary>A 2xx answer that must be a valid <paramref name="schema"/> of <paramref name="contract"/>.</summary>
    public ContractGateway OnContract(string prefix, WebContractSchemas.ContractFile contract, string schema, string body)
    {
        var errors = contract.Validate(schema, body);
        if (errors.Count > 0) throw new ContractViolationException(contract.File, schema, errors);
        _routes.Add((prefix, 200, body));
        return this;
    }

    /// <summary>A non-2xx answer (no contract body to check).</summary>
    public ContractGateway OnStatus(string prefix, int status, string body = "")
    {
        if (status is >= 200 and < 300) throw new ArgumentException("a 2xx answer needs its contract schema: use OnContract", nameof(status));
        _routes.Add((prefix, status, body));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.PathAndQuery;
        lock (Requests) Requests.Add((request.Method.Method, path));
        var route = _routes.Where(r => path.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).FirstOrDefault();
        return Task.FromResult(route.Prefix is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage((HttpStatusCode)route.Status) { Content = new StringContent(route.Body, Encoding.UTF8, "application/json") });
    }
}

public sealed class ContractViolationException(string file, string schema, IReadOnlyList<string> errors)
    : Exception($"fake body is not a valid {schema} of {file}:\n  " + string.Join("\n  ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
