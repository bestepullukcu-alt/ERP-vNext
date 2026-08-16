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
                request.ToApplicationRequest()),
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
        var context = PvgCaseIntakeRequestContext.From(httpContext, requireActor: false, requireCorrelation: false);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.ListDraftsAsync(
            new GetIntakeDraftListQuery(context.TenantContext!, pageNumber, pageSize, status),
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

        var context = PvgCaseIntakeRequestContext.From(httpContext, requireActor: false, requireCorrelation: false);
        if (!context.IsValid)
        {
            return ToHttpResult(PvgCaseIntakeApiResponse.Blocked(context.ReasonCode));
        }

        var result = await service.GetDraftByIdAsync(
            new GetIntakeDraftByIdQuery(context.TenantContext!, intakeDraftId),
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
                request.ToApplicationRequest()),
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
                request.ToApplicationRequest()),
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
    string? SuspectProductText,
    string? Seriousness,
    string? IntakePriority,
    IReadOnlyList<string>? EvidenceLinkReferences)
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
            SuspectProductText,
            Seriousness,
            IntakePriority,
            EvidenceLinkReferences);
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
    string? SuspectProductText,
    string? Seriousness,
    string? IntakePriority,
    IReadOnlyList<string>? EvidenceLinkReferences)
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
            SuspectProductText,
            Seriousness,
            IntakePriority,
            EvidenceLinkReferences);
}

public sealed record PvgCaseIntakeTriageRequest(
    PvgTriageOutcome? TriageOutcome,
    string? TriageReasonCode,
    string? TriageReason)
{
    public PvgTriageIntakeDraftRequest ToApplicationRequest() =>
        new(TriageOutcome, TriageReasonCode, TriageReason);
}

public sealed record PvgCaseIntakeRouteRequest(string? RouteTargetQueue)
{
    public PvgRouteIntakeDraftRequest ToApplicationRequest() => new(RouteTargetQueue);
}

public sealed record PvgCaseIntakeApiResponse(
    string Outcome,
    string? ReasonCode,
    IReadOnlyList<string> ValidationReasonCodes,
    string? IntakeDraftId,
    IReadOnlyList<PvgIntakeDraftSummary> Items)
{
    public static PvgCaseIntakeApiResponse Blocked(string reasonCode) =>
        new(nameof(PvgApplicationOutcome.Blocked), reasonCode, [], null, []);

    public static PvgCaseIntakeApiResponse From(PvgIntakeDraftMutationResult result) =>
        new(
            result.Result.Outcome.ToString(),
            result.Result.ReasonCode,
            result.Result.ValidationFailures.Select(failure => failure.ReasonCode).Distinct().ToArray(),
            result.IntakeDraftId,
            []);

    public static PvgCaseIntakeApiResponse From(PvgIntakeDraftQueryResult result) =>
        new(
            result.Result.Outcome.ToString(),
            result.Result.ReasonCode,
            result.Result.ValidationFailures.Select(failure => failure.ReasonCode).Distinct().ToArray(),
            null,
            result.Items);
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
