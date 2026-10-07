using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes.Commands;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Features.ProductLegalEntityScopes;

public sealed record ProductLegalEntityScopeMutationIdentity(
    string Kind,
    Guid CommandId,
    string PayloadFingerprint,
    bool EnforcedGlobalProductCreateProhibited)
{
    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 B4 — the ids of what the write touches (a product, a GSKU, an abbreviation entry, a
    /// code reservation), read from the command and its request by name. A write's lease and its proof are bounded by
    /// these; two writes conflict only when they share one.
    /// </summary>
    public IReadOnlyList<Guid> ObjectIds { get; init; } = [];

    /// <summary>The lease keys: one per object id ("GlobalProductId:{id}") plus the command's own ("Command:{id}").</summary>
    public IReadOnlyList<string> ObjectKeys { get; init; } = [];

    private static readonly string[] ObjectIdNames =
    [
        "GlobalProductId", "GskuId", "LskuId", "FinishedGoodId", "RegisterEntryId", "ActiveRegisterEntryId",
        "ReservationId", "GskuReservationId", "LskuReservationId"
    ];

    private static (IReadOnlyList<Guid> Ids, IReadOnlyList<string> Keys) Objects(object mutation, Guid commandId)
    {
        var found = new List<(string Name, Guid Id)>();
        void Read(object? value)
        {
            if (value is null) return;
            foreach (var name in ObjectIdNames)
            {
                if (value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(value)
                        is Guid id && id != Guid.Empty)
                    found.Add((name, id));
            }
        }
        Read(mutation);
        var request = mutation.GetType().GetProperty("Request", BindingFlags.Instance | BindingFlags.Public)?.GetValue(mutation);
        if (!ReferenceEquals(request, mutation)) Read(request);
        var keys = found.Select(item => $"{item.Name}:{item.Id:D}")
            .Append($"Command:{commandId:D}")
            .Distinct(StringComparer.Ordinal)
            .Take(ProductLegalEntityScopeWriterLease.MaximumObjectKeys)
            .ToArray();
        return (found.Select(item => item.Id).Distinct().ToArray(), keys);
    }

    public static ProductLegalEntityScopeMutationIdentity Create(
        IProductLegalEntityScopeInventoryMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (mutation is ReplaceProductLegalEntityScopePolicyCommand replace)
        {
            return CreateForegroundReplace(replace);
        }

        var type = mutation.GetType();
        var payload = JsonSerializer.Serialize(mutation, type, JsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var commandId = FindCommandIdentity(mutation) ?? new Guid(bytes[..16]);
        if (commandId == Guid.Empty) commandId = new Guid(SHA256.HashData(bytes)[..16]);
        var kind = type.Name;
        var (ids, keys) = Objects(mutation, commandId);
        return new(
            kind,
            commandId,
            Convert.ToHexString(bytes),
            kind is "ReserveCanonicalCodeCommand" or "CreateGlobalProductDraftCommand")
        {
            ObjectIds = ids,
            ObjectKeys = keys
        };
    }

    private static ProductLegalEntityScopeMutationIdentity CreateForegroundReplace(
        ReplaceProductLegalEntityScopePolicyCommand command)
    {
        var legalEntityIds = ProductLegalEntityScopePolicy.NormalizeLegalEntityIds(
            command.Request.Mode,
            command.Request.LegalEntityIds);
        var payload = string.Join(
            '|',
            "product-legal-entity-scope-replace/v1",
            command.GlobalProductId.ToString("D"),
            command.CommandId.ToString("D"),
            command.Request.ExpectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ((int)command.Request.Mode).ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Join(',', legalEntityIds.Select(id => id.ToString("D"))));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return new(
            nameof(ReplaceProductLegalEntityScopePolicyCommand),
            command.CommandId,
            fingerprint,
            EnforcedGlobalProductCreateProhibited: false)
        {
            ObjectIds = [command.GlobalProductId],
            ObjectKeys = [$"GlobalProductId:{command.GlobalProductId:D}", $"Command:{command.CommandId:D}"]
        };
    }

    private static Guid? FindCommandIdentity(object value)
    {
        foreach (var name in new[] { "CommandId", "CreationCommandId", "IdempotencyKey", "OperationId" })
        {
            var property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(value) is Guid guid && guid != Guid.Empty) return guid;
            if (property?.GetValue(value) is string text && !string.IsNullOrWhiteSpace(text))
            {
                if (Guid.TryParse(text, out var parsed) && parsed != Guid.Empty) return parsed;
                return DeterministicGuid(text);
            }
        }
        var request = value.GetType().GetProperty("Request", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value);
        return request is null || ReferenceEquals(request, value) ? null : FindCommandIdentity(request);
    }

    private static Guid DeterministicGuid(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))[..16]);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
