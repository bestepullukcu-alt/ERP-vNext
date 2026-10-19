/**
 * MOD-0177 R7b — Kanıt Paketi (pack §11.1, §4.13; mockup "isEv"). Draws what the doors answer; the subject check, the section order and the
 * export → download → hash comparison are RecallEvidenceView's (evidence-view.js, measured by Node). The table words are the screen's
 * (Recall.Ev.W.{key}, 7 languages). WP-MVP1-FIX-LIVE2: the preview, the CSV and the PDF are asked in the PAGE language (Accept-Language, one
 * of the 7) — values (status, event, node …) and names come worded by the service; a zh / ar PDF is produced in English (its font has no such
 * glyphs) and the page says so in the page language.
 */
'use strict';

(function () {
    if (typeof document === 'undefined' || !window.RecallUi || !window.RecallEvidenceView) return;
    const ui = window.RecallUi;
    const view = window.RecallEvidenceView;
    const { t, esc, byId } = ui;
    const root = byId('rc-evidence');
    if (!root) return;
    const state = { kind: 'Case', options: [], preview: null, subject: null };
    const show = (el, on) => el?.classList.toggle('d-none', !on);
    const word = (key) => t('Recall.Ev.W.' + key) || key;
    const language = document.documentElement.lang || 'en';
    const LANGS = ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru'];
    const pageLanguage = LANGS.includes(language.split('-')[0].toLowerCase()) ? language.split('-')[0].toLowerCase() : 'en';
    const PDF_LANGS = ['en', 'tr', 'fr', 'es', 'ru'];

    const antiforgery = () => document.querySelector('#rc-antiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const deps = {
        newKey: () => ui.newKey().replace(/^r6-/, 'r7-'),
        write: async (path, body, options = {}) => {
            const headers = { Accept: 'application/json', 'Content-Type': 'application/json', RequestVerificationToken: antiforgery() };
            if (options.language) headers['Accept-Language'] = options.language;
            try {
                const res = await fetch(path, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
                return { ok: res.ok, status: res.status, json: await res.json().catch(() => null) };
            } catch (error) {
                return { ok: false, status: 0, json: null };
            }
        },
        download: async (url) => {
            try {
                const res = await fetch(url, { credentials: 'same-origin' });
                if (!res.ok) return { ok: false, status: res.status, json: await res.json().catch(() => null) };
                return { ok: true, bytes: new Uint8Array(await res.arrayBuffer()), type: res.headers.get('Content-Type') || '' };
            } catch (error) {
                return { ok: false, status: 0, json: null };
            }
        },
        sha256: async (bytes) => view.hex(await crypto.subtle.digest('SHA-256', bytes))
    };
    // the preview in the page language (the service words values and names in it)
    const readIn = async (url) => {
        try {
            const res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json', 'Accept-Language': pageLanguage } });
            return { ok: res.ok, status: res.status, json: await res.json().catch(() => null) };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };

    // ── the subject ──────────────────────────────────────────────────────────────────────────────────────────────
    const fillPick = () => {
        const list = state.options.filter((o) => o.kind === state.kind);
        byId('rc-ev-pick').innerHTML = list.map((o) => `<option value="${esc(o.id)}">${esc(`${o.number} · ${o.lot} · ${t((o.kind === 'Case' ? 'Recall.Status.' : 'Recall.DrillStatus.') + o.status)}`)}</option>`).join('')
            || `<option value="">${esc(t('Recall.Evidence.NoSubjects'))}</option>`;
    };
    const setKind = (kind) => {
        state.kind = kind;
        root.querySelectorAll('[data-ev-kind]').forEach((b) => b.classList.toggle('active', b.dataset.evKind === kind));
        show(byId('rc-ev-pick-box'), kind !== 'Period');
        show(byId('rc-ev-period-box'), kind === 'Period');
        if (kind !== 'Period') fillPick();
    };
    const current = () => (state.kind === 'Period'
        ? { kind: 'Period', from: byId('rc-ev-from').value, to: byId('rc-ev-to').value }
        : { kind: state.kind, id: byId('rc-ev-pick').value });
    root.querySelectorAll('[data-ev-kind]').forEach((b) => b.addEventListener('click', () => setKind(b.dataset.evKind)));

    // ── the preview ──────────────────────────────────────────────────────────────────────────────────────────────
    // a table whose first column is "field" names a field per row: its first cell is a word key
    const table = (tbl, summary) => {
        if (!tbl.rows?.length) return `<p class="small text-muted mb-2">${esc(word('none'))}</p>`;
        const head = tbl.columns.map((c) => `<th>${esc(word(c))}</th>`).join('');
        const rows = tbl.rows.map((r) => `<tr>${r.map((cell, i) => `<td class="small ${/^[0-9a-f-]{20,}$/i.test(cell) ? 'font-monospace' : ''}">${esc(summary && i === 0 ? word(cell) : cell)}</td>`).join('')}</tr>`).join('');
        return `<div class="table-responsive mb-2"><table class="table table-sm mb-0"><thead><tr>${head}</tr></thead><tbody>${rows}</tbody></table></div>`;
    };
    const drawPreview = (preview) => {
        state.preview = preview;
        const s = preview.subject || {};
        byId('rc-ev-title').textContent = `${t('Recall.Evidence.PreviewTitle')} · ${s.label || ''}`;
        byId('rc-ev-sub').textContent = t('Recall.Evidence.Generated', new Date(preview.generatedAtUtc).toLocaleString(language));
        byId('rc-ev-sections').innerHTML = view.sections(preview).map(({ key, section }) => {
            if (!section) return `<section class="mb-4"><h6>${esc(word(key))}</h6><p class="small text-danger mb-0">${esc(t('Recall.Evidence.Missing'))}</p></section>`;
            const note = section.note ? `<p class="small text-muted mb-2">${esc(word(section.note))}</p>` : '';
            const body = section.applicable ? (section.tables || []).map((tbl) => ((section.tables.length > 1 ? `<div class="small fw-medium mb-1">${esc(word(tbl.key))}</div>` : '') + table(tbl, tbl.columns?.[0] === 'field'))).join('') : '';
            const signatures = key === '7_signatures' && (section.tables?.[0]?.rows || []).some((r) => r[1] && !r[2])
                ? `<p class="small text-warning mb-0">${esc(t('Recall.Evidence.Unreadable'))}</p>` : '';
            return `<section class="mb-4"><h6 class="mb-2">${esc(word(key))}</h6>${note}${body}${signatures}</section>`;
        }).join('');
        root.querySelectorAll('[data-ev-format]').forEach((b) => { b.disabled = false; });
    };
    const preview = async () => {
        ui.hideError(byId('rc-error'));
        const picked = view.subject(current());
        if (!picked.ok) { ui.showError(byId('rc-error'), t(picked.error)); return; }
        state.subject = current();
        root.querySelectorAll('[data-ev-format]').forEach((b) => { b.disabled = true; });
        const reply = await readIn(`/Recall/data/evidence/preview?${view.query(picked.params)}`);
        if (!reply.ok) { ui.showError(byId('rc-error'), ui.errorText(reply)); return; }
        drawPreview(reply.json.data || {});
    };
    byId('rc-ev-preview').addEventListener('click', preview);

    // ── export → download → verify ───────────────────────────────────────────────────────────────────────────────
    const save = (bytes, type, name) => {
        const url = URL.createObjectURL(new Blob([bytes], { type }));
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    };
    root.querySelectorAll('[data-ev-format]').forEach((b) => b.addEventListener('click', async () => {
        ui.hideError(byId('rc-error'));
        b.disabled = true;
        const result = await view.exportFile(deps, state.subject || current(), b.dataset.evFormat, pageLanguage);
        b.disabled = false;
        const box = byId('rc-exported');
        if (!result.ok) { ui.showError(byId('rc-error'), result.error ? t(result.error) : ui.errorText(result.reply)); return; }
        save(result.bytes, b.dataset.evFormat === 'Pdf' ? 'application/pdf' : 'text/csv', result.exported.fileName);
        box.className = `alert ${result.verified ? 'alert-success' : 'alert-danger'}`;
        box.innerHTML = `${esc(t(result.verified ? 'Recall.Evidence.Verified' : 'Recall.Evidence.NotVerified', result.exported.fileName))}`
            + `<div class="small font-monospace mt-1" id="rc-ev-sha">SHA-256 ${esc(result.exported.sha256)}</div>`
            + `<div class="small font-monospace" id="rc-ev-sha-local">${esc(t('Recall.Evidence.LocalHash', result.computed))}</div>`
            + (b.dataset.evFormat === 'Pdf' && !PDF_LANGS.includes(pageLanguage) ? `<div class="small mt-1" id="rc-ev-english">${esc(t('Recall.Ev.W.englishFallback'))}</div>` : '');
        await loadLog();
    }));

    // ── the export log ───────────────────────────────────────────────────────────────────────────────────────────
    const loadLog = async () => {
        const reply = await ui.get('/Recall/data/evidence/exports?page=1&pageSize=20');
        const items = reply.ok ? reply.json.data?.items || [] : [];
        byId('rc-ev-log').innerHTML = items.map((x) => `<li class="list-group-item">`
            + `<a href="/Recall/export/evidence/${encodeURIComponent(x.exportId)}" class="fw-medium small text-break">${esc(x.fileName)}</a>`
            + `<div class="small text-muted">${esc(x.exportedBy?.name || x.exportedBy?.userId || '')} · ${esc(new Date(x.exportedBy?.atUtc).toLocaleString(language))}</div>`
            + `<div class="small font-monospace text-break" title="SHA-256">${esc(x.sha256)}</div></li>`).join('')
            || `<li class="list-group-item text-muted small">${esc(t('Recall.Evidence.LogEmpty'))}</li>`;
        if (!reply.ok) ui.showError(byId('rc-error'), ui.errorText(reply));
    };

    const start = async () => {
        const subjects = await ui.get('/Recall/lookups/evidence-subjects');
        state.options = subjects.ok ? view.options(subjects.json.items) : [];
        if (!subjects.ok) byId('rc-ev-pick').innerHTML = `<option value="">${esc(t('Recall.Evidence.SubjectsUnavailable'))}</option>`;
        const asked = root.dataset.case ? { kind: 'Case', id: root.dataset.case } : root.dataset.run ? { kind: 'DrillRun', id: root.dataset.run } : null;
        if (asked && !state.options.some((o) => o.id === asked.id)) state.options.unshift({ kind: asked.kind, id: asked.id, number: asked.id.slice(0, 8), lot: '', status: '' });
        setKind(asked?.kind || 'Case');
        if (asked) { byId('rc-ev-pick').value = asked.id; await preview(); }
        await loadLog();
    };
    start();
})();
