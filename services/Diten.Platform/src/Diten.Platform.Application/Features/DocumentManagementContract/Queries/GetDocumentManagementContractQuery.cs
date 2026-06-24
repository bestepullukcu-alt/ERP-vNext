using MediatR;
using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.DocumentManagementContract.Queries;

public sealed record GetDocumentManagementContractQuery(string CorrelationId)
    : IRequest<Response<DocumentManagementContractResponse>>;
