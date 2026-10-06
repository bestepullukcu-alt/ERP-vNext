using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D10 — a tenant-typed category (label shown as typed). The code is tenant-unique: a
/// pre-check for the readable answer, the unique index for the guarantee.</summary>
public sealed class CreateWorkCategoryHandler : IRequestHandler<CreateWorkCategoryCommand, Response<WorkCategoryDto>>
{
    private readonly IWorkCategoryRepository _categories;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public CreateWorkCategoryHandler(
        IWorkCategoryRepository categories, ICurrentUserContext currentUser, ITenantContext tenantContext)
    {
        _categories = categories;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<WorkCategoryDto>> Handle(CreateWorkCategoryCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = request.Request.Code!.Trim();
        if (await _categories.GetByCodeAsync(code, ct) is not null)
        {
            return Duplicate(request);
        }

        var category = new WorkCategory
        {
            TenantId = _tenantContext.TenantId,
            Code = code,
            LabelText = request.Request.LabelText!.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Request.Description) ? null : request.Request.Description.Trim(),
            CountsAsWork = request.Request.CountsAsWork,
            SortOrder = request.Request.SortOrder,
            IsActive = true,
            CreatedBy = _currentUser.UserId.ToString()
        };

        return await _categories.TryCreateAsync(category, ct)
            ? Response<WorkCategoryDto>.Success(WorkCategoryMapping.ToDto(category), 201, request.CorrelationId)
            : Duplicate(request);
    }

    private static Response<WorkCategoryDto> Duplicate(CreateWorkCategoryCommand request)
        => Response<WorkCategoryDto>.Fail(
            "A category with this code already exists.", 409, TimeEntryReasonCodes.CategoryCodeDuplicate, request.CorrelationId);
}
