using System.Text.Json;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.VisitWorkspace;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// WP-VW-W2 — an in-memory published <c>visit-outcome-reason</c> set behind the THREE production reference seams
/// (single-value check, value attributes, whole list). <see cref="FromCatalog"/> seeds it from the REAL Platform catalog
/// file (<c>crm-visit-reference.json</c>), so the CRM rules are measured against the shipped values; <see cref="Permissive"/>
/// accepts any code for every action (the pre-W2 tests that only care about other rules).
/// </summary>
internal sealed class FakeVisitReasonSet : IReferenceDataValidator, IReferenceMetadataReader, IReferenceDataCatalogReader
{
    public sealed record Value(string Code, string DisplayName, bool IsActive, Dictionary<string, string> Attributes);

    private readonly bool _permissive;

    public List<Value> Values { get; } = new();

    /// <summary>The set cannot be read (Platform down / not published) ⇒ SetMissing / not published.</summary>
    public bool Unavailable { get; set; }

    public int ValidateCalls { get; private set; }

    private FakeVisitReasonSet(bool permissive) => _permissive = permissive;

    public static FakeVisitReasonSet Permissive() => new(true);

    public static FakeVisitReasonSet FromCatalog()
    {
        var set = new FakeVisitReasonSet(false);
        using var json = JsonDocument.Parse(File.ReadAllText(CatalogPath()));
        var values = json.RootElement.GetProperty("sets").EnumerateArray()
            .Single(s => s.GetProperty("set_code").GetString() == VisitOutcomeReasons.ReasonSet)
            .GetProperty("values");
        foreach (var v in values.EnumerateArray())
        {
            set.Values.Add(new Value(
                v.GetProperty("value_code").GetString()!,
                v.GetProperty("display_name").GetString()!,
                v.GetProperty("is_active").GetBoolean(),
                v.GetProperty("attributes").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!)));
        }

        return set;
    }

    public static string CatalogPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"),
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Seed", "business-reference-data", "crm-visit-reference.json");
    }

    public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken cancellationToken)
    {
        ValidateCalls++;
        if (Unavailable)
        {
            return Task.FromResult(new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value));
        }

        var ok = _permissive || Find(value) is { IsActive: true };
        return Task.FromResult(new ReferenceValidationResult(
            ok ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue, setCode, value));
    }

    public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(
        string setCode, string value, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
        }

        if (_permissive)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>
            {
                ["applies_to"] = "cancel,missed,reschedule", ["requires_note"] = "false"
            });
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>?>(Find(value)?.Attributes);
    }

    public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
        => Task.FromResult(Unavailable
            ? ReferenceSetSnapshot.NotPublished(setCode)
            : new ReferenceSetSnapshot(setCode, true, Values
                .Select(v => new ReferenceValueSnapshot(v.Code, v.DisplayName, null, v.IsActive, !v.IsActive, v.Attributes))
                .ToList()));

    public VisitReasonValidator Validator() => new(this, this);

    private Value? Find(string code) => Values.FirstOrDefault(v => string.Equals(v.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>WP-VW-W2 — the atomic reschedule write, in memory: the report write + the new visit insert land in the two
/// fake stores together; <see cref="FailVisitInsert"/> simulates the visit insert failing (nothing is kept).</summary>
internal sealed class FakeVisitRescheduleUnitOfWork : IVisitRescheduleUnitOfWork
{
    private readonly FakeVisitReportRepository _reports;
    private readonly FakePlannedVisitReadRepository _plans;

    public FakeVisitRescheduleUnitOfWork(FakeVisitReportRepository reports, FakePlannedVisitReadRepository plans)
    {
        _reports = reports;
        _plans = plans;
    }

    public bool FailVisitInsert { get; set; }

    public int Calls { get; private set; }

    public Task<bool> SubmitWithNewVisitAsync(
        Domain.Entities.VisitReport report, int? expectedVersion, Domain.Entities.PlannedVisit newVisit,
        CancellationToken cancellationToken)
    {
        Calls++;
        if (FailVisitInsert)
        {
            throw new InvalidOperationException("simulated planned_visits insert failure");
        }

        if (expectedVersion is { } version)
        {
            var stored = _reports.Items.FirstOrDefault(r => r.Id == report.Id);
            if (stored is null || stored.Version != version)
            {
                return Task.FromResult(false);
            }

            report.Version = version + 1;
            _reports.Items[_reports.Items.IndexOf(stored)] = report;
        }
        else
        {
            _reports.Items.Add(report);
        }

        _plans.Items.Add(newVisit);
        return Task.FromResult(true);
    }
}
