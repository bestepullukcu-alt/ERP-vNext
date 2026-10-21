// WP-VW-W6 W6-CFG — Workflow sablonlari: COM-PREORDER-APPROVAL (MOD-0168) + COM-PRICING-PUBLISH (MOD-0156)
//
// NASIL: Web'e kiraci yoneticisi olarak giris yap -> /Platform/Workflow sayfasini ac -> F12 -> Console -> bu dosyanin
//        TAMAMINI yapistir -> Enter. Varsayilan KURU calismadir (hicbir sey yazmaz, plani basar).
//        En alttaki TARGET: 'local' (yerel 97c5, aday POS-SALES-OPS) ya da 'live' (canli be823006, aday 8 ulkenin
//        <ULKE>-SALES-OPS pozisyonu — W3 go-live'da olusturulanlar). Plan dogruysa APPLY degerini true yapip tekrar yapistir.
//
// Ne yapar: workflow-templates-live.js (W3-CFG) deseni — ayni kaynaktaki /Platform/Workflow/api/... proxy'si ile (oturum
// cerezi; parola / belirtec betikte YOK) sablonu olusturur ve yayimlar. Idempotent: kod zaten Published ise dokunmaz;
// olusmus ama yayimlanmamissa yalniz yayimlar. Pozisyon kimliklerini KOD ile bulur; biri eksikse o sablonu atlar (DUR).
//
//   COM-PREORDER-APPROVAL — tek adim, sablonda ADAY YOK (candidatePrincipalIds: []). Platform ilk adimin kendi adayi yoksa
//     baslatma isteginin calisma ani adaylarini kullanir (WorkflowInstanceStartCoordinator: firstStep.CandidatePrincipalIds
//     bos -> request.CandidatePrincipalIds); CRM bunlari temsilcinin hattindaki yoneticilerden (PLT-W6 line-managers) verir.
//     Sablona aday yazilirsa yoneticiler YOK SAYILIR — bu yuzden bos. SLA yok (onay baglantisi suresi CRM kiraci ayari;
//     yukseltilecek sabit kisi yok). Ret / degisiklik istegi gerekceli (D5) -> options.rejectCommentRequired.
//     Sonuc CRM'e mevcut olay yoluyla doner (objectType commercial.pre-order).
//   COM-PRICING-PUBLISH — dort goz onay adimi; aday satis operasyonu pozisyonlari; fiyat listesi (commercial.price-list) ve
//     kural (commercial.pricing-rule) ayni sablonla gider (CRM PricingApprovalKinds). Hazirlayan karar veremez: Platform
//     baslatani adaylardan cikarir, CRM ayrica 403 verir. SLA W3-CFG ile ayni (4320 dk; 2880'de ayni adaylara yukselt).

(function (APPLY, TARGET) {
  const BASE = '/Platform/Workflow';
  const REASON = 'WP-VW-W6 W6-CFG pre-order + pricing approval templates';

  // Canli (be823006): ulke birimlerindeki <ULKE>-SALES-OPS pozisyonlari (workflow-templates-live.js ile ayni liste).
  const LIVE_COUNTRIES = ['AZRO', 'BYRO', 'GERO', 'KGRO', 'KZRO', 'TMRO', 'UARO', 'UZRO'];
  const POSITIONS = {
    local: ['POS-SALES-OPS'],
    live: LIVE_COUNTRIES.map(c => `${c}-SALES-OPS`),
  };

  const TEMPLATES = [
    {
      code: 'COM-PREORDER-APPROVAL',
      name: 'Eczane ön siparişi — Yönetici onayı',
      description: 'WP-VW-W6 eczane ön siparişi onayı (commercial.pre-order, MOD-0168); aday YOK — çalışma anı adayları = temsilcinin hattındaki yöneticiler.',
      objectType: 'commercial.pre-order',
      positions: false,
      stage: { code: 'manager', name: 'Yönetici Onayı' },
      step: { code: 'manager', name: 'Yönetici onayı' },
    },
    {
      code: 'COM-PRICING-PUBLISH',
      name: 'Fiyat listesi / kural yayımlama — Dört göz onayı',
      description: 'WP-VW-W6 fiyat listesi yayımlama ve MF / iskonto kuralı değişikliği (commercial.price-list, commercial.pricing-rule; MOD-0156); aday satış operasyonu pozisyonları.',
      objectType: 'commercial.pricing',
      positions: true,
      stage: { code: 'sales_ops', name: 'Satış Operasyon' },
      step: { code: 'four_eyes', name: 'Dört göz onayı' },
      sla: { dueInMinutes: 4320, escalateAfterMinutes: 2880 },
    },
  ];

  const definitionJson = (t, principals) => {
    const step = {
      code: t.step.code,
      name: t.step.name,
      type: 'approval',
      // COM-PREORDER-APPROVAL: [] on purpose — see the header.
      assignment: { mode: 'candidate_principals', candidatePrincipalIds: t.positions ? principals : [] },
      requirements: { commentRequired: false, evidenceRequired: false },
    };
    if (t.sla) step.sla = { dueInMinutes: t.sla.dueInMinutes, escalateAfterMinutes: t.sla.escalateAfterMinutes, escalationPrincipalIds: principals };
    return JSON.stringify({
      schemaVersion: 'workflow_schema_v1',
      expressionVersion: 'workflow_expr_v1',
      requestedObjectType: t.objectType,
      options: { rejectCommentRequired: true },
      stages: [{ code: t.stage.code, name: t.stage.name, steps: [step] }],
    });
  };

  const run = async (win, apply, target) => {
    const log = win.console;
    if (!POSITIONS[target]) { log.error(`TARGET 'local' ya da 'live' olmali (verilen: ${target}).`); return null; }
    const call = async (method, path, body) => {
      const res = await win.fetch(BASE + path, {
        method,
        credentials: 'same-origin',
        headers: body ? { 'Content-Type': 'application/json', Accept: 'application/json' } : { Accept: 'application/json' },
        body: body ? JSON.stringify(body) : undefined,
      });
      const text = await res.text();
      let json = null;
      try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
      if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${json?.reasonCode || ''} ${(json?.errors || []).join('; ') || text.slice(0, 300)}`);
      return json && 'data' in json ? json.data : (json?.Data ?? json);
    };
    const g = (o, k) => o?.[k] ?? o?.[k[0].toUpperCase() + k.slice(1)];

    log.log(`%cW6-CFG Workflow sablonlari (${target}) — ${apply ? 'UYGULA' : 'KURU CALISMA (yazma yok)'}`, 'font-weight:bold;font-size:14px');

    const positions = await call('GET', '/api/lookups/positions');
    const byCode = new Map();
    for (const p of positions || []) {
      if (!g(p, 'isArchived')) byCode.set(String(g(p, 'code')).toUpperCase(), p);
    }
    const definitions = await call('GET', '/api/definitions');
    const existing = new Map((definitions || []).map(d => [String(g(d, 'templateCode')).toUpperCase(), d]));

    const report = [];
    for (const t of TEMPLATES) {
      const codes = t.positions ? POSITIONS[target] : [];
      const missing = codes.filter(c => !byCode.has(c));
      const principals = codes.filter(c => byCode.has(c)).map(c => 'position:' + g(byCode.get(c), 'id'));
      const ex = existing.get(t.code);
      const row = { sablon: t.code, aday: t.positions ? `${principals.length}/${codes.length} pozisyon` : 'yok (çalışma anı)', mevcut: ex ? g(ex, 'status') : 'yok', islem: '', sonuc: '' };
      report.push(row);

      if (ex && String(g(ex, 'status')).toLowerCase() === 'published') { row.islem = 'atla (zaten yayında)'; continue; }
      if (missing.length) { row.islem = 'DUR: pozisyon yok: ' + missing.join(', '); continue; }

      row.islem = ex ? 'yalnız yayımla' : 'oluştur + yayımla';
      if (!apply) continue;

      try {
        let id = ex ? g(ex, 'id') : null;
        if (!id) {
          const created = await call('POST', '/api/definitions', { templateCode: t.code, name: t.name, description: t.description });
          id = g(created, 'id');
        }
        const published = await call('POST', `/api/definitions/${id}/publish`, {
          definitionJson: definitionJson(t, principals),
          schemaVersion: 'workflow_schema_v1',
          expressionVersion: 'workflow_expr_v1',
          expectedTemplateVersion: null,
          expectedRowVersion: null,
          publishReason: REASON,
        });
        row.sonuc = `OK — şablon ${id}, sürüm v${g(published, 'versionNumber')} ${g(published, 'status')}`;
      } catch (e) {
        row.sonuc = 'HATA: ' + e.message;
      }
    }

    log.table(report);
    if (!apply) log.log('Plan doğruysa en alttaki APPLY değerini true yapıp tekrar yapıştır.');
    return report;
  };

  const api = { TEMPLATES, POSITIONS, definitionJson, run };
  if (typeof module !== 'undefined' && module.exports) { module.exports = api; return; }
  run(window, APPLY, TARGET).catch(e => console.error(e.message));
})(/* APPLY = */ false, /* TARGET = */ 'local');
