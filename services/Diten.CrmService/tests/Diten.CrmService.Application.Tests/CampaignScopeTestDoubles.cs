using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU09 — shared read-only doubles for the campaign scope write path. They live in one file so the FU04, FU08
/// and FU09 suites cannot drift into three slightly different versions of "what a governed set answers".
/// </summary>
internal static class CampaignScopeTestDoubles
{
    /// <summary>A reference-data seam whose published sets are declared per test.</summary>
    internal sealed class FakeReferenceValidator : IReferenceDataValidator
    {
        private readonly Dictionary<string, HashSet<string>?> _sets = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Publishes a set with these values. A set that is never published answers <c>SetMissing</c>.</summary>
        public FakeReferenceValidator Publish(string setCode, params string[] values)
        {
            _sets[setCode] = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            return this;
        }

        public int Calls { get; private set; }

        public Task<ReferenceValidationResult> ValidateAsync(
            string setCode, string value, CancellationToken cancellationToken)
        {
            Calls++;
            if (!_sets.TryGetValue(setCode, out var values) || values is null)
            {
                return Task.FromResult(new ReferenceValidationResult(
                    ReferenceValidationStatus.SetMissing, setCode, value));
            }

            return Task.FromResult(new ReferenceValidationResult(
                values.Contains(value) ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue,
                setCode,
                value));
        }
    }

    /// <summary>
    /// MOD-0165 FU10 — a segment catalogue holding a declared set of segments. Like the real seam it has NO write
    /// member at all, which is itself part of the boundary the FU10 tests assert.
    /// </summary>
    internal sealed class FakeSegmentCatalog : ICampaignSegmentCatalog
    {
        public List<CampaignSegmentRef> Segments { get; } = new();
        public int GetByIdsCalls { get; private set; }

        public Task<IReadOnlyList<CampaignSegmentRef>> GetByIdsAsync(
            IReadOnlyCollection<Guid> segmentIds, CancellationToken ct)
        {
            GetByIdsCalls++;
            return Task.FromResult<IReadOnlyList<CampaignSegmentRef>>(
                Segments.Where(s => segmentIds.Contains(s.SegmentId)).ToList());
        }

        public Task<IReadOnlyList<CampaignSegmentRef>> ListSelectableAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CampaignSegmentRef>>(
                Segments.Where(s => s.SegmentStatus == SegmentStatuses.Active).ToList());

        /// <summary>Adds a segment at a declared status.</summary>
        public Guid Add(
            string code,
            string subjectType = SegmentSubjectTypes.Contact,
            string status = SegmentStatuses.Active,
            bool superseded = false)
        {
            var id = Guid.NewGuid();
            Segments.Add(new CampaignSegmentRef(id, code, code, subjectType, status, superseded, Guid.NewGuid(), 1));
            return id;
        }
    }

    /// <summary>A code sequence that simply counts, for deterministic generated codes.</summary>
    internal sealed class FakeCampaignCodeSequence : Diten.CrmService.Domain.Repositories.ICampaignCodeSequenceRepository
    {
        private long _current;

        public int Calls { get; private set; }

        public int PeekCalls { get; private set; }

        public Task<long> NextAsync(Guid tenantId, int year, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(++_current);
        }

        /// <summary>Reads without consuming — the counter is untouched, and PeekCalls is tracked separately so a test
        /// can assert that peeking never moved it.</summary>
        public Task<long> PeekNextAsync(Guid tenantId, int year, CancellationToken ct)
        {
            PeekCalls++;
            return Task.FromResult(_current + 1);
        }
    }

    /// <summary>An MDM legal-entity validator whose verdict is declared per test.</summary>
    internal sealed class FakeLegalEntityValidator : ICyclePeriodLegalEntityValidator
    {
        public CyclePeriodLegalEntityValidation Verdict { get; set; } = CyclePeriodLegalEntityValidation.Valid;

        public Task<CyclePeriodLegalEntityValidation> ValidateAsync(Guid legalEntityId, CancellationToken ct)
            => Task.FromResult(Verdict);
    }

    /// <summary>
    /// A cycle-period read seam holding a declared set of period snapshots. It has NO write member at all, which is
    /// itself part of the boundary the FU08/FU09 tests assert.
    /// </summary>
    internal sealed class FakeCyclePeriodReader : ICyclePeriodReader
    {
        public List<CyclePeriodSnapshot> Periods { get; } = new();
        public int GetByIdCalls { get; private set; }
        public int GetByIdsCalls { get; private set; }
        public int ListByYearCalls { get; private set; }

        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution(
                CyclePeriodResolutionOutcomes.None, null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
        {
            GetByIdCalls++;
            return Task.FromResult(Periods.FirstOrDefault(p => p.CyclePeriodId == cyclePeriodId));
        }

        /// <summary>Narrows exactly the way the real seam does: by year, then by the (type, ref) address.</summary>
        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
            int year, string? scopeType, string? scopeRef, CancellationToken ct)
        {
            ListByYearCalls++;
            var rows = Periods.Where(p => p.Year == year);

            if (!string.IsNullOrWhiteSpace(scopeType))
            {
                rows = rows.Where(p =>
                    string.Equals(p.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.ScopeRef?.Trim() ?? string.Empty, scopeRef?.Trim() ?? string.Empty,
                        StringComparison.OrdinalIgnoreCase));
            }

            return Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(rows.ToList());
        }

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
            IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken ct)
        {
            GetByIdsCalls++;
            return Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(
                Periods.Where(p => cyclePeriodIds.Contains(p.CyclePeriodId)).ToList());
        }

        /// <summary>Adds a period at an explicit address.</summary>
        public Guid Add(
            string cycleCode,
            string scopeType,
            string? scopeRef,
            DateTimeOffset start,
            DateTimeOffset end,
            string status = CyclePeriodStatuses.Active,
            int year = 2026)
        {
            var id = Guid.NewGuid();
            Periods.Add(new CyclePeriodSnapshot(
                id, cycleCode, cycleCode, year, 1, start, end, status, scopeType, scopeRef,
                scopeType == CyclePeriodScopeTypes.Country ? scopeRef : null,
                scopeType == CyclePeriodScopeTypes.LegalEntity && Guid.TryParse(scopeRef, out var le) ? le : null,
                scopeType == CyclePeriodScopeTypes.BusinessUnit ? scopeRef : null));
            return id;
        }
    }
}
