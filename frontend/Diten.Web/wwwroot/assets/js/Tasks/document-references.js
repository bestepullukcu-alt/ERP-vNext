'use strict';

/*
 * ── DCP-005 slice 3 — choosing which controlled documents a task follows ─────────────────────────────────
 *
 * Lives in its own file rather than inside form.js (1500 lines) for the same reason the type picker does: this
 * is one question with one answer, and a reader looking for "how does a citation get chosen" should find it
 * without reading a form.
 *
 * THREE rules, all of them stated by the pack and none of them invented here:
 *
 *   1. A blocked row is SHOWN and REFUSED, with the register's own reason next to it. Hiding it leaves "where
 *      is that SOP" with nowhere to look. This is the same rule the import screen follows — one behaviour, not
 *      a second one that happens to look similar.
 *   2. A type's governing documents are a SUGGESTION: pre-ticked, removable, and never the only thing citable.
 *   3. This file never sends a title, a code or a version. It collects UIDs; the server resolves and freezes.
 */
(function (global) {
    const t = (key) => global.TasksL10n?.t?.(key) ?? key;
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    const controller = (root) => {
        const searchBox = root.querySelector('#taskDocumentSearch');
        const results = root.querySelector('#taskDocumentResults');
        const chosenList = root.querySelector('#taskDocumentChosen');
        const note = root.querySelector('#taskDocumentSuggestionNote');
        if (!searchBox || !results || !chosenList || !note) { return null; }

        // uid → { documentUid, documentCode, title, documentVersion }. A Map so a second pick of the same
        // document is the same entry, not a duplicate row the server would have to de-duplicate.
        const chosen = new Map();

        const renderChosen = () => {
            chosenList.innerHTML = [...chosen.values()].map((doc) => `
                <li class="tasks-docref-chip" data-uid="${esc(doc.documentUid)}">
                    <span class="tasks-docref-code">${esc(doc.documentCode)}</span>
                    <span class="tasks-docref-title">${esc(doc.title)}</span>
                    ${doc.documentVersion ? `<span class="tasks-docref-version">${esc(doc.documentVersion)}</span>` : ''}
                    <button type="button" class="btn btn-sm btn-label-secondary tasks-docref-remove"
                            data-remove-uid="${esc(doc.documentUid)}"
                            aria-label="${esc(t('docRefRemove'))} ${esc(doc.documentCode)}">
                        <i class="bx bx-x" aria-hidden="true"></i>
                    </button>
                </li>`).join('');
        };

        const row = (doc) => {
            /*
             * ⚠ A blocked row renders as a NON-BUTTON with `aria-disabled` and its reason as TEXT, not as a
             * greyed control with a tooltip. A tooltip is not readable by someone tabbing through, and "why can
             * I not pick this" is exactly the question this row exists to answer.
             */
            if (!doc.linkableInErp) {
                return `
                    <li class="tasks-docref-result tasks-docref-result--blocked" aria-disabled="true">
                        <span class="tasks-docref-code">${esc(doc.documentCode)}</span>
                        <span class="tasks-docref-title">${esc(doc.title)}</span>
                        <span class="tasks-docref-blocked">${esc(t('docRefBlocked'))}${
                            doc.linkBlockedReason ? ` — ${esc(doc.linkBlockedReason)}` : ''}</span>
                    </li>`;
            }

            return `
                <li class="tasks-docref-result">
                    <button type="button" class="tasks-docref-pick" role="option"
                            data-pick-uid="${esc(doc.documentUid)}"
                            data-code="${esc(doc.documentCode)}" data-title="${esc(doc.title)}"
                            data-version="${esc(doc.documentVersion || '')}">
                        <span class="tasks-docref-code">${esc(doc.documentCode)}</span>
                        <span class="tasks-docref-title">${esc(doc.title)}</span>
                    </button>
                </li>`;
        };

        const renderResults = (docs) => {
            results.innerHTML = docs.length
                ? docs.map(row).join('')
                : `<li class="tasks-docref-result tasks-docref-empty">${esc(t('docRefNoResults'))}</li>`;
            searchBox.setAttribute('aria-expanded', docs.length ? 'true' : 'false');
        };

        let searchTimer = null;
        const search = () => {
            const term = searchBox.value.trim();
            if (term.length < 2) { results.innerHTML = ''; searchBox.setAttribute('aria-expanded', 'false'); return; }

            global.TasksApi.searchDocuments(term)
                .then((result) => {
                    if (!result.ok || !Array.isArray(result.data)) {
                        results.innerHTML = `<li class="tasks-docref-result tasks-docref-empty">${
                            esc(t('docRefUnavailable'))}</li>`;
                        return;
                    }
                    renderResults(result.data);
                })
                .catch((error) => {
                    if (error?.authHandled) { return; }
                    results.innerHTML = `<li class="tasks-docref-result tasks-docref-empty">${
                        esc(t('docRefUnavailable'))}</li>`;
                });
        };

        searchBox.addEventListener('input', () => {
            global.clearTimeout(searchTimer);
            searchTimer = global.setTimeout(search, 250);
        });

        results.addEventListener('click', (event) => {
            const button = event.target.closest('[data-pick-uid]');
            if (!button) { return; }
            const uid = button.getAttribute('data-pick-uid');
            chosen.set(uid, {
                documentUid: uid,
                documentCode: button.getAttribute('data-code'),
                title: button.getAttribute('data-title'),
                documentVersion: button.getAttribute('data-version') || null,
            });
            searchBox.value = '';
            results.innerHTML = '';
            searchBox.setAttribute('aria-expanded', 'false');
            renderChosen();
        });

        chosenList.addEventListener('click', (event) => {
            const button = event.target.closest('[data-remove-uid]');
            if (!button) { return; }
            chosen.delete(button.getAttribute('data-remove-uid'));
            renderChosen();
        });

        /*
         * ── What a type suggests, and the FOUR different things "nothing to suggest" can mean ──────────────
         *
         * MEASURED against the counterparty's own seed on 2026-08-26: 15 of the 31 types have no citable
         * governing document. One names nothing at all; seven name documents the register does not contain;
         * seven name documents the register refuses to link. Two more name one citable and one blocked, so the
         * suggestion is PARTIAL. A single empty box would look identical in all four cases and tell the person
         * choosing nothing about which one they are in — which is the state the pack calls "a state, not an
         * error", and a state has to say what it is.
         */
        const applySuggestion = (data) => {
            (data.suggestions || []).forEach((doc) => {
                chosen.set(doc.documentUid, {
                    documentUid: doc.documentUid,
                    documentCode: doc.documentCode,
                    title: doc.title,
                    documentVersion: doc.documentVersion || null,
                });
            });
            renderChosen();

            const unresolved = (data.unresolvedUids || []).length;
            const blocked = (data.blockedSuggestions || []).length;

            let message = '';
            if ((data.suggestions || []).length > 0) {
                message = t('docRefSuggestedByType');
            } else if ((data.namedCount || 0) === 0) {
                message = t('docRefNoneNamed');
            } else if (blocked > 0 && unresolved === 0) {
                message = t('docRefAllBlocked').replace('{0}', String(blocked));
            } else {
                message = t('docRefNotInRegister').replace('{0}', String(unresolved));
            }

            note.textContent = message;
            note.hidden = false;
        };

        const onTypeChosen = (typeId, organizationCode) => {
            if (!typeId) { note.hidden = true; note.textContent = ''; return; }

            global.TasksApi.typeGoverningDocuments(typeId, organizationCode)
                .then((result) => {
                    if (!result.ok || !result.data) { note.hidden = true; return; }
                    applySuggestion(result.data);
                })
                .catch((error) => {
                    if (error?.authHandled) { return; }
                    // A suggestion that cannot be read must not block the form: the author loses a shortcut, not
                    // the ability to cite anything, because the search box is still there.
                    note.hidden = true;
                });
        };

        return {
            uids: () => [...chosen.keys()],
            /*
             * ⚠ Hydration is BY VALUE, from the task's own frozen citations — never re-read from the register.
             * An edit form that re-resolved what it renders would refresh a title on screen, the author would
             * save it, and the freeze would be gone with nothing to notice.
             */
            hydrate: (references) => {
                chosen.clear();
                (references || []).forEach((r) => chosen.set(r.documentUid, {
                    documentUid: r.documentUid,
                    documentCode: r.documentCode,
                    title: r.title,
                    documentVersion: r.documentVersion || null,
                }));
                renderChosen();
            },
            onTypeChosen,
            applySuggestion,
        };
    };

    const boot = () => {
        const root = document.querySelector('[data-task-document-section]');
        if (!root || !global.TasksApi) { return; }

        const api = controller(root);
        if (!api) { return; }
        global.TaskDocumentReferences = api;

        const typeSelect = document.getElementById('taskTypeId');
        if (typeSelect) {
            typeSelect.addEventListener('change', () => api.onTypeChosen(typeSelect.value, root.getAttribute('data-org-code')));
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

    // Exported for the suite: the DOM wiring above is not the part worth testing twice.
    if (typeof module !== 'undefined' && module.exports) { module.exports = { controller }; }
})(typeof window !== 'undefined' ? window : globalThis);
