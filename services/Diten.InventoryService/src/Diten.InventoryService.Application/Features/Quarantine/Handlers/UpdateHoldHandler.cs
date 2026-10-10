using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Queries;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>
/// §4.3 <c>updateHold</c> (FU-0175-Q5-01, WP-0174-ITEM) — the analysis note and attachment references while the hold is open. No
/// stock moves. Order:
/// <list type="number">
/// <item>Shape (400): something to change; note ≤ 2000; ≤ 20 attachments, each with a name and a DMS reference.</item>
/// <item>Idempotency (contract x-rules): the caller's earlier call with this key — same hold + same body → the FIRST result
/// (200, O-ITEM-4: the version, note and attachments that call wrote, from its audit row, over the current read; a later edit
/// does not change the answer), otherwise 400 <c>IDEMPOTENCY_KEY_REUSED</c>.</item>
/// <item>The hold (404), still open (409 <c>QUARANTINE_HOLD_CLOSED</c>), no submitted decision on it (409
/// <c>QUARANTINE_DECISION_ALREADY_PENDING</c> — QUARANTINE 1.2.0, FU-0175-ITEM-03: the GxP lock while a decision waits for
/// approval or is approved and not yet applied; it lifts when the decision ends), If-Match equal to its version (409
/// <c>QUARANTINE_CONFLICT</c>).</item>
/// <item>Nothing different → the current hold, nothing written. Otherwise ONE transaction: the hold with version + 1 (the
/// replace is version-conditioned, so a concurrent edit is a 409 too), one <see cref="QuarantineHoldUpdateAuditRecord"/>
/// holding the before / after values and one <c>NoteUpdated</c> history row (QUARANTINE 1.2.0, FU-0175-ITEM-01: who, when and
/// the note lengths / attachment counts before and after).</item>
/// </list>
/// The answer is the <c>getHold</c> body (HoldDetail) read after the write.
/// </summary>
public sealed class UpdateHoldHandler : IRequestHandler<UpdateHoldCommand, Response<HoldDetailDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineSupport _support;
    private readonly ISender _sender;

    public UpdateHoldHandler(IQuarantineRepository repository, QuarantineSupport support, ISender sender)
    {
        _repository = repository;
        _support = support;
        _sender = sender;
    }

    public async Task<Response<HoldDetailDto>> Handle(UpdateHoldCommand request, CancellationToken ct)
    {
        if (!request.NoteGiven && request.Attachments is null)
        {
            return QuarantineSupport.Invalid<HoldDetailDto>("analysisNote or attachments is required.");
        }

        if (request.AnalysisNote is { Length: > QuarantineContract.MaxNoteLength })
        {
            return QuarantineSupport.Invalid<HoldDetailDto>($"analysisNote must be at most {QuarantineContract.MaxNoteLength} characters.");
        }

        if (request.Attachments is { Count: > QuarantineContract.MaxAttachments })
        {
            return QuarantineSupport.Invalid<HoldDetailDto>($"attachments must have at most {QuarantineContract.MaxAttachments} items.");
        }

        if (request.Attachments?.FirstOrDefault(a => string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.DmsRef)) is not null)
        {
            return QuarantineSupport.Invalid<HoldDetailDto>("every attachment needs a name and a dmsRef.");
        }

        // An empty / blank note clears it; attachments are trimmed as written.
        var note = request.NoteGiven ? (string.IsNullOrWhiteSpace(request.AnalysisNote) ? null : request.AnalysisNote.Trim()) : null;
        var attachments = request.Attachments?.Select(a => new HoldAttachment { Name = a.Name.Trim(), DmsRef = a.DmsRef.Trim() }).ToList();
        var fingerprint = QuarantineSupport.Fingerprint(
            [("holdId", request.HoldId.ToString("D")), ("noteGiven", request.NoteGiven ? "1" : "0"), ("analysisNote", note),
             ("attachments", attachments is null ? null : string.Join("\n", attachments.Select(a => a.Name + "\t" + a.DmsRef)))]);

        if (await _repository.FindHoldUpdateAsync(_support.UserId, request.IdempotencyKey, ct) is { } earlier)
        {
            return string.Equals(earlier.RequestFingerprint, fingerprint, StringComparison.Ordinal)
                ? FirstResult(await DetailAsync(request.HoldId, ct), earlier)
                : QuarantineSupport.KeyReused<HoldDetailDto>();
        }

        var hold = await _repository.FindHoldAsync(request.HoldId, ct);
        if (hold is null)
        {
            return QuarantineSupport.Fail<HoldDetailDto>(QuarantineErrorCodes.HoldNotFound, "Hold not found.", 404);
        }

        if (hold.Status == HoldStatus.Decided)
        {
            return QuarantineSupport.Fail<HoldDetailDto>(QuarantineDecisionRules.HoldClosed, "The hold is already decided.", 409);
        }

        if (hold.ActiveDecisionId is not null)
        {
            return QuarantineSupport.Fail<HoldDetailDto>(QuarantineDecisionRules.DecisionAlreadyPending,
                "A decision on this hold is waiting for approval or to be applied; the analysis note is locked until it ends.", 409);
        }

        if (hold.Version != request.ExpectedVersion)
        {
            return QuarantineSupport.Conflict<HoldDetailDto>();
        }

        var newNote = request.NoteGiven ? note : hold.AnalysisNote;
        var newAttachments = attachments ?? hold.Attachments;
        if (string.Equals(newNote, hold.AnalysisNote, StringComparison.Ordinal) && SameAttachments(newAttachments, hold.Attachments))
        {
            return await DetailAsync(hold.Id, ct);
        }

        var now = QuarantineSupport.Now();
        var audit = new QuarantineHoldUpdateAuditRecord
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Action = QuarantineContract.HoldUpdatedAuditAction,
            HoldId = hold.Id,
            HoldNumber = hold.HoldNumber,
            UserId = _support.UserId,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            PreviousVersion = hold.Version,
            Version = request.ExpectedVersion + 1,
            PreviousAnalysisNote = hold.AnalysisNote,
            AnalysisNote = newNote,
            PreviousAttachments = Copy(hold.Attachments),
            Attachments = Copy(newAttachments),
            CorrelationId = _support.CorrelationId,
            OccurredAt = now
        };

        var history = _support.History(HistorySubject.Hold, hold.Id, HistoryAction.NoteUpdated, now);
        history.TenantId = hold.TenantId;
        history.LegalEntityId = hold.LegalEntityId;
        history.NoteChange = new HistoryNoteChange
        {
            PreviousNoteLength = hold.AnalysisNote?.Length ?? 0,
            NoteLength = newNote?.Length ?? 0,
            PreviousAttachmentCount = hold.Attachments.Count,
            AttachmentCount = newAttachments.Count
        };

        hold.AnalysisNote = newNote;
        hold.Attachments = Copy(newAttachments);
        hold.UpdatedAt = now;
        hold.Version = request.ExpectedVersion + 1;
        var saved = await _repository.InTransactionAsync(async (session, c) =>
        {
            if (!await session.ReplaceHoldAsync(hold, request.ExpectedVersion, c)) return false;
            await session.InsertHoldUpdateAuditAsync(audit, c);
            await session.InsertHistoryAsync([history], c);
            return true;
        }, ct);

        return saved ? await DetailAsync(hold.Id, ct) : QuarantineSupport.Conflict<HoldDetailDto>();
    }

    private Task<Response<HoldDetailDto>> DetailAsync(Guid holdId, CancellationToken ct) => _sender.Send(new GetHoldQuery(holdId.ToString("D")), ct);

    /// <summary>O-ITEM-4: the replay answers what the first call wrote (its audit row) — the rest of the body is the current read.</summary>
    private static Response<HoldDetailDto> FirstResult(Response<HoldDetailDto> current, QuarantineHoldUpdateAuditRecord first)
    {
        if (!current.IsSuccessful || current.Data is not { } d)
        {
            return current;
        }

        return Response<HoldDetailDto>.Success(new HoldDetailDto
        {
            HoldId = d.HoldId,
            HoldNumber = d.HoldNumber,
            LegalEntityId = d.LegalEntityId,
            Item = d.Item,
            Lot = d.Lot,
            Warehouse = d.Warehouse,
            Location = d.Location,
            StockStatus = d.StockStatus,
            Quantity = d.Quantity,
            BaseUom = d.BaseUom,
            Source = d.Source,
            SourceDocument = d.SourceDocument,
            EnteredAtUtc = d.EnteredAtUtc,
            CreatedBy = d.CreatedBy,
            ReviewState = d.ReviewState,
            Status = d.Status,
            WaitDays = d.WaitDays,
            TargetDays = d.TargetDays,
            Late = d.Late,
            ActiveDecision = d.ActiveDecision,
            Version = first.Version,
            AnalysisNote = first.AnalysisNote,
            Attachments = first.Attachments.Select(a => new AttachmentDto(a.Name, a.DmsRef)).ToList(),
            ReviewStartedAtUtc = d.ReviewStartedAtUtc,
            ReviewStartedBy = d.ReviewStartedBy,
            DecidedAtUtc = d.DecidedAtUtc,
            History = d.History,
            Movements = d.Movements,
            Decisions = d.Decisions
        });
    }

    private static bool SameAttachments(IReadOnlyList<HoldAttachment> a, IReadOnlyList<HoldAttachment> b)
        => a.Count == b.Count && a.Zip(b).All(p => p.First.Name == p.Second.Name && p.First.DmsRef == p.Second.DmsRef);

    private static List<HoldAttachment> Copy(IEnumerable<HoldAttachment> source)
        => source.Select(a => new HoldAttachment { Name = a.Name, DmsRef = a.DmsRef }).ToList();
}
