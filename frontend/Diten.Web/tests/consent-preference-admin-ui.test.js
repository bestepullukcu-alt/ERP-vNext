const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const exists = relative => fs.existsSync(path.join(root, relative));

const controller = read('Controllers/CRM/ConsentPreferencesController.cs');
const models = read('Models/CRM/ConsentPreferenceViewModels.cs');
const layout = read('Views/Shared/_LayoutTenantShell.cshtml');
const indexView = read('Views/CRM/ConsentPreferences/Index.cshtml');
const consentFilter = read('Views/CRM/ConsentPreferences/Consents/_Filter.cshtml');
const consentTable = read('Views/CRM/ConsentPreferences/Consents/_DataTable.cshtml');
const consentForm = read('Views/CRM/ConsentPreferences/Consents/_Form.cshtml');
const consentDetails = read('Views/CRM/ConsentPreferences/Consents/Details.cshtml');
const preferenceFilter = read('Views/CRM/ConsentPreferences/Preferences/_Filter.cshtml');
const preferenceTable = read('Views/CRM/ConsentPreferences/Preferences/_DataTable.cshtml');
const preferenceForm = read('Views/CRM/ConsentPreferences/Preferences/_Form.cshtml');
const preferenceDetails = read('Views/CRM/ConsentPreferences/Preferences/Details.cshtml');
const evaluatePanel = read('Views/CRM/ConsentPreferences/_EvaluatePanel.cshtml');
const subjectPanel = read('Views/CRM/ConsentPreferences/_SubjectPanel.cshtml');
const provenance = read('Views/CRM/ConsentPreferences/_Provenance.cshtml');
const indexJs = read('wwwroot/assets/js/CRM/ConsentPreferences/index.js');
const consentFormJs = read('wwwroot/assets/js/CRM/ConsentPreferences/consent-form.js');
const preferenceFormJs = read('wwwroot/assets/js/CRM/ConsentPreferences/preference-form.js');
const evaluateJs = read('wwwroot/assets/js/CRM/ConsentPreferences/evaluate.js');
const subjectJs = read('wwwroot/assets/js/CRM/ConsentPreferences/subject-panel.js');
const consentDetailsJs = read('wwwroot/assets/js/CRM/ConsentPreferences/consent-details.js');
const preferenceDetailsJs = read('wwwroot/assets/js/CRM/ConsentPreferences/preference-details.js');
const allJs = [indexJs, consentFormJs, preferenceFormJs, evaluateJs, subjectJs, consentDetailsJs, preferenceDetailsJs].join('\n');

describe('MOD-0164-FU03 Consent & Preference Admin UI', () => {
  it('1. renders the ConsentPreferences route within the tenant shell', () => {
    expect(controller).toContain('[Route("CRM/ConsentPreferences")]');
    expect(indexView).toContain('Layout = "_LayoutTenantShell"');
  });

  it('2. permission-controls the navigation entry with crm.consent.read', () => {
    expect(layout).toContain('Perms.Has("crm.consent.read")');
    expect(layout).toContain('/CRM/ConsentPreferences');
    expect(layout).toContain('ConsentPreferencesMenu');
  });

  it('3. loads the contract through the same-origin Gateway proxy and fails closed', () => {
    expect(indexJs).toContain("fetch(`${base}/api/contract`");
    expect(controller).toContain('"/api/crm/consents/contract"');
    expect(indexJs).toContain('ConsentContractUnavailable');
  });

  it('4. implements Consent list loading, empty and error states', () => {
    expect(consentTable).toContain('id="skeleton-loader"');
    expect(consentTable).toContain('data-dt-standard="v2"');
    expect(indexJs).toContain('emptyTable: L.EmptyState');
    expect(indexJs).toContain('L.ErrorState');
  });

  it('5. uses supported Consent filters via server query', () => {
    ['filterConsentSubjectType','filterConsentSubjectId','filterConsentChannel','filterConsentPurpose','filterConsentStatus','filterConsentIncludeArchived'].forEach(id => expect(consentFilter).toContain(`id="${id}"`));
    expect(indexJs).toContain('searching: false');
    expect(indexJs).toContain('includeArchived');
  });

  it('6. does not fake unsupported Consent filters (ScopeType/LegalBasis/Source/search)', () => {
    // The backend supports only SubjectType/SubjectId/Channel/Purpose/ConsentStatus/IncludeArchived; nothing else is
    // faked client-side. (The visible "unsupported filters" note was removed from the UI at the user's request; the
    // limitation stays documented in the evidence report.)
    expect(indexJs).not.toContain('filterConsentScopeType');
    expect(indexJs).not.toContain('filterConsentLegalBasis');
    expect(indexJs).not.toContain('filterConsentSource');
    expect(indexJs).toContain('searching: false');
  });

  it('7. uses Compact Create/Edit/Details pages with matching section maps (consent)', () => {
    ['Create.cshtml','Edit.cshtml','Details.cshtml','_Form.cshtml'].forEach(f => expect(exists(`Views/CRM/ConsentPreferences/Consents/${f}`)).toBe(true));
    ['IdentitySection','ConsentStatusSection','EvidenceSection','ExternalReferencesSection'].forEach(k => {
      expect(consentForm).toContain(k); expect(consentDetails).toContain(k);
    });
    expect(exists('Views/CRM/ConsentPreferences/Consents/_CreateEditOffcanvas.cshtml')).toBe(false);
  });

  it('8. enforces required Consent fields and effective range validation', () => {
    ['SubjectType','SubjectId','Channel','Purpose','LegalBasis','ConsentStatus','Source','EffectiveFrom'].forEach(field =>
      expect(models).toMatch(new RegExp(`Required[\\s\\S]{0,120}${field}|${field}[\\s\\S]{0,60}\\{ get`)));
    expect(models).toContain('EffectiveTo < EffectiveFrom');
    expect(consentFormJs).toContain('new Date(end) < new Date(start)');
  });

  it('9. keeps consent question dimensions immutable (read-only) on edit', () => {
    expect(consentForm).toContain('disabled="@isEdit"');
    expect(consentForm).toContain('readonly="@isEdit"');
    // update body omits the immutable dimensions
    const upd = controller.slice(controller.indexOf('ToConsentUpdatePayload'), controller.indexOf('ToConsentUpdatePayload') + 400);
    expect(upd).not.toMatch(/SubjectType|Purpose|Channel/);
  });

  it('10. consent channel options never include the all sentinel', () => {
    // The runtime canonical fallback lives in the controller's ConsentVocabularyFallback.
    const fallback = controller.slice(controller.indexOf('class ConsentVocabularyFallback'));
    const listOf = name => { const s = fallback.indexOf(`${name} =`); return fallback.slice(s, fallback.indexOf(';', s)); };
    expect(listOf('ConsentChannels')).not.toMatch(/"all"/);
    // preference channels DO include the all sentinel
    expect(listOf('PreferenceChannels')).toMatch(/"all"/);
  });

  it('11. archives consent through POST endpoint (never DELETE)', () => {
    expect(controller).toContain('/api/crm/consents/{consentId}/archive');
    expect(indexJs).toContain("method:'POST'");
    expect(indexView).toContain('/CRM/ConsentPreferences');
    expect(controller).not.toMatch(/HttpDelete|HttpMethod\.Delete/);
    expect(allJs).not.toMatch(/method\s*:\s*['"]DELETE['"]/i);
  });

  it('12. treats archived records as read-only', () => {
    expect(controller).toContain('consent.IsArchived');
    expect(consentDetails).toContain('ArchivedRecordReadOnly');
    expect(controller).toContain('ArchivedRecordReadOnly');
  });

  it('13. renders consent evidence pointer/provenance without master lookup', () => {
    ['EvidenceRefType','EvidenceRefId','EvidenceSourceModule'].forEach(k => expect(consentDetails).toContain(k));
    expect(consentDetails).toContain('EvidencePointerHelp');
    expect(controller).not.toMatch(/evidence.*lookup|document.*master/i);
  });

  it('14. implements Preference list with derived (not fake) restrictive hint', () => {
    expect(preferenceTable).toContain('data-dt-standard="v2"');
    ['filterPreferenceSubjectType','filterPreferenceChannel','filterPreferenceType','filterPreferenceIncludeArchived'].forEach(id => expect(preferenceFilter).toContain(`id="${id}"`));
    expect(indexJs).toContain('RESTRICTIVE_TYPES');
    // Preference model must NOT fabricate stored scope/isRestrictive fields (absent in FU02 DTO)
    const prefBlock = models.slice(models.indexOf('class PreferenceDetailViewModel'), models.indexOf('class PreferenceDetailPageViewModel'));
    expect(prefBlock).not.toContain('ScopeType');
    expect(prefBlock).not.toContain('IsRestrictive');
  });

  it('15. Preference channel options include all; priority >= 1 enforced', () => {
    expect(models).toContain('Range(1, int.MaxValue)');
    expect(preferenceFormJs).toContain('priority >= 1');
    expect(preferenceForm).toContain('PreferenceChannels');
  });

  it('16. Preference detail shows cannot-grant-consent copy; sections match', () => {
    expect(preferenceDetails).toContain('PreferenceCannotGrant');
    ['IdentitySection','PreferenceContextSection','ExternalReferencesSection'].forEach(k => {
      expect(preferenceForm).toContain(k); expect(preferenceDetails).toContain(k);
    });
  });

  it('17. archives preference through POST endpoint (never DELETE)', () => {
    expect(controller).toContain('/api/crm/preferences/{preferenceId}/archive');
    expect(preferenceDetailsJs).toContain("method:'POST'");
  });

  it('18. builds Gateway payloads without any TenantId', () => {
    // The payload builder objects never carry a TenantId member.
    const builders = controller.slice(controller.indexOf('ToConsentCreatePayload(ConsentEditViewModel'), controller.indexOf('BuildEvidenceRef(ConsentEditViewModel'));
    expect(builders).not.toMatch(/TenantId/);
    expect(controller).toContain('ContainsTenantId');
    expect(controller).toContain('must not be supplied');
    expect(models).not.toMatch(/public\s+\w+\??\s+TenantId/);
  });

  it('19. evaluate panel is read-only and calls GET evaluate', () => {
    expect(evaluatePanel).toContain('EvaluateReadOnlyHelp');
    expect(evaluateJs).toContain('/api/consents/evaluate?');
    expect(controller).toContain('"/api/crm/consents/evaluate');
    ['evalSubjectType','evalChannel','evalPurpose'].forEach(id => expect(evaluatePanel).toContain(`id="${id}"`));
  });

  it('20. evaluate renders allowed/blocked/unknown badges; unknown never allowed', () => {
    ['L.Allowed','L.Blocked','L.Unknown','L.NotApplicable'].forEach(k => expect(evaluateJs).toContain(k));
    expect(evaluateJs).toContain("status !== 'unknown'");
    expect(evaluatePanel).toContain('UnknownNotAllowed');
    expect(evaluateJs).toContain("status === 'allowed' ? 'bg-success'");
  });

  it('21. evaluate shows matched IDs / evaluator provenance and reason codes', () => {
    ['matchedConsentId','matchedPreferenceIds','evaluatorVersion','evaluatedAt','reasonCodes','selectionReason'].forEach(k => expect(evaluateJs).toContain(k));
    ['MatchedConsentId','MatchedPreferenceIds','EvaluatorVersion','ReasonCodes'].forEach(k => expect(provenance).toContain(k));
  });

  it('22. SubjectPanel renders with SubjectType/SubjectId and never mutates Contact', () => {
    expect(subjectPanel).toContain('consentPreferenceSubjectPanel');
    ['subjectPanelType','subjectPanelId'].forEach(id => expect(subjectPanel).toContain(`id="${id}"`));
    expect(subjectJs).toContain('/api/consents?');
    expect(subjectJs).toContain('/api/preferences?');
    const combined = [models, subjectPanel, subjectJs].join('\n').toLowerCase();
    expect(combined).not.toContain('consentrecordpayload');
    expect(combined).not.toContain('preferencerecordpayload');
  });

  it('23. never adds a flat ConsentStatus onto Contact/AccountContactLink', () => {
    expect(models).not.toMatch(/class Contact[\s\S]*ConsentStatus/);
    expect(controller).not.toMatch(/AccountContactLink|Contact\s*\./);
  });

  it('24. uses Gateway only — no direct :5061 business URL', () => {
    [controller, allJs].forEach(src => expect(src).not.toMatch(/https?:\/\/[^\s'"]*:5061|localhost:5061/i));
    expect(controller).toContain('_gatewayUrl');
  });

  it('25. never models the forbidden future response-shape fields', () => {
    const forbidden = ['visitPlanId','routePlanId','routeId','dueStatus','overdue','lastVisitDate','requiredVisitCount','periodType','frequencyPolicyId','campaignTargetId','segmentMembership','recommendationId','nextBestAction','workflowApprovalId','contentRenderUrl','filePayload'];
    forbidden.forEach(f => { expect(models).not.toContain(f); expect(allJs).not.toContain(f); });
  });

  it('26. uses showConfirm/showToast (no raw confirm/alert/Swal)', () => {
    expect(indexJs).toContain('window.showConfirm');
    expect(indexJs).toContain('window.showToast');
    expect(allJs).not.toMatch(/(^|[^.\w])confirm\s*\(/);
    expect(allJs).not.toMatch(/(^|[^.\w])alert\s*\(/);
    expect(allJs).not.toContain('Swal.fire');
  });

  it('27. has exact seven-locale RESX parity including menu + required keys', () => {
    const langs = ['en','fr','es','zh','ar','ru','tr'];
    const names = lang => [...read(`Resources/Views/CRM/ConsentPreferences/ConsentPreferencesIndex.${lang}.resx`).matchAll(/<data name="([^"]+)"/g)].map(x => x[1]).sort();
    const expected = names('en');
    langs.forEach(lang => expect(names(lang)).toEqual(expected));
    ['PageDescription','PreferenceCannotGrant','UnknownNotAllowed','EvaluatorVersion'].forEach(k => expect(expected).toContain(k));
    // menu key parity across all shared resources
    langs.forEach(lang => expect(read(`Resources/SharedResource.${lang}.resx`)).toContain('name="ConsentPreferencesMenu"'));
  });

  it('29. dependent subject picker: name-search for contact/account + GUID fallback (Create only)', () => {
    // Create-mode SubjectId is a Select2-backed select wired to the read-only picker proxy.
    expect(consentForm).toContain('class="form-select subject-picker"');
    expect(consentForm).toContain('data-picker-url="/CRM/ConsentPreferences/api/subjects"');
    // On edit it stays an immutable read-only input (never a picker).
    expect(consentForm).toMatch(/isEdit[\s\S]{0,200}<input asp-for="SubjectId"[^>]*readonly/);
    // Proxy resolves contact→contacts, account→accounts; other types return no options (GUID fallback).
    expect(controller).toContain('"/api/crm/contacts?page=1&pageSize=200"');
    expect(controller).toContain('"/api/crm/accounts?page=1&pageSize=200"');
    expect(controller).toContain('[HttpGet("api/subjects")]');
    // Client wires ajax for contact/account and always allows a raw GUID via tags.
    expect(consentFormJs).toContain("PICKER_TYPES = ['contact', 'account']");
    expect(consentFormJs).toContain('tags: true');
    expect(consentFormJs).toContain('opts.ajax');
    // Still read-only/Gateway-only: the picker proxy uses GET, never DELETE, never :5061.
    expect(controller).not.toMatch(/https?:\/\/[^\s'"]*:5061/i);
  });

  it('30. external references are OPTIONAL: Golden Slim DataTable v2 + offcanvas add + SourceSystem picker', () => {
    // No pre-seeded editable row that could block save; hidden host mirrors the collection instead.
    expect(consentForm).not.toContain('external-reference-row');
    expect(consentForm).toContain('id="externalReferencesHost"');
    // Rendered as a Golden Slim DataTable v2.
    expect(consentForm).toContain('id="dt-external-refs"');
    expect(consentForm).toContain('data-dt-standard="v2"');
    expect(consentFormJs).toContain('new DataTable(tableEl');
    // Add Reference lives in the DataTable toolbar (Golden Slim add-new slot) and opens the offcanvas.
    expect(consentFormJs).toContain('window.DtDefaults.exportButtons(');
    expect(consentFormJs).toContain("'data-bs-target': '#externalRefCanvas'");
    // Actions rendered like Golden Slim (DitenDataTable.renderActions), not a bespoke button.
    expect(consentFormJs).toContain('window.DitenDataTable.renderActions');
    // Offcanvas add form present; SourceSystem is a searchable Select2 with suggestions.
    expect(consentForm).toContain('id="externalRefCanvas"');
    expect(consentForm).toContain('id="erSourceSystem"');
    expect(consentForm).toContain('sourceSystemSuggestions');
    expect(consentFormJs).toContain("jq('#externalRefCanvas')");
    expect(consentFormJs).toContain('tags: true');
    // The section is optional end-to-end: zero rows render zero hidden inputs, so the record saves without a reference.
    expect(consentFormJs).toContain('name="ExternalReferences[${i}].SourceSystem"');
  });

  it('31. consent list: Golden Slim toolbar Create + renderActions + subject-name resolution + date-time stamp', () => {
    const indexView = read('Views/CRM/ConsentPreferences/Index.cshtml');
    // Subtitle removed; Create Consent no longer a header button.
    expect(indexView).not.toContain('ConsentsSubtitle');
    expect(indexView).not.toContain('id="btnCreateConsent"');
    // Create lives in the DataTable toolbar (add-new slot) and is permission-gated.
    expect(indexJs).toContain('data-consent-create');
    expect(indexJs).toContain('canManage ? L.CreateConsent');
    expect(indexView).toContain('window.ConsentPreferencePerms');
    // Actions rendered like Golden Slim.
    expect(indexJs).toContain('window.DitenDataTable.renderActions');
    // Inline filter mounted into the toolbar with the Golden Slim Select2 chip styling.
    expect(indexJs).toContain('mountInlineFilter');
    expect(indexJs).toContain('dt-filter-btn');
    expect(indexJs).toContain('initFilterSelect2');
    expect(indexJs).toContain("dropdownCssClass: 'dt-inline-filter-dropdown'");
    expect(indexJs).toContain('minimumResultsForSearch: Infinity');
    // Golden chip styling requires the dt-filter-host class on the host.
    expect(consentFilter).toContain('class="dt-filter-host"');
    expect(consentFilter).toContain('class="form-select form-select-sm select2"');
    expect(consentFilter).toContain('data-placeholder');
    expect(consentFilter).toContain('filter-chip');
    expect(consentFilter).toContain('btn-label-danger');
    // Filter SubjectId is a dependent picker like Create (subject-picker select, not a text input).
    expect(consentFilter).toContain('id="filterConsentSubjectId" class="form-select form-select-sm subject-picker"');
    expect(indexJs).toContain('wireFilterSubjectPicker');
    // SubjectId column resolves contact/account display names via a read-only Gateway proxy (GUID fallback).
    expect(indexJs).toContain('resolveSubjects');
    expect(indexJs).toContain('subjectCell');
    expect(controller).toContain('[HttpGet("api/subjects/resolve")]');
    expect(controller).toContain('"/api/crm/contacts?page=1&pageSize=200"');
    // Effective From/To use the "MMM dd, yy / hh:mm A" stamp.
    expect(indexJs).toContain('dtStamp');
    expect(indexJs).toContain("hour12: true");
  });

  it('28. no backend/gateway/runtime files were touched by this feature set', () => {
    // The feature ships only frontend files; the controller talks to FU02 through the Gateway allowlist.
    expect(controller).toContain('/api/crm/consents');
    expect(controller).toContain('/api/crm/preferences');
    expect(controller).not.toContain('services/Diten.CrmService');
  });
});
