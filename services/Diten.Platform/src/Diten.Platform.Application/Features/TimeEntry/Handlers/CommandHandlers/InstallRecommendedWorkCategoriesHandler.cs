using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 R6 — the recommended starter set. Each label is a RESOURCE KEY (<c>TimeEntry.Category.{CODE}</c>),
/// resolved in seven languages by the T2 screen; there is no typed text to go stale in one language. Idempotent: a code
/// the tenant already has — recommended or its own — is left exactly as it is.
/// </summary>
public sealed class InstallRecommendedWorkCategoriesHandler
    : IRequestHandler<InstallRecommendedWorkCategoriesCommand, Response<InstallRecommendedWorkCategoriesResultDto>>
{
    /// <summary>Non-task work every organisation has. No leave or absence (D10), no "Other" (it becomes the biggest
    /// bucket and says nothing).</summary>
    internal static readonly IReadOnlyList<(string Code, bool CountsAsWork, int SortOrder)> Recommended =
    [
        ("ADMINISTRATION", true, 10),
        ("INTERNAL_MEETING", true, 20),
        ("TRAINING", true, 30),
        ("SUPPORT", true, 40),
        ("TRAVEL", true, 50)
    ];

    public static string LabelResourceKey(string code) => $"TimeEntry.Category.{code}";

    private readonly IWorkCategoryRepository _categories;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public InstallRecommendedWorkCategoriesHandler(
        IWorkCategoryRepository categories, ICurrentUserContext currentUser, ITenantContext tenantContext)
    {
        _categories = categories;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<InstallRecommendedWorkCategoriesResultDto>> Handle(
        InstallRecommendedWorkCategoriesCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var present = (await _categories.ListAsync(ct)).Select(c => c.Code).ToHashSet(StringComparer.Ordinal);
        var installed = new List<string>();
        var already = new List<string>();

        foreach (var (code, countsAsWork, sortOrder) in Recommended)
        {
            if (present.Contains(code))
            {
                already.Add(code);
                continue;
            }

            var created = await _categories.TryCreateAsync(new WorkCategory
            {
                TenantId = _tenantContext.TenantId,
                Code = code,
                LabelResourceKey = LabelResourceKey(code),
                CountsAsWork = countsAsWork,
                SortOrder = sortOrder,
                IsActive = true,
                CreatedBy = _currentUser.UserId.ToString()
            }, ct);
            (created ? installed : already).Add(code);
        }

        return Response<InstallRecommendedWorkCategoriesResultDto>.Success(
            new InstallRecommendedWorkCategoriesResultDto(installed, already), correlationId: request.CorrelationId);
    }
}
