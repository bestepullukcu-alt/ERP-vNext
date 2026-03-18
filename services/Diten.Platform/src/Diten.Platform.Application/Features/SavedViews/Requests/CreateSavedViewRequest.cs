using System.Text.Json;

namespace Diten.Platform.Application.Features.SavedViews.Requests;

public sealed class CreateSavedViewRequest
{
    public string ModuleKey { get; init; } = string.Empty;

    public string PageKey { get; init; } = string.Empty;

    public string ViewName { get; init; } = string.Empty;

    public JsonElement ViewDefinition { get; init; }

    public bool IsDefault { get; init; }

    public string? Visibility { get; init; }
}
