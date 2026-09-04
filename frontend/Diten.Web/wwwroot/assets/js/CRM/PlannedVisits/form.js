/**
 * MOD-0155-FU01 Planned Visits — the Create / Edit form (Golden Compact).
 *
 * Every vocabulary control is filled from the runtime CONTRACT (target type, purpose, visit type, resource type,
 * status) — no hardcoded list. The target picker is chained to the target type (accounts for account/pharmacy, contacts
 * for contact; a pharmacy narrows accounts to account-type=pharmacy). The journey → stage picker is chained too, and it
 * marks the content-position ContentSource: a rep-entered journey is `manual` (FU01 has no strategy resolver — the
 * `strategy` default-fill arrives with F-STRATEGY/FU04), and the badge reflects which one is in force.
 */
(function (window, document) {
    'use strict';
    const form = document.getElementById('plannedVisitForm');
    if (!form) return;

    const L = window.L10n || {};
    const accountsUrl = form.dataset.accountsUrl;
    const contactsUrl = form.dataset.contactsUrl;
    const journeysUrl = form.dataset.journeysUrl;
    const contractUrl = form.dataset.contractUrl;

    const targetTypeEl = document.getElementById('targetType');
    const targetIdEl = document.getElementById('targetId');
    const visitPurposeEl = document.getElementById('visitPurpose');
    const visitTypeEl = document.getElementById('visitType');
    const resourceTypeEl = document.getElementById('resourceType');
    const planStatusEl = document.getElementById('planStatus');
    const journeyEl = document.getElementById('journeyId');
    const stageEl = document.getElementById('stageId');
    const contentSourceEl = document.getElementById('contentSource');
    const contentSourceBadge = document.getElementById('contentSourceBadge');

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const sel = el => el?.dataset?.selected || '';

    const labelForVocab = (group, v) => {
        const map = {
            targetTypes: { account: L.TargetTypeAccount, contact: L.TargetTypeContact, 'account-contact-link': L.TargetTypeAccountContactLink, pharmacy: L.TargetTypePharmacy }
        };
        return (map[group] && map[group][v]) || v;
    };

    const fillVocab = (el, values, group, restrict) => {
        if (!el) return;
        const current = sel(el);
        const list = (values || []).filter(v => !restrict || restrict.includes(v));
        el.innerHTML = list.map(v => `<option value="${esc(v)}"${v === current ? ' selected' : ''}>${esc(labelForVocab(group, v))}</option>`).join('');
        if (current && list.includes(current)) el.value = current;
    };

    const getJson = async url => {
        const res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
        const body = await res.json().catch(() => ({}));
        return res.ok ? (body.data ?? body) : null;
    };

    const asItems = data => Array.isArray(data) ? data : (data?.items || data?.Items || []);
    const pick = (o, keys) => { for (const k of keys) { if (o[k] !== undefined && o[k] !== null && o[k] !== '') return o[k]; } return ''; };

    // ── contract-driven vocab ────────────────────────────────────────────────────────────────────────────────────────
    const loadContract = async () => {
        const contract = await getJson(contractUrl);
        const vocab = contract?.vocabularies || {};
        fillVocab(targetTypeEl, vocab.targetTypes, 'targetTypes');
        fillVocab(visitPurposeEl, vocab.purposes, 'purposes');
        fillVocab(visitTypeEl, vocab.visitTypes, 'visitTypes');
        fillVocab(resourceTypeEl, vocab.resourceTypes, 'resourceTypes');
        // On create a plan may be born only draft or planned (the rest are reached through transitions).
        fillVocab(planStatusEl, vocab.statuses, 'statuses', ['draft', 'planned']);
    };

    // ── target picker (chained to target type) ───────────────────────────────────────────────────────────────────────
    const loadTargets = async () => {
        if (!targetIdEl) return;
        const type = targetTypeEl?.value || 'account';
        const current = sel(targetIdEl);
        const currentDisplay = targetIdEl.dataset.display || '';
        let source = accountsUrl, mapVal = ['accountId', 'id'], mapLabel = ['accountName', 'name', 'accountCode'];
        if (type === 'contact') { source = contactsUrl; mapVal = ['contactId', 'id']; mapLabel = ['displayName', 'name']; }
        if (type === 'account-contact-link') {
            // No dedicated link picker here (foundation). Keep the current id as the sole option so an edit is not lost.
            targetIdEl.innerHTML = `<option value="">${esc(L.SelectPlaceholder || '')}</option>`
                + (current ? `<option value="${esc(current)}" selected>${esc(currentDisplay || current)}</option>` : '');
            return;
        }

        let items = [];
        try { items = asItems(await getJson(source)); } catch (e) { items = []; }
        if (type === 'pharmacy') {
            items = items.filter(i => String(pick(i, ['accountType', 'AccountType']) || '').toLowerCase() === 'pharmacy' || !pick(i, ['accountType', 'AccountType']));
        }

        const head = `<option value="">${esc(L.SelectPlaceholder || '')}</option>`;
        const opts = items.map(i => { const v = pick(i, mapVal); return `<option value="${esc(v)}">${esc(pick(i, mapLabel) || v)}</option>`; }).join('');
        const keeps = current && items.some(i => String(pick(i, mapVal)) === String(current));
        const orphan = current && !keeps ? `<option value="${esc(current)}" selected>${esc(currentDisplay || current)}</option>` : '';
        targetIdEl.innerHTML = head + orphan + opts;
        if (keeps) targetIdEl.value = current;
    };

    // ── journey → stage picker + content-source marker ──────────────────────────────────────────────────────────────
    const setContentSource = value => {
        if (contentSourceEl) contentSourceEl.value = value || '';
        if (!contentSourceBadge) return;
        if (!value) { contentSourceBadge.classList.add('d-none'); return; }
        contentSourceBadge.classList.remove('d-none');
        contentSourceBadge.textContent = value === 'strategy' ? (L.ContentSourceStrategy || 'strategy') : (L.ContentSourceManual || 'manual');
        contentSourceBadge.className = 'badge ' + (value === 'strategy' ? 'bg-label-primary' : 'bg-label-secondary');
    };

    const loadJourneys = async () => {
        if (!journeyEl) return;
        const current = sel(journeyEl);
        let items = [];
        try { items = asItems(await getJson(journeysUrl)); } catch (e) { items = []; }
        // Only published journeys are selectable (the runtime enforces this too — V17).
        items = items.filter(i => { const s = String(pick(i, ['journeyStatus', 'status']) || '').toLowerCase(); return !s || s === 'published'; });
        const head = `<option value="">${esc(L.SelectPlaceholder || '')}</option>`;
        journeyEl.innerHTML = head + items.map(i => { const v = pick(i, ['journeyId', 'id']); return `<option value="${esc(v)}"${String(v) === current ? ' selected' : ''}>${esc(pick(i, ['journeyName', 'name']) || v)}</option>`; }).join('');
        if (current) journeyEl.value = current;
        await loadStages(false);
    };

    const loadStages = async (userChanged) => {
        if (!stageEl || !journeyEl) return;
        const journeyId = journeyEl.value;
        const currentStage = sel(stageEl);
        if (!journeyId) {
            stageEl.innerHTML = `<option value="">${esc(L.SelectPlaceholder || '')}</option>`;
            setContentSource('');
            return;
        }
        let items = [];
        try { items = asItems(await getJson(`/CRM/PlannedVisits/api/journeys/${journeyId}/stages`)); } catch (e) { items = []; }
        const head = `<option value="">${esc(L.SelectPlaceholder || '')}</option>`;
        stageEl.innerHTML = head + items.map(i => { const v = pick(i, ['stageId', 'id']); return `<option value="${esc(v)}"${String(v) === currentStage ? ' selected' : ''}>${esc(pick(i, ['stageName', 'name', 'stageCode']) || v)}</option>`; }).join('');
        if (!userChanged && currentStage) stageEl.value = currentStage;
        // A rep-selected journey/stage is a manual content position in FU01 (no strategy resolver here).
        setContentSource(journeyId ? 'manual' : '');
    };

    targetTypeEl?.addEventListener('change', () => { void loadTargets(); });
    journeyEl?.addEventListener('change', () => { void loadStages(true); });
    stageEl?.addEventListener('change', () => setContentSource(journeyEl?.value ? 'manual' : ''));

    (async function init() {
        await loadContract();
        await loadTargets();
        await loadJourneys();
        // Reflect the server's stored content-source on first paint (edit), else derive from the current journey.
        const stored = contentSourceEl?.value;
        if (stored) setContentSource(stored);
        else setContentSource(journeyEl?.value ? 'manual' : '');
    })();
})(window, document);
