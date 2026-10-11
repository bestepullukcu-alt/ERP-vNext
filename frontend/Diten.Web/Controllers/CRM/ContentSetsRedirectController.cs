using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-4 (DESIGN-KP-STUDIO §7, bridge-decision §8) — the Content Sets console is retired; the Knowledge Path Studio
/// took its job. Old bookmarks of <c>/CRM/ContentSets</c> (list, create, edit / workspace, any sub-path) are answered with a
/// permanent redirect to the studio list — never a page, never the old proxy. The studio applies its own read gate.
/// </summary>
[Authorize]
[Route("CRM/ContentSets")]
public sealed class ContentSetsRedirectController : Controller
{
    public const string Target = "/CRM/KnowledgePaths";

    [HttpGet("")]
    [HttpGet("{**rest}")]
    public IActionResult RedirectToKnowledgePaths() => RedirectPermanent(Target);
}
