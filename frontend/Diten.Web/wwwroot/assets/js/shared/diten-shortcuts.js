'use strict';

/*
 * DitenShortcuts — the product's ONE keyboard-shortcut layer (WP-UI-SHORTCUTS-01, BL-438).
 *
 * Before this file the only shortcuts lived inside WorkCenterNext/app.js, on its own `document` keydown, and the
 * hint menu that was meant to document them listed four of seven. Every screen that wanted a key would have
 * written a third copy of the same guard. Now a screen DECLARES its keys and this file owns the listener, the
 * silence rule, the conflict check and the `?` list — so the list cannot drift from what is actually bound.
 *
 *   DitenShortcuts.register(scope, [{ keys, actionKey, description?, handler, when? }], { titleKey? })
 *   DitenShortcuts.unregister(scope)
 *   DitenShortcuts.open()          → the `?` dialog (also what a toolbar keyboard button calls)
 *   DitenShortcuts.list()          → what the dialog prints, scope by scope
 *
 * `keys`       one key or several for the same action, as KeyboardEvent.key spells them ('j', 'Enter', 'Escape',
 *              'ArrowLeft', ' ' for Space). Letters match either case, as the WorkCenterNext handler always did.
 * `actionKey`  the action's resx suffix: its description is `Shortcuts.{actionKey}` in SharedResource (7 languages),
 *              unless an already-localized `description` is passed.
 * `handler`    runs with the event. It owns `preventDefault` (the layer never swallows a key on its own), and it
 *              returns `false` to say "not mine after all" — the key then falls through to an older scope.
 * `when`       optional predicate; a `false` answer is the same as not being registered for this press.
 *
 * ── THE SILENCE RULE ───────────────────────────────────────────────────────────────────────────────────────
 * A layer that catches keys at `document` level can eat a letter somebody was typing, which is data loss. So NO
 * shortcut runs while:
 *   · focus is in an input, a textarea, a select or anything contenteditable;
 *   · a Bootstrap modal or offcanvas is open, or a SweetAlert is on screen — the key belongs to that layer;
 *   · Ctrl, Cmd or Alt is held — those chords belong to the browser and the OS.
 * `?` is the one exception: it opens the list from anywhere except a form field (where it is a character).
 *
 * ── CONFLICTS ARE LOUD ─────────────────────────────────────────────────────────────────────────────────────
 * The same key twice in one scope is a console error and the SECOND entry is refused. Silently letting the later
 * one win is how a screen ends up with a key that does something other than what its list says.
 *
 * Strings: this file holds none. It reads `Shortcuts.*` from the JSON block `Views/Shared/_DitenShortcuts.cshtml`
 * emits (SharedResource, all keys under the prefix, current culture).
 */
(function (global) {
    const doc = global.document;
    const L10N_ELEMENT_ID = 'diten-shortcuts-l10n';
    const LIST_KEY = '?';

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

    // ── Strings ──────────────────────────────────────────────────────────────────────────────────────────
    let strings = null;
    const readStrings = () => {
        if (strings) { return strings; }
        const node = doc && doc.getElementById(L10N_ELEMENT_ID);
        if (!node) { return {}; }
        try {
            strings = JSON.parse(node.textContent || '{}');
        } catch (error) {
            console.error('[DitenShortcuts] #diten-shortcuts-l10n is not valid JSON.', error);
            strings = {};
        }
        return strings;
    };
    const warned = new Set();
    const t = (key) => {
        const value = readStrings()[key];
        if (typeof value === 'string' && value !== '') { return value; }
        // A missing translation is shown as its key — visibly wrong, never silently English.
        if (!warned.has(key)) {
            warned.add(key);
            console.warn(`[DitenShortcuts] missing string "${key}" (is _DitenShortcuts.cshtml on this page?)`);
        }
        return key;
    };

    // ── Keys ─────────────────────────────────────────────────────────────────────────────────────────────
    const normalizeKey = (key) => (key === ' ' ? ' ' : String(key ?? '').toLowerCase());

    const KEY_CAPS = {
        ' ': () => t('Shortcuts.Key.Space'),
        escape: () => 'Esc',
        enter: () => 'Enter',
        arrowleft: () => '←',
        arrowright: () => '→',
        arrowup: () => '↑',
        arrowdown: () => '↓',
        home: () => 'Home',
        end: () => 'End'
    };
    const keyCap = (key) => (KEY_CAPS[key] ? KEY_CAPS[key]() : key);

    // ── The silence rule ─────────────────────────────────────────────────────────────────────────────────
    const FIELD_TAGS = /^(INPUT|TEXTAREA|SELECT)$/;
    const isFormField = (element) => {
        if (!element || element.nodeType !== 1) { return false; }
        if (FIELD_TAGS.test(element.tagName)) { return true; }
        if (element.isContentEditable) { return true; }
        // jsdom does not implement isContentEditable; the attribute is the same fact, read directly.
        return !!(element.closest && element.closest('[contenteditable]:not([contenteditable="false"])'));
    };

    const OVERLAY_SELECTOR = '.modal.show, .offcanvas.show, .offcanvas.showing, .swal2-container';
    const overlayOpen = () => {
        if (doc.querySelector(OVERLAY_SELECTOR)) { return true; }
        const swal = global.Swal;
        return !!(swal && typeof swal.isVisible === 'function' && swal.isVisible());
    };

    const hasModifier = (event) => !!(event.ctrlKey || event.metaKey || event.altKey);

    // ── The registry ─────────────────────────────────────────────────────────────────────────────────────
    // Carried over from a previous load of this file, so a page that includes it twice keeps what was registered.
    const previous = global.DitenShortcuts;
    const scopes = (previous && previous.__scopes instanceof Map) ? previous.__scopes : new Map();

    const register = (scope, entries, options) => {
        if (!scope || typeof scope !== 'string' || !Array.isArray(entries)) {
            console.error('[DitenShortcuts] register(scope, entries) needs a scope name and an array of entries.');
            return 0;
        }
        let record = scopes.get(scope);
        if (!record) {
            record = { scope, titleKey: null, entries: [], byKey: new Map() };
            scopes.set(scope, record);
        }
        if (options && options.titleKey) { record.titleKey = options.titleKey; }

        let accepted = 0;
        entries.forEach((raw) => {
            const keys = (Array.isArray(raw && raw.keys) ? raw.keys : [raw && raw.keys])
                .filter((key) => key !== undefined && key !== null && key !== '')
                .map(normalizeKey);
            const actionKey = raw && raw.actionKey;
            if (!keys.length || typeof actionKey !== 'string' || !actionKey || typeof raw.handler !== 'function') {
                console.error(`[DitenShortcuts] scope "${scope}": an entry needs keys, an actionKey and a handler; `
                    + `refused ${JSON.stringify(actionKey || null)}.`);
                return;
            }
            if (keys.indexOf(LIST_KEY) !== -1) {
                console.error(`[DitenShortcuts] scope "${scope}": "?" is reserved for the shortcut list; `
                    + `refused "${actionKey}".`);
                return;
            }
            const clash = keys.find((key, index) => record.byKey.has(key) || keys.indexOf(key) !== index);
            if (clash !== undefined) {
                const holder = record.byKey.get(clash);
                console.error(`[DitenShortcuts] scope "${scope}": key "${clash}" is already bound to `
                    + `"${holder ? holder.actionKey : actionKey}"; refused "${actionKey}".`);
                return;
            }
            const entry = {
                keys,
                actionKey,
                description: typeof raw.description === 'string' ? raw.description : null,
                handler: raw.handler,
                when: typeof raw.when === 'function' ? raw.when : null
            };
            keys.forEach((key) => record.byKey.set(key, entry));
            record.entries.push(entry);
            accepted += 1;
        });
        return accepted;
    };

    const unregister = (scope) => scopes.delete(scope);

    // ── Dispatch ─────────────────────────────────────────────────────────────────────────────────────────
    const dispatch = (event) => {
        const key = normalizeKey(event.key);
        if (isFormField(event.target)) { return; }
        if (key === LIST_KEY) {
            event.preventDefault();
            open();
            return;
        }
        if (hasModifier(event) || overlayOpen()) { return; }

        // The newest scope answers first; an entry that declines (`when` false, or the handler returns false)
        // passes the key on to the next one.
        const records = Array.from(scopes.values()).reverse();
        for (let i = 0; i < records.length; i += 1) {
            const entry = records[i].byKey.get(key);
            if (!entry) { continue; }
            if (entry.when && !entry.when(event)) { continue; }
            let result;
            try {
                result = entry.handler(event);
            } catch (error) {
                console.error(`[DitenShortcuts] "${entry.actionKey}" failed.`, error);
                return;
            }
            if (result === false) { continue; }
            return;
        }
    };

    // ── The `?` list ─────────────────────────────────────────────────────────────────────────────────────
    const list = () => {
        const out = [{
            scope: 'global',
            title: t('Shortcuts.Scope.Global'),
            entries: [{ keys: [LIST_KEY], actionKey: 'ShowList', description: t('Shortcuts.ShowList') }]
        }];
        scopes.forEach((record) => {
            if (!record.entries.length) { return; }
            out.push({
                scope: record.scope,
                title: record.titleKey ? t(record.titleKey) : record.scope,
                entries: record.entries.map((entry) => ({
                    keys: entry.keys.slice(),
                    actionKey: entry.actionKey,
                    description: entry.description || t(`Shortcuts.${entry.actionKey}`)
                }))
            });
        });
        return out;
    };

    const renderList = () => {
        const sections = list().map((group) => {
            const rows = group.entries.map((entry) => {
                const caps = entry.keys
                    .map((key) => `<kbd class="diten-kbd">${esc(keyCap(key))}</kbd>`)
                    .join('<span class="diten-shortcuts-or">/</span>');
                return `<tr data-diten-shortcut="${esc(entry.actionKey)}">`
                    + `<th scope="row" class="diten-shortcuts-keys">${caps}</th>`
                    + `<td class="diten-shortcuts-desc">${esc(entry.description)}</td></tr>`;
            }).join('');
            return `<section class="diten-shortcuts-scope" data-diten-shortcuts-scope="${esc(group.scope)}">`
                + `<h6 class="diten-shortcuts-scope-title">${esc(group.title)}</h6>`
                + `<table class="table table-sm diten-shortcuts-table"><tbody>${rows}</tbody></table></section>`;
        }).join('');
        const noteClass = typeof global.DitenDialogAppearance === 'function'
            && global.DitenDialogAppearance.description ? global.DitenDialogAppearance.description : '';
        return `<div class="diten-shortcuts" data-diten-shortcuts-list>`
            + `<p class="${esc(noteClass)}">${esc(t('Shortcuts.SilenceNote'))}</p>${sections}</div>`;
    };

    const open = () => {
        const swal = global.Swal;
        const look = global.DitenDialogAppearance;
        if (!swal || typeof swal.fire !== 'function' || typeof look !== 'function') {
            console.error('[DitenShortcuts] SweetAlert2 or DitenDialogAppearance is unavailable '
                + '(is _GlobalConfirmation loaded?); the shortcut list cannot open.');
            return null;
        }
        const appearance = look({ width: '32rem' });
        const icon = typeof look.iconHtml === 'function' ? look.iconHtml('info', 'bx-bxs-keyboard') : '';
        return swal.fire(Object.assign({}, appearance, {
            title: `${icon}<span>${esc(t('Shortcuts.DialogTitle'))}</span>`,
            html: renderList(),
            showCancelButton: false,
            confirmButtonText: esc(t('Shortcuts.Close')),
            customClass: Object.assign({}, appearance.customClass, {
                actions: 'd-flex justify-content-end mt-3 w-100 gap-2'
            })
        }));
    };

    // ── One listener per document ────────────────────────────────────────────────────────────────────────
    if (previous && typeof previous.__uninstall === 'function') { previous.__uninstall(); }
    const onKeydown = (event) => dispatch(event);
    if (doc) { doc.addEventListener('keydown', onKeydown); }

    global.DitenShortcuts = {
        register,
        unregister,
        open,
        list,
        renderList,
        t,
        __scopes: scopes,
        __uninstall: () => { if (doc) { doc.removeEventListener('keydown', onKeydown); } }
    };
})(typeof window !== 'undefined' ? window : globalThis);
