using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.SubscriptionFeatures.Queries;

public sealed record GetFeatureCategoriesQuery(string? Status) : IRequest<Response<IReadOnlyList<FeatureCategoryDto>>>;
