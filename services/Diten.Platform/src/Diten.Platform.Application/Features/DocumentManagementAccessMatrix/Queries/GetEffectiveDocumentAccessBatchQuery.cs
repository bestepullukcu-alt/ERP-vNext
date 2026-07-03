using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Queries;

public sealed record GetEffectiveDocumentAccessBatchQuery(EffectiveDocumentAccessBatchInput Input, string CorrelationId)
    : IRequest<Response<IReadOnlyList<EffectiveDocumentAccessModel>>>;
