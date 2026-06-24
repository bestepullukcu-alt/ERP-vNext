using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record CreateManualQmsBaselineCommand(
    ManualQmsBaselineRequestModel Request,
    string CorrelationId) : IRequest<Response<QmsBaselineSummaryModel>>;
