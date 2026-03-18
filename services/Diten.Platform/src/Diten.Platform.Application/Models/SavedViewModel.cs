using System.Text.Json;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Models;

public sealed record SavedViewModel(
    string Id,
    Guid TenantId,
    Guid UserId,
    string ModuleKey,
    string PageKey,
    string ViewName,
    JsonElement ViewDefinition,
    bool IsDefault,
    string Visibility,
    DateTime CreatedDate,
    DateTime ModifiedDate)
{
    public static SavedViewModel FromEntity(SavedView entity)
    {
        var viewDefinition = ParseViewDefinition(entity.ViewDefinitionJson);

        return new SavedViewModel(
            entity.Id,
            entity.TenantId,
            entity.UserId,
            entity.ModuleKey,
            entity.PageKey,
            entity.ViewName,
            viewDefinition,
            entity.IsDefault,
            entity.Visibility,
            entity.CreatedDate,
            entity.ModifiedDate);
    }

    private static JsonElement ParseViewDefinition(string viewDefinitionJson)
    {
        var payload = string.IsNullOrWhiteSpace(viewDefinitionJson) ? "{}" : viewDefinitionJson;

        try
        {
            return JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}
