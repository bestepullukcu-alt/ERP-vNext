using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Queries;

public sealed record ExportAuditEventsQuery(AuditExportRequest Request) : IRequest<Response<AuditExportResultDto>>;
