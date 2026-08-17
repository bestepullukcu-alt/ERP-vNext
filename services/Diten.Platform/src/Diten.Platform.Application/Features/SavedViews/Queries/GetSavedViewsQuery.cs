using Diten.Platform.Application.Models;
using MediatR;

namespace Diten.Platform.Application.Features.SavedViews.Queries;

public sealed record GetSavedViewsQuery(string ModuleKey, string PageKey) : IRequest<IReadOnlyList<SavedViewModel>>;
