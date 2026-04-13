using Asp.Versioning;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy")]
public sealed class EnterpriseStrategyCascadeReviewController : EnterpriseStrategyApiControllerBase
{
    private static readonly object Gate = new();
    private static readonly List<StrategicReviewEventDto> Reviews = SeedReviews();
    private static readonly List<ReviewDecisionActionDto> Decisions = SeedDecisions();

    [HttpGet("cascade/builder")]
    public ActionResult<Response<CascadeBuilderSnapshotDto>> CascadeBuilder([FromQuery] string? goalId, [FromQuery] string? metric, [FromQuery] string? company)
    {
        var snapshot = SeedCascadeBuilder(goalId, metric, company);
        return Ok(Response<CascadeBuilderSnapshotDto>.Ok(snapshot, HttpContext.TraceIdentifier));
    }

    [HttpGet("cascade/target-allocation")]
    public ActionResult<Response<TargetAllocationSnapshotDto>> TargetAllocation([FromQuery] string? goalId, [FromQuery] string? metric)
    {
        var snapshot = SeedTargetAllocation(goalId, metric);
        return Ok(Response<TargetAllocationSnapshotDto>.Ok(snapshot, HttpContext.TraceIdentifier));
    }

    [HttpGet("cascade/consolidation")]
    public ActionResult<Response<IReadOnlyList<ConsolidationRowDto>>> Consolidation([FromQuery] string? goalId, [FromQuery] string? company)
    {
        var rows = SeedConsolidationRows()
            .Where(x => string.IsNullOrWhiteSpace(goalId) || string.Equals(x.GoalId, goalId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(company) || string.Equals(x.CompanyId, company, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Ok(Response<IReadOnlyList<ConsolidationRowDto>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpGet("cascade/variance")]
    public ActionResult<Response<IReadOnlyList<VarianceAnalysisRowDto>>> Variance([FromQuery] string? goalId, [FromQuery] string? objectiveId, [FromQuery] string? company, [FromQuery] string? period, [FromQuery] string? kpiId)
    {
        var rows = SeedVarianceRows()
            .Where(x => string.IsNullOrWhiteSpace(goalId) || string.Equals(x.GoalId, goalId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(objectiveId) || string.Equals(x.ObjectiveId, objectiveId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(company) || string.Equals(x.CompanyId, company, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(period) || string.Equals(x.TimePeriod, period, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(kpiId) || string.Equals(x.KpiId, kpiId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Ok(Response<IReadOnlyList<VarianceAnalysisRowDto>>.Ok(rows, HttpContext.TraceIdentifier));
    }

    [HttpGet("reviews/calendar")]
    public ActionResult<Response<IReadOnlyList<StrategicReviewEventDto>>> ReviewCalendar()
    {
        lock (Gate)
        {
            var rows = Reviews.OrderBy(x => x.ReviewDate).ToList();
            return Ok(Response<IReadOnlyList<StrategicReviewEventDto>>.Ok(rows, HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("reviews/pack")]
    public ActionResult<Response<ReviewPackDto>> ReviewPack([FromQuery] string? reviewId)
    {
        lock (Gate)
        {
            var review = Reviews.FirstOrDefault(x => string.Equals(x.Id, reviewId, StringComparison.OrdinalIgnoreCase)) ?? Reviews.First();
            var pack = new ReviewPackDto
            {
                ReviewId = review.Id,
                Title = $"Strategic Review Pack - {review.ReviewType}",
                Summary = "Consolidated scorecard, cascade, and variance highlights for executive review.",
                GoalIds = new[] { review.GoalId, "goal-002" },
                ObjectiveIds = new[] { review.ObjectiveId, "obj-002" },
                KpiIds = new[] { "kpi-001", "kpi-002", "kpi-003" },
                CascadeHighlights = new[] { "92% cascade coverage across objective targets.", "Company scope mismatch on 1 objective allocation." },
                VarianceHighlights = new[] { "Revenue Growth at -1.2% variance to target.", "Program throughput improved by 0.8 quarter-over-quarter." },
                DecisionsRequired = new[] { "Approve objective target rebalance for Q3.", "Confirm ownership transition for KPI-003." }
            };
            return Ok(Response<ReviewPackDto>.Ok(pack, HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("reviews/decisions")]
    public ActionResult<Response<IReadOnlyList<ReviewDecisionActionDto>>> ReviewDecisions([FromQuery] string? reviewId, [FromQuery] string? status)
    {
        lock (Gate)
        {
            var rows = Decisions
                .Where(x => string.IsNullOrWhiteSpace(reviewId) || string.Equals(x.ReviewId, reviewId, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrWhiteSpace(status) || string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.DueDate)
                .ToList();
            return Ok(Response<IReadOnlyList<ReviewDecisionActionDto>>.Ok(rows, HttpContext.TraceIdentifier));
        }
    }

    [HttpPost("reviews/decisions")]
    public ActionResult<Response<ReviewDecisionActionDto>> CreateDecision([FromBody] ReviewDecisionActionDto body)
    {
        lock (Gate)
        {
            var row = new ReviewDecisionActionDto
            {
                Id = string.IsNullOrWhiteSpace(body.Id) ? $"dec-{DateTime.UtcNow.Ticks}" : body.Id.Trim(),
                ReviewId = body.ReviewId,
                Title = body.Title,
                RelatedGoalId = body.RelatedGoalId,
                RelatedObjectiveId = body.RelatedObjectiveId,
                RelatedKpiId = body.RelatedKpiId,
                Owner = body.Owner,
                DueDate = body.DueDate == default ? DateTime.UtcNow.Date.AddDays(14) : body.DueDate,
                Status = string.IsNullOrWhiteSpace(body.Status) ? "Open" : body.Status,
                Evidence = body.Evidence,
                Rationale = body.Rationale,
                IsOpen = !string.Equals(body.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            };
            Decisions.Add(row);
            return Ok(Response<ReviewDecisionActionDto>.Ok(row, HttpContext.TraceIdentifier));
        }
    }

    [HttpPatch("reviews/decisions/{decisionId}/status")]
    public ActionResult<Response<ReviewDecisionActionDto>> UpdateDecisionStatus(string decisionId, [FromBody] StatusChangeRequestDto body)
    {
        lock (Gate)
        {
            var index = Decisions.FindIndex(x => string.Equals(x.Id, decisionId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return NotFound(
                    Response<ReviewDecisionActionDto>.Fail(
                        "NOT_FOUND",
                        StatusCodes.Status404NotFound,
                        HttpContext.TraceIdentifier,
                        ErrorDetails("Decision not found.")));
            }

            var row = Decisions[index];
            row.Status = string.IsNullOrWhiteSpace(body.Status) ? row.Status : body.Status;
            row.IsOpen = !string.Equals(row.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            Decisions[index] = row;
            return Ok(Response<ReviewDecisionActionDto>.Ok(row, HttpContext.TraceIdentifier));
        }
    }

    [HttpGet("reviews/history")]
    public ActionResult<Response<IReadOnlyList<ReviewHistoryRowDto>>> ReviewHistory()
    {
        lock (Gate)
        {
            var rows = Reviews
                .OrderByDescending(x => x.ReviewDate)
                .Select(x =>
                {
                    var linked = Decisions.Where(d => string.Equals(d.ReviewId, x.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                    return new ReviewHistoryRowDto
                    {
                        ReviewId = x.Id,
                        ReviewDate = x.ReviewDate,
                        ReviewType = x.ReviewType,
                        DecisionsCount = linked.Count,
                        OpenActions = linked.Count(d => d.IsOpen),
                        ClosedActions = linked.Count(d => !d.IsOpen),
                        ScorecardSnapshotRef = $"scorecard-{x.ReviewDate:yyyyMMdd}",
                        CascadeSnapshotRef = $"cascade-{x.ReviewDate:yyyyMMdd}"
                    };
                })
                .ToList();
            return Ok(Response<IReadOnlyList<ReviewHistoryRowDto>>.Ok(rows, HttpContext.TraceIdentifier));
        }
    }

    private static CascadeBuilderSnapshotDto SeedCascadeBuilder(string? goalId, string? metric, string? company)
    {
        var parentTarget = 100m;
        var rows = new List<CascadeObjectiveRowDto>
        {
            new() { ObjectiveId = "obj-001", ObjectiveName = "Increase revenue from existing customers", ContributionWeight = 0.45m, AllocatedTarget = 45m, CoverageStatus = "Complete", CompanyId = company, Warning = "" },
            new() { ObjectiveId = "obj-002", ObjectiveName = "Expand strategic accounts", ContributionWeight = 0.35m, AllocatedTarget = 35m, CoverageStatus = "Partial", CompanyId = company, Warning = "Initiative linkage incomplete." },
            new() { ObjectiveId = "obj-003", ObjectiveName = "Improve retention performance", ContributionWeight = 0.20m, AllocatedTarget = 20m, CoverageStatus = "Complete", CompanyId = company, Warning = "" }
        };
        return new CascadeBuilderSnapshotDto
        {
            GoalId = string.IsNullOrWhiteSpace(goalId) ? "goal-001" : goalId,
            GoalName = "Accelerate profitable growth",
            GoalMetric = string.IsNullOrWhiteSpace(metric) ? "Revenue Growth %" : metric,
            ParentTarget = parentTarget,
            CompanyId = company,
            Objectives = rows
        };
    }

    private static TargetAllocationSnapshotDto SeedTargetAllocation(string? goalId, string? metric)
    {
        return new TargetAllocationSnapshotDto
        {
            ParentGoalId = string.IsNullOrWhiteSpace(goalId) ? "goal-001" : goalId,
            ParentGoalName = "Accelerate profitable growth",
            GoalMetric = string.IsNullOrWhiteSpace(metric) ? "Revenue Growth %" : metric,
            ParentTarget = 100m,
            Allocations = new List<TargetAllocationRowDto>
            {
                new() { LevelType = "Objective", EntityId = "obj-001", EntityName = "Increase revenue from existing customers", CompanyId = "cmp-001", ManualTarget = 40m, GeneratedTarget = 33m, FinalTarget = 40m },
                new() { LevelType = "Objective", EntityId = "obj-002", EntityName = "Expand strategic accounts", CompanyId = "cmp-002", ManualTarget = 35m, GeneratedTarget = 33m, FinalTarget = 35m },
                new() { LevelType = "Objective", EntityId = "obj-003", EntityName = "Improve retention performance", CompanyId = "cmp-003", ManualTarget = 25m, GeneratedTarget = 34m, FinalTarget = 25m }
            }
        };
    }

    private static List<ConsolidationRowDto> SeedConsolidationRows() =>
        new()
        {
            new() { GoalId = "goal-001", GoalName = "Accelerate profitable growth", ObjectiveId = "obj-001", ObjectiveName = "Increase revenue", CompanyId = "cmp-001", ContributionTotal = 0.45m, CurrentValue = 38m, TargetValue = 45m, Variance = -7m },
            new() { GoalId = "goal-001", GoalName = "Accelerate profitable growth", ObjectiveId = "obj-002", ObjectiveName = "Expand strategic accounts", CompanyId = "cmp-002", ContributionTotal = 0.35m, CurrentValue = 30m, TargetValue = 35m, Variance = -5m },
            new() { GoalId = "goal-001", GoalName = "Accelerate profitable growth", ObjectiveId = "obj-003", ObjectiveName = "Improve retention", CompanyId = "cmp-003", ContributionTotal = 0.20m, CurrentValue = 23m, TargetValue = 20m, Variance = 3m }
        };

    private static List<VarianceAnalysisRowDto> SeedVarianceRows() =>
        new()
        {
            new() { GoalId = "goal-001", ObjectiveId = "obj-001", KpiId = "kpi-001", KpiName = "Revenue Growth", CompanyId = "cmp-001", TimePeriod = "2026-Q1", TargetValue = 6.0m, CurrentValue = 4.8m, VarianceAmount = -1.2m, VariancePercent = -20m, Trend = "Down", Status = "At Risk", AlignmentRowId = "lnk-001" },
            new() { GoalId = "goal-001", ObjectiveId = "obj-002", KpiId = "kpi-002", KpiName = "Coverage", CompanyId = "cmp-002", TimePeriod = "2026-Q1", TargetValue = 85m, CurrentValue = 82m, VarianceAmount = -3m, VariancePercent = -3.53m, Trend = "Stable", Status = "Watch", AlignmentRowId = "lnk-002" },
            new() { GoalId = "goal-001", ObjectiveId = "obj-003", KpiId = "kpi-003", KpiName = "Program Throughput", CompanyId = "cmp-003", TimePeriod = "2026-Q1", TargetValue = 20m, CurrentValue = 21m, VarianceAmount = 1m, VariancePercent = 5m, Trend = "Up", Status = "On Track", AlignmentRowId = "lnk-003" }
        };

    private static List<StrategicReviewEventDto> SeedReviews()
    {
        var now = DateTime.UtcNow.Date;
        return new List<StrategicReviewEventDto>
        {
            new() { Id = "rev-2026-q1", ReviewDate = now.AddDays(-20), ReviewType = "Quarterly Strategy Review", GoalId = "goal-001", ObjectiveId = "obj-001", ScorecardScope = "Enterprise", Facilitator = "Chief Strategy Officer", Status = "Completed" },
            new() { Id = "rev-2026-q2", ReviewDate = now.AddDays(30), ReviewType = "Quarterly Strategy Review", GoalId = "goal-001", ObjectiveId = "obj-002", ScorecardScope = "Enterprise", Facilitator = "Chief Executive Officer", Status = "Planned" },
            new() { Id = "rev-2026-midyear", ReviewDate = now.AddDays(75), ReviewType = "Mid-Year Performance Review", GoalId = "goal-002", ObjectiveId = "obj-004", ScorecardScope = "Company", Facilitator = "Chief Financial Officer", Status = "Planned" }
        };
    }

    private static List<ReviewDecisionActionDto> SeedDecisions()
    {
        var now = DateTime.UtcNow.Date;
        return new List<ReviewDecisionActionDto>
        {
            new() { Id = "dec-001", ReviewId = "rev-2026-q1", Title = "Reallocate Q2 target weights", RelatedGoalId = "goal-001", RelatedObjectiveId = "obj-002", RelatedKpiId = "kpi-002", Owner = "Chief Strategy Officer", DueDate = now.AddDays(14), Status = "Open", IsOpen = true, Evidence = "EVD-900", Rationale = "Coverage gap across initiatives." },
            new() { Id = "dec-002", ReviewId = "rev-2026-q1", Title = "Confirm KPI ownership transition", RelatedGoalId = "goal-001", RelatedObjectiveId = "obj-001", RelatedKpiId = "kpi-003", Owner = "Chief Operating Officer", DueDate = now.AddDays(7), Status = "Completed", IsOpen = false, Evidence = "EVD-901", Rationale = "Owner moved to transformation office." }
        };
    }

    private static Dictionary<string, List<string>> ErrorDetails(string message) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["message"] = new List<string> { message }
        };
}
