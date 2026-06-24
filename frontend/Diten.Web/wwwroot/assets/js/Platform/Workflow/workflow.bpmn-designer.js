'use strict';

// MOD-0023 — Workflow Visual Designer (BPMN authoring layer ONLY).
// bpmn-js is the drawing surface; it is NOT a runtime engine. The diagram is mapped to our existing
// DefinitionJson contract and published through the existing endpoint. Runtime approve/reject/SLA logic
// is unchanged. This adapter writes to the page preview (#wf-dz-preview) and publish modal (#wf-publish-*).
(function () {
    const L = window.WorkflowL10n || {};
    const t = (key, fallback) => (L[key] != null ? L[key] : (fallback != null ? fallback : key));
    const el = (id) => document.getElementById(id);
    const definitionId = window.WorkflowDefinitionId;

    // ---- helpers ----
    const normalizeCsv = (raw) => {
        const seen = new Set(); const out = [];
        if (Array.isArray(raw)) {
            raw.forEach((p) => { const v = String(p || '').trim(); if (v && !seen.has(v)) { seen.add(v); out.push(v); } });
            return out;
        }
        (raw || '').split(',').forEach((p) => { const v = p.trim(); if (v && !seen.has(v)) { seen.add(v); out.push(v); } });
        return out;
    };
    const readSelectValues = (id) => {
        const node = el(id);
        if (!node) return [];
        if (node.multiple) return Array.from(node.selectedOptions || []).map((x) => x.value).filter(Boolean);
        return node.value ? [node.value] : [];
    };
    const setSelectValues = (id, values) => {
        const node = el(id);
        if (!node) return;
        const list = normalizeCsv(values);
        list.forEach((value) => {
            if (![...node.options].some((option) => option.value === value)) {
                node.add(new Option(value, value, true, true));
            }
        });
        if (node.multiple) {
            [...node.options].forEach((option) => { option.selected = list.includes(option.value); });
        } else if (list.length) {
            node.value = list[0];
        }
        if (window.jQuery) window.jQuery(node).trigger('change.select2');
    };
    const sanitizeCode = (raw) => (raw || '').toString().trim().replace(/[^A-Za-z0-9_-]+/g, '-').replace(/^-+|-+$/g, '') || 'step';
    const prettyJson = (obj) => JSON.stringify(obj, null, 2);
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
    const status = (kind, message) => {
        const box = el('wf-vd-status');
        if (!box) return;
        box.className = `alert alert-${kind === 'error' ? 'danger' : (kind === 'warning' ? 'warning' : 'success')}`;
        box.textContent = message;
        box.classList.remove('d-none');
    };
    const clearStatus = () => el('wf-vd-status')?.classList.add('d-none');

    // ---- BPMN subset policy ----
    const STRUCTURAL = new Set(['bpmn:Process', 'bpmn:Definitions', 'bpmn:Collaboration', 'bpmn:BPMNDiagram', 'bpmn:BPMNPlane']);
    const SUPPORTED = new Set(['bpmn:StartEvent', 'bpmn:EndEvent', 'bpmn:Task', 'bpmn:UserTask', 'bpmn:SequenceFlow']);
    const SOFT = new Set(['bpmn:ExclusiveGateway']); // accepted in the diagram, warned, not mapped to branches
    const TASK_TYPES = new Set(['bpmn:Task', 'bpmn:UserTask']);
    const STARTER_BPMN_XML = `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_Workflow_Starter" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:process id="Process_Workflow_Approval" isExecutable="false">
    <bpmn:startEvent id="StartEvent_Request" name="Request">
      <bpmn:outgoing>Flow_Start_Approval</bpmn:outgoing>
    </bpmn:startEvent>
    <bpmn:userTask id="Activity_Approval" name="Approval Task">
      <bpmn:incoming>Flow_Start_Approval</bpmn:incoming>
      <bpmn:outgoing>Flow_Approval_End</bpmn:outgoing>
    </bpmn:userTask>
    <bpmn:endEvent id="EndEvent_Approved" name="Done">
      <bpmn:incoming>Flow_Approval_End</bpmn:incoming>
    </bpmn:endEvent>
    <bpmn:sequenceFlow id="Flow_Start_Approval" sourceRef="StartEvent_Request" targetRef="Activity_Approval" />
    <bpmn:sequenceFlow id="Flow_Approval_End" sourceRef="Activity_Approval" targetRef="EndEvent_Approved" />
  </bpmn:process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_Workflow_Approval">
    <bpmndi:BPMNPlane id="BPMNPlane_Workflow_Approval" bpmnElement="Process_Workflow_Approval">
      <bpmndi:BPMNShape id="StartEvent_Request_di" bpmnElement="StartEvent_Request">
        <dc:Bounds x="170" y="220" width="36" height="36" />
        <bpmndi:BPMNLabel>
          <dc:Bounds x="166" y="263" width="45" height="14" />
        </bpmndi:BPMNLabel>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="Activity_Approval_di" bpmnElement="Activity_Approval">
        <dc:Bounds x="300" y="198" width="150" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="EndEvent_Approved_di" bpmnElement="EndEvent_Approved">
        <dc:Bounds x="550" y="220" width="36" height="36" />
        <bpmndi:BPMNLabel>
          <dc:Bounds x="555" y="263" width="28" height="14" />
        </bpmndi:BPMNLabel>
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id="Flow_Start_Approval_di" bpmnElement="Flow_Start_Approval">
        <di:waypoint x="206" y="238" />
        <di:waypoint x="300" y="238" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="Flow_Approval_End_di" bpmnElement="Flow_Approval_End">
        <di:waypoint x="450" y="238" />
        <di:waypoint x="550" y="238" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;

    let modeler = null;
    let ready = false;
    let gateValidated = false;
    let gateGenerated = false;
    const customProps = {}; // elementId -> { stepCode, stepName, stepType, candidates, commentRequired, evidenceRequired, due, escalate, timeout, escPrincipals }

    // ---- gate helpers ----
    const setButtonEnabled = (id, enabled) => {
        const b = el(id);
        if (!b) return;
        if (b.tagName === 'BUTTON') { b.disabled = !enabled; }
        else { if (enabled) b.classList.remove('disabled'); else b.classList.add('disabled'); }
    };
    const applyGates = () => {
        setButtonEnabled('wf-vd-generate', gateValidated);
        setButtonEnabled('wf-vd-usepublish', gateGenerated);
    };
    const resetGates = () => {
        gateValidated = false;
        gateGenerated = false;
        applyGates();
    };

    const boType = (element) => element?.businessObject?.$type;

    const ensureLibrary = () => {
        if (typeof window.BpmnJS === 'function') return true;
        status('error', t('BpmnLibraryMissing', 'The BPMN designer library failed to load.'));
        ['wf-vd-validate', 'wf-vd-generate', 'wf-vd-preview', 'wf-vd-usepublish', 'wf-vd-export', 'wf-vd-import'].forEach((id) => { const b = el(id); if (b) { if (b.tagName === 'BUTTON') b.disabled = true; else b.classList.add('disabled'); } });
        return false;
    };

    const initModeler = async () => {
        if (modeler || !ensureLibrary()) return;
        modeler = new window.BpmnJS({ container: '#wf-vd-canvas' });

        // Properties panel wiring on selection change.
        modeler.on('selection.changed', (e) => {
            const sel = (e.newSelection || []).filter((x) => TASK_TYPES.has(boType(x)));
            if (sel.length === 1) showProps(sel[0]); else hideProps();
        });
        modeler.on('element.changed', (e) => {
            const sel = modeler.get('selection').get();
            if (sel.length === 1 && sel[0] === e.element && TASK_TYPES.has(boType(e.element))) showProps(e.element);
        });

        // Reset sequential gates when the diagram is mutated.
        ['shape.added', 'shape.removed', 'connection.added', 'connection.removed', 'shape.move.end'].forEach((evt) => {
            modeler.on(evt, () => { if (ready) resetGates(); });
        });

        try {
            const restored = await tryRestorePublishedDiagram();
            if (!restored) {
                await modeler.importXML(STARTER_BPMN_XML);
                customProps.Activity_Approval = {
                    stepCode: 'approval-task',
                    stepName: 'Approval Task',
                    stepType: 'approval',
                    candidates: [],
                    commentRequired: false,
                    evidenceRequired: false,
                    due: '',
                    escalate: '',
                    timeout: '',
                    escPrincipals: ''
                };
            }
            fitViewport();
            ready = true;
            window.setTimeout(fitViewport, 50);
        } catch (err) {
            console.error('BPMN modeler init failed', err);
            status('error', t('BpmnImportFailed', 'Could not import the BPMN XML.'));
        }
    };

    const fitViewport = () => { try { modeler.get('canvas').zoom('fit-viewport', 'auto'); } catch (_e) { /* noop */ } };

    // Best-effort: restore the diagram XML embedded in the active published version, if any.
    const tryRestorePublishedDiagram = async () => {
        try {
            if (!window.WorkflowApi) return false;
            const def = await window.WorkflowApi.getDefinition(definitionId);
            const versionId = def?.data?.activePublishedVersionId;
            if (!versionId) return false;
            const ver = await window.WorkflowApi.getVersion(definitionId, versionId);
            const raw = ver?.data?.definitionJson;
            if (!raw) return false;
            const parsed = JSON.parse(raw);
            const xml = parsed?.diagram?.xml;
            if (typeof xml === 'string' && xml.trim().startsWith('<')) {
                await modeler.importXML(xml);
                hydrateCustomPropsFromDefinition(parsed);
                return true;
            }
        } catch (_e) { /* fall back to empty diagram */ }
        return false;
    };

    // ---- properties panel ----
    let activeElementId = null;
    const showProps = (element) => {
        const id = element.id;
        activeElementId = id;
        const bo = element.businessObject;
        const p = customProps[id] || {};
        el('wf-vd-props').classList.remove('d-none');
        el('wf-vd-noselection').classList.add('d-none');
        el('wf-vd-elementid').value = id;
        el('wf-vd-stepcode').value = p.stepCode != null ? p.stepCode : sanitizeCode(id);
        el('wf-vd-stepname').value = p.stepName != null ? p.stepName : (bo.name || '');
        el('wf-vd-steptype').value = p.stepType || 'approval';
        if (window.jQuery) window.jQuery(el('wf-vd-steptype')).trigger('change.select2');
        setSelectValues('wf-vd-candidates', p.candidates || []);
        el('wf-vd-commentrequired').checked = !!p.commentRequired;
        el('wf-vd-evidencerequired').checked = !!p.evidenceRequired;
        el('wf-vd-due').value = p.due || '';
        el('wf-vd-escalate').value = p.escalate || '';
        el('wf-vd-timeout').value = p.timeout || '';
        setSelectValues('wf-vd-escprincipals', p.escPrincipals || []);
    };
    const hideProps = () => {
        activeElementId = null;
        el('wf-vd-props').classList.add('d-none');
        el('wf-vd-noselection').classList.remove('d-none');
    };
    const captureProps = () => {
        if (!activeElementId) return;
        customProps[activeElementId] = {
            stepCode: el('wf-vd-stepcode').value.trim(),
            stepName: el('wf-vd-stepname').value.trim(),
            stepType: (el('wf-vd-steptype').value || 'approval').trim(),
            candidates: readSelectValues('wf-vd-candidates'),
            commentRequired: el('wf-vd-commentrequired').checked,
            evidenceRequired: el('wf-vd-evidencerequired').checked,
            due: el('wf-vd-due').value.trim(),
            escalate: el('wf-vd-escalate').value.trim(),
            timeout: el('wf-vd-timeout').value.trim(),
            escPrincipals: readSelectValues('wf-vd-escprincipals')
        };
        // Reflect the step name onto the diagram label for a nicer canvas.
        const name = customProps[activeElementId].stepName;
        if (name) {
            try {
                const element = modeler.get('elementRegistry').get(activeElementId);
                if (element) modeler.get('modeling').updateProperties(element, { name });
            } catch (_e) { /* noop */ }
        }
    };

    const hydrateCustomPropsFromDefinition = (definition) => {
        const stages = Array.isArray(definition?.stages) ? definition.stages : [];
        const jsonSteps = stages.flatMap((stage) => Array.isArray(stage?.steps) ? stage.steps : []);
        if (!jsonSteps.length) return;

        const tasks = orderTasks(inspect());
        tasks.forEach((task, index) => {
            const source = jsonSteps[index];
            if (!source) return;
            customProps[task.id] = {
                stepCode: source.code || sanitizeCode(task.id),
                stepName: source.name || task.businessObject?.name || sanitizeCode(task.id),
                stepType: source.type || 'approval',
                candidates: normalizeCsv(source.assignment?.candidatePrincipalIds || []),
                commentRequired: !!source.requirements?.commentRequired,
                evidenceRequired: !!source.requirements?.evidenceRequired,
                due: source.sla?.dueInMinutes != null ? String(source.sla.dueInMinutes) : '',
                escalate: source.sla?.escalateAfterMinutes != null ? String(source.sla.escalateAfterMinutes) : '',
                timeout: source.sla?.timeoutAfterMinutes != null ? String(source.sla.timeoutAfterMinutes) : '',
                escPrincipals: normalizeCsv(source.sla?.escalationPrincipalIds || [])
            };
        });
    };

    // ---- diagram inspection ----
    const inspect = () => {
        const registry = modeler.get('elementRegistry');
        const all = registry.getAll();
        const result = { starts: [], ends: [], tasks: [], flows: [], gateways: [], unsupported: [] };
        all.forEach((element) => {
            if (element.type === 'label' || element.labelTarget) return;
            const ty = boType(element);
            if (!ty) return;
            if (STRUCTURAL.has(ty)) return;
            if (ty === 'bpmn:StartEvent') result.starts.push(element);
            else if (ty === 'bpmn:EndEvent') result.ends.push(element);
            else if (TASK_TYPES.has(ty)) result.tasks.push(element);
            else if (ty === 'bpmn:SequenceFlow') result.flows.push(element);
            else if (SOFT.has(ty)) result.gateways.push(element);
            else if (ty.startsWith('bpmn:')) result.unsupported.push(element);
        });
        return result;
    };

    // Order tasks by following sequence flows from the start event (BFS); append any unreached tasks.
    const orderTasks = (info) => {
        const adj = {};
        info.flows.forEach((f) => {
            const s = f.businessObject.sourceRef && f.businessObject.sourceRef.id;
            const target = f.businessObject.targetRef && f.businessObject.targetRef.id;
            if (s && target) { (adj[s] = adj[s] || []).push(target); }
        });
        const taskIds = new Set(info.tasks.map((x) => x.id));
        const ordered = [];
        const visited = new Set();
        const queue = info.starts.length ? [info.starts[0].id] : [];
        while (queue.length) {
            const id = queue.shift();
            if (visited.has(id)) continue;
            visited.add(id);
            if (taskIds.has(id) && !ordered.includes(id)) ordered.push(id);
            (adj[id] || []).forEach((n) => queue.push(n));
        }
        info.tasks.forEach((x) => { if (!ordered.includes(x.id)) ordered.push(x.id); }); // unreached -> append
        const registry = modeler.get('elementRegistry');
        return ordered.map((id) => registry.get(id)).filter(Boolean);
    };

    // ---- validation ----
    // Returns { ok, message } (and warns for soft elements via toast).
    const validateDiagram = (info) => {
        if (info.unsupported.length) return { ok: false, message: t('UnsupportedBpmnElement', 'Unsupported BPMN element for MOD-0023 runtime.') };
        if (info.starts.length !== 1) return { ok: false, message: t('MissingStartEvent', 'The diagram must have exactly one start event.') };
        if (info.tasks.length < 1) return { ok: false, message: t('MissingApprovalTask', 'The diagram must have at least one approval task.') };
        if (info.ends.length < 1) return { ok: false, message: t('MissingEndEvent', 'The diagram must have at least one end event.') };
        for (const task of info.tasks) {
            const p = customProps[task.id] || {};
            if (normalizeCsv(p.candidates).length === 0) return { ok: false, message: t('CandidatePrincipalsRequired', 'Every approval task needs candidate principal ids.') };
            const due = p.due, esc = p.escalate, tmo = p.timeout;
            if (due !== '' && due != null && due !== undefined && due !== '') {
                if (due && !(Number(due) > 0)) return { ok: false, message: t('InvalidSlaMinutes', 'SLA minutes invalid.') };
                if (due && esc && !(Number(esc) >= Number(due))) return { ok: false, message: t('InvalidSlaMinutes', 'SLA minutes invalid.') };
                if (esc && tmo && !(Number(tmo) >= Number(esc))) return { ok: false, message: t('InvalidSlaMinutes', 'SLA minutes invalid.') };
            }
        }
        if (info.gateways.length) notify('warning', t('ExclusiveGatewayWarning', 'Exclusive gateways are accepted but not mapped to runtime branches.'));
        return { ok: true };
    };

    const runValidate = () => {
        if (!ready) return null;
        captureProps();
        clearStatus();
        const info = inspect();
        const v = validateDiagram(info);
        if (!v.ok) { status('error', v.message); gateValidated = false; gateGenerated = false; applyGates(); return null; }
        gateValidated = true;
        gateGenerated = false;
        applyGates();
        status('success', t('DiagramValid', 'Diagram is valid.'));
        return info;
    };

    // ---- mapping to DefinitionJson ----
    const buildSteps = (info) => orderTasks(info).map((task) => {
        const p = customProps[task.id] || {};
        const bo = task.businessObject;
            const step = {
                code: (p.stepCode && p.stepCode.trim()) ? p.stepCode.trim() : sanitizeCode(task.id),
                name: (p.stepName && p.stepName.trim()) ? p.stepName.trim() : (bo.name || sanitizeCode(task.id)),
                type: p.stepType || 'approval',
                assignment: { mode: 'candidate_principals', candidatePrincipalIds: normalizeCsv(p.candidates) },
            requirements: { commentRequired: !!p.commentRequired, evidenceRequired: !!p.evidenceRequired }
        };
        if (p.due) {
            const sla = { dueInMinutes: Number(p.due) };
            if (p.escalate) sla.escalateAfterMinutes = Number(p.escalate);
            if (p.timeout) sla.timeoutAfterMinutes = Number(p.timeout);
            const esc = normalizeCsv(p.escPrincipals);
            if (esc.length) sla.escalationPrincipalIds = esc;
            step.sla = sla;
        }
        return step;
    });

    const generateDefinitionJson = async () => {
        if (!gateValidated) {
            notify('warning', t('ValidateFirst', 'Please validate the diagram first.'));
            return null;
        }
        captureProps();
        clearStatus();
        const info = inspect();
        const v = validateDiagram(info);
        if (!v.ok) { status('error', v.message); gateValidated = false; gateGenerated = false; applyGates(); return null; }
        let xml = '';
        try { xml = (await modeler.saveXML({ format: true })).xml; } catch (_e) { xml = ''; }
        const def = {
            schemaVersion: 'workflow_schema_v1',
            expressionVersion: 'workflow_expr_v1',
            stages: [{ code: 'stage-1', name: 'BPMN Designed Flow', steps: buildSteps(info) }],
            diagram: { notation: 'bpmn-2.0', xml }
        };
        const json = prettyJson(def);
        const preview = el('wf-dz-preview');
        if (preview) preview.value = json;
        gateGenerated = true;
        applyGates();
        status('success', t('DiagramJsonGenerated', 'Definition JSON generated from the diagram.'));
        notify('success', t('DiagramJsonGenerated', 'Definition JSON generated from the diagram.'));
        return { json, def };
    };

    // ---- publish + preview reuse (shared DOM with visual designer page) ----
    const openSharedPublish = (json) => {
        el('wf-publish-error')?.classList.add('d-none');
        el('wf-publish-result')?.classList.add('d-none');
        el('wf-publish-form')?.reset();
        if (el('wf-publish-definitionid')) el('wf-publish-definitionid').value = definitionId;
        if (el('wf-publish-title-code')) el('wf-publish-title-code').textContent = el('wf-meta-code')?.textContent || '';
        if (el('wf-publish-json')) el('wf-publish-json').value = json;
        if (el('wf-publish-schema')) el('wf-publish-schema').value = 'workflow_schema_v1';
        if (el('wf-publish-expression')) el('wf-publish-expression').value = 'workflow_expr_v1';
        const modal = el('wf-publish-modal');
        if (modal && window.bootstrap?.Modal) bootstrap.Modal.getOrCreateInstance(modal).show();
    };

    const useInPublish = async () => {
        if (!gateGenerated) {
            notify('warning', t('GenerateFirst', 'Generate the Definition JSON first.'));
            return;
        }
        const out = await generateDefinitionJson();
        if (!out) return;
        openSharedPublish(out.json);
    };

    const previewJson = async () => {
        const out = await generateDefinitionJson();
        if (!out) return;
        const preview = el('wf-dz-preview');
        if (preview) preview.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        const designerTabBtn = document.querySelector('button[data-wf-tab="designer"]');
        if (designerTabBtn && window.bootstrap) bootstrap.Tab.getOrCreateInstance(designerTabBtn).show();
    };

    // ---- import / export ----
    const exportXml = async () => {
        if (!ready) return;
        try {
            const { xml } = await modeler.saveXML({ format: true });
            const blob = new Blob([xml], { type: 'application/xml' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url; a.download = `workflow-${definitionId}.bpmn`;
            document.body.appendChild(a); a.click(); a.remove();
            URL.revokeObjectURL(url);
            notify('success', t('BpmnXmlExported', 'BPMN XML exported.'));
        } catch (_e) { notify('error', t('BpmnImportFailed', 'Could not export the BPMN XML.')); }
    };

    const importXmlFile = (file) => {
        if (!ready || !file) return;
        const reader = new FileReader();
        reader.onload = async (ev) => {
            try {
                await modeler.importXML(String(ev.target.result || ''));
                Object.keys(customProps).forEach((k) => delete customProps[k]);
                hideProps();
                fitViewport();
                clearStatus();
                notify('success', t('BpmnXmlImported', 'BPMN XML imported.'));
            } catch (_e) {
                status('error', t('BpmnImportFailed', 'Could not import the BPMN XML.'));
            }
        };
        reader.readAsText(file);
    };

    // ---- wiring ----
    document.addEventListener('DOMContentLoaded', () => {
        const canvas = el('wf-vd-canvas');
        if (!canvas) return;

        const visualTabButtons = document.querySelectorAll('button[data-wf-tab="visual"]');
        visualTabButtons.forEach((btn) => {
            btn.addEventListener('shown.bs.tab', () => { initModeler().then(() => fitViewport()); });
        });

        if (!visualTabButtons.length) {
            window.requestAnimationFrame(() => {
                initModeler().then(() => fitViewport());
            });
        }

        if (window.ResizeObserver) {
            const observer = new ResizeObserver(() => {
                if (ready) fitViewport();
            });
            observer.observe(canvas);
        }

        // Persist property edits on input; reset gates so user must re-validate.
        ['wf-vd-stepcode', 'wf-vd-stepname', 'wf-vd-steptype', 'wf-vd-candidates', 'wf-vd-commentrequired', 'wf-vd-evidencerequired', 'wf-vd-due', 'wf-vd-escalate', 'wf-vd-timeout', 'wf-vd-escprincipals']
            .forEach((id) => { const node = el(id); if (node) node.addEventListener('change', () => { captureProps(); resetGates(); }); });

        // Initial gate state: Generate and UseInPublish start disabled.
        applyGates();

        el('wf-vd-validate')?.addEventListener('click', runValidate);
        el('wf-vd-generate')?.addEventListener('click', generateDefinitionJson);
        el('wf-vd-preview')?.addEventListener('click', previewJson);
        el('wf-vd-usepublish')?.addEventListener('click', useInPublish);
        el('wf-vd-export')?.addEventListener('click', exportXml);
        el('wf-vd-import')?.addEventListener('click', () => el('wf-vd-importfile')?.click());
        el('wf-vd-importfile')?.addEventListener('change', (ev) => { importXmlFile(ev.target.files && ev.target.files[0]); ev.target.value = ''; });
    });
})();
