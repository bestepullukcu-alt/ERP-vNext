using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Queries;

public sealed record GetPayrollResultReferencesQuery(Guid SourceProfileId) : IRequest<Response<IReadOnlyList<PayrollResultReferenceDto>>>;
