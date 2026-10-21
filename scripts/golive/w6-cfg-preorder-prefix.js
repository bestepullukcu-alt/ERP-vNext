// WP-VW-W6 W6-CFG — kiraci ORD oneki (MOD-0168, RCS-001 §5.4): satan tuzel kisilik -> 2–5 harf onek (MG-ORD-000001)
//
// NASIL: Web'e kiraci yoneticisi olarak giris yap -> /Commercial/PreOrderSettings (On siparis ayarlari) sayfasini ac ->
//        F12 -> Console -> bu dosyanin TAMAMINI yapistir -> Enter. Varsayilan KURU calismadir (yalniz plan; yazma yok).
//        En alttaki TARGET: 'local' (yerel 97c5: GMG tuzel kisiligi 1754b820… -> MG) ya da 'live' (canli be823006: onek
//        listesi BOS — betik tuzel kisilikleri ve mevcut onekleri listeler; oneki KULLANICI secer ve LIVE_PREFIXES'e yazar).
//        Plan dogruysa APPLY degerini true yapip tekrar yapistir.
//
// Ne yapar: sayfanin kendi ucunu kullanir (GET / PUT /Commercial/PreOrderSettings/api/settings, sayfanin basliklari +
// oturum cerezi; parola / belirtec betikte YOK). CRM onek listesinin TAMAMINI degistirir; bu yuzden betik mevcut listeyi
// okur ve yalniz eksik satiri ekler (tekrar calistirmak bir sey eklemez), okudugu surumle yazar (surum kaydiysa CRM 409).
// Var olan oneke DOKUNMAZ: numara vermis onek degistirilemez / silinemez (CRM 409 pre_order_prefix_locked); ayni tuzel
// kisilikte farkli onek ya da baska tuzel kisilikte ayni onek -> DUR. Diger ayarlar (onay kurali, baglanti suresi, kademe,
// yeniden deneme) gonderilmez = degismez. Geri alma: numara vermemis onek ayarlar ekranindan satir silinerek kaldirilir.

(function (APPLY, TARGET) {
  const API = '/Commercial/PreOrderSettings/api';
  const PREFIX = /^[A-Z]{2,5}$/;
  const WANTED = {
    // legalEntityId: tam GUID ya da tek bir tuzel kisiligi secen bas kismi (8+ karakter).
    local: [{ legalEntityId: '1754b820', prefix: 'MG' }],
    // Canli: once KURU calistir, listeden sec, sonra buraya yaz. Ornek: { legalEntityId: '<guid>', prefix: 'MG' }
    live: [],
  };

  // (current settings, companies, wanted) -> { rows, merged | null }. merged = the full list to PUT, null = nothing to write.
  const plan = (settings, companies, wanted) => {
    const current = (settings && settings.companyPrefixes) || [];
    const merged = current.map(p => ({ legalEntityId: p.legalEntityId, prefix: p.prefix }));
    const rows = [];
    let stop = false;
    for (const w of wanted) {
      const prefix = String(w.prefix || '').trim().toUpperCase();
      const needle = String(w.legalEntityId || '').trim().toLowerCase();
      const hits = needle.length >= 8 ? companies.filter(c => String(c.id).toLowerCase().startsWith(needle)) : [];
      const row = { tuzelKisilik: w.legalEntityId, onek: prefix, durum: '' };
      rows.push(row);
      if (!PREFIX.test(prefix)) { row.durum = 'DUR: onek 2–5 harf A–Z olmali'; stop = true; continue; }
      if (hits.length !== 1) { row.durum = `DUR: tuzel kisilik ${hits.length ? 'tek degil' : 'bulunamadi'} (${hits.length})`; stop = true; continue; }
      const le = hits[0];
      row.tuzelKisilik = `${le.name} (${le.countryCode || '?'}) ${le.id}`;
      const mine = merged.find(p => String(p.legalEntityId).toLowerCase() === String(le.id).toLowerCase());
      if (mine && mine.prefix === prefix) { row.durum = 'zaten var'; continue; }
      if (mine) { row.durum = `DUR: bu tuzel kisilikte baska onek var (${mine.prefix}) — betik degistirmez`; stop = true; continue; }
      if (merged.some(p => p.prefix === prefix)) { row.durum = `DUR: ${prefix} baska tuzel kisilikte`; stop = true; continue; }
      merged.push({ legalEntityId: le.id, prefix });
      row.durum = 'EKLENECEK';
    }
    const changed = rows.some(r => r.durum === 'EKLENECEK');
    return { rows, merged: !stop && changed ? merged : null, stop };
  };

  const run = async (win, apply, target) => {
    const log = win.console;
    const wanted = WANTED[target];
    if (!wanted) { log.error(`TARGET 'local' ya da 'live' olmali (verilen: ${target}).`); return null; }
    const headers = json => {
      const h = (win.DitenDataTable && win.DitenDataTable.getAuthHeaders && win.DitenDataTable.getAuthHeaders(json)) || {};
      if (json && !h['Content-Type']) h['Content-Type'] = 'application/json';
      return h;
    };
    const call = async (method, path, body) => {
      const res = await win.fetch(API + path, {
        method, credentials: 'same-origin', headers: headers(body !== undefined),
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await res.text();
      let json = null;
      try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
      if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${json?.reasonCode || json?.code || ''} ${(json?.errors || []).join('; ') || text.slice(0, 300)}`);
      return json && json.data !== undefined ? json.data : json;
    };

    log.log(`%cW6-CFG ORD oneki (${target}) — ${apply ? 'UYGULA' : 'KURU CALISMA (yazma yok)'}`, 'font-weight:bold;font-size:14px');
    const settings = await call('GET', '/settings');
    const le = await call('GET', '/legal-entities');
    if (!le || !le.available) { log.error('DUR: MDM tuzel kisilik listesi alinamadi (available=false).'); return null; }
    const companies = (le.items || []).map(x => ({ id: x.id, name: x.name, countryCode: x.countryCode }));
    const prefixOf = id => ((settings && settings.companyPrefixes) || []).find(p => String(p.legalEntityId).toLowerCase() === String(id).toLowerCase());
    log.log('Satan tuzel kisilikler (MDM) ve mevcut onekler:');
    log.table(companies.map(c => ({ id: c.id, ad: c.name, ulke: c.countryCode || '', onek: (prefixOf(c.id) || {}).prefix || '—' })));

    const p = plan(settings, companies, wanted);
    if (!wanted.length) log.log('Bu hedefte eklenecek onek tanimli degil (canli: oneki sec, WANTED.live satirina yaz).');
    if (p.rows.length) log.table(p.rows);
    if (!p.merged) { log.log(p.stop ? 'Yazma yok: DUR satirlarini coz.' : 'Yazilacak bir sey yok.'); return p; }
    if (!apply) { log.log('Plan dogruysa en alttaki APPLY degerini true yapip tekrar yapistir.'); return p; }

    const saved = await call('PUT', '/settings', {
      companyPrefixes: p.merged,
      expectedVersion: settings && !settings.isDefault ? settings.version : null,
    });
    log.log(`OK — onekler: ${(saved.companyPrefixes || []).map(x => `${x.prefix}=${x.legalEntityId}`).join(', ')} (surum ${saved.version}).`);
    return Object.assign(p, { saved });
  };

  const api = { WANTED, plan, run };
  if (typeof module !== 'undefined' && module.exports) { module.exports = api; return; }
  run(window, APPLY, TARGET).catch(e => console.error(e.message));
})(/* APPLY = */ false, /* TARGET = */ 'local');
