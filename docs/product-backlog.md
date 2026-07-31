# ERP-vNext — Product Backlog (Deferred / Out-of-Scope-for-Now)

> **Amaç:** Bilinçli olarak **ertelenen** özelliklerin tek kaydı. Her madde bir gerekçe ve bir "ne zaman yapılır" tetikleyicisiyle park edilir — böylece hiçbir şey sessizce unutulmaz ve hiçbir şey vaktinden önce yapılmaz.
> **Sahiplik modeli:** Claude = CONTROL TOWER (prompt yazar, canlı doğrular); yürütme = Antigravity ajanları. **Go-live kapsamı buradaki her şeyi HARİÇ tutar.**
> **Antigravity ajanları için (ZORUNLU):** Buradaki maddeler, onaylı bir module pack açıkça bu backlog'dan çıkarıp `approved`/`ready-for-dev` kapsamına almadıkça **UYGULANMAZ**. Bir backlog özelliğini "yardımcı olayım" diye kendiliğinden inşa etmek YASAKTIR. Bir talep bir backlog maddesine değiyorsa, kod yazmadan önce bu dosyayı referans göster ve module pack kapısına yönlendir.
> **Son güncelleme:** 2026-07-24.

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

### BL-017 — WorkCenter segment ↔ chip görsel ayrımını keskinleştir
- **Nedir:** İşlerim'de segment (Aktif/Bekleyen/Planlı, tek-seçim segmented-control) + chip (tip/sinyal, çoklu) tek satırda; segment beyaz kutuda dolu-mor aktif, chip'ler dışında. UX kritiği: **pasif segmentler hâlâ chip'lere benziyor** (ikisi de yuvarlak/sayaçlı). "9/10" için segmenti daha da ayrıştır.
- **Konuşulan yüzey:** İşlerim filter-row (2026-07-24).
- **Neden ertelendi:** Mevcut hâli çalışıyor ve yeterince ayrık; bu bir cila. Kullanıcı "şimdilik böyle kalsın" dedi.
- **Yapım tetikleyicisi:** UX polish turu. Seçenek: (a) segment başına `Durum:` etiketi/ikon, (b) pasif segmentleri pill değil düz-sekme göster (yalnız aktif dolu).
- **İlgili:** MOD-0024 WorkCenter, `.wcn-filterbar`/`.wcn-segments`.

### BL-016 — WorkCenter "Başlattıklarım / Outbox" (creator-scope takip)
- **Nedir:** Kullanıcının **oluşturup başkasına atadığı** (viewerRole=Creator/requester) aktif iş öğelerini takip ettiği yüzey — "Ahmet'e atadığım task'ı nerede görürüm?" sorusunun cevabı. İşlerim = yalnız kullanıcının ÜSTLENDİĞİ işler (assignee); başkasına atanan iş o kişinin İşlerim'idir. Creator-scope aktif takip için ayrı bir Outbox/"Başlattıklarım" görünümü gerekir (arama/filtre/recall/rapor).
- **Konuşulan yüzey:** İşlerim sorgusu (2026-07-24) — kullanıcı "sadece bana atananlar" scope'unu onayladı.
- **Neden ertelendi:** Spec §7 zaten "tam outbox"u **v1.5**'e koymuş; go-live için İşlerim (assignee-scope) yeter. Geçmiş'teki "Devrettiklerim" yalnız tarihsel, aktif takip değil.
- **Yapım tetikleyicisi:** v1.5 WorkCenter kapsam pack'i (outbox: arama/filtre/recall/rapor).
- **İlgili:** MOD-0024 WorkCenter, spec §4 (viewerRole=Creator), §7 v1.5.

### BL-015 — WorkCenter alternatif görünümler (Bölünmüş / Kanban / Takvim)
- **Nedir:** WorkCenterNext'in üç ek liste görünümü, go-live kapsamından çıkarılıp ertelendi. Kalan görünümler: **Liste · Tablo · (İşlerim'de Odak)**. Ertelenenler:
  - **Bölünmüş (Split):** in-app iki-panelli master-detay. Görev detayı artık **kendi sayfası** (`/WorkCenterNext/Details/{id}`, `openDetailPage`) olduğu için split emekliye ayrıldı.
  - **Kanban:** duruma göre sütunlu pano (`renderKanban`).
  - **Takvim (Calendar):** sağda takvim, solda planlanmamış işler (splitCard) + sürükle-planla ("drag-to-plan"). `splitCard` bileşeni zaten bunun için tasarlanmıştı.
- **Konuşulan yüzey:** WorkCenter view-switcher (Liste/Tablo/Kanban/Takvim/Odak ikonları).
- **Neden ertelendi:** Go-live için Liste + Tablo yeter; bu üç görünüm başlı başına UX+veri işi. Takvim ayrıca WC-2 çalışma-zamanı seam'ine bağlı.
- **Mevcut durum (2026-07-23):** `TAB_VIEWS`'ten çıkarıldı → seçilemez; tüm satır-açma yolları detay sayfasına yönlendirildi. `renderSplit/renderKanban/renderCalendar` fonksiyonları kodda **erişilemez (dead)** duruyor — geri getirilince temel var; istenirse ayrı bir temizlik commit'inde silinir.
- **Yapım tetikleyicisi:** Ayrı onaylı WorkCenter view pack (her görünüm bağımsız gelebilir). Takvim, WC-2 seam'i kurulduktan sonra.
- **İlgili:** MOD-0024 WorkCenter, WC-2 (çalışma-zamanı/takvim seam'i), `splitCard` bileşeni.

### BL-026 — Meeting invite yanıtı sonrası Takvim ve WorkCenter Ajanda bağlantısı
> **Not (2026-07-25):** Bu madde daha önce yanlışlıkla `BL-016` numarasıyla açılmıştı; `BL-016` "Başlattıklarım / Outbox" maddesine aittir (yukarıda). Alıntılar belirsizleşmesin diye bu madde **BL-026**'ya taşındı; içerik değişmedi.
- **Nedir:** Meeting invite, WorkCenter Inbox içindeki trigger-only “Hızlı Yanıt Bekleyenler” yüzeyinde `Kabul et / Reddet / Takvimde Aç` aksiyonlarıyla gösterilir. Yanıt verildiğinde trigger Inbox'tan çıkar; kabul edilen toplantı **İşlerim'e dönüşmez**. Authoritative toplantı kaydı, katılım durumu, tarih/saat, katılımcılar ve sonradan yapılan yanıt değişiklikleri Takvim modülünde yönetilir. WorkCenter ileride kabul edilmiş yaklaşan toplantıları “Bugünkü Ajanda” içinde salt-okunur özet ve `Takvimde Aç` bağlantısıyla gösterebilir.
- **Davranış sınırı:** Toplantıdan doğan gerçek işler ayrı `task`, `review`, `approval` veya davranışına göre acknowledgment work item olarak üretilir ve normal Task Detail açar. Meeting trigger'a task lifecycle uydurulmaz.
- **Neden ertelendi:** MOD-0024 mevcut slice'ı frontend-only canonical fixture/Task Detail kapsamındadır; gerçek Calendar provider, RSVP command, projection refresh ve Ajanda veri bağlantısı yoktur.
- **Yapım tetikleyicisi:** Calendar/meeting provider kontratı ve WorkCenter aggregation backend'i için ayrı onaylı capability/module pack; BL-015 Takvim görünümünden bağımsız olarak önce RSVP + source-navigation seam'i teslim edilebilir.
- **İlgili:** MOD-0024 WorkCenter, BL-015, WC-1 birleşik WorkItem kontratı, WC-2 çalışma-zamanı/takvim seam'i.

### BL-018 — Enterprise Strategy'yi WorkCenter sağlayıcısı yap (Binding A / MOD-0023)
- **Nedir:** Enterprise Strategy onayları bugün serbest-metin `ApprovalStatus` alanı — gerçek bir kuyruk değil; hiçbir mekanizma bunları WorkCenter'a iş olarak itmiyor. Bu onayları MOD-0023 `ApprovalTask` kuyruğuna (Binding A) taşı ki ES WorkCenter'a **gerçek** iş itsin. Basit salt-okunur strateji durumu gerekirse doğrudan sağlayıcı (Binding B) olabilir.
- **Yapım tetikleyicisi:** WC-1 dilimi **shipped olduktan SONRAKİ** dalga. WC-1'in ilk kanıtı MOD-0023'ün kendi onaylarıdır (ES değil); ES bu ilk kanıttan sonra ikinci sağlayıcı olarak bağlanır.
- **İlgili:** DCP-004 OD-WC-02 · §10.4 (A/B binding law) · §17 · WC-1 birleşik WorkItem kontratı.

### BL-019 — Blueprint canonical MOD-xxxx tahsisi (CAND-CAP-0006 mezuniyeti)
- **Nedir:** EA, Work Aggregation / Task Center (Görev Merkezi) için Blueprint'e canonical bir `MOD-xxxx` satırı açar ve `CAND-CAP-0006 → MOD-xxxx` deprecated-alias zincirini kaydeder (DCP-002). Blueprint'te bugün karşılık yok (doğrulandı); CAND-CAP-0006 geçici governance kimliğidir.
- **Yapım tetikleyicisi:** Yetenek **WC-1'de kanıtlanınca** (şimdi değil). CAND-CAP-0006 WC-1 dilimi boyunca kalır; MOD-xxxx tahsisi ayrı bir EA kararıdır.
- **İlgili:** DCP-004 §1 (EA follow-up) · §19.1 · OD-WC-03 · DCP-002 (kimlik canonicalization) · module-id-registry.

### BL-020 — MOD-0023 pack reconciliation (stale ifade düzeltmesi)
- **Nedir:** MOD-0023 module pack'i "No code is produced by this pack" diyor ve Batch 01 kutuları işaretsiz; ama `ApprovalTask` entity + `GetMyWorkflowTasks` query/handler runtime'ı **gerçekte shipped**. Pack'in framing'ini (durum ifadesi + Batch 01 kutuları) gerçek runtime durumuna göre düzelt.
- **Yapım tetikleyicisi:** Ayrı bir governance edit'i (DCP-004 charter'ı MOD-0023 pack'ine dokunmadı; bu düzeltme ondan bağımsız yapılır).
- **İlgili:** DCP-004 §20 F1 · §19.4 · MOD-0023 module pack.

### BL-021 — Enterprise Strategy fixture-truth cleanup (QA)
- **Nedir:** ES fixture'larındaki `processInstanceId` + `lifecycleOwner: workflow` temsilî; 3/3 deep-link rotası gerçek workflow rotasıyla uyumsuz (fixture-doğruluk borcu). Gerçek sağlayıcı bağlanınca fixture'lar gerçek rota/alan kullanmalı. Bu iş executable kontratı (`fixture-contract.js`) **DEĞİŞTİRMEZ** — yalnız fixture veri doğruluğunu düzeltir.
- **Yapım tetikleyicisi:** ES gerçek sağlayıcı olunca (BL-018 ile birlikte).
- **İlgili:** DCP-004 §20 F4 · §19.5 · BL-018 · WorkCenterNext ES provider fixtures.

### BL-022 — Görev Merkezi tenant modül manifest'i + katalog self-registration
- **Nedir:** WorkCenter/Görev Merkezi'nin tenant modül katalogunda görünmesi, navigasyona düşmesi, izninin (`platform.work-aggregation.inbox.view`) tanımlanıp seed edilmesi ve tenant'a atanabilmesi (entitlement) için bir `WorkAggregation` **manifest provider'ı** gerekir — mevcut 6 tenant modülü (Organization/Workflow/ReferenceData/DocumentManagement/AccessGovernance/TenantSettings) gibi. Bugün WorkCenter'ın manifest'i **YOK**.

  **DÜZELTME (2026-07-25, kodda doğrulandı):** Bu maddenin ilk halinde "manifest + catalog→auth sync izin seed'ini de çözer" yazıyordu — **yanlış**. Gerçek: izin **anahtarı** otomatik oluşuyor (`PlatformPermissionAutoRegistrationWorker` her `[HasPermission]` anahtarını senkronize eder), ama tenant kullanıcısına **verilmesi (grant)** otomatik değil — tenant-Admin baseline'ı küratörlü bir allow-list ve `work-aggregation` orada yok. **Karar (EA 2026-07-25): entitlement** (`IsTenantAssignable: true`, non-baseline) — modül tenant'a atanınca entitlement→permission köprüsü izni tenant Admin'e verir, korumalı `Diten.AuthService` dosyasına dokunulmaz; bedeli, operatör modülü atayana kadar WorkCenter'ın görünmemesi.

  **⚠ TEHLİKE (B2 — scope zehirlenmesi):** A1 worker `moduleCode/scope = null` ile senkronize ettiği için anahtar `Module="platform"`, `Scope=PlatformAdmin` olarak oluşabilir; sonradan gelen manifest `Module`'ü düzeltebilir ama `Scope`'u **asla Tenant'a düşüremez** (`InternalPermissionsController.cs:146-151` — "most restrictive wins"). `PlatformAdmin` kapsamlı bir anahtar hiçbir tenant rolüne atanamaz. WC-1 attribute'u zaten shipped (`866bcbf3`) olduğu için, saklanan `Module`/`Scope` değerinin **doğrulanması/onarılması WC-1b'de zorunlu kabul kriteridir**.
- **Yapım tetikleyicisi:** **WC-1b** (frontend wiring) dilimi — manifest + sayfa + nav + l10n birlikte gider. WC-1 backend projeksiyonundan bağımsız; onu bloklamaz. Additive (self-reg reconcile asla revoke etmez) → WC-1 sonrası yapmak regresyon çıkarmaz, çünkü stabil kimlikler (ModuleCode/permission/shell) zaten kilitli.
- **İlgili:** DCP-004 §8 (WC-1b slice) · CAND-CAP-0006 WC-1 pack §3 (permission note) · module self-registration manifest sistemi (`IModuleManifestProvider`) · catalog→auth permission sync · nav l10n bridge (stable-code).

### BL-023 — WorkCenter "Ekibim" kapsam seçici (yönetici görünümü)
- **Nedir:** Yönetici, altında çalışanların görevlerini görebilsin. Üç kavram AYRIDIR ve karıştırılmamalı: (a) bana atanan → **İşlerim** (mevcut), (b) benim başkasına attığım → **Outbox** (BL-016), (c) **astlarımın kendi görevleri (ben atamadım)** → bu madde. Uygulama şekli: WorkCenter üstünde bir **kapsam seçici** (`Ben ▾ / Ekibim`) — **yeni sekme DEĞİL**, çünkü eksen yasası "sekme = sahiplik" kilitlidir; kapsam seçici yalnız "kimin sahipliği" sorusunu değiştirir (SAP My Inbox deseni). Hiyerarşi altyapısı HAZIR: `Position.ReportsToPositionId` + `OrganizationUnit.ManagerPositionId`.
- **Neden ertelendi:** Create/self-task dilimi önce bitmeli (görev üretimi olmadan ekip görünümünün içi boş). Ayrıca veri erişim kapsamı (data-scoping) kararı gerektiriyor: yönetici astının görevinin TÜM alanlarını mı görür, özet mi? Spec bugün "üst-yönetim gözetimi = ayrı merkez (Cockpit)" diyor; bu madde WorkCenter içinde hafif bir ekip görünümü olarak konumlanır.
- **Yapım tetikleyicisi:** MOD-0024 create/self-task dilimi shipped olduktan sonra; ayrı onaylı kapsam.
- **İlgili:** spec §7 v1.5 (team scope) · BL-016 (Outbox) · MOD-0288 Organization (Position/OrgUnit hiyerarşisi) · DCP-004 (Task Center kişisel yüzey ilkesi).

### BL-024 — Yapılandırılabilir alanlarda alan-seviyesi yetki (businessContext Faz 2)
- **Nedir:** Görev formundaki yapılandırılabilir alanlar (Faz, İş Türü, Pazar/Ülke, Domain, Maliyet vb.) iki katmanlı olacak: **Faz 1 = alan tanımı** (hangi alanlar var — tenant/modül bazlı), **Faz 2 = alan yetkisi** (hangi alanı kim görür/yazar — rol/pozisyon bazlı). Örnek: "Maliyet" alanı yalnız yöneticiye görünür. Executable kontrat bunu ZATEN destekliyor: `classification`, `accessState`, `redacted` (yetkisiz değer tarayıcıya hiç gönderilmez, CSS ile saklanmaz).
- **Neden ertelendi:** Alan-seviyesi güvenlik başlı başına bir iş (tanım UI'ı + değerlendirme + test matrisi). Faz 1 alan tanımıyla create dilimi çalışır hale gelir; yetki additive eklenir (kontrat hazır olduğu için regresyonsuz).
- **Yapım tetikleyicisi:** MOD-0024 create dilimi Faz 1 shipped olduktan sonra; ayrı onaylı kapsam.
- **İlgili:** `fixture-contract.js` (VALUE_TYPES + redaction invariant) · MOD-0024 create pack · MOD-0018 RBAC/ABAC.

### BL-025 — In-app bildirim kanalı + header çanını (bell) gerçek veriye bağlama
- **Nedir:** Tenant shell'deki bildirim çanı (`_LayoutTenantShell.cshtml:395-421`) şu an **çalışmıyor — sadece tema süsü**: bildirim sayısı kodda sabit (`NewNotifications`, `8`), listedeki avatarlar Sneat şablonunun örnek resimleri (`assets/img/avatars/1.png`), ve çanı besleyen **hiç JS yok**. Backend tarafında da in-app kanal yok: `NotificationChannelCode` enum'ında **yalnız `Email = 0`** var. Yani "görev atandı" bildirimi e-posta ile gidebilir (altyapı hazır) ama çanda **hiçbir zaman görünmez**.
- **Gerekenler:** (a) `NotificationChannelCode.InApp` kanalı + dispatch'in in-app okunması; (b) okunmamış bildirim listesi ucu + "okundu işaretle" / "tümünü okundu işaretle"; (c) çanın gerçek API'ye bağlanması (sabit `8` ve örnek avatarların kaldırılması); (d) 7-dil l10n.
- **Neden ayrı iş:** Çan **tüm modüllerin ortak altyapısı** (yalnız WorkCenter'ın değil) — bir modül dilimi içinde yapılırsa sahiplik karışır. Ayrıca e-posta yolu bundan bağımsız çalışabildiği için WorkCenter create dilimini bloklamaz.
- **⚠ Risk (bu yüzden kayıtlı):** Çan bugün **çalışıyor gibi görünüyor** ama görünmüyor; kullanıcı "bildirim gelmedi" diye hatalı hata bildirir. Yanıltıcı UI, kayıt altına alındı.
- **KISMEN ELE ALINDI (2026-07-31, `7e7e8c40` — WC-4):** yanıltıcı yüzey **kaldırıldı**. Sahte `8` rozeti, dört Sneat örnek kaydı, stok avatarlar ve arkasında ne JS ne sayfa olan üç kontrol ("Tümünü göster" / "Tümünü okundu işaretle" / satır başına okundu-arşivle) silindi. Çan duruyor ve **boş olduğunu söylüyor** (`NoNotifications`, 7 dil). Gerekçe: WC-4 gerçek bildirim üretmeye başladığı an sabit "8" kesin bir yanılgı üretirdi — kullanıcı görev atar, çanda 8 durur, her gerçek bildirim "gelmedi" diye raporlanır. **Çanı gerçek veriye bağlamak hâlâ bu maddenin işi.**
- **SAHİP KARARI (2026-07-31) — çanın şekli: (b) iki aşamalı.** Dropdown **+** "Tümünü göster" → **ayrı bir sayfa**. Reddedilen seçenek (a) yalnız-dropdown idi.
  - **Gerekçe — ölçek:** MOD-0024 tek başına beş olay üretiyor (`assigned` · `claimed` · `completed` · `approvalrequested` · `duesoon`). Diğer modüller bağlandığında dropdown'a sığmaz, ve kurumsal kullanıcı *"geçen hafta bana ne atanmıştı"* sorusunu sorar — bu soru sayfalama ve filtre (okunmuş/okunmamış, modül, tarih aralığı) ister. SAP ve ServiceNow bu yüzden ayrı sayfa taşır.
  - **Sıra bağlayıcı:** önce dropdown **gerçek veriye** bağlanır, "Tümünü göster" **ancak o zaman** eklenir ve **gerçek bir sayfaya** gider. Ölü bağlantı bırakılmaz — o hata bu çanda bir kez yapıldı ve yukarıdaki temizliğin sebebi oldu.
  - **Bugün tenant tarafında bildirim yüzeyi yok:** mevcut dört bildirim ekranı **Platform Admin** operatör ekranları (`Platform/NotificationDispatches` · `NotificationTemplates` · `NotificationEvents` · `NotificationSettings`) ve tüm uçlar `/api/platform/*`. Tenant kullanıcısının çağırabileceği tek bir bildirim ucu yok — sayfa da uç da sıfırdan kurulacak.
- **Yapım tetikleyicisi:** Ayrı onaylı kapsam (platform bildirim dilimi). WC-4 seam'i bu maddeyi kapsar.
- **İlgili:** WC-4 (notification seam) · `Features/Notifications` (template/dispatch/event altyapısı MEVCUT, e-posta çalışır) · `ModuleManifestDocument.NotificationEvents` (modüller olaylarını manifest'te beyan eder) · MOD-0024 create dilimi (yalnız e-posta kullanır).

### BL-027 — Premium modal helper'ını tüm modüllere yay (kopyala-yapıştır HTML'i bitir)
- **Nedir:** `premium-modal-standard.md` (MOD-0013) çıplak/özelleştirilmemiş SweetAlert2'yi yasaklıyor ve `swal-icon-circle` premium ikon haznesi + `rounded-4`/`shadow-lg` + `buttonsStyling:false` + Sneat butonları şart koşuyor. Ama projede **paylaşılan bir helper yoktu**: standardı uygulayan 6 dosya (`Account/login.js`, `Account/forgot-password.js`, `Account/reset-password.js`, `Governance/Users/index.js`, `Platform/AuditLog/index.js`, `Platform/Tenants/details.js`) aynı premium HTML'i **kendi içinde tekrar yazıyor**. MOD-0024 create dilimi ile `wwwroot/assets/js/shared/` altına tek bir helper eklendi (error/success/confirm/info) ve Tasks onu kullanıyor.
- **Kalan iş:** yukarıdaki 6 dosyayı (ve sonradan eklenen benzerlerini) helper'a geçir; kopyalanmış inline HTML bloklarını sil. Görsel çıktı birebir aynı kalmalı (regresyon yok).
- **Neden ertelendi:** Her dosya farklı akış (login/şifre sıfırlama/audit/tenant) — tek tek görsel doğrulama gerekiyor; MOD-0024 dilimini bloklamasın diye ayrıldı. Additive: helper zaten yerinde, migrasyon dosya bazında yapılabilir.
- **Yapım tetikleyicisi:** MOD-0024 Faz 1 kapandıktan sonra, tercihen frontend bakım dilimi içinde.
- **İlgili:** `.antigravity/rules/premium-modal-standard.md` (MOD-0013) · MOD-0024 create dilimi (helper'ın kaynağı) · FG-003 (inline CSS yasağı — helper'da da geçerli).

### BL-028 — Görev bağımlılıkları: komut + `blockedState` projeksiyonu (yarım kalmış yetenek)
- **Nedir:** `TaskDependency` şeması MOD-0024 Faz 1'de kuruldu ve **tipli** (`FinishToStart` vb.); detay sorgusu bağımlılıkları okuyor, `TASK_DEPENDENCY_INVALID` hata kodu tanımlı. **Ama:** bağımlılık kurma/kaldırma komutu YOK ve sağlayıcı `blockedState` üretmiyor. Yani "bu görev bitmeden şu başlayamaz" kuralı hiçbir yerde uygulanmıyor; bağımlılık okunabiliyor, kurulamıyor, hiçbir aksiyonu bloklamıyor.
- **Gerekenler:** (a) bağımlılık ekle/kaldır komutları (yalnız MOD-0024'ün kendi görevleri arasında — pack §12 Y3); (b) sağlayıcı `blockedState` doldursun (`blocked`, `blockers[]`, `affectedActionCodes`) ki kontrat gereği ilgili aksiyon **disabled + sebepli** gelsin; (c) döngü tespiti (A→B→A reddedilmeli); (d) WorkCenter'da **salt-okunur** gösterim + tipli sebep ("FS: X bitmeden başlayamaz"); (e) 7-dil.
- **Sınır:** bağımlılık **editörü** aggregator'da olmaz (spec ASLA listesi: dependency graph/Gantt kaynağa deep-link). MOD-0024 kendi görevlerinin kaynağı olduğu için kendi kenarlarını yönetebilir; WorkCenter yalnız render eder.
- **Neden ertelendi:** Faz planında (1-5) hiçbir yerde yok — şema kurulmuş ama runtime planlanmamıştı; CT taramasında ortaya çıktı (2026-07-26). Bloklama semantiği checklist'le karışmasın diye ayrı tutuldu: **checklist tamamlamayı bloklar, alt görev bloklamaz, bağımlılık başlatmayı/tamamlamayı bloklar** — üçünün sınırı net yazılmalı.
- **YAPILDI (2026-07-29):** (a) `AddTaskDependencyCommand`/`RemoveTaskDependencyCommand` + `POST|DELETE /api/v1/tasks/{id}/dependencies[/{depId}]` + Diten.Web proxy rotaları; (b) döngü tespiti — kendine bağımlılık, A→B→A ve daha uzun zincirler reddediliyor (elmas deseni reddedilmiyor), yinelenen kenar 409; (c) sağlayıcı `blockedState` üretiyor, ilgili aksiyon **disabled + sebepli** geliyor (gizlenmiyor); (d) bağımlılık listesi + kırmızı engel banner'ı tipli cümleyle ("FS: X kapanmadan başlanamaz") + hangi aksiyonu engellediği; (e) 7 dil. Bloklama semantiği tek yerde: `TaskDependencyRules` (FS/SS → başlatma, FF/SF → tamamlama; **iptal edilen öncül bloklamaz**). Sözleşme dağarcığı bildirildi: `DEPENDENCY_TYPES` motorun PascalCase yazımı, durum `SUBTASK_STATUSES`'i paylaşıyor. `TaskDependencyType` artık string serialize ediliyor — guard testi sayısal gitmesini yakaladı. Aksiyonu teklif edilmeyen bir blocker (Open görevde FF) düşürülüyor: kontrat her `affectedActionCode`'un görünür-devre-dışı bir aksiyonu adlandırmasını şart koşuyor.
- **YARIM KALMIŞTI, TAMAMLANDI (2026-07-29, aynı gün):** ilk turda kural **yalnız projeksiyonda** vardı — `blockedState` doğru, buton kapalı ve sebepli, ama `POST /api/v1/tasks/{id}/start` 204 dönüyordu ve görev açık öncülüne rağmen başlıyordu (CT canlı kanıtı: görev 2c3896fc). 22 test yeşildi çünkü hepsi **projeksiyonu** doğruluyordu; hiçbiri geçişi POST etmiyordu. `TransitionTaskItemHandler` artık `TaskDependencyRules`'u çağırıp **409 + `DEPENDENCY_BLOCKED`** ile reddediyor (onay kapısından sonra, sebep önceliği projeksiyonla aynı olsun diye). Sebep kodu bilerek projeksiyonunkiyle **aynı string**. Testler artık gerçek `TasksController` eylemini çağırıyor — URL→hedef eşlemesi de kapsam içinde. Ders, `cancel` guard'ındakinin aynısı: **gizli/kapalı buton sunumdur, red kuraldır.**
- **CT CANLI DOĞRULAMA (2026-07-29, `764cb01c`):** 8 vaka gerçek HTTP ile geçildi — FS: `start` **409 `DEPENDENCY_BLOCKED`** · FF: `start` 204 ama `complete` 409 · SS: `start` 409, öncül başlayınca 204 · SF: `start` serbest, `complete` 409, öncül başlayınca 204 · öncül `Done` olunca ardıl `start` 204 · **iptal edilmiş öncül bloklamıyor** (204). Yön ayrımı doğru: tamamlamayı kapatan kenar başlatmayı kapatmıyor. Kapanış: DOĞRULANDI.
- **Yapım tetikleyicisi:** ~~Ayrı onaylı dilim~~ → yapıldı.
- **İlgili:** MOD-0024 create pack §3 (`TaskDependency`), §12 Y3 · `fixture-contract.js` `blockedState` invariantları (`BLOCKER_ACTION_REFERENCE_INVALID`) · DCP-004 §10.2 (actions[] projeksiyon kuralı) · BL-035 (alt görev bloklaması aynı `blockedState` şeklini kullanacak — blocker'ın bağımlılık alanları opsiyonel bırakıldı).

### BL-029 — Eski `/WorkCenter` yüzeyinin sökülmesi
- **Nedir:** Diten.Web'de **iki** Görev Merkezi yüzeyi var, ikisi de "Görev Merkezi" başlıklı: `/WorkCenter` (`WorkCenterController` — kendi İngilizce mock'u, sekmeler "Gelen Kutusu / All Work", fixture tarihleri 2026-03/04'te donmuş) ve `/WorkCenterNext` (canlı MOD-0024 + MOD-0023 sağlayıcıları). Bütün DCP-004 işi ikincisinde. Sol menü doğru şekilde `/WorkCenterNext`'e gidiyor.
- **Gerekenler:** (a) `WorkCenterController.Index`'in `/WorkCenterNext`'e 302 forward etmesi (Tasks/Index'te uygulanan aynı desen — kalıcı 301 değil); (b) `Meeting` ve `Task` sayfalarının akıbeti: WorkCenterNext'in kendi detay yüzeyi bunları karşılıyorsa silinir, karşılamıyorsa taşınır — **karar önce**; (c) `DevScenarios` geliştirici yüzeyi ya WorkCenterNext altına taşınır ya kaldırılır; (d) eski mock verisinin (`MEETINGS`/`NOTES` dışındaki İngilizce fixture'lar) temizliği.
- **Neden ertelendi:** CT canlı doğrulamasında ortaya çıktı (2026-07-26). Giriş yönlendirmesi ayrı ve acil bir hataydı (5 yerde `/WorkCenter` default'u) ve hemen düzeltildi; **yüzeyin sökülmesi** ise `Meeting`/`DevScenarios`'un nereye gideceği kararına bağlı olduğu için ayrı dilim. Karar verilmeden silinirse çalışan iki sayfa kaybolur.
- **Yapım tetikleyicisi:** MOD-0024 Faz 4-5 sonrası; toplantı daveti çipinin gerçek bir sağlayıcıya bağlandığı dilimle birlikte yapılması doğal.
- **İlgili:** `AccountController` post-login default · MOD-0024 pack (Tasks/Index → WorkCenter 302 forward emsali) · `mock-data.js` `MEETINGS`/`NOTES`.

### BL-030 — `DateTimeOffset` BSON dizi temsili: kök neden migrasyonu
- **Nedir:** MongoDB C# sürücüsü `DateTimeOffset`'i varsayılan olarak **BSON dizisi** (`[ticks, offsetMinutes]`) olarak saklar. `Diten.Platform.Infrastructure/DependencyInjection.cs:170-171` yalnız `GuidSerializer` ve `DecimalSerializer` kaydediyor; `DateTimeOffsetSerializer` **kayıtlı değil**. `Diten.Platform.Common.Persistence.BaseEntity` ise `CreatedAt` (`DateTimeOffset`) ve `UpdatedAt` (`DateTimeOffset?`) taşıyor ve Platform'daki **her** tenant-scoped varlığın atası. Sonuç: iki tarih alanına birden sıralayan her sorgu `MongoCommandException: cannot sort with keys that are parallel arrays` ile **çalışma zamanında** patlar. Derleme temiz geçer, testler (fake repository'ler) yeşil kalır.
- **Kanıtlanmış vaka:** `WorkflowRepositories.GetLatestByObjectRefAsync` (`StartedAt` + `CreatedAt`) → MOD-0023 geçiş kapısı hiç değerlendirilemiyordu; canlı doğrulamada yakalandı (2026-07-26). Ayrıca `DocumentManagementAccessMatrixRepositories.cs:70` aynı sınırlamaya çarpıp **bellekte sıralayarak** geçmiş — yorumu duruyor ("in-memory sort … avoids the limitation"), yani bilgi vardı ama genellenmedi.
- **Neden ertelendi:** Kök neden düzeltmesi (global `DateTimeOffsetSerializer` kaydı) **diskteki temsili değiştirir** — mevcut dokümanlar dizi olarak kalır, dolayısıyla veri migrasyonu ister ve tüm servisleri etkiler. Acil olan tek çağrı yerinde cerrahi olarak düzeltildi; sınıfın tamamı ayrı ve onaylı bir dilim olmalı.
- **Gerekenler:** (a) hedef temsile karar (`BsonType.DateTime` — UTC'ye normalize, offset kaybı kabul mü? — yoksa alt-doküman/string); (b) mevcut koleksiyonlar için migrasyon; (c) `DateTime` (skaler, güvenli) ile `DateTimeOffset` (dizi) ayrımının neden **iki farklı `BaseEntity`** sınıfında yaşadığının temizliği (`Domain.Common.BaseEntity` `DateTime` kullanıyor, `Common.Persistence.BaseEntity` `DateTimeOffset`); ~~(d) yeni çok-anahtarlı tarih sıralamasını yakalayan guard~~ → **yapıldı** (2026-07-26), aşağı bak.
- **Doğrulandı ve düzeltildi (2026-07-26):** `BusinessReferenceDataStewardshipRepository.GetUsageRegistrationsAsync` (`UpdatedAt` + `CreatedAt`) gerçek MongoDB'ye karşı koşuldu ve **kırık çıktı** — `UpdatedAt`'i dolu tek bir kayıt tüm listelemeyi öldürüyordu. Aynı desenle (bellekte sıralama) düzeltildi. Platform'da bilinen başka çok-anahtarlı `DateTimeOffset` sıralaması kalmadı; `SavedViewRepository` `DateTime` kullandığı için etkilenmiyor.
- **Guard yerinde:** `DateTimeOffsetSortGuardTests` tüm `services/**` üretim kaynağını tarayıp iki `DateTimeOffset` anahtarlı `SortBy*/ThenBy*` zincirlerini reddediyor. BL-030 kapatılıp global serializer kaydedildiğinde bu guard ve koruduğu bellek-içi sıralamalar **birlikte** kaldırılmalı; `WorkflowInstanceLookupMongoTests.Server_side_sort_on_two_date_time_offset_keys_is_still_rejected_by_mongo` o anda kırılarak bunu hatırlatır.
- **İlgili:** [[feedback_live_verification_gap]] deseni — katmanlar arası sözleşme (burada BSON temsili) test kapsamı dışında.

### BL-031 — Havuz kimliği projeksiyonda yok; grup adı uydurma
- **Nedir:** Havuz sekmesinin tüm anlamı "bu iş hangi kuyrukta bekliyor" sorusudur, ama WC-1 projeksiyonu havuz kimliğini **hiç taşımıyor** — kalemde yalnız `assignmentMode: "groupQueue"` var, havuz pozisyonunun adı/id'si yok. Frontend bu boşluğu **sabit bir Türkçe metinle** dolduruyor: `mock-data.js:197` her groupQueue kalemine `group = 'Operasyon Kuyruğu'` yazıyor, `app.js:2245` de `'Atanmadı — Operasyon Kuyruğu'` metnini gömüyor.
- **Neden ciddi:** (a) ekranda **yanlış bilgi** var — CFO havuzundaki iş "Operasyon Kuyruğu" diye etiketleniyor; (b) birden fazla havuz varken (bugün CFO, Muhasebe Md, E2E Engineer) hepsi tek uydurma grupta çöküyor, yani kullanıcı işin hangi kuyrukta olduğunu **hiçbir şekilde** göremiyor; (c) sabit Türkçe metin l10n kuralını ihlal ediyor (resx'ten gelmiyor, 7 dil yok); (d) fixture-devri mantığının GERÇEK kalemlere uygulanması — `catalogVisible` hatasıyla aynı şekil (bkz [[feedback_live_verification_gap]]).
- **Gerekenler:** (a) sağlayıcı havuz kimliğini projekte etsin (pozisyon id + görüntü adı, kontrat etiketi olarak); (b) frontend grubu bu alandan alsın; (c) iki sabit metin kaldırılsın — alan yoksa grup **gösterilmesin**, uydurulmasın; (d) grup adı görüntü metni olduğu için çeviri gerektirmez, ama "gruplanmamış" durumunun etiketi resx'e 7 dil girsin.
- **Neden ertelendi:** Kontrat eklemesi gerektiriyor, yani WC-3 (pozisyon tabanlı atama seam'i) ile aynı yeri açıyor — iki kez açmak yerine WC-3 ile birlikte yapılmalı. **Ancak (c) maddesi — uydurma etiketin kaldırılması — beklemez:** yanlış etiket, etiketsizlikten kötüdür.
- **Tespit:** CT canlı doğrulaması, 2026-07-26 (CFO havuzuna iki gerçek kalem konduğunda ortaya çıktı).
- **YAPILDI (2026-07-30, `4ded9c82`) — WC-3 ile birlikte:** sözleşmeye `pool: { id, label }` eklendi; `label` **display** (pozisyon adı kullanıcının verisi), üç kural: `POOL_REQUIRED_FOR_GROUP_QUEUE` · `POOL_ON_NON_QUEUE_ITEM` · `POOL_LABEL_INVALID`. Sağlayıcı `TaskItem.PoolPositionId`'den etiketi **toplu** çözüyor (sayfa başına 2 okuma: pozisyonlar + birimler, kalem başına sorgu yok), biçim `"{pozisyon} — {birim}"`. Frontend grubu bu alandan alıyor; uydurma yok, alan yoksa grup gösterilmiyor. `GroupUnnamed` 7 dilde.
- **Sıra hayat kurtardı:** provider → frontend → **ancak sonra** kurallar. Kural önce konsaydı, alan gelene kadar **her havuz kalemi Havuz sekmesinden düşerdi** (`validateItems` doğrulayamadığını atıyor — BL-038'in dersi). Kural eklendiğinde 4 eski fixture testi kırıldı: sıra doğru olduğu için bunlar test düzeltmesiydi, kayıp görev değil.
- **Okunamayan pozisyon — seçilen üçüncü yol:** `pool` gönderilir, `id` dolu, `label` **null**. İki tuzağın ikisi de kurulmadı (GUID'i etiket yerine basmak · kalemi düşürmek). **Yarım isim de reddediliyor**: pozisyon okunup birimi okunamazsa etiket yine null — *"'CFO — ???' bilinmeyen bir yerdeki gerçek bir kuyruk gibi görünür"*. `label`'ın sözleşmede opsiyonel olmasının tek sebebi bu.
- **Arşivlenmiş pozisyon dahil:** atanabilir-pozisyon lookup'ı arşivlenmişi eler (*"nereye havuzlanabilir"*), bu sorgu elemez (*"nerede havuzlanmış"*) — arşivlenmiş bir kuyruktaki iş hâlâ adını hak ediyor.
- **CT CANLI DOĞRULAMA (2026-07-30):** `pool.label = {kind:'display', text:'CFO — Finans'}` telde · havuz olmayan **0** kalemde `pool` · ekranda **GUID yok** · grup seçicisi "Tüm gruplar / CFO — Finans" · Havuz sekmesi 3 kalem, sayaç tutuyor · kalem düşmedi. **Üç ayrı havuz üç ayrı etiket** vakası tek aktörle canlıda **ulaşılamıyor** (sağlayıcı havuz kalemlerini aktörün aktif pozisyonlarına göre süzüyor — doğru davranış); o vaka **testle kapsanmış**, canlı doğrulanmış değil — ayrım kayıtlı.
- **WC-3 seam'i zaten mevcuttu:** `ITaskAssignmentResolver` atama niyetini sözleşme üçlüsüne çeviriyor ve kendi doc'u gerekçesini taşıyor; **dokunulmadı** (`git diff` boş). WC-3 için yeni seam kurmaya gerek kalmadı.

### BL-032 — `priority`: sözleşmede bildirilmemiş alan (sözleşme değişikliği, implementasyon değil)
- **Nedir:** WorkCenterNext tablo görünümü **ÖNCELİK** kolonu basıyor ve fixture'lar `priority` taşıyor (`islerim-showcase-fixtures.js:59+`, değerler **küçük harf**: `'high'`, `'medium'`). Ama `priority` **`fixture-contract.js`'te hiç tanımlı değil** — `validateWorkItem` onu bilmiyor, `VALUE_TYPES`/enum listelerinde yok. Backend projeksiyonunda da yok (`WorkAggregation` özelliğinin tamamında geçmiyor). Sonuç: gerçek kalemde `undefined` → çip sınıfı `wcn-chip-undefined`, etiket `t(undefined)`; ekranda boş bayrak ikonu.
- **Neden implementasyon değil:** MOD-0024 `TaskPriority` enum'u **PascalCase** (`Low`/`Medium`/`High`). Sağlayıcı bunu olduğu gibi projekte ederse çip sınıfı `wcn-chip-High`, fixture'lar `wcn-chip-high` → bu oturumda üç kez yakalanan casing sınıfı hatanın aynısı, bu kez **sözleşmenin onayı olmadan**. Alan sözleşmeye girmeden projekte edilmemeli; sözleşme tek yetkili (DCP-004 kararı).
- **Gerekenler:** (a) `priority` sözleşmeye bildirilsin — değer kümesi + **casing kuralı** (tek doğru: sözleşme hangisini derse fixture'lar VE sağlayıcı ona uyar; bugün ikisi ayrık); (b) `validateWorkItem` alanı doğrulasın (bilinmeyen değer = hata); (c) sağlayıcı projekte etsin; (d) çip etiketleri 7 dil; (e) fixture'lar sözleşmenin casing'ine hizalanır.
- **Ara karar (uygulandı):** sözleşme değişene kadar ÖNCELİK kolonu gerçek kalemlerde **gösterilmez** — boş bayrak ikonu basmak, alanı hiç göstermemekten kötüdür ve test turunun yargısını bozar ("bu görevin önceliği yok mu?").
- **YAPILDI (2026-07-29, sahip kararı):** üç seviye, **PascalCase** kanonik (`Low`/`Medium`/`High`) — motor zaten bunu tutuyor ve iki yazma yüzeyi de bunu gönderiyor. Gerekçe: SLA motoru yokken (WC-2) daha fazla seviye sahte hassasiyet; "P1" tutamayacağımız bir müdahale sözü verir; üçten beşe çıkmak additive, beşten üçe inmek migrasyon. Gösterim ayrı tutuldu (TR ekranda Düşük/Orta/Yüksek). Yapılanlar: sözleşmede `PRIORITIES` + `PRIORITY_INVALID` doğrulaması; tüm fixture'lar ve iki gizli yazıcı (`app.js` toplantı/not → görev) PascalCase'e hizalandı; projeksiyon alanı taşıyor (opsiyonel — sıralamayan sağlayıcı hiçbir şey söylemez, `Medium` varsayılmaz); çip/kolon/filtre/sıralama geri geldi; motor↔sözleşme yazım eşitliği testle sabitlendi.
- **Neden ertelendi:** CT canlı doğrulaması + mock-dikiş denetiminde çıktı (2026-07-26). Sözleşme değişikliği ayrı ve onaylı dilim olmalı; implementasyon prompt'unun içine kaçak sokulmamalı.
- **İlgili:** `docs/workcenter-mock-seam-audit.md` bulgu #3 · [[feedback_live_verification_gap]] (casing sınıfı) · DCP-004 §12 DEC-9 (sözleşme tek yetkili).

### BL-033 — `app.js` test koşum düzeni yok: döngünün yapısal nedeni
- **Nedir:** `WorkCenterNext/app.js` **3576 satır** ve bu repoda test koşum düzeni **yok** (kodun kendi notu: *"app.js itself has no test coverage"*). Yardımcı modüller (`work-items-api.js`, `mock-data.js`, `fixture-contract.js`) test edilebiliyor; **render, aksiyon yönlendirme, guard'lar ve durum makinesi** edilemiyor.
- **Neden kaydedildi:** Bu oturumda kaçan defektlerin **tamamının** kanıtı canlı tarayıcıda bulundu, hiçbiri testte: MVC `action` parametresi · enum JSON · derlenmemiş view · l10n casing · `catalogVisible` eleme · uydurma kuyruk adı · mock kullanıcı unvanı · hayalet vekiller · donmuş bugün · `priority` boş çip. Ajanlar "yeşil build + yeşil test" raporluyor, CT canlıda defekt buluyor — **bu döngünün nedeni yetenek değil, ölçüm boşluğu.** Bugün render tarafı düzeltmelerinin (öncelik çipinin gizlenmesi, kapsam menüsünün daralması, `runBulk` guard'ı) otomatik kanıtı yok; hepsi CT'nin canlı doğrulamasına bağlı ve bu ölçeklenmiyor.
- **Gerekenler:** (a) app.js için DOM'lu bir test koşum düzeni (jsdom + gerçek `render()` çağrısı); (b) sözleşme fixture'larından beslenen render anlık-görüntü testleri (uydurma alan girerse kırılır); (c) guard testleri: gerçek kalem `applyTransition`'a **asla** girmez, gerçek kalemde uydurma alan **asla** basılmaz; (d) uzun vadede app.js'in bölünmesi — 3576 satır tek dosya, test edilebilirliğin de önündeki engel.
- **Neden ertelendi:** Test altyapısı kurmak, WorkCenter'ı bitirmekle aynı dilim değil; ama **her tur bir defektle geri döndüğümüz için** faizi ödenen bir borç. Kanban/Takvim'den (BL-015) ÖNCE ele alınmalı.
- **İlgili:** [[feedback_live_verification_gap]] · `docs/workcenter-mock-seam-audit.md` (yöntem sınırı bölümü).
- **YAPILDI (2026-07-30, `8e71a212`) — iki aşamada:** (1) detay yüzeyi için `bootDetailPage` (113 test, gerçek script sırası, ağ yalnız `fetchWorkItems` dikişinde sabit); (2) liste yüzeyi için `bootListPage` (22 test). İkisi **ortak** `tests/wcn-boot.js#bootSurface` üzerinden koşuyor — kopya değil, çünkü kopya olsa biri düzeltilip diğeri geride kalırdı; detayın 113 testi ortak yola taşındıktan sonra **aynen** geçiyor (davranış-koruyucu çıkarımın kanıtı). Liste harness'i `rootAttrs`'ı **boş** veriyor, çünkü production `Index.cshtml` `data-wcn-page` niteliğini hiç taşımıyor (`Details.cshtml` `="detail"` taşıyor) — uydurma bir `"list"` değeri gerçek sayfada olmayanı test etmek olurdu. **CT teyidi:** iki cshtml okundu, iddia doğru; 135/135 test yeşil.
- **Harness'in kendi bulduğu tuzak:** app.js durumu query string'e yazıyor (`syncUrl` → `replaceState`) ve boot'ta geri okuyor (`hydrateStateFromUrl`), dolayısıyla sekme değiştiren bir test arkasında `?tab=…` bırakıp **sonraki testi istemediği sekmede açıyordu** — izole koşuda görünmez, yalnız tam koşuda patlar. Harness artık URL'i sıfırlıyor. Bu sınıf (testler arası sızan durum) yeşil bir suite'in yalan söylemesinin en sessiz yolu.
- **Pinlenen kurallar:** aks yasası (sekme=sahiplik · segment=durum ≤3 · dördüncü segment/beşinci sekme bir testi düşürür) · sayaç tutarlılığı (sekme=segment toplamı, filtreler dahil) · sekme ayrımı (kabul edilmemiş→Gelen, sahipsiz havuz→Havuz, terminal→Geçmiş). Mutasyon karnesi: 12 mutasyon, hepsi ısırıyor. İki mutasyon ilk turda ısırmadı ve **ikisi de testin zayıflığıydı**, düzeltildi: çift-sayım guard'ı statik fixture'la ulaşılamıyordu (gerçek amacı `normalizedStatus: InProgress` + `taskLifecycle: Done` gibi **iki durum alanının çeliştiği** kalem — telden gelebilir bir şekil), ve Geçmiş salt-okunurluk testi **boştu** (fixture'ı `actions: []` taşıdığı için geçiyordu).
- **Kasten pinlenmeyen:** çip ekseni — aks yasasının çip yarısı için sekme/segment kadar net bir "en fazla N" kuralı yok ve uydurulmadı. Kanban/Takvim yalnız "patlamıyor" düzeyinde (BL-015, tasarım değişecek).

### BL-038 — Geçmiş'in salt-okunurluğunu yüzey uygulamıyor, yalnız sağlayıcı sağlıyor
- **Ölçüm (2026-07-30, BL-033 harness'iyle):** terminal bir kaleme **disabled** bir aksiyon verildiğinde liste o butonu **Geçmiş'te basıyor**. Sözleşme buna izin veriyor: `TERMINAL_STATE_CHANGING_ACTION` yalnız **`enabledInlineActions`**'ı reddediyor (`fixture-contract.js:126,302`), disabled olan geçiyor. `rowHtml`'de `terminal` değişkeni **yalnız pin butonunu** bastırıyor (`app.js:825`), aksiyon kümesini süzmüyor.
- **Bugün neden görünmüyor:** `TaskWorkItemProvider` terminal görev için boş aksiyon kümesi döndürüyor. Yani kuralı **motor** sağlıyor, yüzeyin kendi kuralı yok — bir sağlayıcı hatası kadar uzakta ve yüzey yakalamaz.
- **CT ilk kararı (SÜPERSEDE EDİLDİ):** sözleşmeyi sıkılaştırmak — `enabled inline` yerine `any inline`. Gerekçe doğruydu: "Geçmiş salt okunur" bir **aggregation** özelliği, tek yüzeyin değil; listede süzersek detay · kanban · takvim · her yeni yüzey aynı süzgeci ayrı taşır ve ayrışırlar. **Mekanizma yanlıştı.**
- **Neden değişti (CT ölçümü, aynı gün):** `work-items-api.js:56-62` — `validateItems` yalnız geçerli kalemleri `valid`'e alıyor, **gerisini sessizce düşürüyor**. Sözleşmeye konsaydı hatalı bir sağlayıcı o görevi Geçmiş'ten **kaybettirirdi**. Kaybolan görev, sızan kapalı butondan kötüdür — ve bu kod tabanı gerçek kalemleri bir kez zaten böyle kaybetti (`catalogVisible`).
- **YAPILDI (2026-07-30, `1f9047e1`):** kural **`getActions`**'ta (`mock-data.js`) — sözleşmeye **dokunulmadı** (`git diff` boş, teyitli). Terminal kalemde **inline** aksiyon dönmüyor, **`deeplink`** dönüyor (kapanmış bir görevin kaynak kaydını açmak meşru; `depth` zaten "burada eyler / başka yere gider" proxy'si). Derinlik çözüm sırası sözleşmeninkiyle birebir: `action.depth || item.actionDepth || 'inline'` — bunun için `actionForPresentation`'ın sessizce düşürdüğü `depth` alanı geçirildi. **Tek erişim zinciri** ilk kararın gerekçesini karşılıyor: `app.js:266 itemActions → data.getActions`, ve ona bağlı olan her şey kuralı bedava alıyor (satırlar 908/961, tablo kartı 1193, toplu çubuk 2487, birincil aksiyon 286/291, `actionByKey`/`actionByRole` 295-296, detay rayı). Kanban/Takvim bağlandığında süzgeci ayrıca yazmadan alacak — mekanizmanın seçilme sebebi buydu.
- **`isTerminal` tek tanıma indirildi:** `mock-data.js`'e taşındı ve dışa açıldı; `app.js:340` artık `data.isTerminal(item)` diyor, 7 çağrı yeri (sekme yönlendirme, satır, checklist salt-okunur, alt görev, composer ×2, seçim) aynı tanımı okuyor. Tek-tanım şartını bir mutasyon koruyor.
- **Mutasyon karnesi (141 test):** kural çıkarıldı → 2 · `deeplink` istisnası kaldırıldı → 2 · süzgeç ters çevrildi → 4 · `isTerminal` yalnız `normalizedStatus` okur → 1 · `depth` mapper'da düşürüldü → 2. İkinci satır sıfır değil, yani **yön ayrımının** gerçek testi var.
- **Kalem düşmüyor, ayrıca testli:** terminal + disabled inline aksiyonlu kalem Geçmiş listesinde **görünüyor**, yalnız butonu yok. Bu testin varlığı, biletin sözleşme yerine `getActions`'ı seçmesinin sebebidir.
- **İlgili:** BL-033 harness'i (kusuru buldu ve mevcut davranışı pinledi, sonra yeni kurala çevirdi) · DCP-004 §10.2 (actions[] projeksiyon kuralı) · `catalogVisible` regresyonu (aynı kayıp-kalem sınıfı).

### BL-039 — Toplu seçim yolu ölü: kaldırılacak mı, bağlanacak mı?
- **Ölçüm (2026-07-30, zincirin her halkası ayrı ayrı):** `bulkBar` **tanımlı, hiç çağrılmıyor** (`app.js:2484`, tek eşleşme tanımın kendisi) · `data-wcn-check` ve `data-wcn-check-all` **hiçbir markup'ta üretilmiyor**, yalnız handler'larda `closest()` ile okunuyor (4436, 4443) · dolayısıyla `state.tableSelected` kullanıcı tarafından doldurulamıyor, boşken `bulkBar` `''` dönüyor, `data-wcn-bulk` butonu hiç doğmuyor, `performBulk → runBulkWithProgress → runBulk` girilemiyor. **CT canlı gözlemi (0 checkbox, 0 toplu buton) ile birebir uyumlu.** BL-033 bunu üç testle pinledi; kod eklenmedi.
- **Neden şimdi düzeltilmiyor:** risk kapalı — `runBulk` içinde gerçek kalemi simüle etmek yerine başarısız sayan bir guard var, ve seçim bir gün bağlanırsa bu testler **önce** düşer, bağlayan kişi guard'ı yeniden okur.
- **Karar gerekiyor:** (a) ölü yolu **kaldır** (checkbox/bulkBar/runBulk zinciri) — canlı görünen ölü kod bir tuzaktır; (b) seçimi **bağla** ve toplu aksiyonu gerçek yap. **CT önerisi: (b)'yi UX turundan sonra değerlendir, (a)'yı şimdi yapma** — toplu aksiyon gerçek bir ihtiyaç (10 kalemi tek tek kabul etmek), ve zinciri silip sonra yeniden yazmak israf. Testler zinciri dondurdu, acele yok.
- **Yan bulgu (kusur değil):** `tabFor`'daki `if (['Done','Cancelled']…) return 'history'` **ölü mantık** — `inTab` (`app.js:341`) Geçmiş üyeliğini `isTerminal(item)`'dan karar veriyor, `item.tab`'dan değil, ve diğer sekmelerden `&& !isTerminal(item)` ile bağımsız olarak dışlıyor. Silinse davranış bitişik aynı kalır. Temizlik, düzeltme değil.

### BL-041 — SLA "yaklaşıyor" sınırı yarım gün kaydı (kabul edildi, kayıt için)
- **Nedir:** WC-2'de SLA hesabı istemciden sunucuya taşınırken **sınır vakası kaydı**. Eski istemci (`mock-data.js computeSla`) takvim günü sayıyordu: `diffDays = round((son_tarih_gunu - bugun_gunu)/1gun)`, `<= 2` ise `due-soon`. Yeni sunucu hesabı pencereyi `Add(deadline, -2)` ile **son tarih gününün sonundan** geri yürüyor.
- **CT ölçümü (2026-07-30, bugün = 30 Tem):** bugün son tarihli → ikisi de `due-soon` ✓ · **+2 gün (1 Ağu) → eski `due-soon`, yeni `on-track`** ✗ · +3 gün → ikisi de `on-track` ✓. Yani yalnız eşiğin adlandırdığı sınır kaydı; yaklaşık yarım günlük kayma.
- **Ajanın raporu bunu "eşik birebir korundu, kimsenin gördüğü sessizce değişmedi" diye kaydetmişti — ölçümde tutmadı.** Karar yanlış değil, **parite iddiası** yanlıştı.
- **KARAR: bırakılıyor.** Gerekçe: gerçek çalışma takvimi geldiğinde "gün başı" anlamını yitirir (Pazartesi 09:00 mı, Cuma 17:00 mı?), ve `Add` tabanlı tanım o dünyada tutarlı kalan tek tanımdır. Pariteyi kurmak, bugün doğru görünüp takvim gelince yeniden bozulacak bir tanımı sabitlerdi.
- **Etkisi:** sahibin test dokümanı ve beklentileri eski sınıra göre yazılmıştı; "+2 gün" vakası artık `due-soon` değil. Test turunda kusur sayılmamalı.
- **İlgili:** WC-2 (`be0cc190`) · `WorkItemSlaCalculator.DueSoonWithinWorkingDays` (yapılandırma, sözleşme değil).

### BL-040 — 🔴 PLATFORM GENELİ: her FluentValidation hatası sebep kodunu kaybediyor
- **Nedir:** `ValidationBehavior.TryCreateFailureResponse` (`Diten.Platform.Application/Contracts/Behaviors/ValidationBehavior.cs:56-59`) reflection ile **iki tipli** bir imza arıyor: `GetMethod("Fail", …, [typeof(IReadOnlyList<string>), typeof(int)])`. Gerçek imza **dört parametreli**: `Fail(IReadOnlyList<string> errors, int statusCode = 400, string? reasonCode = null, string? correlationId = null)`. Opsiyonel parametreler `GetMethod`'un tip-dizisi eşleşmesini sağlamaz, dolayısıyla `failMethod` **her zaman null** → satır 41 `throw new ValidationException(failures)` → `GlobalExceptionHandler` bunu `ValidationProblemDetails`'e çeviriyor: **400 doğru, ama `reason_code` yok.**
- **Kapsam:** MediatR pipeline'ındaki her komut, her modül. Yalnız MOD-0024 değil.
- **Neden ciddi:** sebep-kodu köprüsü ([[project_password_error_code_bridge]]) headless API doğrulama mesajlarını **stabil kodlarla** frontend resx'ine (7 dil) bağlıyor. FluentValidation'dan gelen hiçbir hata kod taşımadığı için **çevrilemez İngilizce metin** olarak kullanıcıya ulaşıyor. Yani l10n kapıları geçiyor ama ekranda İngilizce cümle çıkıyor — kapının göremediği bir sınıf.
- **CT CANLI KANITI (2026-07-30), iki ölçüm yan yana:** onay yöneticisi kuralı **validator'da** → `400 · {"detail":"An approval manager is required when approval is requested."}` — kod yok, İngilizce. İnceleyen kuralı **handler'da** → `400 · REVIEW_REVIEWER_REQUIRED` — kod var, çevrilebilir.
- **Nasıl bulundu:** Faz 3b'de ajan inceleyen kuralını önce `CreateTaskItemValidator`'a koydu, testi 400 yerine `ValidationException` gösterdi, sebebini ölçtü ve kuralı handler'a taşıdı. Yani **bugün "kural handler'da olsun" demek bir tercih değil, bu hatanın dayattığı şey.**
- **Neden hemen düzeltilmiyor:** düzeltme **her modülün doğrulama hatası şeklini** değiştirir (throw → tipli `Response`), yani her modülün hata sözleşmesi ve testleri etkilenir. Kendi dilimi + kendi regresyon turu olmalı; WorkCenter dilimine sıkıştırılamaz.
- **Düzeltilince yapılacak temizlik:** handler'a taşınmış kurallar validator'a geri alınabilir mi diye gözden geçirilmeli — ama **acele edilmemeli**: handler'daki kural yazma yolunu koruyor, validator'daki yalnız girdi şeklini.
- **İlgili:** `TaskReviewRules.cs` (handler'a konmuş kural, gerekçesi `CreateTaskItemValidator` içinde yorum olarak yazılı) · [[project_password_error_code_bridge]] · [[feedback_tenant_l10n_seven_langs]].

### BL-034 — MOD-0024 yalnız "mutlu yol"u uyguluyor: 12 tasarlanmış aksiyon yok
- **Nedir:** Frontend-first turunda (mock + fixture'lar) **25 aksiyon kodu** tasarlanmıştı; motor **7** üretiyor (`accept · claim · release · plan · start · complete · cancel`) ve bunlardan `cancel` tasarımda **hiç yoktu** (motorun kendi eklemesi). Yani tasarlanan 25'ten yalnız **6'sı** hayata geçmiş. Eksik olanların hepsi ortak bir temaya sahip: **işin yolunda gitmediği durumlar.**
- **Doğrulanmış sonuç — iki ölü durum:** `TasksController` yalnız 4 hedefe geçiyor (`Planned`/`InProgress`/`Done`/`Cancelled`). `TaskLifecycle.Waiting` ve `TaskLifecycle.PendingReview` tanımlı ve `TaskLifecycleService` geçiş matrisi onlara izin veriyor (Open→Waiting, InProgress→Waiting, InProgress→PendingReview), ama **hiçbir uç nokta onları hedeflemiyor**. Sonuç: İşlerim'in **Bekleyen** segmenti yalnız onay-bekleme ile dolabiliyor; kullanıcının "bilgi bekliyorum / bloke oldum" diyebileceği yol yok.
- **Eksik aksiyonlar (MOD-0024 kapsamı, 12):**
  - *Reddetme / devretme:* `decline` · `reject` · `return` (iade et) · `dispute` · `reassign` (fixture'larda **9 kullanım**) · `delegate` → **atanan iş reddedilemiyor, başkasına verilemiyor.** Tek çıkış `cancel`, o da "iş tamamen iptal" demek — anlamı zıt. Üstelik `cancel` Gelen Kutusu'nda AÇIK: alıcı, talep edenin işini iptal edebiliyor (SAP/ServiceNow ayrımı: *iade* alıcının, *iptal* talep edenin hakkı).
  - *Yürütme kontrolü:* `pause` · `resume` · `replan` · `logTime` → sözleşmede `executionState: paused` ve `timerState: paused` **tanımlı**, hiçbir aksiyon oraya ulaşmıyor.
  - *Bilgi/bekleme:* `requestInfo` (fixture'larda **9 kullanım — en sık aksiyon**) · `inquire` → `Waiting` durumunu dolduracak olan bunlar.
- **Kapsam dışı olması DOĞRU olanlar:** `approve`/`signoff`/`resolve` MOD-0023'ün kararıdır (charter Binding A — MOD-0024 asla ikinci onay motoru yazmaz). `scheduleReviewMeeting`/`acceptMeeting`/`declineMeeting` Faz 3b + BL-026. `acceptOffer` — `offered` atama modu hiç kurulmadı.
- **Neden ertelendi:** Faz 1-3 planı bilinçli olarak mutlu yolu hedefledi; eksikler plana yazılmamıştı, CT'nin mock↔gerçek karşılaştırmasında çıktı (2026-07-26). Ürün açısından kritik olan **reddetme** ve **requestInfo** — ikisi de günlük kullanımda kaçınılmaz.
- **Sıra önerisi:** (1) `decline`/`return` + `cancel`'ın Gelen Kutusu'ndan kaldırılması — yetki semantiği yanlış; (2) `requestInfo`/`inquire` → `Waiting` canlanır; (3) `reassign`/`delegate`; (4) `pause`/`resume`/`logTime` (timeTracking capability ile birlikte).
- **İlgili:** `fixtures/*.js` (`action('...')` sözlüğü) · `app.js applyTransition` · `TaskWorkItemProvider` (7 kod) · `TasksController` (4 hedef) · [[project_mod0024_approval_boundary]].
- **Madde 7 (yorumlar) YAPILDI (2026-07-29):** `TaskComment` **ayrı koleksiyonda** (gömülü dizi her görev okumasını ağırlaştırır ve full-replace bir güncelleme onları siler); `POST /api/v1/tasks/{id}/comments` + Diten.Web proxy rotası; **PUT/DELETE yok** — yorum değişmez, silme hiç eklenmeyecek, gerekirse "geri çekildi" işareti gelir. Yazma yetkisi `platform.tasks.read` (yeni izin açılmadı): yorum bir geçiş değil, çoğu zaman atanan olmayan birinin sorusu. Kapanmış görev **409 + `TASK_COMMENT_TASK_CLOSED`** ile reddediyor (composer zaten gizliydi — gizleme sunum, red kural); okuma açık kalıyor. Metin 1-2000 karakter, `TASK_COMMENT_TEXT_INVALID`. Sağlayıcı `activity` capability'sini **koşulsuz** bildiriyor ve konteyneri hep gönderiyor (bildirilmiş-ve-boş geçerli durum). Akış **yalnız yorum**: yaşam döngüsü olay günlüğü yok ve türetilmedi — dört zaman damgasından çıkarılan bir zaman çizelgesi accept/plan/claim/release/inquire'ı sessizce atlar. Sıra yeniden eskiye, eşitlikte id'ye düşen kararlı tie-break, **bellekte** (BL-030: iki `DateTimeOffset` anahtarlı sunucu sıralaması paralel-dizi hatası verir) ve gerçek MongoDB'ye karşı testli. `at` **mutlak**; sunucu "N gün önce" göndermiyor ve istemci render anında hesaplıyor — önceden hesaplanmış gün sayısı sekme açık kaldıkça yalan söyler. `ago` alanının DTO'ya geri eklenmesi bir reflection testiyle **imkânsız** kılındı.
- **CT CANLI DOĞRULAMA (2026-07-29, `eda716bd`):** yorum ekleme **201** · boş / yalnız-boşluk / 5000 karakter → **400 `TASK_COMMENT_TEXT_INVALID`** · `activity` capability bildiriliyor · sıra **yeniden eskiye** (ms'lik damgalarla doğrulandı) · sunucu `ago` **göndermiyor**, `at` mutlak ISO · kapanmış göreve yorum **409 `TASK_COMMENT_TASK_CLOSED`**, mevcut yorumlar **okunmaya devam ediyor** · tarayıcıda **gerçek tıklamayla** composer çalışıyor: yorum listeye düştü, input temizlendi, boş-durum kalktı, "Bugün" istemcide hesaplandı. Kapanış: DOĞRULANDI.
- **🟡 CT'nin bulduğu kusur — composer placeholder yalan söylüyordu → DÜZELTİLDİ (`62a0a171`):** yedi dilde de *"Yorum yaz… (kaynağa da yazılır)"* diyordu; MOD-0024 kendi görevlerinin **kaynağıdır**, iletilecek başka yer yok. Vaadin **tek anahtarda** (`CommentPlaceholder`) olduğu, komşu anahtarlar (`ActivityLabel`, `CommentPost`, `CommentTextRequired`, `CommentTooLong`, `CommentAuthorUnknown`, `ActivityEmpty`) taranarak kanıtlandı. **CT canlı doğrulama:** `tr` → "Yorum yaz…", `ar` → "اكتب تعليقًا…" (RTL yerleşim doğru, buton "نشر"); vaat hiçbir dilde kalmadı.
- **🔴 Yapısal açık — resx DEĞERLERİ hiçbir testle korunmuyor:** 7-dil guard'ları anahtarın **varlığını** doğruluyor, **içeriğini** doğrulamıyor; jsdom harness'i `t(key) => key` ile anahtarı yansıtıyor, resx değerini hiç görmüyor. Bu kusur tam o boşluktan geçti: anahtar yedi dilde vardı, hepsi çeviriliydi, hepsi yanlış şeyi söylüyordu. Metin doğruluğunu testle kapatmak pahalı (her cümlenin anlamını iddia etmek gerekir), o yüzden **bilinçli kabul edilmiş boşluk** olarak kayda geçiyor: **kullanıcıya görünen metin değişiklikleri canlı doğrulamayla kontrol edilir, testle değil.** Bkz. [[feedback_live_verification_gap]].
- **Bildirim/@bahsetme yapılmadı:** bildirim kanalı yok (WC-4); haber verilmeyen bir bahsetme tutulmayan bir sözdür.

### BL-035 — Alt görev üst görevi bloklamalı + alt görev oluşturmanın tam formu
- **Karar (sahip, 2026-07-28):** açık alt görev varken üst görev **tamamlanamaz**. **İptal edilen alt görev saymaz** — yoksa gereksizleşen bir alt görev üst görevi süresiz kilitler. Bugün hiç bloklamıyor.
- **İki kavram, karıştırılmamalı** (sahip bunu açıkça vurguladı): **checklist** = tek görevin *içinde* işaret kutuları, sahibi/tarihi/yaşam döngüsü YOK; **alt görev** = kendi atananı, tarihi, yaşam döngüsü ve detay sayfası olan **gerçek bir görev**. İkisi de tamamlamayı bloklar ama farklı mekanizmayla; biri diğerinin yerine geçmez.
- **Gerekçe:** *"iş üçe bölündü, ikisi yapılmadı, ama bütünü tamamlandı"* tutarsız bir cümle. Mockup da bu tarafı almış (*"2 subtasks still open — Prevents completion"*). CT'nin test dokümanına yazdığı eski "alt görev bloklamaz" kuralı bu kararla **düzeltildi**.
- **Uluslararası durum:** Jira/Asana bloklamaz (uyarır), ServiceNow genelde engeller, MS Project'te üst görevin durumu alt görevlerden **türetilir**. Ayrım "alt görev işin parçalara ayrılması mı, yardımcı liste mi" sorusundan çıkıyor; burada **parçalara ayrılma** kabul edildi.
- **YAPILDI (2026-07-29):** (a) `complete` açık alt görev varken **409 + `SUBTASK_BLOCKED`** ile reddediliyor, kod istemci köprüsünde + 7 dil; (b) iptal edilen alt görev saymıyor; (c) sağlayıcı her açık alt görev için **ayrı bir blocker** üretiyor (toplu değil — kimlik kaybolmasın), `complete` görünür ama kapalı geliyor. Kural `TaskBlockingRules`'ta, bağımlılıkla aynı yerde (sınıf `TaskDependencyRules`'tan yeniden adlandırıldı, adı içeriğinden dar kalmıştı). Blocker sırası sözleşmesel: **önce bağımlılık, sonra alt görev** — buton sebebi ilk blocker'dan alınıyor ve handler de aynı sırada kontrol ediyor, yoksa ekran ile 409 farklı engeli suçlardı. `start` ve `cancel` bloklanmıyor; cancel bloklansaydı açık çocuğu olan istenmeyen bir görev hiç iptal edilemezdi. **Ters çevrilen karar:** eski kod "açık alt görev asla bloklamaz, iki mekanizma 'neden bitiremiyorum'u cevapsız bırakır" diyordu; itirazın cevabı artık `blockedState.blockers[]` (o yorum yazıldığında yoktu). Uluslararası uygulama bölünmüş: Jira/Asana uyarır, ServiceNow engeller, MS Project üst görevi çocuklardan türetir — burada **engelleme** seçildi.
- **Gerekenler:** ~~(a)(b)(c)(d) — hepsi yapıldı.~~ (d) "detaylı ekle" bu turdan önce tamamlanmıştı (`subtaskCreatePanel`, başkasına atama dahil).
- **Zaten doğru olan, değiştirilmeyecek:** alt görevi **tamamlamak** atananın, **iptal etmek** oluşturanın hakkı — genel kural alt görevi de kapsıyor, yeni kural gerekmiyor.
- **Sıra sözleşmesel:** hem projeksiyon hem handler **önce bağımlılık, sonra alt görev**. Ters olsaydı ekran alt görevi, 409 ise öncülü suçlardı. Mutasyon testinde sıra ters çevrilince 1 test düşüyor — yani sıra davranış olarak sabit, kozmetik değil. Handler kuralı boşaltılınca 4 test düşüyor (BL-028'de bu sayı sıfırdı ve kural yoktu).
- **CT CANLI DOĞRULAMA (2026-07-29):** açık alt görev → `complete` **409 `SUBTASK_BLOCKED`**, `start` **204** (yön ayrımı) · çocuk Done → 204 · **iptal edilmiş çocuk bloklamıyor** → 204 · karışık (biri Done biri açık) → 409 · bağımlılık+alt görev birlikteyken hem ekran hem handler **`DEPENDENCY_BLOCKED`**, öncül kapanınca ikisi birden **`SUBTASK_BLOCKED`**'a düşüyor · ekranda çevrili cümle ("*"CTB cocuk-hala-acik" alt görevi kapanmadan tamamlanamaz*"), ham anahtar yok, Tamamla **görünür + disabled**. Kapanış: DOĞRULANDI.
- **Kasten kapatılmadı:** blocker `code` dağarcığı **açık liste** — WorkCenter çok sağlayıcılı, her sağlayıcı kendi engelini adlandırır (`VALIDATION_BLOCKED` gibi); kapalı liste WorkCenter'ı her yeni modülde değiştirmeye zorlardı. Aksiyon kodları ve bağımlılık tipleri kapalı kalır (onlar MOD-0024'ün kendi motoru), blocker sebebi kalmaz.

### BL-036 — Bilgi talebi: kimi beklediğini seçebilme (orta yol) ve tam soru-cevap sistemi
- **Bugün:** `inquire` tek serbest metin gerekçe alıyor; alıcı yok, cevap yok, `Devam et` her zaman açık.
- **Mockup'ın istediği (tam sistem):** aynı görevde **birden çok** talep · her biri **belirli bir kişiye** · **zorunlu/isteğe bağlı** · cevaplandı/cevaplanmadı takibi · ve **zorunlu talepler cevaplanmadan `Devam et` kapalı**. Ekranda *"2 requests pending, 2 required before resuming"* ve *"Waiting on Mert Demir; Waiting on Zeynep Arslan"*.
- **Sahip kararı (2026-07-28):** şimdilik **orta yol** — beklenen kişinin seçilebilmesi (tipli kimlik, `waitingContext.waitingOn` alanı bunun için zaten ayrılmış ve bugün boş gönderiliyor). Tam soru-cevap sistemi **ertelendi**; iş süreçleri gerektirirse ileride değerlendirilecek.
- **Neden ertelendi:** tam hali yeni bir veri yapısı (talep koleksiyonu), kişi ataması, cevap akışı ve devam etmeyi kapılayan bir kural demek — kendi dilimi. Orta yol ise mevcut alana veri koymak.

### BL-037 — "Kaynak modülde oluştur" hiçbir şey yapmıyor: kalsın mı, kalkacak mı?
- **Bugün (canlı ölçüm 2026-07-30):** `+ Yeni → Kaynak modülde oluştur` bir modül seçtiriyor, sonra yalnız *"{modül} modülünde oluşturma açılacaktı (mock)"* toast'ı basıyor. Hiçbir sekme açılmıyor, hiçbir şey oluşmuyor. Toast dürüst — "(mock)" diyor — ama akış kullanıcıya üç tıklama harcatıp hiçbir sonuç vermiyor.
- **Nasıl bu hâle geldi:** eskiden seçilen modülden **rastgele mevcut bir kaydın** detay sayfasını açıyordu; kullanıcı "oluştur" derken alakasız bir kayıt göstermek yanlış eylemdi ve `39a0819f`'te kaldırıldı (kaldırma gerekçesi kodda blok yorum olarak duruyor, "geri koyma" uyarısıyla). Kaldırıldıktan sonra akışın hiçbir işi kalmadı.
- **Neden kurulamıyor:** başka modülde oluşturmak o modülün **create URL**'ini gerektirir; kanonik projeksiyon yalnız `deepLink` taşıyor ve o **mevcut bir nesneyi** adresliyor. Kontrata `createLink` benzeri bir alan eklemek WC-1'i genişletmek demek — her sağlayıcının doldurması gereken yeni bir zorunluluk.
- **Karar gerekiyor:** (a) menü kalemini **kaldır** — WorkCenter'ın işi işi *yürütmek*, başka modülde kayıt açmak değil; kullanıcı o modüle sol menüden gider; (b) kontrata `createLink` ekle ve gerçekten çalıştır; (c) showcase olarak bırak. **CT önerisi: (a).** Aggregator'ın "ASLA" listesi kaynak-tanımlama işlerini dışarıda tutuyor; oluşturma tam olarak kaynak-tanımlama. (c) ise kullanıcıya boş yol gösterir.
- **İlgili:** DCP-004 §5 (aggregator ASLA listesi) · WC-1 kontrat kapsamı · `app.js openCreateInSource`.

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
