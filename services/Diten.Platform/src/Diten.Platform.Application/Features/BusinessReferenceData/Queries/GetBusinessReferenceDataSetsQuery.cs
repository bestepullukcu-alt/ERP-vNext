using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Queries;

public sealed record GetBusinessReferenceDataSetsQuery(
    string? Search,
    string? Status,
    string? ScopeType,
    int Page,
    int PageSize,
    string Sort) : IRequest<Response<BusinessReferenceDataSetListModel>>, IBusinessReferenceDataRequest;
