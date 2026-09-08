using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Queries;

public sealed record GetLearningTrainingReadinessByIdQuery(Guid Id) : IRequest<Response<LearningTrainingReadinessDto>>;
