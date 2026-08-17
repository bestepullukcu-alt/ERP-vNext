'use strict';

(function () {
    const root = document.getElementById('rd-attributes-page');
    if (!root) return;

    const setCode = root.dataset.setCode;
    const api = window.ReferenceDataApi;
    const permissions = window.ReferenceDataPermissions || { can: () => true, apply: (el, _cap, stateAllowed) => { if (el) el.disabled = stateAllowed === false; return stateAllowed !== false; }, guard: () => true };

    const defsBody = document.getElementById('rd-attribute-definitions-body');
    const valuesHead = document.getElementById('rd-value-attributes-head');
    const valuesBody = document.getElementById('rd-value-attributes-body');
    const statusEl = document.getElementById('rd-attributes-status');
    const emptyEl = document.getElementById('rd-attributes-empty');
    const errorEl = document.getElementById('rd-attributes-error');
    const cardEl = document.getElementById('rd-attributes-card');
    const stepperEl = document.getElementById('rd-attrs-stepper');
    const defsDirtyBadge = document.getElementById('rd-defs-dirty');
    const valuesDirtyBadge = document.getElementById('rd-values-dirty');

    const addDefBtn = document.getElementById('rd-attrs-add-def');
    const saveDefsBtn = document.getElementById('rd-attrs-save-defs');
    const saveValuesBtn = document.getElementById('rd-attrs-save-values');
    const prevBtn = document.getElementById('rd-attrs-prev');
    const refreshBtn = document.getElementById('rd-attrs-refresh');

    let currentSet = null;
    let draftVersion = null;
    let versionValues = [];
    let definitions = [];
    let defsDirty = false;
    let valuesDirty = false;
    let defsTouchedSinceRender = false;
    let stepper = null;

    const show = (el, on) => el && el.classList.toggle('d-none', !on);
    const normalize = (value) => String(value || '').trim().toLowerCase();
    const text = (value) => value == null || String(value).trim() === '' ? '-' : String(value);
    const tt = (key, fallback) => {
        const value = (window.L10n || {})[key];
        return typeof value === 'string' && value.trim() ? value : fallback;
    };
    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const noDraftReason = 'An active draft version is required.';
    const retiredSetReason = permissions.retiredSetReason || 'This reference data set is retired. Changes are disabled.';
    const isRetiredSet = (setInfo) => (typeof permissions.isRetiredSet === 'function'
        ? permissions.isRetiredSet(setInfo)
        : normalize(setInfo?.status || setInfo?.Status) === 'retired');
    const isTruthy = (value) => ['true', '1', 'yes', 'evet', 'on'].includes(String(value ?? '').trim().toLowerCase());
    const toDateInputValue = (raw, withTime) => {
        if (!raw) return '';
        const date = new Date(raw);
        if (Number.isNaN(date.getTime())) return String(raw);
        const iso = date.toISOString();
        return withTime ? iso.slice(0, 16) : iso.slice(0, 10);
    };
    const applySetGate = () => {
        const retired = isRetiredSet(currentSet);
        if (typeof permissions.setGlobalBlock === 'function') {
            permissions.setGlobalBlock(retired, retiredSetReason);
        }
        if (retired) {
            setStatus(retiredSetReason, 'info');
        }
        return retired;
    };

    const setDraftActions = (enabled, reason) => {
        permissions.apply(addDefBtn, 'canUpdateVersion', enabled, reason || noDraftReason);
        permissions.apply(saveDefsBtn, 'canUpdateVersion', enabled, reason || noDraftReason);
        permissions.apply(saveValuesBtn, 'canUpdateVersion', enabled && versionValues.length > 0, versionValues.length > 0 ? reason : 'Draft values are required.');
    };

    // Only the page-header Refresh button is toggled here; the Save buttons now live
    // inside their cards and hide automatically when the cards are hidden (no-draft state).
    const setHeaderActionsVisible = (visible) => {
        if (refreshBtn) refreshBtn.classList.toggle('d-none', !visible);
    };

    const updateDirtyBadges = () => {
        if (defsDirtyBadge) defsDirtyBadge.classList.toggle('d-none', !defsDirty);
        if (valuesDirtyBadge) valuesDirtyBadge.classList.toggle('d-none', !valuesDirty);
    };
    const markDefsDirty = () => { defsDirty = true; updateDirtyBadges(); };
    const markValuesDirty = () => { valuesDirty = true; updateDirtyBadges(); };
    const resetDirty = () => { defsDirty = false; valuesDirty = false; updateDirtyBadges(); };

    const renderEmptyState = ({ icon, title, description, actionsHtml }) => {
        show(cardEl, false);
        if (defsBody) defsBody.innerHTML = '';
        if (valuesHead) valuesHead.innerHTML = '';
        if (valuesBody) valuesBody.innerHTML = '';
        if (!emptyEl) return;
        emptyEl.innerHTML = `
            <div class="card">
                <div class="card-body text-center py-5">
                    <i class="bx ${icon} mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>
                    <h5 class="mb-2">${title}</h5>
                    <p class="text-muted mb-4 mx-auto" style="max-width:560px;">${description}</p>
                    <div class="d-flex justify-content-center gap-2 flex-wrap">${actionsHtml || ''}</div>
                </div>
            </div>`;
        show(emptyEl, true);
    };

    const renderNoDraft = (setId) => {
        draftVersion = null;
        definitions = [];
        versionValues = [];
        setDraftActions(false, noDraftReason);
        setHeaderActionsVisible(false);

        const workspaceHref = `/Platform/ReferenceData/Sets/${setId}`;
        const openWorkspaceBtn = `<a class="btn btn-primary" href="${workspaceHref}"><i class="bx bx-folder-open me-1"></i>${escapeHtml(tt('OpenSetWorkspace', 'Open Set Workspace'))}</a>`;

        if (isRetiredSet(currentSet)) {
            renderEmptyState({
                icon: 'bx-archive',
                title: escapeHtml(tt('NoDraftTitle', 'No editable draft version')),
                description: escapeHtml(tt('NoDraftRetired', 'This set is retired; new drafts cannot be created.')),
                actionsHtml: openWorkspaceBtn
            });
            return;
        }

        const hasPublished = !!(currentSet?.publishedVersionId || currentSet?.PublishedVersionId);
        const hint = hasPublished
            ? tt('NoDraftHintFromPublished', 'You can create a new draft from the published version.')
            : tt('NoDraftHintNoVersions', 'This set has no versions yet. Create the first draft.');
        const description = `${escapeHtml(tt('NoDraftDescription', 'Attributes can only be edited on a draft version.'))}<br><span class="fw-medium text-heading">${escapeHtml(setCode)}</span> — ${escapeHtml(hint)}`;

        renderEmptyState({
            icon: 'bx-edit',
            title: escapeHtml(tt('NoDraftTitle', 'No editable draft version')),
            description,
            actionsHtml: openWorkspaceBtn
        });
    };

    let statusTimer = null;
    const setStatus = (message, level, autoDismiss = false) => {
        if (statusTimer) { clearTimeout(statusTimer); statusTimer = null; }
        if (!statusEl) return;
        if (!message) {
            statusEl.className = 'alert alert-info d-none mb-3';
            statusEl.textContent = '';
            return;
        }

        const css = level === 'error' ? 'danger' : level === 'success' ? 'success' : 'info';
        statusEl.className = `alert alert-${css} mb-3`;
        statusEl.textContent = message;
        if (autoDismiss) {
            statusTimer = setTimeout(() => setStatus(null), 3000);
        }
    };

    const resolveSet = async () => {
        const data = await api.getSets(`?search=${encodeURIComponent(setCode)}&status=&scope_type=&page=1&page_size=100&sort=-createdAt`);
        const items = data?.items || data?.Items || [];
        const candidate = items.find((x) => normalize(x.setCode || x.SetCode) === normalize(setCode)) || null;
        if (!candidate) return null;
        return api.getSet(candidate.setId || candidate.SetId);
    };

    const renderDefinitions = () => {
        if (!defsBody) return;
        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
        const disabled = readOnly ? 'disabled' : '';
        if (!definitions.length) {
            const addBtn = readOnly
                ? ''
                : `<button type="button" class="btn btn-primary rd-def-add-empty"><i class="bx bx-plus me-1"></i>${escapeHtml(tt('AddDefinition', 'Add Definition'))}</button>`;
            defsBody.innerHTML = stepNotice({
                icon: 'bx-slider-alt',
                title: escapeHtml(tt('NoDefinitionsTitle', 'No attribute definitions')),
                description: `<span class="fw-medium text-heading">${escapeHtml(setCode)}</span> — ${escapeHtml(tt('NoDefinitionsYet', 'No attribute definitions yet. Add the first field with Add Definition.'))}`,
                actionsHtml: addBtn
            });
            permissions.apply(saveDefsBtn, 'canUpdateVersion', true);
            return;
        }

        defsBody.innerHTML = definitions.map((item, index) => {
            const type = String(item.dataType || 'string').toLowerCase();
            const required = item.isRequired ? 'checked' : '';
            return `<tr>
                <td><input class="form-control form-control-sm rd-def-code" data-index="${index}" maxlength="128" value="${text(item.attributeCode) === '-' ? '' : escapeHtml(item.attributeCode)}" ${disabled} /></td>
                <td><input class="form-control form-control-sm rd-def-name" data-index="${index}" maxlength="256" value="${text(item.displayName) === '-' ? '' : escapeHtml(item.displayName)}" ${disabled} /></td>
                <td>
                    <select class="form-select form-select-sm rd-def-type" data-index="${index}" ${disabled}>
                        <option value="string" ${type === 'string' ? 'selected' : ''}>string</option>
                        <option value="number" ${type === 'number' ? 'selected' : ''}>number</option>
                        <option value="decimal" ${type === 'decimal' ? 'selected' : ''}>decimal</option>
                        <option value="boolean" ${type === 'boolean' ? 'selected' : ''}>boolean</option>
                        <option value="date" ${type === 'date' ? 'selected' : ''}>date</option>
                        <option value="datetime" ${type === 'datetime' ? 'selected' : ''}>datetime</option>
                    </select>
                </td>
                <td><input type="checkbox" class="form-check-input rd-def-required" data-index="${index}" ${required} ${disabled} /></td>
                <td>
                    <a href="javascript:;" class="btn btn-icon btn-text-danger delete-record rd-def-remove ${disabled ? 'disabled' : ''}" data-index="${index}" aria-label="${escapeHtml(tt('RemoveAction', 'Remove'))}" title="${escapeHtml(tt('RemoveAction', 'Remove'))}">
                        <i class="icon-base bx bx-trash icon-md"></i>
                    </a>
                </td>
            </tr>`;
        }).join('');
        permissions.apply(saveDefsBtn, 'canUpdateVersion', true);
    };

    const markRequiredEmpties = () => {
        document.querySelectorAll('.rd-vattr.rd-vattr-required').forEach((el) => {
            if (el.type === 'checkbox') return;
            el.classList.toggle('is-invalid', !String(el.value || '').trim());
        });
    };

    const valueWizardHref = () => {
        const setId = currentSet?.setId || currentSet?.SetId;
        const versionId = draftVersion?.versionId || draftVersion?.VersionId;
        return (setId && versionId)
            ? `/Platform/ReferenceData/Sets/${setId}/DraftWizard?mode=resume&versionId=${encodeURIComponent(versionId)}`
            : '#';
    };

    // Centered empty-state notice rendered inside a stepper step table (mirrors the no-draft card style).
    const stepNotice = ({ icon, title, description, actionsHtml }) => `
        <tr><td colspan="99" class="border-0">
            <div class="text-center py-5">
                <i class="bx ${icon} mb-3" style="font-size:3rem;line-height:1;color:var(--bs-secondary-color,#a7acb2);"></i>
                <h6 class="mb-2">${title}</h6>
                <p class="text-muted mb-4 mx-auto" style="max-width:520px;">${description}</p>
                <div class="d-flex justify-content-center gap-2 flex-wrap">${actionsHtml || ''}</div>
            </div>
        </td></tr>`;

    const renderValueAttributes = () => {
        if (!valuesBody) return;
        const readOnly = typeof permissions.isBlocked === 'function' && permissions.isBlocked();
        const disabled = readOnly ? 'disabled' : '';

        // No values yet → direct the user to add values in the Draft Wizard first.
        if (!versionValues.length) {
            if (valuesHead) valuesHead.innerHTML = '';
            valuesBody.innerHTML = stepNotice({
                icon: 'bx-data',
                title: escapeHtml(tt('NoValuesTitle', 'No values yet')),
                description: `<span class="fw-medium text-heading">${escapeHtml(setCode)}</span> — ${escapeHtml(tt('NoValuesYet', 'This draft has no values yet. Add values in the Draft Wizard first.'))}`,
                actionsHtml: `<a class="btn btn-primary" href="${valueWizardHref()}"><i class="bx bx-spreadsheet me-1"></i>${escapeHtml(tt('OpenDraftWizard', 'Open Draft Wizard'))}</a>`
            });
            permissions.apply(saveValuesBtn, 'canUpdateVersion', false, 'Draft values are required.');
            return;
        }

        // Values exist but no attribute definitions → ask to define fields first.
        if (!definitions.length) {
            if (valuesHead) valuesHead.innerHTML = '';
            valuesBody.innerHTML = stepNotice({
                icon: 'bx-list-ul',
                title: escapeHtml(tt('NoDefinitionsTitle', 'No attribute definitions')),
                description: escapeHtml(tt('NoDefinitionsYet', 'No attribute definitions yet. Add the first field with Add Definition.')),
                actionsHtml: ''
            });
            permissions.apply(saveValuesBtn, 'canUpdateVersion', false, 'Define attributes first.');
            return;
        }

        if (valuesHead) {
            valuesHead.innerHTML = `<tr>
                <th style="width:200px;">${escapeHtml(tt('ValueColumn', 'Value'))}</th>
                ${definitions.map((def) => {
                const star = def.isRequired ? ' <span class="text-danger">*</span>' : '';
                const dataType = escapeHtml(String(def.dataType || 'string').toLowerCase());
                return `<th>${escapeHtml(def.displayName || def.attributeCode)}${star} <small class="text-muted">(${dataType})</small></th>`;
            }).join('')}
            </tr>`;
        }

        valuesBody.innerHTML = versionValues.map((item, vindex) => {
            const cells = definitions.map((def) => {
                const attrCode = def.attributeCode;
                const type = String(def.dataType || 'string').toLowerCase();
                const raw = item.attributes ? (item.attributes[attrCode] ?? '') : '';
                const dataAttrs = `data-vindex="${vindex}" data-attr="${escapeHtml(attrCode)}" data-type="${escapeHtml(type)}"`;
                const requiredCls = def.isRequired ? 'rd-vattr-required' : '';
                if (type === 'boolean') {
                    const checked = isTruthy(raw) ? 'checked' : '';
                    return `<td class="align-middle"><input type="checkbox" class="form-check-input rd-vattr ${requiredCls}" ${dataAttrs} ${checked} ${disabled}></td>`;
                }
                if (type === 'date') {
                    return `<td><input type="date" class="form-control form-control-sm rd-vattr ${requiredCls}" ${dataAttrs} value="${escapeHtml(toDateInputValue(raw, false))}" ${disabled}></td>`;
                }
                if (type === 'datetime') {
                    return `<td><input type="datetime-local" class="form-control form-control-sm rd-vattr ${requiredCls}" ${dataAttrs} value="${escapeHtml(toDateInputValue(raw, true))}" ${disabled}></td>`;
                }
                if (type === 'number' || type === 'decimal') {
                    const step = type === 'decimal' ? 'any' : '1';
                    return `<td><input type="number" step="${step}" class="form-control form-control-sm rd-vattr ${requiredCls}" ${dataAttrs} value="${escapeHtml(raw)}" ${disabled}></td>`;
                }
                return `<td><input type="text" class="form-control form-control-sm rd-vattr ${requiredCls}" ${dataAttrs} maxlength="512" value="${escapeHtml(raw)}" ${disabled}></td>`;
            }).join('');
            return `<tr><td class="fw-semibold align-middle">${escapeHtml(item.code)}</td>${cells}</tr>`;
        }).join('');

        markRequiredEmpties();
        permissions.apply(saveValuesBtn, 'canUpdateVersion', true);
    };

    const syncDefinitions = () => {
        definitions = definitions.map((item, index) => ({
            attributeCode: (document.querySelector(`.rd-def-code[data-index="${index}"]`)?.value || '').trim(),
            displayName: (document.querySelector(`.rd-def-name[data-index="${index}"]`)?.value || '').trim(),
            dataType: (document.querySelector(`.rd-def-type[data-index="${index}"]`)?.value || 'string').trim().toLowerCase(),
            isRequired: !!document.querySelector(`.rd-def-required[data-index="${index}"]`)?.checked
        }));
    };

    const validateDefinitions = () => {
        const seen = new Set();
        for (const item of definitions) {
            if (!item.attributeCode) return tt('AttributeCodeRequired', 'Attribute code is required.');
            if (!item.displayName) return tt('DisplayNameRequired', 'Display name is required.');
            const normalized = normalize(item.attributeCode);
            if (seen.has(normalized)) return `${tt('DuplicateAttributeCode', 'Duplicate attribute code:')} ${item.attributeCode}`;
            seen.add(normalized);
        }

        return null;
    };

    const syncValueAttributes = () => {
        versionValues = versionValues.map((item, vindex) => {
            const attributes = {};
            document.querySelectorAll(`.rd-vattr[data-vindex="${vindex}"]`).forEach((el) => {
                const attr = el.getAttribute('data-attr');
                const type = el.getAttribute('data-type');
                if (!attr) return;
                if (type === 'boolean') {
                    attributes[attr] = el.checked ? 'true' : 'false';
                    return;
                }
                const value = String(el.value || '').trim();
                if (value) attributes[attr] = value;
            });
            return { ...item, attributes: Object.keys(attributes).length ? attributes : null };
        });
    };

    const sortValues = () => {
        versionValues.sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0) || String(a.code).localeCompare(String(b.code)));
    };

    const load = async () => {
        setStatus(null);
        show(errorEl, false);
        show(emptyEl, false);
        show(cardEl, true);
        setDraftActions(false, noDraftReason);
        setHeaderActionsVisible(true);

        currentSet = await resolveSet();
        if (typeof permissions.clearGlobalBlock === 'function') {
            permissions.clearGlobalBlock();
        }
        if (!currentSet) {
            renderEmptyState({
                icon: 'bx-error-circle',
                title: escapeHtml(tt('SetNotFoundTitle', 'Set not found')),
                description: `${escapeHtml(tt('SetNotFoundDescription', 'The requested reference data set was not found:'))} <span class="fw-medium text-heading">${escapeHtml(setCode)}</span>`,
                actionsHtml: `<a class="btn btn-label-secondary" href="/Platform/ReferenceData"><i class="bx bx-arrow-back me-1"></i>${escapeHtml(tt('BackToSets', 'Back to Sets'))}</a>`
            });
            setDraftActions(false, 'Set must be loaded before editing attributes.');
            return;
        }
        const retired = applySetGate();

        const draftVersionId = currentSet.activeDraftVersionId || currentSet.ActiveDraftVersionId;
        const setId = currentSet.setId || currentSet.SetId;
        const crumb = document.getElementById('rd-attrs-crumb-set');
        if (crumb && setId) crumb.innerHTML = `<a href="/Platform/ReferenceData/Sets/${setId}">${escapeHtml(setCode)}</a>`;
        if (!draftVersionId) {
            renderNoDraft(setId);
            return;
        }

        const [version, defsPayload, valuesPayload] = await Promise.all([
            api.getVersion(draftVersionId),
            api.getVersionAttributeDefinitions(draftVersionId),
            api.getVersionValues(draftVersionId)
        ]);

        draftVersion = version;
        definitions = (defsPayload?.items || defsPayload?.Items || []).map((item) => ({
            attributeCode: item.attributeCode || item.AttributeCode || '',
            displayName: item.displayName || item.DisplayName || '',
            dataType: item.dataType || item.DataType || 'string',
            isRequired: item.isRequired ?? item.IsRequired ?? false
        }));

        versionValues = (valuesPayload?.items || valuesPayload?.Items || []).map((item) => ({
            code: item.code || item.Code || '',
            label: item.label || item.Label || '',
            description: item.description || item.Description || '',
            isActive: item.isActive ?? item.IsActive ?? true,
            sortOrder: item.sortOrder ?? item.SortOrder ?? 0,
            parentValueCode: item.parentValueCode || item.ParentValueCode || null,
            attributes: item.attributes || item.Attributes || null
        }));
        sortValues();

        renderDefinitions();
        renderValueAttributes();
        resetDirty();
        defsTouchedSinceRender = false;
        permissions.apply(addDefBtn, 'canUpdateVersion', true);
        setStatus(retired ? retiredSetReason : null, 'info');
    };

    const saveDefinitions = async () => {
        if (!draftVersion) {
            setStatus(noDraftReason, 'error');
            return false;
        }
        syncDefinitions();
        const validationError = validateDefinitions();
        if (validationError) {
            setStatus(validationError, 'error');
            return false;
        }

        const draftVersionId = draftVersion?.versionId || draftVersion?.VersionId;
        const token = draftVersion?.concurrencyToken || draftVersion?.ConcurrencyToken;
        const payload = {
            expected_concurrency_token: token,
            definitions: definitions.map((item) => ({
                attribute_code: item.attributeCode,
                display_name: item.displayName,
                data_type: item.dataType || 'string',
                is_required: !!item.isRequired
            }))
        };

        await api.replaceVersionAttributeDefinitions(draftVersionId, payload);
        await load();
        window.showToast?.(tt('DefinitionsSaved', 'Attribute definitions saved.'), 'success');
        setStatus(tt('DefinitionsSaved', 'Attribute definitions saved.'), 'success');
        return true;
    };

    const saveValueAttributes = async () => {
        if (!draftVersion) {
            setStatus(noDraftReason, 'error');
            return;
        }
        syncValueAttributes();
        const draftVersionId = draftVersion?.versionId || draftVersion?.VersionId;
        const token = draftVersion?.concurrencyToken || draftVersion?.ConcurrencyToken;
        const payload = {
            expected_concurrency_token: token,
            values: versionValues.map((item) => ({
                code: item.code,
                label: item.label,
                description: item.description || null,
                is_active: item.isActive !== false,
                sort_order: Number(item.sortOrder || 0),
                parent_value_code: item.parentValueCode || null,
                attributes: item.attributes || null
            }))
        };

        await api.replaceVersionValues(draftVersionId, payload);
        await load();
        window.showToast?.(tt('ValueAttributesSaved', 'Value attributes saved.'), 'success');
        setStatus(tt('ValueAttributesSaved', 'Value attributes saved.'), 'success');
    };

    const addDefinition = () => {
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        if (!draftVersion) {
            setStatus(noDraftReason, 'error');
            return;
        }
        syncValueAttributes();
        definitions.push({
            attributeCode: '',
            displayName: '',
            dataType: 'string',
            isRequired: false
        });
        markDefsDirty();
        renderDefinitions();
        renderValueAttributes();
        defsTouchedSinceRender = false;
    };

    addDefBtn?.addEventListener('click', addDefinition);

    defsBody?.addEventListener('input', () => {
        markDefsDirty();
        defsTouchedSinceRender = true;
    });
    defsBody?.addEventListener('change', () => {
        markDefsDirty();
        syncValueAttributes();
        syncDefinitions();
        renderValueAttributes();
        defsTouchedSinceRender = false;
    });

    defsBody?.addEventListener('click', (event) => {
        if (event.target.closest('.rd-def-add-empty')) {
            addDefinition();
            return;
        }
        const removeBtn = event.target.closest('.rd-def-remove');
        if (!removeBtn) return;
        event.preventDefault();
        if (removeBtn.classList.contains('disabled')) return;
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        const index = Number(removeBtn.getAttribute('data-index'));
        if (!Number.isFinite(index)) return;
        syncDefinitions();
        syncValueAttributes();
        definitions.splice(index, 1);
        markDefsDirty();
        renderDefinitions();
        renderValueAttributes();
        defsTouchedSinceRender = false;
    });

    valuesBody?.addEventListener('input', () => {
        markValuesDirty();
        markRequiredEmpties();
    });
    valuesBody?.addEventListener('change', () => {
        markValuesDirty();
        markRequiredEmpties();
    });

    saveDefsBtn?.addEventListener('click', async () => {
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        try {
            saveDefsBtn.disabled = true;
            const ok = await saveDefinitions();
            if (ok && stepper) stepper.next();
        } catch (error) {
            if (error?.isHandled) return;
            setStatus(error?.message || tt('SaveFailed', 'Save failed.'), 'error');
        } finally {
            saveDefsBtn.disabled = !draftVersion;
        }
    });

    prevBtn?.addEventListener('click', () => stepper?.previous());

    saveValuesBtn?.addEventListener('click', async () => {
        if (!permissions.guard('canUpdateVersion', (message) => setStatus(message, 'error'))) return;
        try {
            saveValuesBtn.disabled = true;
            await saveValueAttributes();
        } catch (error) {
            if (error?.isHandled) return;
            setStatus(error?.message || tt('SaveFailed', 'Save failed.'), 'error');
        } finally {
            saveValuesBtn.disabled = !draftVersion || versionValues.length <= 0;
        }
    });

    refreshBtn?.addEventListener('click', () => {
        load().catch((error) => {
            if (error?.isHandled) return;
            show(cardEl, false);
            errorEl.textContent = `${tt('LoadFailed', 'Could not load the attribute workspace.')} ${error?.message || ''}`.trim();
            show(errorEl, true);
        });
    });

    if (stepperEl && window.Stepper) {
        stepper = new window.Stepper(stepperEl, { linear: false, animation: false });
        // When the user jumps to the value step, rebuild its columns from the latest
        // (possibly unsaved) definitions so the grid always matches the schema on screen.
        stepperEl.addEventListener('shown.bs-stepper', (event) => {
            if (event.detail?.to === 1 && defsTouchedSinceRender) {
                syncValueAttributes();
                syncDefinitions();
                renderValueAttributes();
                defsTouchedSinceRender = false;
            }
        });
    }

    load().catch((error) => {
        if (error?.isHandled) return;
        show(cardEl, false);
        errorEl.textContent = `${tt('LoadFailed', 'Could not load the attribute workspace.')} ${error?.message || ''}`.trim();
        show(errorEl, true);
    });
})();
