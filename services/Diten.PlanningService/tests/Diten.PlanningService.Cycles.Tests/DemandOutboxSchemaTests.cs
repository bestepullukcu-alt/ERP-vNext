using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private static JsonDocument EventSchema() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "demand-v2.events.schema.json")));

    [ManualDraftMongoFact]
    public async Task Outbox_PublishedAndInvalidated_ConformToOwnedDraftSchema()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: true);
        var publishedMessage = await context.Outbox.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id && x.EventType == "demand.revision.published.v2")
            .SingleAsync();
        using var schema = EventSchema();
        using var publishedPayload = JsonDocument.Parse(publishedMessage.Payload);
        AssertDraftSchema(publishedPayload.RootElement, schema.RootElement);
        Assert.Equal("DemandRevisionPublished",
            publishedPayload.RootElement.GetProperty("eventType").GetString());
        Assert.Equal("Published", publishedPayload.RootElement.GetProperty("newState").GetString());
        Assert.Equal(publishedMessage.EventId.ToString("D"),
            publishedPayload.RootElement.GetProperty("eventId").GetString());
        Assert.Equal(2, publishedPayload.RootElement.GetProperty("scopeSummary")
            .GetProperty("selectedSeriesCount").GetInt32());
        var publishedDigest = publishedPayload.RootElement.GetProperty("scopeSummary")
            .GetProperty("scopeDigest").GetString();
        Assert.Equal(64, publishedDigest!.Length);
        var snapshot = (await published.ReadSnapshotAsync(draft.TenantId,
            draft.LegalEntityId, draft.Id, default))!.Value;
        Assert.Equal(snapshot.Manifest.Checksum, publishedPayload.RootElement
            .GetProperty("integritySummary").GetProperty("digest").GetString());
        Assert.Equal(snapshot.Manifest.ExpectedRowCount, publishedPayload.RootElement
            .GetProperty("integritySummary").GetProperty("expectedRowCount").GetInt32());

        var actor = Guid.NewGuid();
        var invalidator = Invalidator(draft, drafts, published, context);
        Assert.Equal(InvalidationOutcome.Invalidated,
            (await invalidator.InvalidateAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, actor, InvalidationImpactCode.ContentIntegrity,
                "Wrong source quantity", "verified-mrp-impact-fixture",
                "schema-check", 0, 3, default)).Outcome);
        var invalidatedMessage = await context.Outbox.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id && x.EventType == "demand.revision.invalidated.v2")
            .SingleAsync();
        using var invalidatedPayload = JsonDocument.Parse(invalidatedMessage.Payload);
        AssertDraftSchema(invalidatedPayload.RootElement, schema.RootElement);
        Assert.Equal("Invalidated", invalidatedPayload.RootElement.GetProperty("newState").GetString());
        Assert.Equal(4, invalidatedPayload.RootElement.GetProperty("stateVersion").GetInt32());
        Assert.Equal(publishedDigest, invalidatedPayload.RootElement.GetProperty("scopeSummary")
            .GetProperty("scopeDigest").GetString());
        Assert.Equal(snapshot.Manifest.Checksum, invalidatedPayload.RootElement
            .GetProperty("integritySummary").GetProperty("digest").GetString());
        var invalidation = invalidatedPayload.RootElement.GetProperty("invalidation");
        Assert.Equal("MissingDuplicateOrCorruptContent",
            invalidation.GetProperty("reasonCategory").GetString());
        Assert.Equal(actor.ToString("D"), invalidation.GetProperty("actorId").GetString());
        var audit = await context.ManualDraftPublicationAudit.Find(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id && x.NewState == DemandRevisionState.Invalidated)
            .SingleAsync();
        Assert.Equal(audit.BusinessImpact, invalidation.GetProperty("businessImpact").GetString());

        var missingScope = JsonNode.Parse(publishedMessage.Payload)!.AsObject();
        missingScope.Remove("scopeSummary");
        AssertRejected(missingScope, schema.RootElement);
        var unexpectedRows = JsonNode.Parse(publishedMessage.Payload)!.AsObject();
        unexpectedRows["rows"] = new JsonArray();
        AssertRejected(unexpectedRows, schema.RootElement);
        var missingImpact = JsonNode.Parse(invalidatedMessage.Payload)!.AsObject();
        missingImpact["invalidation"]!.AsObject().Remove("businessImpact");
        AssertRejected(missingImpact, schema.RootElement);
        var wrongState = JsonNode.Parse(invalidatedMessage.Payload)!.AsObject();
        wrongState["newState"] = "Published";
        AssertRejected(wrongState, schema.RootElement);
    }

    [ManualDraftMongoFact]
    public async Task Invalidate_WithoutVerifiedBusinessImpact_LeavesNoDecisionOrEvent()
    {
        var (draft, drafts, published, context) = await PublishedFixture(secondSeries: false);
        var authority = new InvalidationAuthority(draft, businessImpact: null);
        var result = await Invalidate(Invalidator(draft, drafts, published, context,
            authority), draft);
        Assert.Equal(InvalidationOutcome.MaterialImpactMissing, result.Outcome);
        Assert.Equal(DemandRevisionState.Published,
            (await published.ReadSnapshotAsync(draft.TenantId, draft.LegalEntityId,
                draft.Id, default))!.Value.Manifest.State);
        Assert.Equal(0, await context.Outbox.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id && x.EventType == "demand.revision.invalidated.v2"));
        Assert.Equal(0, await context.ManualDraftPublicationAudit.CountDocumentsAsync(x =>
            x.TenantId == draft.TenantId && x.LegalEntityId == draft.LegalEntityId &&
            x.RevisionId == draft.Id && x.NewState == DemandRevisionState.Invalidated));
    }

    private static void AssertRejected(JsonObject node, JsonElement schema)
    {
        using var payload = JsonDocument.Parse(node.ToJsonString());
        Assert.ThrowsAny<Exception>(() => AssertDraftSchema(payload.RootElement, schema));
    }

    private static void AssertDraftSchema(JsonElement payload, JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var type))
        {
            var expected = type.GetString();
            Assert.True(expected switch
            {
                "object" => payload.ValueKind == JsonValueKind.Object,
                "string" => payload.ValueKind == JsonValueKind.String,
                "integer" => payload.ValueKind == JsonValueKind.Number &&
                    payload.TryGetInt64(out _),
                _ => throw new InvalidDataException($"Unsupported test schema type: {expected}")
            });
        }
        if (schema.TryGetProperty("required", out var required))
            foreach (var property in required.EnumerateArray())
                Assert.True(payload.TryGetProperty(property.GetString()!, out _),
                    $"Missing event field: {property.GetString()}");
        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var property in payload.EnumerateObject())
            {
                if (properties.TryGetProperty(property.Name, out var fieldSchema))
                    AssertDraftSchema(property.Value, fieldSchema);
                else if (schema.TryGetProperty("additionalProperties", out var additional) &&
                         additional.ValueKind == JsonValueKind.False)
                    Assert.Fail($"Unexpected event field: {property.Name}");
            }
        }
        if (schema.TryGetProperty("enum", out var values))
            Assert.Contains(values.EnumerateArray(), x =>
                x.ToString() == payload.ToString());
        if (schema.TryGetProperty("const", out var constant))
            Assert.Equal(constant.ToString(), payload.ToString());
        if (schema.TryGetProperty("minLength", out var minLength))
            Assert.True(payload.GetString()!.Length >= minLength.GetInt32());
        if (schema.TryGetProperty("minimum", out var minimum))
            Assert.True(payload.GetInt64() >= minimum.GetInt64());
        if (schema.TryGetProperty("format", out var format))
        {
            var value = payload.GetString();
            Assert.True(format.GetString() switch
            {
                "uuid" => Guid.TryParse(value, out _),
                "date" => DateOnly.TryParseExact(value, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                "date-time" => DateTimeOffset.TryParse(value,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                _ => throw new InvalidDataException("Unsupported event format in DRAFT schema.")
            });
        }
        if (schema.TryGetProperty("not", out var not) &&
            not.TryGetProperty("required", out var prohibited))
            Assert.False(prohibited.EnumerateArray().All(x =>
                payload.TryGetProperty(x.GetString()!, out _)));
        if (schema.TryGetProperty("allOf", out var conditions))
        {
            var eventType = payload.GetProperty("eventType").GetString();
            foreach (var condition in conditions.EnumerateArray())
            {
                var selector = condition.GetProperty("if").GetProperty("properties")
                    .GetProperty("eventType");
                var matches = selector.TryGetProperty("const", out var exact)
                    ? exact.GetString() == eventType
                    : selector.GetProperty("enum").EnumerateArray()
                        .Any(x => x.GetString() == eventType);
                if (matches)
                    AssertDraftSchema(payload, condition.GetProperty("then"));
            }
        }
    }
}
