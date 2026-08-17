using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Commands;

public sealed record DeleteSavedViewCommand(string Id) : IRequest<bool>;
