using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;

public sealed record DeleteLearningTrainingReadinessCommand(Guid Id) : IRequest<Response<bool>>;
