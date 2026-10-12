using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.QueryHandlers;

/// <summary>The stored switch rows only. A legal entity that is not listed is OFF (D12) — there is no default row.</summary>
public sealed class GetLegalEntityTimeSettingListHandler
    : IRequestHandler<GetLegalEntityTimeSettingListQuery, Response<IReadOnlyList<LegalEntityTimeSettingDto>>>
{
    private readonly ILegalEntityTimeSettingRepository _settings;

    public GetLegalEntityTimeSettingListHandler(ILegalEntityTimeSettingRepository settings) => _settings = settings;

    public async Task<Response<IReadOnlyList<LegalEntityTimeSettingDto>>> Handle(
        GetLegalEntityTimeSettingListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<LegalEntityTimeSettingDto> rows = (await _settings.ListAsync(ct)).Select(WorkCategoryMapping.ToDto).ToList();
        return Response<IReadOnlyList<LegalEntityTimeSettingDto>>.Success(rows, correlationId: request.CorrelationId);
    }
}
