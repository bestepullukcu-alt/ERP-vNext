using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Audit.Services;

/// <summary>
/// BL-347 — records that a person took tenant data OUT of the system as a file: who, which tenant's data, from
/// which feature, in which format, how many rows, under which filters.
///
/// <para><b>⚠ WHY THIS IS NOT <see cref="IAuditMetaAuditWriter"/> WITH ONE MORE PARAMETER.</b> That writer
/// records the audit module watching itself: every event is <c>IsMetaAudit</c>, <c>SourceModule = "Audit"</c>,
/// platform-global, and a failed write is swallowed because a meta-audit must never break the platform audit
/// screens. A tenant export differs in FIVE of those properties, not one — actor, tenant ownership, source
/// module, meta flag and failure policy — so an actor-type parameter alone would still write a platform-global
/// meta-audit record about tenant data. And a parameter puts the actor type in the CALLER's hand, which is the
/// exact mistake BL-347 is about.</para>
///
/// <para><b>⚠ WHY THIS IS NOT A QUERY-SIDE <c>IAuditableRequest</c> THROUGH <c>AuditBehavior</c>.</b> Measured
/// 2026-09-15: <c>AuditBehavior.AppendAuditAsync</c> never sets <see cref="AuditAppendRequest.ActorType"/>, so
/// every command it audits is recorded with the request's default, <see cref="AuditActorType.System"/>. Routing
/// an export through it would write "the system did it" — a wrong record again. The row count is also only known
/// after the handler has read the rows, and the behaviour builds its metadata from the request before that.
/// Changing either would change what every audited command records.</para>
///
/// <para><b>Callers:</b> MOD-0024 work report export (first) and MOD-0357 S12 meeting report export (designed
/// for, not built). Both are tenant routes, both need exactly the fields on <see cref="DataExportAuditEntry"/>.</para>
/// </summary>
public interface IDataExportAuditWriter
{
    /// <summary>
    /// Appends one <see cref="AuditCategory.DataExport"/> record for the current principal and tenant.
    ///
    /// <para><b>⚠ THE CALLER MUST HONOUR <see cref="DataExportAuditResult.IsRecorded"/>.</b> A GxP export with no
    /// trace is not acceptable: when it is <c>false</c> the file must not be handed out. This method never
    /// throws for an audit failure (only for cancellation), so a caller cannot lose the answer to an exception
    /// handler somewhere above it.</para>
    /// </summary>
    Task<DataExportAuditResult> RecordAsync(DataExportAuditEntry entry, CancellationToken ct = default);
}

/// <summary>
/// What was exported. ⚠ There is deliberately NO actor, NO tenant and NO "platform-global" field: all three come
/// from the authenticated request, never from the feature that is exporting.
/// </summary>
/// <param name="SourceModule">The exporting feature's module id, e.g. <c>MOD-0024</c>.</param>
/// <param name="RequestType">A stable name for the export, e.g. <c>Tasks.WorkReportExportQuery</c>.</param>
/// <param name="EntityType">What the rows are, e.g. <c>WorkReport</c>.</param>
/// <param name="Format">The normalized file format actually written (<c>csv</c>, <c>json</c>).</param>
/// <param name="RowCount">Rows in the file that is about to be handed out.</param>
/// <param name="Filters">
/// The filter summary. ⚠ NO PERSONAL DATA: a filter whose value names a person is written through
/// <see cref="DataExportFilterSummary.Person"/>, which records THAT it was applied and never WHOSE it was. Null
/// values are dropped, so the record lists only the filters that were in force.
/// </param>
/// <param name="Dataset">The grain, when one export endpoint serves several (S12: meetings | decisions | actions).</param>
/// <param name="RequestCorrelationId">The HTTP correlation id, so the record can be joined to request logs.</param>
public sealed record DataExportAuditEntry(
    string SourceModule,
    string RequestType,
    string EntityType,
    string Format,
    int RowCount,
    IReadOnlyDictionary<string, string?> Filters,
    string? Dataset = null,
    string? RequestCorrelationId = null);

/// <param name="IsRecorded">True only when the record reached the audit outbox (<see cref="AuditAppendStatus.Queued"/>).</param>
/// <param name="ActorType">The actor type resolved from the principal — <see cref="AuditActorType.Unknown"/> when none could be.</param>
/// <param name="AppendStatus">The audit service's answer, or null when the writer refused before asking it.</param>
/// <param name="Diagnostic">Why nothing was recorded. For logs only — never shown to a user.</param>
public sealed record DataExportAuditResult(
    bool IsRecorded,
    AuditActorType ActorType,
    AuditAppendStatus? AppendStatus,
    string? Diagnostic);

/// <summary>Filter-summary helpers shared by every caller, so "no personal data" is one rule, not one per feature.</summary>
public static class DataExportFilterSummary
{
    /// <summary>The only value a person-valued filter is ever recorded with.</summary>
    public const string Applied = "applied";

    /// <summary>
    /// A filter that names a PERSON (assignee, organizer, attendee) — recorded as <see cref="Applied"/> when
    /// present, dropped when absent. The id itself never reaches the record.
    /// </summary>
    public static string? Person(Guid? personId) => personId is null ? null : Applied;
}

/// <summary>Reason codes an export handler answers with when its audit record could not be written.</summary>
public static class DataExportAuditReasonCodes
{
    /// <summary>
    /// The export was REFUSED because its audit record could not be written. No new user-facing sentence: the
    /// screens already answer any failure that is not their own named refusal with their generic export-failed
    /// message (work report: <c>ExportFailed</c>).
    /// </summary>
    public const string NotRecorded = "DATA_EXPORT_AUDIT_NOT_RECORDED";
}

/// <inheritdoc />
public sealed class DataExportAuditWriter : IDataExportAuditWriter
{
    private const string SourceService = "Diten.Platform";

    private readonly IAuditService _auditService;
    private readonly ITenantAuthorizationContext _principal;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DataExportAuditWriter> _logger;

    public DataExportAuditWriter(
        IAuditService auditService,
        ITenantAuthorizationContext principal,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        ILogger<DataExportAuditWriter> logger)
    {
        _auditService = auditService;
        _principal = principal;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DataExportAuditResult> RecordAsync(DataExportAuditEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        /*
         * ⚠ FROM THE TOKEN, AND ONLY FROM THE TOKEN. AuthService mints `actor_type` on every access token
         * (TokenService: tenant_user / platform_admin / partner_admin). A principal without a recognised one is
         * not guessed into "tenant user": an unattributable export is refused, because a wrong actor on a GxP
         * record is worse than no record — and no record is not acceptable either.
         */
        var actorType = ResolveActorType();
        if (actorType == AuditActorType.Unknown)
        {
            return NotRecorded(entry, actorType, null, "The exporting principal carries no recognised actor type.");
        }

        var actorId = _currentUser.UserId;
        if (actorId == Guid.Empty)
        {
            return NotRecorded(entry, actorType, null, "The exporting principal carries no user id.");
        }

        /*
         * ⚠ THE TENANT IS THE REQUEST'S, NEVER THE CALLER'S. The record belongs to the tenant whose data left the
         * system; with no resolved tenant there is no owner to file it under, and the platform-system tenant is
         * NOT a fallback — that is exactly the platform-global stamping BL-347 exists to stop.
         */
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return NotRecorded(entry, actorType, null, "No tenant owns the exported data.");
        }

        var tenantId = _tenantContext.TenantId;

        AuditAppendResult append;
        try
        {
            append = await _auditService.AppendAsync(new AuditAppendRequest
            {
                // A FRESH id per export: the idempotency key is built from it, and reusing the HTTP correlation id
                // would let a retried request's second file be de-duplicated into the first file's record.
                CorrelationId = Guid.NewGuid(),
                RequestType = entry.RequestType,
                ActorType = actorType,
                ActorId = actorId,
                ActorEmail = _currentUser.Email,
                ActorDisplayName = _currentUser.DisplayName ?? _currentUser.ActorName,
                TargetTenantId = tenantId,
                Category = AuditCategory.DataExport,
                EntityType = entry.EntityType,
                Operation = AuditOperation.Export,
                Outcome = AuditOutcome.Succeeded,
                Metadata = BuildMetadata(entry),
                SourceService = SourceService,
                SourceModule = entry.SourceModule,
                IsMetaAudit = false,
                IsPlatformGlobal = false
            }, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return NotRecorded(entry, actorType, null, $"Audit append threw. ErrorType={ex.GetType().Name}");
        }

        return append.Status == AuditAppendStatus.Queued
            ? new DataExportAuditResult(true, actorType, append.Status, null)
            : NotRecorded(entry, actorType, append.Status, append.Diagnostic);
    }

    private AuditActorType ResolveActorType()
    {
        if (!_principal.IsAuthenticated)
        {
            return AuditActorType.Unknown;
        }

        // The same vocabulary PlatformEntitlementAuditSink maps, minus its System fallback: see RecordAsync.
        return _principal.ActorType?.Trim().ToLowerInvariant() switch
        {
            "tenant_user" => AuditActorType.TenantUser,
            "platform_admin" => AuditActorType.PlatformAdministrator,
            "partner_admin" => AuditActorType.PartnerAdministrator,
            _ => AuditActorType.Unknown
        };
    }

    private static IReadOnlyDictionary<string, object?> BuildMetadata(DataExportAuditEntry entry)
    {
        var filters = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (name, value) in entry.Filters)
        {
            if (value is not null)
            {
                filters[name] = value;
            }
        }

        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["format"] = entry.Format,
            ["rowCount"] = entry.RowCount,
            ["filters"] = filters
        };

        if (!string.IsNullOrWhiteSpace(entry.Dataset))
        {
            metadata["dataset"] = entry.Dataset;
        }

        if (!string.IsNullOrWhiteSpace(entry.RequestCorrelationId))
        {
            metadata["requestCorrelationId"] = entry.RequestCorrelationId;
        }

        return metadata;
    }

    private DataExportAuditResult NotRecorded(
        DataExportAuditEntry entry,
        AuditActorType actorType,
        AuditAppendStatus? status,
        string? diagnostic)
    {
        // No actor, tenant or filter values in the log line — the diagnostic strings above carry none either.
        _logger.LogWarning(
            "Data export audit record was NOT written; the export must be refused. SourceModule={SourceModule} "
            + "RequestType={RequestType} ActorType={ActorType} Status={Status} Diagnostic={Diagnostic}",
            entry.SourceModule,
            entry.RequestType,
            actorType,
            status,
            diagnostic);

        return new DataExportAuditResult(false, actorType, status, diagnostic);
    }
}
