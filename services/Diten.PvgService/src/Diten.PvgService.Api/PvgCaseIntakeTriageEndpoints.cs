using System.Text.Json;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;

namespace Diten.PvgService.Api;

public static class PvgCaseIntakeTriageEndpoints
{
    public const string RoutePrefix = "/api/v1/pv-case-intake-triage";
    private static readonly HashSet<string> ReservedIntakeDraftIdSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "export",
        "archive",
        "void",
        "bulk",
        "bulk-delete",
        "delete"
    };

    public static IEndpointRouteBuilder MapPvgCaseIntakeTriageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RoutePrefix, CreateDraftAsync).WithName("PvgCaseIntakeTriageCreateDraft");
        endpoints.MapPut($"{RoutePrefix}/{{intakeDraftId}}", UpdateDraftAsync).WithName("PvgCaseIntakeTriageUpdateDraft");
        endpoints.MapGet(RoutePrefix, ListDraftsAsync).WithName("PvgCaseIntakeTriageListDrafts");
        endpoints.MapGet($"{RoutePrefix}/{{intakeDraftId}}", GetDraftByIdAsync).WithName("PvgCaseIntakeTriageGetDraftById");
        endpoints.MapPost($"{RoutePrefix}/{{intakeDraftId}}/triage", TriageDraftAsync).WithName("PvgCaseIntakeTriageTriageDraft");
        endpoints.MapPost($"{RoutePrefix}/{{intakeDraftId}}/route", RouteDraftAsync).WithName("PvgCaseIntakeTriageRouteDraft");

        return endpoints;
    }

    public static async ValueTask<IResult> CreateDraftAsync(
        PvgCaseIntakeCreateRequest request,
        HttpContext httpContext,
        PvgIntakeDraftApplicationService service,
        CancellationToken cancellationToken)
    {
        var context = PvgCaseIntakeRequestContext.From(httpContext);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.CreateDraftAsync(
            new CreateIntakeDraftCommand(
                context.TenantContext!,
                context.ActorContext!,
                context.CorrelationContext!,
                request.ToApplicationRequest()),
            request.RequestShapeFailures(),
            cancellationToken);

        return ToHttpResult(PvgCaseIntakeApiResponse.From(result));
    }

    public static async ValueTask<IResult> UpdateDraftAsync(
        string intakeDraftId,
        PvgCaseIntakeUpdateRequest request,
        HttpContext httpContext,
        PvgIntakeDraftApplicationService service,
        CancellationToken cancellationToken)
    {
        if (IsReservedIntakeDraftIdSegment(intakeDraftId))
        {
            return Results.NotFound();
        }

        var context = PvgCaseIntakeRequestContext.From(httpContext);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.UpdateDraftAsync(
            new UpdateIntakeDraftCommand(
                context.TenantContext!,
                context.ActorContext!,
                context.CorrelationContext!,
                intakeDraftId,
                request.ToApplicationRequest(),
                request.ExpectedVersion),
            request.RequestShapeFailures(),
            cancellationToken);

        return ToHttpResult(PvgCaseIntakeApiResponse.From(result));
    }

    public static async ValueTask<IResult> ListDraftsAsync(
        HttpContext httpContext,
        PvgIntakeDraftApplicationService service,
        int pageNumber = 1,
        int pageSize = 25,
        PvgIntakeStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var context = PvgCaseIntakeRequestContext.From(httpContext);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.ListDraftsAsync(
            new GetIntakeDraftListQuery(
                context.TenantContext!,
                context.ActorContext!,
                context.CorrelationContext!,
                pageNumber,
                pageSize,
                status),
            cancellationToken);

        return ToHttpResult(PvgCaseIntakeApiResponse.From(result));
    }

    public static async ValueTask<IResult> GetDraftByIdAsync(
        string intakeDraftId,
        HttpContext httpContext,
        PvgIntakeDraftApplicationService service,
        CancellationToken cancellationToken)
    {
        if (IsReservedIntakeDraftIdSegment(intakeDraftId))
        {
            return Results.NotFound();
        }

        var context = PvgCaseIntakeRequestContext.From(httpContext);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.GetDraftByIdAsync(
            new GetIntakeDraftByIdQuery(
                context.TenantContext!,
                context.ActorContext!,
                context.CorrelationContext!,
                intakeDraftId),
            cancellationToken);

        return ToHttpResult(PvgCaseIntakeApiResponse.From(result));
    }

    public static async ValueTask<IResult> TriageDraftAsync(
        string intakeDraftId,
        PvgCaseIntakeTriageRequest request,
        HttpContext httpContext,
        PvgIntakeDraftApplicationService service,
        CancellationToken cancellationToken)
    {
        if (IsReservedIntakeDraftIdSegment(intakeDraftId))
        {
            return Results.NotFound();
        }

        var context = PvgCaseIntakeRequestContext.From(httpContext);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.TriageDraftAsync(
            new TriageIntakeDraftCommand(
                context.TenantContext!,
                context.ActorContext!,
                context.CorrelationContext!,
                intakeDraftId,
                request.ToApplicationRequest(),
                request.ExpectedVersion),
            cancellationToken);

        return ToHttpResult(PvgCaseIntakeApiResponse.From(result));
    }

    public static async ValueTask<IResult> RouteDraftAsync(
        string intakeDraftId,
        PvgCaseIntakeRouteRequest request,
        HttpContext httpContext,
        PvgIntakeDraftApplicationService service,
        CancellationToken cancellationToken)
    {
        if (IsReservedIntakeDraftIdSegment(intakeDraftId))
        {
            return Results.NotFound();
        }

        var context = PvgCaseIntakeRequestContext.From(httpContext);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.RouteDraftAsync(
            new RouteIntakeDraftCommand(
                context.TenantContext!,
                context.ActorContext!,
                context.CorrelationContext!,
                intakeDraftId,
                request.ToApplicationRequest(),
                request.ExpectedVersion),
            cancellationToken);

        return ToHttpResult(PvgCaseIntakeApiResponse.From(result));
    }

    private static IResult ToHttpResult(PvgCaseIntakeApiResponse response)
    {
        return response.Outcome switch
        {
            nameof(PvgApplicationOutcome.Succeeded) => Results.Ok(response),
            nameof(PvgApplicationOutcome.Invalid) => Results.BadRequest(response),
            _ => Results.Json(response, statusCode: StatusCodes.Status409Conflict)
        };
    }

    private static bool IsReservedIntakeDraftIdSegment(string? intakeDraftId) =>
        !string.IsNullOrWhiteSpace(intakeDraftId) &&
        ReservedIntakeDraftIdSegments.Contains(intakeDraftId.Trim());
}

public sealed record PvgCaseIntakeCreateRequest(
    string? IntakeChannel,
    string? SourceType,
    string? SourceReference,
    DateTimeOffset? ReceivedAtUtc,
    string? ReporterType,
    string? ReporterContactSummary,
    string? PatientSubjectCode,
    DateOnly? EventOnsetDate,
    string? AdverseEventNarrative,
    IReadOnlyList<PvgIntakeProductLineRequest>? ProductLines,
    string? Seriousness,
    string? IntakePriority,
    IReadOnlyList<string>? EvidenceLinkReferences,
    DateTimeOffset? AwarenessAtUtc = null,
    string? CompanyRecipientPersonId = null,
    string? EventCountry = null,
    string? ReportType = null,
    string? ReporterSelectionDirectory = null,
    string? ReporterSelectionId = null,
    string? ReporterNameOrInitials = null,
    string? ReporterProfession = null,
    string? ReporterCountry = null,
    bool? RemoveReporterLink = null,
    JsonElement? ValidityResult = null,
    JsonElement? ValidityMissingCriteria = null,
    JsonElement? ReporterSource = null,
    JsonElement? ReporterSourceId = null,
    JsonElement? ReporterCrmSearchState = null)
{
    public PvgCreateIntakeDraftRequest ToApplicationRequest() =>
        new(
            IntakeChannel,
            SourceType,
            SourceReference,
            ReceivedAtUtc,
            ReporterType,
            ReporterContactSummary,
            PatientSubjectCode,
            EventOnsetDate,
            AdverseEventNarrative,
            ProductLines,
            Seriousness,
            IntakePriority,
            EvidenceLinkReferences,
            AwarenessAtUtc,
            CompanyRecipientPersonId,
            EventCountry,
            ReportType,
            ReporterSelectionDirectory,
            ReporterSelectionId,
            ReporterNameOrInitials,
            ReporterProfession,
            ReporterCountry);

    // Response-only fields and a link removal on a record that has no link are request shape errors; the
    // application service reports them after the permission gate (pack AC-7).
    public IReadOnlyList<PvgValidationFailure> RequestShapeFailures()
    {
        var failures = PvgCaseIntakeRequestShape.ResponseOnlyFieldFailures(
            ValidityResult,
            ValidityMissingCriteria,
            ReporterSource,
            ReporterSourceId,
            ReporterCrmSearchState);
        if (RemoveReporterLink == true)
        {
            failures.Add(new PvgValidationFailure(
                PvgIntakeField.RemoveReporterLink,
                PvgValidationReasonCodes.ReporterLinkAbsent));
        }

        return failures;
    }
}

public sealed record PvgCaseIntakeUpdateRequest(
    string? IntakeChannel,
    string? SourceType,
    string? SourceReference,
    DateTimeOffset? ReceivedAtUtc,
    string? ReporterType,
    string? ReporterContactSummary,
    string? PatientSubjectCode,
    DateOnly? EventOnsetDate,
    string? AdverseEventNarrative,
    IReadOnlyList<PvgIntakeProductLineRequest>? ProductLines,
    string? Seriousness,
    string? IntakePriority,
    IReadOnlyList<string>? EvidenceLinkReferences,
    DateTimeOffset? AwarenessAtUtc = null,
    string? CompanyRecipientPersonId = null,
    string? EventCountry = null,
    string? ReportType = null,
    string? ReporterSelectionDirectory = null,
    string? ReporterSelectionId = null,
    string? ReporterNameOrInitials = null,
    string? ReporterProfession = null,
    string? ReporterCountry = null,
    bool? RemoveReporterLink = null,
    long? ExpectedVersion = null,
    JsonElement? ValidityResult = null,
    JsonElement? ValidityMissingCriteria = null,
    JsonElement? ReporterSource = null,
    JsonElement? ReporterSourceId = null,
    JsonElement? ReporterCrmSearchState = null)
{
    public PvgUpdateIntakeDraftRequest ToApplicationRequest() =>
        new(
            IntakeChannel,
            SourceType,
            SourceReference,
            ReceivedAtUtc,
            ReporterType,
            ReporterContactSummary,
            PatientSubjectCode,
            EventOnsetDate,
            AdverseEventNarrative,
            ProductLines,
            Seriousness,
            IntakePriority,
            EvidenceLinkReferences,
            AwarenessAtUtc,
            CompanyRecipientPersonId,
            EventCountry,
            ReportType,
            ReporterSelectionDirectory,
            ReporterSelectionId,
            ReporterNameOrInitials,
            ReporterProfession,
            ReporterCountry,
            RemoveReporterLink);

    public IReadOnlyList<PvgValidationFailure> RequestShapeFailures() =>
        PvgCaseIntakeRequestShape.ResponseOnlyFieldFailures(
            ValidityResult,
            ValidityMissingCriteria,
            ReporterSource,
            ReporterSourceId,
            ReporterCrmSearchState);
}

public sealed record PvgCaseIntakeTriageRequest(
    PvgTriageOutcome? TriageOutcome,
    string? TriageReasonCode,
    string? TriageReason,
    long? ExpectedVersion = null)
{
    public PvgTriageIntakeDraftRequest ToApplicationRequest() =>
        new(TriageOutcome, TriageReasonCode, TriageReason);
}

public sealed record PvgCaseIntakeRouteRequest(string? RouteTargetQueue, long? ExpectedVersion = null)
{
    public PvgRouteIntakeDraftRequest ToApplicationRequest() => new(RouteTargetQueue);
}

internal static class PvgCaseIntakeRequestShape
{
    // JSON null is "not sent"; any other value (of any JSON type) on a response-only field is rejected.
    private static bool IsCarried(JsonElement? value) =>
        value.HasValue && value.Value.ValueKind != JsonValueKind.Null;

    public static List<PvgValidationFailure> ResponseOnlyFieldFailures(
        JsonElement? validityResult,
        JsonElement? validityMissingCriteria,
        JsonElement? reporterSource,
        JsonElement? reporterSourceId,
        JsonElement? reporterCrmSearchState)
    {
        var failures = new List<PvgValidationFailure>();
        if (IsCarried(validityResult))
        {
            failures.Add(new PvgValidationFailure(null, PvgValidationReasonCodes.FieldValueInvalid));
        }

        if (IsCarried(validityMissingCriteria))
        {
            failures.Add(new PvgValidationFailure(null, PvgValidationReasonCodes.FieldValueInvalid));
        }

        if (IsCarried(reporterSource))
        {
            failures.Add(new PvgValidationFailure(PvgIntakeField.ReporterSource, PvgValidationReasonCodes.FieldValueInvalid));
        }

        if (IsCarried(reporterSourceId))
        {
            failures.Add(new PvgValidationFailure(PvgIntakeField.ReporterSourceId, PvgValidationReasonCodes.FieldValueInvalid));
        }

        if (IsCarried(reporterCrmSearchState))
        {
            failures.Add(new PvgValidationFailure(PvgIntakeField.ReporterCrmSearchState, PvgValidationReasonCodes.FieldValueInvalid));
        }

        return failures;
    }
}

public sealed record PvgCaseIntakeDraftResponse(
    string IntakeDraftId,
    string Status,
    long Version,
    IReadOnlyList<PvgIntakeProductLine> ProductLines,
    DateTimeOffset AwarenessAtUtc,
    string? CompanyRecipientPersonId,
    string EventCountry,
    string ReportType,
    string ReporterSource,
    string? ReporterSourceId,
    string ReporterCrmSearchState,
    string? ReporterNameOrInitials,
    string? ReporterProfession,
    string? ReporterCountry)
{
    public static PvgCaseIntakeDraftResponse From(PvgIntakeDraftSummary summary, SafetyCaseIntake intake) =>
        new(
            summary.IntakeDraftId,
            intake.Status.ToString(),
            summary.Version,
            intake.ProductLines,
            intake.AwarenessAtUtc,
            intake.CompanyRecipientPersonId,
            intake.EventCountry,
            intake.ReportType,
            intake.ReporterSource.ToString(),
            intake.ReporterSourceId,
            intake.ReporterCrmSearchState.ToString(),
            intake.ReporterNameOrInitials,
            intake.ReporterProfession,
            intake.ReporterCountry);
}

public sealed record PvgCaseIntakeApiResponse(
    string Outcome,
    string? ReasonCode,
    IReadOnlyList<string> ValidationReasonCodes,
    string? IntakeDraftId,
    IReadOnlyList<PvgIntakeDraftSummary> Items,
    long? Version = null,
    int? TotalCount = null,
    int? FilteredCount = null,
    PvgCaseIntakeDraftResponse? Draft = null)
{
    public static PvgCaseIntakeApiResponse Blocked(string reasonCode) =>
        new(nameof(PvgApplicationOutcome.Blocked), reasonCode, [], null, []);

    public static PvgCaseIntakeApiResponse From(PvgIntakeDraftMutationResult result) =>
        new(
            result.Result.Outcome.ToString(),
            result.Result.ReasonCode,
            result.Result.ValidationFailures.Select(failure => failure.ReasonCode).Distinct().ToArray(),
            result.IntakeDraftId,
            [],
            result.Version);

    public static PvgCaseIntakeApiResponse From(PvgIntakeDraftQueryResult result) =>
        new(
            result.Result.Outcome.ToString(),
            result.Result.ReasonCode,
            result.Result.ValidationFailures.Select(failure => failure.ReasonCode).Distinct().ToArray(),
            null,
            result.Items,
            null,
            result.TotalCount,
            result.FilteredCount,
            result.Intake is { } intake && result.Items.Count == 1
                ? PvgCaseIntakeDraftResponse.From(result.Items[0], intake)
                : null);
}

public sealed record PvgCaseIntakeRequestContext(
    bool IsValid,
    string ReasonCode,
    PvgServerTenantContext? TenantContext,
    PvgActorContext? ActorContext,
    PvgCorrelationContext? CorrelationContext)
{
    public const string TenantContextHeader = "X-Diten-Tenant-Context";
    public const string ActorIdHeader = "X-Diten-Actor-Id";
    public const string ActorKindHeader = "X-Diten-Actor-Kind";
    public const string CorrelationIdHeader = "X-Correlation-Id";

    public static PvgCaseIntakeRequestContext From(
        HttpContext httpContext,
        bool requireActor = true,
        bool requireCorrelation = true)
    {
        var tenantReference = Header(httpContext, TenantContextHeader);
        if (string.IsNullOrWhiteSpace(tenantReference))
        {
            return Blocked(PvgValidationReasonCodes.TenantContextRequired);
        }

        PvgActorContext? actorContext = null;
        if (requireActor)
        {
            var actorId = Header(httpContext, ActorIdHeader);
            var actorKind = Header(httpContext, ActorKindHeader);
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(actorKind))
            {
                return Blocked(PvgPermissionReasonCodes.ActorContextRequired);
            }

            actorContext = new PvgActorContext(actorId, actorKind);
        }

        PvgCorrelationContext? correlationContext = null;
        if (requireCorrelation)
        {
            var correlationId = Header(httpContext, CorrelationIdHeader);
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                return Blocked(PvgPermissionReasonCodes.CorrelationContextRequired);
            }

            if (correlationId.Length > 128 || correlationId.Any(char.IsWhiteSpace))
            {
                return Blocked(PvgPermissionReasonCodes.CorrelationContextInvalid);
            }

            correlationContext = new PvgCorrelationContext(correlationId);
        }

        return new PvgCaseIntakeRequestContext(
            true,
            string.Empty,
            new PvgServerTenantContext(tenantReference),
            actorContext,
            correlationContext);
    }

    private static PvgCaseIntakeRequestContext Blocked(string reasonCode) =>
        new(false, reasonCode, null, null, null);

    private static string? Header(HttpContext httpContext, string name)
    {
        return httpContext.Request.Headers.TryGetValue(name, out StringValues values)
            ? values.FirstOrDefault()?.Trim()
            : null;
    }
}
