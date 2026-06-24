using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record PublishQmsBaselineCommand(
    Guid BaselineReleaseId,
    int ExpectedVersion,
    string CorrelationId) : IRequest<Response<QmsBaselinePublishResult>>;
