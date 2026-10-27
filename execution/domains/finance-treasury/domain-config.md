# Finance & Treasury — Domain Config

> **Durum: TASLAK (draft).** Domain klasörü sahip kararıyla açıldı
> ([TREASURY-MOD0126-PLACEMENT-01](../../../docs/records/decisions/2026-10/treasury-mod0126-placement-decision-01.md),
> 2026-10-23). AGENTS.md §2 (domain ve servis listesi) ve §9 (dal kısa kodu) değişikliği, port tahsisi ve servis
> iskeleti bu dosyayla **yapılmaz**; her biri ayrı onaydır. Bu dosya Finance & Treasury domain'ine özgü sahiplik ve
> sınır kararlarını kaydeder. Mühendislik standartları `.antigravity/rules/` içindedir ve tekrar edilmez, referans
> verilir. Yetki: Module Pack > bu dosya.

## Purpose

Finance & Treasury domain'i, ERP-vNext'in **ödeme, borç/alacak ve nakit pozisyonu** sistem-of-record'larını
sahiplenir: ödeme partileri ve onayları, satıcı borçları (AP), müşteri alacakları (AR) ve nakit pozisyonu. Blueprint
8.8 Domain 4 — Enterprise Application Ecosystem, ERP Suite altındaki **"Treasury & Cash"** ve **"Financial
Management"** capability group'larının execution katmanıdır.

**Planlama tabanı:** [erp-finance-legal-module-intake-01/](../../../docs/roadmap/plans/erp-finance-legal-module-intake-01/)
(TP-001 v1.5 taslağı, Treasury migration sequence v1.0) · **Kuyruk:**
[CT-QUEUE.tsv](../../../docs/roadmap/plans/erp-finance-legal-module-intake-01/CT-QUEUE.tsv) (`treasury-intake`) ·
**Sahip kararları:** [treasury-intake-owner-decisions-01.md](../../../docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md)

## In-Scope Modules (yerleşim kararı 2026-10-23)

Kanonik adlar Blueprint 8.8 `Blueprint_Data` "Module Name" sütunundan birebir alınmıştır. Dördünün de kayıt satırı
henüz yok (`execution/registries/module-id-registry.md`); kimlik rezervasyonu pack açılmadan önce
`verify_module_id.py --check-id` ön kontrolüyle ayrıca yapılır (DCP-002).

- `MOD-0126 Payments & Approvals` — **ödeme partisi / talimat / onay / serbest bırakma SoR** (Blueprint W-3, Treasury &
  Cash). İlk modül. Kapıları: MOD-0023 Workflow, MOD-0021 Audit, MOD-0269 (karar 0.1 = A: önce sözleşme, dağıtım
  kapalı). Ödeme yapan şirket hem aktif hem onaylı olmalıdır (D10/1; MOD-0220'den okunur).
- `MOD-0119 Accounts Payable (AP)` — **satıcı borcu SoR** (Blueprint W-3, Financial Management). Kapısı MOD-0003
  (karar 0.3 açık).
- `MOD-0120 Accounts Receivable (AR)` — **müşteri alacağı / fatura SoR** (Blueprint W-3, Financial Management).
  Kapıları MOD-0003 ve AR-BILLING-BUNDLE (karar 0.5 açık).
- `MOD-0124 Cash Positioning` — **nakit pozisyonu SoR** (Blueprint W-3, Treasury & Cash). Kapıları MOD-0003 ve
  MOD-0037; banka durumu ve ekstre MOD-0269'a bağlı.

> Her modül **kendi module pack'i** `approved` / `ready-for-dev` olmadan kod yazılmaz (AGENTS.md §10). Kaynak
> projeden kod, yalnız modülün pack'i `ready-for-dev` olduktan ve sahip o modül için kod alma iznini verdikten sonra
> alınır ve uyarlanır (sahip kararı D1). Bu domain-config yalnız sınır ve sahiplik tanımlar; hiçbir şeyi tetiklemez.

## Candidate Modules (karar verilmedi)

- `MOD-0125 Cash Forecasting` (W-4) — MOD-0004 ve MOD-0063 kapıları iki depoda da yok; paketi yok.
- `MOD-0121 Bank Reconciliation` (W-4) — MOD-0269 ekstre yayımının tüketicisi; paketi yok.
- `MOD-0118 General Ledger (GL)` — karar 0.7: önerilen "şimdilik kapsam dışı"; eklenirse MOD-0001 (W-1) gelir.
- `MOD-0146 Payment Run Support` — kayıt dosyasında ve procurement domain-config'inde "Finance/Treasury; out of
  scope" olarak dışlanmış; dışlamanın kalması önerildi (karar 0.4).
- MOD-0269 bağdaştırıcı kodu — Blueprint'te External Provider / Partner; bu domain'de durabilir, ayrı onaydır.

## Explicitly NOT in this domain (out-of-scope)

- `MOD-0289 Bank Master & Bank Account Registry` → **master-data-management** önerisi (sahip D7; MDM domain sahibinin
  onayı bekleniyor). Bu domain banka ana verisi tutmaz; banka referansını opak kimlik olarak tüketir.
- `MOD-0140 Supplier Onboarding (KYC/Sanctions/Docs)` → **procurement** (tedarikçi kiracıda tek kayıt, şirket
  ilişkisi LE bazında; MVP-2 değişikliği). Bu domain tedarikçi kaydı açmaz; `supplierId` + aktif şirket ilişkisini
  sözleşme üzerinden tüketir. Tedarikçi banka bilgisi MOD-0140'ta tutulmaz.
- `MOD-0149 Customer 360 / Account Hierarchy` → **commercial-suite**. Müşteri kaydı açılmaz; `customerId` tüketilir.
- `MOD-0220 Corporate Secretarial / Entity Management` → **master-data-management** (LE SoR).
- `MOD-0217 Contract Lifecycle Management (CLM)` ve `MOD-0057 Semantic Tagging & Taxonomy Management` — bu domain'e
  ait değil; yerleşimleri ayrı karar (TRM-13).
- `MOD-0143 Invoice Capture & 3-Way Match` → **procurement**; eşleştirme sonucu üretir, ödeme yürütmez. AP bu sonucu
  tüketir.

## Consumed Masters and Backbone (bu domain SAHİPLENMEZ — reference only)

- Legal Entity (MOD-0220, MDM) · Supplier (MOD-0140, procurement) · Customer (MOD-0149, CRM) · Bank / bank account
  (MOD-0289, MDM önerisi).
- Backbone: RBAC (MOD-0018) · Audit (MOD-0021) · Workflow / Approvals (MOD-0023) · Reference Data (MOD-0048) ·
  Evidence (MOD-0031) · Integration Monitoring (MOD-0037) · Gateway (MOD-0032).
- Dış sağlayıcı: MOD-0269 Banks/SWIFT/Payments Gateway [External Provider] — karar 0.1 = A.

## Domain-Level Owned Boundaries

- **Banka verisi bu domain'de saklanmaz.** Ödeme talimatı banka hesabını yalnız opak bir referansla taşır; ham hesap
  numarası, IBAN değeri ya da sağlayıcı sırrı tutulmaz (kaynak MOD-0126 pack kısıtı; MOD-0289 çözümleme anında gerekir,
  derleme anında değil).
- **Bankaya dağıtım kapalıdır** (karar 0.1 = A). Sağlayıcıdan bağımsız arayüz sözleşmesi yazılır; bağdaştırıcı
  "yapılandırılmamış" döner. Gerçek dağıtım, banka kimlikli geri çağrı, güvenilir non-HTTP çalışan ve MOD-0121'e ekstre
  yayımı kaynakta kapalıdır ve burada **varsayılmaz**; her biri kendi sözleşmesi, kimlik sınırı, denetim yolu, testleri
  ve sahip kabulüyle açılır.
- **Onay ayrılığı (SoD):** talep eden kendi ödemesini onaylayamaz. Kontrol MOD-0023 iş akışı motorundan miras alınır;
  modül kendi SoD kontrolünü icat etmez
  ([SOD-APPROVER-DEPTH-ASSESSMENT-v1.0.md](../../../docs/roadmap/plans/erp-finance-legal-module-intake-01/SOD-APPROVER-DEPTH-ASSESSMENT-v1.0.md)).
- **Yerel kayıt önce, uzak çağrı sonra.** Kaynakta iş akışı başlatma ve kanıt dışa aktarımı yerel kayıttan önce
  çağrılıyor (sonuç bilinemez). Burada her dış etki ya kayıtlı bir niyet ve uzlaştırma ile, ya da yerel kayıttan sonra
  yapılır (TP-001 T16).
- **Denetim:** her yazma komutu denetlenir (AUD-001). Kaynaktaki NoOp / kapalı denetim yolları taşınmaz.
- **Contract-first:** modüller arası iletişim yalnız yayımlanmış sözleşmeyle (`docs/analysis/contracts/`). Başka
  servisin koleksiyonu doğrudan okunmaz (kaynakta `Mod0289BankRepository.cs:51` `mod0140_suppliers`'ı doğrudan
  okuyor; burada yasak).
- **Single-writer:** bu domain'in sözleşmelerinin tek yazıcısı ilgili modüldür (K15).

## Key Decisions (RESOLVED / OPEN)

- **Scoping (RESOLVED, repo kuralı):** Tenant + Legal-Entity; `TenantId` / `LegalEntityId` sunucu tarafında çözülür,
  payload'dan alınmaz; LE uygunluğu kayıt anındaki snapshot değil, güncel durumdur (TP-001 T14).
- **Para / decimal (RESOLVED, SOP §18.8):** parasal alanlar `decimal`, float yasak; para birimi kaynağı, hassasiyet,
  yuvarlama, saat dilimi ve FX tarihi her module pack'te açıkça yazılır.
- **Dil (RESOLVED, repo kuralı):** kiracı modülü — 7 dil (en, tr, fr, es, zh, ar, ru); `_LayoutTenantShell`. Kaynak
  ekranlardaki `_LayoutBackbone` ve tek dilli resx taşınmaz.
- **İzin anahtarı öneki (OPEN):** pack'lerde `permission-key-standard.md`'ye göre belirlenir; kaynaktaki `ap.*` ve
  büyük harfli anahtarlar olduğu gibi alınmaz.
- **MOD-0003 Data Contract Registry (OPEN, karar 0.3):** önerilen — tam modül yerine süreli sapma (T32) ve
  `docs/analysis/contracts/`'ta dondurulmuş sözleşmeler.
- **AR fatura sınırı (OPEN, karar 0.5)** · **ERP-FIN-BUNDLE arayüz sahipliği (OPEN, karar 0.6, EA)** · **GL kapsamı
  (OPEN, karar 0.7)**.

## Service & Deployment (TASLAK)

- **Servis:** `Diten.FinanceTreasuryService` (sahip kararı D2) — henüz yok; iskeleti ilk pack `ready-for-dev`
  olduktan sonra `@orchestrator /add-module` ile açılır.
- **Port:** **5071** — sahip kararı D10 (2026-10-24; sahip EA kararını kendisi verdi). AGENTS.md §3 ve
  `.antigravity/rules/ports.md` güncellendi (bant 5011–5071; sahip onayıyla D30, 2026-10-25).
- **Dal kısa kodu:** `fin` (D3) — AGENTS.md §9'a ayrı onayla yazılır.
- **Persistence:** MongoDB; her sorguda `TenantId` izolasyonu; soft delete (`IsDeleted`, `DeletedAt`).
- **Gateway:** rotalar yalnız `integration-agent` tarafından eklenir (korunan `ocelot.json`).

## Cross-references

Placement: [treasury-mod0126-placement-decision-01.md](../../../docs/records/decisions/2026-10/treasury-mod0126-placement-decision-01.md)
· Önceki yerleşim belgesi: [MOD-0126-PLACEMENT-DECISION-PACK.md](../../../docs/roadmap/plans/erp-finance-legal-module-intake-01/MOD-0126-PLACEMENT-DECISION-PACK.md)
· MOD-0289 yerleşim notu: [MOD-0289-PLACEMENT-NOTE-v0.1.md](../../../docs/roadmap/plans/erp-finance-legal-module-intake-01/MOD-0289-PLACEMENT-NOTE-v0.1.md)
· Kaynak kanıtı (depo dışı): handoff `source-build-evidence-v1.0`, SHA256SUMS
`7ec2cd3332a4ff8d8f7c5fbfe1885b49a5f1e552ee4954806090b59726c536eb`
