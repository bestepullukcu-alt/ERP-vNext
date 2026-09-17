/**
 * MOD-0165-FU03 (WP-FREQ-DET-B) Visit Frequency / Call-Cycle Policy — DETAILS separate page. FRONTEND ONLY, READ-ONLY
 * (except the header Archive action, which reuses the FU03 archive endpoint behind a confirm).
 *
 * The FREQ-C quick-view offcanvas is retired; this is the standalone Details page. On load it fetches the full policy
 * read model (GET /visit-frequency-policies/{id}) + the DET-A detail analysis (GET /{id}/analysis) and renders the
 * mockup: header (name/status/code/description + Düzenle/Archive), 4 stat cards (frequency / target[+impact count] /
 * validity / weight), context chips, notes, a status-flow timeline, an ETKİ (impact) block and the conflicting-policies
 * list (DET-A analysis.conflicts candidates → selected/aday + human-language reason).
 *
 * The name/label/verdict/reason/band resolution mirrors the FREQ-C resolve.js pattern (READERS / mapOption / nameOf /
 * humanized-with-L10n-fallback) — the pattern is COPIED here; resolve.js is not touched. Nothing is fabricated: an
 * uncomputable impact renders "—" + the backend ProjectionNote, and an unresolved ref degrades to a short id.
 */
(function (window, document) {
    'use strict';

    const ROOT = document.getElementById('vfpDetailPage');
    if (!ROOT) return;
    const endpoint = ROOT.dataset.endpoint || '/CRM/VisitFrequencyPolicies/api';
    const policyId = ROOT.dataset.policyId || '';
    const canManage = ROOT.dataset.canManage === '1';

    let L = {};
    try { L = JSON.parse(document.getElementById('vfp-detail-l10n')?.textContent || '{}'); } catch (e) { L = {}; }
    L = Object.assign({}, window.L10n || {}, L);
    const statusLabels = L.statusLabels || {};
    const verdictLabels = L.verdictLabels || {};
    const reasonLabels = L.reasonLabels || {};
    const bandLabels = L.bandLabels || {};
    const sourceLabels = L.sourceLabels || {};
    const periodLabels = L.periodLabels || {};
    const timelineLabels = L.timelineLabels || {};

    // ── helpers (mirrors resolve.js) ─────────────────────────────────────────────
    const el = id => document.getElementById(id);
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const t = (key, fallback) => L[key] || fallback || key;
    const fmt = (tpl, val) => norm(tpl).includes('{0}') ? norm(tpl).replace('{0}', val) : `${val} ${norm(tpl)}`.trim();
    const humanize = code => norm(code).split(/[-_\s]+/).filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
    const dash = () => t('NotAvailable', '—');
    const shortId = id => { const s = norm(id); return s ? s.slice(0, 8) + '…' : ''; };
    const fmtDate = v => { const s = norm(v); return s ? s.slice(0, 10) : ''; };
    const fmtDateTime = v => { const s = norm(v); return s ? s.slice(0, 10) + ' ' + s.slice(11, 16) : ''; };

    const statusLabel = s => statusLabels[norm(s)] || humanize(s) || dash();
    const statusTone = s => ({ draft: 'secondary', active: 'success', inactive: 'warning', archived: 'secondary' }[norm(s)] || 'primary');
    const verdictLabel = v => verdictLabels[norm(v)] || humanize(v) || dash();
    const reasonLabel = r => reasonLabels[norm(r)] || humanize(r);
    const sourceLabel = s => sourceLabels[norm(s)] || humanize(s) || dash();
    const periodLabel = p => periodLabels[norm(p)] || humanize(p) || '';
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
        'business-unit': () => getJson('/business-units')
    };
    const mapOption = x => ({
        value: x.id ?? x.value ?? x.valueCode ?? x.segmentId ?? x.accountId ?? x.contactId ?? x.campaignId
            ?? x.cyclePeriodId ?? x.conceptNodeId ?? x.audienceProfileId ?? x.brandId ?? x.productId
            ?? x.globalProductId ?? '',
        text: x.name || x.Name || x.text || x.displayName || x.DisplayName || x.label || x.Label
            || x.segmentName || x.SegmentName || x.accountName || x.AccountName || x.contactName || x.ContactName
            || x.fullName || x.FullName || x.campaignName || x.CampaignName || x.periodName || x.PeriodName
            || x.cyclePeriodName || x.conceptName || x.ConceptName || x.audienceProfileName || x.profileName
            || x.brandName || x.BrandName || x.productName || x.ProductName || x.code || x.Code
            || String(x.id ?? x.value ?? x.valueCode ?? '')
    });
    const optionCache = new Map();
    const loadOptions = async kind => {
        if (optionCache.has(kind)) return optionCache.get(kind);
        let options = [];
        try {
            const data = await (READERS[kind] ? READERS[kind]() : Promise.resolve([]));
            const items = Array.isArray(data) ? data : (data?.items || data?.nodes || data?.values || []);
            options = items.map(mapOption).filter(o => o.value !== '' && o.value != null);
        } catch (e) { options = []; }
        optionCache.set(kind, options);
        return options;
    };
    const nameOf = async (kind, id) => {
        const s = norm(id);
        if (!s) return '';
        const options = await loadOptions(kind);
        return (options.find(o => String(o.value) === s) || {}).text || '';
    };

    // Territory nodes have no flat sibling list to page (they live under a parent model), so a bare TerritoryNodeId is
    // resolved to its name through the F19 `nodes/by-ids` reverse-lookup proxy — the SAME proxy the editor's edit-restore
    // uses. Cached per id; degrades to a short id (never a fabricated name) when it cannot resolve.
    const territoryNodeCache = new Map();
    const territoryNodeName = async id => {
        const s = norm(id);
        if (!s) return '';
        if (territoryNodeCache.has(s)) return territoryNodeCache.get(s);
        let name = '';
        try {
            const rows = await getJson(`/territory-models/nodes/by-ids?ids=${encodeURIComponent(s)}`);
            const list = Array.isArray(rows) ? rows : (rows?.items || rows?.nodes || []);
            const hit = list.find(r => String(r.id ?? r.Id ?? r.nodeId ?? r.NodeId) === s) || list[0];
            name = hit ? (hit.name || hit.Name || '') : '';
        } catch (e) { name = ''; }
        territoryNodeCache.set(s, name);
        return name;
    };
    const TARGET_KIND = {
        segment: 'segment', account: 'account', contact: 'contact',
        'campaign-target': 'campaign', 'concept-node': 'concept-node', 'audience-profile': 'audience-profile'
    };

    // ── contract-driven priority band label (weight → band code → localized label) ──────────────────────────────────
    let contractPromise = null;
    const loadContract = () => {
        if (!contractPromise) contractPromise = getJson('/visit-frequency-policies/contract').catch(() => ({}));
        return contractPromise;
    };
    let bandByWeight = null;
    const weightLabel = async priority => {
        const p = priority == null ? null : Number(priority);
        if (p == null || Number.isNaN(p)) return dash();
        if (!bandByWeight) {
            bandByWeight = new Map();
            const c = await loadContract();
            (c?.vocabulary?.priorityBands || []).forEach(b => bandByWeight.set(Number(b.value), b.code));
        }
        const code = bandByWeight.get(p);
        const label = code ? (bandLabels[code] || humanize(code)) : '';
        return label || String(p);
    };

    const freqSentence = (count, period) => {
        const c = norm(count);
        const per = t('PerPeriod', '/');
        const pd = norm(period) ? periodLabel(period) : '';
        return [c, per, pd].filter(Boolean).join(' ');
    };
    const nameValue = (name, id) => {
        const n = norm(name);
        const ref = shortId(id);
        return n ? esc(n) : (ref ? `<span class="vfp-mono">${esc(ref)}</span>` : dash());
    };

    // ── state toggles ────────────────────────────────────────────────────────────
    const showState = state => {
        el('vfpDetLoading')?.classList.toggle('d-none', state !== 'loading');
        el('vfpDetError')?.classList.toggle('d-none', state !== 'error');
        el('vfpDetBody')?.classList.toggle('d-none', state !== 'body');
    };

    // ── renderers ──────────────────────────────────────────────────────────────
    const renderHeader = p => {
        el('vfpDetName').textContent = norm(p.policyName) || dash();
        el('vfpDetStatus').innerHTML = badge(statusLabel(p.status), statusTone(p.status));
        const code = el('vfpDetCode');
        code.textContent = norm(p.policyCode) || '';
        code.classList.toggle('d-none', !norm(p.policyCode));
        const desc = el('vfpDetDesc');
        desc.textContent = norm(p.description) || '';
        desc.classList.toggle('d-none', !norm(p.description));
        // Archive is a manage-only action AND only offered while the policy is not already archived.
        const archiveBtn = el('vfpDetArchive');
        if (archiveBtn) archiveBtn.classList.toggle('d-none', !(canManage && norm(p.status) !== 'archived'));
    };

    const renderStats = async (p, impact) => {
        // FREKANS
        el('vfpStatFreqValue').textContent = freqSentence(p.requiredVisitCount, p.periodType) || dash();
        const period = periodLabel(p.periodType);
        const cnt = p.requiredVisitCount != null ? String(p.requiredVisitCount) : '';
        el('vfpStatFreqSub').textContent = (cnt && period) ? `${cnt} ${t('VisitsWord', 'visits')} / ${period}` : '';

        // HEDEF (target name + "type · N target")
        const targetKind = TARGET_KIND[p.targetType];
        const targetName = targetKind ? await nameOf(targetKind, p.targetId) : '';
        el('vfpStatTargetValue').innerHTML = nameValue(targetName, p.targetId);
        const countTxt = (impact && impact.targetCountComputable && impact.targetCount != null)
            ? `${impact.targetCount} ${t('TargetsWord', 'targets')}` : dash();
        el('vfpStatTargetSub').textContent = `${humanize(p.targetType) || dash()} · ${countTxt}`;

        // GEÇERLİLİK
        const from = fmtDate(p.effectiveFrom);
        const to = norm(p.effectiveTo) ? fmtDate(p.effectiveTo) : t('NoEndDate', 'No end date');
        el('vfpStatValidityValue').innerHTML = `${esc(from || dash())} <span class="text-muted">→</span> ${esc(to)}`;
        el('vfpStatValiditySub').textContent = norm(p.effectiveTo) ? t('FreqBounded', '') : t('FreqOpenEnded', '');

        // AĞIRLIK
        el('vfpStatWeightValue').innerHTML = badge(await weightLabel(p.priority), 'primary');
        el('vfpStatWeightSub').textContent = `${t('FieldSource', 'Source')}: ${sourceLabel(p.source)}`;
    };

    const chip = (label, value) => `<span class="vfp-det-chip"><span class="vfp-det-chip-k">${esc(label)}</span><span class="vfp-det-chip-v">${value}</span></span>`;
    const renderContext = async p => {
        const rows = [];
        if (norm(p.businessUnit)) {
            // BusinessUnit is a MOD-0048 value CODE (a human-readable string, not a GUID): resolve it to the published
            // label when possible, else keep the raw code as-is (a code is readable — never truncate it to a short id).
            const buName = await nameOf('business-unit', p.businessUnit);
            rows.push(chip(t('FieldBusinessUnit', 'Business unit'),
                norm(buName) ? esc(buName) : `<span class="vfp-mono">${esc(p.businessUnit)}</span>`));
        }
        if (norm(p.territoryNodeId)) rows.push(chip(t('FieldTerritory', 'Territory'), nameValue(await territoryNodeName(p.territoryNodeId), p.territoryNodeId)));
        if (norm(p.segmentId)) rows.push(chip(t('FieldSegment', 'Segment'), nameValue(await nameOf('segment', p.segmentId), p.segmentId)));
        if (norm(p.campaignId)) rows.push(chip(t('FieldCampaign', 'Campaign'), nameValue(await nameOf('campaign', p.campaignId), p.campaignId)));
        if (norm(p.brandId)) rows.push(chip(t('FieldBrand', 'Brand'), nameValue(await nameOf('brand', p.brandId), p.brandId)));
        if (norm(p.productId)) rows.push(chip(t('FieldProduct', 'Product'), nameValue(await nameOf('product', p.productId), p.productId)));
        if (norm(p.cyclePeriodId)) rows.push(chip(t('FieldCyclePeriod', 'Cycle period'), nameValue(await nameOf('cycle-period', p.cyclePeriodId), p.cyclePeriodId)));
        el('vfpDetContext').innerHTML = rows.length ? rows.join('') : `<span class="vfp-meta">${esc(t('ContextNone', '— all targets —'))}</span>`;
    };

    const renderNotes = p => {
        if (!norm(p.notes)) return;
        el('vfpDetNotesCard').classList.remove('d-none');
        el('vfpDetNotes').textContent = p.notes;
    };

    // ── DURUM AKIŞI timeline (WP-FREQ-DET-C) ─────────────────────────────────────
    // Rendered from analysis.Timeline (the embedded audit trail, or a timestamp backfill for a pre-trail policy). Each
    // entry: colored dot (by type) + title (weight-changed → "Ağırlık {from}→{to}" with band labels) + date + actor.
    // The derived next-eval entry is faded (future). Falls back to a created/archived stub if analysis is unavailable.
    const TIMELINE_TONE = {
        created: 'secondary', published: 'success', deactivated: 'warning',
        reactivated: 'success', 'weight-changed': 'primary', archived: 'secondary', 'next-eval': 'future'
    };
    const bandCodeLabel = code => { const c = norm(code); return c ? (bandLabels[c] || humanize(c)) : ''; };
    const timelineTitle = e => {
        const type = norm(e.type);
        if (type === 'weight-changed') {
            const arrow = `${bandCodeLabel(e.fromValue)} → ${bandCodeLabel(e.toValue)}`.trim();
            const base = timelineLabels[type] || t('TlWeightChanged', 'Weight changed');
            return `${base}: ${arrow}`;
        }
        return timelineLabels[type] || humanize(type) || dash();
    };
    const renderTimeline = (p, timeline) => {
        const by = who => { const s = norm(who); return s ? ` · ${esc(s.length === 36 ? shortId(s) : s)}` : ''; };
        const item = (tone, title, when, who, future) =>
            `<div class="vfp-dv-tl-item${future ? ' vfp-dv-tl-item--future' : ''}"><span class="vfp-dv-tl-dot vfp-dv-tl-dot--${tone}"></span><span>`
            + `<span class="vfp-dv-tl-title">${esc(title)}</span>`
            + `<span class="vfp-dv-tl-detail">${esc(fmtDateTime(when))}${by(who)}</span></span></div>`;

        const entries = Array.isArray(timeline) ? timeline : null;
        let tl;
        if (entries && entries.length) {
            tl = entries.map(e => item(
                TIMELINE_TONE[norm(e.type)] || 'primary',
                timelineTitle(e),
                e.at,
                e.by,
                !!e.isFuture));
        } else {
            // Defensive fallback (analysis endpoint unavailable): the same created/archived stubs from the read model.
            tl = [item('secondary', timelineLabels.created || t('TlCreated', 'Created'), p.createdAt, p.createdBy, false)];
            if (norm(p.archivedAt)) tl.push(item('secondary', timelineLabels.archived || t('TlArchived', 'Archived'), p.archivedAt, p.archivedBy, false));
        }
        el('vfpDetTimeline').innerHTML = tl.join('');
    };

    const renderImpact = impact => {
        const computable = !!(impact && impact.targetCountComputable && impact.targetCount != null);
        const note = norm(impact && impact.projectionNote);
        const parts = [];
        if (computable) {
            // A computable count (real membership, or a draft preview) shows the two figures; any caveat (draft/period)
            // rides below as a note.
            const visitsVal = (impact && impact.plannedVisitsPerQuarter != null) ? String(impact.plannedVisitsPerQuarter) : dash();
            parts.push(`<div class="vfp-det-impact-line">${esc(fmt(t('ImpactTargetsLine', '{0} targets'), String(impact.targetCount)))}</div>`);
            parts.push(`<div class="vfp-det-impact-line">${esc(fmt(t('ImpactVisitsLine', '{0} visits / quarter'), visitsVal))}</div>`);
            if (note) parts.push(`<div class="vfp-meta mt-2">${esc(note)}</div>`);
        } else {
            // Genuinely uncomputable (candidate cap exceeded, or a target type with no counting path): show the backend's
            // honest reason IN PLACE OF a fabricated "—".
            parts.push(note
                ? `<div class="vfp-meta">${esc(note)}</div>`
                : `<div class="vfp-det-impact-line">${esc(fmt(t('ImpactTargetsLine', '{0} targets'), dash()))}</div>`);
        }
        el('vfpDetImpact').innerHTML = parts.join('');
    };

    const conflictRow = c => {
        const sel = !!c.selected;
        const mark = sel ? badge(t('SelectedBadge', 'Selected'), 'success') : badge(t('CandidateBadge', 'Candidate'), 'secondary');
        return `<div class="vfp-det-cand ${sel ? 'vfp-det-cand--selected' : ''}">`
            + `<div class="vfp-det-cand-head">${mark}<span class="vfp-det-cand-name">${esc(c.policyName || dash())}</span></div>`
            + (norm(c.policyCode) ? `<div class="vfp-mono vfp-meta">${esc(c.policyCode)}</div>` : '')
            + `<div class="vfp-det-cand-freq">${esc(norm(c.frequencySummary))}</div>`
            + `<div class="vfp-meta">${esc(reasonLabel(c.reason))}</div>`
            + `</div>`;
    };
    const renderConflicts = conflicts => {
        const cands = (conflicts && conflicts.candidates) || [];
        const countEl = el('vfpDetConflictsCount');
        if (countEl) countEl.innerHTML = cands.length ? badge(fmt(t('ConflictsCandidateCount', '{0}'), cands.length), 'secondary') : '';
        el('vfpDetConflicts').innerHTML = cands.length
            ? cands.map(conflictRow).join('')
            : `<div class="vfp-meta">${esc(t('ConflictsEmpty', 'No competing policies.'))}</div>`;
    };

    // ── archive (manage-only; reuses the FU03 archive endpoint behind a confirm) ────────────────────────────────────
    const bindArchive = () => {
        const btn = el('vfpDetArchive');
        if (!btn) return;
        btn.addEventListener('click', () => {
            const name = el('vfpDetName')?.textContent || '';
            window.showConfirm?.(t('ArchiveConfirm', ''), async () => {
                try {
                    await envelope(await fetch(`${endpoint}/visit-frequency-policies/${policyId}/archive`,
                        { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' } }));
                    window.showToast?.(t('RecordArchived', ''), 'success');
                    window.location.reload();
                } catch (e) { window.showToast?.(e.message || t('ErrorState', 'Error'), 'error'); }
            }, { entityName: name, type: 'warning', confirmButtonText: t('Archive', 'Archive') });
        });
    };

    const load = async () => {
        showState('loading');
        if (!policyId) { showState('error'); if (el('vfpDetError')) el('vfpDetError').textContent = t('ErrorState', 'Error'); return; }
        try {
            const [p, analysis] = await Promise.all([
                getJson(`/visit-frequency-policies/${policyId}`),
                getJson(`/visit-frequency-policies/${policyId}/analysis`).catch(() => null)
            ]);
            if (!p) throw new Error(t('ErrorState', 'Error'));
            const impact = analysis?.impact || null;
            renderHeader(p);
            await renderStats(p, impact);
            await renderContext(p);
            renderNotes(p);
            renderTimeline(p, analysis?.timeline || null);
            renderImpact(impact);
            renderConflicts(analysis?.conflicts || null);
            bindArchive();
            showState('body');
        } catch (e) {
            if (el('vfpDetError')) el('vfpDetError').textContent = e.message || t('ErrorState', 'Error');
            showState('error');
        }
    };

    document.addEventListener('DOMContentLoaded', () => void load());
})(window, document);
