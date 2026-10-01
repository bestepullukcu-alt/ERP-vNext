# ERP-vNext — Product Backlog (Deferred / Out-of-Scope-for-Now)

> **Amaç:** Bilinçli olarak **ertelenen** özelliklerin tek kaydı. Her madde bir gerekçe ve bir "ne zaman yapılır" tetikleyicisiyle park edilir — böylece hiçbir şey sessizce unutulmaz ve hiçbir şey vaktinden önce yapılmaz.
> **Sahiplik modeli:** Claude = CONTROL TOWER (prompt yazar, canlı doğrular); yürütme = Antigravity ajanları. **Go-live kapsamı buradaki her şeyi HARİÇ tutar.**
> **Antigravity ajanları için (ZORUNLU):** Buradaki maddeler, onaylı bir module pack açıkça bu backlog'dan çıkarıp `approved`/`ready-for-dev` kapsamına almadıkça **UYGULANMAZ**. Bir backlog özelliğini "yardımcı olayım" diye kendiliğinden inşa etmek YASAKTIR. Bir talep bir backlog maddesine değiyorsa, kod yazmadan önce bu dosyayı referans göster ve module pack kapısına yönlendir.
> **Son güncelleme:** 2026-07-09.

## Nasıl kullanılır
- Bir özellik konuşulup bilinçli ertelendiğinde madde ekle: **ne olduğu**, **neden ertelendiği**, **hangi tetikleyiciyle yapılacağı**, **ilgili modül**.
- Bir maddeyi ancak onaylı bir module pack'e alınıp teslim edildiğinde kaldır/üstünü çiz.

---

## Foundation guardrail'leri (ŞİMDİ uygulanır — ERTELENMEZ)

> Bunlar ertelenen özellik DEĞİL; **bugünden itibaren geçerli mimari kurallardır.** Bedavadırlar (ekstra iş yok) ama uygulanmazsa ileride BL-007/BL-008 eklerken **geriye dönük ayıklama/migration acısı** doğar. Antigravity ajanları ve developer'lar bunlara uyar.

### FG-001 — Legal Entity yalnız KENDİ grubun tüzel kişileridir (iç-only)
- Legal Entity master'ına yalnız senin sahip olduğun/kontrol ettiğin grup şirketleri girer (Grand Medical Group, Monom, GM Polan, Setonda AZ rep-office vb.).
- **Dış taraflar (distributor, müşteri, tedarikçi) Legal Entity'ye ASLA girilmez** → onlar Business Partner master'ının işidir ([BL-007]). Bugün dışarıyı LE'ye sokmak = ileride acılı extraction.
- **Regresyon:** Kural korunursa BP eklemek 🟢 additive; ihlal edilirse 🔴 migration.

### FG-002 — User / Employee / Business Partner üç AYRI kavramdır
- **User** = login/erişim (sisteme giren herkes: iç + dış). **Employee** = yalnız kendi iş gücün (HR). **Business Partner** = dış şirket + kişileri.
- User'ı "employee" yerine kullanma; erişim **daima Role üzerinden** verilir (iç ve dış için çalışır). **PositionAssignment yalnız kendi Employee'lerin içindir**; dış kişi PositionAssignment almaz, doğrudan Role ile erişir.
- **Regresyon:** Ayrım korunursa Employee/BP katmanı 🟢 additive; kavramlar karışırsa 🔴 veri ayıklama.

### FG-003 — Inline CSS YASAK (yeni kod)
- Yeni kod stilini **CSS class'ı** (backbone-custom.css / site.css) veya Bootstrap utility ile verir. Markup'ta `style="…"` **veya** JS'te statik stil için `element.style.setProperty()` **kullanılmaz**.
- Dinamik davranış gerekiyorsa: **JS class toggle'lar, CSS stiller** ("JS decides which, CSS decides how").
- **Bilinen istisna:** mevcut `dt-defaults.js` button-group radius'u runtime inline-style ile basıyor → [BL-012]'de ertelendi (çalışıyor, tek kaynak, aciliyeti yok). Yeni kodda tekrarlanmaz.

### FG-004 — Yeni modül reference list'i GÖMMEZ (hardcode yasak)
- Sabit / enum-benzeri / lookup listesi gerekiyorsa **kaynağı scope'a göre** seçilir: **platform-geneli ortak** liste → Platform lookup (`/api/lookups/{key}`); **tenant'a özel / işsel** liste → **BRD** (governed set + `published-values`).
- Backend'de VEYA frontend'de **yeni hardcoded array = YASAK.** Amaç: mevcut iki sistemin (platform lookup + BRD) yanına **3. dağınık kaynak** eklenmesin; her modül **tek kontrattan** (`published-values` / lookup endpoint) beslensin.
- **Bilinen borç:** LE'nin `control-type` / `accounting-standard` / `tax-regime` listeleri MDM'de hardcoded (operatör düzenleyemiyor) → istenirse BRD'ye taşınır (opsiyonel, düşük öncelik). Ülke/para/legal-form zaten BRD'de.
- **Regresyon:** Kural tutulursa yeni modüller 🟢 tek-kontrat; hardcode eklenirse 🔴 parçalanma birikir.

### FG-005 — Audit gate (yeni modül audit'siz kapanmaz)
- Write/mutation komutu olan **her yeni modül** audit ihtiyacını **değerlendirir** ve iş-kritik komutları auditable yapar. Modül "bitti" denmeden önce bu değerlendirme yapılmalı (l10n gate gibi).
- **Platform komutu:** `IAuditableCommand` + `IAuditMetadataProvider` ekle → `AuditBehavior` merkezi `audit_events`'e otomatik yazar (handler'a dokunma). Örnek: CreateOrganizationUnitCommand.
- **MDM / başka-servis komutu:** S2S audit-forwarding pattern'i kullan — `AuditForwardingBehavior` → Platform `/api/internal/audit/append` (X-Internal-Api-Key), `SourceService=<servis>` (merkezi store'da birleşir). Örnek: MDM Legal Entity (Faz 2).
- **Muaf:** dev-only sandbox modüller (DevEnablement/Golden Reference).
- **Amaç:** MDM'de yaşanan gibi bir daha audit boşluğu oluşmasın. Kapsam: mevcut boşluklar Faz 1-2'de kapatıldı; kalanlar (ModulePages/Navigation/SavedViews) düşük öncelik.

---

## Backlog maddeleri

### BL-001 — Corporate Action Workspace (Legal Entity)
- **Nedir:** CRUD'un ötesinde kurumsal/tüzel-kişi olayları için çalışma alanı — birleşme & devralma (M&A), sermaye değişikliği, yeniden yapılanma, unvan değişikliği / yeniden yerleşim (redomiciliation), fesih — kendi audit izleri ve (ileride) onay akışıyla.
- **Konuşulan yüzey:** Legal Entity liste/satır action'ı ("Corporate Action Workspace").
- **Neden ertelendi:** Başlı başına büyük bir modül; go-live için gerekli değil.
- **Yapım tetikleyicisi:** Ayrı onaylı module pack (corporate-actions).
- **İlgili:** MOD-0220 Legal Entity (yukarı-akış veri kaynağı).

### BL-002 — Filing Calendar / Inbox (Legal Entity compliance)
- **Nedir:** Resmi beyan/başvuru son-tarih takibi — yıllık raporlar, statüter/vergi beyanları, lisans yenilemeleri — tüzel kişi başına takvim + vadesi gelen/geçen yükümlülükler için bir inbox.
- **Konuşulan yüzey:** Legal Entity liste/satır action'ı ("Filing Calendar / Inbox").
- **Neden ertelendi:** Başlı başına bir compliance modülü; go-live için değil.
- **Yapım tetikleyicisi:** Ayrı onaylı module pack (compliance/filings).
- **İlgili:** MOD-0220 Legal Entity; document-management (başka ekip) ile örtüşür.

### BL-003 — Legal Entity governance/approval workflow bağlantısı
- **Nedir:** LE `Approval Status` (Draft→InReview→Approved) ve `Review Due` (periyodik yeniden-gözden-geçirme tarihi) alanlarını, Draft'ta duran statik alanlar olmaktan çıkarıp gerçek bir **veri-yönetişim / stewardship iş akışına** bağlamak.
- **Neden ertelendi:** Workflow motoru (MOD-0023) entegrasyonu + steward rolleri gerekir; go-live için değil.
- **Yapım tetikleyicisi:** governance-workflow capability pack.
- **İlgili:** MOD-0220 Legal Entity, MOD-0023 Workflow.

### BL-004 — Legal Entity evidence/belge toplama
- **Nedir:** LE `Evidence Status`'ünü gerçek destekleyici-belge toplamayla (kuruluş evrakı, vergi levhası) beslemek — compliance kanıt ilerlemesi.
- **Neden ertelendi:** document-management (başka ekip) + compliance akışına bağlı.
- **Yapım tetikleyicisi:** doc-management entegrasyon pack'i.
- **İlgili:** MOD-0220 Legal Entity, MOD-0028 Document Management.

### BL-005 — OrgUnit tiplerini genişlet (Warehouse / Plant / Sales / RepOffice)
- **Nedir:** `OrgUnitType` enum'u şu an: Department, Division, Branch, Team, HQ. Grup yapısındaki depo (Monom, distributor deposu), üretim tesisi (Poland, Migual), saha satış (rep office) için ayrı tip yok — bugün Branch/Division ile temsil ediliyor.
- **Neden ertelendi:** Küçük ama ürün-kararı gerektiren bir tip genişletmesi.
- **Yapım tetikleyicisi:** **Blueprint'e (`docs/System Capability & Implementation Blueprint - master 7.xlsx`) bakılarak, org-model buna uygunsa yapılacak** — aksi halde mevcut tiplerle temsil devam.
- **İlgili:** MOD-0288 Organization.

### ~~BL-006 — MDM / Position audit entegrasyonu~~ ✅ TAMAMLANDI (2026-07-11)
- **TESLİM EDİLDİ:** Faz 1 (Platform: Position/PositionAssignment + Quotas + Subscriptions auditable) + Faz 2 (MDM/Legal Entity → S2S ile Platform merkezi audit_events, SourceService="Diten.MDM") + Faz 3 (FG-005 audit gate). Canlı doğrulandı, commit `c3a66794`. Kalan düşük-öncelik: BL-014 (correlation-id) + Platform biz-config/prefs (~50 cmd, ertelendi).

### BL-007 — Business Partner / Distributor master
- **Nedir:** Grubun kendi tüzel kişisi olmayan 3. parti taraflar (distributor'lar, onların branch/filyaları, müşteriler) için ayrı bir iş-ortağı/müşteri master'ı. Bunlar Legal Entity değildir. Ayrıca intercompany ticaret akışı (Poland→Group→Monom→AZ satış zinciri) da bu/ilişkili ticari kapsamda.
- **Neden ertelendi:** Legal Entity ve Organization kapsamı dışında, ayrı bir master + ticari ilişki modeli.
- **Yapım tetikleyicisi:** **Blueprint'e bakılarak, uygunsa yapılacak.**
- **İlgili:** MOD-0220 Legal Entity (ayrım netliği için), gelecek commercial/supply-chain kapsamı.

### BL-008 — Position-based access provisioning (birthright roles) + Employee model
- **Nedir:** Bugün erişim tamamen role-based (`User → UserRoleAssignment → Role → Permission`); Position erişimden kopuk, sadece org-yapısı. Hedef: pozisyona rol(ler) bağlanır, bir kullanıcı o pozisyona atanınca pozisyonun rolleri/izinleri **otomatik** gelir ("birthright access"). Gerekenler: (1) Position→Role bağı, (2) Employee entity + `PositionAssignment → Employee → (opsiyonel) User` zinciri (bugün PositionAssignment doğrudan `UserId`'ye bağlı), (3) yetki çözümleyicinin kullanıcının aktif pozisyon atamalarını okuyup rol/izin türetmesi.
- **Neden ertelendi:** HR/Employee modülü henüz yok; RBAC bugün yalnız role-based; ciddi bir mimari katman.
- **Yapım tetikleyicisi:** **Blueprint'e bakılarak, org/HR modeli buna uygunsa yapılacak** — HR modülü (Employee) geldiğinde birlikte ele alınır.
- **İlgili:** MOD-0288 Organization (Position/PositionAssignment), MOD-0018 RBAC / Access Governance, gelecek HR modülü.

### BL-009 — Reference Data tam governance UI (olgun onay akışı)
- **Nedir:** Reference data yönetiminin "öner→onayla→yayınla" tam ekranları + tam değişiklik geçmişi (şu an basit hali var).
- **Neden ertelendi:** Blueprint bunu W-3'e (3. dalga) koymuş; go-live için basit hali yeter.
- **Yapım tetikleyicisi:** Blueprint W-3 / operatör onay ihtiyacı doğunca.
- **İlgili:** MOD-0048 Reference Data Management.

### BL-010 — Cascade (bağlı/dependent listeler)
- **Nedir:** Bir listenin başka listeye bağlı olması (ülke→şehir, kategori→alt-kategori). Value shape'e `parentCode` eklenerek additive gelir.
- **Neden ertelendi:** Go-live için düz listeler yeter; bağlı listeler ileri ihtiyaç.
- **Yapım tetikleyicisi:** Blueprint'e bakılarak, dependent liste ihtiyacı doğunca.
- **İlgili:** MOD-0048 Reference Data (BRD v2).

### BL-011 — Financial Dimensions / Cost Center registry
- **Nedir:** Mali boyutlar, cost center, profit center, dimension set'leri — reference data'dan AYRI bir governance modülü (GL hareketsel defter ayrı kalır).
- **Neden ertelendi:** ERP mali kapsamı; go-live dışı.
- **Yapım tetikleyicisi:** Blueprint MOD-0291 sırası gelince.
- **İlgili:** Blueprint MOD-0291.

### BL-012 — dt-defaults.js button-group radius'unu inline-style'dan CSS'e taşı
- **Nedir:** [dt-defaults.js:364-440](../../frontend/Diten.Web/wwwroot/assets/js/dt-defaults.js) toolbar button-group'un köşe yuvarlaması/ayraçlarını runtime'da `this.style.setProperty('border-radius'…, 'margin-left'…, 'position'…)` ile **inline** basıyor (responsive gizlenen butonlar `:last-child` CSS'ini bozduğu için JS ile görünür ilk/son buton hesaplanıyor). FG-003 ihlali.
- **Çözüm:** JS inline-style yerine **class toggle** etsin (ör. `.dt-btn-visible-first/-last/-middle`), radius'lar `backbone-custom.css`'te class üzerinden tanımlansın.
- **Neden ertelendi:** Çalışıyor (bug değil), **tek kaynak** (dt-defaults.js) → ileride tek yerde değişir, tüm sisteme yansır, dağınık regresyon yok. Go-live aciliyeti yok. DİKKAT: körlemesine silme — grup butonlarının (ColVis+Filter) radius'u buna bağlı; standalone Add butonunda etkisiz (radius zaten default).
- **İlgili:** FG-003, tüm DataTable toolbar'ları.

### BL-013 — Country/Currency tam ISO genişletme
- **Nedir:** BRD `country` (şu an 22) ve `base-currency` (26) setlerini tam ISO 3166/4217'ye (~195 ülke / ~180 para) genişletmek. Şu an grubun faal ülkeleri (TR/CH/GE/AZ/PL + majör ekonomiler) kapsanıyor.
- **Neden ertelendi:** Faal footprint yeterli; tam ISO "someday" nicelik. Yeni ülke gerekince tek satır JSON + version bump ile eklenir (bkz. legal-entity-reference.json, catalog_version bump şart).
- **Yapım tetikleyicisi:** Daha geniş coğrafya ihtiyacı doğunca.
- **İlgili:** MOD-0048 Reference Data (BRD), FG-004.

### ~~BL-014 — MDM audit forward correlation-id threading~~ ✅ TAMAMLANDI (2026-07-11)
- **TESLİM EDİLDİ:** `PlatformAuditForwarder` artık gelen isteğin `X-Correlation-Id`'sini (Guid ise) audit CorrelationId olarak kullanıyor; yoksa fresh id fallback. Canlı doğrulandı (gönderilen correlation audit kaydına birebir geçti). Commit BEKLİYOR (sabah commit+push).

---

## MOD-0290 SKU & Coding Foundation — ilk faz dışında bırakılan konular

### BL-015 — Composition / Active-Substance SoR ve Complex Strength
- **Nedir:** Composition ve active-substance için otoritatif SoR ile multi-active/complex strength modelidir. İlk fazdaki scalar presentation descriptor korunur; Composition SoR devreye girdiğinde bu descriptor onaylı Composition verisinden türetilen, salt-okunur bir gösterime dönüşür.
- **Neden ertelendi:** İlk faz ürün/SKU kimliğine odaklanır; Composition, ingredient, formula ve complex-strength sahipliği bu sınıra bilinçli olarak alınmamıştır.
- **Yapım tetikleyicisi:** Composition/active-substance sahipliği, kaynak sistemi, kontrollü UoM kuralları ve complex-strength kullanım senaryoları onaylandığında.
- **İlgili:** MOD-0290 Product / Item / SKU Master sınırı; gelecekteki Composition / Active-Substance SoR capability'si (kanonik sahiplik teslimat öncesinde Master 8.1 ile kesinleştirilecektir).
- **Bağımlılık / çıkış kriteri:** Onaylı domain contract, türetme ve migration kuralları bulunmalıdır. İlk faz Product Definition veya GSKU içine Composition ID/FK/placeholder eklenmez.

### BL-016 — Product Definition Revision Effective Dating ve Concurrency
- **Nedir:** Revision geçerlilik tarihleri, paralel revision davranışı, optimistic concurrency ve consumer'ların hangi revision'ı seçtiğine ilişkin sözleşmedir.
- **Neden ertelendi:** İlk fazda otomatik current revision, tek-current unique index ve overlap invariant'ı kabul edilmemiştir; consumer açık `RevisionId` kullanır.
- **Yapım tetikleyicisi:** Paralel geliştirme, planlı gelecek revision veya tarihsel geçerlilik isteyen ilk onaylı use-case.
- **İlgili:** MOD-0290 Product / Item / SKU Master.
- **Bağımlılık / çıkış kriteri:** Effective-dating sınırı, concurrency token davranışı, seçim algoritması ve conflict failure path'leri Domain Contract ile onaylanmalıdır.

### BL-017 — Packaging Hierarchy
- **Nedir:** Inner pack, case, shipper ve pallet gibi çok seviyeli paketlerin parent-child yapısı, seviye dönüşümleri ve lojistik ilişkileridir.
- **Neden ertelendi:** İlk faz yalnız uygulanabilir GSKU için `PackQuantity` ve `PackUomCode` sunumunu taşır; çok seviyeli paket kimliği ve hiyerarşisi ilk faz gereksinimi değildir.
- **Yapım tetikleyicisi:** Onaylı lojistik, depo, regülasyon veya ticari kullanım senaryosunun çok seviyeli paket yapısı istemesi.
- **İlgili:** MOD-0290 Product / Item / SKU Master; gelecekteki lojistik ve manufacturing consumer'ları.
- **Bağımlılık / çıkış kriteri:** Paket seviyesi sözlüğü, cardinality, conversion ve lifecycle kuralları onaylanmadan hierarchy alanları MOD-0290'a eklenmez.

### BL-018 — Market Supply Assignment
- **Nedir:** Registered Presentation/LSKU'nun pazara sunulan Finished Good ile gelecekte kuracağı açık atama ilişkisidir.
- **Neden ertelendi:** İlk fazda LSKU–Finished Good doğrudan ilişki değildir; pazar, regülasyon ve supply bağlamını taşıyan ayrı ilişki henüz sözleşmelendirilmemiştir.
- **Yapım tetikleyicisi:** İlk onaylı market-supply use-case'i ile MA/Registered Presentation sözleşmesinin hazır olması.
- **İlgili:** MOD-0290 Product / Item / SKU Master ve gelecekteki Market Supply / Regulatory Information capability sınırı.
- **Bağımlılık / çıkış kriteri:** Atama SoR'u, effective dating, market/legal-entity anlamları ve approval kuralları onaylanmalıdır; LSKU–Finished Good doğrudan FK ile kestirme yapılmaz.

### BL-019 — Marketing Authorization / Registered Presentation
- **Nedir:** Pazara özgü ruhsat (MA), ruhsat sahibi ve onaylı/registered presentation kimliği ile bunların tarihçe ve durum yönetimidir.
- **Neden ertelendi:** Regulatory SoR, MA lifecycle ve Registered Presentation ilk faz Product/SKU identity kapsamının dışındadır.
- **Yapım tetikleyicisi:** Regulatory Information Management domain contract'ı, otoritatif kaynak ve ilk onaylı market-registration use-case'i hazır olduğunda.
- **İlgili:** Regulatory Information Management capability'si; MOD-0290 yalnız onaylı dış kimliklere gelecekte referans verebilir. Kesin kanonik module sahipliği teslimat öncesinde Master 8.1 ile doğrulanacaktır.
- **Bağımlılık / çıkış kriteri:** SoR, cardinality, lifecycle, effective dating ve Market Supply Assignment sınırı onaylanmadan MA/Registered Presentation alanları MOD-0290'a taşınmaz.

### BL-020 — Artwork, Label ve Leaflet Lifecycle
- **Nedir:** Artwork, label ve leaflet artefact'larının version, approval, effective dating, publication ve retirement yaşam döngüsüdür.
- **Neden ertelendi:** Kontrollü doküman ve regulatory labeling workflow'ları Product/SKU identity ilk fazının dışındadır.
- **Yapım tetikleyicisi:** İlk onaylı labeling/controlled-document use-case'i ve artefact SoR kararı.
- **İlgili:** MOD-0238 Labeling Lifecycle ve ilgili Controlled Documents capability'si; MOD-0290 yalnız kimlik referansı sağlar.
- **Bağımlılık / çıkış kriteri:** MarketTradeName, LSKU/Finished Good ve yayımlanan dokümanlar arasındaki ownership ve version bağları onaylanmalıdır; binary/content lifecycle MOD-0290'a alınmaz.

### BL-021 — BOM, Manufacturing Version, Quality Specification, Batch ve Release
- **Nedir:** Üretim reçetesi/BOM, manufacturing version, quality specification, batch execution ve release/quarantine kararlarının kendi SoR ve lifecycle'larıdır.
- **Neden ertelendi:** Bunlar Product/SKU kimliğinden farklı manufacturing ve quality sorumluluklarıdır; ilk faza alınmaları domain sınırlarını ihlal eder.
- **Yapım tetikleyicisi:** İlk onaylı manufacturing execution veya quality-release entegrasyon use-case'i.
- **İlgili:** MOD-0193 BOM & Routings, MOD-0195 Batch Execution / eBR, MOD-0174 Lot/Batch/Serial Tracking ve MOD-0175 Quarantine/Blocked Stock; quality-specification sahipliği teslimat öncesinde doğrulanacaktır.
- **Bağımlılık / çıkış kriteri:** İlgili SoR ve entegrasyon kontratları MOD-0290 kimliklerini referans almalı; manufacturing/quality lifecycle'ı MOD-0290 içinde kopyalanmamalıdır.

### BL-022 — GTIN Lifecycle
- **Nedir:** GTIN tahsisi, issuer kapsamı, doğrulama, replacement, retirement, reuse yasağı ve tarihsel izlenebilirlik sözleşmesidir.
- **Neden ertelendi:** İlk fazda GTIN'in lifecycle ve dış kayıt otoritesi bulunmaz; basit bir metin alanıyla bu governance taklit edilmeyecektir.
- **Yapım tetikleyicisi:** İlk onaylı barcode/GS1 use-case'i, otoritatif provider ve lifecycle gereksinimleri hazır olduğunda.
- **İlgili:** MOD-0290 Product / Item / SKU Master'ın identifier sınırı; varsa dış GTIN registry/provider capability'si.
- **Bağımlılık / çıkış kriteri:** Issuer/namespace, uniqueness, status, replacement ve no-reuse kuralları ile provider erişim sözleşmesi onaylanmalıdır.

### BL-023 — Bulk Legacy Migration
- **Nedir:** Legacy ürün ve kod export'larının profiling, staging, mapping, steward reconciliation, dry-run, import ve rollback sürecidir. İlk fazda yalnız manuel legacy onboarding ve `LegacyAlias` vardır.
- **Neden ertelendi:** Gerçek legacy export bulunmadığından segment anlamı, duplicate oranı, cardinality ve migration sonucu kanıtlanamaz.
- **Yapım tetikleyicisi:** Gerçek legacy export'un sağlanması ve onaylı migration pack.
- **İlgili:** MOD-0290 Product / Item / SKU Master.
- **Bağımlılık / çıkış kriteri:** Kaynak kolon sözlüğü, veri-kalitesi raporu, deterministik mapping, steward kuyruğu, dry-run kabul ölçütleri ve geri alma planı onaylanmalıdır; legacy kod canonical code'a dönüştürülmez.

### BL-024 — MarketTradeName Official Downstream-Usage Contract
- **Nedir:** Onaylı MarketTradeName'in downstream business/reference kaydında, resmi export/published document'ta veya onaylı dış entegrasyonda kullanıldığını belirleyen olay ve kanıt sözleşmesidir.
- **Neden ertelendi:** İlk fazda bu kullanım olayını otoritatif üretecek integration/document capability'si yoktur; draft audit olayından sahte bir `IsUsed` türetilmez.
- **Yapım tetikleyicisi:** MarketTradeName kullanan ilk onaylı downstream consumer, resmi yayın veya dış entegrasyon use-case'i.
- **İlgili:** MOD-0290 MarketTradeName ownership'i ve gelecekteki integration/document/export consumer'ları.
- **Bağımlılık / çıkış kriteri:** Olay sahibi, business-event tanımı, idempotency, reference payload'ı ve retention/audit kanıtı onaylanmalıdır.

### BL-025 — ERP/PLM Ingestion, Distribution ve External Feed Implementation
- **Nedir:** ERP/PLM kaynaklarından veri alma, MOD-0290 verisini dağıtma ve dış feed'ler için mapping, transport, retry, reconciliation ve observability implementasyonudur.
- **Neden ertelendi:** İlk faz ERP/PLM feed ve dış entegrasyon teslimatı içermez; yalnız ileride kullanılacak sınırlar ve gate'ler korunur.
- **Yapım tetikleyicisi:** İlk approved external-feed use-case'i.
- **İlgili:** MOD-0290 Product / Item / SKU Master, MOD-0252 ERP ve MOD-0253 PLM.
- **Bağımlılık / çıkış kriteri:** DCP'deki deferral owner/trigger/exit kriterine göre source/consumer ownership, data contract, security, idempotency, retry ve reconciliation kanıtları onaylı teslimat artefact'ına alınmalıdır.

### BL-026 — External Data-Contract Publication
- **Nedir:** MOD-0290 sözleşmelerinin dış consumer'lar için runtime publication, versioning, compatibility, deprecation ve discovery yüzeyidir.
- **Neden ertelendi:** MOD-0003 internal registration/deferral kararı mevcut bir DCP gate'idir; runtime external publication ise ayrı teslimat ve consumer kanıtı gerektirir.
- **Yapım tetikleyicisi:** İlk onaylı external consumer veya yayınlanmış sözleşme ihtiyacı.
- **İlgili:** MOD-0003 Data Contract Registry ve MOD-0290 Product / Item / SKU Master.
- **Bağımlılık / çıkış kriteri:** Publication owner'ı, erişim yüzeyi, schema/version compatibility, security ve deprecation politikası onaylanmış bir delivery artefact'ında kapanmalıdır; internal registration kararı bu backlog maddesiyle ikame edilmez.

### BL-027 — Provider-Owned Legacy PSS-012 Governance Risk Assessment
- **Nedir:** Provider-owned eski PSS-012 veri setlerinin tenant provenance, version/publish durumu, approval/audit izi, kullanım bağımlılıkları ve remediation seçenekleri için ayrı risk değerlendirmesidir.
- **Neden ertelendi:** Mevcut legacy verinin toplu karantinası, toplu yeniden onayı veya migration'ı MOD-0290 ilk fazının ve bu backlog yazımının kapsamı değildir.
- **Yapım tetikleyicisi:** Reference Data owner tarafından ayrı risk değerlendirmesi ve ayrıca onaylanmış provider-owned delivery artefact'ı.
- **İlgili:** MOD-0048 Reference Data Management ve mevcut PSS-012 Business Reference Data Stewardship runtime adayı; PSS-012'nin kanonik identity/governance durumu ayrıca kapanmalıdır.
- **Bağımlılık / çıkış kriteri:** Owner; retain, remediate, quarantine veya migrate kararını veri kanıtıyla onaylamalıdır. Bu madde kapsamında şimdi toplu karantina, yeniden onay veya migration yapılmaz.

### BL-028 — MDM Governance Scaffold Reconciliation
- **Nedir:** MDM README ve domain-config içindeki `Diten.MdmService` servisinin mevcut olmadığına dair güncelliğini yitirmiş governance ifadelerinin, repodaki gerçek servis varlığıyla kontrollü biçimde uzlaştırılmasıdır.
- **Neden ertelendi:** Bu çalışma MOD-0290 alan modeli veya runtime teslimatı değildir; mevcut draft Module Pack'in approval ya da code-start kapısı yapılmadan ayrı governance bakımı olarak ele alınmalıdır.
- **Yapım tetikleyicisi:** MOD-0290 veya başka bir MDM implementation pack'i `ready-for-dev` aşamasına yaklaştığında.
- **Owner / ilgili:** MDM domain owner; `execution/domains/master-data-management/README.md`, `execution/domains/master-data-management/domain-config.md` ve gerçek `services/Diten.MdmService/` yapısı.
- **Bağımlılık / çıkış kriteri:** README/domain-config gerçek repo yapısıyla uyumlu olmalı, authority order korunmalı ve tarihsel kararlar silinmeden güncel durumdan açıkça ayrıştırılmalıdır. Bu backlog maddesi mevcut MOD-0290 Module Pack'in approval veya code-start şartı değildir.

### BL-029 — Product Abbreviation Register (ABB) Governance
- **Nedir:** Üç karakterli, SOP'ye göre grup genelinde tekil, yeniden kullanılmayan ve market rebrand'lerinden bağımsız Product Abbreviation Register; tahsis, reservation, duplicate correction ve segregation-of-duties kurallarıyla birlikte. **Karar (2026-08-03):** Bir GMG grup sınırı bir ERP tenant'ıdır; bu tenant içinde birden fazla Legal Entity bulunabilir. ABB tenant genelinde tekildir.
- **Neden şimdi yapılmıyor:** Mevcut MOD-0290 slice'ı Global Product/GSKU internal foundation ile sınırlıdır. `CanonicalCode` sayacı ve unique indexleri `TenantId` kapsamındadır; bu teknik gerçek tek başına ABB policy kararı değildi. ABB runtime aggregate'i, grammar'ı ve R&D/Master Data allocator/SoD sınırı henüz onaylanmamıştır.
- **Yapım tetikleyicisi:** DCP-005 onayı, MOD-0290'ın Master 8.1 product-identifier sahipliğinin ABB için owner kabulü ve ayrı approved/ready-for-dev follow-up pack.
- **Owner / ilgili:** Önerilen MOD-0290 Product / Item / SKU Master; Head of R&D + Supply Chain/Master Data business stewardship. Ayrı group-wide registry ABB için seçilmemiştir.
- **Bağımlılık / çıkış kriteri:** Üç-karakter grammar, tenant-geneli normalize uniqueness/no-reuse, rebrand davranışı, allocation SoD, audit ve change-controlled duplicate correction test edilebilir sözleşme olarak kapanmalıdır. Bu karar tek başına ABB issuance code-start vermez.

### BL-030 — Material Master Class/Grade ve Generic-versus-Printed Packaging Identity
- **Nedir:** API, HD, EXC, PP, BX, LB, LF ve DV sınıfları; sınıfa özgü identity keys; `grade değişirse yeni material identity`; supplier'ın ayrı qualified link olması; baskısız primer ambalajın shared material, basılı BX/LB/LF'nin artwork component olması sınırı. Aynı herbal distillate'in başka ürün veya formülasyonda kullanılması özgün material identity ve controlled HD code'u değiştirmez; ek kullanım gelecekte relationship olarak izlenir.
- **Neden şimdi yapılmıyor:** MOD-0290 ilk slice'ı raw-material/item modelini içermez; sınıf semantiği, grade değişim davranışı ve MOD-0238 artwork handoff'u onaylı değildir.
- **Yapım tetikleyicisi:** BL-029 çıkışı, DCP-005 owner kararı ve ayrı Material Master/Labeling contract + approved Module Pack'ler.
- **Owner / ilgili:** Önerilen MOD-0290 item-master scope; basılı artwork lifecycle için MOD-0238 Labeling Lifecycle. Controlled value hosting gerekiyorsa MOD-0048 owner contract'ı tüketilir, provider runtime bu maddeyle değiştirilmez.
- **Bağımlılık / çıkış kriteri:** Class-specific duplicate keys, grade invariant, generic shared-usage relation, supplier-link ayrımı ve printed-component handoff'u kabul/test kriterleriyle kapanmalıdır. Material SOP-code namespace scope'u owner-approved olarak kapanmadan Material code issuance yapılamaz; mevcut tenant-scoped CanonicalCode bu kararı ikame etmez.
- **Ayrı çıkış kriteri — Herbal distillate reuse across products:** Aynı HD ikinci bir ürün veya formülasyonda kullanıldığında özgün material identity ve controlled HD code korunmalı; ikinci ürün ABB'siyle yeni HD identity/code veya duplicate supplier/specification kaydı oluşturulmamalı; ek kullanım material-to-formulation/product usage relationship olarak izlenmelidir. Başka üründe kullanım tek başına yeni material revision veya controlled code gerekçesi değildir; yeni HD identity/revision yalnız gerçek material-identity değişikliği için gelecekteki Material Master policy'si altında değerlendirilir. Bu kriter bu görevde runtime relation, entity, field veya code oluşturmaz.

### BL-031 — SOP-Controlled FPF/FPP/Artwork Code and Revision Issuance
- **Nedir:** Permanent internal UID, MOD-0290 low-semantic `CanonicalCode`, SOP-controlled base code, controlled revision ve legacy/commercial alias rollerini ayıran; FPF/FPP/artwork grammar, reservation, no-reuse ve issuance SoD sözleşmesi.
- **Neden şimdi yapılmıyor:** Formula/Composition, Registered Presentation/MA, Market Supply Assignment ve MOD-0238 artwork identity contract'ları henüz hazır değildir; anlamlı `...-V1` kodlarını mevcut `CanonicalCode`/`RevisionIdentifier` alanlarına taşımak mimari ihlaldir.
- **Yapım tetikleyicisi:** DCP-005 adım 1-6 çıkışları, ABB ile Material/FPF/FPP/artwork için ayrı owner-approved namespace-scope kararları, gerçek Master 8.1 owner veya DCP-002 candidate kimlikleri, approved owner Module Pack'ler ve açık code-start onayı.
- **Owner / ilgili:** MOD-0290, MOD-0238 ve henüz kimliği/owner'ı çözülmemiş Formula/Composition, Registered Presentation/MA ve Market Supply Assignment capability'leri.
- **Bağımlılık / çıkış kriteri:** ABB scope'unun controlled grammar'lara etkisi ile Material/FPF/FPP/artwork namespace scope'ları ayrı ayrı kapanmalı; tenant-scoped `CanonicalCode` herhangi bir group-wide regulated-code namespace'i olarak varsayılmamalıdır. Ardından grammar, base-versus-revision cardinality, idempotent reservation/no-reuse, actor trust/SoD, historical lookup, alias migration ve cross-domain audit/reconciliation testleri geçmelidir. Scope kararları kapanmadan issuance yapılamaz.

### BL-032 — Regulatory Change-Impact and Affected-Market Assessment Matrix
- **Nedir:** Material, formula, primary-pack configuration veya artwork değişikliğini ilgili FPF/FPP/Registered Presentation ve bütün etkilenen marketlere bağlayan; tamamlanmamış market assessment varken implementasyonu engelleyen etki matrisi.
- **Neden şimdi yapılmıyor:** Source identity/revision grafiği, Registered Presentation/MA ve Market Supply Assignment henüz sözleşmelendirilmemiştir; market kapsamı güvenilir biçimde türetilemez.
- **Yapım tetikleyicisi:** BL-031 kontrollü issuance sözleşmesi ile BL-018/BL-019 market/regulatory sınırlarının onaylı ve tüketilebilir olması.
- **Owner / ilgili:** MOD-0209 Change Control + MOD-0237 Variations & Renewals; source identity owner'ları event producer, regulatory owner karar sahibi.
- **Bağımlılık / çıkış kriteri:** Impact edge/event kontratı, all-market completeness kuralı, assessment status/decision, implementation block, idempotency, audit/evidence ve reconciliation testleri kapanmalıdır.

### BL-033 — Artwork Language/Market Identity Semantics
- **Nedir:** Market'in artwork controlled-code segmenti olmadığı ve artwork-to-market kullanımının relation/attribute olarak tutulduğu sınır ile language için iki açık policy seçeneğinin karara bağlanmasıdır: (A) normalize language set artwork controlled-code segmentidir veya (B) normalize language set yalnız controlled/register attribute'dur. Policy owner seçim yapmadan hiçbir seçenek normatif veya implementation-ready değildir.
- **Neden şimdi yapılmıyor:** MOD-0238 runtime/module pack'i bu repo milestone'ında yoktur; ayrıca SOP §3.1 language'ı kod segmenti yaparken §6 language ve market'i attribute sayarak kendi içinde çelişmektedir.
- **Yapım tetikleyicisi:** SOP policy owner A veya B seçeneğini ve buna bağlı artwork identity/revision davranışını açıkça onayladığında, DCP-005 onaylandığında ve MOD-0238 için approved/ready-for-dev delivery artifact hazır olduğunda.
- **Owner / ilgili:** MOD-0238 Labeling Lifecycle; MOD-0029 yalnız açıkça gerekli görülen document/version binding dependency'sidir, labeling owner değildir.
- **Bağımlılık / çıkış kriteri:** Market'in artwork code segmenti olmadığı ve artwork-to-market kullanımının relation/attribute olduğu onaylanmalı; policy owner language için A veya B seçeneğini seçmelidir. Multilingual alphabetical ordering yalnız A seçeneğinde code-generation kuralı olabilir. Dil eklenmesinin yeni artwork identity, yeni revision veya yalnız attribute değişikliği yaratıp yaratmadığı seçilen identity/revision modeliyle birlikte test edilebilir şekilde kapanmalıdır. Bu backlog maddesi yeni runtime alanı, code generator, validation, API veya Module Pack oluşturmaz.

### BL-034 — Service Onboarding Governance & Runtime Registry Reconciliation
- **Nedir:** Mevcut bir domain'e yeni production mikroservisi eklendiğinde servis klasörü, owner/domain, canonical port, `AGENTS.md`, domain-config, `.antigravity` port/route/agent/workflow belgeleri, local runner'lar, Gateway, health/readiness, module self-registration, CI/Docker ve release kontrollerinin tek ve doğrulanabilir bir onboarding akışıyla birlikte güncellenmesidir. Tekrar drift oluşmaması için açık bir `add-service`/`onboard-service` workflow'u ile makine tarafından çalıştırılabilir service/port registry doğrulayıcısı gerekir.
- **Mevcut kanıt / drift (2026-08-09):** `Diten.MdmService` (`5059`) ve `Diten.HcmService` (`5060`) gerçek runtime servisleridir ve `run_all.sh` içinde bulunur; buna karşılık root `AGENTS.md` MDM servisinin henüz scaffold edilmediğini söyler ve MDM/HCM port/build/test envanterini içermez. MDM `domain-config.md` production service scaffold'ının bulunmadığını söylemeye devam eder. `.antigravity/rules/ports.md` ve `.antigravity/rules/GEMINI.md` aktif listelerinde `5059/5060` yoktur; `.antigravity/agents/devops-agent.md` MDM için yanlış `5050` değerini taşır. `add-module`, integration/route örnekleri, `ARCHITECTURE.md` ve `dev-runbook.md` de gerçek servis filosuyla tam hizalı değildir. `run-diten.sh` MDM'yi içerir fakat HCM'yi içermez. `.antigravity/workflows/` altında yeni bir production servisinin bütün bu yüzeylerini atomik biçimde kapatan bir service-onboarding workflow'u bulunmaz.
- **Neden şimdi yapılmıyor:** Aktif MOD-0290/ABB teslimat ve WorkCenter çalışmaları tamamlanmadan global engineering sistemi ile repo-level governance dosyalarını geniş kapsamlı değiştirmek, mevcut yoğun dirty worktree'de çakışma ve yanlış otorite güncellemesi riski yaratır. Bu madde mevcut business-module teslimatlarının code-start veya merge kapısı değildir; işler tamamlandıktan sonra ayrı governance/DevOps bakım dilimi olarak ele alınacaktır.
- **Yapım tetikleyicisi:** Aktif MOD-0290/ABB kapanış işleri ve devam eden WorkCenter teslimatı tamamlanıp ilgili branch'ler birleştirme hazırlığına geldiğinde; bir sonraki production mikroservisi scaffold edilmeden önce.
- **Owner / ilgili:** Enterprise Architecture + Platform/DevOps owner + ilgili domain owner'ları. Mevcut `BL-028 — MDM Governance Scaffold Reconciliation` MDM'ye özgü tarihsel metin temizliğini korur; BL-034 bunun fleet-wide, tekrar kullanılabilir ve doğrulanabilir üst sınırıdır.
- **Bağımlılık / çıkış kriteri:** (1) Servis kimliği/owner/port için tek canonical registry veya açık tek otorite seçilmeli; `AGENTS.md` ile `ports.md` eşzamanlı “single source of truth” iddiasında bulunmamalıdır. (2) `add-service`/`onboard-service` workflow'u; AGENTS proje ağacı, port/build/test/branch envanteri, domain-config runtime durumu, `ports.md`, `GEMINI.md`, ilgili ajan/workflow örnekleri, `run_all.sh`, `run-diten.sh` ve Windows runner, Gateway base/catch-all/health rotaları, JWT/tenant middleware, module self-registration, CI/Docker ve release checklist güncellemelerini zorunlu kapılar olarak içermelidir. (3) MDM `5059` ve HCM `5060` bütün aktif kaynaklarda hizalanmalı; yanlış/legacy `5050` anlatımları tarihsel örnek olarak açıkça ayrılmalı veya kaldırılmalıdır. (4) Makine doğrulayıcısı gerçek service klasörleri ve launch settings ile canonical registry, runner ve dokümantasyon arasındaki eksik/farklı portları fail-closed raporlamalıdır. (5) Build, health ve Gateway smoke kanıtı olmadan yeni servis onboarding'i tamamlanmış sayılmamalıdır.

### BL-035 — Stable Product Definition Identity and Revision Semantics
- **Nedir:** Global Product ile GSKU arasındaki ürün-tanımı kimliğini, onun revision'larını ve hangi değişikliğin yeni revision ya da yeni GSKU doğurduğunu açıkça ayıran domain contract'ıdır.
- **Neden şimdi yapılmıyor:** Mevcut kodda her First GSKU oluşturma kendi `ProductDefinitionRevision` kaydını üretir; bağımsız ve stabil bir Product Definition kimliği yoktur. Bunu varsayımla yeni aggregate veya alan ekleyerek çözmek, pack/product/strength/presentation sınırlarını yanlış dondurabilir.
- **Yapım tetikleyicisi:** Aynı ürün tanımından birden fazla GSKU/ambalaj varyantı üreten ilk onaylı gerçek use-case ve Product Data owner kararı.
- **Owner / ilgili:** MOD-0290 Product / Item / SKU Master; Composition, Packaging ve Registered Presentation sahipleri yalnız kendi sözleşmeleriyle bağımlıdır.
- **Bağımlılık / çıkış kriteri:** Stable identity, revision cardinality, effective dating, concurrency, GSKU bağlama kuralı, migration ve historical read modeli onaylanmalı; BL-016 ile çakışan kısım tek delivery contract altında uzlaştırılmalıdır.

### BL-036 — Product Legal Entity Scope Historical and Future-Dated Consumer Evaluation
- **Nedir:** Product Legal Entity Scope politikasının yalnız “şimdi” için değil, açık bir `asOfUtc` ile geçmiş ve gelecek dönemlerde deterministik değerlendirilmesi; downstream export, regulatory evidence ve audit consumer'larının aynı tarihsel sonucu almasıdır.
- **Neden şimdi yapılmıyor:** Domain policy effective-dated dönemleri saklıyor olsa da mevcut consumer sorguları server `UtcNow` kullanıyor ve dışarıya tarih parametreli bir consumer kontratı sunmuyor. MVP canlı erişim kontrolünü çözmekle sınırlıdır.
- **Yapım tetikleyicisi:** İlk historical export/audit/regulatory evidence veya future-dated activation use-case'i.
- **Owner / ilgili:** MOD-0290-FU03 Product Legal Entity Scope Assignment ve ilgili downstream consumer owner'ları.
- **Bağımlılık / çıkış kriteri:** Yetkili `asOfUtc` kaynağı, maksimum sorgu aralığı, timezone/UTC kuralı, period-boundary semantiği, permission, tenant isolation, immutable evidence ve pagination sözleşmesi onaylanmalıdır. Bugünkü UI veya JWT kapsamı tarihsel yetkiyi kendiliğinden vermemelidir.

---

## WorkCenter ön-koşulları (seam register — WorkCenter branch'ından ÖNCE karar/stub)

> WorkCenter (MOD-0024) backend'i yapılmadan ÖNCE, ertelenen özelliklerin (calendar, position-based access, notification) sonradan **regresyonsuz** takılabilmesi için şu soyutlama noktaları seam olarak konmalı. Frontend zaten var; backend + wiring bu seam'lere göre kurulmalı. **Bu branch'te YAPILMIYOR** — WorkCenter branch'ı için kayıt.

- **WC-1 — Birleşik Work-Item kontratı:** WorkCenter çok modülden görev toplar. Bugün `ApprovalTask` (workflow) var; ama workflow-dışı modüller de görev üretecekse tek bir "WorkItem" kontratı (id/tip/başlık/modül/entity-ref/assignee/due/durum/aksiyon) gerekir. Yanlış kurulursa her modül entegrasyonu yeniden yazılır. **En kritik seam.**
- **WC-2 — Çalışma-zamanı / takvim seam'i:** SLA/son-tarih hesabı (`SlaEscalationRule`) koda gömülmesin, bir "çalışma-zamanı" arayüzünden geçsin; şimdilik naive 7/24 dönsün. İleride gerçek çalışma saatleri + tatil (BL: Calendar) sadece arayüzü değiştirir, WorkCenter'a dokunmaz. (Kullanıcıların şirket çalışma saatleri bu seam sayesinde regresyonsuz eklenir.)
- **WC-3 — Assignee çözümleme seam'i:** `GetMyWorkflowTasks` bugün görevi user'a göre çözüyor. İleride position-based ([BL-008]) gelince atama pozisyondan türeyecek → atama bir "assignee resolver" arkasından geçsin ki WorkCenter yeniden yazılmasın.
- **WC-4 — Notification seam'i:** Görev bildirimleri (bell/email) bir arayüzden çıksın; gerçek notification (ertelenmiş/başka ekip) sonradan additive takılsın.
- **WC-5 — Görev-kaynağı kaydı:** Workflow için `WorkflowManifestProvider` var; workflow-dışı modüllerin de WorkCenter'a görev katkısı yapabilmesi için benzer bir kayıt yolu.

---

## Açık kararlar

### DEC-001 — "Yakında" disabled action butonları?
BL-001/BL-002'yi **şimdi** disabled satır-action'ı olarak göstermek (yol haritası sinyali) mi, yoksa yapılana kadar hiç koymamak mı?
- **Öneri:** Şimdilik koymamak. Yol haritası sinyali isteniyorsa, disabled **ama açık "Yakında / Coming soon" tooltip'i ile** — böylece bozuk değil kasıtlı okunur. Boş/açıklamasız ölü buton go-live'da anti-pattern (kullanıcı "bozuk mu?" diye bug açar).
- **Durum:** Sahip kararı BEKLİYOR.
