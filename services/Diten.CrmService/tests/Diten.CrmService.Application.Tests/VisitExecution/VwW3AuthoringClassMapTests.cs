using Diten.CrmService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;

namespace Diten.CrmService.Application.Tests.VisitExecution;

/// <summary>
/// WP-VW-W3 · W3-BE-c — the new-aggregate class-map trap for what this package stores: the time-boxed exception (its
/// workflow round instance id a string, its dates "yyyy-MM-dd" strings), the policy history row, the objection answer's
/// new members (copy source, rounds, decisions) and the execution's exception id. Each round-trips (the AutoMap is strict).
/// </summary>
public sealed class VwW3AuthoringClassMapTests
{
    [Fact]
    public void The_new_documents_store_guids_and_dates_as_strings_and_read_back()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        foreach (var type in new[] { typeof(VisitEvidenceException), typeof(VisitEvidencePolicyChange) })
        {
            Assert.True(BsonClassMap.IsClassMapRegistered(type), type.Name);
        }

        var instance = Guid.NewGuid();
        var exception = new VisitEvidenceException
        {
            TenantId = Guid.NewGuid(), ResourceId = "rep-1", Rules = { VisitEvidenceExceptionRules.LocationInfo },
            StartDate = new DateOnly(2026, 10, 15), EndDate = new DateOnly(2026, 11, 13), Reason = "r",
            ReviewRounds = { new ClaimReviewRound { WorkflowInstanceId = instance, RoundNo = 1, SubmittedAt = DateTimeOffset.UtcNow } },
            Decisions = { new RegulatoryTextDecision { RoundNo = 1, By = "x", Outcome = WorkflowApprovalDecisions.Return, Comment = "c", At = DateTimeOffset.UtcNow } }
        };
        var doc = exception.ToBsonDocument();
        Assert.Equal(BsonType.String, doc["_id"].BsonType);
        Assert.Equal(BsonType.String, doc["TenantId"].BsonType);
        Assert.Equal(("2026-10-15", "2026-11-13"), (doc["StartDate"].AsString, doc["EndDate"].AsString));
        Assert.Equal(BsonType.String, doc["ReviewRounds"][0]["WorkflowInstanceId"].BsonType);
        var read = BsonSerializer.Deserialize<VisitEvidenceException>(doc);
        Assert.Equal((exception.EndDate, instance, "c"), (read.EndDate, read.ReviewRounds.Single().WorkflowInstanceId, read.Decisions.Single().Comment));

        var change = new VisitEvidencePolicyChange { TenantId = Guid.NewGuid(), At = DateTimeOffset.UtcNow, Field = "radiusMeters", OldValue = "300", NewValue = "400" };
        Assert.Equal("400", BsonSerializer.Deserialize<VisitEvidencePolicyChange>(change.ToBsonDocument()).NewValue);

        var source = Guid.NewGuid();
        var answer = new ObjectionResponse { TenantId = Guid.NewGuid(), ProductId = Guid.NewGuid(), CopiedFromId = source, Status = ObjectionResponseStatuses.InReview };
        answer.ReviewRounds.Add(new ClaimReviewRound { WorkflowInstanceId = instance, RoundNo = 1 });
        var answerDoc = answer.ToBsonDocument();
        Assert.Equal(BsonType.String, answerDoc["CopiedFromId"].BsonType);
        Assert.Equal(source, BsonSerializer.Deserialize<ObjectionResponse>(answerDoc).CopiedFromId);

        var execution = new ExecutionEntity { TenantId = Guid.NewGuid(), PlannedVisitId = Guid.NewGuid(), ExceptionId = Guid.NewGuid(), RelaxedRules = { "photo_optional" } };
        var executionDoc = execution.ToBsonDocument();
        Assert.Equal(BsonType.String, executionDoc["ExceptionId"].BsonType);
        Assert.Equal(execution.ExceptionId, BsonSerializer.Deserialize<ExecutionEntity>(executionDoc).ExceptionId);
    }
}
