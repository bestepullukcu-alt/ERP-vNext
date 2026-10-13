using System.Globalization;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.DoctorInsights;

/// <summary>The profile codes a report suggests (null = not suggested, the stored value stays).</summary>
public sealed record DoctorProfileSuggestion(string? AdoptionTypologyCode, string? CommunicationStyleCode, string? KolLevelCode);

/// <summary>One indication row as written (the report / the API).</summary>
public sealed record DoctorPotentialRowInput(Guid IndicationNodeId, Guid? ProfileNodeId, int MonthlyPatients);

/// <summary>
/// One product of a report: <see cref="Rows"/> / <see cref="NoPatients"/> null = the numbers were not asked this time
/// (not due and not updated — the stored ones stay); <see cref="ShareBucket"/> null = not given.
/// </summary>
public sealed record DoctorProductInsightInput(Guid ProductId, IReadOnlyList<DoctorPotentialRowInput>? Rows, bool? NoPatients, string? ShareBucket);

/// <summary>The outcome of a record / a check: <see cref="Ok"/>, or the coded failure (<c>[message, code]</c> + HTTP status).</summary>
public sealed record DoctorInsightsResult(bool Ok, string? Code, string? Message, int Status)
{
    public static readonly DoctorInsightsResult Success = new(true, null, null, 200);

    public static DoctorInsightsResult Fail(string code, string message, int status = 400) => new(false, code, message, status);
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2) — the seam the visit report (BE-a) writes the doctor's insights through on SUBMIT: the
/// profile suggestion, the doctor's monthly total, and per product the indication rows / "no patients" and the share.
/// It CHECKS first (codes in their BRD sets, products, rows of the product's Concept Graph, the total rule) and writes
/// nothing on a failure; every change appends a history row with the source <c>report:{visitReportId}</c>.
/// <see cref="CheckFromReportAsync"/> is the same check without writing (the report's submit validation).
/// </summary>
public interface IDoctorInsightsRecorder
{
    Task<DoctorInsightsResult> CheckFromReportAsync(Guid tenantId, Guid contactId, DoctorProfileSuggestion? profileSuggestion,
        int? totalPatients, IReadOnlyList<DoctorProductInsightInput> perProduct, CancellationToken ct);

    Task<DoctorInsightsResult> RecordFromReportAsync(Guid tenantId, Guid contactId, Guid visitReportId, string? actor,
        DoctorProfileSuggestion? profileSuggestion, int? totalPatients, IReadOnlyList<DoctorProductInsightInput> perProduct,
        CancellationToken ct);
}

/// <summary>
/// WP-VW-W4 (W4-BE-c) — the ONE writer of doctor profiles and product potentials (the report recorder and the manual
/// profile edit). Optimistic replace with one retry on a concurrent writer; histories are append-only.
/// </summary>
public sealed class DoctorInsightsRecorder : IDoctorInsightsRecorder
{
    private readonly IDoctorProfileRepository _profiles;
    private readonly IDoctorProfileChangeRepository _profileHistory;
    private readonly IDoctorProductPotentialRepository _potentials;
    private readonly IDoctorProductPotentialChangeRepository _potentialHistory;
    private readonly IReferenceDataValidator _codes;
    private readonly ProductIndicationProfileReader _indications;
    private readonly IStrategyTemplateProductReferenceValidator? _products;
    private readonly TimeProvider _clock;

    public DoctorInsightsRecorder(IDoctorProfileRepository profiles, IDoctorProfileChangeRepository profileHistory,
        IDoctorProductPotentialRepository potentials, IDoctorProductPotentialChangeRepository potentialHistory,
        IReferenceDataValidator codes, ProductIndicationProfileReader indications,
        IStrategyTemplateProductReferenceValidator? products = null, TimeProvider? clock = null)
    {
        _profiles = profiles;
        _profileHistory = profileHistory;
        _potentials = potentials;
        _potentialHistory = potentialHistory;
        _codes = codes;
        _indications = indications;
        _products = products;
        _clock = clock ?? TimeProvider.System;
    }

    public Task<DoctorInsightsResult> CheckFromReportAsync(Guid tenantId, Guid contactId, DoctorProfileSuggestion? profileSuggestion,
        int? totalPatients, IReadOnlyList<DoctorProductInsightInput> perProduct, CancellationToken ct)
        => CheckAsync(tenantId, contactId, profileSuggestion, totalPatients, perProduct, ct);

    public async Task<DoctorInsightsResult> RecordFromReportAsync(Guid tenantId, Guid contactId, Guid visitReportId, string? actor,
        DoctorProfileSuggestion? profileSuggestion, int? totalPatients, IReadOnlyList<DoctorProductInsightInput> perProduct,
        CancellationToken ct)
    {
        var check = await CheckAsync(tenantId, contactId, profileSuggestion, totalPatients, perProduct, ct);
        if (!check.Ok) return check;

        var source = DoctorInsightsSources.Report(visitReportId);
        var now = _clock.GetUtcNow();
        var suggestion = profileSuggestion ?? new DoctorProfileSuggestion(null, null, null);
        var profile = await WriteProfileAsync(tenantId, contactId, actor, source, now, null, p => new ProfileValues(
            Clean(suggestion.AdoptionTypologyCode) ?? p.AdoptionTypologyCode,
            Clean(suggestion.CommunicationStyleCode) ?? p.CommunicationStyleCode,
            Clean(suggestion.KolLevelCode) ?? p.KolLevelCode,
            totalPatients ?? p.TotalMonthlyPatients,
            totalPatients is not null), ct);
        if (!profile.Ok) return profile;

        foreach (var product in perProduct)
        {
            var written = await WritePotentialAsync(tenantId, contactId, product, actor, source, now, ct);
            if (!written.Ok) return written;
        }

        return DoctorInsightsResult.Success;
    }

    // ---------------- the manual profile edit (PUT) ----------------

    /// <summary>The full manual profile (null clears a field) with the caller's expected version (409 on a mismatch).</summary>
    public async Task<DoctorInsightsResult> UpdateProfileAsync(Guid tenantId, Guid contactId, DoctorProfileSuggestion values,
        int? totalPatients, int? expectedVersion, string? actor, CancellationToken ct)
    {
        var check = await CheckAsync(tenantId, contactId, values, totalPatients, Array.Empty<DoctorProductInsightInput>(), ct);
        if (!check.Ok) return check;

        return await WriteProfileAsync(tenantId, contactId, actor, DoctorInsightsSources.Manual, _clock.GetUtcNow(), expectedVersion,
            p => new ProfileValues(Clean(values.AdoptionTypologyCode), Clean(values.CommunicationStyleCode), Clean(values.KolLevelCode),
                totalPatients, totalPatients is not null && totalPatients != p.TotalMonthlyPatients), ct);
    }

    // ---------------- the check ----------------

    private async Task<DoctorInsightsResult> CheckAsync(Guid tenantId, Guid contactId, DoctorProfileSuggestion? profile,
        int? totalPatients, IReadOnlyList<DoctorProductInsightInput> perProduct, CancellationToken ct)
    {
        if (contactId == Guid.Empty) return DoctorInsightsResult.Fail(DoctorInsightsErrors.Invalid, "A doctor (contact id) is required.");
        if (totalPatients is < 0 or > DoctorInsightsLimits.MaxMonthlyPatients)
        {
            return DoctorInsightsResult.Fail(DoctorInsightsErrors.Invalid, $"The monthly patients are 0–{DoctorInsightsLimits.MaxMonthlyPatients}.");
        }

        if (profile is not null)
        {
            foreach (var (set, code) in new[]
                     {
                         (DoctorInsightsReferenceSets.AdoptionTypology, profile.AdoptionTypologyCode),
                         (DoctorInsightsReferenceSets.CommunicationStyle, profile.CommunicationStyleCode),
                         (DoctorInsightsReferenceSets.KolLevel, profile.KolLevelCode)
                     })
            {
                if (await CodeAsync(set, code, ct) is { } failure) return failure;
            }
        }

        if (perProduct.Select(p => p.ProductId).Distinct().Count() != perProduct.Count || perProduct.Any(p => p.ProductId == Guid.Empty))
        {
            return DoctorInsightsResult.Fail(DoctorInsightsErrors.Invalid, "Each product once, a Global Product id each.");
        }

        var stored = await _profiles.GetByContactAsync(tenantId, contactId, ct);
        var doctorTotal = totalPatients ?? stored?.TotalMonthlyPatients;
        foreach (var product in perProduct)
        {
            if (await ProductAsync(product.ProductId, ct) is { } productFailure) return productFailure;
            if (await CodeAsync(DoctorInsightsReferenceSets.ShareBucket, product.ShareBucket, ct) is { } shareFailure) return shareFailure;
            if (await RowsAsync(tenantId, product, ct) is { } rowFailure) return rowFailure;

            // The total rule on what the product will hold after this write (new rows, else the stored ones).
            int? indicationTotal = product.NoPatients == true ? 0
                : product.Rows is { Count: > 0 } rows ? rows.Sum(r => r.MonthlyPatients)
                : (await _potentials.GetAsync(tenantId, contactId, product.ProductId, ct))?.IndicationTotal();
            if (PotentialRules.ExceedsTotal(indicationTotal, doctorTotal))
            {
                return DoctorInsightsResult.Fail(DoctorInsightsErrors.PotentialExceedsTotal,
                    $"The indications of product {product.ProductId:D} add up to {indicationTotal}, more than the doctor's {doctorTotal} monthly patients.");
            }
        }

        // A lower total may not fall below a product that is not rewritten now.
        if (totalPatients is { } newTotal)
        {
            var rewritten = perProduct.Where(p => p.Rows is not null || p.NoPatients is not null).Select(p => p.ProductId).ToHashSet();
            foreach (var other in (await _potentials.ListByContactAsync(tenantId, contactId, ct)).Where(p => !rewritten.Contains(p.GlobalProductId)))
            {
                if (PotentialRules.ExceedsTotal(other.IndicationTotal(), newTotal))
                {
                    return DoctorInsightsResult.Fail(DoctorInsightsErrors.PotentialExceedsTotal,
                        $"Product {other.GlobalProductId:D} already holds {other.IndicationTotal()} monthly patients, more than the new total {newTotal}.");
                }
            }
        }

        return DoctorInsightsResult.Success;
    }

    private async Task<DoctorInsightsResult?> RowsAsync(Guid tenantId, DoctorProductInsightInput product, CancellationToken ct)
    {
        var rows = product.Rows ?? Array.Empty<DoctorPotentialRowInput>();
        if (product.NoPatients == true && rows.Count > 0)
        {
            return DoctorInsightsResult.Fail(DoctorInsightsErrors.Invalid, "\"No patients in these indications\" carries no rows.");
        }

        if (rows.Count == 0) return null;
        if (rows.Count > DoctorInsightsLimits.MaxIndicationRows
            || rows.Any(r => r.IndicationNodeId == Guid.Empty || r.ProfileNodeId == Guid.Empty
                             || r.MonthlyPatients < 0 || r.MonthlyPatients > DoctorInsightsLimits.MaxMonthlyPatients)
            || rows.Select(r => (r.IndicationNodeId, r.ProfileNodeId)).Distinct().Count() != rows.Count)
        {
            return DoctorInsightsResult.Fail(DoctorInsightsErrors.Invalid,
                $"At most {DoctorInsightsLimits.MaxIndicationRows} distinct indication rows, each 0–{DoctorInsightsLimits.MaxMonthlyPatients} patients.");
        }

        // Every row is one of the product's indication × profile rows in the Concept Graph.
        var known = (await _indications.ReadAsync(tenantId, product.ProductId, ct)).Items
            .Select(i => (i.IndicationNodeId, i.ProfileNodeId)).ToHashSet();
        var unknown = rows.FirstOrDefault(r => !known.Contains((r.IndicationNodeId, r.ProfileNodeId)));
        return unknown is null
            ? null
            : DoctorInsightsResult.Fail(DoctorInsightsErrors.Invalid,
                $"Indication {unknown.IndicationNodeId:D}{(unknown.ProfileNodeId is { } p ? " × " + p.ToString("D") : string.Empty)} is not a row of product {product.ProductId:D}.");
    }

    private async Task<DoctorInsightsResult?> CodeAsync(string set, string? code, CancellationToken ct)
    {
        if (Clean(code) is not { } value) return null;
        var verdict = await _codes.ValidateAsync(set, value, ct);
        return verdict.Status switch
        {
            ReferenceValidationStatus.SetMissing => DoctorInsightsResult.Fail(DoctorInsightsErrors.ReferenceDataUnavailable,
                $"The list '{set}' could not be read. Nothing was saved.", 503),
            ReferenceValidationStatus.InvalidValue => DoctorInsightsResult.Fail(DoctorInsightsErrors.CodeInvalid,
                $"'{value}' is not an active '{set}' value."),
            _ => null
        };
    }

    private async Task<DoctorInsightsResult?> ProductAsync(Guid productId, CancellationToken ct)
    {
        if (_products is null) return null;
        return await _products.ValidateAsync(IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct, productId, ct) switch
        {
            IStrategyTemplateProductReferenceValidator.Outcome.NotFound => DoctorInsightsResult.Fail(DoctorInsightsErrors.ProductInvalid,
                $"{productId:D} is not an MDM Global Product."),
            IStrategyTemplateProductReferenceValidator.Outcome.Unavailable => DoctorInsightsResult.Fail(DoctorInsightsErrors.ReferenceDataUnavailable,
                "The product master could not be read. Nothing was saved.", 503),
            _ => null
        };
    }

    // ---------------- the writes ----------------

    private sealed record ProfileValues(string? Typology, string? Style, string? Kol, int? Total, bool TotalGiven);

    private async Task<DoctorInsightsResult> WriteProfileAsync(Guid tenantId, Guid contactId, string? actor, string source,
        DateTimeOffset now, int? expectedVersion, Func<DoctorProfile, ProfileValues> next, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var profile = await _profiles.GetByContactAsync(tenantId, contactId, ct);
            var isNew = profile is null;
            profile ??= new DoctorProfile { TenantId = tenantId, ContactId = contactId, Version = 0 };
            if (expectedVersion is { } expected && expected != profile.Version)
            {
                return DoctorInsightsResult.Fail(DoctorInsightsErrors.VersionConflict, "The profile was changed by someone else; reload it.", 409);
            }

            var values = next(profile);
            var changes = new List<DoctorProfileChange>();
            void Track(string field, string? old, string? @new)
            {
                if (!string.Equals(old, @new, StringComparison.Ordinal))
                {
                    changes.Add(new DoctorProfileChange
                    {
                        TenantId = tenantId, ContactId = contactId, Field = field, OldValue = old, NewValue = @new, By = actor, At = now, Source = source
                    });
                }
            }

            Track("adoptionTypologyCode", profile.AdoptionTypologyCode, values.Typology);
            Track("communicationStyleCode", profile.CommunicationStyleCode, values.Style);
            Track("kolLevelCode", profile.KolLevelCode, values.Kol);
            Track("totalMonthlyPatients", Num(profile.TotalMonthlyPatients), Num(values.Total));
            if (changes.Count == 0 && !values.TotalGiven) return DoctorInsightsResult.Success;

            var version = profile.Version;
            profile.AdoptionTypologyCode = values.Typology;
            profile.CommunicationStyleCode = values.Style;
            profile.KolLevelCode = values.Kol;
            profile.TotalMonthlyPatients = values.Total;
            if (values.TotalGiven) profile.TotalPatientsAsOf = now;   // asked again (even unchanged): the three months restart
            profile.UpdatedBy = actor;
            profile.UpdatedAt = now;

            bool stored;
            if (isNew)
            {
                profile.Version = 1;
                await _profiles.InsertAsync(profile, ct);
                stored = true;
            }
            else
            {
                stored = await _profiles.ReplaceAsync(profile, version, ct);
            }

            if (stored)
            {
                await _profileHistory.InsertManyAsync(changes, ct);
                return DoctorInsightsResult.Success;
            }

            if (expectedVersion is not null) break;
        }

        return DoctorInsightsResult.Fail(DoctorInsightsErrors.VersionConflict, "The profile was changed by someone else; reload it.", 409);
    }

    private async Task<DoctorInsightsResult> WritePotentialAsync(Guid tenantId, Guid contactId, DoctorProductInsightInput input,
        string? actor, string source, DateTimeOffset now, CancellationToken ct)
    {
        var numbersGiven = input.Rows is not null || input.NoPatients is not null;
        var share = Clean(input.ShareBucket);
        if (!numbersGiven && share is null) return DoctorInsightsResult.Success;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var potential = await _potentials.GetAsync(tenantId, contactId, input.ProductId, ct);
            var isNew = potential is null;
            potential ??= new DoctorProductPotential { TenantId = tenantId, ContactId = contactId, GlobalProductId = input.ProductId, Version = 0 };

            var changes = new List<DoctorProductPotentialChange>();
            void Track(string field, string? old, string? @new)
            {
                if (!string.Equals(old, @new, StringComparison.Ordinal))
                {
                    changes.Add(new DoctorProductPotentialChange
                    {
                        TenantId = tenantId, ContactId = contactId, GlobalProductId = input.ProductId, Field = field,
                        OldValue = old, NewValue = @new, By = actor, At = now, Source = source
                    });
                }
            }

            var version = potential.Version;
            if (numbersGiven)
            {
                var noPatients = input.NoPatients == true;
                var rows = noPatients ? new List<DoctorPotentialRow>()
                    : (input.Rows ?? Array.Empty<DoctorPotentialRowInput>())
                        .Select(r => new DoctorPotentialRow { IndicationNodeId = r.IndicationNodeId, ProfileNodeId = r.ProfileNodeId, MonthlyPatients = r.MonthlyPatients })
                        .ToList();
                var oldRows = potential.IndicationRows.ToDictionary(RowKey, r => r.MonthlyPatients);
                var newRows = rows.ToDictionary(RowKey, r => r.MonthlyPatients);
                foreach (var key in oldRows.Keys.Union(newRows.Keys).OrderBy(k => k, StringComparer.Ordinal))
                {
                    Track(key, oldRows.TryGetValue(key, out var o) ? Num(o) : null, newRows.TryGetValue(key, out var n) ? Num(n) : null);
                }

                Track("noPatientsInIndications", Bool(potential.NoPatientsInIndications), Bool(noPatients));
                potential.IndicationRows = rows;
                potential.NoPatientsInIndications = noPatients;
                potential.AsOf = now;   // asked again (even unchanged): the three months restart
            }

            if (share is not null)
            {
                Track("shareBucketCode", potential.ShareBucketCode, share);
                potential.ShareBucketCode = share;
                potential.ShareAsOf = now;
            }

            potential.UpdatedBy = actor;
            potential.UpdatedAt = now;

            bool stored;
            if (isNew)
            {
                potential.Version = 1;
                await _potentials.InsertAsync(potential, ct);
                stored = true;
            }
            else
            {
                stored = await _potentials.ReplaceAsync(potential, version, ct);
            }

            if (stored)
            {
                await _potentialHistory.InsertManyAsync(changes, ct);
                return DoctorInsightsResult.Success;
            }
        }

        return DoctorInsightsResult.Fail(DoctorInsightsErrors.VersionConflict, "The potential was changed by someone else; send it again.", 409);
    }

    public static string RowKey(DoctorPotentialRow row)
        => "row:" + row.IndicationNodeId.ToString("D") + (row.ProfileNodeId is { } p ? "/" + p.ToString("D") : string.Empty);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Num(int? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";
}
