using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Queries;

public sealed record GetBaselineExperimentMeasurementAuditMetadataQuery(Guid Id) : IRequest<Response<BaselineExperimentMeasurementAuditMetadataDto>>;
