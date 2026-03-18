using System.Text.Json;

namespace Diten.Platform.Application.Features.SavedViews.Requests;

public sealed class UpdateSavedViewRequest
{
    public string? ModuleKey { get; init; }

    public string? PageKey { get; init; }

    public string? ViewName { get; init; }

    public JsonElement? ViewDefinition { get; init; }

    public bool? IsDefault { get; init; }

    public string? Visibility { get; init; }
}
