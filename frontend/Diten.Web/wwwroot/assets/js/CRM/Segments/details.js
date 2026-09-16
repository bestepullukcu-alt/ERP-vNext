/**
 * MOD-0167-FU02 Segments — Details page: the read-only membership list plus the resolve preview.
 *
 * The preview PERSISTS NOTHING. It is a report computed on demand, which is why it is a button and not a stored
 * number: a member count on a segment document would be a second, quietly ageing source of truth.
 * Eliminated candidates are shown WITH their reason, so nobody has to guess why someone is missing.
 *
 * WP-SEG-DETAILS — the markup this file writes was ported to the mockup's `segd-*` row/chip language (avatar + name +
 * secondary line + verdict/mode badge). The BEHAVIOUR is unchanged: the 3-state segment-type toggle, the SAVED
 * `/resolve` fetch (no fake setTimeout, no fixed count), the manual-member load and the lifecycle actions all stay.
 * The idle → running → result state machine only toggles server-rendered blocks; every number comes from the API.
 */
(function (window, document) {
    'use strict';

    const L = window.SegmentsL10n || window.L10n || {};
    const memberHost = document.getElementById('detailsMemberList');
    const previewHost = document.getElementById('resolvePreview');
    const endpoint = (memberHost || previewHost)?.dataset.endpoint || '/CRM/Segments/api';
    const segmentId = (memberHost || previewHost)?.dataset.segmentId;
    if (!segmentId) return;

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));

    /** Two-letter avatar from a display name, honorifics dropped, falling back to the id when the name is absent. */
    const initials = (name, id) => {
        const clean = String(name ?? '').replace(/\b(Prof\.?|Do[çc]\.?|Dr\.?|Assoc\.?|Asst\.?)\s*/gi, '').trim();
        const parts = clean.split(/\s+/).filter(Boolean);
        if (parts.length === 0) return String(id ?? '?').slice(0, 2).toUpperCase();
        return parts.slice(-2).map(w => w[0]).join('').toUpperCase();
    };

    const segmentType = (document.querySelector('.segments-details')?.dataset.segmentType || '').trim();

    /**
     * Shows only the membership blocks this segment type can actually produce, mirroring the runtime:
     *   static  -> membership IS the manual list, so there is no rule to preview
     *   dynamic -> the rule decides everything, and a manual row is refused with a 400
     *   hybrid  -> both
     */
    const applySegmentTypeVisibility = () => {
        document.getElementById('resolveSection')?.classList.toggle('segd-hidden', segmentType === 'static');
        document.getElementById('manualMembersBlock')?.classList.toggle('segd-hidden', segmentType === 'dynamic');
    };

    /** avatar + name + quiet id — the readable label with the id kept as provenance underneath, never the id alone. */
    const personCell = (displayName, subjectId, extraAvatarClass) => `
        <span class="segd-col-person">
            <span class="segd-avatar ${extraAvatarClass || ''}">${esc(initials(displayName, subjectId))}</span>
            <span class="segd-person-body">
                <span class="segd-name">${esc(displayName || subjectId || '—')}</span>
                ${displayName ? `<span class="segd-subid segd-mono">${esc(subjectId)}</span>` : ''}
            </span>
        </span>`;

    const verdictClass = v => v === 'member' ? 'segd-badge-ok' : v === 'unknown' ? 'segd-badge-unknown' : 'segd-badge-no';

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    const setStatMembers = value => {
        const el = document.getElementById('statMembersValue');
        if (el) el.textContent = value;
    };

    // ---- Manual membership rows (static / hybrid) --------------------------------------------------------------
    const loadMembers = async () => {
        const body = document.getElementById('detailsMemberBody');
        if (!body) return;
        try {
            const data = await envelope(await fetch(`${endpoint}/segments/${segmentId}/targets?includeArchived=true`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            const items = data?.items || [];
            body.innerHTML = items.length === 0
                ? `<div class="segd-empty-row">${esc(L.NoMembers || L.EmptyState || '')}</div>`
                : items.map(m => `
                    <div class="segd-row ${m.isArchived ? 'is-archived' : ''}">
                        ${personCell(m.subjectDisplayName, m.subjectId, m.membershipMode === 'manual-exclude' ? 'segd-avatar-out' : '')}
                        <span class="segd-col-secondary">${esc(m.subjectSecondaryLabel || '—')}</span>
                        <span class="segd-col-mode"><span class="segd-badge ${m.membershipMode === 'manual-include' ? 'segd-badge-ok' : 'segd-badge-no'}">${esc(m.membershipMode)}</span></span>
                        <span class="segd-col-reason"><span class="segd-reason-text">${esc(m.selectionReason || '')}</span></span>
                        <span class="segd-col-from">${esc(String(m.effectiveFrom || '').slice(0, 10))}</span>
                    </div>`).join('');

            // counts: kept-in / kept-out, from the loaded rows (active only) — honest, not a stored number.
            const active = items.filter(m => !m.isArchived);
            const keptIn = active.filter(m => m.membershipMode === 'manual-include').length;
            const keptOut = active.filter(m => m.membershipMode === 'manual-exclude').length;
            const inEl = document.getElementById('manualKeptIn');
            const outEl = document.getElementById('manualKeptOut');
            if (inEl) inEl.textContent = keptIn;
            if (outEl) outEl.textContent = keptOut;
            // For a static segment the membership IS the kept-in list, so that is the "Members" stat.
            if (segmentType === 'static') setStatMembers(keptIn.toLocaleString());
        } catch (error) {
            body.innerHTML = `<div class="segd-error-row">${esc(error.message || L.ErrorState)}</div>`;
        }
    };

    // ---- Resolve preview (dynamic / hybrid) — SAVED /resolve, persists nothing --------------------------------
    const setResolveState = state => {
        document.getElementById('resolveIdle')?.classList.toggle('segd-hidden', state !== 'idle');
        document.getElementById('resolveRunning')?.classList.toggle('segd-hidden', state !== 'running');
        document.getElementById('resolveResult')?.classList.toggle('segd-hidden', state !== 'done');
    };

    const runResolve = async () => {
        const summary = document.getElementById('resolveSummary');
        const memberBody = document.getElementById('resolveMembersBody');
        const excludedWrap = document.getElementById('resolveExcluded');
        const excludedBody = document.getElementById('resolveExcludedBody');
        const btn = document.getElementById('btnResolve');
        if (!summary || !memberBody) return;

        setResolveState('running');
        if (btn) btn.disabled = true;

        try {
            const response = await fetch(`${endpoint}/segments/${segmentId}/resolve`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                // includeExcluded: an elimination must be as visible as an acceptance.
                body: JSON.stringify({ limit: 100, offset: 0, includeExcluded: true })
            });

            const data = await envelope(response);
            const members = data.members || [];
            const excluded = data.excluded || [];
            const fromManual = members.filter(m => String(m.membershipSource || '').startsWith('manual')).length;

            setStatMembers(Number(data.matchedCount ?? members.length).toLocaleString());

            summary.innerHTML = `
                <span class="segd-chip-stat segd-chip-included"><span class="segd-chip-value">${esc(Number(data.matchedCount ?? 0).toLocaleString())}</span><span class="segd-chip-label">${esc(L.MatchedCount || 'Members')}</span></span>
                <span class="segd-chip-stat segd-chip-dropped"><span class="segd-chip-value">${esc(Number(data.excludedCount ?? 0).toLocaleString())}</span><span class="segd-chip-label">${esc(L.ExcludedCount || 'Excluded')}</span></span>
                <span class="segd-chip-stat segd-chip-manual"><span class="segd-chip-value">${esc(fromManual.toLocaleString())}</span><span class="segd-chip-label">${esc(L.FromManual || 'from manual rows')}</span></span>
                ${data.segmentEffective === false ? `<span class="segd-chip-stat"><span class="segd-chip-label">${esc((data.reasonCodes || []).join(', '))}</span></span>` : ''}
                <span class="segd-resolvedat">${esc(new Date().toLocaleString())}</span>`;

            // A genuine 0 is a result, not a failure: the summary chips still show "0 included" and this line names it
            // (fetch/HTTP errors take the catch path below and render a visible segd-error-row instead).
            memberBody.innerHTML = members.length === 0
                ? `<div class="segd-empty-row">${esc(L.NoMembers || L.EmptyState || '')}</div>`
                : members.map(m => `
                    <div class="segd-row">
                        ${personCell(m.subjectDisplayName, m.subjectId)}
                        <span class="segd-col-secondary">${esc(m.subjectSecondaryLabel || '—')}</span>
                        <span class="segd-col-verdict"><span class="segd-badge ${verdictClass(m.verdict)}">${esc(m.verdict)}</span></span>
                        <span class="segd-col-source">${esc(m.membershipSource || '—')}</span>
                        <span class="segd-col-reasons segd-mono">${esc((m.reasonCodes || []).join(', '))}</span>
                    </div>`).join('');

            if (excludedWrap && excludedBody) {
                excludedWrap.classList.toggle('segd-hidden', excluded.length === 0);
                excludedBody.innerHTML = excluded.map(x => `
                    <div class="segd-row">
                        <span class="segd-x-name">${esc(x.subjectDisplayName || x.subjectId || '—')}</span>
                        <span class="segd-x-secondary">${esc(x.subjectSecondaryLabel || '—')}</span>
                        <span class="segd-x-code segd-mono">${esc((x.reasonCodes || []).join(', '))}</span>
                    </div>`).join('');
            }

            setResolveState('done');
        } catch (error) {
            // A 422 here is the ceiling refusing to hand back a partial list, not a crash: the message says so.
            summary.innerHTML = `<div class="segd-error-row">${esc(error.message || L.ErrorState)}</div>`;
            memberBody.innerHTML = '';
            if (excludedWrap) excludedWrap.classList.add('segd-hidden');
            setResolveState('done');
        } finally {
            if (btn) btn.disabled = false;
        }
    };

    // ---- Stored-rule JSON toggle -------------------------------------------------------------------------------
    const toggleStoredJson = btn => {
        const pre = document.getElementById('storedJson');
        if (!pre) return;
        const nowHidden = pre.classList.toggle('segd-hidden');
        btn.textContent = nowHidden ? (btn.dataset.showLabel || '') : (btn.dataset.hideLabel || '');
    };

    document.addEventListener('click', event => {
        if (event.target.closest('#btnResolve')) { event.preventDefault(); void runResolve(); return; }

        const jsonBtn = event.target.closest('#btnToggleJson');
        if (jsonBtn) { event.preventDefault(); toggleStoredJson(jsonBtn); return; }

        const activate = event.target.closest('.js-activate-segment');
        if (activate) {
            event.preventDefault();
            window.showConfirm?.(L.ActivateSegmentConfirm, async () => {
                try {
                    const response = await fetch(`${endpoint}/segments/${activate.dataset.id}/activate`, {
                        method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                    });
                    if (!response.ok) await envelope(response);
                    window.showToast?.(L.RecordActivated || '', 'success');
                    window.location.reload();
                } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            }, { type: 'question', confirmButtonText: L.ActivateSegment });
            return;
        }

        const newVersion = event.target.closest('.js-new-version');
        if (!newVersion) return;
        event.preventDefault();
        window.showConfirm?.(L.NewVersionConfirm, async () => {
            try {
                const created = await envelope(await fetch(`${endpoint}/segments/${newVersion.dataset.id}/new-version`, {
                    method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
                }));
                if (created) window.location.href = `/CRM/Segments/Edit/${created}`;
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { type: 'question', confirmButtonText: L.NewVersion });
    });

    applySegmentTypeVisibility();
    // A dynamic segment has no manual rows by construction, so the table is hidden and the request is not worth making.
    if (segmentType !== 'dynamic') {
        void loadMembers();
    }
})(window, document);
