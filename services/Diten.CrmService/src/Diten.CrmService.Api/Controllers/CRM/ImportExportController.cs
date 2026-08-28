using System.Text;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ImportExport;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0150 FU06 — Contact / AccountContactLink / AccountRelationship import (JSON rows → validated result summary,
/// with dry-run) and export / import-template (CSV). Routes sit under the existing Gateway wildcards
/// (<c>/api/crm/contacts/{everything}</c>, <c>/api/crm/accounts/{everything}</c>) — no new Gateway route. Reference
/// values are validated against MOD-0048 published sets during import (never a local fallback).
/// </summary>
[Authorize]
public sealed class ImportExportController : CustomBaseController
{
    private const string CsvContentType = "text/csv";
    private const string XlsxFormat = "xlsx";
    private const string AccountContactReadPermission = "crm.account-contact.read";
    private const string AccountReadPermission = "crm.account.read";
    // MOD-0150 Task 2 — row-level capabilities for the workbook import. Existing MOD-0018 keys only.
    private const string ContactCreatePermission = "crm.contact.create";
    private const string ContactUpdatePermission = "crm.contact.update";
    private const string AccountContactManagePermission = "crm.account-contact.manage";
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private readonly IMediator _mediator;

    public ImportExportController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ---------- Contacts ----------

    [HttpPost("api/crm/contacts/import")]
    [HasPermission("crm.contact.import")]
    public async Task<IActionResult> ImportContacts([FromBody] ImportContactsCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command, cancellationToken));

    /// <summary>
    /// Contact export. Default (no <c>format</c>, or <c>format=csv</c>) is the unchanged FU06 CSV. <c>format=xlsx</c>
    /// returns the round-trip workbook, optionally with related account links, historical links, notes and an account
    /// lookup sheet. Sub-scopes are fail-closed: asking for links/accounts without the owning read permission is a 403,
    /// never a silently trimmed file.
    /// </summary>
    [HttpGet("api/crm/contacts/export")]
    [HasPermission("crm.contact.export")]
    public async Task<IActionResult> ExportContacts(
        [FromQuery] string? format,
        [FromQuery] bool includeLinks,
        [FromQuery] bool includeHistorical,
        [FromQuery] bool includeNotes,
        [FromQuery] bool includeAccounts,
        [FromQuery] string? contactType,
        [FromQuery] string? status,
        [FromQuery] string? country,
        [FromQuery] DateTimeOffset? updatedAfter,
        CancellationToken cancellationToken)
    {
        if (!IsXlsx(format))
        {
            return CsvResult(await _mediator.Send(new ExportContactsQuery(), cancellationToken), "contacts.csv");
        }

        if (RequireSubScope(includeLinks, AccountContactReadPermission, "related account links") is { } linkDenied)
        {
            return linkDenied;
        }

        if (RequireSubScope(includeAccounts, AccountReadPermission, "the account lookup sheet") is { } accountDenied)
        {
            return accountDenied;
        }

        var options = new ContactWorkbookOptions(
            includeLinks, includeHistorical && includeLinks, includeNotes, includeAccounts,
            contactType, status, country, updatedAfter);

        return FileResultFrom(await _mediator.Send(new ExportContactsWorkbookQuery(options), cancellationToken));
    }

    /// <summary>Import template. Default stays the FU06 CSV header; <c>format=xlsx</c> returns the multi-sheet workbook
    /// (Instructions / Contacts / AccountLinks / ReferenceData [+ Accounts when the caller may read accounts]).</summary>
    [HttpGet("api/crm/contacts/import-template")]
    [HasPermission("crm.contact.import")]
    public async Task<IActionResult> ContactTemplate(
        [FromQuery] string? format, [FromQuery] bool includeAccounts, CancellationToken cancellationToken)
    {
        if (!IsXlsx(format))
        {
            return TemplateResult(ImportTemplates.ContactHeader, "contacts-template.csv");
        }

        if (RequireSubScope(includeAccounts, AccountReadPermission, "the account lookup sheet") is { } denied)
        {
            return denied;
        }

        return FileResultFrom(await _mediator.Send(new BuildContactTemplateWorkbookQuery(includeAccounts), cancellationToken));
    }

    /// <summary>
    /// MOD-0150 Import/Export Task 2 — uploads a Task 1 workbook. <c>dryRun=true</c> (the default) validates and
    /// returns the preview without writing anything; <c>dryRun=false</c> runs the same validation and then applies the
    /// rows that passed. Row-level capabilities are resolved from the caller's claims, so a user who may import
    /// contacts but not manage account links gets a precise per-row message instead of a silently trimmed import.
    /// The JSON row import (FU06) and the CSV/XLSX export endpoints are untouched.
    /// </summary>
    [HttpPost("api/crm/contacts/import-file")]
    [HasPermission("crm.contact.import")]
    [RequestSizeLimit(MaxUploadBytes)]
    public Task<IActionResult> ImportContactWorkbook(
        IFormFile? file, [FromQuery] bool dryRun = true, [FromQuery] bool strictMode = false,
        CancellationToken cancellationToken = default)
        => HandleWorkbookUploadAsync(file, dryRun, strictMode, cancellationToken);

    /// <summary>Applies a previously previewed workbook. Same engine, <c>dryRun=false</c> — a separate route so the
    /// destructive call can never be reached by a stray GET/preview request.</summary>
    [HttpPost("api/crm/contacts/import-file/apply")]
    [HasPermission("crm.contact.import")]
    [RequestSizeLimit(MaxUploadBytes)]
    public Task<IActionResult> ApplyContactWorkbook(
        IFormFile? file, [FromQuery] bool strictMode = false, CancellationToken cancellationToken = default)
        => HandleWorkbookUploadAsync(file, dryRun: false, strictMode, cancellationToken);

    private async Task<IActionResult> HandleWorkbookUploadAsync(
        IFormFile? file, bool dryRun, bool strictMode, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return CreateActionResultInstance(Response<ImportPreviewDto>.Fail("Select an .xlsx file to import.", 400));
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return CreateActionResultInstance(Response<ImportPreviewDto>.Fail(
                "Only .xlsx files produced by the import template or the export are supported.", 400));
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        // The file name is never persisted, logged or echoed back — it can carry personal data.
        var capabilities = new ImportCapabilities(
            PermissionClaims.HasPermission(User, ContactCreatePermission),
            PermissionClaims.HasPermission(User, ContactUpdatePermission),
            PermissionClaims.HasPermission(User, AccountContactManagePermission));

        var command = new ImportContactWorkbookCommand(buffer.ToArray(), dryRun, strictMode, capabilities);
        return CreateActionResultInstance(await _mediator.Send(command, cancellationToken));
    }

    // ---------- Account ↔ Contact links ----------

    [HttpPost("api/crm/accounts/contact-links/import")]
    [HasPermission("crm.account-contact.manage")]
    public async Task<IActionResult> ImportAccountContacts([FromBody] ImportAccountContactsCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command, cancellationToken));

    [HttpGet("api/crm/accounts/contact-links/export")]
    [HasPermission("crm.account-contact.read")]
    public async Task<IActionResult> ExportAccountContacts(CancellationToken cancellationToken)
        => CsvResult(await _mediator.Send(new ExportAccountContactsQuery(), cancellationToken), "account-contacts.csv");

    [HttpGet("api/crm/accounts/contact-links/import-template")]
    [HasPermission("crm.account-contact.manage")]
    public IActionResult AccountContactTemplate()
        => TemplateResult(ImportTemplates.AccountContactHeader, "account-contacts-template.csv");

    // ---------- Account ↔ Account relationships ----------

    [HttpPost("api/crm/accounts/relationships/import")]
    [HasPermission("crm.account-relationship.manage")]
    public async Task<IActionResult> ImportAccountRelationships([FromBody] ImportAccountRelationshipsCommand command, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(command, cancellationToken));

    [HttpGet("api/crm/accounts/relationships/export")]
    [HasPermission("crm.account-relationship.read")]
    public async Task<IActionResult> ExportAccountRelationships(CancellationToken cancellationToken)
        => CsvResult(await _mediator.Send(new ExportAccountRelationshipsQuery(), cancellationToken), "account-relationships.csv");

    [HttpGet("api/crm/accounts/relationships/import-template")]
    [HasPermission("crm.account-relationship.manage")]
    public IActionResult AccountRelationshipTemplate()
        => TemplateResult(ImportTemplates.AccountRelationshipHeader, "account-relationships-template.csv");

    // ---------- helpers ----------

    private IActionResult CsvResult(Response<string> response, string fileName)
    {
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        return File(Encoding.UTF8.GetBytes(response.Data), CsvContentType, fileName);
    }

    private IActionResult TemplateResult(IReadOnlyList<string> header, string fileName)
        => File(Encoding.UTF8.GetBytes(ImportTemplates.TemplateCsv(header)), CsvContentType, fileName);

    private static bool IsXlsx(string? format)
        => string.Equals(format?.Trim(), XlsxFormat, StringComparison.OrdinalIgnoreCase);

    /// <summary>Fail-closed gate for an optional workbook sheet that exposes another module's read model.</summary>
    private IActionResult? RequireSubScope(bool requested, string permission, string what)
        => !requested || PermissionClaims.HasPermission(User, permission)
            ? null
            : CreateActionResultInstance(Response<ExportFileDto>.Fail(
                $"Including {what} requires the '{permission}' permission.", 403));

    private IActionResult FileResultFrom(Response<ExportFileDto> response)
        => !response.IsSuccessful || response.Data is null
            ? CreateActionResultInstance(response)
            : File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
}
