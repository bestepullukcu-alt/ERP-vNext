using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.RecordCodes;
using Diten.Platform.Application.Features.RecordCodes.Commands;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Observability;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.RecordCodes;
using Diten.Platform.Domain.Features.RecordCodes;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.RecordCodes;

/// <summary>
/// MOD-0040-FU02 DL-2 §8.14 — the in-process port. It holds NO rule of its own: it builds the DL-1
/// <see cref="AllocateRecordCodeCommand"/> from the request's verified principal and sends it, so the allocation handler's
/// steps 4–13 and its refusal record run exactly as they do for the internal endpoint. What it adds is only what an HTTP
/// caller gets from the executor: the tenant / actor defence (answered 403 before anything is sent — no allocation, no
/// refusal row, the central create row records it) and the permission verdict.
///
/// <para>No rate limiter: it guards against a leaked credential, and this path has none — every call is a
/// <c>[HasPermission]</c>-gated, user-started create (pack §8.14, named decision).</para>
/// </summary>
public sealed class InProcessRecordCodeAllocator : IInProcessRecordCodeAllocator
{
    /// <summary>The audit source service of every in-process allocation — already in the door's closed set.</summary>
    public const string CallerService = "Diten.Platform";

    private const string TenantUserActorType = "tenant_user";

    private readonly ISender _sender;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantAuthorizationContext _authorization;
    private readonly IActorPermissionContext _permissions;
    private readonly ICorrelationContext? _correlation;

    /// <param name="correlation">Optional (F-14): the type is registered only by the API's observability setup, and the
    /// container validation builds the Application without it (the <c>CanonicalTransactionalAuditOutboxWriter</c> precedent).</param>
    public InProcessRecordCodeAllocator(
        ISender sender,
        ITenantContext tenantContext,
        ITenantAuthorizationContext authorization,
        IActorPermissionContext permissions,
        ICorrelationContext? correlation = null)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _correlation = correlation;
    }

    public async Task<InProcessRecordCodeResult> AllocateAsync(InProcessRecordCodeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The collision-skip attempt starts at 1.");
        }

        // The tenant the record is written in must be the token's tenant, and the actor a tenant user with a subject —
        // the middleware already enforces both on these routes; this is the defence that never reaches the allocation.
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        if (tenantId == Guid.Empty
            || tenantId != _authorization.TenantId
            || _authorization.UserId == Guid.Empty
            || !string.Equals(_authorization.ActorType, TenantUserActorType, StringComparison.OrdinalIgnoreCase))
        {
            return new InProcessRecordCodeResult(false, null, null, 403, RecordCodeReasonCodes.Forbidden);
        }

        var known = RecordCodeTypeCatalog.TryGet(request.RecordType, out var definition);
        IReadOnlySet<string> allowed = known && string.Equals(definition.AllowedCaller, CallerService, StringComparison.Ordinal)
            ? new HashSet<string>(StringComparer.Ordinal) { request.RecordType }
            : new HashSet<string>(StringComparer.Ordinal);
        var correlationId = string.IsNullOrWhiteSpace(_correlation?.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : _correlation!.CorrelationId!;

        var response = await _sender.Send(
            new AllocateRecordCodeCommand(
                tenantId,
                _authorization.UserId,
                CallerService,
                allowed,
                new AllocateRecordCodeRequest(
                    request.RecordType, RecordCodePrefixScope.LegalEntity, request.LegalEntityId, IdempotencyKey(request)),
                [],
                HoldsRequiredPermission(known ? definition.RequiredPermission : null, _permissions),
                correlationId),
            ct);

        return response.IsSuccessful && response.Data is { } allocated
            ? new InProcessRecordCodeResult(true, allocated.Code, allocated.Sequence, response.StatusCode, null)
            : new InProcessRecordCodeResult(false, null, null, response.StatusCode, response.ReasonCode);
    }

    /// <summary>
    /// Step 8's verdict: the catalogue key, asked of the same evaluator the controller's <c>[HasPermission]</c> uses. A blank
    /// key is CLOSED here — <see cref="IActorPermissionContext.Has"/> answers true for it (field-level authorization wants
    /// that), and an in-process type whose key is blank must not become an open door (pack §8.14, F-9).
    /// </summary>
    internal static bool HoldsRequiredPermission(string? key, IActorPermissionContext permissions) =>
        !string.IsNullOrWhiteSpace(key) && permissions.Has(key);

    /// <summary><c>organization-unit:{id:N}</c> / <c>position:{id:N}</c>; attempts 2 and 3 append <c>:{n}</c> (§8.14, F-8).</summary>
    internal static string IdempotencyKey(InProcessRecordCodeRequest request)
    {
        var root = request.RecordType switch
        {
            "ORG" => "organization-unit",
            "POS" => "position",
            _ => request.RecordType.ToLowerInvariant()
        };
        var key = $"{root}:{request.RecordId:N}";
        return request.Attempt == 1 ? key : $"{key}:{request.Attempt}";
    }
}

/// <summary>
/// MOD-0040-FU02 DL-2 §8.15 (Q-1 A) — legacy hand-typed codes are kept, so an automatic code can land on one that already
/// exists (deleted records included: a number is never issued twice, RCS-001 §5.8). The creating handler asks its own
/// collection; a taken code is skipped — it stays allocated-but-unused in the ledger, a gap — and a NEW allocation is made
/// under the next attempt's key. At most three attempts; three taken codes answer 409 <c>RECORD_CODE_COLLISION</c>.
/// </summary>
public static class InProcessRecordCodeCollisionSkip
{
    public const int MaximumAttempts = 3;

    public static async Task<InProcessRecordCodeResult> AllocateAsync(
        IInProcessRecordCodeAllocator allocator,
        string recordType,
        Guid legalEntityId,
        Guid recordId,
        Func<string, CancellationToken, Task<bool>> codeIsTaken,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(codeIsTaken);
        ArgumentNullException.ThrowIfNull(logger);
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            var result = await allocator.AllocateAsync(
                new InProcessRecordCodeRequest(recordType, legalEntityId, recordId, attempt), ct);
            if (!result.IsAllocated || !await codeIsTaken(result.Code!, ct))
            {
                return result;
            }

            logger.LogWarning(
                "Record code {Code} allocated for {RecordType} {RecordId} is already held by an existing record (deleted ones "
                + "included); skipped, the number stays a gap (attempt {Attempt} of {MaximumAttempts}).",
                result.Code, recordType, recordId, attempt, MaximumAttempts);
        }

        return new InProcessRecordCodeResult(false, null, null, 409, RecordCodeReasonCodes.Collision);
    }
}
