/**
 * Notification Templates - Details page (MOD-0027-FU02).
 * Read-only view + archive action + sandboxed server-side render preview of the SAVED template content.
 */
'use strict';

(function () {
    const apiBase = '/Platform/NotificationTemplates/api';
    const root = document.getElementById('templateDetailsRoot');
    if (!root) return;

    const templateId = root.dataset.templateId || '';
    const scopeTenantId = root.dataset.scopeTenantId || '';
    const L = () => window.L10n || {};
    let currentTemplate = null;

    const unwrap = (payload) => payload?.data ?? payload?.Data ?? null;
    const errorsOf = (payload) => payload?.errors || payload?.Errors || [];
    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.innerText = (value === null || value === undefined || value === '') ? '-' : String(value);
    };
    const showError = (message) => {
        const el = document.getElementById('detailsError');
        if (!el) return;
        el.textContent = message || L().ErrorOccurred || '';
        el.classList.remove('d-none');
    };

    const statusMap = () => ({
        'Draft': { title: L().StatusDraft || 'Draft', class: 'bg-label-warning' },
        'Active': { title: L().StatusActive || 'Active', class: 'bg-label-success' },
        'Archived': { title: L().StatusArchived || 'Archived', class: 'bg-label-secondary' }
    });

    const renderVariables = (variables) => {
        const host = document.getElementById('d-variables');
        const empty = document.getElementById('d-variablesEmpty');
        if (!host) return;
        host.innerHTML = '';
        (variables || []).forEach((variable) => {
            const row = document.createElement('div');
            row.className = 'd-flex align-items-center justify-content-between border rounded p-2';
            const name = document.createElement('span');
            name.className = 'fw-medium';
            name.textContent = variable.name;
            const meta = document.createElement('span');
            meta.className = 'badge bg-label-info';
            meta.textContent = variable.type + (variable.isRequired ? ' *' : '');
            row.appendChild(name);
            row.appendChild(meta);
            host.appendChild(row);
        });
        empty?.classList.toggle('d-none', (variables || []).length > 0);
    };

    // Representative sample value per variable so the preview renders a realistic message out-of-the-box
    // (mirrors how the real send fills the template) instead of showing blanks or a missing-variable 400.
    const sampleValueFor = (variable) => {
        const type = (variable.type || 'String').toLowerCase();
        const name = variable.name || '';
        if (type === 'number') return '123';
        if (type === 'boolean') return 'true';
        if (type === 'date') return new Date().toISOString();
        if (type === 'url') return 'https://app.diten.local/invite';
        // String: guess a natural-looking sample from the variable name.
        if (/id$/i.test(name)) return scopeTenantId || '00000000-0000-0000-0000-000000000000';
        if (/email/i.test(name)) return 'user@example.com';
        if (/(url|link)/i.test(name)) return 'https://app.diten.local/invite';
        if (/name/i.test(name)) return 'Acme Corporation';
        if (/(date|at$|time)/i.test(name)) return new Date().toISOString();
        return `Sample ${name}`;
    };

    const buildSampleInputs = (variables) => {
        const host = document.getElementById('previewSampleInputs');
        if (!host) return;
        host.innerHTML = '';
        (variables || []).forEach((variable) => {
            const wrapper = document.createElement('div');
            const label = document.createElement('label');
            label.className = 'form-label small mb-1';
            label.textContent = variable.name + (variable.isRequired ? ' *' : '');
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'form-control form-control-sm';
            input.dataset.variable = variable.name;
            // Pre-fill with a representative value; operator can still edit before re-previewing.
            input.value = sampleValueFor(variable);
            wrapper.appendChild(label);
            wrapper.appendChild(input);
            host.appendChild(wrapper);
        });
    };

    const renderPreview = async () => {
        if (!currentTemplate) return;
        const previewError = document.getElementById('previewError');
        const previewResult = document.getElementById('previewResult');
        previewError?.classList.add('d-none');
        const samples = {};
        document.querySelectorAll('#previewSampleInputs input[data-variable]').forEach((input) => {
            if (input.value !== '') samples[input.dataset.variable] = input.value;
        });
        const payload = {
            subjectTemplate: currentTemplate.subjectTemplate || '',
            bodyHtmlTemplate: currentTemplate.bodyHtmlTemplate || null,
            bodyTextTemplate: currentTemplate.bodyTextTemplate || null,
            variables: currentTemplate.variables || [],
            sampleVariables: samples
        };
        try {
            const res = await fetch(`${apiBase}/templates/render-preview`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            const body = await res.json();
            if (!res.ok) {
                previewResult?.classList.add('d-none');
                if (previewError) {
                    previewError.textContent = errorsOf(body).join(' ') || L().ErrorOccurred || '';
                    previewError.classList.remove('d-none');
                }
                return;
            }
            const rendered = unwrap(body);
            setText('previewSubject', rendered?.subject);
            const frame = document.getElementById('previewFrame');
            if (frame) frame.srcdoc = rendered?.bodyHtml || '';
            const text = document.getElementById('previewText');
            if (text) {
                text.textContent = rendered?.bodyText || '';
                text.classList.toggle('d-none', !rendered?.bodyText);
            }
            previewResult?.classList.remove('d-none');
        } catch (error) {
            console.error('[NotificationTemplates Details Preview] Failed.', error);
            if (previewError) {
                previewError.textContent = L().ErrorOccurred || '';
                previewError.classList.remove('d-none');
            }
        }
    };

    const archiveTemplate = () => {
        if (!currentTemplate || currentTemplate.status === 'Archived') return;
        window.showConfirm?.(L().ArchiveConfirm, async () => {
            try {
                const res = await fetch(`${apiBase}/templates/${templateId}/archive`, {
                    method: 'POST',
                    credentials: 'same-origin'
                });
                if (!res.ok) throw new Error('Archive failed.');
                window.showToast?.(L().TemplateArchived, 'success');
                window.location.href = '/Platform/NotificationTemplates';
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: currentTemplate.templateKey, type: 'danger', confirmButtonText: L().Archive });
    };

    const load = async () => {
        if (!templateId) return;
        try {
            const res = await fetch(`${apiBase}/templates/${templateId}`, { credentials: 'same-origin' });
            const body = await res.json();
            if (!res.ok) {
                showError(errorsOf(body).join(' '));
                return;
            }
            const dto = unwrap(body);
            if (!dto) { showError(); return; }
            currentTemplate = dto;
            setText('detailsTitle', dto.templateKey);
            setText('d-templateKey', dto.templateKey);
            setText('d-locale', dto.locale);
            setText('d-channel', dto.channel);
            setText('d-semanticVersion', dto.semanticVersion);
            setText('d-scope', dto.isPlatformDefault ? (L().ScopePlatformDefaults || 'Platform') : (L().ScopeTenantOverrides || 'Tenant'));
            setText('d-updatedAt', dto.updatedAt ? new Date(dto.updatedAt).toLocaleString(window.CurrentLanguage || undefined) : '-');
            setText('d-subjectTemplate', dto.subjectTemplate);
            setText('d-bodyHtmlTemplate', dto.bodyHtmlTemplate);
            setText('d-bodyTextTemplate', dto.bodyTextTemplate);
            const statusEl = document.getElementById('d-status');
            const status = statusMap()[dto.status] || { title: dto.status, class: 'bg-label-primary' };
            if (statusEl) {
                statusEl.className = `badge ${status.class}`;
                statusEl.innerText = status.title;
            }
            renderVariables(dto.variables);
            buildSampleInputs(dto.variables);
            // Auto-render once with the seeded sample values so the preview shows the real message immediately.
            void renderPreview();

            const editBtn = document.getElementById('btnEditTemplate');
            if (editBtn) {
                const suffix = dto.tenantId ? `?tenantId=${dto.tenantId}` : '';
                editBtn.href = `/Platform/NotificationTemplates/Edit/${templateId}${suffix}`;
                editBtn.classList.remove('d-none');
            }
            const archiveBtn = document.getElementById('btnArchiveTemplate');
            if (archiveBtn && dto.status !== 'Archived') {
                archiveBtn.classList.remove('d-none');
                archiveBtn.addEventListener('click', archiveTemplate);
            }
        } catch (error) {
            console.error('[NotificationTemplates Details] Load failed.', error);
            showError();
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        void load();
        document.getElementById('btnRenderPreview')?.addEventListener('click', renderPreview);
    });
})();
