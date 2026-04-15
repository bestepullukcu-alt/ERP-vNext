using Diten.Persistence.Context;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.Persistence.EnterpriseStrategy;

internal static class StrategicGoalMongoMigration
{
    private const string MigrationId = "strategic-goal-separated-model-v1";

    private const string StateCollectionName = "EnterpriseStrategyMigrationState";
    private const string ReportCollectionName = "EnterpriseStrategyMigrationReport";
    private const string BackupCollectionName = "StrategicGoalMigrationBackup";
    private const string ManualReviewCollectionName = "StrategicGoalMigrationManualReview";

    public static async Task EnsureAppliedAsync(
        MongoDbContext context,
        IConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var forceRun = ReadBoolFlag(configuration,
            "EnterpriseStrategy:Migrations:StrategicGoal:Force",
            "ESBP_STRATEGIC_GOAL_MIGRATION_FORCE");
        var dryRun = ReadBoolFlag(configuration,
            "EnterpriseStrategy:Migrations:StrategicGoal:DryRun",
            "ESBP_STRATEGIC_GOAL_MIGRATION_DRY_RUN");

        var stateCollection = context.GetCollection<StrategicGoalMigrationState>(StateCollectionName);
        var reportCollection = context.GetCollection<StrategicGoalMigrationRunReport>(ReportCollectionName);
        var backupCollection = context.GetCollection<StrategicGoalMigrationBackupRow>(BackupCollectionName);
        var manualReviewCollection = context.GetCollection<StrategicGoalMigrationManualReviewRow>(ManualReviewCollectionName);

        await EnsureMigrationCollectionsIndexedAsync(stateCollection, reportCollection, backupCollection, manualReviewCollection, cancellationToken);

        var existingState = await stateCollection.Find(x => x.MigrationId == MigrationId).FirstOrDefaultAsync(cancellationToken);
        if (existingState is not null && !forceRun && !dryRun)
            return;

        var runId = Guid.NewGuid().ToString("N");
        var startedAt = DateTime.UtcNow;

        var report = new StrategicGoalMigrationRunReport
        {
            RunId = runId,
            MigrationId = MigrationId,
            StartedAtUtc = startedAt,
            DryRun = dryRun,
            Forced = forceRun
        };

        var goalCollection = context.GetCollection<BsonDocument>("GoalAggregate");
        var metricCollection = context.GetCollection<BsonDocument>("StrategicGoalMetric");
        var yearlyCollection = context.GetCollection<BsonDocument>("StrategicGoalMetricYearlyTarget");
        var budgetCollection = context.GetCollection<BsonDocument>("StrategicGoalBudgetEnvelope");

        var goals = await goalCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var allMetrics = await metricCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var allYearly = await yearlyCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var allBudgets = await budgetCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);

        report.GoalsScanned = goals.Count;
        report.PreMigration = AnalyzeDataQuality(goals, allMetrics, allYearly, allBudgets);

        var metricsByGoalId = allMetrics
            .GroupBy(x => ResolveString(x, "GoalId", "goalId") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var yearlyByMetricId = allYearly
            .GroupBy(x => ResolveString(x, "GoalMetricId", "goalMetricId") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var budgetsByGoalId = allBudgets
            .GroupBy(x => ResolveString(x, "GoalId", "goalId") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var manualReviewRows = new List<StrategicGoalMigrationManualReviewRow>();

        foreach (var goal in goals)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var goalId = ResolveGoalId(goal);
            if (string.IsNullOrWhiteSpace(goalId))
            {
                report.GoalsSkipped++;
                manualReviewRows.Add(BuildManualReview(runId, "", "Goal", "Error", "Missing GoalId/Id/_id. Goal could not be migrated safely."));
                continue;
            }

            var goalReviews = new List<StrategicGoalMigrationManualReviewRow>();
            var goalMetricDocs = metricsByGoalId.TryGetValue(goalId, out var metricRows)
                ? metricRows
                : new List<BsonDocument>();
            var goalBudgetDocs = budgetsByGoalId.TryGetValue(goalId, out var budgetRows)
                ? budgetRows
                : new List<BsonDocument>();

            var migrationPlan = BuildGoalMigrationPlan(goal, goalId, goalMetricDocs, yearlyByMetricId, goalBudgetDocs, runId, goalReviews);

            manualReviewRows.AddRange(goalReviews);
            report.ManualReviewCount += goalReviews.Count;

            if (migrationPlan.SkipWrite)
            {
                report.GoalsSkipped++;
                continue;
            }

            if (dryRun)
            {
                if (migrationPlan.MasterChanged || migrationPlan.MetricsToUpsert.Count > 0 || migrationPlan.YearlyRowsToUpsert.Count > 0 || migrationPlan.BudgetsToUpsert.Count > 0 || migrationPlan.MetricIdsToDelete.Count > 0 || migrationPlan.YearlyIdsToDelete.Count > 0 || migrationPlan.BudgetIdsToDelete.Count > 0)
                    report.GoalsChanged++;
                report.MetricsUpserted += migrationPlan.MetricsToUpsert.Count;
                report.YearlyRowsUpserted += migrationPlan.YearlyRowsToUpsert.Count;
                report.BudgetsUpserted += migrationPlan.BudgetsToUpsert.Count;
                report.DuplicateMetricsRemoved += migrationPlan.MetricIdsToDelete.Count;
                report.DuplicateYearlyRowsRemoved += migrationPlan.YearlyIdsToDelete.Count;
                report.DuplicateBudgetRowsRemoved += migrationPlan.BudgetIdsToDelete.Count;
                continue;
            }

            if (migrationPlan.MasterChanged)
                await BackupDocumentAsync(backupCollection, runId, goalId, "GoalAggregate", goal, cancellationToken);

            foreach (var metricId in migrationPlan.MetricIdsToDelete)
            {
                var doc = goalMetricDocs.FirstOrDefault(x => BsonValueEquals(x.GetValue("_id", BsonNull.Value), metricId));
                if (doc is not null)
                    await BackupDocumentAsync(backupCollection, runId, goalId, "StrategicGoalMetric", doc, cancellationToken);
            }

            foreach (var yearlyId in migrationPlan.YearlyIdsToDelete)
            {
                var doc = allYearly.FirstOrDefault(x => BsonValueEquals(x.GetValue("_id", BsonNull.Value), yearlyId));
                if (doc is not null)
                    await BackupDocumentAsync(backupCollection, runId, goalId, "StrategicGoalMetricYearlyTarget", doc, cancellationToken);
            }

            foreach (var budgetId in migrationPlan.BudgetIdsToDelete)
            {
                var doc = goalBudgetDocs.FirstOrDefault(x => BsonValueEquals(x.GetValue("_id", BsonNull.Value), budgetId));
                if (doc is not null)
                    await BackupDocumentAsync(backupCollection, runId, goalId, "StrategicGoalBudgetEnvelope", doc, cancellationToken);
            }

            await goalCollection.ReplaceOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", goal.GetValue("_id", new BsonString(goalId))),
                migrationPlan.MasterDocument,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            if (migrationPlan.MetricIdsToDelete.Count > 0)
            {
                await metricCollection.DeleteManyAsync(
                    Builders<BsonDocument>.Filter.In("_id", migrationPlan.MetricIdsToDelete),
                    cancellationToken);
            }

            foreach (var metric in migrationPlan.MetricsToUpsert)
            {
                await metricCollection.ReplaceOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", metric.GetValue("_id")),
                    metric,
                    new ReplaceOptions { IsUpsert = true },
                    cancellationToken);
            }

            if (migrationPlan.YearlyIdsToDelete.Count > 0)
            {
                await yearlyCollection.DeleteManyAsync(
                    Builders<BsonDocument>.Filter.In("_id", migrationPlan.YearlyIdsToDelete),
                    cancellationToken);
            }

            foreach (var yearly in migrationPlan.YearlyRowsToUpsert)
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("GoalMetricId", yearly["GoalMetricId"]),
                    Builders<BsonDocument>.Filter.Eq("Year", yearly["Year"]));
                await yearlyCollection.ReplaceOneAsync(filter, yearly, new ReplaceOptions { IsUpsert = true }, cancellationToken);
            }

            if (migrationPlan.BudgetIdsToDelete.Count > 0)
            {
                await budgetCollection.DeleteManyAsync(
                    Builders<BsonDocument>.Filter.In("_id", migrationPlan.BudgetIdsToDelete),
                    cancellationToken);
            }

            foreach (var budget in migrationPlan.BudgetsToUpsert)
            {
                var filter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("GoalId", budget["GoalId"]),
                    Builders<BsonDocument>.Filter.Eq("Year", budget["Year"]));
                await budgetCollection.ReplaceOneAsync(filter, budget, new ReplaceOptions { IsUpsert = true }, cancellationToken);
            }

            if (migrationPlan.MasterChanged || migrationPlan.MetricsToUpsert.Count > 0 || migrationPlan.YearlyRowsToUpsert.Count > 0 || migrationPlan.BudgetsToUpsert.Count > 0 || migrationPlan.MetricIdsToDelete.Count > 0 || migrationPlan.YearlyIdsToDelete.Count > 0 || migrationPlan.BudgetIdsToDelete.Count > 0)
                report.GoalsChanged++;

            report.MetricsUpserted += migrationPlan.MetricsToUpsert.Count;
            report.YearlyRowsUpserted += migrationPlan.YearlyRowsToUpsert.Count;
            report.BudgetsUpserted += migrationPlan.BudgetsToUpsert.Count;
            report.DuplicateMetricsRemoved += migrationPlan.MetricIdsToDelete.Count;
            report.DuplicateYearlyRowsRemoved += migrationPlan.YearlyIdsToDelete.Count;
            report.DuplicateBudgetRowsRemoved += migrationPlan.BudgetIdsToDelete.Count;
        }

        if (manualReviewRows.Count > 0 && !dryRun)
            await manualReviewCollection.InsertManyAsync(manualReviewRows, cancellationToken: cancellationToken);

        var goalsAfter = await goalCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var metricsAfter = await metricCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var yearlyAfter = await yearlyCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);
        var budgetsAfter = await budgetCollection.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync(cancellationToken);

        report.PostMigration = AnalyzeDataQuality(goalsAfter, metricsAfter, yearlyAfter, budgetsAfter);
        report.CompletedAtUtc = DateTime.UtcNow;

        if (!dryRun)
        {
            await EnsureDuplicatePreventionIndexesAsync(metricCollection, yearlyCollection, budgetCollection, report, cancellationToken);

            await stateCollection.ReplaceOneAsync(
                x => x.MigrationId == MigrationId,
                new StrategicGoalMigrationState
                {
                    MigrationId = MigrationId,
                    LatestRunId = runId,
                    AppliedAtUtc = report.CompletedAtUtc ?? DateTime.UtcNow,
                    GoalsChanged = report.GoalsChanged,
                    ManualReviewCount = report.ManualReviewCount
                },
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        await reportCollection.InsertOneAsync(report, cancellationToken: cancellationToken);
    }

    private static GoalMigrationPlan BuildGoalMigrationPlan(
        BsonDocument goal,
        string goalId,
        List<BsonDocument> existingMetricDocs,
        Dictionary<string, List<BsonDocument>> yearlyByMetricId,
        List<BsonDocument> existingBudgetDocs,
        string runId,
        List<StrategicGoalMigrationManualReviewRow> manualReviews)
    {
        var masterClone = goal.DeepClone().AsBsonDocument;
        var masterChanged = false;

        var ownerRoleCandidates = new[]
        {
            ResolveString(goal, "OwnerRole"),
            ResolveString(goal, "OwnerId"),
            ResolveString(goal, "Owner")
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ownerRoleCandidates.Count > 1)
        {
            manualReviews.Add(BuildManualReview(runId, goalId, "Ownership", "Warning", "Conflicting owner role aliases detected; canonical OwnerRole selected from precedence OwnerRole > OwnerId > Owner."));
        }

        var ownerRole = FirstNonEmpty(
            ResolveString(goal, "OwnerRole"),
            ResolveString(goal, "OwnerId"),
            ResolveString(goal, "Owner"));

        var applicableCompanies = ResolveStringList(goal, "ApplicableCompanyIds");
        var ownerCompany = FirstNonEmpty(
            ResolveString(goal, "OwnerCompanyId"),
            ResolveString(goal, "PrimaryCompanyId"),
            applicableCompanies.FirstOrDefault());

        if (string.IsNullOrWhiteSpace(ownerCompany) && applicableCompanies.Count > 1)
        {
            manualReviews.Add(BuildManualReview(runId, goalId, "Ownership", "Warning", "OwnerCompanyId is ambiguous (multiple applicable companies and no owner company). Manual mapping required."));
        }

        var ownerPerson = FirstNonEmpty(
            ResolveString(goal, "OwnerPersonId"),
            ResolveString(goal, "OwnerDisplayName"));

        var startDate = ResolveDate(goal, "StartDate", "PlanningHorizonStart")
            ?? ResolveYearAsDate(goal, "StartYear", preferStart: true);
        var endDate = ResolveDate(goal, "EndDate", "PlanningHorizonEnd")
            ?? ResolveYearAsDate(goal, "EndYear", preferStart: false);

        var applicabilityMode = FirstNonEmpty(
            ResolveString(goal, "ApplicabilityMode"),
            ResolveString(goal, "ScopeMode"),
            ResolveString(goal, "ScopeModeCode"));

        var appliesToAll = ResolveBool(goal, "AppliesToAllCompanies", "AppliesToAllCompaniesFlag")
            ?? false;

        if (ResolveBool(goal, "AppliesToSelectedCompaniesFlag") == true && applicableCompanies.Count == 0)
        {
            manualReviews.Add(BuildManualReview(runId, goalId, "Applicability", "Warning", "AppliesToSelectedCompaniesFlag=true but ApplicableCompanyIds is empty."));
        }

        if (string.IsNullOrWhiteSpace(applicabilityMode))
        {
            applicabilityMode = appliesToAll
                ? "AllCompanies"
                : applicableCompanies.Count > 1
                    ? "MultiCompany"
                    : "SingleCompany";
            masterChanged = true;
        }

        if (string.IsNullOrWhiteSpace(ownerRole))
        {
            manualReviews.Add(BuildManualReview(runId, goalId, "Ownership", "Warning", "OwnerRole is empty after alias resolution."));
        }

        var normalizedGoalId = goalId;
        masterChanged |= UpsertCanonicalString(masterClone, "GoalId", normalizedGoalId);
        masterChanged |= UpsertCanonicalString(masterClone, "Id", normalizedGoalId);
        masterChanged |= UpsertCanonicalString(masterClone, "GoalTitle", FirstNonEmpty(ResolveString(goal, "GoalTitle"), ResolveString(goal, "Name"), ResolveString(goal, "Goal")));
        masterChanged |= UpsertCanonicalString(masterClone, "Name", FirstNonEmpty(ResolveString(goal, "GoalTitle"), ResolveString(goal, "Name"), ResolveString(goal, "Goal")));
        masterChanged |= UpsertCanonicalString(masterClone, "Category", FirstNonEmpty(ResolveString(goal, "Category"), ResolveString(goal, "CategoryCode")));
        masterChanged |= UpsertCanonicalString(masterClone, "GoalStatement", FirstNonEmpty(ResolveString(goal, "GoalStatement"), ResolveString(goal, "Statement")));
        masterChanged |= UpsertCanonicalString(masterClone, "Statement", FirstNonEmpty(ResolveString(goal, "GoalStatement"), ResolveString(goal, "Statement")));
        masterChanged |= UpsertCanonicalString(masterClone, "Status", FirstNonEmpty(ResolveString(goal, "Status"), ResolveString(goal, "StatusCode"), "Draft"));
        masterChanged |= UpsertCanonicalString(masterClone, "Priority", FirstNonEmpty(ResolveString(goal, "Priority"), ResolveString(goal, "PriorityCode"), "Medium"));
        masterChanged |= UpsertCanonicalString(masterClone, "StrategyPeriodId", ResolveString(goal, "StrategyPeriodId"));
        masterChanged |= UpsertCanonicalDate(masterClone, "StartDate", startDate);
        masterChanged |= UpsertCanonicalDate(masterClone, "EndDate", endDate);
        masterChanged |= UpsertCanonicalDate(masterClone, "PlanningHorizonStart", startDate);
        masterChanged |= UpsertCanonicalDate(masterClone, "PlanningHorizonEnd", endDate);
        masterChanged |= UpsertCanonicalString(masterClone, "OwnerRole", ownerRole);
        masterChanged |= UpsertCanonicalString(masterClone, "OwnerId", ownerRole);
        masterChanged |= UpsertCanonicalString(masterClone, "Owner", ownerRole);
        masterChanged |= UpsertCanonicalString(masterClone, "OwnerCompanyId", ownerCompany);
        masterChanged |= UpsertCanonicalString(masterClone, "PrimaryCompanyId", ownerCompany);
        masterChanged |= UpsertCanonicalString(masterClone, "OwnerPersonId", ownerPerson);
        masterChanged |= UpsertCanonicalString(masterClone, "OwnerDisplayName", ownerPerson);
        masterChanged |= UpsertCanonicalString(masterClone, "RelatedEntityScope", FirstNonEmpty(ResolveString(goal, "RelatedEntityScope"), ResolveString(goal, "EntityScope")));
        masterChanged |= UpsertCanonicalString(masterClone, "EntityScope", FirstNonEmpty(ResolveString(goal, "RelatedEntityScope"), ResolveString(goal, "EntityScope")));
        masterChanged |= UpsertCanonicalString(masterClone, "ApplicabilityMode", applicabilityMode);
        masterChanged |= UpsertCanonicalString(masterClone, "ScopeMode", applicabilityMode);
        masterChanged |= UpsertCanonicalString(masterClone, "ScopeModeCode", applicabilityMode);
        masterChanged |= UpsertCanonicalBool(masterClone, "AppliesToAllCompanies", appliesToAll);
        masterChanged |= UpsertCanonicalBool(masterClone, "AppliesToAllCompaniesFlag", appliesToAll);
        masterChanged |= UpsertCanonicalBool(masterClone, "AppliesToSelectedCompaniesFlag", !appliesToAll && applicableCompanies.Count > 0);
        masterChanged |= UpsertCanonicalArray(masterClone, "ApplicableCompanyIds", applicableCompanies);
        masterChanged |= UpsertCanonicalString(masterClone, "ChangeLogRef", ResolveString(goal, "ChangeLogRef"));
        masterChanged |= UpsertCanonicalString(masterClone, "DecisionReference", ResolveString(goal, "DecisionReference"));
        masterChanged |= UpsertCanonicalString(masterClone, "EvidenceLink", FirstNonEmpty(ResolveString(goal, "EvidenceLink"), ResolveString(goal, "EvidenceReference")));
        masterChanged |= UpsertCanonicalString(masterClone, "EvidenceReference", FirstNonEmpty(ResolveString(goal, "EvidenceLink"), ResolveString(goal, "EvidenceReference")));
        masterChanged |= UpsertCanonicalInt(masterClone, "Version", ResolveInt(goal, "Version") ?? 1);

        if (masterClone.Contains("Metrics") && masterClone["Metrics"].IsBsonArray)
        {
            masterClone["Metrics"] = new BsonArray();
            masterChanged = true;
        }

        if (masterClone.Contains("YearlyBudgets") && masterClone["YearlyBudgets"].IsBsonArray)
        {
            masterClone["YearlyBudgets"] = new BsonArray();
            masterChanged = true;
        }

        var legacyMixedFields = new[]
        {
            "MetricName", "MetricDefinitionId", "MetricDefId", "MetricType", "UnitOfMeasure", "AggregationMethod",
            "DirectionPolarity", "PolarityCode", "ThresholdModel", "ThresholdModelCode", "ReportingFrequency", "ReportingFrequencyCode",
            "BaselineValue", "TargetValue", "CascadeMetric", "MetricOrigin", "MetricRole", "RestrictionMode", "RollupEligible",
            "YearlyValues", "YearlyTargets", "RevenueTarget", "EbitdaTarget", "CapexEnvelope", "OpexEnvelope", "SavingsTarget", "FundingPool", "FundingPoolEnvelope"
        };

        foreach (var field in legacyMixedFields)
        {
            if (masterClone.Contains(field))
            {
                masterClone.Remove(field);
                masterChanged = true;
            }
        }

        var metricCandidates = new List<CanonicalMetricCandidate>();

        metricCandidates.AddRange(existingMetricDocs.Select(x => ToCanonicalMetric(x, goalId, "metric-collection", manualReviews, runId)));
        metricCandidates.AddRange(ExtractEmbeddedMetricCandidates(goal, goalId, manualReviews, runId));

        var topLevelMetric = ExtractTopLevelMetricCandidate(goal, goalId, manualReviews, runId);
        if (topLevelMetric is not null)
            metricCandidates.Add(topLevelMetric);

        var groupedMetrics = metricCandidates
            .Where(x => !string.IsNullOrWhiteSpace(x.MetricName) || !string.IsNullOrWhiteSpace(x.MetricDefinitionId) || x.YearlyTargets.Count > 0)
            .GroupBy(x => x.SemanticKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var metricsToUpsert = new List<BsonDocument>();
        var metricIdsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var metricIdsToDelete = new HashSet<BsonValue>(new BsonValueComparer());
        var yearlyRowsToUpsert = new List<BsonDocument>();
        var yearlyIdsToDelete = new HashSet<BsonValue>(new BsonValueComparer());

        foreach (var group in groupedMetrics)
        {
            var winner = group.OrderByDescending(ScoreMetricCandidate).First();
            var winnerId = string.IsNullOrWhiteSpace(winner.MetricId) ? Guid.NewGuid().ToString("N") : winner.MetricId;

            metricIdsToKeep.Add(winnerId);

            var mergedMetric = winner with
            {
                MetricId = winnerId,
                GoalId = goalId,
                YearlyTargets = MergeYearlyTargets(group.SelectMany(x => x.YearlyTargets).ToList(), goalId, winnerId, manualReviews, runId)
            };

            metricsToUpsert.Add(BuildMetricDocument(mergedMetric));

            foreach (var duplicate in group.Where(x => !string.IsNullOrWhiteSpace(x.MetricId) && !string.Equals(x.MetricId, winnerId, StringComparison.OrdinalIgnoreCase)))
            {
                if (duplicate.SourceDocument is null) continue;
                metricIdsToDelete.Add(duplicate.SourceDocument.GetValue("_id", BsonNull.Value));
            }

            var existingYearlyForGroupMetricIds = group
                .Where(x => !string.IsNullOrWhiteSpace(x.MetricId))
                .SelectMany(x => yearlyByMetricId.TryGetValue(x.MetricId!, out var docs) ? docs : Enumerable.Empty<BsonDocument>())
                .ToList();

            var existingYearlyByYear = existingYearlyForGroupMetricIds
                .Select(ToCanonicalYearlyTarget)
                .Where(x => x is not null)
                .Cast<CanonicalYearlyTarget>()
                .GroupBy(x => x.Year)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(ScoreYearlyTarget).First());

            foreach (var yearly in mergedMetric.YearlyTargets)
            {
                if (existingYearlyByYear.TryGetValue(yearly.Year, out var existing))
                {
                    yearlyRowsToUpsert.Add(BuildYearlyDocument(existing.SourceId, winnerId, yearly));
                    existingYearlyByYear.Remove(yearly.Year);
                }
                else
                {
                    yearlyRowsToUpsert.Add(BuildYearlyDocument(BsonNull.Value, winnerId, yearly));
                }
            }

            foreach (var remaining in existingYearlyByYear.Values)
            {
                if (!remaining.SourceId.IsBsonNull)
                    yearlyIdsToDelete.Add(remaining.SourceId);
            }

            foreach (var duplicateYearDoc in existingYearlyForGroupMetricIds)
            {
                var duplicateId = duplicateYearDoc.GetValue("_id", BsonNull.Value);
                if (duplicateId.IsBsonNull) continue;
                var metricRef = ResolveString(duplicateYearDoc, "GoalMetricId", "goalMetricId") ?? string.Empty;
                var year = ResolveInt(duplicateYearDoc, "Year", "year");
                if (!year.HasValue) continue;
                var winnerExists = mergedMetric.YearlyTargets.Any(x => x.Year == year.Value);
                if (winnerExists && !string.Equals(metricRef, winnerId, StringComparison.OrdinalIgnoreCase))
                    yearlyIdsToDelete.Add(duplicateId);
            }
        }

        foreach (var staleMetric in existingMetricDocs)
        {
            var metricId = ResolveString(staleMetric, "Id") ?? ResolveBsonValueAsString(staleMetric.GetValue("_id", BsonNull.Value));
            if (string.IsNullOrWhiteSpace(metricId)) continue;
            if (metricIdsToKeep.Contains(metricId)) continue;

            metricIdsToDelete.Add(staleMetric.GetValue("_id", BsonNull.Value));
            if (yearlyByMetricId.TryGetValue(metricId, out var staleYears))
            {
                foreach (var yearDoc in staleYears)
                {
                    var id = yearDoc.GetValue("_id", BsonNull.Value);
                    if (!id.IsBsonNull)
                        yearlyIdsToDelete.Add(id);
                }
            }
        }

        var budgetCandidates = new List<CanonicalBudgetEnvelope>();
        budgetCandidates.AddRange(existingBudgetDocs.Select(ToCanonicalBudgetEnvelope).Where(x => x is not null).Cast<CanonicalBudgetEnvelope>());
        budgetCandidates.AddRange(ExtractEmbeddedBudgetCandidates(goal));
        var topLevelBudget = ExtractTopLevelBudget(goal, startDate);
        if (topLevelBudget is not null)
            budgetCandidates.Add(topLevelBudget);

        var budgetRowsByYear = budgetCandidates
            .Where(x => x.Year > 0)
            .GroupBy(x => x.Year)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(ScoreBudgetEnvelope).First());

        var budgetsToUpsert = new List<BsonDocument>();
        var budgetIdsToDelete = new HashSet<BsonValue>(new BsonValueComparer());
        var existingBudgetByYear = existingBudgetDocs
            .Select(ToCanonicalBudgetEnvelope)
            .Where(x => x is not null)
            .Cast<CanonicalBudgetEnvelope>()
            .Where(x => x.Year > 0)
            .GroupBy(x => x.Year)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(ScoreBudgetEnvelope).First());

        foreach (var kv in budgetRowsByYear)
        {
            var year = kv.Key;
            var row = kv.Value with { GoalId = goalId };
            if (existingBudgetByYear.TryGetValue(year, out var existing))
            {
                budgetsToUpsert.Add(BuildBudgetDocument(existing.SourceId, row));
                existingBudgetByYear.Remove(year);
            }
            else
            {
                budgetsToUpsert.Add(BuildBudgetDocument(BsonNull.Value, row));
            }
        }

        foreach (var stale in existingBudgetByYear.Values)
        {
            if (!stale.SourceId.IsBsonNull)
                budgetIdsToDelete.Add(stale.SourceId);
        }

        foreach (var existingBudget in existingBudgetDocs)
        {
            var existingId = existingBudget.GetValue("_id", BsonNull.Value);
            if (existingId.IsBsonNull) continue;
            var year = ResolveInt(existingBudget, "Year", "year");
            if (!year.HasValue) continue;
            var winning = budgetRowsByYear.TryGetValue(year.Value, out var winner) ? winner : null;
            if (winning is not null && !BsonValueEquals(existingId, winning.SourceId))
                budgetIdsToDelete.Add(existingId);
        }

        return new GoalMigrationPlan(
            MasterDocument: masterClone,
            MasterChanged: masterChanged,
            MetricsToUpsert: metricsToUpsert,
            MetricIdsToDelete: metricIdsToDelete.Where(x => !x.IsBsonNull).ToList(),
            YearlyRowsToUpsert: yearlyRowsToUpsert,
            YearlyIdsToDelete: yearlyIdsToDelete.Where(x => !x.IsBsonNull).ToList(),
            BudgetsToUpsert: budgetsToUpsert,
            BudgetIdsToDelete: budgetIdsToDelete.Where(x => !x.IsBsonNull).ToList(),
            SkipWrite: false);
    }

    private static StrategicGoalMigrationDataQualitySnapshot AnalyzeDataQuality(
        List<BsonDocument> goals,
        List<BsonDocument> metrics,
        List<BsonDocument> yearly,
        List<BsonDocument> budgets)
    {
        var goalIds = goals
            .Select(ResolveGoalId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var metricIds = metrics
            .Select(x => ResolveString(x, "Id") ?? ResolveBsonValueAsString(x.GetValue("_id", BsonNull.Value)) ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphanMetricRows = metrics.Count(x =>
        {
            var goalId = ResolveString(x, "GoalId", "goalId");
            return string.IsNullOrWhiteSpace(goalId) || !goalIds.Contains(goalId);
        });

        var orphanYearlyRows = yearly.Count(x =>
        {
            var metricId = ResolveString(x, "GoalMetricId", "goalMetricId");
            return string.IsNullOrWhiteSpace(metricId) || !metricIds.Contains(metricId);
        });

        var orphanBudgetRows = budgets.Count(x =>
        {
            var goalId = ResolveString(x, "GoalId", "goalId");
            return string.IsNullOrWhiteSpace(goalId) || !goalIds.Contains(goalId);
        });

        var duplicateMetricRows = metrics
            .Select(x => ToCanonicalMetric(x, ResolveString(x, "GoalId", "goalId") ?? string.Empty, "quality-check", new List<StrategicGoalMigrationManualReviewRow>(), string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x.GoalId))
            .GroupBy(x => $"{x.GoalId}::{x.SemanticKey}", StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Sum(g => g.Count() - 1);

        var duplicateYearlyRows = yearly
            .GroupBy(x =>
            {
                var metricId = ResolveString(x, "GoalMetricId", "goalMetricId") ?? string.Empty;
                var year = ResolveInt(x, "Year", "year") ?? -1;
                return $"{metricId}::{year}";
            }, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Sum(g => g.Count() - 1);

        var duplicateBudgetRows = budgets
            .GroupBy(x =>
            {
                var goalId = ResolveString(x, "GoalId", "goalId") ?? string.Empty;
                var year = ResolveInt(x, "Year", "year") ?? -1;
                return $"{goalId}::{year}";
            }, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Sum(g => g.Count() - 1);

        var missingOwnerRoleCount = goals.Count(x => string.IsNullOrWhiteSpace(FirstNonEmpty(ResolveString(x, "OwnerRole"), ResolveString(x, "OwnerId"), ResolveString(x, "Owner"))));
        var missingOwnerCompanyCount = goals.Count(x =>
        {
            var applicable = ResolveStringList(x, "ApplicableCompanyIds");
            return string.IsNullOrWhiteSpace(FirstNonEmpty(ResolveString(x, "OwnerCompanyId"), ResolveString(x, "PrimaryCompanyId"), applicable.FirstOrDefault()));
        });

        return new StrategicGoalMigrationDataQualitySnapshot
        {
            GoalCount = goals.Count,
            MetricCount = metrics.Count,
            YearlyTargetCount = yearly.Count,
            BudgetCount = budgets.Count,
            OrphanMetricCount = orphanMetricRows,
            OrphanYearlyTargetCount = orphanYearlyRows,
            OrphanBudgetCount = orphanBudgetRows,
            DuplicateMetricCount = duplicateMetricRows,
            DuplicateYearlyTargetCount = duplicateYearlyRows,
            DuplicateBudgetCount = duplicateBudgetRows,
            MissingOwnerRoleCount = missingOwnerRoleCount,
            MissingOwnerCompanyCount = missingOwnerCompanyCount
        };
    }

    private static async Task EnsureMigrationCollectionsIndexedAsync(
        IMongoCollection<StrategicGoalMigrationState> stateCollection,
        IMongoCollection<StrategicGoalMigrationRunReport> reportCollection,
        IMongoCollection<StrategicGoalMigrationBackupRow> backupCollection,
        IMongoCollection<StrategicGoalMigrationManualReviewRow> manualReviewCollection,
        CancellationToken cancellationToken)
    {
        await reportCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<StrategicGoalMigrationRunReport>(
                Builders<StrategicGoalMigrationRunReport>.IndexKeys.Descending(x => x.StartedAtUtc),
                new CreateIndexOptions { Name = "ix_es_migration_report_started" }),
            cancellationToken: cancellationToken);

        await backupCollection.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<StrategicGoalMigrationBackupRow>(
                    Builders<StrategicGoalMigrationBackupRow>.IndexKeys.Ascending(x => x.RunId).Ascending(x => x.GoalId),
                    new CreateIndexOptions { Name = "ix_es_goal_mig_backup_run_goal" }),
                new CreateIndexModel<StrategicGoalMigrationBackupRow>(
                    Builders<StrategicGoalMigrationBackupRow>.IndexKeys.Ascending(x => x.Collection),
                    new CreateIndexOptions { Name = "ix_es_goal_mig_backup_collection" })
            },
            cancellationToken);

        await manualReviewCollection.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<StrategicGoalMigrationManualReviewRow>(
                    Builders<StrategicGoalMigrationManualReviewRow>.IndexKeys.Ascending(x => x.RunId).Ascending(x => x.GoalId),
                    new CreateIndexOptions { Name = "ix_es_goal_mig_review_run_goal" }),
                new CreateIndexModel<StrategicGoalMigrationManualReviewRow>(
                    Builders<StrategicGoalMigrationManualReviewRow>.IndexKeys.Ascending(x => x.Severity),
                    new CreateIndexOptions { Name = "ix_es_goal_mig_review_severity" })
            },
            cancellationToken);
    }

    private static async Task EnsureDuplicatePreventionIndexesAsync(
        IMongoCollection<BsonDocument> metricCollection,
        IMongoCollection<BsonDocument> yearlyCollection,
        IMongoCollection<BsonDocument> budgetCollection,
        StrategicGoalMigrationRunReport report,
        CancellationToken cancellationToken)
    {
        await metricCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("GoalId"),
                new CreateIndexOptions { Name = "ix_strategic_goal_metric_goal_id" }),
            cancellationToken: cancellationToken);

        try
        {
            await yearlyCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("GoalMetricId").Ascending("Year"),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Name = "ux_strategic_goal_metric_yearly_target",
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Exists("GoalMetricId", true),
                            Builders<BsonDocument>.Filter.Exists("Year", true))
                    }),
                cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex)
        {
            report.Notes.Add($"Could not create unique yearly-target index: {ex.Message}");
        }

        try
        {
            await budgetCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("GoalId").Ascending("Year"),
                    new CreateIndexOptions<BsonDocument>
                    {
                        Name = "ux_strategic_goal_budget_envelope",
                        Unique = true,
                        PartialFilterExpression = Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Exists("GoalId", true),
                            Builders<BsonDocument>.Filter.Exists("Year", true))
                    }),
                cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex)
        {
            report.Notes.Add($"Could not create unique budget index: {ex.Message}");
        }
    }

    private static async Task BackupDocumentAsync(
        IMongoCollection<StrategicGoalMigrationBackupRow> backupCollection,
        string runId,
        string goalId,
        string collection,
        BsonDocument source,
        CancellationToken cancellationToken)
    {
        await backupCollection.InsertOneAsync(
            new StrategicGoalMigrationBackupRow
            {
                Id = Guid.NewGuid().ToString("N"),
                RunId = runId,
                GoalId = goalId,
                Collection = collection,
                CapturedAtUtc = DateTime.UtcNow,
                Document = source.DeepClone().AsBsonDocument
            },
            cancellationToken: cancellationToken);
    }

    private static List<CanonicalMetricCandidate> ExtractEmbeddedMetricCandidates(
        BsonDocument goal,
        string goalId,
        List<StrategicGoalMigrationManualReviewRow> manualReviews,
        string runId)
    {
        var candidates = new List<CanonicalMetricCandidate>();
        foreach (var field in new[] { "Metrics", "GoalMetrics", "goalMetrics", "StrategicGoalMetrics" })
        {
            if (!goal.TryGetValue(field, out var value) || !value.IsBsonArray)
                continue;

            foreach (var item in value.AsBsonArray)
            {
                if (!item.IsBsonDocument) continue;
                candidates.Add(ToCanonicalMetric(item.AsBsonDocument, goalId, $"goal-embedded:{field}", manualReviews, runId));
            }
        }

        return candidates;
    }

    private static CanonicalMetricCandidate? ExtractTopLevelMetricCandidate(
        BsonDocument goal,
        string goalId,
        List<StrategicGoalMigrationManualReviewRow> manualReviews,
        string runId)
    {
        var hasTopLevelMetric =
            !string.IsNullOrWhiteSpace(ResolveString(goal, "MetricName")) ||
            !string.IsNullOrWhiteSpace(ResolveString(goal, "MetricDefinitionId", "MetricDefId")) ||
            !string.IsNullOrWhiteSpace(ResolveString(goal, "MetricType")) ||
            !string.IsNullOrWhiteSpace(ResolveString(goal, "UnitOfMeasure")) ||
            !string.IsNullOrWhiteSpace(ResolveString(goal, "AggregationMethod")) ||
            goal.Contains("YearlyValues") ||
            goal.Contains("YearlyTargets");

        if (!hasTopLevelMetric)
            return null;

        manualReviews.Add(BuildManualReview(runId, goalId, "KPI", "Warning", "Top-level KPI fields detected on Goal master; migrated into metric child structure."));

        var metricDoc = new BsonDocument
        {
            { "GoalId", goalId },
            { "MetricDefinitionId", ResolveString(goal, "MetricDefinitionId", "MetricDefId") ?? string.Empty },
            { "MetricName", ResolveString(goal, "MetricName") ?? string.Empty },
            { "MetricType", ResolveString(goal, "MetricType") ?? string.Empty },
            { "UnitOfMeasure", ResolveString(goal, "UnitOfMeasure") ?? string.Empty },
            { "AggregationMethod", ResolveString(goal, "AggregationMethod") ?? string.Empty },
            { "DirectionPolarity", FirstNonEmpty(ResolveString(goal, "DirectionPolarity"), ResolveString(goal, "PolarityCode")) ?? string.Empty },
            { "ThresholdModel", FirstNonEmpty(ResolveString(goal, "ThresholdModel"), ResolveString(goal, "ThresholdModelCode")) ?? string.Empty },
            { "ReportingFrequency", FirstNonEmpty(ResolveString(goal, "ReportingFrequency"), ResolveString(goal, "ReportingFrequencyCode")) ?? string.Empty },
            { "BaselineValue", ResolveDecimal(goal, "BaselineValue") ?? 0m },
            { "TargetValue", ResolveDecimal(goal, "TargetValue") ?? 0m },
            { "CascadeMetric", ResolveBool(goal, "CascadeMetric") ?? true },
            { "MetricOrigin", ResolveString(goal, "MetricOrigin") ?? "Local" },
            { "MetricRole", ResolveString(goal, "MetricRole") ?? "Strategic" },
            { "RestrictionMode", ResolveString(goal, "RestrictionMode") ?? "GoalGovernedStructure" },
            { "RollupEligible", ResolveBool(goal, "RollupEligible") ?? true },
            { "YearlyTargets", ResolveYearlyArray(goal, "YearlyValues", "YearlyTargets") }
        };

        return ToCanonicalMetric(metricDoc, goalId, "goal-top-level", manualReviews, runId);
    }

    private static List<CanonicalBudgetEnvelope> ExtractEmbeddedBudgetCandidates(BsonDocument goal)
    {
        var list = new List<CanonicalBudgetEnvelope>();
        foreach (var field in new[] { "YearlyBudgets", "BudgetEnvelopes", "goalYearlyBudgets", "yearlyBudgets" })
        {
            if (!goal.TryGetValue(field, out var value) || !value.IsBsonArray)
                continue;

            foreach (var item in value.AsBsonArray)
            {
                if (!item.IsBsonDocument) continue;
                var mapped = ToCanonicalBudgetEnvelope(item.AsBsonDocument);
                if (mapped is not null)
                    list.Add(mapped);
            }
        }

        return list;
    }

    private static CanonicalBudgetEnvelope? ExtractTopLevelBudget(BsonDocument goal, DateTime? startDate)
    {
        var hasBudgetFields =
            goal.Contains("RevenueTarget") ||
            goal.Contains("EbitdaTarget") ||
            goal.Contains("CapexEnvelope") ||
            goal.Contains("OpexEnvelope") ||
            goal.Contains("SavingsTarget") ||
            goal.Contains("FundingPool") ||
            goal.Contains("FundingPoolEnvelope");

        if (!hasBudgetFields)
            return null;

        var year = ResolveInt(goal, "Year") ?? startDate?.Year ?? 0;
        if (year <= 0) return null;

        return new CanonicalBudgetEnvelope
        {
            Source = "goal-top-level",
            SourceId = BsonNull.Value,
            GoalId = string.Empty,
            Year = year,
            RevenueTarget = ResolveDecimal(goal, "RevenueTarget"),
            EbitdaTarget = ResolveDecimal(goal, "EbitdaTarget"),
            CapexEnvelope = ResolveDecimal(goal, "CapexEnvelope"),
            OpexEnvelope = ResolveDecimal(goal, "OpexEnvelope"),
            SavingsTarget = ResolveDecimal(goal, "SavingsTarget"),
            FundingPool = FirstNonNullDecimal(ResolveDecimal(goal, "FundingPool"), ResolveDecimal(goal, "FundingPoolEnvelope")),
            Commentary = ResolveString(goal, "Commentary")
        };
    }

    private static CanonicalMetricCandidate ToCanonicalMetric(
        BsonDocument source,
        string goalId,
        string sourceName,
        List<StrategicGoalMigrationManualReviewRow> manualReviews,
        string runId)
    {
        var direction = FirstNonEmpty(ResolveString(source, "DirectionPolarity"), ResolveString(source, "PolarityCode")) ?? string.Empty;
        var threshold = FirstNonEmpty(ResolveString(source, "ThresholdModel"), ResolveString(source, "ThresholdModelCode")) ?? string.Empty;
        var frequency = FirstNonEmpty(ResolveString(source, "ReportingFrequency"), ResolveString(source, "ReportingFrequencyCode")) ?? string.Empty;
        var metricDefinitionId = FirstNonEmpty(ResolveString(source, "MetricDefinitionId"), ResolveString(source, "MetricDefId")) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(ResolveString(source, "MetricDefinitionId")) &&
            !string.IsNullOrWhiteSpace(ResolveString(source, "MetricDefId")) &&
            !string.Equals(ResolveString(source, "MetricDefinitionId"), ResolveString(source, "MetricDefId"), StringComparison.OrdinalIgnoreCase))
        {
            manualReviews.Add(BuildManualReview(runId, goalId, "KPI", "Warning", "MetricDefinitionId and MetricDefId conflict; canonical MetricDefinitionId retained."));
        }

        var metricId = ResolveString(source, "Id") ?? ResolveBsonValueAsString(source.GetValue("_id", BsonNull.Value));

        var yearlyTargets = ResolveYearlyArray(source, "YearlyTargets", "YearlyValues", "yearlyTargets", "yearlyValues")
            .Where(x => x.IsBsonDocument)
            .Select(x => ToCanonicalYearlyTarget(x.AsBsonDocument))
            .Where(x => x is not null)
            .Cast<CanonicalYearlyTarget>()
            .ToList();

        return new CanonicalMetricCandidate
        {
            Source = sourceName,
            SourceDocument = source,
            MetricId = metricId,
            GoalId = FirstNonEmpty(ResolveString(source, "GoalId", "goalId"), goalId) ?? goalId,
            MetricDefinitionId = metricDefinitionId,
            MetricName = ResolveString(source, "MetricName") ?? string.Empty,
            MetricType = FirstNonEmpty(ResolveString(source, "MetricType"), ResolveString(source, "MetricTypeCode")) ?? string.Empty,
            UnitOfMeasure = FirstNonEmpty(ResolveString(source, "UnitOfMeasure"), ResolveString(source, "UnitOfMeasureCode")) ?? string.Empty,
            AggregationMethod = FirstNonEmpty(ResolveString(source, "AggregationMethod"), ResolveString(source, "AggregationMethodCode")) ?? string.Empty,
            DirectionPolarity = direction,
            ThresholdModel = threshold,
            ReportingFrequency = frequency,
            BaselineValue = ResolveDecimal(source, "BaselineValue") ?? 0m,
            TargetValue = ResolveDecimal(source, "TargetValue") ?? 0m,
            CascadeMetric = ResolveBool(source, "CascadeMetric") ?? true,
            MetricOrigin = ResolveString(source, "MetricOrigin") ?? "Local",
            MetricRole = ResolveString(source, "MetricRole") ?? "Strategic",
            RestrictionMode = ResolveString(source, "RestrictionMode") ?? "GoalGovernedStructure",
            RollupEligible = ResolveBool(source, "RollupEligible") ?? true,
            SortOrder = ResolveInt(source, "SortOrder") ?? 0,
            YearlyTargets = yearlyTargets
        };
    }

    private static List<CanonicalYearlyTarget> MergeYearlyTargets(
        List<CanonicalYearlyTarget> rows,
        string goalId,
        string metricId,
        List<StrategicGoalMigrationManualReviewRow> manualReviews,
        string runId)
    {
        var merged = new List<CanonicalYearlyTarget>();

        foreach (var group in rows.Where(x => x.Year > 0).GroupBy(x => x.Year))
        {
            var winner = group.OrderByDescending(ScoreYearlyTarget).First();
            if (group.Count() > 1)
            {
                manualReviews.Add(BuildManualReview(runId, goalId, "KPI-Yearly", "Warning", $"Duplicate yearly KPI target rows detected for metric '{metricId}' year '{group.Key}'. Richest row retained."));
            }

            merged.Add(winner with { GoalMetricId = metricId });
        }

        return merged.OrderBy(x => x.Year).ToList();
    }

    private static BsonDocument BuildMetricDocument(CanonicalMetricCandidate metric)
    {
        var id = string.IsNullOrWhiteSpace(metric.MetricId) ? Guid.NewGuid().ToString("N") : metric.MetricId;

        return new BsonDocument
        {
            { "_id", id },
            { "Id", id },
            { "MetricAssignmentId", id },
            { "GoalId", metric.GoalId },
            { "MetricDefinitionId", metric.MetricDefinitionId },
            { "MetricDefId", metric.MetricDefinitionId },
            { "MetricName", metric.MetricName },
            { "MetricType", metric.MetricType },
            { "UnitOfMeasure", metric.UnitOfMeasure },
            { "AggregationMethod", metric.AggregationMethod },
            { "DirectionPolarity", metric.DirectionPolarity },
            { "PolarityCode", metric.DirectionPolarity },
            { "ThresholdModel", metric.ThresholdModel },
            { "ThresholdModelCode", metric.ThresholdModel },
            { "ReportingFrequency", metric.ReportingFrequency },
            { "ReportingFrequencyCode", metric.ReportingFrequency },
            { "BaselineValue", metric.BaselineValue },
            { "TargetValue", metric.TargetValue },
            { "CascadeMetric", metric.CascadeMetric },
            { "MetricOrigin", metric.MetricOrigin },
            { "MetricRole", metric.MetricRole },
            { "RestrictionMode", metric.RestrictionMode },
            { "RollupEligible", metric.RollupEligible },
            { "SortOrder", metric.SortOrder },
            { "MetricBindingStatus", "Unbound" },
            { "CreatedAt", DateTime.UtcNow },
            { "UpdatedAt", DateTime.UtcNow },
            { "YearlyTargets", new BsonArray() }
        };
    }

    private static BsonDocument BuildYearlyDocument(BsonValue sourceId, string metricId, CanonicalYearlyTarget row)
    {
        var doc = new BsonDocument
        {
            { "GoalMetricId", metricId },
            { "Year", row.Year },
            { "TargetValue", ToNullableDecimalBson(row.TargetValue) },
            { "ThresholdMin", ToNullableDecimalBson(row.ThresholdMin) },
            { "ThresholdMax", ToNullableDecimalBson(row.ThresholdMax) },
            { "Commentary", ToNullableStringBson(row.Commentary) },
            { "ThresholdCommentary", ToNullableStringBson(row.Commentary) }
        };

        if (!sourceId.IsBsonNull)
            doc["_id"] = sourceId;

        return doc;
    }

    private static BsonDocument BuildBudgetDocument(BsonValue sourceId, CanonicalBudgetEnvelope row)
    {
        var doc = new BsonDocument
        {
            { "GoalId", row.GoalId },
            { "Year", row.Year },
            { "RevenueTarget", ToNullableDecimalBson(row.RevenueTarget) },
            { "EbitdaTarget", ToNullableDecimalBson(row.EbitdaTarget) },
            { "CapexEnvelope", ToNullableDecimalBson(row.CapexEnvelope) },
            { "OpexEnvelope", ToNullableDecimalBson(row.OpexEnvelope) },
            { "SavingsTarget", ToNullableDecimalBson(row.SavingsTarget) },
            { "FundingPool", ToNullableDecimalBson(row.FundingPool) },
            { "FundingPoolEnvelope", ToNullableDecimalBson(row.FundingPool) },
            { "Commentary", ToNullableStringBson(row.Commentary) }
        };

        if (!sourceId.IsBsonNull)
            doc["_id"] = sourceId;

        return doc;
    }

    private static CanonicalYearlyTarget? ToCanonicalYearlyTarget(BsonDocument doc)
    {
        var year = ResolveInt(doc, "Year", "year");
        if (!year.HasValue || year.Value <= 0)
            return null;

        return new CanonicalYearlyTarget
        {
            SourceId = doc.GetValue("_id", BsonNull.Value),
            GoalMetricId = ResolveString(doc, "GoalMetricId", "goalMetricId") ?? string.Empty,
            Year = year.Value,
            TargetValue = ResolveDecimal(doc, "TargetValue"),
            ThresholdMin = ResolveDecimal(doc, "ThresholdMin"),
            ThresholdMax = ResolveDecimal(doc, "ThresholdMax"),
            Commentary = FirstNonEmpty(ResolveString(doc, "Commentary"), ResolveString(doc, "ThresholdCommentary"))
        };
    }

    private static CanonicalBudgetEnvelope? ToCanonicalBudgetEnvelope(BsonDocument doc)
    {
        var year = ResolveInt(doc, "Year", "year");
        if (!year.HasValue || year.Value <= 0)
            return null;

        return new CanonicalBudgetEnvelope
        {
            Source = "budget-collection",
            SourceId = doc.GetValue("_id", BsonNull.Value),
            GoalId = ResolveString(doc, "GoalId", "goalId") ?? string.Empty,
            Year = year.Value,
            RevenueTarget = ResolveDecimal(doc, "RevenueTarget"),
            EbitdaTarget = ResolveDecimal(doc, "EbitdaTarget"),
            CapexEnvelope = ResolveDecimal(doc, "CapexEnvelope"),
            OpexEnvelope = ResolveDecimal(doc, "OpexEnvelope"),
            SavingsTarget = ResolveDecimal(doc, "SavingsTarget"),
            FundingPool = FirstNonNullDecimal(ResolveDecimal(doc, "FundingPool"), ResolveDecimal(doc, "FundingPoolEnvelope")),
            Commentary = ResolveString(doc, "Commentary")
        };
    }

    private static StrategicGoalMigrationManualReviewRow BuildManualReview(
        string runId,
        string goalId,
        string category,
        string severity,
        string message) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            RunId = runId,
            GoalId = goalId,
            Category = category,
            Severity = severity,
            Message = message,
            LoggedAtUtc = DateTime.UtcNow
        };

    private static int ScoreMetricCandidate(CanonicalMetricCandidate row)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(row.MetricDefinitionId)) score += 2;
        if (!string.IsNullOrWhiteSpace(row.MetricName)) score += 2;
        if (!string.IsNullOrWhiteSpace(row.MetricType)) score += 1;
        if (!string.IsNullOrWhiteSpace(row.UnitOfMeasure)) score += 1;
        if (!string.IsNullOrWhiteSpace(row.AggregationMethod)) score += 1;
        if (!string.IsNullOrWhiteSpace(row.DirectionPolarity)) score += 1;
        if (!string.IsNullOrWhiteSpace(row.ThresholdModel)) score += 1;
        if (!string.IsNullOrWhiteSpace(row.ReportingFrequency)) score += 1;
        score += row.YearlyTargets.Count;
        if (string.Equals(row.Source, "metric-collection", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (string.Equals(row.Source, "goal-embedded:Metrics", StringComparison.OrdinalIgnoreCase)) score += 2;
        return score;
    }

    private static int ScoreYearlyTarget(CanonicalYearlyTarget row)
    {
        var score = 0;
        if (row.TargetValue.HasValue) score += 2;
        if (row.ThresholdMin.HasValue) score += 1;
        if (row.ThresholdMax.HasValue) score += 1;
        if (!string.IsNullOrWhiteSpace(row.Commentary)) score += 1;
        if (!row.SourceId.IsBsonNull) score += 1;
        return score;
    }

    private static int ScoreBudgetEnvelope(CanonicalBudgetEnvelope row)
    {
        var score = 0;
        if (row.RevenueTarget.HasValue) score++;
        if (row.EbitdaTarget.HasValue) score++;
        if (row.CapexEnvelope.HasValue) score++;
        if (row.OpexEnvelope.HasValue) score++;
        if (row.SavingsTarget.HasValue) score++;
        if (row.FundingPool.HasValue) score++;
        if (!string.IsNullOrWhiteSpace(row.Commentary)) score++;
        if (!row.SourceId.IsBsonNull) score++;
        return score;
    }

    private static string ResolveGoalId(BsonDocument doc)
    {
        var explicitId = FirstNonEmpty(
            ResolveString(doc, "GoalId"),
            ResolveString(doc, "Id"),
            ResolveString(doc, "id"));
        if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId;

        var mongoId = doc.GetValue("_id", BsonNull.Value);
        return ResolveBsonValueAsString(mongoId) ?? string.Empty;
    }

    private static string? ResolveString(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) continue;
            if (value.IsString)
            {
                var trimmed = value.AsString.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    return trimmed;
                continue;
            }

            if (value.IsInt32 || value.IsInt64 || value.IsDouble || value.IsDecimal128)
                return value.ToString();
        }

        return null;
    }

    private static List<string> ResolveStringList(BsonDocument doc, string fieldName)
    {
        if (!doc.TryGetValue(fieldName, out var value) || value.IsBsonNull || !value.IsBsonArray)
            return new List<string>();

        return value.AsBsonArray
            .Select(v => ResolveBsonValueAsString(v))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
    }

    private static BsonArray ResolveYearlyArray(BsonDocument doc, params string[] fieldNames)
    {
        foreach (var field in fieldNames)
        {
            if (!doc.TryGetValue(field, out var value) || value.IsBsonNull || !value.IsBsonArray)
                continue;
            return value.AsBsonArray;
        }

        return new BsonArray();
    }

    private static bool? ResolveBool(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) continue;
            if (value.IsBoolean) return value.AsBoolean;
            if (value.IsString && bool.TryParse(value.AsString, out var parsed)) return parsed;
            if (value.IsInt32) return value.AsInt32 != 0;
            if (value.IsInt64) return value.AsInt64 != 0;
        }

        return null;
    }

    private static int? ResolveInt(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) continue;
            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64) return checked((int)value.AsInt64);
            if (value.IsString && int.TryParse(value.AsString, out var parsed)) return parsed;
            if (value.IsDouble) return (int)value.AsDouble;
            if (value.IsDecimal128) return (int)value.AsDecimal128;
        }

        return null;
    }

    private static decimal? ResolveDecimal(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) continue;
            if (value.IsDecimal128) return Decimal128.ToDecimal(value.AsDecimal128);
            if (value.IsDouble) return Convert.ToDecimal(value.AsDouble);
            if (value.IsInt32) return value.AsInt32;
            if (value.IsInt64) return value.AsInt64;
            if (value.IsString && decimal.TryParse(value.AsString, out var parsed)) return parsed;
        }

        return null;
    }

    private static DateTime? ResolveDate(BsonDocument doc, params string[] names)
    {
        foreach (var name in names)
        {
            if (!doc.TryGetValue(name, out var value) || value.IsBsonNull) continue;
            if (value.BsonType == BsonType.DateTime) return value.ToUniversalTime();
            if (value.IsString && DateTime.TryParse(value.AsString, out var parsed)) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }

    private static DateTime? ResolveYearAsDate(BsonDocument doc, string fieldName, bool preferStart)
    {
        var year = ResolveInt(doc, fieldName);
        if (!year.HasValue || year <= 0) return null;
        return preferStart
            ? new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(year.Value, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    }

    private static bool UpsertCanonicalString(BsonDocument doc, string fieldName, string? value)
    {
        value ??= string.Empty;
        var existing = ResolveString(doc, fieldName) ?? string.Empty;
        if (string.Equals(existing, value, StringComparison.Ordinal))
            return false;

        doc[fieldName] = value;
        return true;
    }

    private static bool UpsertCanonicalBool(BsonDocument doc, string fieldName, bool value)
    {
        var existing = ResolveBool(doc, fieldName);
        if (existing.HasValue && existing.Value == value)
            return false;

        doc[fieldName] = value;
        return true;
    }

    private static bool UpsertCanonicalInt(BsonDocument doc, string fieldName, int value)
    {
        var existing = ResolveInt(doc, fieldName);
        if (existing.HasValue && existing.Value == value)
            return false;

        doc[fieldName] = value;
        return true;
    }

    private static bool UpsertCanonicalDate(BsonDocument doc, string fieldName, DateTime? value)
    {
        if (!value.HasValue)
        {
            if (!doc.Contains(fieldName)) return false;
            doc[fieldName] = BsonNull.Value;
            return true;
        }

        var existing = ResolveDate(doc, fieldName);
        var normalized = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        if (existing.HasValue && existing.Value.Date == normalized.Date)
            return false;

        doc[fieldName] = normalized;
        return true;
    }

    private static bool UpsertCanonicalArray(BsonDocument doc, string fieldName, IReadOnlyCollection<string> values)
    {
        var normalized = values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (doc.TryGetValue(fieldName, out var existing) && existing.IsBsonArray)
        {
            var current = existing.AsBsonArray.Select(ResolveBsonValueAsString).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (current.Count == normalized.Count && !current.Except(normalized, StringComparer.OrdinalIgnoreCase).Any())
                return false;
        }

        doc[fieldName] = new BsonArray(normalized);
        return true;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static decimal? FirstNonNullDecimal(params decimal?[] values) =>
        values.FirstOrDefault(v => v.HasValue);

    private static string? ResolveBsonValueAsString(BsonValue value)
    {
        if (value.IsBsonNull) return null;
        return value.BsonType switch
        {
            BsonType.String => string.IsNullOrWhiteSpace(value.AsString) ? null : value.AsString.Trim(),
            BsonType.ObjectId => value.AsObjectId.ToString(),
            BsonType.Int32 => value.AsInt32.ToString(),
            BsonType.Int64 => value.AsInt64.ToString(),
            BsonType.Decimal128 => value.AsDecimal128.ToString(),
            BsonType.Double => value.AsDouble.ToString(),
            _ => value.ToString()
        };
    }

    private static BsonValue ToNullableDecimalBson(decimal? value) =>
        value.HasValue ? new BsonDecimal128(value.Value) : BsonNull.Value;

    private static BsonValue ToNullableStringBson(string? value) =>
        string.IsNullOrWhiteSpace(value) ? BsonNull.Value : new BsonString(value);

    private static bool BsonValueEquals(BsonValue left, BsonValue right)
    {
        if (left.IsBsonNull && right.IsBsonNull) return true;
        if (left.IsBsonNull || right.IsBsonNull) return false;
        if (left.BsonType == right.BsonType) return left == right;

        var leftString = ResolveBsonValueAsString(left);
        var rightString = ResolveBsonValueAsString(right);
        return string.Equals(leftString, rightString, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadBoolFlag(IConfiguration? configuration, string configKey, string environmentKey)
    {
        var fromConfig = configuration?[configKey];
        if (!string.IsNullOrWhiteSpace(fromConfig) && bool.TryParse(fromConfig, out var parsedConfig))
            return parsedConfig;

        var fromEnv = Environment.GetEnvironmentVariable(environmentKey);
        if (!string.IsNullOrWhiteSpace(fromEnv) && bool.TryParse(fromEnv, out var parsedEnv))
            return parsedEnv;

        return false;
    }

    private sealed class BsonValueComparer : IEqualityComparer<BsonValue>
    {
        public bool Equals(BsonValue? x, BsonValue? y)
        {
            if (x is null || y is null) return x is null && y is null;
            return BsonValueEquals(x, y);
        }

        public int GetHashCode(BsonValue obj)
        {
            if (obj.IsBsonNull) return 0;
            var text = ResolveBsonValueAsString(obj);
            return (text ?? obj.ToString()).ToUpperInvariant().GetHashCode();
        }
    }

    private sealed record GoalMigrationPlan(
        BsonDocument MasterDocument,
        bool MasterChanged,
        List<BsonDocument> MetricsToUpsert,
        List<BsonValue> MetricIdsToDelete,
        List<BsonDocument> YearlyRowsToUpsert,
        List<BsonValue> YearlyIdsToDelete,
        List<BsonDocument> BudgetsToUpsert,
        List<BsonValue> BudgetIdsToDelete,
        bool SkipWrite);

    private sealed record CanonicalMetricCandidate
    {
        public required string Source { get; init; }
        public required BsonDocument SourceDocument { get; init; }
        public string? MetricId { get; init; }
        public required string GoalId { get; init; }
        public required string MetricDefinitionId { get; init; }
        public required string MetricName { get; init; }
        public required string MetricType { get; init; }
        public required string UnitOfMeasure { get; init; }
        public required string AggregationMethod { get; init; }
        public required string DirectionPolarity { get; init; }
        public required string ThresholdModel { get; init; }
        public required string ReportingFrequency { get; init; }
        public decimal BaselineValue { get; init; }
        public decimal TargetValue { get; init; }
        public bool CascadeMetric { get; init; }
        public required string MetricOrigin { get; init; }
        public required string MetricRole { get; init; }
        public required string RestrictionMode { get; init; }
        public bool RollupEligible { get; init; }
        public int SortOrder { get; init; }
        public List<CanonicalYearlyTarget> YearlyTargets { get; init; } = new();

        public string SemanticKey => string.Join("|", new[]
        {
            MetricDefinitionId,
            MetricName,
            MetricType,
            UnitOfMeasure,
            AggregationMethod,
            DirectionPolarity,
            ThresholdModel,
            ReportingFrequency
        }.Select(x => (x ?? string.Empty).Trim().ToUpperInvariant()));
    }

    private sealed record CanonicalYearlyTarget
    {
        public BsonValue SourceId { get; init; } = BsonNull.Value;
        public string GoalMetricId { get; init; } = string.Empty;
        public int Year { get; init; }
        public decimal? TargetValue { get; init; }
        public decimal? ThresholdMin { get; init; }
        public decimal? ThresholdMax { get; init; }
        public string? Commentary { get; init; }
    }

    private sealed record CanonicalBudgetEnvelope
    {
        public BsonValue SourceId { get; init; } = BsonNull.Value;
        public string Source { get; init; } = string.Empty;
        public string GoalId { get; init; } = string.Empty;
        public int Year { get; init; }
        public decimal? RevenueTarget { get; init; }
        public decimal? EbitdaTarget { get; init; }
        public decimal? CapexEnvelope { get; init; }
        public decimal? OpexEnvelope { get; init; }
        public decimal? SavingsTarget { get; init; }
        public decimal? FundingPool { get; init; }
        public string? Commentary { get; init; }
    }
}

internal sealed class StrategicGoalMigrationState
{
    [BsonId]
    public string MigrationId { get; set; } = string.Empty;
    public string LatestRunId { get; set; } = string.Empty;
    public DateTime AppliedAtUtc { get; set; }
    public int GoalsChanged { get; set; }
    public int ManualReviewCount { get; set; }
}

internal sealed class StrategicGoalMigrationRunReport
{
    [BsonId]
    public string RunId { get; set; } = string.Empty;
    public string MigrationId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool DryRun { get; set; }
    public bool Forced { get; set; }

    public int GoalsScanned { get; set; }
    public int GoalsChanged { get; set; }
    public int GoalsSkipped { get; set; }
    public int MetricsUpserted { get; set; }
    public int YearlyRowsUpserted { get; set; }
    public int BudgetsUpserted { get; set; }
    public int DuplicateMetricsRemoved { get; set; }
    public int DuplicateYearlyRowsRemoved { get; set; }
    public int DuplicateBudgetRowsRemoved { get; set; }
    public int ManualReviewCount { get; set; }

    public StrategicGoalMigrationDataQualitySnapshot PreMigration { get; set; } = new();
    public StrategicGoalMigrationDataQualitySnapshot PostMigration { get; set; } = new();
    public List<string> Notes { get; set; } = new();
}

internal sealed class StrategicGoalMigrationDataQualitySnapshot
{
    public int GoalCount { get; set; }
    public int MetricCount { get; set; }
    public int YearlyTargetCount { get; set; }
    public int BudgetCount { get; set; }
    public int OrphanMetricCount { get; set; }
    public int OrphanYearlyTargetCount { get; set; }
    public int OrphanBudgetCount { get; set; }
    public int DuplicateMetricCount { get; set; }
    public int DuplicateYearlyTargetCount { get; set; }
    public int DuplicateBudgetCount { get; set; }
    public int MissingOwnerRoleCount { get; set; }
    public int MissingOwnerCompanyCount { get; set; }
}

internal sealed class StrategicGoalMigrationBackupRow
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string GoalId { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
    public BsonDocument Document { get; set; } = new();
}

internal sealed class StrategicGoalMigrationManualReviewRow
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string GoalId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Message { get; set; } = string.Empty;
    public DateTime LoggedAtUtc { get; set; }
}
