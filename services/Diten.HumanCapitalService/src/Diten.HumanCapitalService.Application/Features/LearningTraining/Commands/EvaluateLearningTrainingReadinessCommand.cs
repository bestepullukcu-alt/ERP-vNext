using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;

public sealed record EvaluateLearningTrainingReadinessCommand(Guid Id) : IRequest<Response<LearningTrainingReadinessDto>>;
