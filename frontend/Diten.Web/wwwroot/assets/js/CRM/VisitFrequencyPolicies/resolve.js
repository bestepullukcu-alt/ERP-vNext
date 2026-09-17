/**
 * MOD-0165-FU03 (WP-FREQ-C) Visit Frequency / Call-Cycle Policy — Details quick-view + Resolve/Çözümleme panel.
 * FRONTEND ONLY, entirely READ-ONLY.
 *
 * Two concerns, one module (loaded once from the always-rendered _DetailsQuickView partial):
 *
 *  1) Details quick-view — the FREQ-A console (index.js) owns the row "Details" action and shows the offcanvas + fills
 *     its header. This file is NOT allowed to touch index.js, so it listens (capture phase) for the same `.js-quick-view`
 *     click to learn the id, then on the offcanvas `show.bs.offcanvas` fetches the full policy read model
 *     (GET /visit-frequency-policies/{id}) and renders the read-only body — every context GUID resolved to a NAME through
 *     the same proxied sibling lists the FREQ-B editor uses.
 *
 *  2) Resolve panel — "how often should THIS target be visited?". Mirrors the EligibilityPolicies _Evaluate flow: build
 *     an input (target type + target + optional context + effectiveAt), GET /resolve, and render exactly what the backend
 *     returns — verdict + selected policy + candidate diagnostics + reason codes. The endpoint is GET/read-only.
 *
 * CONTRACT/RESPONSE-DRIVEN: target/frequency/period/source codes are humanized (same transform as the console);
 * statuses, verdicts (FrequencyStatus) and reason codes (FrequencyReasonCodes) use the closed L10n maps with a humanized
 * fallback — nothing is a hardcoded authoring vocabulary, and no result is fabricated (every value is from a real fetch).
 */
(function (window, document) {
    'use strict';

    const endpoint = '/CRM/VisitFrequencyPolicies/api';

    let L = {};
    try { L = JSON.parse(document.getElementById('vfp-dr-l10n')?.textContent || '{}'); } catch (e) { L = {}; }
    L = Object.assign({}, window.L10n || {}, L);
    const statusLabels = L.statusLabels || {};
    const verdictLabels = L.verdictLabels || {};
    const reasonLabels = L.reasonLabels || {};
    const bandLabels = L.bandLabels || {};

    // ── helpers ─────────────────────────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const t = (key, fallback) => L[key] || fallback || key;
    const humanize = code => norm(code).split(/[-_\s]+/).filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
    const dash = () => t('NotAvailable', '—');
    const shortId = id => { const s = norm(id); return s ? s.slice(0, 8) + '…' : ''; };
    const fmtDate = v => { const s = norm(v); return s ? s.slice(0, 10) : ''; };
    const fmtDateTime = v => { const s = norm(v); return s ? s.slice(0, 10) + ' ' + s.slice(11, 16) : ''; };

    const statusLabel = s => statusLabels[norm(s)] || humanize(s) || dash();
    const statusTone = s => ({ draft: 'secondary', active: 'success', inactive: 'warning', archived: 'secondary' }[norm(s)] || 'primary');
    const verdictLabel = v => verdictLabels[norm(v)] || humanize(v) || dash();
    const verdictTone = v => ({ resolved: 'success', unknown: 'warning', conflict: 'danger', not_applicable: 'secondary' }[norm(v)] || 'secondary');
    const reasonLabel = r => reasonLabels[norm(r)] || humanize(r);
    const badge = (text, tone = 'primary') => `<span class="badge bg-label-${tone}">${esc(text)}</span>`;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [t('ErrorState', 'Error')]).join(' · ')), { status: response.status });
        return body.data;
    };
    const getJson = path => fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } }).then(envelope);

    // ── entity-picker readers (reuse the FREQ-B proxied sibling lists — names, never GUIDs) ──────────────────────────
    const READERS = {
        segment: () => getJson('/segments?pageSize=200'),
        account: () => getJson('/accounts?pageSize=200'),
        contact: () => getJson('/contacts?pageSize=200'),
        campaign: () => getJson('/campaigns?pageSize=200'),
        'concept-node': () => getJson('/concept-nodes?pageSize=200'),
        'audience-profile': () => getJson('/audience-profiles?pageSize=200'),
        brand: () => getJson('/mdm-brands?pageSize=200'),
        product: () => getJson('/mdm-products?pageSize=200'),
        'cycle-period': () => getJson('/cycle-periods'),
        'business-unit': () => getJson('/business-units'),
        'territory-model': () => getJson('/territory-models?pageSize=200'),
        'territory-node': ctx => (ctx ? getJson(`/territory-models/${ctx}/nodes`) : Promise.resolve([]))
    };
    // Generous id/name fallbacks (endpoints differ in casing + field names) so a NAME is shown, never a GUID.
    const mapOption = x => ({
        value: x.id ?? x.value ?? x.valueCode ?? x.segmentId ?? x.accountId ?? x.contactId ?? x.campaignId
            ?? x.cyclePeriodId ?? x.conceptNodeId ?? x.audienceProfileId ?? x.territoryNodeId ?? x.nodeId
            ?? x.brandId ?? x.productId ?? x.globalProductId ?? x.territoryModelId ?? '',
        text: x.name || x.Name || x.text || x.displayName || x.DisplayName || x.label || x.Label
            || x.segmentName || x.SegmentName || x.accountName || x.AccountName || x.contactName || x.ContactName
            || x.fullName || x.FullName || x.campaignName || x.CampaignName || x.periodName || x.PeriodName
            || x.cyclePeriodName || x.conceptName || x.ConceptName || x.nodeName || x.NodeName
            || x.audienceProfileName || x.profileName || x.brandName || x.BrandName || x.productName || x.ProductName
            || x.modelName || x.ModelName || x.code || x.Code
            || String(x.id ?? x.value ?? x.valueCode ?? '')
    });

    const optionCache = new Map();
    const loadOptions = async (kind, ctx) => {
        const key = `${kind}:${ctx || ''}`;
        if (optionCache.has(key)) return optionCache.get(key);
        let options = [];
        try {
            const data = await (READERS[kind] ? READERS[kind](ctx) : Promise.resolve([]));
            const items = Array.isArray(data) ? data : (data?.items || data?.nodes || data?.values || []);
            options = items.map(mapOption).filter(o => o.value !== '' && o.value != null);
        } catch (e) { options = []; }
        optionCache.set(key, options);
        return options;
    };
    // id → name for a kind (built from the same list), for the details read-only view.
    const nameOf = async (kind, id) => {
        const s = norm(id);
        if (!s) return '';
        const options = await loadOptions(kind);
        return (options.find(o => String(o.value) === s) || {}).text || '';
    };

    // targetType → entity-picker kind (anything unmapped falls back to a manual id / short id).
    const TARGET_KIND = {
        segment: 'segment', account: 'account', contact: 'contact',
        'campaign-target': 'campaign', 'concept-node': 'concept-node', 'audience-profile': 'audience-profile'
    };

    const fillSelect = (select, options, placeholder) => {
        if (!select) return;
        const head = placeholder != null ? `<option value="">${esc(placeholder)}</option>` : '';
        select.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };

    // ── contract (loaded once, shared) ──────────────────────────────────────────
    let contractPromise = null;
    const loadContract = () => {
        if (!contractPromise) contractPromise = getJson('/visit-frequency-policies/contract').catch(() => ({}));
        return contractPromise;
    };
    // weight (int priority) → contract band code → localized label. Falls back to the bare weight.
    let bandByWeight = null;
    const priorityText = async priority => {
        const p = priority == null ? null : Number(priority);
        if (p == null || Number.isNaN(p)) return dash();
        if (!bandByWeight) {
            bandByWeight = new Map();
            const c = await loadContract();
            (c?.vocabulary?.priorityBands || []).forEach(b => bandByWeight.set(Number(b.value), b.code));
        }
        const code = bandByWeight.get(p);
        const label = code ? (bandLabels[code] || humanize(code)) : '';
        return label ? `${label} (${p})` : String(p);
    };

    const freqSentence = (count, period) => {
        const c = norm(count);
        const per = t('PerPeriod', '/');
        const pd = norm(period) ? humanize(period) : '';
        return [c, per, pd].filter(Boolean).join(' ');
    };

    // ════════════════════════════════════════════════════════════════════════════
    //  1) DETAILS QUICK-VIEW
    // ════════════════════════════════════════════════════════════════════════════
    const OFFCANVAS = el('offcanvasDetailsPreview');
    let pendingDetailId = null;

    // Capture phase so we learn the id BEFORE the FREQ-A console shows the offcanvas.
    document.addEventListener('click', event => {
        const q = event.target.closest('.js-quick-view');
        if (q) pendingDetailId = q.dataset.id || null;
    }, true);

    const field = (label, valueHtml, full) =>
        `<div class="${full ? 'vfp-dv-field-full' : ''}"><div class="vfp-dv-label">${esc(label)}</div><div class="vfp-dv-value">${valueHtml}</div></div>`;
    const nameValue = (name, id) => {
        const n = norm(name);
        const ref = shortId(id);
        return n ? `${esc(n)}${ref ? `<span class="vfp-dv-sub vfp-mono">${esc(ref)}</span>` : ''}` : (ref ? `<span class="vfp-mono">${esc(ref)}</span>` : dash());
    };
    const section = (title, icon, bodyHtml) =>
        `<div class="vfp-dv-section"><div class="vfp-dv-section-title"><i class="bx ${icon}"></i>${esc(title)}</div>${bodyHtml}</div>`;

    const renderDetail = async p => {
        // Resolve the names we can (each list is cached; unresolved refs degrade to a short id — never a fabricated name).
        const [targetName, buName, segName, campName, brandName, prodName, cpName, prio] = await Promise.all([
            TARGET_KIND[p.targetType] ? nameOf(TARGET_KIND[p.targetType], p.targetId) : Promise.resolve(''),
            nameOf('business-unit', p.businessUnit),
            nameOf('segment', p.segmentId),
            nameOf('campaign', p.campaignId),
            nameOf('brand', p.brandId),
            nameOf('product', p.productId),
            nameOf('cycle-period', p.cyclePeriodId),
            priorityText(p.priority)
        ]);

        // Identity
        const identity = '<div class="vfp-dv-grid">'
            + field(t('FieldPolicyCode', 'Code'), `<span class="vfp-mono">${esc(p.policyCode || dash())}</span>`)
            + field(t('FieldStatus', 'Status'), badge(statusLabel(p.status), statusTone(p.status)))
            + field(t('FieldPolicyName', 'Name'), esc(p.policyName || dash()), true)
            + field(t('FieldDescription', 'Description'), esc(p.description || dash()), true)
            + '</div>';

        // Target
        const target = '<div class="vfp-dv-grid">'
            + field(t('FieldTargetType', 'Target type'), esc(humanize(p.targetType) || dash()))
            + field(t('FieldTarget', 'Target'), nameValue(targetName, p.targetId))
            + '</div>';

        // Context — only rows that carry a value; if none, a single muted line.
        const ctxRows = [];
        if (norm(p.businessUnit)) ctxRows.push(field(t('FieldBusinessUnit', 'Business unit'), buName ? esc(buName) : `<span class="vfp-mono">${esc(p.businessUnit)}</span>`));
        if (norm(p.territoryNodeId)) ctxRows.push(field(t('FieldTerritory', 'Territory'), nameValue('', p.territoryNodeId)));
        if (norm(p.segmentId)) ctxRows.push(field(t('FieldSegment', 'Segment'), nameValue(segName, p.segmentId)));
        if (norm(p.campaignId)) ctxRows.push(field(t('FieldCampaign', 'Campaign'), nameValue(campName, p.campaignId)));
        if (norm(p.brandId)) ctxRows.push(field(t('FieldBrand', 'Brand'), nameValue(brandName, p.brandId)));
        if (norm(p.productId)) ctxRows.push(field(t('FieldProduct', 'Product'), nameValue(prodName, p.productId)));
        if (norm(p.cyclePeriodId)) ctxRows.push(field(t('FieldCyclePeriod', 'Cycle period'), nameValue(cpName, p.cyclePeriodId)));
        const context = ctxRows.length ? `<div class="vfp-dv-grid">${ctxRows.join('')}</div>` : `<div class="vfp-meta">${esc(t('ContextNone', '— none —'))}</div>`;

        // Frequency
        const frequency = `<div class="vfp-dv-readsas"><div class="vfp-dv-readsas-label">${esc(t('FreqReadsAsLabel', 'Reads as'))}</div>`
            + `<div class="vfp-dv-readsas-text">${esc(freqSentence(p.requiredVisitCount, p.periodType))}</div></div>`
            + '<div class="vfp-dv-grid mt-3">'
            + field(t('FieldFrequencyType', 'Frequency type'), badge(humanize(p.frequencyType), 'info'))
            + field(t('FieldRequiredVisitCount', 'Required visits'), esc(p.requiredVisitCount != null ? String(p.requiredVisitCount) : dash()))
            + field(t('FieldPeriodType', 'Period'), esc(humanize(p.periodType) || dash()))
            + '</div>';

        // Priority + Source
        const priority = `<div class="vfp-dv-grid">${field(t('DvSectionPriority', 'Priority'), esc(prio))}${field(t('FieldSource', 'Source'), esc(humanize(p.source) || dash()))}</div>`;

        // Effective
        const effective = '<div class="vfp-dv-grid">'
            + field(t('FieldEffectiveFrom', 'Effective from'), esc(fmtDate(p.effectiveFrom) || dash()))
            + field(t('FieldEffectiveTo', 'Effective to'), esc(fmtDate(p.effectiveTo) || t('NoEndDate', 'No end date')))
            + '</div>' + (norm(p.notes) ? `<div class="vfp-dv-grid mt-2">${field(t('FieldNotes', 'Notes'), esc(p.notes), true)}</div>` : '');

        // Audit trail — dates + who (raw ids are shown short; no fabricated name)
        const by = who => { const s = norm(who); return s ? ` · ${esc(s.length === 36 ? shortId(s) : s)}` : ''; };
        const tl = [];
        tl.push(`<div class="vfp-dv-tl-item"><span class="vfp-dv-tl-dot"></span><span><span class="vfp-dv-tl-title">${esc(t('DetailCreated', 'Created'))}</span><span class="vfp-dv-tl-detail">${esc(fmtDateTime(p.createdAt))}${by(p.createdBy)}</span></span></div>`);
        if (norm(p.updatedAt)) tl.push(`<div class="vfp-dv-tl-item"><span class="vfp-dv-tl-dot"></span><span><span class="vfp-dv-tl-title">${esc(t('DetailUpdated', 'Updated'))}</span><span class="vfp-dv-tl-detail">${esc(fmtDateTime(p.updatedAt))}${by(p.updatedBy)}</span></span></div>`);
        if (norm(p.archivedAt)) tl.push(`<div class="vfp-dv-tl-item"><span class="vfp-dv-tl-dot"></span><span><span class="vfp-dv-tl-title">${esc(t('DetailArchived', 'Archived'))}</span><span class="vfp-dv-tl-detail">${esc(fmtDateTime(p.archivedAt))}${by(p.archivedBy)}</span></span></div>`);
        const trail = `<div class="vfp-dv-tl">${tl.join('')}</div>`;

        return section(t('DvSectionIdentity', 'Identity'), 'bx-id-card', identity)
            + section(t('DvSectionTarget', 'Target'), 'bx-target-lock', target)
            + section(t('DvSectionContext', 'Context'), 'bx-sitemap', context)
            + section(t('DvSectionFrequency', 'Frequency'), 'bx-repeat', frequency)
            + section(t('DvSectionPriority', 'Priority'), 'bx-sort', priority)
            + section(t('DvSectionEffective', 'Effective window'), 'bx-calendar', effective)
            + section(t('DvSectionTrail', 'Audit trail'), 'bx-time-five', trail);
    };

    const showDetailState = state => {
        const loading = el('vfpDetailLoading'), error = el('vfpDetailError'), body = el('vfpDetailBody');
        if (!loading || !error || !body) return;
        loading.classList.toggle('d-none', state !== 'loading');
        error.classList.toggle('d-none', state !== 'error');
        body.classList.toggle('d-none', state !== 'body');
    };

    if (OFFCANVAS) {
        OFFCANVAS.addEventListener('show.bs.offcanvas', async () => {
            const id = pendingDetailId;
            showDetailState('loading');
            if (!id) { showDetailState('error'); el('vfpDetailError').textContent = t('ErrorState', 'Error'); return; }
            try {
                const p = await getJson(`/visit-frequency-policies/${id}`);
                if (!p) throw new Error(t('ErrorState', 'Error'));
                el('vfpDetailBody').innerHTML = await renderDetail(p);
                showDetailState('body');
            } catch (e) {
                el('vfpDetailError').textContent = e.message || t('ErrorState', 'Error');
                showDetailState('error');
            }
        });
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  2) RESOLVE / ÇÖZÜMLEME PANEL
    // ════════════════════════════════════════════════════════════════════════════
    const PANEL = el('vfpResolvePanel');

    const renderTargetPicker = async targetType => {
        const host = el('vfpRsTargetPicker');
        if (!host) return;
        const kind = TARGET_KIND[targetType];
        if (targetType === 'territory-node') {
            host.innerHTML = `<select class="form-select form-select-sm mb-2" id="vfpRsTargetTerModel"></select>`
                + `<select class="form-select form-select-sm" id="vfpRsTargetId" data-role="targetId"></select>`;
            fillSelect(el('vfpRsTargetTerModel'), await loadOptions('territory-model'), t('SelectTerritoryModel', 'Select model'));
            fillSelect(el('vfpRsTargetId'), [], t('SelectTerritoryNode', 'Select node'));
            el('vfpRsTargetTerModel').addEventListener('change', async e => {
                fillSelect(el('vfpRsTargetId'), e.target.value ? await loadOptions('territory-node', e.target.value) : [], t('SelectTerritoryNode', 'Select node'));
            });
            return;
        }
        if (!kind) {
            host.innerHTML = `<input type="text" class="form-control" id="vfpRsTargetId" data-role="targetId" placeholder="${esc(t('TargetIdManual', 'Enter id (GUID)'))}">`;
            return;
        }
        host.innerHTML = `<select class="form-select" id="vfpRsTargetId" data-role="targetId"></select>`;
        fillSelect(el('vfpRsTargetId'), await loadOptions(kind), t('SelectOption', '—'));
    };

    const targetIdValue = () => norm(el('vfpRsTargetId')?.value);

    const initResolvePanel = async () => {
        const c = await loadContract();
        const targetTypes = c?.vocabulary?.targetTypes || [];
        fillSelect(el('vfpRsTargetType'), targetTypes.map(code => ({ value: code, text: humanize(code) })), t('SelectOption', '—'));

        // context selects
        const fill = async (id, kind) => fillSelect(el(id), await loadOptions(kind), t('ContextNone', '— none —'));
        await Promise.all([
            fill('vfpRsBusinessUnit', 'business-unit'),
            fill('vfpRsSegment', 'segment'),
            fill('vfpRsCampaign', 'campaign'),
            fill('vfpRsBrand', 'brand'),
            fill('vfpRsProduct', 'product'),
            fill('vfpRsConceptNode', 'concept-node'),
            fill('vfpRsAudienceProfile', 'audience-profile')
        ]);
        fillSelect(el('vfpRsTerritoryModel'), await loadOptions('territory-model'), t('SelectTerritoryModel', 'Select model'));
        fillSelect(el('vfpRsTerritoryNode'), [], t('SelectTerritoryNode', 'Select node'));

        el('vfpRsTargetType')?.addEventListener('change', e => void renderTargetPicker(e.target.value));
        el('vfpRsTerritoryModel')?.addEventListener('change', async e => {
            fillSelect(el('vfpRsTerritoryNode'), e.target.value ? await loadOptions('territory-node', e.target.value) : [], t('SelectTerritoryNode', 'Select node'));
        });
        el('vfpRsRun')?.addEventListener('click', () => void runResolve());
    };

    const showResolveState = state => {
        el('vfpRsResultEmpty')?.classList.toggle('d-none', state !== 'empty');
        el('vfpRsRunning')?.classList.toggle('d-none', state !== 'running');
        el('vfpRsResult')?.classList.toggle('d-none', state !== 'result');
    };

    const candidateRow = c => {
        const sel = c.selected;
        const tone = sel ? 'success' : 'secondary';
        const mark = sel ? badge(t('ResolveSelectedBadge', 'Selected'), 'success') : badge(t('ResolveEliminated', 'Eliminated'), 'secondary');
        return `<tr class="${sel ? 'vfp-rs-cand-selected' : ''}">`
            + `<td><span class="fw-medium">${esc(c.policyName || '')}</span><br><span class="vfp-meta vfp-mono">${esc(c.policyCode || '')}</span></td>`
            + `<td>${esc(freqSentence(c.requiredVisitCount, c.periodType))}</td>`
            + `<td class="text-center">${esc(c.priority != null ? String(c.priority) : '')}</td>`
            + `<td class="text-center">${esc(c.specificity != null ? String(c.specificity) : '')}</td>`
            + `<td>${badge(statusLabel(c.status), statusTone(c.status))}</td>`
            + `<td>${mark}<div class="vfp-meta mt-1">${esc(reasonLabel(c.reason))}</div></td>`
            + `</tr>`;
    };

    const renderResolveResult = async r => {
        const parts = [];
        parts.push(`<div class="vfp-rs-verdict-row">${badge(verdictLabel(r.frequencyStatus), verdictTone(r.frequencyStatus))}</div>`);

        if (norm(r.selectedFrequencyPolicyId)) {
            const prio = await priorityText(r.priority);
            parts.push('<div class="vfp-rs-selected">'
                + `<div class="vfp-rs-selected-name">${esc(r.selectedPolicyName || dash())}</div>`
                + `<div class="vfp-rs-selected-code vfp-mono">${esc(r.selectedPolicyCode || '')}</div>`
                + `<div class="vfp-rs-selected-freq">${esc(freqSentence(r.requiredVisitCount, r.periodType))}</div>`
                + `<div class="vfp-rs-reason"><strong>${esc(t('ResolveSelectionReason', 'Selection reason'))}:</strong> ${esc(reasonLabel(r.selectionReason))}</div>`
                + `<div class="vfp-meta mt-2">${esc(t('DvSectionPriority', 'Priority'))}: ${esc(prio)} · ${esc(t('FieldSource', 'Source'))}: ${esc(humanize(r.source) || dash())}`
                + ` · ${esc(fmtDate(r.effectiveFrom) || dash())} → ${esc(fmtDate(r.effectiveTo) || t('NoEndDate', 'No end date'))}</div>`
                + '</div>');
        } else {
            parts.push(`<div class="vfp-rs-empty">${esc(t('ResolveNoSelected', 'No policy governs this target in this context.'))}</div>`);
        }

        const cands = r.candidatePolicies || [];
        parts.push(`<h6 class="text-uppercase text-heading fw-semibold mb-2 mt-3">${esc(t('ResolveCandidates', 'Candidate policies'))}</h6>`);
        if (cands.length) {
            parts.push('<div class="table-responsive"><table class="table table-sm border-top"><thead><tr>'
                + `<th>${esc(t('FieldPolicyName', 'Name'))}</th><th>${esc(t('DvSectionFrequency', 'Frequency'))}</th>`
                + `<th class="text-center">${esc(t('DvSectionPriority', 'Priority'))}</th><th class="text-center">${esc(t('ResolveSpecificity', 'Specificity'))}</th>`
                + `<th>${esc(t('FieldStatus', 'Status'))}</th><th>${esc(t('ResolveSelectedBadge', 'Selected'))}</th>`
                + '</tr></thead><tbody>' + cands.map(candidateRow).join('') + '</tbody></table></div>');
        } else {
            parts.push(`<div class="vfp-meta">${esc(t('ResolveNoCandidates', 'No candidate policies.'))}</div>`);
        }

        const codes = r.reasonCodes || [];
        if (codes.length) {
            parts.push(`<h6 class="text-uppercase text-heading fw-semibold mb-2 mt-3">${esc(t('ResolveReasonCodes', 'Reason codes'))}</h6>`);
            parts.push(`<div class="vfp-rs-codes">${codes.map(rc => badge(reasonLabel(rc), 'secondary')).join('')}</div>`);
        }

        el('vfpRsResult').innerHTML = parts.join('');
    };

    async function runResolve() {
        const targetType = norm(el('vfpRsTargetType')?.value);
        if (!targetType) { window.showToast?.(t('ResolveTargetTypeRequired', 'Pick a target type.'), 'error'); return; }
        const targetId = targetIdValue();
        if (!targetId) { window.showToast?.(t('TargetRequired', 'Pick a target.'), 'error'); return; }

        const q = new URLSearchParams();
        q.set('targetType', targetType);
        q.set('targetId', targetId);
        const at = norm(el('vfpRsEffectiveAt')?.value);
        if (at) q.set('effectiveAt', at);
        const ctx = {
            businessUnit: norm(el('vfpRsBusinessUnit')?.value),
            territoryNodeId: norm(el('vfpRsTerritoryNode')?.value),
            campaignId: norm(el('vfpRsCampaign')?.value),
            segmentId: norm(el('vfpRsSegment')?.value),
            brandId: norm(el('vfpRsBrand')?.value),
            productId: norm(el('vfpRsProduct')?.value),
            conceptNodeId: norm(el('vfpRsConceptNode')?.value),
            audienceProfileId: norm(el('vfpRsAudienceProfile')?.value)
        };
        Object.entries(ctx).forEach(([k, v]) => { if (v) q.set(k, v); });

        showResolveState('running');
        try {
            const r = await getJson(`/visit-frequency-policies/resolve?${q.toString()}`);
            if (!r) throw new Error(t('ErrorState', 'Error'));
            await renderResolveResult(r);
            showResolveState('result');
        } catch (e) {
            window.showToast?.(e.message || t('ErrorState', 'Error'), 'error');
            showResolveState('empty');
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        if (PANEL) void initResolvePanel();
    });
})(window, document);
