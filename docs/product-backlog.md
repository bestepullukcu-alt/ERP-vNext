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
- **Nedir:** WorkCenter/Görev Merkezi'nin tenant modül katalogunda görünmesi, navigasyona düşmesi, izninin (`platform.work-aggregation.inbox.view`) tanımlanıp seed edilmesi ve tenant'a atanabilmesi (entitlement) için bir `WorkAggregation` **manifest provider'ı** gerekir — mevcut 6 tenant modülü (Organization/Workflow/ReferenceData/DocumentManagement/AccessGovernance/TenantSettings) gibi. ~~Bugün WorkCenter'ın manifest'i **YOK**.~~ **DÜZELTME (2026-07-31):** manifest **VAR** ve DI'a kayıtlı — `WorkAggregationManifestProvider` (`work-aggregation`, `/WorkCenterNext`, `IsTenantAssignable: true`), commit `ee0dbb50`. Kayıt kodun gerisinde kalmıştı. **Kalan açık:** manifest'in beyan ettiği izinlerin tenant scope-zehirlenmesine karşı kontrol edildiğine dair kanıt bulunamadı — o kısım hâlâ doğrulanmadı.

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
- **Nedir:** `WorkCenterNext/app.js` **büyük tek dosya** (`wc -l frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js` — 2026-07-31'de 4655) ve bu repoda test koşum düzeni **yok** (kodun kendi notu: *"app.js itself has no test coverage"*). Yardımcı modüller (`work-items-api.js`, `mock-data.js`, `fixture-contract.js`) test edilebiliyor; **render, aksiyon yönlendirme, guard'lar ve durum makinesi** edilemiyor.
- **Neden kaydedildi:** Bu oturumda kaçan defektlerin **tamamının** kanıtı canlı tarayıcıda bulundu, hiçbiri testte: MVC `action` parametresi · enum JSON · derlenmemiş view · l10n casing · `catalogVisible` eleme · uydurma kuyruk adı · mock kullanıcı unvanı · hayalet vekiller · donmuş bugün · `priority` boş çip. Ajanlar "yeşil build + yeşil test" raporluyor, CT canlıda defekt buluyor — **bu döngünün nedeni yetenek değil, ölçüm boşluğu.** Bugün render tarafı düzeltmelerinin (öncelik çipinin gizlenmesi, kapsam menüsünün daralması, `runBulk` guard'ı) otomatik kanıtı yok; hepsi CT'nin canlı doğrulamasına bağlı ve bu ölçeklenmiyor.
- **Gerekenler:** (a) app.js için DOM'lu bir test koşum düzeni (jsdom + gerçek `render()` çağrısı); (b) sözleşme fixture'larından beslenen render anlık-görüntü testleri (uydurma alan girerse kırılır); (c) guard testleri: gerçek kalem `applyTransition`'a **asla** girmez, gerçek kalemde uydurma alan **asla** basılmaz; (d) uzun vadede app.js'in bölünmesi — tek dosyada binlerce satır, test edilebilirliğin de önündeki engel.
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
- **Ölçüm (2026-07-30, zincirin her halkası ayrı ayrı):** `bulkBar` **tanımlı, hiç çağrılmıyor** (`grep -n 'bulkBar' app.js` — tek eşleşme tanımın kendisi) · `data-wcn-check` ve `data-wcn-check-all` **hiçbir markup'ta üretilmiyor**, yalnız handler'larda `closest()` ile okunuyor (`grep -n 'data-wcn-check' app.js`) · dolayısıyla `state.tableSelected` kullanıcı tarafından doldurulamıyor, boşken `bulkBar` `''` dönüyor, `data-wcn-bulk` butonu hiç doğmuyor, `performBulk → runBulkWithProgress → runBulk` girilemiyor. **CT canlı gözlemi (0 checkbox, 0 toplu buton) ile birebir uyumlu.** BL-033 bunu üç testle pinledi; kod eklenmedi.
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

#### ⚠️ KAPANIŞ (KISMİ) — BL-040 (altyapı) · BL-048 — 2026-08-10 — **CANLI DOĞRULAMA BEKLİYOR**

**① ÖNCE KAPSAM ÖLÇÜMÜ (karar bundan sonra verildi)**

| ölçüm | sonuç |
|---|---|
| Validator sınıfı | Platform **150** sınıf / **126** dosya · Auth 7 · Mdm 2 · DevEnablement 4 |
| `ValidationBehavior` kopyası | **5** servis |
| Kusurun kapsamı | **YALNIZ Platform.** Mdm/DevEnablement'ın `Response<T>.Fail`'i **iki** parametreli, yani onların reflection'ı **eşleşiyor**; Auth/Hcm zaten reflection kullanmıyor. Kusur, Platform'un `Fail`'i `reasonCode`+`correlationId` ile büyürken reflection'ın ikide kalmasından doğdu. |
| Bugünkü 400 şekli | iki tane: validator yolu → `ValidationProblemDetails` (**kodsuz**) · handler yolu → `Response<T>` (**kodlu**) |
| Şekle bağlı istemci | `problem.detail` okuyan **6 dosya** (personalization-client · login · reset-password · Administrators · AuditLog · reference-data.api) |
| Şekle bağlı test | `TaskReviewerRequiredHttpTests` — kusuru **kendi XML yorumunda tarif ediyor** |

**② KARAR — reflection düzeltilmedi, KALDIRILDI.** Gerekçe iki katmanlı:

1. **Kusur tip dizisi değildi, sessiz null'du.** İmzayı düzeltmek aynı arıza modunu yaşatırdı: `Fail`'e eklenecek bir sonraki parametre eşleşmeyi yine aynı sessizlikle bozardı. Artık davranışın tek çıkışı var — `throw` — ve sessizce yanlış yapabilecek bir arama **yok**.
2. **Tipli yol zaten oradaydı.** `ValidationFailure` `PropertyName` ve `ErrorCode`'u kendisi taşıyor; behavior'ın `Response<T>` üretmesine hiç gerek yok. Kaldırmak pipeline'dan **hiçbir bilgi eksiltmedi** — reflection dört aydır bir kez bile başarılı olmamıştı, yani kaldırma Platform davranışında **birebir no-op**.
3. **Şekli değiştirmek ölçülen bir regresyon olurdu.** "Reflection'ı çalıştır" seçeneği gövdeyi `ValidationProblemDetails` → `Response<T>` yapardı ve `detail` okuyan **6 dosyanın altısı da** mesajını kaybederdi. Düzeltme bu yüzden **eklemeli**: gövde aynen duruyor, üstüne bir uzantı geliyor.

**③ KOD NEREDEN GELİYOR — türetilmiş, gerekçesiyle.** `ValidationReasonCode.From(failure)`:
- **Küratörlü kod aynen geçer:** `.WithErrorCode("REVIEW_REVIEWER_REQUIRED")` → o dize, ön ek yok. Handler'dan validator'a taşınan bir kural aynı kodu vermeye devam etsin diye.
- **Yoksa ALAN + KURAL'dan türetilir:** `Request.Title` + `MaximumLengthValidator` → `VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH`.
- **Neden türetilmiş:** el yazımı kod şartı, 150 validator düzenlenene kadar **hiçbir** hatanın kod taşımaması demekti — platform geneli kusur en son modüle kadar açık kalırdı. Türetme, bugün var olan her validator'ı **düzenlemeden** kodlu hâle getiriyor.
- **STABİLİTE ÖLÇÜTÜ karşılanıyor:** kod **metinden hiç beslenmiyor**. Mesaj değişince kod değişmez (test bunu doğrudan iddia ediyor). Alan adı ya da kural değişirse kod değişir — o zaten **başka bir hatadır**.

**④ BL-048 — ÖLÇÜLDÜ, KAPANIYOR.** Zincir uçtan uca izlendi:
`RuleFor(x => x.Request.Title).MaximumLength(200)` → `reason_code: VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH` → `TasksApi` `payload.reason_code`'u okuyor → `failureMessage` **yalnız** koda karşılık gelen metni gösteriyor, sunucunun ham `errors` metnini **hiç** göstermiyor. Yani *"'Request Title', 200 karakterden…"* cümlesi bu yüzeylerde okuyucuya **ulaşamaz**. Eksik olan tek halka eşlemeydi: iki kod (`_NOT_EMPTY`, `_MAXIMUM_LENGTH`) köprüye ve **7 dile** eklendi. **Eşlenmemiş kod hâlâ genel mesaja düşüyor ve konsola kodu yazıyor** — tasarlanan "asla sessiz değil" yolu, ve bir sonraki eşlemeyi kimin yapacağını söyleyen şey bu.

**⑤ ÖLÇÜM SIRASINDA BULUNAN AYRI KUSUR — `errors` haritası tele hiç çıkmıyor.**
`GlobalExceptionHandler` `ValidationProblemDetails`'i alan-bazlı sözlükle kuruyor, ama `switch` sonucu `ProblemDetails` olarak tipleniyor ve `WriteAsJsonAsync` **statik tipe göre** serileştiriyor → türetilmiş tipin `Errors` özelliği düşüyor. (`reason_code` sağ kalıyor, çünkü `Extensions` taban tipte `[JsonExtensionData]`.) Yani bu platformun gönderdiği **her** doğrulama 400'ü `title/status/detail` taşıyor, alan bazlı hiçbir şey taşımıyor. **Bu turda DÜZELTİLMEDİ** — paylaşılan hata yolunun serileştirmesini değiştirmek kendi turunu ve kendi regresyon ölçümünü hak ediyor. Bugünkü gerçek bir testle **sabitlendi**, bir sonraki okuyan tarayıcıdan değil düşen bir iddiadan öğrensin diye.

**KIRMIZI kanıtı (bu kusur özel dikkat istedi):**
Bugüne kadar hiçbir test yakalamamıştı çünkü hepsi *"400 döndü mü"* diye soruyordu — hep dönüyordu. Yeni dosyadaki **her iddia kodun varlığı üzerine**; 400 iddiası bugün de yeşil olurdu ve hiçbir şey kanıtlamazdı.
- Düzeltmeden önce: **9 test düştü / 2 geçti.** Geçen 2'si kasıtlı non-vacuity (geçerli komut handler'a ulaşıyor) ve o an **boş geçen** stabilite iddiası — düzeltmeden sonra anlamlı hâle geldi.
- İstemci köprüsü **mutasyonla** ölçüldü: iki eşleme silinince **3 test** düşüyor; `ru.resx`'ten tek anahtar silinince **1 test** düşüyor.
- Reflection'ın gerçekten gittiği, yorumları ayıklanmış **kaynak taramasıyla** iddia ediliyor (yorum, kaldırılan şeyi adıyla anlatıyor — ham dosya taransa kendi açıklamasına takılırdı).

**REGRESYON — ölçüldü, sıfır.** Yalnız **kendi satırlarım** geri alınıp (başka oturumun devam eden işi yerinde bırakılarak) tam paket koşuldu:
| | dosya | test |
|---|---|---|
| benim satırlarım **yokken** | 10 düşen | 23 düşen |
| benim satırlarım **varken** | 10 düşen | 23 düşen |

Yani BL-040'a atfedilebilir **tek bir** düşen test yok. Platform paketi düzeltmenin hemen ardından **2080/2080** yeşildi.

**⚠ ÇALIŞMA AĞACI EŞ ZAMANLI YAZILIYOR.** Ölçüm sırasında başka bir oturum aynı ağaçta MOD-0024 "configurable fields" işini sürdürüyordu: `Tasks/form.js`, `Tasks/form-page.js`, `TaskFieldDefinitionQueryHandlers.cs`, yeni `tasks-custom-fields.test.js` ve `Tasks/api.js`'in bir bölümü **benim değil**. Düşen 10 dosyanın hepsi ya eski enterprise-strategy hataları ya da o devam eden iştir (ör. `errorFieldValueInvalid` köprüye eklenmiş ama resx'e girmemiş; `form.js` `<option>` değerini hiçbir DTO'nun bildirmediği `value`'dan kuruyor). Turun sonunda `Diten.Platform.Application` **onların** dosyasındaki eksik `using` yüzünden derlenmiyordu — **benim commit'imde o dosya yok**, ama bu yüzden C# paketi tur sonunda yeniden koşulamadı.

**BU TURDA YAPILMAYANLAR — açıkça:**
- **Modül modül geçiş yapılmadı.** Bilinçli: ölçüm 150 validator gösterdi, talimat *"ölçüm büyükse yalnız altyapıyı düzelt"* diyordu. Bugün her validator kod **üretiyor**; frontend'de eşlenmiş olan yalnız **iki** tanesi. Kalan eşleme modül modül, ayrı madde.
- **Alan bazlı kodlar gönderilmiyor.** `reason_code` tekil ve **ilk** hatanın kodu (`detail` ile aynı sıra — ikisi farklı seçilse ekran, metnin adlandırdığından başka bir alan hakkında çeviri gösterirdi). Üç alanı birden işaretleyen bir form için alan bazlı harita gerekir; spekülatif olduğu için eklenmedi.
- **Diğer 4 servisin behavior'ına dokunulmadı** — ölçüm kusurun onlarda olmadığını gösterdi.
- **Frontend yarısı (BL-048) COMMIT EDİLMEDİ.** `Tasks/api.js`, `_IndexL10n.cshtml` ve 7 resx dosyasının **üçü de** başka oturumun yarım işini taşıyor; onları commit etmek, testleri şu anda düşen bir işi benim dilimime karıştırırdı. Değişiklikler çalışma ağacında duruyor ve yukarıdaki testle yeşil.

**CANLI DOĞRULAMA ADIMLARI (CT):**
1. **Kod geliyor mu** — 224 karakterlik başlıkla görev oluştur → yanıt **400** ve gövdede
   `"reason_code": "VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH"` olmalı. (Eskiden bu alan **hiç yoktu**.)
2. **Boş başlık** → `reason_code: VALIDATION_REQUEST_TITLE_NOT_EMPTY`.
3. **Ekranda çeviri** — aynı iki denemeyi **Türkçe arayüzden** yap: *"Başlık en fazla 200 karakter olabilir."* ve *"Başlık girin."* görünmeli; *"Request Title"* **hiçbir yerde** görünmemeli. **(BL-048'in kapanış ölçümü budur.)**
4. **Eşlenmemiş kod** — açıklama alanını 4000 karakter yap → genel hata mesajı + konsolda
   `[TasksApi] no message key for reason code "VALIDATION_REQUEST_DESCRIPTION_MAXIMUM_LENGTH"`.
5. **Küratörlü kod bozulmadı** — inceleyen seçmeden inceleme isteyen görev oluştur → hâlâ `REVIEW_REVIEWER_REQUIRED` (handler yolu, ön ek almamalı).
6. **Şekil korundu** — 1. adımın gövdesinde `title`, `detail`, `status` **duruyor** olmalı; `errors` **yok** (⑤'teki ayrı kusur).
7. **Diğer modüller** — bir platform yönetim ekranında doğrulama hatası tetikle; mesajın hâlâ göründüğünü doğrula (`detail` okuyan 6 dosya).

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "GetMethod|System.Reflection" services/Diten.Platform/src/Diten.Platform.Application/Contracts/Behaviors/ValidationBehavior.cs   # BOŞ olmalı
rg -n "reason_code" services/Diten.Platform/src/Diten.Platform.API/Middleware/GlobalExceptionHandler.cs
rg -n "VALIDATION_" frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js
rg -c "AbstractValidator<" services/Diten.Platform/src | wc -l        # geçişi bekleyen kapsam
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~ValidationReasonCodeTests"
cd frontend/Diten.Web && npx vitest run tests/validation-reason-code-bridge.test.js
```

### BL-034 — MOD-0024 yalnız "mutlu yol"u uyguluyor: tasarlanan aksiyonların bir kısmı yok
- **Nedir:** Frontend-first turunda (mock + fixture'lar) bir aksiyon dağarcığı tasarlanmıştı; motorun ürettiği alt kümedir. Eksik olanların hepsi ortak bir temaya sahip: **işin yolunda gitmediği durumlar.**
  - Tasarlanan kodlar: `grep -rhoE "action\('[a-zA-Z]+'|disabledAction\('[a-zA-Z]+'" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixtures/*.js | grep -oE "'[a-zA-Z]+'" | sort -u`
  - Motorun ürettikleri: proxy regex'i `frontend/Diten.Web/Controllers/TasksController.cs` + `TaskTransitionRoutes.All`
- **⚠ BU MADDENİN GÖVDESİ İKİ KEZ BAYATLADI (düzeltme 2026-07-31).** Önceki hâli "motor 7 aksiyon üretiyor" ve "`Waiting` ile `PendingReview` iki ölü durum, hiçbir uç nokta onları hedeflemiyor" diyordu. **İkisi de artık yanlış:** motor 11 kod üretiyor (`inquire`, `return`, `reassign`, `submitReview` eklendi) ve iki durum da canlı — `POST {id}/inquire` → `Waiting`, `POST {id}/submitReview` → `PendingReview`. Kayıt bu yüzden artık **sayı taşımıyor**, ölçüm komutu taşıyor (demir kural #10).
- **Bugün gerçekten eksik olanlar (2026-07-31 ölçümü):** `decline` · `reject` · `dispute` · `delegate` · `pause` · `replan` · `logTime`. Sınırda: `decline`'ı `return` karşılıyor sayılırsa 6, sayılmazsa 7 — bu bir **ürün kararı**, ölçüm değil.
- **Kapsam dışı bırakıldığı için eksik SAYILMAYANLAR:** `approve`/`signoff`/`resolve` MOD-0023'ün kararıdır (Binding A) · `acceptMeeting`/`declineMeeting`/`scheduleReviewMeeting` Faz 3b + BL-026 · `acceptOffer` (`offered` atama modu hiç kurulmadı) · `requestInfo` → motorda `inquire` adıyla var · `resume` → `start` kodu + resume etiketi olarak shipped.
- **Eksik aksiyonlar (MOD-0024 kapsamı, 12):**
  - *Reddetme / devretme:* `decline` · `reject` · `return` (iade et) · `dispute` · `reassign` (fixture'larda **9 kullanım**) · `delegate` → **atanan iş reddedilemiyor, başkasına verilemiyor.** Tek çıkış `cancel`, o da "iş tamamen iptal" demek — anlamı zıt. Üstelik `cancel` Gelen Kutusu'nda AÇIK: alıcı, talep edenin işini iptal edebiliyor (SAP/ServiceNow ayrımı: *iade* alıcının, *iptal* talep edenin hakkı).
  - *Yürütme kontrolü:* `pause` · `resume` · `replan` · `logTime` → sözleşmede `executionState: paused` ve `timerState: paused` **tanımlı**, hiçbir aksiyon oraya ulaşmıyor.
  - *Bilgi/bekleme:* `requestInfo` (fixture'larda **9 kullanım — en sık aksiyon**) · `inquire` → `Waiting` durumunu dolduracak olan bunlar.
- **Kapsam dışı olması DOĞRU olanlar:** `approve`/`signoff`/`resolve` MOD-0023'ün kararıdır (charter Binding A — MOD-0024 asla ikinci onay motoru yazmaz). `scheduleReviewMeeting`/`acceptMeeting`/`declineMeeting` Faz 3b + BL-026. `acceptOffer` — `offered` atama modu hiç kurulmadı.
- **Neden ertelendi:** Faz 1-3 planı bilinçli olarak mutlu yolu hedefledi; eksikler plana yazılmamıştı, CT'nin mock↔gerçek karşılaştırmasında çıktı (2026-07-26). Ürün açısından kritik olan **reddetme** ve **requestInfo** — ikisi de günlük kullanımda kaçınılmaz.
- **Sıra önerisi:** ~~(1) `decline`/`return` + `cancel`'ın Gelen Kutusu'ndan kaldırılması~~ → **`return` ve `cancel` yetkisi YAPILDI** (projeksiyon `cancel`'ı yalnız talep edene sunuyor, handler `CANCEL_NOT_REQUESTER` ile reddediyor; `TaskWaitingAndCancelAuthorityTests`). Kalan: `decline`; (2) `requestInfo`/`inquire` → `Waiting` canlanır; (3) `reassign`/`delegate`; (4) `pause`/`resume`/`logTime` (timeTracking capability ile birlikte).
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

## CT test turu — 2026-07-31 (BL-042 … BL-049)

> **Kapanış kaydı.** `docs/workcenter-test-sequence.md` oturum 1–7 ve 9, canlı sistemde CONTROL TOWER
> tarafından koşuldu (oturum 8 = görsel/RTL/tema, sahibin UX turuna ait). Her adım **iki yerden**
> ölçüldü: ekran + `/WorkCenterNext/api/work-items` projeksiyonu; kapılar ayrıca **doğrudan uç noktaya
> zorlanarak** sınandı.
>
> **Geçenler (yeniden ölçüm komutlarıyla):** üç kapı da hem projeksiyonda kapalı hem sunucuda
> zorlamaya dayanıklı (`CHECKLIST_INCOMPLETE` · `WORKFLOW_PENDING_APPROVAL` · `DEPENDENCY_BLOCKED`,
> üçünde de durum değişmedi) · sekme/segment sayaç özdeşliği · SLA gün metinlerinin tamamı ·
> Gelen→İşlerim kabul akışı ve yenilemede kalıcılık · havuz üstlen↔bırak tam gidiş-dönüş ·
> Geçmiş'te sıfır aksiyon butonu · bayat sürümle ikinci yazma reddi · oluşturma (emoji, HTML kaçışı,
> 200 karakter sınırı) · liste↔tablo kalem korunumu.
>
> **Koşulamayanlar ve nedeni:** 1.6 (Gelen Kutusu BL-042 yüzünden boşaltılamadı) · 2.5b–2.5e
> (BL-043 yüzünden Bekleyen segmenti arayüzden doldurulamıyor) · oturum 3'ün alt görev yarısı
> (veri yok) · oturum 8 (sahibin turu).
>
> **Turun bıraktığı dev verisi:** bağımlılık kenarı `Kesin geçiş provası ← Anahtar kullanıcı eğitimi` ·
> `CT testi — Ödeme koşulları 🚀` görevi · `asda` planlandı · Gelen Kutusu'ndan birkaç kabul · iki görev başlatıldı.

### BL-042 — 🔴 Planlanmış + kişiye atanmış görev Gelen Kutusu'nda kalıcı kilitleniyor
- **Belirti (canlı, 2026-07-31):** Gelen Kutusu satırında **Planla → Kabul et** sırası izlenirse görev
  Gelen Kutusu'ndan **hiç çıkmıyor**. `POST {id}/accept` **204** dönüyor, `admissionState` `pendingAcceptance`
  kalıyor. Altı ardışık denemenin altısı da 204 döndü ve hiçbiri durumu değiştirmedi.
- **Kök neden — kabul ayrı bir bayrak değil, yaşam döngüsünden çıkarsanıyor:**
  `Features/Tasks/Services/ITaskAssignmentResolver.cs:73-74` → `IsAccepted = Lifecycle not (Open or Planned)`;
  `Handlers/CommandHandlers/TaskItemTransitionHandlers.cs:52-55` yalnız `Open → InProgress` terfisi yapıyor.
  `Planned` görev `Open` olmadığı için terfi etmiyor, `Planned` olduğu için de kabul edilmiş sayılmıyor.
- **İkinci doğruluk kaynağı zaten yazılıyor ama okunmuyor:** aynı handler `TaskAssignment` kaydını
  `EventType = Accepted` ile oluşturuyor (`:66-76`); projeksiyon o kaydı hiç okumuyor.
- **Neden ucu keskin:** tuzağa **iki tıkla** ulaşılıyor — `Planla` butonu Gelen Kutusu satırında
  `Kabul et`in yanında duruyor. Ve uç nokta **başarı raporluyor**: ekran yalan söylemiyor, API söylüyor.
- **Yön (CT):** kabul, lifecycle'dan çıkarsanmayı bırakıp **kendi kalıcı işaretini** taşımalı
  (`AcceptedAt`/`AcceptedByUserId` alanı ya da mevcut `TaskAssignment` Accepted olayının okunması).
  Lifecycle'a bindirilen her ikinci anlam bu sınıfı yeniden üretir.
- **Yeniden ölçüm:** `rg -n "IsAccepted" services/Diten.Platform/src` · canlı: bir kalemi planla, sonra kabul et,
  `admissionState`'e bak.

### BL-043 — 🔴 `inquire` · `return` · `reassign` sunucuda hiç çalışmıyor (alan adı sözleşmesi kaymış)
- **Belirti (canlı, 2026-07-31):** istemci her geçişe aynı gövdeyi yolluyor —
  `{"expectedVersion":N,"reasonCode":null,"note":"…"}` (`wwwroot/assets/js/WorkCenterNext/app.js:3186-3190`).
  Bu üç uç nokta farklı bir sözleşme istiyor (`Features/Tasks/TaskModels.cs:338/344/350`):
  `InquireTaskItemRequest(ExpectedVersion, Reason)` · `ReturnTaskItemRequest(ExpectedVersion, Reason)` ·
  `ReassignTaskItemRequest(ExpectedVersion, AssigneeUserId, Reason)`.
  Sonuç: **400 · `errors.Reason: "The Reason field is required."`** — `inquire` ve `reassign` canlı ölçüldü;
  `return` aynı DTO ailesinde ve aynı istemci yolundan geçiyor.
- **İkinci katman — alan adı düzelse bile `reassign` çalışmaz:** diyalog yalnız gerekçe soruyor,
  **kime atanacağını hiç sormuyor**, yani `AssigneeUserId` hiçbir zaman gönderilmiyor.
- **Üçüncü katman — hata sebebi kayboluyor:** kullanıcıya dönen tek şey *"İşlem sırasında bir hata oluştu."*
  Model-binding hatası `Response<T>` zarfı taşımadığı için `failureMessage` sebep kodu çıkaramıyor.
  **BL-040 ile aynı aile**, farklı giriş kapısı (model binding, FluentValidation değil) — ikisi birlikte çözülmeli.
- **Ürün etkisi:** `Bekleyen` segmenti arayüzden **hiç doldurulamıyor**; iade ve devretme akışlarının tamamı ölü.
- **Yön (CT):** üç uç noktanın gövdesini tek bir dağarcığa bağla ve **her iki tarafı da aynı kaynaktan test et**
  (`fixture-contract.js` dağarcığı, WC-1 dersi). Kaymanın nedeni "istemci genel, sunucu özel" ayrımının
  hiçbir yerde yazılı olmaması.
- **Yeniden ölçüm:** `rg -n "record (Inquire|Return|Reassign)TaskItemRequest" services/Diten.Platform/src` ·
  `rg -n "reasonCode: null" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js`

#### ✅ KAPANIŞ — `d71a3529` (kod) · CT canlı doğrulaması 2026-08-01 · BL-042 + BL-043

> **Ders, kayıtta kalıyor.** Bu başlık ilk yazıldığında `✅` idi, sonra `⚠️ KISMİ`'ye indirildi, ve
> ancak **üç tur sonra** gerçekten kapandı. Kod ilk günden doğruydu, testler yeşildi — ve akış yine
> çalışmıyordu: önce kişi seçici boş değer üretiyordu (BL-050), sonra kabul kapısı devretmede
> yeniden açılmıyordu (BL-051). İki kusur da **yalnız canlı turda** göründü. Demir kural #10'un
> "kapanış = kod değil, doğrulama" maddesi bu iki vakadan doğdu.
>
> **Kapatan ölçümler (CT, 2026-08-01, `ed527d52` sonrası):**
> Planlanmış+atanmış görev kabul edildi → `owned/admitted`, `lifecycle` `Planned` **kaldı** ·
> ikinci accept → `409 TASK_ALREADY_ACCEPTED` · backfill sonrası dağılım turdan önceki hâlinde
> (`admitted 12 · pending 2 · havuz 2`) · `inquire` gövdesi `{expectedVersion, reason}` → 204,
> `Waiting`, gerekçe sunucuda, **sekme değişmedi** · devretme gövdesi `+assigneeUserId` → 204,
> kişi seçici değerleri dolu · devret→geri devret sonrası `assigned/pendingAcceptance`, kabul
> sonrası `owned/admitted` (tam gidiş-dönüş) · havuz üstlen↔bırak ×2 · üç kapı 409.
>
> **Kapsam dışında kalan tek davranış:** `return` uçtan uca **koşulamadı**. Kusur değil, koşul
> eksikliği: `TaskWorkItemProvider.cs:1152` iadeyi yalnız **ayrı bir talep eden varsa** sunuyor
> (kendine iade no-op'tur), bu ortamda talep eden = atanan. İkinci bir kullanıcı oturumu gerektirir;
> `⬜` olarak MOD-0024 pack'inde duruyor.

**BL-042 — ne yapıldı.** Kabul artık yaşam döngüsünden çıkarsanmıyor; `TaskItem.AcceptedByUserId`
taşıyor ve **varlığı** kabul demek. `AcceptTaskItemHandler` bunu yazıyor, `ITaskAssignmentResolver`
bunu okuyor, bayat yorum güncellendi.

**Kararlar ve gerekçeleri:**
- **Zaman damgası eklenmedi** — BL-030: `DateTimeOffset` BSON dizisi olarak yazılıyor ve sorgu/sıralama
  kırıyor. Kabul anı zaten `TaskAssignment` (EventType=Accepted) satırında; ikinci kez saklamak
  hiçbir şey kazandırmaz, o riski geri getirir.
- **Lifecycle terfisi ayrı karar olarak kaldı** — `Open → InProgress` sürüyor ama `Planned` görev
  planlı kalıyor. Kabul, planı silmemeli; iki anlamı tek alana bindirmek bu kusurun ta kendisiydi.
- **Sonuç değiştirmeyen accept artık 204 dönmüyor** — `409 · TASK_ALREADY_ACCEPTED`. İdempotent-sessiz
  başarı yerine tipli ret seçildi: ikinci kez soran istemci bayat bir görüntüyle çalışıyordur ve tek
  yararlı cevap bunu söylemektir (eşzamanlılık çakışmasıyla aynı şekil, istemci onu zaten yönetiyor).
  "Başarı dönüp hiçbir şey yapmamak" kusuru altı tur boyunca görünmez kılan şeydi.
- **Backfill kodla birlikte gönderildi, ayrı adım değil** — `TaskAcceptanceBackfillMigration`, her iki
  startup yolunda. Yüklem **eski kuralın birebir kendisi** (`Person` && assignee != null && lifecycle
  ∉ {Open, Planned}); uydurma bir kural değil, kopyalanmış bir tanım — davranışın korunduğu bu yüzden
  bir umut değil, bir olgu. `AcceptedByUserId = AssigneeUserId`: eski kuralın kastedebileceği tek kişi
  odur (accept zaten yalnız atanana açık). İdempotent — yalnız damgasız satırlara dokunuyor.

> ⛔ **BL-043'ÜN KAPANIŞI GERİ ALINDI (CT canlı doğrulaması, 2026-07-31).** Aşağıdaki kayıt
> yapılan işi doğru anlatıyor, ama madde **kapanmadı**: devretme hâlâ çalışmıyor. Ayrıntı
> BL-050'de. Kapanış kaydı, iş bittiğinde değil **doğrulandığında** yazılmalıydı; bu satır o
> dersin kaydıdır.

**BL-043 — ne yapıldı.** Gövde şekli tek bir yerde bildirildi (`TRANSITION_BODIES`, `app.js`).
`inquire`/`return` → `{expectedVersion, reason}`, `reassign` → `+ assigneeUserId`; diğer yedi geçiş
doğru olan jenerik gövdede kaldı. Devretme diyaloğuna kişi seçici eklendi (kaynak: oluşturma
formuyla **aynı** `TasksApi.assignablePeople` listesi — sunucunun doğruladığı liste), kişi
seçilmeden onaylanamıyor. 4 yeni dize **7 dilde** tam.

**Kararlar ve gerekçeleri:**
- **Sunucu gevşetilmedi, istemci düzeltildi.** `Reason`'ın zorunluluğu bilinçli bir kural
  (`TaskModels.cs:341-343`); gövdeyi opsiyonel yapmak onu sessizce silerdi.
- **Eşleme bir yorumla değil, İKİ TARAFI DA okuyan bir testle bağlandı.** WC-1 dersi: iki yerde
  yaşayıp hiçbir yerde bildirilmeyen değer kayar. `task-transition-contract.test.js` gerçek C#
  record'larını ve gerçek istemci haritasını ayrıştırıp alan alan karşılaştırıyor — hiçbirini
  yeniden yazmıyor.

**KASTEN yapılmayanlar:**
- **BL-040'ın tamamı (model-binding hatalarının sebep kodu taşıması) çözülmedi.** Bu tur istemciyi
  400 üretmeyecek hâle getirdi, yani bu iki maddenin kullanıcıya dönük etkisi kalktı; ama zarfsız
  model-binding hatası hâlâ sebep kodu taşımıyor. İki maddenin **aynı çatlaktan** geldiği
  `TRANSITION_BODIES` yorumunda kayıtlı ki sonraki tur ikisini birlikte görsün.
- **HTTP seviyesinde canlı gidiş-dönüş testi yazılmadı** — servisleri başlatmam yasak. Yerine
  **gerçek MongoDB + gerçek migration + gerçek resolver** ile 8 test yazıldı
  (`TaskAcceptanceBackfillMongoTests`). Bu, birim testinden güçlü ama canlı HTTP turundan zayıf;
  **kabul/inquire/return/reassign'ın uçtan uca çalıştığını canlıda CT doğrulamalı.**

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "IsAccepted" services/Diten.Platform/src
rg -n "AcceptedByUserId" services/Diten.Platform/src | rg -v Tests
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~TaskAcceptanceBackfillMongoTests"
cd frontend/Diten.Web && npx vitest run tests/task-transition-contract.test.js
```
Canlı: bir görevi **Planla → Kabul et**, `admissionState` `admitted` olmalı; ikinci accept **409
TASK_ALREADY_ACCEPTED**. `inquire` gerekçeyle → `Waiting` + Bekleyen segmenti. `reassign` → kişi
seçici geliyor, seçmeden onaylanamıyor, sonra görev yeni kişide `pendingAcceptance`.

#### 🔬 CT CANLI DOĞRULAMASI — `d858bb36` sonrası, 2026-07-31

Servisler yeniden başlatılıp ölçüldü (aşağıya bak: **süreç canlıydı ama ikili 4 saat eskiydi**).

| Ölçüm | Sonuç |
|---|---|
| Backfill regresyonu — dağılım turdan önceki hâlinde mi | ✅ `owned/admitted 18 · assigned/pendingAcceptance 3 · unowned/pendingClaim 2` — birebir aynı, kimsenin İşlerim'i boşalmadı |
| Planlanmış görev (`asda`: Planned + pendingAcceptance) → Kabul et | ✅ `owned/admitted`, **`lifecycle` `Planned` KALDI** — kabul planı silmedi, tasarım kararı canlıda tuttu |
| İkinci accept | ✅ `409 · TASK_ALREADY_ACCEPTED`, durum değişmedi |
| `inquire` gerekçeyle | ✅ gövde `{expectedVersion, reason}` → **204**; `Waiting`; `waitingContext.reason.text` = kullanıcının cümlesi; Bekleyen 1→2; **sekme değişmedi** (aks yasası) |
| `reassign` | 🔴 **BL-050** — kişi seçici geliyor ama seçilemiyor |
| Üç kapı regresyonu | ✅ `CHECKLIST_INCOMPLETE` · `WORKFLOW_PENDING_APPROVAL` · `DEPENDENCY_BLOCKED`, üçünde de 409 ve durum değişmedi |

**⚠ Doğrulamanın kendisi hakkında bir ders:** ilk ölçümde accept **hâlâ 204 dönüp hiçbir şey
yapmıyordu** ve bu, düzeltmenin çalışmadığı sonucuna götürüyordu. Ölçüm yanlıştı: `5057` süreci
`18:12`'de başlamıştı, yeni ikili `22:55`'te derlenmişti ve Platform `--no-build` ile **watch'sız**
koşuyordu. Yani dosyada doğru kod, bellekte eski kod. `strings … | grep AcceptedByUserId` ikilide
alanı gösterdi, süreç başlangıç saati farkı açıkladı. **`/health` 200 ≠ güncel ikili; süreç canlı ≠
kod canlı.** Doğrulama, süreç başlangıç saatini ikili tarihiyle karşılaştırmadan başlamamalı.

#### ⚠️ KAPANIŞ (KISMİ) — BL-044 · BL-046 · BL-047 · BL-048 — 2026-08-01 — **CANLI DOĞRULAMA BEKLİYOR**

> Demir kural #10, yeni eşik: kod yazıldı ve testler yeşil — bu **kapanış değil**. `✅` yalnız CT canlı turundan
> sonra yazılır. BL-043 bu dersin kaydıydı; aynı hatayı tekrarlamıyoruz.

#### 🔬 CT CANLI TURU — 2026-08-01, `ce9aa7ba` sonrası (süreç 00:21 > ikili 00:20)

| Madde | Sonuç |
|---|---|
| **BL-044** | ✅ **DOĞRULANDI.** `kapanış` · `KAPANIŞ` · `kapanis` · `KAPANIS` · `KaPaNiŞ` — beşi de aynı kalemi buluyor. |
| **BL-047** | ❌ **ULAŞMADI.** Türkçe sayfada hâlâ *"Showing 1 to 8 of 8 entries"*. |
| **BL-046** | ❌ **DAHA KÖTÜ.** Geçmiş'te artık *"-2g kaldı"* · *"-1g kaldı"* yazıyor. |
| **BL-048** | ⚠️ Kayıttaki gerekçe **yanlış** — aşağıda düzeltildi. |

**BL-047 — arz düzeldi, teslimat yok.** Payload artık altı `Dt*` anahtarını Türkçe taşıyor (canlı ölçüm:
`#workcenternext-l10n` içinde `DtInfo = "_TOTAL_ kayıttan _START_ - _END_ arasındaki kayıtlar gösteriliyor"`).
Ama `dt-defaults.js:8` → `var L = function () { return window.L10n || {}; }` — tüketici **yalnız
`window.L10n`'a** bakıyor, modül payload'ına değil. `app.js:2506-2508` `window.L10n`'a yalnız **iki**
anahtar tohumluyor (`Search`, `Action`). Canlı ölçüm: `Object.keys(window.L10n)` → 9 anahtar,
`Dt*` sayısı **0**. Yani zincirin son halkası hâlâ kopuk; düzeltme yanlış uçtan yapıldı.
**Bu, BL-050 ile aynı sınıf:** kaynak doğru, tüketicinin okuduğu yer başka.

**BL-046 — sunucu yarısı doğru, istemci yarısı okunamaz metin üretiyor.** Sunucu `slaState`'i artık
kapanış anına göre hesaplıyor (ölçüm: *Ay sonu kapanış*, son tarih 2026-07-30, iptal → `on-track` ✓).
Ama gün sayısını **istemci** hâlâ `dueAt` ile **bugünden** türetiyor, ve `slaLabel`'ın `on-track`
dalında `d <= 0` koruması yok → `tf('SlaDueInDays', -2)` → **"-2g kaldı"**. Değişiklikten önce bu
kalemler `overdue` olduğu için "2g gecikmiş" yazıyordu: yanlıştı ama **okunabilirdi**. Şimdi anlamsız.
Hâlâ `overdue` olanlarda kayma da sürüyor (dün 11g/9g/5g → bugün 12g/10g/6g).
**Ders:** sözleşme alanı (`closedAt`) olmadan "sunucunun sahibi olduğu yarıyı" göndermek, kusuru
düzeltmedi — **görünür hâlini bozdu.** Yarım düzeltme, bekleyen düzeltmeden kötü olabilir.

**BL-048 — kayıttaki "`RequestTitle` diye bir özellik repoda hiç yok" ifadesi YANLIŞ.** Ad bir özellik
adı değil, FluentValidation'ın **ifade yolundan türettiği** görünen ad:
`CreateTaskItemValidator.cs:16` → `RuleFor(x => x.Request.Title)` → görünen ad `"Request Title"`.
CT'nin ölçtüğü mesaj (`400 · "'Request Title', 200 karakterden küçük veya eşit olmalıdır. 224 karakter
girdiniz."`) doğrudan bu koddan geliyor ve **sunucudan** üretiliyor — ajan istemcide (`Tasks/api.js`)
aradığı için bulamadı. **Sonuç değişmiyor:** madde yine BL-040'a bağlı; yalnız gerekçesi bu.

**BL-044 — Türkçe büyük harf araması** · `4ce30d29`
Arama iki tarafta da **yerelden bağımsız** katlanıyor: NFD ile birleşen işaretler ayrılıyor (ş→s, ü→u, é→e)
ve I/İ/ı/i ailesi `i`'ye iniyor. Bu tek değişiklik `KAPANIŞ`'ı da aksansız `kapanis`'i de çözüyor.
`toLocaleLowerCase('tr')` **reddedildi**: bir dili düzeltip diğer altısını bozardı; testler ru ve ar vakalarını
da koşuyor. Dört arama noktasının hepsi katlanıyor (kalem araması + tetik araması).
**Vacuity kanıtı testin içinde:** bir test **eski** uygulamayı koşturup raporlanan yanlış cevabı verdiğini
gösteriyor — yazımı doğru, davranışı yanlış bir katlama böylece geçemez.

**BL-046 — kapanmış görevin canlı SLA sayacı** · `70c0912e`
Terminal görevde SLA saati **kapanış anında** duruyor (`CompletedAt` → `CancelledAt` → bugün).
Rozet **silinmedi**, donduruldu: gecikmeyle kapanmış iş raporlamada değerlidir.
**KIRMIZI kanıtı:** düzeltme geri alındığında `Work_finished_ON_TIME_does_not_drift_into_overdue_as_the_calendar_moves`
düşüyor (diğer üçü doğru şekilde yeşil kalıyor — mutasyon tam da değişen davranışı vurdu).
**⚠ YARIM:** `slaState` bir **dize**; kullanıcının okuduğu **gün sayısı** (`slaDiffDays`) istemcide
`dueAt` ile bugün'den türetiliyor ve projeksiyon **kapanış zamanını göndermiyor**. Yani "11g → 12g" kayması
için sözleşmeye bir `closedAt` alanı gerekiyor. Sunucunun sahibi olduğu yarı düzeltildi; istemci yarısı açık.

**BL-047 — DataTable metinleri İngilizce** · `4ce30d29`
**Mekanizma zaten merkezî ve zaten doğruydu:** `dt-defaults.js:462-466` altı `Dt*` anahtarını sayfa
payload'ından okuyup DataTables'a veriyor, ve `SharedResource` altısını da **yedi dilde** taşıyor.
Eksik olan tek halka `_L10n.cshtml`'di: yalnız **modül** resx'ini numaralandırıyordu, dolayısıyla `Dt*`
anahtarları istemciye hiç ulaşmıyor ve merkezî kod çalışacak veri bulamıyordu. Modül anahtarları çakışmada
kazanmaya devam ediyor.
**KAPSAM ÖLÇÜMÜ (istenen tarama):** `Dt*` anahtarlarını payload'ına koyan sayfa sayısı **repo genelinde 0**'dı
— yani bu WorkCenterNext'e özgü değil, **her DataTable sayfası** aynı durumda. 61 dosya DataTable kuruyor.
**Önerilen kontrol:** payload üreten her `_L10n.cshtml`'in `Dt*` anahtarlarını enjekte ettiğini doğrulayan
tek bir kaynak-taraması testi (bu turda WorkCenterNext için yazıldı; genelleştirilmesi ayrı dilim).

**BL-048 — doğrulama mesajında ham alan adı** — **DOKUNULMADI, ölçüm gereği**
İstenen ölçüm yapıldı: `TasksApi.failureMessage` (`Tasks/api.js:122-135`) mesajı **yalnız `reasonCode`**
üzerinden resx'e çeviriyor; sunucunun ham `errors` metnini **hiç göstermiyor**. Yani BL-040 (sebep kodu
köprüsü) çözülünce bu yol tamamen yerelleşir → **BL-048 kendiliğinden kapanır**. Ayrıca `RequestTitle`
diye bir özellik **repoda hiç yok** (`rg` → 0 sonuç), yani CT'nin gördüğü ekran bu koddan üretilmiyor;
hangi yüzey olduğu ölçülemedi. **Kayıt: BL-040'a bağlı.**

**CANLI DOĞRULAMA ADIMLARI (CT):**
1. **BL-044** — Türkçe sayfada aramaya sırayla `kapanış` · `KAPANIŞ` · `kapanis` · `KAPANIS` yaz;
   dördü de **aynı** kalemi bulmalı. Sonra `Überprüfung` içeren bir kalemde `uberprufung` dene.
2. **BL-046** — Geçmiş sekmesi: gecikmeyle kapanmış bir kalemin rozeti **hâlâ gecikmiş** demeli
   (silinmemeli). Zamanında kapanmış bir kalem **gecikmiş görünmemeli**. *(Gün sayısının donması bu turda
   YOK — yukarıdaki yarım madde.)*
3. **BL-047** — Türkçe sayfada tablo görünümüne geç: alt bilgi satırı, sayfalama ve boş-tablo metni
   **Türkçe** olmalı ("Showing 1 to 9 of 9 entries" **görünmemeli**).
4. **BL-045 / BL-049** — bu turda **yapılmadı**, aşağıya bakın.

**BU TURDA YAPILMAYANLAR — açıkça:**
- **BL-045 (çip sayacı ↔ segment sayaçları)** — **yapılmadı.** Karar (segment sayaçlarının çip etkinken
  yeniden hesaplanması) anlaşıldı ama `app.js`'te sayaç hesabı üç ayrı yerden besleniyor ve doğru dilim
  kendi turunu hak ediyor; yarım bir faceted sayaç, bugünkü tutarsızlığı başka bir yere taşırdı.
- **BL-049 (ham GUID)** — **yapılmadı.** Yer tespit edildi (`app.js` `renderSourceContext`,
  `previewField('bx-hash', 'DetailSourceId', item.sourceId)`); GUID'i kopyala-düğmesine taşımak + 7 dilde
  yeni anahtar gerekiyor.

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "toLowerCase\(\)" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js   # arama yolunda olmamalı
rg -n "dtKeys" frontend/Diten.Web/Views/WorkCenterNext/_L10n.cshtml
rg -n "SlaReferenceInstant" services/Diten.Platform/src
cd frontend/Diten.Web && npx vitest run tests/workcenter-next-search-and-chrome.test.js
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~TaskHandoverTests"
```

#### ⚠️ KAPANIŞ (KISMİ) — BL-046 · BL-047 · BL-049 — 2026-08-01 · `a786d194`

> **Bugünün iki dersi bu turda uygulandı.** (1) *Arz düzeldi, teslimat yok:* BL-047 için bu kez **tüketicinin
> sözlüğü** test ediliyor, payload değil. (2) *Yarım düzeltme kusurdan kötü olabilir:* BL-046'nın gün sayısı
> için sözleşme alanı **hâlâ yok**, o yüzden o yarı **yapılmadı** — ama önceki turda benim ürettiğim
> `-2g kaldı` regresyonu kapatıldı.

#### 🔬 CT CANLI TURU — 2026-08-01, `9574fce2` sonrası (süreç 00:36 > ikili 00:36)

| Madde | Sonuç |
|---|---|
| **BL-047** | ✅ **DOĞRULANDI.** Tablo alt bilgisi *"8 kayıttan 1 - 8 arasındaki kayıtlar gösteriliyor"*; sayfada İngilizce kalıntı **yok**. Tablo monte edildikten sonra `window.L10n`'da **6** `Dt*` anahtarı. *(İlk okumamda 0 çıktı — tohumlama montaj sırasında olduğu için; ölçüm sırası benim hatamdı, kusur değil.)* |
| **BL-049** | ✅ **DOĞRULANDI.** Ekranda tam GUID **yok**; kısaltılmış `523a954e…8237` + *"Referansı kopyala"* düğmesi. Başarısızlık yolu da çalışıyor: pano reddedince **görünür** *"Referans kopyalanamadı"* çıkıyor. **Başarı yolu ölçülmedi** — betikle yapılan tıklama güvenilir kullanıcı hareketi sayılmadığı için tarayıcı `NotAllowedError` verdi; gerçek tıklamayla sahibin doğrulaması gerekiyor. |
| **BL-046** | ❌ **AÇIK KALIYOR.** `-Ng kaldı` gitti ✓ ama yeni bir tutarsızlık ölçüldü — aşağıda. |
| **BL-045** | ⬜ Yapılmadı (ikinci kez, bilinçli). |

**BL-046 — ölçülen yeni sonuç: ekran artık SUNUCUYLA ÇELİŞİYOR.**
Negatif koruması, geçmiş tarihli her kalemi `slaState`'ten **bağımsız olarak** "gecikmiş" ifadesine
yönlendiriyor. Sonuç, zamanında kapanmış işin geç kapanmış gibi görünmesi:

| kalem | son tarih | sunucu `slaState` | ekranda |
|---|---|---|---|
| Ay sonu kapanış kontrol listesi (İptal) | 2026-07-30 | **on-track** | **"2g gecikmiş"** |
| Üretim tesisi 2 kapasite raporu (Tamam) | 2026-07-31 | **on-track** | **"1g gecikmiş"** |

Yani madde bir adım ilerledi (**okunamaz** → **okunabilir**) ama hedefine varmadı: rozet hâlâ kapanmış
iş hakkında **yanlış** konuşuyor, üstelik artık sunucunun kendi kararına da aykırı. Kalan iş değişmedi:
projeksiyona kapanış zamanı alanı + dağarcığa bildirim + istemcinin gün sayısını ondan türetmesi.
`Done`/`Cancelled` kalemde istemci, sunucunun `slaState`'ini **ezmemeli**.

**BL-047 — tablo dili, TÜKETİCİ tarafı**
`dt-defaults.js:8` `window.L10n` okuyor; `app.js` oraya yalnız `Search` ve `Action` tohumluyordu. Altı `Dt*`
anahtarı iki uçta da vardı ve **hiç buluşmuyordu**. Artık modül payload'ından `window.L10n`'a tohumlanıyorlar,
**çevirmenden geçirilerek** (`t(key) === key` ise yazılmıyor — yoksa ekranda `DtInfo` yazardı).
**Seçim (a), gerekçesi:** (b) — dt-defaults'un modül payload'ını okuması — daha genel ve 61 dosyanın ihtiyacı;
ama tek ekranın kusuru yüzünden **ürünün tamamının** tablo bootstrap'ini değiştirmek kendi regresyon turunu
hak eden bir platform dilimidir. (a) yerel ve geri alınabilir.
**KIRMIZI kanıtı:** tohumlama satırları silinince 2 test düşüyor.
*(Not: ilk mutasyon denemem regex uyuşmadığı için **hiç uygulanmadı** ve "0 düştü" yanıltıcı çıktı — satır
bazlı yeniden ölçtüm. Uygulanmayan mutasyonun sayısı geçersizdir.)*

**BL-046 — etiket sınırı** *(gün sayısı YARIM kaldı, bilerek)*
`slaLabel`'ın `on-track` dalında negatif/sıfır koruması yoktu; sunucu tarafı donunca Geçmiş'te **`-2g kaldı`**
çıktı. Gelecekte olmayan bir tarih artık **hangi durum gelirse gelsin** gecikmiş ifadesine gidiyor; bu aynı
zamanda eski `on-track + d===0 → "0g kaldı"` sınır kusurunu ve `dueAt` yokken oluşan `NaN`'ı da kapatıyor.
**KIRMIZI kanıtı:** koruma kaldırılınca 2 test düşüyor.
**⚠ YAPILMADI:** gün sayısının **donması**. Projeksiyon hâlâ kapanış zamanını göndermiyor; `closedAt` benzeri
bir sözleşme alanı + `fixture-contract` dağarcığına bildirim + istemci hesabının ona geçmesi gerekiyor.
Bugün hâlâ overdue kalemlerde sayı kayıyor (dün 11g → bugün 12g). **Bunu yarım göndermek bu turun ilk
dersiydi; tekrarlamadım.**

**BL-049 — ham GUID**
Kimlik ana yüzeyden kalktı: kısaltılmış gösterim (`31a44983…b2b0`), **tam değer** başlıkta ve kopyala
düğmesinde, pano yoksa **görünür hata**. Silinmedi — destek konuşmasının ihtiyacı olan şey tam olarak o.
Kısa iş anahtarları (`INV-2026-0042` gibi) kısaltılmıyor. 3 yeni dize **7 dilde**.
**KIRMIZI kanıtı:** `previewField('bx-hash', 'DetailSourceId'` geri konunca test düşüyor.

**BL-045 — YAPILMADI (bilinçli, ikinci kez).**
Karar anlaşıldı (faceted segment sayaçları). Sayaç hesabı hâlâ üç ayrı yerden besleniyor ve bu turda ona
hakkını verecek yer kalmadı. Talimatınız net: *"sığmazsa yapma"*. Bugün yarım bir düzeltmenin kusurdan kötü
çıktığını iki kez ölçtük; üçüncüsünü üretmiyorum.

**CANLI DOĞRULAMA ADIMLARI (CT):**
1. **BL-047** — Türkçe sayfada tablo görünümü: alt bilgi, sayfalama ve boş-tablo metni **Türkçe**.
   Konsolda `Object.keys(window.L10n).filter(k => k.startsWith('Dt'))` → **6 anahtar** dönmeli (bugün 0'dı).
2. **BL-046** — Geçmiş'te **`-Ng kaldı` ifadesi HİÇ görünmemeli**. Gecikmeyle kapanmış kalem hâlâ
   "gecikmiş" demeli. *(Gün sayısı hâlâ kayacak — o yarı yapılmadı.)*
3. **BL-049** — Detay → Kaynak Bağlamı: tam GUID yerine kısaltılmış kimlik + kopyala düğmesi;
   düğmeye basınca "Referans kopyalandı" ve pano gerçekten dolmalı.

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "global.L10n\[key\]" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js
rg -n "d < 0|d === 0" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js | head
rg -n "previewField\('bx-hash'" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js   # boş olmalı
cd frontend/Diten.Web && npx vitest run tests/workcenter-next-search-and-chrome.test.js
```

#### ✅ KAPANIŞ — BL-046 · BL-045 — `7b2f3772` + CT sınır düzeltmesi · CT canlı doğrulaması 2026-08-09

> **BL-045 — canlı ölçüm, tam geçti.** Çip kapalı: `Aktif 8 · Bekleyen 2 · Planlı 2 = 12` = sekme sayacı.
> Çip AÇIK: `Aktif 3 · Bekleyen 1 · Planlı 1 = 5` = çip sayacı (**SLA riski 5**), liste 3 (aktif segment).
> Sekme rozetleri değişmedi (`2·12·2·6`), çipi kapatınca her şey birebir eski hâline döndü. Kullanıcı artık
> *"SLA riski 5 — 1'i Bekleyen'de, 1'i Planlı'da"* bilgisini görüyor; sinyal ekseni segmentin altına inmedi.
>
> **BL-046 — canlı ölçüm.** Gün sayıları `dueAt ↔ closedAt` farkına eşit, bugünle ilgisi kalmadı (yarın
> değişmeyecek); negatif ifade yok; ekran sunucunun `slaState`'i ile **çelişmiyor**:
> `6g/7g gecikmeyle kapandı` (overdue) · `Zamanında kapandı` ×3 (on-track). Turun asıl kusuru — zamanında
> biten işin "gecikmiş" demesi — kapandı.
>
> **CT'nin bulduğu sınır vakası ve düzeltmesi (aynı oturumda).** Canlı ölçüm: *Tedarikçi sözleşme…*
> son tarih `2026-07-26 18:00`, kapanış `21:04` — **aynı gün, 3 saat 4 dakika geç**. Sunucu doğru biçimde
> `overdue` diyor, ama gün granülaritesinde fark `0`'a yuvarlanınca ekran **"0g gecikmeyle kapandı"** yazıyordu.
> Bu cümle insana "gecikmemiş" diye okunur; durum ise "gecikmiş" — yani **durum ile cümle yine ayrışmıştı**,
> bu dalın var oluş sebebi tam olarak bunu bitirmekti. Bu turda üçüncü kez görülen sınır ailesi
> (`0g kaldı` · `-2g kaldı` · `0g gecikmeyle kapandı`).
> **Düzeltme:** `app.js` `slaLabel` terminal-geç dalında `Math.abs(d) >= 1` koşulu; gün altı aşım sayıyı
> düşürüp zaten var olan sayısız etiketi kullanıyor (`SlaClosedLate` — 7 dilde hazırdı, **yeni dize gerekmedi**).
> **Kırmızı kanıtı:** koşul mutasyonla kaldırıldı → yalnız yeni test düştü (1 başarısız / 17 geçti); geri
> konunca `workcenter-next-sla-closed-freeze` + `workcenter-next-faceted-counters` = **29/29**.
> **Canlı teyit:** ekranda `"Gecikmeyle kapandı"`, `"0g gecikmeyle"` ifadesi **hiç yok**, diğer beş satır aynı.
>
> **Yeniden ölçüm:** `cd frontend/Diten.Web && npx vitest run tests/workcenter-next-sla-closed-freeze.test.js tests/workcenter-next-faceted-counters.test.js` ·
> canlı: Geçmiş'te gün sayısı yarın değişmemeli; çipe tıklayınca segment sayaçları yeniden hesaplanmalı.

> **Bu turun iki dersi, uygulandığı yer.** (1) *Yarım düzeltme kusurdan kötü olabilir:* BL-046 bu kez üç
> parçanın **üçü birden** gönderildi — sözleşme alanı, dağarcık bildirimi, istemci hesabı. İkisi gönderilseydi
> yine yeni bir yalan çıkardı, o yüzden bölünmedi. (2) *Arz düzeldi, teslimat yok:* alanı üreten tarafın
> testi **yetmez** sayıldı; `ClosedAt`'in tel üstüne çıktığı sunucu tarafında, proxy'nin gövdeyi olduğu gibi
> geçirdiği ise okunarak ayrıca doğrulandı.

**BL-046 — kapanmış görevin gün sayısı** *(üç parça, tek dilim)*

| parça | ne yapıldı |
|---|---|
| (a) sözleşme alanı | `WorkItemProjectionDto.ClosedAt` — opsiyonel, null'ken **hiç yazılmaz**, kuyrukta. `TaskWorkItemProvider` `CompletedAt ?? CancelledAt` gönderir (terminal değilse **null**); `WorkItemProjectionService` (MOD-0023) `CompletedAt` gönderir ve **kendi slaState'ini de** kapanışta dondurur — orada hâlâ `UtcNow` ile ölçülüyordu. |
| (a) dağarcık | `fixture-contract.js` `closedAt`'i **tanır**: varsa doğrulanır, asla zorunlu değil (BL-038). İki hata: `CLOSED_AT_INVALID` (ayrıştırılamaz an) ve `CLOSED_AT_ON_OPEN_ITEM` (açık işte kapanış anı — çelişki). |
| (b) gün sayısı | `mock-data.js daysLateAtClose(dueAt, closedAt)`. Terminal kalemde sayı **bugüne hiç bakmaz**. Kapanış anı yoksa canlı sayıya düşer ama **etikete çıkmaz**. |
| (c) ezme yok | `app.js slaLabel` terminal dalı **önce** döner: durum sunucunun, sayı iki tarihin farkı. `on-track` diyen kapanmış iş artık "gecikmiş" **diyemez**. |

**Rozet silinmedi, donduruldu.** Yeni 3 dize, **7 dilde**: `SlaClosedLateByDays` ("{0}g gecikmeyle kapandı"),
`SlaClosedLate` (sayı yokken), `SlaClosedOnTime`.
**Terminal olmayan kalemdeki `d<=0` boşluğu korundu** (`0g kaldı` / `-Ng kaldı` geri gelemez) — ve orada
"gecikmiş" demek sunucuyla çelişmez, çünkü iş hâlâ canlı.
**Showcase da hizalandı:** `computeShowcaseSla` terminal fixture'ı kapanış gününden ölçüyor; Geçmiş
fixture'larının üçüne kendi etkinlik kayıtlarındaki kapanış anı yazıldı. Biri **bilerek geç kapatıldı**, yoksa
donmuş "geç kapandı" rozetinin gösterecek örneği kalmıyordu.

**KIRMIZI kanıtı (vacuity değil):** düzeltmeden önce iki yeni dosya birlikte **14 test düşürdü**; kalan 12'si
(ölçülen şeklin yeniden üretimi, canlı geri sayımın sürmesi, sekme rozetinin sabitliği) o anda da geçiyordu —
yani kırmızıların hepsi gerçek davranış farkı. Sunucu yarısı ayrıca **mutasyonla** ölçüldü: `ClosedAt: null`
yapılınca 2 test düşüyor (null bekleyen 2'si doğal olarak ayakta kalıyor). 7 dil kapısı da mutasyonla ölçüldü:
`ru.resx`'ten tek anahtar silinince l10n testi düşüyor.

**BL-045 — çip ↔ segment sayaçları (faceted)** *(üçüncü turda yapıldı)*

Karar uygulandı: **segment sayaçları çip/arama altında yeniden hesaplanıyor**. Çip "SLA riski 3" derken segment
barı artık *2 · 1 · 0* diyor — "1'i Bekleyen'de" bilgisi **fazladan bir gösterim icat etmeden** oradan okunuyor.
Süsleme yapılmadı; nihai gösterim sahibin UX turunda karara bağlanacak.

**Reddedilen alternatif teste çakıldı:** çip sayacı aktif segmente **daraltılmıyor** — Bekleyen'e geçince de 3
diyor. Sinyal, statüden bağımsız bir eksendir.

**Üç yolun üçü birden hizalandı** (talimatın şartı): `segmentCount` · `typeCount` · `signalCount` artık tek bir
`facetItems(except)` tabanından besleniyor; bir sayaç **kendi eksenini** hiç uygulamaz, diğer hepsini uygular.
"Tümü" çipi de aynı tabana alındı — tip ekseninin sıfır durumu olduğu için yanındaki çiplerle çelişmesi
kaçınılmazdı. **Sekme rozetleri değişmedi ve değişmemeli:** başka sekmeden okunan bir sayıdır; `app.js`'teki o
yorum silinmedi, **daraltıldı**.

**CANLI DOĞRULAMA ADIMLARI (CT):**
1. **BL-046 / sunucuyla çelişki** — Geçmiş'te *"Ay sonu kapanış kontrol listesi"* (İptal) ve *"Üretim tesisi 2
   kapasite raporu"* (Tamam): ikisi de **"Zamanında kapandı"** demeli. `SlaOverdueByDays` metni ("Ng gecikmiş")
   Geçmiş sekmesinde **hiç görünmemeli**.
2. **BL-046 / donma** — Geçmiş'te gecikmeyle kapanmış bir kalemin sayısını not al; **ertesi gün aynı sayı**
   olmalı. (Aynı şey ağ yanıtından da okunabilir: kalem `closedAt` taşıyor mu?)
3. **BL-046 / rozet duruyor** — gecikmeyle kapanmış kalem hâlâ **"{N}g gecikmeyle kapandı"** demeli; rozet
   silinmiş olmamalı.
4. **BL-046 / eski veri** — `CompletedAt`/`CancelledAt` taşımayan eski bir kapanmış kalem varsa: **sayısız**
   "Gecikmeyle kapandı" demeli, uydurulmuş bir sayı değil.
5. **BL-045** — İşlerim'de "SLA riski" çipini aç: segment sayaçları **değişmeli** ve toplamları çipin sayısına
   **eşit** olmalı. Bekleyen'e geç: satır sayısı o segmentin sayacına eşit, çip hâlâ **3**.
6. **BL-045 / sekme** — çip açıkken üstteki sekme rozetleri **değişmemeli**.
7. **l10n** — sayfayı 7 dilde aç; yeni üç ifade hiçbirinde ham anahtar (`SlaClosed…`) olarak görünmemeli.

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "ClosedAt" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs
rg -n "ClosedAt" services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Services/WorkItemProjectionService.cs
rg -n "CLOSED_AT_" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js
rg -n "daysLateAtClose" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/mock-data.js
rg -n "facetItems" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js
rg -n "SlaClosedOnTime" frontend/Diten.Web/Resources/Views/WorkCenterNext/   # 7 dosya
cd frontend/Diten.Web && npx vitest run tests/workcenter-next-sla-closed-freeze.test.js tests/workcenter-next-faceted-counters.test.js
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~TaskHandoverTests"
```

*(Not: depoda `tests/strategy-*`, `tests/planning-cycles-*`, `tests/objectives-edit-hydration` altında **9 test
bu turdan ÖNCE de düşüyordu** — dokunulmamış ağaçta ölçüldü, bu dilimle ilgisi yok.)*

#### ⚠️ KAPANIŞ (KISMİ) — BL-047 (ikinci yarı) · BL-052 — 2026-08-10 — **CANLI DOĞRULAMA BEKLİYOR**

> **Tek dilim, çünkü ayrılırlarsa yeni ekran İngilizce doğardı.** Sıra bağımlılığı gerçek çıktı: tekrarlama
> ekranının tablosu, dil paketi teslimat yolu olmadan aynı `No data available in table` ile doğacaktı.

**1) BL-047'nin ikinci yarısı — yönetim ekranlarının DataTable dili**

**Karar: (b)**, ve gerekçesi. Dil paketi `dt-defaults.js`'in KENDİSİNE, tek bir ortak payload'dan
(`Views/Shared/_DataTableL10n.cshtml`, iki layout da render ediyor) bağlandı.
(a) — her yönetim sayfasının dahil ettiği bir kısmi görünüm — **reddedildi**, çünkü *"her sayfa hatırlamak
zorunda"* kusurun ta kendisi: WorkCenterNext hatırladı, Alan Tanımları hatırlamadı, depoda **61 dosya**
DataTable kuruyor. Yerel çözüm üçüncü ve dördüncü örneği davet ederdi.

- Payload **geç** okunuyor ve önbelleğe alınıyor: `dt-defaults.js` layout'ta bir `<script>`; erken okunsaydı
  iki etiketin sırası çevirileri sessizce geri alırdı.
- Öncelik BL-047'nin kuralı: **sayfanın kendi sözcüğü kazanır**, anahtar anahtar. Bir anahtarı ezen sayfa
  diğer beşini kaybetmiyor.
- Kaynağı olmayan anahtar **yazılmıyor** (`IsResourceNotFound`): eksik kayıt kendi adını basar, ki bu
  yerini aldığı İngilizceden beter olurdu.

**⛔ DERSİN UYGULANDIĞI YER — testin iddiası TÜKETİCİNİN sözlüğünde.** Yedi iddianın hepsi
`DtDefaults.create()` üzerinden geçiyor — 61 sayfanın çağırdığı gerçek fonksiyon — ve DataTables'a
gerçekten verilecek `language` nesnesini okuyor. *"Payload'da anahtar var"* kanıt sayılmadı; ekran
İngilizceyken de doğruydu.
*Bir vacuity yakalandı ve düzeltildi:* "render edilen config'te İngilizce yok" iddiası **ekran tamamen
İngilizceyken de geçiyordu** (İngilizce cümleler vendor'ın kendi varsayılanları, config'e hiç yazılmıyor).
İddia **doluluk** üzerinden yeniden yazıldı: boş bırakılan her slot, İngilizcenin geldiği yerdir.

**2) BL-052 — yinelenen görev kuralı ekranı**

Desen icat edilmedi: Alan Tanımları ekranı kopyalandı (golden DataTable + tam sayfa CRUD + inline filtre +
save-view). **Sunucu farklı olduğu için** iki bilinçli sapma var: toplu-silme ucu olmadığından **bulk bar
yok** (olmayan uca bağlı düğme = başarısız düğme), ve sıklık kolonu `interval` ile birlikte *"Haftalık · Her 2"*
diye okunuyor — kuralı yazan kişinin seçtiği cümle budur.

**"Kendim" iki yerde birden yasak.** Form seçeneği sunmuyor **ve** model reddediyor
(`RecurrenceAssignmentTargetInvalid`) — gizli bir `<option>` bir devtools düzenlemesi uzaktadır, tarayıcının
GÖNDERDİĞİ değer tek gerçektir. Sunucu zaten `allowSelfAssigned: false` ile reddediyordu; bu ön kontrol
hatayı kullanıcının dilinde söyletiyor.

**Bitiş tarihi boş = süresiz**, ve bu bir cevap olarak gösteriliyor: listede boş hücre değil *"Süresiz"*,
detayda da öyle. Boş hücre eksik veri gibi okunur.

**Şablon bağlama gerçek oldu.** Şablonları listeleyen bir uç **yoktu** — depo `ListActiveAsync` ile
listeleyebiliyordu, hiçbir şey açığa çıkarmıyordu. Doldurulamayan bir seçici, kimsenin okumadığı bir payload
ile aynı sınıftır; bu yüzden `GET /api/v1/tasks/lookups/task-templates` + Diten.Web proxy'si **aynı dilimde**
eklendi.

**Menü + yetki — ve neden yeni bir anahtar.** Kural uçları `Read/Create/Update/Delete` ile korunuyordu;
dördü de `PersonalWorkSurfaceScoped` içinde, yani menüde görünen bir sayfa Görev Merkezi'ni **parçalardı**
(manifest testinin yasakladığı şey). Yeni anahtar `platform.tasks.recurrence-rules.manage` eklendi, beş
kural ucu + şablon lookup'ı ona taşındı ve sayfa manifest'e `IsNavigationVisible: true` ile kaydedildi.
**Kimseyi kilitlemez:** bu uçlara bugüne kadar hiçbir ekrandan ulaşılamıyordu, taşımak için doğru an tam
olarak buydu. Ekran menü kaydı olmadan da doğrudan URL ile çalışır — menü onu **bulunur** yapar, erişilir
değil.

**KIRMIZI kanıtı (vacuity değil):**
- BL-047b: düzeltmeden önce **4 test düştü**; kalan 3'ü (payload yokken İngilizce varsayılan korunuyor,
  bozuk JSON tabloyu düşürmüyor, sayfanın kendi sözcüğü kazanıyor) o anda da geçiyordu.
- BL-052: düzeltmeden önce **7 test düştü**; geçen 3'ü kasıtlı non-vacuity (geçerli kural kabul ediliyor,
  bitiş tarihi boş kabul ediliyor, olmayan sayfa 404).
- Manifest: sayfa eklenmeden önce **2 test düştü** (menü kaydı + "her yetki anahtarının bir manifest evi
  olmalı" kapısı).

**7 dil:** yeni ekranın **46 anahtarı × 7 dil**, parite doğrulandı.

**BU TURDA YAPILMAYANLAR — açıkça:**
- **Canlı doğrulama yok.** Tam yığın (Gateway + Platform + Diten.Web + oturum) ayağa kaldırılmadı.
- **`platform.tasks.recurrence-rules.manage` yetkisi canlıda hiçbir role verilmiş değil.** Katalog→Auth
  senkronu Alan Tanımları anahtarında çalıştığı için aynı yolu izlemesi bekleniyor, ama **ölçülmedi**;
  aşağıdaki 6. adım tam olarak bunu ölçüyor. 403 alınırsa kusur budur, ekran değil.
- **WorkCenterNext'in kendi `Dt*` tohumlaması silinmedi.** Artık gereksiz (ortak yol aynı değerleri
  taşıyor, sayfa sözlüğü zaten kazanıyor) ama BL-047 testleri onu sabitliyor; kaldırılması kendi turunu
  ve kendi kırmızısını hak eden ayrı bir adım.
- **Sweep hâlâ konfigürasyona bağlı:** `BackgroundJobs:RegisterStandardJobs` ve
  `EnabledJobs["Diten.Platform.MOD-0024.TaskRecurrenceSweepJob"]` ikisi de açık değilse kural kaydedilir
  ama hiçbir şey üretmez. Ekranın kusuru değil, ama 5. adımda görülecek şey budur.

**CANLI DOĞRULAMA ADIMLARI (CT):**
1. **BL-047b** — Türkçe `/Tasks/FieldDefinitions`: tablo alt bilgisi, sayfalama ve boş-tablo metni
   **Türkçe**. *"No data available in table"* ve *"Showing 0 to 0 of 0 entries"* **hiç görünmemeli**.
   Konsolda: `JSON.parse(document.getElementById('datatable-l10n').textContent)` → **6 anahtar**.
2. **BL-047b / yayılım** — aynı kontrol rastgele iki yönetim ekranında daha (ör. bir platform tarafı
   tablosu): dil paketi artık layout'tan geldiği için hepsi Türkçe olmalı.
3. **BL-052 / ekran** — `/Tasks/RecurrenceRules` **200**; tablo ve *"Kural Ekle"* düğmesi gelmeli.
4. **BL-052 / form** — "Kime" listesinde **yalnız** *Bir kişiye* ve *Bir havuza* olmalı; **"Kendim" YOK**.
   Kişi seçilince kişi seçicisi, havuz seçilince pozisyon seçicisi görünmeli (diğeri **temizlenmeli**).
   Bitiş tarihi **boş bırakılıp** kaydedilebilmeli.
5. **BL-052 / liste** — kaydedilen kural listede *"Aylık"* (veya *"Haftalık · Her 2"*), bitişi boşsa
   **"Süresiz"**, hiç üretmediyse **"Henüz yok"** demeli.
6. **BL-052 / yetki** — 3-5 adımları 403 verirse: `platform.tasks.recurrence-rules.manage` role verilmemiş
   demektir. Erişim Yönetimi'nde anahtarın **var olup olmadığına** bak (manifest self-registration onu
   oluşturmalı), sonra role ver.
7. **BL-052 / menü** — Görevler altında *"Yinelenen Görev Kuralları"* girdisi görünmeli; görünmüyorsa
   sebep 6. adımdır, ekran değil.
8. **l10n** — yeni ekranı 7 dilde aç; hiçbirinde ham anahtar (`RecurrenceRulesTitle` gibi) görünmemeli.

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "datatable-l10n" frontend/Diten.Web/Views/Shared/                      # partial + iki layout
rg -n "sharedL10n|dtText" frontend/Diten.Web/wwwroot/assets/js/dt-defaults.js
rg -rl "ecurrence" frontend/Diten.Web/Views frontend/Diten.Web/wwwroot/assets/js   # artık BOŞ OLMAMALI
rg -n "RecurrenceManage" services/Diten.Platform/src                          # sabit + 6 uç + manifest
rg -n "lookups/task-templates" services/Diten.Platform/src frontend/Diten.Web/Controllers
cd frontend/Diten.Web && npx vitest run tests/datatable-language-one-delivery-path.test.js
dotnet test frontend/Diten.Web.Tests --filter "FullyQualifiedName~TaskRecurrenceRuleScreenTests"
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~TaskManifestProviderTests"
```

*(Not: `tests/strategy-*`, `tests/planning-cycles-*`, `tests/objectives-edit-hydration` altındaki **9 test bu
turdan ÖNCE de düşüyordu** — dokunulmamış ağaçta ölçüldü, bu dilimle ilgisi yok.)*

### BL-050 — 🔴 Devretme kişi seçicisi yanlış alanı okuyor: seçilemez liste
- **Belirti (canlı, 2026-07-31):** Devretme diyaloğu iki kişiyi doğru gösteriyor (*Agent Sub*,
  *Diten Admin*) ama **her `<option>`'ın `value`'su boş**. Kullanıcı bir kişi seçse bile
  `assigneeUserId` boş kalıyor, doğrulama devreye giriyor ve diyalog *"Görevin kime devredileceğini
  seçin."* diyor — **seçmiş olan kullanıcıya seçmediğini söylüyor.** Hiçbir ağ çağrısı yapılmıyor.
- **Kök neden:** `app.js:3885` `person.id` okuyor; sunucu DTO'su
  `AssignablePersonDto(Guid UserId, string? DisplayName, …)` (`TaskModels.cs:462-463`), yani
  JSON'da alan **`userId`**. `person.id` diye bir alan yok → `undefined` → boş `value`.
  Ad doğru geliyor çünkü `displayName` doğru okunuyor; **kusuru gizleyen şey tam olarak bu** —
  liste dolu ve sağlıklı görünüyor.
- **Aynı depoda doğrusu zaten iki yerde yazılı:** `app.js:2256` → `person.userId || person.id` ·
  `assets/js/Tasks/form.js:226` → `option.value = row.userId;`
- **Neden sözleşme testi yakalamadı:** `task-transition-contract.test.js` `TRANSITION_BODIES`
  haritasını ve `assignablePeople()` çağrısının **varlığını** karşılaştırıyor; seçicinin **lookup
  DTO'sunun alan adını** hiç okumuyor. Yani BL-043'te kurulan iki-taraflı sözleşme testi geçiş
  gövdelerini kapsıyor, **lookup gövdelerini kapsamıyor.**
- **Bu, BL-043'ün kendisiyle aynı kusur sınıfı:** bir değer iki yerde yaşıyor, sözleşme hiçbirini
  bildirmiyor, sessizce kayıyor. Düzeltme yalnız `person.id → person.userId` değil; **lookup
  DTO'ları da aynı iki-taraflı teste alınmalı**, yoksa bu üçüncü kez olur.
- **Yeniden ölçüm:** `rg -n "person\.id|row\.userId|person\.userId" frontend/Diten.Web/wwwroot/assets/js` ·
  canlı: devretme diyaloğunu aç, `document.getElementById('wcnReassignAssignee').value` boş mu?


#### ✅ KAPANIŞ — `bb82b4f8` · 2026-07-31 · *(kod; canlı tur CT'de)*

**Ne yapıldı.** `AssignablePersonDto` `userId` gönderiyor; seçici `person.id` okuyordu — olmayan bir
alan. `undefined` şablon dizesinde **boş dize** olarak render olur, hata fırlatmaz: her `<option>`
`value=""` aldı, doğrulama kullanıcının her seçimini reddetti, hiçbir istek gitmedi. Ad doğru
görünüyordu çünkü `displayName` doğru okunuyordu — kusuru gizleyen tam olarak buydu.

**Kararlar ve gerekçeleri:**
- **Tek okuma noktası:** `personUserId(person)` (`app.js`). Depoda doğru cevap **zaten iki yerde**
  yazılıydı (`app.js`'in kendi oluşturma formu, `Tasks/form.js`) ve üçüncüsü yine yanlış gönderildi.
  Bir olgunun üç yazılışı bu kusuru üreten koşuldur; artık bir tane var.
- **`|| person.id` yedeği kaldırıldı.** Olmayan bir alanın savunmacı okuması, `person.id`'yi makul
  gösteren şeydi. Yedek kalsaydı kusur "çalışıyor gibi" görünmeye devam ederdi.
- **Asıl iş tek satır değil, sözleşme testinin genişletilmesi.** `task-transition-contract.test.js`
  **istek gövdelerini** iki taraftan okuyor — BL-043 bu yüzden bir daha kayamaz. BL-050 ise bir
  **yanıt** alanı, o yüzden testten geçti. Aynı iki-taraflı yöntem lookup'lara uygulandı: sunucu
  record'unun alanları ve istemcinin `<option>` **değer** okumaları ikisi de dosyadan ayrıştırılıyor,
  hiçbiri elle yazılmıyor. Kapsam: `AssignablePersonDto` (app.js + Tasks/form.js) ve
  `AssignablePositionDto` (Tasks/form.js).
- **Tarama `<option>` değerine daraltıldı**, her `person.X` okumasına değil. Geniş tarama ilk denemede
  ilgisiz `person.name/role/status`'u ve `form.js`'in iki farklı lookup için kullandığı aynı `row`
  adını yakalayıp gürültü üretti. Kusur sınıfı **değerin kendisi**; orada `undefined` sessizce `""`
  olur. Ayrıca taramanın gerçekten bir şeye baktığını kanıtlayan bir vacuity testi var.

**KIRMIZI→YEŞİL kanıtı (düzeltmeden ÖNCE ölçüldü):**
```
× no <option> takes its value from a field no lookup DTO declares
  → app.js builds an <option> value from id, which no lookup DTO declares: expected ['id'] to deeply equal []
```
düzeltmeden sonra: `Tests  22 passed (22)`.

**KASTEN yapılmayanlar:**
- **`Tasks/form.js` değiştirilmedi** — `row.userId` / `row.positionId` zaten doğru. Doğru olanı
  "tek noktaya taşımak" için değiştirmek, çalışan kodu kanıtsız riske atmak olurdu; test onu
  **kapsıyor**, yani kayarsa yakalanır.
- **Canlı tur koşulmadı** — servis başlatma CT'de.

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "person\.id" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js
rg -n "personUserId" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js
cd frontend/Diten.Web && npx vitest run tests/task-transition-contract.test.js
```
Canlı: devretme diyaloğunu aç → kişi seç → onayla. Doğrulama uyarısı **çıkmamalı**, ağ çağrısı
**gitmeli**, görev yeni kişide `pendingAcceptance` belirmeli.

#### 🔬 CT CANLI DOĞRULAMASI — `4e111132` sonrası, 2026-07-31

| Ölçüm | Sonuç |
|---|---|
| Seçenek değerleri | ✅ `Agent Sub → 93bcb22e-…` · `Diten Admin → 11111111-…` — artık dolu |
| Kişi seç → Onayla | ✅ doğrulama uyarısı yok, diyalog kapandı |
| Gönderilen gövde | ✅ `{expectedVersion, assigneeUserId, reason}` → **204** |
| Görev benim listemden çıktı | ✅ İşlerim 13→12, projeksiyondan düştü |
| Sunucu durumu | ✅ `assigneeUserId` = Agent Sub |
| **Ama:** devredilen görev yeni sahibinde **Gelen Kutusu'na düşmüyor** | 🔴 **BL-051** |

`return` canlı koşulamadı — mevcut hiçbir kalem `return` aksiyonu sunmuyor
(`(x.actions||[]).some(a => a.code === 'return')` → boş). Sözleşme testi gerçek C# record'unu
okuyarak kapsıyor, ama uçtan uca doğrulanmadı; kayda böyle geçiyor.

### BL-051 — 🔴 Kabul kapısı devretme/iadede yeniden AÇILMIYOR: BL-042'nin ürettiği regresyon
- **Belirti (CT canlı, 2026-07-31):** kabul edilmiş bir görev (`sasasa`, `owned/admitted`) Agent Sub'a
  devredildi, sonra bana geri devredildi. Sonuç: **`owned/admitted`** — yani görev doğrudan İşlerim'e
  düştü, Gelen Kutusu'na hiç uğramadı. Beklenen: `assigned/pendingAcceptance`.
- **Kök neden — BL-042 anlamı taşıdı, taşıyanları güncellemedi.** `AcceptedByUserId` depoda **tek bir
  yerde** yazılıyor (`TaskItemTransitionHandlers.cs:73`, accept) ve **hiçbir yerde temizlenmiyor**
  (`rg -n "AcceptedByUserId\s*=" services/Diten.Platform/src` → 1 sonuç). Kapıyı yeniden açmayı
  amaçlayan üç handler ise hâlâ **eski sinyali** sıfırlıyor:
  - `:1020` `ReassignTaskItemHandler` — *"Unaccepted on arrival: the acceptance gate reopens…"* 🔴 **canlı doğrulandı**
  - `:887` `ReturnTaskItemHandler` — *"…the acceptance gate reopens…"* 🔴 aynı desen (canlı koşulamadı, `return` sunulmuyor)
  - `:151` `ReleaseTaskItemHandler` — havuz dalı `AssigneeUserId is null`'dan projekte ettiği için bugün
    görünür bir kırılma üretmiyor; yine de bayat alan geride kalıyor.
  Eski kuralda `Lifecycle = Open` yazmak kapıyı **gerçekten** açıyordu (`IsAccepted = Lifecycle not
  (Open|Planned)`). Yeni kuralda (`IsAccepted = AcceptedByUserId is not null`) aynı satır hiçbir şey
  yapmıyor. **Üç yorum artık kodun yapmadığı bir şeyi iddia ediyor.**
- **Neden testler yakalamadı:** `TaskAssignmentResolverTests` **resolver'ı** ölçüyor, handler'ları değil;
  `task-transition-contract.test.js` **istek gövdelerini** ölçüyor, durum geçişini değil. Aradaki boşluk
  tam olarak "handler yeni sinyali doğru yazıyor mu" sorusu — hiçbir test bunu sormuyor.
- **Güvenlik/yetki boyutu:** devredilen iş, alan kişinin kabulü olmadan onun iş listesine giriyor.
  Kabul kapısı yalnız UX değil, **sorumluluğun devredildiği an**; SAP/Oracle'da da iş kabul edilene
  kadar devredene aittir.
- **Düzeltme yönü (CT):** tek satır `AcceptedByUserId = null` eklemek **yetmez** — bu, aynı sınıfın
  dördüncü tekrarını davet eder. Kabul kapısını açma/kapama **tek bir yerden** yapılsın (ör. domain
  üzerinde `ReopenAcceptanceGate()` / `CloseAcceptanceGate()`), üç handler da onu çağırsın, ve
  **handler seviyesinde** test edilsin: "devret → yeni kişide pendingAcceptance", "iade et → talep
  edende pendingAcceptance". Bayat üç yorum da düzeltilsin.
- **Yeniden ölçüm:** `rg -n "AcceptedByUserId\s*=" services/Diten.Platform/src` (accept + kapı açıcılar
  görünmeli) · `rg -n "Lifecycle = TaskLifecycle.Open" services/…/TaskItemTransitionHandlers.cs` ·
  canlı: kabul edilmiş bir görevi devret, karşı tarafta `admissionState` `pendingAcceptance` olmalı.


#### ✅ KAPANIŞ — `8579df87` (kod) · CT canlı doğrulaması 2026-08-01

> **Kapatan ölçüm.** Kabul edilmiş `sasasa` Agent Sub'a devredildi, sonra geri devredildi →
> `assigned/pendingAcceptance` (düzeltmeden önce aynı ölçüm `owned/admitted` veriyordu). Ardından
> kabul → `owned/admitted`: kapı açılıp **kapanıyor** da. Toplu regresyon yok — dağılım devretmede
> tam **bir** kalem kaydı (`admitted 12→11 · pending 2→3`) ve kabulden sonra tabana döndü; fazla
> hevesli bir reopen 12 görevi birden Gelen Kutusu'na dökerdi, dökmedi. Havuz üstlen↔bırak ×2 temiz.
> Üç kapı regresyonu 409.
>
> **CT notu — ajanın istenenden fazlası:** `AcceptedByUserId` setter'ı **private** yapıldı, yani kural
> belgeye değil **dile** yazıldı; doğrudan atama yapan mevcut bir testi derleyici anında yakaladı. Bu
> kusur sınıfının dördüncü tekrarı artık unutulamaz, çünkü alana erişilemiyor.

**Ne yapıldı.** Kabul kapısı artık **tek bir yerden** hareket ediyor: `TaskItem.CloseAcceptanceGate()` /
`ReopenAcceptanceGate()`. `accept` kapatıyor; `reassign` · `return` · `release` açıyor.
`AcceptedByUserId` setter'ı **private** — kapıyı doğrudan alan ataması ile oynatan kod artık
**derlenmiyor**. Üç bayat yorum, kodun gerçekte yaptığını söyleyecek şekilde düzeltildi.

**Kararlar ve gerekçeleri:**
- **Tek satır `= null` eklenmedi.** BL-042 ve BL-051 aynı kusurun iki tekrarı: *bir olgu, birden çok
  yazar, biri unutulmuş.* Dördüncü tekrarı davet etmemek için kapı bir **alan** değil, adıyla niyeti
  söyleyen bir **işlem** oldu. Private setter bunu derleyiciye zorlatıyor — ve buna rağmen testle de
  iddia ediliyor, çünkü "bir anlığına public yapayım" üçüncü tekrarın geleceği yol tam olarak budur.
- **`release` de kapsandı.** Havuz dalı `AssigneeUserId is null`'dan projekte ettiği için bugün
  görünür bir kırılma üretmiyor. Bırakılma gerekçesi değil, tam tersi: **görünmeyen bayat alan**, bir
  sonraki projeksiyon değişikliğinde kimsenin izini süremeyeceği bir kusura dönüşür.
- **Yorumlar kusur sayıldı.** Üçü de "the acceptance gate reopens" diyordu; kod bunu yapmıyordu.
  Kodun yapmadığını iddia eden yorum, bir sonraki okuyucuyu yanlış yöne gönderir.

**Test boşluğu — asıl iş.** Mevcut iki test **zaten** `…lands in the … inbox unaccepted` adını
taşıyordu ve baştan sona geçti: ikisi de **hiç kabul edilmemiş** bir görevden başlıyor, dolayısıyla
"sonrasında hâlâ kabul edilmemiş" **kendiliğinden doğru**. Yeni testlerin hepsi **KABUL EDİLMİŞ** bir
görevden başlıyor ve alanı değil **projeksiyonu** ölçüyor (kullanıcının gördüğü şey o).
Eklenen dördüncü test ters yönü koruyor: kabul edilmiş görev kabul edilmiş kalmalı — fazla hevesli
bir reopen, aynı büyüklükte bir kusurdur.

**KIRMIZI→YEŞİL kanıtı (üç `ReopenAcceptanceGate()` çağrısı kaldırılarak, 0 derleme hatasıyla):**
```
[FAIL] An_ACCEPTED_task_released_to_the_pool_leaves_no_acceptance_behind
[FAIL] An_ACCEPTED_task_that_is_reassigned_waits_in_the_new_holders_inbox
[FAIL] An_ACCEPTED_task_that_is_returned_waits_in_the_requesters_inbox
Başarısız: 3, Başarılı: 15, Toplam: 18
```
Geri alındıktan sonra: `18/18` · tüm Platform paketi `2059/2059`.

**KASTEN yapılmayanlar:**
- **Canlı tur koşulmadı** — servis başlatma CT'de.
- **BL-042 `⚠️ KISMİ` kalıyor.** BL-051 onun ürettiği regresyondu; kabul akışının bütünü canlıda
  doğrulanmadan `✅`'e dönmez.

**Yeniden ölçüm (sayı değil, komut):**
```
rg -n "AcceptedByUserId\s*=" services/Diten.Platform/src        # yalnız entity içi olmalı
rg -n "ReopenAcceptanceGate\(\)" services/Diten.Platform/src    # 3 handler
dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~TaskHandoverTests"
```
Canlı: kabul edilmiş bir görevi devret → **yeni kişide Gelen Kutusu'nda, `pendingAcceptance`**.
Sonra iade et → talep edende `pendingAcceptance`.

### BL-052 — 🟠 Yinelenen görev kuralının ekranı yok: motor çalışıyor, kullanıcı erişemiyor
- **Ölçüm (CT, 2026-08-01):** Faz 4 motoru **tam** — `TaskRecurrenceRule` (Günlük/Haftalık/Aylık/
  Çeyreklik/Yıllık + `Interval` + `StartsAt`/`EndsAt` + isteğe bağlı `TaskTemplateId`),
  `TaskRecurrenceSweepJob` **saatte bir** koşuyor ve dönem başına **tam bir kez** üretiyor, beş CRUD
  uç noktası ve Diten.Web proxy'si yerinde (`Controllers/TasksController.cs:155-173`).
  **Ama ekran yok:** `rg -rl "ecurrence" frontend/Diten.Web/Views` → boş ·
  `rg -rl "recurrence" frontend/Diten.Web/wwwroot/assets/js` → boş.
- **Bugünkü sonuç:** kullanıcı yinelenen kural **oluşturamıyor**; yalnız API çağırarak yapılabiliyor.
  Aynı fazda Alan Tanımları'na yönetim ekranı yapıldı, buna yapılmadı — **kapanış kaydı bu boşluğu
  hiç anmadı**, çünkü o zaman kayıt "motor bitti"yi ölçüyordu, "kullanıcı yapabiliyor mu"yu değil.
  (Demir kural #10'un yeni eşiği tam olarak bunun için var.)
- **Kopyalanacak desen hazır:** Alan Tanımları ekranı (`Views/Tasks/FieldDefinitions`) aynı şekle
  sahip — golden DataTable + tam sayfa CRUD formu.
- **⚠ SIRA BAĞIMLILIĞI — GÜNCELLENDİ (CT ölçümü 2026-08-10):** iki engel vardı, **biri kalktı**.
  - *Menü engeli kalktı:* yönetim ekranı **doğrudan URL ile çalışıyor** — `GET /Tasks/FieldDefinitions`
    → **200**, tablo ve "Tanım Ekle" düğmesi geliyor. Yani `TASKS` yetkilendirmesi ekranı
    **erişilemez yapmıyor**, yalnız menüde **bulunamaz** yapıyor. Yeni ekran yetkilendirme
    beklemeden yapılabilir **ve canlı doğrulanabilir**.
  - *Dil engeli duruyor ve KANITLANDI:* aynı ekranda **"No data available in table"** ve
    **"Showing 0 to 0 of 0 entries"** yazıyor. BL-047 dil paketini yalnız **WorkCenterNext**
    payload'ına bağladı; yönetim ekranları kendi sayfaları olduğu için almıyor. Ajanın ölçtüğü
    "61 dosya DataTable kuruyor, hiçbiri paketi beslemiyor" tespitinin **canlı ikinci örneği**.
  - **Sonuç:** tekrarlama ekranı, yönetim ekranlarının dil paketiyle **aynı dilimde** yapılmalı;
    ayrı yapılırsa İngilizce metinlerle doğar.

### BL-053 — 🟡 İzleyiciler rollü katılımcı listesine dönüşsün (RACI'nin "danışılan"ı)
- **Nereden çıktı:** sahibin create prototipinde ayrı bir **"Danışman (Consultant)"** bölümü var;
  bizde yalnız düz bir **İzleyiciler** listesi mevcut (`Views/Tasks/_Form.cshtml` `taskWatchers`).
- **Kavramın adı RACI:** Responsible · Accountable · **Consulted** · Informed. Sahibin "Danışman"ı
  bunun **C**'si. Oracle Fusion Projects'te "Project Team Members" listesi üyelere **rol** verir;
  SAP tarafında iş akışı "involved parties" rolleriyle aynı işi görür. **Hiçbirinde ayrı bir alan
  değil** — katılımcı listesindeki bir rol.
- **Yön (CT):** düz izleyici listesini **rollü katılımcı listesine** çevir: *İzleyici · Danışman ·
  Onaylayan*. Böylece ileride "Bilgilendirilen" istendiğinde yeni alan açılmaz, rol eklenir.
- **Neden bugün yapılmadı:** küçük değil. Projeksiyonu (katılımcı + rol), detay sayfasını ve
  bildirim hedeflemesini etkiler; kendi dilimini hak ediyor. Ayrıca **onaylayan** rolü MOD-0023'ün
  karar verdiği kişiyle karışmamalı — MOD-0024 onayı raporlar, karar vermez.
- **Yeniden ölçüm:** `rg -n "taskWatchers|watchers" frontend/Diten.Web/Views/Tasks/_Form.cshtml` ·
  projeksiyonda `watchers` alanının şekli.

### BL-054 — 🟠 Görev şablonu yönetim ekranı yok: yinelenen görev bu yüzden içeriksiz üretiyor
- **Ölçüm (CT, 2026-08-10):** `TaskTemplate` varlığı zengin — `TitleTemplate · DescriptionTemplate ·
  DefaultPriority · DefaultAssignmentTarget · DefaultPoolPositionId · DefaultDueInDays ·
  ChecklistTemplateId · DefaultFieldValues` (`TaskSupportingEntities.cs:222-234`). Sunucuda **yalnız
  okuma** ucu var (`TasksController.cs:449` `lookups/task-templates`); **liste/oluşturma ekranı yok**.
- **Sonucu ölçülü, teorik değil:** `GenerateDueRecurringTasksHandler.cs:212-227` — şablon **yoksa**
  üretilen görev `Title = rule.Name`, `Description = null`, `Priority = Medium`, kontrol listesi yok.
  Arayüzden şablon yaratılamadığı için **her yinelenen kural zorunlu olarak bu dala düşüyor** ve
  başlıktan ibaret görev üretiyor. Kural ekranındaki "Şablon" seçicisinin boş olmasının sebebi de bu
  (`GET /Tasks/api/task-templates` → `200 · data: []`).
- **Yön (CT):** Alan Tanımları / Yinelenen Kural ekranlarının deseni (golden DataTable + tam sayfa form).
  Kontrol listesi bağlama bu ekranın en kritik parçası — şablonun asıl değeri orada.
- **Bu madde BL-056'nın ÖN KOŞULU.**

### BL-055 — 🟡 Yinelenen kural pasif doğuyor; listeden duraklatma yok
- **Ölçüm (CT canlı):** formdan oluşturulan kural `isActive: false` doğuyor → listede **Pasif**, ve
  pasif kural hiçbir şey üretmiyor. Yani "kaydedildi" diyen ama çalışmayan bir kural — bu turda beş
  kez düzelttiğimiz *"başarı raporlayıp bir şey yapmama"* deseninin aynısı.
- **Sahip kararı (2026-08-10): AKTİF doğsun.** Kural oluşturmak zaten "bunu istiyorum" demektir.
- **İkinci istek (sahip):** satır aksiyonlarına **Duraklat / Devam ettir** eklensin. Bugün aksiyonlar
  `Görüntüle · Düzenle · Sil`; bir kuralı geçici durdurmak için forma girip kutu kaldırmak gerekiyor.
  Geçici durdurma sık yapılan iştir, tek tık olmalı.

### BL-056 — 🟡 Görev oluşturma formuna "Tekrarlama" alanı (⚠ BL-054'ten SONRA)
- **Nereden çıktı:** sahibin create prototipinde sağ rayda `⟳ Tekrarlama · Tekrarsız ▾` var; bizde yok.
- **⛔ SIRA ŞARTI — ve gerekçesi sahibin kendi gözleminden çıktı:** *"create ekranında çok fazla veri var,
  ama tekrarlama kuralında bu kadar veri yok."* Doğru tespit. Kullanıcı create'te 15 alan doldurup
  "her ay tekrarla" derse, o içeriğin yaşayacağı yer **şablondur**; şablon ekranı olmadan gelecek ayki
  görev **başlıktan ibaret** doğar (BL-054'teki ölçüm). Yani bu alan BL-054'ten önce eklenirse
  **veri kaybettiren bir özellik** olur.
- **Kurumsal emsal (CT):** SAP FI tekrarlayan kayıtlar **referans belge + çevrim**; SAP PM bakım planları
  **bakım kalemi + planlama**; Oracle tekrarlayan yevmiye **şablon + zamanlama**. Hepsi *içerik nesnesi +
  zamanlama nesnesi* ayrımı yapıyor — bizim `TaskTemplate` + `TaskRecurrenceRule` ikilimiz bu desenin
  aynısı. "Öğenin kendisine tekrarla tiki" deseni hafif araçlarda (Outlook, Jira) var, ERP'lerde yok.
- **Yön:** kutu, ne yaptığını **söylesin**: *"Seçersen bu görevin içeriği şablon olarak kaydedilir ve
  her dönem yeniden üretilir."* Kural yine **tek doğruluk kaynağı** kalır; yönetim/durdurma kural
  ekranından yapılır. Yeri: sağ rayın en altı (nadiren kullanılır, ana akışı bölmemeli).

### BL-057 — 🔴 TEMEL: şirket (Legal Entity) kapsamı örtük; açık hâle gelmeli
- **Ölçüm (CT, 2026-08-10):** zincir kurulu — `TaskItem.OrganizationUnitId` **zorunlu**
  (`TaskItem.cs:86`) → `OrganizationUnit.LegalEntityId` **zorunlu**
  (`Organization/OrganizationUnit.cs:9`). Yani **her görev bir şirkete bağlı**.
  Ama listeleri süzen şey şirket değil: `TaskWorkItemProvider` → `ListByAssigneeAsync(userId)` ve
  `ListUnclaimedByPositionsAsync(pozisyonlarım)`.
- **Bugün doğru sonuç veriyor, ama tesadüfen:** MG'de çalışan biri GMPO pozisyonu tutmadığı için
  GMPO havuzunu göremiyor. Şirket ayrımını sağlayan **pozisyon sahipliği**, açık bir şirket filtresi değil.
- **Bunun üç sonucu:**
  - *(a)* İki şirkette birden pozisyonu olan kişi ikisini de görür — muhtemelen doğru, ama **bilinçli
    karar olmalı**, yan etki olarak kalmamalı.
  - *(b)* **Şirket seçici yok.** Kullanıcı hangi şirket adına çalıştığını seçmiyor, pozisyonlarından
    örtük geliyor. SAP'de "company code" seçilir. Tek şirketli kullanıcı için sorun değil; çok
    şirketlide "şu an hangi şirketteyim?" sorusu cevapsız.
  - *(c)* **Şirkete göre raporlama üretilemez** — *"MG'nin bu ay açılan tüm görevleri"* diye bir görünüm
    bugün yazılamaz; görev şirketi taşıyor ama hiçbir sorgu ona bakmıyor.
- **BL-023 (Ekibim) ile doğrudan kesişiyor:** yönetici görünümü gelince "hangi ekip" sorusunun cevabı
  şirket sınırını da tanımlamak zorunda. Bugün kimse sormadığı için sorun görünmüyor.
- **⚠ NEDEN ERTELENMESİ PAHALI:** diğer maddeler ekran işi, bu **temel**. Sonradan eklenen bir kapsam
  kuralı, o zamana kadar yazılmış **her sorguyu** yeniden gözden geçirmeyi gerektirir.
  ([[feedback_defer_regression_assessment]] — ertelenen her madde gelecekteki regresyon riskini de beyan eder.)
- **Formda kararlaştırılması gerekenler:** `SelfAssigned` **yasal değil** (kuralda atama zorunlu —
  arka plan işinin "kendi"si yoktur; gerekçe `TaskSupportingEntities.cs:251-260`'ta yazılı) ·
  şablon seçimi isteğe bağlı mı zorunlu mu · `EndsAt` boş bırakılabilir mi (süresiz kural).
- **Yeniden ölçüm:** `rg -rl "ecurrence" frontend/Diten.Web/Views frontend/Diten.Web/wwwroot/assets/js` ·
  canlı: menüden yinelenen kural oluştur, bir sonraki süpürmede görev üretiliyor mu.

### BL-058 — 🟡 Şablon ve yinelenen kural, zorunlu yapılandırılabilir alanı dolduramıyor
- **Ölçüm (CT, 2026-08-10, bu turda açıldı):** zorunlu alan artık **iki tarafta da** tutuluyor — form boş
  bırakılan zorunlu alanda kaydı engelliyor, sunucu da alanı **hiç göndermeyen** isteği reddediyor
  (`TaskFieldDefinitionService`, `TASK_FIELD_VALUE_INVALID`). Ama **iki makine yolu** bu kuralın dışında
  bırakıldı: `CreateTaskItemFromTemplateHandler` ve `GenerateDueRecurringTasksHandler`
  (`EnforceRequiredFields: false`, gerekçesi çağrı yerinde yazılı).
- **Neden dışarıda bırakıldı:** süpürmenin soracağı kimse yok. Zorunlu kılınsaydı değer toplanmaz,
  **tekrarlama sessizce dururdu** — dönem yine tüketilirken. Bu hata bu handler'da atama için bir kez
  yapıldı ve düzeltildi; aynısı alanlar için tekrarlanmadı. Şablonda ise ekran yok: zorunlu alan tanımlanmadan
  **önce** yazılmış her şablon kullanılamaz hâle gelirdi ve düzeltileceği bir yüzey yok.
- **Gerekenler:** (a) şablon editörü yapılandırılabilir alan varsayılanlarını sunsun
  (`TaskTemplate.DefaultFieldValues` **zaten var**, editörü yok); (b) yinelenen kural ekranı aynısını taşısın;
  (c) ikisi de doldurduğunda `EnforceRequiredFields: false` **kaldırılsın** — bayrak, eksikliğin adıdır.
- **⚠ Regresyon riski (ertelenirse):** 🟢 additive. Bayrak tek yerde, iki çağrı yerinde adlandırılmış;
  kaldırılması şema değişikliği istemiyor. Ama **erteledikçe sessiz kalıyor**: şablondan üretilen görev bugün
  zorunlu alanı boş taşıyabiliyor ve bunu kimse görmüyor.
  ([[feedback_defer_regression_assessment]])
- **Yeniden ölçüm:** `rg -n "EnforceRequiredFields" services/Diten.Platform/src` → bugün 4 isabet
  (tanım + 1 okuma + 2 opt-out). Canlı: zorunlu bir alan tanımla, şablondan görev üret → 201 dönüyor ve
  görev alanı boş; form aynı alanı boş bırakınca kaydettirmiyor.

### BL-044 — 🟡 Türkçe büyük harfle arama sıfır sonuç veriyor
- **Ölçüm:** `kapanış` → 1 eşleşme ✅ · `KAPANIŞ` → **0** ❌ · `kapanis` (aksansız) → 0.
- **Kök neden:** `app.js:372,374,391,397` **invariant** `toLowerCase()` kullanıyor. `'I'.toLowerCase()` noktalı
  `'i'` veriyor; metindeki harf noktasız `'ı'`. Yani içinde I/ı geçen her Türkçe kelime büyük harfle aranınca kaybolur.
- **Neden gerçek bir kullanım:** caps lock ve mobil otomatik büyük harf sıradan; kullanıcı "arama bozuk" der, nedenini bilemez.
- **Yön (CT):** iki tarafı da **yerelden bağımsız katlama** ile normalize et (NFD ile aksan ayır + I/İ/ı/i'yi ortak forma indir).
  Bu tek değişiklik aksansız aramayı da (`kapanis`) çözer. `toLocaleLowerCase('tr')` **yanlış yol** — 7 dilli üründe diğer dilleri bozar.

### BL-045 — 🟡 Sinyal çipi sayacı sekme kapsamlı, liste segment kapsamlı
- **Ölçüm:** İşlerim'de çip *"SLA riski **3**"* diyor, tıklanınca **2** satır süzülüyor. Üçüncüsü
  (`Yeni maliyet merkezi açılış talebi`) **Bekleyen** segmentinde. Çip etkinken segment sayaçları
  (Aktif 9 · Bekleyen 1 · Planlı 1) **hiç değişmiyor** → kullanıcı kaybolan kalemi bulamıyor.
- **Neden tasarım gereği böyle:** sayaçlar sekme içi filtreleri yok sayıyor (`app.js:343` yorumu bunu beyan ediyor),
  liste ise segment + çip + arama süzgecini uyguluyor.
- **Karar gerekiyor:** (a) çip sayacını **aktif segmente** daralt — sayı ile liste her zaman tutar, ama sinyal
  başka segmentte saklıysa görünmez olur; (b) çip etkinken **segment sayaçlarını yeniden hesapla** (faceted search
  davranışı — SAP/Oracle worklist'lerinin yaptığı), kullanıcı "3'ün 1'i Bekleyen'de" bilgisini görür.
  **CT önerisi: (b)** — sinyal, segmentten bağımsız bir eksendir; onu segmente daraltmak aks yasasını sinyal aleyhine bozar.

### BL-046 — 🟡 Kapanmış görevler Geçmiş'te canlı SLA sayacı gösteriyor
- **Ölçüm:** Geçmiş sekmesinde *"Haziran KDV beyannamesini gönder · **Tamamlandı** · 11g gecikmiş"*.
  Sayaç bugüne göre işlemeye devam ediyor; yarın "12g gecikmiş" olacak.
- **Neden yanlış:** biten iş gecikmez. SAP/Oracle worklist'lerinde kapanmış kalem **tamamlanma tarihini**
  ve varsa "son tarihi X gün aştı" **donmuş** ölçüsünü gösterir, ilerleyen bir sayaç değil.
- **Yön (CT):** terminal durumda SLA çipi ya tamamlanma tarihine dönmeli ya da kapanış anındaki değere donmalı.
  Karar noktası: gecikmeyle kapanmış iş için "geciken kapanış" rozeti raporlamada değerlidir — silmek yerine dondurmak yeğ.

### BL-047 — 🟡 Tablo görünümünde DataTable bilgi metni İngilizce
- **Ölçüm:** Türkçe sayfada tablo altında **"Showing 1 to 9 of 9 entries"**.
- **Kapsam:** yalnız görünen metin değil — sayfalama, arama kutusu ve boş-tablo metinleri de aynı l10n paketinden gelir.
- **Neden kapı görmedi:** l10n kapısı resx dosyalarını denetliyor; bu metin **vendor bileşeninin kendi paketinden**
  geliyor, resx'te hiç görünmüyor. `[[feedback_tenant_l10n_seven_langs]]` kuralının göremediği bir sınıf.
- **Yön (CT):** tenant tarafındaki her DataTable için dil paketi bağlanmalı — **7 dil**. Tek sayfalık düzeltme değil,
  bir kural: yeni tablo eklendiğinde paketi bağlanmamışsa İngilizce sızar.

### BL-048 — 🟢 Sunucu doğrulama mesajı Türkçe, alan adı ham İngilizce
- **Ölçüm:** 224 karakterlik başlıkla oluşturma → `400 · "'Request Title', 200 karakterden küçük veya eşit olmalıdır. 224 karakter girdiniz."`
  Cümle çevrilmiş, alan adı (`Request Title`) çevrilmemiş — FluentValidation property adını olduğu gibi basıyor.
- **İlgili:** BL-040 (sebep kodu köprüsü). Kod taşınırsa alan adı da frontend tarafında çevrilebilir hale gelir;
  bu madde BL-040 çözülünce **kendiliğinden** kapanabilir — ayrı iş açmadan önce oraya bakılmalı.

### BL-049 — 🟢 Görev detayında ham GUID gösteriliyor
- **Ölçüm:** "KAYNAK BAĞLAMI → Kaynak kaydı `31a44983-40cc-4252-ac49-3fa2766e4014`". Hemen altında zaten
  **"Kaynak kaydını aç"** bağlantısı var, yani GUID kullanıcıya hiçbir yetenek kazandırmıyor.
- **Neden kayda değer:** SAP/Oracle'da kaynak kaydı **iş anahtarıyla** (belge numarası) gösterilir; teknik kimlik
  destek/hata ayıklama içindir, ana yüzeyde değil.
- **Yön (CT):** ya iş anahtarı göster, ya da GUID'i destek amaçlı bir yere (kopyala düğmesi / geliştirici katmanı) taşı.

---

## WorkCenter ön-koşulları (seam register — WorkCenter branch'ından ÖNCE karar/stub)

> **DURUM GÜNCELLENDİ (2026-07-31 mutabakatı) — BEŞ SEAM DE KURULDU.** Bu bölüm uzun süre "Bu branch'te YAPILMIYOR" diyordu; ölçüm beşinin de shipped olduğunu gösterdi. Kayıt kodun gerisinde kalmıştı.
>
> | Seam | Durum | Kod |
> |---|---|---|
> | **WC-1** Birleşik Work-Item kontratı | ✅ | `Features/WorkAggregation/Providers/IWorkItemProvider.cs` (`866bcbf3`) |
> | **WC-2** Çalışma-zamanı / takvim seam'i | ✅ | `Features/WorkAggregation/Services/IWorkingTimeCalculator.cs` + `WorkItemSlaCalculator` (`be0cc190`) |
> | **WC-3** Assignee çözümleme seam'i | ✅ | `Features/Tasks/Services/ITaskAssignmentResolver.cs` |
> | **WC-4** Notification seam'i | ✅ | `Features/Tasks/Services/TaskNotificationService.cs` (`7e7e8c40`) |
> | **WC-5** Görev-kaynağı kaydı | ✅ | `IWorkItemProvider` kayıt yolu, 2 sağlayıcı DI'da (`DependencyInjection.cs`) |
>
> Aşağıdaki özgün gerekçeler **tarihsel kayıt** olarak duruyor — seam'lerin neden bu şekilde kurulduğunu anlatıyorlar. "YAPILMIYOR" ifadesi artık geçersizdir.

- **WC-1 — Birleşik Work-Item kontratı:** WorkCenter çok modülden görev toplar. Bugün `ApprovalTask` (workflow) var; ama workflow-dışı modüller de görev üretecekse tek bir "WorkItem" kontratı (id/tip/başlık/modül/entity-ref/assignee/due/durum/aksiyon) gerekir. Yanlış kurulursa her modül entegrasyonu yeniden yazılır. **En kritik seam.**
- **WC-2 — Çalışma-zamanı / takvim seam'i:** SLA/son-tarih hesabı (`SlaEscalationRule`) koda gömülmesin, bir "çalışma-zamanı" arayüzünden geçsin; şimdilik naive 7/24 dönsün. İleride gerçek çalışma saatleri + tatil (BL: Calendar) sadece arayüzü değiştirir, WorkCenter'a dokunmaz. (Kullanıcıların şirket çalışma saatleri bu seam sayesinde regresyonsuz eklenir.)
- **WC-3 — Assignee çözümleme seam'i:** `GetMyWorkflowTasks` bugün görevi user'a göre çözüyor. İleride position-based ([BL-008]) gelince atama pozisyondan türeyecek → atama bir "assignee resolver" arkasından geçsin ki WorkCenter yeniden yazılmasın.
- **WC-4 — Notification seam'i:** Görev bildirimleri (bell/email) bir arayüzden çıksın; gerçek notification (ertelenmiş/başka ekip) sonradan additive takılsın.
- **WC-5 — Görev-kaynağı kaydı:** Workflow için `WorkflowManifestProvider` var; workflow-dışı modüllerin de WorkCenter'a görev katkısı yapabilmesi için benzer bir kayıt yolu.

---

## Açık kararlar

### DEC-002 — Zaman kaydının sahibi (DCP-003 B2) · **teyit bekliyor, açık tercih değil**
`logTime` ("bu göreve 2 saat harcadım") kaydı **nereye** yazılacak? MOD-0024 bu kaydı asla kendi tutmaz; yalnız bir giriş noktasıdır, kaynağa yazar. Kaynağın kim olduğu bilinmeden buton bağlanamaz.

- **Blueprint yönü ZATEN belli — İK tarafı (MOD-0280):** `execution/registries/module-id-registry.md:41` *"Time Entry SoR stays with Blueprint MOD-0280"* · `execution/domains/portfolio-delivery/domain-config.md:53` PPM, `Project Effort Log`'u **geçici** sahiplenir, MOD-0280 gelince kontrat kurulur ve **gerekirse sahiplik devri** yapılır · `portfolio-delivery/README.md:32` *"Time Entry / devamsızlık / izin SoR'u → MOD-0280"*. Üç kayıt aynı şeyi söylüyor.
- **DCP-003'te 🔴 AÇIK görünmesinin sebebi** SoR'un kim olduğu değil, **PPM tarafındaki C5 Effort Log'un fazı** (`DCP-003:194,230`). MOD-0024 açısından o soru bağlayıcı değil.
- **CT DÜZELTMESİ (2026-07-31):** CT bir ara turda **proje tarafını (MOD-0117)** önerdi — **kayıtları okumadan**, ezberden SAP/Oracle örneğiyle. Geri çekildi. Örnek de yanlıştı: SAP **CATS** bağımsız bir *Cross-Application Time Sheet*'tir, süreyi kaydedip **birden çok alıcıya** (proje · maliyet merkezi · iş emri) dağıtır — projenin sahiplendiği bir kayıt değil, tam olarak MOD-0280 modeli. Ayrıca "proje kaydı daha zengin" savı eksikti: İK zaman modülü **devamsızlık ve izni** de taşır, proje maliyetlendirmesi bunları hiç görmez.
- **MOD-0024 için sonuç (ara düzen):** `logTime` **yapılmaz**, `timeTracking` capability'si **bildirilmez**. MOD-0280 geldiğinde MOD-0024'ün butonu oraya yazar, kendi kaydını tutmaz. Diğer beş eksik aksiyon (`dispute` · `delegate` · `pause` · `replan` · `reject`) bundan **etkilenmez**, sırayla yapılabilir.
- **Sahipten istenen:** yeni bir tercih değil — blueprint'in kararını **teyit** ya da ona **itiraz**. Teyit gelirse bu madde kapanır ve `logTime` Grup D sonrasına alınır.
- **İlgili:** BL-034 (eksik aksiyonlar) · DCP-003 §B2 · `execution/domains/portfolio-delivery/`.

### DEC-001 — "Yakında" disabled action butonları?
BL-001/BL-002'yi **şimdi** disabled satır-action'ı olarak göstermek (yol haritası sinyali) mi, yoksa yapılana kadar hiç koymamak mı?
- **Öneri:** Şimdilik koymamak. Yol haritası sinyali isteniyorsa, disabled **ama açık "Yakında / Coming soon" tooltip'i ile** — böylece bozuk değil kasıtlı okunur. Boş/açıklamasız ölü buton go-live'da anti-pattern (kullanıcı "bozuk mu?" diye bug açar).
- **Durum:** Sahip kararı BEKLİYOR.
