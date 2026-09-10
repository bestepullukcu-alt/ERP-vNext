using Asp.Versioning;
using Diten.Domain.Aggregates.DemandIdea;
using Diten.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

/// <summary>
/// Demand-idea attachment upload/download.
/// <para>
/// ⚠️ <b>D-5 security fix.</b> Both actions were reachable <b>anonymously</b>: this controller carried no
/// authorisation filter, the service registered no authentication scheme and never called
/// <c>UseAuthentication()</c>, and neither gateway route declared <c>AuthenticationOptions</c>. That exposed
/// unauthenticated file READ (<c>GET {demandId}/{attachmentId}</c>) and unauthenticated WRITE
/// (<c>POST {demandId}</c> — a 50 MB disk write plus an aggregate mutation for anyone holding a draft
/// demand id). Authentication is now required at the gateway edge and again here, so a caller reaching
/// port 5004 directly is refused too.
/// </para>
/// <para>
/// ⚠️ <b>Known remaining gap — tenant isolation.</b> This service has <b>no tenant dimension at all</b>:
/// neither <c>DemandIdeaAggregate</c> nor its <c>BaseEntity</c> carries a <c>TenantId</c>, and the string
/// does not appear anywhere under <c>src</c>. One is deliberately NOT invented here. Any authenticated user
/// can therefore still read any attachment whose demand id they know. Closing that requires a tenant
/// dimension on the aggregate and is tracked with the MOD-0262 hand-over, where
/// <c>DemandIdeaAttachment</c> also has to gain <c>Checksum</c>, <c>TenantId</c>, <c>VersionId</c> and
/// <c>StorageProvider</c>.
/// </para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/uploads")]
[Authorize]
public sealed class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly IRepository<DemandIdeaAggregate> _repository;

    public UploadsController(IWebHostEnvironment env, IRepository<DemandIdeaAggregate> repository)
    {
        _env = env;
        _repository = repository;
    }

    /// <summary>Upload a file for an existing demand idea (draft). Returns metadata to include on save.</summary>
    [HttpPost("{demandId}")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<IActionResult> Post(string demandId, IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file." });

        var entity = await _repository.GetByIdAsync(demandId);
        if (entity == null || entity.IsDeleted)
            return NotFound();
        if (entity.Status != "Draft")
            return Conflict(new { message = "Only draft records accept uploads." });

        var attachmentId = Guid.NewGuid().ToString("N");
        var safeName = Path.GetFileName(file.FileName);
        var root = Path.Combine(_env.ContentRootPath, "Data", "uploads", "demand-ideas", demandId);
        Directory.CreateDirectory(root);
        var storedName = $"{attachmentId}_{safeName}";
        var physicalPath = Path.Combine(root, storedName);
        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var storageKey = Path.Combine("demand-ideas", demandId, storedName).Replace('\\', '/');
        entity.Attachments.Add(new DemandIdeaAttachment
        {
            Id = attachmentId,
            FileName = safeName,
            ContentType = file.ContentType ?? "application/octet-stream",
            SizeBytes = file.Length,
            StorageKey = storageKey
        });
        entity.LastModifiedDate = DateTime.UtcNow;
        await _repository.UpdateAsync(entity);

        return Ok(new
        {
            id = attachmentId,
            fileName = safeName,
            contentType = file.ContentType ?? "application/octet-stream",
            sizeBytes = file.Length,
            storageKey
        });
    }

    [HttpGet("{demandId}/{attachmentId}")]
    public async Task<IActionResult> Download(string demandId, string attachmentId)
    {
        var entity = await _repository.GetByIdAsync(demandId);
        if (entity == null || entity.IsDeleted)
            return NotFound();

        var att = entity.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (att == null)
            return NotFound();

        var path = Path.Combine(_env.ContentRootPath, "Data", "uploads", att.StorageKey);
        if (!System.IO.File.Exists(path))
            return NotFound();

        return PhysicalFile(path, att.ContentType, att.FileName);
    }
}
