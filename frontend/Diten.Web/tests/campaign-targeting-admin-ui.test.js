const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const controller = read('Controllers/CRM/CampaignsController.cs');
const models = read('Models/CRM/CampaignViewModels.cs');
const indexView = read('Views/CRM/Campaigns/Index.cshtml');
const filterView = read('Views/CRM/Campaigns/_Filter.cshtml');
const tableView = read('Views/CRM/Campaigns/_DataTable.cshtml');
const detailsView = read('Views/CRM/Campaigns/Details.cshtml');
const targetsView = read('Views/CRM/Campaigns/_TargetsDataTable.cshtml');
const targetCanvas = read('Views/CRM/Campaigns/_TargetCreateEditOffcanvas.cshtml');
const snapshotPanel = read('Views/CRM/Campaigns/_SnapshotPanel.cshtml');
const formView = read('Views/CRM/Campaigns/_Form.cshtml');
const indexJs = read('wwwroot/assets/js/CRM/Campaigns/index.js');
const detailsJs = read('wwwroot/assets/js/CRM/Campaigns/details.js');
const formJs = read('wwwroot/assets/js/CRM/Campaigns/form.js');
const layout = read('Views/Shared/_LayoutTenantShell.cshtml');

describe('MOD-0165-FU05 Campaign / Targeting Admin UI', () => {
  it('renders the Campaigns deep-link route with the tenant shell', () => {
    expect(controller).toContain('[Route("CRM/Campaigns")]');
    expect(indexView).toContain('Layout = "_LayoutTenantShell"');
  });

  it('loads the contract through the same-origin Gateway proxy and fails closed', () => {
    expect(indexJs).toContain("fetch('/CRM/Campaigns/api/contract'");
    expect(controller).toContain('"/api/crm/campaigns/contract"');
    expect(indexJs).toContain('CampaignContractUnavailable');
  });

  it('implements Campaign list loading, empty and error states', () => {
    expect(tableView).toContain('id="skeleton-loader"');
    expect(indexJs).toContain('emptyTable: L.EmptyState');
    expect(indexJs).toContain('L.ErrorState');
  });

  it('renders Golden DataTable v2 list and supported filters', () => {
    expect(tableView).toContain('data-dt-standard="v2"');
    ['filterCampaignStatus','filterCampaignType','filterBrandId','filterProductId','filterSubjectId','filterIncludeArchived'].forEach(id => expect(filterView).toContain(`id="${id}"`));
    expect(indexJs).toContain("colReorder: { columns: ':gt(0):not(:last-child)' }");
    expect(indexJs).toContain("column-reorder.dt columns-reordered.dt");
  });

  it('documents unsupported filters without a fake client filter', () => {
    expect(filterView).toContain('UnsupportedFiltersNote');
    expect(indexJs).toContain('searching: false');
    expect(controller).not.toContain('objectiveType = Request.Query');
  });

  it('uses Compact create/edit/detail pages with matching four sections', () => {
    ['Create.cshtml','Edit.cshtml','Details.cshtml','_Form.cshtml'].forEach(file => expect(fs.existsSync(path.join(root,'Views/CRM/Campaigns',file))).toBe(true));
    ['SummarySection','ReferencesSection','ConsentContextSection','ExternalReferencesSection'].forEach(key => {
      expect(formView).toContain(key); expect(detailsView).toContain(key);
    });
  });

  it('enforces required Campaign fields and date range validation', () => {
    ['CampaignCode','CampaignName','CampaignType','CampaignStatus','StartDate'].forEach(field => expect(models).toMatch(new RegExp(`Required[\\s\\S]{0,180}${field}|${field}[\\s\\S]{0,180}Required`)));
    expect(models).toContain('EndDate < StartDate');
    expect(formJs).toContain('new Date(end) < new Date(start)');
  });

  it('creates and updates through Gateway payloads without TenantId', () => {
    expect(controller).toContain('ToCreatePayload(model)');
    expect(controller).toContain('ToUpdatePayload(model)');
    expect(controller).toContain('ContainsTenantId');
    const payloadSection = controller.slice(controller.indexOf('private static object ToCreatePayload'), controller.indexOf('private static CampaignEditViewModel'));
    expect(payloadSection.toLowerCase()).not.toContain('tenantid');
  });

  it('handles archived Campaigns as read-only', () => {
    expect(controller).toContain('campaign.IsArchived');
    expect(detailsView).toContain('ArchivedCampaignReadOnly');
    expect(detailsView).toContain('Model.CanManageTargets');
  });

  it('uses archive confirmation and POST archive endpoints for Campaign and Target', () => {
    expect(indexJs).toContain('window.showConfirm');
    expect(detailsJs).toContain('ArchiveTargetConfirm');
    expect(detailsJs).toContain("method:'POST'");
    expect(controller).toContain('/archive');
    expect(controller).not.toMatch(/HttpDelete|HttpMethod\.Delete/);
    expect(indexJs).not.toMatch(/method\s*:\s*['"]DELETE['"]/i);
    expect(detailsJs).not.toMatch(/method\s*:\s*['"]DELETE['"]/i);
  });

  it('renders reference IDs without master lookup calls', () => {
    ['BrandId','ProductId','SubjectId','TopicId','DefaultKnowledgePathId','DefaultKnowledgeContentId'].forEach(key => expect(detailsView).toContain(key));
    expect(controller).not.toMatch(/brand.*lookup|product.*lookup|knowledge.*lookup/i);
  });

  it('renders Targets DataTable fields and keeps exclusions visible', () => {
    expect(targetsView).toContain('data-dt-standard="v2"');
    ['TargetStatus','TargetSource','ReasonCodes','ExclusionReason','ConsentDecision','EligibilityStatus','MatchedConsentId','MatchedPreferenceIds'].forEach(key => expect(targetsView).toContain(key));
    expect(detailsView).toContain('ExcludedTargetsVisibleHelp');
    expect(detailsJs).toContain("v === 'excluded'");
  });

  it('provides the Golden Slim manual target canvas', () => {
    expect(targetCanvas).toContain('class="offcanvas offcanvas-end"');
    expect(targetCanvas).toContain('id="targetForm"');
    expect(detailsJs).toContain("window.bootstrap?.Offcanvas");
  });

  it('excludes campaign-target from every target option', () => {
    expect(detailsJs).toContain("filter(x=>x!=='campaign-target')");
    expect(detailsJs).toContain("filter(x => x !== 'campaign-target')");
  });

  it('requires SelectionReason, ReasonCodes and conditional ExclusionReason', () => {
    expect(targetCanvas).toContain('id="selectionReason"');
    expect(detailsJs).toContain("csv('targetReasonCodes').length === 0");
    expect(detailsJs).toContain("value('targetStatus') === 'excluded'");
  });

  it('provides lightweight snapshot rows and JSON paste fallback', () => {
    expect(snapshotPanel).toContain('id="snapshotRows"');
    expect(snapshotPanel).toContain('id="snapshotJson"');
    expect(detailsJs).toContain('collectSnapshotItems');
  });

  it('validates consent context and empty snapshot items', () => {
    expect(detailsJs).toContain("apply && (!value('consentChannel') || !value('consentPurpose'))");
    expect(detailsJs).toContain('!items.length');
  });

  it('shows consent-filter-not-applied warning', () => {
    expect(snapshotPanel).toContain('id="consentNotAppliedWarning"');
    expect(snapshotPanel).toContain('consent_filter_not_applied');
    expect(detailsJs).toContain("evaluation?.filterApplied === false");
  });

  it('shows snapshot batch id and created/reconciled/excluded counts', () => {
    ['snapshotBatchId','createdCount','reconciledCount','excludedCount'].forEach(value => expect(detailsJs).toContain(value));
  });

  it('shows different-source 409 as an atomic batch failure', () => {
    expect(detailsJs).toContain('error.status===409');
    expect(detailsJs).toContain('DifferentSourceConflict');
  });

  it('renders allowed, blocked, unknown and not-applicable consent badges', () => {
    ['Allowed','Blocked','Unknown','NotApplicable'].forEach(key => expect(detailsJs).toContain(`L.${key}`));
    expect(detailsJs).toContain('consent_evaluation_not_applicable');
  });

  it('renders matched IDs and evaluator version as provenance only', () => {
    ['matchedConsentId','matchedPreferenceIds','evaluatorVersion','evaluatedAt'].forEach(value => expect(detailsJs).toContain(value));
  });

  it('does not model or render consent/preference record payloads', () => {
    const combined = [models,detailsView,detailsJs].join('\n').toLowerCase();
    expect(combined).not.toContain('consentrecordpayload');
    expect(combined).not.toContain('preferencerecordpayload');
  });

  it('permission-controls menu and actions', () => {
    expect(layout).toContain('Perms.Has("crm.campaign.read")');
    expect(indexView).toContain('crm.campaign.manage');
    expect(controller).toContain('RequirePage(ReadPermission, ReadFallback)');
    expect(detailsView).toContain('Model.CanCreateSnapshot');
  });

  it('has exact seven-locale RESX parity', () => {
    const langs = ['en','fr','es','zh','ar','ru','tr'];
    const names = lang => [...read(`Resources/Views/CRM/Campaigns/CampaignIndex.${lang}.resx`).matchAll(/<data name="([^"]+)"/g)].map(x=>x[1]).sort();
    const expected = names('en');
    langs.forEach(lang => expect(names(lang)).toEqual(expected));
    expect(expected).toContain('PageDescription');
    expect(expected).toContain('ConsentFilterNotAppliedWarning');
  });

  it('contains no direct service business URL', () => {
    [controller,indexJs,detailsJs,formJs].forEach(source => expect(source).not.toMatch(/https?:\/\/[^\s'"]*:5061|localhost:5061/i));
    expect(controller).toContain('_gatewayUrl');
  });

  it('ignores forbidden future capabilities by never modeling them', () => {
    const forbidden = ['visitPlanId','routePlanId','routeId','dueStatus','overdue','lastVisitDate','requiredVisitCount','periodType','frequencyPolicyId','segmentMembership','recommendationId','nextBestAction','workflowApprovalId','contentRenderUrl'];
    forbidden.forEach(field => expect(models).not.toContain(field));
  });
});
