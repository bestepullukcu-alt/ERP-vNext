using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Queries;

public sealed record GetBusinessReferenceDataHierarchyQuery(
    string SetCode,
    string? ScopeKey,
    int? VersionNumber,
    DateTimeOffset? AsOfDate,
    bool IncludeDeprecated,
    bool IncludeAttributes,
    bool IncludeMappings) : IRequest<Response<BusinessReferenceDataHierarchyLookupModel>>, IBusinessReferenceDataRequest;
