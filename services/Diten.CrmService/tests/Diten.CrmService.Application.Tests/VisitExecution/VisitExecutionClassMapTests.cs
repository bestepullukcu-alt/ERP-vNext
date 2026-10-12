using Diten.CrmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3 (BE-a) — the CRM new-aggregate GUID trap: every Guid of the execution (the plan FK, the evidence content ids,
/// the timing / mark / note product and step ids) is written as a STRING, or every by-plan / by-id filter silently finds
/// nothing. And the stored document reads back (the AutoMap is strict about unknown elements).
/// </summary>
public sealed class VisitExecutionClassMapTests
{
    [Fact]
    public void Every_execution_guid_is_a_string_and_the_document_round_trips()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        foreach (var type in new[]
                 {
                     typeof(ExecutionEntity), typeof(VisitExecutionLocation), typeof(VisitExecutionEvidence),
                     typeof(VisitExecutionSignature), typeof(VisitExecutionProductTiming), typeof(VisitExecutionSlideTiming),
                     typeof(VisitExecutionMark), typeof(VisitExecutionPersonalNote), typeof(VisitEvidencePolicy)
                 })
        {
            Assert.True(BsonClassMap.IsClassMapRegistered(type), type.Name);
        }

        var product = Guid.NewGuid();
        var execution = new ExecutionEntity
        {
            TenantId = Guid.NewGuid(),
            PlannedVisitId = Guid.NewGuid(),
            ResourceId = "rep",
            StartedAt = DateTimeOffset.UtcNow,
            Photo = new VisitExecutionEvidence { ContentId = Guid.NewGuid(), MediaType = "image/jpeg", ByteSize = 3 },
            Signature = new VisitExecutionSignature { ContentId = Guid.NewGuid(), MediaType = "image/png" },
            ProductTimings = { new VisitExecutionProductTiming { ProductId = product, Seconds = 10 } },
            SlideTimings = { new VisitExecutionSlideTiming { ProductId = product, StepId = Guid.NewGuid(), Seconds = 5 } },
            Marks = { new VisitExecutionMark { ProductId = product, Flags = { "question" } } },
            PersonalNotes = { new VisitExecutionPersonalNote { ProductId = product, Text = "x" } }
        };

        var doc = execution.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["_id"].BsonType);
        Assert.Equal(BsonType.String, doc["TenantId"].BsonType);
        Assert.Equal(BsonType.String, doc["PlannedVisitId"].BsonType);
        Assert.Equal(BsonType.String, doc["Photo"]["ContentId"].BsonType);
        Assert.Equal(BsonType.String, doc["Signature"]["ContentId"].BsonType);
        Assert.Equal(BsonType.String, doc["ProductTimings"][0]["ProductId"].BsonType);
        Assert.Equal(BsonType.String, doc["SlideTimings"][0]["StepId"].BsonType);
        Assert.Equal(BsonType.String, doc["Marks"][0]["ProductId"].BsonType);
        Assert.Equal(BsonType.String, doc["PersonalNotes"][0]["ProductId"].BsonType);

        var read = BsonSerializer.Deserialize<ExecutionEntity>(doc);
        Assert.Equal(execution.PlannedVisitId, read.PlannedVisitId);
        Assert.Equal(execution.Photo.ContentId, read.Photo!.ContentId);
        Assert.Equal(product, read.Marks.Single().ProductId);
    }
}
