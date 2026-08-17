using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance.Commands;

public sealed record RecordPayrollIntegrationEvidenceExportReferenceCommand(Guid RunId, PayrollIntegrationEvidenceExportReferenceRequest Request) : IRequest<Response<Guid>>;
