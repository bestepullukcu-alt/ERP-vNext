'use strict';

document.addEventListener('DOMContentLoaded', async () => {
    const root = document.getElementById('project-workspace');
    if (!root) return;
    const L = JSON.parse(document.getElementById('project-workspace-l10n')?.textContent || '{}');
    const text = (id, value) => {
        const node = document.getElementById(id);
        if (node) node.textContent = value ?? L.notAvailable;
    };
    const statusMessage = (status) => L[`error${status}`] || L.errorGeneric;
    const ensureAuthorized = (response) => {
        if (response.status !== 401) return;
        window.DtDefaults?.handleUnauthorized?.();
        const error = new Error(statusMessage(401));
        error.authHandled = true;
        throw error;
    };
    const alert = document.getElementById('workspace-alert');
    try {
        const response = await fetch(`/ppm/projects/api/${encodeURIComponent(root.dataset.projectId)}`, {
            credentials: 'same-origin',
            headers: { Accept: 'application/json' }
        });
        ensureAuthorized(response);
        if (!response.ok) {
            const error = new Error(statusMessage(response.status));
            error.status = response.status;
            throw error;
        }
        const envelope = await response.json();
        const project = envelope.data ?? envelope.Data;
        if (!project) throw new Error(L.errorGeneric);
        const get = (camel, pascal) => project[camel] ?? project[pascal];
        const code = get('code', 'Code');
        const name = get('name', 'Name');
        const description = get('description', 'Description');
        const lifecycle = get('lifecycleState', 'LifecycleState');
        const parentType = get('parentType', 'ParentType');
        const parentId = get('parentId', 'ParentId');
        const referenceable = Boolean(get('isReferenceable', 'IsReferenceable'));
        const referenceability = referenceable ? L.referenceable : L.notReferenceable;
        text('workspace-name', name);
        text('workspace-code', code);
        text('workspace-description', description);
        text('workspace-parent-type', parentType);
        const parentResource = parentType === 'Initiative' ? 'initiatives'
            : parentType === 'Program' ? 'programs' : null;
        if (parentResource && parentId) {
            const parentResponse = await fetch(
                `/ppm/${parentResource}/api/${encodeURIComponent(parentId)}`,
                { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            ensureAuthorized(parentResponse);
            if (!parentResponse.ok) {
                const parentError = new Error(statusMessage(parentResponse.status));
                parentError.status = parentResponse.status;
                throw parentError;
            }
            const parentEnvelope = await parentResponse.json();
            const parent = parentEnvelope.data ?? parentEnvelope.Data;
            const parentCode = parent?.code ?? parent?.Code;
            const parentName = parent?.name ?? parent?.Name;
            text('workspace-parent', parentCode && parentName
                ? `${parentCode} — ${parentName}`
                : L.notAvailable);
        } else {
            text('workspace-parent', L.notAvailable);
        }
        text('charter-code', code);
        text('charter-name', name);
        text('charter-lifecycle', lifecycle);
        text('charter-visibility', get('visibilityPolicyKey', 'VisibilityPolicyKey'));
        text('charter-referenceability', referenceability);
        text('charter-version', get('version', 'Version'));
        const lifecycleBadge = document.getElementById('workspace-lifecycle');
        lifecycleBadge.textContent = lifecycle;
        lifecycleBadge.classList.remove('d-none');
        const referenceBadge = document.getElementById('workspace-referenceability');
        referenceBadge.textContent = referenceability;
        referenceBadge.classList.remove('d-none');
        referenceBadge.classList.toggle('bg-label-success', referenceable);
    } catch (error) {
        if (error?.authHandled) return;
        if (error instanceof TypeError && navigator.onLine === false) {
            alert.textContent = L.error503 || L.errorGeneric;
            alert.classList.remove('d-none');
            return;
        }
        alert.textContent = error.message || L.errorGeneric;
        alert.classList.remove('d-none');
        text('workspace-name', L.errorGeneric);
    }
});
