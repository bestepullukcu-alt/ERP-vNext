using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;

/// <summary>
/// Files an append-only correction to a finalised report (D-EDIT-WINDOW). The report must be <c>submitted</c> or
/// <c>amended</c>; a draft is corrected by re-submitting, not amending. The correction records a
/// <see cref="VisitReportAmendment"/> (who / when / why + the changed field names) and moves the report to
/// <c>amended</c> — an append-only trail, NEVER a silent in-place rewrite of history. Single-aggregate, version-guarded.
/// </summary>
public sealed class AmendVisitReportHandler : IRequestHandler<AmendVisitReportCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitReportRepository _reports;

    public AmendVisitReportHandler(ITenantContext tenant, IActorContext actor, IVisitReportRepository reports)
    {
        _tenant = tenant;
        _actor = actor;
        _reports = reports;
    }

    public async Task<Response<Guid>> Handle(AmendVisitReportCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var reason = VisitReportValidation.Trim(request.Reason);
        if (reason is null)
        {
            return Fail(new VisitReportValidation.Failure(
                "An amendment reason is required.", VisitReportErrorCodes.AmendmentReasonRequired));
        }

        if (VisitReportValidation.ValidateFreeText("Reason", reason, VisitReportLimits.MaxReasonLength)
            is { } reasonFailure)
        {
            return Fail(reasonFailure with { Code = VisitReportErrorCodes.AmendmentReasonRequired });
        }

        var report = await _reports.GetByIdAsync(tenantId, request.VisitReportId, cancellationToken);
        if (report is null)
        {
            return Fail(new VisitReportValidation.Failure(
                "Visit report not found.", VisitReportErrorCodes.ReportNotFound, 404));
        }

        // Only a finalised report is amended; a draft is corrected by re-submitting (§12, no reverse).
        if (!report.IsFinalised())
        {
            return Fail(new VisitReportValidation.Failure(
                "Only a submitted report can be amended; a draft is corrected by submitting.",
                VisitReportErrorCodes.NotFinalised, 409));
        }

        if (VisitReportValidation.ValidateAmendmentContent(request.ContentActuals, request.Samples, request.Feedback)
            is { } contentFailure)
        {
            return Fail(contentFailure);
        }

        var resourceId = VisitReportValidation.Trim(request.ReportedByResourceId) ?? report.ReportedByResourceId;
        if (VisitReportValidation.ValidateResourceId(resourceId) is { } resourceFailure)
        {
            return Fail(resourceFailure);
        }

        var expected = request.ExpectedVersion ?? report.Version;
        var now = DateTimeOffset.UtcNow;

        // Apply the optional corrections, tracking which fields the amendment changed (append-only trail).
        var changed = new List<string>();
        if (request.ContentActuals is not null)
        {
            report.ContentActuals = VisitReportMapper.FromInput(request.ContentActuals);
            changed.Add("ContentActuals");
        }

        if (request.Samples is not null)
        {
            report.Samples = VisitReportMapper.FromInput(request.Samples);
            changed.Add("Samples");
        }

        if (request.Feedback is not null)
        {
            report.Feedback = VisitReportMapper.FromInput(request.Feedback);
            changed.Add("Feedback");
        }

        report.Amendments.Add(new VisitReportAmendment
        {
            At = now,
            ByResourceId = resourceId!,
            Reason = reason,
            ChangedFields = changed
        });
        report.ReportStatus = VisitReportStatus.Amended;
        report.AmendedAt = now;
        report.UpdatedAt = now;
        report.UpdatedBy = _actor.ActorName;

        var replaced = await _reports.ReplaceAsync(report, expected, cancellationToken);
        return replaced ? Response<Guid>.Success(report.Id) : ConcurrencyFail();
    }

    private static Response<Guid> Fail(VisitReportValidation.Failure failure)
        => Response<Guid>.Fail(VisitReportValidation.ToErrors(failure), failure.StatusCode);

    private static Response<Guid> ConcurrencyFail()
        => Response<Guid>.Fail(
            new[] { "The report changed since it was loaded. Reload and try again.", VisitReportErrorCodes.ConcurrencyConflict },
            409);
}
