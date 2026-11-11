// WP-MVP1-FIX-LIVE4 (FU-0173-REQ-COUNTER, G1 live run: "Zorunlu: 8 / 9" stayed after the FEFO reason was written) — the SHARED required
// tracker (assets/js/custom/required-fields-tracker.js, unchanged) and the SHIPPED band (assets/js/Expiry/expiry-bands.js), with the band's
// reason field rendered from the real partial (Views/Shared/Expiry/_FefoBand.cshtml) inside the form's source block, hidden at load.
// Band open → the reason counts (8 / 9) and writing it reaches 9 / 9; band closed → the reason does not count (8 / 8).
// CT HOLD (2026-10-21): no jsdom — a clean checkout (and CI phase 1) has no node_modules. The production scripts run unchanged on the
// small DOM below: exactly the surface the tracker and the band touch (elements, attributes, class lists, the few selectors they use,
// events, MutationObserver with microtask delivery like the browser's). ExpiryBandExemptTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const web = (p) => readFileSync(path.join(repo, 'frontend/Diten.Web', p), 'utf8');

// ── a small DOM: what required-fields-tracker.js and expiry-bands.js use, nothing more ──────────────────────────────────────────────
const VOID = new Set(['input', 'br', 'img', 'hr', 'meta', 'link']);
const createDom = () => {
    const observers = [];
    const notify = (record) => observers.forEach((o) => o.targets.forEach(({ node, options }) => {
        const inside = node === record.target || (options.subtree && contains(node, record.target));
        if (!inside) return;
        if (record.type === 'attributes' && !(options.attributes && (!options.attributeFilter || options.attributeFilter.includes(record.attributeName)))) return;
        if (record.type === 'childList' && !options.childList) return;
        o.queue.push(record);
        if (!o.scheduled) { o.scheduled = true; queueMicrotask(() => { o.scheduled = false; const batch = o.queue.splice(0); if (batch.length) o.callback(batch, o); }); }
    }));
    const contains = (root, node) => { for (let n = node; n; n = n.parentElement) if (n === root) return true; return false; };

    class ClassList {
        constructor(el) { this.el = el; }
        get set() { return new Set((this.el.attrs.class || '').split(/\s+/).filter(Boolean)); }
        write(set) { this.el.setAttribute('class', [...set].join(' ')); }
        contains(c) { return this.set.has(c); }
        add(...cs) { const s = this.set; cs.forEach((c) => s.add(c)); this.write(s); }
        remove(...cs) { const s = this.set; cs.forEach((c) => s.delete(c)); this.write(s); }
        toggle(c, on) { const s = this.set; const want = on === undefined ? !s.has(c) : !!on; want ? s.add(c) : s.delete(c); this.write(s); return want; }
    }

    class Element {
        constructor(tag) { this.tagName = tag.toUpperCase(); this.nodeType = 1; this.attrs = {}; this.children = []; this.parentElement = null; this.listeners = {}; this._text = ''; this._value = undefined; this.classList = new ClassList(this); }
        get parentNode() { return this.parentElement; }
        get firstChild() { return this.children[0] || null; }
        get id() { return this.attrs.id || ''; }
        get className() { return this.attrs.class || ''; }
        set className(v) { this.setAttribute('class', v); }
        get type() { return (this.attrs.type || (this.tagName === 'INPUT' ? 'text' : this.tagName.toLowerCase())).toLowerCase(); }
        get value() { return this._value ?? (this.tagName === 'TEXTAREA' ? this._text : this.attrs.value ?? ''); }
        set value(v) { this._value = String(v); }
        get required() { return 'required' in this.attrs; }
        set required(on) { on ? this.setAttribute('required', '') : this.removeAttribute('required'); }
        get disabled() { return 'disabled' in this.attrs; }
        set disabled(on) { on ? this.setAttribute('disabled', '') : this.removeAttribute('disabled'); }
        get checked() { return false; }
        get dataset() { return Object.fromEntries(Object.entries(this.attrs).filter(([k]) => k.startsWith('data-')).map(([k, v]) => [k.slice(5).replace(/-([a-z])/g, (m, c) => c.toUpperCase()), v])); }
        get textContent() { return this._text + this.children.map((c) => c.textContent).join(''); }
        set textContent(v) { this.children.forEach((c) => { c.parentElement = null; }); this.children = []; this._text = String(v); }
        set innerHTML(html) { this.textContent = ''; parseInto(this, html); }
        get innerHTML() { return this.textContent; }
        hasAttribute(k) { return k in this.attrs; }
        getAttribute(k) { return k in this.attrs ? this.attrs[k] : null; }
        setAttribute(k, v) { const old = this.attrs[k]; this.attrs[k] = String(v); if (old !== this.attrs[k]) notify({ type: 'attributes', target: this, attributeName: k }); }
        removeAttribute(k) { if (k in this.attrs) { delete this.attrs[k]; notify({ type: 'attributes', target: this, attributeName: k }); } }
        appendChild(child) { return this.insertBefore(child, null); }
        insertBefore(child, ref) {
            child.parentElement = this;
            const at = ref ? this.children.indexOf(ref) : -1;
            at < 0 ? this.children.push(child) : this.children.splice(at, 0, child);
            notify({ type: 'childList', target: this, addedNodes: [child] });
            return child;
        }
        addEventListener(type, fn) { (this.listeners[type] ||= []).push(fn); }
        dispatch(type) { (this.listeners[type] || []).forEach((fn) => fn({ type, target: this })); }
        checkValidity() { return true; }
        focus() {}
        descendants() { return this.children.flatMap((c) => [c, ...c.descendants()]); }
        matches(selector) { return matches(this, selector); }
        closest(selector) { for (let n = this; n; n = n.parentElement) if (matches(n, selector)) return n; return null; }
        querySelectorAll(selector) { return this.descendants().filter((e) => matches(e, selector)); }
        querySelector(selector) { return this.querySelectorAll(selector)[0] || null; }
    }

    // selectors used by the two scripts: tag, #id, .class, [attr], [attr="v"], :not(<simple>), descendant, comma lists
    const simple = (el, s) => {
        let rest = s;
        const not = /:not\(([^)]*)\)/g;
        for (const m of s.matchAll(not)) if (simple(el, m[1])) return false;
        rest = rest.replace(not, '');
        const tag = /^[a-z]+/i.exec(rest)?.[0];
        if (tag && el.tagName !== tag.toUpperCase()) return false;
        for (const m of rest.matchAll(/#([\w-]+)/g)) if (el.id !== m[1]) return false;
        for (const m of rest.matchAll(/\.([\w-]+)/g)) if (!el.classList.contains(m[1])) return false;
        for (const m of rest.matchAll(/\[([\w-]+)(?:="([^"]*)")?\]/g)) if (!(m[1] in el.attrs) || (m[2] !== undefined && el.attrs[m[1]] !== m[2])) return false;
        return true;
    };
    const matches = (el, selector) => selector.split(',').map((s) => s.trim()).some((s) => {
        const parts = s.split(/\s+/);
        if (!simple(el, parts.at(-1))) return false;
        let n = el.parentElement;
        for (let i = parts.length - 2; i >= 0; i--) { while (n && !simple(n, parts[i])) n = n.parentElement; if (!n) return false; n = n.parentElement; }
        return true;
    });

    // a tolerant parser for the markup these tests feed (the real partial, the tracker's badge template)
    const parseInto = (parent, html) => {
        const stack = [parent];
        for (const m of html.matchAll(/<\/([a-z0-9]+)\s*>|<([a-z0-9]+)((?:\s+[\w-]+(?:="[^"]*")?)*)\s*(\/?)>|([^<]+)/gi)) {
            const top = stack.at(-1);
            if (m[1]) { if (stack.length > 1) stack.pop(); continue; }
            if (m[2]) {
                const el = new Element(m[2]);
                for (const a of (m[3] || '').matchAll(/([\w-]+)(?:="([^"]*)")?/g)) el.attrs[a[1]] = a[2] ?? '';
                top.appendChild(el);
                if (!m[4] && !VOID.has(m[2].toLowerCase())) stack.push(el);
                continue;
            }
            if (m[5] && m[5].trim()) top._text += m[5];
        }
    };

    const body = new Element('body');
    const listeners = {};
    const document = {
        documentElement: { lang: 'tr' },
        body,
        createElement: (tag) => new Element(tag),
        getElementById: (id) => body.descendants().find((e) => e.id === id) || null,
        querySelector: (s) => body.querySelector(s),
        querySelectorAll: (s) => body.querySelectorAll(s),
        addEventListener: (type, fn) => { (listeners[type] ||= []).push(fn); },
        fire: (type) => (listeners[type] || []).forEach((fn) => fn({ type }))
    };
    class MutationObserver {
        constructor(callback) { this.callback = callback; this.targets = []; this.queue = []; this.scheduled = false; observers.push(this); }
        observe(node, options) { this.targets.push({ node, options }); }
        disconnect() { this.targets = []; }
    }
    return { document, MutationObserver, parse: (html) => parseInto(body, html) };
};

// the real band partial, Razor stripped (directives, comments, localizer calls → plain text)
const bandPartial = web('Views/Shared/Expiry/_FefoBand.cshtml')
    .replace(/@\*[\s\S]*?\*@/g, '').replace(/^@(using|inject)[^\n]*\n/gm, '').replace(/@Localizer\["[^"]+"\]/g, 'Metin');
const FIELDS = ['movementType', 'itemId', 'sku', 'quantity', 'uomId', 'transactionDate', 'postingDate'];
const markup = `<script id="expiry-band-l10n" type="application/json"></script>
<form id="formStockMovement">
${FIELDS.map((f) => `<label for="${f}">${f} <span class="text-danger">*</span></label><input id="${f}" required value="x">`).join('\n')}
<div data-matrix="source"><label for="source">source <span class="text-danger">*</span></label><input id="source" required value="x">${bandPartial}</div>
<div class="actions"><button type="submit" class="btn btn-primary">Kaydet</button></div>
</form>`;

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

const page = async () => {
    const { document, MutationObserver, parse } = createDom();
    parse(markup);
    document.getElementById('expiry-band-l10n').textContent = JSON.stringify({ 'Expiry.Band.FefoFirst': 'Daha yakın tarihli lot var: {0} (SKT {1})' });
    const window = { L10n: { RequiredProgressText: 'Zorunlu: {0} / {1}' }, CSS: { escape: (v) => String(v) } };
    window.window = window;
    const order = { order: { eligible: [{ lotId: 'early', lotNumber: 'AMX-2604', expiryDate: '2028-03-31' }, { lotId: 'late', lotNumber: 'AMX-2611', expiryDate: '2028-11-30' }] }, policy: { fefoOutboundMode: 'Reason' } };
    const context = vm.createContext({
        window, document, MutationObserver, console, URLSearchParams, Date, queueMicrotask,
        $: () => ({ hasClass: () => false, on() {}, val: () => '' }),
        fetch: async () => ({ ok: true, status: 200, json: async () => order })
    });
    vm.runInContext(web('wwwroot/assets/js/custom/required-fields-tracker.js'), context);   // the shared tracker, as shipped
    vm.runInContext(web('wwwroot/assets/js/Expiry/expiry-bands.js'), context);              // our band, as shipped
    document.fire('DOMContentLoaded');
    await flush();
    assert.equal(document.querySelectorAll('[data-required-tracker]').length, 1);
    const counter = () => document.querySelector('.required-text').textContent;
    const open = async (movementType) => {
        await window.ExpiryBands.movement.onSource({ direction: 'out', movementType, fromStatus: 'AVAILABLE', itemId: 'i', sku: 'Lsku:s', warehouseId: 'w', lotId: 'late' });
        await flush();
    };
    return { document, counter, open };
};

test('band open: the reason counts, and writing it moves the counter to 9 / 9', async () => {
    const { document, counter, open } = await page();
    assert.equal(counter(), 'Zorunlu: 8 / 8');
    await open('GOODS_ISSUE_SALES');
    assert.equal(counter(), 'Zorunlu: 8 / 9');
    const reason = document.getElementById('ex-band-reason');
    reason.value = 'Müşteri talebiyle bu lot sevk ediliyor';
    reason.dispatch('input');
    await flush();
    assert.equal(counter(), 'Zorunlu: 9 / 9');
});

test('band closed (an exempt type): the reason is not counted', async () => {
    const { counter, open } = await page();
    await open('COUNT_LOSS');
    assert.equal(counter(), 'Zorunlu: 8 / 8');
});
