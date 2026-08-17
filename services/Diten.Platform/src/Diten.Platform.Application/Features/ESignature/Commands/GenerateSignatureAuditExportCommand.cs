using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.ESignature.Commands;

public sealed record GenerateSignatureAuditExportCommand(
    Guid TargetTenantId,
    Guid EnvelopeId,
    Guid CorrelationId) : IRequest<Response<Guid>>;
