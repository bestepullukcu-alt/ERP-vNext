/**
 * MOD-0167-FU02 Segments — Details page: the read-only membership list plus the resolve preview.
 *
 * The preview PERSISTS NOTHING. It is a report computed on demand, which is why it is a button and not a stored
 * number: a member count on a segment document would be a second, quietly ageing source of truth.
 * Eliminated candidates are shown WITH their reason, so nobody has to guess why someone is missing.
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

    const segmentType = (document.querySelector('.segments-details')?.dataset.segmentType || '').trim();

    /**
     * Shows only the membership blocks this segment type can actually produce, mirroring the runtime:
     *   static  -> membership IS the manual list, so there is no rule to preview
     *   dynamic -> the rule decides everything, and a manual row is refused with a 400
     *   hybrid  -> both
     */
    const applySegmentTypeVisibility = () => {
        document.getElementById('resolvePreview')?.classList.toggle('d-none', segmentType === 'static');
        document.getElementById('manualMembersBlock')?.classList.toggle('d-none', segmentType === 'dynamic');
    };

    /** A readable label with the id kept as quiet provenance underneath - never the id alone. */
    const subjectCell = (displayName, subjectId) => displayName
        ? `<div class="fw-medium text-heading">${esc(displayName)}</div>
           <div class="text-muted small">${esc(subjectId)}</div>`
        : `<div class="text-muted small">${esc(subjectId)}</div>`;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    const loadMembers = async () => {
        const body = document.getElementById('detailsMemberBody');
        if (!body) return;
        try {
            const data = await envelope(await fetch(`${endpoint}/segments/${segmentId}/targets?includeArchived=true`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            const items = data?.items || [];
            body.innerHTML = items.length === 0
                ? `<tr><td colspan="6" class="text-muted">${esc(L.EmptyState || '')}</td></tr>`
                : items.map(m => `
                    <tr class="${m.isArchived ? 'opacity-50' : ''}">
                        <td class="fw-medium text-heading">${esc(m.subjectDisplayName || '—')}</td>
                        <td class="text-muted small">${esc(m.subjectId)}</td>
                        <td><span class="badge bg-label-${m.membershipMode === 'manual-include' ? 'success' : 'danger'}">${esc(m.membershipMode)}</span></td>
                        <td>${esc(m.selectionReason)}</td>
                        <td>${esc(String(m.effectiveFrom || '').slice(0, 10))}</td>
                        <td>${m.isArchived ? esc(L.Yes || 'Yes') : esc(L.No || 'No')}</td>
                    </tr>`).join('');
        } catch (error) {
            body.innerHTML = `<tr><td colspan="6" class="text-danger">${esc(error.message || L.ErrorState)}</td></tr>`;
        }
    };

    const runResolve = async () => {
        const summary = document.getElementById('resolveSummary');
        const table = document.getElementById('resolveMembers');
        const body = document.getElementById('resolveMembersBody');
        if (!summary || !table || !body) return;

        summary.classList.remove('d-none');
        summary.innerHTML = `<span class="text-muted">${esc(L.Loading || '')}</span>`;

        try {
            const response = await fetch(`${endpoint}/segments/${segmentId}/resolve`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
                // includeExcluded: an elimination must be as visible as an acceptance.
                body: JSON.stringify({ limit: 100, offset: 0, includeExcluded: true })
            });

            const data = await envelope(response);
            const rows = (data.members || []).concat(data.excluded || []);

            summary.innerHTML = `
                <div class="d-flex flex-wrap gap-3">
                    <span class="badge bg-label-primary">${esc(L.CandidateCount || 'Candidates')}: ${esc(data.candidateCount)}</span>
                    <span class="badge bg-label-success">${esc(L.MatchedCount || 'Members')}: ${esc(data.matchedCount)}</span>
                    <span class="badge bg-label-warning">${esc(L.ExcludedCount || 'Excluded')}: ${esc(data.excludedCount)}</span>
                    ${data.segmentEffective ? '' : `<span class="badge bg-label-secondary">${esc((data.reasonCodes || []).join(', '))}</span>`}
                    <span class="text-muted small">${esc(L.MembershipNeverStoredHelp || '')}</span>
                </div>`;

            table.classList.toggle('d-none', rows.length === 0);
            body.innerHTML = rows.map(m => `
                <tr>
                    <td>${subjectCell(m.subjectDisplayName, m.subjectId)}</td>
                    <td><span class="badge bg-label-${m.verdict === 'member' ? 'success' : m.verdict === 'unknown' ? 'secondary' : 'danger'}">${esc(m.verdict)}</span></td>
                    <td>${esc(m.membershipSource || '—')}</td>
                    <td class="small">${esc((m.reasonCodes || []).join(', '))}</td>
                </tr>`).join('');
        } catch (error) {
            // A 422 here is the ceiling refusing to hand back a partial list, not a crash: the message says so.
            summary.innerHTML = `<span class="text-danger">${esc(error.message || L.ErrorState)}</span>`;
            table.classList.add('d-none');
        }
    };

    document.addEventListener('click', event => {
        if (event.target.closest('#btnResolve')) { event.preventDefault(); void runResolve(); return; }

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
