using System.Collections;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Audit;

/// <summary>
/// BL-347 — what the export-audit suites share: a REAL principal (claims, as the JWT middleware leaves them), a
/// spy that forwards to the real audit service, and readers for what actually landed in the outbox.
/// </summary>
internal static class DataExportAuditTestKit
{
    /// <summary>A raw email and name — the audit service must MASK both before anything is stored.</summary>
    internal const string RawEmail = "ayse.yilmaz@tenant.example";

    internal const string RawDisplayName = "Ayşe Yılmaz";

    /// <summary>
    /// The request's principal. Every production reader — <c>JwtTenantAuthorizationContext</c>,
    /// <c>CurrentUserContext</c> — is built over this, so the actor type a test sees is the one a token carries.
    /// </summary>
    internal static IHttpContextAccessor Principal(
        string? actorType,
        Guid? userId,
        bool authenticated = true,
        Guid? tenantClaim = null)
    {
        var claims = new List<Claim> { new("email", RawEmail), new("name", RawDisplayName) };
        if (userId is { } id)
        {
            claims.Add(new Claim("sub", id.ToString()));
        }

        if (actorType is not null)
        {
            claims.Add(new Claim("actor_type", actorType));
        }

        if (tenantClaim is { } tenant)
        {
            claims.Add(new Claim("tenant_id", tenant.ToString()));
        }

        var identity = authenticated
            ? new ClaimsIdentity(claims, authenticationType: "test")
            : new ClaimsIdentity(claims);

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    internal static IMongoCollection<AuditOutboxMessage> Outbox(IMongoDatabase database)
        => database.GetCollection<AuditOutboxMessage>(AuditCollectionNames.AuditOutbox);

    /// <summary>The shape the audit worker claims — so the production mapper can say what audit_events will hold.</summary>
    internal static AuditOutboxProcessingItem ToProcessingItem(AuditOutboxMessage message) => new(
        message.Id,
        message.TenantId,
        message.CorrelationId,
        message.IdempotencyKey,
        message.RequestType,
        message.Operation,
        message.EntityType,
        message.EntityId,
        message.Payload,
        message.Status,
        message.Attempts,
        message.NextAttemptAtUtc,
        message.CreatedAtUtc);

    internal static IReadOnlyDictionary<string, object?> AsDictionary(object? value) => value switch
    {
        IEnumerable<KeyValuePair<string, object?>> pairs =>
            pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        IDictionary dictionary => dictionary.Cast<DictionaryEntry>().ToDictionary(
            entry => Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!,
            entry => entry.Value,
            StringComparer.Ordinal),
        _ => throw new InvalidOperationException($"Expected a dictionary, got {value?.GetType().Name ?? "null"}.")
    };

    /// <summary>Every key and leaf value of a stored payload as one string — for "this never reached the record".</summary>
    internal static string Flatten(object? value)
    {
        var text = new StringBuilder();
        Walk(value, text);
        return text.ToString();
    }

    private static void Walk(object? value, StringBuilder text)
    {
        switch (value)
        {
            case null:
                return;
            case string s:
                text.Append(s).Append('|');
                return;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                foreach (var (key, item) in pairs)
                {
                    text.Append(key).Append('=');
                    Walk(item, text);
                }

                return;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    text.Append(entry.Key).Append('=');
                    Walk(entry.Value, text);
                }

                return;
            case IEnumerable items:
                foreach (var item in items)
                {
                    Walk(item, text);
                }

                return;
            default:
                text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('|');
                return;
        }
    }
}

/// <summary>Forwards to the real audit service and keeps what it was asked and what it answered.</summary>
internal sealed class RecordingAuditService(IAuditService inner) : IAuditService
{
    public List<AuditAppendRequest> Requests { get; } = [];

    public List<AuditAppendResult> Results { get; } = [];

    public async Task<AuditAppendResult> AppendAsync(AuditAppendRequest request, CancellationToken ct = default)
    {
        Requests.Add(request);
        var result = await inner.AppendAsync(request, ct);
        Results.Add(result);
        return result;
    }
}
