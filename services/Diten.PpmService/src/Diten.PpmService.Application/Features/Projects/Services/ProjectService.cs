using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.Repositories;
using Diten.Shared.Core;

namespace Diten.PpmService.Application.Features.Projects;

public sealed class ProjectService(
    IProjectRepository repository, IInitiativeRepository initiatives, IProgramRepository programs,
    IAuditIntentRepository audit, IPpmUnitOfWork unitOfWork, ITenantContext tenant,
    ICurrentActorContext actor, ICorrelationContext correlation, IPpmAccessAuthorizer access)
{
    public async Task<Response<ProjectDto>> Create(CreateProjectCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProjectsCreate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProjectDto>();
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await ParentExists(r.ParentType, r.ParentId, token)) return Response<ProjectDto>.Fail("Project parent was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), null, token)) return Response<ProjectDto>.Fail("Project code already exists.", 409);
            var e = new Project(tenant.TenantId, actor.ActorId, r.Code, r.Name, r.Description, r.ParentType, r.ParentId, r.VisibilityPolicyKey);
            await repository.AddAsync(e, token);
            await audit.AddAsync(Intent(e, "created"), token);
            return Response<ProjectDto>.Success(e.ToDto(), 201);
        }, ct);
    }
    public async Task<Response<ProjectDto>> Update(UpdateProjectCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProjectsUpdate, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProjectDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<ProjectDto>.Fail("Project was not found.", 404);
        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            if (!await ParentExists(r.ParentType, r.ParentId, token)) return Response<ProjectDto>.Fail("Project parent was not found.", 404);
            if (await repository.CodeExistsAsync(tenant.TenantId, ApplicationGuard.NormalizeCode(r.Code), r.Id, token)) return Response<ProjectDto>.Fail("Project code already exists.", 409);
            e.Update(actor.ActorId, r.Code, r.Name, r.Description, r.ParentType, r.ParentId, r.VisibilityPolicyKey);
            await repository.ReplaceAsync(e, r.ExpectedVersion, token);
            await audit.AddAsync(Intent(e, "updated"), token);
            return Response<ProjectDto>.Success(e.ToDto());
        }, ct);
    }
    public async Task<Response<ProjectDto>> Transition(TransitionProjectLifecycleCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProjectsLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProjectDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<ProjectDto>.Fail("Project was not found.", 404);
        if (!e.CanTransitionTo(r.TargetState)) return Response<ProjectDto>.Fail("Invalid lifecycle transition.", 409);
        e.Transition(actor.ActorId, r.TargetState);
        return await Persist(e, r.ExpectedVersion, "lifecycle-changed", ct);
    }
    public async Task<Response<NoContent>> SoftDelete(SoftDeleteProjectCommand r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProjectsLifecycle, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<NoContent>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        if (e is null) return Response<NoContent>.Fail("Project was not found.", 404);
        e.SoftDelete(actor.ActorId);
        return await unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, r.ExpectedVersion, token); await audit.AddAsync(Intent(e, "soft-deleted"), token); return Response<NoContent>.SuccessWithoutData(); }, ct);
    }
    public async Task<Response<ProjectDto>> GetById(GetProjectByIdQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProjectsRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<ProjectDto>();
        var e = await repository.GetByIdAsync(tenant.TenantId, r.Id, ct);
        return e is null ? Response<ProjectDto>.Fail("Project was not found.", 404) : Response<ProjectDto>.Success(e.ToDto());
    }
    public async Task<Response<IReadOnlyList<ProjectDto>>> List(ListProjectsQuery r, CancellationToken ct)
    {
        var decision = await access.AuthorizeAsync(PpmPermissions.ProjectsRead, ct);
        if (decision != PpmAccessDecision.Allowed) return decision.Failure<IReadOnlyList<ProjectDto>>();
        var items = await repository.ListAsync(tenant.TenantId, ct);
        return Response<IReadOnlyList<ProjectDto>>.Success(items.Select(x => x.ToDto()).ToArray());
    }
    private async Task<bool> ParentExists(ProjectParentType type, Guid id, CancellationToken ct)
        => type switch
        {
            ProjectParentType.Initiative => await initiatives.GetByIdAsync(tenant.TenantId, id, ct) is not null,
            ProjectParentType.Program => await programs.GetByIdAsync(tenant.TenantId, id, ct) is not null,
            _ => false
        };
    private AuditIntent Intent(Project e, string m) => new(Guid.NewGuid(), tenant.TenantId, actor.ActorId, correlation.CorrelationId, nameof(Project), e.Id, m, DateTime.UtcNow);
    private Task<Response<ProjectDto>> Persist(Project e, int version, string mutation, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async token => { await repository.ReplaceAsync(e, version, token); await audit.AddAsync(Intent(e, mutation), token); return Response<ProjectDto>.Success(e.ToDto()); }, ct);
}
