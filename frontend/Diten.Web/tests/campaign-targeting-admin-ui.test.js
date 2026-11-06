const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const repoRoot = path.resolve(__dirname, '../../..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const readRepo = relative => fs.readFileSync(path.join(repoRoot, relative), 'utf8');
// The slice of `source` from `start` up to (not including) `end`; fails loudly if either marker is gone.
const between = (source, start, end) => {
  const from = source.indexOf(start);
  expect(from).toBeGreaterThan(-1);
  const to = source.indexOf(end, from);
  expect(to).toBeGreaterThan(from);
  return source.slice(from, to);
};
// Paging parameters a server-paged list would send; the Campaign list fetches every row and searches client-side.
const PAGING_PARAM = /\b(page|pageSize|pageNumber|pageIndex|skip|take|offset|limit|start|length|draw)\b/i;
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
// Navigation is manifest-driven: the CRM menu comes from the Platform self-registration manifest, not the layout.
const crmManifest = readRepo('services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs');
// The server half of FU11: what the manual target canvas no longer asks for, the CRM write path fills in.
const targetHandlers = readRepo('services/Diten.CrmService/src/Diten.CrmService.Application/Features/Campaign/Handlers/CampaignTargetCommandHandlers.cs');

// Resolves a manifest page by key -> { route, permission } (the permission may be a const name or a literal).
const manifestPage = (manifest, key) => {
  const match = manifest.match(new RegExp(`new ModuleManifestPage\\(\\s*"${key}"\\s*,\\s*"[^"]*"\\s*,\\s*"([^"]+)"\\s*,\\s*("[^"]+"|\\w+)`));
  expect(match).toBeTruthy();
  const [, route, permissionRef] = match;
  const literal = permissionRef.startsWith('"')
    ? permissionRef.slice(1, -1)
    : (manifest.match(new RegExp(`\\b${permissionRef}\\s*=\\s*"([^"]+)"`)) || [])[1];
  return { route, permission: literal };
};

describe('MOD-0165-FU05 Campaign / Targeting Admin UI', () => {
  it('renders the Campaigns deep-link route with the tenant shell', () => {
    expect(controller).toContain('[Route("CRM/Campaigns")]');
    expect(indexView).toContain('Layout = "_LayoutTenantShell"');
  });

  it('loads the contract through the same-origin Gateway proxy and fails closed', () => {
    expect(indexJs).toContain("const endpoint = '/CRM/Campaigns/api'");
    expect(indexJs).toContain('fetch(`${endpoint}/contract`');
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
    ['filterCampaignStatus','filterCampaignType','filterTargetingMode','filterIncludeArchived'].forEach(id => expect(filterView).toContain(`id="${id}"`));
    // MOD-0165 FU10 deprecated the brand/product/subject anchors on Campaign: no filter for them, none sent.
    ['filterBrandId','filterProductId','filterSubjectId'].forEach(id => expect(filterView).not.toContain(`id="${id}"`));
    expect(between(indexJs, 'const query = () => {', 'return params.toString();')).not.toMatch(/brandId|productId|subjectId/i);
    expect(indexJs).toContain("colReorder: { columns: ':gt(0):not(:last-child)' }");
    expect(indexJs).toContain("column-reorder.dt columns-reordered.dt");
  });

  it('documents unsupported filters; search runs client-side over the whole list and is kept in the saved view', () => {
    expect(filterView).toContain('UnsupportedFiltersNote');
    expect(controller).not.toContain('objectiveType = Request.Query');
    // The list fetches every row (no paging) and hands them to DataTables; search therefore covers the whole list.
    // If the list ever becomes server-paged, client search would only see one page - this test must then go red.
    const query = between(indexJs, 'const query = () => {', 'return params.toString();');
    expect(query).not.toMatch(PAGING_PARAM);
    expect(query).not.toMatch(/\bsearch\b/i);
    expect(indexJs).toMatch(/fetch\(`\$\{endpoint\}\?\$\{query\(\)\}`,/);
    expect(indexJs).toContain('data: rows,');
    expect(indexJs).not.toMatch(/serverSide\s*:\s*true/);
    // The search term is part of the saved view (Save View), not a server filter.
    expect(indexJs).toMatch(/search:\s*norm\(api\.search\(\)\)/);
  });

  it('uses Compact create/edit/detail pages with matching four sections in the same order', () => {
    ['Create.cshtml','Edit.cshtml','Details.cshtml','_Form.cshtml'].forEach(file => expect(fs.existsSync(path.join(root,'Views/CRM/Campaigns',file))).toBe(true));
    const sections = ['SummarySection','TargetingSection','ConsentContextSection','ScopeSection'];
    [formView, detailsView].forEach(view => {
      const positions = sections.map(key => view.indexOf(`["${key}"]`));
      positions.forEach(p => expect(p).toBeGreaterThan(-1));
      positions.slice(1).forEach((p, i) => expect(p).toBeGreaterThan(positions[i]));
      // FU10 removed the References / External References sections from both pages.
      ['ReferencesSection','ExternalReferencesSection'].forEach(key => expect(view).not.toContain(key));
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

  it('no longer renders the FU10-deprecated reference IDs and makes no master lookup calls', () => {
    ['BrandId','ProductId','SubjectId','TopicId','DefaultKnowledgePathId','DefaultKnowledgeContentId'].forEach(key => expect(detailsView).not.toContain(key));
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

  it('FU11: the manual target canvas asks no reason; the server writes the reason/source defaults', () => {
    // The canvas no longer collects reason, reason codes, source or exclusion reason...
    ['selectionReason','targetReasonCodes','targetSource','exclusionReason'].forEach(id => expect(targetCanvas).not.toContain(`id="${id}"`));
    expect(targetCanvas).toContain('ManualTargetDefaultsHint');
    // ...and the payload does not send them (omitted on update = keep what the target already says).
    const payload = between(detailsJs, 'const targetPayload', 'const validateTarget');
    expect(payload).not.toMatch(/selectionReason|reasonCodes|targetSource|exclusionReason/);
    // The FU04 invariant (every target states why it exists) now lives on the server: the CRM write path resolves the
    // missing fields in CampaignTargetWrite.ResolveManualDefaults on create AND update, then validates the result.
    const targetWrite = between(targetHandlers, 'internal static class CampaignTargetWrite', 'public static string BuildSelectionReason');
    expect(targetWrite).toMatch(/ResolveManualDefaults\(\s*string\? targetSource,\s*string\? selectionReason/);
    expect(targetWrite).toContain('BuildSelectionReason(actorName, now)');
    expect(targetHandlers.split('CampaignTargetWrite.ResolveManualDefaults(').length - 1).toBeGreaterThan(1);
    expect(targetHandlers).toMatch(/CampaignTargetWrite\.Validate\(\s*defaults\.TargetSource,\s*defaults\.SelectionReason/);
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
    // The menu entry is the CRM manifest's CAMPAIGNS page, gated by crm.campaign.read.
    expect(manifestPage(crmManifest, 'CAMPAIGNS')).toEqual({ route: '/CRM/Campaigns', permission: 'crm.campaign.read' });
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
