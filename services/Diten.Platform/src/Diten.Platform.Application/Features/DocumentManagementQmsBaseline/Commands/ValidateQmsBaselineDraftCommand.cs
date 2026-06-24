using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record ValidateQmsBaselineDraftCommand(
    Guid BaselineReleaseId,
    string CorrelationId) : IRequest<Response<QmsDraftTreeValidationResult>>;
