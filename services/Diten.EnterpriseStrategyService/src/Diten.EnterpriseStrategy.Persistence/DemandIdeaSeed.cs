using Diten.Application.Common.Interfaces;
using Diten.Domain.Aggregates.DemandIdea;

namespace Diten.Persistence;

internal static class DemandIdeaSeed
{
    public static async Task SeedIfEmptyAsync(IRepository<DemandIdeaAggregate> repository)
    {
        var all = await repository.GetAllAsync();
        if (all.Count > 0)
            return;

        var year = DateTime.UtcNow.Year;
        var n = 1;
        DemandIdeaAggregate D(string title, string bu, string rt, string pri, string[] tags, string align) => new()
        {
            RecordNumber = $"IC-{year}-{n++:D4}",
            Title = title,
            ProblemStatement = $"Business units need clarity on {title.ToLowerInvariant()} and related dependencies.",
            ExpectedOutcome = "Measurable improvement in cycle time and stakeholder alignment.",
            RequestType = rt,
            StrategicAlignment = align,
            BusinessUnit = bu,
            Requestor = "Sarah Chen",
            Sponsor = "Mike Johnson",
            OwnerName = "Sarah Chen",
            Category = "Technology",
            DemandSource = "Internal business request",
            Priority = pri,
            ComplianceImpact = "Low",
            EstimatedComplexity = "Medium",
            RiskSensitivity = "Medium",
            ProposedScope = "Core initiative scope as discussed in Q planning.",
            Tags = tags.ToList(),
            StrategicThemeKeys = new List<string> { "digital", "ops" },
            Status = "Draft",
            ReviewDueDate = DateTime.UtcNow.Date.AddDays(14),
            CreatedDate = DateTime.UtcNow.AddDays(-7),
            LastModifiedDate = DateTime.UtcNow.AddDays(-1)
        };

        var seeds = new[]
        {
            D("Customer portal enhancement", "IT", "Enhancement", "High", new[] { "portal", "ux" }, "Digital Transformation"),
            D("Cloud cost optimization program", "IT", "Program", "High", new[] { "cloud", "cost" }, "Operational Excellence"),
            D("HR onboarding portal refresh", "HR", "Enhancement", "Medium", new[] { "hr", "onboarding" }, "Customer Experience"),
            D("Regulatory reporting automation", "Risk", "Compliance", "Critical", new[] { "compliance", "reporting" }, "Risk & Compliance"),
            D("Data lake governance framework", "IT", "Platform", "Medium", new[] { "data", "governance" }, "Digital Transformation"),
            D("Partner API rate limiting", "Engineering", "Enhancement", "Low", new[] { "api", "partner" }, "Operational Excellence")
        };

        foreach (var s in seeds)
            await repository.AddAsync(s);
    }
}
