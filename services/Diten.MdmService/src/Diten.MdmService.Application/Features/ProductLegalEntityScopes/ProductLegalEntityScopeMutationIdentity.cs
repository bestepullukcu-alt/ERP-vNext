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
        return new(
            kind,
            commandId,
            Convert.ToHexString(bytes),
            kind is "ReserveCanonicalCodeCommand" or "CreateGlobalProductDraftCommand");
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
            EnforcedGlobalProductCreateProhibited: false);
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
