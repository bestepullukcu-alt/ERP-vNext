using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Queries;

public sealed record GetBaselineExperimentMeasurementReadinessByIdQuery(Guid Id) : IRequest<Response<BaselineExperimentMeasurementReadinessDto>>;
