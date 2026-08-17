using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Queries;

public sealed record GetPayrollCycleReferencesQuery(Guid SourceProfileId) : IRequest<Response<IReadOnlyList<PayrollCycleReferenceDto>>>;
