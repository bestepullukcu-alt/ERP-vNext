/* K-TABS — the shared tab card's behaviour (assets/js/diten-tabs.js, the REAL file) in Node with a small fake DOM. Run by
   TabCardWebTests with the script path as argv[2] and the direction as argv[3] ('ltr' | 'rtl'); prints one JSON object:
   the state after a click, after → / ← / Home / End, the panels, the roving tabindex and the diten:tab-change events. */
const scriptPath = process.argv[2];
const dir = process.argv[3] || 'ltr';
const events = [];
const panels = {};
function el(id, attrs) {
  const e = {
    id: id, attrs: Object.assign({}, attrs || {}), _cls: new Set(), listeners: {}, focused: false, disabled: false,
    getAttribute(k) { return this.attrs[k] == null ? null : this.attrs[k]; }, setAttribute(k, v) { this.attrs[k] = String(v); },
    addEventListener(t, fn) { (this.listeners[t] = this.listeners[t] || []).push(fn); },
    dispatchEvent(ev) { events.push({ type: ev.type, detail: ev.detail, bubbles: ev.bubbles }); return true; },
    focus() { tabs.forEach(t => { t.focused = false; }); this.focused = true; }
  };
  e.classList = { toggle: (c, on) => { if (on === undefined ? !e._cls.has(c) : on) { e._cls.add(c); } else { e._cls.delete(c); } }, contains: c => e._cls.has(c) };
  return e;
}
const holder = el('holder', { dir: dir });
const list = el('list', { role: 'tablist', 'data-diten-tabs': '' });
const tabs = ['a', 'b', 'c'].map((k, i) => {
  const t = el('tab-' + k, { role: 'tab', 'data-tab-key': k, 'aria-controls': 'pane-' + k, 'aria-selected': i === 0 ? 'true' : 'false', tabindex: i === 0 ? '0' : '-1' });
  if (i === 0) { t._cls.add('active'); }
  t.closest = q => (q === '[role="tablist"]' ? list : q === '[role="tab"]' ? t : null);
  t.ownerDocument = doc;
  panels['pane-' + k] = el('pane-' + k);
  if (i === 0) { panels['pane-' + k]._cls.add('active'); panels['pane-' + k]._cls.add('show'); }
  return t;
});
var doc = { documentElement: el('html', {}), getElementById: id => panels[id] || null, querySelectorAll: q => (q === '[data-diten-tabs]' ? [list] : []), addEventListener() {} };
tabs.forEach(t => { t.ownerDocument = doc; });
list.querySelectorAll = q => (q === '[role="tab"]' ? tabs : []);
list.contains = n => tabs.indexOf(n) > -1;
list.closest = q => (q === '[dir]' ? holder : null);
list.ownerDocument = doc;
global.CustomEvent = function (type, init) { this.type = type; this.detail = init && init.detail; this.bubbles = !!(init && init.bubbles); };
global.window = { document: doc, CustomEvent: global.CustomEvent };
global.document = doc;
require(scriptPath);
const T = global.window.DitenTabs;
const bound = T.init(doc);
const state = () => ({
  active: tabs.filter(t => t._cls.has('active')).map(t => t.id),
  selected: tabs.map(t => t.getAttribute('aria-selected')).join(','),
  tabindex: tabs.map(t => t.getAttribute('tabindex')).join(','),
  panels: Object.keys(panels).filter(k => panels[k]._cls.has('active') && panels[k]._cls.has('show')),
  focused: (tabs.find(t => t.focused) || {}).id || null
});
const key = (from, k) => { let prevented = false; list.listeners.keydown.forEach(fn => fn({ key: k, target: { closest: () => from }, preventDefault() { prevented = true; } })); return prevented; };
const out = { bound: bound, initial: state() };
list.listeners.click.forEach(fn => fn({ target: { closest: q => (q === '[role="tab"]' ? tabs[1] : null) }, preventDefault() {} }));
out.click = state();
out.right = (key(tabs[1], 'ArrowRight'), state());
out.rightWraps = (key(tabs[2], 'ArrowRight'), state());
out.left = (key(tabs[0], 'ArrowLeft'), state());
out.home = (key(tabs[2], 'Home'), state());
out.end = (key(tabs[0], 'End'), state());
out.otherKeyPrevented = key(tabs[2], 'a');
out.events = events.slice();
out.sameTabEvents = (() => { const n = events.length; T.activate(tabs[2]); return events.length - n; })();
process.stdout.write(JSON.stringify(out));
