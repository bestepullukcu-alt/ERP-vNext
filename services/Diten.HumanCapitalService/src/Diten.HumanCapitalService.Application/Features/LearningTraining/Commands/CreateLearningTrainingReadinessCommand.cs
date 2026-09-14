using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;

public sealed record CreateLearningTrainingReadinessCommand(LearningTrainingReadinessCreateRequest Request) : IRequest<Response<Guid>>;
