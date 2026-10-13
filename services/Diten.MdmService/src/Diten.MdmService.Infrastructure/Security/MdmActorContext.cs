using System.Security.Claims;
using Diten.MdmService.Application.Features.Approvals;
using Microsoft.AspNetCore.Http;

namespace Diten.MdmService.Infrastructure.Security;

/// <summary>WP-VW-W4 (W4-BE-b1) — the approval actor: the caller principal's <c>sub</c> (NameIdentifier), the same id
/// MOD-0023 records as <c>ActionedBy</c>, so the SoD comparison (author / editor / submitter vs approver) really holds.
/// Null without a usable identity (never a guess).</summary>
public sealed class MdmActorContext : IMdmActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MdmActorContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string? ActorName
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            var actor = principal?.FindFirstValue("sub") ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrWhiteSpace(actor) ? null : actor.Trim();
        }
    }
}
