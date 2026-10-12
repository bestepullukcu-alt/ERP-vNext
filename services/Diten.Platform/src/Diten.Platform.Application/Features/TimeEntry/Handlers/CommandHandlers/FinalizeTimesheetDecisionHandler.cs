using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>MOD-0280-FU01 D7 (system) — the audited door to <see cref="ITimesheetFinalizer"/>. Answers what the run
/// did (<see cref="TimesheetFinalizationResult"/>) so the audit entry says it too.</summary>
public sealed class FinalizeTimesheetDecisionHandler : IRequestHandler<FinalizeTimesheetDecisionCommand, Response<string>>
{
    private readonly ITimesheetFinalizer _finalizer;

    public FinalizeTimesheetDecisionHandler(ITimesheetFinalizer finalizer) => _finalizer = finalizer;

    public async Task<Response<string>> Handle(FinalizeTimesheetDecisionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await _finalizer.FinalizeAsync(request.WeekId, ct);
        return Response<string>.Success(result.ToString(), correlationId: request.CorrelationId);
    }
}
