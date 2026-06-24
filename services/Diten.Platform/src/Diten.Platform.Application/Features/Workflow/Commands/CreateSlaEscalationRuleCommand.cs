using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Workflow.Commands;

public sealed record CreateSlaEscalationRuleCommand(
    CreateSlaEscalationRuleRequest Request,
    string CorrelationId) : IRequest<Response<SlaEscalationRuleDto>>;
