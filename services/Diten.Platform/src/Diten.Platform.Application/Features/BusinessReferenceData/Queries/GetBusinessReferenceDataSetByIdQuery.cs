using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Queries;

public sealed record GetBusinessReferenceDataSetByIdQuery(Guid SetId) : IRequest<Response<BusinessReferenceDataSetDetailModel>>, IBusinessReferenceDataRequest;
