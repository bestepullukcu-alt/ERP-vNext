using Diten.ManagementGovernanceService.Api.LocalTestSecurity;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.Catalog;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.Controllers;

[ApiController]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/local-test/v1/process-modeling")]
public sealed class ProcessModelingLocalTestController(ISender sender) : ControllerBase
{
    [HttpGet("catalog/tree")] public Task<IActionResult> Tree(CancellationToken ct) => Query(ProcessModelingPermissions.ExactPermissions[0], c => new GetCatalogTreeQuery(c), ct);
    [HttpGet("catalog/definitions/{id:guid}")] public Task<IActionResult> Definition(Guid id,CancellationToken ct) => Query(ProcessModelingPermissions.ExactPermissions[4], c => new GetProcessDefinitionByIdQuery(id,c),ct);
    [HttpPost("catalog/architectures")] public Task<IActionResult> CreateArchitecture(CreateArchitectureRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[1],c=>new CreateProcessArchitectureCommand(b.Id,b.ArchitectureCode,b.Name,b.Description,b.SortOrder,c),ct);
    [HttpPut("catalog/architectures/{id:guid}")] public Task<IActionResult> UpdateArchitecture(Guid id,UpdateCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[2],c=>new UpdateProcessArchitectureCommand(id,b.Name,b.Description,b.SortOrder,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/architectures/{id:guid}/archive")] public Task<IActionResult> ArchiveArchitecture(Guid id,ArchiveCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[3],c=>new ArchiveProcessArchitectureCommand(id,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/domains")] public Task<IActionResult> CreateDomain(CreateDomainRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[1],c=>new CreateProcessDomainCommand(b.Id,b.ProcessArchitectureId,b.DomainCode,b.Name,b.Description,b.SortOrder,c),ct);
    [HttpPut("catalog/domains/{id:guid}")] public Task<IActionResult> UpdateDomain(Guid id,UpdateCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[2],c=>new UpdateProcessDomainCommand(id,b.Name,b.Description,b.SortOrder,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/domains/{id:guid}/archive")] public Task<IActionResult> ArchiveDomain(Guid id,ArchiveCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[3],c=>new ArchiveProcessDomainCommand(id,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/families")] public Task<IActionResult> CreateFamily(CreateFamilyRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[1],c=>new CreateProcessFamilyCommand(b.Id,b.ProcessDomainId,b.FamilyCode,b.Name,b.Description,b.SortOrder,c),ct);
    [HttpPut("catalog/families/{id:guid}")] public Task<IActionResult> UpdateFamily(Guid id,UpdateCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[2],c=>new UpdateProcessFamilyCommand(id,b.Name,b.Description,b.SortOrder,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/families/{id:guid}/archive")] public Task<IActionResult> ArchiveFamily(Guid id,ArchiveCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[3],c=>new ArchiveProcessFamilyCommand(id,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/definitions")] public Task<IActionResult> CreateDefinition(CreateDefinitionRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[5],c=>new CreateProcessDefinitionCommand(b.Id,b.ProcessFamilyId,b.ProcessCode,b.Name,b.Purpose,b.Description,c),ct);
    [HttpPut("catalog/definitions/{id:guid}")] public Task<IActionResult> UpdateDefinition(Guid id,UpdateDefinitionRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[6],c=>new UpdateProcessDefinitionCommand(id,b.Name,b.Purpose,b.Description,b.ExpectedVersion,c),ct);
    [HttpPost("catalog/definitions/{id:guid}/archive")] public Task<IActionResult> ArchiveDefinition(Guid id,ArchiveCatalogRequest b,CancellationToken ct)=>Command(ProcessModelingPermissions.ExactPermissions[7],c=>new ArchiveProcessDefinitionCommand(id,b.ExpectedVersion,c),ct);

    private async Task<IActionResult> Command<T>(
        string permission,
        Func<CatalogCommandContext, IRequest<CatalogResponse<T>>> create,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = ResolveActor(permission, mutation: true);
            var context = new CatalogCommandContext(actor.TenantId, actor.ActorId, actor.IdempotencyKey, permission);
            return Envelope(await sender.Send(create(context), cancellationToken));
        }
        catch (ProcessModelingLocalTestSecurityException exception)
        {
            return Envelope(CatalogResponse<object>.Fail(exception.ReasonCode, exception.StatusCode));
        }
    }

    private async Task<IActionResult> Query<T>(
        string permission,
        Func<CatalogQueryContext, IRequest<CatalogResponse<T>>> create,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = ResolveActor(permission, mutation: false);
            var context = new CatalogQueryContext(actor.TenantId, actor.ActorId, permission);
            return Envelope(await sender.Send(create(context), cancellationToken));
        }
        catch (ProcessModelingLocalTestSecurityException exception)
        {
            return Envelope(CatalogResponse<object>.Fail(exception.ReasonCode, exception.StatusCode));
        }
    }

    private ProcessModelingLocalTestActor ResolveActor(string permission, bool mutation) =>
        ProcessModelingLocalTestSecurity.Resolve(
            User,
            Request.Headers["X-Tenant-Id"].ToString(),
            permission,
            Request.Headers["Idempotency-Key"].ToString(),
            mutation);

    private ObjectResult Envelope<T>(CatalogResponse<T> response) => StatusCode(response.StatusCode, response);
}

public sealed record CreateArchitectureRequest(Guid Id,string ArchitectureCode,string Name,string? Description,int SortOrder);
public sealed record CreateDomainRequest(Guid Id,Guid ProcessArchitectureId,string DomainCode,string Name,string? Description,int SortOrder);
public sealed record CreateFamilyRequest(Guid Id,Guid ProcessDomainId,string FamilyCode,string Name,string? Description,int SortOrder);
public sealed record CreateDefinitionRequest(Guid Id,Guid ProcessFamilyId,string ProcessCode,string Name,string? Purpose,string? Description);
public sealed record UpdateCatalogRequest(string Name,string? Description,int SortOrder,int ExpectedVersion);
public sealed record UpdateDefinitionRequest(string Name,string? Purpose,string? Description,int ExpectedVersion);
public sealed record ArchiveCatalogRequest(int ExpectedVersion);
