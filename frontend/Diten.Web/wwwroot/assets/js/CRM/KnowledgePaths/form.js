/**
 * MOD-0162-FU04 KnowledgePath form — path fields + card-based step builder.
 *  - Topic cascade narrows the topic list to the selected subject (bound through jQuery so select2's change fires it).
 *  - Card-based step builder (Edit page): a content LIBRARY of matching published KnowledgeContents (auto-filtered by the
 *    path classification) rendered as selectable cards; checking a card adds that content as a step. The SELECTED steps
 *    are a drag-drop ordered list (window.Sortable); clicking a row opens the detail editor (step canvas + branch repeater).
 *  - Steps are the path's sub-resource: created/updated/archived through /CRM/KnowledgePaths/api/paths/{id}/steps.
 *    The branch conditions are authorable data only — they are never evaluated here.
 */
(function (window, document) {
    'use strict';
    const L = window.KnowledgePathsL10n || window.L10n || {};
    const $ = window.jQuery;

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const toast = (m, t) => window.showToast?.(m, t || 'info');

    // Flatpickr on the date inputs.
    if (window.flatpickr) {
        document.querySelectorAll('.flatpickr-date').forEach(el => window.flatpickr(el, { dateFormat: 'Y-m-d', allowInput: true }));
    }
    if ($ && $.fn.select2) {
        $('.select2').each(function () {
            const $s = $(this);
            if (!$s.hasClass('select2-hidden-accessible')) $s.select2({ width: '100%', dropdownParent: $(document.body) });
        });
    }

    // ─── Topic cascade ─────────────────────────────────────────────────────────
    // BUGFIX: select2 raises its change through jQuery (`$(el).trigger('change')`), which does NOT reach a native
    // addEventListener('change') handler. The cascade must therefore be bound with jQuery, and it must also run ON LOAD
    // for the subject that is already selected (Edit page / a just-picked subject), rebuilding the topic <option>s and
    // refreshing select2 so the topic stays selectable. The option's `group` is the topic's SubjectId — the same GUID the
    // subject option carries — so the equality test is correct.
    const topicOptions = readJson('knowledgePathTopicOptions') || [];
    const subjectSelect = document.getElementById('SubjectId');
    const topicSelect = document.getElementById('TopicId');
    if (subjectSelect && topicSelect) {
        const populateTopics = () => {
            const subject = subjectSelect.value;
            const current = topicSelect.value;
            const opts = topicOptions.filter(o => o.group === subject || o.value === current);
            topicSelect.innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
                opts.map(o => `<option value="${esc(o.value)}"${o.value === current ? ' selected' : ''}>${esc(o.label)}${o.isInactive ? ' (' + esc(L.Archived || '') + ')' : ''}</option>`).join('');
            // Refresh select2 so the rebuilt options become selectable (change.select2 re-renders without re-firing change).
            if ($ && $.fn.select2 && $(topicSelect).hasClass('select2-hidden-accessible')) $(topicSelect).trigger('change.select2');
        };
        if ($ && $.fn.on) $(subjectSelect).on('change', populateTopics);
        else subjectSelect.addEventListener('change', populateTopics);
        populateTopics(); // initial population for the current subject value
    }

    // ─── Card-based step builder (Edit page only) ──────────────────────────────
    const editor = document.getElementById('stepEditor');
    if (!editor) return;
    const pathId = editor.dataset.pathId;
    const endpoint = editor.dataset.endpoint;
    const subjectId = editor.dataset.subjectId || '';
    const topicId = editor.dataset.topicId || '';
    const audienceId = editor.dataset.audienceId || '';
    const vocab = readJson('knowledgePathVocab') || { stepTypes: [], completionRules: [], versionPinPolicies: [] };

    const stepList = document.getElementById('stepList');
    const stepEmpty = document.getElementById('stepEmpty');
    const contentLibrary = document.getElementById('contentLibrary');
    const contentEmpty = document.getElementById('contentLibraryEmpty');
    const branchList = document.getElementById('branchList');
    let steps = [];
    let contents = [];
    let nodes = [];
    let canvas = null;
    let sortable = null;
    // Client-side refine bar state (search over title/code + content-type), applied on the already-loaded card set.
    let searchTerm = '';
    let typeFilter = '';

    const optionHtml = (arr, sel) => arr.map(v => `<option value="${esc(v)}"${v === sel ? ' selected' : ''}>${esc(v)}</option>`).join('');
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const contentIdOf = c => c.contentId || c.id;
    const stepContentOf = s => s.contentId;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error((body.errors || [L.ErrorState]).join(' · '));
        return body.data;
    };

    // ── Content type icon / thumbnail (mirrors the Knowledge list logo chip) ──
    const IMAGE_URL_RE = /\.(png|jpe?g|gif|webp|svg)(\?.*)?$/i;
    const pickTypeIcon = row => {
        const url = norm(row && row.url).toLowerCase();
        if (url.endsWith('.pdf') || norm(row && row.contentType).toLowerCase() === 'pdf') return 'bxs-file-pdf';
        switch (norm(row && row.contentType).toLowerCase()) {
            case 'presentation':       return 'bx-slideshow';
            case 'clinical-summary':   return 'bx-file';
            case 'faq':                return 'bx-help-circle';
            case 'objection-handling': return 'bx-message-rounded-dots';
            case 'video':              return 'bx-play-circle';
            case 'brochure':           return 'bxs-file-pdf';
            case 'quiz':               return 'bx-list-check';
            default:                   return norm(row && row.fileRef) ? 'bx-file' : 'bx-file-blank';
        }
    };
    const iconChip = row => `<span class="rounded border d-inline-flex align-items-center justify-content-center text-muted flex-shrink-0 bg-label-secondary" style="width:40px;height:40px;"><i class="icon-base bx ${pickTypeIcon(row)}"></i></span>`;
    const imgThumb = (src, row) => `<img src="${esc(src)}" class="rounded border flex-shrink-0" style="width:40px;height:40px;object-fit:contain;background:var(--bs-body-bg);" alt="" data-fallback="${esc(iconChip(row))}" onerror="this.insertAdjacentHTML('afterend', this.dataset.fallback); this.remove();">`;
    const titleThumb = row => {
        const url = norm(row && row.url);
        if (url && IMAGE_URL_RE.test(url)) return imgThumb(url, row);
        const fileRef = norm(row && row.fileRef);
        if (fileRef) return imgThumb(`/CRM/Knowledge/document-preview/${encodeURIComponent(fileRef)}`, row);
        return iconChip(row);
    };
    // ── Centered "book cover" (larger). Image content → centered <img>; anything else → a same-sized icon placeholder,
    //    so every card has a consistent cover band. Image errors fall back to the icon tile via the same onerror hook. ──
    const COVER_STYLE = 'height:140px;';
    const coverIconTile = row => `<div class="d-flex align-items-center justify-content-center rounded border bg-label-secondary text-muted w-100" style="${COVER_STYLE}"><i class="icon-base bx ${pickTypeIcon(row)}" style="font-size:2.75rem;"></i></div>`;
    const coverImg = (src, row) => `<img src="${esc(src)}" class="d-block mx-auto rounded border" style="${COVER_STYLE}max-width:100%;object-fit:contain;background:var(--bs-body-bg);" alt="" data-fallback="${esc(coverIconTile(row))}" onerror="this.insertAdjacentHTML('afterend', this.dataset.fallback); this.remove();">`;
    const coverBlock = row => {
        const url = norm(row && row.url);
        if (url && IMAGE_URL_RE.test(url)) return coverImg(url, row);
        const fileRef = norm(row && row.fileRef);
        if (fileRef) return coverImg(`/CRM/Knowledge/document-preview/${encodeURIComponent(fileRef)}`, row);
        return coverIconTile(row);
    };
    const typeBadge = c => `<span class="badge bg-label-info">${esc(c.contentType || '—')}</span>`;
    const statusBadge = c => `<span class="badge bg-label-${c.contentStatus === 'archived' ? 'secondary' : 'success'}">${esc(c.contentStatus || '—')}</span>`;
    const fmtDate = v => { if (!v) return ''; try { return new Date(v).toLocaleDateString(); } catch (e) { return norm(v); } };
    // Icon + label + value row, mirroring the SubscriptionPlans "Default Quotas" list markup.
    const metaRow = (icon, label, value) => value
        ? `<li class="d-flex align-items-center gap-2 mb-1 small"><i class="bx ${icon} text-muted"></i><span class="text-muted">${esc(label)}:</span><span class="font-monospace text-heading fw-medium">${esc(value)}</span></li>`
        : '';

    const loadRefData = async () => {
        // Content library: matching PUBLISHED content, filtered server-side by the path classification (small dataset).
        const params = new URLSearchParams({ includeArchived: 'false', contentStatus: 'published' });
        if (subjectId) params.set('subjectId', subjectId);
        if (topicId) params.set('topicId', topicId);
        if (audienceId) params.set('audienceProfileId', audienceId);
        try { contents = (await envelope(await fetch(`${endpoint}/contents?${params.toString()}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || []; }
        catch (e) { contents = []; }
        // Defensive client-side narrowing in case the proxy ignores a param.
        contents = contents.filter(c => (!subjectId || norm(c.subjectId) === subjectId) && norm(c.contentStatus) === 'published');
        try { nodes = (await envelope(await fetch(`${endpoint}/concept-nodes?includeArchived=false${subjectId ? '&subjectId=' + subjectId : ''}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || []; }
        catch (e) { nodes = []; }
    };

    const loadSteps = async () => {
        try {
            steps = (await envelope(await fetch(`${endpoint}/paths/${pathId}/steps?includeArchived=false`, { credentials: 'same-origin', headers: { Accept: 'application/json' } })))?.items || [];
        } catch (e) { steps = []; }
        renderLibrary();
        renderSteps();
    };

    const activeSteps = () => steps.filter(s => !s.isArchived).sort((a, b) => a.stepOrder - b.stepOrder);
    const stepForContent = cid => activeSteps().find(s => stepContentOf(s) === cid);

    // ── Refine bar (client-side) ──
    const matchesFilter = c => {
        if (typeFilter && norm(c.contentType) !== typeFilter) return false;
        if (searchTerm) {
            const hay = `${norm(c.contentTitle)} ${norm(c.contentCode)}`.toLowerCase();
            if (!hay.includes(searchTerm)) return false;
        }
        return true;
    };
    const populateTypeFilter = () => {
        const sel = document.getElementById('contentTypeFilter');
        if (!sel) return;
        const allText = (sel.options[0] && sel.options[0].textContent) || (L.ShowAll || 'All');
        const types = Array.from(new Set(contents.map(c => norm(c.contentType)).filter(Boolean))).sort();
        sel.innerHTML = `<option value="">${esc(allText)}</option>` + types.map(t => `<option value="${esc(t)}">${esc(t)}</option>`).join('');
    };
    const bindFilters = () => {
        const s = document.getElementById('contentSearch');
        const ty = document.getElementById('contentTypeFilter');
        const all = document.getElementById('contentSelectAll');
        if (s) {
            s.addEventListener('input', () => { searchTerm = norm(s.value).toLowerCase(); renderLibrary(); });
            // The refine input lives inside the path <form>; keep Enter from submitting it.
            s.addEventListener('keydown', e => { if (e.key === 'Enter') e.preventDefault(); });
        }
        if (ty) ty.addEventListener('change', () => { typeFilter = norm(ty.value); renderLibrary(); });
        if (all) all.addEventListener('change', () => { if (all.checked) selectAllAdd(); else selectAllRemove(); });
    };

    // ── Part A: content library cards ──
    const renderLibrary = () => {
        if (!contentLibrary) return;
        const visible = contents.filter(matchesFilter);
        contentEmpty?.classList.toggle('d-none', visible.length > 0);
        contentLibrary.innerHTML = visible.map(c => {
            const cid = contentIdOf(c);
            const added = !!stepForContent(cid);
            const inputId = `kp-add-${esc(cid)}`;
            const meta = [
                metaRow('bx-globe', L.LanguageCode || 'Language', norm(c.languageCode)),
                metaRow('bx-git-branch', L.ContentVersion || 'Version', norm(c.contentVersion)),
                metaRow('bx-calendar-event', L.EffectiveFrom || 'Effective from', fmtDate(c.effectiveFrom))
            ].filter(Boolean).join('');
            // Structure mirrors Platform/SubscriptionPlans renderPlanCard: header (name h5 + badges + monospace code
            // subtitle), then a centered "book cover" preview band, the summary, a border-top divider pushed to the
            // bottom (mt-auto) with a metadata icon-row block laid out like "Default Quotas", then the action switch.
            return `<div class="col-12 col-md-6 col-xl-4">
                <div class="card h-100 kp-content-card${added ? ' border-primary' : ''}" data-content-id="${esc(cid)}" role="button">
                    <div class="card-body d-flex flex-column p-4">
                        <div class="mb-3">
                            <div class="d-flex flex-wrap align-items-center gap-1 mb-1">
                                <h5 class="mb-0 text-truncate" title="${esc(c.contentTitle)}">${esc(c.contentTitle)}</h5>
                                ${typeBadge(c)}${statusBadge(c)}
                            </div>
                            <small class="text-uppercase font-monospace text-primary fw-medium">${esc(c.contentCode)}</small>
                        </div>
                        <div class="mb-4">${coverBlock(c)}</div>
                        ${c.summary ? `<p class="text-muted mb-4 kp-content-summary">${esc(c.summary)}</p>` : ''}
                        <div class="border-top pt-6 mt-auto">
                            ${meta ? `<small class="text-muted d-block mb-2">${esc(L.Details || 'Details')}</small>
                            <ul class="list-unstyled mb-3">${meta}</ul>` : ''}
                            <div class="d-flex align-items-center justify-content-between js-content-action">
                                <div class="form-check form-switch mb-0">
                                    <input class="form-check-input js-content-toggle" type="checkbox" id="${inputId}" data-content-id="${esc(cid)}"${added ? ' checked' : ''} />
                                    <label class="form-check-label small" for="${inputId}">${esc(L.AddToPath || 'Add to path')}</label>
                                </div>
                                ${added ? `<span class="badge bg-label-success"><i class="bx bx-check me-1"></i>${esc(L.Added || 'Added')}</span>` : ''}
                            </div>
                        </div>
                    </div>
                </div>
            </div>`;
        }).join('');
        syncSelectAll();
    };

    // ── Part B: selected step rows (drag-drop ordered) ──
    const renderSteps = () => {
        const rows = activeSteps();
        stepEmpty?.classList.toggle('d-none', rows.length > 0);
        syncSelectedCount();
        stepList.innerHTML = rows.map(s => {
            const resCls = s.contentResolutionStatus === 'unresolved' ? 'bg-label-danger' : s.contentResolutionStatus === 'resolved-latest' ? 'bg-label-info' : 'bg-label-success';
            const branchCount = s.branchConditions && s.branchConditions.length;
            // WorkCenter inbox-row task-card layout (.inbox-row* is a globally-loaded bordered/rounded card).
            return `<article class="inbox-row p-3 kp-step-row" data-id="${esc(s.stepId)}" role="button">
                <div class="me-2 d-flex align-items-center flex-shrink-0">
                    <i class="bx bx-grid-vertical text-muted kp-step-handle" role="button" aria-label="reorder" style="cursor:grab;"></i>
                </div>
                <div class="inbox-row__main">
                    <div class="inbox-row__line inbox-row__line--primary d-flex align-items-center gap-2 flex-wrap">
                        <span class="badge inbox-row__type inbox-row__badge-outline inbox-row__badge--type-default flex-shrink-0">${esc(s.stepType)}</span>
                        <span class="badge inbox-row__status ${resCls} flex-shrink-0">${esc(s.contentResolutionStatus)}</span>
                        ${s.versionPinPolicy ? `<span class="badge inbox-row__badge-outline inbox-row__badge--role-approver flex-shrink-0">${esc(s.versionPinPolicy)}</span>` : ''}
                        <h5 class="inbox-row__title mb-0 text-truncate">${esc(s.stepTitle)}</h5>
                    </div>
                    <div class="inbox-row__line inbox-row__line--secondary text-muted">
                        <span class="inbox-row__required-arrow">→</span>
                        <span class="inbox-row__required-text">${esc(s.stepCode)}</span>
                        <span class="inbox-row__meta-separator">•</span>
                        <span>${esc(s.completionRule || s.stepType)}</span>
                    </div>
                    <div class="inbox-row__line inbox-row__line--tertiary text-muted">
                        <span class="inbox-row__meta-item"><i class="bx bx-hash inbox-row__calendar-icon"></i><span>#${esc(s.stepOrder)}</span></span>
                        ${s.isRequired ? `<span class="inbox-row__meta-separator">•</span><span class="badge inbox-row__priority">${esc(L.IsRequired || 'required')}</span>` : ''}
                        ${branchCount ? `<span class="inbox-row__meta-separator">•</span><span class="badge bg-label-secondary">${esc(L.BranchConditions || 'branch')}: ${esc(branchCount)}</span>` : ''}
                    </div>
                </div>
                <div class="inbox-row__actions d-flex align-items-center gap-1 flex-shrink-0">
                    <button type="button" class="btn btn-sm btn-icon btn-label-secondary js-step-edit" data-id="${esc(s.stepId)}"><i class="bx bx-edit"></i></button>
                    <button type="button" class="btn btn-sm btn-icon btn-label-warning js-step-archive" data-id="${esc(s.stepId)}"><i class="bx bx-archive-in"></i></button>
                </div>
            </article>`;
        }).join('');
        wireSortable();
    };

    // Drag-drop reorder → renumber StepOrder (10,20,30…) and persist each changed step (window.Sortable, per repo idiom).
    const wireSortable = () => {
        if (sortable) { try { sortable.destroy(); } catch (e) {} sortable = null; }
        if (!window.Sortable || !stepList) return;
        sortable = window.Sortable.create(stepList, {
            handle: '.kp-step-handle',
            animation: 150,
            ghostClass: 'kp-step-ghost',
            onEnd: persistOrder
        });
    };

    const persistOrder = async () => {
        const ordered = Array.from(stepList.querySelectorAll('.kp-step-row')).map(el => el.dataset.id);
        const changed = [];
        ordered.forEach((id, i) => {
            const s = steps.find(x => x.stepId === id);
            const desired = (i + 1) * 10;
            if (s && s.stepOrder !== desired) { s.stepOrder = desired; changed.push(s); }
        });
        if (!changed.length) return;
        try {
            for (const s of changed) await envelope(await fetch(`${endpoint}/paths/${pathId}/steps/${s.stepId}`, {
                method: 'PUT', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify(toStepPayload(s))
            }));
            toast(L.RecordUpdated || 'Updated', 'success');
        } catch (e) { toast(e.message || L.ErrorState, 'error'); }
        await loadSteps();
    };

    const toStepPayload = s => ({
        stepOrder: s.stepOrder,
        stepCode: s.stepCode,
        stepTitle: s.stepTitle,
        stepType: s.stepType,
        contentId: stepContentOf(s),
        isRequired: !!s.isRequired,
        versionPinPolicy: s.versionPinPolicy || 'pinned',
        completionRule: s.completionRule || 'none',
        prerequisiteStepId: s.prerequisiteStepId || null,
        conceptNodeId: s.conceptNodeId || null,
        estimatedDurationMinutes: s.estimatedDurationMinutes || null,
        notes: s.notes || null,
        branchConditions: (s.branchConditions || []).map(b => ({ conditionCode: b.conditionCode, description: b.description || null, targetStepId: b.targetStepId || null }))
    });

    // ── Add a step for a content / archive the existing one (single card + bulk select-all share these) ──
    const nextOrder = () => { const rows = activeSteps(); return rows.length ? Math.max(...rows.map(s => s.stepOrder)) + 10 : 10; };
    // Build a POST payload for a content; `usedCodes` accumulates codes so a batch stays collision-free without a reload.
    const newStepPayload = (c, order, usedCodes) => {
        const baseCode = `STEP-${norm(c.contentCode) || (usedCodes.size + 1)}`.slice(0, 90);
        let code = baseCode, i = 2;
        while (usedCodes.has(code.toLowerCase())) { code = `${baseCode}-${i++}`; }
        usedCodes.add(code.toLowerCase());
        return {
            stepOrder: order,
            stepCode: code,
            stepTitle: norm(c.contentTitle) || baseCode,
            stepType: vocab.stepTypes[0] || '',
            contentId: contentIdOf(c),
            isRequired: false,
            versionPinPolicy: (vocab.versionPinPolicies || []).includes('pinned') ? 'pinned' : (vocab.versionPinPolicies[0] || null),
            completionRule: (vocab.completionRules || []).includes('none') ? 'none' : (vocab.completionRules[0] || null),
            branchConditions: []
        };
    };
    const postStep = payload => fetch(`${endpoint}/paths/${pathId}/steps`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify(payload) }).then(envelope);
    const archiveStep = stepId => fetch(`${endpoint}/paths/${pathId}/steps/${stepId}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }).then(envelope);
    const usedCodeSet = () => new Set(activeSteps().map(s => (s.stepCode || '').toLowerCase()));

    const addStepForContent = async (cid, checkbox) => {
        const c = contents.find(x => contentIdOf(x) === cid);
        if (!c) return;
        try {
            await postStep(newStepPayload(c, nextOrder(), usedCodeSet()));
            toast(L.RecordCreated || 'Created', 'success');
            await loadSteps();
        } catch (e) {
            if (checkbox) checkbox.checked = false;
            toast(e.message || L.ErrorState, 'error');
        }
    };

    const removeStepForContent = (cid) => {
        const s = stepForContent(cid);
        if (!s) return;
        // Snap the checkbox back to the true (still-added) state; the confirm callback does the real archive + reload,
        // so a CANCEL leaves the card correctly checked without relying on an onCancel hook (the shared confirm has none).
        renderLibrary();
        window.showConfirm?.(L.ArchiveStepConfirm || L.AreYouSure, async () => {
            try {
                await archiveStep(s.stepId);
                toast(L.RecordArchived || 'Archived', 'success');
                await loadSteps();
            } catch (err) { toast(err.message || L.ErrorState, 'error'); }
        }, { type: 'warning' });
    };

    // ── Select-all: add every currently-visible (filtered) content as a step, or archive them. Sequential awaits +
    //    a busy guard prevent the double-POST / order-collision the single-card guard also protects against. ──
    let bulkBusy = false;
    const selectAllAdd = async () => {
        if (bulkBusy) return; bulkBusy = true;
        try {
            const toAdd = contents.filter(matchesFilter).filter(c => !stepForContent(contentIdOf(c)));
            if (!toAdd.length) { renderLibrary(); return; }
            const used = usedCodeSet();
            let order = nextOrder();
            for (const c of toAdd) { await postStep(newStepPayload(c, order, used)); order += 10; }
            toast(L.RecordCreated || 'Created', 'success');
            await loadSteps();
        } catch (e) { toast(e.message || L.ErrorState, 'error'); await loadSteps(); }
        finally { bulkBusy = false; }
    };
    const selectAllRemove = () => {
        const toRemove = Array.from(new Set(contents.filter(matchesFilter).map(c => stepForContent(contentIdOf(c))).filter(Boolean)));
        // Re-sync first so a cancelled confirm leaves the checkbox reflecting the true (still-added) state.
        renderLibrary();
        if (!toRemove.length) return;
        window.showConfirm?.(L.ArchiveStepConfirm || L.AreYouSure, async () => {
            if (bulkBusy) return; bulkBusy = true;
            try {
                for (const s of toRemove) await archiveStep(s.stepId);
                toast(L.RecordArchived || 'Archived', 'success');
                await loadSteps();
            } catch (e) { toast(e.message || L.ErrorState, 'error'); await loadSteps(); }
            finally { bulkBusy = false; }
        }, { type: 'warning' });
    };

    // Keep the select-all checkbox (checked / indeterminate / disabled) and the Tab-2 count badge in sync.
    const syncSelectAll = () => {
        const sel = document.getElementById('contentSelectAll');
        if (!sel) return;
        const visible = contents.filter(matchesFilter);
        const added = visible.filter(c => stepForContent(contentIdOf(c))).length;
        sel.disabled = visible.length === 0;
        sel.checked = visible.length > 0 && added === visible.length;
        sel.indeterminate = added > 0 && added < visible.length;
    };
    const syncSelectedCount = () => {
        const el = document.getElementById('kpSelectedCount');
        if (!el) return;
        const n = activeSteps().length;
        el.textContent = String(n);
        el.classList.toggle('d-none', n === 0);
    };

    contentLibrary?.addEventListener('change', e => {
        const cb = e.target.closest('.js-content-toggle');
        if (!cb) return;
        const cid = cb.dataset.contentId;
        if (cb.checked) addStepForContent(cid, cb); else removeStepForContent(cid);
    });
    // Clicking a card (outside the checkbox): if already added, open its editor; otherwise toggle the checkbox on.
    contentLibrary?.addEventListener('click', e => {
        // The action region (switch input + its for-label) drives add/remove via the 'change' handler; ignore it here so
        // the label's native toggle isn't compounded by a second add from the card-body click affordance.
        if (e.target.closest('.js-content-action')) return;
        const card = e.target.closest('.kp-content-card');
        if (!card) return;
        const cid = card.dataset.contentId;
        const s = stepForContent(cid);
        if (s) { openCanvas(s); return; }
        const cb = card.querySelector('.js-content-toggle');
        if (cb) { cb.checked = true; addStepForContent(cid, cb); }
    });

    // ── Branch repeater ──
    const addBranchRow = (b) => {
        const row = document.createElement('div');
        row.className = 'branch-row border rounded p-2';
        const targetOpts = `<option value="">${esc(L.SelectOption || '')}</option>` +
            activeSteps().map(s => `<option value="${esc(s.stepId)}"${b && b.targetStepId === s.stepId ? ' selected' : ''}>#${esc(s.stepOrder)} ${esc(s.stepCode)}</option>`).join('');
        row.innerHTML = `<div class="row g-2 align-items-end">
            <div class="col-4"><input type="text" class="form-control form-control-sm js-branch-code" placeholder="${esc(L.ConditionCode || 'code')}" value="${b ? esc(b.conditionCode) : ''}" /></div>
            <div class="col-4"><input type="text" class="form-control form-control-sm js-branch-desc" placeholder="${esc(L.Description || 'description')}" value="${b && b.description ? esc(b.description) : ''}" /></div>
            <div class="col-3"><select class="form-select form-select-sm js-branch-target">${targetOpts}</select></div>
            <div class="col-1 text-end"><button type="button" class="btn btn-sm btn-icon btn-label-danger js-branch-remove"><i class="bx bx-x"></i></button></div>
        </div>`;
        branchList.appendChild(row);
    };
    document.getElementById('btnAddBranch')?.addEventListener('click', () => addBranchRow(null));
    branchList?.addEventListener('click', e => { const b = e.target.closest('.js-branch-remove'); if (b) b.closest('.branch-row').remove(); });

    const readBranches = () => Array.from(branchList.querySelectorAll('.branch-row')).map(row => ({
        conditionCode: row.querySelector('.js-branch-code').value.trim(),
        description: row.querySelector('.js-branch-desc').value.trim() || null,
        targetStepId: row.querySelector('.js-branch-target').value || null
    })).filter(b => b.conditionCode);

    // ── Canvas open (edit an existing step; content is fixed by the card that created it) ──
    const openCanvas = (step) => {
        if (!step) return;
        document.getElementById('stepCanvasError').classList.add('d-none');
        document.getElementById('stepEditId').value = step.stepId;
        document.getElementById('stepContentId').value = stepContentOf(step);
        document.getElementById('stepCanvasLabel').textContent = L.EditStep || L.EditPath || 'Edit Step';

        const c = contents.find(x => contentIdOf(x) === stepContentOf(step));
        const contentLabel = c ? `${norm(c.contentCode)} — ${norm(c.contentTitle)} (${norm(c.contentType)})`
            : (step.resolvedContentTitle || step.contentCode || stepContentOf(step));
        const disp = document.getElementById('stepContentDisplay');
        if (disp) disp.textContent = contentLabel;

        document.getElementById('stepOrder').value = step.stepOrder;
        document.getElementById('stepCode').value = step.stepCode || '';
        document.getElementById('stepTitle').value = step.stepTitle || '';
        document.getElementById('stepType').innerHTML = optionHtml(vocab.stepTypes, step.stepType);
        document.getElementById('stepVersionPin').innerHTML = optionHtml(vocab.versionPinPolicies, step.versionPinPolicy || 'pinned');
        document.getElementById('stepCompletionRule').innerHTML = optionHtml(vocab.completionRules, step.completionRule || 'none');
        document.getElementById('stepConceptNode').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            nodes.map(n => `<option value="${esc(n.conceptNodeId || n.id)}"${step.conceptNodeId === (n.conceptNodeId || n.id) ? ' selected' : ''}>${esc(n.conceptNodeCode)} — ${esc(n.conceptNodeName)}</option>`).join('');
        document.getElementById('stepPrerequisite').innerHTML = `<option value="">${esc(L.SelectOption || '')}</option>` +
            activeSteps().filter(s => s.stepId !== step.stepId).map(s => `<option value="${esc(s.stepId)}"${step.prerequisiteStepId === s.stepId ? ' selected' : ''}>#${esc(s.stepOrder)} ${esc(s.stepCode)}</option>`).join('');
        document.getElementById('stepDuration').value = step.estimatedDurationMinutes || '';
        document.getElementById('stepNotes').value = step.notes || '';
        document.getElementById('stepRequired').checked = !!step.isRequired;
        branchList.innerHTML = '';
        (step.branchConditions || []).forEach(addBranchRow);
        canvas = window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('stepCanvas'));
        canvas.show();
    };

    const saveStep = async () => {
        const editId = document.getElementById('stepEditId').value;
        if (!editId) return;
        const payload = {
            stepOrder: parseInt(document.getElementById('stepOrder').value, 10),
            stepCode: document.getElementById('stepCode').value.trim(),
            stepTitle: document.getElementById('stepTitle').value.trim(),
            stepType: document.getElementById('stepType').value,
            contentId: document.getElementById('stepContentId').value,
            isRequired: document.getElementById('stepRequired').checked,
            versionPinPolicy: document.getElementById('stepVersionPin').value,
            completionRule: document.getElementById('stepCompletionRule').value,
            prerequisiteStepId: document.getElementById('stepPrerequisite').value || null,
            conceptNodeId: document.getElementById('stepConceptNode').value || null,
            estimatedDurationMinutes: document.getElementById('stepDuration').value ? parseInt(document.getElementById('stepDuration').value, 10) : null,
            notes: document.getElementById('stepNotes').value.trim() || null,
            branchConditions: readBranches()
        };
        try {
            await envelope(await fetch(`${endpoint}/paths/${pathId}/steps/${editId}`, { method: 'PUT', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify(payload) }));
            canvas?.hide();
            toast(L.RecordUpdated || 'Updated', 'success');
            await loadSteps();
        } catch (e) {
            const err = document.getElementById('stepCanvasError');
            err.textContent = e.message || L.ErrorState; err.classList.remove('d-none');
        }
    };

    document.getElementById('stepSaveBtn')?.addEventListener('click', saveStep);
    stepList?.addEventListener('click', e => {
        const edit = e.target.closest('.js-step-edit');
        if (edit) { const s = steps.find(x => x.stepId === edit.dataset.id); if (s) openCanvas(s); return; }
        const arc = e.target.closest('.js-step-archive');
        if (arc) {
            window.showConfirm?.(L.ArchiveStepConfirm || L.AreYouSure, async () => {
                try {
                    await envelope(await fetch(`${endpoint}/paths/${pathId}/steps/${arc.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                    toast(L.RecordArchived || 'Archived', 'success');
                    await loadSteps();
                } catch (err) { toast(err.message || L.ErrorState, 'error'); }
            }, { type: 'warning' });
            return;
        }
        // Click anywhere else on the row opens its editor (ignore drags off the handle).
        const rowEl = e.target.closest('.kp-step-row');
        if (rowEl && !e.target.closest('.kp-step-handle')) { const s = steps.find(x => x.stepId === rowEl.dataset.id); if (s) openCanvas(s); }
    });

    function readJson(id) {
        const el = document.getElementById(id);
        if (!el) return null;
        try { return JSON.parse(el.textContent || 'null'); } catch (e) { return null; }
    }

    (async () => { await loadRefData(); populateTypeFilter(); bindFilters(); await loadSteps(); })();
})(window, document);
