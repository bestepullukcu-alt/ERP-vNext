using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Audit;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 — everything one audit record says, before it becomes the <c>audit_outbox</c> payload.
/// Actor e-mail / display name / IP are the RAW values; <see cref="AuditOutboxPayload.Build"/> masks them.
/// </summary>
public sealed record AuditCanonicalRecord(
    Guid TenantId,
    Guid CorrelationId,
    string RequestType,
    AuditActorType ActorType,
    Guid? ActorId,
    string? ActorEmail,
    string? ActorDisplayName,
    Guid? TargetTenantId,
    AuditCategory Category,
    string EntityType,
    Guid? EntityId,
    AuditOperation Operation,
    AuditOutcome Outcome,
    IReadOnlyDictionary<string, object?>? BeforeState,
    IReadOnlyDictionary<string, object?>? AfterState,
    IReadOnlyDictionary<string, object?>? Metadata,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset? OccurredAtUtc,
    string SourceService,
    string? SourceModule,
    bool IsMetaAudit);

/// <summary>
/// THE ONE PLACE that knows what an <c>audit_outbox</c> payload looks like — the keys the outbox mapper
/// (<c>AuditOutboxPayloadMapper</c>) reads to build the <c>audit_events</c> row. Two doors use it and nothing else may
/// hand-write a payload: the central pipeline (<see cref="AuditService"/>, best effort, after the handler) and the
/// in-transaction door (<see cref="CanonicalTransactionalAuditOutboxWriter"/>, same transaction as the business data).
///
/// <para>Before this the in-transaction callers each wrote two or three free fields (<c>Outcome</c>, <c>ModuleCode</c>,
/// …); the mapper found no <c>TenantId</c> / <c>ActorType</c> / <c>Category</c> / <c>SourceService</c> and every such
/// row went to dead letter — not one reached <c>audit_events</c> (measured on dev 2026-10-02: 7 of 7).</para>
///
/// <para>Redaction and masking are the same for both doors: before/after/metadata through
/// <see cref="ISensitiveFieldRedactor"/>, e-mail / display name / IP masked here.</para>
/// </summary>
public static class AuditOutboxPayload
{
    public static IReadOnlyDictionary<string, object?> Build(AuditCanonicalRecord record, ISensitiveFieldRedactor redactor)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(redactor);

        return new Dictionary<string, object?>
        {
            ["TenantId"] = record.TenantId,
            ["CorrelationId"] = record.CorrelationId,
            ["RequestType"] = record.RequestType.Trim(),
            ["ActorType"] = record.ActorType.ToString(),
            ["ActorId"] = record.ActorId,
            ["ActorEmailMasked"] = MaskEmail(record.ActorEmail),
            ["ActorDisplayNameMasked"] = MaskDisplayName(record.ActorDisplayName),
            ["TargetTenantId"] = record.TargetTenantId,
            ["Category"] = record.Category.ToString(),
            ["EntityType"] = record.EntityType.Trim(),
            ["EntityId"] = record.EntityId,
            ["Operation"] = record.Operation.ToString(),
            ["Outcome"] = record.Outcome.ToString(),
            ["BeforeState"] = record.BeforeState is null ? null : redactor.RedactDictionary(record.BeforeState),
            ["AfterState"] = record.AfterState is null ? null : redactor.RedactDictionary(record.AfterState),
            ["Metadata"] = redactor.RedactDictionary(record.Metadata),
            ["IpAddressMasked"] = MaskIpAddress(record.IpAddress),
            ["UserAgent"] = record.UserAgent,
            ["OccurredAtUtc"] = record.OccurredAtUtc ?? DateTimeOffset.UtcNow,
            ["SourceService"] = record.SourceService.Trim(),
            ["SourceModule"] = record.SourceModule,
            ["IsMetaAudit"] = record.IsMetaAudit,
            ["RedactionStatus"] = AuditRedactionStatus.SensitiveFieldsRedacted.ToString()
        };
    }

    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        var atIndex = trimmed.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0)
        {
            return MaskDisplayName(trimmed);
        }

        var local = trimmed[..atIndex];
        var domain = trimmed[(atIndex + 1)..];
        return $"{local[0]}***@{domain}";
    }

    public static string? MaskDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 1)
        {
            return "*";
        }

        return trimmed.Length == 2
            ? $"{trimmed[0]}*"
            : $"{trimmed[0]}***{trimmed[^1]}";
    }

    public static string? MaskIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var trimmed = ipAddress.Trim();
        var ipv4Parts = trimmed.Split('.');
        if (ipv4Parts.Length == 4)
        {
            return $"{ipv4Parts[0]}.{ipv4Parts[1]}.{ipv4Parts[2]}.0";
        }

        var colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
        return colonIndex > 0 ? $"{trimmed[..colonIndex]}:****" : "[REDACTED]";
    }
}
