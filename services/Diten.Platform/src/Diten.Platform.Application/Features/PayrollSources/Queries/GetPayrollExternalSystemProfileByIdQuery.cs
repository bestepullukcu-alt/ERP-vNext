using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Queries;

public sealed record GetPayrollExternalSystemProfileByIdQuery(Guid Id) : IRequest<Response<PayrollExternalSystemProfileDto>>;
