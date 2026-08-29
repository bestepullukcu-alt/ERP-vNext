using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Subject.Queries;

/// <summary>Lists subjects for the tenant. Archived rows are included by default so history stays visible.</summary>
public sealed record ListSubjectsQuery(
    string? Status = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<SubjectListDto>>;

public sealed record GetSubjectQuery(Guid SubjectId) : IRequest<Response<SubjectDto>>;
