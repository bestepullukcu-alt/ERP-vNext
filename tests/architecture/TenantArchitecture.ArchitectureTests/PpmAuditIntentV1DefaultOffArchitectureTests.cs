using System.Text.Json;

namespace TenantArchitecture.ArchitectureTests;

/// <summary>
/// Default-off source guard for the governance-only PPM audit seam.  It has no PPM project reference,
/// so it remains executable while the PPM-owned contracts artifact is intentionally unavailable.
/// </summary>
public sealed class PpmAuditIntentV1DefaultOffArchitectureTests
{
    private const string EventName = "ppm.audit-intent.submitted.v1";
    private const string EventTypeName = "PpmAuditIntentSubmittedV1";
    private const string PpmAssemblyPrefix = "Diten.PpmService";
    private static readonly string[] DefaultOffFoundationPaths =
    [
        "services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/PpmAuditIntentV1AuditMapping.cs",
        "services/Diten.Platform/src/Diten.Platform.Infrastructure/Eventing/PpmAuditIntentV1TransportShapeValidator.cs"
    ];

    private static readonly string[] RequiredPayloadProperties =
    [
        "actorId",
        "auditIntentId",
        "entityId",
        "entityType",
        "mutation",
        "occurredAtUtc"
    ];

    [Fact]
    public void Ppm_audit_identity_exists_only_in_the_unregistered_default_off_foundation()
    {
        var identitySources = PlatformRuntimeSources()
            .Where(source => source.Body.Contains(EventName, StringComparison.Ordinal)
                             || source.Body.Contains(EventTypeName, StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(
            DefaultOffFoundationPaths.OrderBy(path => path),
            identitySources.Select(source => source.RelativePath).OrderBy(path => path));
        Assert.All(identitySources, source =>
        {
            Assert.DoesNotContain("AddScoped", source.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("AddSingleton", source.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("IConsumer<", source.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("IPublishEndpoint", source.Body, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Both_platform_transport_message_families_are_guarded_against_ppm_consumer_wiring()
    {
        var root = RepoRoot();
        var applicationAlias = Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "Eventing",
            "PlatformEventTransportMessageAlias.cs");
        var brokerEnvelope = Path.Combine(
            root,
            "services", "Diten.Building.Blocks", "src", "Diten.BuildingBlocks.Eventing",
            "EventTransportMessage.cs");

        Assert.True(File.Exists(applicationAlias), "The current Platform transport alias disappeared.");
        Assert.True(File.Exists(brokerEnvelope), "The shared eventing transport message disappeared.");
        Assert.Contains(
            "Diten.Platform.Application.Contracts.Eventing.EventTransportMessage",
            File.ReadAllText(applicationAlias),
            StringComparison.Ordinal);
        Assert.Contains(
            "class EventTransportMessage",
            File.ReadAllText(brokerEnvelope),
            StringComparison.Ordinal);

        var consumerWiring = PlatformRuntimeSources()
            .Where(source => source.Body.Contains("IConsumer<EventTransportMessage>", StringComparison.Ordinal)
                             || source.Body.Contains("IConsumer<Diten.Platform.Application.Contracts.Eventing.EventTransportMessage>", StringComparison.Ordinal)
                             || source.Body.Contains("IConsumer<Diten.BuildingBlocks.Eventing.EventTransportMessage>", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(consumerWiring);
        Assert.DoesNotContain(
            consumerWiring,
            source => source.Body.Contains(EventName, StringComparison.Ordinal)
                      || source.Body.Contains(EventTypeName, StringComparison.Ordinal)
                      || source.Body.Contains(PpmAssemblyPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Compatibility_fixture_has_the_locked_six_field_shape_without_an_undocumented_aggregate_allowlist()
    {
        const string payload =
            "{\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"entityType\":\"BenefitCommitment\",\"mutation\":\"lifecycle-changed\",\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}";

        using var document = JsonDocument.Parse(payload);
        var names = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(RequiredPayloadProperties.OrderBy(name => name), names);
        Assert.All(RequiredPayloadProperties.Take(3), propertyName =>
        {
            var value = document.RootElement.GetProperty(propertyName);
            Assert.Equal(JsonValueKind.String, value.ValueKind);
            Assert.True(Guid.TryParse(value.GetString(), out var identifier));
            Assert.NotEqual(Guid.Empty, identifier);
        });
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("entityType").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("mutation").GetString()));
        Assert.True(DateTimeOffset.TryParse(
            document.RootElement.GetProperty("occurredAtUtc").GetString(),
            out var occurredAtUtc));
        Assert.Equal(TimeSpan.Zero, occurredAtUtc.Offset);
    }

    private static IEnumerable<SourceFile> PlatformRuntimeSources()
    {
        var root = Path.Combine(RepoRoot(), "services", "Diten.Platform", "src");
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => new SourceFile(
                Path.GetRelativePath(RepoRoot(), path).Replace('\\', '/'),
                File.ReadAllText(path)));
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found (no AGENTS.md above the test binary).");
    }

    private sealed record SourceFile(string RelativePath, string Body);
}
