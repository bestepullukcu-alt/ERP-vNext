using Diten.Application.Dtos.DemandIdeas;
using Diten.Application.Queries.DemandIdeaQueries;
using Diten.Domain.Aggregates.DemandIdea;

namespace Diten.Application.Handlers.DemandIdeaHandlers;

internal static class DemandIdeaHandlerSupport
{
    public static DemandIdeaMetadataDto Metadata() => new()
    {
        RequestTypes = new[] { "Process Improvement", "Program", "Enhancement", "Compliance", "New Capability", "Platform", "Infrastructure", "Regulatory" },
        BusinessUnits = new[] { "IT", "HR", "Risk", "Operations", "Engineering", "Security", "Sales", "Finance", "Corporate", "Legal", "Product" },
        Categories = new[]
        {
            "Business application",
            "Technology",
            "Infrastructure & platform",
            "Data & analytics",
            "Security & privacy",
            "Customer experience",
            "Regulatory / compliance",
            "Operations",
            "Product innovation",
            "Program / initiative",
            "Other"
        },
        DemandSources = new[]
        {
            "Portfolio intake",
            "Business unit request",
            "Internal business request",
            "Executive / steering committee",
            "Customer / partner",
            "Regulatory / audit finding",
            "Incident / problem management",
            "Innovation lab / hackathon",
            "Vendor / contract",
            "Other"
        },
        Priorities = new[] { "Low", "Medium", "High", "Critical" },
        ComplianceImpacts = new[] { "None", "Low", "Medium", "High", "Critical" },
        EstimatedComplexities = new[] { "Trivial", "Low", "Medium", "High", "Very high" },
        RiskSensitivities = new[] { "Very low", "Low", "Medium", "High", "Critical" },
        StrategicAlignments = new[] { "Growth & Revenue", "Operational Excellence", "Digital Transformation", "Cost Reduction", "Customer Experience", "Risk & Compliance", "Innovation & R&D" }
    };

    public static IReadOnlyList<StrategicThemeDto> StrategicThemes() => new[]
    {
        new StrategicThemeDto { Key = "digital", Label = "Digital Transformation" },
        new StrategicThemeDto { Key = "ops", Label = "Operational Excellence" },
        new StrategicThemeDto { Key = "growth", Label = "Growth & Revenue" },
        new StrategicThemeDto { Key = "risk", Label = "Risk & Compliance" },
        new StrategicThemeDto { Key = "customer", Label = "Customer Experience" },
        new StrategicThemeDto { Key = "portfolio", Label = "Portfolio Agility" }
    };

    public static Dictionary<string, List<string>> ValidateSubmit(DemandIdeaAggregate e)
    {
        var err = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Require(string field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                err[field] = new List<string> { "Required." };
            }
        }

        Require(nameof(e.Title), e.Title);
        Require(nameof(e.ProblemStatement), e.ProblemStatement);
        Require(nameof(e.ExpectedOutcome), e.ExpectedOutcome);
        Require(nameof(e.RequestType), e.RequestType);
        Require(nameof(e.BusinessUnit), e.BusinessUnit);
        Require(nameof(e.Requestor), e.Requestor);
        Require(nameof(e.Priority), e.Priority);
        return err;
    }

    public static bool CanUpdate(DemandIdeaAggregate e) =>
        string.Equals(e.Status, "Draft", StringComparison.OrdinalIgnoreCase);

    public static bool CanSubmit(DemandIdeaAggregate e) =>
        string.Equals(e.Status, "Draft", StringComparison.OrdinalIgnoreCase);

    public static string NextRecordNumber(IReadOnlyList<DemandIdeaAggregate> all)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"IC-{year}-";
        var max = 0;
        foreach (var x in all)
        {
            if (x.RecordNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var tail = x.RecordNumber[prefix.Length..];
                if (int.TryParse(tail, out var n))
                {
                    max = Math.Max(max, n);
                }
            }
        }

        return $"{prefix}{(max + 1).ToString("D4")}";
    }

    public static void ApplyUpsert(DemandIdeaAggregate e, DemandIdeaUpsertRequest r)
    {
        if (r.Title != null) e.Title = r.Title;
        if (r.ProblemStatement != null) e.ProblemStatement = r.ProblemStatement;
        if (r.ExpectedOutcome != null) e.ExpectedOutcome = r.ExpectedOutcome;
        if (r.RequestType != null) e.RequestType = r.RequestType;
        if (r.StrategicAlignment != null) e.StrategicAlignment = r.StrategicAlignment;
        if (r.BusinessUnit != null) e.BusinessUnit = r.BusinessUnit;
        if (r.Requestor != null) e.Requestor = r.Requestor;
        if (r.Sponsor != null) e.Sponsor = r.Sponsor;
        if (r.OwnerName != null) e.OwnerName = r.OwnerName;
        if (r.ProposedScope != null) e.ProposedScope = r.ProposedScope;
        if (r.OutOfScope != null) e.OutOfScope = r.OutOfScope;
        if (r.Assumptions != null) e.Assumptions = r.Assumptions;
        if (r.Constraints != null) e.Constraints = r.Constraints;
        if (r.Category != null) e.Category = r.Category;
        if (r.DemandSource != null) e.DemandSource = r.DemandSource;
        if (r.Priority != null) e.Priority = r.Priority;
        if (r.ComplianceImpact != null) e.ComplianceImpact = r.ComplianceImpact;
        if (r.EstimatedComplexity != null) e.EstimatedComplexity = r.EstimatedComplexity;
        if (r.RiskSensitivity != null) e.RiskSensitivity = r.RiskSensitivity;
        if (r.SupportingLinks != null) e.SupportingLinks = r.SupportingLinks;
        if (r.Notes != null) e.Notes = r.Notes;
        if (r.Tags != null) e.Tags = r.Tags;
        if (r.StrategicThemeKeys != null) e.StrategicThemeKeys = r.StrategicThemeKeys;
        if (r.RelatedIdeaIds != null)
        {
            e.RelatedIdeaIds = r.RelatedIdeaIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (r.ReviewDueDate.HasValue) e.ReviewDueDate = r.ReviewDueDate;
        if (r.Attachments == null) return;

        e.Attachments = r.Attachments.Select(a => new DemandIdeaAttachment
        {
            Id = string.IsNullOrEmpty(a.Id) ? Guid.NewGuid().ToString() : a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            SizeBytes = a.SizeBytes,
            StorageKey = a.StorageKey
        }).ToList();
    }

    public static DemandIdeaResponseDto MapToDto(DemandIdeaAggregate e) => new()
    {
        Id = e.Id,
        RecordNumber = e.RecordNumber,
        Title = e.Title,
        ProblemStatement = e.ProblemStatement,
        ExpectedOutcome = e.ExpectedOutcome,
        RequestType = e.RequestType,
        StrategicAlignment = e.StrategicAlignment,
        BusinessUnit = e.BusinessUnit,
        Requestor = e.Requestor,
        Sponsor = e.Sponsor,
        OwnerName = e.OwnerName,
        ProposedScope = e.ProposedScope,
        OutOfScope = e.OutOfScope,
        Assumptions = e.Assumptions,
        Constraints = e.Constraints,
        Category = e.Category,
        DemandSource = e.DemandSource,
        Priority = e.Priority,
        ComplianceImpact = e.ComplianceImpact,
        EstimatedComplexity = e.EstimatedComplexity,
        RiskSensitivity = e.RiskSensitivity,
        SupportingLinks = e.SupportingLinks,
        Notes = e.Notes,
        Tags = e.Tags,
        Attachments = e.Attachments.Select(a => new AttachmentResponseDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            SizeBytes = a.SizeBytes,
            DownloadUrl = $"/api/v1/uploads/{e.Id}/{a.Id}"
        }).ToList(),
        StrategicThemeKeys = e.StrategicThemeKeys,
        RelatedIdeaIds = e.RelatedIdeaIds ?? new List<string>(),
        Status = e.Status,
        ReviewDueDate = e.ReviewDueDate,
        CreatedAt = e.CreatedDate,
        UpdatedAt = e.LastModifiedDate,
        CreatedBy = e.CreatedBy,
        UpdatedBy = e.LastModifiedBy
    };

    public static int ScoreRelated(GetRelatedDemandIdeasQuery query, DemandIdeaAggregate o)
    {
        var s = 0;
        if (!string.IsNullOrEmpty(query.BusinessUnit) && string.Equals(query.BusinessUnit, o.BusinessUnit, StringComparison.OrdinalIgnoreCase))
            s += 18;
        if (!string.IsNullOrEmpty(query.RequestType) && string.Equals(query.RequestType, o.RequestType, StringComparison.OrdinalIgnoreCase))
            s += 12;
        if (!string.IsNullOrEmpty(query.StrategicAlignment) && string.Equals(query.StrategicAlignment, o.StrategicAlignment, StringComparison.OrdinalIgnoreCase))
            s += 10;
        if (query.Tags is { Count: > 0 })
            s += query.Tags.Intersect(o.Tags, StringComparer.OrdinalIgnoreCase).Count() * 14;
        if (!string.IsNullOrWhiteSpace(query.Title) && !string.IsNullOrWhiteSpace(o.Title))
            s += (int)(TitleTokensOverlap(query.Title, o.Title) * 35);
        return s;
    }

    public static int ScoreDuplicate(CheckDemandIdeaDuplicatesQuery request, DemandIdeaAggregate o)
    {
        var s = 0;
        if (!string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(o.Title))
        {
            var sim = TitleTokensOverlap(request.Title, o.Title);
            s += (int)(sim * 55);
        }

        if (!string.IsNullOrEmpty(request.BusinessUnit) && string.Equals(request.BusinessUnit, o.BusinessUnit, StringComparison.OrdinalIgnoreCase))
            s += 20;
        if (!string.IsNullOrEmpty(request.RequestType) && string.Equals(request.RequestType, o.RequestType, StringComparison.OrdinalIgnoreCase))
            s += 15;
        if (request.Tags is { Count: > 0 } && o.Tags.Count > 0)
        {
            var overlap = request.Tags.Intersect(o.Tags, StringComparer.OrdinalIgnoreCase).Count();
            s += overlap * 12;
        }

        return Math.Min(100, s);
    }

    public static string BuildDuplicateReason(CheckDemandIdeaDuplicatesQuery request, DemandIdeaAggregate o)
    {
        var parts = new List<string>();
        if (TitleTokensOverlap(request.Title, o.Title) > 0.5)
            parts.Add("Similar title");
        if (!string.IsNullOrEmpty(request.BusinessUnit) && string.Equals(request.BusinessUnit, o.BusinessUnit, StringComparison.OrdinalIgnoreCase))
            parts.Add("Same business unit");
        if (!string.IsNullOrEmpty(request.RequestType) && string.Equals(request.RequestType, o.RequestType, StringComparison.OrdinalIgnoreCase))
            parts.Add("Same request type");
        return parts.Count == 0 ? "Heuristic match" : string.Join(", ", parts);
    }

    private static double TitleTokensOverlap(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0;
        var ta = new HashSet<string>(SplitWords(a), StringComparer.OrdinalIgnoreCase);
        var tb = new HashSet<string>(SplitWords(b), StringComparer.OrdinalIgnoreCase);
        if (ta.Count == 0 || tb.Count == 0) return 0;
        var inter = ta.Intersect(tb).Count();
        return 2.0 * inter / (ta.Count + tb.Count);
    }

    private static IEnumerable<string> SplitWords(string s) =>
        s.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2);
}
