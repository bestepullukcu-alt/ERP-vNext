using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Entities.Audit;

namespace Diten.Platform.Application.Features.Audit.Services;

public static class AuditExportSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static byte[] ToCsv(IReadOnlyList<AuditEvent> events, ISensitiveFieldRedactor redactor)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', CsvHeaders));

        foreach (var auditEvent in events)
        {
            var detail = AuditEventMapper.ToDetailDto(auditEvent, redactor);
            var row = new object?[]
            {
                detail.Id,
                detail.TenantId,
                detail.TargetTenantId,
                detail.CorrelationId,
                detail.OccurredAtUtc,
                detail.WrittenAtUtc,
                detail.ActorType,
                detail.ActorId,
                detail.ActorEmailMasked,
                detail.ActorDisplayNameMasked,
                detail.Category,
                detail.EntityType,
                detail.EntityId,
                detail.Operation,
                detail.Outcome,
                detail.SourceService,
                detail.SourceModule,
                detail.IsMetaAudit,
                detail.RedactionStatus,
                SerializeField(detail.BeforeState),
                SerializeField(detail.AfterState),
                SerializeField(detail.Metadata),
                detail.IpAddressMasked,
                detail.UserAgent
            };

            builder.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static byte[] ToJson(IReadOnlyList<AuditEvent> events, ISensitiveFieldRedactor redactor)
    {
        var items = events.Select(auditEvent => AuditEventMapper.ToDetailDto(auditEvent, redactor)).ToList();
        return JsonSerializer.SerializeToUtf8Bytes(items, JsonOptions);
    }

    private static string SerializeField(object? value)
    {
        return value is null ? string.Empty : JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string EscapeCsv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTimeOffset offset => offset.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };

        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
        {
            text = "'" + text;
        }

        if (text.Contains('"', StringComparison.Ordinal)
            || text.Contains(',', StringComparison.Ordinal)
            || text.Contains('\n', StringComparison.Ordinal)
            || text.Contains('\r', StringComparison.Ordinal))
        {
            text = "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }

    private static readonly string[] CsvHeaders =
    [
        "Id",
        "TenantId",
        "TargetTenantId",
        "CorrelationId",
        "OccurredAtUtc",
        "WrittenAtUtc",
        "ActorType",
        "ActorId",
        "ActorEmailMasked",
        "ActorDisplayNameMasked",
        "Category",
        "EntityType",
        "EntityId",
        "Operation",
        "Outcome",
        "SourceService",
        "SourceModule",
        "IsMetaAudit",
        "RedactionStatus",
        "BeforeState",
        "AfterState",
        "Metadata",
        "IpAddressMasked",
        "UserAgent"
    ];
}
