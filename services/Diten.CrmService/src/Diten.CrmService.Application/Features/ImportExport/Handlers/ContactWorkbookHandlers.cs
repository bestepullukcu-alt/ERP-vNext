using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using DomainAccount = Diten.CrmService.Domain.Entities.Account;
using DomainContact = Diten.CrmService.Domain.Entities.Contact;

namespace Diten.CrmService.Application.Features.ImportExport.Handlers;

/// <summary>
/// MOD-0150 Import/Export Task 1 — XLSX template and existing-data export handlers.
/// Template and export share <see cref="ContactWorkbookSchema"/> and <see cref="ContactWorkbookBuilder"/>, so an
/// exported file is column-identical to the template and round-trips into the future import reader.
/// Audit stays counts/flags only (never row payload); Notes are opt-in; PII never reaches the file name.
/// </summary>
internal static class ContactWorkbookSupport
{
    internal const string DefaultSourceSystem = "default";

    /// <summary>Reads every workbook reference set once. An unpublished set comes back as NotPublished — no fallback.</summary>
    internal static async Task<IReadOnlyList<ReferenceSetSnapshot>> ReadReferenceSetsAsync(
        IReferenceDataCatalogReader reader, CancellationToken cancellationToken)
    {
        var snapshots = new List<ReferenceSetSnapshot>();
        foreach (var setCode in ContactWorkbookSchema.AllSets)
        {
            snapshots.Add(await reader.GetPublishedValuesAsync(setCode, cancellationToken));
        }

        return snapshots;
    }

    internal static async Task<IReadOnlyList<IReadOnlyList<string?>>?> ReadAccountLookupAsync(
        IAccountRepository accounts, Guid tenantId, CancellationToken cancellationToken)
    {
        var (items, _) = await accounts.ListAsync(tenantId, null, 1, ContactWorkbookSchema.MaxAccountLookupRows, cancellationToken);
        return items
            .OrderBy(a => a.AccountCode, StringComparer.OrdinalIgnoreCase)
            .Select(a => (IReadOnlyList<string?>)new string?[]
            {
                a.Id.ToString(), a.AccountCode, a.AccountName, a.AccountType, a.CountryRef, a.CityRef
            })
            .ToList();
    }

    internal static string Iso(DateTimeOffset? value) => value?.ToString("yyyy-MM-dd") ?? string.Empty;

    /// <summary>Deterministic, PII-free download name: no contact name, e-mail or filter value ever reaches the file name.</summary>
    internal static string FileName(string prefix, Guid tenantId, string correlationId)
    {
        var tenantShort = tenantId.ToString("N")[..8];
        return $"{prefix}-{tenantShort}-{DateTime.UtcNow:yyyyMMddHHmm}-{correlationId}.xlsx";
    }
}

public sealed class BuildContactTemplateWorkbookHandler
    : IRequestHandler<BuildContactTemplateWorkbookQuery, Response<ExportFileDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IReferenceDataCatalogReader _referenceCatalog;
    private readonly IAccountRepository _accounts;
    private readonly IContactAuditPublisher _audit;

    public BuildContactTemplateWorkbookHandler(
        ITenantContext tenant, IReferenceDataCatalogReader referenceCatalog, IAccountRepository accounts, IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _referenceCatalog = referenceCatalog;
        _accounts = accounts;
        _audit = audit;
    }

    public async Task<Response<ExportFileDto>> Handle(BuildContactTemplateWorkbookQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ExportFileDto>.Fail("Tenant context is required.", 400);
        }

        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var referenceSets = await ContactWorkbookSupport.ReadReferenceSetsAsync(_referenceCatalog, cancellationToken);
        var accountRows = request.IncludeAccountsSheet
            ? await ContactWorkbookSupport.ReadAccountLookupAsync(_accounts, tenantId, cancellationToken)
            : null;

        var workbook = ContactWorkbookBuilder.Build(new ContactWorkbookRequest(
            IsTemplate: true,
            Options: ContactWorkbookOptions.Template,
            ContactRows: Array.Empty<IReadOnlyList<string?>>(),
            AccountLinkRows: Array.Empty<IReadOnlyList<string?>>(),
            ReferenceSets: referenceSets,
            AccountRows: accountRows,
            GeneratedAtUtc: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
            CorrelationId: correlationId));

        // Template carries no contact data — the audit note records the shape only.
        await _audit.PublishAsync("crm.contact.import-template.downloaded", tenantId, Guid.Empty,
            $"corr={correlationId} format=xlsx accountsSheet={request.IncludeAccountsSheet} sets={referenceSets.Count}", cancellationToken);

        return Response<ExportFileDto>.Success(new ExportFileDto(
            workbook,
            ContactWorkbookSupport.FileName("contacts-template", tenantId, correlationId),
            ExportFileDto.XlsxContentType));
    }
}

public sealed class ExportContactsWorkbookHandler
    : IRequestHandler<ExportContactsWorkbookQuery, Response<ExportFileDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContactRepository _contacts;
    private readonly IContactExternalReferenceRepository _externalRefs;
    private readonly IAccountContactLinkRepository _links;
    private readonly IAccountRepository _accounts;
    private readonly IReferenceDataCatalogReader _referenceCatalog;
    private readonly IContactAuditPublisher _audit;

    public ExportContactsWorkbookHandler(
        ITenantContext tenant,
        IContactRepository contacts,
        IContactExternalReferenceRepository externalRefs,
        IAccountContactLinkRepository links,
        IAccountRepository accounts,
        IReferenceDataCatalogReader referenceCatalog,
        IContactAuditPublisher audit)
    {
        _tenant = tenant;
        _contacts = contacts;
        _externalRefs = externalRefs;
        _links = links;
        _accounts = accounts;
        _referenceCatalog = referenceCatalog;
        _audit = audit;
    }

    public async Task<Response<ExportFileDto>> Handle(ExportContactsWorkbookQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ExportFileDto>.Fail("Tenant context is required.", 400);
        }

        var options = request.Options;
        var correlationId = Guid.NewGuid().ToString("N")[..8];

        var contacts = Filter(await _contacts.ListAllAsync(tenantId, cancellationToken), options);
        if (contacts.Count > ContactWorkbookSchema.MaxContactRows)
        {
            return Response<ExportFileDto>.Fail(
                $"The export matches {contacts.Count} contacts, which exceeds the {ContactWorkbookSchema.MaxContactRows} row limit. "
                + "Narrow the export with the contact type, status, country or updated-after filters and try again.", 400);
        }

        var externalRefs = (await _externalRefs.ListAllAsync(tenantId, cancellationToken))
            .GroupBy(r => r.ContactId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SourceSystem, StringComparer.OrdinalIgnoreCase).First());

        var contactRows = contacts
            .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(c => ToContactRow(c, externalRefs.GetValueOrDefault(c.Id), options.IncludeNotes))
            .ToList();

        var linkRows = new List<IReadOnlyList<string?>>();
        if (options.IncludeLinks)
        {
            var contactIds = contacts.Select(c => c.Id).ToHashSet();
            var links = (await _links.ListAllAsync(tenantId, cancellationToken))
                .Where(l => contactIds.Contains(l.ContactId))
                .Where(l => options.IncludeHistorical || !RelationshipLifecycle.IsClosed(l.Status))
                .ToList();

            if (links.Count > ContactWorkbookSchema.MaxLinkRows)
            {
                return Response<ExportFileDto>.Fail(
                    $"The export matches {links.Count} account links, which exceeds the {ContactWorkbookSchema.MaxLinkRows} row limit. "
                    + "Narrow the export with a filter, or export without related accounts.", 400);
            }

            var accountMap = await BuildAccountMapAsync(tenantId, links.Select(l => l.AccountId), cancellationToken);
            linkRows.AddRange(links
                .OrderBy(l => l.ContactId).ThenBy(l => l.RoleCode, StringComparer.OrdinalIgnoreCase)
                .Select(l => ToLinkRow(l, accountMap.GetValueOrDefault(l.AccountId), externalRefs.GetValueOrDefault(l.ContactId), options.IncludeNotes)));
        }

        var referenceSets = await ContactWorkbookSupport.ReadReferenceSetsAsync(_referenceCatalog, cancellationToken);
        var accountRows = options.IncludeAccountsSheet
            ? await ContactWorkbookSupport.ReadAccountLookupAsync(_accounts, tenantId, cancellationToken)
            : null;

        var workbook = ContactWorkbookBuilder.Build(new ContactWorkbookRequest(
            IsTemplate: false,
            Options: options,
            ContactRows: contactRows,
            AccountLinkRows: linkRows,
            ReferenceSets: referenceSets,
            AccountRows: accountRows,
            GeneratedAtUtc: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
            CorrelationId: correlationId));

        // PII-safe audit: counts, option flags and filter FIELD NAMES only — never a name/phone/e-mail/filter value.
        var filters = options.AppliedFilterFields();
        await _audit.PublishAsync("crm.contact.exported", tenantId, Guid.Empty,
            $"corr={correlationId} format=xlsx count={contactRows.Count} links={linkRows.Count} "
            + $"includeLinks={options.IncludeLinks} includeHistorical={options.IncludeHistorical} includeNotes={options.IncludeNotes} "
            + $"accountsSheet={options.IncludeAccountsSheet} filters={(filters.Count == 0 ? "none" : string.Join("|", filters))}",
            cancellationToken);

        return Response<ExportFileDto>.Success(new ExportFileDto(
            workbook,
            ContactWorkbookSupport.FileName("contacts-export", tenantId, correlationId),
            ExportFileDto.XlsxContentType));
    }

    private static IReadOnlyList<DomainContact> Filter(IReadOnlyList<DomainContact> contacts, ContactWorkbookOptions options)
        => contacts
            .Where(c => string.IsNullOrWhiteSpace(options.ContactType)
                        || string.Equals(c.ContactType, options.ContactType, StringComparison.OrdinalIgnoreCase))
            .Where(c => string.IsNullOrWhiteSpace(options.Status)
                        || string.Equals(c.Status, options.Status, StringComparison.OrdinalIgnoreCase))
            .Where(c => string.IsNullOrWhiteSpace(options.Country)
                        || string.Equals(c.CountryRef, options.Country, StringComparison.OrdinalIgnoreCase))
            .Where(c => options.UpdatedAfter is not { } after || (c.UpdatedAt ?? c.CreatedAt) >= after)
            .ToList();

    private async Task<Dictionary<Guid, DomainAccount>> BuildAccountMapAsync(
        Guid tenantId, IEnumerable<Guid> accountIds, CancellationToken cancellationToken)
    {
        var (items, _) = await _accounts.ListAsync(tenantId, null, 1, ContactWorkbookSchema.MaxAccountLookupRows, cancellationToken);
        var map = items.ToDictionary(a => a.Id);

        // Tenants with more accounts than the lookup page: resolve only the ids the links actually reference.
        foreach (var id in accountIds.Distinct().Where(id => !map.ContainsKey(id)))
        {
            if (await _accounts.GetByIdAsync(tenantId, id, cancellationToken) is { } account)
            {
                map[account.Id] = account;
            }
        }

        return map;
    }

    private static IReadOnlyList<string?> ToContactRow(DomainContact c, ContactExternalReference? externalRef, bool includeNotes)
        => new string?[]
        {
            string.Empty,                       // Operation — the user decides on re-import; never pre-filled.
            c.Id.ToString(),                    // ContactId — stable identity for round-trip matching.
            externalRef?.SourceSystem ?? string.Empty,
            externalRef?.ExternalId ?? string.Empty,
            c.FirstName,
            c.LastName,
            c.DisplayName,
            c.ContactType,
            c.Status,
            c.Gender ?? string.Empty,
            c.ProfessionalTitle ?? string.Empty,
            c.Specialty ?? string.Empty,
            c.Department ?? string.Empty,
            c.CountryRef ?? string.Empty,
            c.CityRef ?? string.Empty,
            c.DistrictRef ?? string.Empty,
            c.AddressLine ?? string.Empty,
            c.PostalCode ?? string.Empty,
            c.PreferredLanguage ?? string.Empty,
            c.PhoneCountryCode ?? string.Empty,
            c.Phone ?? string.Empty,
            c.Email ?? string.Empty,
            // Notes may carry free-text special-category data → opt-in only (PII/KVKK hardening decision).
            includeNotes ? c.Notes ?? string.Empty : string.Empty
        };

    private static IReadOnlyList<string?> ToLinkRow(
        AccountContactLink link, DomainAccount? account, ContactExternalReference? contactRef, bool includeNotes)
        => new string?[]
        {
            string.Empty,                       // Operation
            link.Id.ToString(),                 // LinkId
            link.ContactId.ToString(),
            contactRef?.SourceSystem ?? string.Empty,
            contactRef?.ExternalId ?? string.Empty,
            link.AccountId.ToString(),
            account?.AccountCode ?? string.Empty,
            account?.AccountName ?? string.Empty,   // read-only helper
            link.RoleCode,
            link.IsPrimary ? "TRUE" : "FALSE",
            link.Status,
            ContactWorkbookSupport.Iso(link.ValidFrom),
            ContactWorkbookSupport.Iso(link.ValidTo),
            link.ReportsToContactId?.ToString() ?? string.Empty,
            includeNotes ? link.Notes ?? string.Empty : string.Empty,
            // Cross-country justification is operator free text and may carry personal data → same opt-in gate as Notes.
            includeNotes ? link.CrossCountryReason ?? string.Empty : string.Empty
        };
}
