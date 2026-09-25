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
 * branches, SVG branch-flow edges); the per-branch pager and the SortableJS step reorder are gone. WP-CT-FE-4 adds
 * custom HTML5 drag and drop on that diagram (palette type → branch, step move / re-order / cross-branch, type-repeat
 * guard, draft only). The model, the compose add-row, Min/Max editing and submit are unchanged.
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
    // use it, and a "not on spine" badge when no branch uses it (the spine is derived from the branches). On a draft a
    // live type row is draggable onto a branch lane (WP-CT-FE-4); an archived type is shown but not draggable (the
    // compose picker does not offer it either).
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
            const canDrag = !templateReadOnly && !t.isArchived;
            return `<li class="diten-checkitem flex-wrap js-palette-type" data-ct="${esc(id)}" draggable="${canDrag}"${canDrag ? ' style="cursor:grab"' : ''}>
                    <span class="diten-checkitem-grip js-palette-handle flex-shrink-0${canDrag ? '' : ' opacity-50'}" aria-hidden="true"><i class="bx bx-grid-vertical"></i></span>
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
        return `<div class="card border shadow-none js-step-card" data-b="${bi}" data-s="${si}" data-ct="${esc(String(s.conceptTypeId))}" draggable="${!ro}" style="grid-column:${col + 1};grid-row:1;z-index:1;align-self:start${ro ? '' : ';cursor:grab'}">
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
    // WP-CT-FE-3-REFINE: DAL A/B/C… for the lane header (index 26+ falls back to the 1-based number).
    const branchLetter = bi => (bi < 26 ? String.fromCharCode(65 + bi) : String(bi + 1));
    // The compact "+ add type" row (compose) is collapsed by default; which lanes have it open survives re-renders, so
    // adding several types in a row does not close it after every add.
    const openCompose = new Set();
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
        if (total === 0) { host.innerHTML = ''; scheduleDiagnostics(); renderSidePanel(); return; }

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
            // WP-CT-FE-3-REFINE: an empty draft lane is a drag dropzone (the FE-4 lane drop target, made visible); a
            // read-only one keeps the plain "no steps" text.
            const emptyLane = ro
                ? `<div class="small text-muted align-self-center" style="grid-column:1 / -1">${esc(L.BranchStepsEmpty || '')}</div>`
                : `<div class="js-lane-dropzone rounded text-muted small d-flex align-items-center justify-content-center gap-2 align-self-center" style="grid-column:1 / -1;min-height:3.25rem;border:2px dashed var(--bs-border-color)">
                        <i class="bx bx-move"></i>${esc(L.DragTypeHerePlaceholder || '')}
                   </div>`;
            const composeId = `tplCompose-${bi}`;
            const composeOpen = openCompose.has(bi);
            // WP-CT-FE-3-REFINE lane header (mockup "DAL A · Ana akış · 2 adım"): a fixed DAL-X label, the branch name as a
            // light inline field (same js-branch-name input → same model handler), the step count and delete.
            return `<div class="border-top pt-2 pb-2 js-lane" data-b="${bi}">
                    <div class="js-lane-sticky d-flex align-items-center gap-2 mb-1 px-2" style="position:sticky;left:0">
                        <span class="badge bg-label-primary text-uppercase fw-semibold flex-shrink-0">${esc(L.BranchLabelPrefix || 'Branch')} ${esc(branchLetter(bi))}</span>
                        <input type="text" class="form-control form-control-sm border-0 bg-transparent shadow-none px-1 fw-medium text-heading js-branch-name flex-grow-1" data-b="${bi}" value="${esc(b.name || '')}" placeholder="${esc(L.BranchNamePlaceholder || '')}" aria-label="${esc(L.BranchNamePlaceholder || '')}" ${ro ? 'disabled' : ''}>
                        <span class="small text-muted text-nowrap flex-shrink-0">${esc(fmtN(L.StepCountLabel || '{0}', b.steps.length))}</span>
                        ${ro ? '' : `<button type="button" class="btn btn-sm btn-text-primary text-nowrap flex-shrink-0 js-compose-toggle" data-b="${bi}" data-bs-toggle="collapse" data-bs-target="#${composeId}" aria-controls="${composeId}" aria-expanded="${composeOpen}"><i class="bx bx-plus me-1"></i>${esc(L.AddTypeCompact || '')}</button>`}
                        <button type="button" class="btn btn-icon btn-sm btn-text-danger js-branch-remove flex-shrink-0" data-b="${bi}" title="${esc(L.DeleteBranch || '')}" aria-label="${esc(L.DeleteBranch || '')}" ${ro || total <= 1 ? 'disabled' : ''}><i class="icon-base bx bx-trash icon-sm"></i></button>
                    </div>
                    <div class="js-lane-body position-relative" data-b="${bi}" style="${gridStyle(cols)};padding-top:1.75rem;min-height:4.5rem">
                        <svg class="js-lane-edges position-absolute top-0 start-0" style="pointer-events:none;z-index:0;color:var(--bs-secondary-color)" aria-hidden="true"></svg>
                        ${cards || emptyLane}
                    </div>
                    ${ro ? '' : `<div class="js-lane-sticky px-2" style="position:sticky;left:0"><div class="collapse js-compose${composeOpen ? ' show' : ''}" id="${composeId}" data-b="${bi}"><div class="pt-1">${addStepRow(bi, opts)}</div></div></div>`}
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
        scheduleDiagnostics();   // WP-CT-FE-5: re-requests only when subject|spine changed
        renderSidePanel();
    };

    // ─── WP-CT-FE-4: drag and drop on the lane diagram (custom HTML5 DnD) ──────────────────────────────────────────
    // SortableJS does not fit: a lane's DOM order is the steps order but a card's position is its type column, so an
    // auto-sorted list would lie. The payload lives in module state (dataTransfer cannot be read during dragover):
    //   palette row → { kind:'type', typeId }          step card → { kind:'step', bid, sid }
    // Dropping on a card inserts BEFORE that step (its steps index, never its grid x); anywhere else in the lane appends.
    // Draft only — a read-only template renders nothing draggable and every handler bails. The ←/→ buttons, compose,
    // remove and branch ops stay as the non-drag path.
    let dragPayload = null;
    const clearDropTargets = () => document.querySelectorAll('#tplBranches .is-drop-target').forEach(el => el.classList.remove('is-drop-target'));
    const ensureDropStyle = () => {
        if (document.getElementById('tplDropTargetStyle')) return;
        const st = document.createElement('style');
        st.id = 'tplDropTargetStyle';
        // Theme token (the primary purple), readable in light and dark.
        st.textContent = '#tplBranches .js-lane-body.is-drop-target{outline:2px dashed var(--bs-primary);outline-offset:-2px;'
            + 'background:rgba(var(--bs-primary-rgb),.06);border-radius:.375rem}';
        document.head.appendChild(st);
    };
    // Returns true when the model changed. The type-repeat guard mirrors the backend (a type appears once per branch):
    // a drop that would repeat a type does nothing and warns — except a step being re-ordered inside its own branch.
    const moveTo = (targetBranch, insertIndex, payload) => {
        if (templateReadOnly || !payload) return false;
        const target = branches[targetBranch];
        if (!target) return false;
        const source = payload.kind === 'step' ? branches[payload.bid] : null;
        const moving = source ? source.steps[payload.sid] : null;
        const typeId = payload.kind === 'type' ? String(payload.typeId || '') : String(moving?.conceptTypeId || '');
        if (!typeId || (payload.kind === 'step' && !moving)) return false;
        const existing = target.steps.findIndex(s => String(s.conceptTypeId) === typeId);
        const sameStep = payload.kind === 'step' && payload.bid === targetBranch && existing === payload.sid;
        if (existing >= 0 && !sameStep) {
            window.showToast?.(L.TypeAlreadyInBranch || '', 'warning');
            return false;
        }
        const clamp = (i, max) => Math.min(Math.max(0, i), max);
        if (payload.kind === 'type') {
            target.steps.splice(clamp(insertIndex, target.steps.length), 0, { conceptTypeId: typeId, min: 1, max: 1 });
        } else {
            source.steps.splice(payload.sid, 1);
            // Removing the step first shifts every later index of the same branch down by one.
            const at = payload.bid === targetBranch && payload.sid < insertIndex ? insertIndex - 1 : insertIndex;
            target.steps.splice(clamp(at, target.steps.length), 0, moving);
        }
        renderBranches();
        return true;
    };
    const dropPoint = event => {
        const lane = event.target.closest?.('#tplBranches .js-lane');
        if (!lane) return null;
        const bi = Number(lane.dataset.b);
        const card = event.target.closest('.js-step-card');
        const insertIndex = card && Number(card.dataset.b) === bi ? Number(card.dataset.s) : (branches[bi]?.steps.length ?? 0);
        return { lane, bi, insertIndex };
    };

    // ─── WP-CT-FE-5: right panel — Connections / Non-conforming / Versions ────────────────────────────────────────
    // Non-conforming consumes the WP-CT-BE-A diagnostics (a READ over the live, possibly unsaved spine — the backend
    // classifier is the single source; nothing is re-derived here) and the WP-CT-BE-B resolutions write ("Yok say").
    // Conformance is never enforced (D8): a resolution only records the decision, no relationship is changed or deleted.
    // Actions are draft-only. Outputs (knowledge paths / journeys / visits) have no reverse reference to a chain
    // template today, so they show "—" — never an invented count.
    let currentRow = null;           // the loaded template (null on create)
    let ignoredIds = new Set();      // IgnoredNonConformingRelationshipIds (saved: persisted per click; new: Create payload)
    let diag = null;                 // last diagnostics payload { items, total, conformingCount, orderCount, outCount }
    let diagState = 'idle';          // idle | loading | ready | error
    let diagKey = '';                // subject|spine the last request was for — re-request only when it changes
    let diagTimer = null;
    let diagSeq = 0;
    let versions = null;             // same subject + chainCode templates, or null when not loaded
    const dash = '<span class="text-muted">—</span>';
    const fmtN = (template, ...args) => String(template || '').replace(/\{(\d)\}/g, (_, i) => String(args[Number(i)] ?? ''));
    const typeNameOrDash = id => typeNameOnlyById[id] ? esc(typeNameOnlyById[id]) : '—';
    const tabButton = () => document.getElementById('tplTabNonConformingBtn');
    const nonConforming = () => (diag?.items || []).filter(i => i.result !== 'conforming');
    const unresolved = () => nonConforming().filter(i => !ignoredIds.has(String(i.conceptRelationshipId)));

    const scheduleDiagnostics = force => {
        const subjectId = val('tplSubjectId');
        const key = `${subjectId}|${spineFromBranches().join(',')}`;
        if (!force && key === diagKey) return;
        diagKey = key;
        clearTimeout(diagTimer);
        if (!subjectId) { diag = null; diagState = 'idle'; renderSidePanel(); return; }
        diagState = 'loading';
        renderSidePanel();
        diagTimer = setTimeout(async () => {
            const seq = ++diagSeq;
            try {
                const data = await envelope(await fetch(`${base}/concept-chain-templates/conformance-diagnostics`, {
                    method: 'POST', credentials: 'same-origin', headers: jsonHeaders,
                    body: JSON.stringify({ subjectId, orderedConceptTypeIds: spineFromBranches() })
                }));
                if (seq !== diagSeq) return;   // a newer spine won
                diag = data; diagState = 'ready';
            } catch {
                if (seq !== diagSeq) return;
                diag = null; diagState = 'error';
            }
            renderSidePanel();
        }, 350);
    };

    // "Yok say" / "Geri al": a saved template writes the whole set through the resolve endpoint at once (published →
    // 409, surfaced as the toast); a new template keeps it in memory and the Create payload carries it.
    const setIgnored = async (relationshipId, on) => {
        if (templateReadOnly || !relationshipId) return;
        const next = new Set(ignoredIds);
        if (on) next.add(relationshipId); else next.delete(relationshipId);
        const id = val('templateFormId');
        if (id) {
            try {
                await envelope(await fetch(`${base}/concept-chain-templates/${encodeURIComponent(id)}/conformance-resolutions`, {
                    method: 'PUT', credentials: 'same-origin', headers: jsonHeaders,
                    body: JSON.stringify({ ignoredRelationshipIds: Array.from(next) })
                }));
            } catch (e) {
                window.showToast?.(e.message || L.ErrorState || '', 'error');
                return;
            }
        }
        ignoredIds = next;
        renderSidePanel();
    };

    const kv = (label, value) => `<div class="d-flex justify-content-between gap-2 small py-1 border-bottom">
            <span class="text-muted">${esc(label)}</span><span class="text-end text-heading">${value}</span></div>`;
    const renderConnections = () => {
        const host = document.getElementById('tplConnections');
        if (!host) return;
        const subjectId = val('tplSubjectId');
        const subjectTypes = subjectId ? types.filter(t => String(t.subjectId) === String(subjectId) && !t.isArchived) : [];
        const spine = new Set(spineFromBranches());
        const onSpine = subjectTypes.filter(t => spine.has(String(t.conceptTypeId))).length;
        const nodeTotal = nodeCountsReady ? Object.values(nodeCountByType).reduce((a, n) => a + n, 0) : null;
        const rels = diagState === 'ready' && diag
            ? esc(fmtN(L.RelationshipsSummary || '{0} · {1}', diag.total ?? (diag.items || []).length, unresolved().length))
            : dash;
        const forWhom = $ ? ($('#tplForWhom').val() || []) : [];
        const forWhomText = forWhom.length
            ? esc(audienceOptions.find(o => String(o.value) === String(forWhom[0]))?.text || '') + (forWhom.length > 1 ? ` <span class="badge bg-label-secondary">+${forWhom.length - 1}</span>` : '')
            : dash;
        const moderator = val('tplModeratorRoleType');
        const flow = ['FlowSubject', 'FlowConceptGraph', 'FlowChainTemplate', 'FlowKnowledgePath', 'FlowJourney', 'FlowVisit']
            .map(k => `<span class="badge ${k === 'FlowChainTemplate' ? 'bg-primary' : 'bg-label-secondary'}">${esc(L[k] || '')}</span>`)
            .join('<i class="bx bx-chevron-right text-muted"></i>');
        host.innerHTML = `
            <h6 class="small text-uppercase text-muted fw-semibold mb-1">${esc(L.Inputs || '')}</h6>
            ${kv(L.SubjectId || '', subjectLabelById[subjectId] ? esc(subjectLabelById[subjectId]) : dash)}
            ${kv(L.TypePalette || '', subjectId ? esc(fmtN(L.TypesOnSpine || '{0} / {1}', onSpine, subjectTypes.length)) : dash)}
            ${kv(L.Nodes || '', nodeTotal == null ? dash : esc(String(nodeTotal)))}
            ${kv(L.Relationships || '', rels)}
            ${kv(L.ForWhom || '', forWhomText)}
            ${kv(L.Moderator || '', moderator ? esc(moderatorLabel(moderator)) : dash)}
            <h6 class="small text-uppercase text-muted fw-semibold mt-3 mb-1">${esc(L.Outputs || '')}</h6>
            ${kv(L.KnowledgePaths || '', dash)}
            ${kv(L.Journeys || '', dash)}
            ${kv(L.Visits || '', dash)}
            <div class="form-text">${esc(L.OutputsNoSource || '')}</div>
            <h6 class="small text-uppercase text-muted fw-semibold mt-3 mb-2">${esc(L.DataFlow || '')}</h6>
            <div class="d-flex flex-wrap align-items-center gap-1">${flow}</div>`;
    };

    // "Add to branch" offers every branch that does not already hold the missing type (type-repeat rule); the add goes
    // through the FE-4 moveTo, so it is exactly a palette drop: {min 1, max 1} at the end of that branch.
    const addToBranchMenu = typeId => {
        const targets = branches.map((b, bi) => ({ b, bi })).filter(x => !x.b.steps.some(s => String(s.conceptTypeId) === String(typeId)));
        if (!targets.length || !typeOptionsFor(val('tplSubjectId')).some(o => String(o.value) === String(typeId))) return '';
        return `<div class="dropdown d-inline-block">
                <button type="button" class="btn btn-sm btn-label-primary dropdown-toggle py-0 px-2" data-bs-toggle="dropdown" aria-expanded="false">
                    <i class="bx bx-plus me-1"></i>${typeNameOrDash(typeId)}
                </button>
                <ul class="dropdown-menu">${targets.map(x => `<li><button type="button" class="dropdown-item small js-nc-add" data-ct="${esc(String(typeId))}" data-b="${x.bi}">${esc(fmt(L.AddToBranch || '{0}', x.bi + 1))}${x.b.name ? ` · ${esc(x.b.name)}` : ''}</button></li>`).join('')}</ul>
            </div>`;
    };
    const ncCard = (item, ignored) => {
        const ro = templateReadOnly;
        const isOut = item.result === 'out';
        const reason = isOut ? L.ReasonNotOnSpine : L.ReasonWrongOrder;
        const missing = isOut && (item.missingTypeIds || []).length
            ? `<div class="small mt-1"><span class="text-muted">${esc(L.MissingTypes || '')}:</span> ${(item.missingTypeIds || []).map(typeNameOrDash).join(', ')}</div>`
            : '';
        const reversed = item.isReversed ? ` <i class="bx bx-transfer-alt text-muted" title="${esc(item.relationshipType)}"></i>` : '';
        const actions = ro ? '' : ignored
            ? `<button type="button" class="btn btn-sm btn-text-secondary py-0 px-2 js-nc-undo" data-rel="${esc(item.conceptRelationshipId)}"><i class="bx bx-undo me-1"></i>${esc(L.Undo || '')}</button>`
            : `${isOut ? (item.missingTypeIds || []).map(addToBranchMenu).join(' ') : ''}
               <button type="button" class="btn btn-sm btn-text-secondary py-0 px-2 js-nc-ignore" data-rel="${esc(item.conceptRelationshipId)}"><i class="bx bx-hide me-1"></i>${esc(L.Ignore || '')}</button>`;
        return `<div class="border rounded p-2 mb-2${ignored ? ' opacity-75' : ''}">
                <div class="small text-heading">
                    <span class="fw-medium">${item.fromConceptNodeName ? esc(item.fromConceptNodeName) : '—'}</span>
                    <span class="badge bg-label-info mx-1">${esc(item.relationshipType)}</span>${reversed}
                    <span class="fw-medium">${item.toConceptNodeName ? esc(item.toConceptNodeName) : '—'}</span>
                </div>
                <div class="small mt-1"><span class="badge ${isOut ? 'bg-label-warning' : 'bg-label-danger'}">${esc(reason || '')}</span></div>
                ${missing}
                ${actions ? `<div class="d-flex flex-wrap gap-1 mt-2">${actions}</div>` : ''}
            </div>`;
    };
    const renderNonConforming = () => {
        const host = document.getElementById('tplNonConforming');
        if (!host) return;
        const open = unresolved();
        const done = nonConforming().filter(i => ignoredIds.has(String(i.conceptRelationshipId)));
        const badge = document.getElementById('tplNonConformingBadge');
        if (badge) { badge.textContent = String(open.length); badge.classList.toggle('d-none', diagState !== 'ready' || open.length === 0); }
        if (!val('tplSubjectId')) { host.innerHTML = `<div class="form-text">${esc(L.NodePickerSubjectFirst || '')}</div>`; return; }
        if (diagState === 'error') { host.innerHTML = `<div class="text-danger small">${esc(L.ErrorState || '')}</div>`; return; }
        if (diagState !== 'ready' && !diag) { host.innerHTML = `<div class="small text-muted"><span class="spinner-border spinner-border-sm me-2"></span>${esc(L.Loading || '')}</div>`; return; }
        const loading = diagState === 'loading' ? '<span class="spinner-border spinner-border-sm text-muted ms-2"></span>' : '';
        host.innerHTML = `
            <div class="small text-muted mb-2">${esc(fmt(L.UnresolvedCount || '{0}', open.length))}${loading}</div>
            ${open.length ? open.map(i => ncCard(i, false)).join('') : `<div class="small text-success mb-2"><i class="bx bx-check-circle me-1"></i>${esc(L.NonConformingEmpty || '')}</div>`}
            ${done.length ? `<h6 class="small text-uppercase text-muted fw-semibold mt-3 mb-2">${esc(fmt(L.IgnoredSection || '{0}', done.length))}</h6>${done.map(i => ncCard(i, true)).join('')}` : ''}`;
    };
    // Off-spine summary under the diagram: the unresolved "out" relationships grouped by missing type.
    const renderOffSpine = () => {
        const host = document.getElementById('tplOffSpineSummary');
        if (!host) return;
        const counts = new Map();
        unresolved().filter(i => i.result === 'out')
            .forEach(i => (i.missingTypeIds || []).forEach(id => counts.set(String(id), (counts.get(String(id)) || 0) + 1)));
        host.classList.toggle('d-none', counts.size === 0);
        host.innerHTML = counts.size === 0 ? '' : `<button type="button" class="btn btn-sm btn-label-warning w-100 text-start js-offspine-open">
                <i class="bx bx-error me-1"></i><span class="fw-medium">${esc(L.OffSpineSummary || '')}:</span>
                ${Array.from(counts.entries()).map(([id, n]) => `${typeNameOrDash(id)} (${n})`).join(' · ')}
            </button>`;
    };
    const loadVersions = async () => {
        const subjectId = val('tplSubjectId');
        const code = currentRow?.chainCode;
        if (!subjectId || !code) { versions = null; renderVersions(); return; }
        try {
            const data = await getJson(`/concept-chain-templates?subjectId=${encodeURIComponent(subjectId)}&includeArchived=true`);
            versions = (data?.items || []).filter(t => t.chainCode === code)
                .sort((a, b) => new Date(b.createdAt || b.effectiveFrom) - new Date(a.createdAt || a.effectiveFrom));
        } catch { versions = []; }
        renderVersions();
    };
    const renderVersions = () => {
        const host = document.getElementById('tplVersions');
        if (!host) return;
        if (!versions || !versions.length) { host.innerHTML = `<div class="form-text">${esc(L.VersionsEmpty || '')}</div>`; return; }
        const day = v => v ? new Date(v).toLocaleDateString() : '';
        host.innerHTML = `<ul class="list-unstyled mb-0">${versions.map(t => {
            const self = currentRow && t.conceptChainTemplateId === currentRow.conceptChainTemplateId;
            const tone = t.isArchived ? 'secondary' : t.status === 'published' ? 'success' : 'primary';
            const spine = (t.orderedConceptTypes || []).map(typeNameOrDash).join(' → ');
            const who = t.updatedBy || t.createdBy;
            const title = self
                ? `<span class="fw-semibold text-heading">${esc(t.chainVersion || '—')}</span> <span class="badge bg-label-primary">${esc(L.ThisVersion || '')}</span>`
                : `<a class="fw-semibold" href="/CRM/KnowledgeConcepts/Templates/Edit/${encodeURIComponent(t.conceptChainTemplateId)}">${esc(t.chainVersion || '—')}</a>`;
            return `<li class="border-start border-2 ps-3 pb-3 position-relative">
                    <div class="d-flex align-items-center gap-2 flex-wrap">${title}<span class="badge bg-label-${tone}">${esc(t.isArchived ? 'archived' : t.status)}</span></div>
                    <div class="small text-muted">${esc(day(t.effectiveFrom))}${t.effectiveTo ? ` – ${esc(day(t.effectiveTo))}` : ''}</div>
                    ${who ? `<div class="small text-muted">${esc(L.UpdatedByLabel || '')}: ${esc(who)}</div>` : ''}
                    <div class="small mt-1">${spine || '—'}</div>
                </li>`;
        }).join('')}</ul>`;
    };
    const renderSidePanel = () => { renderConnections(); renderNonConforming(); renderOffSpine(); };

    // ─── WP-CT-FE-6: publish confirm dialog + read-only band ──────────────────────────────────────────────────────
    // The dialog is a client-side MIRROR of the WP-CT-BE-B publish guards — the service still enforces them (a 400 is
    // shown like any save error). Blocking: spine ≥ 2 types · every non-empty branch ≥ 2 steps (empty branches are not
    // sent) · ≥ 1 audience. Non-blocking: unresolved non-conforming relationships (D8 — never enforced). Confirm =
    // status "published" + the existing save; any save with status "published" is routed through this dialog, so the
    // status dropdown and the button share one gate.
    let publishConfirmed = false;
    const publishChecks = () => {
        const short = branches.map((b, i) => ({ b, i })).filter(x => x.b.steps.length > 0 && x.b.steps.length < 2);
        const filled = branches.filter(b => b.steps.length > 0).length;
        const forWhom = $ ? ($('#tplForWhom').val() || []) : [];
        return [
            { ok: spineFromBranches().length >= 2, label: L.PublishBlockMinTypes },
            {
                ok: filled > 0 && short.length === 0, label: L.PublishBlockBranchSteps,
                detail: short.length ? fmtN(L.PublishBranchesShort || '{0}', short.map(x => `${L.BranchLabel || 'Branch'} ${x.i + 1}`).join(', ')) : ''
            },
            { ok: forWhom.length >= 1, label: L.PublishBlockAudience }
        ];
    };
    const openPublishDialog = () => {
        const el = document.getElementById('tplPublishModal');
        if (!el || templateReadOnly) return;
        const spine = spineFromBranches();
        document.getElementById('tplPublishSpine').innerHTML = spine.length
            ? spine.map(id => `<span class="badge bg-label-primary">${typeNameOrDash(id)}</span>`).join('<i class="bx bx-chevron-right text-muted"></i>')
            : dash;
        const checks = publishChecks();
        document.getElementById('tplPublishChecks').innerHTML = checks.map(c => `<li class="d-flex align-items-start gap-2 small mb-1">
                <i class="bx ${c.ok ? 'bx-check-circle text-success' : 'bx-x-circle text-danger'} fs-5"></i>
                <span class="${c.ok ? '' : 'text-danger fw-medium'}">${esc(c.label || '')}${c.detail ? `<span class="d-block text-muted fw-normal">${esc(c.detail)}</span>` : ''}</span>
            </li>`).join('');
        const open = diagState === 'ready' ? unresolved().length : 0;
        const warn = document.getElementById('tplPublishWarn');
        if (warn) { warn.textContent = open ? fmtN(L.PublishWarnUnresolved || '{0}', open) : ''; warn.classList.toggle('d-none', !open); }
        document.getElementById('btnTplPublishConfirm').disabled = checks.some(c => !c.ok);
        window.bootstrap?.Modal.getOrCreateInstance(el).show();
    };
    const confirmPublish = () => {
        if (templateReadOnly || publishChecks().some(c => !c.ok)) return;
        setVal('tplStatus', 'published'); if ($) $('#tplStatus').trigger('change.select2');
        const el = document.getElementById('tplPublishModal');
        if (el) window.bootstrap?.Modal.getOrCreateInstance(el).hide();
        publishConfirmed = true;
        document.getElementById('conceptTemplateForm')?.requestSubmit();
    };
    // Published → the band says so and offers "New version" (the existing startNewVersion); the older frozen note is
    // superseded by the band, and the publish button disappears.
    const renderReadOnlyBand = () => {
        const band = document.getElementById('tplReadOnlyBand');
        if (band) {
            band.classList.toggle('d-none', !templateReadOnly);
            band.innerHTML = templateReadOnly ? `<div class="alert alert-secondary d-flex align-items-center gap-3 flex-wrap mb-0" role="status">
                    <i class="bx bx-lock-alt fs-3"></i>
                    <span class="flex-grow-1">${esc(L.ReadOnlyBandText || '')}</span>
                    <button type="button" class="btn btn-sm btn-primary js-band-new-version"><i class="bx bx-git-branch me-1"></i>${esc(L.NewVersion || '')}</button>
                </div>` : '';
        }
        if (templateReadOnly) document.getElementById('conceptTemplateFrozenNote')?.classList.add('d-none');
        document.getElementById('btnTplPublish')?.classList.toggle('d-none', templateReadOnly);
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
        if (!id) {
            payload.subjectId = val('tplSubjectId'); payload.chainCode = val('tplChainCode');
            // WP-CT-FE-5: a saved template persists "Yok say" per click; a new one (incl. a new version) sends it here.
            payload.ignoredNonConformingRelationshipIds = Array.from(ignoredIds);
        }

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
        renderReadOnlyBand();   // WP-CT-FE-6: the new draft drops the band and gets the publish button back
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

        currentRow = row;
        ignoredIds = new Set((row?.ignoredNonConformingRelationshipIds || []).map(String));

        const frozen = norm(row?.status) === 'published';
        templateReadOnly = frozen;
        document.getElementById('conceptTemplateFrozenNote')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnTplNewVersion')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnSaveConceptTemplate')?.classList.toggle('d-none', frozen);
        document.getElementById('btnTplAddBranch').disabled = frozen;
        // SCMM-10-MOD-C (D-f): a published template's Moderator/ForWhom freeze with the rest (edit → new version).
        setIdentityDisabled(frozen);
        renderReadOnlyBand();   // WP-CT-FE-6

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
        renderConnections();
        void loadVersions();
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
        const onSubjectPalette = () => { if (subj.disabled) return; void loadNodeCounts(val('tplSubjectId')).then(() => { renderPalette(); renderConnections(); }); };
        subj?.addEventListener('change', onSubjectPalette);
        if ($) $(subj).on('change', onSubjectPalette);

        // Structural builder actions.
        document.addEventListener('click', event => {
            if (event.target.closest('#btnTplAddBranch')) { event.preventDefault(); if (templateReadOnly) return; branches.push({ name: '', steps: [] }); renderBranches(); return; }
            if (event.target.closest('#btnTplNewVersion')) { event.preventDefault(); startNewVersion(); return; }
            const br = event.target.closest('.js-branch-remove');
            // WP-CT-FE-3: "Delete branch" keeps at least one branch.
            if (br) { event.preventDefault(); if (templateReadOnly || branches.length <= 1) return; branches.splice(Number(br.dataset.b), 1); openCompose.clear(); renderBranches(); return; }
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
        // WP-CT-FE-3-REFINE: remember which lanes have the compact compose open; opening it moves keyboard focus to
        // the type picker (the non-drag path).
        lanesHost?.addEventListener('shown.bs.collapse', event => {
            const box = event.target.closest?.('.js-compose');
            if (!box || box !== event.target) return;
            openCompose.add(Number(box.dataset.b));
            box.querySelector('.js-branch-type-picker')?.focus();
        });
        lanesHost?.addEventListener('hidden.bs.collapse', event => {
            const box = event.target.closest?.('.js-compose');
            if (box && box === event.target) openCompose.delete(Number(box.dataset.b));
        });
        lanesHost?.addEventListener('change', event => {
            if (!event.target.classList?.contains('js-back-edges-toggle')) return;
            showBackEdges = !!event.target.checked;
            drawAllEdges();
        });
        window.addEventListener('resize', () => { pinLaneChrome(); drawAllEdges(); });

        // WP-CT-FE-6: publish button (validates the form first) / confirm / band "New version".
        document.getElementById('btnTplPublish')?.addEventListener('click', event => {
            event.preventDefault();
            const form = document.getElementById('conceptTemplateForm');
            if (form && !form.checkValidity()) { form.reportValidity(); return; }
            openPublishDialog();
        });
        document.getElementById('btnTplPublishConfirm')?.addEventListener('click', event => { event.preventDefault(); confirmPublish(); });
        document.addEventListener('click', event => {
            if (!event.target.closest('.js-band-new-version')) return;
            event.preventDefault();
            startNewVersion();
        });

        // WP-CT-FE-5: right-panel actions (draft only — the handlers bail on a read-only template).
        document.addEventListener('click', event => {
            const add = event.target.closest('.js-nc-add');
            if (add) { event.preventDefault(); const bi = Number(add.dataset.b); moveTo(bi, branches[bi]?.steps.length ?? 0, { kind: 'type', typeId: add.dataset.ct }); return; }
            const ign = event.target.closest('.js-nc-ignore');
            if (ign) { event.preventDefault(); void setIgnored(ign.dataset.rel, true); return; }
            const undo = event.target.closest('.js-nc-undo');
            if (undo) { event.preventDefault(); void setIgnored(undo.dataset.rel, false); return; }
            if (event.target.closest('.js-offspine-open')) {
                event.preventDefault();
                const btn = tabButton();
                if (btn && window.bootstrap?.Tab) window.bootstrap.Tab.getOrCreateInstance(btn).show();
                document.getElementById('tplSidePanel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
        });
        ['tplForWhom', 'tplModeratorRoleType'].forEach(id => {
            const el = document.getElementById(id);
            el?.addEventListener('change', renderConnections);
            if ($ && el) $(el).on('change', renderConnections);
        });

        // WP-CT-FE-4: drag sources (palette rows, step cards) + lane drop targets.
        document.addEventListener('dragstart', event => {
            dragPayload = null;
            if (templateReadOnly) return;
            const pal = event.target.closest?.('.js-palette-type[draggable="true"]');
            const card = event.target.closest?.('#tplBranches .js-step-card[draggable="true"]');
            // A card whose Min/Max editor is open (or holds focus) is being edited, not dragged.
            if (card && (card.querySelector('.js-step-detail.show') || card.contains(document.activeElement) && document.activeElement.matches('input'))) {
                event.preventDefault();
                return;
            }
            if (pal) dragPayload = { kind: 'type', typeId: pal.dataset.ct };
            else if (card) dragPayload = { kind: 'step', bid: Number(card.dataset.b), sid: Number(card.dataset.s) };
            else return;
            ensureDropStyle();
            event.dataTransfer.effectAllowed = dragPayload.kind === 'type' ? 'copy' : 'move';
            try { event.dataTransfer.setData('text/plain', JSON.stringify(dragPayload)); } catch { /* payload is in module state */ }
        });
        document.addEventListener('dragend', () => { dragPayload = null; clearDropTargets(); });
        lanesHost?.addEventListener('dragover', event => {
            if (!dragPayload || templateReadOnly) return;
            const point = dropPoint(event);
            if (!point) { clearDropTargets(); return; }
            event.preventDefault();
            event.dataTransfer.dropEffect = dragPayload.kind === 'type' ? 'copy' : 'move';
            const body = point.lane.querySelector('.js-lane-body');
            if (body && !body.classList.contains('is-drop-target')) { clearDropTargets(); body.classList.add('is-drop-target'); }
        });
        lanesHost?.addEventListener('dragleave', event => {
            if (!event.relatedTarget || !lanesHost.contains(event.relatedTarget)) clearDropTargets();
        });
        lanesHost?.addEventListener('drop', event => {
            if (!dragPayload || templateReadOnly) return;
            const point = dropPoint(event);
            if (!point) return;
            event.preventDefault();
            const payload = dragPayload;
            dragPayload = null;
            clearDropTargets();
            moveTo(point.bi, point.insertIndex, payload);
        });

        // Save (JS submit; the button is a form submit but we own the flow).
        const formEl = document.getElementById('conceptTemplateForm');
        formEl?.addEventListener('submit', async event => {
            event.preventDefault();
            if (!formEl.checkValidity()) { formEl.reportValidity(); return; }
            // WP-CT-FE-6: every save that publishes goes through the confirm dialog first (button or status dropdown).
            const confirmed = publishConfirmed;
            publishConfirmed = false;
            if (val('tplStatus') === 'published' && !confirmed) { openPublishDialog(); return; }
            try { await submit(); }
            catch (err) { if (!err?.handled) showAlert(err.message || L.ErrorState); }
        });
    });
})(window, document);
