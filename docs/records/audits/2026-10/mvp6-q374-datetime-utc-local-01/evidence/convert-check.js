// Mirrors details.js prefill/instant (fixed) and the old prefill, under the process time zone.
const pad = (n) => String(n).padStart(2, '0');
const localInputValue = (w) => `${w.getFullYear()}-${pad(w.getMonth() + 1)}-${pad(w.getDate())}T${pad(w.getHours())}:${pad(w.getMinutes())}`;
const instant = (v) => { const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(v); if (!m) return null;
  const [, y, mo, d, h, mi, s] = m.map(Number); const x = new Date(y, mo - 1, d, h, mi, s || 0); return x.getFullYear() === y && x.getMonth() === mo - 1 && x.getDate() === d && x.getHours() === h && x.getMinutes() === mi ? x : null; };
const oldInstant = (v) => { const x = new Date(v); return Number.isNaN(x.getTime()) ? null : x; };
const now = new Date(Math.floor(Date.now() / 60000) * 60000);
const fixedField = localInputValue(now), oldField = now.toISOString().slice(0, 16);
console.log(`zone ${Intl.DateTimeFormat().resolvedOptions().timeZone} offset ${-now.getTimezoneOffset()} min`);
console.log(`real instant       ${now.toISOString()}`);
console.log(`fixed: field ${fixedField} -> sent ${instant(fixedField).toISOString()}  delta ${(instant(fixedField) - now) / 60000} min`);
console.log(`old:   field ${oldField} -> sent ${oldInstant(oldField).toISOString()}  delta ${(oldInstant(oldField) - now) / 60000} min`);
console.log(`empty/garbage -> ${instant('')} / ${instant('2026-13-99T99:99')}`);
