// WP-VW-W6 W6-CFG — canli rol yetkileri (be823006): eczane on siparisi (MOD-0168) + fiyat listeleri / kurallar (MOD-0156)
//
// NASIL: canli Web'e kiraci yoneticisi olarak giris yap -> /RoleAssignments (Rol Izinleri) sayfasini ac -> F12 ->
//        Console -> bu dosyanin TAMAMINI yapistir -> Enter. Varsayilan KURU calismadir (yalniz plan; yazma yok).
//        Plan dogruysa en alttaki APPLY degerini true yapip tekrar yapistir.
//
// Ne yapar: grant-roles-live.js deseni — ekranin kendi "ata" ucunu (/RoleAssignments/assign, sayfadaki antiforgery
// belirteci) kullanir; yalniz eksik anahtarlari ekler (tekrar calistirmak bir sey eklemez), hicbir yetkiyi kaldirmaz,
// rol / izin olusturmaz. Parola / belirtec betikte YOK: oturum cerezi + sayfanin kendi basliklari.
// Matris = scripts/rbac/w6_cfg_role_matrix.json (K-W6-ROLES; test iki kopyanin ayni oldugunu denetler). '.*' satiri
// katalogdaki (CRM manifestinden kayitli) o onekin butun commercial.* anahtarlaridir; hicbir role dusmeyen commercial.*
// anahtari "ATANMADI" satiriyla raporlanir (sessiz atlama yok). Katalogda olmayan anahtar "DUR" satiridir.
// Canlida bu yolla eklenen satirin isaretcisi yoktur (ekranin ucu CreatedBy'yi oturumdan yazar): geri alma = rapordaki
// "EKLENDI" satirlarini ayni ekrandan kaldirmak.
// platform.workflow.* satirlari (kullanici karari 2026-10-21): CRM onay turunu CAGIRANIN token'iyla baslatir ve karara
// baglar — gonder -> instances.start, onaycinin gorevi -> instances.view, karar -> tasks.approve / reject.
// Rol sahipleri degisikligi gormek icin yeniden giris yapmalidir.

(function (APPLY) {
  const MARKER = 'manual-grant-w6-cfg';
  const NAMESPACE = 'commercial.';
  const MATRIX = [
    { role: 'CrmUser', keys: [
      'commercial.pre-orders.read', 'commercial.pre-orders.create', 'commercial.pre-orders.submit', 'commercial.pre-orders.cancel',
      'crm.account.read', 'crm.contact.read', 'crm.account-contact.read', 'crm.visit-plan.read',
      'mdm.product-master.read',
      'platform.workflow.instances.start', 'platform.workflow.instances.view',
    ] },
    { role: 'Team Leader', keys: [
      'commercial.pre-orders.read', 'commercial.pre-orders.approve', 'commercial.pre-orders.cancel',
      'platform.workflow.instances.view', 'platform.workflow.tasks.approve', 'platform.workflow.tasks.reject',
    ] },
    { role: 'Department Manager', keys: [
      'commercial.pre-orders.read', 'commercial.pre-orders.approve', 'commercial.pre-orders.cancel',
      'commercial.pre-orders.read-all', 'commercial.pre-orders.record-depot-response', 'commercial.pre-orders.retry-transmission',
      'commercial.pre-order-settings.*', 'commercial.price-lists.*', 'commercial.pricing-rules.*', 'commercial.fx-rates.*',
      'platform.workflow.instances.start', 'platform.workflow.instances.view', 'platform.workflow.tasks.approve', 'platform.workflow.tasks.reject',
    ] },
    { role: 'Admin', keys: [
      'commercial.*',
      'platform.workflow.instances.start', 'platform.workflow.instances.view', 'platform.workflow.tasks.approve', 'platform.workflow.tasks.reject',
    ] },
  ];

  // '.*' -> catalog keys under the prefix; an exact commercial key the catalog lacks stays (it becomes a DUR row).
  const resolve = (universe) => MATRIX.map(r => {
    const keys = [];
    const unmatched = [];
    for (const e of r.keys) {
      if (e.endsWith('.*')) {
        const hit = universe.filter(k => k.startsWith(e.slice(0, -1)));
        if (!hit.length) unmatched.push(e);
        keys.push(...hit);
      } else keys.push(e);
    }
    return { role: r.role, keys: [...new Set(keys)], unmatched };
  });
  const notGranted = (plan, universe) => universe.filter(k => !plan.some(p => p.keys.includes(k)));

  const run = async (win, apply) => {
    const log = win.console;
    const apiUrl = win.API && win.API.auth;
    const hdr = (win.DitenDataTable && win.DitenDataTable.getAuthHeaders && win.DitenDataTable.getAuthHeaders()) || {};
    const tokenInput = win.document.querySelector('#raAntiForgery input[name="__RequestVerificationToken"]');
    const token = tokenInput && tokenInput.value;
    if (!apiUrl || !token) { log.error('Bu betik /RoleAssignments sayfasinda calismali.'); return null; }
    const unwrap = j => j?.data?.data !== undefined ? j.data.data : (j?.data ?? j?.Data ?? j);
    const get = async p => {
      const r = await win.fetch(apiUrl + p, { credentials: 'include', headers: hdr });
      if (r.status === 401) throw new Error('Oturum belirteci dolmus (401). Sayfayi yenile (F5), sonra betigi hemen tekrar yapistir.');
      if (!r.ok) throw new Error(`GET ${p} -> ${r.status}`);
      return unwrap(await r.json());
    };

    log.log(`%cW6-CFG rol yetkileri (${MARKER}) — ${apply ? 'UYGULA' : 'KURU CALISMA (yazma yok)'}`, 'font-weight:bold;font-size:14px');
    const roles = await get('/api/roles');
    const catalog = new Map((await get('/api/permissions/tenant-assignable')).map(c => [c.key, c]));
    const universe = [...catalog.keys()].filter(k => k.startsWith(NAMESPACE)).sort();
    const plan = resolve(universe);

    const report = [];
    for (const p of plan) {
      for (const e of p.unmatched) report.push({ rol: p.role, anahtar: e, durum: 'DUR: katalogda bu onekle anahtar yok' });
      const role = roles.find(r => String(r.name).trim() === p.role);
      if (!role) { report.push({ rol: p.role, anahtar: '—', durum: 'DUR: rol yok' }); continue; }
      const rp = await get(`/api/roles/${role.id}/permissions`);
      const held = new Set((Array.isArray(rp) ? rp : ((rp && rp.permissions) || [])).map(x => x.key));
      for (const key of p.keys) {
        const perm = catalog.get(key);
        if (!perm) { report.push({ rol: p.role, anahtar: key, durum: 'DUR: katalogda yok' }); continue; }
        if (held.has(key)) { report.push({ rol: p.role, anahtar: key, durum: 'zaten var' }); continue; }
        if (!apply) { report.push({ rol: p.role, anahtar: key, durum: 'EKLENECEK' }); continue; }
        const body = new URLSearchParams({ roleId: role.id, permissionId: perm.id });
        const res = await win.fetch('/RoleAssignments/assign', {
          method: 'POST', credentials: 'same-origin', body,
          headers: Object.assign({ RequestVerificationToken: token, 'Content-Type': 'application/x-www-form-urlencoded' }, hdr),
        });
        const j = await res.json().catch(() => null);
        report.push({ rol: p.role, anahtar: key, durum: j && j.success ? 'EKLENDI' : `HATA ${res.status} ${((j && j.errors) || []).join('; ')}` });
      }
    }
    for (const k of notGranted(plan, universe)) report.push({ rol: '—', anahtar: k, durum: 'ATANMADI (matriste yok)' });

    log.table(report);
    log.log(`commercial.* katalogda: ${universe.length} anahtar.`);
    if (!apply) log.log('Plan dogruysa en alttaki APPLY degerini true yapip tekrar yapistir.');
    else log.log('Bitti. Rol sahipleri cikis yapip yeniden girmeli.');
    return report;
  };

  const api = { MARKER, MATRIX, resolve, notGranted, run };
  if (typeof module !== 'undefined' && module.exports) { module.exports = api; return; }
  run(window, APPLY).catch(e => console.error(e.message));
})(/* APPLY = */ false);
