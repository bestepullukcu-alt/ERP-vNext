using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Subject.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using SubjectEntity = Diten.CrmService.Domain.Entities.Subject;

namespace Diten.CrmService.Application.Features.Knowledge.Subject.Handlers;

public sealed class ListSubjectsHandler : IRequestHandler<ListSubjectsQuery, Response<SubjectListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISubjectRepository _repository;

    public ListSubjectsHandler(ITenantContext tenant, ISubjectRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<SubjectListDto>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SubjectListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<SubjectEntity> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TaxonomyStatuses.Normalize(request.Status);
            rows = rows.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(s =>
                s.SubjectName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || s.SubjectCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(s => !s.IsArchived());
        }

        var items = rows.Select(KnowledgeMapper.ToDto).ToList();
        return Response<SubjectListDto>.Success(new SubjectListDto(items, items.Count));
    }
}

public sealed class GetSubjectHandler : IRequestHandler<GetSubjectQuery, Response<SubjectDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ISubjectRepository _repository;

    public GetSubjectHandler(ITenantContext tenant, ISubjectRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<SubjectDto>> Handle(GetSubjectQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<SubjectDto>.Fail("Tenant context is required.", 400);
        }

        var subject = await _repository.GetByIdAsync(tenantId, request.SubjectId, cancellationToken);
        return subject is null
            ? Response<SubjectDto>.Fail("Subject not found.", 404)
            : Response<SubjectDto>.Success(KnowledgeMapper.ToDto(subject));
    }
}
