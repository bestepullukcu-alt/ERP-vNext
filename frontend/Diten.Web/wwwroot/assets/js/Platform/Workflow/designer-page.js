'use strict';

// Workflow Designer (form-based) standalone page host.
// Builds a single stage with multiple ordered approval steps (Step 1 → Step 2 → …) that the runtime
// executes sequentially — e.g. "Software Developer approves, then CEO approves". Produces the same
// DefinitionJson shape as the Visual Designer, then feeds the publish modal.
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));
    const definitionId = window.WorkflowDefinitionId;

    const el = (id) => document.getElementById(id);
    const val = (id) => (el(id)?.value ?? '').trim();
    const show = (node) => node && node.classList.remove('d-none');
    const hide = (node) => node && node.classList.add('d-none');

    // scoped helpers (read inside a step card)
    const qv = (card, sel) => (card.querySelector(sel)?.value ?? '').trim();
    const qc = (card, sel) => !!card.querySelector(sel)?.checked;
    const readSel = (card, sel) => {
        const node = card.querySelector(sel);
        if (!node) return [];
        return Array.from(node.selectedOptions || []).map((o) => o.value).filter(Boolean);
    };

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };

    const notify = (kind, message) => {
        const type = kind === 'error' || kind === 'danger'
            ? 'error'
            : (kind === 'warning' || kind === 'info' ? kind : 'success');
        if (typeof window.showToast === 'function') { window.showToast(message, type); return; }
        console[type === 'error' ? 'error' : 'log'](message);
    };

    const status = (kind, message) => {
        const box = el('wf-dz-status');
        if (!box) return;
        box.className = `alert alert-${kind === 'error' ? 'danger' : (kind === 'warning' ? 'warning' : 'success')}`;
        box.textContent = message;
        box.classList.remove('d-none');
    };
    const clearStatus = () => el('wf-dz-status')?.classList.add('d-none');

    const failureMessage = (res) => {
        if (res.status === 403) return t('PermissionMissing', 'Permission missing or not assigned.');
        if (res.status === 0) return t('NetworkError', 'Cannot reach the workflow gateway.');
        if (res.status === 503) return t('GatewayUnavailable', 'Workflow gateway proxy unavailable.');
        return res.message || t('RequestFailed', 'Request failed.');
    };

    const unwrapItems = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.data?.items)) return payload.data.items;
        if (Array.isArray(payload?.items)) return payload.items;
        return [];
    };
    const getJson = async (url) => {
        const response = await fetch(url, { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' });
        if (!response.ok) return [];
        return unwrapItems(await response.json());
    };
    const userLabel = (user) => {
        const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
        return name || user.fullName || user.displayName || user.email || user.userName || user.id;
    };

    // =====================================================================
    // Principal lookup pool (users + positions), shared by every step's selects.
    // Values are stored as `user:{id}` / `position:{id}` — the backend resolves both.
    // =====================================================================
    let principalOptions = [];
    const loadPrincipalOptions = async () => {
        try {
            const [users, positions] = await Promise.all([
                getJson('/Platform/Workflow/lookup/users'),
                getJson('/Platform/Workflow/lookup/positions')
            ]);
            const userOptions = users.filter((u) => u?.id).map((u) => ({ id: `user:${u.id}`, text: userLabel(u) }));
            const positionOptions = positions.filter((p) => p?.id)
                .map((p) => ({ id: `position:${p.id}`, text: `${p.code || ''} ${p.name || p.id}`.trim() }));
            principalOptions = [...userOptions, ...positionOptions];
        } catch (_e) {
            notify('warning', t('RequestFailed', 'Request failed.'));
        }
    };

    const populateNativeOptions = (selectNode) => {
        principalOptions.forEach((opt) => {
            const o = document.createElement('option');
            o.value = opt.id;
            o.textContent = opt.text;
            selectNode.appendChild(o);
        });
    };

    // Initialise a principal multi-select. The sibling type <select> drives which kind the dropdown
    // lists; chosen chips of either kind persist so a step can mix users and positions.
    const initPrincipalSelect = (selectNode, typeNode, placeholder) => {
        populateNativeOptions(selectNode);
        if (!window.jQuery || !window.jQuery.fn?.select2) return;
        const $ = window.jQuery;
        $(selectNode).select2({
            width: '100%',
            dropdownParent: $(document.body),
            placeholder: placeholder,
            closeOnSelect: false,
            matcher: (params, data) => {
                if (!data || !data.id) return data;
                if (!String(data.id).startsWith(`${typeNode.value || 'user'}:`)) return null;
                const term = (params.term || '').trim().toLowerCase();
                if (!term) return data;
                return (data.text || '').toLowerCase().indexOf(term) > -1 ? data : null;
            }
        });
    };

    // =====================================================================
    // Step cards
    // =====================================================================
    const stepsHost = () => el('wf-dz-steps');
    const stepCards = () => Array.from(stepsHost().querySelectorAll('.wf-dz-step'));

    const reindexSteps = () => {
        const cards = stepCards();
        cards.forEach((card, i) => {
            const badge = card.querySelector('.wf-dz-step-index');
            if (badge) badge.textContent = `${t('Step', 'Step')} ${i + 1}`;
            const up = card.querySelector('.wf-dz-step-up');
            const down = card.querySelector('.wf-dz-step-down');
            if (up) up.disabled = i === 0;
            if (down) down.disabled = i === cards.length - 1;
        });
        const empty = el('wf-dz-steps-empty');
        if (empty) empty.classList.toggle('d-none', cards.length > 0);
    };

    const addStep = (focus) => {
        const tpl = el('wf-dz-step-template');
        const card = tpl.content.firstElementChild.cloneNode(true);
        stepsHost().appendChild(card);

        const count = stepCards().length;
        const codeInput = card.querySelector('.wf-dz-stepcode');
        if (codeInput && !codeInput.value) codeInput.value = `step-${count}`;

        const nameInput = card.querySelector('.wf-dz-stepname');
        const echo = card.querySelector('.wf-dz-step-name-echo');
        if (nameInput && echo) {
            nameInput.addEventListener('input', () => { echo.textContent = nameInput.value.trim(); });
        }

        initPrincipalSelect(
            card.querySelector('.wf-dz-candidates'),
            card.querySelector('.wf-dz-cand-type'),
            t('CandidatePrincipalIds', 'Candidate Principal IDs'));
        initPrincipalSelect(
            card.querySelector('.wf-dz-escprincipals'),
            card.querySelector('.wf-dz-esc-type'),
            t('EscalationPrincipalIds', 'Escalation Principal IDs'));

        reindexSteps();
        if (focus && nameInput) nameInput.focus();
    };

    const onStepsClick = (event) => {
        const card = event.target.closest('.wf-dz-step');
        if (!card) return;
        if (event.target.closest('.wf-dz-step-remove')) {
            card.remove();
            reindexSteps();
        } else if (event.target.closest('.wf-dz-step-up')) {
            const prev = card.previousElementSibling;
            if (prev) { card.parentNode.insertBefore(card, prev); reindexSteps(); }
        } else if (event.target.closest('.wf-dz-step-down')) {
            const next = card.nextElementSibling;
            if (next) { card.parentNode.insertBefore(next, card); reindexSteps(); }
        }
    };

    // =====================================================================
    // Build / validate / generate
    // =====================================================================
    const buildDefinition = () => {
        const steps = stepCards().map((card, idx) => {
            const code = qv(card, '.wf-dz-stepcode') || `step-${idx + 1}`;
            const step = {
                code: code,
                name: qv(card, '.wf-dz-stepname') || code,
                type: qv(card, '.wf-dz-steptype') || 'approval',
                assignment: { mode: 'candidate_principals', candidatePrincipalIds: readSel(card, '.wf-dz-candidates') },
                requirements: {
                    commentRequired: qc(card, '.wf-dz-commentrequired'),
                    evidenceRequired: qc(card, '.wf-dz-evidencerequired')
                }
            };
            const due = qv(card, '.wf-dz-due');
            if (due !== '') {
                const sla = { dueInMinutes: Number(due) };
                const esc = qv(card, '.wf-dz-escalate');
                const tmo = qv(card, '.wf-dz-timeout');
                if (esc !== '') sla.escalateAfterMinutes = Number(esc);
                if (tmo !== '') sla.timeoutAfterMinutes = Number(tmo);
                const escP = readSel(card, '.wf-dz-escprincipals');
                if (escP.length) sla.escalationPrincipalIds = escP;
                step.sla = sla;
            }
            return step;
        });

        const def = {
            schemaVersion: val('wf-dz-schema') || 'workflow_schema_v1',
            expressionVersion: val('wf-dz-expression') || 'workflow_expr_v1'
        };
        const desc = val('wf-dz-description');
        const objectType = val('wf-dz-objecttype');
        if (desc) def.description = desc;
        if (objectType) def.requestedObjectType = objectType;
        def.stages = [{
            code: val('wf-dz-stagecode') || 'stage-1',
            name: val('wf-dz-stagename') || 'Approval Flow',
            steps: steps
        }];
        return def;
    };

    const validate = () => {
        const cards = stepCards();
        if (!cards.length) return { ok: false, message: t('NoStepsYet', 'Add at least one approval step.') };
        if (!val('wf-dz-stagecode')) return { ok: false, message: t('StageCodeRequired', 'Stage Code is required.') };
        if (!val('wf-dz-stagename')) return { ok: false, message: t('StageNameRequired', 'Stage Name is required.') };
        for (const card of cards) {
            if (!qv(card, '.wf-dz-stepname')) return { ok: false, message: t('StepNameRequired', 'Step Name is required.') };
            if (readSel(card, '.wf-dz-candidates').length === 0) {
                return { ok: false, message: t('CandidatesRequired', 'At least one Candidate Principal Id is required.') };
            }
            const due = qv(card, '.wf-dz-due');
            const esc = qv(card, '.wf-dz-escalate');
            const tmo = qv(card, '.wf-dz-timeout');
            if (due !== '' && !(Number(due) > 0)) return { ok: false, message: t('DueMinutesPositive', 'Due In Minutes must be greater than 0.') };
            if (due !== '' && esc !== '' && !(Number(esc) >= Number(due))) return { ok: false, message: t('EscalateGteDue', 'Escalate After Minutes must be ≥ Due In Minutes.') };
            if (esc !== '' && tmo !== '' && !(Number(tmo) >= Number(esc))) return { ok: false, message: t('TimeoutGteEscalate', 'Timeout After Minutes must be ≥ Escalate After Minutes.') };
        }
        return { ok: true };
    };

    const generate = () => {
        clearStatus();
        const check = validate();
        if (!check.ok) { status('error', check.message); return false; }
        el('wf-dz-preview').value = JSON.stringify(buildDefinition(), null, 2);
        status('success', t('DesignerJsonReady', 'Definition JSON generated.'));
        return true;
    };

    // =====================================================================
    // Publish
    // =====================================================================
    const setBoxError = (id, msg) => { const b = el(id); if (b) { b.textContent = msg; show(b); } };
    const clearBox = (id) => hide(el(id));

    const useInPublish = () => {
        if (!generate()) return;
        clearBox('wf-publish-error');
        hide(el('wf-publish-result'));
        el('wf-publish-form').reset();
        el('wf-publish-definitionid').value = definitionId || '';
        el('wf-publish-json').value = el('wf-dz-preview').value;
        el('wf-publish-schema').value = val('wf-dz-schema') || 'workflow_schema_v1';
        el('wf-publish-expression').value = val('wf-dz-expression') || 'workflow_expr_v1';
        new bootstrap.Modal(el('wf-publish-modal')).show();
    };

    const submitPublish = async () => {
        clearBox('wf-publish-error');
        const jsonText = el('wf-publish-json').value;
        if (!jsonText.trim()) { setBoxError('wf-publish-error', t('DefinitionJsonRequired', 'Definition JSON is required.')); return; }
        try { JSON.parse(jsonText); } catch (_e) { setBoxError('wf-publish-error', t('InvalidJson', 'The JSON is not valid.')); return; }
        if (!val('wf-publish-schema')) { setBoxError('wf-publish-error', t('SchemaVersionRequired', 'Schema Version is required.')); return; }
        if (!val('wf-publish-expression')) { setBoxError('wf-publish-error', t('ExpressionVersionRequired', 'Expression Version is required.')); return; }

        const expectedRaw = val('wf-publish-expected');
        const payload = {
            definitionJson: jsonText,
            schemaVersion: val('wf-publish-schema'),
            expressionVersion: val('wf-publish-expression'),
            expectedTemplateVersion: expectedRaw ? Number(expectedRaw) : null,
            expectedRowVersion: val('wf-publish-rowversion') || null,
            publishReason: val('wf-publish-reason') || null
        };

        const btn = el('wf-publish-submit');
        if (btn) btn.disabled = true;
        const res = await api.publishDefinition(definitionId, payload);
        if (btn) btn.disabled = false;
        if (!res.ok) { setBoxError('wf-publish-error', failureMessage(res)); return; }

        const d = res.data || {};
        const result = el('wf-publish-result');
        if (result) {
            result.innerHTML = `
                <div class="alert alert-success mb-0">
                    <div class="fw-medium mb-1">${escapeHtml(t('PublishSucceeded', 'Published successfully.'))}</div>
                    <div>${escapeHtml(t('VersionNumber', 'Version Number'))}: <strong>${escapeHtml(d.versionNumber || '-')}</strong></div>
                </div>`;
            show(result);
        }
        notify('success', t('PublishSucceeded', 'Published successfully.'));
    };

    // =====================================================================
    const loadMeta = async () => {
        if (!api || !definitionId) return;
        const res = await api.getDefinition(definitionId);
        if (!res.ok) { notify('error', failureMessage(res)); return; }
        const d = res.data || {};
        if (el('wf-meta-code')) el('wf-meta-code').textContent = d.templateCode || '-';
        if (el('wf-meta-name')) el('wf-meta-name').textContent = d.name || '-';
    };

    document.addEventListener('DOMContentLoaded', async () => {
        loadMeta();
        await loadPrincipalOptions();

        el('wf-dz-addstep')?.addEventListener('click', () => addStep(true));
        el('wf-dz-addstep-2')?.addEventListener('click', () => addStep(true));
        el('wf-dz-generate')?.addEventListener('click', generate);
        el('wf-dz-usepublish')?.addEventListener('click', useInPublish);
        el('wf-dz-steps')?.addEventListener('click', onStepsClick);
        el('wf-publish-submit')?.addEventListener('click', submitPublish);

        // Start with one empty step so the page is immediately usable.
        addStep(false);
    });
})();
