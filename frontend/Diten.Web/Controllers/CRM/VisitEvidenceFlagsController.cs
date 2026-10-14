using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-WEB-b, M1–M6) — <c>/CRM/VisitEvidenceFlags</c>: the manager's visit evidence flags (manifest page
/// <c>CRM_VISIT_EVIDENCE_FLAGS</c>): the summary strip, the visits list with its filters, the right drawer and the rep
/// summary. Read only (K-W3E-11). Every call is a same-origin proxy to the CRM endpoints of W3E-BE-b / BE-c with the
/// caller's session; the CRM decides the scope (<c>crm.visit-evidence-flags.read</c> = the team, <c>.read-all</c> = the
/// tenant; outside the scope 404). The photo thumbnail is read through the EXISTING evidence path with its OWN keys:
/// <c>crm.visit-execution.read</c> + <c>crm.planned-visit.read-all</c> (another rep's visit) + the Platform
/// <c>platform.document-repository.visit-evidence.read-all</c>; without them the drawer says "photo present" only.
/// </summary>
[Authorize]
[Route("CRM/VisitEvidenceFlags")]
public sealed class VisitEvidenceFlagsController : CrmGatewayProxy
{
    public const string Read = "crm.visit-evidence-flags.read";
    public const string ReadAll = "crm.visit-evidence-flags.read-all";
    public const string ExecutionRead = "crm.visit-execution.read";
    public const string PlannedVisitReadAll = "crm.planned-visit.read-all";
    public const string EvidenceReadAll = "platform.document-repository.visit-evidence.read-all";

    private const string Flags = "/api/crm/visit-evidence-flags";

    public VisitEvidenceFlagsController(HttpClient httpClient, IConfiguration configuration, ILogger<VisitEvidenceFlagsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, ReadAll);

    /// <summary>The photo thumbnail's keys (the existing evidence read path: another rep's visit needs read-all + the
    /// Platform tenant-wide evidence read).</summary>
    private bool MaySeePhotos => HasAll(ExecutionRead, PlannedVisitReadAll, EvidenceReadAll);

    /// <summary>The page. Without a flags read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/VisitEvidenceFlags/Index.cshtml", new VisitEvidenceFlagsViewModel
    {
        CanRead = MayRead,
        CanReadAll = HasAny(ReadAll),
        CanSeePhotos = MaySeePhotos
    });

    [HttpGet("api/flags")]
    public Task<IActionResult> List(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Flags}{Request.QueryString}", null, MayRead, ct);

    [HttpGet("api/summary")]
    public Task<IActionResult> Summary(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Flags}/summary{Request.QueryString}", null, MayRead, ct);

    [HttpGet("api/flags/{executionId:guid}")]
    public Task<IActionResult> Detail(Guid executionId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Flags}/{executionId:D}", null, MayRead, ct);

    /// <summary>The start photo (bytes) through the existing evidence stream, with its own keys (never the flags key alone).</summary>
    [HttpGet("api/flags/{executionId:guid}/photo")]
    public Task<IActionResult> Photo(Guid executionId, CancellationToken ct)
        => ProxyBytesAsync($"/api/crm/visit-execution/{executionId:D}/photo", MayRead && MaySeePhotos, ct);
}
