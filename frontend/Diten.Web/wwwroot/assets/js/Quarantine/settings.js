/**
 * MOD-0175 Q5 — Karantina Ayarları (pack §11.3; §21.3 #10). (a) the company settings — getSettings / saveSettings with If-Match
 * W/"<version>" (W/"0" before the first save; a member left out keeps its value; stale → QUARANTINE_CONFLICT): bekleme hedefi
 * (1–60, KQ-D), dört göz (KQ-C), onaylayıcı adaylar ("position:{id}" / "user:{id}", ≤ 10, positions from the Workflow lookup),
 * numune lokasyonları (warehouse → stockable location, KQ-E — the service checks the same warehouse, V13), lot serbest bırakma
 * onayı (KQ-I), yeniden okuma aralığı. (b) Bloke nedenleri — DataTable v2 CLIENT mode (a bounded configuration list, CT-Q8):
 * code + 7 names + active; the code never changes. (c) "Onay şablonunu hazırla" (create + publish, idempotent).
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc } = ui;
    const lang = (document.documentElement.lang || 'tr').slice(0, 2);
    const LANGS = ['tr', 'en', 'fr', 'es', 'zh', 'ar', 'ru'];
    const form = ui.byId('qr-settings-form');
    if (!form) return;
    let etag = 'W/"0"';
    let candidates = [];
    let samples = {};
    const labels = { positions: {}, warehouses: {}, locations: {}, users: {} };
    let reasonsList = null;
    let editing = null;

    // ── settings ────────────────────────────────────────────────────────────────────────────────────────────────
    const candidateLabel = (value) => {
        const [kind, id] = value.split(':');
        // FU-0175-Q5-02: a user candidate by name (lookups/users), else the short id + "unknown user"
        const text = kind === 'position' ? (labels.positions[id] || id) : (labels.users[id] || ui.unknownPerson(id));
        return `${t(kind === 'position' ? 'Quarantine.Settings.Approvers.Position' : 'Quarantine.Settings.Approvers.User')}: ${text}`;
    };
    const renderCandidates = () => {
        const box = ui.byId('qr-s-candidates');
        box.innerHTML = candidates.length
            ? candidates.map((c, i) => `<span class="badge bg-label-primary d-inline-flex align-items-center gap-1">${esc(candidateLabel(c))}<button type="button" class="btn-close btn-close-sm ms-1" data-cand="${i}" aria-label="${esc(t('Quarantine.Settings.Remove'))}"></button></span>`).join('')
            : `<span class="text-muted small">${esc(t('Quarantine.Settings.Approvers.Empty'))}</span>`;
    };
    const renderSamples = () => {
        const list = ui.byId('qr-s-samples');
        const entries = Object.entries(samples);
        list.innerHTML = entries.length
            ? entries.map(([wh, loc]) => `<li class="list-group-item d-flex justify-content-between align-items-center"><span><span class="fw-medium">${esc(labels.warehouses[wh] || wh)}</span> <i class="bx bx-right-arrow-alt text-muted"></i> ${esc(labels.locations[loc] || loc)}</span>`
                + `<button type="button" class="btn btn-sm btn-icon btn-label-danger" data-sample="${esc(wh)}" aria-label="${esc(t('Quarantine.Settings.Remove'))}"><i class="bx bx-trash"></i></button></li>`).join('')
            : `<li class="list-group-item text-muted small">${esc(t('Quarantine.Settings.SampleLocations.Empty'))}</li>`;
    };
    const renderTemplate = (template) => {
        ui.byId('qr-s-template-status').innerHTML = template?.status === 'Published'
            ? `<span class="badge bg-label-success">${esc(t('Quarantine.Settings.Template.Published'))}</span>`
            : `<span class="badge bg-label-warning">${esc(t('Quarantine.Settings.Template.Missing'))}</span>`;
        ui.byId('qr-s-template-version').textContent = template?.publishedVersion ?? '—';
    };
    const fill = (s, tag) => {
        etag = tag || `W/"${s.version ?? 0}"`;
        ui.byId('qr-s-wait').value = s.waitTargetDays ?? '';
        ui.byId('qr-s-reconcile').value = s.reconcileAfterSeconds ?? '';
        ui.byId('qr-s-foureyes').checked = s.fourEyes !== false;
        ui.byId('qr-s-lotrelease').checked = s.lotReleaseRequiresApproval === true;
        candidates = [...(s.approverCandidates || [])];
        samples = Object.assign({}, s.sampleLocations || {});
        renderCandidates();
        loadUserNames();
        renderSamples();
        renderTemplate(s.workflowTemplate);
    };
    /** Names of the user candidates not resolved yet (one Web call; unknown ones stay "unknown user"). */
    async function loadUserNames() {
        const ids = candidates.filter((c) => c.startsWith('user:')).map((c) => c.slice(5)).filter((id) => !(id in labels.users));
        if (!ids.length) return;
        const reply = await ui.get(`/Quarantine/lookups/users?ids=${encodeURIComponent(ids.join(','))}`);
        (reply.ok ? reply.json?.items || [] : []).forEach((u) => { if (u.name) labels.users[u.id] = u.name; });
        renderCandidates();
    }

    const load = async () => {
        const reply = await ui.get(ui.api('settings'));
        if (!reply.ok) return ui.showError(ui.byId('qr-page-error'), reply);
        fill(reply.json, reply.etag);
    };

    const save = async (event) => {
        event.preventDefault();
        ui.clearFieldErrors(form);
        ui.hideError(ui.byId('qr-page-error'));
        const wait = Number(ui.byId('qr-s-wait').value);
        if (!Number.isInteger(wait) || wait < 1 || wait > 60) {
            ui.fieldError(form, 'wait', t('Quarantine.Settings.WaitTarget.Invalid'));
            return;
        }
        const body = {
            waitTargetDays: wait,
            fourEyes: ui.byId('qr-s-foureyes').checked,
            sampleLocations: samples,
            lotReleaseRequiresApproval: ui.byId('qr-s-lotrelease').checked
        };
        const reconcile = ui.byId('qr-s-reconcile').value;
        if (reconcile !== '') body.reconcileAfterSeconds = Number(reconcile);
        if (candidates.length) body.approverCandidates = candidates;
        const button = ui.byId('qr-settings-save');
        button.disabled = true;
        const reply = await ui.send('PUT', 'settings', body, etag);
        button.disabled = false;
        if (!reply.ok) return ui.showError(ui.byId('qr-page-error'), reply);
        fill(reply.json, reply.etag);
        ui.toast(t('Quarantine.Settings.Saved'), 'success');
    };

    const loadPickers = async () => {
        const [positions, warehouses, locations] = await Promise.all([
            ui.get('/Quarantine/lookups/positions'), ui.get('/Quarantine/lookups/warehouses'), ui.get('/Quarantine/lookups/locations')
        ]);
        const fillSelect = (select, reply, map) => {
            (reply.ok ? reply.json?.items || [] : []).forEach((o) => {
                map[o.value] = o.text || o.value;
                select.add(new Option(o.text || o.value, o.value));
            });
        };
        fillSelect(ui.byId('qr-s-cand-position'), positions, labels.positions);
        fillSelect(ui.byId('qr-s-sample-wh'), warehouses, labels.warehouses);
        fillSelect(ui.byId('qr-s-sample-loc'), locations, labels.locations);
        renderCandidates();
        renderSamples();
    };

    ui.byId('qr-s-cand-kind').addEventListener('change', (event) => {
        const position = event.target.value === 'position';
        ui.byId('qr-s-cand-position').classList.toggle('d-none', !position);
        ui.byId('qr-s-cand-user').classList.toggle('d-none', position);
    });
    ui.byId('qr-s-cand-add').addEventListener('click', () => {
        ui.fieldError(form, 'candidates', '');
        const kind = ui.byId('qr-s-cand-kind').value;
        const id = kind === 'position' ? ui.byId('qr-s-cand-position').value : ui.byId('qr-s-cand-user').value.trim();
        if (!id || (kind === 'user' && !/^[0-9a-f-]{36}$/i.test(id))) return ui.fieldError(form, 'candidates', t('Quarantine.Settings.Approvers.Invalid'));
        if (candidates.length >= 10) return ui.fieldError(form, 'candidates', t('Quarantine.Settings.Approvers.Max'));
        const value = `${kind}:${id}`;
        if (!candidates.includes(value)) candidates.push(value);
        ui.byId('qr-s-cand-user').value = '';
        renderCandidates();
        loadUserNames();
    });
    ui.byId('qr-s-candidates').addEventListener('click', (event) => {
        const index = event.target.closest('[data-cand]')?.getAttribute('data-cand');
        if (index == null) return;
        candidates.splice(Number(index), 1);
        renderCandidates();
    });
    ui.byId('qr-s-sample-add').addEventListener('click', () => {
        const wh = ui.byId('qr-s-sample-wh').value;
        const loc = ui.byId('qr-s-sample-loc').value;
        if (!wh || !loc) return;
        samples[wh] = loc;
        renderSamples();
    });
    ui.byId('qr-s-samples').addEventListener('click', (event) => {
        const wh = event.target.closest('[data-sample]')?.getAttribute('data-sample');
        if (!wh) return;
        delete samples[wh];
        renderSamples();
    });
    form.addEventListener('submit', save);

    ui.byId('qr-template-prepare').addEventListener('click', () => {
        window.showConfirm?.(t('Quarantine.Settings.Template.Prepare'), async () => {
            const reply = await ui.write('POST', 'settings/workflow-template', {});
            if (!reply.ok) return window.DitenModal?.error({ title: t('Quarantine.Settings.Template.Prepare'), message: ui.errorText(reply) });
            ui.toast(t(reply.json?.created ? 'Quarantine.Settings.Template.Created' : 'Quarantine.Settings.Template.Existing', reply.json?.publishedVersion ?? ''), 'success');
            await load();
        }, { type: 'info', subtext: esc(t('Quarantine.Settings.Template.Text')), confirmButtonText: t('Quarantine.Settings.Template.Prepare'), cancelButtonText: t('Quarantine.Common.Cancel') });
    });

    // ── block reasons (client mode: the whole bounded list in one answer) ───────────────────────────────────────
    const nameOf = (r) => r.names?.[lang] || r.names?.en || r.code;
    const openReason = (reason) => {
        editing = reason || null;
        const modal = ui.byId('qr-br-modal');
        ui.byId('qr-br-form').reset();
        ui.hideError(ui.byId('qr-br-error'));
        ui.byId('qr-br-title').textContent = reason ? t('Quarantine.Settings.BR.Edit') : t('Quarantine.Settings.BR.Add');
        const code = ui.byId('qr-br-code');
        code.value = reason?.code || '';
        code.readOnly = !!reason;
        LANGS.forEach((l) => { ui.byId(`qr-br-name-${l}`).value = reason?.names?.[l] || ''; });
        ui.byId('qr-br-active').checked = reason ? reason.isActive : true;
        window.bootstrap.Modal.getOrCreateInstance(modal).show();
    };
    ui.byId('qr-br-add').addEventListener('click', () => openReason(null));
    ui.byId('qr-br-form').addEventListener('submit', async (event) => {
        event.preventDefault();
        const names = Object.fromEntries(LANGS.map((l) => [l, ui.byId(`qr-br-name-${l}`).value.trim()]));
        const code = ui.byId('qr-br-code').value.trim();
        if (!/^[A-Z][A-Z0-9_]{1,31}$/.test(code)) return ui.showError(ui.byId('qr-br-error'), t('Quarantine.Settings.BR.CodeInvalid'));
        if (LANGS.some((l) => !names[l])) return ui.showError(ui.byId('qr-br-error'), t('Quarantine.Errors.QUARANTINE_BLOCK_REASON_LANGUAGE_MISSING'));
        const body = { names, isActive: ui.byId('qr-br-active').checked };
        const reply = editing
            ? await ui.send('PUT', `block-reasons/${encodeURIComponent(editing.code)}`, body)
            : await ui.send('POST', 'block-reasons', Object.assign({ code }, body));
        if (!reply.ok) return ui.showError(ui.byId('qr-br-error'), reply);
        window.bootstrap.Modal.getOrCreateInstance(ui.byId('qr-br-modal')).hide();
        ui.toast(t('Quarantine.Settings.BR.Saved'), 'success');
        reasonsList?.dt?.ajax?.reload(null, false);
    });

    const initReasons = async () => {
        const tableEl = document.querySelector('.datatables-quarantine-block-reasons');
        if (!tableEl) return;
        reasonsList = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'client',
            ajax: { url: ui.api('block-reasons'), type: 'GET', dataSrc: (json) => (Array.isArray(json) ? json : []) },
            actions: { onRowAction: { EDITREASON: ({ row }) => row && openReason(row) } },
            toolbar: { addNewText: '', colvisColumns: [1, 2, 3, 4] },
            savedView: { moduleKey: 'Quarantine', pageKey: 'BlockReasons', saveViewColumnIndexes: [1, 2, 3, 4], defaultVisibleColumnIndexes: [1, 2, 3, 4], baseOrder: [[1, 'asc']] },
            config: {
                ordering: true,
                searching: true,
                pageLength: 10,
                initComplete: function () { try { this.api().buttons('.dt-export-collection-btn').remove(); } catch (error) { /* no export group */ } },
                columns: [
                    { data: 'code', name: 'control' }, { data: 'code', name: 'code' }, { data: 'names', name: 'name' },
                    { data: 'isActive', name: 'active' }, { data: 'isLocked', name: 'locked' }, { data: 'code', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="font-monospace fw-medium">${esc(data)}</span>` },
                    { targets: 2, render: (data, type, full) => esc(nameOf(full)) },
                    { targets: 3, render: (data) => `<span class="badge ${data ? 'bg-label-success' : 'bg-label-secondary'}">${esc(t(data ? 'Quarantine.Common.Yes' : 'Quarantine.Common.No'))}</span>` },
                    { targets: 4, render: (data) => (data ? `<i class="bx bx-lock-alt text-muted" title="${esc(t('Quarantine.Settings.BR.Locked'))}"></i>` : '') },
                    {
                        targets: -1, title: t('Quarantine.Col.Actions'), searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data, type, full) => window.DitenDataTable.renderActions([{ key: 'EDITREASON', visible: ui.can('SAVESETTINGS'), icon: 'bx bx-edit', text: t('Quarantine.Settings.BR.Edit'), attrs: { 'data-id': full.code, 'data-json': JSON.stringify(full), title: t('Quarantine.Settings.BR.Edit') } }])
                    }
                ]
            }
        });
    };

    document.addEventListener('DOMContentLoaded', async () => {
        ui.loadCounts(null);
        await load();
        loadPickers();
        initReasons();
    });
})();
