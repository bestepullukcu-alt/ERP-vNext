'use strict';

// MOD-0023 Batch 08 — Workflow definition detail page.
// Shows template metadata and Versions / Instances / SLA tabs. Version DefinitionJson is
// strictly read-only (immutable published artifacts). Instances/Tasks are filtered client-side
// by templateId because the backend list endpoints are tenant-scoped, not template-scoped.
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));
    const definitionId = window.WorkflowDefinitionId;

    const el = (id) => document.getElementById(id);
    const show = (n) => n && n.classList.remove('d-none');
    const hide = (n) => n && n.classList.add('d-none');

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    };
    const shortId = (v) => (v ? `<code title="${escapeHtml(v)}">${escapeHtml(String(v).slice(0, 8))}…</code>` : '<span class="text-muted">—</span>');
    const fmtDate = (v) => {
        if (!v) return '<span class="text-muted">—</span>';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? escapeHtml(v) : escapeHtml(d.toLocaleString());
    };
    const STATUS_TONE = { approved: 'success', published: 'success', active: 'success', completed: 'success', pending: 'warning', draft: 'secondary', rejected: 'danger', cancelled: 'danger', immutable: 'success' };
    const statusBadge = (s) => {
        if (s === null || s === undefined || s === '') return '<span class="text-muted">—</span>';
        const tone = STATUS_TONE[String(s).toLowerCase().replace(/[^a-z]/g, '')] || 'secondary';
        return `<span class="badge bg-label-${tone}">${escapeHtml(s)}</span>`;
    };
    const failureMessage = (res) => {
        if (res.status === 403) return t('PermissionMissing', 'Permission missing or not assigned.');
        if (res.status === 0) return t('NetworkError', 'Cannot reach the workflow gateway.');
        if (res.status === 503) return t('GatewayUnavailable', 'Workflow gateway proxy unavailable.');
        return res.message || t('RequestFailed', 'Request failed.');
    };
    const renderState = (prefix, { loading, error, empty }) => {
        ['loading', 'error', 'empty', 'table'].forEach((k) => hide(el(`wf-${prefix}-${k}`)));
        if (loading) show(el(`wf-${prefix}-loading`));
        else if (error) show(el(`wf-${prefix}-error`));
        else if (empty) show(el(`wf-${prefix}-empty`));
        else show(el(`wf-${prefix}-table`));
    };
    const asArray = (data) => (Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : []));
    const normalizeInitialTab = () => {
        const raw = String(window.location.hash || '').replace(/^#/, '').toLowerCase();
        if (raw === 'designer') return 'designer';
        if (raw === 'visual' || raw === 'visual-designer' || raw === 'virtual-designer') return 'visual';
        if (raw === 'instances') return 'instances';
        if (raw === 'sla' || raw === 'sla-rules') return 'sla';
        return 'versions';
    };
    const activateTab = (key) => {
        const tab = document.querySelector(`button[data-wf-tab="${key}"]`);
        if (tab && window.bootstrap?.Tab) bootstrap.Tab.getOrCreateInstance(tab).show();
    };

    const loadMeta = async () => {
        const res = await api.getDefinition(definitionId);
        if (!res.ok) {
            el('wf-meta').innerHTML = `<div class="alert alert-danger mb-0">${escapeHtml(failureMessage(res))}</div>`;
            return;
        }
        const d = res.data || {};
        el('wf-meta-code').textContent = d.templateCode || '—';
        el('wf-meta-name').textContent = d.name || '—';
        el('wf-meta').innerHTML = `
            <dl class="row mb-0">
                <dt class="col-sm-3">${escapeHtml(t('TemplateCode', 'Template Code'))}</dt><dd class="col-sm-9">${escapeHtml(d.templateCode || '—')}</dd>
                <dt class="col-sm-3">${escapeHtml(t('Name', 'Name'))}</dt><dd class="col-sm-9">${escapeHtml(d.name || '—')}</dd>
                <dt class="col-sm-3">${escapeHtml(t('Description', 'Description'))}</dt><dd class="col-sm-9">${escapeHtml(d.description || '—')}</dd>
                <dt class="col-sm-3">${escapeHtml(t('Status', 'Status'))}</dt><dd class="col-sm-9">${statusBadge(d.status)}</dd>
                <dt class="col-sm-3">${escapeHtml(t('ActivePublishedVersionId', 'Active Published Version'))}</dt><dd class="col-sm-9">${shortId(d.activePublishedVersionId)}</dd>
                <dt class="col-sm-3">${escapeHtml(t('CurrentVersionId', 'Current Version'))}</dt><dd class="col-sm-9">${shortId(d.currentVersionId)}</dd>
                <dt class="col-sm-3">${escapeHtml(t('CreatedAt', 'Created At'))}</dt><dd class="col-sm-9">${fmtDate(d.createdAt)}</dd>
            </dl>`;
    };

    let versionsCache = [];
    const loadVersions = async () => {
        renderState('ver', { loading: true });
        const res = await api.listVersions(definitionId);
        if (!res.ok) { el('wf-ver-error').textContent = failureMessage(res); renderState('ver', { error: true }); return; }
        versionsCache = asArray(res.data);
        if (!versionsCache.length) { renderState('ver', { empty: true }); return; }
        el('wf-ver-rows').innerHTML = versionsCache.map((v) => `
            <tr>
                <td>${escapeHtml(v.versionNumber)}</td>
                <td>${statusBadge(v.status)}</td>
                <td class="text-center">${v.isImmutable ? '<i class="icon-base bx bx-lock-alt text-success"></i>' : '<i class="icon-base bx bx-lock-open-alt text-muted"></i>'}</td>
                <td>${fmtDate(v.publishedAt)}</td>
                <td>${escapeHtml(v.publishedBy || '—')}</td>
                <td class="text-end"><button type="button" class="btn btn-sm btn-icon btn-text-secondary wf-ver-view" data-id="${escapeHtml(v.id)}" title="${escapeHtml(t('ViewDefinitionJson', 'View Definition JSON'))}"><i class="icon-base bx bx-code-alt"></i></button></td>
            </tr>`).join('');
        renderState('ver', {});
    };

    const openVersionModal = async (versionId) => {
        const modal = new bootstrap.Modal(el('wf-ver-modal'));
        el('wf-ver-json').textContent = t('Loading', 'Loading…');
        el('wf-ver-meta').innerHTML = '';
        modal.show();
        const res = await api.getVersion(definitionId, versionId);
        if (!res.ok) { el('wf-ver-json').textContent = failureMessage(res); return; }
        const v = res.data || {};
        el('wf-ver-meta').innerHTML = `
            <span class="me-3">${escapeHtml(t('VersionNumber', 'Version Number'))}: <strong>${escapeHtml(v.versionNumber)}</strong></span>
            <span class="me-3">${escapeHtml(t('SchemaVersion', 'Schema Version'))}: ${escapeHtml(v.schemaVersion || '—')}</span>
            <span class="me-3">${escapeHtml(t('ExpressionVersion', 'Expression Version'))}: ${escapeHtml(v.expressionVersion || '—')}</span>
            <span>${statusBadge(v.isImmutable ? 'Immutable' : v.status)}</span>`;
        let pretty = v.definitionJson || '';
        try { pretty = JSON.stringify(JSON.parse(v.definitionJson), null, 2); } catch (_e) { /* show raw */ }
        el('wf-ver-json').textContent = pretty;
    };

    const loadInstances = async () => {
        renderState('inst', { loading: true });
        const res = await api.listInstances();
        if (!res.ok) { el('wf-inst-error').textContent = failureMessage(res); renderState('inst', { error: true }); return; }
        const items = asArray(res.data).filter((i) => String(i.templateId) === String(definitionId));
        if (!items.length) { renderState('inst', { empty: true }); return; }
        el('wf-inst-rows').innerHTML = items.map((i) => `
            <tr>
                <td>${shortId(i.id)}</td>
                <td>${escapeHtml(i.objectRef || '—')}</td>
                <td>${statusBadge(i.status)}</td>
                <td>${escapeHtml(i.currentStage || '—')} / ${escapeHtml(i.currentStep || '—')}</td>
                <td>${fmtDate(i.startedAt)}</td>
                <td>${fmtDate(i.dueAt)}</td>
                <td>${fmtDate(i.completedAt)}</td>
            </tr>`).join('');
        renderState('inst', {});
    };

    const loadSla = async () => {
        renderState('sla', { loading: true });
        const res = await api.listSlaRules(definitionId);
        if (!res.ok) { el('wf-sla-error').textContent = failureMessage(res); renderState('sla', { error: true }); return; }
        const items = asArray(res.data);
        if (!items.length) { renderState('sla', { empty: true }); return; }
        el('wf-sla-rows').innerHTML = items.map((r) => `
            <tr>
                <td>${escapeHtml(r.stageCode)} / ${escapeHtml(r.stepCode)}</td>
                <td class="text-center">${escapeHtml(r.dueInMinutes)}</td>
                <td class="text-center">${escapeHtml(r.escalateAfterMinutes)}</td>
                <td class="text-center">${r.timeoutAfterMinutes != null ? escapeHtml(r.timeoutAfterMinutes) : '—'}</td>
                <td>${escapeHtml((r.escalationPrincipalIds || []).join(', '))}</td>
                <td>${statusBadge(r.isActive ? 'Active' : 'Passive')}</td>
            </tr>`).join('');
        renderState('sla', {});
    };

    // =====================================================================
    // Designer Lite / Step Builder — authors a stable DefinitionJson, then feeds the publish modal.
    // No BPMN/drag-drop: a single stage/step with assignment, requirements and optional SLA.
    // =====================================================================
    const val = (id) => (el(id)?.value ?? '').trim();
    const checked = (id) => !!el(id)?.checked;

    const notify = (kind, message) => {
        if (window.Swal && typeof window.Swal.fire === 'function') {
            window.Swal.fire({
                toast: true, position: 'top-end', timer: kind === 'error' ? 5000 : 3000,
                timerProgressBar: true, showConfirmButton: false,
                icon: kind === 'error' ? 'error' : (kind === 'warning' ? 'warning' : 'success'),
                title: message
            });
        } else {
            console[kind === 'error' ? 'error' : 'log'](message);
        }
    };

    const setBoxError = (id, msg) => { const b = el(id); if (b) { b.textContent = msg; show(b); } };
    const clearBox = (id) => hide(el(id));

    // Comma-separated -> trimmed, de-duplicated, non-empty array (order preserved).
    const normalizeCsvPrincipals = (raw) => {
        const seen = new Set();
        const out = [];
        (raw || '').split(',').forEach((part) => {
            const v = part.trim();
            if (v && !seen.has(v)) { seen.add(v); out.push(v); }
        });
        return out;
    };

    const isValidJson = (text) => {
        try { JSON.parse(text); return true; } catch (_e) { return false; }
    };
    const prettyPrintJson = (obj) => JSON.stringify(obj, null, 2);

    const DESIGNER_DEFAULTS = {
        'wf-dz-schema': 'workflow_schema_v1',
        'wf-dz-expression': 'workflow_expr_v1',
        'wf-dz-stagecode': 'stage-1',
        'wf-dz-stagename': 'Initial Approval',
        'wf-dz-stepcode': 'step-1',
        'wf-dz-stepname': '',
        'wf-dz-candidates': '',
        'wf-dz-due': '',
        'wf-dz-escalate': '',
        'wf-dz-timeout': '',
        'wf-dz-escprincipals': '',
        'wf-dz-objecttype': '',
        'wf-dz-description': ''
    };

    // Returns { ok, message } — mirrors the SLA chained-minutes rules from the SLA tab.
    const validateDesignerForm = () => {
        if (!val('wf-dz-stagecode')) return { ok: false, message: t('StageCodeRequired', 'Stage Code is required.') };
        if (!val('wf-dz-stagename')) return { ok: false, message: t('StageNameRequired', 'Stage Name is required.') };
        if (!val('wf-dz-stepcode')) return { ok: false, message: t('StepCodeRequired', 'Step Code is required.') };
        if (!val('wf-dz-stepname')) return { ok: false, message: t('StepNameRequired', 'Step Name is required.') };
        if (normalizeCsvPrincipals(val('wf-dz-candidates')).length === 0) {
            return { ok: false, message: t('CandidatesRequired', 'At least one Candidate Principal Id is required.') };
        }
        const due = val('wf-dz-due');
        const esc = val('wf-dz-escalate');
        const tmo = val('wf-dz-timeout');
        if (due !== '' && !(Number(due) > 0)) return { ok: false, message: t('DueMinutesPositive', 'Due In Minutes must be greater than 0.') };
        if (esc !== '' && due !== '' && !(Number(esc) >= Number(due))) return { ok: false, message: t('EscalateGteDue', 'Escalate After Minutes must be >= Due In Minutes.') };
        if (tmo !== '' && esc !== '' && !(Number(tmo) >= Number(esc))) return { ok: false, message: t('TimeoutGteEscalate', 'Timeout After Minutes must be >= Escalate After Minutes.') };
        return { ok: true };
    };

    const buildDefinitionJson = () => {
        const def = {
            schemaVersion: val('wf-dz-schema') || 'workflow_schema_v1',
            expressionVersion: val('wf-dz-expression') || 'workflow_expr_v1'
        };
        const description = val('wf-dz-description');
        const objectType = val('wf-dz-objecttype');
        if (description) def.description = description;
        if (objectType) def.requestedObjectType = objectType;

        const step = {
            code: val('wf-dz-stepcode'),
            name: val('wf-dz-stepname'),
            type: val('wf-dz-steptype') || 'approval',
            assignment: {
                mode: val('wf-dz-approvalmode') || 'candidate_principals',
                candidatePrincipalIds: normalizeCsvPrincipals(val('wf-dz-candidates'))
            },
            requirements: {
                commentRequired: checked('wf-dz-commentrequired'),
                evidenceRequired: checked('wf-dz-evidencerequired')
            }
        };

        // SLA block is included only when a due time is supplied (the primary SLA driver).
        const due = val('wf-dz-due');
        if (due !== '') {
            const sla = { dueInMinutes: Number(due) };
            if (val('wf-dz-escalate') !== '') sla.escalateAfterMinutes = Number(val('wf-dz-escalate'));
            if (val('wf-dz-timeout') !== '') sla.timeoutAfterMinutes = Number(val('wf-dz-timeout'));
            const escPrincipals = normalizeCsvPrincipals(val('wf-dz-escprincipals'));
            if (escPrincipals.length) sla.escalationPrincipalIds = escPrincipals;
            step.sla = sla;
        }

        def.stages = [{
            code: val('wf-dz-stagecode'),
            name: val('wf-dz-stagename'),
            steps: [step]
        }];
        return def;
    };

    const isManual = () => checked('wf-dz-manual');

    const generateDesignerJson = () => {
        clearBox('wf-dz-error');
        const check = validateDesignerForm();
        if (!check.ok) { setBoxError('wf-dz-error', check.message); return false; }
        el('wf-dz-preview').value = prettyPrintJson(buildDefinitionJson());
        notify('success', t('DesignerJsonReady', 'Definition JSON generated.'));
        return true;
    };

    // Returns the JSON text to use, generating from the form unless manual editing is active.
    const resolveDesignerJson = () => {
        if (isManual()) {
            const text = el('wf-dz-preview').value;
            if (!text.trim() || !isValidJson(text)) { setBoxError('wf-dz-error', t('InvalidJson', 'The JSON is not valid.')); return null; }
            return text;
        }
        if (!el('wf-dz-preview').value.trim()) {
            if (!generateDesignerJson()) return null;
        } else {
            // Regenerate so the preview always reflects current form state in non-manual mode.
            if (!generateDesignerJson()) return null;
        }
        return el('wf-dz-preview').value;
    };

    const copyDesignerJson = async () => {
        clearBox('wf-dz-error');
        const text = el('wf-dz-preview').value;
        if (!text.trim()) { setBoxError('wf-dz-error', t('GenerateFirst', 'Generate the Definition JSON first.')); return; }
        if (isManual() && !isValidJson(text)) { setBoxError('wf-dz-error', t('InvalidJson', 'The JSON is not valid.')); return; }
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
            } else {
                const ta = el('wf-dz-preview'); ta.removeAttribute('readonly'); ta.select();
                document.execCommand('copy');
                if (!isManual()) ta.setAttribute('readonly', 'readonly');
            }
            notify('success', t('JsonCopied', 'Definition JSON copied to clipboard.'));
        } catch (_e) {
            notify('error', t('CopyFailed', 'Could not copy to clipboard.'));
        }
    };

    const applyManualToggle = () => {
        const ta = el('wf-dz-preview');
        if (isManual()) ta.removeAttribute('readonly');
        else ta.setAttribute('readonly', 'readonly');
    };

    const resetDesigner = () => {
        clearBox('wf-dz-error');
        Object.entries(DESIGNER_DEFAULTS).forEach(([id, v]) => { if (el(id)) el(id).value = v; });
        if (el('wf-dz-steptype')) el('wf-dz-steptype').value = 'approval';
        if (el('wf-dz-approvalmode')) el('wf-dz-approvalmode').value = 'candidate_principals';
        if (el('wf-dz-commentrequired')) el('wf-dz-commentrequired').checked = false;
        if (el('wf-dz-evidencerequired')) el('wf-dz-evidencerequired').checked = false;
        if (el('wf-dz-manual')) el('wf-dz-manual').checked = false;
        applyManualToggle();
        el('wf-dz-preview').value = '';
    };

    // ---- Publish modal (fed by Designer or used directly) ----
    const openPublishModal = (json, schema, expression) => {
        clearBox('wf-publish-error'); hide(el('wf-publish-result'));
        el('wf-publish-form').reset();
        el('wf-publish-definitionid').value = definitionId;
        el('wf-publish-title-code').textContent = el('wf-meta-code')?.textContent || '';
        if (json != null) el('wf-publish-json').value = json;
        if (schema) el('wf-publish-schema').value = schema;
        if (expression) el('wf-publish-expression').value = expression;
        new bootstrap.Modal(el('wf-publish-modal')).show();
    };

    const useInPublish = () => {
        const json = resolveDesignerJson();
        if (json == null) return;
        openPublishModal(json, val('wf-dz-schema') || 'workflow_schema_v1', val('wf-dz-expression') || 'workflow_expr_v1');
    };

    const submitPublish = async () => {
        clearBox('wf-publish-error');
        const jsonText = el('wf-publish-json').value;
        if (!jsonText.trim()) { setBoxError('wf-publish-error', t('DefinitionJsonRequired', 'Definition JSON is required.')); return; }
        if (!isValidJson(jsonText)) { setBoxError('wf-publish-error', t('InvalidJson', 'The JSON is not valid.')); return; }
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
        const btn = el('wf-publish-submit'); btn.disabled = true;
        const res = await api.publishDefinition(definitionId, payload);
        btn.disabled = false;
        if (!res.ok) { setBoxError('wf-publish-error', failureMessage(res)); return; }
        const d = res.data || {};
        el('wf-publish-result').innerHTML = `
            <div class="alert alert-success mb-0">
                <div class="fw-medium mb-1">${escapeHtml(t('PublishSucceeded', 'Published successfully.'))}</div>
                <div>${escapeHtml(t('VersionNumber', 'Version Number'))}: <strong>${escapeHtml(d.versionNumber)}</strong></div>
                <div>${escapeHtml(t('Immutable', 'Immutable'))}: ${statusBadge(d.isImmutable ? 'Immutable' : 'Draft')}</div>
            </div>`;
        show(el('wf-publish-result'));
        notify('success', t('PublishSucceeded', 'Published successfully.'));
        loadMeta();
        loadVersions();
    };

    const loadedOnce = new Set();
    document.addEventListener('DOMContentLoaded', () => {
        loadMeta();
        const initialTab = normalizeInitialTab();
        loadVersions(); loadedOnce.add('versions');
        document.querySelectorAll('button[data-bs-toggle="tab"]').forEach((tab) => {
            tab.addEventListener('shown.bs.tab', (ev) => {
                const key = ev.target?.getAttribute('data-wf-tab');
                if (!key || loadedOnce.has(key)) return;
                loadedOnce.add(key);
                if (key === 'instances') loadInstances();
                else if (key === 'sla') loadSla();
            });
        });
        if (initialTab !== 'versions') activateTab(initialTab);
        el('wf-ver-refresh')?.addEventListener('click', loadVersions);
        el('wf-inst-refresh')?.addEventListener('click', loadInstances);
        el('wf-sla-refresh')?.addEventListener('click', loadSla);
        el('wf-ver-rows')?.addEventListener('click', (ev) => {
            const b = ev.target.closest('.wf-ver-view');
            if (b) openVersionModal(b.dataset.id);
        });

        // Designer Lite wiring
        el('wf-dz-generate')?.addEventListener('click', generateDesignerJson);
        el('wf-dz-copy')?.addEventListener('click', copyDesignerJson);
        el('wf-dz-usepublish')?.addEventListener('click', useInPublish);
        el('wf-dz-reset')?.addEventListener('click', resetDesigner);
        el('wf-dz-manual')?.addEventListener('change', applyManualToggle);
        el('wf-publish-submit')?.addEventListener('click', submitPublish);
    });
})();
