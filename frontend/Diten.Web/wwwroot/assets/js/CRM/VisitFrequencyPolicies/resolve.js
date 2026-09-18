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
    const periodLabels = L.periodLabels || {};       // period CODE → "ay/hafta/…" (freq "N / dönem")
    const cadenceLabels = L.cadenceLabels || {};     // period CODE → "ayda/haftada/…" (headline)
    const targetTypeLabels = L.targetTypeLabels || {}; // targetType CODE → friendly label

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
    const isGuid = v => /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(norm(v));

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
    // WP-FREQ-DET-O — target types whose picker searches server-side (large lists); the others keep the DET-L preload.
    const REMOTE_KINDS = new Set(['contact', 'account']);

    const fillSelect = (select, options, placeholder) => {
        if (!select) return;
        const head = placeholder != null ? `<option value="">${esc(placeholder)}</option>` : '';
        select.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };

    // ── select2 (search) enhancement — the app-wide pattern (jQuery select2, dropdownParent, native-change bridge). ─────
    // WP-FREQ-DET-L. select2 + jQuery are loaded app-wide by the tenant shell layout. PRESENTATION + SEARCH ONLY: the
    // underlying <select> keeps its id, data-role, value and selectedOptions, so targetIdValue() and the /resolve query
    // read it byte-for-byte. select2 announces a choice the jQuery way ($(el).trigger('change')), which a native
    // addEventListener never hears — a bridge re-dispatches a native BUBBLING change so the existing showPicked / cascade
    // change listeners still fire. select2 renders from a SNAPSHOT of the options, so any re-fill must destroy + re-init.
    const jq = () => window.jQuery;
    const hasSelect2 = () => { const $ = jq(); return !!($ && $.fn && $.fn.select2); };
    const bindSelect2 = select => {
        if (!select || !hasSelect2()) return;
        const $ = jq();
        const $s = $(select);
        if ($s.hasClass('select2-hidden-accessible')) return; // already bound
        $s.select2({ dropdownParent: $s.parent() });
        // Bridge: carry select2's jQuery-synthesised change across to a native bubbling change (a real DOM change already
        // reached the native listeners and jQuery marks it with `originalEvent` — that guard stops the bridge echoing).
        $s.off('change.vfpBridge').on('change.vfpBridge', ev => {
            if (ev && ev.originalEvent) return;
            select.dispatchEvent(new Event('change', { bubbles: true }));
        });
    };
    const unbindSelect2 = select => {
        if (!select || !hasSelect2()) return;
        const $ = jq();
        const $s = $(select);
        if ($s.hasClass('select2-hidden-accessible')) { $s.off('change.vfpBridge'); $s.select2('destroy'); }
    };
    // destroy + re-init after the options were rebuilt — select2 snapshots them at init.
    const rebindSelect2 = select => { unbindSelect2(select); bindSelect2(select); };

    // ── remote (server-side) select2 — WP-FREQ-DET-O. contact/account lists are 128K/43K rows; a client-side snapshot
    // (fillSelect + pageSize=200) can't find a record outside the first page. The backend List already accepts ?search=
    // (ListContactsQuery/ListAccountsQuery → ListAsync search) and the web proxy forwards it, so search runs server-side:
    // select2 ajax calls getJson('/contacts?search=…&pageSize=20') and shows a NAME (mapOption). Nothing fabricated;
    // value stays the selected GUID. jQuery/select2 absent → silent no-op (degrades to the bare select), like DET-L.
    const REMOTE_PLURAL = { contact: 'contacts', account: 'accounts' };
    const bindSelect2Remote = (select, kind) => {
        if (!select || !hasSelect2()) return;
        const $ = jq();
        const $s = $(select);
        if ($s.hasClass('select2-hidden-accessible')) return; // already bound
        const plural = REMOTE_PLURAL[kind];
        if (!plural) return;
        $s.select2({
            dropdownParent: $s.parent(),
            minimumInputLength: 0,
            allowClear: true,
            placeholder: t('SelectOption', '—'),
            ajax: {
                delay: 250,
                transport: (params, success, failure) => {
                    const term = (params.data && params.data.term) ? params.data.term : '';
                    getJson('/' + plural + '?search=' + encodeURIComponent(term) + '&pageSize=20')
                        .then(data => {
                            const items = Array.isArray(data) ? data : (data?.items || data?.values || []);
                            const results = items.map(mapOption)
                                .filter(o => o.value !== '' && o.value != null)
                                .map(o => ({ id: o.value, text: o.text }));
                            success({ results });
                        })
                        .catch(() => { if (failure) failure(); });
                    return { abort() { } };
                }
            }
        });
        // same native-change bridge as bindSelect2 so the existing showPicked change listener still fires.
        $s.off('change.vfpBridge').on('change.vfpBridge', ev => {
            if (ev && ev.originalEvent) return;
            select.dispatchEvent(new Event('change', { bubbles: true }));
        });
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

    // period/targetType humanized display through the closed L10n maps (contract codes drive them; humanized fallback).
    const periodLabel = p => periodLabels[norm(p)] || humanize(p) || '';
    const cadenceLabel = p => cadenceLabels[norm(p)] || periodLabel(p);
    const targetTypeLabel = code => targetTypeLabels[norm(code)] || humanize(code);
    // composite-format template ({0},{1},…) so each language keeps its own word order.
    const fmt = (tpl, ...args) => String(tpl || '').replace(/\{(\d+)\}/g, (m, i) => (args[Number(i)] != null ? String(args[Number(i)]) : ''));

    const freqSentence = (count, period) => {
        const c = norm(count);
        const per = t('PerPeriod', '/');
        const pd = norm(period) ? periodLabel(period) : '';
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
    //  2) RESOLVE / ÇÖZÜMLEME PANEL — WP-FREQ-DET-H "Frekans Kontrolü" mockup (3 cards).
    //     A friendly SCENARIO → (targetType + context) drives GET /resolve (IncludeDiagnostics=true); the answer, the
    //     rule ladder and the "ne yapmalıyım?" steps are all DERIVED from the real payload — nothing is fabricated.
    // ════════════════════════════════════════════════════════════════════════════
    const PANEL = el('vfpResolvePanel');

    // Friendly scenario → targetType (+ scenario-specific context). Kept as a display mapping; the targetType still has
    // to be in the contract vocabulary (scenarios whose type is absent are dropped in initResolvePanel).
    const SCENARIOS = [
        { id: 'contact-territory', targetType: 'contact', context: ['territory'], labelKey: 'ResolveScenario_contact_territory' },
        { id: 'contact', targetType: 'contact', context: [], labelKey: 'ResolveScenario_contact' },
        { id: 'account', targetType: 'account', context: [], labelKey: 'ResolveScenario_account' },
        { id: 'account-contact-link', targetType: 'account-contact-link', context: [], labelKey: 'ResolveScenario_account-contact-link' },
        { id: 'segment', targetType: 'segment', context: [], labelKey: 'ResolveScenario_segment' },
        { id: 'campaign-target', targetType: 'campaign-target', context: [], labelKey: 'ResolveScenario_campaign-target' },
        { id: 'territory-node', targetType: 'territory-node', context: [], labelKey: 'ResolveScenario_territory-node' },
        { id: 'concept-node', targetType: 'concept-node', context: [], labelKey: 'ResolveScenario_concept-node' },
        { id: 'audience-profile', targetType: 'audience-profile', context: [], labelKey: 'ResolveScenario_audience-profile' }
    ];
    let scenarioList = [];
    const scenarioById = id => scenarioList.find(s => s.id === norm(id)) || scenarioList[0] || null;

    // priority weight → contract band code (last-resort detection for the "yedek" ladder tag). Reuses bandByWeight.
    const ensureBands = async () => {
        if (!bandByWeight) {
            bandByWeight = new Map();
            const c = await loadContract();
            (c?.vocabulary?.priorityBands || []).forEach(b => bandByWeight.set(Number(b.value), b.code));
        }
        return bandByWeight;
    };
    const bandCode = p => { const n = Number(p); return (bandByWeight && !Number.isNaN(n)) ? (bandByWeight.get(n) || '') : ''; };

    // WP-FREQ-DET-I — the picked "Hangi kayıt" chip (type badge + resolved NAME + optional external code + "değiştir").
    // Empty → the app select in #vfpRsTargetPicker is shown; picked → that picker is hidden (d-none) and this chip shows.
    // The [data-role="targetId"] control (id vfpRsTargetId) stays in the DOM with its value, so targetIdValue() and the
    // /resolve query are unchanged; "değiştir" simply re-reveals the picker.
    const showPicked = name => {
        const host = el('vfpRsTargetPicked');
        const picker = el('vfpRsTargetPicker');
        if (!host) return;
        if (!name) {
            host.innerHTML = '';
            host.classList.remove('is-shown');
            picker?.classList.remove('d-none');
            return;
        }
        const targetType = scenarioById(el('vfpRsScenario')?.value)?.targetType || '';
        const typeLabel = targetType ? targetTypeLabel(targetType) : '';
        const val = targetIdValue();
        const ext = (val && !isGuid(val) && val !== name) ? val : '';
        host.innerHTML = `
            <div class="vfp-rs-picked-chip">
                ${typeLabel ? `<span class="vfp-rs-picked-code">${esc(typeLabel)}</span>` : ''}
                <span class="vfp-rs-picked-name">${esc(name)}</span>
                ${ext ? `<span class="vfp-rs-picked-ext">${esc(ext)}</span>` : ''}
                <button type="button" class="vfp-rs-picked-change">${esc(t('ResolveChange', 'değiştir'))}</button>
            </div>`;
        host.classList.add('is-shown');
        picker?.classList.add('d-none');
        const change = host.querySelector('.vfp-rs-picked-change');
        if (change) change.addEventListener('click', () => {
            picker?.classList.remove('d-none');
            host.classList.remove('is-shown');
            host.innerHTML = '';
        });
    };

    // the record (Kayıt) picker for the scenario's targetType — reuses TARGET_KIND / loadOptions (names, never GUIDs).
    // WP-FREQ-DET-I — each control is wrapped in the app field pattern (.diten-field + leading icon); a normal single
    // select shows the picked chip on change (showPicked). id vfpRsTargetId + data-role="targetId" are unchanged.
    const renderTargetPicker = async targetType => {
        const host = el('vfpRsTargetPicker');
        if (!host) return;
        showPicked('');
        const kind = TARGET_KIND[targetType];
        if (targetType === 'territory-node') {
            host.innerHTML = `<div class="diten-field mb-2"><i class="bx bx-sitemap diten-field-icon" aria-hidden="true"></i><select class="form-select select2" id="vfpRsTargetTerModel"></select></div>`
                + `<div class="diten-field"><i class="bx bx-map-pin diten-field-icon" aria-hidden="true"></i><select class="form-select select2" id="vfpRsTargetId" data-role="targetId"></select></div>`;
            fillSelect(el('vfpRsTargetTerModel'), await loadOptions('territory-model'), t('SelectTerritoryModel', 'Select model'));
            fillSelect(el('vfpRsTargetId'), [], t('SelectTerritoryNode', 'Select node'));
            rebindSelect2(el('vfpRsTargetTerModel'));
            rebindSelect2(el('vfpRsTargetId'));
            el('vfpRsTargetTerModel').addEventListener('change', async e => {
                fillSelect(el('vfpRsTargetId'), e.target.value ? await loadOptions('territory-node', e.target.value) : [], t('SelectTerritoryNode', 'Select node'));
                rebindSelect2(el('vfpRsTargetId'));
            });
            el('vfpRsTargetId').addEventListener('change', e => {
                const opt = e.target.selectedOptions[0];
                showPicked(opt && opt.value ? opt.text : '');
            });
            return;
        }
        // WP-FREQ-DET-O — contact/account: server-side (ajax) search instead of a preloaded pageSize=200 snapshot.
        if (REMOTE_KINDS.has(targetType)) {
            host.innerHTML = `<div class="diten-field"><i class="bx bx-crosshair diten-field-icon" aria-hidden="true"></i><select class="form-select select2" id="vfpRsTargetId" data-role="targetId"><option value=""></option></select></div>`;
            bindSelect2Remote(el('vfpRsTargetId'), targetType);
            el('vfpRsTargetId').addEventListener('change', e => {
                const opt = e.target.selectedOptions[0];
                showPicked(opt && opt.value ? opt.text : '');
            });
            return;
        }
        if (!kind) {
            host.innerHTML = `<div class="diten-field"><i class="bx bx-hash diten-field-icon" aria-hidden="true"></i><input type="text" class="form-control" id="vfpRsTargetId" data-role="targetId" placeholder="${esc(t('TargetIdManual', 'Enter id (GUID)'))}"></div>`;
            return;
        }
        host.innerHTML = `<div class="diten-field"><i class="bx bx-crosshair diten-field-icon" aria-hidden="true"></i><select class="form-select select2" id="vfpRsTargetId" data-role="targetId"></select></div>`;
        fillSelect(el('vfpRsTargetId'), await loadOptions(kind), t('SelectOption', '—'));
        rebindSelect2(el('vfpRsTargetId'));
        el('vfpRsTargetId').addEventListener('change', e => {
            const opt = e.target.selectedOptions[0];
            showPicked(opt && opt.value ? opt.text : '');
        });
    };

    // scenario-specific context (e.g. doctor-in-hospital reveals a Saha alanı model+node picker → territoryNodeId).
    const renderScenarioContext = async scenario => {
        const host = el('vfpRsScenarioContext');
        if (!host) return;
        if (!scenario || !scenario.context.includes('territory')) {
            host.innerHTML = '';
            host.classList.add('d-none');
            return;
        }
        host.classList.remove('d-none');
        host.innerHTML = `<label class="form-label small mb-1">${esc(t('FieldTerritory', 'Territory'))}</label>`
            + `<select class="form-select form-select-sm select2 mb-2" id="vfpRsCtxTerModel"></select>`
            + `<select class="form-select form-select-sm select2" id="vfpRsCtxTerNode"></select>`;
        fillSelect(el('vfpRsCtxTerModel'), await loadOptions('territory-model'), t('SelectTerritoryModel', 'Select model'));
        fillSelect(el('vfpRsCtxTerNode'), [], t('SelectTerritoryNode', 'Select node'));
        rebindSelect2(el('vfpRsCtxTerModel'));
        rebindSelect2(el('vfpRsCtxTerNode'));
        el('vfpRsCtxTerModel').addEventListener('change', async e => {
            fillSelect(el('vfpRsCtxTerNode'), e.target.value ? await loadOptions('territory-node', e.target.value) : [], t('SelectTerritoryNode', 'Select node'));
            rebindSelect2(el('vfpRsCtxTerNode'));
        });
    };

    const targetIdValue = () => norm(el('vfpRsTargetId')?.value);

    const applyScenario = async scenario => {
        if (!scenario) return;
        await Promise.all([renderTargetPicker(scenario.targetType), renderScenarioContext(scenario)]);
    };

    const initResolvePanel = async () => {
        const c = await loadContract();
        const targetTypes = c?.vocabulary?.targetTypes || [];
        await ensureBands();
        // scenario → targetType mapping is anchored to the contract vocabulary.
        scenarioList = SCENARIOS.filter(s => targetTypes.includes(s.targetType));
        fillSelect(el('vfpRsScenario'), scenarioList.map(s => ({ value: s.id, text: t(s.labelKey, s.id) })), null);
        fillSelect(el('vfpRsCyclePeriod'), await loadOptions('cycle-period'), t('ContextNone', '— none —'));

        await applyScenario(scenarioById(el('vfpRsScenario')?.value));
        el('vfpRsScenario')?.addEventListener('change', e => void applyScenario(scenarioById(e.target.value)));
        el('vfpRsRun')?.addEventListener('click', () => void runResolve());
    };

    const showResolveState = state => {
        el('vfpRsResultEmpty')?.classList.toggle('d-none', state !== 'empty');
        el('vfpRsRunning')?.classList.toggle('d-none', state !== 'running');
        el('vfpRsResult')?.classList.toggle('d-none', state !== 'result');
    };

    // ── verdict → answer headline / explain / tone ────────────────────────────────
    const HEADLINE_KEY = { unknown: 'ResolveHeadlineUnknown', conflict: 'ResolveHeadlineConflict', not_applicable: 'ResolveHeadlineNotApplicable' };
    const EXPLAIN_KEY = { resolved: 'ResolveExplainResolved', unknown: 'ResolveExplainUnknown', conflict: 'ResolveExplainConflict', not_applicable: 'ResolveExplainNotApplicable' };
    // WP-FREQ-DET-M — display verdict: the mockup's five colour states. Backend FrequencyStatus stays
    // resolved/conflict/unknown/not_applicable; "date_out" is DERIVED (unknown + every candidate eliminated ONLY because
    // it is outside its effective window) so the answer box can read "tarih dışında" in danger tone. Nothing fabricated.
    const displayVerdict = (r, cands) => {
        const v = norm(r.frequencyStatus);
        if (v === 'conflict') return 'conflict';
        if (v === 'resolved') return 'resolved';
        if (v === 'not_applicable') return 'not_applicable';
        // v === 'unknown' (no winner): every candidate rejected solely as out-of-date → date_out; mixed reasons → unknown.
        if (cands.length > 0 && cands.every(c => norm(c.reason) === 'policy_not_effective')) return 'date_out';
        return 'unknown';
    };
    // headline: any winner (resolved OR conflict) reads "N / dönem"; date_out gets its own headline; else the status key.
    const answerHeadline = (r, dv) => {
        if (!!norm(r.selectedFrequencyPolicyId) && r.requiredVisitCount != null) {
            return fmt(t('ResolveHeadlineResolved', '{1} {0}'), r.requiredVisitCount, cadenceLabel(r.periodType));
        }
        if (dv === 'date_out') return t('ResolveHeadlineDateOut', 'Bu tarihte geçerli kural yok');
        const v = norm(r.frequencyStatus);
        return t(HEADLINE_KEY[v] || 'ResolveHeadlineUnknown', verdictLabel(v));
    };
    // display verdict → answer-box tone modifier + badge (text/tone). unknown/not_applicable stay neutral (no modifier).
    const ANSWER_TONE_CLASS = { resolved: 'success', conflict: 'warning', date_out: 'danger' };
    const VERDICT_BADGE = {
        resolved: { key: 'ResolveVerdictBadge_resolved', fb: 'net sonuç', tone: 'success' },
        conflict: { key: 'ResolveVerdictBadge_conflict', fb: 'çakışma çözüldü', tone: 'warning' },
        date_out: { key: 'ResolveVerdictBadge_dateout', fb: 'tarih dışında', tone: 'danger' },
        unknown: { key: 'ResolveVerdictBadge_unknown', fb: 'Bilinmiyor', tone: 'secondary' }
    };
    const answerTone = v => verdictTone(v);

    // ── candidate (ladder) status tag ─────────────────────────────────────────────
    const ELIMINATED_REASONS = new Set(['policy_not_effective', 'policy_inactive', 'policy_archived',
        'business_scope_mismatch', 'campaign_context_missing', 'segment_context_missing', 'cycle_context_missing']);
    const candTag = c => {
        if (c.selected) return { text: t('ResolveTagSelected', 'seçildi'), tone: 'success' };
        const reason = norm(c.reason);
        if (reason === 'policy_not_effective') return { text: t('ResolveTagNotEffective', 'tarih dışı'), tone: 'danger' };
        if (ELIMINATED_REASONS.has(reason)) return { text: t('ResolveTagInactive', 'devrede değil'), tone: 'secondary' };
        // eligible-but-lost: a last-resort-band policy reads as a "yedek" (fallback); otherwise it just didn't win.
        if (bandCode(c.priority) === 'last-resort') return { text: t('ResolveTagFallback', 'yedek'), tone: 'info' };
        return { text: t('ResolveTagInactive', 'devrede değil'), tone: 'secondary' };
    };

    // ── NE YAPMALIYIM? — steps derived from the verdict ───────────────────────────
    const NEXT_STEPS = {
        resolved: [{ kind: 'ok', l: 'ResolveDo_resolved_1', d: 'ResolveDo_resolved_1d' }, { kind: 'info', l: 'ResolveDo_resolved_2', d: 'ResolveDo_resolved_2d' }],
        conflict: [{ kind: 'warn', l: 'ResolveDo_conflict_1', d: 'ResolveDo_conflict_1d' }, { kind: 'info', l: 'ResolveDo_conflict_2', d: 'ResolveDo_conflict_2d' }],
        unknown: [{ kind: 'warn', l: 'ResolveDo_unknown_1', d: 'ResolveDo_unknown_1d' }, { kind: 'info', l: 'ResolveDo_unknown_2', d: 'ResolveDo_unknown_2d' }],
        not_applicable: [{ kind: 'info', l: 'ResolveDo_na_1', d: 'ResolveDo_na_1d' }]
    };
    const DO_ICON = { ok: '✓', warn: '!', info: 'i' };
    const renderResolveResult = async r => {
        await ensureBands();
        const v = norm(r.frequencyStatus);
        const hasWinner = !!norm(r.selectedFrequencyPolicyId);

        // ladder candidates — computed FIRST so the display verdict (date_out derivation) can read them.
        // "dardan genişe" (ascending specificity = narrow → broad).
        const cands = (r.candidatePolicies || []).slice().sort((a, b) => {
            const sa = a.specificity == null ? 99 : Number(a.specificity), sb = b.specificity == null ? 99 : Number(b.specificity);
            if (sa !== sb) return sa - sb;
            return (a.priority || 0) - (b.priority || 0);
        });
        const specs = cands.map(c => Number(c.specificity)).filter(n => !Number.isNaN(n));
        const minSpec = specs.length ? Math.min(...specs) : null;
        const maxSpec = specs.length ? Math.max(...specs) : null;

        // CEVAP card — WP-FREQ-DET-M: the mockup's five colour states. The answer box is tinted by the DISPLAY verdict
        // (resolved=green / conflict=amber / date_out=red / unknown+not_applicable=neutral) and the badge on the right
        // states it in words; the number stays the focus. Backend FrequencyStatus is unchanged (date_out is derived).
        const dv = displayVerdict(r, cands);
        const toneClass = ANSWER_TONE_CLASS[dv] || '';
        const bd = VERDICT_BADGE[dv];
        const verdictBadge = bd ? badge(t(bd.key, bd.fb), bd.tone) : badge(verdictLabel(v), 'secondary');
        const explainKey = dv === 'date_out' ? 'ResolveExplainDateOut' : (EXPLAIN_KEY[v] || 'ResolveExplainUnknown');
        const answer = `<div class="vfp-rs-answer${toneClass ? ' vfp-rs-answer--' + toneClass : ''}">`
            + '<div class="vfp-rs-answer-main">'
            + `<span class="vfp-rs-answer-label">${esc(t('ResolveAnswerLabel', 'Cevap'))}</span>`
            + `<span class="vfp-rs-answer-headline">${esc(answerHeadline(r, dv))}</span>`
            + `<span class="vfp-rs-answer-explain">${esc(t(explainKey, ''))}</span>`
            + '</div>'
            + verdictBadge
            + '</div>';

        // winning rule ----------------------------------------------------------------
        let winner = '';
        if (hasWinner) {
            const prio = await priorityText(r.priority);
            const scopeBits = [prio, humanize(r.source) ? sourcePretty(r.source) : '',
                `${fmtDate(r.effectiveFrom) || dash()} → ${fmtDate(r.effectiveTo) || t('NoEndDate', 'No end date')}`,
                reasonLabel(r.selectionReason)].filter(Boolean);
            const openBtn = `<a class="btn btn-sm btn-label-secondary" href="/CRM/VisitFrequencyPolicies/Details/${esc(r.selectedFrequencyPolicyId)}">${esc(t('ResolveOpenPolicy', 'Open policy'))}</a>`;
            winner = `<div class="mt-2"><span class="vfp-rs-block-title">${esc(t('ResolveWinnerTitle', 'Bu frekansı veren kural'))}</span></div>`
                + '<div class="vfp-rs-winner mt-2">'
                + `<div class="vfp-rs-winner-id"><span class="vfp-rs-winner-name">${esc(r.selectedPolicyName || dash())}</span>`
                + `<span class="vfp-rs-winner-scope">${esc(scopeBits.join(' · '))}</span></div>`
                + `<span class="vfp-rs-winner-freq">${esc(freqSentence(r.requiredVisitCount, r.periodType))}</span>`
                + openBtn + '</div>';
        }

        // (ladder candidates `cands` + specs/minSpec/maxSpec are computed above so the display verdict can read them.)

        // WP-FREQ-DET-I — friendly type sub-line: targetType label (segment → "Segmentteki tüm hedefler") + a resolved
        // NAME only where the candidate genuinely carries one (nameOf; never fabricated) + the narrow/broad hint.
        const specHintOf = c => {
            const s = Number(c.specificity);
            if (Number.isNaN(s) || minSpec === maxSpec) return '';
            if (s === minSpec) return t('SpecNarrowest', 'en dar kapsam');
            if (s === maxSpec) return t('SpecBroadest', 'en geniş kapsam');
            return '';
        };
        const candTypeFriendly = tt => (norm(tt) === 'segment'
            ? t('ResolveCandSegmentAll', 'Segmentteki tüm hedefler') : targetTypeLabel(tt));
        const candTypeSub = async c => {
            const kind = TARGET_KIND[c.targetType];
            const cid = norm(c.targetId);
            let name = '';
            if (kind && cid) { try { name = await nameOf(kind, cid); } catch (e) { name = ''; } }
            return [candTypeFriendly(c.targetType), name, specHintOf(c)].filter(Boolean).join(' · ');
        };

        // WP-FREQ-DET-I — an explanatory "why" sentence, driven ONLY by real fields (reason code + selected + band +
        // specificity), keyed to L10n (7 languages) with a reasonLabel/humanize fallback. Nothing is fabricated.
        const CTX_MISS = {
            segment_context_missing: 'FieldSegment', campaign_context_missing: 'FieldCampaign',
            cycle_context_missing: 'FieldCyclePeriod', business_scope_mismatch: 'FieldBusinessUnit',
            contact_location_context_absent: 'FieldTerritory'
        };
        const candWhy = c => {
            const reason = norm(c.reason);
            if (c.selected) {
                if (reason === 'policy_selected_by_priority') return t('ResolveWhySelPriority', reasonLabel('policy_selected_by_priority'));
                if (reason === 'policy_selected_by_latest_effective_from') return reasonLabel(reason);
                return t('ResolveWhySelSpecificity', 'En dar kapsamlı kural olduğu için seçildi.');
            }
            if (reason === 'policy_not_effective') return t('ResolveWhyNotEffective', 'Kural var ama seçtiğiniz tarihte geçerli değil.');
            if (reason === 'policy_inactive' || reason === 'policy_archived') return reasonLabel(reason);
            if (CTX_MISS[reason]) return fmt(t('ResolveWhyContextMissing', 'Seçilen bağlamda geçerli değil ({0} eksik).'), L[CTX_MISS[reason]] || humanize(reason));
            if (bandCode(c.priority) === 'last-resort') return t('ResolveWhyLastResort', 'Daha dar bir kural bulunmazsa bu kural devreye girer.');
            const s = Number(c.specificity);
            if (!Number.isNaN(s) && minSpec !== maxSpec && s === maxSpec) return t('ResolveWhyBroader', 'Bölge/segment kuralı, daha dar kurallardan geniş kapsamlı.');
            return t('ResolveWhySpecificityLost', 'Daha dar kapsamlı bir kural olduğu için bu kural uygulanmadı.');
        };

        const candRow = async c => {
            const tag = candTag(c);
            const sub = await candTypeSub(c);
            return `<div class="vfp-rs-cand${c.selected ? ' vfp-rs-cand--selected' : ''}">`
                + `<div class="vfp-rs-cand-id"><span class="vfp-rs-cand-name">${esc(c.policyName || dash())}</span>`
                + `<span class="vfp-rs-cand-type">${esc(sub)}</span></div>`
                + `<span class="vfp-rs-cand-freq">${esc(freqSentence(c.requiredVisitCount, c.periodType))}</span>`
                + badge(tag.text, tag.tone)
                + `<div class="vfp-rs-cand-why">${esc(candWhy(c))}</div>`
                + '</div>';
        };
        const ladderHead = `<div class="mt-3 mb-2"><span class="vfp-rs-block-title">${esc(t('ResolveLadderTitle', 'Bu hedefe denk gelen diğer kurallar'))}</span> `
            + `<span class="vfp-rs-block-hint">${esc(t('ResolveLadderSort', 'dardan genişe sıralı'))}</span></div>`;
        const ladderBody = cands.length ? (await Promise.all(cands.map(candRow))).join('') : `<div class="vfp-meta">${esc(t('ResolveLadderEmpty', 'Bu hedefe denk gelen başka kural yok.'))}</div>`;

        const answerCard = '<section class="card mb-4"><div class="card-body p-4">'
            + answer + winner + ladderHead + ladderBody + '</div></section>';

        // NE YAPMALIYIM? card ---------------------------------------------------------
        const steps = NEXT_STEPS[v] || NEXT_STEPS.unknown;
        const stepRow = s => `<div class="vfp-rs-do"><span class="vfp-rs-do-icon vfp-rs-do-icon--${s.kind}">${DO_ICON[s.kind]}</span>`
            + `<div><div class="vfp-rs-do-label">${esc(t(s.l, ''))}</div><div class="vfp-rs-do-detail">${esc(t(s.d, ''))}</div></div></div>`;
        const doCard = '<section class="card"><div class="card-body p-4">'
            + `<div class="mb-3"><span class="vfp-rs-block-title">${esc(t('ResolveNextTitle', 'Ne yapmalıyım?'))}</span></div>`
            + steps.map(stepRow).join('') + '</div></section>';

        el('vfpRsResult').innerHTML = answerCard + doCard;
    };

    // source code → localized label (reuses the closed source set; humanized fallback).
    const sourceLabels = L.sourceLabels || {};
    const sourcePretty = s => sourceLabels[norm(s)] || humanize(s) || dash();

    async function runResolve() {
        const scenario = scenarioById(el('vfpRsScenario')?.value);
        if (!scenario) { window.showToast?.(t('ResolveTargetTypeRequired', 'Pick a target type.'), 'error'); return; }
        const targetId = targetIdValue();
        if (!targetId) { window.showToast?.(t('TargetRequired', 'Pick a target.'), 'error'); return; }

        const q = new URLSearchParams();
        q.set('targetType', scenario.targetType);
        q.set('targetId', targetId);
        // WP-FREQ-DET-K — "X için çöz" semantiği: senaryonun targetType'ı kendi-kapsam boyutuysa targetId'yi eşleşen
        // context param olarak DA gönder. Böylece hem hedeflenen (targetMatched) hem kapsamlı (context) politikalar
        // uygulanır; başka bir kapsamdaki politika request≠policy ile doğru şekilde elenir. (Detay-analiz handler ile
        // aynı davranış.) Map dışı targetType'lar (contact/account/account-contact-link) değişmez.
        const SCENARIO_SELF_CONTEXT = {
            'segment': 'segmentId',
            'campaign-target': 'campaignId',
            'territory-node': 'territoryNodeId',
            'concept-node': 'conceptNodeId',
            'audience-profile': 'audienceProfileId'
        };
        const selfCtx = SCENARIO_SELF_CONTEXT[norm(scenario.targetType)];
        if (selfCtx) q.set(selfCtx, targetId);
        q.set('includeDiagnostics', 'true');
        const at = norm(el('vfpRsEffectiveAt')?.value);
        if (at) q.set('effectiveAt', at);
        // scenario-specific context: the doctor-in-hospital scenario supplies the contact-location (territory) context.
        const terNode = norm(el('vfpRsCtxTerNode')?.value);
        if (scenario.context.includes('territory') && terNode) q.set('territoryNodeId', terNode);

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
