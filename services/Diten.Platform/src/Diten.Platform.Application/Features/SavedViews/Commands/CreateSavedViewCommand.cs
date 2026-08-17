using Diten.Platform.Application.Models;
using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Commands;

public sealed record CreateSavedViewCommand(
    string ModuleKey,
    string PageKey,
    string ViewName,
    string ViewDefinitionJson,
    bool IsDefault,
    string Visibility) : IRequest<SavedViewModel>;
