using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Queries;

public sealed record GetPayrollSourceHealthQuery(Guid SourceProfileId) : IRequest<Response<PayrollSourceHealthSnapshotDto>>;
