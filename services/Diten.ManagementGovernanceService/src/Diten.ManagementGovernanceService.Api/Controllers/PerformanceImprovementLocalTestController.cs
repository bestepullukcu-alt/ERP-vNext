using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Features.ProcessModelingLocalTest;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DwsEnvelope = Diten.ManagementGovernanceService.Application.Features.Dws;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
[Route("internal/local-test/v1/process-modeling/improvements")]
[Authorize(AuthenticationSchemes = LocalTestAuthenticationHandler.SchemeName)]
public sealed class PerformanceImprovementLocalTestController(
    PerformanceImprovementCoordinator coordinator,
    PerformanceImprovementContract.IQueryStore queries,
    TimeProvider clock) : CustomBaseController
{
    private static readonly JsonSerializerOptions StrictJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var context = Context(PerformanceImprovementPermissions.ImprovementRead);
        if (context.Error is not null) return context.Error;
        return FromResponse(DwsEnvelope.Response<object>.Success(await queries.ListAsync(context.TenantId, cancellationToken)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var context = Context(PerformanceImprovementPermissions.ImprovementRead);
        if (context.Error is not null) return context.Error;
        var value = await queries.GetAsync(context.TenantId, id, cancellationToken);
        return value is null
            ? FromResponse(DwsEnvelope.Response<object>.Fail("performance_improvement_not_found", 404))
            : FromResponse(DwsEnvelope.Response<object>.Success(value));
    }

    [HttpPost]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        Mutate<CreateBody>(PerformanceImprovementContract.Command.CreateOpportunity,
            (body, context, raw) => BuildRequest(PerformanceImprovementContract.Command.CreateOpportunity, context, raw, Guid.NewGuid(), body.ProcessModelVersionId,
                title: body.Title, problem: body.ProblemStatement, expected: body.ExpectedOutcome, priority: body.PriorityRank),
            cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, CancellationToken cancellationToken) =>
        Mutate<UpdateBody>(PerformanceImprovementContract.Command.UpdateOpportunity,
            (body, context, raw) => BuildRequest(PerformanceImprovementContract.Command.UpdateOpportunity, context, raw, id, Guid.Empty, body.ExpectedVersion,
                title: body.Title, problem: body.ProblemStatement, expected: body.ExpectedOutcome, priority: body.PriorityRank),
            cancellationToken);

    [HttpPost("{id:guid}/qualify")]
    public Task<IActionResult> Qualify(Guid id, CancellationToken cancellationToken) =>
        Mutate<VersionBody>(PerformanceImprovementContract.Command.QualifyOpportunity,
            (body, context, raw) => BuildRequest(PerformanceImprovementContract.Command.QualifyOpportunity, context, raw, id, Guid.Empty, body.ExpectedVersion), cancellationToken);

    [HttpPost("{id:guid}/outcomes")]
    public Task<IActionResult> RecordOutcome(Guid id, CancellationToken cancellationToken) =>
        Mutate<OutcomeBody>(PerformanceImprovementContract.Command.RecordOutcome,
            (body, context, raw) => BuildRequest(PerformanceImprovementContract.Command.RecordOutcome, context, raw, id, Guid.Empty, body.ExpectedVersion,
                outcomeId: Guid.NewGuid(), summary: body.Summary), cancellationToken);

    [HttpPost("{id:guid}/close")]
    public Task<IActionResult> Close(Guid id, CancellationToken cancellationToken) =>
        Mutate<VersionBody>(PerformanceImprovementContract.Command.CloseOpportunity,
            (body, context, raw) => BuildRequest(PerformanceImprovementContract.Command.CloseOpportunity, context, raw, id, Guid.Empty, body.ExpectedVersion), cancellationToken);

    private async Task<IActionResult> Mutate<TBody>(
        PerformanceImprovementContract.Command command,
        Func<TBody, TrustedContext, byte[], PerformanceImprovementContract.MutationRequest> factory,
        CancellationToken cancellationToken)
    {
        var permission = PerformanceImprovementPermissions.RequiredFor(command);
        var context = Context(permission);
        if (context.Error is not null) return context.Error;
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var key) || key.Count != 1 || string.IsNullOrWhiteSpace(key[0]))
            return FromResponse(DwsEnvelope.Response<object>.Fail("idempotency_key_required", 400));
        var raw = await ReadBody(cancellationToken);
        TBody? body;
        try { body = JsonSerializer.Deserialize<TBody>(raw, StrictJson); }
        catch (JsonException) { return FromResponse(DwsEnvelope.Response<object>.Fail("performance_improvement_invalid", 400)); }
        if (body is null) return FromResponse(DwsEnvelope.Response<object>.Fail("performance_improvement_invalid", 400));
        context = context with { IdempotencyKey = key[0]! };
        var result = await coordinator.ExecuteAsync(factory(body, context, raw), cancellationToken);
        return result.Success
            ? FromResponse(DwsEnvelope.Response<object>.Success(new { id = result.AggregateId, outcome = result.Code }, result.StatusCode))
            : FromResponse(DwsEnvelope.Response<object>.Fail(result.Code, result.StatusCode));
    }

    private TrustedContext Context(string permission)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(User.FindFirstValue("sub"), out var actor) || actor == Guid.Empty)
            return TrustedContext.Rejected(FromResponse(DwsEnvelope.Response<object>.Fail("performance_improvement_unauthenticated", 401)));
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var header) || header.Count != 1 ||
            !Guid.TryParse(header[0], out var headerTenant) || headerTenant != tenant)
            return TrustedContext.Rejected(FromResponse(DwsEnvelope.Response<object>.Fail("performance_improvement_tenant_mismatch", 400)));
        if (!User.FindAll("permission").Any(x => string.Equals(x.Value, permission, StringComparison.Ordinal)))
            return TrustedContext.Rejected(FromResponse(DwsEnvelope.Response<object>.Fail("performance_improvement_permission_denied", 403)));
        return new(tenant, actor, permission, string.Empty, null);
    }

    private PerformanceImprovementContract.MutationRequest BuildRequest(
        PerformanceImprovementContract.Command command, TrustedContext context, byte[] raw, Guid aggregateId, Guid versionId, int expectedVersion = 0,
        Guid? outcomeId = null, string? title = null, string? problem = null, string? expected = null,
        int? priority = null, string? summary = null) =>
        new(context.TenantId, context.ActorId, aggregateId, versionId, outcomeId,
            command, context.Permission, context.IdempotencyKey,
            ProcessModelingLocalRequestHash.Compute(
                ExactMethod(command), ExactResolvedPath(command, aggregateId), ExactCommandName(command),
                context.TenantId, context.ActorId, context.Permission, raw),
            expectedVersion, title, problem, expected, priority, summary, clock.GetUtcNow().UtcDateTime);

    private static string ExactMethod(PerformanceImprovementContract.Command command) => command switch
    {
        PerformanceImprovementContract.Command.UpdateOpportunity => "PUT",
        _ => "POST"
    };

    private static string ExactResolvedPath(PerformanceImprovementContract.Command command, Guid aggregateId)
    {
        const string root = "/internal/local-test/v1/process-modeling/improvements";
        return command switch
        {
            PerformanceImprovementContract.Command.CreateOpportunity => root,
            PerformanceImprovementContract.Command.UpdateOpportunity => $"{root}/{aggregateId:D}",
            PerformanceImprovementContract.Command.QualifyOpportunity => $"{root}/{aggregateId:D}/qualify",
            PerformanceImprovementContract.Command.RecordOutcome => $"{root}/{aggregateId:D}/outcomes",
            PerformanceImprovementContract.Command.CloseOpportunity => $"{root}/{aggregateId:D}/close",
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }

    private static string ExactCommandName(PerformanceImprovementContract.Command command) => command switch
    {
        PerformanceImprovementContract.Command.CreateOpportunity => "CreateProcessImprovementOpportunityCommand",
        PerformanceImprovementContract.Command.UpdateOpportunity => "UpdateProcessImprovementOpportunityCommand",
        PerformanceImprovementContract.Command.QualifyOpportunity => "QualifyProcessImprovementOpportunityCommand",
        PerformanceImprovementContract.Command.RecordOutcome => "RecordProcessImprovementOutcomeCommand",
        PerformanceImprovementContract.Command.CloseOpportunity => "CloseProcessImprovementOpportunityCommand",
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private async Task<byte[]> ReadBody(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await HttpContext.Request.Body.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private sealed record TrustedContext(Guid TenantId, Guid ActorId, string Permission, string IdempotencyKey, IActionResult? Error)
    {
        public static TrustedContext Rejected(IActionResult error) => new(Guid.Empty, Guid.Empty, string.Empty, string.Empty, error);
    }

    private sealed record CreateBody([property: JsonRequired] Guid ProcessModelVersionId, [property: JsonRequired] string Title,
        [property: JsonRequired] string ProblemStatement, string? ExpectedOutcome, int? PriorityRank);
    private sealed record UpdateBody([property: JsonRequired] string Title, [property: JsonRequired] string ProblemStatement,
        string? ExpectedOutcome, int? PriorityRank, [property: JsonRequired] int ExpectedVersion);
    private sealed record VersionBody([property: JsonRequired] int ExpectedVersion);
    private sealed record OutcomeBody([property: JsonRequired] string Summary, [property: JsonRequired] int ExpectedVersion);
}
