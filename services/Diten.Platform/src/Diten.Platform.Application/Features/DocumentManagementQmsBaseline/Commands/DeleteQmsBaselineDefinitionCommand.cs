using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record DeleteQmsBaselineDefinitionCommand(
    Guid BaselineReleaseId,
    string CanonicalId,
    int VersionToken,
    string CorrelationId) : IRequest<Response<NoContent>>;
