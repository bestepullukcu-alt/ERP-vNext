using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record MoveQmsBaselineDefinitionCommand(
    Guid BaselineReleaseId,
    string CanonicalId,
    QmsCollectionDefinitionMoveModel Request,
    string CorrelationId) : IRequest<Response<QmsCollectionDefinitionModel>>;
