using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record UpdateQmsBaselineDefinitionCommand(
    Guid BaselineReleaseId,
    string CanonicalId,
    QmsCollectionDefinitionUpsertModel Request,
    string CorrelationId) : IRequest<Response<QmsCollectionDefinitionModel>>;
