using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Domain.Entities;
using ExecutionEntity = Diten.CrmService.Domain.Entities.VisitExecution;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitExecution;

/// <summary>
/// WP-VW-W3 (BE-a) — the pure rules of a visit execution: the location verdict, the capture checks, the policy bounds and
/// the client view. No I/O, no clock: the handlers pass everything in, so each rule is tested on its own.
/// </summary>
public static class VisitExecutionRules
{
    public sealed record Failure(string Message, string Code, int StatusCode, string? Detail = null)
    {
        public IReadOnlyList<string> Errors => Detail is null ? new[] { Message, Code } : new[] { Message, Code, Detail };
    }

    // ── location ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The institution the visit is measured against: a doctor's visit → the plan's institution; an
    /// institution / pharmacy visit → the target itself.</summary>
    public static Guid? AccountOf(PlanAtom plan)
        => PlannedVisitTargetType.Normalize(plan.TargetType) is PlannedVisitTargetType.Account or PlannedVisitTargetType.Pharmacy
            ? plan.TargetId
            : plan.AccountId;

    /// <summary>Great-circle distance in whole metres (the route engine's Haversine).</summary>
    public static int DistanceMeters(double lat1, double lng1, double lat2, double lng2)
        => (int)Math.Round(HaversineTravelModel.HaversineKm(new GeoPoint(lat1, lng1), new GeoPoint(lat2, lng2)) * 1000d);

    public sealed record LocationVerdict(VisitExecutionLocation Location, IReadOnlyList<string> Flags, Failure? Refusal);

    /// <summary>
    /// The start's location rule (§0 CT 1–3): the institution has no coordinates → <c>allow_flagged</c> (start + flag) or
    /// <c>block</c> (409); no fix / a fix worse than <c>maxAccuracyMeters</c> / farther than <c>radiusMeters</c> → 409
    /// in <c>required</c> mode, recorded + flagged in <c>info</c> mode. A fix with no accuracy is treated as inaccurate
    /// (fail-closed: its quality is unknown). No device exemption — the device type is the client's claim.
    /// </summary>
    public static LocationVerdict EvaluateLocation(
        VisitEvidencePolicy policy, double? accountLat, double? accountLng,
        double? lat, double? lng, double? accuracy, string? locationError)
    {
        var hasFix = lat is not null && lng is not null;
        var location = new VisitExecutionLocation
        {
            Latitude = hasFix ? lat : null,
            Longitude = hasFix ? lng : null,
            AccuracyMeters = hasFix ? accuracy : null,
            RadiusMeters = policy.RadiusMeters,
            Mode = policy.LocationMode,
            Error = hasFix ? null : Normalize(locationError)
        };
        var required = string.Equals(policy.LocationMode, VisitLocationModes.Required, StringComparison.Ordinal);

        if (accountLat is null || accountLng is null)
        {
            location.Status = VisitLocationStatus.AccountUnknown;
            if (string.Equals(policy.AccountWithoutLocation, AccountWithoutLocationModes.Block, StringComparison.Ordinal))
            {
                return new LocationVerdict(location, Array.Empty<string>(), new Failure(
                    "The institution has no location on record, and this tenant does not start a visit without one.",
                    VisitExecutionErrorCodes.AccountLocationUnknown, 409));
            }

            return new LocationVerdict(location, new[] { VisitExecutionFlags.AccountLocationUnknown }, null);
        }

        if (!hasFix)
        {
            location.Status = VisitLocationStatus.Missing;
            return required
                ? new LocationVerdict(location, Array.Empty<string>(), new Failure(
                    "Your location is required to start the visit.", VisitExecutionErrorCodes.LocationRequired, 409))
                : new LocationVerdict(location, new[] { VisitExecutionFlags.LocationMissing }, null);
        }

        location.DistanceMeters = DistanceMeters(lat!.Value, lng!.Value, accountLat.Value, accountLng.Value);

        if (accuracy is null || accuracy > policy.MaxAccuracyMeters)
        {
            location.Status = VisitLocationStatus.Inaccurate;
            return required
                ? new LocationVerdict(location, Array.Empty<string>(), new Failure(
                    $"Your location is not accurate enough (at most {policy.MaxAccuracyMeters} m is accepted). Try again in the open.",
                    VisitExecutionErrorCodes.LocationInaccurate, 409))
                : new LocationVerdict(location, new[] { VisitExecutionFlags.LocationInaccurate }, null);
        }

        if (location.DistanceMeters > policy.RadiusMeters)
        {
            location.Status = VisitLocationStatus.Far;
            return required
                ? new LocationVerdict(location, Array.Empty<string>(), new Failure(
                    $"You are {location.DistanceMeters} m from the institution (at most {policy.RadiusMeters} m).",
                    VisitExecutionErrorCodes.LocationTooFar, 409,
                    VisitExecutionErrorCodes.DistancePrefix + location.DistanceMeters))
                : new LocationVerdict(location, new[] { VisitExecutionFlags.LocationFar }, null);
        }

        location.Status = VisitLocationStatus.Ok;
        return new LocationVerdict(location, Array.Empty<string>(), null);
    }

    public static Failure? ValidateCoordinates(double? lat, double? lng, double? accuracy, string? locationError)
    {
        if ((lat is null) != (lng is null)
            || lat is < -90 or > 90 || lng is < -180 or > 180
            || accuracy is < 0)
        {
            return Capture("latitude / longitude / accuracyMeters are out of range.");
        }

        return Normalize(locationError) is { } error && !VisitLocationErrors.All.Contains(error, StringComparer.Ordinal)
            ? Capture($"locationError must be one of: {string.Join(", ", VisitLocationErrors.All)}.")
            : null;
    }

    // ── evidence files ────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<string> PhotoTypes = new[] { "image/jpeg", "image/png" };
    private static readonly IReadOnlyList<string> PhotoExtensions = new[] { ".jpg", ".jpeg", ".png" };

    public static IReadOnlyList<string> PhotoMediaTypes => PhotoTypes;

    /// <summary>The Platform CrmVisitEvidence scope rule, checked before any upload: jpeg / png, at most 10 MB.</summary>
    public static Failure? ValidatePhoto(VisitEvidenceFile file)
        => IsAccepted(file, PhotoTypes, PhotoExtensions)
            ? null
            : new Failure("The photo must be a JPEG or PNG image of at most 10 MB.", VisitExecutionErrorCodes.PhotoInvalid, 400);

    public static Failure? ValidateSignatureFile(VisitEvidenceFile? file)
        => file is not null && IsAccepted(file, new[] { "image/png" }, new[] { ".png" })
            ? null
            : Capture("The signature must be a PNG image of at most 10 MB.");

    private static bool IsAccepted(VisitEvidenceFile file, IReadOnlyList<string> types, IReadOnlyList<string> extensions)
        => file.Content is { Length: > 0 }
           && file.Content.LongLength <= VisitExecutionLimits.MaxEvidenceBytes
           && types.Contains((file.MediaType ?? string.Empty).Trim().ToLowerInvariant(), StringComparer.Ordinal)
           && extensions.Contains(Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant(), StringComparer.Ordinal);

    // ── capture ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The capture shape: products are the plan's products, slides the plan's steps of that product, seconds 0–86400, the
    /// interest / flag vocabularies, note lengths, at most one entry per product (timing / mark / personal note) and per
    /// slide. The objection category and the co-visitors are checked by the handler (reference set, Auth).
    /// </summary>
    public static Failure? ValidateCapture(PlanAtom plan, VisitCaptureInput capture)
    {
        if (capture.Kind is not null && !VisitExecutionKinds.IsKnown(capture.Kind))
        {
            return Capture($"kind must be one of: {string.Join(", ", VisitExecutionKinds.All)}.");
        }

        var products = (plan.ContentItems ?? new List<PlannedVisitContentItem>())
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.SelectMany(i => i.Steps ?? new List<PlannedVisitContentStep>())
                .Select(s => s.StepId).ToHashSet());

        var timings = capture.ProductTimings ?? Array.Empty<VisitProductTimingInput>();
        if (timings.Any(t => !products.ContainsKey(t.ProductId)) || timings.Any(t => !InRange(t.Seconds))
            || timings.GroupBy(t => t.ProductId).Any(g => g.Count() > 1))
        {
            return Capture("productTimings: each product of the visit once, seconds 0–86400.");
        }

        var slides = capture.SlideTimings ?? Array.Empty<VisitSlideTimingInput>();
        if (slides.Any(s => !products.TryGetValue(s.ProductId, out var steps) || !steps.Contains(s.StepId))
            || slides.Any(s => !InRange(s.Seconds))
            || slides.GroupBy(s => (s.ProductId, s.StepId)).Any(g => g.Count() > 1))
        {
            return Capture("slideTimings: each step of the visit's products once, seconds 0–86400.");
        }

        var marks = capture.Marks ?? Array.Empty<VisitMarkInput>();
        foreach (var mark in marks)
        {
            if (!products.ContainsKey(mark.ProductId))
            {
                return Capture("marks: only the visit's products can be marked.");
            }

            if (Normalize(mark.Interest) is { } interest && !VisitInterestValues.All.Contains(interest, StringComparer.Ordinal))
            {
                return Capture($"marks.interest must be one of: {string.Join(", ", VisitInterestValues.All)}.");
            }

            if ((mark.Flags ?? Array.Empty<string>()).Any(f => Normalize(f) is not { } flag
                                                                || !VisitMarkFlags.All.Contains(flag, StringComparer.Ordinal)))
            {
                return Capture($"marks.flags must be among: {string.Join(", ", VisitMarkFlags.All)}.");
            }
        }

        if (marks.GroupBy(m => m.ProductId).Any(g => g.Count() > 1))
        {
            return Capture("marks: one entry per product.");
        }

        if (capture.Note is { Length: > VisitExecutionLimits.MaxNoteLength })
        {
            return Capture($"note must be at most {VisitExecutionLimits.MaxNoteLength} characters.");
        }

        var notes = capture.PersonalNotes ?? Array.Empty<VisitPersonalNoteInput>();
        if (notes.Any(n => !products.ContainsKey(n.ProductId))
            || notes.Any(n => (n.Text ?? string.Empty).Length > VisitExecutionLimits.MaxPersonalNoteLength)
            || notes.GroupBy(n => n.ProductId).Any(g => g.Count() > 1))
        {
            return Capture($"personalNotes: one per product of the visit, at most {VisitExecutionLimits.MaxPersonalNoteLength} characters.");
        }

        return null;
    }

    /// <summary>The distinct, non-empty objection categories a capture names (to check against the reference set).</summary>
    public static IReadOnlyList<string> ObjectionCategories(VisitCaptureInput capture)
        => (capture.Marks ?? Array.Empty<VisitMarkInput>())
            .Select(m => Normalize(m.ObjectionCategory))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>The co-visitor rule before the Auth lookup: at most 3, distinct, user ids (GUIDs), never the rep.</summary>
    public static (Failure? Failure, IReadOnlyList<Guid> Ids) ParseCoVisitors(IReadOnlyList<string>? raw, string resourceId)
    {
        var values = (raw ?? Array.Empty<string>()).Select(v => (v ?? string.Empty).Trim()).ToList();
        var ids = new List<Guid>();
        foreach (var value in values)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            {
                return (CoVisitor("coVisitorUserIds must be tenant user ids."), Array.Empty<Guid>());
            }

            ids.Add(id);
        }

        if (ids.Count > VisitExecutionLimits.MaxCoVisitors || ids.Distinct().Count() != ids.Count)
        {
            return (CoVisitor($"At most {VisitExecutionLimits.MaxCoVisitors} distinct co-visitors."), Array.Empty<Guid>());
        }

        if (Guid.TryParse(resourceId, out var me) && ids.Contains(me))
        {
            return (CoVisitor("You cannot add yourself as a co-visitor."), Array.Empty<Guid>());
        }

        return (null, ids);
    }

    /// <summary>A FULL replacement of the capture on the record (kind kept when the body leaves it out).</summary>
    public static void ApplyCapture(ExecutionEntity execution, VisitCaptureInput capture, IReadOnlyList<Guid> coVisitors)
    {
        if (Normalize(capture.Kind) is { } kind)
        {
            execution.Kind = kind;
        }

        execution.ProductTimings = (capture.ProductTimings ?? Array.Empty<VisitProductTimingInput>())
            .Select(t => new VisitExecutionProductTiming { ProductId = t.ProductId, Seconds = t.Seconds }).ToList();
        execution.SlideTimings = (capture.SlideTimings ?? Array.Empty<VisitSlideTimingInput>())
            .Select(s => new VisitExecutionSlideTiming { ProductId = s.ProductId, StepId = s.StepId, Seconds = s.Seconds }).ToList();
        execution.Marks = (capture.Marks ?? Array.Empty<VisitMarkInput>())
            .Select(m => new VisitExecutionMark
            {
                ProductId = m.ProductId,
                Interest = Normalize(m.Interest),
                Flags = (m.Flags ?? Array.Empty<string>()).Select(f => Normalize(f)!).Distinct(StringComparer.Ordinal).ToList(),
                ObjectionCategory = Normalize(m.ObjectionCategory)
            })
            .ToList();
        execution.Note = string.IsNullOrWhiteSpace(capture.Note) ? null : capture.Note.Trim();
        execution.PersonalNotes = (capture.PersonalNotes ?? Array.Empty<VisitPersonalNoteInput>())
            .Where(n => !string.IsNullOrWhiteSpace(n.Text))
            .Select(n => new VisitExecutionPersonalNote { ProductId = n.ProductId, Text = n.Text!.Trim() })
            .ToList();
        execution.CoVisitorUserIds = coVisitors.Select(id => id.ToString("D")).ToList();
    }

    /// <summary>The finish: duration from the SERVER instants, the short-visit flag when the tenant checks it.</summary>
    public static void ApplyFinish(ExecutionEntity execution, VisitEvidencePolicy policy, DateTimeOffset finishedAt)
    {
        execution.Status = VisitExecutionStatus.Finished;
        execution.FinishedAt = finishedAt;
        execution.DurationSeconds = (int)Math.Max(0, Math.Floor((finishedAt - execution.StartedAt).TotalSeconds));
        execution.ShortVisit = policy.DurationConsistency && execution.DurationSeconds < policy.ShortVisitSeconds;
        execution.Flags.Remove(VisitExecutionFlags.ShortVisit);
        if (execution.ShortVisit)
        {
            execution.Flags.Add(VisitExecutionFlags.ShortVisit);
        }
    }

    /// <summary>The signature context: <c>sample</c> when any product was marked <c>sample_request</c>.</summary>
    public static string SignatureContextOf(ExecutionEntity execution)
        => execution.Marks.Any(m => m.Flags.Contains(VisitMarkFlags.SampleRequest, StringComparer.Ordinal))
            ? VisitSignatureContexts.Sample
            : VisitSignatureContexts.Approval;

    // ── policy ───────────────────────────────────────────────────────────────────────────────────────────────────

    public static VisitEvidencePolicy Defaults(Guid tenantId) => new() { TenantId = tenantId, Id = Guid.Empty };

    /// <summary>Applies a PUT onto <paramref name="target"/> (absent fields keep their value) and checks the bounds.</summary>
    public static Failure? ApplyPolicy(VisitEvidencePolicy target, VisitEvidencePolicyInput input)
    {
        string? Pick(string? value, IReadOnlyList<string> allowed, string current, out bool bad)
        {
            bad = false;
            if (Normalize(value) is not { } v)
            {
                return current;
            }

            bad = !allowed.Contains(v, StringComparer.Ordinal);
            return v;
        }

        target.LocationMode = Pick(input.LocationMode, VisitLocationModes.All, target.LocationMode, out var b1)!;
        target.PhotoMode = Pick(input.PhotoMode, VisitPhotoModes.All, target.PhotoMode, out var b2)!;
        target.AccountWithoutLocation = Pick(input.AccountWithoutLocation, AccountWithoutLocationModes.All, target.AccountWithoutLocation, out var b3)!;
        target.RadiusMeters = input.RadiusMeters ?? target.RadiusMeters;
        target.MaxAccuracyMeters = input.MaxAccuracyMeters ?? target.MaxAccuracyMeters;
        target.ShortVisitSeconds = input.ShortVisitSeconds ?? target.ShortVisitSeconds;
        target.DoctorConfirmation = input.DoctorConfirmation ?? target.DoctorConfirmation;
        target.DurationConsistency = input.DurationConsistency ?? target.DurationConsistency;
        // WP-VW-W3E (W3E-BE-a) — off | optional | required
        target.QrMode = Pick(input.QrMode, VisitQrModes.All, target.QrMode, out var b4)!;

        if (b1 || b2 || b3 || b4
            || target.RadiusMeters is < VisitEvidencePolicyDefaults.MinRadiusMeters or > VisitEvidencePolicyDefaults.MaxRadiusMeters
            || target.MaxAccuracyMeters is < VisitEvidencePolicyDefaults.MinAccuracyMeters or > VisitEvidencePolicyDefaults.MaxAccuracyMetersBound
            || target.ShortVisitSeconds is < VisitEvidencePolicyDefaults.MinShortVisitSeconds or > VisitEvidencePolicyDefaults.MaxShortVisitSeconds)
        {
            return new Failure(
                "Policy out of bounds: locationMode required|info, photoMode required|optional, accountWithoutLocation " +
                "allow_flagged|block, radiusMeters 50–5000, maxAccuracyMeters 10–1000, shortVisitSeconds 30–1800, " +
                "qrMode off|optional|required.",
                VisitExecutionErrorCodes.PolicyInvalid, 400);
        }

        return null;
    }

    public static VisitEvidencePolicyDto ToDto(VisitEvidencePolicy policy, bool isDefault)
        => new(policy.LocationMode, policy.RadiusMeters, policy.MaxAccuracyMeters, policy.PhotoMode,
            policy.DoctorConfirmation, policy.DurationConsistency, policy.ShortVisitSeconds, policy.AccountWithoutLocation,
            isDefault, policy.Version, policy.UpdatedBy, policy.UpdatedAt, QrMode: policy.QrMode);

    // ── the client view ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The DTO. Personal notes go to their AUTHOR only (<paramref name="callerResourceId"/> = the rep); every
    /// other reader gets an empty list. Evidence content ids are never mapped.</summary>
    public static VisitExecutionDto ToDto(
        ExecutionEntity e, DateOnly plannedDate, string? callerResourceId, IReadOnlyDictionary<Guid, string>? names = null)
    {
        var own = callerResourceId is not null && string.Equals(callerResourceId, e.ResourceId, StringComparison.Ordinal);
        return new VisitExecutionDto(
            e.Id, e.PlannedVisitId, e.ResourceId, e.Status, e.Kind, e.StartedAt, e.FinishedAt, e.DurationSeconds,
            new VisitExecutionLocationDto(e.Location.Latitude, e.Location.Longitude, e.Location.AccuracyMeters,
                e.Location.DistanceMeters, e.Location.RadiusMeters, e.Location.Mode, e.Location.Status),
            e.Photo is null
                ? new VisitExecutionPhotoDto(false, null, null, null)
                : new VisitExecutionPhotoDto(true, e.Photo.MediaType, e.Photo.ByteSize, e.Photo.CapturedAt),
            e.Signature is null
                ? new VisitExecutionSignatureDto(false, null, null, null)
                : new VisitExecutionSignatureDto(true, e.Signature.Confirmed, e.Signature.Context, e.Signature.SignedAt),
            e.ProductTimings.Select(t => new VisitProductTimingDto(t.ProductId, t.Seconds)).ToList(),
            e.SlideTimings.Select(s => new VisitSlideTimingDto(s.ProductId, s.StepId, s.Seconds)).ToList(),
            e.Marks.Select(m => new VisitMarkDto(m.ProductId, m.Interest, m.Flags.ToList(), m.ObjectionCategory)).ToList(),
            e.Note,
            own
                ? e.PersonalNotes.Select(n => new VisitPersonalNoteDto(n.ProductId, n.Text)).ToList()
                : Array.Empty<VisitPersonalNoteDto>(),
            e.CoVisitorUserIds
                .Select(id => new VisitCoVisitorDto(id,
                    Guid.TryParse(id, out var g) && names is not null && names.TryGetValue(g, out var name) ? name : null))
                .ToList(),
            e.ShortVisit,
            e.Flags.ToList(),
            Diten.CrmService.Application.Features.VisitReport.VisitReportDeadline.For(plannedDate),
            e.ExceptionId,
            e.RelaxedRules.ToList(),
            e.Qr is null ? null : new VisitExecutionQrDto(e.Qr.Status, e.Qr.AccountId, e.Qr.Version, e.Qr.ScannedAt, e.Qr.MatchedVia));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static bool InRange(int seconds) => seconds is >= 0 and <= VisitExecutionLimits.MaxSeconds;

    public static Failure Capture(string message) => new(message, VisitExecutionErrorCodes.CaptureInvalid, 400);

    private static Failure CoVisitor(string message) => new(message, VisitExecutionErrorCodes.CoVisitorInvalid, 400);
}
