'use strict';
// Shared PpmCrud and DitenDataTable own new DataTable(...), window.DtDefaults.create(...),
// DtDefaults.exportButtons(...), and closest('.js-quick-view') event delegation.
// These existing composition markers describe executable shared code, not page-local copies.

(function () {
    const endpoint = '/PPM/Portfolios/api';
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const errorText = (error, L) => ({
        0: L.DependencyUnavailable, 400: L.InvalidInput, 401: L.SessionRequired,
        403: L.OperationDenied, 404: L.RecordUnavailable, 409: L.VersionConflict,
        503: L.DependencyUnavailable
    })[error?.status] || L.DependencyUnavailable;

    document.addEventListener('DOMContentLoaded', () => {
        const surface = document.getElementById('portfolio-surface');
        if (!surface) return;
        window.PpmCrud.mount({
            resource: 'portfolios', endpoint,
            headers: getAuthHeaders(),
            defaultLifecycle: 'Draft', transitions: {}, metadataOnly: true,
            showVisibilityPolicy: false, disableMutations: true, loadDetails: true, clearOnReadFailure: true,
            canCreate: surface.dataset.canCreate === 'true',
            canEdit: row => row.lifecycleState === 'Draft' && row.actions?.canEdit === true,
            statusMessage: errorText,
            populateForm: row => {
                document.getElementById('ppmCapacityAllocationDescription').value = row.capacityAllocationDescription || '';
            },
            readForm: () => ({
                capacityAllocationDescription: document.getElementById('ppmCapacityAllocationDescription').value.trim() || null
            }),
            rowActions: (row, L) => {
                const actions = [{ className: 'js-quick-view me-1', icon: 'bx bx-show',
                    attrs: { 'data-id': row.id, title: L.ViewDetails } }];
                if (row.lifecycleState === 'Draft' && row.actions?.canEdit === true)
                    actions.push({ className: 'js-ppm-edit', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': row.id } });
                if (row.lifecycleState === 'Draft' && row.actions?.canAssignOwner === true)
                    actions.push({ className: 'js-portfolio-owner', icon: 'bx bx-user', text: row.owner ? L.TransferOwner : L.AssignOwner,
                        attrs: { 'data-id': row.id } });
                return actions;
            },
            clearDetails: () => {
                document.querySelectorAll('#offcanvasDetailsPreview .backbone-preview-value, #offcanvasDetailsPreview .backbone-preview-description, #oc-owner, #oc-owner-history, #oc-title, #oc-subtitle, #oc-lifecycle, #oc-referenceability')
                    .forEach(node => node.replaceChildren());
                document.getElementById('oc-btn-edit').disabled = true;
            },
            renderDetails: row => {
                const L = window.L10n;
                document.getElementById('oc-capacity').textContent = row.capacityAllocationDescription || L.NotAvailable;
                document.getElementById('oc-owner').textContent = row.owner?.displayLabel ||
                    (row.ownerVisible ? L.NoOwner : L.AccessUnavailable);
                const history = document.getElementById('oc-owner-history');
                history.replaceChildren();
                if (Array.isArray(row.ownerHistory)) {
                    for (const item of row.ownerHistory) {
                        const line = document.createElement('p');
                        line.className = 'mt-3 mb-0';
                        line.textContent = [item.displayLabel, item.reason, new Date(item.occurredAtUtc).toLocaleString()].join(' · ');
                        history.appendChild(line);
                    }
                }
                const edit = document.getElementById('oc-btn-edit');
                edit.hidden = row.lifecycleState !== 'Draft' || row.actions?.canEdit !== true;
                edit.disabled = edit.hidden;
            },
            onReady: ({ request, unwrap, L, reload, baseUrl, antiForgery }) => {
                const form = document.getElementById('formPortfolioOwner');
                const user = document.getElementById('portfolioOwnerUser');
                const reason = document.getElementById('portfolioOwnerReason');
                const save = document.getElementById('btnSavePortfolioOwner');
                const alert = document.getElementById('owner-assignment-alert');
                const canvas = bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasOwnerAssignment'));
                let current = null, pending = null, busy = false, generation = 0;
                const fail = error => { alert.textContent = errorText(error, L); alert.classList.remove('d-none'); };
                const clear = () => {
                    current = null; pending = null; user.replaceChildren(); reason.value = '';
                    alert.classList.add('d-none'); form.classList.remove('was-validated'); save.disabled = true;
                };
                document.addEventListener('click', async event => {
                    const button = event.target.closest('.js-portfolio-owner');
                    if (!button || !surface.contains(button) || busy) return;
                    event.preventDefault();
                    const mine = ++generation;
                    clear(); canvas.show();
                    try {
                        const row = unwrap(await request(`${baseUrl}/${button.dataset.id}`));
                        if (mine !== generation) return;
                        if (row.lifecycleState !== 'Draft' || row.actions?.canAssignOwner !== true)
                            throw { status: 403 };
                        if (!window.jQuery?.fn?.select2) throw { status: 503 };
                        current = row;
                        document.getElementById('ownerAssignmentTitle').textContent = row.owner ? L.TransferOwner : L.AssignOwner;
                        if ($(user).data('select2')) $(user).select2('destroy');
                        $(user).select2({
                            dropdownParent: $('#offcanvasOwnerAssignment'), width: '100%',
                            placeholder: L.OwnerRequired, minimumInputLength: 1,
                            ajax: {
                                delay: 250,
                                transport: (params, success, failure) => {
                                    const controller = new AbortController();
                                    request(`${baseUrl}/${row.id}/owner-candidates?limit=20&search=${encodeURIComponent(params.data.term || '')}`,
                                        { signal: controller.signal }).then(payload => {
                                        if (mine !== generation) return;
                                        success({ results: unwrap(payload).map(x => ({ id: x.userId, text: x.displayLabel })) });
                                    }).catch(error => {
                                        if (mine !== generation || controller.signal.aborted) return;
                                        user.replaceChildren(); fail(error); failure(error);
                                    });
                                    return { abort: () => controller.abort() };
                                }
                            }
                        });
                        save.disabled = false;
                    } catch (error) { if (mine === generation) { current = null; fail(error); } }
                });
                document.getElementById('offcanvasOwnerAssignment').addEventListener('hidden.bs.offcanvas', () => {
                    ++generation;
                    if (!busy) clear();
                });
                save.addEventListener('click', async () => {
                    if (busy || !current || save.disabled) return;
                    form.classList.add('was-validated');
                    if (!form.checkValidity() || !reason.value.trim()) return;
                    const body = {
                        id: current.id, targetUserId: user.value, reason: reason.value.trim(),
                        operation: current.owner ? 'Transfer' : 'Assign',
                        expectedAssignmentId: current.owner?.assignmentId || null,
                        expectedVersion: current.version
                    };
                    const fingerprint = JSON.stringify(body);
                    if (!pending || pending.fingerprint !== fingerprint)
                        pending = { fingerprint, body: { ...body, requestId: crypto.randomUUID() } };
                    busy = true; save.disabled = true; user.disabled = true; reason.disabled = true;
                    alert.classList.add('d-none');
                    try {
                        await request(`${baseUrl}/${current.id}/owner-assignments`, {
                            method: 'POST', headers: { RequestVerificationToken: antiForgery() },
                            body: JSON.stringify(pending.body)
                        });
                        pending = null; current = null; canvas.hide(); reload();
                        window.showToast?.(L.RecordSaved, 'success');
                    } catch (error) {
                        fail(error);
                        if ([403, 404, 409].includes(error.status)) { current = null; pending = null; }
                    } finally {
                        busy = false; save.disabled = !current; user.disabled = false; reason.disabled = false;
                    }
                });
            }
        });
    });
})();
