using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Commands;

public sealed record DeleteBaselineExperimentMeasurementReadinessCommand(Guid Id) : IRequest<Response<bool>>;
