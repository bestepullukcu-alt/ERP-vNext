using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// Q151 D1 + D2 (mvp6-q151-integrated-run-01). D1: the edge mints a canonical lowercase hyphenated UUID — the
/// SupplyChain Carrier/Load/Return/Claim parsers accept only "D", and "N" made every request through the gateway 400.
/// D2: with a signing key, the edge mints self-authenticating ids and adopts an inbound id ONLY when it carries the
/// edge's own signature, so a client can present a shipment's lifecycle root on its transitions/returns/claims; any
/// value the caller chose — including a well-formed UUID or a tampered signed id — is still never adopted (INTX FIX2).
/// </summary>
public sealed class SignedCorrelationTests
{
    private const string Key = "q151-test-signing-key-not-a-secret";

    private static async Task<string> EdgeCorrelation(string? signingKey, params (string Name, string Value)[] headers)
    {
        var http = new DefaultHttpContext();
        foreach (var (name, value) in headers)
        {
            http.Request.Headers[name] = value;
        }

        var correlation = new CorrelationContext();
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, Options.Create(new ObservabilityOptions
        {
            Correlation = new CorrelationOptions { TrustInboundCorrelation = false, SigningKey = signingKey }
        })).InvokeAsync(http, correlation);
        return correlation.CorrelationId!;
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Key)]
    public async Task The_edge_mints_a_canonical_lowercase_hyphenated_uuid(string? key)
    {
        var minted = await EdgeCorrelation(key);

        Assert.Equal(36, minted.Length);
        Assert.True(Guid.TryParseExact(minted, "D", out _));
        Assert.Equal(minted.ToLowerInvariant(), minted);
    }

    [Fact]
    public async Task A_signed_id_the_edge_issued_is_adopted_when_presented_again()
    {
        var root = await EdgeCorrelation(Key);

        Assert.Equal(root, await EdgeCorrelation(Key, ("X-Correlation-Id", root)));
        Assert.Equal(root, await EdgeCorrelation(Key, ("X-Correlation-Id", root.ToUpperInvariant())));
    }

    [Fact]
    public async Task A_well_formed_uuid_the_caller_chose_is_not_adopted()
    {
        var chosen = Guid.NewGuid().ToString("D");

        Assert.NotEqual(chosen, await EdgeCorrelation(Key, ("X-Correlation-Id", chosen)));
    }

    [Fact]
    public async Task A_tampered_signed_id_is_not_adopted()
    {
        var root = await EdgeCorrelation(Key);
        var last = root[^1] == '0' ? '1' : '0';
        var tampered = root[..^1] + last;

        Assert.NotEqual(tampered, await EdgeCorrelation(Key, ("X-Correlation-Id", tampered)));
    }

    [Fact]
    public async Task An_id_signed_with_another_key_is_not_adopted()
    {
        var foreign = await EdgeCorrelation("some-other-gateway-key");

        Assert.NotEqual(foreign, await EdgeCorrelation(Key, ("X-Correlation-Id", foreign)));
    }

    [Fact]
    public async Task Without_a_key_nothing_inbound_is_ever_adopted()
    {
        var signedElsewhere = await EdgeCorrelation(Key);

        Assert.NotEqual(signedElsewhere, await EdgeCorrelation(null, ("X-Correlation-Id", signedElsewhere)));
    }

    [Fact]
    public async Task Two_values_on_the_header_are_not_adopted_even_if_one_is_signed()
    {
        var root = await EdgeCorrelation(Key);
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Correlation-Id"] = new Microsoft.Extensions.Primitives.StringValues(new[] { root, root });
        var correlation = new CorrelationContext();
        await new CorrelationIdMiddleware(_ => Task.CompletedTask, Options.Create(new ObservabilityOptions
        {
            Correlation = new CorrelationOptions { TrustInboundCorrelation = false, SigningKey = Key }
        })).InvokeAsync(http, correlation);

        Assert.NotEqual(root, correlation.CorrelationId);
    }
}
