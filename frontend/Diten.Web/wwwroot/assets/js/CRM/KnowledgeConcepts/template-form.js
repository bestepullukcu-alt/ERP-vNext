/**
 * SCMM-10-UI-refine (MOD-0162) Chain Template — Golden Compact full-page create/edit (Not 5), replacing the Slim
 * offcanvas. Ports the SCMM-10 branched builder onto a route-based page and submits through the same-origin
 * concept-chain-templates proxy.
 *
 * SCMM-10-MOD-C (template-level Moderator/ForWhom): Moderator and ForWhom moved OFF the step and ONTO the template
 * (Identity & Classification). Moderator = a single content-moderator-role published value (store ValueCode →
 * ModeratorRoleType; who presents the chain). ForWhom = an AudienceProfile multi-select (store id[] →
 * ForWhomAudienceProfileIds; the chain's target audience). The step-level AllowedRoleRefs / AudienceDimensionRefs are
 * gone (backend WP-A removed them). Branches are a Tasks-checklist compose-then-add builder (AUD-UI-6 pattern):
 * `.diten-checkitem` step rows + a compose-row (Concept Type + Min/Max + Add).
 *
 * WP-CT-FE-3: the middle panel renders every branch at once as a type-lane diagram (columns = spine types, rows =
 * branches, SVG branch-flow edges); the per-branch pager and the SortableJS step reorder are gone (drag returns in
 * FE-4). The model, the compose add-row, Min/Max editing and submit are unchanged.
 */
(function (window, document) {
    'use strict';
    const $ = window.jQuery;
    const base = '/CRM/KnowledgeConcepts/api';
    const MODERATOR_SET_CODE = 'content-moderator-role';
    const headers = { Accept: 'application/json' };
    const jsonHeaders = { Accept: 'application/json', 'Content-Type': 'application/json' };

    let L = {};
    try { L = JSON.parse(document.getElementById('concept-template-l10n')?.textContent || '{}'); } catch (e) { L = {}; }

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const toDateInput = v => v ? new Date(v).toISOString().slice(0, 10) : '';
    const fromDateInput = v => v ? new Date(`${v}T00:00:00Z`).toISOString() : null;
    const todayInput = () => new Date().toISOString().slice(0, 10);
    const setVal = (id, v) => { const el = document.getElementById(id); if (el) el.value = v == null ? '' : String(v); };
    const val = id => norm(document.getElementById(id)?.value);
    const showAlert = msg => { const el = document.getElementById('conceptTemplateFormAlert'); if (!el) return; el.textContent = msg || ''; el.classList.toggle('d-none', !msg); };

    const envelope = async res => {
        const body = await res.json().catch(() => ({}));
        if (!res.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { handled: true });
        return body.data;
    };
    const getJson = async path => envelope(await fetch(`${base}${path}`, { credentials: 'same-origin', headers }));

    // ─── reference data ─────────────────────────────────────────────────────────
    let types = [];             // { conceptTypeId, subjectId, conceptTypeCode, conceptTypeName, isArchived }
    let audienceOptions = [];   // { value: audienceProfileId, text: "code — name" }
    let moderatorOptions = [];  // { value: ValueCode, text: label }  (content-moderator-role published values)
    const typeNameById = {};
    const typeCodeById = {};       // conceptTypeId → conceptTypeCode (SCMM-10-MOD-D2 step-card code badge)
    const typeNameOnlyById = {};   // conceptTypeId → conceptTypeName (title = name only)
    const subjectLabelById = {};
    const labelType = id => typeNameById[id] || id || '';
    const codeOf = id => typeCodeById[id] || '';
    const nameOf = id => typeNameOnlyById[id] || labelType(id);
    const moderatorLabel = code => moderatorOptions.find(o => String(o.value) === String(code))?.text || code || '';
    const typeOptionsFor = subjectId => types
        .filter(t => String(t.subjectId) === String(subjectId) && !t.isArchived)
        .map(t => ({ value: t.conceptTypeId, text: `${t.conceptTypeCode} — ${t.conceptTypeName}` }));

    // A stored value no longer offered (an archived subject, a retired status) is kept so the form never silently
    // drops it; its label falls back to a resolver, then to the raw value.
    const fillSelect = (id, options, withEmpty, current, currentLabel) => {
        const el = document.getElementById(id);
        if (!el) return;
        const list = (options || []).slice();
        if (current && !list.some(o => String(o.value) === String(current))) list.unshift({ value: current, text: currentLabel || current });
        el.innerHTML = (withEmpty ? '<option value=""></option>' : '') + list.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    // ForWhom multi-select: mark selected audience-profile ids, preserving any stored ref that is no longer offered.
    const fillForWhom = selectedIds => {
        const el = document.getElementById('tplForWhom');
        if (!el) return;
        const sel = (selectedIds || []).map(String);
        const known = audienceOptions.map(o =>
            `<option value="${esc(o.value)}"${sel.includes(String(o.value)) ? ' selected' : ''}>${esc(o.text)}</option>`);
        const extra = sel.filter(v => !audienceOptions.some(o => String(o.value) === v))
            .map(v => `<option value="${esc(v)}" selected>${esc(v)}</option>`);
        el.innerHTML = known.concat(extra).join('');
    };
    const initSelect2 = (sel) => { if ($ && $.fn.select2) $(sel).each(function () { if (!$(this).hasClass('select2-hidden-accessible')) $(this).select2({ width: '100%', placeholder: this.getAttribute('data-placeholder') || '' }); }); };

    // ─── builder model + spine ──────────────────────────────────────────────────
    let branches = [];         // [{ name, steps:[{ conceptTypeId, min, max }] }]
    let templateReadOnly = false;
    const spineFromBranches = () => {
        const seen = new Set(); const out = [];
        branches.forEach(b => b.steps.forEach(s => { const id = String(s.conceptTypeId || ''); if (id && !seen.has(id)) { seen.add(id); out.push(id); } }));
        return out;
    };
    const bumpVersion = v => {
        const m = /^v?(\d+)(?:\.(\d+))?$/i.exec(norm(v));
        if (!m) return norm(v) ? `${norm(v)}-2` : 'v2';
        const major = parseInt(m[1], 10);
        return m[2] != null ? `v${major}.${parseInt(m[2], 10) + 1}` : `v${major + 1}`;
    };

    // ─── WP-CT-FE-2: type palette (left panel) ────────────────────────────────────────────────────────────────────
    // Every type of the selected subject with: its node count (subject-scoped concept-nodes read), how many branches
    // use it, and a "not on spine" badge when no branch uses it (the spine is derived from the branches). Rows carry
    // draggable-ready hooks (js-palette-type / js-palette-handle) — drag behaviour is FE-4; a click is a no-op today.
    const nodeCountByType = {};
    let nodeCountsFor = null;      // subject id the counts belong to
    let nodeCountsReady = false;   // false → counts unknown (loading / failed): the cell shows "—", never a fake 0
    let nodeCountsPending = null;  // { subjectId, promise } — de-dupes the native + jQuery change double fire
    const loadNodeCounts = subjectId => {
        if (nodeCountsPending && nodeCountsPending.subjectId === subjectId) return nodeCountsPending.promise;
        Object.keys(nodeCountByType).forEach(k => delete nodeCountByType[k]);
        nodeCountsFor = subjectId || null;
        nodeCountsReady = false;
        const promise = (async () => {
            if (!subjectId) return;
            try {
                const data = await getJson(`/concept-nodes?subjectId=${encodeURIComponent(subjectId)}&includeArchived=false`);
                if (nodeCountsFor !== subjectId) return;   // a newer subject won the race
                (data?.items || []).forEach(n => {
                    if (n.isArchived || String(n.subjectId) !== String(subjectId)) return;
                    nodeCountByType[n.conceptTypeId] = (nodeCountByType[n.conceptTypeId] || 0) + 1;
                });
                nodeCountsReady = true;
            } catch { /* counts stay unknown → "—" */ }
        })();
        nodeCountsPending = { subjectId, promise };
        return promise;
    };
    const fmt = (template, n) => String(template || '{0}').replace('{0}', String(n));
    const swatch = c => c
        ? `<span class="d-inline-block rounded-circle flex-shrink-0" style="width:10px;height:10px;background:${esc(c)};border:1px solid rgba(0,0,0,.15)"></span>`
        : '';
    const renderPalette = () => {
        const host = document.getElementById('tplTypePalette');
        const empty = document.getElementById('tplTypePaletteEmpty');
        if (!host) return;
        const subjectId = val('tplSubjectId');
        const spine = new Set(spineFromBranches());
        const used = new Set(branches.flatMap(b => b.steps.map(s => String(s.conceptTypeId))));
        // Live types of the subject, plus an archived one still used by a branch (so the palette never hides a step's type).
        const list = subjectId
            ? types.filter(t => String(t.subjectId) === String(subjectId) && (!t.isArchived || used.has(String(t.conceptTypeId))))
            : [];
        if (empty) {
            empty.textContent = subjectId ? (L.TypePaletteEmpty || '') : (L.NodePickerSubjectFirst || '');
            empty.classList.toggle('d-none', list.length > 0);
        }
        host.innerHTML = list.map(t => {
            const id = String(t.conceptTypeId);
            const usage = branches.filter(b => b.steps.some(s => String(s.conceptTypeId) === id)).length;
            const count = nodeCountsReady ? (nodeCountByType[id] || 0) : null;
            const countBadge = `<span class="badge bg-label-secondary">${esc(count == null ? '—' : fmt(L.NodeCount, count))}</span>`;
            const usageBadge = usage > 0 ? `<span class="badge bg-label-info">${esc(fmt(L.BranchUsage, usage))}</span>` : '';
            const spineBadge = spine.has(id) ? '' : `<span class="badge bg-label-warning">${esc(L.NotOnSpine || '')}</span>`;
            return `<li class="diten-checkitem flex-wrap js-palette-type" data-ct="${esc(id)}" draggable="false">
                    <span class="diten-checkitem-grip js-palette-handle flex-shrink-0 opacity-50" aria-hidden="true"><i class="bx bx-grid-vertical"></i></span>
                    ${swatch(t.color)}
                    <span class="diten-checkitem-text text-truncate" title="${esc(t.conceptTypeName)}">${esc(t.conceptTypeName)}</span>
                    <span class="d-flex flex-wrap gap-1 w-100 ps-4">${countBadge}${usageBadge}${spineBadge}</span>
                </li>`;
        }).join('');
    };

    // ─── Branches builder — WP-CT-FE-3 type-lane diagram (mockup v2 Ekran 2, middle panel) ─────────────────────
    // Every branch is visible at once: columns = the spine types (spineFromBranches, first-appearance order), rows =
    // branches, a step = a card in its type's column. The lane body is a CSS grid of 200px columns with a 16px gap and
    // 16px left padding, so a card sits at x = 16 + col × 216 (the mockup rule) and a card that grows (open Min/Max)
    // pushes the lane taller instead of overlapping. Branch-flow edges (consecutive steps of one branch — never a
    // relationship) are an SVG layer under the cards, drawn from the measured card boxes after every render and after
    // a Min/Max editor opens/closes. The model {name, steps:[{conceptTypeId,min,max}]}, spineFromBranches, the hidden
    // tplOrderedConceptTypes and submit are untouched. Drag is FE-4; out-of-spine summary / conformance is FE-5.
    const LANE_COL = 200, LANE_GAP = 16, LANE_PAD = 16;
    let showBackEdges = true;   // Tweaks: backward arcs on/off (view-only preference)
    // Mockup chip: ×N when min == max, else ×min–max (∞ when unbounded).
    const chipLabel = s => {
        const min = s.min == null || s.min === '' ? 1 : Number(s.min);
        const max = s.max == null || s.max === '' ? null : Number(s.max);
        return max === min ? `×${min}` : `×${min}–${max == null ? '∞' : max}`;
    };
    const stepCard = (bi, si, s, col, lastIndex, ro) => {
        const detId = `stepDet-${bi}-${si}`;
        const name = nameOf(s.conceptTypeId);
        const rm = ro ? '' : `<button type="button" class="btn btn-icon btn-sm btn-text-danger rounded-pill js-step-remove flex-shrink-0" style="width:1.5rem;height:1.5rem" data-b="${bi}" data-s="${si}" title="${esc(L.RemoveStep || '')}" aria-label="${esc(L.RemoveStep || '')}"><i class="bx bx-x"></i></button>`;
        const moves = ro ? '' : `
                    <button type="button" class="btn btn-icon btn-sm btn-text-secondary js-step-move ms-auto" style="width:1.5rem;height:1.5rem" data-b="${bi}" data-s="${si}" data-delta="-1" title="${esc(L.MoveEarlier || '')}" aria-label="${esc(L.MoveEarlier || '')}" ${si === 0 ? 'disabled' : ''}><i class="bx bx-chevron-left"></i></button>
                    <button type="button" class="btn btn-icon btn-sm btn-text-secondary js-step-move" style="width:1.5rem;height:1.5rem" data-b="${bi}" data-s="${si}" data-delta="1" title="${esc(L.MoveLater || '')}" aria-label="${esc(L.MoveLater || '')}" ${si === lastIndex ? 'disabled' : ''}><i class="bx bx-chevron-right"></i></button>`;
        // The chip is the Min/Max toggle (click → the inline editor below; js-step-min / js-step-max are unchanged).
        const toggle = ro ? ' disabled' : ` data-bs-toggle="collapse" data-bs-target="#${detId}" aria-expanded="false" aria-controls="${detId}"`;
        const chip = `<button type="button" class="badge bg-label-primary border-0 js-step-chip" data-chip-b="${bi}" data-chip-s="${si}"${toggle} title="${esc(`${L.MinSelection || 'Min'} / ${L.MaxSelection || 'Max'}`)}">${esc(chipLabel(s))}</button>`;
        const minmax = ro ? '' : `<div class="collapse js-step-detail" id="${detId}">
                    <div class="d-flex gap-2 align-items-end mt-2">
                        <div><label class="form-label small mb-0">${esc(L.MinSelection || 'Min')}</label><input type="number" min="0" max="9" step="1" class="form-control form-control-sm js-step-min" data-b="${bi}" data-s="${si}" value="${esc(String(s.min == null || s.min === '' ? 1 : s.min))}"></div>
                        <div><label class="form-label small mb-0">${esc(L.MaxSelection || 'Max')}</label><input type="number" min="1" max="9" step="1" class="form-control form-control-sm js-step-max" data-b="${bi}" data-s="${si}" value="${s.max == null || s.max === '' ? '' : esc(String(s.max))}"></div>
                    </div>
                </div>`;
        return `<div class="card border shadow-none js-step-card" data-b="${bi}" data-s="${si}" data-ct="${esc(String(s.conceptTypeId))}" style="grid-column:${col + 1};grid-row:1;z-index:1;align-self:start">
                <div class="card-body p-2">
                    <div class="d-flex align-items-center gap-1 js-step-head" style="height:1.5rem">
                        <span class="badge bg-label-secondary rounded-pill flex-shrink-0">${si + 1}</span>
                        <span class="text-truncate small fw-medium text-heading flex-grow-1" title="${esc(name)}">${esc(name)}</span>
                        ${rm}
                    </div>
                    <div class="d-flex align-items-center gap-1 mt-1">${chip}${moves}</div>
                    ${minmax}
                </div>
            </div>`;
    };
    // The add-step row (bottom of every lane): Concept Type + Min/Max + Add step — the unchanged compose model.
    const addStepRow = (bi, opts) => `<div class="card border shadow-none">
                <div class="card-body p-2 d-flex gap-2 align-items-end flex-wrap">
                    <div class="flex-grow-1">
                        <label class="form-label small mb-0">${esc(L.AddStep || '')}</label>
                        <select class="form-select form-select-sm js-branch-type-picker" data-b="${bi}" data-placeholder="${esc(L.ConceptType || L.SelectOption || '')}">
                            <option value="">${esc(L.SelectOption || '')}</option>
                            ${opts.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('')}
                        </select>
                    </div>
                    <div><label class="form-label small mb-0">${esc(L.MinSelection || 'Min')}</label><input type="number" min="0" max="9" step="1" value="1" class="form-control form-control-sm js-compose-min" data-b="${bi}" aria-label="${esc(L.MinSelection || 'Min')}" style="width:5.5rem"></div>
                    <div><label class="form-label small mb-0">${esc(L.MaxSelection || 'Max')}</label><input type="number" min="1" max="9" step="1" class="form-control form-control-sm js-compose-max" data-b="${bi}" aria-label="${esc(L.MaxSelection || 'Max')}" style="width:5.5rem"></div>
                    <button type="button" class="btn btn-label-primary btn-sm js-branch-add-step" data-b="${bi}"><i class="bx bx-plus me-1"></i>${esc(L.AddToSequence || '')}</button>
                </div>
            </div>`;
    const gridStyle = cols => `display:grid;grid-template-columns:repeat(${cols},${LANE_COL}px);column-gap:${LANE_GAP}px;padding-left:${LANE_PAD}px;padding-right:${LANE_PAD}px;width:${LANE_PAD * 2 + cols * LANE_COL + (cols - 1) * LANE_GAP}px`;
    const spineStatus = spine => {
        if (templateReadOnly) return `<span class="badge bg-label-secondary"><i class="bx bx-lock-alt me-1"></i>${esc(L.SpineStatusFrozen || '')}</span>`;
        return spine.length >= 2
            ? `<span class="badge bg-label-success"><i class="bx bx-check me-1"></i>${esc(L.SpineStatusValid || '')}</span>`
            : `<span class="badge bg-label-warning"><i class="bx bx-error me-1"></i>${esc(L.SpineStatusNeedsTwo || '')}</span>`;
    };

    // SVG edges for one lane, from measured card boxes (offsets are relative to the positioned lane body).
    // forward to the next column with no card of this branch in between → straight arrow on the card-head line;
    // forward over a card of this branch → upper arc through the top padding; backward → lower arc under the lane.
    const drawLaneEdges = laneBody => {
        const svg = laneBody.querySelector('.js-lane-edges');
        if (!svg) return;
        const bi = Number(laneBody.dataset.b);
        const b = branches[bi];
        const spineIdx = new Map(spineFromBranches().map((id, i) => [id, i]));
        const cards = Array.from(laneBody.querySelectorAll('.js-step-card'));
        const box = si => {
            const el = cards.find(c => Number(c.dataset.s) === si);
            if (!el) return null;
            const head = el.querySelector('.js-step-head');
            return {
                l: el.offsetLeft, r: el.offsetLeft + el.offsetWidth, t: el.offsetTop, b: el.offsetTop + el.offsetHeight,
                cx: el.offsetLeft + el.offsetWidth / 2,
                hy: el.offsetTop + (head ? head.offsetTop + head.offsetHeight / 2 : 20)
            };
        };
        const bottom = cards.reduce((m, c) => Math.max(m, c.offsetTop + c.offsetHeight), 0);
        const paths = [];
        let hasBack = false;
        (b?.steps || []).forEach((s, si) => {
            const next = b.steps[si + 1];
            if (!next) return;
            const a = spineIdx.get(String(s.conceptTypeId));
            const c = spineIdx.get(String(next.conceptTypeId));
            const p = box(si), q = box(si + 1);
            if (a == null || c == null || !p || !q) return;
            if (c > a) {
                const blocked = b.steps.some(o => { const k = spineIdx.get(String(o.conceptTypeId)); return k > a && k < c; });
                paths.push(blocked
                    ? `<path d="M${p.cx},${p.t} C${p.cx},${p.t - 26} ${q.cx},${q.t - 26} ${q.cx},${q.t}" class="ct-edge" marker-end="url(#ctArrow-${bi})"/>`
                    : `<path d="M${p.r},${p.hy} L${q.l},${q.hy}" class="ct-edge" marker-end="url(#ctArrow-${bi})"/>`);
            } else if (showBackEdges) {
                hasBack = true;
                const y = bottom + 22;
                paths.push(`<path d="M${p.cx},${p.b} C${p.cx},${y} ${q.cx},${y} ${q.cx},${q.b}" class="ct-edge ct-edge-back" stroke-dasharray="5 4" marker-end="url(#ctArrowBack-${bi})"/>`);
            }
        });
        laneBody.style.paddingBottom = hasBack ? '2.25rem' : '0.5rem';
        svg.setAttribute('width', String(laneBody.scrollWidth));
        svg.setAttribute('height', String(laneBody.offsetHeight));
        // Theme-safe: strokes/fills are currentColor or Bootstrap tokens, readable in both themes.
        svg.innerHTML = `<defs>
                <marker id="ctArrow-${bi}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="currentColor"/></marker>
                <marker id="ctArrowBack-${bi}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10 z" fill="var(--bs-warning)"/></marker>
            </defs>
            <style>.ct-edge{fill:none;stroke:currentColor;stroke-width:1.5}.ct-edge-back{stroke:var(--bs-warning)}</style>
            ${paths.join('')}`;
    };
    const drawAllEdges = () => document.querySelectorAll('#tplBranches .js-lane-body').forEach(drawLaneEdges);
    // The lane name row and the compose row stay pinned to the visible width while the grid scrolls horizontally.
    const pinLaneChrome = () => {
        const scroller = document.querySelector('#tplBranches .js-lane-scroll');
        if (!scroller) return;
        const w = `${scroller.clientWidth}px`;
        scroller.querySelectorAll('.js-lane-sticky').forEach(el => { el.style.width = w; });
    };

    const renderBranches = () => {
        const host = document.getElementById('tplBranches');
        const empty = document.getElementById('tplBranchesEmpty');
        if (!host) return;
        const subjectId = val('tplSubjectId');
        const ro = templateReadOnly;
        const total = branches.length;
        const spine = spineFromBranches();
        empty?.classList.toggle('d-none', total > 0);
        setVal('tplOrderedConceptTypes', spine.join(','));
        renderPalette();   // WP-CT-FE-2: branch usage / not-on-spine follow every structural change
        if (total === 0) { host.innerHTML = ''; return; }

        const cols = Math.max(spine.length, 1);
        const colOf = new Map(spine.map((id, i) => [id, i]));
        const lock = ro ? '<i class="bx bx-lock-alt text-muted me-1"></i>' : '';
        const header = spine.length
            ? spine.map((id, i) => `<div class="small fw-semibold text-heading text-truncate border-bottom pb-1" style="grid-column:${i + 1}" title="${esc(nameOf(id))}">${lock}${esc(nameOf(id))}</div>`).join('')
            : `<div class="small text-muted" style="grid-column:1">${esc(L.SequenceMinTwo || '')}</div>`;
        const lanes = branches.map((b, bi) => {
            const lastIndex = b.steps.length - 1;
            const opts = typeOptionsFor(subjectId).filter(o => !b.steps.some(s => String(s.conceptTypeId) === String(o.value)));
            const cards = b.steps.map((s, si) => stepCard(bi, si, s, colOf.get(String(s.conceptTypeId)) ?? 0, lastIndex, ro)).join('');
            const emptyLane = `<div class="small text-muted align-self-center" style="grid-column:1 / -1">${esc(L.BranchStepsEmpty || '')}</div>`;
            return `<div class="border-top pt-2 pb-2 js-lane" data-b="${bi}">
                    <div class="js-lane-sticky d-flex align-items-center gap-2 mb-1 px-2" style="position:sticky;left:0">
                        <span class="badge bg-primary rounded-pill flex-shrink-0">${bi + 1}</span>
                        <input type="text" class="form-control form-control-sm js-branch-name flex-grow-1" data-b="${bi}" value="${esc(b.name || '')}" placeholder="${esc(L.BranchNamePlaceholder || '')}" ${ro ? 'disabled' : ''}>
                        <span class="badge bg-label-secondary flex-shrink-0">${b.steps.length} ${esc(L.Steps || '')}</span>
                        <button type="button" class="btn btn-icon btn-text-danger js-branch-remove flex-shrink-0" data-b="${bi}" title="${esc(L.DeleteBranch || '')}" aria-label="${esc(L.DeleteBranch || '')}" ${ro || total <= 1 ? 'disabled' : ''}><i class="icon-base bx bx-trash icon-sm"></i></button>
                    </div>
                    <div class="js-lane-body position-relative" data-b="${bi}" style="${gridStyle(cols)};padding-top:1.75rem;min-height:4.5rem">
                        <svg class="js-lane-edges position-absolute top-0 start-0" style="pointer-events:none;z-index:0;color:var(--bs-secondary-color)" aria-hidden="true"></svg>
                        ${cards || emptyLane}
                    </div>
                    ${ro ? '' : `<div class="js-lane-sticky px-2 mt-1" style="position:sticky;left:0">${addStepRow(bi, opts)}</div>`}
                </div>`;
        }).join('');

        host.innerHTML = `
            <div class="d-flex align-items-center justify-content-between gap-2 flex-wrap mb-2">
                <div class="d-flex align-items-center gap-2"><span class="small text-muted">${esc(L.Spine || '')}</span>${spineStatus(spine)}</div>
                <div class="form-check form-switch mb-0">
                    <input class="form-check-input js-back-edges-toggle" type="checkbox" id="tplBackEdgesToggle" ${showBackEdges ? 'checked' : ''}>
                    <label class="form-check-label small" for="tplBackEdgesToggle">${esc(L.BackEdgesToggle || '')}</label>
                </div>
            </div>
            <div class="js-lane-scroll border rounded" style="overflow-x:auto">
                <div style="${gridStyle(cols)};padding-top:.5rem;padding-bottom:.25rem">${header}</div>
                ${lanes}
            </div>`;
        pinLaneChrome();
        drawAllEdges();
    };

    // ─── submit ─────────────────────────────────────────────────────────────────
    const submit = async () => {
        const id = val('templateFormId');
        const error = document.getElementById('tplSequenceError');
        const spine = spineFromBranches();
        if (spine.length < 2) {
            if (error) { error.textContent = L.SequenceMinTwo || ''; error.classList.remove('d-none'); }
            throw Object.assign(new Error(L.SequenceMinTwo || ''), { handled: true });
        }
        error?.classList.add('d-none');

        const branchPayload = branches.filter(b => b.steps.length > 0).map((b, i) => ({
            branchCode: `BR${i + 1}`,
            branchName: norm(b.name) || null,
            sortOrder: i,
            steps: b.steps.map(s => ({
                conceptTypeId: String(s.conceptTypeId),
                minSelection: Number.isFinite(Number(s.min)) ? Number(s.min) : 1,
                maxSelection: (s.max === '' || s.max == null) ? null : Number(s.max)
            }))
        }));

        // SCMM-10-MOD-C: template-level Moderator (single ValueCode, blank = unspecified) + ForWhom (audience ids).
        const moderator = val('tplModeratorRoleType');
        const forWhom = $ ? ($('#tplForWhom').val() || []) : [];

        const payload = {
            chainName: val('tplChainName'),
            orderedConceptTypes: spine,
            branches: branchPayload,
            moderatorRoleType: moderator || null,
            forWhomAudienceProfileIds: forWhom,
            effectiveFrom: fromDateInput(val('tplEffectiveFrom')),
            description: val('tplDescription') || null,
            status: val('tplStatus') || null,
            chainVersion: val('tplChainVersion') || null,
            effectiveTo: fromDateInput(val('tplEffectiveTo'))
        };
        if (!id) { payload.subjectId = val('tplSubjectId'); payload.chainCode = val('tplChainCode'); }

        await envelope(await fetch(id ? `${base}/concept-chain-templates/${id}` : `${base}/concept-chain-templates`, {
            method: id ? 'PUT' : 'POST', credentials: 'same-origin', headers: jsonHeaders, body: JSON.stringify(payload)
        }));
        window.showToast?.(id ? (L.RecordUpdated || '') : (L.RecordCreated || ''), 'success');
        window.location.href = '/CRM/KnowledgeConcepts';
    };

    const setIdentityDisabled = disabled => {
        ['tplModeratorRoleType', 'tplForWhom'].forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.disabled = !!disabled;
            if ($ && $(el).hasClass('select2-hidden-accessible')) $(el).trigger('change.select2');
        });
    };

    const startNewVersion = () => {
        templateReadOnly = false;
        setVal('templateFormId', '');
        setVal('tplStatus', 'draft'); if ($) $('#tplStatus').trigger('change');
        setVal('tplEffectiveFrom', todayInput());
        setVal('tplEffectiveTo', '');
        setVal('tplChainVersion', bumpVersion(val('tplChainVersion')));
        document.getElementById('tplChainCode').readOnly = false;
        document.getElementById('tplChainCodeHint')?.classList.remove('d-none');
        document.getElementById('conceptTemplateFrozenNote')?.classList.add('d-none');
        document.getElementById('btnTplNewVersion')?.classList.add('d-none');
        document.getElementById('btnSaveConceptTemplate')?.classList.remove('d-none');
        setIdentityDisabled(false);
        document.getElementById('btnTplAddBranch')?.removeAttribute('disabled');
        renderBranches();
    };

    // ─── load ───────────────────────────────────────────────────────────────────
    const loadModeratorOptions = async () => {
        try {
            const data = await getJson(`/reference-data/${encodeURIComponent(MODERATOR_SET_CODE)}/values`);
            moderatorOptions = (data?.items || []).map(v => {
                const code = norm(v.code || v.valueCode || v.value);
                return {
                    value: code,
                    text: norm(v.label || v.displayName || v.text) || code,
                    isDeprecated: v.isDeprecated === true || v.isActive === false,
                    sortOrder: Number(v.sortOrder || 0)
                };
            }).filter(o => o.value).sort((a, b) => a.sortOrder - b.sortOrder || a.text.localeCompare(b.text));
        } catch { moderatorOptions = []; }
    };
    const loadRefs = async () => {
        // Subjects come back with archived ones so a disabled edit field still resolves a friendly label; the create
        // dropdown offers only live subjects. Audience profiles feed the ForWhom picker; moderator roles the Moderator.
        const [subs, tps, auds, , contract] = await Promise.all([
            getJson('/subjects?includeArchived=true').catch(() => ({ items: [] })),
            getJson('/concept-types?includeArchived=true').catch(() => ({ items: [] })),
            getJson('/audience-profiles?includeArchived=false').catch(() => ({ items: [] })),
            loadModeratorOptions(),
            getJson('/contract').catch(() => null)
        ]);
        types = (tps?.items || []).map(t => ({ conceptTypeId: t.conceptTypeId, subjectId: t.subjectId, conceptTypeCode: t.conceptTypeCode, conceptTypeName: t.conceptTypeName, isArchived: t.isArchived, color: t.color }));
        types.forEach(t => {
            typeNameById[t.conceptTypeId] = `${t.conceptTypeCode} — ${t.conceptTypeName}`;
            typeCodeById[t.conceptTypeId] = t.conceptTypeCode;
            typeNameOnlyById[t.conceptTypeId] = t.conceptTypeName;
        });
        audienceOptions = (auds?.items || []).filter(a => !a.isArchived).map(a => ({ value: a.audienceProfileId, text: `${a.profileCode} — ${a.profileName}` }));
        (subs?.items || []).forEach(s => { subjectLabelById[s.subjectId] = `${s.subjectCode} — ${s.subjectName}`; });
        const subjectOptions = (subs?.items || []).filter(s => !s.isArchived).map(s => ({ value: s.subjectId, text: subjectLabelById[s.subjectId] }));
        // "archived" is a lifecycle action, never a status the editor sets (a save carrying it is a backend 400).
        const statuses = (contract?.vocabularies?.chainStatuses || ['draft', 'review', 'approved', 'published', 'inactive', 'archived'])
            .filter(v => v !== 'archived').map(v => ({ value: v, text: v }));
        return { subjectOptions, statuses };
    };

    const suggestCode = () => `CHN-${new Date().getUTCFullYear()}-${Math.random().toString(36).slice(2, 8).toUpperCase()}`;

    const init = async () => {
        if (window.flatpickr) document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
        const editId = document.getElementById('templateEditHeader')?.getAttribute('data-template-id');
        let refs;
        try { refs = await loadRefs(); }
        catch (e) { showAlert(L.ErrorState); return; }

        const row = editId ? await getJson(`/concept-chain-templates/${editId}`).catch(() => null) : null;
        if (editId && !row) { showAlert(L.ErrorState); document.getElementById('btnSaveConceptTemplate')?.setAttribute('disabled', 'disabled'); return; }
        fillSelect('tplSubjectId', refs.subjectOptions, true, row?.subjectId, subjectLabelById[row?.subjectId]);
        fillSelect('tplStatus', refs.statuses, false, row?.status, row?.status);
        // SCMM-10-MOD-C: Moderator (single, blank = unspecified) + ForWhom (audience-profile multi).
        fillSelect('tplModeratorRoleType', moderatorOptions, true, row?.moderatorRoleType, moderatorLabel(row?.moderatorRoleType));
        fillForWhom(row?.forWhomAudienceProfileIds);
        initSelect2('#tplSubjectId,#tplStatus,#tplModeratorRoleType,#tplForWhom');

        setVal('templateFormId', row?.conceptChainTemplateId || '');
        setVal('tplSubjectId', row?.subjectId || ''); if ($) $('#tplSubjectId').trigger('change');
        setVal('tplChainCode', row ? row.chainCode : suggestCode());
        setVal('tplChainName', row?.chainName || '');
        setVal('tplDescription', row?.description || '');
        setVal('tplChainVersion', row?.chainVersion || '');
        setVal('tplModeratorRoleType', row?.moderatorRoleType || ''); if ($) $('#tplModeratorRoleType').trigger('change.select2');
        if ($) $('#tplForWhom').trigger('change.select2');
        setVal('tplStatus', row?.status || 'draft'); if ($) $('#tplStatus').trigger('change');
        setVal('tplEffectiveFrom', row ? toDateInput(row.effectiveFrom) : todayInput());
        setVal('tplEffectiveTo', toDateInput(row?.effectiveTo));

        branches = (row?.branches || []).map(b => ({
            name: b.branchName || '',
            steps: (b.steps || []).map(s => ({
                conceptTypeId: s.conceptTypeId,
                min: s.minSelection ?? 1,
                max: s.maxSelection ?? null
            }))
        }));
        if (!row && branches.length === 0) branches = [{ name: '', steps: [] }];

        const frozen = norm(row?.status) === 'published';
        templateReadOnly = frozen;
        document.getElementById('conceptTemplateFrozenNote')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnTplNewVersion')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnSaveConceptTemplate')?.classList.toggle('d-none', frozen);
        document.getElementById('btnTplAddBranch').disabled = frozen;
        // SCMM-10-MOD-C (D-f): a published template's Moderator/ForWhom freeze with the rest (edit → new version).
        setIdentityDisabled(frozen);

        // Subject + code are stable across versions (update contract carries neither); the code hint hides on edit.
        document.getElementById('tplSubjectId').disabled = !!row;
        if ($) $('#tplSubjectId').trigger('change.select2');
        document.getElementById('tplChainCode').readOnly = !!row;
        document.getElementById('tplChainCodeHint')?.classList.toggle('d-none', !!row);
        const crumb = document.getElementById('tplEditCrumb');
        if (crumb && row) crumb.textContent = row.chainCode || (L.EditTemplate || '');

        renderBranches();
        // WP-CT-FE-2: the palette renders at once (counts "—"), then again when the subject's node counts arrive.
        await loadNodeCounts(val('tplSubjectId'));
        renderPalette();
    };

    // ─── event wiring ─────────────────────────────────────────────────────────────
    document.addEventListener('DOMContentLoaded', () => {
        void init();

        // Subject change (create) rebuilds the builder — types are subject-scoped. Moderator/ForWhom are template-level
        // and are deliberately left untouched.
        const subj = document.getElementById('tplSubjectId');
        const onSubjectChange = () => { if (templateReadOnly) return; branches = [{ name: '', steps: [] }]; renderBranches(); };
        subj?.addEventListener('change', () => { if (!subj.disabled) onSubjectChange(); });
        if ($) $(subj).on('change', () => { if (!subj.disabled) onSubjectChange(); });
        // WP-CT-FE-2: a (create-mode) subject change re-reads that subject's node counts for the palette.
        const onSubjectPalette = () => { if (subj.disabled) return; void loadNodeCounts(val('tplSubjectId')).then(renderPalette); };
        subj?.addEventListener('change', onSubjectPalette);
        if ($) $(subj).on('change', onSubjectPalette);

        // Structural builder actions.
        document.addEventListener('click', event => {
            if (event.target.closest('#btnTplAddBranch')) { event.preventDefault(); if (templateReadOnly) return; branches.push({ name: '', steps: [] }); renderBranches(); return; }
            if (event.target.closest('#btnTplNewVersion')) { event.preventDefault(); startNewVersion(); return; }
            const br = event.target.closest('.js-branch-remove');
            // WP-CT-FE-3: "Delete branch" keeps at least one branch.
            if (br) { event.preventDefault(); if (templateReadOnly || branches.length <= 1) return; branches.splice(Number(br.dataset.b), 1); renderBranches(); return; }
            const addStep = event.target.closest('.js-branch-add-step');
            if (addStep) {
                event.preventDefault(); if (templateReadOnly) return;
                const bi = Number(addStep.dataset.b);
                const picker = document.querySelector(`.js-branch-type-picker[data-b="${bi}"]`);
                const v = norm(picker?.value);
                if (!v || branches[bi].steps.some(s => String(s.conceptTypeId) === v)) return;
                const minEl = document.querySelector(`.js-compose-min[data-b="${bi}"]`);
                const maxEl = document.querySelector(`.js-compose-max[data-b="${bi}"]`);
                const minRaw = norm(minEl?.value);
                const maxRaw = norm(maxEl?.value);
                branches[bi].steps.push({
                    conceptTypeId: v,
                    min: minRaw === '' ? 1 : Math.max(0, Number(minRaw)),
                    max: maxRaw === '' ? null : Math.max(1, Number(maxRaw))
                });
                renderBranches();
                return;
            }
            const mv = event.target.closest('.js-step-move');
            if (mv) {
                event.preventDefault(); if (templateReadOnly) return;
                const bi = Number(mv.dataset.b), si = Number(mv.dataset.s), target = si + Number(mv.dataset.delta);
                const steps = branches[bi].steps;
                if (target < 0 || target >= steps.length) return;
                const [it] = steps.splice(si, 1); steps.splice(target, 0, it); renderBranches();
                return;
            }
            const rm = event.target.closest('.js-step-remove');
            if (rm) { event.preventDefault(); if (templateReadOnly) return; branches[Number(rm.dataset.b)].steps.splice(Number(rm.dataset.s), 1); renderBranches(); return; }
        });

        // branch-name + a step row's Details Min/Max update the model WITHOUT re-render (keeps the collapse open and the
        // input focused); a Min/Max change also refreshes that row's cardinality chip in place. The add-step compose
        // Min/Max are read at Add time, so they need no per-keystroke sync.
        document.getElementById('tplBranches')?.addEventListener('input', event => {
            const el = event.target;
            if (!el?.dataset || el.dataset.b == null) return;
            const bi = Number(el.dataset.b);
            if (!branches[bi]) return;
            if (el.classList.contains('js-branch-name')) { branches[bi].name = el.value; return; }
            if (el.dataset.s == null) return;
            const s = branches[bi].steps[Number(el.dataset.s)];
            if (!s) return;
            if (el.classList.contains('js-step-min')) s.min = el.value === '' ? 1 : Math.max(0, Number(el.value));
            else if (el.classList.contains('js-step-max')) s.max = el.value === '' ? null : Math.max(1, Number(el.value));
            else return;
            const chip = document.querySelector(`.js-step-chip[data-chip-b="${bi}"][data-chip-s="${el.dataset.s}"]`);
            if (chip) chip.textContent = chipLabel(s);
        });

        // WP-CT-FE-3: the lane diagram re-measures its edges when a card's Min/Max editor opens or closes (the card
        // grows), the back-arc Tweak toggles the backward edges, and a resize re-pins the lane chrome to the viewport.
        const lanesHost = document.getElementById('tplBranches');
        lanesHost?.addEventListener('shown.bs.collapse', drawAllEdges);
        lanesHost?.addEventListener('hidden.bs.collapse', drawAllEdges);
        lanesHost?.addEventListener('change', event => {
            if (!event.target.classList?.contains('js-back-edges-toggle')) return;
            showBackEdges = !!event.target.checked;
            drawAllEdges();
        });
        window.addEventListener('resize', () => { pinLaneChrome(); drawAllEdges(); });

        // Save (JS submit; the button is a form submit but we own the flow).
        const formEl = document.getElementById('conceptTemplateForm');
        formEl?.addEventListener('submit', async event => {
            event.preventDefault();
            if (!formEl.checkValidity()) { formEl.reportValidity(); return; }
            try { await submit(); }
            catch (err) { if (!err?.handled) showAlert(err.message || L.ErrorState); }
        });
    });
})(window, document);
