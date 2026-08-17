using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class RecordAttendanceSummaryReferenceHandler : IRequestHandler<RecordAttendanceSummaryReferenceCommand, Response<Guid>>
{
    private readonly ITimeAttendanceProviderRepository _repository;

    public RecordAttendanceSummaryReferenceHandler(ITimeAttendanceProviderRepository repository) => _repository = repository;

    public async Task<Response<Guid>> Handle(RecordAttendanceSummaryReferenceCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct);
        if (profile == null)
        {
            return Response<Guid>.Fail("Time-attendance provider not found.", 404);
        }

        var summary = new AttendanceSummaryReference
        {
            TenantId = profile.TenantId,
            ProviderProfileId = profile.Id,
            ExternalSummaryId = request.Request.ExternalSummaryId.Trim(),
            ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim(),
            SummaryPeriodStart = request.Request.SummaryPeriodStart,
            SummaryPeriodEnd = request.Request.SummaryPeriodEnd,
            SummaryState = request.Request.SummaryState,
            CorrelationId = string.IsNullOrWhiteSpace(request.Request.CorrelationId) ? null : request.Request.CorrelationId.Trim()
        };

        await _repository.CreateAttendanceSummaryReferenceAsync(summary, ct);
        return Response<Guid>.Success(summary.Id, 201);
    }
}
