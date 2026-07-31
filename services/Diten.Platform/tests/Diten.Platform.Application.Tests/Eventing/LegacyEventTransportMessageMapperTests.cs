using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Infrastructure.Eventing;
using Xunit;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class LegacyEventTransportMessageMapperTests
{
    private static readonly IReadOnlyDictionary<string, string> SignedHeaders =
        new Dictionary<string, string>
        {
            [TrustedTransportMetadata.SignatureSchemeHeader] = "hmac-sha256-v1",
            [TrustedTransportMetadata.KeyIdHeader] = "ppm-key-2026",
            [TrustedTransportMetadata.SignatureHeader] = new string('a', 64)
        };

    [Fact]
    public void Map_PreservesEnvelopePayloadBytesAndAuthoritativeMessageHeadersExactly()
    {
        const string payload = "{ \n  \"z\":\"Cafe\u0301\", \"a\":1\n}";
        var legacy = CreateLegacy(payload, SignedHeaders);
        var untrustedBrokerHeaders = CompleteHeaders("broker-scheme", "broker-key", 'b');

        var mapped = LegacyEventTransportMessageMapper.Map(legacy, untrustedBrokerHeaders);

        Assert.Equal(legacy.EventId, mapped.EventId);
        Assert.Equal(legacy.EventName, mapped.EventName);
        Assert.Equal(legacy.EventVersion, mapped.EventVersion);
        Assert.Equal(legacy.TenantId, mapped.TenantId);
        Assert.Equal(legacy.CorrelationId, mapped.CorrelationId);
        Assert.Equal(legacy.CausationId, mapped.CausationId);
        Assert.Equal(legacy.Producer, mapped.Producer);
        Assert.Equal(legacy.OccurredAtUtc, mapped.OccurredAtUtc);
        Assert.Equal(new UTF8Encoding(false, true).GetBytes(payload), mapped.CanonicalPayloadUtf8.ToArray());
        Assert.False(mapped.CanonicalPayloadUtf8.Span.StartsWith(Encoding.UTF8.Preamble));
        AssertHeadersEqual(SignedHeaders, mapped.TransportMetadata.Headers);
        Assert.Contains("\"z\":\"Cafe\u0301\", \"a\":1", mapped.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_UsesBrokerHeadersOnlyWhenLegacyMessageHeadersAreEmpty()
    {
        var brokerHeaders = CompleteHeaders("hmac-sha256-v1", "broker-key", 'c');
        var legacy = CreateLegacy("{\"a\":1}", new Dictionary<string, string>());

        var mapped = LegacyEventTransportMessageMapper.Map(legacy, brokerHeaders);

        AssertHeadersEqual(brokerHeaders, mapped.TransportMetadata.Headers);
    }

    [Theory]
    [MemberData(nameof(InvalidHeaders))]
    public void Map_RejectsInvalidTrustedHeaderSets(
        IReadOnlyDictionary<string, string>? messageHeaders,
        IEnumerable<KeyValuePair<string, string>>? brokerHeaders)
    {
        var legacy = CreateLegacy("{}", messageHeaders);

        Assert.Throws<EventValidationException>(
            () => LegacyEventTransportMessageMapper.Map(legacy, brokerHeaders));
    }

    [Fact]
    public void Map_RejectsCaseInsensitiveDuplicateBrokerHeaders()
    {
        var brokerHeaders = new[]
        {
            KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader, "hmac-sha256-v1"),
            KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, "key"),
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, new string('a', 64)),
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader.ToLowerInvariant(), new string('b', 64))
        };

        Assert.Throws<EventValidationException>(
            () => LegacyEventTransportMessageMapper.Map(CreateLegacy("{}", null), brokerHeaders));
    }

    [Fact]
    public void Map_RejectsPayloadContainingInvalidUtf16()
    {
        var invalid = "{\"value\":\"\uD800\"}";

        Assert.Throws<EncoderFallbackException>(
            () => LegacyEventTransportMessageMapper.Map(CreateLegacy(invalid, SignedHeaders)));
    }

    public static TheoryData<IReadOnlyDictionary<string, string>?, IEnumerable<KeyValuePair<string, string>>?>
        InvalidHeaders =>
        new()
        {
            {
                new Dictionary<string, string>
                {
                    [TrustedTransportMetadata.SignatureSchemeHeader] = "hmac-sha256-v1"
                },
                null
            },
            {
                null,
                new[]
                {
                    KeyValuePair.Create(
                        TrustedTransportMetadata.SignatureSchemeHeader,
                        "hmac-sha256-v1")
                }
            },
            {
                new Dictionary<string, string>
                {
                    [TrustedTransportMetadata.SignatureSchemeHeader] = "hmac-sha256-v1",
                    [TrustedTransportMetadata.KeyIdHeader] = "key",
                    [TrustedTransportMetadata.SignatureHeader] = new string('a', 64),
                    ["X-Unknown"] = "value"
                },
                null
            },
            { CompleteHeaders(" ", "key", 'a'), null },
            { CompleteHeaders("hmac-sha256-v1\r\nInjected", "key", 'a'), null }
        };

    private static LegacyEventTransportMessage CreateLegacy(
        string payload,
        IReadOnlyDictionary<string, string>? headers) =>
        new(
            Guid.Parse("017f2f44-d0f3-42a7-a77f-4edca6637390"),
            "ppm.audit-intent-submitted.v1",
            1,
            Guid.Parse("6fb40d4e-9bd8-435f-a6c7-af88737fab56"),
            Guid.Parse("6bdf1272-a41a-4f7a-a1cb-46cb4c76cc2f"),
            Guid.Parse("ba4c30be-ec83-41ef-a554-c18385b8d80e"),
            "Diten.PpmService",
            new DateTimeOffset(2026, 7, 31, 9, 10, 11, TimeSpan.Zero),
            payload,
            headers);

    private static IReadOnlyDictionary<string, string> CompleteHeaders(
        string scheme,
        string keyId,
        char signatureCharacter) =>
        new Dictionary<string, string>
        {
            [TrustedTransportMetadata.SignatureSchemeHeader] = scheme,
            [TrustedTransportMetadata.KeyIdHeader] = keyId,
            [TrustedTransportMetadata.SignatureHeader] = new string(signatureCharacter, 64)
        };

    private static void AssertHeadersEqual(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var header in expected)
        {
            Assert.True(actual.TryGetValue(header.Key, out var value));
            Assert.Equal(header.Value, value);
        }
    }
}
