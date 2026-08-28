using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling.PerformanceImprovement;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.PerformanceImprovement;

public static class PerformanceImprovementMongoPersistence
{
    public const string Opportunities = "mg_process_improvement_opportunities";
    public const string Outcomes = "mg_process_improvement_outcomes";
    public const string Receipts = "mg_process_modeling_idempotency_receipts";
    public const string AuditIntents = "mg_process_modeling_audit_intents";
    public const string Outbox = "mg_process_modeling_outbox_messages";
    public const string ProcessModelVersions = "mg_process_model_versions";

    public sealed record Index(string Name, string Collection, IReadOnlyList<string> Keys, bool Unique = false);

    public static IReadOnlyList<string> BusinessCollections { get; } = [Opportunities, Outcomes];
    public static IReadOnlyList<Index> BusinessIndexes { get; } =
    [
        new("ix_pi_opportunity_query", Opportunities, ["TenantId", "ProcessModelVersionId", "LifecycleState", "PriorityRank", "_id"]),
        new("ix_pi_outcome_query", Outcomes, ["TenantId", "ProcessImprovementOpportunityId", "ObservedAtUtc", "_id"])
    ];

    public sealed class Context
    {
        private static readonly HashSet<string> Allowed = new(
            [Opportunities, Outcomes, Receipts, AuditIntents, Outbox, ProcessModelVersions], StringComparer.Ordinal);
        private readonly IMongoDatabase _database;

        public Context(IMongoClient client, string databaseName)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
            if (!ProcessModelingMongoGuidPolicy.IsCanonical(client)) throw new InvalidOperationException("performance_improvement_uuid_policy_incompatible");
            if (string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentException(nameof(databaseName));
            _database = client.GetDatabase(databaseName);
        }

        public IMongoClient Client { get; }

        public IMongoCollection<BsonDocument> Collection(string name)
        {
            if (!Allowed.Contains(name)) throw new InvalidOperationException("performance_improvement_collection_forbidden");
            return _database.GetCollection<BsonDocument>(name);
        }
    }

    public sealed class IndexInitializer(Context context)
    {
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var index in BusinessIndexes)
            {
                var keys = new BsonDocument(index.Keys.Select(x => new BsonElement(x, 1)));
                await context.Collection(index.Collection).Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions<BsonDocument> { Name = index.Name, Unique = index.Unique }),
                    cancellationToken: cancellationToken);
            }

            await CreateTechnicalIndexAsync(Receipts, "ux_pi_receipt", ["TenantId", "CommandFamily", "IdempotencyKey"], cancellationToken);
            await CreateTechnicalIndexAsync(AuditIntents, "ux_pi_audit", ["TenantId", "AuditIntentId"], cancellationToken);
            await CreateTechnicalIndexAsync(Outbox, "ux_pi_outbox", ["TenantId", "EventId"], cancellationToken);
        }

        private async Task CreateTechnicalIndexAsync(string collection, string name, string[] fields, CancellationToken cancellationToken)
        {
            var keys = new BsonDocument(fields.Select(x => new BsonElement(x, 1)));
            await context.Collection(collection).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions<BsonDocument> { Name = name, Unique = true }),
                cancellationToken: cancellationToken);
        }
    }

    public sealed class Writer(Context context, Action<PerformanceImprovementContract.Participant>? testOnlyFault = null)
    {
        public async Task<bool> WriteAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            RequireSession(session);
            var effective = request.CommandFamily switch
            {
                PerformanceImprovementContract.Command.CreateOpportunity => await CreateOpportunityAsync(session, request, cancellationToken),
                PerformanceImprovementContract.Command.UpdateOpportunity => await UpdateOpportunityAsync(session, request, cancellationToken),
                PerformanceImprovementContract.Command.QualifyOpportunity => await QualifyOpportunityAsync(session, request, cancellationToken),
                PerformanceImprovementContract.Command.CloseOpportunity => await CloseOpportunityAsync(session, request, cancellationToken),
                PerformanceImprovementContract.Command.RecordOutcome => await RecordOutcomeAsync(session, request, cancellationToken),
                _ => throw new InvalidRequestException("performance_improvement_command_forbidden")
            };
            if (!effective) return false;

            testOnlyFault?.Invoke(PerformanceImprovementContract.Participant.Business);
            await context.Collection(Receipts).InsertOneAsync(session, Receipt(request), cancellationToken: cancellationToken);
            testOnlyFault?.Invoke(PerformanceImprovementContract.Participant.Receipt);
            await context.Collection(AuditIntents).InsertOneAsync(session, Audit(request), cancellationToken: cancellationToken);
            testOnlyFault?.Invoke(PerformanceImprovementContract.Participant.AuditIntent);
            await context.Collection(Outbox).InsertOneAsync(session, OutboxDocument(request), cancellationToken: cancellationToken);
            testOnlyFault?.Invoke(PerformanceImprovementContract.Participant.Outbox);
            return true;
        }

        private async Task<bool> CreateOpportunityAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            if (request.ProcessModelVersionId == Guid.Empty) throw new InvalidRequestException("process_model_version_required");
            var published = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("TenantId", G(request.TenantId)),
                Builders<BsonDocument>.Filter.Eq("_id", G(request.ProcessModelVersionId)),
                Builders<BsonDocument>.Filter.Eq("LifecycleState", "Published"),
                Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
            if (!await context.Collection(ProcessModelVersions).Find(session, published).AnyAsync(cancellationToken))
                throw new NotFoundException();

            var entity = new ProcessImprovementOpportunity(
                request.AggregateId, request.TenantId, request.OccurredAtUtc, request.ProcessModelVersionId,
                request.Title ?? string.Empty, request.ProblemStatement ?? string.Empty,
                request.ExpectedOutcome, request.PriorityRank);
            await context.Collection(Opportunities).InsertOneAsync(session, OpportunityDocument(entity), cancellationToken: cancellationToken);
            return true;
        }

        private async Task<bool> UpdateOpportunityAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            var current = await RequireOpportunityAsync(session, request, "Open", cancellationToken);
            var title = ProcessModelingText.Required(request.Title ?? string.Empty, 200);
            var problem = ProcessModelingText.Required(request.ProblemStatement ?? string.Empty, 4000);
            var expected = ProcessModelingText.Optional(request.ExpectedOutcome, 2000);
            if (request.PriorityRank is <= 0) throw new InvalidRequestException("priority_rank_invalid");
            if (current["Title"].AsString == title && current["ProblemStatement"].AsString == problem &&
                NullableString(current, "ExpectedOutcome") == expected && NullableInt(current, "PriorityRank") == request.PriorityRank)
                return false;

            var values = new BsonDocument
            {
                ["Title"] = title,
                ["ProblemStatement"] = problem,
                ["ExpectedOutcome"] = expected is null ? BsonNull.Value : expected,
                ["PriorityRank"] = request.PriorityRank.HasValue ? request.PriorityRank.Value : BsonNull.Value,
                ["UpdatedAtUtc"] = request.OccurredAtUtc
            };
            await CasAsync(session, request, "Open", values, cancellationToken);
            return true;
        }

        private async Task<bool> QualifyOpportunityAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            var current = await RequireOpportunityAsync(session, request, "Open", cancellationToken);
            if (string.IsNullOrWhiteSpace(NullableString(current, "ExpectedOutcome"))) throw new InvalidRequestException("expected_outcome_required");
            if (NullableInt(current, "PriorityRank") is null or <= 0) throw new InvalidRequestException("priority_rank_required");
            await CasAsync(session, request, "Open", new BsonDocument { ["LifecycleState"] = "Qualified", ["UpdatedAtUtc"] = request.OccurredAtUtc }, cancellationToken);
            return true;
        }

        private async Task<bool> RecordOutcomeAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            await RequireOpportunityAsync(session, request, "Qualified", cancellationToken);
            if (!request.OutcomeId.HasValue || request.OutcomeId.Value == Guid.Empty) throw new InvalidRequestException("outcome_id_required");
            var outcome = new ProcessImprovementOutcome(
                request.OutcomeId.Value, request.TenantId, request.OccurredAtUtc, request.AggregateId,
                request.Summary ?? string.Empty, request.OccurredAtUtc);
            await CasAsync(session, request, "Qualified", new BsonDocument { ["UpdatedAtUtc"] = request.OccurredAtUtc }, cancellationToken);
            await context.Collection(Outcomes).InsertOneAsync(session, OutcomeDocument(outcome), cancellationToken: cancellationToken);
            return true;
        }

        private async Task<bool> CloseOpportunityAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            await RequireOpportunityAsync(session, request, "Qualified", cancellationToken);
            var outcome = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("TenantId", G(request.TenantId)),
                Builders<BsonDocument>.Filter.Eq("ProcessImprovementOpportunityId", G(request.AggregateId)),
                Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
            if (!await context.Collection(Outcomes).Find(session, outcome).AnyAsync(cancellationToken))
                throw new InvalidRequestException("outcome_required_before_close");
            await CasAsync(session, request, "Qualified", new BsonDocument { ["LifecycleState"] = "Closed", ["UpdatedAtUtc"] = request.OccurredAtUtc }, cancellationToken);
            return true;
        }

        private async Task<BsonDocument> RequireOpportunityAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, string lifecycle, CancellationToken cancellationToken)
        {
            var visible = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("TenantId", G(request.TenantId)),
                Builders<BsonDocument>.Filter.Eq("_id", G(request.AggregateId)),
                Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
            var current = await context.Collection(Opportunities).Find(session, visible).FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException();
            if (current["Version"].AsInt32 != request.ExpectedVersion || current["LifecycleState"].AsString != lifecycle)
                throw new ConflictException();
            return current;
        }

        private async Task CasAsync(IClientSessionHandle session, PerformanceImprovementContract.MutationRequest request, string lifecycle, BsonDocument values, CancellationToken cancellationToken)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("TenantId", G(request.TenantId)),
                Builders<BsonDocument>.Filter.Eq("_id", G(request.AggregateId)),
                Builders<BsonDocument>.Filter.Eq("Version", request.ExpectedVersion),
                Builders<BsonDocument>.Filter.Eq("LifecycleState", lifecycle),
                Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
            var update = new BsonDocument("$set", values).Add("$inc", new BsonDocument("Version", 1));
            if ((await context.Collection(Opportunities).UpdateOneAsync(session, filter, update, cancellationToken: cancellationToken)).ModifiedCount != 1)
                throw new ConflictException();
        }

        private void RequireSession(IClientSessionHandle session)
        {
            if (session is null || !session.IsInTransaction || !ReferenceEquals(session.Client, context.Client))
                throw new InvalidOperationException("performance_improvement_session_required");
        }

        private static BsonDocument OpportunityDocument(ProcessImprovementOpportunity x) => new()
        {
            ["_id"] = G(x.Id), ["TenantId"] = G(x.TenantId), ["ProcessModelVersionId"] = G(x.ProcessModelVersionId),
            ["Title"] = x.Title, ["ProblemStatement"] = x.ProblemStatement,
            ["ExpectedOutcome"] = x.ExpectedOutcome is null ? BsonNull.Value : x.ExpectedOutcome,
            ["PriorityRank"] = x.PriorityRank.HasValue ? x.PriorityRank.Value : BsonNull.Value,
            ["LifecycleState"] = x.LifecycleState.ToString(), ["CreatedAtUtc"] = x.CreatedAtUtc,
            ["UpdatedAtUtc"] = BsonNull.Value, ["IsDeleted"] = false, ["DeletedAtUtc"] = BsonNull.Value, ["Version"] = 0
        };

        private static BsonDocument OutcomeDocument(ProcessImprovementOutcome x) => new()
        {
            ["_id"] = G(x.Id), ["TenantId"] = G(x.TenantId),
            ["ProcessImprovementOpportunityId"] = G(x.ProcessImprovementOpportunityId), ["Summary"] = x.Summary,
            ["ObservedAtUtc"] = x.ObservedAtUtc, ["CreatedAtUtc"] = x.CreatedAtUtc,
            ["UpdatedAtUtc"] = BsonNull.Value, ["IsDeleted"] = false, ["DeletedAtUtc"] = BsonNull.Value, ["Version"] = 0
        };

        private static BsonDocument Receipt(PerformanceImprovementContract.MutationRequest x) => new()
        {
            ["TenantId"] = G(x.TenantId), ["SubjectId"] = G(x.ActorId), ["CommandFamily"] = CommandName(x.CommandFamily),
            ["IdempotencyKey"] = x.IdempotencyKey, ["PayloadHash"] = x.CanonicalPayloadHash, ["Outcome"] = "accepted",
            ["HttpStatus"] = 200, ["AggregateId"] = G(x.AggregateId), ["OutcomeEntityId"] = G(x.OutcomeId ?? x.AggregateId),
            ["CreatedAtUtc"] = x.OccurredAtUtc, ["CompletedAtUtc"] = x.OccurredAtUtc
        };

        private static BsonDocument Audit(PerformanceImprovementContract.MutationRequest x)
        {
            var pair = AuditPair(x.CommandFamily);
            return new BsonDocument
            {
                ["TenantId"] = G(x.TenantId), ["AuditIntentId"] = G(DeterministicId(x, "audit")),
                ["Payload"] = new BsonDocument
                {
                    ["auditIntentId"] = G(DeterministicId(x, "audit")), ["actorId"] = G(x.ActorId),
                    ["entityType"] = pair.Entity, ["entityId"] = G(x.OutcomeId ?? x.AggregateId),
                    ["mutation"] = pair.Mutation, ["occurredAtUtc"] = x.OccurredAtUtc
                },
                ["TrustedMetadata"] = new BsonDocument { ["moduleCode"] = PerformanceImprovementContract.ModuleCode, ["permission"] = x.Permission },
                ["CreatedAtUtc"] = x.OccurredAtUtc
            };
        }

        private static BsonDocument OutboxDocument(PerformanceImprovementContract.MutationRequest x)
        {
            var audit = Audit(x);
            return new BsonDocument
            {
                ["TenantId"] = G(x.TenantId), ["EventId"] = G(DeterministicId(x, "event")),
                ["AggregateId"] = G(x.OutcomeId ?? x.AggregateId), ["EventType"] = PerformanceImprovementContract.EventName,
                ["EventVersion"] = PerformanceImprovementContract.EventVersion, ["Payload"] = audit["Payload"],
                ["TrustedMetadata"] = audit["TrustedMetadata"], ["CreatedAtUtc"] = x.OccurredAtUtc
            };
        }

        private static (string Entity, string Mutation) AuditPair(PerformanceImprovementContract.Command command) => command switch
        {
            PerformanceImprovementContract.Command.CreateOpportunity => ("ProcessImprovementOpportunity", "Created"),
            PerformanceImprovementContract.Command.UpdateOpportunity => ("ProcessImprovementOpportunity", "Updated"),
            PerformanceImprovementContract.Command.QualifyOpportunity => ("ProcessImprovementOpportunity", "Qualified"),
            PerformanceImprovementContract.Command.CloseOpportunity => ("ProcessImprovementOpportunity", "Closed"),
            PerformanceImprovementContract.Command.RecordOutcome => ("ProcessImprovementOutcome", "Recorded"),
            _ => throw new InvalidRequestException("audit_pair_forbidden")
        };

        internal static string CommandName(PerformanceImprovementContract.Command command) => command switch
        {
            PerformanceImprovementContract.Command.CreateOpportunity => "CreateProcessImprovementOpportunityCommand",
            PerformanceImprovementContract.Command.UpdateOpportunity => "UpdateProcessImprovementOpportunityCommand",
            PerformanceImprovementContract.Command.QualifyOpportunity => "QualifyProcessImprovementOpportunityCommand",
            PerformanceImprovementContract.Command.CloseOpportunity => "CloseProcessImprovementOpportunityCommand",
            PerformanceImprovementContract.Command.RecordOutcome => "RecordProcessImprovementOutcomeCommand",
            _ => throw new InvalidRequestException("command_forbidden")
        };

        private static Guid DeterministicId(PerformanceImprovementContract.MutationRequest x, string family)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{x.TenantId:N}|{x.IdempotencyKey}|{family}"));
            return new Guid(bytes.AsSpan(0, 16));
        }

        private static string? NullableString(BsonDocument x, string name) => !x.TryGetValue(name, out var value) || value.IsBsonNull ? null : value.AsString;
        private static int? NullableInt(BsonDocument x, string name) => !x.TryGetValue(name, out var value) || value.IsBsonNull ? null : value.AsInt32;
    }

    public sealed class Store(Context context, Writer writer, TransactionRunner? runner = null) : PerformanceImprovementContract.IMutationStore
    {
        private readonly TransactionRunner _runner = runner ?? new TransactionRunner(context);

        public async Task<PerformanceImprovementContract.MutationResult> ExecuteAsync(PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var existing = await ReconcileAsync(request, cancellationToken);
                if (existing is not null) return existing;
                return await _runner.ExecuteAsync(
                    async (session, ct) =>
                    {
                        var effective = await writer.WriteAsync(session, request, ct);
                        return new PerformanceImprovementContract.MutationResult(true, 200, effective ? "accepted" : "no_op", request.OutcomeId ?? request.AggregateId);
                    },
                    ct => ReconcileAsync(request, ct),
                    cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (NotFoundException) { return PerformanceImprovementContract.MutationResult.NotFound(request.AggregateId); }
            catch (ConflictException) { return PerformanceImprovementContract.MutationResult.Conflict(request.AggregateId); }
            catch (InvalidRequestException error) { return PerformanceImprovementContract.MutationResult.Invalid(request.AggregateId, error.Message); }
            catch (MongoWriteException error) when (error.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return await ReconcileAsync(request, cancellationToken) ?? PerformanceImprovementContract.MutationResult.Conflict(request.AggregateId);
            }
            catch (Exception error) when (error is MongoException or TimeoutException or InvalidOperationException or NotSupportedException)
            {
                return PerformanceImprovementContract.MutationResult.Unavailable(request.AggregateId);
            }
        }

        private async Task<PerformanceImprovementContract.MutationResult?> ReconcileAsync(PerformanceImprovementContract.MutationRequest request, CancellationToken cancellationToken)
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("TenantId", G(request.TenantId)),
                Builders<BsonDocument>.Filter.Eq("CommandFamily", Writer.CommandName(request.CommandFamily)),
                Builders<BsonDocument>.Filter.Eq("IdempotencyKey", request.IdempotencyKey));
            var receipt = await context.Collection(Receipts).WithReadConcern(ReadConcern.Majority).Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (receipt is null) return null;
            if (ProcessModelingMongoGuidPolicy.Read(receipt["SubjectId"]) != request.ActorId || receipt["PayloadHash"].AsString != request.CanonicalPayloadHash)
                return PerformanceImprovementContract.MutationResult.Conflict(request.AggregateId, "performance_improvement_idempotency_conflict");
            return new(true, receipt["HttpStatus"].AsInt32, receipt["Outcome"].AsString, ProcessModelingMongoGuidPolicy.Read(receipt["OutcomeEntityId"]));
        }
    }

    public sealed class TransactionRunner(
        Context context,
        int maximumBodyAttempts = 3,
        int maximumCommitAttempts = 3,
        Func<IClientSessionHandle, int, CancellationToken, Task>? testOnlyCommit = null)
    {
        public async Task<PerformanceImprovementContract.MutationResult> ExecuteAsync(
            Func<IClientSessionHandle, CancellationToken, Task<PerformanceImprovementContract.MutationResult>> body,
            Func<CancellationToken, Task<PerformanceImprovementContract.MutationResult?>> reconcile,
            CancellationToken cancellationToken)
        {
            for (var bodyAttempt = 1; bodyAttempt <= maximumBodyAttempts; bodyAttempt++)
            {
                using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
                try
                {
                    session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
                    var result = await body(session, cancellationToken);
                    for (var commitAttempt = 1; commitAttempt <= maximumCommitAttempts; commitAttempt++)
                    {
                        try
                        {
                            if (testOnlyCommit is null) await session.CommitTransactionAsync(cancellationToken);
                            else await testOnlyCommit(session, commitAttempt, cancellationToken);
                            return result;
                        }
                        catch (UnknownCommitException) when (commitAttempt < maximumCommitAttempts) { }
                        catch (MongoException error) when (error.HasErrorLabel("UnknownTransactionCommitResult") && commitAttempt < maximumCommitAttempts) { }
                        catch (UnknownCommitException) { break; }
                        catch (MongoException error) when (error.HasErrorLabel("UnknownTransactionCommitResult")) { break; }
                    }

                    return await reconcile(cancellationToken) ?? PerformanceImprovementContract.MutationResult.Unavailable(result.AggregateId);
                }
                catch (OperationCanceledException)
                {
                    if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
                    throw;
                }
                catch (MongoException error) when (error.HasErrorLabel("TransientTransactionError") && bodyAttempt < maximumBodyAttempts)
                {
                    if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
                }
                catch
                {
                    if (session.IsInTransaction) await session.AbortTransactionAsync(CancellationToken.None);
                    throw;
                }
            }

            return PerformanceImprovementContract.MutationResult.Unavailable(Guid.Empty);
        }
    }

    public sealed class UnknownCommitException : Exception;
    private sealed class NotFoundException : Exception;
    private sealed class ConflictException : Exception;
    private sealed class InvalidRequestException(string code) : Exception(code);

    private static BsonBinaryData G(Guid value) => ProcessModelingMongoGuidPolicy.Write(value);
}
