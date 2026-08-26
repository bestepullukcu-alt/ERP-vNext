using System.Text;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// DCP-005 slice 2 — importing the controlled-document reference list.
///
/// <para>Dry-run then commit, the same two-step the folder taxonomy uses: an import that can only be attempted
/// blind is one nobody runs twice.</para>
/// </summary>
public sealed class DryRunDocumentReferenceListHandler
    : IRequestHandler<DryRunDocumentReferenceListCommand, Response<DocumentReferenceListDryRunResult>>
{
    private readonly IDocumentReferenceListRepository _lists;

    public DryRunDocumentReferenceListHandler(IDocumentReferenceListRepository lists) => _lists = lists;

    public async Task<Response<DocumentReferenceListDryRunResult>> Handle(
        DryRunDocumentReferenceListCommand command, CancellationToken ct)
    {
        if (!TryDecode(command.Request.ContentBase64, out var csv, out var decodeError))
        {
            return Response<DocumentReferenceListDryRunResult>.Fail(
                decodeError!, 400, TaskReasonCodes.DocumentListInvalid, command.CorrelationId);
        }

        var parsed = DocumentReferenceListParser.Parse(csv);
        var existing = await _lists.FindVersionByHashAsync(parsed.ContentHash, ct);

        return Response<DocumentReferenceListDryRunResult>.Success(
            new DocumentReferenceListDryRunResult(
                parsed.Entries.Count,
                parsed.LinkableCount,
                parsed.Entries.Count - parsed.LinkableCount,
                parsed.Errors,
                parsed.MissingColumns,
                parsed.UnreadColumns,
                parsed.ContentHash,
                existing?.ListVersion),
            200, command.CorrelationId);
    }

    internal static bool TryDecode(string? base64, out string content, out string? error)
    {
        content = string.Empty;
        error = null;
        try
        {
            content = Encoding.UTF8.GetString(Convert.FromBase64String(base64 ?? string.Empty));
            return true;
        }
        catch (FormatException)
        {
            error = "The uploaded content is not valid base64.";
            return false;
        }
    }
}

/// <summary>
/// Store the list as a VERSION.
///
/// <para>⚠ <b>IDENTICAL BYTES ARE RECOGNISED, NOT DUPLICATED.</b> Re-uploading the same file returns the
/// version that already holds it instead of writing a second copy — a register imported twice by two people on
/// the same afternoon must not produce two "current" lists, because the next question is always "which one did
/// the task resolve against".</para>
///
/// <para>⚠ <b>ENTRIES ARE NEVER UPDATED IN PLACE.</b> A new import is a new version with its own rows; the old
/// version keeps its own. That is what lets a closed task point at what it actually saw.</para>
/// </summary>
public sealed class ImportDocumentReferenceListHandler
    : IRequestHandler<ImportDocumentReferenceListCommand, Response<DocumentReferenceListVersionDto>>
{
    private readonly IDocumentReferenceListRepository _lists;
    private readonly ITenantContext _tenants;
    private readonly ICurrentUserContext _currentUser;

    public ImportDocumentReferenceListHandler(
        IDocumentReferenceListRepository lists, ITenantContext tenants, ICurrentUserContext currentUser)
    {
        _lists = lists;
        _tenants = tenants;
        _currentUser = currentUser;
    }

    public async Task<Response<DocumentReferenceListVersionDto>> Handle(
        ImportDocumentReferenceListCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.ListVersion))
        {
            return Response<DocumentReferenceListVersionDto>.Fail(
                "An import needs a list version.", 400, TaskReasonCodes.DocumentListInvalid, command.CorrelationId);
        }

        if (!DryRunDocumentReferenceListHandler.TryDecode(request.ContentBase64, out var csv, out var decodeError))
        {
            return Response<DocumentReferenceListVersionDto>.Fail(
                decodeError!, 400, TaskReasonCodes.DocumentListInvalid, command.CorrelationId);
        }

        var parsed = DocumentReferenceListParser.Parse(csv);
        if (parsed.Errors.Count > 0)
        {
            /*
             * ⚠ ALL OR NOTHING. A partially imported register is worse than none: the search would answer
             * confidently from a list nobody can characterise, and the rows that failed would be invisible.
             */
            return Response<DocumentReferenceListVersionDto>.Fail(
                string.Join(" ", parsed.Errors.Take(5)), 400,
                TaskReasonCodes.DocumentListInvalid, command.CorrelationId);
        }

        var already = await _lists.FindVersionByHashAsync(parsed.ContentHash, ct);
        if (already is not null)
        {
            /*
             * Not an error — the caller asked for a state that already holds. Reported as a refusal with its
             * own code so the screen can say "this is already version X" rather than "something went wrong".
             */
            return Response<DocumentReferenceListVersionDto>.Fail(
                $"This exact file is already stored as list version '{already.ListVersion}'.",
                409, TaskReasonCodes.DocumentListAlreadyImported, command.CorrelationId);
        }

        var version = await _lists.CreateVersionAsync(new DocumentReferenceListVersion
        {
            TenantId = _tenants.TenantId,
            SourceKey = request.SourceKey?.Trim() ?? string.Empty,
            ListVersion = request.ListVersion.Trim(),
            ContentHash = parsed.ContentHash,
            FileName = request.FileName?.Trim() ?? string.Empty,
            EntryCount = parsed.Entries.Count,
            LinkableCount = parsed.LinkableCount,
            CreatedBy = _currentUser.ActorName
        }, ct);

        /*
         * ⚠ RE-PARSED WITH THE VERSION ID, not patched afterwards. `ListVersionId` is `required`, so the rows
         * cannot be built first and adopted later — which is the type system refusing to let an entry exist
         * without knowing which import it belongs to.
         */
        var owned = DocumentReferenceListParser.Parse(
            csv, _tenants.TenantId, version.Id, _currentUser.ActorName);

        await _lists.AddEntriesAsync(owned.Entries, ct);

        return Response<DocumentReferenceListVersionDto>.Success(
            new DocumentReferenceListVersionDto(
                version.Id, version.ListVersion, version.SourceKey, version.FileName,
                version.ContentHash, version.EntryCount, version.LinkableCount, version.ImportedAt),
            201, command.CorrelationId);
    }
}
