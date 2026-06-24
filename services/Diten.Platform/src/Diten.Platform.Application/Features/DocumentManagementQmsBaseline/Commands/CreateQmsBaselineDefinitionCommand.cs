using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record CreateQmsBaselineDefinitionCommand(
    Guid BaselineReleaseId,
    QmsCollectionDefinitionUpsertModel Request,
    string CorrelationId) : IRequest<Response<QmsCollectionDefinitionModel>>;
