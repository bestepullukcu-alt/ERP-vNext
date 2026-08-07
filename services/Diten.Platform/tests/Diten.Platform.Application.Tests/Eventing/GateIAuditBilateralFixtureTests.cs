using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Infrastructure.Eventing;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class GateIAuditBilateralFixtureTests
{
    public static IEnumerable<object[]> Producers() => FixtureCatalog.Load().Select(item => new object[] { item });

    [Theory]
    [MemberData(nameof(Producers))]
    public void OwnerCheckpointBytes_MatchDeclaredProvenanceAndCryptographicVector(ProducerFixture fixture)
    {
        Assert.Equal(fixture.ByteLength, fixture.Payload.Length);
        Assert.Equal(fixture.PayloadSha256, Sha256(fixture.Payload));
        Assert.Equal(fixture.PayloadSha256, fixture.ChecksumText.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]);
        Assert.Equal(fixture.ExpectedSignature, Hmac(fixture.Secret, fixture.UnsignedMessage));
        Assert.Equal(fixture.ExpectedSignature, fixture.ManifestHmacSha256);
        if (fixture.SigningInputSha256.Length != 0)
            Assert.Equal(fixture.SigningInputSha256,
                Sha256(new EventSigningInputBuilder().Build(fixture.UnsignedMessage, fixture.Scheme)));

        using var schema = JsonDocument.Parse(fixture.Schema);
        Assert.Equal(fixture.SchemaId, schema.RootElement.GetProperty("$id").GetString());
        Assert.Equal(1, fixture.EventVersion);
        Assert.Contains(fixture.ModuleCode, fixture.Projection, StringComparison.Ordinal);
        Assert.Contains(fixture.Producer, fixture.Projection, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Producers))]
    public async Task CanonicalFixture_TraversesRealVerifierParserAdapterAndIsIdempotent(ProducerFixture fixture)
    {
        var repository = new DigestAcceptanceRepository();
        var processor = Processor(fixture, repository);

        Assert.Equal(GateIAuditAcceptanceResult.Accepted,
            await processor.ProcessAsync(fixture.UnsignedMessage, fixture.Headers(), fixture.SigningIdentity));
        Assert.Equal(GateIAuditAcceptanceResult.Duplicate,
            await processor.ProcessAsync(fixture.UnsignedMessage, fixture.Headers(), fixture.SigningIdentity));

        Assert.Equal(fixture.ModuleCode, repository.LastMapping!.ModuleCode);
        Assert.Equal(fixture.ExpectedEntityType, repository.LastMapping.AuditEntityType);
        Assert.Equal(fixture.ExpectedOperation, $"AuditOperation.{repository.LastMapping.Operation}");
        Assert.Equal(fixture.EventId, repository.LastIntent!.AuditIntentId);
        Assert.Equal(fixture.PayloadSha256, repository.LastIntent.PayloadSha256);
    }

    [Theory]
    [MemberData(nameof(Producers))]
    public async Task ContractSecurityAndImmutableIdentityMutations_FailClosed(ProducerFixture fixture)
    {
        var repository = new DigestAcceptanceRepository();
        var processor = Processor(fixture, repository);
        await processor.ProcessAsync(fixture.UnsignedMessage, fixture.Headers(), fixture.SigningIdentity);

        await Assert.ThrowsAsync<EventUnsupportedException>(() => processor.ProcessAsync(
            fixture.Message(producer: fixture.Producer + ".wrong"), fixture.Headers(), fixture.SigningIdentity));
        await Assert.ThrowsAsync<EventUnsupportedException>(() => processor.ProcessAsync(
            fixture.Message(eventName: "wrong." + fixture.EventName), fixture.Headers(), fixture.SigningIdentity));
        await Assert.ThrowsAsync<EventValidationException>(() => Task.FromResult(
            fixture.Message(eventVersion: 2)));

        var wrongPayload = MutateCanonicalEntityId(fixture.Payload);
        var payloadMessage = fixture.Message(payload: wrongPayload);
        await Assert.ThrowsAsync<GateIAuditConflictException>(() => processor.ProcessAsync(
            payloadMessage, fixture.Headers(Sign(payloadMessage, fixture)), fixture.SigningIdentity));

        var metadataMessage = fixture.Message(correlationId: Guid.NewGuid());
        await Assert.ThrowsAsync<GateIAuditConflictException>(() => processor.ProcessAsync(
            metadataMessage, fixture.Headers(Sign(metadataMessage, fixture)), fixture.SigningIdentity));

        const string alternateIdentity = "gate-i-bilateral-alternate-test-identity";
        await Assert.ThrowsAsync<GateIAuditConflictException>(() => processor.ProcessAsync(
            fixture.UnsignedMessage, fixture.Headers(), alternateIdentity));

        await Assert.ThrowsAsync<EventSecurityException>(() => processor.ProcessAsync(
            fixture.UnsignedMessage, fixture.Headers(new string('0', 64)), fixture.SigningIdentity));
        await Assert.ThrowsAsync<EventUnsupportedException>(() => processor.ProcessAsync(
            fixture.Message(payload: MutateCanonicalPair(fixture.Payload)),
            fixture.Headers(Sign(fixture.Message(payload: MutateCanonicalPair(fixture.Payload)), fixture)),
            fixture.SigningIdentity));
    }

    [Theory]
    [MemberData(nameof(Producers))]
    public async Task TestOnlyIdentityAndKey_AreRejectedByProductionBoundaryInActiveAndPreviousSlots(ProducerFixture fixture)
    {
        Assert.True(fixture.IsTestOnly);
        Assert.True(fixture.ProductionReject);
        var authorization = new SignedEventAuthorizationTuple(
            fixture.Producer, fixture.EventName, 1, fixture.Scheme, fixture.SigningIdentity);
        var provider = new ProductionBoundaryProvider(fixture.SigningIdentity, fixture.KeyId);

        await Assert.ThrowsAsync<EventSecurityException>(async () =>
            await provider.GetVerificationKeyAsync(authorization, fixture.KeyId));
        await Assert.ThrowsAsync<EventSecurityException>(async () =>
            await provider.GetVerificationKeyAsync(authorization, fixture.KeyId + ".previous"));
    }

    [Fact]
    public void ProvenanceMatricesCoverDelegatedNonDelegatedAndMissingProof_WithoutPpmReuse()
    {
        var fixtures = FixtureCatalog.Load();
        var ppm = File.ReadAllText(Path.Combine(FixtureCatalog.RepositoryRoot,
            "services/Diten.Platform/tests/Diten.Platform.Application.Tests/Fixtures/Eventing/ppm-audit-intent-submitted-v1.json"));
        using var ppmJson = JsonDocument.Parse(ppm);
        var ppmKeyId = ppmJson.RootElement.GetProperty("keyId").GetString();
        var ppmSignature = ppmJson.RootElement.GetProperty("signatureLowerHex").GetString();
        var ppmSecret = ppmJson.RootElement.GetProperty("fixtureSecretBase64").GetString();

        foreach (var fixture in fixtures)
        {
            Assert.Contains("delegat", fixture.ProvenanceEvidence, StringComparison.OrdinalIgnoreCase);
            Assert.True(fixture.ProvenanceEvidence.Contains("non-delegat", StringComparison.OrdinalIgnoreCase)
                || fixture.ProvenanceEvidence.Contains("\"delegated\": false", StringComparison.Ordinal));
            Assert.Contains("missing", fixture.ProvenanceEvidence, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(ppmKeyId, fixture.KeyId);
            Assert.NotEqual(ppmSignature, fixture.ExpectedSignature);
            Assert.NotEqual(ppmSecret, Convert.ToBase64String(fixture.Secret));
        }
    }

    private static GateIAuditConsumerProcessor Processor(
        ProducerFixture fixture, IGateIAuditAcceptanceRepository repository) => new(
        new GateIAuditAdapterRegistry(),
        new HmacSha256EventSignatureVerifier(
            new FixtureKeyProvider(fixture),
            new EventSigningInputBuilder(),
            new FixedTimeProvider(fixture.OccurredAtUtc.AddMinutes(1))),
        repository);

    private static string Sign(EventTransportMessage message, ProducerFixture fixture) =>
        Hmac(fixture.Secret, message);

    private static string Hmac(byte[] secret, EventTransportMessage message) =>
        Convert.ToHexString(HMACSHA256.HashData(secret,
            new EventSigningInputBuilder().Build(message,
                message.TransportMetadata.Headers.TryGetValue(TrustedTransportMetadata.SignatureSchemeHeader, out var scheme)
                    ? scheme
                    : throw new InvalidOperationException("Fixture scheme is missing.")))).ToLowerInvariant();

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] MutateCanonicalEntityId(byte[] payload)
    {
        var text = Encoding.UTF8.GetString(payload);
        var marker = "\"entityId\":\"";
        var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return Encoding.UTF8.GetBytes(text[..start] + "99999999-9999-4999-8999-999999999999" + text[(start + 36)..]);
    }

    private static byte[] MutateCanonicalPair(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["auditIntentId"] = root.GetProperty("auditIntentId").GetString(),
            ["actorId"] = root.GetProperty("actorId").GetString(),
            ["entityType"] = "UnsupportedEntity",
            ["entityId"] = root.GetProperty("entityId").GetString(),
            ["mutation"] = "UnsupportedMutation",
            ["occurredAtUtc"] = root.GetProperty("occurredAtUtc").GetString()
        }));
    }

    private sealed class FixtureKeyProvider(ProducerFixture fixture) : IEventVerificationKeyProvider
    {
        public ValueTask<EventVerificationKey> GetVerificationKeyAsync(
            SignedEventAuthorizationTuple authorization, string keyId, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(keyId, fixture.KeyId, StringComparison.Ordinal)
                || !string.Equals(authorization.Producer, fixture.Producer, StringComparison.Ordinal)
                || !string.Equals(authorization.EventName, fixture.EventName, StringComparison.Ordinal))
            {
                throw new EventSecurityException("Fixture key tuple is not trusted.", "fixture.key.untrusted");
            }
            return ValueTask.FromResult(new EventVerificationKey(keyId, fixture.Secret));
        }
    }

    private sealed class ProductionBoundaryProvider(string fixtureIdentity, string fixtureKeyId)
        : IEventVerificationKeyProvider
    {
        public ValueTask<EventVerificationKey> GetVerificationKeyAsync(
            SignedEventAuthorizationTuple authorization, string keyId, CancellationToken cancellationToken = default)
        {
            if (string.Equals(authorization.SigningIdentity, fixtureIdentity, StringComparison.Ordinal)
                || string.Equals(keyId, fixtureKeyId, StringComparison.Ordinal)
                || string.Equals(keyId, fixtureKeyId + ".previous", StringComparison.Ordinal))
            {
                throw new EventSecurityException("Test-only fixture material is forbidden in production.",
                    "fixture.production-rejected");
            }
            throw new EventSecurityException("Key is not provisioned.", "event.key.untrusted");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class DigestAcceptanceRepository : IGateIAuditAcceptanceRepository
    {
        private readonly Dictionary<Guid, (string Payload, string Metadata)> _accepted = [];
        public GateIAuditIntent? LastIntent { get; private set; }
        public GateIAuditMapping? LastMapping { get; private set; }

        public Task<GateIAuditAcceptanceResult> AcceptAsync(EventTransportMessage message, GateIAuditIntent intent,
            GateIAuditMapping mapping, CancellationToken cancellationToken)
        {
            var metadata = string.Join('\n', message.Producer, message.EventName, message.EventVersion,
                message.CorrelationId, message.CausationId, message.TenantId, message.OccurredAtUtc,
                mapping.ModuleCode, intent.VerifiedSigningIdentity,
                message.TransportMetadata.Headers[TrustedTransportMetadata.SignatureSchemeHeader],
                message.TransportMetadata.Headers[TrustedTransportMetadata.KeyIdHeader],
                message.TransportMetadata.Headers[TrustedTransportMetadata.SignatureHeader]);
            var metadataDigest = Sha256(Encoding.UTF8.GetBytes(metadata));
            if (_accepted.TryGetValue(message.EventId, out var existing))
            {
                if (!string.Equals(existing.Payload, intent.PayloadSha256, StringComparison.Ordinal)
                    || !string.Equals(existing.Metadata, metadataDigest, StringComparison.Ordinal))
                    throw new GateIAuditConflictException();
                return Task.FromResult(GateIAuditAcceptanceResult.Duplicate);
            }
            _accepted.Add(message.EventId, (intent.PayloadSha256, metadataDigest));
            LastIntent = intent;
            LastMapping = mapping;
            return Task.FromResult(GateIAuditAcceptanceResult.Accepted);
        }
    }
}

public sealed record ProducerFixture(
    string ModuleCode, string Checkpoint, string FixtureRoot, string Producer, string EventName, int EventVersion,
    string SchemaId, int ByteLength, string PayloadSha256, string ManifestHmacSha256, byte[] Payload,
    string ChecksumText, byte[] Schema, string Projection, string ProvenanceEvidence, string Scheme,
    string SigningIdentity, string KeyId, byte[] Secret, string ExpectedSignature, string SigningInputSha256,
    Guid EventId, Guid TenantId, Guid CorrelationId, Guid? CausationId, DateTimeOffset OccurredAtUtc,
    bool IsTestOnly, bool ProductionReject, string ExpectedEntityType, string ExpectedOperation)
{
    public EventTransportMessage UnsignedMessage => Message();

    public EventTransportMessage Message(byte[]? payload = null, string? producer = null, string? eventName = null,
        int? eventVersion = null, Guid? correlationId = null) => new(
        EventId, eventName ?? EventName, eventVersion ?? EventVersion, correlationId ?? CorrelationId,
        CausationId, TenantId, producer ?? Producer, OccurredAtUtc, payload ?? Payload,
        new TrustedTransportMetadata(Headers()));

    public KeyValuePair<string, string>[] Headers(string? signature = null) =>
    [
        KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader, Scheme),
        KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, KeyId),
        KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, signature ?? ExpectedSignature)
    ];
}

internal static class FixtureCatalog
{
    private const string ManifestPath =
        "execution/domains/platform-shared-services/module-packs/fixtures/MOD-0021/gate-i/gate-i-audit-intake-bilateral-acceptance-v1.json";
    internal static string RepositoryRoot { get; } = GitText("rev-parse", "--show-toplevel").Trim();

    internal static IReadOnlyList<ProducerFixture> Load()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepositoryRoot, ManifestPath)));
        return manifest.RootElement.GetProperty("producerCases").EnumerateArray().Select(Load).ToArray();
    }

    private static ProducerFixture Load(JsonElement item)
    {
        string Text(string name) => item.GetProperty(name).GetString()!;
        var checkpoint = Text("checkpoint");
        var root = Text("fixtureRoot");
        var canonical = item.GetProperty("canonical");
        var artifacts = item.GetProperty("artifacts");
        byte[] Blob(string name) => GitBytes("show", $"{checkpoint}:{root}/{name}");
        string Artifact(string property) => artifacts.GetProperty(property).GetString()!;
        var payload = Blob(canonical.GetProperty("file").GetString()!);
        var vectorBytes = Blob(Artifact("signingVector"));
        using var vectorDocument = JsonDocument.Parse(vectorBytes);
        var vector = vectorDocument.RootElement;
        string V(params string[] names) => names.Select(name => vector.TryGetProperty(name, out var value)
            ? value.GetString() : null).First(value => value is not null)!;
        var envelope = vector.TryGetProperty("envelope", out var nested) ? nested : vector;
        string E(params string[] names) => names.Select(name => envelope.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.Null ? "-" : value.GetString()
            : null).First(value => value is not null)!;
        var expectedSignatureRaw = Encoding.UTF8.GetString(Blob(Artifact("expectedSignature"))).Trim();
        var expectedSignature = expectedSignatureRaw.StartsWith('{')
            ? JsonDocument.Parse(expectedSignatureRaw).RootElement.TryGetProperty("signatureLowerHex", out var sig)
                ? sig.GetString()!
                : JsonDocument.Parse(expectedSignatureRaw).RootElement.GetProperty("expectedSignature").GetString()!
            : expectedSignatureRaw;
        var secret = vector.TryGetProperty("testKey", out var hex) ? Convert.FromHexString(hex.GetString()!)
            : vector.TryGetProperty("fixtureSecretBase64", out var b64) ? Convert.FromBase64String(b64.GetString()!)
            : Encoding.UTF8.GetBytes(vector.GetProperty("testOnlyKeyUtf8").GetString()!);
        var projection = Encoding.UTF8.GetString(Blob(Artifact("projection")));
        using var projectionJson = JsonDocument.Parse(projection);
        var expected = projectionJson.RootElement.TryGetProperty("expectedAuditEvent", out var lower) ? lower
            : projectionJson.RootElement.TryGetProperty("ExpectedAuditEvent", out var upper) ? upper
            : projectionJson.RootElement;
        string P(string lowerName, string upperName) => expected.TryGetProperty(lowerName, out var value)
            ? value.GetString()! : expected.GetProperty(upperName).GetString()!;
        var provenanceNames = artifacts.EnumerateObject()
            .Where(property => property.Name.Contains("Provenance", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Delegated", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Value.GetString()!).ToArray();
        var provenance = string.Join('\n', provenanceNames.Select(name => Encoding.UTF8.GetString(Blob(name))));
        var signingIdentity = V("signingIdentity", "fixtureIdentity");
        if (vector.TryGetProperty("fixtureIdentity", out var fixtureIdentity)) signingIdentity = fixtureIdentity.GetString()!;

        return new ProducerFixture(Text("moduleCode"), checkpoint, root, Text("producer"), Text("eventName"),
            item.GetProperty("eventVersion").GetInt32(), Text("schemaId"), canonical.GetProperty("byteLength").GetInt32(),
            canonical.GetProperty("sha256").GetString()!, canonical.GetProperty("hmacSha256").GetString()!, payload,
            Encoding.UTF8.GetString(Blob(Artifact("checksum"))).Trim(), Blob(Artifact("schema")), projection, provenance,
            V("scheme", "signatureScheme"), signingIdentity, V("keyId"), secret, expectedSignature,
            vector.TryGetProperty("signingInputSha256", out var inputDigest) ? inputDigest.GetString()! : string.Empty,
            Guid.Parse(E("eventId")), Guid.Parse(E("tenantId")),
            Guid.Parse(E("correlationId")), ParseNullableGuid(E("causationId")),
            DateTimeOffset.Parse(E("occurredAtUtc")), IsTestOnly(vector), IsProductionReject(vector),
            P("entityType", "EntityType"), P("operation", "Operation"));
    }

    private static bool IsTestOnly(JsonElement vector) =>
        (vector.TryGetProperty("testOnly", out var marker) && marker.GetBoolean())
        || (vector.TryGetProperty("classification", out var classification)
            && classification.GetString()!.Contains("TEST-ONLY", StringComparison.Ordinal))
        || (vector.TryGetProperty("fixturePurpose", out var purpose)
            && purpose.GetString()!.Contains("TEST-ONLY", StringComparison.Ordinal));

    private static bool IsProductionReject(JsonElement vector) =>
        (vector.TryGetProperty("productionValidatorDisposition", out var disposition)
            && disposition.GetString() == "REJECT") || IsTestOnly(vector);

    private static Guid? ParseNullableGuid(string value) => value == "-" ? null : Guid.Parse(value);

    private static string GitText(params string[] arguments) => Encoding.UTF8.GetString(GitBytes(arguments));

    private static byte[] GitBytes(params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Unable to start git for fixture provenance.");
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        return output.ToArray();
    }
}
