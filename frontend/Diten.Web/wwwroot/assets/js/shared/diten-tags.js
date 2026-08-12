'use strict';

// ─────────────────────────────────────────────────────────────────────────────
// DitenTags — the ONE tag input for the whole app.
//
// WHY IT EXISTS. Three screens constructed Tagify independently — the task form
// (#taskTags), the platform tenant-security screen and the governance
// tenant-security screen — on the LIBRARY'S DEFAULT CSS. Change one and the
// other two drift, which is the pattern this project has corrected five times.
// Tagify is now constructed HERE and nowhere else; a test greps for that.
//
// LAYOUT "C" — the box stays EMPTY, the tags flow BELOW it.
// The reason is ALIGNMENT, not taste. Every control on these forms is one line
// (38px MEASURED in this theme), and chips rendered inside grow the box as they
// accumulate, breaking the row the field shares with its neighbour. The golden-reference round fixed that
// alignment; leaving the chips inside would undo it. So Tagify keeps doing what
// it is good at — parsing, delimiters, patterns, paste, keyboard — its in-box
// chips are removed by the stylesheet, and this module renders them as a strip
// underneath, driven by the SAME `tagify.value`.
//
// CARRIED OVER, NOT REWRITTEN (breaking either changes what the API receives):
//   • originalInputValueFormat → the underlying <input> stays comma-separated,
//     which is why every payload builder can keep splitting on commas
//   • __tagify                 → the double-initialisation guard
//
// All styles live in backbone-custom.css under `.diten-tags*` (FG-003): this
// file writes classes and text, never a style attribute. Strings come from the
// SharedResource bridge (`#tags-l10n`, rendered by the layout) so one sentence
// is not copied into three screen resources.
//
// Usage:
//   DitenTags.enhance(document, { selector: '#taskTags' });
//   DitenTags.enhance(document, { selector: '#allowedIps',
//                                 tagify: { pattern, delimiters, maxTags } });
// ─────────────────────────────────────────────────────────────────────────────
(function (global) {
    const STRIP_CLASS = 'diten-tags-strip';

    /*
     * The l10n payload the layout publishes. Read LAZILY (first use, then cached) rather than at load time:
     * this script may be parsed before the bridge element exists, and a component that captured an empty
     * payload once would render placeholder-less chrome forever.
     */
    let strings = null;

    const l10n = () => {
        if (strings) { return strings; }
        strings = {};
        try {
            const node = global.document.getElementById('tags-l10n');
            if (node) { strings = JSON.parse(node.textContent || '{}') || {}; }
        } catch (_) { /* a missing/!valid payload degrades to the fallbacks below, never to a crash */ }
        return strings;
    };

    // A miss falls back to English rather than printing the key: this chrome sits inside a form the user is
    // filling in, and "TagsPlaceholder" in the box is worse than a word in the wrong language.
    const FALLBACK = {
        TagsPlaceholder: 'Type a tag and press Enter',
        TagsCount: '{0} tags',
        TagsRemove: 'Remove {0}'
    };

    const t = (key) => l10n()[key] || FALLBACK[key] || '';
    const tf = (key, value) => String(t(key)).replace('{0}', String(value));

    /** The values currently held, as plain strings — the one shape the strip renders from. */
    const valuesOf = (tagify) => (tagify && Array.isArray(tagify.value) ? tagify.value : [])
        .map((entry) => (entry && entry.value !== undefined ? entry.value : entry))
        .filter((value) => value !== undefined && value !== null && String(value).length > 0);

    const removeStrip = (input) => {
        const existing = input.__ditenTagsStrip;
        if (existing && existing.parentNode) { existing.parentNode.removeChild(existing); }
        input.__ditenTagsStrip = null;
    };

    /*
     * Render (or remove) the strip under the control.
     *
     * With NO tags the strip is not rendered at all — an empty band is its own kind of noise, and it would also
     * occupy exactly the space the alignment fix reclaimed.
     */
    const renderStrip = (input, tagify) => {
        const doc = global.document;
        const values = valuesOf(tagify);

        if (values.length === 0) { removeStrip(input); return; }

        let strip = input.__ditenTagsStrip;
        if (!strip) {
            strip = doc.createElement('div');
            strip.className = STRIP_CLASS;
            // AFTER the control Tagify put in the DOM (its own <tags> scope), so the chips read as belonging to
            // the field above them rather than to whatever follows.
            const anchor = (tagify.DOM && tagify.DOM.scope) || input;
            anchor.parentNode.insertBefore(strip, anchor.nextSibling);
            input.__ditenTagsStrip = strip;
        }

        strip.textContent = '';

        values.forEach((value) => {
            const chip = doc.createElement('span');
            chip.className = 'diten-tags-chip';

            const label = doc.createElement('span');
            label.className = 'diten-tags-chip-label';
            label.textContent = value;
            chip.appendChild(label);

            const remove = doc.createElement('button');
            remove.type = 'button';
            remove.className = 'diten-tags-remove';
            // Named, not "×": a screen reader announcing "button" alone cannot say WHICH tag it drops.
            remove.setAttribute('aria-label', tf('TagsRemove', value));
            remove.textContent = '×';
            remove.addEventListener('click', () => {
                /*
                 * `removeTags(value)` — the API that takes a VALUE. `removeTag()` takes a DOM tag element, and
                 * calling it with a data object is a silent no-op: the chip stayed, the value stayed, and only a
                 * live click revealed it (the unit double had been too forgiving to notice).
                 *
                 * Removal goes through the LIBRARY either way, so its own value, input and events stay
                 * authoritative — deleting our chip alone would leave the real value behind and the payload
                 * unchanged.
                 */
                tagify.removeTags(value);
            });
            chip.appendChild(remove);

            strip.appendChild(chip);
        });

        const count = doc.createElement('span');
        count.className = 'diten-tags-count';
        count.textContent = tf('TagsCount', values.length);
        strip.appendChild(count);
    };

    /**
     * Enhance every input matching `options.selector` inside `root`.
     * Returns the Tagify instances CREATED by this call (an already-enhanced node yields none).
     */
    const enhance = (root, options) => {
        const scope = root || global.document;
        const settings = options || {};
        const selector = settings.selector;

        if (!scope || !scope.querySelectorAll || !selector) { return []; }
        // No library ⇒ the plain <input> keeps working. A comma-separated box is a degraded control, not a
        // broken screen, and throwing here would take the whole page's boot with it.
        if (typeof global.Tagify !== 'function') { return []; }

        const created = [];

        Array.from(scope.querySelectorAll(selector))
            .filter((node) => !node.__tagify)
            .forEach((node) => {
                const tagify = new global.Tagify(node, Object.assign({}, settings.tagify, {
                    // THE CONTRACT. Applied last so a caller cannot replace it by accident: three payload
                    // builders split this value on commas, and losing it changes what the API receives on all
                    // three screens at once, silently.
                    originalInputValueFormat: (values) => values.map((v) => v.value).join(',')
                }));

                node.__tagify = tagify;

                /*
                 * MARK the control Tagify built. Without this the stylesheet below never attaches: Tagify
                 * renders its own <tags class="tagify form-control"> scope element, and every `.diten-tags*`
                 * rule keys off a class only we can add. Missing it looked exactly like a working component —
                 * the JS ran, the value was right, and the layout was the library default.
                 */
                if (tagify.DOM && tagify.DOM.scope) { tagify.DOM.scope.classList.add('diten-tags'); }

                /*
                 * MARK THE ORIGINAL INPUT TOO — and this one was a live defect, caused by this very component.
                 *
                 * Tagify hides the element it replaces with an ADJACENT-SIBLING rule
                 * (tagify.css:121 `.tagify + input { position:absolute; left:-9999em; transform:scale(0) }`).
                 * The strip is inserted BETWEEN the <tags> element and that input, so the two stopped being
                 * adjacent, the vendor rule stopped matching, and the theme's `form-control` painted the input
                 * back as a visible 38px box — showing the raw comma value under the chips.
                 *
                 * Marking the input means the hiding no longer depends on DOM order at all, so moving the strip
                 * again cannot resurrect this.
                 */
                node.classList.add('diten-tags-source');

                // The strip mirrors the library's value, so every path that changes it — typing, paste,
                // delimiters, our own remove button, a programmatic add — lands here through one event.
                tagify.on('change', () => renderStrip(node, tagify));
                renderStrip(node, tagify);

                created.push(tagify);
            });

        return created;
    };

    global.DitenTags = { enhance, STRIP_CLASS };
})(typeof window !== 'undefined' ? window : globalThis);
