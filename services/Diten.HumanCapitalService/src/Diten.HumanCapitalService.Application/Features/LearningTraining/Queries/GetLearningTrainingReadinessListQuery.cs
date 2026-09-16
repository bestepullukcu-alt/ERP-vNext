using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Queries;

public sealed record GetLearningTrainingReadinessListQuery : IRequest<Response<IReadOnlyList<LearningTrainingReadinessListItemDto>>>;
