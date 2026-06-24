'use strict';

// Workflow Visual Designer standalone page host.
(function () {
    const api = window.WorkflowApi;
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));
    const definitionId = window.WorkflowDefinitionId;

    const el = (id) => document.getElementById(id);
    const val = (id) => (el(id)?.value ?? '').trim();
    const show = (node) => node && node.classList.remove('d-none');
    const hide = (node) => node && node.classList.add('d-none');

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    };

    const notify = (kind, message) => {
        const type = kind === 'error' || kind === 'danger'
            ? 'error'
            : (kind === 'warning' || kind === 'info' ? kind : 'success');
        if (typeof window.showToast === 'function') {
            window.showToast(message, type);
            return;
        }
        console[type === 'error' ? 'error' : 'log'](message);
    };

    const failureMessage = (res) => {
        if (res.status === 403) return t('PermissionMissing', 'Permission missing or not assigned.');
        if (res.status === 0) return t('NetworkError', 'Cannot reach the workflow gateway.');
        if (res.status === 503) return t('GatewayUnavailable', 'Workflow gateway proxy unavailable.');
        return res.message || t('RequestFailed', 'Request failed.');
    };

    const isValidJson = (text) => {
        try {
            JSON.parse(text);
            return true;
        } catch (_e) {
            return false;
        }
    };

    const unwrapItems = (payload) => {
        if (Array.isArray(payload)) return payload;
        if (Array.isArray(payload?.data)) return payload.data;
        if (Array.isArray(payload?.data?.items)) return payload.data.items;
        if (Array.isArray(payload?.items)) return payload.items;
        return [];
    };

    const getJson = async (url) => {
        const response = await fetch(url, {
            headers: { 'Accept': 'application/json' },
            credentials: 'same-origin'
        });
        if (!response.ok) return [];
        return unwrapItems(await response.json());
    };

    const userLabel = (user) => {
        const name = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
        return name || user.fullName || user.displayName || user.email || user.userName || user.id;
    };

    // Active principal kind per field. The type toggle drives which options the dropdown lists;
    // already-selected chips of either kind persist so a step can still mix users and positions.
    const principalType = { 'wf-vd-candidates': 'user', 'wf-vd-escprincipals': 'user' };

    // Filters the dropdown to the field's active type (user/position), then applies the term match.
    const buildMatcher = (selectId) => (params, data) => {
        if (!data || !data.id) return data;
        if (!String(data.id).startsWith(`${principalType[selectId]}:`)) return null;
        const term = (params.term || '').trim().toLowerCase();
        if (!term) return data;
        return (data.text || '').toLowerCase().indexOf(term) > -1 ? data : null;
    };

    const initSelect2 = async () => {
        if (!window.jQuery || !window.jQuery.fn?.select2) return;
        const $ = window.jQuery;

        $('#wf-vd-steptype').select2({
            width: '100%',
            minimumResultsForSearch: Infinity,
            dropdownParent: $('#wf-vd-props')
        });

        let principalOptions = [];
        try {
            const [users, positions] = await Promise.all([
                getJson('/Platform/Workflow/lookup/users'),
                getJson('/Platform/Workflow/lookup/positions')
            ]);

            const userOptions = users
                .filter((user) => user?.id)
                .map((user) => ({ id: `user:${user.id}`, text: userLabel(user) }));
            const positionOptions = positions
                .filter((position) => position?.id)
                .map((position) => ({ id: `position:${position.id}`, text: `${position.code || ''} ${position.name || position.id}`.trim() }));

            // Single flat pool; the per-field matcher hides the non-active kind from the dropdown.
            principalOptions = [...userOptions, ...positionOptions];
        } catch (_e) {
            notify('warning', t('RequestFailed', 'Request failed.'));
        }

        const initPrincipalField = (selectId, placeholderKey, placeholderFallback) => {
            $(`#${selectId}`).select2({
                width: '100%',
                dropdownParent: $('#wf-vd-props'),
                placeholder: t(placeholderKey, placeholderFallback),
                closeOnSelect: false,
                data: principalOptions,
                matcher: buildMatcher(selectId)
            });

            $(`input[name="${selectId}-type"]`).on('change', function () {
                principalType[selectId] = this.value === 'position' ? 'position' : 'user';
            });
        };

        initPrincipalField('wf-vd-candidates', 'CandidatePrincipalIds', 'Candidate Principal IDs');
        initPrincipalField('wf-vd-escprincipals', 'EscalationPrincipalIds', 'Escalation Principal IDs');
    };

    const setBoxError = (id, msg) => {
        const box = el(id);
        if (!box) return;
        box.textContent = msg;
        show(box);
    };
    const clearBox = (id) => hide(el(id));

    const loadMeta = async () => {
        if (!api || !definitionId) return;
        const res = await api.getDefinition(definitionId);
        if (!res.ok) {
            notify('error', failureMessage(res));
            return;
        }

        const d = res.data || {};
        if (el('wf-meta-code')) el('wf-meta-code').textContent = d.templateCode || '-';
        if (el('wf-meta-name')) el('wf-meta-name').textContent = d.name || '-';
    };

    const submitPublish = async () => {
        clearBox('wf-publish-error');
        const jsonText = el('wf-publish-json')?.value || '';
        if (!jsonText.trim()) {
            setBoxError('wf-publish-error', t('DefinitionJsonRequired', 'Definition JSON is required.'));
            return;
        }
        if (!isValidJson(jsonText)) {
            setBoxError('wf-publish-error', t('InvalidJson', 'The JSON is not valid.'));
            return;
        }
        if (!val('wf-publish-schema')) {
            setBoxError('wf-publish-error', t('SchemaVersionRequired', 'Schema Version is required.'));
            return;
        }
        if (!val('wf-publish-expression')) {
            setBoxError('wf-publish-error', t('ExpressionVersionRequired', 'Expression Version is required.'));
            return;
        }

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

        if (!res.ok) {
            setBoxError('wf-publish-error', failureMessage(res));
            return;
        }

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

    document.addEventListener('DOMContentLoaded', () => {
        loadMeta();
        initSelect2();
        el('wf-publish-submit')?.addEventListener('click', submitPublish);
    });
})();
