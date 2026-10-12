using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>DTO mapping for the admin-side records, and the one activate/deactivate path both handlers share.</summary>
public static class WorkCategoryMapping
{
    public static WorkCategoryDto ToDto(WorkCategory category) => new(
        category.Id, category.Code, category.LabelText, category.LabelResourceKey, category.Description,
        category.CountsAsWork, category.SortOrder, category.IsActive, category.Version);

    public static LegalEntityTimeSettingDto ToDto(LegalEntityTimeSetting setting) => new(
        setting.LegalEntityId, setting.TimerEnabled, setting.ChangedAtUtc, setting.ChangedByUserId, setting.Reason, setting.Version);

    public static async Task<Response<WorkCategoryDto>> SetActiveAsync(
        IWorkCategoryRepository categories, Guid id, int expectedVersion, bool active, Guid actor, string correlationId,
        CancellationToken ct)
    {
        var category = await categories.GetByIdAsync(id, ct);
        if (category is null)
        {
            return Response<WorkCategoryDto>.Fail("Category not found.", 404, TimeEntryReasonCodes.CategoryNotFound, correlationId);
        }

        category.IsActive = active;
        category.UpdatedBy = actor.ToString();
        return await categories.UpdateAsync(category, expectedVersion, ct)
            ? Response<WorkCategoryDto>.Success(ToDto(category), correlationId: correlationId)
            : Response<WorkCategoryDto>.Fail(
                "The category changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.CategoryConcurrencyConflict,
                correlationId);
    }
}
