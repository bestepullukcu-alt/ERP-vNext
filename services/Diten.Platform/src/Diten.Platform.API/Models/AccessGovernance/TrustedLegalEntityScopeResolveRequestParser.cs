using System.Text.Json;
using Diten.Platform.Application.Features.AccessGovernance.LegalEntityScopeResolution;

namespace Diten.Platform.API.Models.AccessGovernance;

public static class TrustedLegalEntityScopeResolveRequestParser
{
    private const string ContractError = "LEGAL_ENTITY_SCOPE_CONTRACT_INVALID";

    public static async Task<(TrustedLegalEntityScopeResolveRequest? Request, string? Error)> ParseAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Query.Count != 0
            || !string.Equals(
                request.ContentType?.Split(';', 2)[0],
                "application/json",
                StringComparison.OrdinalIgnoreCase)
            || request.ContentLength is > TrustedLegalEntityScopeResolutionLimits.MaxRequestBodyBytes)
        {
            return (null, ContractError);
        }

        var buffer = new byte[TrustedLegalEntityScopeResolutionLimits.MaxRequestBodyBytes + 1];
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = await request.Body.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        if (bytesRead == 0 || bytesRead > TrustedLegalEntityScopeResolutionLimits.MaxRequestBodyBytes)
        {
            return (null, ContractError);
        }

        try
        {
            using var document = JsonDocument.Parse(buffer.AsMemory(0, bytesRead));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, ContractError);
            }

            string? moduleCode = null;
            string? permissionKey = null;
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!propertyNames.Add(property.Name) || property.Value.ValueKind != JsonValueKind.String)
                {
                    return (null, ContractError);
                }

                switch (property.Name)
                {
                    case "module_code":
                        moduleCode = property.Value.GetString();
                        break;
                    case "permission_key":
                        permissionKey = property.Value.GetString();
                        break;
                    default:
                        return (null, ContractError);
                }
            }

            if (propertyNames.Count != 2
                || !TrustedLegalEntityScopeResolutionLimits.IsValidModuleCode(moduleCode)
                || !TrustedLegalEntityScopeResolutionLimits.IsValidPermissionKey(permissionKey))
            {
                return (null, ContractError);
            }

            return (new TrustedLegalEntityScopeResolveRequest(moduleCode!, permissionKey!), null);
        }
        catch (JsonException)
        {
            return (null, ContractError);
        }
    }
}
