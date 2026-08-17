using Asp.Versioning;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy")]
public sealed class EnterpriseStrategyLookupsController : EnterpriseStrategyApiControllerBase
{
    private readonly IGoalRepository _goals;
    private readonly IObjectiveRepository _objectives;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;
    private readonly IProjectStrategyLinkRepository _projectLinks;
    private readonly IPpmInitiativeCacheRepository _ppmInitiatives;
    private readonly IPpmProjectCacheRepository _ppmProjects;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyLookupsController(
        IGoalRepository goals,
        IObjectiveRepository objectives,
        IInitiativeStrategyLinkRepository initiativeLinks,
        IProjectStrategyLinkRepository projectLinks,
        IPpmInitiativeCacheRepository ppmInitiatives,
        IPpmProjectCacheRepository ppmProjects,
        ICorrelationContextAccessor correlation)
    {
        _goals = goals;
        _objectives = objectives;
        _initiativeLinks = initiativeLinks;
        _projectLinks = projectLinks;
        _ppmInitiatives = ppmInitiatives;
        _ppmProjects = ppmProjects;
        _correlation = correlation;
    }

    [HttpGet("lookups")]
    public ActionResult<Response<EnterpriseStrategyWorkbookLookupsDto>> Lookups()
    {
        var dto = EnterpriseStrategyLookupCatalog.BuildWorkbookLookups();
        return Ok(Response<EnterpriseStrategyWorkbookLookupsDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpGet("runtime-ids/preview")]
    public async Task<ActionResult<Response<EnterpriseStrategyRuntimeIdPreviewDto>>> RuntimeIdPreview(CancellationToken ct)
    {
        var goals = await SafeListAsync(() => _goals.ListAsync(ct));
        var objectives = await SafeListAsync(() => _objectives.ListAsync(ct));
        var initiativeLinks = await SafeListAsync(() => _initiativeLinks.ListAsync(ct));
        var projectLinks = await SafeListAsync(() => _projectLinks.ListAsync(ct));
        var ppmIn = await SafeListAsync(() => _ppmInitiatives.ListAsync(ct));
        var ppmPr = await SafeListAsync(() => _ppmProjects.ListAsync(ct));

        var initiativeIds = initiativeLinks.Select(x => x.InitiativeId)
            .Concat(ppmIn.Select(x => x.InitiativeId))
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var projectIds = projectLinks.Select(x => x.ProjectId)
            .Concat(ppmPr.Select(x => x.ProjectId))
            .Where(x => !string.IsNullOrWhiteSpace(x));

        var preview = new EnterpriseStrategyRuntimeIdPreviewDto
        {
            GoalId = EnterpriseStrategyRuntimeIds.NextGoalId(goals.Select(x => x.Id)),
            ObjectiveId = EnterpriseStrategyRuntimeIds.NextObjectiveId(objectives.Select(x => x.Id)),
            InitiativeId = EnterpriseStrategyRuntimeIds.NextInitiativeId(initiativeIds),
            ProjectId = EnterpriseStrategyRuntimeIds.NextProjectId(projectIds)
        };

        return Ok(Response<EnterpriseStrategyRuntimeIdPreviewDto>.Ok(preview, _correlation.CorrelationId));
    }

    private static async Task<IReadOnlyList<T>> SafeListAsync<T>(Func<Task<IReadOnlyList<T>>> loader)
    {
        try
        {
            return await loader();
        }
        catch
        {
            return Array.Empty<T>();
        }
    }
}
