# ERP-vNext — Product Backlog (Deferred / Out-of-Scope-for-Now)

> **Amaç:** Bilinçli olarak **ertelenen** özelliklerin tek kaydı. Her madde bir gerekçe ve bir "ne zaman yapılır" tetikleyicisiyle park edilir — böylece hiçbir şey sessizce unutulmaz ve hiçbir şey vaktinden önce yapılmaz.
> **Sahiplik modeli:** Claude = CONTROL TOWER (prompt yazar, canlı doğrular); yürütme = Antigravity ajanları. **Go-live kapsamı buradaki her şeyi HARİÇ tutar.**
> **Antigravity ajanları için (ZORUNLU):** Buradaki maddeler, onaylı bir module pack açıkça bu backlog'dan çıkarıp `approved`/`ready-for-dev` kapsamına almadıkça **UYGULANMAZ**. Bir backlog özelliğini "yardımcı olayım" diye kendiliğinden inşa etmek YASAKTIR. Bir talep bir backlog maddesine değiyorsa, kod yazmadan önce bu dosyayı referans göster ve module pack kapısına yönlendir.
> **Son güncelleme:** 2026-07-24.

## Nasıl kullanılır
- Bir özellik konuşulup bilinçli ertelendiğinde madde ekle: **ne olduğu**, **neden ertelendiği**, **hangi tetikleyiciyle yapılacağı**, **ilgili modül**.
- Bir maddeyi ancak onaylı bir module pack'e alınıp teslim edildiğinde kaldır/üstünü çiz.

---

## DURUM DİZİNİ (CT, 2026-08-13)

> **Bu dizin bir gezinme aracıdır, otorite değildir.** Bir madde ile bu dizin çelişirse **madde gövdesi**
> doğrudur. Dizin, 80 maddelik dosyada "bugün ne kaldı" sorusunu gövdeleri okumadan cevaplamak için var.
>
> ⚠ **Sınıflandırma otomatik DEĞİL:** aşağıdaki üç grup CT'nin **canlı doğruladığı** maddelerdir. Kalan
> **60 madde gözden geçirilmedi** — "açık" sayılırlar ama bu bir ölçüm değil, varsayımdır. Bir sonraki
> ayıklama turu onları derecelendirmeli (38'inin işareti bile yok: BL-001…BL-041 işaret geleneğinden önce
> yazıldı).

**✅ Bu oturumda KAPANDI — canlı doğrulandı, arşiv adayı**

| Madde | Ne kapandı | Kanıt |
|---|---|---|
| BL-065 | Görev başına bildirim tercihi + son tarih hatırlatması (üç katman: saklama · sözleşme · gönderici) | Süpürme canlı çalıştı, reddedilen gönderim yeniden denendi, ikinci tetikte tekrar göndermedi |
| BL-072 | Aday seçicide sessiz eleme → sayılı ipucu | "1 kişi listelenmedi: 1 kişi kapsamınız dışında" ekranda, isim sızdırmıyor |

**🟠 YARIM — yarısı kapandı, yarısı duruyor. Bir sonraki ayıklamada İKİYE BÖLÜNMELİ**

| Madde | Kapanan | Açık kalan |
|---|---|---|
| BL-057 | Atama/havuz **seçicileri** kapsamla süzülüyor; onaycı listesi bilerek muaf | Liste · Gelen Kutusu · Havuz **ekran** süzmesi · şirket seçici · şirkete göre raporlama |
| BL-023 | "Ekibim" kapsam seçici · yukarı akan iş **talep** oluyor (MOD-0023'e devir) | Talebin **sonucu** Görev Merkezi'nde okunmuyor (rozet/durum yok) |

**🆕 Bu oturumda AÇILDI (16)** — BL-067 · BL-068 · BL-071 · BL-073 · BL-074 · BL-075 · BL-076 · BL-077 ·
BL-078 · BL-079 · BL-080 · BL-081 · BL-099 (+ BL-060…BL-066 aralığındaki daha erken kayıtlar)

**⛔ BİZDE DEĞİL / BLOKE** — sayımdan düşer, beklenen şey madde gövdesinde yazılı

| Madde | Bekleyen |
|---|---|
| BL-067 | BL-054 (görev şablonu ekranı) |
| BL-068 | AuthService'te kullanıcı dili alanı yok |
| BL-071 | Employee modülünü **başka bir geliştirici** yazıyor — bizden çıkan şey **karar notu** |
| BL-075 | MDM isim çözücüsü (tüzel kişi adı Platform'da yok) |
| BL-079 | Kontrol listesi şablon **okuma ucu** yok |
| BL-081 | `_Layout.cshtml` başka bir ekranın işi |

**🔧 YAPISAL KUSUR — dosyanın okunamamasının ASIL sebebi**

`### BL-043` başlığı **709 satır** taşıyor (643→1352) ve içinde **on üç başka maddenin** kapanış kaydı var:
BL-030 · BL-038 · BL-040 · BL-042 · BL-044 · BL-045 · BL-046 · BL-047 · BL-048 · BL-049 · BL-050 ·
BL-051 · BL-052. Kapanışlar `#### ✅ KAPANIŞ — BL-046 · BL-045 — …` gibi alt başlıklar hâlinde orada duruyor.

**Sonucu:** BL-046'ya bakan biri kendi gövdesinde hiçbir durum bulamaz ve maddeyi **açık sanır** — oysa
kapanışı 400 satır ötede, başka bir maddenin altında yazılıdır. Bu, "bir gerçek iki yerde" kusurunun
dosya düzeyindeki hâli: kaydın kendisi doğru, **bulunabilir değil**.

⚠ Otomatik hiçbir tarama bunu göremez; kapanış metni ilgili maddenin gövdesinde OLMADIĞI için her sinyal
taraması o maddeleri "sinyal yok" diye işaretler. Bu dizinin ilk sürümü de tam olarak buna düştü.

**Bir sonraki ayıklama turunun ilk işi:** her kapanış bloğunu ait olduğu maddenin gövdesine taşımak
(ya da maddeye "kapanışı BL-043 altında, tarih X" diye tek satırlık çapa koymak). İçerik SİLİNMEZ —
yalnız doğru başlığın altına gider.

**Ayrıca kayda geçsin:**
- **BL-006 ve BL-014** başlıkları üstü çizili + ✅ TAMAMLANDI (2026-07-11) — bitmiş ama dosyada duruyorlar;
  arşiv bölümünün ilk sakinleri. BL-014 hâlâ *"Commit BEKLİYOR"* diyor, doğrulanmalı.
- **İki madde-dışı blok** madde başlıklarının altına sıkışmış: `## CT test turu — 2026-07-31` (BL-037'den
  sonra) ve `## WorkCenter ön-koşulları` (BL-049'dan sonra). Bunlar madde değil, bölüm.
- **İşaret sözlüğü sandığımızdan geniş:** 🔴 · 🟠 · 🟡 · 🟢 **ve** bileşik `🔴→🟢` (BL-062, BL-066).
  Dört değil beş biçim var; ayıklama turu sözlüğü sabitlemeli.

---

---

## Foundation guardrail'leri (ŞİMDİ uygulanır — ERTELENMEZ)

> Bunlar ertelenen özellik DEĞİL; **bugünden itibaren geçerli mimari kurallardır.** Bedavadırlar (ekstra iş yok) ama uygulanmazsa ileride BL-007/BL-008 eklerken **geriye dönük ayıklama/migration acısı** doğar. Antigravity ajanları ve developer'lar bunlara uyar.

### FG-001 — Legal Entity yalnız KENDİ grubun tüzel kişileridir (iç-only)
- Legal Entity master'ına yalnız senin sahip olduğun/kontrol ettiğin grup şirketleri girer (Grand Medical Group, Monom, GM Polan, Setonda AZ rep-office vb.).
- **Dış taraflar (distributor, müşteri, tedarikçi) Legal Entity'ye ASLA girilmez** → onlar Business Partner master'ının işidir ([BL-007]). Bugün dışarıyı LE'ye sokmak = ileride acılı extraction.
- **Regresyon:** Kural korunursa BP eklemek 🟢 additive; ihlal edilirse 🔴 migration.

### FG-002 — User / Employee / Business Partner üç AYRI kavramdır
- **User** = login/erişim (sisteme giren herkes: iç + dış). **Employee** = yalnız kendi iş gücün (HR). **Business Partner** = dış şirket + kişileri.
- User'ı "employee" yerine kullanma; erişim **daima Role üzerinden** verilir (iç ve dış için çalışır).
- **DÜZELTME (CT, 2026-08-11) — "dış kişi PositionAssignment almaz" YANLIŞTI.** Maddenin önceki hâli
  *"PositionAssignment yalnız kendi Employee'lerin içindir; dış kişi PositionAssignment almaz, doğrudan Role
  ile erişir"* diyordu. Bu **iki farklı "dış"ı** birbirine karıştırıyor ve biri için yanlış cevap veriyor:
  - **Senin işini yapan dış kişi** (danışman, dış avukat, ajans personeli, stajyer) → **WORKER'dır, koltuk ALIR.**
    Bir pozisyonu doldurur, ona iş atanır, havuzundan iş üstlenir, org şemasında görünür.
  - **Ticaret ettiğin dış şirket** (distribütör, müşteri, tedarikçi ve onların kişileri) → **BUSINESS PARTNER'dır,
    koltuk ALMAZ.** Erişimi olacaksa Role ile olur; org şemasına girmez.
- **⚠ ÖLÇÜM — bu proje ZATEN doğrusunu düşünmüş, çelişen taraf FG-002'ydi:**
  `services/Diten.HcmService/src/Diten.HcmService.Application/Features/CoreHrEmployeeMaster/EmployeeReferenceDataContracts.cs:23-28`
  → `WorkerTypes = employee · contractor · intern · consultant · other`. **"contractor" ve "consultant" HCM
  sözlüğünde birinci sınıf worker tipi.** FG-002'nin eski metni onlara koltuk yasaklıyordu; iki kural
  çelişiyordu ve doğru olan HCM tarafı.
- **Kurumsal emsal:** SAP SuccessFactors **"Contingent Worker"** birinci sınıf worker tipidir ve pozisyona
  atanır · Oracle Fusion worker tipleri **Employee / Contingent Worker / Pending Worker / Non-worker**, hepsinin
  assignment'ı olur · Workday'de **Worker = Employee VEYA Contingent Worker**, ikisi de pozisyona yerleştirilir.
- **FG-001 DEĞİŞMEDİ:** dış **ŞİRKET** Legal Entity'ye girmez (Business Partner, [BL-007]). Ahmet'in hukuk
  bürosu şemada yoktur; şemada duran şey Ahmet'in **SİZİN için tuttuğu koltuktur**. Ayrım "kişi mi şirket mi"
  değil, **"benim işimi mi yapıyor, yoksa benimle ticaret mi ediyor"**.
- **Bu bir METİN düzeltmesidir — kod değişmedi.** Sistem bugün de doğru çalışıyor: `PositionAssignment` yalnız
  `UserId` taşır (`Organization/PositionAssignment.cs:8`), worker tipini hiç sormaz, dolayısıyla bir danışmana
  koltuk vermeyi engelleyen bir kod zaten yoktu. Yanlış olan yalnız yazılı kuraldı.
- **Regresyon:** Ayrım korunursa Employee/BP katmanı 🟢 additive; kavramlar karışırsa 🔴 veri ayıklama.
- **İlgili:** [BL-071] (Employee ↔ PositionAssignment çift kayıt — koltuğun sahibi kim, oturanın sahibi kim).

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

**GENİŞLETME (CT, 2026-08-11) — YÖN AYRIMI. Madde altyapıyı yazmıştı, yönü yazmamıştı.**

Hiyerarşi yalnız "kimi görürüm" sorusunu değil, **"ona iş verebilir miyim yoksa ondan iş isteyebilir miyim"**
sorusunu da cevaplar. İkisi aynı zincirden okunur, ama sonuçları farklıdır:

| Hedefin konumu | Ne olur | Neden |
|---|---|---|
| **Aşağı** (astım) veya **yana** (aynı kapsamda) | Doğrudan **ATANIR** | Yönetme yetkisi zaten var; görev emirdir |
| **Yukarı** (üstüm) | Atanmaz — **TALEP** olur | Astın üstüne iş "atayamaz"; isteyebilir. Kayıt MOD-0023'e düşer |

- **Tespit maliyeti sıfır:** yukarı/aşağı ayrımı [BL-057]'nin **(2) raporlama zinciri** ayağıyla **AYNI**
  yürüyüştür. Tek zincir yürüyüşü, iki soru — *"atayabilir miyim?"* (kapsam) ve *"bu yukarı mı?"* (yön).
  İki ayrı mekanizma kurulmaz.
- **UI sonucu:** hedef üstse gönderim düğmesi **"Oluştur" yerine "Talep gönder"** olur ve kart bir satırla
  ne olacağını söyler. Sessizce farklı davranan bir düğme, bu turlarda tekrar tekrar düzelttiğimiz kusurun
  aynısı olur.
- **⛔ SINIR — MOD-0024 kararı VERMEZ:** talep MOD-0023'e devredilir (Binding A). Yerel bir
  `if (isUpward) { … }` dalı bu sınırın ihlalidir; [[project_mod0024_approval_boundary]].
- **ÖLÇÜM — altyapı hazır, veri değil:** `Position.ReportsToPositionId`
  (`Organization/Position.cs:10`) · yürüyüş `GetManagerChainQueryHandler.cs:22-46` (döngü tespiti,
  32 derinlik sınırı, arşiv kontrolü) · aynı yürüyüş ikinci kez
  `OrgDataScopeResolver.AddManagerChainScopesAsync:191-226`. Dev verisinde 11 pozisyondan **2'sinde**
  `ReportsToPositionId` dolu, ve **ikisi de aynı tüzel kişi içinde** kalıyor — yani şirket sınırını geçen
  zincir hiç test edilmemiş. Ayrıntı ve go-live önkoşulu [BL-057]'de.

- **⚠ SIRA DEĞİŞTİ (2026-08-11): BL-023 artık liste UX turundan ÖNCE.**
  **Gerekçe:** BL-023 header'a bir **KONTROL** ekliyor (`Ben ▾ / Ekibim`). UX turu ise kontrollerin nasıl
  görüneceğine karar veriyor. Önce görsel dili kurup **sonra** yeni bir kontrol eklemek, header'ı iki kez
  elden geçirmek demektir — bu, planın kendi *"Create'ten önce listeyi öne al"* gerekçesinin aynısıdır
  (`docs/workcenter-completion-plan.md` § UX tur sırası notu). Maddenin eski yeri (Aşama 4b, "UX turundan
  **sonra**") bu yüzden değişti.
  **Yeni sıra:** 1. kayıt turu → 2. [BL-057] (+ [BL-072] aynı turda) → 3. **BL-023** → 4. liste UX turu →
  5. diğer sayfalar. Sıranın tek kaynağı `docs/workcenter-completion-plan.md`.
---

**✅ HER İKİ PARÇA DA YAPILDI (Parça A 2026-08-11 başlandı, 2026-08-12 EKRANA GELDİ · Parça B 2026-08-12).**

**⚠ ÖNCE BİR HATA KAYDI — Parça A bir kez "tamamlandı" diye raporlandı ama EKRANDA YOKTU.**
Kontrol hiç yazılmamıştı: `Views/WorkCenterNext/Index.cshtml` değişmemişti, dört dile çevrilen metin hiçbir
zaman basılmıyordu. Bunu söylemesi gereken test **boştu** — yalnız resx'te anahtar arıyordu, render yüzeyine
hiç bakmıyordu, dolayısıyla kontrol yokken de yeşildi. Canlı doğrulama da yalnız API ucunu ölçmüştü, ekranı
değil. **Alınan ders, testlere yazıldı:** her iddia RENDER YÜZEYİNİ okur (`app.js` — sayfayı basan yer;
`Index.cshtml` yalnız `#wcnApp` kabuğu ve script etiketleridir). Kanıt: kontrol geçici silindiğinde suite
**kırmızıya dönüyor** (2 test), `TABS_PRIMARY`'ye `team` eklendiğinde eksen testi **kırmızıya dönüyor**.
İkinci hata: `ScopeLabel` anahtarı **yinelenmiş** olarak eklenmişti (zaten vardı) ve parite kontrolü bunu
göremedi çünkü yedi dosya da eşit biçimde yinelenmişti — artık ayrı bir test yinelenen anahtarı yakalıyor.

**Parça A — "Ekibim" kapsam seçici:**
- **Eksen yasası korundu:** `Ekibim` bir SEKME değil. Header'da **zaten var olan** kapsam açılırına eklendi —
  o açılır vekâlet için aynı soruyu (*"kimin işine bakıyorum"*) zaten soruyordu. İkinci bir açılır eklemek
  kullanıcıya iki "Ben ▾" gösterirdi. Sekme dizileri (`TABS_PRIMARY`/`TABS_SECONDARY`) testle çivilendi.
- **İniş TEK YERDE:** `TaskTeamResolver` kendi yürüyüşünü yapmıyor —
  `TaskAssignmentScopeResolver.SubordinatePositionIds` (BL-057'de kurulan iniş) okuyup pozisyonu TUTAN
  kullanıcılara çeviriyor. Kanıt: `rg -n "ReportsToPositionId" .../Features/Tasks` → tek yürüyüş.
- **Kapsam kuralı aynen geçerli:** başka şirketteki ast **görünür** (zincir geçiyor), kapsam dışı **görünmez**.
- **BOŞ DURUM — devre dışı + gerekçe, gizleme değil.** `TaskTeamScope.HasTeam` *"size rapor veren kimse yok"*
  ile *"ekibinizin açık işi yok"*u ayırıyor; ayrı uç (`GET /api/v1/work-items/team-availability`) sorulduğu
  için istemci boş listeden tahmin etmiyor, ve **fail-closed** (ulaşılamayan cevap = ekip yok).
- **Uçlar:** `GET /api/v1/work-items/mine?scope=team` + `…/team-availability` (ikisi de aynı izin — ekibin kim
  olduğu org şemasından gelir ve zaten kapsamlıdır). Proxy parametreyi tanıyıp iletiyor; tanımadığı değer
  `self`'e düşüyor.
- **l10n:** yalnız **2** gerçek yeni anahtar (`ScopeTeam`, `ScopeTeamEmpty`) + `ScopeTeamCount`; `ScopeSelf`
  gereksizdi (mevcut `ScopeMine` "Ben" demek) ve yinelenen `ScopeLabel` geri alındı. 679 → **682**.
- **CANLI ÖLÇÜM (2026-08-12, ikili damgasından sonra başlatılmış süreçler):**
  açılır metni `Kendim / Ekibim / 2 kişi` · etiket `Kendim` → `Ekibim` · liste **2 → 10 satır** · sekme
  sayısı **4**. Boş durum gerçek veriyle ölçüldü (zincir geçici kaldırıldı, sonra geri kondu):
  `hasTeam:false` → seçenek `disabled`, metin *"Size rapor veren kimse yok, bu yüzden gösterilecek bir ekip
  de yok."*, tıklama **yok sayıldı** (satır sayısı değişmedi).
- **Testler:** `TaskTeamScopeTests.cs` (9) · `workcenter-next-team-scope.test.js` (12).

**Parça B — yukarı atama değil, TALEP:**
- **Üçüncü MOD-0023 akışı, icat değil kurulu desen:** `TaskUpwardRequestService`, `TaskReviewService`'i birebir
  örnek alıyor. `RequestObjectType = "task-request"` — `task` (onay) ve `task-review`den ayrı, çünkü her kapı
  kendi nesne tipiyle çalışıyor ve paylaşmak birinin kararını diğerine okuturdu. MOD-0023'ün **hiçbir dosyası
  değişmedi**.
- **YENİ YÜRÜYÜŞ YOK:** tespit, çözücünün zaten ürettiği `EntitlementDataScopeKind.ManagerChain` kapsamını
  (YUKARI yön) olduğu gibi okuyor — `TaskAssignmentScope.ManagerChainPositionIds`. Bu kapsam `Allows()`
  tarafından bilerek okunmuyor: okunsaydı her ast kendi üstüne iş **atayabilirdi**, ki bu maddenin tam olarak
  talebe çevirdiği şey.
- **Zinciri olmayan kişi "yukarı" SAYILMIYOR:** ne ast ne üst olan biri sıradan bir atamadır. *"Ast değil"*i
  *"üst"* saymak her yatay atamayı gereksiz bir talebe çevirirdi; ayrı testi var.
- **⛔ Binding A korundu:** `TaskUpwardRequestService` bir instance **başlatır**, kararı **vermez**. Kaynağa
  karşı yazılmış bir test yerel `Approved`/`Rejected` atamasını ve karar komutlarını yasaklıyor.
- **UI:** hedef üstse düğme *"Oluştur"* → *"Talep gönder"*, ve kart bir satırla ne olacağını söylüyor. Yön
  **sunucudan** soruluyor (`GET .../lookups/assignment-direction/{userId}`) — tarayıcı zinciri türetmiyor, yani
  etiket ile davranış ayrışamaz. Bayat cevap koruması (`upwardCheck`) ve fail-safe varsayılan (ulaşılamayan
  cevap = sıradan "Oluştur") var.
- **CANLI ÖLÇÜM (2026-08-12):** test zinciri kuruldu (`CT Yonetim Kurulu` → CFO'nun üstü, admin CFO'yu tutuyor).
  Astıma seçince `Oluştur` · üstüme seçince **`Talep gönder`** + açıklama görünür · tekrar asta dönünce
  `Oluştur`. Gerçek görev açıldı → `RequestWorkflowInstanceId: 662c91a5-…`, `workflow_instances` içinde
  `ObjectType: "task-request"`, `ObjectId` = görev id, `Status: 1`; `task-upward-request` şablonu otomatik
  kurulup **yayınlandı**.
- **Testler:** `TaskUpwardRequestTests.cs` (10) · `tasks-upward-request.test.js` (13).
- **l10n:** `ActionSendRequest`, `UpwardRequestHint` — 7 dil, kümeler özdeş (144 → 146).

- **⛔ AÇIK KALAN:** talebin KABUL/RET sonucunun Görev Merkezi'nde okunması. Bugün link duruyor ve MOD-0023
  kararı veriyor, ama projeksiyon `RequestWorkflowInstanceId`'yi henüz bir rozet/durum olarak göstermiyor —
  reddedilen talebin `TaskApprovalView.Resolve` yolundan `Cancelled` okunması ayrı bir dilim.
- **İlgili:** spec §7 v1.5 (team scope) · BL-016 (Outbox) · [BL-057] (kapsam kuralı — bu maddenin önkoşulu) · MOD-0288 Organization (Position/OrgUnit hiyerarşisi) · DCP-004 (Task Center kişisel yüzey ilkesi).

### BL-024 — Yapılandırılabilir alanlarda alan-seviyesi yetki (businessContext Faz 2)
- **Nedir:** Görev formundaki yapılandırılabilir alanlar (Faz, İş Türü, Pazar/Ülke, Domain, Maliyet vb.) iki katmanlı olacak: **Faz 1 = alan tanımı** (hangi alanlar var — tenant/modül bazlı), **Faz 2 = alan yetkisi** (hangi alanı kim görür/yazar — rol/pozisyon bazlı). Örnek: "Maliyet" alanı yalnız yöneticiye görünür. Executable kontrat bunu ZATEN destekliyor: `classification`, `accessState`, `redacted` (yetkisiz değer tarayıcıya hiç gönderilmez, CSS ile saklanmaz).
- **Neden ertelendi:** Alan-seviyesi güvenlik başlı başına bir iş (tanım UI'ı + değerlendirme + test matrisi). Faz 1 alan tanımıyla create dilimi çalışır hale gelir; yetki additive eklenir (kontrat hazır olduğu için regresyonsuz).
- **Yapım tetikleyicisi:** MOD-0024 create dilimi Faz 1 shipped olduktan sonra; ayrı onaylı kapsam.
- **İlgili:** `fixture-contract.js` (VALUE_TYPES + redaction invariant) · MOD-0024 create pack · MOD-0018 RBAC/ABAC.
- **2026-08-10 — Faz 1 genişledi, Faz 2 aynı yerde duruyor:** alan tanımı artık **üçüncü** bir seçenek kaynağı tanıyor — `ModuleRecord`, yani **başka modülün kayıtları** (SAP check table · Oracle table-validated value set · ServiceNow reference field). Kaynak sözleşmesi `ITaskRecordSource`, ilk iki kaynak organizasyon birimi ve pozisyon. Bu madde **etkilenmedi**: `Classification`/`DefaultAccessState` kayıt kaynaklı alanlarda da tanımdan değere kopyalanıyor, hiçbir yetki kararı verilmiyor. Faz 2 geldiğinde kayıt seçicinin de **sunucuda** kısılması gerekir — gizlenmiş bir alanın seçicisi hâlâ o modülün kayıtlarını listeler, ve o uç `TaskPermissions.Read` ile açık. Yani Faz 2'nin kapsamına **bir uç daha** girdi: `GET .../field-definitions/{code}/records`.

- **✅ FAZ 2 YAPILDI (2026-08-13) — dört katman, her biri ayrı kanıtlı.**
  - **MOD-0018 ÖLÇÜMÜ (yeni motor kurulmadı):** `IDataScopeResolver` **satır** seviyesi (`OrgUnit · Position ·
    ManagerChain · LegalEntity`) — alan kavramı yok, uzatma noktası yok. `RolePermission`'da üçüncü boyut yok.
    **Kritik bulgu:** rol GUID'i Platform'a **hiç ulaşmıyor** (`JwtTenantAuthorizationContext.RoleIds` sabit
    boş; yalnız rol ADI geliyor), pozisyon da token'da yok. Dolayısıyla "rol/pozisyon bazlı" kural bağlanacak
    bir kimlik bulamazdı. Kural bu yüzden **izin anahtarına** bağlandı: MOD-0018'in zaten bastığı tek para
    birimi. Tanım gereksinimi söyler, kimin karşıladığına MOD-0018 karar vermeye devam eder.
  - **Tüketilen seam:** `PermissionClaimEvaluator` (canonical + legacy-alias çift okuma, `[HasPermission]` ile
    aynı). Yeni `IActorPermissionContext` yalnız bir **soru yüzeyi**; tek uygulaması API katmanında, o
    değerlendiriciyi çağırıyor. Infrastructure'da ham `PermissionKeys` okumak alias genişletmesini atlar ve
    aynı controller'daki uçtan farklı davranırdı.
  - **Katman 1 — tanım:** `TaskFieldDefinition.ViewPermission` / `EditPermission` (null = kısıtsız, bu yüzden
    deploy hiçbir şeyi karartmıyor). Create/update **isteklerine** eklendi ve canlı doğrulandı.
  - **Katman 2 — okuma:** değer sunucuda kesiliyor, **iki** yolda birden (Tasks detay + Görev Merkezi
    projeksiyonu). Tel formatı **ölçülerek** seçildi: kontrat `REDACTED_VALUE_MUST_BE_OMITTED`'i yazıldığından
    beri doğruluyordu ama DTO'da `redacted` alanı yoktu — kural uygulanabilir ve erişilemezdi. Artık
    `redacted: true` + değer YOK. Etiket gidiyor: sır içerik, varlık değil.
  - **Katman 3 — seçenek ucu:** gizli alanın `options`/`records` ucu **403** (`TASK_FIELD_ACCESS_DENIED`).
    404 değil — tanımın varlığı sır değil, `GET field-definitions` zaten listeliyor.
  - **Katman 4 — yazma:** yetkisiz alana elle konan değer **reddediliyor**. Okuma kısıtı yazma kısıtı DEĞİL:
    ayrı anahtar, ayrı test. Okuma yazmanın **tabanı** (göremediğini yazamazsın) ama yerine geçmez.
  - **⚠ CANLI DOĞRULAMANIN YAKALADIĞI GERÇEK HATA — sessiz VERİ KAYBI.** Redaction + full-replace tek başına
    zararsız, birlikte öldürücü: `UpdateTaskItemRequest` `FieldValues`'ı toptan değiştiriyor ve alanı GÖREMEYEN
    çağıran değeri hiç almadı — yani sıradan bir "başlığı değiştir" gidiş-dönüşü alanı **eksik** geri gönderip
    **siliyordu**. 204, hata yok, iz yok, saldırgan yok. Servis düzeltilmişti ve `UpdateTaskItemHandler`
    `existing` argümanını **hiç geçmiyordu**; birim testleri yeşildi çünkü servisi doğrudan çağırıyorlardı.
    Yalnız gerçek HTTP gidiş-dönüşü gösterdi. Handler seviyesinde test eklendi ve mutasyonla kanıtlandı.
  - **Önbellek yok:** tanım her istekte okunuyor, kural saf fonksiyon → tanım değişikliği **bir sonraki
    istekte** geçerli (canlı ölçüldü: kısıtla → değer null, kaldır → değer geri geldi).
  - **⚠ AÇIK KALAN — İZİN DEĞİŞİKLİĞİNİN GECİKMESİ (bu madde çözmez):** izinler login'de JWT'ye basılıyor ve
    iptal kanalı yok (`AccessTokenExpirationMinutes: 120`). Yani *tanım* anında etkili, ama *kimin izni olduğu*
    token yenilenene kadar eski. Platform geneli bir özellik; **BL-082**'ye ayrıldı.
  - **TESLİM EDİLMEYEN:** (a) alan-tanımı EKRANINDA iki izin anahtarını girecek kontrol — uçlar hazır, form
    alanı yok, dolayısıyla kural bugün yalnız API'den kurulabiliyor; (b) `redacted` bayrağının tarayıcıda
    "gizli" olarak GÖSTERİLMESİ (bugün alan boş görünüyor, "yetkiniz yok" demiyor); (c) iki gerçek kullanıcıyla
    ekran doğrulaması — ikinci kullanıcının parolası yok, bu yüzden yetkisiz taraf **API seviyesinde gerçek
    oturumla** ve tanımı kimsenin tutmadığı bir anahtara bağlayarak ölçüldü. Ekrandan görülmedi; bu üçü
    **BL-083**'te.

### BL-082 — 🟡 İzin değişikliği 120 dakikaya kadar eski kalıyor (JWT'de iptal kanalı yok)
- **Ölçüm (2026-08-13, BL-024 Faz 2 sırasında):** izinler ve roller login'de access token'a basılıyor
  (`TokenService.cs`: `permission` ve `ClaimTypes.Role` claim'leri), `AccessTokenExpirationMinutes: 120`
  (`Diten.AuthService.Api/appsettings.json`). **Token-version, iptal listesi veya yenileme kanalı yok** —
  `SelfAccessExplainResponse` bunu zaten açıkça yazıyor.
- **Sonucu:** bir roldeki izin geri alındığında kullanıcı o izni **iki saate kadar** kullanmaya devam eder. Bu
  alan-seviyesi yetkiye özgü değil; **her** `[HasPermission]` ucu aynı gecikmeyi taşıyor. BL-024 Faz 2 bunu
  tüketiyor, üretmiyor.
- **Seçenekler:** (a) kısa access token + refresh (en küçük değişiklik, en çok tur) · (b) token-version claim +
  kullanıcı başına sürüm sayacı (iptal anında geçersizleşir) · (c) izinleri token'dan çıkarıp istek başına
  okumak (en doğru, en pahalı).
- **Gelecek regresyon riski: 🔴 foundation.** Hangi seçenek olursa olsun her serviste doğrulama yolunu etkiler.

### BL-083 — 🟡 Alan-seviyesi yetkinin EKRAN yüzeyi (tanım formu + "gizli" göstergesi)
- **Ölçüm (2026-08-13):** BL-024 Faz 2 kuralı uçtan uca çalışıyor ama **ekranda kurulamıyor ve
  okunamıyor**: (a) alan-tanımı formunda `ViewPermission`/`EditPermission` girişi yok — kural yalnız API ile
  kuruluyor · (b) `redacted: true` telde geliyor ve hiçbir yüzey onu göstermiyor; kullanıcı yetkisi olmayan
  alanı **boş** görüyor, "gizli" değil — boş bir alanla saklanmış bir alan aynı görünüyor · (c) iki gerçek
  kullanıcıyla ekran doğrulaması yapılmadı (ikinci kullanıcının parolası CT'de yok).
- **Neden ayrı:** (a) bir yönetim ekranı işi (izin anahtarı seçici — sabit liste değil, MOD-0018 kataloğundan),
  (b) yedi dilde metin + kart tasarımı, (c) bir ortam/kimlik işi. Üçü de güvenlik kuralının kendisi değil.
- **Gelecek regresyon riski: 🟢 eklemeli** — sunucu kararı zaten veriliyor, ekran onu yalnız gösterecek.

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

#### ✅ KAPANIŞ — yapılandırılabilir alanlar: **başka modülün kayıtları** — `be7918ed` · CT canlı doğrulaması 2026-08-10

> **Uçtan uca ölçüldü.** `"Fin"` yazıldı → `GET .../delivery.department/records?term=Fin` →
> `{value:"967a6cd5-…", label:"Finans", secondary:"OU-FIN"}` → liste tek satıra daraldı → kaydet **201**
> → sunucuda **kimlik** saklandı (ad değil) → düzenlemede **"Finans — OU-FIN" dolu geldi**.
> Sözleşmenin en kritik iddiası tuttu: **ekranda ad+kod, veritabanında kimlik.**
>
> **Hata yolları:** olmayan kayıt kimliği → `400 TASK_FIELD_VALUE_INVALID` · olmayan tanım →
> `404 TASK_FIELD_DEFINITION_UNKNOWN`.
>
> **İŞ 1 doğrulandı:** kaynak anahtarı artık açılır liste (`Yok · Platform listesi · İş referans
> verisi · Başka modülün kayıtları` → `Organizasyon birimleri · Pozisyonlar`) **ve düzenlemede
> seçili geliyor** — ajanın ölçemediği madde buydu.
>
> **Arapça/RTL doğrulandı:** `dir="rtl"`, başlık ve yer tutucular Arapça, ham anahtar yok,
> **`—` ayıracı bozulmadı** (`Finans — OU-FIN`). *Ekrandaki "Faz/Pazar/Departman/Regulatory"
> çeviri değil **tenant içeriğidir**; çevrilmemeleri doğru davranış.*
>
> **CT'nin ölçmediği tek madde:** 5000 kayıt. İki kaynak da bellek içi süzüyor — ajan bunu bilinçli
> sınır olarak beyan etti ve sözleşmenin parçası olmadığını yazdı. Asıl sınav ürün modülüyle.
>
> **Ajanın kendi turunda bulduğu kusur, kayda değer:** `/Tasks/Create` ve `/Edit`, `form-page.js`'in
> çağırdığı `premium-modal.js`'i **hiç yüklemiyordu**. 201 döndükten sonra sayfa ne bildirim veriyor
> ne yönlendiriyordu — tur tekrar Kaydet'e bastı ve **tek niyetten iki görev** çıktı. Bu, SOP K5'in
> ("arz düzeldi ≠ teslimat oldu") ders kitabı vakası. Düzeltildi ve *"sayfa kendi betiğinin
> çağırdığını yüklüyor mu"* testiyle sabitlendi.
>
> **UX turuna devredilen gözlem (kusur değil):** aramalı alanda arama kutusu ile seçim listesi ayrı
> ayrı duruyor (etiket → arama → liste); çalışıyor ama iki alan gibi okunuyor.

> **Desen icat edilmedi.** SAP'ın *check table + F4 search help*'i, Oracle'ın *table-validated value set*'i,
> ServiceNow'ın *reference field*'ı aynı cümleyi söylüyor: **alanı yönetici tanımlar, değerleri başka modül
> sahiplenir.** Uygulanan budur; `TaskFieldOptionsSourceKind` üçüncü üyesini aldı: `ModuleRecord`.
>
> **⛔ ZOR OLAN KOD DEĞİLDİ, SÖZLEŞMEYDİ — ve önce o yazıldı.** `TaskRecordDto(Id · Code · Name · Secondary?)`:
> kimlik · iş anahtarı · görünen ad · isteğe bağlı ikincil satır. `Id` **string**, çünkü anahtarları GUID
> olmayan bir modül sözleşmeyi bozmamalı; yolun hiçbir yerinde parse edilmiyor, yalnız sahibi yorumluyor.
>
> **Kaynak koda gömülü değil.** `ITaskRecordSource` uygulamak KAYDIN TAMAMI: genişletilecek `switch` yok,
> eklenecek anahtar listesi yok. `TaskRecordSourceRegistry` konteynerdeki uygulamalardan kurulur, her tüketici
> ona sorar. Ürün modülü geldiğinde `DependencyInjection.cs`'e **bir satır** + yedi resx satırı; başka hiçbir
> dosya değişmez. İki modül aynı anahtarı isterse **açılışta patlar** — kazananı kayıt sırası belirlerdi.
>
> **Tek çözüm yolu, üç kaynak türü.** Kayıt araması ayrı bir uca konmadı: `term`/`ids`/`take` **ortak sorguda**,
> kısa kaynaklar bunları zaten ellerindeki listeye uyguluyor. İkinci yol, ikinci kaynağın sözleşmeden çıktığı
> yerdir — WC-1 dersi.

**İŞ 1 — kaynak anahtarı artık seçiliyor (CANLI DOĞRULANDI)**

Serbest metin kutusu gitti; `GET .../field-definitions/option-sources?kind=` besliyor. Ekranda ölçüldü:
tür *"Başka modülün kayıtları"* → anahtar listesi **Organizasyon birimleri · Pozisyonlar**; tür
*"Platform listesi"*'ne çevrildiğinde liste **Ülkeler · Para birimleri · Diller · Saat dilimleri** oldu ve
**eski seçim temizlendi** (taşınsaydı, kaydedilen ama hiç çözülmeyen bir eşleşme olurdu — aynı kaybolan alan,
başka yoldan). Koruma kaldırılmadı: çözülemeyen kaynak hâlâ alanı düşürüyor; **kaldırılan, oraya varma yolu.**

Kaynak adları **önek** ile geliyor (`OptionSource.<key>`, `GetAllStrings`), listeyle değil — yeni kaynak
`_Form.cshtml`'i düzenletmiyor. Platform tenant resx taşımadığı için kaynak **kararlı anahtar** taşıyor, sözcükler
yedi dosyada: nav l10n köprüsünün aynısı.

**İŞ 2 — aramalı seçici, saklanan kimlik**

`Reference` + `ModuleRecord` → `record` kontrolü: arama kutusu + seçim listesi. Açılır liste **değil**, çünkü
beş bin kayıt `<option>` listesine dökülmez; kullanıcı yazar, **sunucu arar** (250 ms debounce + sıra numarası:
"Kal" için geç gelen yanıt "Kalite"nin üstüne yazmaz). Saklanan **kimlik**, gösterilen **ad + iş anahtarı** —
ham GUID hiçbir yerde yok (BL-049).

**Bir alan tipi kısıtı bilerek eklendi:** `ModuleRecord` yalnız `Reference` üstünde. Sayı/tarih üstünde sunucu
değeri reddedeceği için form kontrolü **hiç üretmiyor** — sunulan tek şey bir ret olurdu.

**Değer artık gerçekten kontrol ediliyor** (check table'ın asıl işi): önceden `Reference` yalnız *GUID gibi
görünmek* zorundaydı, herhangi bir GUID geçiyordu. Şimdi kaynağa çözülüyor; çözülmezse
`400 · TASK_FIELD_VALUE_INVALID`. Tanım yazarken de: kayıtsız kaynak → `400 · FIELD_OPTION_SOURCE_INVALID`,
çünkü kaydedip bir daha göremeyen yöneticinin elinde teşhis kalmıyor.

**İŞ 3 — iki kaynak, tek yol (ürün YOK)**

Organizasyon birimleri ve pozisyonlar. İkisi de aynı rotadan, aynı şekilde:
```
organization-unit → {value: <id>, label: "Finans",  secondary: "OU-FIN"}
position          → {value: <id>, label: "CFO",     secondary: "P-CFO · Finans"}
```
Pozisyonun ikincil satırı **org birimini** taşıyor — iki tesisin ikisi de "Kalite Uzmanı" tutabilir, etiketsiz
girdi işi yanlış tesise gönderir; `GetTaskAssignmentPositionLookupHandler`'ın kuralı, aynı sözcüklerle.
Her ikisi de **bellek içi** filtreliyor (yüzlerce kayıt): bilinçli ve sınırlı bir seçim, sözleşme değil —
`SearchAsync` terimi ve tavanı aldığı için büyük tablolu bir kaynak ikisini kendi sorgusuna iter.

**DOĞRULAMA — testin kusuru yakaladığı KANITLANDI, sayı yazılmadı**

Yeni JS paketi **önce 12 kırmızı** ile yazıldı (`customFieldControlKind` `record` yerine `null` dönüyordu).
Backend'de yeni tip için kırmızı derleme hatasından ibaret olurdu, o yüzden **mutasyonla** kanıtlandı — her biri
tek başına çalıştırıldı ve testleri düşürdü:

| Mutasyon | Ne kırıldı |
|---|---|
| kimlik yerine iş anahtarı saklansın | 3 test |
| kayıt varlık kontrolü atlansın | 2 test |
| iki modül aynı anahtarı alabilsin | 1 test |
| terim kaynağa gitmesin (sonradan filtrele) | 1 test |
| saklanan kimlik çözümü kaldırılsın | düzenleme turu |
| arama kutusu bağlanmasın | sunucu-arar turu |
| çözülemeyen kaynak boş seçici versin | gizleme turu |
| `choice.secondary` yeniden adlandırılsın | BL-050 muhafızı |
| yönetici seçicisi olmayan alanı okusun | BL-050 muhafızı |

**Yeniden ölçüm:**
```bash
cd frontend/Diten.Web && npx vitest run tests/tasks-record-fields.test.js tests/tasks-record-fields-round-trip.test.js tests/task-transition-contract.test.js
cd services/Diten.Platform && dotnet test --filter "FullyQualifiedName~TaskModuleRecordFieldTests"
```

**BL-050 iki-taraflı muhafızı genişletildi.** `TaskFieldOptionDto` üçüncü alanını (`Secondary`) kazandı ve
`TaskFieldOptionSourceDto` yeni. İkisinin de okuma yerleri kayıt listesine girdi — biri etiket biçimleyicisinin
içinde, diğeri element kuran yardımcının içinde, ikisi de eski `option.value =` taramasının göremeyeceği yerde.
Ayrıca ayrıştırıcıda sessiz bir kusur düzeltildi: `string? Secondary = null` **"null"** diye okunuyordu, yani
isteğe bağlı parametre adını taşıyan her istemci alanı mazur görülecekti.

**CANLI DOĞRULANDI — uçtan uca, tarayıcıda**

```
tanımla  → Departman (Reference ← organization-unit) · Pozisyon (Reference ← position)
formda   → "EK ALANLAR › Teslimat" · Departman [Aramak için yazın…] + seçici
ara      → "Finans" yazıldı → GET .../records?term=Finans → liste "Finans — OU-FIN"e indi
kaydet   → 201
sunucuda → fieldValues: [{delivery.department, Reference, 967a6cd5-…}]   ← KİMLİK
düzenle  → Departman "Finans — OU-FIN" DOLU geldi, ham GUID YOK
ret      → olmayan kimlik → 400 · TASK_FIELD_VALUE_INVALID
ret      → kayıtsız kaynakla tanım → 400 · FIELD_OPTION_SOURCE_INVALID
```

**CANLI TUR BİR KUSUR BULDU VE DÜZELTİLDİ (bu turun işi değildi, ama turu bloklardı).**
`/Tasks/Create` ve `/Tasks/{id}/Edit` `shared/premium-modal.js`'i **hiç yüklemiyordu**, oysa `form-page.js` her
sonucu `DitenModal` üzerinden bildiriyor. Sonuç: **201 döndükten sonra** `DitenModal.success` fırlatıyor, sayfa
ne bildirim veriyor ne yönleniyor — kullanıcının makul tek hamlesi tekrar Kaydet'e basmak, ve canlı tur tam
bunu yapıp **tek niyetten iki görev** üretti. Hata yolu daha kötüydü: `DitenModal.error` de fırlattığı için
sunucunun sebep kodu hiç kimseye ulaşmıyordu. Script eklendi, test **"sayfa, kendi betiğinin çağırdığını
yüklüyor mu"** biçiminde yazıldı (aynı listeyi kopyalayan üçüncü sayfa da düşsün diye). Bu, 1500+ yeşil testin
göremediği sınıfın bir örneği daha: **canlı doğrulama pazarlık konusu değil.**

**⚠️ NEDEN KISMİ — doğrulanacaklar:**
1. **Türkçe dışında hiçbir dil ekranda görülmedi.** Yedi resx dolduruldu ve parite testi İngilizce kopyasını
   reddediyor (tr/ru/zh karşılaştırması), ama fr/es/zh/ar/ru **ekranda** ölçülmedi. Özellikle **ar** (RTL)
   ve `—` ayıracının Arapça'da nasıl okunduğu.
2. **Düzenlemede kaynak anahtarı** ekranda ölçülmedi: `data-selected` ile taşınıyor ve testte var, ama mevcut
   bir tanımı açıp kaynağın **seçili geldiği** tarayıcıda görülmedi.
3. **"Kayıt bulunamıyor"** yolu ekranda ölçülmedi — yukarı akışta silinmiş bir kayda işaret eden görev
   üretmek gerekiyor.
4. **Beş bin kayıt** denenmedi. Tavan (50) ve arama var, ama iki kaynağın ikisi de yüzlerce satırlık; ilk
   gerçek büyük kaynak geldiğinde bellek içi filtre kendi sorgusuna itilmeli.
5. **`option-sources` yetkisi** yalnız kodda okundu: yönetim izni OLMAYAN bir kullanıcıyla 403 alındığı
   görülmedi.
6. **Kiracıda bırakılan veri:** `delivery.department` ve `delivery.position` tanımları dev kiracısında
   **duruyor** (sahibin kendi turu için bilerek). İstenmiyorsa Alan Tanımları ekranından emekliye ayrılır.
   Turda üretilen görevler silindi.

**Yapılmadı (bilinçli):** ürün modülü diye bir şey kodlanmadı. `Person` tipi kayıt sözleşmesine taşınmadı —
kendi listesini kullanmaya devam ediyor; birleştirmek ayrı bir tur.

---

#### ✅ KAPANIŞ — BL-047 (ikinci yarı) · BL-052 · BL-040 · BL-048 · yapılandırılabilir alanlar — CT canlı doğrulaması 2026-08-10

> **BL-047b — merkezi teslimat doğrulandı.** Payload artık layout seviyesinde: `/Positions`,
> `/OrganizationUnits`, `/LegalEntities`, `/Tasks/FieldDefinitions`, `/Tasks/RecurrenceRules`,
> `/WorkCenterNext` — **6/6** taşıyor. Alan Tanımları sabah İngilizceydi, şimdi *"0 kayıttan 0 - 0
> arasındaki kayıtlar gösteriliyor"* · *"Tabloda veri bulunmuyor"*, İngilizce kalıntı **sıfır**.
>
> **BL-052 — kural ekranı uçtan uca.** Bir kural oluşturuldu, listede
> `Aylık │ 01.09.2026 │ Süresiz │ Bir kişiye │ Henüz yok` olarak göründü. "Kime" listesinde **Kendim
> yok**; bitiş boş kaydedildi ve boş hücre değil **"Süresiz"** olarak yazıldı.
> **CT'nin bulduğu iki dil kalıntısı aynı oturumda düzeltildi:** satır aksiyonu `Edit` → **Düzenle**
> (kök neden: `Edit` anahtarı **7 dilin hiçbirinde yoktu**, localizer anahtarın kendi adını basıyordu
> — `SharedResource.*.resx`'e eklendi) ve dışa aktarma menüsü `Action` → **Dışa Aktar**.
>
> **BL-040 — sebep kodu canlıda taşınıyor** (bu alan dört aydır hiç yoktu):
> 224 karakterlik başlık → `400 · VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH` ·
> boş başlık → `400 · VALIDATION_REQUEST_TITLE_NOT_EMPTY`.
> **CT notu:** reflection'ı düzeltmek yerine **kaldırması** doğru karardı — kusur imza uyuşmazlığı
> değil **sessiz null**'du; imza düzeltilseydi aynı arıza bir sonraki değişiklikte geri gelirdi.
> Önce kapsamı ölçüp kusurun **yalnız Platform'da** olduğunu göstermesi, diğer dört servise
> gereksiz dokunmayı önledi.
>
> **BL-048 — kapandı.** Türkçe ekranda *"Başlık en fazla 200 karakter olabilir."*;
> **"Request Title" hiçbir yerde yok.**
>
> **Yapılandırılabilir alanlar — uçtan uca doğrulandı.** CT'nin bulduğu boşluk (konteyner var,
> dolduran kod yok) kapandı:
> ```
> tanımla  → regulatory.phase (Metin) · regulatory.market (Durum ← ülke seti)
> formda   → "EK ALANLAR › Regulatory" · Faz [metin] · Pazar [23 ülke DOLU]
> kaydet   → 201 · sunucuda fieldValues: [{regulatory.phase:"Faz II"},{regulatory.market:"TR"}]
> düzenle  → Faz "Faz II" · Pazar "Turkey" — DOLU GELDİ, veri kaybı yok
> ```
> Bilinçli kapsam dışı: `Reference` tipi alan **hiç gösterilmiyor** (jenerik çözücü yok; gösterilse
> çıplak GUID kutusu olurdu — yarım kontrol yerine hiç göstermemek doğru).
>
> **Sıradaki tura devredilen, bu turda düzeltilmeyen:** `errors` alan-haritası tele hiç çıkmıyor
> (`ProblemDetails` statik tipiyle serileşiyor, türetilmiş tipin alanı düşüyor). Ajan düzeltmedi,
> **testle sabitledi** — paylaşılan hata yolunun serileştirmesi kendi turunu hak ediyor.

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
- **Formda kararlaştırılması gerekenler:** `SelfAssigned` **yasal değil** (kuralda atama zorunlu —
  arka plan işinin "kendi"si yoktur; gerekçe `TaskSupportingEntities.cs:251-260`'ta yazılı) ·
  şablon seçimi isteğe bağlı mı zorunlu mu · `EndsAt` boş bırakılabilir mi (süresiz kural).
- **Yeniden ölçüm:** `rg -rl "ecurrence" frontend/Diten.Web/Views frontend/Diten.Web/wwwroot/assets/js` ·
  canlı: menüden yinelenen kural oluştur, bir sonraki süpürmede görev üretiliyor mu.
  > **Yer düzeltmesi (CT, 2026-08-11):** yukarıdaki iki madde BL-057'nin gövdesinde duruyordu
  > (`SelfAssigned` / şablon / `EndsAt` / `rg "ecurrence"` — hepsi **yinelenen kural** konusu, şirket
  > kapsamıyla ilgisi yok). Kopyala-yapıştır artığıydı; asıl sahibi olan bu maddeye taşındı.

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

---

**GENİŞLETME (CT, 2026-08-11) — maddenin eksik yarısı: LİSTELEME değil, ATAMA.**

Yukarısı **listelemeyi** anlatıyor ("bugün doğru sonuç veriyor ama tesadüfen"). Asıl açık **atama
seçicisinde** ve orada tesadüf bile yok.

- **⚠ ÖLÇÜM (CT, 2026-08-11) — kişi seçicisinde şirket filtresi YOK:**
  `GetTaskAssignmentPersonLookupHandler.cs:51-53`
  ```
  _positionAssignments.GetAllAsync(ct)   ← HEPSİ
  _positions.GetAllAsync(ct)             ← HEPSİ
  _organizationUnits.GetAllAsync(ct)     ← HEPSİ
  ```
  Uygulanan tek filtre: atama iptal değil + tarih aralığı içinde (`:61-63`) · pozisyon arşivli değil ve
  `Status == Active` (`:80-81`) · birim arşivli değil (`:89`). **Tüzel kişi hiç sorulmuyor.**
- **Sonuç:** Miguel Garriga'daki bir kullanıcı *"Bir kişi"* dediğinde Grand Medical **Poland** ve **Turkey**'nin
  tüm çalışanlarını görür ve onlara iş atayabilir. Listeleme tesadüfen doğruydu (pozisyon sahipliği
  süzüyordu); **atama tesadüfen bile doğru değil.**
- **⚠ Bu bir UX tercihi değil, hukuki mesele:** Poland AB/GDPR kapsamında, Turkey değil. Bir şirketin
  çalışan listesinin başka bir şirketin kullanıcısına açılması, veri işleme sınırının aşılmasıdır.
- **İkinci yüzey — havuz seçicisi de aynı:** `GetTaskAssignmentPositionLookupHandler.cs:46-48` yine
  `GetAllAsync` üçlüsü, şirket filtresi yok. Tek farkı: DTO'su `LegalEntityId`'yi **zaten taşıyor**
  (`:84`) — yani veri telde var, süzülmüyor. Kişi DTO'su onu **hiç taşımıyor**
  (`TaskModels.cs:512-520` — `AssignablePersonDto`'da `LegalEntityId` yok), bu yüzden frontend gruplayamaz
  bile. Kural yazılırken DTO'ya alan eklenmesi gerekecek.

**── YAZILACAK KURAL — dört satır ──**

| Ne seçiliyor | Kural |
|---|---|
| **Atanan** (assignee) | (1) aynı tüzel kişide **VEYA** (2) raporlama zincirimde altımda **VEYA** (3) bana açıkça verilmiş kapsamda |
| **Onaycı / inceleyen** | O kararı verme **YETKİSİ** olan herkes — **ŞİRKET SINIRI YOK** |
| **Havuz** | Atama ile aynı |
| **Süreçten gelen iş** | Hiçbiri — süreci yönlendirir, kullanıcı seçmez |

- **Kaynak uydurma değil:** Oracle Fusion'ın Security Profile'ları birebir bu üçlüdür — **Organization
  Security Profile** (1) · **Person Security Profile → "Manager Hierarchy"** (2) · **"Custom/List"** (3).
  SAP'deki karşılığı **Structural Authorization** (OM ağacında bir kökten aşağı yürüme) + genel yetki
  nesneleri.
- **⚠ ÖLÇÜM — kelime dağarcığı ZATEN VAR, tüketen yok:** `IDataScopeResolver` →
  `OrgDataScopeResolver` (MOD-0018-FU15, `Authorization/OrgDataScopeResolver.cs`) tam olarak dört kapsam
  türü üretiyor: `OrgUnit` (kendi + alt ağaç, düzleştirilmiş) · `Position` · `ManagerChain` (zincirdeki
  pozisyon id'leri, döngü güvenli, 32 derinlik) · `LegalEntity` (MOD-0220'ye karşı **fail-closed**
  doğrulanmış). Kayıtlı: `DependencyInjection.cs:57`. **Ama Tasks tarafında hiçbir sorgu bunu
  çağırmıyor** — iki atama seçicisi de doğrudan `GetAllAsync` kullanıyor. Yani kural için yeni bir
  kavram kurmak gerekmiyor; **var olan çözücüyü tüketmek** gerekiyor. Bu, işi belirgin biçimde küçültür.

**── ÜÇ ÖRNEK — kuralı tartışılır olmaktan çıkarır ──**

**(a) GRUP CEO'SU.** GMG CEO'su, Grand Medical Poland'daki Fabrika Müdürü'ne iş verir.
(1) ile geçmez (ayrı şirket). **(2) ile geçer** — fabrika müdürü ona rapor veriyor. Fabrikanın maliyeti
Poland'a yazılmaya devam eder: **mülkiyet ile yetki AYRI şeylerdir.**
⚠ Aynı kural GMG'deki bir **muhasebeciyi geçirmez** — Poland'da kimse ona rapor vermiyor. Yani *"ana
şirkette olmak"* tek başına hak **vermez**; hak **miras değil, zincirdir**.

**(b) GRUP İÇİ ONAY — kapsam kuralının TUZAĞI.** GMG TR'deki kullanıcı bir kutu üretir, GMG AZ'deki
Fahreddin Bey'in onayına gönderir. Fahreddin ne astı ne üstü, ayrı şirket: atama kuralının **üçü de**
geçmez — ama bu tamamen meşru bir iştir.
**Çözüm:** bu bir **ATAMA değil, ONAY**. Yetki kullanıcının değil, **SÜRECİN**.
⚠ **ÖLÇÜM — tuzak somut ve bugün kurulu:** `form-page.js:320,329,330,331`
```
taskAssignee         ← assignablePeople
taskReviewer         ← assignablePeople   AYNI LİSTE
taskApprovalManager  ← assignablePeople   AYNI LİSTE
taskWatchers         ← assignablePeople   AYNI LİSTE
```
Dört seçici tek listeden besleniyor. Kapsamı yazan kişi filtreyi **listeye** uygularsa grup içi onayı
**sessizce öldürür** ve kimse bunu bir hata olarak göremez — sadece Fahreddin Bey seçilemez olur.
**Onaycı/inceleyen listesi kapsamla değil ROLLE sınırlanır**, ve bu yüzden ayrı bir uç ister.

**(c) DIŞ KAYNAK.** Miguel Garriga altında *"Dış Kaynak"* birimi → *"Avukat"* pozisyonu → Ahmet.
Görev otomatik Miguel Garriga'ya dosyalanır — **yeni kural gerekmez**, mevcut kademeli çözüm
(`CreateTaskItemHandler.cs:139-155`) cevabı zaten üretir. Sözleşme bitince `EffectiveTo` kapatılır:
Ahmet listeden düşer (`:62-63` yarı-açık aralık), eski görevleri durur, kullanıcı silinmez.
İlgili kural düzeltmesi: [FG-002] (danışman/kontratlı **worker'dır, koltuk alır**).

**── NEDEN İKİ AĞAÇ AYRI — bilinçli tasarım, kural bunu KORUMALI ──**

| Ağaç | Şirket sınırı | Ölçüm |
|---|---|---|
| **Birim ağacı** (`OrganizationUnit.ParentOrganizationUnitId`) | **GEÇEMEZ** | `CreateOrganizationUnitCommandHandler.cs:86` ve `UpdateOrganizationUnitCommandHandler.cs:84` — *"Parent Organization Unit must belong to the same Legal Entity."* |
| **Pozisyon zinciri** (`Position.ReportsToPositionId`) | **GEÇEBİLİR** | `PositionReferenceGuard.ValidateAsync:8-39` yalnız kendine-rapor, varlık, döngü ve 32 derinlik denetler — **tüzel kişi kısıtı YOK** |

Bu tesadüf değil, **doğru ayrımdır**: birim ağacı **mali/hukuki** gerçeği taşır (maliyet nereye yazılır),
pozisyon zinciri **yetki** gerçeğini taşır (kim kime hesap verir). Kural bunu **kullanır, değiştirmez.**
Yürüyüş altyapısı hazır: `GetManagerChainQueryHandler.cs:22-46`. **Sorgu var, veri yok.**

**── GO-LIVE ÖNKOŞULU (bugünkü engel DEĞİL) ──**

- **ÖLÇÜM (dev, `diten_personalization_dev`, 2026-08-11):** 11 pozisyondan **2'sinde**
  `ReportsToPositionId` dolu (`Muhasebe Md → CFO`, `Staff → Manager`) ve **ikisi de tek bir tüzel kişinin
  içinde** kalıyor. Yani kuralın (2) ayağının asıl vakası — **şirket sınırını geçen zincir** — hiç test
  edilmemiş. Maddeyi yazmak ve doğrulamak için dev'de üç satırlık bir test zinciri yeter
  (CEO → Genel Müdür → Fabrika Md, sınırı geçerek).
- **⚠ İKİNCİ ÖLÇÜM — daha sessiz bir risk:** aynı veritabanında **11 pozisyonun 5'i**, `LegalEntityId`'si
  **null** olan bir birime bağlı. Şirket kapsamı kuralı bugün yazılsa bu beş pozisyon için **hiçbir cevap
  üretemez**. Fail-closed davranış (kapsam yoksa satır yok) doğrudur ama sonucu "kimse listede yok"tur —
  yani kural, veri temizlenmeden **sessiz bir boş listeye** dönüşür. Kural ile veri temizliği aynı turda
  gitmeli.
- **⚠ YUKARIDAKİ İKİ SAYI KİRACI KAPSAMI BELİRSİZ — kural yazılmadan ÖNCE çözülmeli.** İkisi de
  veritabanından doğrudan ölçüldü; veritabanı **çok kiracılıdır**, sorgunun `TenantId` süzüp süzmediği
  kayıtta yazılı değil. CT'nin aynı gün yaptığı **kiracı kapsamlı** ölçüm (DefaultTenant, oturum açık
  tarayıcı, `/Positions/api?pageSize=200` ve `/OrganizationUnits/api?pageSize=200`) farklı çıkıyor:
  **3 pozisyon** (1'inde `ReportsToPositionId`), **10 birim**, `LegalEntityId` boş olan **0**, boş-LE
  birimine bağlı pozisyon **0**. `GetPositionsQueryHandler` süzmüyor (`GetAllAsync`), yani fark gizli
  kayıttan gelmiyor — büyük olasılıkla **kiracılar arası toplam** ile **tek kiracı** karşılaştırılıyor.
  Fark önemlidir: başka bir kiracının pozisyonu bu kiracının seçicisinde zaten görünmez, dolayısıyla
  burada "sessiz boş liste" üretemez. Kuralı yazan tur önce **hangi kiracıda kaç pozisyon** olduğunu
  kiracı kapsamlı ölçsün; risk ancak DefaultTenant içinde boş-LE birim varsa gerçektir.
- **ÖLÇÜM — çok şirketli vaka gerçekten var:** bir kullanıcı (`93bcb22e-…`) iki farklı tüzel kişide birer
  pozisyon tutuyor (`Muhasebe Md` / `E2E Engineer`). Yukarıdaki *(a)* şıkkı teorik değil.
  **CT doğruladı (kiracı kapsamlı, 2026-08-11):** `Muhasebe Md` → Finans → LE `b7ef0102-…`,
  `E2E Engineer` → E2E Test Unit → LE `c96d9807-…`. İki ayrı tüzel kişi. Bu iddia bir üstteki
  belirsizlikten **etkilenmiyor**, bağımsız olarak doğru.
- **⚠ RİSK — org şeması artık DEKORATİF DEĞİL.** Bugüne kadar `ReportsToPositionId` hiçbir davranışı
  belirlemiyordu. Kural yazıldığı an **işin kime gideceğini belirleyen veri** olur. Yanlış girilirse iş
  yanlış kişiye yönlenir **ve kimse fark etmez** — çünkü sonuç bir hata değil, makul görünen bir atamadır.
  **Canlıdan önce cevaplanmalı:** zinciri **kim girer** (İK mı, yöneticiler mi) ve **kim doğrular**?
  Gerçek org şeması canlıya geçiş önkoşuludur; bu turun işi değil.

- **Yeniden ölçüm:** `rg -n "GetAllAsync" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/QueryHandlers/GetTaskAssignmentPersonLookupHandler.cs` (bugün 3 satır, hiçbiri süzülmüyor) ·
  `rg -n "IDataScopeResolver" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks` (bugün **boş** — çözücü Tasks'ta hiç tüketilmiyor) ·
  `rg -n "LegalEntityId" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskModels.cs` (bugün `AssignablePersonDto`'da yok).
- **İlgili:** [BL-023] (yukarı/aşağı ayrımı — aynı zincir yürüyüşü) · [BL-072] (aday sessizce eleniyor — aynı turda yapılmalı) · [FG-002] (dış worker koltuk alır) · [FG-001] (dış şirket LE'ye girmez) · MOD-0018 (data-scoping) · MOD-0288 (Organization).

---

**✅ YAPILDI (2026-08-11) — atama seçicileri kısmı. Listeleme tarafı AÇIK KALDI.**

- **Kural tek yerde:** `Features/Tasks/Services/TaskAssignmentScopeResolver.cs`. Kanıt:
  `rg -n "\.Allows\(" services/Diten.Platform/src` → **2 çağrı** (iki lookup handler'ı), **1 tanım**.
  `IDataScopeResolver` Tasks içinde **yalnız** bu dosyada tüketiliyor — paralel kapsam motoru kurulmadı.
- **Uygulandığı yer — dört seçici, üçüne aynı kural değil:**
  `taskAssignee` + `taskWatchers` + havuz pozisyonları → **kapsamlı** ·
  `taskReviewer` + `taskApprovalManager` → **kapsamdan muaf**, yeni uç `lookups/decision-makers`
  (`TaskPersonLookupPurpose.Decision`). İzleyici kararı: **kapsamlı** — izlemek karar vermek değil *görmek*tir
  ve başka şirketin çalışanının görevi görmesi bir veri erişimi kararıdır.
- **⚠ ÖLÇÜLEN EKSİK — çözücü YUKARI veriyor, atama AŞAĞI istiyor:** `OrgDataScopeResolver` `ManagerChain`
  kapsamını **üstlerim** olarak üretiyor (`AddManagerChainScopesAsync:191-226`); atanabilirlik **altımdakileri**
  soruyor ve çözücünün aşağı yönlü bir kapsam türü **yok**. İniş, aynı alandan (`ReportsToPositionId`) ve aynı
  korumalarla (döngü kümesi, 32 derinlik) `TaskAssignmentScopeResolver.ResolveSubordinatePositionsAsync`
  içinde türetiliyor; "benim pozisyonlarım" yine çözücünün kendi `Position` kapsamından geliyor. Çözücüye
  aşağı yönlü bir tür eklenirse **silinecek kod budur**.
- **DTO:** `AssignablePersonDto` artık `LegalEntityId` taşıyor (`TaskModels.cs`). Yanıt şekli değişti:
  `IReadOnlyList<AssignablePersonDto>` → `AssignablePersonLookupDto { People, Excluded }`.
- **⚠ VERİ ÖLÇÜMÜ DÜZELTMESİ — yukarıdaki "11 pozisyon / 5 boş-LE" sayıları YANLIŞTI.** O ölçüm
  **kiracı süzmesi olmadan** ve silinmiş/arşivli kayıtlar dahil yapılmıştı. DefaultTenant kapsamlı doğru ölçüm
  (2026-08-11): **3 kullanılabilir pozisyon** (Active + arşivsiz + silinmemiş; ham 6, 3'ü silinmiş),
  **1'inde** zincir (`Muhasebe Md → CFO`), **10 birim** (ham 14; 4 silinmiş, 1 arşivli), **boş `LegalEntityId`:
  0**. CT'nin tarayıcı ölçümüyle (3 / 1 / 10 / 0) **birebir uyuşuyor**; fark tümüyle kiracı+silinmiş süzmesiydi.
- **Test zinciri kuruldu — şirket sınırını GEÇİYOR:** `CT Fabrika Md` (E2E Test Unit, LE `c96d9807`)
  → `ReportsToPositionId` = `CFO` (Finans, LE `b7ef0102`). API **201** döndü, yani `PositionReferenceGuard`
  gerçekten tüzel kişi denetlemiyor. Karşılaştırma için `CT Yabanci Uzman` aynı yabancı birimde, zinciri yok.
  Canlı sonuç: atama listesi 3 kişi (Fabrika Md **var**, Yabancı Uzman **yok**) · karar listesi 4 kişi
  (Yabancı Uzman **var** — grup içi onay yaşıyor) · havuz aynı kuralla süzülüyor.
- **⛔ HÂLÂ AÇIK — bu tur KAPSAM DIŞI bırakıldı:** listeleme/gelen kutusu/havuz **ekran** süzmesi ·
  şirket seçicisi ve rozet · şirkete göre raporlama. Maddenin başındaki (a)/(b)/(c) sonuçları bu yüzden
  **geçerliliğini koruyor**.
- **Testler:** `services/Diten.Platform/tests/…/Tasks/TaskAssignmentScopeTests.cs` (13) ·
  `frontend/Diten.Web/tests/tasks-assignment-scope.test.js` (15).

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

### BL-059 — 🟠 Platform sistem kiracısı entitlement kapısını geçmeli (KARAR VERİLDİ, YAPILMADI)
- **Sorun (CT canlı ölçümü 2026-08-10):** Platform Admin Tenant'ta menü **eksik**. Ölçüm:
  `work-aggregation` (IsBaseline:false) menüde **var** → *Görev Merkezi*; `tasks` (IsBaseline:false)
  menüde **yok** → *Alan Tanımları* ve *Yinelenen Kurallar* görünmüyor. `document-management`'ın dört
  sayfası da manifest'te **var** ama katalogdan **gelmiyor** — menüde yalnız elle yazılmış (LEGACY-NAV)
  hâlleriyle duruyorlar. Yani her yeni modül için **elle entitlement** açmak gerekiyor.
- **⚠ KARAR ZATEN VERİLDİ (2026-08-10, sahip + Codex incelemesi) AMA HİÇBİR YERE YAZILMAMIŞTI.** Bu madde
  o boşluğu kapatıyor. Karar metni:
  > Platform Admin Tenant, katalogda **aktif ve tenant-assignable** bütün modüllerde entitlement kapısını
  > geçer; **permission kontrolü devam eder**. Sonraki modüllerde ayrı entitlement açılmaz.
- **Anlaşılmış güvenlik sınırları (Codex incelemesinden, aynen korunur):**
  - yalnız **tam** `SystemTenantRules.PlatformSystemTenantId` (`…0001`); GUID elle yazılmaz,
  - baypas **kendi içinde** doğrular: modül mevcut · soft-deleted değil · `Active` · `IsTenantAssignable`
    (üstteki `GetAssignableAsync` süzgecine **güvenilmez** — defense-in-depth),
  - **müşteri kiracılarının** entitlement davranışı birebir korunur (negatif test zorunlu),
  - `IsBaseline` semantiği **değişmez** (bir modülü baseline yapmak onu tüm müşterilere açardı),
  - **permission kontrolü asla atlanmaz**,
  - sonuç "baseline" değil, ayrı bir **`PlatformSystemTenant`** erişim nedeni olarak raporlanır.
- **⚠ CT'nin eklediği ve karar sırasında kabul edilen uyarı — yan etki, kendiliğinden oluşuyor:** baypas
  + `FullCatalogPermissionGrantService` (her yeni permission'ı `…0001` SuperAdmin'e otomatik verir)
  birlikte, platform yöneticisine her iş modülünün ekranlarında **görme değil işletme** yetkisi verir.
  Sahip bunu bilerek kabul etti ("bütün modülleri oradan kontrol ediyoruz"), **ama domain seviyesindeki
  SoD / maker-checker / lifecycle / actor kuralları asla baypas edilmez** — permission sahibi olmak
  onaysız geçişe izin vermez.
- **Neden önemli — menü temizliğinin ÖN KOŞULU:** LEGACY-NAV'daki elle yazılmış bağlantıları silmek
  ancak aynı sayfalar **katalogdan geldikten sonra** güvenlidir. Baypas olmadan silinirse beş sayfa
  menüden tamamen kaybolur (URL ile erişilebilir kalır, menüden bulunamaz).
- **Durum (2026-08-10): KOD YAZILDI, CANLI DOĞRULAMA BEKLİYOR.** Baypas `TenantModuleAccessService`
  `GetEffectiveAccessDetailAsync` içinde, baseline kontrolünün **hemen ardından**: yalnız tam
  `SystemTenantRules.IsSystemTenantId(tenantId)` + modül kendi içinde doğrulanır (mevcut · `IsDeleted:false` ·
  `Status:Active` · `IsTenantAssignable`). Erişim `Source = "PlatformSystemTenant"` olarak raporlanır;
  baseline modüller sistem kiracısında da `"Baseline"` demeye devam eder (semantik değişmedi). Permission
  kapısı el değmemiş — bu yalnız kiracı entitlement duvarını kaldırır.
  Testler `tests/Diten.Platform.Application.Tests/AccessGovernance/PlatformSystemTenantModuleAccessTests.cs`:
  pozitif (entitlement'sız erişim + sebep alanı) **düzeltmeden önce kırmızıydı**; negatifler (müşteri kiracısı ·
  pasif · soft-deleted · tenant-assignable değil · katalogda yok · baseline korunumu) düzeltmeden önce de
  sonra da yeşil — regresyon nöbetçisi olarak yazıldılar.
- **Yeniden ölçüm:** `rg -n "IsSystemTenantId|PlatformSystemTenantId" services/Diten.Platform/src/Diten.Platform.Application/Services/TenantModuleAccessService.cs` ·
  `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~PlatformSystemTenantModuleAccessTests"` ·
  canlı: platform kiracısında menüde *Alan Tanımları* ve *Yinelenen Kurallar* görünmeli.

### BL-060 — 🟡 Sol menüde elle yazılmış bağlantılar ve çift bölüm başlığı (BL-059'dan SONRA)
- **Ölçüm (CT canlı, 2026-08-10) — menünün bugünkü hâli:**
  ```
  ── Çalışma Alanı              ← ELLE (LEGACY-NAV)
     Kontrollü Belgeler · Kurumsal Şablonlar · Şablon Varyantları · Erişim Matrisi · Mutabakat
  ── İnsan Sermayesi Yönetimi   ← ELLE
     Çalışan Taslakları
  ── Geliştirici Etkinleştirme  ← KATALOG
  ── Çalışma Alanı              ← KATALOG  (aynı başlık İKİNCİ kez)
     Görev Merkezi
  ── Yönetim                    ← KATALOG
  ```
  **"Çalışma Alanı" başlığı iki kez basılıyor** — biri elle blok, biri dinamik menü.
- **Altı bağlantının ölçülmüş durumu:**
  | Bağlantı | Manifest | Bugün katalogdan geliyor mu | Yapılacak |
  |---|---|---|---|
  | Kontrollü Belgeler · Kurumsal Şablonlar · Şablon Varyantları · Erişim Matrisi | ✓ var | ✗ (entitlement yok) | BL-059'dan sonra **sil** |
  | Mutabakat (`/DocumentManagementReconciliation`) | ✗ **yok** | ✗ | önce **manifest'e eklenmeli** (sahibi), sonra sil |
  | Çalışan Taslakları (`/HCM/Employees/Create`) | ✗ **yok** | ✗ | **HCM sahibinin işi**, dokunulmaz |
- **⛔ SIRA ŞARTI:** bir bağlantı ancak **manifest'te var VE menüde katalogdan görünüyor** ise silinir.
  Yalnız manifest yeterli değil — bugün dördü manifest'te olduğu hâlde entitlement kapısında duruyor.
  Ölçmeden silmek, çalışan bir menüyü kırmaktır.
- **Sahibin duruşu (2026-08-10):** *"silsek ne olur, modül silinmeyecek sonuçta; diğer developer'a
  söyledim, o düzeltsin."* **Meşru bir risk tercihi** ve bir üstünlüğü var: elle yazılmış bağlantı,
  sahibin manifest eksiğini **gizliyor**; silinince eksik görünür hale gelir ve sahibi düzeltir.
  Bedeli: sahibi düzeltene kadar sayfa menüden bulunamaz (URL çalışmaya devam eder).
- **CT önerisi:** BL-059 önce → çiftler görünür → sonra sil. Böylece kayıp penceresi hiç oluşmaz.
- **Durum (2026-08-10): KOD YAZILDI, CANLI DOĞRULAMA BEKLİYOR.** BL-059 canlı doğrulandıktan sonra dört
  çift bağlantı `_LayoutTenantShell.cshtml`'den `@if (Perms.Has(...))` sarmalayıcılarıyla birlikte silindi
  (`/DocumentManagementControlledDocuments` · `/DocumentManagementTemplateMasters` ·
  `/DocumentManagementTemplateVariants` · `/DocumentManagementAccessMatrix`) — dördü de katalogdan
  `Doküman Yönetimi` grubu altında geliyor, sayfa başına `RequiredPermission` katalogda taşındığı için kapı
  kaybı yok. **Mutabakat ve HCM Çalışan Taslakları silinmedi**; her ikisinin üstünde ölçülebilir silme koşulu
  yazılı (manifest'te nav-visible sayfa **ve** menüde görülmesi — ikisi birden). LEGACY-NAV çiti duruyor.
  **Çift "Çalışma Alanı" başlığı:** elle bloktaki başlık **kaldırıldı** — `Workspace` resx anahtarı ile dinamik
  menünün `Nav.Domain.WORKSPACE` anahtarı aynı metni ("Çalışma Alanı") basıyordu ve silmelerden sonra elle
  başlığın altında yalnız bir **doküman** sayfası kalıyordu (yanlış domain + çift ad). `Nav.Domain.DOCUMENTMANAGEMENT`
  ile değiştirilmedi: bu yalnız çakışmayı "Doküman Yönetimi"ne taşırdı. Kalan tek elle bağlantı başlıksız duruyor —
  olduğu şey bu: bölüm değil, kataloglanmayı bekleyen tekil bir giriş.
- **Yeniden ölçüm:** `rg -n 'menu-header-text|href="/(Document|HCM)[^"]*"' frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` ·
  `rg -c 'Perms.Has\("platform.document-management' frontend/Diten.Web/Views/Shared/_LayoutTenantShell.cshtml` (1 olmalı) ·
  canlı: `dotnet build frontend/Diten.Web` + yeniden başlat, sonra kiracı menüsünde dört rotanın **katalogdan**
  geldiğini ve "Çalışma Alanı" başlığının **tek** kaldığını gör.

### BL-061 — 🟢 Görev formu golden referansa hizalandı (KOD YAZILDI, CANLI DOĞRULAMA BEKLİYOR)
- **Kapsam:** yalnız `/Tasks/Create` + `/Tasks/{id}/Edit`. Alan eklenmedi/çıkarılmadı; yerleşim, gruplama ve
  kontrol tipi değişti. Referans: `Views/DevEnablement/GoldenReferenceCompact/_Form.cshtml` (desen icat edilmedi).
- **Yapılanlar:** tüm select'ler `select2 form-select`; tek kart → **beş** `<section class="card">`
  (Temel bilgiler · Atama · Planlama · Ek alanlar | sağ sütun: Yönetişim); açıklama cümlesi yerine
  **h5 + breadcrumb** (başlık `_Form.cshtml`'e taşındı, Create/Edit tek başlık paylaşıyor); Kaydet/İptal
  **başlık satırında, tek kopya** (`<button type="submit" form="taskForm">`, kayıt artık form submit'i — Enter da
  kaydediyor); `#taskCustomFields` kendi kartı.
- **KARAR — dinamik select'ler (K4):** yapılandırılabilir alanların select'leri JS ile üretildiği için markup'ı
  düzeltmek yetmezdi. `TaskForm.enhanceSelects(root)` eklendi ve **renderCustomFields + hidrasyondan SONRA**
  çağrılıyor; üretilen select'ler `select2` sınıfını kendileri taşıyor.
- **KARAR — zorunlu alan rozeti (#5):** rozet **eklendi**, çünkü yapılandırılabilir zorunlu alanları da sayıyor.
  Paylaşılan `required-fields-tracker.js` yalnız `<form>` içinde çalışıyordu (form artık gerçek `<form>`) ve
  sonradan eklenen kontrollere dinlemez bağlamıyordu; MutationObserver ile hem **eklenen** hem **görünür olan**
  (`d-none` kalkan) alanlar sayılıyor. Sayamasaydı hiç eklenmeyecekti. Yan bulgu: gözlemciye "kendi rozetini
  yoksay" koruması şart — testte iki rozet birbirini tetikleyip sonsuz döngü yaptı, `data-required-tracker` ile
  kapatıldı.
- **KARAR — arama kutusu + açılır liste (#6, sahibin sorusu):** ayrı arama kutusu **kaldırılamaz**. Ölçüm:
  record alanı sunucuda arıyor (`TasksApi.fieldRecords(code, {term})`) ve kontrol kaynağın **tek sayfasını**
  tutuyor; select2'nin kendi araması yalnız DOM'daki option'ları süzer, yani var olan bir kayıt için "sonuç yok"
  derdi — yalan söyleyen bir arama kutusu. Çözüm: sunucu araması kalır, **select2'nin kendi arama kutusu
  kapatılır** (`data-select2-search="off"` → `minimumResultsForSearch: Infinity`), böylece kullanıcı tek arama
  görür.
- **Inline CSS:** yok (FG-003 temiz). **Yeni l10n stringi yok** — mevcut anahtarlar kullanıldı
  (`TasksTitle · FormTitleCreate/Edit · Section* · Action*`, yedi dilde mevcut). `PageDescription` artık
  kullanılmıyor, silinmedi (zararsız).
- **Yeniden ölçüm:** `npx vitest run tests/tasks-form-golden-alignment.test.js` (frontend/Diten.Web içinden) ·
  `dotnet build frontend/Diten.Web` · canlı: forma bir görev kaydet, düzenlemede geri gel — yapılandırılabilir
  alan değerleri yerinde olmalı.

### BL-062 — 🔴→🟢 Görev formu 2. tur: kişi alanları çalışmıyordu (KOD YAZILDI, CANLI DOĞRULAMA BEKLİYOR)
- **İşlevsel kusur (ölçüm):** İnceleyen · Onay yöneticisi · İzleyiciler **serbest metin** kutusuydu; arkalarında
  seçici yoktu. Sunucu `Guid? ReviewerCandidateUserId`/`Guid? ApprovalManagerUserId` bekliyor, yani alanı doğru
  doldurmanın tek yolu **elle GUID yazmak**tı. İzleyiciler daha kötüydü: `readForm` **metin** üretiyor,
  `buildCreatePayload` yalnız **dizi** iletiyordu → girilen her izleyici **sessizce çöpe gidiyordu**.
- **Çözüm yeniden yazılmadı:** üçü de `taskAssignee`'nin kullandığı `TasksApi.assignablePeople()` kaynağına ve
  aynı `renderPersonOptions` fonksiyonuna bağlandı. İzleyiciler `multiple` (sunucu şekli `TaskWatcherRequest`
  listesi — değiştirilmedi; `toWatcherRequests` kimlikleri o şekle çeviriyor, rol her zaman `Watcher`, Consultant
  BL-053). Çoklu seçicide placeholder satırı **yok** — seçilebilir olduğu için boş kimlik gönderirdi.
- **Kayıt alanları tek kontrol:** ayrı arama kutusu kaldırıldı, `data-select2-search="off"` geri alındı; select2
  **ajax** kullanılıyor (`TasksApi.fieldRecords(code,{term})`). Debounce select2'nin `delay: 250`'sine, yarış
  koruması transport'un sequence guard'ına taşındı — ikisi de korundu. **Ajax kuralı kodda:** yalnız
  `data-custom-field-record="1"` (ModuleRecord kaynaklı, sunucuda sayfalanan) kontrol ajax alır; tam yüklü
  listeler (Lookup/Status, kişi, öncelik) select2'nin yerel aramasını kullanır — tarayıcının elinde olan listeyi
  süzmek için sunucuya gitmek gereksizdir.
- **Tarihler:** üç `<input type="date">` → `flatpickr-date` metin alanı + flatpickr css/js (golden deseni).
  `dateFormat: 'Y-m-d'` — **giden biçim değişmedi**, test bunu pinliyor. Gerekçe: yerli kontrol biçimi
  **işletim sistemi** dilinden alıyordu, Arapça sayfada bile gg.aa.yyyy.
- **Yönetişim beş karta bölündü:** İnceleme Ayarları · Onay Yöneticisi · İzleyiciler · E-posta Bildirimleri ·
  Devir Ayarları — her biri ikon + başlık + bir cümle. İzleyiciler Atama kartından buraya taşındı (izlemek atama
  değildir). **Danışman kartı ve onay akışı diyagramı YAPILMADI** (BL-053 / BL-063).
- **l10n:** 7 yeni anahtar × 7 dil (`CardReviewTitle · CardApprovalTitle · CardApprovalDescription ·
  CardWatchersTitle · CardEmailTitle · CardEmailDescription · CardDelegationTitle · CardDelegationDescription`);
  mevcut `ReviewRequiredHint · ApprovalHint · WatchersHint` cümle olarak yeniden kullanıldı.
  `customFieldRecordSearchPlaceholder` öksüz kalmadı — artık kayıt seçicisinin `placeholder`'ı.
- **Güncellenen eski testler (desen değişti, davranış değil):** `tasks-record-fields` (arama kutusu →
  `data-custom-field-record` bayrağı) · `tasks-record-fields-round-trip` (arama artık select2 transport'undan
  sürülüyor) · `tasks-reviewer-field` (iki alan da select) · `tasks-form-golden-alignment` (iki-kontrol pini
  kaldırıldı, yerine yeni dosyaya işaret). Hiçbir davranış iddiası düşürülmedi.
- **Yeniden ölçüm:** `npx vitest run tests/tasks-form-pickers-dates-governance.test.js` (frontend/Diten.Web
  içinden) · tam paket `npx vitest run` — taban 9 kırmızı (strategy/objectives/planning, Tasks dışı) ·
  `dotnet build frontend/Diten.Web` · canlı: inceleyen SEÇ → kaydet → düzenlemede **ad** dolu gelmeli.

### BL-066 — 🔴→🟢 select2 bildirim kopukluğu: koşullu alanlar hiç açılmıyordu (KOD YAZILDI, CANLI DOĞRULAMA BEKLİYOR)
- **Belirti (sahip ekranda, CT ölçtü):** "Kime → Bir kişi" seçiliyor, **Atanan kişi alanı açılmıyor**; havuzda
  da aynı. Yalnız "Kendim" çalışıyordu — yani form kişiye/havuza görev **atayamıyordu**.
- **Kök neden — sınıf hatası, tek tel değil:** select2 değişikliği **jQuery ile** bildiriyor
  (`$(select).trigger('change')`), sayfa ise **yerel** dinliyordu (`addEventListener('change', …)`).
  jQuery'nin trigger'ı yerel dinleyicileri çağırmaz. Bir önceki tur her seçiciyi select2'ye çevirdi —
  **üretici değişti, tüketiciler değişmedi** ve select2'ye bağlı her yerel `change` dinleyicisi aynı anda
  sağır oldu (üç tanesi).
- **Çözüm bağlamanın kendisinde:** `TaskForm.enhanceSelects` artık bağladığı her select'in jQuery
  değişikliğini yerel bir olaya **köprülüyor**. Yani "select2'ye bağlandı ama haber vermiyor" durumu yapısal
  olarak imkânsız; yarın eklenecek dördüncü koşullu alan bu ayrıntıyı hiç bilmeden çalışır. Döngü koruması
  jQuery'nin kendi ayrımıyla: yerelden gelen olayın `originalEvent`'i vardır, `trigger`'ınki yoktur.
- **Test neden bu kez yakalıyor:** guard **gerçek jQuery + gerçek select2** vendor dosyalarını yükleyip
  select2'nin kendi yolunu sürüyor (`$(el).val(x).trigger('change')`). Yerel olay gönderen bir test bu kusuru
  bir kez daha kaçırırdı — bugün tam olarak öyle olmuştu. Uçtan uca vaka, atanan alanın **görünür olduğunu**
  da iddia ediyor: onsuz test, alan hiç açılmasa bile programatik değer atayıp yeşil verirdi (ilk yazımda
  öyle oldu, düzeltildi).
- **Aynı turda:** kart başlıkları golden'ın `text-uppercase` reçetesine hizalandı · yapılandırılabilir alan
  ızgarası `col-md-4` → `col-md-6` (formun geri kalanıyla aynı) · etiketler **Tagify** çipleri (deponun
  mevcut deseni: tenant-security IP/ülke listeleri; `originalInputValueFormat` ile alt input virgüllü kalıyor,
  sunucunun beklediği **dizi şekli değişmedi**) · inceleme türü BL-064 · e-posta tercihleri BL-065.
- **Yeniden ölçüm:** `npx vitest run tests/tasks-form-select2-notification.test.js` (frontend/Diten.Web
  içinden) · `dotnet build frontend/Diten.Web` · canlı: "Kime → Bir kişi" seç, alan **açılmalı**; kişi seç,
  kaydet, görev o kişide olmalı.

### BL-063 — 🟢 Onay akışı diyagramı: ancak gerçek rotayı okuyabilirse çizilir
- **Nereden çıktı:** sahibin create prototipinde onay kartının içinde üç kutuluk bir şema var —
  `Görev Oluşturulur → Yönetici Onaylar → Göreve Başlanır` + *"Yönetici onay verene kadar görev
  'Onay Bekliyor' durumunda kalır. Reddedilirse görev iptal edilir."*
- **2026-08-11 turunda bilinçli olarak YAPILMADI.** Gerekçe (ajan önerdi, CT kabul etti):
  > Form yalnız *"onay gerekli, yönetici şu kişi"* der. Bundan sonrasına — kaç aşama, hangi sıra,
  > reddedilince iptal mi geri mi — **MOD-0023 karar verir.** Sabit üç kutu çizmek, MOD-0024'ün
  > kontrol etmediği bir akışı kullanıcıya vaat etmek olur.
- **Neden bu, mimari sınırın kendisi:** MOD-0024 onayı **raporlar, karar vermez** (Binding A).
  Ekran da bu sınıra uymak zorunda. Yarın MOD-0023'te iki aşamalı bir onay tanımlanırsa, resim olarak
  çizilmiş şema **yalan söyler ve kimse fark etmez** — çünkü şema veri değil, resimdir.
- **İki meşru yol, biri seçilecek:**
  - *(a)* Diyagram **MOD-0023'ten gerçek rotayı okur** ve onu çizer. Gerekli dikiş: iş akışı
    tanımının adımlarını (aşama · rol · reddetme davranışı) okunabilir biçimde veren bir uç.
    Bu, WC-1'deki "sağlayıcı kendi gerçeğini bildirir" deseninin aynısı.
  - *(b)* Diyagram hiç çizilmez; kartta tek cümle kalır. **Bugünkü davranış budur ve dürüsttür.**
- **CT önerisi:** (b) ile devam; (a) ancak MOD-0023 rota okuma ucu geldiğinde. O uç yokken (a)'yı
  yapmak, çizilmiş bir varsayımı gerçek sanmaktır.
- **Yeniden ölçüm:** `rg -n "Onay Akışı|approval-flow" frontend/Diten.Web/Views/Tasks` (bugün boş
  olmalı) · MOD-0023 tarafında rota okuma ucu var mı.

### BL-064 — 🟡 "Review Toplantısı" inceleme türü: yeri açıldı, modül bekliyor
- **Bugün ne var:** görev formunda inceleme kartında **inceleme türü** seçimi var: *Hızlı inceleme*
  (varsayılan, seçili) ve *Review toplantısı* (**devre dışı**, sebebini söyleyen yardım metniyle:
  "toplantı modülü yapılmadı"). DEC-001 gereği sebepsiz ölü kontrol bırakılmadı.
- **Bu bir davranış değişikliği DEĞİL:** bugün tek inceleme türü var — inceleyen doğrudan onaylar — ve o
  türün adı yoktu. Seçim, **bugünkü davranışı adıyla görünür kılıyor**; ikinci tür geldiğinde okunur olması
  için. Sunucuya **yeni alan gitmiyor** (`reviewType` payload'da yok; test bunu pinliyor): tek değerin
  alanı olmaz, okunmayan bir sözleşme alanı ise her gelecek okuyucunun cevaplaması gereken bir soru olur.
- **Ne gerekiyor (tetikleyici):** bir **toplantı modülü** — toplantı oluşturma, katılımcı, tarih, karar
  kaydı. Geldiğinde: (1) seçenek etkinleşir, (2) `reviewType` sözleşmeye **o zaman** eklenir,
  (3) MOD-0023'e "toplantı sonucu = review kararı" dikişi tanımlanır.
- **Yeniden ölçüm:** `rg -n "taskReviewTypeMeeting" frontend/Diten.Web/Views/Tasks/_Form.cshtml` (disabled
  olmalı) · toplantı modülü manifest'i var mı.

### BL-065 — 🟡 Görev başına bildirim tercihi: ekranda yeri var, sözleşmede karşılığı YOK
- **Sahibin mockup'ı** e-posta kartında iki tercih istiyor: **hangi olaylarda** bildirim (son tarih
  yaklaşınca · durum değişince · yorum eklenince · gecikmede) ve **ne zaman hatırlatılsın**
  (1 gün / 3 gün / 1 hafta / 2 hafta / aynı gün).
- **ÖLÇÜM (2026-08-11) — karşılığı yok:** `TaskItem`'da bildirimle ilgili **tek** alan var:
  `EmailNotificationsEnabled` (bool, `TaskItem.cs:146`). `CreateTaskItemRequest`/`UpdateTaskItemRequest`
  da yalnız onu taşıyor. `TaskNotificationEvents` (`TaskModels.cs:257`) beş olay **kodu** tanımlıyor
  (`assigned · claimed · duesoon · completed · approvalrequested`) ama bunlar **manifest/dispatch
  seviyesi** — görev başına "bu olayda haber ver" tercihi değil. Depoda `NotificationPreference` diye bir
  varlık **yok**.
- **Karar: kart bugünkü hâlinde bırakıldı (yalnız aç/kapa).** Seçtirip hiçbir yere yazmayan kontrol koymak,
  bu turlarda defalarca düzelttiğimiz *"ekran tamam der, arkada bir şey yok"* kusurunun ta kendisi olurdu.
- **Ne gerekiyor:** görev başına bildirim tercihi **sözleşmesi** — (a) olay seçimi: `TaskNotificationEvents`
  kodlarından bir alt küme (`IReadOnlyList<string> NotifyOnEvents`), (b) hatırlatma önceliği: `duesoon`
  olayının kaç gün önce tetikleneceği (`int? ReminderLeadDays`), (c) ikisinin de `TaskItem`'da saklanması ve
  dispatch tarafında **okunması** — saklanıp okunmayan tercih de aynı kusur. Mevcut `EmailNotificationsEnabled`
  ana anahtar olarak kalır (kapalıysa hiçbiri gönderilmez).
- **DURUM (2026-08-11): ÜÇ KATMAN DA YAZILDI, CANLI DOĞRULAMA BEKLİYOR.**
  - **Saklama:** `TaskItem.NotifyOnEvents` (`IReadOnlyList<string>?`) · `ReminderLeadDays` (`int?`) ·
    `LastDueSoonReminderKey` (`string?`). **Neden nullable:** `null` = "hiç seçilmedi" → bugünkü davranış (her
    olay gider). Boş liste = "hiçbiri", ayrı bir seçim. Non-nullable boş liste varsayılanı, deploy anında
    **mevcut her görevi susturur** — varsayılan kılığına girmiş bir veri göçü olurdu; bu yüzden **backfill YOK**,
    anlamlı varsayılan var. Hatırlatma **gün sayısı** olarak (BL-030: DateTimeOffset dizi olarak saklanıyor,
    sorgu kırıyor). `EmailNotificationsEnabled` ana anahtar olarak duruyor ve önce kontrol ediliyor.
  - **Sözleşme + form:** create/update istekleri iki tercihi taşıyor (update'te `null` = "bu çağıran bu alanı
    düzenlemiyor", `ApprovalRequired` ile aynı kural — gidiş-dönüşte veri kaybını önler). E-posta kartında beş
    olay çoklu seçimi + hatırlatma tekli seçimi; **yalnız gerçekten gönderilebilen olaylar** listeleniyor ve
    `duesoon` bu listeye **ancak göndericisi aynı turda yazıldığı için** girdi. Ana anahtar kapalıyken blok
    gizleniyor ve payload'da tercih **gönderilmiyor**; hatırlatma süresi de `duesoon` seçili değilken gizli.
  - **Gönderici:** `TaskNotificationService` tercihi ana anahtarın **yanında** okuyor.
    `SendDueSoonRemindersHandler` + `TaskDueSoonSweepJob` (yeni) — `TaskRecurrenceSweepJob` ile birebir aynı
    şekil: aktif kiracıları dolaşır, `TenantScope.Begin` içinde tenant-scoped komutu çalıştırır.
    **Idempotency görevin üstünde:** son tarih başına claim anahtarı (`LastDueSoonReminderKey`), **gönderimden
    ÖNCE** expected-version yazımıyla damgalanıyor (recurrence claim'inin aynısı) — elle çalıştırılan komut da
    korunur. Anahtar **son tarihe** bağlı, böylece ertelenen görev yeni tarihinde yeniden hatırlatılır.
  - **⚠ İŞİ NE KAPALI TUTUYOR — sevkiyattaki gerçek (2026-08-11 düzeltmesi):** kodda iki bayrak var
    (`RegisterStandardJobs` **VE** `EnabledJobs[...]`) ama **`RegisterStandardJobs` hem `appsettings.json` hem
    `appsettings.Development.json` içinde TRUE**. Yani **Development'ta işi kapalı tutan TEK şey per-job
    `false`** — "iki kez kapalı" ifadesi boolean için doğru, sevkiyat için yanlıştı ve düzeltildi.
    **Production'da ayrı ve gerçek bir kapı var:** `BackgroundJobs:Enabled = false` (base appsettings) —
    zamanlayıcının tamamı kapalı. "Hatırlatma gelmedi" önce bir yapılandırma sorusudur (BL-055 dersi).
  - **Bilinen sınır (dürüstçe):** süpürme `GetAllForTenantAsync` + bellek içi süzme kullanıyor ve tur başına
    `MaxTasksPerTenant`=200 ile sınırlı — depoda son-tarih sorgusu yok. Kiracı başına görev sayısı büyüdüğünde
    indeksli bir sorgu gerekir.
  - **Kapanış turu (2026-08-11) — doğrulamada çıkan altı bulgu kapatıldı:**
    1. **Hatırlatma kapatılamıyordu.** Sözleşme "düzenlemiyorum" ile "temizliyorum"u aynı `null` ile
       söylüyordu → form "Hatırlatma yok" derdi, hiçbir şey kaydolmazdı, süpürme e-posta atmaya devam ederdi.
       **Karar: tercihler TEK BLOK olarak düzenlenir**; `NotifyOnEvents` non-null ise çağıran bloğu
       düzenliyordur ve `ReminderLeadDays` **olduğu gibi** uygulanır (null = temizle). Sentinel (-1) veya ikinci
       bir "temizle" alanı **seçilmedi**: sözleşmeye sihirli sayı sokardı, blok ise tek formda tek karttır ve
       `ReviewRequired` + reviewer emsali zaten bu şekli kullanıyor. **Yalnız süreyi güncelleyen çağıran:**
       bloğu düzenlemiyor sayılır, **yok sayılır** — korumak istediği olay listesini birlikte göndermelidir
       (form her kayıtta bunu yapar). Frontend'de `buildUpdatePayload` eklendi: düzenleme her zaman olay
       listesini taşır, yoksa temizleme sessizce kaybolurdu.
    2. **Claim-before-send sırası artık ölçülüyor.** `FakeTaskItemRepository.ForcedUpdateConflicts` ile sürüm
       çakışması enjekte ediliyor; test "hiç e-posta gitmedi + AlreadyReminded=1" diyor. Sıra ters çevrildiğinde
       **kırmızı** oluyor (kanıtlandı).
    3. **Vacuous test kaldırıldı.** Süpürme testleri artık **gerçek** `TaskNotificationService` üzerinde koşuyor;
       filtreyi yeniden yazan sahte silindi (kopya ayrıca kaymıştı: varsayılan karşılaştırıcı ↔ `StringComparer.Ordinal`).
       Üretim filtresi silinince ilgili test artık **kırmızı** oluyor (kanıtlandı).
    4. **Test double'ın `Detach`'i sığdı** — `Tags`/`FieldValues` paylaşılan referanstı. Derin kopya eklendi;
       sızıntıyı gösteren test var.
    5. **`Failed` sayacı düşüyordu** — hem due-soon hem **recurrence** süpürmesi komutun `Failed` alanını
       okumuyordu, yani her gönderimi patlayan kiracı temiz koşu olarak loglanıyordu. **İkisi de düzeltildi**
       (recurrence sadık bir kopya olduğu için yeni kusur değildi ama aynı yalanı söylüyordu; ayrı backlog
       maddesi açmak yerine iki satırla kapatmak dürüst olanıydı).
- **Yeniden ölçüm:** `dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~TaskNotificationPreferenceTests"` ·
  `npx vitest run tests/tasks-notification-preferences.test.js` (frontend/Diten.Web içinden) ·
  `rg -n "TaskDueSoonSweepJob" services/Diten.Platform/src/Diten.Platform.Application/BackgroundJobs/PlatformRecurringJobRegistrar.cs`
  - **Son tur (2026-08-11) — bir 🔴 ve dört 🟡 daha kapatıldı:**
    1. **Vazgeçen görev claim'i yakıyordu.** `IsDue` yalnız tarihe/yaşam döngüsüne bakıyor, tercih süzgeci bir kat
       altta — ve damga süzgeçten **önce** atılıyordu. "Son tarih yaklaştığında" tikini kaldır → damga atılır,
       e-posta gitmez → tiki geri koy → **o son tarih için hatırlatma bir daha asla gelmez**. Varsayılan süre
       seçili geldiği için olağan yoldu. **Çözüm tek-sahip kuralına uyarak:** süzgeç `IsDue`'ya
       **kopyalanmadı**; kural `TaskNotificationPolicy`'ye çıkarıldı, `ITaskNotificationService.WouldNotify`
       **default interface** üyesi olarak ona devrediyor (böylece 13 dosyadaki sahte de aynayı kopyalamak zorunda
       kalmadı), süpürme **damgalamadan önce soruyor**. Üç test: tik yokken damga **null kalıyor** · fikir
       değişince aynı son tarih için hatırlatma geliyor · ana anahtar için aynısı. Guard silindiğinde üçü de
       kırmızı (kanıtlandı).
    2. **"Derin kopya" fazla iddiaydı.** Liste yeniden ayrılıyordu ama `TaskFieldValue` mutable — `Value` yerinde
       değiştirilince depoya sızıyordu; ayrıca başarılı `UpdateAsync` çağıranın listelerini depoya veriyordu.
       **Karar: vaadi daraltmak yerine tutmak** — elemanlar da klonlanıyor, hem okumada hem yazımda. İki test.
    3. **Kardeş işteki bayat "twice" metni** (recurrence job + registrar) due-soon ile aynı gerçeğe uyduruldu.
    4. **Yinelenen görevler hatırlatma alamıyor** → **BL-067** açıldı (Priority/Tags sabitliği de aynı maddede).
       Bu turda taşınmadı: tercihlerin yeri şablon, şablon ekranı yok (BL-054) — düzenlenemeyen bir alana yazmak
       aynı kusurun tekrarı olurdu. Koda da açıklama satırı bırakıldı.
    5. **Ana anahtar kapalıyken eski tercihler korunuyor** — davranış **kasıtlı** (geçici sessizlik, kullanıcının
       hiç dönmediği ayarları yok etmemeli) ve artık **testi var**, bir sonraki tur kusur sanıp bozmasın diye.
  - **Kapanış kalemi (2026-08-11): `TaskFieldValue` klonu bir üyeyi düşürüyordu.** `DetachCollections` altı
    yazılabilir üyeden **beşini** elle sayıyordu; **`Classification`** düşüyordu — yani `Confidential` bir alan
    değeri depodan **`Normal`** olarak dönüyordu. Bu, `Redacted` ile birlikte BL-024'ün "tarayıcı payload'ından
    çıkar" kuralını taşıyan alan.
    **Asıl bulgu tek alan değil, aynı dosyadaki disiplin farkıydı:** `CopyWritableFields` **yansıma** kullanıyor
    (yeni alan kendiliğinden taşınır), eleman klonu **elle liste** idi (yeni alan sessizce düşer).
    **Karar: (a) + (b) birlikte.** (a) eleman klonu da **yansımaya** çevrildi — tek yöntem, tek disiplin, sınıfın
    tamamını kapatır. (b) üye listesini **yansımayla türeten** bir guard testi eklendi
    (`EVERY_writable_member_of_a_field_value_survives_detachment`): her yazılabilir üyeye varsayılanından
    **farklı** bir değer verip geri okuyor. Yalnız (a) yapılsaydı, yarın yansımanın yetmediği bir üye tipi
    (ör. iç içe mutable nesne) eklendiğinde yine sessizce kaybederdik; yalnız (b) yapılsaydı elle listeyi her
    seferinde biri güncellemek zorunda kalırdı. Test ayrıca bilmediği bir üye tipiyle karşılaşınca
    **kapsamı daraltmak yerine** açık bir hata veriyor.
    Kanıt: klondan bir üye düşürüldüğünde **iki test de kırmızı** (biri somut alanı, biri sınıfı yakalıyor).
  - **Kapanış kalemi B (2026-08-11): sahte bildirim servisi kuralın YARISINI uyguluyordu.**
    `TaskTestDoubles.FakeTaskNotificationService.NotifyAsync` yorumu *"gerçek servisin AYNI iki atlama kuralını
    uygular"* diyordu; kod yalnız `EmailNotificationsEnabled`'a bakıyordu. BL-065 ikinci kuralı
    (`NotifyOnEvents`) eklediğinde sahte güncellenmedi → **yorum yanlış hale geldi** ve bu sahteyi kullanan
    **13 test dosyası**, üretimin artık kullanmadığı bir politikayı ölçmeye başladı. "Bildirim gitmedi" diyen bir
    iddianın üretimden **farklı bir sebeple** doğru olması, izinli bir sahteden daha kötüdür: test kanıt gibi
    okunur.
    **Düzeltme tek satır:** sahte de `TaskNotificationPolicy.WouldNotify(task, eventCode)` çağırıyor — ayna bitti,
    yorum doğrulandı, politikaya eklenecek bir sonraki kural bu metodu kimse düzenlemeden 13 dosyaya ulaşır.
    Test **eşdeğerlik** olarak yazıldı (sahte ile gerçek servisin aynı girdiye aynı cevabı vermesi), tek tek
    beklenti olarak değil — önemli olan hangisinin ne dediği değil, **ayrışamamaları**.
    **Beklenen sonuç doğrulandı:** 13 dosyanın hiçbiri kırmızıya dönmedi (hiçbiri `NotifyOnEvents` set etmiyor),
    yani sahte bugüne kadar yanlış bir şey ölçmemişti — yalnız yeni kuralı ölçmüyordu.
  - **CANLI DOĞRULAMA TURU (2026-08-11) — iki kusur, ikisi de bu turun kodunun merkezinde:**
    - **A. Başarısız gönderim claim'i yakıyordu.** Canlı kanıt: `PROVIDER_REJECTED` (Mailpit AUTH duyurmuyor,
      dev config dummy kimlik gönderiyor) → damga atılmıştı → o son tarih **kalıcı sessizleşti**, ancak tarih
      değiştirilerek kurtarıldı. Dosyanın "belgelenmiş ödünleşim" dediği şey ilk canlı koşuda gerçekleşti.
      **Seçilen tasarım: (a) claim teslimle kesinleşir.** Damga hâlâ gönderimden **önce** atılıyor (çökme
      durumunda çift gönderim yerine sessizliğe düşmek doğrusu), ama sonuç `Dispatched` değilse damga
      **expected-version ile geri alınıyor** ve sonraki süpürme aynı son tarihi yeniden deniyor.
      **Reddedilen alternatif (b) "denendi/teslim edildi" + sınırlı yeniden deneme:** ikinci bir alan ve bir
      yeniden deneme politikası gerektiriyor, ve terminal durumu **yine kalıcı kayıp** — sadece önünde daha çok
      makine var. (a) tek yazımla aynı kurtarmayı sağlıyor; kurşun penceresi günler genişliğinde olduğu için
      saatlik süpürmenin içinde bol şans var. **Geri alma yazımı da başarısız olursa bugünkü davranışa düşer —
      yani hiçbir durumda öncekinden kötü değil.** Yarış kaybı (`ForcedUpdateConflicts`) **ayrı tutuldu**:
      "yarışı kaybettim" claim'i serbest bırakmaz, çünkü onu başka bir koşucu tutuyor.
    - **B. Süpürme kaybolan hatırlatmayı temiz koşu diye raporluyordu** (`Sent=0 AlreadyReminded=0 FailedTasks=0
      FailedTenants=0`). Sebep: yalnız `Dispatched` sayılıyordu ve yalnız **exception** hata sayılıyordu; arada
      kalan her sonuç hiçbir sayaca girmiyordu. **Düzeltme:** `SendDueSoonRemindersResponse` sonuçları ayırıyor
      (`RemindersSent · AlreadyReminded · NotDelivered · Failed`) ve **her değerlendirilen görev tam olarak bir
      sayaca** giriyor — testi bu toplamı iddia ediyor. Süpürme işi hepsini topluyor ve **kayıp varsa log satırı
      Warning**, yoksa Information. Aynı boşluk **recurrence süpürmesinde de vardı** (`SkippedUnassigned` hiç
      toplanmıyordu) — o da düzeltildi.
    - **C. Bildirimler yanlış dilde** → ölçüldü, **BL-068** açıldı (kapsam: beş görev bildiriminin hepsi).
    - **Ortam notu (ürün kusuru değil):** Mailpit varsayılan olarak AUTH duyurmuyor; dev config dummy kimlik
      gönderdiği için MailKit `NotSupportedException` atıyor. Çözüm
      `mailpit --smtp-auth-accept-any --smtp-auth-allow-insecure`. Depoda dev kurulum belgesi **yoktu**;
      `docs/dev-environment.md` oluşturuldu (Mailpit + RabbitMQ + işlerin varsayılan kapalılığı + bildirim dili).
- **EK — 🟡 F. Süpürme, alıcısı olmayan görevde SÜRÜM ŞİŞİRİYOR (CT, 2026-08-11, açık kalem):**
  - **Ne yapıldı, doğru olan:** yukarıdaki A maddesiyle süpürme artık gönderim `Dispatched` değilse claim'i
    geri alıyor (`SendDueSoonRemindersHandler.cs:136-143` → `ReleaseClaimAsync:176-192`). Kaybolan
    hatırlatma sorununu bu çözdü.
  - **⚠ Yan etki — ölçüm:** geri alma da bir **expected-version yazımıdır** (`:180-181`, tıpkı claim'in
    kendisi gibi `:105-106`). Ve alıcı çözümü claim'den **SONRA** yapılıyor (`:117-118`). Yani alıcısı
    **kalıcı olarak** çözülemeyen bir görevde her süpürmede:
    ```
    damgala (v5) → NoRecipients → geri al (v6) → [bir saat] → damgala (v7) → …
    ```
    Sürüm saatte **2 artıyor**, görevde hiçbir şey değişmeden.
  - **Kullanıcıya yansıması:** görevi açar, bir saat sonra kaydeder, **"bu görev siz bakarken değişti"**
    eşzamanlılık uyarısını alır — oysa **kimse değiştirmemiştir**. Hata mesajı doğru ama olay yanlış.
  - **Bugün patlamıyor** (öyle bir görev yok), **ama olacak:** e-posta adresi olmayan kullanıcı ·
    arşivlenmiş havuz pozisyonu (`ResolvePoolHoldersAsync` boş döner) · tüm tutucuları çıkmış havuz.
  - **Öneri — mekanizma zaten orada:** `WouldNotify` claim'den **önce** soruluyor (`:89`) ve gerekçesi
    satırın kendi yorumunda yazılı (*"ASK BEFORE CLAIMING"*). **Alıcı çözümü de aynı yere alınsın:**
    gönderilemeyeceği önceden bilinen görev için damga **hiç atılmasın**. Yeni makine değil, var olan
    sıranın genişletilmesi.
  - **Yeniden ölçüm:** `rg -n "var audience" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/SendDueSoonRemindersHandler.cs`
    → bugün **117**, `WouldNotify`'ın (89) ve claim yazımının (105) **altında**; düzeltince 89-105
    arasına çıkmalı. Canlı: alıcısız bir görev bırak, iki süpürme sonrası `version` +4 olmamalı.

### BL-067 — 🟡 Yinelenen görevler şablonsuz doğuyor: hatırlatma, öncelik, etiket hiç ayarlanamıyor
- **Ölçüm (2026-08-11):** `GenerateDueRecurringTasksHandler.cs:209-238` şablonsuz dalda **sabit** bir istek
  kuruyor: `Priority: Medium · Tags: null · ReviewRequired: false · ApprovalRequired: false ·
  DelegationAllowed: false · FieldValues: null`, ve BL-065 sonrası `NotifyOnEvents`/`ReminderLeadDays` de
  **null**. Sonuç: **yinelenen görev due-soon hatırlatması hiç alamıyor** — süpürme yalnız kurşun süresi seçilmiş
  görevleri hatırlatır, burada seçen kimse yok. Hatırlatmaya en çok ihtiyacı olan görev tipi bu.
- **BL-065'in regresyonu DEĞİL:** şablon dalının zaten ince olmasının sonucu; BL-065 yalnız görünür kıldı.
- **Neden bu turda TAŞINMADI (karar + gerekçe):** tercihlerin doğru yeri `TaskTemplate`, ve **şablon yönetim
  ekranı yok** (BL-054). Kimsenin düzenleyemediği bir şablona alan eklemek, bu turlarda beş kez düzelttiğimiz
  *"saklanıyor ama ayarlanamıyor"* kusurunun aynısı olurdu. Kuralın (`TaskRecurrenceRule`) üstüne koymak da
  yanlış yer: kural **ne zaman**ı söyler, **ne**yi değil.
- **Sıra şartı:** BL-054 (şablon ekranı) → sonra bu madde. Yapılacak: `TaskTemplate`'e
  `DefaultNotifyOnEvents` + `DefaultReminderLeadDays` (+ `DefaultTags`, `DefaultPriority` zaten var mı ölç) ·
  şablon ekranında düzenlenebilir · `GenerateDueRecurringTasksHandler` şablonlu **ve** şablonsuz dalda bunları
  aktarsın.
- **Yeniden ölçüm:** `rg -n "NotifyOnEvents|ReminderLeadDays" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/GenerateDueRecurringTasksHandler.cs`
  (bugün yalnız açıklama satırı) · canlı: yinelenen kuraldan doğan görevi Düzenle'de aç — hatırlatma **seçilmemiş**
  gelmeli, ve bu beklenen davranıştır.

### BL-068 — 🟡 Görev bildirimleri kiracının dilinde gidiyor, OKUYANIN dilinde değil
- **Canlı gözlem (2026-08-11):** kiracı arayüzü Türkçe, şablonlar 7 dilde seed'li, ama hatırlatma **İngilizce**
  gitti: *"A task is due soon: CT BL-065 hatirlatma testi"*.
- **ÖLÇÜM — kusur değil, tasarımın sınırı:** `TenantNotificationLocaleResolver` zinciri **kaynaktan ölçüldü**:
  (1) çağıranın verdiği locale → (2) `Tenant.Settings.Language` → (3) `Tenant.DefaultLanguage` → (4) `"en"`.
  `TaskNotificationService` `Locale: null` geçiyor (kendi dokümanında yazılı: gönderenin UI kültürü **okuyanın**
  dili değildir), yani dil **kiracı kaydından** geliyor. Dev'de `TenantManagement:DefaultLanguage = "en"`
  (`appsettings.Development.json:78`) → kiracı kaydı "en" → e-posta İngilizce. Arayüz dili istek başına
  seçiliyor ve **hiçbir yere yazılmıyor**.
- **Kapsam duesoon DEĞİL:** beş görev bildiriminin **hepsi** aynı çözücüden geçiyor (`assigned · claimed ·
  duesoon · completed · approvalrequested`). Yani tek bir olayın değil, kanalın tamamının sorusu.
- **Kök eksik:** **kullanıcı başına dil alanı yok.** AuthService `User`/`PlatformUser` üzerinde Locale/Language/
  Culture alanı **yok** (ölçüldü), `internal/users/contacts` ucu id + ad + e-posta döndürüyor.
  `TaskNotificationService` sınıf dokümanı bunu zaten "eksik, eklendiğinde 1.5. halka olur" diye yazmış.
- **Bu turda DÜZELTİLMEDİ (karar):** küçük bir iş değil — ya (a) AuthService `User`'a dil alanı + contacts ucu +
  `TaskNotificationRecipient` alanı + dil grubuna göre çoklu dispatch, ya (b) en azından kiracı ayarları
  ekranından `Settings.Language`'in gerçekten yazıldığının doğrulanması. (a) servis sınırı aşıyor.
  **Ara çözüm önerisi (ucuz, dürüst):** kiracı dilini kiracı ayarlarından ayarlanabilir kılmak/doğrulamak —
  o zaman en azından "kiracının dili" doğru olur; okuyan başına dil (a) ile gelir.
- **Yeniden ölçüm:** `rg -n "Locale: null" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskNotificationService.cs` ·
  `rg -n "Language|Locale" services/Diten.AuthService/src/Diten.AuthService.Domain/Entities/*.cs` (bugün boş) ·
  canlı: kiracı kaydının `Settings.Language`'i ne, e-posta hangi dilde geldi.

### BL-069 — 🟢 Görev oluşturma formundan ÇIKARILAN üç alan: gerekçe ve geri getirme şartı
Kapanış turunda (2026-08-11) create formundan üç kontrol kaldırıldı. Üçü de **sözleşmede duruyor** — kaldırılan
yalnız formdaki soru. Bu madde, "neden yok?" sorusunun ve geri getirme şartının kaydıdır.

- **`organizationUnitId` (Organizasyon birimi) — kaldırıldı.** Backend'in kendi kuralı (pack §12 K6,
  `CreateTaskItemHandler.cs:139-155`): *her görevin bir birimi vardır ve kullanıcı asla birini seçmez*; kademe
  **istekteki değer → atananın pozisyonunun birimi → kök birim**. Form kutusu kademenin **1. basamağındaydı**,
  yani elle yazılan değer kişinin gerçek birimini **sessizce eziyordu** (Ahmet Finans'ta, "Ankara" yazılır, görev
  Ankara'ya dosyalanır, uyarı yok). Bilgi zaten ekranda: kişi/pozisyon seçenekleri *"Ad — Pozisyon — Birim"*
  basıyor. **Sözleşme:** `Guid? OrganizationUnitId` **nullable kalıyor** — sistem entegrasyonu birimi gerçekten
  biliyorsa gönderebilir. **Geri getirme şartı:** yalnızca kademe kaldırılırsa (o zaman zaten zorunlu olur).
- **`plannedDate` (Planlanan tarih) — kaldırıldı.** `PlanTaskItemCommand`
  (`TaskItemTransitionHandlers.cs:690-698`) görevi **Planned** durumuna taşır *ve* tarihi zorunlu kılar. Create
  ise tarihi yazıyordu (`CreateTaskItemHandler.cs:229`) ama yaşam döngüsünü taşımıyordu — *"planlanan tarihi var
  ama Planned değil"* diye doğan görev: aynı gerçek iki yerde, doğar doğmaz çelişiyor. Kartta yardım metni
  olmamasının sebebi de buydu. **Sözleşme:** duruyor, Planla geçişi kullanıyor. **Düzenlemede veri kaybı yok:**
  `UpdateTaskItemHandler` (`TaskItemWriteHandlers.cs:86`) `task.PlannedDate = request.PlannedDate` diye
  **koşulsuz** atıyor, dolayısıyla alanı sadece göndermemek **silerdi**; `buildUpdatePayload` saklı değeri
  taşıyor (`form.js`, `withheldOnEdit`).
- **`startAt` · `estimateHours` — kaldırılmadı, HEDEFE bağlandı.** Bitiş tarihi **isteyenin** taahhüdü
  ("ne zamana lazım", her hedefte zorunlu); başlangıç ve tahmin **yapanın** planı ("nasıl yetiştiririm"). Bir
  başkası adına plan yapmak, işini yerine koymaktır — SAP/Oracle'ın *talep eden deadline verir, kaynak schedule
  yapar* ayrımı. Yalnız hedef **"Kendim"** iken görünür; gizliyken **değer göndermez**, ama düzenlemede saklı
  değeri **ezmez** (aynı `withheldOnEdit` yolu).
- **Ölçüm:** `frontend/Diten.Web/tests/tasks-form-closing-round.test.js` (kalem 1-3) · geri getirilirse bu
  testler kırmızıya döner ve gerekçe burada okunur.

### BL-070 — 🟢 Ek alan tanımları: test artıkları emekliye ayrıldı, canlı örnek olarak "Pazar" bırakıldı
- **Ölçüm (2026-08-11):** `diten_personalization_dev.task_field_definitions` içinde AKTİF dört tanımın dördü de
  2026-08-10'da **mekanizmayı doğrulamak** için açılmıştı: `delivery.department` (Departman → ModuleRecord/
  organization-unit) · `delivery.position` (Pozisyon → ModuleRecord/position) · `regulatory.phase` (Faz → metin) ·
  `regulatory.market` (Pazar → BusinessReferenceData/country). Gerçek kiracının göreceği şey bu değildi; forma
  bakan *"görev formunda neden departman var?"* diye soruyordu.
- **Yapılan (veri, kod değil):** ilk üçü `IsActive: false` yapıldı — **silinmedi**, çünkü mevcut görevler bu
  tanımlara değer taşıyor olabilir ve ekranın kendi "pasifleştir" işlemi de budur (geri alınabilir).
  `regulatory.market` **aktif bırakıldı**: Ek alanlar kartı görünür kalsın ve BusinessReferenceData kaynağı
  canlı bir örnek olarak dursun.
- **Yeniden ölçüm:** aktif tanım sayısı 1 olmalı ve o tanım `regulatory.market` olmalı.
- **Not:** bu bir **dev veritabanı** temizliğidir; başka bir ortamda aynı artıklar varsa aynı işlem tekrarlanır.

### BL-071 — 🔴 Employee ↔ PositionAssignment ÇİFT KAYIT: "kim hangi koltukta" iki serviste birden yazılı
- **👤 SAHİPLİK — bu bizim KOD işimiz DEĞİL:** Employee modülünü **başka bir geliştirici** geliştiriyor. Bu
  madde bir **KARAR NOTU**dur ve ona ulaşması gerekir. Buradaki iş, kararı ölçümle birlikte kayda geçirmek.
- **⚠ ÖLÇÜM (CT, 2026-08-11) — aynı gerçek iki serviste:**

  | Nerede | Kayıt | Taşıdığı |
  |---|---|---|
  | **Platform** | `Organization/PositionAssignment.cs:5-20` | `UserId` · `PositionId` · `EffectiveFrom` / `EffectiveTo` · `AssignmentType` · `AllocationPercent` · `Reason` · `IsCancelled` |
  | **HCM** | `Diten.HcmService.Domain/Entities/EmploymentRecord.cs:3-27` | `EmployeeId` · `PositionId` · `OrganizationUnitId` · `LegalEntityId` · `StartDate` / `EndDate` · `ContractType` · `ProbationStatus` / `ProbationEndDate` · `EmploymentStatus` · `TerminationReasonCategory` · `RehireEligibility` |

  İkisi de aynı cümleyi kuruyor: **"kim, hangi koltukta, ne zamandan ne zamana."** HCM'inki belirgin
  biçimde daha zengin.
- **AYRIM — kararın özü:**
  - **Koltuğun KENDİSİ** (pozisyon, birim, raporlama zinciri) → **PLATFORM'un.** Bu org şemasıdır; görev
    atama, onay, yetki kapsamı ve havuz onu okur. İK'ya özel bir veri değil.
  - **Koltukta KİM OTURUYOR** (istihdam) → **HCM'in.** Sözleşme tipi, deneme süresi, çıkış sebebi, yeniden
    işe alınabilirlik — hiçbiri Platform'un sorusu değil.
- **ÖNERİ:** oturma bilgisinin **tek sahibi HCM** olsun. Platform sözleşme tipine ihtiyaç duymuyor; tek bir
  soruya cevap lazım: **"bu kullanıcı bugün hangi koltukta?"** HCM yayınlar, Platform **yansıma** tutar.
  Bu proje bu deseni zaten kullanıyor (modül self-registration → katalog reconcile ·
  [[project_catalog_permission_sync]] · [[project_entitlement_permission_plan_sync]]) — **yeni makine değil.**
- **⚠ ÖLÇÜM — bugün BİRLEŞTİRME ANAHTARI YOK, ve bu kararı zorlaştıran asıl şey bu:**
  Platform `PositionAssignment.UserId` (bir **login kimliği**) tutuyor; HCM `EmploymentRecord.EmployeeId`
  tutuyor ve `Employee` üzerinde **`UserId` alanı yok** — `Employee.cs:7` `PersonId` taşıyor
  (`Employee.cs:5-31` içinde `UserId` geçmiyor). Yani *"şu Employee şu login'dir"* diyen bir alan **hiçbir
  yerde yok.** Hangi tarafın hangi anahtarı yayınlayacağı kararın bir parçasıdır. **ÖLÇÜLMEDİ:**
  `PersonId`'nin AuthService `User` kaydıyla ilişkisi — bu turda kaynağı okunmadı.
- **GÖREV TARAFI DEĞİŞMİYOR:** `TaskItem.AssigneeUserId` kalır, çünkü işi **LOGIN yapar**. Hesabı olmayan
  bir Employee *"Tamamla"*ya basamaz. Karar ne olursa olsun görev sözleşmesi etkilenmez.
- **⏰ ZAMANLAMA — kararın maliyeti bugün sıfır:** HCM `employment_records` bugün **boş** (gerçek çalışan
  girilmemiş; **ÖLÇÜLMEDİ:** koleksiyon sayımı bu turda yapılmadı, tespit modülün henüz kullanıma
  alınmamış olmasına dayanıyor). **Bir tek gerçek kayıt girdiği an bedel "karar"dan "göç"e döner.** Karar
  HCM gerçek çalışan almadan **önce** verilmelidir.
- **İlgili:** [FG-002] (worker tipleri — danışman/kontratlı da koltuk alır) · [BL-057] (kapsam kuralı aynı
  atama verisini okuyor) · MOD-0288 (Organization) · MOD-0280 (İK).

**🔧 HAZIRLIK YAPILDI (2026-08-12) — göç KARARI değil, göç YÜZEYİ. Davranış değişmedi.**
- **Ölçüm (önce):** Features/Tasks altında **dokuz** dosya `IPositionAssignmentRepository`'yi doğrudan
  enjekte ediyordu ve hepsi `GetAllAsync` çağırıyordu — kimse dar bir soru sormuyor, herkes tüm tabloyu
  çekip bellekte süzüyordu. Aktiflik kuralı (`!IsCancelled && EffectiveFrom <= now && (EffectiveTo is null
  || EffectiveTo > now)`) **on** yerde elle yazılmıştı; kanonik hâli `TenantOrganizationMapper.IsActiveNow`
  (`TenantOrganizationContracts.cs:205`) Tasks tarafında **sıfır okuyucuyla** duruyordu.
- **Şimdi:** tek yüzey — `Features/Tasks/Services/TaskSeatDirectory.cs` (`ITaskSeatDirectory`). Dokuz
  çağıran da onu enjekte ediyor; repository'ye dokunan **tek** dosya odur.
- **Arayüz ÖLÇÜMLE türetildi** — çağrı yerlerinin gerçekten sorduğu beş soru:
  | Soru | Üye | Soranlar |
  |---|---|---|
  | (A) U bugün hangi koltuklarda? | `PositionIdsForUserAsync` · `ActiveForUserAsync` | WorkItemProvider · GetTaskItemList (id) · CreateTaskItem (satır, PRIMARY önce) |
  | (B) U şu koltuklardan birinde mi? | `HoldsAnyAsync` | ClaimTaskItem · TaskAssignmentDirection (yönetici zinciri) |
  | (C) Şu koltuklarda kim oturuyor? | `HoldersOfAsync` | TaskNotificationService (havuz) · TaskTeamResolver (astların pozisyonları) |
  | (D) Bugün dolu tüm koltuklar | `ActiveAsync` | iki lookup handler · reassign guard (TaskAssigneeEligibility) |
  | (E) Hiç koltuğa oturmuş herkes | `EverAssignedUserIdsAsync` | yalnız kişi lookup'ı — [BL-072]'nin eleme sayacı |
- **`IsActiveNow` TÜKETİLDİ, kopyalanmadı.** MOD-0288'in kendi varlığı için yazdığı kural; Tasks içine
  taşımak tekrarı kaldırmaz, yerini değiştirirdi. Semantik birebir doğrulandı (iptal ⇒ Ended ·
  `EffectiveFrom > now` ⇒ Planned · `EffectiveTo <= now` ⇒ Ended).
- **BU MADDE İÇİN ANLAMI:** HCM'e geçişte değişecek yer artık **on değil bir**. `EmploymentRecord`
  `StartDate`/`EndDate` alanlarını **`DateOnly`** taşıyor, Platform ise `DateTimeOffset` — yarı-açık aralık
  bu iki tipte **gün sınırında farklı cevap veriyor**. Tek yerde olması bu ayrımın bir kez ve bilerek
  kararlaştırılmasını mümkün kılar; on yerde olsa kaçırılan biri **çökmez**, sessizce eski koltuk verisinden
  cevap vermeye devam ederdi (ayrılmış kişi iş almaya devam eder).
- **Sınır (yapılmadı, bilerek):** yüzey bugün de `GetAllAsync` + bellek filtresi kullanıyor. **Davranış
  değişmeyecek** turuydu; indeksli/dar sorgu artık **mümkün** ama bu turun kapsamında değil.
- **Kilitleyen testler:** `TaskSeatDirectoryTests` — repository'ye dokunan dosya sayısı **= 1**,
  `EffectiveFrom` geçen dosya sayısı **= 1**, ve dokuz çağıranın hepsi yansımayla yüzeye bağlı. Onuncu
  kopya sessizce eklenemiyor.

### BL-072 — 🟡 Aday seçicide SESSİZCE eleniyor: beş sebep var, hiçbiri kullanıcıya söylenmiyor
- **⚠ ÖLÇÜM (CT, 2026-08-11):** `GetTaskAssignmentPersonLookupHandler.cs:60-92` bir kişiyi listeden
  **beş** sebepten biriyle atıyor ve **hiçbirini** söylemiyor:

  | # | Sebep | Satır |
  |---|---|---|
  | 1 | Aktif pozisyon ataması yok (hiç kayıt yok) | `:60-66` (döngüye hiç girmez) |
  | 2 | `EffectiveFrom` gelecekte / `EffectiveTo` geçmişte | `:62-63` |
  | 3 | Pozisyon `Draft` (veya `Active` değil) | `:80-81` |
  | 4 | Pozisyon ya da birim arşivli | `:80`, `:89` |
  | 5 | Atama iptal (`IsCancelled`) | `:61` |

- **Kullanıcının gördüğü:** hiçbir şey. Liste kısa gelir, sebep yok. **Somut vaka:** Ahmet sisteme eklenir,
  pozisyonu **Draft** bırakılır → *"Bir kişi"* listesinde **yok**, ve ekranda bunu açıklayan tek kelime yok.
  Bu, bu oturumda bizzat yaşanan sınıftan bir kusur.
- **Öneri:** seçicinin altında bir ipucu — *"N kişi aktif pozisyonu olmadığı için listelenmedi."* Sayı
  sunucudan gelir (elenen satır zaten sayılıyor olur); **kimlerin** elendiği **söylenmez** (o, kapsam
  dışı bilgi sızdırmak olurdu).
- **⛔ BAĞIMLILIK — [BL-057] ile AYNI TURDA yapılmalı.** Kapsam kuralı geldiğinde *"neden yok"* sorusunun
  cevabına **altıncı** bir madde eklenir (*"farklı şirkette"*). Ayrı turlarda yapılırsa aynı metin iki kez
  yazılır ve ikincisi birincisini bozar.
- **⚠ Bu, Create formuna DÖNMEK demektir.** Kapanış turunda *"bu sayfadan sonra dönmeyeceğiz"* denmişti;
  bu kalem için o söz **tutmuyor** ve bilerek kayda geçiriliyor. Gerekçe: ipucunun metni kapsam kuralının
  cevabına bağlı, dolayısıyla kapanış turunda yazılamazdı.
- **Yeniden ölçüm:** `rg -n "continue;" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/QueryHandlers/GetTaskAssignmentPersonLookupHandler.cs`
  (bugün elenen her dal sessiz) · canlı: bir kişiye Draft pozisyon ver, `/Tasks/Create` → *"Bir kişi"*
  listesinde yok ve ekranda gerekçe yok.
- **İlgili:** [BL-057] (aynı tur) · [BL-073] (SOP'un sessiz başarısızlık tablosu bu beş sebebi belgeliyor).

**✅ YAPILDI (2026-08-11) — [BL-057] ile aynı turda, planlandığı gibi.**
- Sunucu artık **kırılımı döndürüyor** (istemci tahmin etmiyor): `ExcludedCandidateSummary(Total,
  NoActivePosition, PositionNotActive, OutOfScope)`. Altıncı sebep — **kapsam dışı** — kapsam kuralıyla
  birlikte aynı anda eklendi, yani "neden yok" metni bir kez yazıldı.
- Beş sebep **üç kovaya** indi, çünkü kullanıcı için ayrımı olan budur: *aktif pozisyonu yok* (atama yok /
  tarih dışı / iptal) · *pozisyonu aktif değil* (Draft / arşivli / birimi arşivli) · *kapsam dışı*.
  Birden çok pozisyonu olan kişi **en iyi** sonucuyla raporlanıyor.
- **⚠ Güvenlik sınırı tutuldu:** `ExcludedCandidateSummary`'nin **her örnek üyesi `int`** ve bunu bir test
  çiviliyor (`The_exclusion_summary_NEVER_carries_a_name_or_an_identity`). Tarayıcı tarafında
  `describeExcludedCandidates` yalnız dört tam sayıyı okuyor — isim/kimlik taşıyan bir payload verilse bile
  aynı cümleyi üretiyor, ve bunun da testi var.
- **Ekranda (canlı, tr):** *"1 kişi listelenmedi: 1 kişi kapsamınız dışında"* — atanan seçicisinin altında,
  `d-none` sınıf geçişiyle (FG-003), `textContent` ile yazılıyor. Sıfır elenmişte **hiçbir şey** görünmüyor.
- **l10n:** 4 yeni anahtar × 7 dil (`ExcludedHint` · `ExcludedNoActivePosition` ·
  `ExcludedPositionNotActive` · `ExcludedOutOfScope`), hepsi `{0}` sayaç yer tutucusu taşıyor.
- **Create formuna dönüldü** — maddede yazılı olduğu gibi, ve sebebi buydu.

### BL-073 — 🔴 MOD-0024 çalışıyor ama kiracı onu KULLANIMA ALAMIYOR: ana veri zinciri hiçbir yerde yazılı değil
- **Sorun:** motor bitti, ekranlar var, testler yeşil — ama bir kiracının Görev Merkezi'ni **açıp
  kullanabilmesi** sıralı bir ana veri zincirinin doldurulmasına bağlı (şirket → birim → pozisyon →
  kullanıcı → **pozisyon ataması** → yönetici zinciri) ve bu zincir bugüne kadar **hiçbir belgede
  yazılı değildi**. Daha kötüsü: **her eksik halka sessiz başarısızlık üretiyor** — hata mesajı yok,
  boş liste var. 2026-08-11 oturumunda üç kez bizzat yaşandı.
- **📄 GÖVDE AYRI DOSYADA:** [`docs/workcenter-onboarding-sop.md`](./workcenter-onboarding-sop.md).
  Bu madde **işaret eder, içeriği kopyalamaz** — aynı gerçek iki yerde tutulmaz. SOP altı bölüm taşıyor:
  sıralı zincir · sessiz başarısızlık tablosu · opsiyonel yapılandırma · rol/sorumluluk · kabul
  kontrol listesi · en küçük çalışan kurulum.
- **Ölçüm dökümü (2026-08-11):** 16 önkoşul/başarısızlık/rota **ölçümle** yazıldı (dosya:satır ya da
  canlı HTTP durumu), **3 kalem ÖLÇÜLMEDİ** olarak işaretlendi. Dokuz rota oturum açılmış tarayıcıdan
  **200** doğrulandı.
- **⚠ En keskin tek bulgu:** `Position.Status` varsayılanı **`Draft`** (`Position.cs:19`) ve Draft
  pozisyondaki kişi atama seçicisinde **hiç görünmez**, **sebep söylenmez**
  (`GetTaskAssignmentPersonLookupHandler.cs:80-81`). Kurulumu en çok bu ısırıyor.
- **👤 Sahiplik:** SOP'un **bakımı** CT'de; **uygulanması** kiracı kurulumunu yapan ekipte (BT + İK +
  kiracı yöneticisi — SOP § Bölüm 4 dağılımı yazıyor).
- **🚦 GO-LIVE BAĞI — bu madde bir kapıdır:** SOP'un **§ Bölüm 5 kabul kontrol listesi** geçilmeden
  hiçbir kiracı canlıya alınmaz. Liste "kurulum bitti"nin ölçülebilir tanımıdır; "kaydettim, olmuştur"
  bu zincirde güvenilir değildir çünkü on bir sessiz başarısızlığın dokuzunda kullanıcı hata görmez.
- **Bu madde sessiz başarısızlıkları ÇÖZMÜYOR:** düzeltmeler [BL-072] (aday elenme ipucu) ·
  [BL-057] (şirket kapsamı) · [BL-065] § EK-F (sürüm şişmesi). SOP onları **belgeler**.
- **Yeniden ölçüm:** SOP'taki dokuz rotayı oturum açıp tekrar çağır (hepsi 200 olmalı) ·
  `rg -n "PositionStatus.Draft" services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/Position.cs` ·
  `rg -n "TaskDueSoonSweepJob|TaskRecurrenceSweepJob" services/Diten.Platform/src/Diten.Platform.API/appsettings.Development.json`
  (bugün ikisi de `false`).
- **İlgili:** [`dev-environment.md`](./dev-environment.md) (dev ortamı — SOP oraya işaret eder, kopyalamaz) ·
  [`workcenter-completion-plan.md`](./workcenter-completion-plan.md) (iş sırası) · [BL-074] (son kullanıcı
  kılavuzu — **ayrı okuyucu**: bu SOP yöneticiye, o kılavuz çalışana hitap eder).

### BL-075 — 🟢 Kişi/pozisyon seçicide grup başlığı BİRİM adı; ŞİRKET adını Platform bilmiyor
- **⚠ ÖLÇÜM (2026-08-12):** seçici satırları artık **birime göre gruplanıyor** (üç fabrikanın insanları
  karışmasın). Başlıkta şirket adı da olmalı mı diye ölçüldü: `AssignablePersonDto` /
  `AssignablePositionDto` **`LegalEntityId` taşıyor, ad taşımıyor** (`TaskModels.cs:517-529`) — Platform'da
  tüzel kişi **adı** yok. Adlar MDM'de (MOD-0220); tarayıcıya bugün yalnız doküman modülünün proxy'sinden
  ulaşıyor (`DocumentManagement/Instantiations/index.js:594`).
- **Bu turda verilen karar:** başlık **birim adı**. GUID basmak BL-049'un ta kendisi olurdu, ad uydurmak
  kabul edilemez; ayrıca sorulan soru zaten birim sorusu — *"fabrika"* bir organizasyon birimidir.
  **Aynı adlı iki farklı birim** varsa başlığa birim **kodu** ekleniyor (`Üretim (TR-URT)`), çünkü iki kez
  *"Üretim"* yazan başlık, başlıksızdan kötüdür — iki listeyi tek liste gibi gösterir.
- **Ne zaman iş çıkar:** başlıkta gerçekten ŞİRKET adı istenirse. O zaman gereken şey bir **isim
  çözücü**dür (Platform → MDM S2S ya da mevcut proxy'nin genelleştirilmesi) + DTO'ya ad alanı. Küçük değil:
  atama seçicisi sıcak yol, yani önbellek/hata davranışı da kararın parçası.
- **Regresyon riski:** 🟢 additive — bugünkü başlık zaten birim; şirket eklenirse başlık metni değişir,
  sözleşme (option value = id) değişmez.
- **İlgili:** [BL-057] (kapsam kuralı — `LegalEntityId`'yi DTO'ya bu tur eklemişti) · [BL-049] (ekranda GUID
  yasak) · MOD-0220.

### BL-074 — 🟡 Görev Merkezi SON KULLANICI EL KİTABI (⏳ metin UX turundan SONRA)
- **Okuyucu:** sistemi kullanacak **kiracı çalışanı**. Geliştirici değil, yönetici değil. Dolayısıyla
  dosya:satır **yok**, kod adı **yok**, *"handler / endpoint / DTO"* gibi kelimeler **yok** — yalnız
  kullanıcının **ekranda gördüğü** isimler.
- **⚠ [BL-073] ile karıştırılmayacak:** `workcenter-onboarding-sop.md` **kurulum/ana veri** dokümanıdır
  ve **yöneticiye** hitap eder. Bu, **kullanım** kılavuzudur. İkisi birbirine işaret eder, içerik
  **kopyalanmaz**.
- **📄 Dosya (bu turda İSKELET olarak açıldı):**
  [`docs/workcenter-user-guide.md`](./workcenter-user-guide.md) — 11 bölüm başlığı + her bölümün altında
  1-2 cümlelik *"burada ne anlatılacak"* notu + bağımlılık işaretleri. **Metin yazılmadı.**
- **⏳ NEDEN ŞİMDİ YAZILMIYOR:** metin ve ekran görüntüleri liste/detay/gelen kutusu **UX turu**
  bittikten sonra üretilir. Şimdi yazılırsa iki hafta içinde **yanlış** olur — bu, planın kendi
  *"Dokümantasyon en sonda"* kısıtıdır (`workcenter-completion-plan.md` § Neden bu sıra, madde 3).
- **Bölüm bazlı bağımlılık:**

  | Bölüm | Neye bağlı |
  |---|---|
  | 3. Ekranlarda ne nerede (sekme · segment · çip) | **UX turu** — kararların doğrudan çıktısı; en son yazılır |
  | 9. Kim kime iş verebilir | **[BL-057]** (kapsam) + **[BL-023]** (yukarı/aşağı) — kural henüz **yok**, yazılamaz |
  | 6. Onay ve inceleme → grup içi onay | **[BL-057]** § (b) örneği — onaycı listesi kapsamla değil rolle sınırlanır |
  | 8. Bildirimler | **[BL-065]** (görev başına tercih) + **[BL-068]** (dil) |
  | 4 · 5 · 7 · 10 · 11 | Bugünkü davranışla yazılabilir, ama **ekran görüntüleri** yine UX turunu bekler |

- **Yazım kuralları (metin turu için şimdiden kayıtlı):** her bölüm bir **SORUYA** cevap versin, özellik
  anlatmasın · ekran görüntüsü UX turu bitmeden **alınmayacak** · terimler ekrandaki Türkçe metinle
  **birebir** aynı olacak ve `Resources/Views/Tasks/TasksIndex.tr.resx`'ten doğrulanacak.
- **❓ SAHİBE AÇIK SORU — kılavuz kaç dilde olacak?** Tenant **ekranları** 7 dil zorunlu
  ([[feedback_tenant_l10n_seven_langs]]), ama bir **doküman** ekran değildir ve 7 dilde kılavuz bakımı
  her metin değişikliğinde 7 kat iş demektir. CT önerisi: **önce Türkçe**, diğer diller ayrı bir madde
  olarak ve gerçek talep geldiğinde. **Karar sahipte.**
- **Yapım tetikleyicisi:** liste UX turu (Aşama 1') bittikten sonra; § 9 için ayrıca [BL-057] + [BL-023].

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

### BL-076 — 🟢 Alan bazlı denetim izi: WC-1 YAŞAM DÖNGÜSÜNÜ kaydeder, alan değişikliklerini değil
- **Ölçüm (2026-08-12):** WC-1 ile `task_transitions` koleksiyonu geldi ve her yaşam döngüsü hareketi
  (oluştur · kabul · planla · başlat · devam · beklet · incelemeye gönder · inceleme iptali · tamamla ·
  iptal · üstlen · havuza bırak · başkasına ata · geri gönder) kayda giriyor. **Kapsam dışı bırakılan:**
  başlığın, bitiş tarihinin, önceliğin, etiketlerin veya ek alan değerlerinin değişmesi. `UpdateAsync`
  yalnız üç alanı (lifecycle · assignee · kabul işareti) diff'liyor; bir başlık düzenlemesi hiç kayıt üretmiyor.
- **Neden bilerek:** görevin hikâyesini anlatan altı satırı, onu anlatmayan altmış satırın altına gömmemek
  için. Alan bazlı izleme **ayrı** bir iş: kim hangi alanı ne zaman ne yaptı sorusunun kendi ekranı,
  kendi saklama maliyeti ve kendi yetki kuralı olur (bir maaş alanının eski değerini herkes göremez).
- **Gelecek regresyon riski: 🟢 katmerlenmez.** `TaskTransition` ayrı bir koleksiyon ve `Kind` enum'u
  append-only; alan denetimi geldiğinde kendi koleksiyonuna yazar, bu logu değiştirmez. Projeksiyon
  `kind` ile ayrıştığı için üçüncü bir kind eklemek de sözleşmeyi bozmaz — `ACTIVITY_KINDS` genişler.
- **Tetikleyici:** MOD-0024'te `IAuditableCommand` altyapısı kullanılmaya başlandığında (bugün Tasks
  tarafında hiç kullanılmıyor — bu, WC-1 ölçümünün ikinci bulgusuydu).

### BL-077 — 🟢 `personInitials` iki bundle'da iki kopya
- **Ölçüm (2026-08-12):** kişi monogramı (`AT`, `DK`) iki yerde ayrı yazılı: `assets/js/Tasks/form.js`
  (seçici satırları, MOD-0024 picker) ve `assets/js/WorkCenterNext/app.js` (WC-1 yorum satırı). Algoritma
  aynı — iki kelime varsa ilk+son baş harf, tek kelimede ilk iki karakter, locale-aware büyütme.
  Ayrıca `app.js` içindeki toplantı katılımcısı avatarı hâlâ ham `name.charAt(0)` kullanıyor: **aynı
  dosyada iki farklı kural**.
- **Neden şimdi birleştirilmedi:** `Tasks/form.js` bir IIFE ve dışa aktarım yüzeyi yok; ortak bir modül
  çıkarmak iki sayfanın script sırasını değiştirir ve bu tur ekran turu değildi. WC-1'de yapılan tek şey
  `app.js`'in **tek** bir algoritmaya sahip olması.
- **Gelecek regresyon riski: 🟡 sessiz tutarsızlık.** Biri düzelip diğeri kalırsa aynı kişi seçicide
  "AT", yorumda "AL" görünür ve bunu hiçbir test yakalamaz (iki dosyanın testleri birbirini bilmiyor).
  Birleştirmenin kendisi 🟢 — davranış değişmiyor, yalnızca kaynak tekilleşiyor.
- **Tetikleyici:** katılımcı avatarı da monogramla düzeltileceği zaman; üç çağrı yeri birden tek kaynağa alınır.

### BL-078 — 🟡 `task_assignments` listesi BL-030'a takılan sunucu tarafı sıralama yapıyor
- **Ölçüm (2026-08-12, WC-1 turunda yan bulgu):** `TaskAssignmentRepository.ListByTaskIdAsync`
  `.SortBy(x => x.OccurredAt)` çağırıyor. `OccurredAt` bir `DateTimeOffset` ve BL-030 gereği sürücü onu
  `[ticks, offsetMinutes]` BSON **dizisi** olarak yazıyor — yani bu sıralama dizinin ilk elemanına göre
  yapılıyor, tarih anlamına göre değil. `TaskCommentRepository` aynı sebeple **bellekte** sıralıyor ve
  yorumunda bunu açıkça yazıyor; bu çağrı o kuralın dışında kalmış.
- **Bugün neden patlamıyor:** tek anahtarlı sıralama çalışma zamanında hata vermiyor (paralel dizi hatası
  iki anahtar gerektiriyor) ve ofsetler dev ortamında aynı olduğu için sonuç doğru görünüyor. Farklı
  saat dilimlerinden yazılmış iki kayıt geldiğinde sıra sessizce bozulur.
- **Neden bu turda düzeltilmedi:** WC-1'in kapsamı yaşam döngüsü logu; `task_assignments` ayrı bir
  koleksiyon ve ayrı bir okuyucusu var. Kapsamı kendiliğinden genişletmemek için ölçüm kayda geçirildi.
- **Gelecek regresyon riski: 🟢 tek satırlık düzeltme** — `TaskTransitionRepository.Order` ile birebir
  aynı desen (bellekte, `Id` ile eşitlik bozma). BL-030 asıl çözümü (`DateTimeOffsetSerializer` + veri
  göçü) gelirse bu madde de onunla birlikte kapanır.

### BL-079 — 🟡 Kontrol listesi ŞABLONLARI: model var, okuma yolu YOK (düğme bu yüzden çizilmedi)
- **Ölçüm (2026-08-13):** `ChecklistTemplate` + `ChecklistTemplateItem` entity'leri, `IChecklistTemplateRepository`
  ve `ChecklistTemplateRepository` (koleksiyon `checklist_templates`) Faz 1'den beri duruyor.
  `CreateTaskItemRequest.ChecklistTemplateId` de duruyor ve `CreateTaskItemHandler` onu **gerçekten
  uyguluyor** (`TaskChecklistService.Instantiate`). Eksik olan tek şey **listeleme**:
  `IChecklistTemplateRepository.ListActiveAsync`'in **sıfır** çağıranı var — query handler yok, controller
  ucu yok, yönetim ekranı yok. Yani bir kiracı doğrudan veritabanına yazmadan şablon **oluşturamıyor**, ve
  tarayıcı mevcut olanları **listeleyemiyor**.
- **Bu turda ne yapıldı:** create formuna "Şablondan" düğmesi **KONULMADI**. Çekilemeyecek bir listeyi açan
  düğme, bu projede birkaç kez sökülmüş olan ölü kontroldür (`cappedList`'in boş `data-wcn-showall`'ı en
  sonuncusuydu). Kartın kendi yorumu ve `tasks-form-checklist.test.js` bu yokluğu kilitliyor: düğme geri
  gelirse test kırmızı olur.
- **Yapılacak iş üç parça, sırayla:** (1) `GetChecklistTemplatesQuery` + uç → form düğmesi anlam kazanır ·
  (2) şablon CRUD ekranı (`Views/Tasks/ChecklistTemplates`, alan tanımları ekranıyla aynı desen) ·
  (3) şablon maddeleri `LabelResourceKey` taşıyabildiği için **7 dil** sorusu: kiracının yazdığı şablon
  maddesi `LabelText`'tir, sistem şablonları `LabelResourceKey` — ikisi tek ekranda karışmamalı
  ([[project_nav_l10n_bridge]] ile aynı ayrım).
- **Gelecek regresyon riski: 🟢 tamamen eklemeli.** Create yolu şablon + serbest maddeyi **tek** run'da
  birleştiriyor ve sıra korunuyor (`A_template_and_typed_items_make_ONE_list_in_the_order_shown`), yani
  şablon listeleme geldiğinde birleştirme mantığı yeniden yazılmaz; sadece id'yi seçen kontrol eklenir.

### BL-080 — 🔴 Görev ↔ belge bağı: TEK mekanizma, ÜÇ amaç (referans · kanıt · kapanış raporu)
- **Ölçüm (2026-08-13):** MOD-0024'te görevi bir belgeye bağlayan **hiçbir alan yok** — ne `AttachmentId`,
  ne `DocumentLink`, ne bir ara tablo. `TaskItem` sınır notu bunu bilerek söylüyor (pack §12 Y4:
  *"Attachments are out of scope; binary storage belongs to an approved document/storage provider"*).
  Buna karşılık **doküman modülü canlı**: gateway `/api/v1/document-management/{**catch-all}` → `localhost:5057`
  (Platform ile **aynı** servis), ve canlı çağrı **401** döndü — yani uç var, yalnız yetkilendirme istiyor (404 değil).
  `ChecklistRunItem.EvidenceRequired` Faz 1'den beri saklanıyor ve **hiçbir şeyi zorlamıyor**.
- **⚠ ÖLÇÜM SONUCU — madde kimliklerini SUNUCU üretiyor.** `adhoc-{Guid:N}` iki yerde de sunucuda mintleniyor
  (`CreateTaskItemHandler.cs:405`, `ChecklistHandlers.cs:160`); istemcide tek bir `adhoc` geçmiyor.
  **Sonucu:** create anında istemcinin elinde madde kimliği YOKTUR → create'te belge yalnız **göreve**
  bağlanabilir, tek tek maddeye bağlanamaz. Maddeye kanıt bağlamak ancak görev kaydedildikten sonra (detay
  sayfasında, kimlikler dönmüşken) mümkün. Tasarım bu kısıtla kurulmalı; alternatifi istemci tarafı kimlik
  üretimine geçmektir ve bu, kimliğin sahibini değiştiren ayrı bir karardır.
- **Tek mekanizma, üç amaç — ayrışmasın:** referans (create'te var olan belgeyi göster) · kanıt (çalışırken,
  maddeye) · kapanış raporu (kapanışta, göreve). Üçü **ayrı yetenek** olarak kurulursa göreve belge bağlamanın
  üç yolu olur ve üçü ayrı ayrı bozulur. **Kapanış raporu YENİ bir yetenek değil**, aynı bağın `purpose`
  alanıyla ayrılan farklı bir amacıdır.
- **Kapsam:** veri modeli (bağ + `purpose` + isteğe bağlı `checklistItemCode`) · seçme yüzeyi ·
  **YETKİ** (doküman erişim matrisiyle kesişim — *görevi gören belgeyi görebilir mi?* Bu **yapısal yan etki
  olarak verilemez**; görev görünürlüğü belge görünürlüğü demek değildir) · kapı (*"kanıtsız işaretlenemez"*).
- **Kapı en sona:** `EvidenceRequired`'ı gerçekten zorlamak ancak **saklama politikası** (hangi klasör, hangi
  ad, ne kadar süre) geldikten sonra açılabilir — politikasız bir kapı, kullanıcıyı belgeyi nereye koyacağını
  bilmeden engeller. Politika **sahibin yöneticisinden** gelecek.
- **Bu turda ne yapıldı:** ataç KALDI, bayrak saklanmaya devam ediyor (veri kaybı yok), ama artık ne olduğunu
  ve ne zaman işe yarayacağını **söylüyor** — `ChecklistEvidenceHint`, 7 dilde, hem create formunda hem detay
  kartında. 13. maddenin dürüstlüğü bu backlog maddesinin varlığına bağlıydı.
- **Gelecek regresyon riski: 🟡 yetki kesişimi.** Veri modeli ve yüzey 🟢 eklemeli; **yetki** kısmı foundation'a
  dokunuyor — belge erişimi görev erişiminden türetilirse geri alınması zor bir sızıntı olur.

### BL-081 — 🟡 `_Layout.cshtml` sortablejs'i CDN'den çekiyor, yerel kopya duruyor
- **Ölçüm (2026-08-13):** `Views/Shared/_Layout.cshtml:581` →
  `<script src="https://cdn.jsdelivr.net/npm/sortablejs@1.15.0/Sortable.min.js">`.
  Oysa `wwwroot/assets/vendor/libs/sortablejs/sortable.js` (129K) depoda **var**. Kullanan ekran:
  `Views/Governance/TenantNavigationSettings/Index.cshtml`.
- **İki ayrı sorun:** (1) **internetsiz kurulumda sessizce bozulur** — sayfa açılır, sürükleme çalışmaz, hata
  yok · (2) dışarıdan yüklenen script **tedarik zinciri yüzeyidir**; CDN'deki bir değişiklik doğrudan tenant
  tarayıcısında çalışır (SRI hash'i de yok).
- **Bu turda ne yapılmadı:** dokunulmadı. Görev Merkezi'nin create formu kendi `<script>`'ini **yerel**
  kopyadan yüklüyor (`Views/Tasks/Create.cshtml`) ve `tasks-form-checklist.test.js` o sayfada dış host
  olmadığını kilitliyor — ama `_Layout` başka bir sayfanın işi, ayrı tur.
- **Yapılacak:** CDN satırını yerel yolla değiştir, navigasyon ayarları ekranında sürüklemeyi doğrula.
- **Gelecek regresyon riski: 🟢 tek satır**, davranış değişmiyor (aynı sürüm, aynı API).

- **DÜZELTME + İKİNCİ TÜKETİCİ (2026-08-14).** Bu maddenin kapsamı yukarıda **fazla geniş** yazılmıştı; canlı
  ölçüm daralttı:
  - `_Layout.cshtml:581`'deki CDN satırı Görev Merkezi'ne **hiç ulaşmıyor**. `Views/WorkCenterNext/Details.cshtml`,
    `Index.cshtml` ve bütün `Views/Tasks/*` sayfaları `Layout = "_LayoutTenantShell"` kullanıyor. Canlı ölçüldü:
    detay sayfasında `typeof window.Sortable === "undefined"` ve DOM'da tek bir `sortable` script etiketi yok.
  - Yani CDN satırının **bilinen tek tüketicisi** hâlâ `Views/Governance/TenantNavigationSettings/Index.cshtml`.
    "Yerel kopya kullanılmadan duruyor" ifadesi de doğru değil: `Views/Tasks/Create.cshtml` onu zaten yüklüyor.
  - **BU TUR EKLENEN İKİNCİ VE ÜÇÜNCÜ TÜKETİCİ — ikisi de YEREL kopya, CDN değil:** BL-094 kapsamında
    `Views/WorkCenterNext/Details.cshtml` ve `Views/WorkCenterNext/Index.cshtml`
    `~/assets/vendor/libs/sortablejs/sortable.js` yüklüyor (canlı: 200 OK, dış host isteği yok). Bağımlılık
    **derinleşmedi** — CDN'in tüketici sayısı artmadı, yerel kopyanınki arttı.
  - **Kalan iş aynı ve hâlâ tek satır:** `_Layout.cshtml:581` → yerel yol; etkilenen tek ekran navigasyon
    ayarları, orada sürükleme doğrulanacak.

### BL-099 — 🟡 Yapılandırılabilir alan BÖLÜM adı serbest metin: varyantlar sessizce ayrı grup oluyor  <!-- numara çakışması düzeltildi: eskiden BL-082, JWT kalemiyle aynı numarayı taşıyordu -->
- **Sahip sorusu (2026-08-13):** *"aynı alana `Regulatory` diye başka bir alan eklenirse ne olacak?"*
- **ÖLÇÜM (CT, canlı):**
  - `TaskFieldDefinition.Section` **serbest metin** (`required string`, enum değil) —
    `TaskSupportingEntities.cs:187`. Alan Tanımları formunda `maxlength=64` bir `<input>`.
  - İstemci sıralaması: `section` (localeCompare) → `sortOrder` → `code` (`form.js:975-978`).
  - Gruplama: `section !== currentSection` — **tam dizgi eşleşmesi** (`form.js:1004`).
  - Sözleşme sınırı: `TaskFieldLimits.MaxSections = 6`.
- **Sorulan durumun cevabı: SORUN YOK.** Aynı yazımla eklenen ikinci alan aynı başlığın altına girer;
  sıralama onları yan yana getirir, tek başlık basılır. Bugünkü davranış doğru.
- **Kırılgan olan VARYANTLAR — kayıt sebebi bu:**
  - `Regulatory` / `regulatory` / `Regulatory ` (sondaki boşluk) → **üç ayrı grup**, ikisi ekranda
    **birbirinin aynı görünür**. Kullanıcı "aynı bölümü yazdım" der, ekran ona katılmaz.
  - `Regulatory` / `Mevzuat` → aynı kavram, iki grup, ve `localeCompare` onları **birbirinden uzağa** koyar.
  - Her varyant `MaxSections = 6` kotasından bir yer yer. Altı yazım hatası kotayı doldurur.
- **Ayrıca dil tutarsızlığı (aynı kökten):** bugün dev kiracıda Türkçe arayüzde `Regulatory` başlığı altında
  `Pazar` alanı duruyor — aynı satırda iki dil. Bölüm adı kiracının kelimesidir ve çevrilmemelidir (doğru
  karar, `form.js:1002` bunu açıkça yazıyor); ama kiracıya **kendi dilinde yazmasını** kolaylaştıran hiçbir şey
  yok: ne öneri, ne var olan bölümlerden seçme, ne "bu bölüm zaten var" uyarısı.
- **Değerlendirilecek çözümler (karar verilmedi):**
  - *(a)* Bölüm alanı serbest metin kalsın ama **var olan bölümlerden seçmeli** olsun (yazarak yeni de eklenir) —
    Tagify/select2 "create" deseni; varyant üretmeyi zorlaştırır, kiracı sözcüğünü elinden almaz.
  - *(b)* Kaydederken **normalize et** (trim + büyük/küçük duyarsız eşleştirme) ve yakın bir bölüm varsa uyar.
  - *(c)* Dokunma; kotanın dolması ve ikiz başlıklar kabul edilsin.
  - ⚠ Enum'a çevirmek ÖNERİLMEZ: bölüm kiracının kavramıdır, ürünün değil (BL-024 Faz 1 kararıyla tutarlı).
- **`MaxSections = 6` ÖLÇÜLMEDİ:** sınırın nerede ZORLANDIĞI (sunucu doğrulaması var mı, yoksa yalnız
  sözleşme metni mi) bu turda ölçülmedi. Çözüm turunda ilk iş bu olmalı — zorlanmıyorsa varyantlar sessizce
  altıyı aşar.
- **Yeniden ölçüm:** `grep -n "Section" services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskSupportingEntities.cs` ·
  `sed -n '973,1006p' frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js` ·
  `grep -n "MaxSections" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskModels.cs`
- **Gelecek regresyon riski: 🟢** — bugünkü davranış doğru, madde bir SERTLEŞTİRME. Ertelenirse kiracı verisinde
  ikiz bölümler birikir ve sonradan birleştirmek **veri göçü** olur (bugün üç tanım var, sonra üç yüz).
- **İlgili:** BL-024 (alan-seviyesi yetki, aynı tanım kaydı) · FG-003 değil, veri modeli.

### BL-084 — 🟢 Etkinlik kartının zaman çizgisi jsdom'da ÖLÇÜLEMİYOR (::before/::after)
- **Ölçüm (2026-08-13, D4 turu):** zaman çizgisi tamamen CSS sahte elemanlarıyla kuruldu (`.wcn-audit-item::before`
  nokta, `::after` çizgi). `wcn-boot` harness'ı **gerçek app.js**'i jsdom'a yüklüyor (sahte değil) ama jsdom
  harici stylesheet uygulamıyor: `getComputedStyle(el,'::before')` boş döner. Yani nokta boyutu, çizgi yüksekliği
  ve ilk/son kırpması **birim testiyle korunamıyor**.
- **Bu turda ne yapıldı:** geometri **canlı** ölçüldü (iki sayfa × iki genişlik × iki tema, değerler raporda) ve
  testte yalnız markup tarafı kilitlendi (`.wcn-audit-event` / `.wcn-audit-comment` sınıfları hâlâ ayrı).
  Yani bir yeniden adlandırma kırmızı olur, ama `::after`'ın `content: none` kırpması silinirse **hiçbir test
  görmez**.
- **Seçenekler:** (a) jsdom yerine gerçek tarayıcıda çalışan bir görsel/CSS test katmanı (Playwright) — bu
  projede henüz yok, kurulumu ayrı bir karar · (b) noktayı gerçek bir DOM elemanına çevirip sınıfını test etmek —
  ama D4 açıkça "CSS ile, DOM'a sarmalayıcı ekleme" diyordu · (c) CSS'i ayrıştıran bir regex testi (kırılgan,
  önerilmez).
- **Gelecek regresyon riski: 🟡 sessiz.** Çizgi kaybolursa ya da uçları kırpılmazsa üretimde görünür, testte
  görünmez. Playwright kararı verilene kadar bu maddenin varlığı uyarı görevi görüyor.

### BL-085 — 🟢 Rozetin filtre bağımsızlığı CANLI veriyle ispatlanamıyor (12 olay eşiği)
- **Ölçüm (2026-08-13):** "yalnız yorumlar" çipi ancak **12+ olay** varken çiziliyor
  (`ACTIVITY_FILTER_MIN_EVENTS = 12`). Kiracıdaki iki gerçek görevin olay sayısı **8 ve 6**. Dolayısıyla
  "rozet filtre uygulanınca değişmiyor" kuralı **canlı sayfada tetiklenemedi**; yalnız gerçek app.js'i süren
  birim testinde (14 kayıt: 12 olay + 2 yorum) ölçüldü.
- **Neden bırakıldı:** 12 olaylı gerçek görev üretmek için bir görevi 12 kez durum değiştirmek gerekir; bu, test
  verisi uğruna kiracı verisini kirletmek olur. Eşiği düşürmek de ürün kararını teste feda etmek olurdu.
- **Yapılacak (isteğe bağlı):** dev sandbox'ta 12+ geçişli bir tohum görev; o zaman kural canlı da ölçülür.
- **Gelecek regresyon riski: 🟢** — kural birim testinde kilitli, yalnız canlı kanıt eksik.

### BL-086 — 🟢 Kaynak tarayan testler YORUMLARI da tarıyor (kuralı açıklayan metin kuralı düşürüyor)
- **Ölçüm (2026-08-13, sekme turu):** `wcn-detail-three-regions.test.js` ve
  `workcenter-next-detail-page.test.js` app.js'i **ham metin** olarak tarıyor. Bu turda **üç** test, yazdığım
  **yorumlar** yüzünden kırmızı oldu — kodu değil:
  (a) `NO TABS` kilidi, "rail asla sekmenin içine girmez" diye açıklayan yorumdaki `role="tablist"` kelimesine
  takıldı · (b) bölge sırası testi, yorumda geçen `.wcn-detail-head`/`.wcn-detail-content` adlarına takıldı ·
  (c) `ago:` kilidi, "a few rounds ago:" cümlesine takıldı.
- **Bu turda ne yapıldı:** `detailHtml()` yardımcısına **yorum ayıklama** eklendi (l10n paketinde zaten var olan
  `stripComments` disiplini) — bu, testi zayıflatmaz, ölçtüğünü iddia ettiği şeyi ölçmesini sağlar. `ago:` kilidi
  ise **dokunulmadı**; onun yerine kendi cümlemi yeniden yazdım: kilit kasten kaba ve nesir uğruna gevşetilmemeli.
- **Kalan iş:** aynı ham-metin taraması `workcenter-next-detail-page.test.js` ve `tasks-form-checklist.test.js`
  içinde de var. Bugün kırmızı değiller, ama bir sonraki iyi yorum onları da düşürebilir.
- **Gelecek regresyon riski: 🟡 yanlış alarm.** Kırmızı olduğunda kod doğrudur ve okuyucu testin haklı olduğunu
  varsayıp iyi bir açıklamayı siler. Ortak bir `sourceOf(name, {stripComments:true})` yardımcısı doğru cevap.

### BL-087 — 🟢 Detay sekmesi seçimi URL'de tutulmuyor (yalnız #etkinlik ile açılış var)
- **Ölçüm (2026-08-13):** `#etkinlik` ile açılış **çalışıyor** (canlı doğrulandı: hash → Etkinlik sekmesi seçili,
  panel görünür). Ama sekme değiştirmek URL'i **güncellemiyor**: kullanıcı Etkinlik'e geçip bağlantıyı kopyalarsa
  karşı taraf Genel'de açar. Liste sayfası kendi durumunu `syncUrl`/`replaceState` ile yansıtıyor; detay sayfası
  yansıtmıyor.
- **Neden bu turda yapılmadı:** brief **kalıcılık istemiyorum** dedi ve URL yazımı kalıcılığın bir biçimi;
  ayrıca `#etkinlik`'in bugünkü tek tüketicisi D7'de gelecek yorum bildirimi. Kapsamı kendiliğinden genişletmedim.
- **Yapılacak (D7 ile birlikte değerlendirilmeli):** sekme değişiminde `history.replaceState(null,'','#etkinlik')`
  / hash temizleme — üç satır, ama "paylaşılan bağlantı ne göstermeli" kararı sahibin.
- **Gelecek regresyon riski: 🟢 eklemeli.**

### BL-088 — 🟡 `renderSplit` / `.wcn-split-detail` ölü kod; ad çakışmasının kaynağı burası
- **Ölçüm (2026-08-13):** `renderSplit` (app.js:1342) **hiçbir yerden çağrılmıyor** — grep ile tek eşleşme
  tanımın kendisi. Görünüm düğmeleri yalnız `list` ve `table` üretiyor (canlı ölçüm: `data-wcn-view=list`,
  `data-wcn-view=table`). Yani `splitCard`, `.wcn-split-detail` ve ona bağlı CSS bloğu çalışmayan koddur.
- **Neden önemli:** bu tur kaybedilen zamanın tamamı buradan çıktı. `.wcn-detail-tabs` adı **bu ölü bileşene**
  aitti ve `position: sticky` + kenarlık + yarıçap + `backdrop-filter` + `margin-block-start: 1rem` taşıyordu.
  Detay şeridine aynı adı verince şerit bunların hepsini giydi: iki kılpayı çizgi ve rayla 16px hizasızlık.
  Kusur hiçbir zaman detay sayfasının dosyasında değildi. **Çalışmayan kod da isim alanını işgal eder.**
- **Yapılacak:** ya split görünümü gerçekten bağlanır ya `renderSplit`/`splitCard`/`.wcn-split-detail` kaldırılır.
  Karar sahibin: split görünümü ürün planında mı, değil mi?
- **Gelecek regresyon riski: 🟢 eklemeli** — bugün hiçbir şey çizmiyor; silinmesi ekranı değiştirmez.

### BL-089 — 🟢 Kart yüzeyi CSS değişkenleriyle okunamıyor (`--bs-card-bg` boş dönüyor)
- **Ölçüm (2026-08-13):** `getComputedStyle(root).getPropertyValue('--bs-card-bg' | '--bs-card-border-radius' |
  '--bs-card-box-shadow')` **üçü de boş dize** döndürüyor; değerler yalnız `.card` kuralının içinde yaşıyor.
- **Sonuç:** "var olan kart yüzeyini kullan" demek pratikte "`.card` sınıfını kullan" demek — bir bileşen yüzeyi
  değişkenle **alıntılayamıyor**, sınıfı giymek zorunda. Bu turda doğru sonuca çıktı (şerit `card` + `card-body p-3`,
  liste sayfasının kendi şeridiyle aynı iki satır), ama sınıf giyilemeyen bir yerde tek yol elle renk yazmak olur —
  FG-003'ün korumadığı, ikinci bir kart tonunun doğduğu yer tam burasıdır.
- **Yapılacak:** Sneat kart değişkenlerini `:root`'a köprüle (`--dt-card-bg: …` vb.), tek kaynak kalsın.
- **Gelecek regresyon riski: 🟢 eklemeli.**

### BL-090 — 🟢 Detay sayfası 1024'te değil 992'de tek sütuna iniyor; 992–1200 arası rayın kendi tasarımı yok
- **Ölçüm (2026-08-13):** sütunlar `col-lg-8` / `col-lg-4`; Bootstrap `lg` = **992px**. 1024px'te ray hâlâ SAĞDA
  (canlı ölçüm: `stacked:false`, hiza 0, tepe 379/379). Yığılma 900px'te doğrulandı (`stacked:true`).
- **Bu turda yapılan:** yığılmış durumda içerik son kartı ile ray ilk kartı arasındaki dikiş **16px** ölçüldü —
  sayfadaki tek 16px, çünkü iki sütun hiç buluşmadığı bir düzenden artakalan çıplak satır oluğuydu. Tek sütunda
  bunlar artık kart-karta bir aralık; `@media (max-width: 991.98px)` içinde **24px**'e getirildi.
- **Açık kalan:** 992–1200 arasında ray ~%33 × ~1000px ≈ 330px'e düşüyor; "Mevcut aksiyonlar" düğmeleri ve durum
  kartı bu genişlik için ayrıca tasarlanmadı. Tabletin kendi kırılma noktası kararı sahibin.
- **Gelecek regresyon riski: 🟢 eklemeli** — mevcut iki kırılma noktası korunuyor.

### BL-091 — 🟡 `ChecklistRequiredOpen` artık adını yalanlıyor; çoğul biçimler hâlâ "item(s)" hilesiyle
- **Ölçüm (2026-08-13):** anahtar adı bilinçli olarak KORUNDU (kablo değeri `Required` ve enum ile aynı hizada
  kalsın diye), ama gösterdiği metin artık "beklenen / expected". Yani anahtar adı ile içeriği ayrıştı.
- **İkinci ve daha ciddi kusur:** yedi dilin hiçbirinde gerçek çoğul kuralı yok — `{0} élément(s) attendu(s)`,
  `{0} elemento(s) esperado(s)`, `{0} expected item(s)`. Rusça'nın üç çoğul biçimi, Arapça'nın altısı var;
  parantezli "(s)" hepsinde yanlış. Bugün sayı her zaman ≥1 olduğu için kimse fark etmiyor.
- **Yapılacak:** ICU MessageFormat / `.resx` çoğul desteği kararı — bu tek dize için değil, sayı içeren TÜM
  dizeler için tek seferde. Anahtar adı yeniden adlandırması ancak o göç sırasında anlamlı olur.
- **Gelecek regresyon riski: 🟡** — çoğullaştırma altyapısı gelirse sayı içeren her dize yeniden yazılır.

### BL-092 — 🟡 Kontrol listesi yazmalarının HİÇBİRİ task_transitions'a düşmüyor
- **Ölçüm (2026-08-13):** geçmiş günlüğü `TaskItemRepository.UpdateAsync` içinde bir GÖREVİ diff'leyerek yazılıyor.
  Kontrol listesi yazmalarının tamamı `ChecklistRunRepository` üzerinden **RUN**'a gidiyor, yani bugün ne "ekle"
  ne "işaretle" bir geçiş kaydı üretiyor — bu turda eklenen üç fiil de üretmiyor.
- **Neden bu turda yapılmadı:** brief "bugün ne yapılıyorsa aynısını yap, ayrışma olmasın" dedi ve ölçüm bunu
  destekledi. Yeni üç fiilin günlüğe düşüp eski ikisinin düşmemesi, akışı YENİ bir biçimde yalancı yapardı:
  okuyan kişi "madde silindi" satırını görüp geri kalanına dokunulmadığı sonucunu çıkarırdı.
- **Yapılacak:** kontrol listesi için tek bir geçiş yazımı — beş fiil birden, ayrı ayrı değil. Karar noktası:
  her tik bir satır mı (gürültü), yoksa yalnız yapı değişiklikleri (ekle/sil/sırala/seviye) mi?
- **Gelecek regresyon riski: 🟡** — akışın "tam" olduğu iddiası bugün de doğru değil; eklendiğinde geçmişin
  yeniden yorumlanması gerekir.

### BL-093 — ✅ KAPANDI (2026-08-13) — `AddChecklistItem` kapalı görevi reddetmiyor
- **Ölçüm (2026-08-13):** `SetChecklistItemStateHandler` kapalı görevde 409 döndürüyor (ChecklistHandlers.cs:61).
  `AddChecklistItemHandler` bu denetimi **hiç yapmıyor** — Done/Cancelled bir göreve yeni madde eklenebiliyor.
  Bu turda eklenen üç fiilin üçü de reddediyor (canlı doğrulandı: iptal edilmiş görevde `TASK_INVALID_STATE`).
- **Sonuç:** aynı kartın beş fiilinden dördü kapalı görevi reddediyor, biri kabul ediyor.
- **Kapanış (2026-08-13):** `AddChecklistItemHandler` artık Done/Cancelled görevde 409 `TASK_INVALID_STATE`
  dönüyor. `ChecklistWriteGuards.ResolveAsync` ile DEĞİL, elle: bu fiil meşru olarak henüz RUN YOKKEN çalışıyor
  (ilk maddeyi ekleyen o), resolver'ın "bu görevin kontrol listesi yok" 404'ü diğer dördü için doğru, bunun için
  yanlış olurdu. İki test: `BL_093_a_closed_task_can_no_longer_GROW_new_checklist_items` (Done + Cancelled).
- **Gelecek regresyon riski: 🟠** — bugün ön yüz kapalı görevde ekleme kutusunu gizliyor, yani kusur yalnız
  API seviyesinde erişilebilir; ön yüz kilidi güvenlik değildir.

### BL-094 — ✅ KAPANDI (2026-08-14) — Detay sayfasında sürükle-bırak: karar DEĞİŞTİ, yapıldı

> **KARAR DEĞİŞİKLİĞİ.** Bu maddenin ilk hâli "hayır" diyordu. Silinmedi, **yeniden yazıldı** — aşağıda önce
> eski gerekçe, sonra hangi dayanağının düştüğü, sonra ölçüm var.

- **Eski karar (2026-08-13) ve gerekçesi:** create formunda Sortable vardı, detayda yoktu. "Sürükle gelirse taşı
  düğmeleri zorunlu" deniyordu; tersi zorunlu değildi. Düğmeler WCAG 2.2 §2.5.7 karşılığı olarak tek başına
  yeterliydi, sürükleme bir kolaylıktı — ve **iki ayrı bileşen** vardı, yani iki ekranın farklı davranması
  savunulabilirdi.
- **Düşen dayanak (2026-08-14):** artık **tek bileşen** var (`assets/js/shared/diten-checkitem.js`). Aynı satırın
  bir ekranda sürüklenip diğerinde sürüklenmemesi, bileşenin bitirmek için var olduğu ayrışmanın ta kendisi.
  Sahip kararı: al — **iki şartla**.
- **Şart 1 — OK DÜĞMELERİ KALDI.** Sürükleme düğmelerin ÜSTÜNE eklendi, yerine değil. §2.5.7'nin istediği
  tek-işaretçi alternatifi ve klavye yolunun tamamı onlar (Sortable'ın klavye hikâyesi yok). Canlı doğrulandı:
  sürüklemeden SONRA `data-diten-check-move="down"` tıklandı → sıra değişti, düğme `disabled=false`, odak alıyor.
- **Şart 2 — YIĞILMIŞ DÜZENDE ÖLÇÜLDÜ.** 900×1600, `.wcn-detail-content` = 869px (tek sütun), sayfa kaydırılmış
  (`scrollingElement.scrollTop = 64`), `pointerType: 'touch'` pointer olayları. 1. satır griple tutulup 3. satırın
  %75'ine bırakıldı → DOM sırası tam bırakılan yere geçti, `Kontrol listesi güncellendi.` bildirimi, ve sunucu
  projeksiyonu yeni sırayı doğruladı. 1440×1800'de de aynısı (4. konuma bırakma) çalıştı. Kaydırma kaynaklı
  konum kayması YOK.
- **Ne ölçülemedi (dürüstçe):** tarayıcı panelinin gerçek dokunmatik emülasyonu (`width < 768` gerektiriyor, oysa
  şart 900px'di) ve 64px'ten daha derin bir kaydırma — barındırılan tarayıcı paneli gizliyken gerçek girdi çağrısı
  30 sn'de zaman aşımına uğruyor ve programatik `scrollTop` ataması reddediliyor. Kullanılan yöntem: sentetik
  `pointerType:'touch'` olayları (Sortable `forceFallback` ile bunları gerçek girdi gibi işler — bileşenin
  yorumunda "el dışında hiçbir şeyle sınanamaz" diye kaydedilen native DnD'den kaçınmanın sebebi tam da bu).
- **Uygulama:** `.wcn-checks` üzerine Sortable (`bindChecklistDrag`), `handle: '[data-diten-check-grip]'`,
  `forceFallback: true` — create formuyla birebir aynı ayarlar. Grip artık iki kipte de çiziliyor. Bırakma sırası
  **projeksiyondan** hesaplanıyor, DOM yalnız INDEKS'i veriyor. Kapalı görevde Sortable hiç bağlanmıyor.
- **Gelecek regresyon riski: 🟢 eklemeli** — düğmeler yerinde, kapalı görev korunuyor.

### BL-095 — 🟠 Sıralama ucu sahiplik sormuyor; sıra da bir anlam taşıyabilir
- **Ölçüm (2026-08-13):** `ReorderChecklistCommand` görev/run/kapalı/sürüm denetimlerinden geçiyor ama
  `RefuseNotYours` çağırmıyor — bilinçli: sıralama TÜM listeyi bir kerede yazıyor, tek maddenin anlamını
  değiştirmiyor ve kimseden bir şey almıyor. Ön yüzde de taşı düğmeleri başkasının satırında ÇİZİLİYOR.
- **Açık soru:** bir kontrol listesinde sıra bazen prosedürdür ("önce izolasyon, sonra ölçüm"). Şablondan gelen
  bir listede sırayı işleyicinin değiştirebilmesi, seviyeyi değiştirebilmesinden farklı mı? Bu turun tablosu
  sıralamayı kapsamıyordu; kendiliğinden genişletmedim.
- **Yapılacak (karar sahibin):** ya sıralama da yazarlık/şablon kuralına girer (o zaman karışık listede sıralama
  kısmen kilitlenir ve bu kendi başına bir UX sorusu), ya da bugünkü hâli açıkça "sıra serbesttir" olarak yazılır.
- **Gelecek regresyon riski: 🟡** — sonradan kilitlenirse bugün sıralayabilen kullanıcılar sıralayamaz olur.

### BL-096 — 🟡 Sahiplik alanı için geriye dönük veri göçü YOK; tüm eski maddeler "başkasının" oldu
- **Ölçüm (2026-08-13, canlı):** mevcut veride **28 kontrol listesi maddesi** var (8 görevde) ve **28'inin
  tamamında** `AddedByUserId` null — yani hepsi artık düzenlenemez/silinemez. Yeni eklenen maddeler doğru
  şekilde düzenlenebilir çıkıyor (canlı doğrulandı).
- **Karar (brief'in talimatı):** null = "talep edenin", yani başkasının. Yanlışlıkla silmeye izin vermenin
  bedeli, yanlışlıkla reddetmenin bedelinden büyük. Uygulama: alan doldurulmadı, **kural null'ı reddediyor** —
  veriye dokunan bir göç yazılmadı, çünkü hangi kullanıcının eklediği bilgisi hiçbir yerde saklı değil ve
  uydurmak, korumanın kendisini yalanlamak olurdu.
- **Sonuç, açıkça:** bugünkü demo/test verisindeki hiçbir kontrol listesi maddesi düzenlenemiyor. Gerçek bir
  kiracıda aynı şey olacak.
- **Yapılacak (istenirse):** göç seçenekleri — (a) hepsini görevin `CreatedByUserId`'sine ata (talep eden
  gerçekten çoğu zaman ekleyendir), (b) olduğu gibi bırak, kullanıcılar yeni madde ekleyerek ilerlesin.
  (a) tek satırlık bir betik ama bir VARSAYIMI veriye yazar; kararı sahibin.
- **Gelecek regresyon riski: 🟢** — (a) seçilirse yalnız izin genişler, daralmaz.

### BL-097 — 🟠 `AddChecklistItem` gövdedeki `evidenceRequired`'ı sessizce YUTUYOR
- **Ölçüm (2026-08-14, canlı, API katmanı):** `POST /api/v1/tasks/{id}/checklist/items` gövdesi
  `{"text":"…","requirement":"Blocking","evidenceRequired":true,"expectedVersion":11}` → **204**. Hemen ardından
  aynı görevin projeksiyonu: `blocking: true` **geldi**, `evidenceRequired: false` **geldi**. Yani `requirement`
  onurlandırılıyor, `evidenceRequired` düşüyor.
- **Aynı değer PUT ile yazılabiliyor:** `PUT …/checklist/items/{code}` gövdesi
  `{"labelText":"…","requirement":"Blocking","evidenceRequired":true,"expectedVersion":12}` → 204 ve projeksiyon
  `evidenceRequired: true`. Demek ki alan modelde ve güncelleme yolunda var; eksik olan yalnız EKLEME yolu.
- **Neden bugün görünmüyor:** ön yüzün ekleme satırında ataç düğmesi yok — seviye çipi var, ataç yok. Yani hiçbir
  ekran bu alanı ekleme sırasında göndermiyor ve kayıp fark edilmiyor. API'yi doğrudan kullanan bir tüketici
  (veya ekleme satırına ataç eklendiği gün) sessizce veri kaybeder.
- **Sınıf:** bu, bu modülün defalarca düzelttiği "saklanıyor ama etkisiz" kusurunun tersi — *gönderiliyor ama
  saklanmıyor*. İkisi de aynı sebepten kötü: yazan kişi bir karar verdiğini sanıyor.
- **Yapılacak:** `AddChecklistItemCommand`/handler'ında `EvidenceRequired`'ı taşı; ya da alan kabul edilmiyorsa
  400 ile açıkça reddet. Sessiz yutma iki seçenekten de kötü.
- **Gelecek regresyon riski: 🟢 eklemeli** — bugün hiçbir ekran göndermiyor, davranış değişmez.

### BL-098 — 🟢 Sürüklemenin derin kaydırma ve gerçek dokunmatik emülasyonu altında ölçümü yapılamadı
- **Bağlam:** BL-094 kapanışının 2. şartı "900px yığılmış düzen, panel kayarken, dokunmatik emülasyonuyla"ydı.
  Karşılanan: 900px yığılmış (869px tek sütun), `scrollTop = 64`, `pointerType:'touch'` pointer olayları, iki
  genişlik, sunucuda kalıcılık. **Karşılanamayan iki koşul:**
  1. **Gerçek dokunmatik emülasyonu** — panelin dokunmatik kipi `width < 768` gerektiriyor, şart ise 900px'di.
     İkisi aynı anda sağlanamıyor; sentetik `pointerType:'touch'` olaylarıyla ölçüldü.
  2. **64px'ten derin kaydırma** — barındırılan tarayıcı paneli gizliyken gerçek tekerlek girdisi çağrı başına
     ~34px ilerletip 30 sn'de zaman aşımına uğruyor, programatik `scrollingElement.scrollTop` ataması ise
     reddediliyor (atama aynı tick'te geri okunduğunda eski değeri veriyor).
- **Neden yine de alındı:** 64px bir satır yüksekliğinden (~49px) büyük, yani kaydırma kaynaklı klasik ofset
  hatası bir satırdan fazla kayma olarak GÖRÜNÜRDÜ; görünmedi. Ok düğmeleri de yerinde durduğundan sürüklemenin
  bozulduğu bir durumda sıralama yine de yapılabilir.
- **Yapılacak (istenirse):** panel görünür durumdayken, gerçek dokunmatik emülasyonlu bir tarayıcıda, listeyi
  sayfanın ~1000px derinliğine kaydırıp elle bir sürükleme; ayrıca dar (<768px) gerçek dokunmatik kipte tekrar.
- **Gelecek regresyon riski: 🟢** — ölçüm boşluğu, kod borcu değil.

<!-- ────────────────────────────────────────────────────────────────────────────────────────────────────────
     KART KART DENETİM — Görev Merkezi detay sayfası. Kartlar tek tek ele alınıyor; bu bölümdeki her madde
     BAŞLIĞINDA hangi kartta olduğunu söyler. Bir kart bitmeden diğerine geçilmiyor, kayıt da o sırayla.
     ──────────────────────────────────────────────────────────────────────────────────────────────────────── -->

### BL-100 — 🟠 [KOMUT KARTI] Odak halkası tema sınıflarında hâlâ yok: `.btn`, `.nav-link`, `.form-control`
- **Ölçüm (2026-08-14, canlı, GERÇEK Tab tuşuyla):** detay sayfasında 60 Tab durağı sayıldı.
  **Önce 0/60'ında** görünür odak göstergesi vardı (ne `outline` ne `box-shadow`). Bu turda bizim çizdiğimiz
  sınıflara halka eklendi → **43/60**. Kalan **17 durak tema sınıfı**: 14 × `.btn.btn-icon.dropdown-toggle`,
  1 × `.btn.btn-outline-primary`, 1 × `.btn.btn-sm.btn-label-secondary`, 1 × `input.form-control`.
- **Brief'in varsayımı tutmadı — kayıt sebebi bu:** prompt "30 `.btn` (korumalı)" diyordu. **Değiller.**
  `core.css` `button:focus, button:focus-visible { outline: 0 }` diyor ve temanın `.btn` için verdiği
  `box-shadow` telafisi bu sayfada ölçülemedi: `.btn.btn-outline-primary` üzerinde `boxShadow: none`.
  Yani kopyalanacak "mevcut tema göstergesi" YOK; bu turda kullanılan desen projenin kendi
  `outline: 2px solid var(--bs-primary)` idiomu (`.wcn-row`, `.wcn-tr`, `.wcn-kcard`, `.diten-tree-row` … 8 yer).
- **Neden bu turda yapılmadı:** `.btn` ve `.form-control` **ürünün her ekranında** var. Yaşam döngüsü kartı
  turunda uygulamanın tamamına görsel değişiklik sokmak, kart kart ilerleme kararının kendisini bozardı.
- **Dokunulmayan ve dokunulmaması gereken:** `.dropdown-item`. Bootstrap ona zaten `:focus` arka planı veriyor
  (ölçüldü: `rgba(34,48,62,.06)`), yani 29 kontrol kapsanmış durumda; ikinci bir gösterge eklemek aynı menünün
  iki farklı dille cevap vermesi olurdu.
- **Yapılacak:** `.btn:focus-visible` ve `.form-control:focus-visible` için tek bir merkezi kural, ardından
  DataTable/form ekranlarında görsel regresyon taraması.
- **Gelecek regresyon riski: 🟡** — uygulama geneli görsel değişiklik; davranış değişmiyor ama her ekran etkilenir.

### BL-101 — 🟡 [SEKME ŞERİDİ] `.wcn-detail-tab` CSS bloğu ÖLÜ; markup o sınıfı hiç taşımıyor
- **Ölçüm (2026-08-14):** `backbone-custom.css` içinde `.wcn-detail-tab`, `.wcn-detail-tab:hover`,
  `.wcn-detail-tab.active`, `.wcn-detail-tab:focus-visible`, `.wcn-detail-tab i`, `.wcn-detail-tab span` ve
  `.wcn-detail-tabpanel` kuralları var. Markup ise `nav-link border shadow-none wc-tab-compact` sınıflarını ve
  `data-wcn-detail-tab` **niteliğini** kullanıyor — `.wcn-detail-tab` **sınıfı** hiçbir yerde uygulanmıyor.
- **Somut sonucu:** sekmelerin odak halkası yazılmıştı ve çalışmıyordu; `:focus-visible` kuralı var olmayan bir
  sınıfı bekliyordu. Bu turda halka `.wc-tab-compact` üzerinden verildi, yani **semptom kapandı, ölü blok durdu**.
- **Yapılacak:** ya blok silinsin, ya markup o sınıfı taşısın. İkisinden biri; ikisi birden değil.
- **Gelecek regresyon riski: 🟢** — bugün hiçbir şeyi boyamıyor.

### BL-102 — 🟢 [YAŞAM DÖNGÜSÜ KARTI] Hedef 96px tutmadı: 177 → 114px (%36), 18px açık kaldı
- **Ölçüm (1440×900, canlı):** kart 177px → **114px**. Engelli görevde ~290px → **170px**.
- **Aritmetiği, çünkü sayı zorlanmadı:** 32 (kart dolgusu) + 21 (kimlik satırı) + 16 (boşluk) + 44 (şerit) = 113.
- **96'ya inmenin tek yolu**, briefin AYNI turda istediği üç şeyden birini geri almak olurdu:
  bulunulan adımın görünür etiketi (−20px), şeridin sonundaki durum/kapanış bilgisi (satır içine alındı, artık
  0px), ya da kart dolgusunu 16px'ten düşürmek (kartın tamamının ritmini değiştirir, kart kart ilerleme kararına
  aykırı).
- **ÖLÇÜLMÜŞ SÜRPRİZ — brief'in 1c gerekçesi dikey tasarruf sağlamıyor:** dört adım etiketinin üçünü gizlemek
  **0px** kazandırdı. Etiketler YAN YANA tek satırda; `<ol>` yüksekliği (44px) tek bir görünür etiket tarafından
  belirleniyor. 1c'nin kazancı görsel gürültüde, dikey alanda değil. Gerçek kazanç: başlık (−34px) ve iki çip
  satırının tek satıra inmesi (−29px).
- **Gelecek regresyon riski: 🟢** — kayıt, kod borcu değil.

### BL-103 — 🟢 [YAŞAM DÖNGÜSÜ KARTI] Enter/Space bu ortamda hiç iletilemiyor; klavye ETKİNLEŞTİRME ölçülemedi
- **Ölçüm (2026-08-14):** `Tab` gerçek tuş olarak çalışıyor (60 durak kaydedildi). `Return` ve `Enter`
  gönderildiğinde odaklanmış düğmede **`keydown` bile tetiklenmiyor** (dinleyici kuruldu, olay dizisi boş kaldı).
  Yani tuş sayfaya ulaşmıyor.
- **Bunun yerine kanıtlanan:** engel uyarısının bağlantısı native `<button>` (Tab ile ulaşılıyor, `tabIndex 0`),
  handler delegasyonlu `click` dinleyicisinde ve **canlı tıklamayla** çalıştığı doğrulandı; ayrıca
  `scrollIntoView` ve `focus` çağrılarının doğru öğeye yapıldığı köstebekle ölçüldü.
- **Yapılacak (istenirse):** panel görünürken elle Enter/Space denemesi.
- **Gelecek regresyon riski: 🟢** — ölçüm boşluğu.

### BL-104 — 🟡 [YAŞAM DÖNGÜSÜ KARTI] Tek engelleyici varken adı artık görünmüyor
- **Karar ve bedeli:** engel uyarısı N alt görevi tek cümleye indirdi ("{0} alt görev kapanmadan tamamlanamaz")
  ve Alt Görevler kartına bağlantı verdi. Üç engelleyicide bu net kazanç: aynı cümle dört kez yazılıyordu.
  **Tek engelleyicide ise kayıp var** — eski uyarı adı söylüyordu ("Bütçe kalemini doğrula tamamlamayı
  engelliyor"), yenisi "1 alt görev" diyor ve adı bağlantının arkasına koyuyor.
- **Yapılacak (karar sahibin):** `n === 1` için adı yazan bir dal. Tek satır kod, ama iki farklı cümle şekli
  demek; tutarlılık ile bilgi arasında bir tercih ve bu benim değil sahibin kararı.
- **Gelecek regresyon riski: 🟢 eklemeli.**

### BL-105 — 🟠 [KOMUT KARTI] `closedAt` normalizasyonu sözleşme muhafızını sessizce siliyordu (BU TURDA YAKALANDI)
- **Ne oldu:** `closedAt` bu turda ekranda çizilmeye başlandı ve `dueAt`/`startAt`/`plannedDate` ile aynı dikişte
  `toDateOnly` ile normalleştirildi. **Ama `mapPayload` önce ADAPTE edip sonra DOĞRULUYOR** — yani
  `toDateOnly('yakında')` → `null`, `null` ise geçerli bir `closedAt`, dolayısıyla sözleşmenin kendi
  `CLOSED_AT_INVALID` kuralı **bir daha asla ateşlenemezdi**. Test kırmızıya döndüğü için yakalandı.
- **Düzeltme:** yalnız ayrıştırılabilen değer normalleştiriliyor; ayrıştırılamayan ham hâliyle geçiyor ki
  doğrulayıcı reddedebilsin.
- **AÇIK KALAN, aynı sınıf:** `dueAt`, `startAt`, `plannedDate` **aynı desende** ve aynı riski taşıyor. Bugün
  bir zararı yok, çünkü sözleşmede bu üçü için `*_INVALID` kuralı bulunmuyor — yani kural eklenirse sessizce
  ölü doğar. Kalıcı çözüm: doğrulama HAM dto üzerinde çalışsın, adaptasyon sonra gelsin.
- **Gelecek regresyon riski: 🟠** — sözleşme kuralı eklendiği gün, eklendiğini sanan kişi yanılır.

### BL-106 — 🟢 [YAŞAM DÖNGÜSÜ KARTI] Nokta-çizgi şeridinin CSS'i ölü kaldı, silinmedi
- **Ne oldu:** basamak şeridi nokta + bağlantı çizgisi + etiket düzeninden **ince segment çubuğuna** geçti.
  `<ol>/<li>` yapısı aynen korundu; her `<li>` artık bir segment.
- **ÖLÇÜLDÜ (silmeden önce):** `wcn-step-dot` ve `wcn-step-label` sınıfları **hiçbir başka view, partial veya
  bundle'da** geçmiyor — `grep -rn --include="*.js" --include="*.cshtml"` yalnız `renderLifecycleStepper`'ı ve
  testleri döndürdü. Yani başka bir ekranı kırma riski yok.
- **Bugün ölü olan kurallar** (`backbone-custom.css`, "DEAD, kept rather than deleted" başlığı altında):
  `.wcn-step-dot`, `.wcn-step-done .wcn-step-dot`, `.wcn-step-active .wcn-step-dot`,
  `.wcn-step-done .wcn-step-label`, `.wcn-step-active .wcn-step-label`,
  `.wcn-step-optional .wcn-step-dot`, `.wcn-step-optional .wcn-step-label`.
  `.wcn-step-label` hâlâ bir öğeyle eşleşiyor ama o öğe `visually-hidden`, dolayısıyla hiçbir bildirimi boyamıyor.
- **Neden silinmedi:** brief "ölü kalıyorsa işaretle" dedi ve çubuk henüz sahip onayından geçmedi. Görünüm geri
  alınırsa bu kurallar tek adımda geri gelir.
- **Yapılacak:** çubuk kabul edilince blok silinir. `.wcn-step::before` (bağlantı çizgisi) bu turda zaten kaldırıldı,
  çünkü segmentler arasında çizgi kavramı yok.
- **Gelecek regresyon riski: 🟢** — bugün hiçbir şeyi boyamıyor.

### BL-107 — 🟠 [YAŞAM DÖNGÜSÜ KARTI] Segment çubuğunun kontrastı İKİ TEMADA da eşiğin altında
- **Ölçüm (2026-08-14, canlı, WCAG 1.4.11 metin-dışı eşiği = 3.0):**

  | çift | IŞIK | KARANLIK |
  |---|---|---|
  | gelecek ↔ kart zemini | **1.25** ✗ | **1.73** ✗ |
  | aktif ↔ gelecek | 3.23 ✓ | **1.95** ✗ |
  | tamam ↔ gelecek | **1.39** ✗ | 4.55 ✓ |
  | tamam ↔ kart | **1.74** ✗ | 7.87 ✓ |
  | aktif ↔ kart | 4.05 ✓ | 3.38 ✓ |

  Renkler: kart `#fff` / `#2b2c40`, gelecek `--bs-border-color` (`#e4e6e8` / `#4e4f6c`),
  tamam `--bs-success` (`#71dd37`), aktif `--bs-primary` (`#696cff`).
- **Somut anlamı:** ışık temasında **"tamamlandı" yeşili ile "gelecek" grisi arasındaki sınır 1.39:1** — yani
  ilerlemenin nerede bittiği pratikte görünmüyor. Karanlık temada aynı sorun **aktif ↔ gelecek** çiftinde
  (1.95:1). Her iki temada da çubuğun *izi* kart zemininden zor ayrılıyor.
- **Neden çözülmedi:** brief açıkça "ayırt edilemiyorsa BANA SÖYLE, çözümü sen seçme" dedi ve "renkler MEVCUT
  değişkenlerden, yeni renk tanımlama" kısıtı koydu. Bu iki kısıt birlikte, mevcut değişken kümesiyle 3.0'ı
  tutturmayı imkânsız kılıyor — çözüm bir renk KARARI gerektiriyor.
- **Seçenekler (karar sahibin):** (a) gelecek segmentine `--bs-secondary-color` gibi daha koyu bir mevcut
  değişken · (b) segmentlere 1px iç kenarlık ekleyip sınırları çizgiyle ayırmak · (c) tamamlanmış segment için
  success yerine primary'nin koyu tonu · (d) eşiği bilinçli kabul etmek (çubuk tek bilgi kaynağı değil —
  caption "Tamamlandı — 4/4" zaten yazıyla söylüyor, ki bu WCAG açısından geçerli bir savunmadır).
- **Not:** (d) savunulabilir bir konum, çünkü çubuk **tek başına bilgi taşımıyor**; üstündeki caption durumu ve
  n/total'ı metinle veriyor, `<li>`'ler de erişilebilir ağaçta ad + durum taşıyor. Yine de karar sahibin.
- **Gelecek regresyon riski: 🟡** — renk değişirse iki tema × üç durum yeniden ölçülmeli.

### BL-108 — ✅ KAPANDI (2026-08-14) — [SAYFA BAŞLIĞI] Breadcrumb ile ilk kart arası 28px, standart 12px
- **Sahip bildirimi:** "breadcrumb ile altındaki kartta sorun var, aralarındaki boşluk standartların dışında."
- **Ölçüm (canlı, 1440px):**
  - `/WorkCenterNext/Details/{id}` → breadcrumb alt **142**, kart üst **170** = **28px**
  - `/Tasks/Create` (Golden Reference Compact deseni) → breadcrumb alt **142**, kart üst **154** = **12px**
  - Aynı desen: `Views/Organization/Positions/Details.cshtml`, `OrganizationUnits/Details.cshtml` — başlık bloğu
    `.row`'un **dışında**, boşluğu yalnız kendi `mb-3`'ü (bu temada 12px) veriyor.
- **Kök sebep — kopyalanan markup, kopyalanmayan YERLEŞİM:** `app.js` başlığı
  `<div class="col-12">${pageHeader}</div>` olarak `.row.g-4.wcn-detail-grid` **içine** koyuyordu. Böylece
  altındaki kolon başlığın `mb-3`'ünü (12px) **artı** satırın dikey gutter'ını (16px) alıyordu. 28 = 12 + 16;
  iki ayrı boşluk sistemi üst üste biniyor ve ortaya kimsenin seçmediği bir sayı çıkıyordu.
  Kod incelemesinde görünmez, ekranda görünür.
- **Düzeltme:** başlık grid'in dışına, referanstaki yere alındı. Yeni CSS **yok** — kusur yapısaldı, düzeltme de
  yapısal. Bootstrap'in gutter'ı kendi kuralına göre sadeleşiyor (satır `-16px` ↔ ilk kolon `+16px`).
- **Doğrulama (canlı):** açık görev, engelli görev · 1440 ve 900 · açık ve karanlık tema → **hepsinde 12px**.
  İçerik/ray kolonları yan yana kalıyor, yatay taşma yok, rehber banner'ı başlıkla birlikte taşındı.
- **Test:** `wcn-detail-three-regions.test.js` → "sits OUTSIDE the grid row" + "leaves the grid carrying only the
  three regions". Yapısal olarak iddia ediliyor çünkü jsdom layout hesaplamıyor; piksel ölçümü tarayıcıda yapıldı.
  **Mutasyon:** başlık grid'e geri konunca iki test kırmızı.
- **Gelecek regresyon riski: 🟢** — tek yapısal satır, davranış değişmiyor.

### BL-109 — ✅ KAPANDI (2026-08-14) — [MEVCUT AKSİYONLAR] "Başkasına ata" HİÇ açılamıyordu: `.data` vs `.data.people`
- **Bulunma şekli:** bu turun Kural 2 kapısı ("cümleyi silmeden önce varış yerini AÇ, GÖR, ÖLÇ") uygulanırken.
  Düğmeye basıldı, diyalog açılmadı, yerine "Bu görevin devredilebileceği kimse yok." bildirimi çıktı.
- **Ölçüm (canlı):** `TasksApi.assignablePeople()` → `{ data: { people: [...4 kişi...] } }`. `app.js` ise
  `people = (res.ok && res.data) ? res.data : []` yazıyordu; `res.data` bir NESNE, `.length` `undefined`,
  guard "kimse yok" sonucuna varıp `return` ediyordu. **Kiracıda dört atanabilir kişi varken.**
- **Neden bu kadar sinsi:** aksiyon çizilmiş, klavyeyle erişilebilir, tıklanabilir ve her seferinde nazikçe
  reddediyordu. Hata yok, konsol sessiz, test yok.
- **ÜÇÜNCÜ TEKRAR:** aynı dosyada `loadAssignablePeople` (`app.js`) bunu DOĞRU açıyor ve başında BL-057'den
  kalma bir yorum var — quick-create offcanvas'ta yaşanan ikizini anlatıyor. Bu satır o yorumun üçüncü kardeşi.
- **Düzeltme:** `(res.ok && Array.isArray(res.data?.people)) ? res.data.people : []` — diğer iki çağrı yeriyle
  birebir aynı yazım.
- **Doğrulama (canlı):** diyalog açılıyor, atanabilir kişi seçicisi **4** kayıt, neden alanı yerinde.
- **Yapılacak (açık):** `assignablePeople` için tek bir unwrap yardımcısı; üç çağrı yeri üç ayrı yazımda kaldıkça
  dördüncüsü de yanlış yazılacak.
- **Gelecek regresyon riski: 🟡** — yardımcı yazılmazsa tekrar eder.

### BL-110 — 🟠 [MEVCUT AKSİYONLAR] Brief'in iki varsayımı ölçümde çürüdü — cümle SİLİNMEDİ, uyarı TAŞINMADI
- **(a) "Başkasına ata" açıklaması etiketin aynısı değil.** Brief "açıklaması etiketin aynısı, bilgi taşımıyor,
  SİLİNİR" diyordu. Ölçüm: `OutcomeReassign` = **"Görevi, kabul edilmemiş olarak başkasına verir"** — "kabul
  edilmemiş olarak" bilgisi etikette yok. Silinmedi; diğer ikincil cümlelerle birlikte kendi diyaloğuna taşındı
  (BL-109 düzeltilince o diyalog gerçekten açılabilir oldu).
- **(b) Alt görev uyarısı, engellenen aksiyonun gerekçesi DEĞİL.** Brief "'Tamamla — 14 açık alt görev
  kapatılmalı' satırı bu karta gelir, Alt Görevler kartından KALKAR" diyordu. Ölçüm (görev
  `049e9109-f3c9-4104-9899-22d515eb6925`):
  - aksiyonun gerekçesi (SUNUCU, `complete.disabledReasonCode`) = `CHECKLIST_INCOMPLETE` →
    "Tamamlanmamış zorunlu bir kontrol listesi maddesi var."
  - Alt Görevler kartındaki cümle (İSTEMCİ, açık alt görev sayımı) = "3 açık alt görev kapatılmalı"
  **İki farklı kaynak, iki farklı iddia.** Çiftleme değil. Alt görev cümlesi silinseydi, sunucunun hiç
  söylemediği bir bilgi kaybolurdu.
- **Uygulanan:** aksiyonun kendi gerekçesi düğmesinin yanında, aynı `<li>` içinde (Kural 3'ün özü). Alt görev
  uyarısı yerinde bırakıldı.
- **Yapılacak (karar sahibin):** sunucu alt-görev engelini de `disabledReasonCode` ile bildirsin mi? Bildirirse
  iki cümle gerçekten tek cümle olur ve Alt Görevler kartındaki sayım kaldırılabilir.
- **Gelecek regresyon riski: 🟢** — bugün bilgi kaybı yok.

### BL-111 — 🟢 [MEVCUT AKSİYONLAR] Kebap düğmesi artık çizilmiyor; Kural 6 (aria-label) konusuz kaldı
- **Kural 6** kebap düğmesine `aria-label` istiyordu ("ekran okuyucu 'Diğer, düğme' diyor").
- **Ölçüm:** kebabın tek sakini yıkıcı aksiyondu; Kural 1 gereği o açığa çıkınca menü boş kaldı ve hiç
  render edilmiyor (canlı: `.wcn-actrail-menu` yok). Başka bir "overflow" kademesi bu kartta hiç yoktu.
- **Sonuç:** eklenecek `aria-label` taşıyacak bir düğme kalmadı. `ActionsOther` anahtarı **silinmedi**;
  `.wcn-actrail-other` CSS'i ölü olarak işaretlendi.
- **Yapılacak:** ileride gerçek bir overflow kademesi gelirse desen hazır — alt görev satır kebabı hem `title`
  hem `aria-label` taşıyor, kopyalanacak yer orası.
- **Gelecek regresyon riski: 🟢.**

### BL-112 — 🟡 [MEVCUT AKSİYONLAR] Odak halkası bizim sınıfa eklendi ama tema 3px kendi rengiyle eziyor
- **Ölçüm (gerçek Tab):** kart düğmelerinde önce **0/3** odak göstergesi vardı (BL-100'ün `.btn` boşluğu).
  `.wcn-act-btn:focus-visible` eklendikten sonra **3/3** gösterge var.
- **Ama uygulanan kural bizimki değil:** hesaplanan `outline` **3px** ve düğmenin kendi türünün rengi
  (ikincil `rgb(133,146,163)`, yıkıcı `rgb(255,62,29)`), bizim kuralımızın `2px var(--bs-primary)`'si değil.
  Yani `core.css`'teki `.btn:focus-visible` kazanıyor; bizim kuralımız yalnız `outline: 0` bastırmasını
  kaldırmış oluyor.
- **Sonuç bugün kabul edilebilir** — hatta tür rengi tek tip primary halkadan okunaklı. Ama **ev tokenının
  uygulandığı sanılmamalı**; BL-100 çözülürken bu etkileşim yeniden ölçülmeli.
- **Gelecek regresyon riski: 🟡** — BL-100 dokunulduğunda bu kart yeniden ölçülmeli.

### BL-113 — ✅ KAPANDI (2026-08-14) — [API] `assignablePeople` zarfı ARTIK ÇAĞIRANLARDA AÇILMIYOR
- **Sorun:** uç `{ people, excluded }` döndürüyordu; **dört çağıran, üç ayrı açma biçimi** yazmıştı ve üçü zaman
  içinde yanlıştı. Yanlış olan çökmüyordu — `res.data` bir nesne, `.length` `undefined`, guard "kimse yok"
  sonucuna varıp nazikçe reddediyordu. Son örneği (BL-109) hiç açılamayan bir devretme diyaloğuydu.
- **Neden yardımcı yetmezdi:** yardımcı isteğe bağlıdır; beşinci çağıran yine kendi satırını yazar. **Şekil
  API katmanında durduruldu:** `TasksApi.assignablePeople()` artık `data`'yı **dizi** olarak döndürüyor.
- **Dokunulan çağıranlar:** `Tasks/form-page.js`, `WorkCenterNext/quick-create.js`, `WorkCenterNext/app.js` ×2 —
  hepsi `people.ok ? people.data : []`.
- **Uyarı yorumları güncellendi:** `quick-create.js` ve `app.js`'teki "bu satır yanlış olmuştu" notları, tehlike
  kaynakta ortadan kalktığı için yeniden yazıldı (silinmedi — tarih kayıtta kaldı).
- **`excluded` bilerek düşürüldü:** hiçbir çağıran okumuyordu ve ikinci alan, nesne şeklinin geri büyüme yolu.
  BL-072 ("X neden listede yok") gerektiğinde kendi adlandırılmış çağrısını alır.
- **Testler:** iki test **ters çevrildi** — eskiden çağıranın `.data.people`'a uzanmasını ZORUNLU kılıyorlardı,
  yani kusuru hayatta tutan kuralı koruyorlardı. Artık çağıranın uzanmamasını ve açmanın `api.js`'te olmasını
  şart koşuyorlar.
- **Gelecek regresyon riski: 🟢** — şekil tek yerde.

### BL-114 — ✅ KAPANDI (2026-08-14) — [DURUM KARTI] Kart dağıtıldı; iki tarih iki farklı şey söylüyordu
- **Ölçüm:** kart 121px, içeriğinin tamamı iki tarih. Adı "Durum", içinde durum yok. **Ve bir çelişki:** aynı
  son tarih Özet'te `rgb(167,172,178)` gri, Durum'da `rgb(255,62,29)` kırmızıydı — bir ekran, bir olgu, iki cevap.
- **Dağıtım:** kaynak son tarih → **Özet** (kırmızıyı da götürdü; kural değişmedi, `item.slaState === 'overdue'`,
  yani Durum kartının zaten kullandığı kaynak). Kişisel plan + plan çakışması uyarısı → **Kişisel** kartı
  (adı "Kişisel Not" → "Kişisel").
- **⚠ BRIEF'İN VARSAYIMI KISMEN YANLIŞTI:** kart yalnız tarih taşımıyordu — **onay/inceleme gate satırlarını da**
  taşıyor. Tümden silinseydi onlar da silinirdi. Yalnız tarihler çıkarıldı; gate'i olmayan görevde kart artık
  kendiliğinden görünmüyor (istenen sonuç), gate'i olan görevde duruyor.
- **Ölçülerek işaretlenen ölü CSS:** `.wcn-dates`, `.wcn-date-cell/-label/-value/-overdue/-conflict` — başka
  hiçbir view/partial/bundle'da kullanılmıyor. **`.wcn-date-warn` ÖLÜ DEĞİL:** plan çakışması notu Kişisel
  kartında hâlâ kullanıyor. Silinmedi, işaretlendi.
- **Ray:** 648px → **583px**.
- **Gelecek regresyon riski: 🟢.**

### BL-115 — 🟡 [MEVCUT AKSİYONLAR / ÖZET] Kartlar kısaldı ama İKİ KART kendi içinde büyüdü
- **Ölçüm (1440, olağan görev):** aksiyonlar **224 → 267px**, özet **211 → 269px**, ray **648 → 583px**.
- **Neden büyüdüler:** aksiyonlar kartında ritim tek ölçeğe oturdu (grup içi 8px, gruplar arası 16px — eskiden
  4/10/14 karışıktı) ve ikincil düğmeler gerçek 38px dokunma alanı aldı (eskiden 30px). Özet kartında üç sütunlu
  ızgara tek sütunlu tanım listesine dönüştü: yedi olgunun dördü artık alt alta.
- **Net kazanç yine de var** (ray 65px kısaldı, Durum kartı gitti) ama kart başına hedef tutmadı.
- **Karar sahibin:** (a) böyle kalsın — okunabilirlik ve dokunma hedefi yükseklikten önemli · (b) özet listesi
  iki sütuna dönsün (yetim hücre riski geri gelir) · (c) aksiyonlarda gruplar arası boşluk 12px'e insin
  (tek ölçek kuralı bozulur).
- **Gelecek regresyon riski: 🟢** — yalnız boşluk değeri.

### BL-116 — 🟢 [ÖZET] `summaryFact` ve olgu ızgarası CSS'i ölü kaldı
- Tanım listesi ızgaranın yerini alınca `summaryFact` yardımcısının **hiç çağıranı kalmadı** (ölçüldü) ve
  kaldırıldı; kuralı ("boş alan çizilmez") `renderSummary`'nin `row()`'unda yeniden ifade edildi.
- `.wcn-facts`, `.wcn-fact-wide`, `.wcn-fact-body`, `.wcn-fact-label`, `.wcn-fact-value`, `.wcn-fact-tags`
  **ölü olarak işaretlendi, silinmedi** (bir tur geri dönüş payı). **`.wcn-facts-grid` (iş bağlamı bölümleri) ve
  dosyanın üst kısmındaki ayrı `.wcn-fact` bloğu farklı bileşenler — dokunulmadı.**
- **Yapılacak:** görünüm kabul edilince blok silinsin.
- **Gelecek regresyon riski: 🟢.**

### BL-117 — 🟢 [ÖZET] Golden referanstan BİLİNÇLİ SAPMA: boş alanda "-" basmıyoruz
- **Golden referans** (`Views/DevEnablement/GoldenReferenceCompact/Details.cshtml`) boş değer için `-` basıyor:
  `@(string.IsNullOrWhiteSpace(Model.Code) ? "-" : Model.Code)`.
- **Özet kartı basmıyor — alanı hiç çizmiyor.** Gerekçe: tire, "alan kontrol edildi ve boş bulundu" iddiasıdır;
  okuyucu bunu "yüklenemedi" durumundan ayırt edemez. Bu sayfada olguların çoğu isteğe bağlı (başlangıç tarihi,
  tahmini süre, etiketler), dolayısıyla tire basmak kartın yarısını anlamsız çizgiyle doldururdu.
- **TEK İSTİSNA — Atanan:** boşsa satır YİNE çizilir ve "Atanmamış" der. Atanansız görev eksik alan değil,
  sonucu "kimse fark etmezse iş bekler" olan bir OLGU.
- **Sapma bilinçli ve kayıtlı** — golden referansı takip eden bir sonraki ekran bunu drift sanmasın diye.
- **Gelecek regresyon riski: 🟢.**

### BL-118 — ✅ KAPANDI (2026-08-14) — [BAŞLIK] Kaynak izi her görevde aynıydı; silinmedi, koşullandı
- **Ölçüm:** bu yüzeydeki her kayıt `providerCode: "tasks"` ve `objectType: "task"` taşıyor. "Görevler · Görev"
  her görevde aynı çıkıyor, hiçbir şeyi ayırt etmiyordu — iki sabit, olgu kılığında, gözün ilk geçişini
  gerçekten değişen sinyallerden önce alıyordu.
- **Yapılan:** modül adı yalnız `providerCode !== 'tasks'` ise, nesne türü yalnız `objectType !== 'task'` ise
  çiziliyor. İkisi de gizlenince ayırıcı hairline de gizleniyor (bölecek bir şey yokken çizgi çizmek).
- **Neden silinmedi:** merkez tasarım gereği başka sağlayıcılardan da iş topluyor. MOD-0023 iş akışı kalemleri
  geldiği gün "bu nereden geldi" gerçek bir soru olur ve alan kendiliğinden görünür. Silinseydi o gün yeniden
  yazılması gerekirdi — ve yeniden yazılması gereken şey tam olarak unutulan şeydir.
- **Test:** iki test — varsayılan sağlayıcıda gizli, yabancı sağlayıcıda görünür (`providerCode: "workflow"`).
- **Gelecek regresyon riski: 🟢.**

### BL-119 — 🟠 [VERİ] Seed görevlerinin açıklaması durum cümlesi gibi yazılmış
- **Ölçüm:** `98d1f94e` görevinin `description` alanı **"Kabul bekliyor."**. Bu bir açıklama değil, bir durum
  ifadesi — ve Özet kartında etiketsiz paragraf olarak çizilince sahip haklı olarak "bu metin anlaşılmıyor" dedi.
- **Projeksiyon suçsuz:** `TaskWorkItemProvider` açıklama varsa açıklamayı, yoksa **`null`** yolluyor. Üretilmiş
  yedek YOK (CONTROL TOWER'ın düzeltmesi de bu noktada eskimiş — kod bugün yedek üretmiyor). İki gerçek görevle
  ölçüldü: açıklamalı → `{kind:"display", text:…}`, açıklamasız → `null`.
- **Ön yüz tarafı çözüldü:** cümle artık "Açıklama" etiketli kendi alanı; durum cümlesiyle karışması imkânsız.
- **Kalan iş VERİDE:** seed açıklamaları gerçek açıklamalarla değiştirilmeli, yoksa demo ekranlarında
  "Açıklama: Kabul bekliyor." yazacak.
- **Gelecek regresyon riski: 🟢** — yalnız seed verisi.

### BL-120 — 🟡 [ÖZET] Hedef 150px tutmadı: 230px
- **Ölçüm (1440, açıklamalı görev `b0c67d51`):** 269 → **230px**. Hedef ~150'ydi.
- **Neden tutmadı:** (a) açıklama artık tam genişlikte kendi alanı — tek başına ~56px; (b) golden alan deseni
  etiket ÜSTÜNDE değer ALTINDA çiziyor, yani her alan iki satır (eski `<dl>` yan yanaydı); (c) ikon sütunu
  22px + 12px boşluk.
- **Kazanç yine de var:** kartın sağ yarısı artık kullanılıyor (854px genişlikte iki sütun), yetim hücre yok,
  açıklamasız görevde kart **170px**.
- **Karar sahibin:** (a) böyle kalsın — ürün deseni tutarlılığı yükseklikten önemli · (b) açıklama tek sütuna
  insin (uzun metin dar sütunda kötü sarar) · (c) etiket/değer yan yana olacak şekilde golden deseni değiştir
  (ürün genelinde etki, bu kartın kararı değil).
- **Gelecek regresyon riski: 🟢.**

### BL-121 — ✅ KAPANDI (2026-08-14) — [KURAL] Kart içi bölüm ayırıcısı: kenardan kenara, iki yanı eşit
- **Aynı kusur iki turda iki kartta çıktı**, üçüncüsü beklenmedi; kural yazıldı ve sekiz kart tarandı.
- **Tarama sonucu — gerçek bölüm ayırıcısı YALNIZ İKİ TANE:**
  | kart | ayırıcı | önce | sonra |
  |---|---|---|---|
  | Mevcut aksiyonlar | `.wcn-acts-destructive` | 0/0 kenar ✓ · 24px üst / 16px alt ✗ | 0/0 · **16/16** |
  | Özet | `.wcn-sumtags` | 8/8 kenar ✗ · 4px üst / 16px alt ✗ | **0/0** · **16/16** |
  Yaşam Döngüsü · Alt Görevler · Kontrol Listesi · Etkinlik · Kişisel · Teknik kartlarında **bölüm ayırıcısı yok**
  (taramada çıkanlar satır kenarlıkları, form kontrolleri, ilerleme çubukları ve uyarı bloklarıydı).
- **⚠ BRIEF'İN REFERANSI KURALI İHLAL EDİYORDU:** brief aksiyon kartını "negatif kenar boşluğu kullanmadan
  düzeltildi" diye gösteriyordu; oysa geçen turun CSS'i `margin: 1rem -1.5rem 0` kullanıyordu ve **kendi yorumu
  bunu yapmadığını iddia ediyordu**. Negatif marj, iptal etmeye çalıştığı dolguyla kavga eder ve o dolgu
  değiştiği an kırılır. İkisi de gerçek tekniğe geçirildi: ana blok satır-içi dolgu tutmuyor, her blok kendi
  içini ödüyor, çizgi kendiliğinden iki kenara varıyor.
- **Boşluk değeri kartın kendi grup ölçeğinden:** 16px (aksiyon kademelerini ayıran değerle aynı). Kartın 1.5rem'i
  yalnız kart kenarına bakan yüzlerde kaldı.
- **Gelecek regresyon riski: 🟢** — test hem kenarı hem eşitliği hem de negatif marj yokluğunu kilitliyor.

### BL-122 — ✅ KAPANDI (2026-08-14) — [ALT GÖREVLER / KONTROL LİSTESİ] İki liste tek satır diline geçti
- **Ölçüm (önce):** kontrol listesi satırı `bg rgb(245,245,249) · border 1px · radius 6px` (kutu);
  alt görev satırı `bg transparent · yalnız border-top · radius 0` (ayrılmış çizgi). **İkisinde de `:hover` yok**,
  oysa projenin idiomu `.wcn-row:hover` bir dosya ötede duruyor.
- **Karar — alt görev satırı da KUTU oldu.** Gerekçe: ikisi de kendi denetimlerini taşıyan etkileşimli NESNE
  (biri tik+başlık+durum+menü, diğeri tik+metin+seviye+ataç+taşı+sil). Kutu "bu bir şeydir" der, çizgi "bu bir
  metin satırıdır" der. Ayrıca kontrol listesinin gri dolgusu beyaza dönünce kutu, satırı ayırt eden **tek** şey
  kaldı — alt görevler çizgi olarak bırakılsaydı iki liste bu turdan sonra daha FARKLI görünecekti.
- **Dolgu kartın kendi yüzeyi** (`--bs-card-bg`): `--bs-body-bg` beyaz kartın üstünde gri ölçülüyordu, yani her
  satır beyaz panelin içinde gri paneldi — içerikte olmayan bir iç içelik.
- **Tamamlanmış = devre dışı tonu** iki listede de; iptal edilmiş alt görev kendi daha derin solukluğunu korudu
  (iptal edilmiş iş, bitmiş iş değildir).
- **`:hover` + `:focus-within` birlikte:** ikincisi olmadan fare kullanıcısı nerede olduğunu görür, klavye
  kullanıcısı görmez. Satır içi düğmelerin kendi hover'ı ezilmiyor (ölçüldü: düğme zemini satırdan farklı).
- **Gelecek regresyon riski: 🟢.**

### BL-123 — 🟡 [ALT GÖREVLER / KONTROL LİSTESİ] Hover tonu ölçülebilir ama neredeyse görünmez
- **Ölçüm (alfa bileşimiyle):**
  | | ışık | karanlık |
  |---|---|---|
  | hover ↔ kart | **1.032** | **1.035** |
  | tamamlanmış ↔ kart | 1.251 | 1.730 |
- `rgba(var(--bs-primary-rgb), .03)` — **projenin kendi idiomu ve sahibin seçimi**, o yüzden değiştirmedim.
  Ama 1.03:1 pratikte fark edilmiyor; satırın kutusu ve metni bilgiyi taşıdığı için WCAG ihlali değil, yalnız
  etkisiz bir geri bildirim.
- **Seçenekler (karar sahibin):** (a) böyle kalsın — idiom tutarlılığı · (b) `.05`–`.06`'ya çıkar (tüm projede
  etki) · (c) yalnız bu iki listede daha güçlü bir ton (idiomdan sapma).
- **Gelecek regresyon riski: 🟢.**

### BL-124 — 🟠 [ALT GÖREVLER] Detaylı oluşturma paneli tekrar açılmıyor — Enter canlı doğrulanamadı
- **Ne yapıldı:** `#wcnSubtaskTitle` ve `#wcnNewSubtaskTitle` artık Enter'ı bağlıyor, düğmenin çağırdığı **aynı**
  save yoluna gidiyor (doğrulama, meşgul bayrağı ve hata yolu tek uygulama).
- **Canlı kanıt — hızlı düzenleme paneli ✓:** gerçek `Enter` tuşuyla panel kapandı, yeni başlık listeye düştü,
  "Alt görev kaydedildi." bildirimi çıktı.
- **Canlı kanıt — detaylı oluşturma paneli ✗ YAPILAMADI:** `+ Detaylı görev ekle` düğmesi ilk denemede paneli
  açtı, sonraki denemelerin **hiçbirinde** açmadı — sayfa yenilendikten sonra bile. Ölçüm: offcanvas DOM'da,
  `show` sınıfı yok, `visibility: hidden`, `transform: translateX(400px)`. Enter dalı bu yüzden yalnız **testle**
  ve düğmeyle kod-özdeşliğiyle kanıtlandı, canlı tuşla değil.
- **Yapılacak:** panelin açılma yolu (`openSubtaskCreatePanel` → Bootstrap Offcanvas örneği) neden ikinci
  çağrıda sessiz kalıyor, ayrı bir tur olarak incelenmeli. Bu, Enter'dan bağımsız bir kusur.
- **Not (ölçüm aracı):** tarayıcı panelinde `key: "Return"` `keydown` üretmiyor, `key: "Enter"` üretiyor; ayrıca
  programatik `.focus()` sonrası tuş enjeksiyonu panele ulaşmıyor — önce gerçek tıklama gerekiyor.
- **Gelecek regresyon riski: 🟡** — Enter dalı testle kilitli, panel açılışı açık.

### BL-125 — ✅ KAPANDI (2026-08-14) — [TEKNİK BİLGİ → KAYNAK] Kart yeniden adlandırıldı, açıldı, koşullandı
- **Ölçüm (önce):** kart `<details>` içinde, açıkken **277px**, altı alan + iki düğme. (Brief 331px demişti;
  aradaki iki turun ayırıcı ve satır değişiklikleri sayıyı düşürmüş — güncel taban 277.)
- **Ad:** "Teknik bilgi" → **"Kaynak"**. Teknik alanlar çıkınca içinde teknik bir şey kalmıyor; kalan şey
  "bu iş nereden geldi". Ayrıca eski başlık kapıya "burası sana göre değil" yazıyordu.
- **Kat kaldırıldı:** üç satırlık kart bir tık maliyeti ekliyordu. Başlık artık diğer kartlarla aynı yapıda
  (`cardHead` + ikon).
- **Tek sütun tanım listesi:** özet kartının iki sütunlu golden ızgarası 337px'lik rayda her değeri sardırıyordu.
- **İki kip — alan ancak ayırt ettiğinde görünür** (head kartındaki kaynak izine uygulanan kuralın aynısı):
  | alan | kendi modülümüz | yabancı sağlayıcı |
  |---|---|---|
  | Kaynaktaki durumu | görünür | görünür |
  | Modül · nesne türü · kayıt kimliği | **gizli** | görünür (kimlik + kopyala) |
  | Kaynak sürümü · işlem derinliği | **kaldırıldı** | kaldırıldı |
- **Etiket:** "Kaynak durumu" → **"Kaynaktaki durumu"**. Sayfa aynı görev için iki kelime söylüyordu — head'de
  "Beklemede" (bizim `normalizedStatus`), burada "Planlandı" (kaynağın kendi kelimesi) — ve hangisinin kimin
  kelimesi olduğu yazmıyordu.
- **Sonuç:** kart **277 → 131px**, ray **806 → 660px**. Kendi modülümüz kipinde tek alan kalıyor.
- **Ölü işaretlendi, silinmedi:** `.wcn-tech*` CSS bloğu; `TechnicalDetailsLabel`, `TechVersionValue`,
  `DetailSourceVersion`, `DetailActionDepth`, `ActionDepthInline/Deeplink` anahtarları 7 dilde duruyor.
  `referenceField`, `previewField`, `technicalVersion` yardımcıları çağıransız kaldığı için kaldırıldı (ölçüldü).
- **Gelecek regresyon riski: 🟢.**

### BL-126 — ✅ KAPANDI (2026-08-14) — [MEVCUT AKSİYONLAR] "Nerede tamamlanır" teknik alan değil, aksiyon oldu
- **Ölçüm:** `actionDepth` **tam olarak iki değer** alıyor — `ACTION_DEPTHS = ['inline', 'deeplink']`
  (`fixture-contract.js:13`). Yani "inline dışındaki her değer" tek bir değer demek; tahmin edilecek üçüncü
  vaka yok. Eski "İşlem derinliği" satırı neredeyse her görevde "Burada tamamlanır" yazıyordu.
- **Yapılan:** `deeplink` durumunda birincil (dolu) denetim **"{Modül}'de tamamla"** bağlantısına dönüşüyor,
  dış-bağlantı ikonuyla; altında "Bu iş burada tamamlanamaz; {Modül} modülünde bitirilir". Motorumuzun hâlâ
  geçerli aksiyonları (Bilgi bekle, Başkasına ata) ikincil kalıyor. **"Kartta tam olarak bir dolu düğme" kuralı
  korunuyor** — bu birincilin yerine geçiyor, yanına değil.
- **Kaynak kartındaki "Kaynak kaydını aç" düğmesi bu durumda çekiliyor** — aynı hedefe iki denetim, bu sayfanın
  sürekli temizlediği çiftlemedir.
- **⚠ BİR TUZAK ÖLÇÜLDÜ:** ilk yazımda koşul `item.actionDepth === 'deeplink'` idi ve **hiç çalışmadı** —
  sunum katmanı o alanı taşımıyor. Doğrusu resolver'ın çözdüğü `surface.surfaceMode === 'deeplink'`; tek model
  tüketiliyor, ikincisi türetilmiyor.
- **Gelecek regresyon riski: 🟢.**

### BL-127 — 🟡 [KAYNAK] Yabancı sağlayıcı kipi CANLI DOĞRULANMADI
- **Bugün sistemdeki her kayıt** `providerCode: "tasks"` / `objectType: "task"` / `actionDepth: "inline"`.
  Dolayısıyla **yabancı sağlayıcı kipi ve deeplink kipi canlıda üretilemez**; o dallar ikinci sağlayıcı
  (MOD-0023 iş akışı) gelene kadar hiç çalışmaz.
- **Kapsama:** her iki dal da fikstürle test edildi (`wcn-detail-three-regions.test.js`). Bu fikstürler
  **ulaşılamayan bir dalı kapsıyor**; üretim kodunun yerine geçip kusurunu gizlemiyorlar.
- **Canlı ölçülemeyen davranışlar:** yabancı kipte modül/tür/kimlik alanlarının görünmesi · **kopyala düğmesinin
  gerçekten kopyalaması** (düğme artık yalnız yabancı kipte çiziliyor, yani tam da üretilemeyen dalda) ·
  deeplink birincilinin gerçek tıklamayla hedefe gitmesi.
- **İkinci sağlayıcı geldiğinde ölçülecek** — bu madde o zaman kapanır.
- **Gelecek regresyon riski: 🟡.**

### BL-128 — 🟢 [ÖLÇÜM DİSİPLİNİ] Kendi eklediğim CSS yorumu stil dosyasını kırdı, canlı ölçüm yakaladı
- **Ne oldu:** ölü `.wcn-tech` bloğunu işaretlerken seçiciyi çiftledim —
  `.wcn-tech > .wcn-tech-summary {.wcn-tech > .wcn-tech-summary {` — bu parse hatası **dosyanın geri kalanını
  öldürdü** (`.wcn-subtask-body` dahil, yani alt görev satırı tek sütuna çöktü).
- **Nasıl yakalandı:** ekran görüntüsünde alt görev başlığı ile metası yan yana göründü; hesaplanan
  `flex-direction` `column` yerine `row` çıktı; hiçbir stylesheet kuralı `.wcn-subtask-body` ile eşleşmiyordu.
  **Testler bunu yakalamadı** — jsdom CSS yüklemiyor.
- **Ders:** CSS'te blok ekleyen betikler için ayraç dengesi kontrolü ucuz ve etkili
  (`{` ve `}` sayısı, yorumlar soyulduktan sonra).
- **Yapılacak (istenirse):** bu kontrolü bir teste bağla — `backbone-custom.css` ayraçları dengeli olmalı.
- **Gelecek regresyon riski: 🟢** — düzeltildi ve canlı doğrulandı.

### BL-129 — ✅ KAPANDI (2026-08-14) — [ALT GÖREV PANELLERİ] Açık bir panelin altından render çekiliyordu
- **Belirti:** "Detaylı görev ekle" ilk tıklamada açıyor, sonra hiçbir tıklamada açmıyor — sayfa yenilense bile.
- **TEŞHİS — iki panelin karşılaştırması sebebi verdi.** Aynı sayfada iki panel var, biri çalışıyordu:
  | | çalışan (hızlı düzenleme) | çalışmayan (detaylı oluşturma) |
  |---|---|---|
  | açılış sırası | `render() → show() → await` | `render() → show() → await → **render()**` |
  | await sonrası render | **yok** | **var** ← fark burada |
- **ÖLÇÜM (MutationObserver, iki gerçek tıklama):**
  - `t=83014` düğüm #2 oluştu — `showPanel` Offcanvas örneğini **buna** bağladı, `.show()` çağırdı
  - `t=83077` düğüm #3 oluştu — **+63ms**, tam olarak `assignablePeople` gidiş-dönüşü
  - son durum: düğüm #3, örnek **yok**, `show` **yok**
  İkinci `render()` `#wcnApp` altını değiştiriyor; Bootstrap örneği artık belgede olmayan düğüme bağlı kalıyor.
  "İlk tıklamada çalışıyor" görüntüsü, açılış animasyonunun silinmeden önceki o 63 ms'si.
- **DAHA KÖTÜ YARISI — kayıt yollarını ölçünce çıktı:** iki panel de meşgul durumu ve hata dalında `render()`
  çağırıyordu. Düğüm değişince `hidden.bs.offcanvas` hiç ateşlenmiyor, dolayısıyla Bootstrap
  **`body { overflow: hidden }`'ı geri almıyor**. Canlı ölçüldü: başarısız oluşturmadan sonra panel görünmez,
  örnek yok, backdrop gitmiş — **ve sayfa kaydırılamıyor.** Kullanıcı sıkışıyor.
- **DÜZELTME (üç parça):**
  1. Panel **hemen** açılıyor; arama sonucu `render()` yerine **canlı `<select>`'e yazılıyor** (`fillAssigneeSelect`).
  2. Meşgul durumu **düğmeye yerinde** uygulanıyor (`setPanelBusy`), render ile değil.
  3. Kapanış **Bootstrap'in `hide()`'ı üzerinden** (`hidePanel`); `hidden` olayı state'i temizliyor ve TEK
     render'ı o yapıyor — body kilidini uygulayan kütüphane geri alıyor.
- **⚠ İLK DÜZELTMEM YANLIŞTI ve testler yakaladı:** aramayı panelden ÖNCE await etmiştim. O zaman yavaş/hatalı
  bir kişi servisi paneli hiç açtırmıyor — üç mevcut test kırmızıya döndü (arama stub'lanmamış, yani tam da
  "servis yanıt vermiyor" vakası). Panelin açılışı hiçbir uzak çağrıya bağlanmamalı.
- **CANLI DOĞRULAMA:** AÇ → KAPAT → TEKRAR AÇ → TEKRAR KAPAT → ÜÇÜNCÜ AÇ, üç kez (yenilemeden önce, arada yazma
  işlemi yaptıktan sonra, ve sayfa yenilendikten sonra). Her açılışta örnek var, backdrop 1, atanan listesi
  5 seçenekli; her kapanışta backdrop 0 ve body serbest.
- **Enter kanıtı tamamlandı** (geçen turun borcu): `#wcnNewSubtaskTitle`'a yazıp gerçek `Enter` → alt görev
  oluştu (19→20), listede göründü, panel temiz kapandı, bildirim: "'CT son kanit Enter' alt görevi eklendi."
- **Gelecek regresyon riski: 🟢** — üç kural da testle kilitli.

### BL-130 — 🟡 [ALT GÖREV PANELİ] Detaylı panelde son tarih zorunlu ama işaretli değil
- **Ölçüm:** panelde başlık dışında hiçbir alan yıldızlı değil, ama son tarih boş bırakılınca oluşturma
  **başarısız** oluyor ve bildirim yalnız "İşlem sırasında bir hata oluştu." diyor. Gerçek sebep API'de:
  `VALIDATION_REQUEST_DUE_AT_NOT_NULL` ("A due date is required.").
- **İki kusur birden:** (a) zorunlu alan UI'da işaretsiz — orchestrator kuralı "Backend Validator'daki zorunlu
  alanlara UI label'larında kırmızı yıldız" ihlali; (b) sunucunun anlaşılır reason_code'u generic bir mesajla
  maskeleniyor.
- **Yapılacak:** son tarihe `*` ve `required`; `failureMessage` bu reason_code'u kendi diline çevirsin.
- **Gelecek regresyon riski: 🟢** — bugün de başarısız oluyor, yalnız sebebi görünmüyor.

### BL-131 — ✅ KAPANDI (2026-08-14) — [KOMPOZİSYON] Ray erken bitiyordu; yapışkan hâle geldi
- **Ölçüm (1440×900):** içerik sütunu 1860px'e kadar sürüyor, ray 925px'te bitiyor → **936px** boyunca sağda boş
  sütun var ve sayfanın var olma sebebi olan "Mevcut aksiyonlar" kartı ekran dışında.
  ⚠ Brief 766px demişti; geçen turun test alt görevleri (17→20) içerik sütununu uzatmış, olgu aynı, sayı büyümüş.
- **Üç yol ölçüldü, biri seçildi:**
  | yol | ölçüm | karar |
  |---|---|---|
  | (a) yapışkan ray | ray 660px, gereken görüntü alanı 676px | **seçildi** |
  | (b) kontrol listesi raya | ray 1273 ↔ içerik 983 | dengesizlik ters dönüyor |
  | (c) içerikte iki sütun | her sütun 427px; alt görev başlığı bugün 626px | sıkışık |
- **Uygulama:** `@media (min-width: 992px)` içinde `position: sticky` + `inset-block-start: 5rem` (64px sabit
  navbar + 16px) + `align-self: start` (esnemiş bir ızgara öğesinin yapışacak yeri olmaz) +
  `max-block-size: calc(100vh - 6rem)` + `overflow-y: auto`.
- **Kırpmak yerine bozuluyor:** 676px'ten kısa bir pencerede ray kendi içinde kayıyor; sabit yükseklikli bir
  yapışkan sütun alt kartlarını ulaşılamaz kılardı — ki yapışkanlığın bütün amacı aksiyonların erişilebilir
  kalması.
- **992'nin altında kapalı** (ölçüldü: 900'de `position: static`) — yığılmış düzende ray içeriğin altındadır ve
  yapışacak anlamlı bir şey yoktur.
- **Doğrulama:** yapışkanlığı kıran tek şey ata zincirinde `overflow ≠ visible`'dır; **zincir temiz** ölçüldü,
  kapsayıcı blok 1722px, rayın yapışabileceği mesafe **1062px**. Derin kaydırmalı görsel doğrulama bu ortamda
  yapılamadı (tarayıcı paneli gizliyken gerçek tekerlek zaman aşımına uğruyor, programatik `scrollTop`
  reddediliyor — BL-098 ile aynı sınırlama).
- **Gelecek regresyon riski: 🟢.**

### BL-132 — 🟠 [KOMPOZİSYON] 900px'te aksiyonlar 1876px aşağıda — DOM/görsel sıra çelişkisi kararı sizde
- **Ölçüm (900×900):** "Mevcut aksiyonlar" kartının üst kenarı sayfanın **1876. pikselinde**, yani **2.08 ekran**
  kaydırma. Brief ~1000px tahmin etmişti; gerçek daha kötü.
- **DOM sırası ile görsel sıra bugün AYRIŞMIYOR** (head → content → rail, ikisi de aynı).
- **CSS `order` ile rayı yukarı almak** görsel sırayı değiştirir ama **sekme sırasını değiştirmez** — Tab hâlâ
  head → 1596px içerik → ray diye gider. Yani gören kullanıcı aksiyonları üstte görür, klavye kullanıcısı onlara
  ulaşmak için bütün içeriği geçer. WCAG 2.4.3 (Odak Sırası) anlamında gerçek bir ayrışma.
- **Brief'in talimatı gereği çözümü seçmedim.** Seçenekler: (a) olduğu gibi bırak · (b) `order` uygula ve
  ayrışmayı kabul et · (c) dar ekranda DOM sırasını değiştir (yeniden boyutlandırma dinleyicisi gerekir, sayfa
  bugün boyut değişiminde yeniden çizilmiyor) · (d) dar ekranda aksiyonları head kartının altına taşı.
- **Gelecek regresyon riski: —** (karar bekliyor).

### BL-133 — 🟡 [KONTROL LİSTESİ] Kapak yok: 6 maddede 294px, 20 maddede ~1000px olur
- **Ölçüm:** Alt Görevler kartı 561px = 320px kapak + **241px** kapak dışı (başlık 22, çubuk 6, ekleme satırı 38,
  ipucu 17, "Tümünü gör" 30, engel uyarısı 44, dolgu 32, boşluklar ~52). Bu 241'in çoğu işlevsel.
- **Kontrol Listesi 597px ve kapağı HİÇ YOK** — listesi 294px olarak sınırsız çiziliyor. `cappedList` yardımcısı
  mevcut ve alt görevlerle etkinlik akışında kullanılıyor; kontrol listesi kullanmıyor.
- **Sonuç:** 20 maddelik bir kontrol listesi kartı tek başına ~1300px olur ve sayfa 3+ ekrana çıkar.
- **Yapılacak:** kontrol listesine de `cappedList('checklist', …)` — yardımcı zaten `aria-label` için
  `ChecklistLabel`'ı biliyor.
- **Gelecek regresyon riski: 🟢** — eklemeli.

### BL-134 — ✅ KAPANDI (2026-08-14) — [ALT GÖREV PANELİ] Zorunlu son tarih işaretsizdi, hata gerçeği gizliyordu
- **Kural önce sorgulandı, sonra yıldız kondu.** Ana görev oluşturma ucu da son tarihsiz isteği reddediyor
  (ölçüldü: `400 VALIDATION_REQUEST_DUE_AT_NOT_NULL`, "A due date is required.") ve `_Form.cshtml` alanı zaten
  kırmızı yıldızla işaretliyor. Kural ürünün; tutarsız olan tek yüzey alt görev paneliydi.
- **İki düzeltme:** panelin son tarih etiketine `*`; ve `VALIDATION_REQUEST_DUE_AT_NOT_NULL` →
  `errorDueDateRequired` eşlemesi `REASON_CODE_MESSAGE_KEYS`'e eklendi (köprü zaten vardı ve eşlenmemiş kodlar
  için konsola uyarı bile veriyordu — kimse bu kodu eşlememişti).
- **Köprünün ikinci ucu da bağlandı:** `_IndexL10n.cshtml`'e anahtar eklendi — **bunu iki mevcut guard testi
  yakaladı**, ben eklemeyi unutmuştum. 7 dil.
- **Gelecek regresyon riski: 🟢.**

### BL-135 — 🟡 [ÖLÇÜM DİSİPLİNİ] İkinci kez kendi CSS eklemem stil dosyasını kırdı
- **Bu turda:** yapışkan ray bloğunu `.wcn-detail-rail > .wcn-detail-card` satırına çıpalayarak ekledim. Oysa o
  kural **iki seçicili**ydi:
  `.wcn-detail-content > .wcn-detail-card,` / `.wcn-detail-rail > .wcn-detail-card { … }`
  Blok tek kuralın iki seçicisinin **arasına** düştü → sarkan seçici + at-rule → parser bloğu attı.
  Belirti: dosyada kural var, `getComputedStyle` `static` diyor, tarayıcının kural listesinde hiç yok.
- **BL-128 ile aynı sınıf** (o sefer seçiciyi çiftlemiştim). İkisini de **canlı ölçüm** yakaladı; hiçbirini
  derleme veya test yakalamadı — CSS derlenmiyor, testler jsdom'da stil uygulamıyor.
- **Yapılacak:** CSS'e bir sözdizimi/lint kapısı (stylelint) veya en azından derleme öncesi ayraç-denge kontrolü.
  Bugün bu dosyanın tek doğrulayıcısı gözle canlı ölçüm.
- **Gelecek regresyon riski: 🟠** — üçüncüsü gelene kadar açık.

### BL-136 — ✅ KAPANDI (2026-08-14) — [DAR EKRAN] Yapışkan aksiyon şeridi (<992px)
- **Ölçüm:** 900px'te "Mevcut aksiyonlar" kartının üstü sayfanın **1876. pikselinde** (sayfa 2597) → 2.08 ekran
  kaydırma. ≥992'de ray yapışkan ve aksiyonlar hep görünür, orada sorun yok.
- **İŞ 0 — desen taraması:** projede **alta yapışan aksiyon çubuğu deseni YOK**. Konumlandırma mekanizması var:
  Bootstrap'in kendi `.sticky-bottom` yardımcısı (`position:sticky; bottom:0; z-index:1020`), üründe bir yerde
  kullanılıyor (`GoldCreate.cshtml` → `goal-readiness-panel`, bir hazırlık paneli).
  **Mekanizma yeniden kullanıldı, kompozisyon YENİ — projeye yeni bir desen eklendi.**
- **Kapsam:** yalnız `<992px` (`d-lg-none`: `display:none` öğeyi yerleşimden, erişilebilirlik ağacından ve sekme
  sırasından birlikte çıkarır). **Tek render çıktısı**, genişlik dalı yok, resize dinleyicisi yok.
- **Geniş ekran birebir aynı** (ölçüldü, 1440 ve 1200, iki tema): şerit `display:none`, ray sticky, aksiyon kartı
  263/303px, içerik 870/710, ray 435/355, sayfa 1921px, ayırıcı 0/0, tek dolu düğme, kart ritmi 16px.
- **TEK KAYNAK:** `actionTiers(item)` çıkarıldı; hem kart hem şerit ondan okuyor. Canlı: ikisi de
  `accept · cancel · inquire · plan · reassign`. Açıklanamayan devre dışı aksiyonun filtresi de oraya taşındı,
  yani iki yüzeyden birine değil ikisine birden uygulanıyor.
- **z-index doğrulandı:** şerit 1020 < backdrop 1089 < offcanvas 1090 → açık panel şeridi örtüyor, tersi değil.
- **Kilit doğrulandı** (canlı): uçuşta şerit `wcn-actionbar-locked`, birincil "Uygulanıyor…" + `aria-busy`,
  "Diğer aksiyonlar" da devre dışı; sonra geri dönüyor.
- **Dört durum (900px):** olağan `98d1f94e` · kilit `98d1f94e` · engellenen `049e9109` (birincil "Tamamla"
  devre dışı + gerekçesi şeritte okunabilir) · kapanmış `ad7f9af3` (şerit YOK).
- **⚠ İlk denemem negatif kenar boşluğu kullandı ve yatay kaydırma yarattı** (şerit −8→893, viewport 900).
  Kaldırıldı; şerit içerikle hizalı. Bu, BL-121'de zaten yasakladığımız desendi.
- **Gelecek regresyon riski: 🟢.**

### BL-137 — 🟠 [DAR EKRAN] Şerit klavye kullanıcısına kısayol SAĞLAMIYOR
- **Ölçüm (900px):** şeridin düğmeleri sekme sırasında **139/149** — yani en sonda, çünkü şerit DOM'da en son.
  Görsel olarak ekranın altında sabit duruyor ama Tab ile oraya varmak için sayfanın tamamı geçiliyor.
- **Sonuç:** şerit fare/dokunmatik kullanıcı için kısayol, klavye kullanıcısı için değil. Aksiyon kartı da
  900px'te içerikten sonra geldiği için klavye kullanıcısı her hâlükârda uzakta.
- **Neden `order`/DOM taşıma yapılmadı:** sahip kararı bu turda aksiyon kartının yerini değiştirmemekti; DOM'da
  şeridi öne almak da görsel/sekme ayrışması yaratırdı (BL-132'nin aynısı).
- **Seçenekler:** (a) şeride `accesskey` · (b) sayfa başına "aksiyonlara atla" bağlantısı · (c) BL-132'nin
  kararıyla birlikte çözülsün.
- **Gelecek regresyon riski: —** (karar bekliyor).

### BL-138 — 🟢 [DAR EKRAN] Şerit yokken de 80px alt dolgu uygulanıyor
- **Ölçüm:** kapanmış görevde (`ad7f9af3`) şerit çizilmiyor ama `.wcn-details-page` yine `padding-block-end: 80px`
  alıyor → sayfanın altında 80px ölü boşluk.
- **Sebep:** dolgu medya sorgusuyla genişliğe bağlı, şeridin varlığına değil.
- **Yapılacak:** dolguyu şeridin varlığına bağla (şerit çizilirken sayfaya bir sınıf, ya da `:has()`).
- **Gelecek regresyon riski: 🟢.**

### BL-139 — ✅ KAPANDI (2026-08-14) — [KONTROL LİSTESİ] Kapak eklendi (BL-133 kapanışı)
- 8 maddenin üstünde `cappedList('checklist', …)` — alt görev listesi ve etkinlik akışıyla **aynı yardımcı**,
  aynı 320px kutu. Eşik gerekçesi: kontrol listesi satırı ve alt görev satırı ikisi de 38px, yani aynı kapak
  ikisinde aynı sayıda satır gösteriyor; üçüncü bir sayı seçmek yerine mevcut olanı kullandım.
- `aria-expanded` ve bölge etiketi yardımcıdan geliyor (diğer ikisinde zaten vardı).
- **Gelecek regresyon riski: 🟢.**

### BL-140 — ✅ KAPANDI (2026-08-14) — [KİŞİSEL NOT] Gereksiz tam sayfa yeniden çizimi kaldırıldı
- Not kaydetme `render()` çağırıyordu; oysa metin kutusu yazılanı zaten tutuyor ve sayfada notu gösteren başka
  yer yok — yani tüm detay sayfası hiçbir görünür değişiklik için yeniden çiziliyordu.
- **⚠ RAPORUN VARSAYIMI ÖLÇÜMDE ÇÜRÜDÜ:** "panel açıkken not kaydetmek paneli düşürüyor" deniyordu. Ölçüm: panel
  açıkken offcanvas backdrop'u tüm görüntü alanını kaplıyor (900×900; sayfa ortasında `elementFromPoint`
  backdrop döndürüyor) ve Bootstrap odağı panelin içinde hapsediyor — **gerçek bir okuyucu o düğmeye
  ulaşamıyor.** Geçen turun uyarısı programatik bir tıklamadan ateşlemişti.
- Render yine de kaldırıldı, çünkü zaten gereksizdi. Erteleme ve plan yazmaları render çağırmaya devam ediyor;
  ikisi de gerçekten görünür değişiklik üretiyor ve ikisi de panel açıkken erişilemez.
- **Gelecek regresyon riski: 🟢.**

### BL-141 — [KİŞİSEL KATMAN] "Kişisel plan tarihi" aslında kişisel değil; ekrandaki etiket yanlış
- **ÖLÇÜM (2026-08-14).** İş 0 "plan tarihi nerede saklanıyor, not ve erteleme de oraya gitsin" diye soruyordu.
  Ölçüldü: `PlannedDate` **`TaskItem` üzerinde** — paylaşılan görev kaydında, `TaskWorkItemProvider.cs:551`'de
  üst düzey bir alan olarak yansıtılıyor ve `plan` eylemi paylaşılan yaşam döngüsünü Open→Planned oynatıyor.
  Görevi okuyabilen **herkes** görüyor.
- Yani üç şey değil **iki** kişisel şey var. Not ve erteleme (kişi başına, gizli) tek bir yere — yeni
  `task_personal_overlays` belgesine — gitti. Plan tarihi yerinde kaldı: oraya taşımak **nerede durduğunu değil
  ne anlama geldiğini** değiştirirdi (talep eden yeniden planlamayı artık göremezdi).
- **Kusur depoda değil, ekranda:** Kişisel kartı plan tarihini "Kişisel" başlığı altında gösteriyor. Etiket
  düzeltilmeli ya da satır Özet'e taşınmalı. **Bu tur yapılmadı — yer kararı CT'nin.**
- **Gelecek regresyon riski: 🟡** — birisi tutarlılık adına plan tarihini overlay'e taşımaya kalkarsa sessizce
  bir görünürlük kaybı üretir. `WorkAggregationModels.cs`'teki yorum bunu artık açıkça yazıyor.

### BL-142 — [KİŞİSEL KATMAN] Dört ayar projeksiyonda; ekranda yeri kararlaştırılmadı
- Projeksiyona giren şekiller (canlı ölçüldü, 2026-08-14):
  `watchers: [{ person: {id, displayName, isCurrentUser}, role: "Watcher|Consultant|Informed" }]` · yoksa alan yok
  `delegationAllowed: true|false` · `notifications: { emailEnabled: bool, events?: string[] }` (events **yoksa**
  = "hiç seçilmedi, hepsi gönderilir"; **boş dizi** = "hiçbiri seçilmedi") · `reminderLeadDays: 3` · yoksa alan yok.
- Canlı doğrulama, seed edilip geri alınan bir görevle: dördü de tel üstünde göründü, izleyici adıyla birlikte.
- **Ekrana konmadı, bilerek.** Hangi kartta duracakları tasarım kararı. Öneri (CT'ye): izleyiciler ve devir
  izni Özet'e; bildirim tercihleri + hatırlatma günü tek bir "Bildirimler" satırına.
- **Gelecek regresyon riski: 🟢** — hepsi opsiyonel ve null'da atlanıyor.

### BL-143 — [KİŞİSEL KATMAN] Erteleme gelen kutusu süzmesi hâlâ istemcide
- **ÖLÇÜLDÜ:** `segmentFor` ve liste süzgeci `item.snoozedUntil`'ı tarayıcıda okuyor. Artık sunucudan geliyor,
  ama **kararı** hâlâ istemci veriyor: sayfalama sunucuda olsaydı ertelenmiş işler sayıya dahil olurdu.
- Bugün zararsız (sayfalama istemcide). Sunucu tarafı sayfalama geldiği gün süzme de sunucuya taşınmalı, yoksa
  "3 iş" yazan bir sekme 2 satır gösterir.
- **Bu turda uygulanmadı, karar kaydedildi.** **Gelecek regresyon riski: 🟡.**

### BL-144 — [KİŞİSEL KATMAN] Kişisel not düzenlenemiyor (karar), ve sabitleme (pin) hâlâ hiçbir yere yazmıyor
- Not için **düzenleme yok**, karar: sil + yeniden yaz. Bir uç, bir eşzamanlılık sorusu, bir denetim hikâyesi az.
- **Pin bilerek dışarıda bırakıldı:** ne ön yüzde ne arkada bir davranışı var. Hiçbir şeyin yazmadığı ve hiçbir
  şeyin okumadığı bir alanı yansıtmak, bu turun kapattığı yarımın aynısını yeniden üretirdi.
  `WorkAggregationModels.cs`'teki yorum bunu da yazıyor.
- **Gelecek regresyon riski: 🟢.**

### BL-145 — [GÖÇ] 137 görevin 136'sında overlay belgesi yok; geri doldurma yapılmadı
- **ÖLÇÜM (2026-08-14, dev):** `task_items` = 137, `task_personal_overlays` = 1 (bu turda canlı testte yazılan).
  Yani **mevcut her görev** overlay'siz.
- Davranış ölçüldü: overlay yoksa `personal` alanı **hiç gönderilmiyor** (boş kap değil), istemci `item.notes`'u
  boş diziye normalleştiriyor, kart yalnız ekleme satırını çiziyor. Geri doldurma **gerekmiyor ve yapılmadı** —
  boş bir belge yazmak, 137 kaydı hiçbir şey için üretmek olurdu.
- Aynı şey erteleme için: süresi geçmiş bir erteleme `null` olarak yansıtılıyor, kararı sunucu veriyor.
- **Gelecek regresyon riski: 🟢.**

### BL-146 — [MODAL] On ham `Swal.fire` kaldı; ortak sarmalayıcı bu şekilleri desteklemiyor
- **ÖLÇÜM (2026-08-14).** `app.js`'te **on beş** doğrudan `Swal.fire(` vardı (brifing on diyordu; on beş ölçüldü,
  beşi bu turda taşındı, geriye **on** kaldı — brifingin sayısı taşımadan SONRAKİ duruma denk geliyor).
- **Taşınanlar (5):** eylem onayı · toplu eylem onayı · tetikleyici gerekçesi · toplu gerekçe · hızlı not.
  Hepsi `window.showConfirm` üzerinden; canlı doğrulandı (`.swal-icon-circle`, `btn btn-danger … px-5`,
  `btn btn-label-secondary … px-5` = sarmalayıcının kendi parmak izleri).
- **⚠ `window.DitenModal` bu sayfada TANIMSIZ** — premium-modal.js WorkCenterNext görünümlerine yüklenmiyor.
  `DitenModal.confirm` zaten `showConfirm`'e devrediyor, o yüzden doğrudan `showConfirm` aynı uygulamaya varıyor;
  yüklenmemiş bir global üzerinden gitmek sessiz bir no-op olurdu.
- **Taşınamayanlar (10) — sarmalayıcı yalnız TEXTAREA girdisi sunuyor:**
  | # | diyalog | ihtiyaç duyduğu şekil |
  |---|---|---|
  | 1 | plan tarihi | flatpickr tarih |
  | 2 | toplantı zamanı | flatpickr tarih+saat |
  | 3 | süre gir | `input: number` |
  | 4 | ertele | flatpickr tarih |
  | 5 | "+ Yeni" menüsü | iki düğmeli menü (onay değil) |
  | 6 | kaynakta oluştur | `input: select` |
  | 7 | toplantı formu | 4 alanlı form |
  | 8 | yeniden atama | select + textarea |
  | 9 | toplu sonuç | bilgilendirme (onay değil) |
  | 10 | toplu ilerleme | ilerleme çubuğu |
- **KARAR CT'DE.** Seçenekler: (a) sarmalayıcıya `input` tipi + `didOpen` seamı eklemek — ortak bileşen büyür,
  tek modülün ihtiyacıyla değil bir tasarım kararıyla; (b) bunları ham bırakmak ve kuralı "yalnız onay diyalogları
  sarmalayıcıdan geçer" diye daraltmak; (c) diyalog dışı bir yüzeye taşımak (offcanvas form).

- **GÜNCELLEME (2026-08-23) — seçenek (a) alındı, ama BÜYÜTEREK DEĞİL: sabit bir varsayım parametreye çevrildi.**
  `swalConfig.input = 'textarea'` artık `options.inputType || 'textarea'`. Yeni yetenek yok, varsayılan bugünkü
  davranış. `showInput` kullanan **altı** mevcut çağıran (Tenants ×3, AuditLog ×1, TemplateMasters ×1,
  WorkCenterNext seamı ×1) hiçbir tip vermiyor, dolayısıyla altısı da textarea almaya devam ediyor — bu, view'in
  scripti gerçek bir Swal taklidiyle çalıştırılarak ÖLÇÜLDÜ, kaynak metnine bakılarak değil.
- **Ertele taşındı** → `app.js`'te kalan ham diyalog sayısı **10 → 8**. Kalanların taşınabilirliği yeniden ölçüldü:
  | # | diyalog | durum |
  |---|---|---|
  | 1 | plan tarihi (`app.js:6263`) | ✅ **artık taşınabilir** — ertelemenin birebir aynı şekli (`inputType:'text'` + flatpickr + `validate`) |
  | 2 | toplantı zamanı (`app.js:6413`) | ✅ **artık taşınabilir** — aynı şekil, flatpickr `enableTime` ile |
  | 3 | süre gir (`app.js:6438`) | ✅ **artık taşınabilir** — `inputType: 'number'` |
  | 4 | kaynakta oluştur (`app.js:6736`) | ⚠ **hâlâ değil** — `input: 'select'` çalışır ama sarmalayıcı `inputOptions`'ı iletmiyor; TEK bir parametre daha gerekiyor |
  | 5 | "+ Yeni" menüsü (`app.js:6615`) | ❌ onay diyaloğu değil — iki düğmeli menü, onay düğmesi yok |
  | 6 | toplantı formu (`app.js:6762`) | ❌ dört alanlı form — tek girdilik şekle sığmaz |
  | 7 | gerekçe + atanan + beklenen kişi (`app.js:6887`) | ❌ çok alanlı form |
  | 8 | toplu ilerleme (`app.js:7065`) | ❌ ilerleme çubuğu; düğmesi yok, kapatılamaz |
- **BU TURDA TAŞIMA YAPILMADI** (sahibin talimatı). Üçü hazır bekliyor; dördüncüsü için `inputOptions`
  kararı CT'de.
  **Ajan kendi başına genişletmedi.** **Gelecek regresyon riski: 🟡** — kural bugünkü hâliyle kısmen ihlal görünüyor.

### BL-147 — [MODAL] Toplu sonuç bildirimi hâlâ ham modal; `DitenModal` yüklenmediği için taşınamadı
- Toplu işlem kısmi başarısızlığında açılan `icon: error|warning` modali bir ONAY değil, bir BİLDİRİM.
  Ürünün bildirim seamı `DitenModal.error/warning` — ama o global bu sayfalarda yok (BL-146).
- Seçenekler: premium-modal.js'i WorkCenterNext görünümlerine eklemek · sayfanın kendi `toast(...,'error')`'ına
  çevirmek (davranış değişikliği, sorulmadan yapılmadı).
- **Gelecek regresyon riski: 🟢.**

### BL-148 — [ÖLÇÜM SINIRI] Alt görev satırının hizası kural listesinden doğrulandı, DOM'dan değil
- Kusur 4'te "satır dili üç listede aynı" iddiası için kontrol listesi satırı (`.diten-checkitem`) ve not satırı
  (`.wcn-note-row`) **canlı DOM'da** `center` ölçüldü. Alt görev satırı (`.wcn-subtask`) test görevinde yoktu;
  `center` değeri tarayıcının **kural listesinden** okundu.
- Alt görevi olan bir görevde DOM ölçümü yapılmadı. Küçük ama açıkça yazılıyor.
- **Gelecek regresyon riski: 🟢.**

### BL-149 — 🔴 [ENTERPRISE STRATEGY] Legacy emeklilik kapısı: eşdeğerlik matrisi olmadan silme yok
- **Nereden geldi (2026-08-14):** başka bir çalışmadan (Codex) sahibe iletildi, sahip CONTROL TOWER'a
  aktardı. **Görev Merkezi'nin işi DEĞİL** — ayrı bir modülün yeniden yazımına ait. Buraya kaybolmasın
  diye yazılıyor; backlog zaten ortak ertelenen-iş kaydı.
- **ÖLÇÜM.** İkisi de depoda duruyor: `services/Diten.EnterpriseStrategyService` ve
  `frontend/Diten.Web/Views/EnterpriseStrategyBusinessPerformance`. Bu depoda bir "final acceptance plan"
  dokümanı **yok** — kapının ekleneceği plan başka bir çalışmanın bağlamında.
- **Kapının kendisi:** eski servis ve eski ekranlar **silinmeyecek, değiştirilmeyecek**. Her legacy sayfa için
  kaydedilecek: eski URL · varlıklar · alanlar · komutlar/aksiyonlar · yaşam döngüsü · izinler · entegrasyonlar
  · yeni sahip/modül · yeni URL · alan eşlemesi · her alanın durumu (Same / Replaced / Missing /
  Intentionally Removed / Out of Scope) · göç gereksinimi · tarayıcı kanıtı.
  Kullanıcı eski ve yeni sistemi **farklı portlarda aynı anda açıp sayfa sayfa** karşılaştıracak. Matris ve
  kullanıcı kabulü tamamlanmadan **legacy retirement / delete / "full parity complete"** kararı verilmeyecek.
- **Neden doğru bir kapı:** yeniden yazmalarda en sık kaybolan şey, kimsenin kullandığını bilmediği ekrandır —
  ve kim kullandığı ancak kaybolunca öğrenilir. Bu, bu depoda zaten uyguladığımız "canlı doğrulanmadan
  kapanış yok" kuralının modül emekliliği ölçeğindeki hali.
- **⚠ BİZE DEĞEN TEK YER — 9 KIRMIZI TEST.** Bu oturumun her turunda "bizden değil, HEAD'de de kırmızı"
  diye raporlanan testler tam olarak bu modülün: `frontend/Diten.Web/tests/goals-*.test.js` ve
  `objectives-*.test.js`. Sayıyı üreten komut:
  `cd frontend/Diten.Web && npx vitest run 2>&1 | tail -5`
  Enterprise Strategy yeniden yazılıyorsa o testler de o işin parçası; bugün sahipsiz duruyorlar.
- **Kimde:** Görev Merkezi'nde değil. Bu kalem bir **kayıt**, bir iş emri değil — kapıyı uygulayacak taraf
  kendi planına almalı.
- **Gelecek regresyon riski: 🔴 foundation.** Kapı konmadan silme yapılırsa geri dönüşü yok.

### BL-152 — ✅ KAPANDI (2026-08-14) — [BL-148 kapanışı] Alt görev satırı bu kez DOM'da ölçüldü
- BL-148 "alt görev satırının hizası yalnız kural listesinden doğrulandı" diyordu. Bu turda alt görevi OLAN bir
  göreve (049e9109, 5 alt görev) not eklenip **üç satır aynı sayfada** ölçüldü:
  | | zemin | kenarlık | yarıçap | dolgu | hiza |
  |---|---|---|---|---|---|
  | not | rgb(255,255,255) | 1px solid rgb(228,230,232) | 6px | 6px 8px | center |
  | kontrol listesi | aynı | aynı | aynı | aynı | aynı |
  | alt görev | aynı | aynı | aynı | aynı | aynı |
- Hover: üçü de `rgba(var(--bs-primary-rgb), .03)`, tarayıcının kural listesinden okundu.
- **⚠ BU TURDA BULUNAN GERÇEK SAPMA:** not satırı `--bs-body-bg` + `.4375rem` dolgu taşıyordu; diğer ikisi
  `--bs-card-bg` + `.375rem`. Yani "satır dili aynı" iddiası geçen tur DOĞRU DEĞİLDİ — yalnız `align-items`
  ölçülmüştü. `--bs-body-bg` tam da kontrol listesi kuralının kendi yorumunda uyardığı hata (beyaz kartın
  içinde gri panel). Düzeltildi ve dört özelliği birden karşılaştıran bir test yazıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-153 — [ÖLÇÜM SINIRI] Kişisel kart 900px'te ekran görüntüsüyle doğrulanamadı (dördüncü kez)
- 900px'te Kişisel kart sayfa-y **2180**'de başlıyor (2.4 ekran aşağıda) ve bu ortam oraya kaydıramıyor
  (`scrollY` 0'da kalıyor; BL-098). Bu turda dördüncü kez.
- **Ne ÖLÇÜLDÜ:** kartın tüm hesaplanmış stilleri, satır hizası, satır dili karşılaştırması, kontrol sayıları —
  `getComputedStyle` ve `getBoundingClientRect` kaydırmadan bağımsız çalışıyor. **Ne ÖLÇÜLEMEDİ:** kartın 900px'te
  nasıl GÖRÜNDÜĞÜ (ekran görüntüsü).
- **Gelecek regresyon riski: 🟢** — ölçüm boşluğu, kod boşluğu değil.

### BL-154 — [ARTIK] `Unsnooze` ve `PersonalActionsLabel` anahtarlarının durumu
- Erteleme satırı gelince `Unsnooze` ("Ertelemeyi kaldır") artık hiçbir yerde çizilmiyor; yerini satırdaki
  `SnoozeClear` ("Kaldır") aldı. **Silinmedi** — liste yüzeyinde hâlâ kullanılıyor olabilir, ölçülmedi.
- `PersonalActionsLabel` yalnız ertelenmemiş durumdaki grup etiketinde kaldı.
- **Yapılacak:** liste yüzeyinde `Unsnooze` kullanımı var mı ölç; yoksa ölü işaretle. Bu turda ölçülmedi.
- **Gelecek regresyon riski: 🟢.**

### BL-155 — ⚠ ÇELİŞKİ DÜZELTİLDİ — Bildirilen "grip çelişkisi" bir çelişki değildi
- **Bildirim:** `diten-checkitem.js:121` yorumu "Drawn in BOTH modes now" diyor ama `:139` kodu
  `if (!working) { el.appendChild(grip); }` — yorum mu bayat, kod mu yanlış?
- **ÖLÇÜM: İKİSİ DE DOĞRU.** Grip kaynakta **iki kez** ekleniyor. `:139` yazma kipinin sırası için erken ekliyor
  ("bunları düzenle" — tutamak önde); `:262` çalışma kipinin `if (working)` bloğu içinde geç ekliyor (metin ve
  seviye çipinden sonra, "bunları işaretle" sırası). `if (!working)` "yalnız yazma kipinde" demek değil, "burada,
  yazma kipinin sırasında" demek. Tek başına okunduğunda tersi görünüyor.
- **Canlı doğrulama (çalışma kipi, detay sayfası):** satırın çocukları `box · text · level · GRIP · move` —
  tutamak var, dolayısıyla `bindChecklistDrag`'in `handle: '[data-diten-check-grip]'` seçicisi bir şey buluyor.
- **Kod değiştirilmedi.** İki satırın tek tek okunamayacağını sabitleyen iki test eklendi.
- **Gelecek regresyon riski: 🟢.**

### BL-156 — [ÖLÇÜM] Yönlendirme ve engel uyarısı canlı veride hiç yan yana gelmiyor
- Sıra kararı verildi ve gerekçelendirildi: **yönlendirme önce, engel sonra.** Yönlendirme SIRADAKİ işi söyler;
  engel bir şeyin HENÜZ yapılamadığını söyler — engeli önce okuyan okuyucunun onu bağlayacağı bir şey yoktur.
- **⚠ CANLI ÖLÇÜLEMEDİ:** yüzeydeki 20 görev `pendingAcceptance`, 4 görev engelli/bekleyen, **kesişim sıfır**.
  İkisi bugün gerçek bir görevde asla birlikte çıkmıyor. Sıra, ikisini birden taşıyabilen bir fixture ile testte
  sabitlendi; canlı boşluk ölçümü yapılamadı.
- **Gelecek regresyon riski: 🟢.**

### BL-157 — [BRİFİNG DÜZELTMESİ] Ölçüm görevinin kontrol listesi boş, tek maddeli olan başka görev
- Brifing 46f6a43a'yı "alt görev, tek maddeli kontrol listesi" diye veriyordu. **Ölçüm: 0 madde.**
  Tek maddeli olan **d77e97d6** (o da bir alt görev). 98d1f94e'de 6 madde var.
- Kusur 3 ölçümü d77e97d6 (1) ↔ 98d1f94e (6) üzerinden yapıldı.
- **Gelecek regresyon riski: 🟢** — veri seçimi hatası, kod değil.

### BL-158 — [ÖLÇÜM] Alt görev satırında sıralama denetimi YOK, aynı desen orada geçerli değil
- "Kardeşini bırakma" kuralı gereği ölçüldü: alt görev satırının çocukları `wcn-subtask-check · wcn-subtask-body ·
  wcn-subtask-status · dropdown` — **taşı düğmesi ya da tutamak yok.** Alt görevler sıralanamıyor, dolayısıyla
  madde sayısına bağlı yükseklik değişimi orada oluşamaz.
- Düzeltme yalnız kontrol listesine uygulandı, çünkü desen yalnız orada var. Ölçülüp yazıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-159 — [TEST] "cancelling a subtask" testi tam süit altında kararsız (flaky)
- `wcn-detail-three-regions.test.js :: calls the cancel transition once the user confirms` bir tam süit
  koşusunda düştü ("reached no endpoint at all"), hemen ardından dosya tek başına 208/208 geçti ve ikinci tam
  süit koşusunda da geçti.
- Sebep: test sahte `showConfirm`'ü `setTimeout(…, 5)` ile çözüp `setTimeout(…, 30)` bekliyor. Tam süit yükü
  altında 25ms'lik pay yetmiyor. **Bu turdaki değişikliklerle ilgisi yok** — zamanlamaya duyarlı bir bekleme.
- **Yapılacak:** sabit beklemeyi bir koşul beklemesiyle değiştir (çağrı gelene kadar yokla). Bu turda
  yapılmadı; testin kendi konusu bu turun konusu değil.
- **Gelecek regresyon riski: 🟡** — yalancı kırmızı, gerçek bir kusuru gizlemez ama güveni aşındırır.

### BL-160 — ⛔ YAPILAMADI — İki uyarı YAPISAL OLARAK bir arada olamıyor (İş 4b'nin cevabı)
- İstenen: hem `pendingAcceptance` hem engelli bir görev tohumla, iki uyarının sırasını canlıda göster.
- **TOHUMLANDI (9bf6194e, açık alt görevle) VE OLMADI.** Sebep veri değil, mekanizma:
  1. `guidanceFor` yalnız şu hâllerde konuşur: pendingAcceptance · pendingClaim · onay/inceleme bekleyen · Waiting.
  2. Bir engelleyici ancak **etkilediği eylem SUNULUYORSA** hayatta kalır
     (`TaskWorkItemProvider`: `effectiveBlockers = blockers.Where(b => offered.Contains(...))`).
  3. Alt görev/bağımlılık engelleyicileri `complete`'i etkiler.
  4. `complete` yalnız **admitted + InProgress** iken sunulur — yani `guidanceFor`'un sustuğu tam da o hâl.
- **Canlı zincir ölçüldü:** pendingAcceptance → aksiyonlar `accept,plan,inquire,reassign,cancel`, `blocked:false`.
  Accept+inquire → Waiting → `start,reassign,cancel`, hâlâ `blocked:false`. Start → InProgress → `complete,…`,
  `blocked:true, blockers:[SUBTASK_BLOCKED]`, ve **yönlendirme yok**.
- Sıra yine de kararlaştırıldı ve testte sabitlendi (yönlendirme önce, engel sonra); ikisi bir gün buluşursa
  doğru sırada duracak. **Ekran görüntüsü alınamadı çünkü gösterilecek durum yok.**
- **Tohumlanan görevler TEMİZLENMEDİ** (sahip bakarak test ediyor): 9bf6194e (üst, InProgress+engelli) ve
  b1cc3ede (alt görev, kabul bekliyor).
- **Gelecek regresyon riski: 🟢** — bulgu, kusur değil.

### BL-161 — [BİLDİRİM] Alıcının dili değil, KİRACININ dili gönderiliyor
- Brifing "bildirim ALICININ dilinde gitmeli, bu modülde dil seçimi çözülmüş, aynısını kullan" diyordu.
  **ÖLÇÜM: çözülmüş olan şey kiracı dili.** `TaskNotificationService` `Locale: null` geçiyor ve
  `INotificationLocaleResolver` kiracının yapılandırılmış dilini döndürüyor — çünkü **AuthService'in User
  varlığında dil alanı yok** (servisin kendi yorumu bunu uzun uzun yazıyor).
- Canlı kanıt: yorum bildirimi `Locale = en` ile gitti (kiracı dili), alıcı `agent@diten.com`.
- Yorum şablonu yine de **yedi dilde** tohumlandı; eksik olan alıcı başına dil, şablon değil.
- **Yapılacak (bu turda YAPILMADI):** User'a dil alanı + dil grubuna göre gönderim. Bu MOD-0018 işi.
- **Gelecek regresyon riski: 🟡** — çok dilli bir kiracıda herkes aynı dili alıyor.

### BL-162 — [BİLDİRİM] Çözülemeyen alıcı sessizce düşüyor (loglanıyor ama kimseye söylenmiyor)
- Canlı ölçüm: `task.notification.recipients_unresolved Count=1` — adaylardan biri AuthService'te
  çözülemedi ve **bildirilmedi**. Log var, ekranda iz yok.
- Bugün doğru davranış (yazma başarısız olmamalı), ama "izleyici ekledim, haber gitmedi" durumunu kimse göremiyor.
- **Öneri:** çözülemeyen alıcı sayısını görev detayında sessiz bir satır olarak göster, ya da yönetici için bir
  rapor. Karar CT'de.
- **Gelecek regresyon riski: 🟢.**

### BL-163 — ✅ KAPANDI (2026-08-14) — BL-159 kararsız test: saat değil KOŞUL bekleniyor
- `setTimeout(30)` yerine `until(() => calls.length > 0)` — koşul gerçekleşir gerçekleşmez dönüyor, 2sn tavanı var.
- Üç ardışık koşuda 208/208. Süre **artırılmadı**; sabit bekleme yalnız bir yerde kaldı ve orada doğru:
  "hiçbir şey olmadı" iddiasının bekleyecek bir koşulu yok, ve hatası yalnız yanlış-YEŞİL üretebilir, yanlış-kırmızı
  değil — yani BL-159'un gürültüsünü yaratamaz. Gerekçe testin içine yazıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-164 — ⚠ BULUNAN KUSUR — "bekleme sona erince temizlenir" iki yorumda yazıyordu, kodda YOKTU
- `TaskItem.WaitingReason` özeti: "Cleared when the task resumes, so a stale reason never outlives the wait."
  `InquireTaskItemHandler`: gerekçe geçmişe kopyalanıyor "because WaitingReason is CLEARED when the task resumes".
- **ÖLÇÜM (2026-08-15): kod tabanında hiçbir yer bu alanı null'a döndürmüyordu.** Mart'ta beklemeye alınıp
  Nisan'da devam eden bir görev Mart'ın cümlesini süresiz taşıyordu.
- Zararsız değil, GÖRÜNMEZdi: `ResolveWaitingContext` alanı yalnız yaşam döngüsü Waiting iken okuyor, o yüzden
  bayat değer saklı kalıyordu — ta ki görev ikinci kez beklemeye alınana kadar; o an eski cümle, yenisi
  yazılmadan önceki pencerede yüzeye çıkıyordu. Ve alana başka hiçbir şey güvenemiyordu.
- **Düzeltildi:** `TaskItem.ClearWaiting()` (gerekçe + kişi birlikte), `TransitionTaskItemHandler` içinde
  Waiting'den çıkan HER geçişte çağrılıyor. Dal başına değil tek yerde, çünkü Waiting'den üç çıkış var ve
  unutulan hep üçüncüsü olur. İki yorum da gerçeğe göre yeniden yazıldı.
- Canlı doğrulandı: devam ettikten sonra `WaitingReason = None`, `WaitingOnUserId = None` (Mongo'dan okundu).
- **Gelecek regresyon riski: 🟢** — mutasyon testi (temizlemeyi kaldır) iki testi kırmızıya çeviriyor.

### BL-165 — ⚠ BULUNAN KUSUR — Kişi bilindiğinde GEREKÇE düşüyordu (iki yüzeyde)
- Eski kod: `item.waitingOn ? tf('WaitingOn', item.waitingOn) : item.waitingReason` — yani birini seçmek,
  okuyucuya neyin beklendiğini söyleyen cümleyi KAYBETTİRİYORDU. Detay notunda ve liste çipinde aynı hata.
- Bugüne kadar görünmüyordu çünkü `waitingOn` her zaman null'dı; bu tur onu doldurunca kusur canlanacaktı.
- **Düzeltildi:** tek bir `waitingSentence(item)` — üç yüzey (detay notu · liste çipi · yaşam döngüsü şeridi)
  aynı yerden alıyor. İkisi birden varsa `WaitingOnWithReason` ile ikisi de gösteriliyor.
- **Gelecek regresyon riski: 🟢.**

### BL-166 — [TEST ALTYAPISI] Fixture `tf` yalnız `{0}`'ı dolduruyordu — iki yuvalı mesajlar sessizce yarım kalıyordu
- `wcn-detail-three-regions` fixture'ındaki `tf` tohumu `` `${key}:{0}` `` idi; iki argümanlı bir mesajın
  İKİNCİ değeri hiçbir zaman görünmüyordu. Yani "iki olgudan birini düşüren cümle" kusuru — bu turun tam da
  test ettiği şey — testte geçerdi.
- `WaitingOnWithReason` yakaladı. Tohum artık argüman sayısı kadar yuva üretiyor.
- **Gelecek regresyon riski: 🟢.**

### BL-167 — [TEST ALTYAPISI] Geçiş sözleşmesi guard'ı "her builder tek satır" kuralını gizlice dayatıyordu
- `task-transition-contract.test.js` her eylemin girdisini TEK SATIR olarak okuyordu; `inquire` ikinci
  parametresini alıp satır kaydırınca guard "builder bir nesne döndürmüyor" diye düştü — kodun değil, biçimin
  hatası. Ayrıca sonraki girdiyi açıklayan `//` yorumu da eşleşmeyi bozuyordu.
- Guard artık girdiyi bir sonraki anahtara kadar okuyor ve önce yorumları soyuyor. Bu oturumda dördüncü kez bir
  guard kendi prozasına takıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-168 — [TEST] `creating a subtask in detail` testi tam süit altında zaman aşımına uğrayabiliyor
- Bir tam süit koşusunda 5000ms vitest zaman aşımı; dosya tek başına 117/117, ikinci tam koşuda da geçti.
  BL-159/BL-163 ile aynı sınıf: yük altında yetmeyen bekleme. **Bu turun değişiklikleriyle ilgisi ölçülmedi
  ama yol farklı** (alt görev paneli, `inquire` diyaloğu değil).
- **Yapılacak:** aynı `until(...)` desenine çevir. Bu turda yapılmadı.
- **Gelecek regresyon riski: 🟡** — yalancı kırmızı.

### BL-169 — ⚠ CANLIDA BULUNAN KUSUR — alan geçmişi "kim" diyemiyordu
- İlk canlı ölçümde satırlar **"Öncelik: High → Low · İsim bulunamadı"** diyordu; yanındaki `created` satırı
  kişiyi adıyla söylüyordu.
- **Sebep:** geçmişi COMMIT anında depo yazıyor ve deponun kullanıcı bağlamı yok — aktör her zaman yazan
  tarafından `Declare` edilmek zorunda. `UpdateTaskItemHandler` hiçbir şey declare etmiyordu, çünkü alan
  günlüğünden ÖNCE zaten hiç kayıt üretmiyordu.
- "Son tarihi kim değiştirdi" bu turun tek sorusu; kişiyi söyleyemeyen bir kayıt sorunun yarısını cevaplıyor.
- **Düzeltildi** (`task.Declare(TaskTransitionKind.Edited, _currentUser.UserId)`) ve yan etkisi kapatıldı:
  boş bir "Kaydet" artık satır yazmıyor (`editedWithNothingToSay`). İkisi de testli.
- **Testle değil, canlı ölçümle bulundu.** **Gelecek regresyon riski: 🟢.**

### BL-170 — [İŞ 3 CEVABI] Mevcut "kayıt öncesi" cümlesi alan değişiklikleri için DOĞRU DEĞİL
- Cümle: *"Bu görevin, kayıt tutulmaya başlanmadan önceki **adımları** kayıtlı değil."* — bir "adım" yaşam
  döngüsü hareketi; alan değişikliği adım değil.
- **Tetikleyicisi de yanlış:** cümle yalnız `created` olayı YOKSA çıkıyor. Geçiş günlüğü varken ama alan
  günlüğü yokken oluşturulmuş bir görevde `created` VAR → cümle çıkmaz → oysa o görevin alan geçmişi de yok.
- **Ve bir alan-geçmişi boşluğu görev başına TESPİT EDİLEMEZ:** "hiç alan değişmedi" ile "kayıt başlamadan önce
  değişti" arasında ayrım yapacak bir işaret yok. Uydurulmuş bir cümle, kanıtlanamayan bir iddia olurdu.
- **Bu turda hiçbir şey eklenmedi.** Seçenekler (karar CT'de): (a) kiracı bazında "alan geçmişi şu tarihte
  başladı" damgası tutup cümleyi ona dayandırmak; (b) mevcut cümlenin metnini "adımları" yerine "geçmişi" diye
  genişletip tetikleyiciyi olduğu gibi bırakmak (eksik kalır ama yanlış olmaz); (c) susmak.
- **Gelecek regresyon riski: 🟢.**

### BL-171 — [ÖLÇÜM] Kısıtlı alanın yazımı zaten reddediliyor; geçmiş satırı elle tohumlandı
- Kısıtlı bir alanın (`ViewPermission` taşıyan) değerini GÖREMEYEN aktör onu YAZAMIYOR da — canlı 400.
  Doğru davranış, ama bu turun test edeceği şey okuma yolu olduğu için geçmiş satırı doğrudan Mongo'ya kondu.
- Okuma ölçümü: değerler (45000/52000), tanım kodu ve etiket **tüm yanıtta hiç geçmiyor**; ekranda satır
  **"bir alan değiştirildi"** olarak duruyor.
- **Açıkça yazılıyor:** ikinci aktör için parola CT'de yok; ölçüm **API katmanında** yapıldı, ekranla değil.
- **Gelecek regresyon riski: 🟢.**

### BL-172 — [KARAR] Geçmişte "aktör" alanı olmayan eski satırlar var
- Bu turdan önce yazılmış iki `Edited` satırı `ActorUserId = null` taşıyor (BL-169 düzeltilmeden önce
  üretildiler) ve ekranda "İsim bulunamadı" diyorlar. **Geriye doldurma YAPILMADI** — kim olduğu kayıtlı değil
  ve üretilemez; uydurmak günlüğün tek işini bozardı.
- Bunlar yalnızca dev veritabanındaki test kayıtları. Üretimde aynı durum oluşamaz (alan günlüğü bu turla
  birlikte, aktör bildirimiyle birlikte geliyor).
- **Gelecek regresyon riski: 🟢.**

### BL-173 — ✅ KAPANDI (2026-08-23) — BL-168 kararsız test `until(...)` desenine çevrildi
- `openCreate` bir makro-görev bekliyordu; panel ise kişi aramasının `await`inden SONRA açılıyor, bu yüzden tam
  süit yükü altında test 5000ms'i boş yere bekliyordu.
- Süre **artırılmadı**: `until(() => panel var mı)` ve `until(() => created.length > 0)`. İki ardışık koşuda
  117/117.
- **Gelecek regresyon riski: 🟢.**

### BL-174 — [KARAR SENİN] `DelegationAllowed` varsayılanı `false` ve canlı veri bunu doğruluyor
- Kural doğru ve açıkça istendi: sunucu, `delegationAllowed=false` olan bir görevin devredilmesini reddediyor
  (`409 TASK_DELEGATION_NOT_ALLOWED`, kimlik kontrolünden ÖNCE). Sorun kuralda değil, **varsayılanda**.
- `TaskItem.DelegationAllowed` başlangıç değeri olmayan `bool` — yani `false`. Create formundaki kutu da
  **işaretsiz** açılıyor. Sonuç: kutuyu kimsenin bilinçli olarak açmadığı her görev "asla devredilemez" oluyor.
- **Ölçüm (2026-08-23, canlı `/api/v1/work-items/mine`): 60 görevin 43'ü (%72) `false` taşıyor.** Bunların
  neredeyse hiçbiri "bu iş devredilemez" demek istemiyordu; hiç sorulmamış bir soruya verilmiş varsayılan cevap.
- Kural bugünkü haliyle yayına girerse bu %72, ekranda **kendinden emin bir gerekçeyle** ("Bu görev
  devredilemez") kilitlenir — yanlış bir cümle değil, ama kimsenin vermediği bir kararı aktarır.
- Mevcut satırlar için kod düzeltmesi yok: veritabanında **literal `false`** yazıyorlar. Seçenekler:
  **(a)** olduğu gibi yayınla ve kabul et; **(b)** alanı nullable yap — `null` = "hiç seçilmedi" = izinli, yalnız
  GELECEK görevler için; **(c)** formun varsayılanını işaretli yap. (b) ve (c) eski satırları düzeltmez;
  onlar için ayrı bir veri taşıma gerekir. **Bu turda taşıma YAPILMADI.**
- **Gelecek regresyon riski: 🟡** — (b) seçilirse sözleşmede `delegationAllowed` bool'dan nullable'a döner ve
  ön yüzün üç durumu (izinli / yasak / seçilmemiş) ayırt etmesi gerekir.

### BL-175 — [ÖLÇÜM] "Haber verilemedi" bilgisi tel üzerinden TÜRETİLEMİYOR (BL-162'nin cevabı)
- Önce ölçüldü, sonra yazıldı: iki ayrı çözümleyici **iki ayrı soruya** cevap veriyor.
  `IUserDisplayNameResolver` → "bu kişinin ADI var mı" (projeksiyon; bulunamazsa `displayName: null`).
  `ITaskNotificationRecipientResolver` → "bu kişinin E-POSTASI var mı" (bildirim; bulunamazsa alıcı listesinden
  sessizce düşer).
- Bunlar birbirinin yerine geçmiyor: **adı olan ama e-postası olmayan** bir izleyici ekranda tamamen normal bir
  satır gibi görünürken bildirim sessizce başarısız oluyor; **adı olmayan ama e-postası olan** biri ise yanlış
  yere işaretlenirdi.
- Brief'e uyularak **yeni saklama alanı AÇILMADI**. Öneri: bilgi zaten var olduğu yerde yüzeye çıkarılsın —
  gönderim kaydı ve `task.notification.recipients_unresolved` günlüğü üzerinden bir **operasyon raporu** olarak;
  görev kartında değil. Kartta olması, okuyanın düzeltemeyeceği bir arızayı ona yüklemek olurdu.
- **Karar senin:** rapor yüzeyi açılsın mı, yoksa bu bilgi ops tarafında mı kalsın.
- **Gelecek regresyon riski: 🟢.**

### BL-176 — [YAPILMADI] `TaskWatcherRole` yalnızca iki değer taşıyor
- Enum: `Watcher`, `Consultant`. "Bilgilendirilen" (RACI'nin *Informed*'ı) **yok**. Tasarım kararı "ad + sessiz
  rol soneki" dediği için üçüncü bir rol uydurulmadı; yanlışlıkla eklenen `WatcherRoleInformed` anahtarı yedi
  dilden de geri alındı.
- Üçüncü rol istenirse enum, create formu ve yedi dil birlikte açılmalı — ekran tarafı zaten hazır.
- **Gelecek regresyon riski: 🟢.**

### BL-177 — [YAPILMADI] `.wcn-notes-composer` ayırıcısı eşit değil (yan panel, detay kartı değil)
- Süpürmede bulundu: `margin-block-start: 1rem` üstte, `padding-block-start: .75rem` altta → **16 / 12**, eşit değil.
- Sekiz detay kartından biri değil; hızlı notlar YAN PANELİNDE yaşıyor. Dahası panel bugün **arayüzden
  açılamıyor**: `state.notesOpen`'ı çeviren bir düğme render edilmiyor ("Hızlı not" başka bir akış).
- Bu yüzden **CSS metninden ölçüldü, ekrandan değil** — ve bu turda değiştirilmedi: ölçemediğim bir yüzeyde
  düzeltme yapmak, düzelttiğimi ekranda gösteremeyeceğim bir değişiklik demek.
- **Gelecek regresyon riski: 🟢** (kart ailelerinden bağımsız).

### BL-178 — [ÖLÇÜLEMEDİ] Bölünmüş görünüm yüzeyi arayüzden açılamıyor
- `[data-wcn-view]` yalnızca `list` ve `table` üretiyor; `.wcn-split-detail` hiçbir tıklamayla açılmıyor.
- Sonuç: o yüzeydeki ayırıcılar (`.wcn-split-detail .wcn-detail-tabs`, `.wcn-detail-command .wcn-personal`
  kenar boşluğu) canlı ölçülemedi. Kişisel kartın bu turdaki yapı değişikliği o yüzeyi de etkiliyor olabilir.
- **Gelecek regresyon riski: 🟡** — bölünmüş görünüm geri geldiğinde kişisel blok orada yeniden ölçülmeli.

### BL-179 — [ÖLÇÜM NOTU] Üst üste bölüm ayırıcısı canlı hiçbir görevde çizilmiyor
- `renderBusinessContext` N bölümü tek karta yığıyor, ama canlı verideki **hiçbir görev iki bölüm taşımıyor**
  (60 görevin 2'sinde iş bağlamı var, ikisi de tek bölüm). Fixture'larda da yok.
- Yine de düzeltildi (`.wcn-bizctx-card`): kart dolgu ödemiyor, her bölüm kendi 1rem'ini ödüyor, çizgi kenardan
  kenara. Ölçüm için tarayıcıda **bölüm klonlandı** — stiller ürünün, DOM elle çoğaltıldı; açıkça yazılıyor.
- Genel `.wcn-detail-card > section + section` kuralı **kaldırıldı**: dolgunun içinde çizgi çizen bir yedek
  kural, bu turda üç kez düzeltilen kusurun dördüncü kez doğacağı yerdi.
- **Gelecek regresyon riski: 🟢** — yeni bir yığılma eklenirse BL-180'siz kalmaz: gardiyan test onu yakalar.

### BL-181 — ✅ KAPANDI (2026-08-24) — Erteleme artık gerçekten erteliyor
- Brifing, ertele diyaloğunun açıklamasının şunu demesini istedi: *"Bu görev, seçtiğin tarihe kadar gelen
  kutunda görünmez."* Aynı brifing "uydurma, ÖLÇ ve doğrula" dedi. **Ölçüldü ve doğru değil.**
- Cümlenin diğer üç iddiası doğrulandı ve sunucuda güvence altında (`SNOOZE_MUST_NOT_CREATE_WAITING`):
  yaşam döngüsü / normalleştirilmiş durum / bekleme bağlamı değişmiyor (`SetTaskSnoozeHandler` yalnız okuyucunun
  kendi katmanını yazıyor), son tarih bambaşka bir alan, talep eden bu katmana hiç ulaşmayan bir projeksiyon
  okuyor.
- **Dördüncü iddia yok:** `snoozedUntil` üzerinde süzen tek bir yer bile yok — ne sağlayıcıda, ne
  `activeItems()`'ta. Ertelenen görev listede durmaya devam ediyor, üstüne bir çip ve bir "Bu öğeyi ertelediniz"
  şeridi ekleniyor. Yani erteleme bugün **bir not**, bir gizleme değil.
- Bu yüzden diyaloğun cümlesi o iddiayı **yazmıyor**; ürünün yapmadığı bir şeyi anlatan bir diyalog, hiçbir şey
  söylemeyenden daha kötüdür. Bir gardiyan test (`wcn-snooze-dialog.test.js`) iddianın süzme gelmeden geri
  sızmasını engelliyor.
- **Karar senin:** (a) süzmeyi ekle — o zaman cümle tam hâline kavuşur; (b) ertelemeyi açıkça "kişisel bir not"
  olarak bırak ve adını buna göre gözden geçir.
- **Gelecek regresyon riski: 🟡** — süzme eklenirse "İşlerim boş" gibi sayaçlar ve boş durumlar da değişir.

### BL-182 — [ÖLÇÜM] Takvim bugünü seçtiriyor, doğrulayıcı bugünü reddediyor
- `minDate: data.todayIso` bugünü **seçilebilir** bırakıyor; doğrulayıcı ise `value <= todayIso` diyerek onu
  **reddediyor**. Yani bugüne tıklayan biri sessizce çalışmayan bir tarih seçip "Gelecek bir tarih seçin"
  uyarısını yiyor.
- Doğrulayıcıya bu turda dokunulmadı (brifing: "doğrulayıcı olduğu gibi kalsın"). Sunucu ertelemeyi günün
  **23:59:59**'una yazdığı için bugün aslında anlamlı bir seçim olurdu ("bu akşama kadar").
- **Karar senin:** ya `minDate` yarına çekilsin, ya doğrulayıcı bugünü kabul etsin. İkisi bugün aynı şeyi
  söylemiyor.
- **Gelecek regresyon riski: 🟢.**

### BL-183 — [YAPILMADI] "İptal" kelimesi WorkCenter'ın diğer diyaloglarında da aksiyonla çakışıyor
- Ertele diyaloğunun vazgeçme düğmesi artık `DialogDismiss` ("Vazgeç") — çünkü sarmalayıcının varsayılanı ortak
  `Cancel` dizesi ve Türkçesi "İptal", bu sayfada ise **"Görevi iptal et"** diye bir AKSİYON var.
- Aynı çakışma WorkCenterNext'in diğer diyaloglarında da duruyor: `t('ReasonCancel')` = "İptal", modül seamının
  varsayılanı ve **dört ayrı ham diyalogda** doğrudan geçiliyor.
- Bu turda yalnız ertele değiştirildi (sahip sırayla gidiyor). Modül geneline yayılması ayrı bir karar.
- **Gelecek regresyon riski: 🟢.**

### BL-184 — [KARARSIZ TEST] Tam süit yükü altında yedi WorkCenter testi zaman aşımına düşüyor
- Tam koşuda (`npx vitest run`, 92 dosya paralel) şu yedisi kırmızı: alt görev oluşturma ×3, "tümünü göster"
  kapağı, kontrol listesi seviyesi, havuz kuyruğu kovası, atanan seçici. Süreleri 7–11 saniye.
- **Yedisi de tek başına koşturulduğunda yeşil** (aynı komut, tek dosya: 356/356 · 71/71). Yani ürün kusuru
  değil, testlerin sabit bekleme kullanması: makine yüklüyken beklenen olay pencerenin dışına taşıyor.
- Çözüm biliniyor ve bu depoda üç kez uygulandı (BL-159 / BL-163 / BL-168): sabit `setTimeout` yerine
  `until(koşul, {timeout, step})`. Bu turda YAPILMADI — tur tek bir kusura ayrılmıştı.
- **Gelecek regresyon riski: 🟡** — kararsız testler gerçek kırmızıları gizler; bu turda bir gerçek kırmızıyı
  (ham diyalog sayacı 9→8) ayırt etmek fazladan üç koşu aldı.

### BL-185 — [KARAR SENİN] Ortak modalin girdisine alan ikonu takılamıyor (İş 2b'nin ölçümü)
- Soru şuydu: `.diten-field` + `.diten-field-icon` deseni (create formunda 17 kez canlı) SweetAlert'in kendi
  girdisine uygulanabiliyor mu? **Tarayıcıda denendi, cevap: yapısal olarak evet, görsel olarak hayır.**
- Ölçüm: `didOpen` içinde girdiyi bir `.diten-field` ile sarmak **çalışıyor** — SweetAlert girdiyi hâlâ buluyor
  (`.swal2-popup .swal2-input` sorgusu geçerli kalıyor). Ama ikon **girdinin dışına** düşüyor: `-19px`.
- Nedeni ölçüldü: `.swal2-input` üzerinde `margin: 17px 34px 3px` var. `.diten-field-icon` mutlak konumunu
  SARMALAYICIYA göre alıyor, sarmalayıcının kutusu ise girdinin marjlarını da içeriyor → ikon 34px dışarıda.
  Girdiye `form-control` eklemek iç dolguyu (39px) getiriyor ama marj sorununu çözmüyor.
- Düzeltmek için gereken şey **`.swal2-input`'un marjını sıfırlayan ya da ikonu 34px kaydıran bir CSS kuralı** —
  yani ortak modale ikon altyapısı eklemek. Brifing bunu yasakladı, **eklenmedi**.
- **Karar senin:** (a) ortak modale bir "ikonlu girdi" desteği ekleyelim (tek CSS bloğu, ürün geneli);
  (b) ikonsuz kalsın — placeholder zaten biçimi söylüyor ve takvim tıklamayla açılıyor.
- **Gelecek regresyon riski: 🟢** (bugün hiçbir şey değişmedi).

### BL-186 — [KARAR SENİN] Sarmalayıcının ikon sözlüğünde "ne zaman?" yok (İş 3'ün ölçümü)
- `options.type` beş değer tanıyor ve her biri ikonla BİRLİKTE onay düğmesinin rengini de belirliyor:
  | type | ikon | düğme |
  |---|---|---|
  | `info` (varsayılan) | `bx-help-circle` (primary) | `btn-primary` |
  | `delete` | `bx-trash` (danger) | `btn-danger` |
  | `danger` / `error` | `bx-error-circle` (danger) | `btn-danger` |
  | `success` | `bx-check-circle` (success) | `btn-success` |
  | `warning` | `bx-error` (warning) | `btn-warning` |
- Ertele ne yıkıcı, ne hata, ne başarı, ne uyarı. Geriye `info` kalıyor — ve o da soru işareti çiziyor.
  **Hiçbiri uygun değil**, o yüzden bugünkü hâli (soru işareti) korundu.
- ⚠ Ek ölçüm: özel ikon parametresi `inputType` ile **aynı sınıftan değil**. İkon ve düğme rengi bu dosyada tek
  bir `if` zincirinde birlikte kararlaştırılıyor; ikonu dışarıdan vermek, düğme rengini de dışarıdan verilebilir
  kılmadan tutarsızlık üretir. İki parametre demek.
- **Karar senin.** CT'nin prototipindeki ay (moon) bugün desteklenmiyor.
- **Gelecek regresyon riski: 🟢.**

### BL-187 — [ÖLÇÜM] Create formunun tarih placeholder'ı yerelleştirilebilir değil
- Ertele diyaloğunun placeholder'ı yeni bir biçim icat etmedi: **ürünün kendi maskesi** kullanıldı — create
  formundaki iki tarih alanı (`Views/Tasks/_Form.cshtml:173,189`) `YYYY-MM-DD` yazıyor, yedi dilde de aynı.
- Ama oradaki değer **doğrudan .cshtml'e gömülü**, bir kaynak anahtarı değil: Türkçe bir okuyucu için "AA/GG"
  demek isteseydik, o iki alan için kod değişikliği gerekirdi. Ertele'nin anahtarı 7 dilde AYRI duruyor
  (bugün hepsi aynı değeri taşıyor), yani orada karar koda dokunmadan değişebilir.
- **Karar senin:** maskeler yerelleşsin mi (o zaman create formu da anahtara taşınmalı), yoksa ürün genelinde
  nötr `YYYY-MM-DD` mi kalsın.
- **Gelecek regresyon riski: 🟢.**

### BL-188 — ✅ KAPANDI (2026-08-23) — Bayat okuma, yeni okumanın üstüne yazıyordu
- Sıra: erteleme kaldırıldı (sunucu doğrulandı: `personal.snoozedUntil` yok) → ertele diyaloğu açıldı → geçmiş
  tarih reddedildi → "Vazgeç". Ekranda **kaldırılmış erteleme satırı geri geldi**; sayfa yenilenince gitti.
- Yani vazgeçme yolu, bir önceki anlık görüntüden yeniden çiziyor olabilir. Sunucu durumu her zaman doğruydu;
  yanlış olan tek şey ekrandı ve yalnız yenilemeye kadar sürdü.
- Bu turun konusu değildi, **düzeltilmedi**; tek bir gözlem olarak kaydediliyor, kovalanacaksa kendi turunu
  hak ediyor.
- **Gelecek regresyon riski: 🟡** — "kaydettim ama geri geldi" tipi şikâyetlerin klasik kaynağı.

### BL-189 — [ÖLÇÜM] Harness'ta iki modül örneği tek DOM'u paylaşıyor
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24 (Tur C).** `window.__wcnTeardown` canlı ölçüldü (`typeof` = function): boot önceki örneğin dinleyicilerini söküyor. Üretim davranışı, test uyarlaması değil.
- `wcn-boot` her boot'ta `app.js`'i yeniden yüklüyor; global modül nesneleri siliniyor ama **belge üzerindeki
  tıklama dinleyicileri** kalıyor. Sonuç: bir tıklama iki kez işleniyor, iki ağ okuması üretiyor.
- Bugün testleri yanıltmıyor (iddiaların hepsi DOM üzerinde), ama **yarış/sıra** iddialarını imkânsız kılıyor —
  BL-188'in davranış testi bu yüzden tarayıcıya taşındı.
- Çözüm yönü: app.js'in boot'ta kendi dinleyicilerini sökebilmesi ya da harness'ın her testi kendi
  `document`'ında koşturması. **Bu turda yapılmadı.**
- **Gelecek regresyon riski: 🟡** — zamanlamaya dayalı her yeni test aynı duvara toslar.

### BL-190 — ✅ KAPANDI (2026-08-23) — Satır içi stil bloğu backbone-custom.css'e taşındı
- İkon bastırıldığında SweetAlert `.swal2-icon`'a zaten `style="display:none"` yazıyor. Buna rağmen 80px'lik
  boş kutu ekranda kalıyordu: `_GlobalConfirmation.cshtml`'in kendi satır-içi `<style>` bloğu
  `.swal2-icon { display: flex !important; … }` diyor ve kütüphanenin satır-içi `display:none`'ını yeniyor.
- Bu turda `backbone-custom.css`'e `:empty` koşullu bir kural eklenerek çözüldü (ikonu OLAN diyaloglar
  etkilenmedi — canlı doğrulandı: Rol İzinleri'nde kırmızı çöp kutusu 80px, yerinde).
- ⚠ Asıl mesele duruyor: **ortak bileşenin satır-içi `<style>` bloğu** FG-003'ün "CSS sınıflarla,
  backbone-custom.css'te" kuralının dışında ve kütüphanenin kendi davranışlarını `!important` ile eziyor.
  Taşınması ayrı bir tur; bu turda dokunulmadı.
- **Gelecek regresyon riski: 🟡.**

### BL-191 — ✅ KAPANDI (2026-08-23) — Tarih kutusu ürünün kontrolü oldu; 21→19px KABUL
- Sahibin bildirdiği "ikon yanlış" kusuru **glif değil, kutu** çıktı. Ölçüm, create formunun tarih alanıyla
  yan yana: ürün **38px** yükseklik / 15px / `--bs-border-radius`; kütüphanenin kutusu **44.6px** / 17px / 8px.
- Fark kozmetik değildi: `.diten-field-icon` kendini `calc(38px / 2)`'ye sabitliyor, çünkü bu üründe bir kontrol
  38px'tir. 45px'lik kutuda glif ortadan **20px yukarıda** duruyordu — alanın *içinde* değil, üst kenarında.
- Düzeltme, temanın kendi `.form-control` değerlerinden kopyalandı (core.css:2571 ve :8645), hiçbiri seçilmedi.
  Ayrıca sarmalayıcı alanı **kucaklıyor**: kütüphanenin dikey marjı (15px/3px) girdiden sarmalayıcıya taşındı,
  yoksa aradaki boşluk glifle kutu arasına düşüyordu.
- Ölçülen sonuç: 38px · 15px · 6px yarıçap · glif merkezde (0) ve x=15 — create formuyla **birebir**.
- ⚠ Bir yan etki bilerek bırakıldı: etiket→alan boşluğu 21px'ten **19px**'e indi (kutu 7px kısaldı). Alan→düğme
  zaten 19px'ti; ikisi artık eşit. Yeni sayı seçilmedi.
- **Gelecek regresyon riski: 🟢.**

### BL-192 — ✅ KAPANDI (2026-08-23, CT kararı) — "Erişilemez ama doğru"
- Sahibin isteği üzerine ret mesajı artık ürünün **alert** dilinde: `--bs-danger-bg-subtle` / `-text-emphasis` /
  `-border-subtle`, tema yarıçapı, 13px. Kütüphanenin #f0f0f0, 16px, köşesiz şeridi gitti. İki temada da
  doğrulandı ve başka bir ekranda (Rol İzinleri "Value is required") aynı dili konuşuyor.
- ⚠ Ama **arayüzden tetiklenemiyor**: takvim geçmiş günleri zaten devre dışı bırakıyor ve flatpickr
  `allowInput` olmadan bağlandığı için elle yazılan tarih geri alınıyor. Ölçüm bu yüzden değeri programatik
  atayarak yapıldı — açıkça yazılıyor.
- Yani istemci kontrolü bugün **ikinci savunma hattı**; birinci hat takvimin kendisi, üçüncüsü sunucunun 400'ü.
- **Karar senin:** `allowInput: true` verilip elle yazma açılsın mı (o zaman ret mesajı gerçekten görünür bir
  yüzey olur), yoksa yazma kapalı mı kalsın?
- **Gelecek regresyon riski: 🟢.**

### BL-193 — ✅ KAPANDI (2026-08-23) — Sarmalayıcı, kütüphanenin girdisini kaybettiriyordu
- **Bir önceki tur bir kusur getirdi ve commit alınmadığı için üretime gitmedi.** Takvim ikonunu koymak için
  girdiyi `.diten-field` ile sarmıştım. SweetAlert girdisini popup'ın **doğrudan çocukları** arasındaki sabit
  slot listesinden bulur; sarmalayıcı kutuyu **torun** yaptı.
- **Tek sarmalayıcı, üç yara** (üçü de canlı ölçüldü): `Swal.getInput()` → `null` · doğrulayıcıya `''` geliyor,
  bu yüzden **ileri** bir tarih "geçmiş tarih seçilemez" ile reddediliyordu · otomatik odak ve Enter ölü.
  Kullanıcının gördüğü yalnız birincisiydi.
- **Düzeltme (a şıkkı):** sarmalayıcı gitti, glif **kutunun arka planına** boyandı — SVG ürünün kendi
  `.bx-calendar` varlığından (`vendor/fonts/iconify-icons.css:3690`) kopyalandı. Ek eleman yok, kütüphaneyle
  dövüş yok. Kutunun tamamı zaten takvimi açıyor.
- **(b) seçilmedi:** popup'ın doğrudan çocuğu olan mutlak konumlu ikon, dikey yerini üstündeki içerikten alır;
  başlık/açıklama uzunluğu değişince kayar. **(c) reddedildi:** yaraların yalnız birini kapatıyordu.
- **Bedeli açıkça yazılıyor:** `background-image` `currentColor` miras alamıyor, bu yüzden glifin grisi iki
  temada iki ayrı kuralda yazılı (`#a7acb2` / `#7e7f96`). İkisi de `--bs-secondary-color`'ın ölçülmüş değeri;
  token değişirse bu iki URI elle güncellenmeli. Testte ikisi de kilitli.
- **Geçen turun ölçümleri korundu ve yeniden ölçüldü:** kutu 38px · yazı 15px · yarıçap 6px · metin 39px'ten ·
  glif 16px, x=15, dikey merkezde · placeholder duruyor — create formuyla birebir.
- **Gelecek regresyon riski: 🟢** — "girdi popup'ın doğrudan çocuğudur" iki testte kilitli.

### BL-194 — [ÖLÇÜM] Textarea'lı diyaloglarda Enter onaylamaz (kütüphane davranışı)
- Geriye uyum ölçümünde çıktı: tek satırlık girdide Enter **onaylıyor**; `textarea` kullanan diyaloglarda
  **onaylamıyor** — çünkü orada Enter satır başıdır. SweetAlert'in kendi davranışı, bu turda değişmedi.
- `showInput` kullanan altı çağrının beşi textarea; yani onlarda Enter zaten hiç onaylamıyordu. Kayıt, ileride
  "Enter çalışmıyor" diye bildirilirse kusur mu davranış mı sorusunu bir kez daha ölçmemek için.
- **Gelecek regresyon riski: 🟢.**

### BL-195 — [ÖLÇÜM] Sol menü bir süre sonra kısalıyor, yenileyince geri geliyor
- Sahip 2026-08-24'te bildirdi ve ekran görüntüsü verdi: kenar çubuğunda yalnız iki giriş kalmış
  ("Mutabakat" ve "İnsan Sermayesi Yönetimi ▸ Çalışan Taslakları"); sayfa yenilenince menü tam geldi.
- İlk ölçüm (CT, aynı gün): istemci tarafında menüyü tazeleyen **hiçbir zamanlayıcı yok** — tüm frontend JS
  içinde `setInterval` tek yerde geçiyor ve o da WorkCenter'ın saniye sayacı. Yani menü JS ile küçülmüyor;
  **o sayfa yüklenirken sunucu zaten kısa menüyü üretmiş.**
- Şüphe (ÖLÇÜLMEDİ, iddia değil): jetonun süresi dolmak üzereyken yapılan bir menü çekimi kısmi/boş dönüyor
  ve kabuk ne geldiyse onu çiziyor. Bu projede aynı sınıf bir korumanın **var olduğu** bir yer biliyoruz —
  hak/modül eşitlemesinde "boş çekim = dokunma, asla geri alma" kuralı — ama **menü render'ında yok.**
- Ölçülecekler: menü hangi çağrıdan besleniyor · o çağrı 401/timeout dönerse ne çiziliyor · kısmi sonucu
  eleyen bir koruma var mı · jeton yenileme ile zamanlaması çakışıyor mu.
- ⚠ Bu bir WorkCenter kusuru değil, kiracı kabuğunun (tenant shell) kusuru. Kendi turunda ölçülecek.
- **Gelecek regresyon riski: 🟡** — menü her sayfada çiziliyor; sessizce eksik çizmesi kullanıcıya
  "yetkim gitti" gibi görünür ve yanlış hata bildirimleri üretir.

### BL-196 — ✅ KAPANDI (2026-08-24) — Ertele diyaloğunun sorusu alanın kendi metni oldu
- **Sahip kararı:** "Hangi tarihe kadar" artık üstteki ayrı etiket satırı değil, **tarih alanının placeholder'ı**.
  Diyalog tek bir soru soruyor ve onu bir kez soruyor; boş bir kutunun üstünde duran etiket, kutunun kendisinin
  söyleyebileceği şeyi bir satır harcayarak söylüyordu.
- **İkisi birden yapılmadı:** aynı cümle hem etikette hem placeholder'da olsaydı alt alta iki kez yazardı.
  Kelimeler değişmedi, yedi dilde aynı `SnoozeUntilLabel` anahtarından geliyor — yalnız **nerede çizildiği**
  değişti.
- **Bedeli açıkça:** biçim ipucu (`YYYY-MM-DD`) artık görünmüyor. Alanı KULLANMAK için gerekmiyor (kutuyu takvim
  dolduruyor, elle yazılmıyor), ama şekli seçmeden önce bilmek isteyen okuyucu artık göremiyor.
  `SnoozeDatePlaceholder` yedi dilde **duruyor** — serbest yazım açılırsa (BL-192) geri gelecek metin bu; yedi
  dili yeniden çevirmek yerine kayıtlı bırakıldı, testle de kilitli.
- **Canlı doğrulandı (gerçek tıklama, tam döngü):** aç → takvimden 28 Ağustos → "Ertele"ye BAS → diyalog kapandı
  → çip ve şerit göründü → sayfa yenile → "Ertelendi 2026-08-28 Kaldır" duruyor. Dört kombinasyonda
  (1440/900 × aydınlık/karanlık) kutu 38px · yazı 15px · yarıçap 6px · glif 15px/merkez · metin 39px'ten ·
  girdi popup'ın doğrudan çocuğu · odak girdide · `getInput()` dolu.
- **Gelecek regresyon riski: 🟢.**

### BL-197 — [ÖLÇÜM] Testte "bugün" UTC'den alınırsa günün üç saati kırmızı olur
- Bu turda yakalandı: modülün `todayIso`'su **2026-08-24** derken `new Date().toISOString()` **2026-08-23**
  veriyordu — okuyucunun saati UTC'nin önünde (UTC+3) ve o üç saat boyunca UTC'den türetilen tarih bu
  doğrulayıcıya göre **dün**.
- Ürün tutarlı: takvimin `minDate`'i de doğrulayıcı da aynı `data.todayIso`'yu okuyor. Yanlış olan **testti**;
  düzeltildi ve gerekçesi testin içine yazıldı.
- Aynı tuzak zamana dayanan her yeni test için geçerli: "bugün"ü üründen sor, saatten değil.
- **Gelecek regresyon riski: 🟢.**

### BL-198 — [KARAR SENİN] "Ertelenmiş" çipi Havuz ve Geçmiş'te de görünüyor (ama orada gizlemiyor)
- Kapsam kararı gereği gizleme yalnız `inbox`/`islerim`'de. Çip ise sayısı sıfırdan büyükse **her sekmede**
  çiziliyor ve orada **normal daraltan** bir sinyal gibi davranıyor.
- Canlı görüldü: Geçmiş'te "Ertelenmiş 1" çıktı — ertelenip sonra tamamlanmış işi bulmaya yarıyor, hiçbir şeyi
  gizlemiyor. Zararsız, hatta faydalı; ama aynı çip iki sekmede iki farklı şey yapıyor.
- **Karar senin:** (a) böyle kalsın (Geçmiş'te "parkettiğim ve sonra bitirdiklerim" araması); (b) çip yalnız
  `SNOOZE_TABS`'ta çizilsin.
- **Gelecek regresyon riski: 🟢.**

### BL-199 — [DÜZELTİLDİ] Gardiyan testte sayı vardı, kural değil
- `wcn-snooze-dialog.test.js` `isSnoozed(item)` çağrılarını **dörde** sabitlemişti. BL-181 üç meşru çağıran
  ekleyince doğru bir değişiklik kırmızıya döndü — orchestrator demir kural #10'un "kayıtta sayı yerine ölçüm"
  uyarısının test hâli.
- Sayı, kuralın kendisiyle değiştirildi: "bu soruyu tek bir yüklem cevaplar" → karşılaştırmanın ikinci bir
  kopyası olmadığı iddia ediliyor, çağrı sayısı değil.
- **Gelecek regresyon riski: 🟢.**

### BL-200 — ✅ KAPANDI (2026-08-24) — Havada duran üç metin ürünün kendi kutu diline girdi
Sahip üçünü aynı oturumda gösterdi; üçü tek hastalıktı ve tek kararla kapandı: **bir şeye ait olan cümle,
o şey için ürünün zaten sahip olduğu kutunun içine girer.** Hiçbiri için yeni tasarım yapılmadı.

**A1 — Yasak cümlesi.** Ölçüldü: alt görev engeli `alert alert-warning` (`wcn-subtask-gate`), aynı türden
cümleyi taşıyan rail gerekçesi ise **zeminsiz, kenarlıksız, dolgusuz** — yalnız `color: var(--bs-warning)`.
Aynı yasak, iki muamele. Rail gerekçesi aynı alert'e geçti; `.wcn-act-reason` artık yalnız `.wcn-subtask-gate`'in
yaptığını yapıyor (dolgu `.625rem .875rem`, 13px), renk/kenarlık/yarıçap temanın.
`.wcn-act-reason` kullanıcısı **bir taneydi** (rail). Ama **kardeşi** `.wcn-actionbar-reason` (dar ekran şeridi)
birebir aynı çıplak muameleyi taşıyordu — bu oturumda üç kez tekrarlanan "birini düzeltip kardeşini bırakma"
hatasına düşmemek için o da aynı anda düzeltildi. **Canlı yan yana kanıt:** rail gerekçesi ile alt görev engeli
tek karede, altı CSS özelliğinde birebir aynı (zemin · kenarlık · yarıçap · dolgu · punto · renk).
Ray dar: kutu 1440 ve 900'de raya **sığıyor**, metin sarıyor ama **düğmeleri itmiyor**.

**A2 — Onayın nesnesi.** Ölçüm: kaydın adını söylemenin **iki mekanizması** vardı — `entityName` rozeti
(**altı dosyada on çağrı**) ve cümlenin içine tırnakla gömülü başlık (**yalnız WorkCenterNext**). İkisi aynı
anda hiç kullanılmıyordu, ama iki mekanizma tek iş demekti. **Rozet seçildi**: zaten var, ürünün çoğunluğu onu
konuşuyor ve zaten istenen çerçeveli kutu (yüzey · yarıçap · dolgu temadan). Cümle karşılığında tırnaklı
başlığını bıraktı: `ConfirmBody` artık `{0}` taşımıyor, `ConfirmBodyOnBehalf` yalnız vekâlet edeni taşıyor —
**yedi dilde** güncellendi. Açıklama satırı (gri nesir) düz kaldı; kutuya giren, eylemin nesnesi.
⚠ Ortak bileşene **beşinci** değişiklik; geriye uyum yine ölçüldü (15 çağrı / 12 dosya, üç ekran canlı).

**A5 — Boş alt görev kartı.** Ölçüldü: alt görev yokken kart **başlığıyla birlikte kayboluyor**, yerine tek
satırlık `wcn-empty-line` geliyordu. **Ürünün kendi boş-durum dili bulundu ve kullanıldı**: yan kart (Kontrol
Listesi) `cardHead`'ini koruyor, ekleme satırını yerinde bırakıyor ve "henüz yok" cümlesini
`.wcn-block-hint` olarak yazıyor — o ipucu zaten **bu kartın** ekleme satırının altında da var
(`subtaskInheritHint`). Yani yeni bir şey çizilmedi, iki mevcut satır kendi sırasına kondu.
**Alert yok** (sahip açıkça istedi). Cümle ekleme satırının **altında**, yani silinen satırın olacağı yerde.
İlerleme çubuğu ve "N tamam" okuması boşken **çizilmiyor**: 0/0 bir ölçüm değil.
**Yükseklik ölçüldü:** boş **163px** → bir alt görev eklendiğinde **255px** (aynı kart, gerçek yazımla).

**Canlı doğrulama:** devredilemez görevde gerekçe kutusu · "Görevi iptal et" diyaloğunda çerçeveli rozet
("Yeni maliyet merkezi açılış talebi") · alt görevi olmayan görevde başlık + ekleme satırı + altında cümle.
**Mutasyon (3/3 kırmızı):** çıplak `<p>` · rozetsiz gövde · başlıksız boş kart.
**İki genişlik × iki tema:** 1440/900 × aydınlık/karanlık — dördünde de aynı.
- ⚠ **YAZILI BİR KARAR TERS ÇEVRİLDİ, açıkça:** eski tasarımı çivileyen iki test vardı ("tek satır, üstünde
  ekleme kutusu"). Sahibin kararıyla ikisi de yeniden yazıldı; testlerin içine hem eski şeklin ne olduğu hem de
  neden bırakıldığı yazıldı, sessizce silinmedi.
- **Gelecek regresyon riski: 🟢.**

### BL-201 — ✅ KAPANDI (2026-08-24, CT kararı) — Silme EKLENMEYECEK, iptal doğru mekanizma
- Sahip "veri silindiğinde de aynı yere düşsün" dedi. Boş durum `items.length === 0` olduğu anda çiziliyor,
  yani sebebi ne olursa olsun aynı — ama bugün **hiçbir yol** bir alt görevi listeden kaldırmıyor: satır menüsü
  yalnız **"Alt görevi iptal et"** sunuyor, iptal edilen satır listede kalıyor (aşağı sıralanıyor) ve API'de de
  silme ucu yok.
- Yani "sildim ve kart boşaldı" hâli bugün **yalnız hiç eklenmemiş** görevlerde oluşuyor. Boş kartın kendisi
  doğru; eksik olan silme eylemi.
- **Karar senin:** alt görev silme gerçekten gerekiyor mu, yoksa iptal yeterli mi?
- **Gelecek regresyon riski: 🟢.**

### BL-202 — [ÖLÇÜM] "Görevi iptal et" diyaloğunun vazgeçme düğmesi de "İptal" diyor
- BL-183'te modül geneli için not edilmişti; bu turda **tek karede** görüldü: başlık "Görevi iptal et",
  vazgeçme düğmesi "İptal", onay düğmesi "Evet, uygula". Yani aynı diyalogda "iptal" iki farklı şey demek.
- Ertele diyaloğu bunu `DialogDismiss` ("Vazgeç") ile çözmüştü; aksiyon onayları hâlâ `t('ReasonCancel')`
  varsayılanını kullanıyor.
- **Karar senin:** `DialogDismiss` modül geneline yayılsın mı?
- **Gelecek regresyon riski: 🟢.**

### BL-203 — [ÖLÇÜLDÜ, KAPANDI] Menü ucundaki N+1 gerçek ama bugün yavaş DEĞİL
- KOD BULGUSU (geçerli), `GetTenantNavigationMenuQueryHandler.cs`:
  - satır 55-57: `foreach (var module in modules)` içinde `await _accessService.HasAccessAsync(...)`
  - satır 94-96: `foreach (var module in entitled)` içinde `await _pageRepository.GetByModuleAsync(...)`
  N modül için 2N gidiş-dönüş. Klasik N+1. Bu kısım doğru ve duruyor.
- ⚠ CT İKİ KEZ YANILDI, ikisi de burada yazılı dursun:
  1. İlk kayıtta `27.2s · 18.1s · 13.9s · 10.7s · 5.3s` yazdım. O ölçüm 8 çekirdekte
     **yük 58-100** iken, başka bir worktree'nin test süiti çalışırken alınmıştı.
  2. Sonra "yavaşlığın sebebi N+1" dedim ve BL-195'i (kaybolan sol menü) buna bağladım.
- TEMİZ ÖLÇÜM (2026-08-24, yük 1.4, aynı kiracı, aynı oturum, 5 çağrı):
  **0.01s · 0.04s · 0.02s · 0.08s · 0.03s — ortanca 30 ms.**
  Yani uç HIZLI. 27 saniye makinenin doymuşluğuydu, sorgunun değil.
- SONUÇ: N+1 bir **ölçekleme riski**, bir kusur değil. Modül sayısı büyürse doğrusal
  kötüleşir; bugün ölçülebilir bir etkisi yok. Düzeltmek istenirse iki döngü toplu
  okumaya çevrilebilir, ama ACİL DEĞİL ve bugün önceliklendirilmiyor.
- ⚠ **BL-195 İLE BAĞ GERİ ÇEKİLDİ.** Sol menünün kısalmasının sebebi bu uç değil.
  BL-195 yeniden açıklanmamış durumda; bir dahaki gözlemde yükü de ölçmek gerekiyor.
  (`22eeed97` commit mesajında bu bağ iddia edilmişti — geçersiz.)
- ALINAN DERS: süre ölçümü, makinenin yükü yazılmadan kayda geçmez. Bu oturumda
  ikinci kez kirli koşulda ölçüp sonuç çıkardım.
- **Gelecek regresyon riski: 🟡** — kod şekli kötü, etkisi bugün yok.

### ⚠ KAYIT (2026-08-24) — BL-204…BL-212 bu dosyada YOK
- Bu turda ölçüldü: dosyanın en büyük numarası **BL-203**. Oysa son iki turda BL-206…BL-212 bu dosyaya
  yazılmıştı ve bu turun şartnamesi **BL-205 ile BL-211'e numarayla atıfta bulunuyor** — yani sahip tarafında
  o kayıtlar var, diskteki dosyada yok.
- `git status`: `docs/product-backlog.md` **değişmemiş** görünüyor; yani kayıtlar commit'lenmedi, geri alındı.
- Çakışmayı önlemek için bu tur **BL-213'ten** devam ediyor. BL-204…BL-212 aralığı **kullanılmadı ve
  yeniden kullanılmayacak** — sahibin elindeki numaralar korunsun diye.
- ⚠ Bu bir tahmin değil ölçüm: kaybolan kayıtların içeriği bu turda yeniden yazılmadı; yalnız durum bildirildi.

### BL-204 — ✅ KAPANDI (2026-08-24) — Gerekçe kutusu sütuna hapsolmuştu
- Ölçüm: `.wcn-actrail-secondary` saran bir flex satırı ve öğeleri **içerikleri kadar** (`flex: 0 1 auto`);
  alert de düğmeyle **aynı `<li>`** içinde. Sonuç: 371px'lik kartta **194px**'lik uyarı — girinti gibi okunuyor.
- **Düzeltme:** gerekçe taşıyan ikincil satır tüm satırı alıyor (`wcn-act-hasreason` → `flex: 1 0 100%`) ve
  **düğme de birlikte** genişliyor (`inline-size: 100%`). Gerekçesi olmayan aksiyonlar doğal genişliklerinde.
- **Düğme neden birlikte taşınıyor:** bu kart **aynı anda iki gerekçe** gösterebiliyor (canlı ölçüldü: Tamamla
  altında "Bir alt görev hâlâ açık", Başkasına ata altında "Bu görev devredilemez."). Alert'leri tek başına
  karta yaymak, hangi cümlenin hangi düğmeye ait olduğunu söyleyen tek şeyi — altında durmasını — bozardı.
- **Dar ekran kardeşi ölçüldü, hapsolma YOK:** `.wcn-actionbar` `display: block`, gerekçe zaten tam genişlik
  (853px'lik şeritte 821px). Dokunulmadı.
- Dört kombinasyonda doğrulandı (1440/900 × aydınlık/karanlık): gerekçeli satırlar liste genişliğinde
  (371px / 805px), gerekçesizler 95px ve 116px.
- **Gelecek regresyon riski: 🟢.**

### BL-205 — [YAPILMADI] Panel kapatma düğmeleri "İptal" diye adlandırılıyor
- `app.js:4473` ve `4603`: offcanvas kapatma (×) düğmelerinin `aria-label`'ı `t('ReasonCancel')` = "İptal".
  Ekran okuyucu bir KAPATMA düğmesini "İptal" diye duyuyor.
- Doğru karşılık büyük olasılıkla `PanelClose` ("Paneli kapat") — modülde zaten var.
- BL-202'nin kapsamı diyalog düğmeleriydi; bu ikisi ayrı bir yüzey, bu turda **değiştirilmedi**.
- **Gelecek regresyon riski: 🟢.**

### BL-206 kapanış notu (2026-08-24) — Düğmeler satırı paylaşır, cümle kendi eylemini söyler
- **Kusurun kökü (ölçüldü):** alert, düğmeyle AYNI `<li>`'nin içindeydi. BL-201'de `<li>`'ye
  `flex: 1 0 100%` verilerek cümleye kart genişliği kazandırıldı — ve satır kaybedildi: sarma yapan bir flex
  sırasında tam genişlik bir `<li>`, diğer bütün düğmeleri kendi satırına iter. Sahibin sorusu tam bu:
  "Başkasına ata neden Bilgi bekle'nin yanında değil?"
- **Karar (sahip, 2026-08-24):** cümle `<li>`'den ÇIKAR. Düğmeler doğal genişlikte tek satırda
  (`flex: 0 1 auto` geri geldi), gerekçeler `<ul class="wcn-actrail-secondary">`'den SONRA tam genişlik.
  Yakınlığın taşıdığı eşleştirme iki yeni taşıyıcıya devredildi: cümle **aksiyonun adını söyler**
  (`ActionDisabledWithName`, 7 dil) ve düğme `aria-describedby` ile **kendi cümlesine bağlanır**.
- **Silinen:** `.wcn-actrail-secondary .wcn-act-hasreason` iki CSS kuralı + yorumu, `app.js`'teki
  `wcn-act-hasreason` sınıfı. Yanlış bir kararı doğru anlatan yorum da gitti.
- **Kimlik kararlı:** `wcn-actreason-{itemId}-{actionCode}` — sayaçtan/rastgeleden değil aksiyon kodundan
  türetiliyor, çünkü bu kart her yoklamada yeniden çiziliyor; sayaç tabanlı bir id `aria-describedby`'yi bir
  sonraki çizimde boşluğa düşürürdü. Testle kilitli (iki çizim, aynı id).
- **Canlı ölçüm (9bf6194e, 1440×900, koyu ve açık):** "Bilgi bekle" ve "Başkasına ata" üst kenarları
  **y=523 ve y=523 — aynı satır**. İki gerekçe kutusu da **371px = kartın iç genişliği**. Cümle:
  "Başkasına ata — Bu görev devredilemez." `aria-describedby` →
  `wcn-actreason-9bf6194e-…-reassign` → var olan `.wcn-act-reason`, doğru cümle. 900×900'da aynısı: y=1388
  / y=1388, kutular 805px. İkinci aday 869195b4 aynı sonucu verdi.
- **Birincil aksiyon (Tamamla) DEĞİŞMEDİ:** kendi katmanında tek başına, zaten tam genişlik, belirsizlik yok
  → adını söylemesine gerek yok, `aria-describedby` almadı. Canlı doğrulandı: "Bir alt görev hâlâ açık"
  hâlâ kendi `<li>`'sinin içinde, 371px.
- **Yıkıcı katman aynı yoldan geçiyor:** her iki tier de artık tek bir `actionRail()` üretiyor, `<ul>`
  markup'ı kodda tek yerde (testle kilitli). ⚠ **Ölçüldü: 62 fixture öğesinin hiçbirinde gerekçesi olan
  devre dışı bir yıkıcı aksiyon yok** — yani canlı vaka bulunamadı; muamele yapısal olarak uygulanmış ve
  testle korunuyor, ekranda gözlenemedi.
- **Dar şerit (`.wcn-actionbar`) ayrı kod yolu, DEĞİŞMEDİ:** `renderActionBar` yalnız **birincil** aksiyonun
  gerekçesini çiziyor (`wcn-actionbar-reason`), ikincil/yıkıcı olanlar gerekçesiz bir dropdown'a katlanıyor.
  Tek cümle, tek düğme → belirsizlik yok, ad gerekmiyor. 900×900'de ölçüldü: 821px, tam genişlik.
- **Gelecek regresyon riski: 🟢** (yapı sadeleşti; iki tier tek üreticiye indi).

### BL-207 — [YAPILMADI] Aynı engel sayfada üç yerde birden yazıyor
- Ölçüldü (9bf6194e, 1440×900): (1) sayfa üstü kırmızı şerit "1 alt görev kapanmadan tamamlanamaz — Alt
  görevlere git", (2) aksiyon kartında "Bir alt görev hâlâ açık", (3) alt görev kartında sarı kutuda aynı
  engel. **Üçü de yanlış değil, ama üçü birden fazla.**
- Ölçülecek: üçü aynı kaynaktan mı geliyor (`disabledReasonCode` / `gates` / `wcn-subtask-gate`), hangisi
  hangi soruyu cevaplıyor (— "bu sayfada bir sorun var" / "bu düğme neden çalışmıyor" / "hangi alt görev"),
  hangisi silinebilir?
- Bu turda **kasıtlı olarak dokunulmadı** — sahibin kararı alınmadan bir uyarı silmek, üç kez söylemekten
  daha kötü olabilir.
- **Gelecek regresyon riski: 🟢.**

### BL-208 — [YAPILMADI] Dar şeritteki dropdown'da devre dışı aksiyon sebebini söylemiyor
- Ölçüldü (900×900, 9bf6194e): `.wcn-actionbar` dropdown'ında "Başkasına ata" **disabled** ama yanında hiçbir
  cümle yok. Kartta aynı düğme "Bu görev devredilemez." diyor; şeritte sessiz.
- BL-206'nın kapsamı karttı; şerit ayrı bir kod yolu (`renderActionBar`) ve bu turda değiştirilmedi.
- **Gelecek regresyon riski: 🟢** (katkısal düzeltme).

### BL-209 — [YAPILMADI] Enterprise Strategy testleri kırmızı (bu turdan önce de kırmızıydı)
- `npx vitest run tests/` → **1517 geçti, 9 kırmızı**; hepsi `strategy-apis`, `objectives-edit-hydration`,
  `planning-cycles-*`, `strategy-periods-*` dosyalarında.
- `git stash` ile doğrulandı: bu turun değişikliklerinden **önce de** kırmızıydılar. WorkCenterNext'e ait
  değil, bu turda düzeltilmedi.
- Ayrıca `wcn-text-in-boxes.test.js` içindeki BL-201 testlerinden biri (`inline-size: 100%` bekleyen) de
  bu turdan önce kırmızıydı — o test bloğu BL-206 ile tamamen değiştirildi.

### BL-210 kapanış notu (2026-08-24) — Bağımlılık satırı kuralı söylüyor (A4, sahip C seçeneği)
- **Ölçülen kusur (canlı, `bfcfa8ba`):** `ÖNCÜL · sasasa · FS · tamam` — dört parça yan yana, aralarında
  görünür ilişki yok; `FS`'in açılımı YALNIZ `title` tooltip'inde (dokunmatikte hiç yok); `tamam` öncülün
  durumu ama satırın sağ ucunda satırın kendi durumu gibi okunuyor.
- **Yapılan:** kompakt tek satır korundu. Yön **ok ikonu** oldu (sol = yukarıdan biri beni tutuyor,
  sağ = ben aşağıdakini tutuyorum); `ÖNCÜL`/`ARDIL` kelimeleri gitti. Tür **yarım cümle** olarak satırın
  içine yazıldı. Durum rozeti ve sözlüğü (`DEP_STATE_KEY` / `DEP_STATE_KIND`, `cancelled` dahil)
  **değişmedi**.
- **Anlam TÜRETİLDİ, uydurulmadı.** Kaynak: `DEPENDENCY_TYPES` (fixture-contract.js:76 = motorun
  `TaskDependencyType`'ı) + ürünün zaten yazdığı iki yer — `DepTypeFS` "Bitince başlar (FS)" ailesi ve
  `BlockerFinishToStart` "«{0}» kapanmadan başlanamaz" ailesi. İlk fiil öncülün ulaşması gereken nokta,
  ikinci fiil ardılın o zaman yapabileceği şey. Sekiz cümle bu tek kuralı iki uçtan okuyor; beşinci bir
  anlam üretilmedi.
- **`Blocker*` anahtarları YENİDEN KULLANILMADI**, bilerek: onlar edilgen, tırnaklı ve yalnız CANLI bir
  engeli anlatıyor. Bu satır ilişkinin kendisini anlatıyor (bitmiş bir öncülün de türü var) ve sahibin
  seçtiği ikinci tekil şahısla konuşuyor.
- **Kısaltma KALDI, rütbesi düştü** — cümleden SONRA, küçük ve soluk, `title`'ı hâlâ duruyor; artık `wcn-chip`
  değil, çünkü çip cümleyle eşit ağırlık iddia eder. Karar: bileni için en hızlı okuma, ama cümle onsuz da
  tam. Testle kilitli (kısaltma DOM'dan silinince satır hâlâ kuralı söylüyor).
- **Satır dili icat edilmedi:** `.diten-checkitem`'dan değer değer kopyalandı — `padding: .375rem .5rem`,
  `1px solid var(--bs-border-color)`, `border-radius: .375rem`, `background: var(--bs-card-bg)`,
  `align-items: center`. Canlı ölçüm: `6px 8px` / `1px rgb(228,230,232)` / `6px` / `rgb(255,255,255)` / center.
- **Ok SESSİZ (`aria-hidden="true"`)** — cümle yönü zaten kelimeyle söylüyor; kendini duyuran bir ikon yönü
  ekran okuyucuya iki kez okuturdu.
- **CANLI KAPSAM — SEKİZDEN KAÇI GÖRÜLDÜ:**
  - Gerçek backend verisiyle **2/8**: `pred/FS` (`bfcfa8ba` "sasasa", durum **tamam**; `38589f6a`, durum
    **başlamadı**) ve `succ/FS` (`95312464`). Ölçüldü: 62 öğenin 3'ünde bağımlılık var, **üçü de FS**.
  - Kalan **6/8** için **FIXTURE EKLENDİ** ve bu yazıldı: `islerim-showcase-fixtures.js` içindeki
    `ISLERIM-WORK-ACTIVE` ("max veri" showcase görevi) bağımlılık listesi 2'den 8'e çıkarıldı. Mevcut iki
    satır **değiştirilmedi**, yeni dize eklenmedi (başlıklar o görevin zaten kullandığı iki kaynak).
    `?fixtures=showcase` ile sekizi de canlı görüldü, sekiz farklı cümle.
- **DURUM × YÖN (sahibin 2. koşulu), canlı:** `pred/FS/done` → "sasasa bitmeden başlayamazsın" + yeşil
  **tamam**; `pred/FS/not-started` → "Anahtar kullanıcı eğitimi bitmeden başlayamazsın" + gri **başlamadı**.
  Fark rozetin renginde ve kelimesinde görünür; cümle ikisinde de aynı kuralı söylüyor — çünkü kural durumla
  değişmiyor, yalnız o kuralın şu an ısırıp ısırmadığı değişiyor.
- **İki genişlik × iki tema:** 1440 ve 900'de sekiz satır da 35px, yatay taşma **yok** (satır `scrollWidth -
  clientWidth = 0`, sayfa `0`). Koyu temada satır yüzeyi kart yüzeyiyle aynı (rgb(43,44,64)), kutu kenarlıkla
  tanımlanıyor — `.diten-checkitem` ile aynı davranış.
- **UZUN BAŞLIK:** 121 karakterlik bir cümleyle ölçüldü (900px): satır 35px → **50px**, cümle **2 satıra
  sarıyor**, kırpma yok, yatay taşma yok. Karar: sarsın — kırpılmış bir kural, üzerine hareket edilemeyen
  bir kuraldır.
- **DEĞİŞMEYENLER (dokunulmadı, yazıldı):** kart **salt okunur**, "Bağımlılık ilişkisi kaynağında yönetilir"
  ipucu duruyor, düzenleme/Gantt/graf eklenmedi (testle kilitli: kartta 0 adet buton/input/link). Boş durum
  (`!dependencies.length` → kart hiç çizilmez) **bu turda değişmedi**; A5'te kararlaştırılan boş-durum dili
  ayrı bir turda gelecek.
- **Gelecek regresyon riski: 🟢** (katkısal; sözlükler ve kart sözleşmesi el değmedi).

### BL-211 — [YAPILMADI] Bağımlılık durum sözlüğünde büyük/küçük harf tutarsız
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** `DepDone` artık "Tamamlandı"; üçü de ürünün kendi durum sözlüğüyle aynı. Kayıt turlar arasında güncellenmemişti.
- Canlı ölçüldü (`ISLERIM-WORK-ACTIVE`, tr): rozetler **"tamam" · "devam" · "başlamadı"** küçük harfle
  başlarken `DepCancelled` **"İptal edildi"** büyük harfle ve tam cümle gibi.
- Sahibin bu turdaki talimatı sözlüğün **değişmemesiydi** (`DEP_STATE_KEY` / `DEP_STATE_KIND`, `cancelled`
  dahil) → **dokunulmadı**.
- Düzeltilecekse yedi dilde birden ve rozet ailesinin tamamına bakılarak yapılmalı.
- **Gelecek regresyon riski: 🟢.**

### BL-212 — [YAPILMADI] Engel afişindeki `FS` çipi hâlâ tek taşıyıcı
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Afişteki kısaltma `wcn-dep-abbr` dipnotu olarak çiziliyor (canlı ölçüldü, ISLERIM-WORK-BLOCKED), kırmızı hap gitti. Kart ile afiş tek dil konuşuyor.
- `renderBlocked` satırları `<span class="wcn-chip wcn-chip-danger wcn-dep-type" title="…">FS</span>`
  kullanmayı sürdürüyor: kısaltmanın açılımı orada **hâlâ yalnız tooltip'te**.
- Orada cümle zaten var (`BlockerFinishToStart` ailesi), yani afiş bağımlılık satırı kadar kör değil — ama
  çip aynı tooltip-bağımlılığını taşıyor.
- A4'ün kapsamı **bağımlılık kartıydı**; afiş ayrı bir yüzey ve bu turda **değiştirilmedi**.
- **Gelecek regresyon riski: 🟢.**

### BL-213 kapanış notu (2026-08-24) — WorkCenter diyalogları tek dil konuşuyor (A3, sahip kararı (b))
- **Ölçülen fark (ham `Swal.fire` ↔ ertele diyaloğu):** başlık 38px↔18px · açıklama 18px↔13px · popup
  512px↔400px · vazgeç KIRMIZI↔nötr · ikon yok↔ay.
- **Sebep, dikkatsizlik değil yapıydı:** görünüm `showConfirm`'ün İÇİNDE bir `customClass` sabitiydi. Onay
  olamayan bir diyalog, görünümü de alamıyordu. Kırmızı vazgeç düğmesi temanın **küresel varsayılanı**
  (`btn-label-danger`); paketi alan onu eziyordu, almayan ezemiyordu.
- **Yapılan:** `window.DitenDialogAppearance(options)` — adlandırılmış, yayınlanmış, **tek tanım**. `showConfirm`
  artık onu OKUYOR; `confirmVariant` (düğme rengi) dışarıdan gelmiyor, `type`'tan türüyor.
- **Reddedilen (a):** ortak bileşene "özel alanlar" dikişi. Bu oturumda ona altı parametre eklendi; yedincisi
  form üreticisine çevirirdi.
- **`inputOptions` = YEDİNCİ ve SON parametre.** `select` seçenekleriyle gelmezse kullanılamaz bir kutudur —
  eksik bir parametrenin diğer yarısı, yeni bir yetenek değil. **Geriye uyum sayıldı: 15 çağrı / 12 dosya,
  hiçbiri `inputOptions` geçmiyor** (testle kilitli).
- **Dört diyalog TAŞINDI** (tek değer → onay): Planla · Toplantı zamanı · Süre gir · Modül seç.
  Tarih dikişi açılmadı — ertele diyaloğunun `inputType` + `onOpen` + `validate` yolu kullanıldı.
- **Dört diyalog PAKETİ ALDI** (onay değil / çok alanlı): "+ Yeni" menüsü · Yeni toplantı formu ·
  Aksiyon onayı (sahibin fotoğrafladığı) · Toplu ilerleme. Ham `Swal.fire` kaldılar ama artık `dialogLook()`
  yayıyorlar. Yeni toplantı formunun **yapısına dokunulmadı** (B3'te offcanvas olacak).
- **PLACEHOLDER SAYIMI:** sekiz diyalogda **9 kutu** var. Doldurulabilir 7'sinin **3'ünde placeholder VARDI**
  (`LogTimePlaceholder` "örn. 30", `ReasonPlaceholder`, select'in boş seçeneği `NewPickModule`), **4'üne
  EKLENDİ** (`DatePlaceholder`, `DateTimePlaceholder`, `MeetingTitlePlaceholder`,
  `MeetingLocationPlaceholder`). Kalan 2 kutu `type="time"` — native saat kutusu placeholder'ı **hiç
  göstermez**, değeri (09:00/09:30) zaten örnek; eklenmedi ve **eklenmediği yazıldı**.
- **ALAN İKONU — tek tek:**
  - tarih (Planla) → **takvim**, `.wcn-date-input`, sarmalayıcı YOK
  - tarih+saat (Toplantı zamanı) → **takvim**, aynı sınıf
  - süre (Süre gir) → **saat**, yeni `.wcn-time-input`, AYNI teknik (glif kutunun arka planında), SVG ürünün
    kendi `.bx-time-five`'ı, koyu tema ve RTL varyantlarıyla
  - select (Modül seç) → **ikon YOK**: temanın kendi oku zaten var, ikincisi iki ok olurdu
  - serbest metin (gerekçe, toplantı başlığı, konum) → **ikon YOK**: kutunun ne olduğunu kutu söylüyor
  - saat kutuları (başlangıç/bitiş) → native kontrolün kendi saat ikonu var, dokunulmadı
- **BL-205 kapandı:** `app.js:4578` ve `4708` → `PanelClose`. Canlı doğrulandı: panelin kapatma düğmesi
  **"Paneli kapat"** diyor. Dosyada `ReasonCancel` **sıfır** kez geçiyor.
- **BL-211 kapandı, İLK TEŞHİS DÜZELTİLEREK:** `DepCancelled` baştan doğruymuş; aykırı olan üçü.
  `DepDone`/`DepInProgress`/`DepNotStarted` artık **ürünün kendi sözlüğüyle birebir aynı** —
  `SubtaskStatusDone`/`InProgress`/`NotStarted` değerlerinden ÖLÇÜLEREK kopyalandı, uydurulmadı, 7 dil.
  (en "Done", tr "Tamamlandı", fr "Terminé", es "Completado", zh "已完成", ar "مكتملة", ru "Готово").
- **Gelecek regresyon riski: 🟢** (görünüm tek tanıma indi; sözleşme genişlemedi).

### BL-214 — [YAPILMADI] İki diyalog kullanıcı arayüzünden ULAŞILAMIYOR
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Tur B'de silindi: `runBulk`/`bulkBar` sıfır eşleşme, `openNew` yalnız silinme gerekçesini anlatan bir yorum olarak duruyor.
- **"+ Yeni" Swal menüsü (`openNew`, app.js:7012):** dispatch onu yalnız `data-wcn-new` değeri
  task/note/meeting/source **olmayan** bir düğme için çağırıyor. Canlı ölçüldü: DOM'daki dört düğmenin
  dördü de bilinen kind taşıyor → **hiçbir tıklama buraya varmıyor**. Liste sayfasındaki "+ Yeni" artık bir
  Bootstrap dropdown'ı; bu Swal menüsünün yerini almış.
- **Toplu ilerleme (`runBulkWithProgress`, app.js:7448):** `data-wcn-check` işaretçisi kodda yalnız
  OKUNUYOR, hiçbir yerde ÇİZİLMİYOR (grep: 4 eşleşme, hepsi olay yakalayıcı). Seçim kutusu olmadan
  `state.tableSelected` boş kalıyor, toplu şerit hiç görünmüyor, diyalog hiç açılmıyor.
- Bu turda **ikisi de görünüm paketini aldı** (kaynak seviyesinde ve testle kilitli), ama görünümleri
  tıklanarak değil kendi kod yollarına girilerek ölçüldü — raporda böyle yazıldı.
- Ulaşılabilir kılmak için: `openNew` ya silinmeli ya dropdown'un yerine geri konmalı; toplu işlem için
  tablonun seçim sütunu geri gelmeli. **İkisi de ürün kararı, bu turun konusu değil.**
- **Gelecek regresyon riski: 🟡** — ölü kod, ama görünüm paketini aldığı için ileride yanlış bir "çalışıyor"
  izlenimi verebilir.

### BL-215 — [YAPILMADI] Görünüm paketinin DÖRT eski kopyası WorkCenter dışında duruyor
- Ölçüldü: `popup: 'rounded-4 shadow-lg'` dizesi bu turdan ÖNCE de dört dosyada kendi kopyasını taşıyordu —
  `shared/premium-modal.js`, `Account/login.js`, `Account/forgot-password.js`, `Account/reset-password.js`.
- A3'ün kapsamı WorkCenter'dı: bu turda **hiçbiri değiştirilmedi**. Test onları **listeliyor** (birine
  dokunulursa kırmızı olur) ve WorkCenter'ın **sıfır** kopya taşıdığını kilitliyor.
- Doğrusu: dördü de `window.DitenDialogAppearance()` okumalı. Account ekranları ayrı bir tur.
- **Gelecek regresyon riski: 🟡** — paket değişirse bu dört ekran ayrışır.

### BL-216 — [YAPILMADI] Referans diyaloğun kendi placeholder'ı, sahibin A-kuralını çiğniyor
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Ertele kutusunun placeholder'ı `YYYY-AA-GG`; etiket ayrıca duruyor. Referans artık kendi kuralını çiğnemiyor.
- Sahibin (A) kuralı: placeholder GERÇEK BİR ÖRNEK olacak, alan adının tekrarı değil.
- Ölçüldü: **ertele diyaloğu** — bu turun REFERANSI — tarih kutusuna placeholder olarak
  `SnoozeUntilLabel` ("Hangi tarihe kadar") koyuyor; bu bir soru, örnek değil. `SnoozeDatePlaceholder`
  ("YYYY-AA-GG") resx'te duruyor ama **kullanılmıyor**.
- Ertele diyaloğu sekiz diyalogdan biri DEĞİLDİ, o yüzden bu turda **kasıtlı olarak dokunulmadı** —
  referansı tur ortasında değiştirmek, kıyaslamayı geçersiz kılardı.
- **Gelecek regresyon riski: 🟢.**

### BL-217 kapanış notu (2026-08-24) — Yedi kusur, üç kök sebep, iki ölü uç (A2)
- **1·3·4·5 — ortalanmış etiketler.** Dördü de ortak bileşenin `inputLabel` yolundan geçiyordu; popup her şeyi
  ortaladığı için etiket alanın üstünde ortada duruyordu. **Tek satır** düzeltti:
  `inputLabel: 'form-label d-block w-100 text-start'`.
  ⚠ `w-100` süs değil, **ölçüldü**: `d-block text-start` ile etiket `display: block` hesaplanıyor ama yine
  58px genişlikte, kendi kutusunun **94px sağında** kalıyordu (etiket x=691, girdi x=597) — SweetAlert popup'ı
  GRID kuruyor ve otomatik genişlikli bir öğe kendi izinin ortasına düşüyor. `d-block w-100 text-*` üçlüsü
  paketteki BAŞLIĞIN zaten kullandığı üçlü; yeni bir şey icat edilmedi, var olan deyim tamamlandı.
  Canlı: etiket x=597, kutu x=597 — **aynı sol kenar**.
- **Başlık ve açıklama ORTADA KALDI**, kasıtlı: ikisi de DİYALOĞA sesleniyor, etiket ise altındaki KUTUYA.
  Ürünün her oluşturma formunda aynı düzen var. Testle kilitli.
- **Elle yazılan etiketler ayrışmadı:** "Bilgi bekle"nin iki etiketi zaten `form-label d-block text-start`
  taşıyordu; test artık DİYALOG içindeki elle yazılmış her etiketin `text-start` taşıdığını doğruluyor.
  ⚠ Offcanvas PANEL etiketleri (`form-label`, `text-start` yok) kapsam dışı bırakıldı ve bu yazıldı: panel
  zaten sola dayalı, orada `text-start` hiçbir şeyi değiştirmezdi.
- **6 — "Devam etmek istediğinize emin misiniz?"** Ortak bileşenin varsayılanıydı ve `options.subtext || default`
  ifadesi `''` değerini "çağıran bir şey söylemedi" sayıyordu. Artık `undefined` "söylenmedi", `''` ise
  "bilerek yok" demek. WorkCenter seam'i: `options.input` varsa (yani GİRDİ İSTEMİ ise) `''`, yoksa varsayılan.
  **GERÇEK ONAYLAR DOKUNULMADI** — canlı doğrulandı: `/RoleAssignments` silme onayı hâlâ
  "Devam etmek istediğinize emin misiniz?" diyor.
- **ONAY / İSTEM SAYIMI (15 çağrı, 12 dosya):** 14'ü ONAY (Tenants ×2, ReferenceData hierarchy + mappings,
  AuditLog redaksiyon, UserRoleAssignments, RoleAssignments, QmsBaselines details + designer ×3,
  ControlledDocuments, Instantiations, WorkCenter `confirmDestructive`, premium-modal aktarıcı) → **varsayılanı
  korudu**. 1'i WorkCenter seam'i (`sharedConfirm`) → istemleri sessizleştirdi, onaylarını korudu.
- **Üç yeni cümle, 7 dil:** `LogTimeSubtext` · `MeetingWhenSubtext` · `NewInSourceSubtext`. Her biri kutunun
  SORMADIĞI bir şeyi söylüyor (eklenir/sayacı başlatmaz · takvime yazar/son tarih değişmez · kayıt o modülde
  kalır).
- **2·6·7 — ikonlar.** Çember+glif `showConfirm`'ün İÇİNDE kuruluyordu, bu yüzden ham bir diyalog paketi alıp
  yine ikonsuz açılıyordu. `window.DitenDialogAppearance.iconHtml(type, glyph)` yayınlandı; `showConfirm` de
  onu okuyor (tek üretici, iki tüketici). **Yeni parametre AÇILMADI** — `options.icon` ertele turunda açılmıştı.
  - "Bilgi bekle"/"Başkasına ata" → **`bx-conversation`**. Gerekçe: bu diyalog işi bir KİŞİYE devrediyor ve
    nedenini yazıyor; ikisi de bir insana mesaj. Varsayılan `?` "emin misin?" diye soruyor — sorulan soru bu
    değil; kilit ya da uyarı ise olmayan bir yasak iddia ederdi. Çember ve rengi hâlâ `type`'ın (`info`).
  - "Hızlı not"un `?` ikonu **düzeltilmedi, diyalog silindi**.
- **4 — iki select artık select2.** Ürünün kendi kurulumu kullanıldı, yeni sarmalayıcı yazılmadı.
  ⚠ **Z-INDEX YAPISAL OLARAK ÇÖZÜLDÜ:** `dropdownParent` = POPUP. flatpickr takvimi bu oturumda 1074'te kalıp
  1090'lık diyaloğun ARKASINA düşmüştü; bir torun atasının arkasında kalamaz, yani soru bir sayıyla
  cevaplanmadı, ortadan kaldırıldı.
  **KANIT (canlı):** liste açıldı, bir seçeneğin merkezinde `document.elementFromPoint` çağrıldı → dönen eleman
  **o seçeneğin ta kendisi** (`select2-results__option | Diten Admin`). Sonra **gerçek tıklama**: `<select>`
  değeri `11111111-1111-1111-1111-111111111111`, kontrolde "Diten Admin" göründü. Modül seçicide de aynı:
  gerçek tıklama → `Swal.getInput().value === "Görevler"`.
  **`Swal.getInput()` HÂLÂ ÇALIŞIYOR** ve `.swal2-select` popup'ın DOĞRUDAN ÇOCUĞU: select2 orijinali yerinde
  gizleyip kendi kabını KARDEŞ olarak ekliyor, araya girmiyor.
- **⚠ SEAM'DE BULUNAN GERÇEK KUSUR (bir tur yaşamış olacaktı):** `didOpen` dikişi kutuyu
  `popup.querySelector('.swal2-input, .swal2-select, …')` ile buluyordu; `querySelector` seçici sırasına değil
  **BELGE SIRASINA** bakar ve SweetAlert bütün yuvalarını çizip kullanmadıklarını gizler — bu yüzden `select`
  diyaloğuna **gizli `.swal2-input`** veriliyordu ve modül seçici sessizce yerli kaldı. Artık kütüphanenin
  kendi cevabı soruluyor: `Swal.getInput()`.
- **⚠ SELECT2'NİN YUTTUĞU GERÇEK CÜMLE:** `placeholder: ''` geçmek select2'nin placeholder mekanizmasını
  AÇIYOR ve boş değerli ilk seçeneği placeholder sayıp hiç çizmiyor. "Kim bekleniyor?" seçicisinin ilk
  seçeneği bir placeholder değil, **gerçek bir cevap** ("Belirli bir kişi değil") — ve kutu boş açılıyordu.
  Artık placeholder anahtarı yalnız gerçekten varsa geçiliyor. Canlı doğrulandı.
- **⚠ ETİKET DÜZELTMESİNİN İKİNCİ YARISI (ölçülerek bulundu):** etiket sola gelince, kütüphanenin girdi
  yuvalarına verdiği `margin-inline: 34px` yüzünden etiket kutunun **34px soluna** düştü (etiket x=544,
  kutu x=578) — bu 34px ürünün hiçbir yerinde kullanılmıyor ve `.wcn-date-input`/`.wcn-time-input` onu zaten
  elle iptal ediyordu. Tek yerde iptal edildi: popup içindeki `.swal2-input/.swal2-select/.swal2-textarea`
  artık sütununu dolduruyor. **Her modülün onayını etkiler** — kutular 68px genişledi, hiçbir şey yer
  değiştirmedi; üç ekran öncesi/sonrası canlı ölçüldü. Canlı: etiket x=544, kutu x=544.
- **5 — iki ölü uç KALDIRILDI (devre dışı bırakılmadı).** `openQuickNote` → `state.notes.unshift(...)`,
  `openMeetingForm` → `state.meetings.push(...)`, ikisinde de API çağrısı YOK, `state` ikisini de `[]` ile
  başlatıp hiç yüklemiyor. Menüden iki madde, iki diyalog ve iki dispatch dalı silindi.
  ⚠ **AJANDA PANELİNİN "+" DÜĞMESİ DE GİTTİ** — bu turun şartnamesinde YOKTU ve burada söyleniyor: aynı silinen
  forma açılan **ikinci kapıydı**; bırakılsaydı var olmayan bir fonksiyonu çağıracaktı. Panelin başka hiçbir
  yeri değişmedi.
  ⚠ **DOKUNULMAYANLAR:** detay sayfasının KİŞİSEL NOT kartı (`TasksApi.addPersonalNote`, gerçek) ve
  "Onay toplantısı planla" AKSİYONU (sözleşmesi var) — ikisi de yerinde, testle kilitli.
- **Gelecek regresyon riski: 🟢.**

### BL-218 — [ERTELENDİ, silinmedi] Genel not ve ajanda: ürünün istediği, arkası olmayan iki özellik
- BL-217'de kaldırılan iki uç bir NİYETİ temsil ediyordu ve o niyet kayboldu sayılmasın:
  - **Hızlı not:** göreve bağlı olmayan, kişisel, serbest bir not. (Göreve BAĞLI kişisel not zaten gerçek ve
    çalışıyor — `TasksApi.addPersonalNote`.)
  - **Toplantı planla:** Görev Merkezi'nden takvime bir toplantı yazmak.
- Gereken: bir kalıcılık sahibi (hangi servis? MOD-0024 mü, ayrı bir kişisel-veri servisi mi?) ve toplantı için
  gerçek bir takvim entegrasyonu.
- **Gelecek regresyon riski: 🟢** (bugün kod yok).
- **GÜNCELLEME 2026-08-25 (BL-244):** "bugün kod yok" artık tam olarak doğru. BL-217'de render'lar
  silinmişti ama kancaları, durum alanları, `panel` URL parametresi ve veri üreticileri kalmıştı; hepsi
  kaldırıldı. Paneller geri geldiğinde bu katman da **yeniden yazılacak** — geriye yalnızca bu niyet kaydı
  kaldı.

### BL-219 — [KAYIT] "Onay toplantısı planla" da yalnız tarayıcı belleğine yazıyor
- Ölçüldü: `applyReviewMeeting` → `state.meetings.push({...})`; kodun kendi yorumu "the mock applies an explicit
  replacement projection after Calendar returns" diyor. **Sözleşmesi var** (`WorkAggregationModels.cs:832`
  `reviewMeetingPolicy`), **gerçeklemesi yok**.
- Bu yüzden BL-217'de SİLİNMEDİ: silinen ikisinin aksine bunun arkasında bir sözleşme duruyor, yani eksik olan
  özellik değil, servis.
- Bu turda **dokunulmadı** — yalnız kaydedildi.
- **Gelecek regresyon riski: 🟡** — kullanıcı bir toplantı planladığını sanıp takvimde bulamaz.

### BL-220 — [KAYIT] Notlar ve ajanda PANELLERİ hep boş
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** Tur B'de paneller kaldırıldı; `/WorkCenterNext` üzerinde panel düğmesi ve `#wcnSidePanel` sıfır. BL-218 ile birlikte geri gelecekler.
- İkisi de liste sayfasının parçası; `state.notes` / `state.meetings` artık hiç doldurulmuyor (BL-217), yani
  panellerin ikisi de kalıcı olarak boş.
- Bu turda **kasıtlı olarak değiştirilmediler**: kaderleri liste sayfasının kendi turunda kararlaşacak
  (sil / boş-durum dili / BL-218 ile birlikte geri getir).
- **Gelecek regresyon riski: 🟡** — boş bir panel, açan için cevapsız bir soru.

### BL-221 kapanış notu (2026-08-24) — Diyalogların dikey ritmi ürünün ritmi oldu (A1)
- **REFERANS ÖLÇÜMÜ (/Tasks/Create, canlı):** etiket → alan **4px** (`.form-label { margin-bottom: 4px }`,
  altı alanda da) · alan → sonraki etiket **14px** ("Tahmin (saat)" → "Etiketler") · etiket **13px**.
- **KUSUR, ÖLÇÜLDÜ:** Planla · Süre gir · Modül seç → etiket→alan **19px**. Bilgi bekle · Yeniden ata →
  alan→sonraki etiket **0px**.
- **İKİ KÖK SEBEP:**
  1. Geçen tur yalnız `margin-inline` sıfırlandı; kütüphanenin DİKEY marjları duruyordu.
     ÖNCE: `.swal2-input` `margin: 15px 0px 3px` · `.swal2-input-label` `margin: 13px 0px 4px` → 4+15 = 19px.
     SONRA: yuvaların dikey marjı **0**, etiket `margin: 14px 0px 4px` → **4px**.
  2. Elle yazılmış diyaloglarda grup boşluğu `<select class="form-select mb-3">` üzerindeydi — select2
     orijinali gizleyince o marj hiçbir şey üretmez oldu (**0px** ölçüldü). `mb-3` kaldırıldı; grup boşluğunu
     artık TEK mekanizma taşıyor: bir sonraki etiketin üst marjı.
- **SAYILAR UYDURULMADI, ÖLÇÜLDÜ:** `0.25rem` (4px) = referansın kendi `.form-label` alt marjı.
  `0.875rem` (14px) = referansın **ekranda görünen** grup boşluğu.
  ⚠ **Neden 14 ve neden 12 değil — fudge gibi göründüğü için yazılıyor:** o formda boşluğu `.row.g-3` üretiyor
  ve her grup SÜTUNUNA `margin-top: 12px` koyuyor; sütunun kutusu içindeki girdiden 2px daha aşağı iniyor
  (alan altı 755, sonraki etiket üstü 769). Popup'ta öyle bir sütun yok, dolayısıyla 12px kural 12px çizer ve
  kopyaladığı formdan 2px daha sıkı durur. Kopyayı aslına benzeten değer ölçülen değerdir; mekanizma CSS'te
  yazılı ki sonraki okuyucu nereden geldiğini bilsin.
- **YAN YANA KANIT:** /Tasks/Create üzerinde ürünün KENDİ onay diyaloğu açıldı; aynı ekran görüntüsünde form
  grubu (REF 4px / REF 14px işaretli) ve diyalog grubu (DLG 4px işaretli) birlikte, aynı fonksiyonla ölçülmüş.
- **İKON SÖZLÜĞÜ İKİYE AYRILMIŞTI, BİRLEŞTİRİLDİ.** Geçen tur elle `bx-conversation` seçilmiş ve HEM
  "Bilgi bekle"ye HEM "Yeniden ata"ya verilmişti; rail düğmesi ise `bx-user-pin` çiziyordu — **aynı aksiyon,
  iki ikon**. Artık aksiyondan açılan her diyalog `inboxActionIcon(action)` okuyor (5 okuma: taşma menüsü +
  dört diyalog). Sözlükte eksik olan ikisi **sözlüğe** eklendi: `logTime: 'bx-time-five'`,
  `requestInfo: 'bx-question-mark'`. Canlı doğrulandı: rail `plan → bx-calendar-plus`,
  `inquire → bx-question-mark`, `reassign → bx-user-pin`; açtıkları diyaloglar **aynı üç glif**.
- **"Yeniden ata" denetim listesine eklendi** (geçen tur yoktu): etiket→alan 4/4, alan→sonraki etiket 14,
  x eşit, ikon `bx-user-pin`.
- **GERİYE UYUM (üç ekran canlı, ritim kuralı hepsini etkiliyor):** `/Platform/ReferenceData` (etiket→alan 4,
  x eşit, kutu→düğme 16px, taşma yok, popup 400px) · `/UserRoleAssignments` (357px, taşma yok, `bx-trash`,
  ters düğme sırası, rozet) · `/RoleAssignments` (357px, taşma yok, danger çemberi, varsayılan cümle yerinde).
  **Hiçbiri daralmadı, hiçbiri taşmadı.**
- **Gelecek regresyon riski: 🟢.**

### BL-222 — [KAYIT] İkon eşleşmesi için jsdom testi yazılamadı, canlı ölçüldü
- ✅ **KAPANDI (Tur C).** Kural `fixture-contract.js`'te tek yerde. ⚠ Ajan kendi testinin zayıflığını da kaydetti: ilk hâli, aradığı dizeler yorumda da geçtiği için nesne silinmesine rağmen yeşil kalmıştı — düzeltildi.
- Kıyaslanacak glif LİSTE SATIRININ aksiyon kümesinde çiziliyor ve bir satırın oraya ulaşması sekme/kabul
  kurallarına bağlı (`admissionState`, `ownershipState`, aktif sekme). Üç ayrı fixture şekli denendi, hiçbiri
  satırı varsayılan sekmeye koymadı — test ikonları değil fixture'ı doğrulamış olacaktı.
- Bunun yerine iddia **iki başka yoldan** kilitlendi: (a) kaynak testi — iki yüzey de `inboxActionIcon`
  çağırıyor ve hiçbir diyalog elle glif seçmiyor; (b) canlı ölçüm — üç aksiyon için rail düğmesinin sınıfı ile
  açılan diyaloğun sınıfı aynı dize.
- Yapılacak: liste fixture'ının hangi alanla varsayılan sekmeye düştüğünü belgeleyip DOM testini eklemek.
- **Gelecek regresyon riski: 🟡** — kaynak testi bir yeniden düzenlemede yeşil kalıp DOM'da ayrışabilir.

### BL-223 kapanış notu (2026-08-24) — Diyalogdaki select2'nin metni 18px'ti, ürünün alanı 15px
- **Sahip resimle bildirdi.** Ölçüldü: "Bir kişi seçin" **18px**; yanındaki textarea **15px**, arkadaki sayfanın
  her `.form-control`'ü **15px**, etiket 13px.
- **SEBEP — sessizce çalışmayan bir JS seçeneği:** bağlayıcı `selectionCssClass: 'form-select'` geçiyordu ki
  kontrol ürünün alan stilini giysin. Sınıf elemana **hiç ulaşmadı**: DOM'da
  `class="select2-selection select2-selection--single"`, içinde `form-select` yok. O anahtar daha yeni bir
  select2 sürümüne ait; bu paket bilinmeyen anahtarları **sessizce düşürüyor**. Yani stil hiç uygulanmamış,
  hata da vermemişti.
- **ÖLÇEREK DÜZELTİLDİ, TAHMİNLE DEĞİL:** kanca `containerCssClass: 'wcn-dialog-select'` yapıldı; sonra DOM
  tekrar okundu ve sınıfın **kabın değil `.select2-selection`'ın kendisine** indiği görüldü
  (`select2-selection select2-selection--single wcn-dialog-select`). CSS seçicileri **ölçülen yapıya** göre
  yazıldı, seçeneğin adına göre değil.
- **Sayılar ürünün kendi sayıları:** `0.9375rem` (15px) ve `38px` — `.form-control`'den, `.wcn-date-input`'un
  zaten kopyaladığı aynı değerler. Stil `backbone-custom.css`'te (FG-003), tıpkı ürünün select2'yi diğer
  yüzeylerde (filtre çipleri) stillediği gibi.
- **Açılan liste de aynı boyutta:** 15px'lik bir kontrolün 18px'lik menü açması aynı uyumsuzluğun bir adım
  sonrasıdır (`.wcn-dialog-select-dropdown .select2-results__option`).
- **Canlı ölçüm (sonra):** select2 metni **15px** = textarea 15px = sayfadaki form-control 15px; kutu 38px;
  sol kenarlar hizalı (573/573).
- **Gelecek regresyon riski: 🟢** — testle kilitli (çalışmayan seçenek geri gelirse kırmızı).

### BL-224 kapanış notu (2026-08-24) — Ortak onay diyaloğunun sayımı yanlıştı; bekçi kuruldu
- **YANLIŞ ÖLÇÜM, DÜZELTİLDİ.** Bu oturum boyunca ortak confirm'in yayılımı **"15 çağrı / 12 dosya"** diye
  ölçüldü ve her prompt'a öyle yazıldı. Gerçek:
    `showConfirm(`   = 16
    `showConfirm?.(` = 58   ← optional chaining; grep'ler bunu HİÇ görmedi
    **TOPLAM = 74 çağrı / 53 dosya**
  Yani bu oturumdaki her "geriye uyum ölçüldü" cümlesi yüzeyin **%20'sini** kapsıyordu.
  ⚠ Yanlış sayı `wcn-dialog-one-language.test.js` içinde `toBe(15)` diye **kilitliydi ve yeşildi** —
  yanlış güvence veren bir bekçi, bekçisizlikten kötüdür.
- **DÜZELTME BİÇİMİ ÖNEMLİ: `toBe(15)` → `toBe(74)` YAPILMADI.** Sabit sayı her meşru yeni çağrıda kırılır ve
  okuyucuya "sayıyı büyüt" refleksini öğretir — 15'in bir oturum boyu yaşamasının sebebi tam olarak buydu.
  Test artık **KURALI** doğruluyor: opt-in bir parametre, onu adıyla geçmeyen hiç kimseye ulaşmaz. Sayı
  yalnızca **raporlanıyor**, kilitlenmiyor.
- **BEKÇİ KURULDU:** `tests/dialog-one-implementation.test.js`
  - Ham `Swal.fire` açan her dosyayı tarar; **isimli istisna listesi** dışında bir tane bile varsa KIRMIZI.
  - İstisna listesi **12 dosya** (ham çağrı sayısı 25): `WorkCenter/task-detail.js` 8 ·
    `WorkCenterNext/app.js` 4 · `Account/{login,forgot-password,reset-password}.js` 2+2+2 ·
    `pages/demand-ideas/*` 3 · `Platform/{AuditRetention,Administrators}` 2 ·
    `DocumentManagement/TemplateMasters` 1 · `diten-unauthorized.js` 1.
  - **Bayat istisna da kırmızı:** listedeki bir dosya düzeltilirse ve satırı kalırsa test uyarır — yoksa
    delik açık kalır.
  - Her regex **iki çağrı biçimini de** görür (`showConfirm\s*\??\.?\s*\(`) — sayımı bozan hata buydu.
  - **Sanity taban:** optional-chaining biçimi toplamın yarısından fazla olmalı; olmazsa matcher'dan şüphelen.
- **İKİNCİ GERÇEKLEME BULUNDU VE MEŞRU ÇIKTI:** `backbone-shell.js:76` `window.showConfirm` atıyor — ama
  `if (typeof window.showConfirm !== 'undefined') return;` ile korunmuş bir **yedek** (partial yüklenmezse
  yerli `window.confirm`). Test bunu **isimle** kabul ediyor VE kapının açık kaldığını doğruluyor: yedek
  koşulsuz hale gelirse gerçek diyaloğu gölgeler, o yüzden koşul testte kilitli.
- **MUTASYON (2, ikisi de kırmızı):** yeni bir modüle ham `Swal.fire` yazıldı → bekçi dosya yoluyla kırmızı ·
  istisna listesine ham çağrısı olmayan bir dosya eklendi → bayatlık testi kırmızı.
- **KASTEN YAPILMAYANLAR:** 25 ham çağrının hiçbirine dokunulmadı. Sebebi CT kararı: içlerinde `login.js`,
  `forgot-password.js`, `reset-password.js` var — giriş akışı; kırılırsa kimse sisteme giremez. Ayrı tur.
- **Gelecek regresyon riski: 🟢** — bu madde riski AZALTIYOR: bundan sonra merge edilen her yeni modül ortak
  diyaloğu ya çağırır ya da testte durur.

### BL-225 — [TASARLANDI, ÖLÇÜM BEKLİYOR] Onay diyaloğunda ağırlık kademesi
- **Fikir:** diyaloğun okuyucuyu yavaşlatma derecesi sonuçla orantılı olsun — hafif (sadece sor) / orta
  (renkli şerit) / ağır ("geri alınamaz" + onay kutucuğu).
- **CT REDDETTİ, GEREKÇESİYLE (2026-08-24):** kademeyi `type` alanından türetmek yanlış. Ölçüldü: 73 çağrının
  **50'si `danger`**, yani "en ağır kademe" varsayılan olurdu — %68'e uygulanan bir uyarı hiçbir şey söylemez.
  Dahası o 46 çağrıya `danger` yazılmış çünkü **düğme kırmızı olsun** istenmiş, "bu iş geri alınamaz" denmek
  istendiği için değil. **`type` bir renk alanı, bir sonuç alanı değil.** 50 diyaloğa "geri alınamaz" yazmak
  çoğunda yalan olur; kullanıcı üçüncü seferden sonra o cümleyi okumayı bırakır — ve gerçekten geri alınamayan
  işte de okumaz.
- **BU OTURUMUN TEKRAR EDEN HATASI:** anlamı, o anlamı taşımayan bir alandan türetmek (bkz. `snoozedUntil`
  işaretliyordu ama gizlemiyordu; yorum `segmentFor`'un ona baktığını söylüyordu, bakmıyordu).
- **YAPIM TETİKLEYİCİSİ:** önce **geri-alınabilirlik ölçümü** (BL-226). Kademe gerçek sonuca göre kurulur;
  kutucuk yalnız gerçekten dönüşü olmayanlara.
- **Gelecek regresyon riski: 🟢** (bugün kod yok).

### BL-226 — [YAPILMADI] Yıkıcı aksiyonların geri-alınabilirlik envanteri
- **Soru:** her yıkıcı aksiyonun bir geri alma yolu var mı? Grep'lenebilir: `reopen`, `reactivate`, `restore`,
  `undo`, soft-delete alanları.
- **Bilinen tek ölçüm:** WorkCenterNext'te "Görevi iptal et" **geri alınamıyor** — `reopen`/`reactivate`/`undo`
  yok.
- Çıktısı BL-225'in girdisi. CT "ucuz bir tur" dedi.
- **Gelecek regresyon riski: 🟢.**

### BL-227 kapanış notu (2026-08-24) — Onay diyaloğu Seçenek B'ye geçti (74 diyalog, tek dosyadan)
- **SAHİBİN SEÇİMİ:** dört prototipten **Seçenek B**, rozet **secondary**. CT ağırlık kademesini reddetti
  (BL-225), yani **tek ağırlık** uygulandı — "geri alınamaz" cümlesi ve onay kutucuğu YOK.
- **NE DEĞİŞTİ (hepsi `_GlobalConfirmation.cshtml` + `backbone-custom.css`):**
  - İkon **80px → 32px** ve **başlığın satırına** taşındı. ⚠ Yuvaya değil, **başlığın İÇİNE**: popup bir GRID
    ve her yuva bir satır; iki şeyi bir satıra almak grid'i ezmek demekti — bu oturumda iki kez kırılan
    manevra. Tek yuvaya birleştirmek hiç grid cerrahisi istemiyor.
  - Başlık, açıklama, etiket, alan, rozet, düğmeler: **hepsi sola dayalı, hepsi 24px**.
  - ⚠ Bunu mümkün kılan tek satır: kütüphanenin `.swal2-html-container` üzerindeki
    **`padding: 18px 28.8px 5.4px`** iptal edildi. O 28.8px ürünün hiçbir yerinde yok ve açıklamanın x=573,
    diğerlerinin x=544 olmasının sebebiydi. (Aynı sınıf: daha önce iptal edilen 34px yatay ve dikey marjlar.)
  - Rozet **`bg-label-primary` → `bg-label-secondary`**, tam genişlik, tek satır (kırpılır).
    ⚠ `type`'ı İZLEMİYOR, bilerek: rozet kaydın **adını** taşıyor, eylemin ciddiyetini değil. Bir ismi
    kırmızıya boyamak "bu isim tehlikeli" demektir.
  - Düğmeler **iki uca** (`justify-content-between`), `px-5` kaldırıldı — referans create-task offcanvas
    footer'ı; ölçüldü, temanın kendi 20px inset'ini taşıyor.
  - Popup dolgusu `2.5rem 1.5rem 2rem` → **`1.5rem`** (üstteki fazlalık 80px ikon bloğu içindi, o blok yok).
  - **Onay düğmesi eylemi adıyla söylüyor:** `ConfirmProceedNamed` = "Evet, {0}", argüman
    `actionLabel(action)` — yani **düğmede yazan dizenin ta kendisi**. 7 dil. Önce "Evet, uygula" diyordu;
    o cümle üründeki her onay için doğru, dolayısıyla hiçbiri hakkında bir şey söylemiyordu.
- **CANLI ÖLÇÜM (5 modül, 4 tip):**
  | Ekran | Yükseklik | İkon | Hizalar | Onay düğmesi |
  |---|---|---|---|---|
  | Görev Merkezi · Görevi iptal et | 436→**260px** | 32px `bx-error-circle` | 24/24/24/24 | "Evet, görevi iptal et" |
  | Görev Merkezi · Bilgi bekle (ham, 2 alan) | **351px** | 32px `bx-question-mark` | 7 satır da 24 | Onayla |
  | Kullanıcı Rolleri · sil | **201px** | 32px `bx-trash` | 24/24/24/24 | "Evet, Sil" |
  | Referans Verileri · gerekçe (girdili) | **319px** | 32px `bx-help-circle` | 24/24/24/24 · etiket→alan 4px · `getInput()` true, doğrudan çocuk | — |
  | Rol İzinleri · danger | **201px** | 32px `bx-error-circle`, danger çember | 24/24/24 | `btn-danger` |
  | Görev Merkezi · warning | **201px** | 32px `bx-error`, warning çember | 24/24/24 | `btn-warning` |
- **İki genişlik × iki tema:** 1440 ve 900'de popup 400px, yatay/dikey taşma **0**. Koyu ve açık temada
  çember tonu ve glif rengi `type`'tan geliyor (açık: `rgb(255,62,29)` danger).
- **YOLDA BULUNAN VE DÜZELTİLEN KUSUR:** ikon yuvası gizlenince ham diyalogların çemberi **0px** oldu
  (ölçüldü). Ham diyalog da ikonu başlığına aldı; artık 32px ve başlıkla aynı satırda.
- **KASTEN YAPILMAYANLAR:** 25 ham `Swal.fire` (giriş akışı dahil) — BL-224'teki bekçi listesinde, ayrı tur.
  Ağırlık kademesi — BL-225, ölçüm bekliyor.
- **Gelecek regresyon riski: 🟢** — görünüm tek tanımda, bekçi (BL-224) yeni ham diyalogları durduruyor.

### BL-228 kapanış notu (2026-08-24) — Detay sayfasının son dört maddesi
- **① İPTAL EDİLEN ALT GÖREV — iki mekanizma bire indi.**
  ÖLÇÜM, üç durum ve sinyal sayıları: `bekliyor` 1 (sadece glif) · `tamamlandı` 5 (dolgu + soluk başlık +
  üstü çizili + yeşil kutu + tik glifi) · `iptal edildi` 4 (üstü çizili + **opaklık .55** + soluk kutu +
  x glifi). İkisi aynı şeyi söylüyordu ve `opacity` bunu **metni karartarak** söylüyordu — hâlâ okunması
  gereken bir kayıt satırında.
  Düzeltme yeni bir fikir değil: tamamlanmış satırın kendi sözlüğü (temanın devre-dışı tonu + üstü çizili).
  İptal, tamamlanmıştan **gerçekten farklı iki sinyalle** ayrılıyor: tamamlanma dolgusu YOK, glifi x.
  **KONTRAST (ölçüldü):** açık tema 2.21 → **2.29** · koyu tema 3.06 → **3.49**. İyileşti ama ⚠ ikisi de
  WCAG AA (4.5) altında — sebebi `--bs-secondary-color`'ın kendisi; `tamamlandı` satırı kendi dolgusu
  üzerinde **1.83** ölçüyor. Bu turda tanıtılmadı, bu turda çözülmedi → **BL-229**.
  ⚠ Satır **görünmeye devam ediyor** ve sıralaması değişmedi (dibe iniyor) — sahip kararı.
- **② /Tasks/{id} — ürünün golden desenine getirildi.**
  ÖLÇÜM ÇİZİLEN SAYFADA yapıldı (Razor'da değil):
  | | golden (`GoldenReferenceCompact/Details`) | `/Tasks/{id}` ÖNCE | SONRA |
  |---|---|---|---|
  | breadcrumb | var | **YOK** | var |
  | düğmeler | Geri · Düzenle | **"Kaydet"** → `href=…/Edit` | Geri · Düzenle |
  | `.backbone-preview-field` | 12 | **0** | 8 |
  | `.backbone-preview-section` | 4 | 0 | 1 |
  | `col-md-6` | var | **yok** | var |
  | alan deseni | ikon + etiket üstte + değer altta | `<dl class="row">` 3/9 | golden ile aynı |
  ⚠ **"Kaydet aslında Edit'e link" kusuru DOĞRULANDI ve düzeltildi** — etiket artık "Düzenle"; sayfa
  salt-okunur kaldı, aksiyon EKLENMEDİ (testte: başlıkta tam 2 kontrol, `TasksApi` yazma çağrısı yok).
  ⚠ Başlık/breadcrumb/düğmeler **Razor'a** taşındı, alanlar JS'te kaldı — golden'ın kendi bölüşümü bu.
  ⚠ `WorkCenter/task-detail.js` (bekçinin KNOWN_RAW listesindeki 8 ham Swal.fire) **hiç açılmadı**; bu sayfa
  `Tasks/details-page.js` tarafından çiziliyor. Bekçi yeşil, KNOWN_RAW hâlâ **12 dosya**.
- **③ ENGEL AFİŞİNDEKİ `FS` — kartla aynı muameleye geçti.**
  Afiş `wcn-chip wcn-chip-danger wcn-dep-type` çiziyordu: yüksek sesli kırmızı hap, açılımı YALNIZ `title`
  tooltip'inde. Artık kartın dipnotu (`wcn-dep-abbr`) — küçük, soluk, **cümleden SONRA**. Modülde tek
  kısaltma sınıfı kaldı (testle kilitli).
  ⚠ **Kartın cümleleri (`DepSentence*`) burada KULLANILMADI, gerekçesiyle:** onlar İLİŞKİYİ tarif eder
  (şu an ısırsa da ısırmasa da). Afiş CANLI bir engeli tarif ediyor ve cümlesini "hangi eylemi durduruyor"
  yan cümlesiyle (`BlockedAffects*`) eşliyor. Kartın cümlesini koymak kuralı iki kez söyleyip şu an önemli
  olan yarısını düşürürdü. **Aynı kısaltma muamelesi, farklı cümle — bilerek.**
  ⚠ **CANLI GÖRÜLEMEDİ:** hiçbir fixture ve hiçbir canlı öğe `blocker.dependencyType` taşımıyor (kanonik
  engelli fixture `code: 'DEPENDENCY_BLOCKED'` veriyor ama tür vermiyor), dolayısıyla afişteki kısaltma
  bugün hiç çizilmiyor. Değişiklik testle korunuyor, ekranda gözlenemedi → **BL-230**.
- **④ ERTELE PLACEHOLDER'I — referans kendi kuralına uydu.**
  Etiketi yoktu ve "Hangi tarihe kadar" placeholder olarak kullanılıyordu. Artık diğer tarih diyaloglarının
  kullandığı çift: etiket `SnoozeUntilLabel`, placeholder `DatePlaceholder` ("YYYY-AA-GG"). Yeni dize
  yazılmadı. `SnoozeDatePlaceholder` ("YYYY-MM-DD", yerelleştirilmemiş) artık **kullanılmıyor** → BL-230.
  ⚠ **ÜÇ YARA CANLI DOĞRULANDI:** `.swal2-input` popup'ın **doğrudan çocuğu** ✓ · `Swal.getInput()` null
  **değil** ✓ · açılışta **odak girdide** ✓. Ayrıca yeni tasarım: ikon 32px başlık satırında, etiket→alan
  4px, düğmeler iki uçta.
- **MUTASYON (4, hepsi kırmızı):** iptal satırına opaklık geri · Razor'dan breadcrumb düşürüldü · afiş çipi
  tooltip'e döndü · placeholder etiketin tekrarına döndü.
- **ESKİ İDDİAYI SAVUNAN İKİ TEST GÜNCELLENDİ:** `wcn-snooze-dialog` ("etiket değil placeholder" diyordu —
  tersine çevrildi, gerekçesiyle) ve `workcenter-next-detail-page` (`.wcn-dep-type` bekliyordu).
- **Gelecek regresyon riski: 🟢.**

### BL-229 — [YAPILMADI] Ürünün "geri çekilmiş metin" tonu WCAG AA altında
- Ölçüldü (canlı, iki tema): `--bs-secondary-color` üzerine kurulu geri-çekilmiş satırlar —
  iptal edilmiş alt görev **2.29** (açık) / **3.49** (koyu); **tamamlanmış** alt görev kendi dolgusu üzerinde
  **1.83**. AA eşiği normal metin için 4.5.
- Bu ton temanın kendi devre-dışı rengi; elle daha koyu bir gri seçmek aynı kusuru başka mekanizmayla geri
  getirir. Doğru çözüm token seviyesinde ve **bütün ürünü** etkiler.
- BL-228'de tanıtılmadı (opaklık kaldırılınca kontrast **arttı**), sadece görünür oldu.
- **Gelecek regresyon riski: 🟡** — okunabilirlik borcu, her yeni "soluk" satırda büyüyor.

### BL-230 — [KAYIT] İki ölü yol: afişteki kısaltma ve `SnoozeDatePlaceholder`
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24.** İki ölü yol da canlandı: afişteki kısaltma fixture eklendikten sonra çiziliyor, `SnoozeDatePlaceholder` hem resx'te hem çağrıda kullanılıyor.
- **Afişteki `FS` kısaltması hiç çizilmiyor:** hiçbir fixture ve hiçbir canlı öğe `blocker.dependencyType`
  taşımıyor. Kod ve testi hazır, veri yok. Bir fixture eklenirse görünür olur.
- **`SnoozeDatePlaceholder`** ("YYYY-MM-DD", yerelleştirilmemiş) BL-228 ile kullanımdan çıktı; yerini
  `DatePlaceholder` ("YYYY-AA-GG") aldı. Yedi resx'te duruyor.
- İkisi de zararsız; silinmeleri ya da beslenmeleri ayrı bir karar.
- **Gelecek regresyon riski: 🟢.**

### BL-231 kapanış notu (2026-08-24) — Tur A: üçüncü oluşturma kapısı, iki görsel düzeltme, üç fixture
- **İŞ 1 — "Tüm alanlar" kapısı.** ÖLÇÜLEN üç kapı ve alan sayıları: kutuya yaz+Enter **1** (son tarih/öncelik/
  atanan EBEVEYNDEN miras) · panel **5** · `/Tasks/Create` **20**. Kritik olan sayı değil: **`#taskCustomFields`
  yalnız tam formda çiziliyor** ve çalışma anında `TaskFieldDefinition`'dan doluyor (`IsRequired` taşır). Yani
  kiracı zorunlu bir özel alan tanımladığı gün iki kısayol onu TOPLAYAMAZ, tam form toplayabilir. Kısayolu
  büyütmek değil, kapı açmak doğrusu.
  - İlk iki kapı **aynen duruyor** (testle kilitli).
  - Desen **aynalandı, icat edilmedi**: düzenleme panelindeki `SubtaskOpenFullDetail` ile aynı ikincil düğme,
    aynı `bx-link-external` glifi, aynı footer konumu.
  - `TasksController.Create()` iki parametre kazandı: `parent` + `returnUrl`.
  - ⚠ **AÇIK YÖNLENDİRME KAPALI:** `Url.IsLocalUrl(returnUrl)` sunucu tarafında; dışarıyı gösteren değer
    **null**'a düşer ve istemci Görev Merkezi'ne döner. `form-page.js` URL'i kendisi PARSE ETMİYOR (testle
    kilitli: `URLSearchParams`/`location.search` yok) — yani aşağıda kimse kapıyı genişletemez.
  - Ebeveyn, modülün zaten kullandığı alanla taşınıyor: `parentTaskItemId`.
  - **CANLI TAM DÖNGÜ:** panelde "Tüm alanlar" → `/Tasks/Create?parent=…&returnUrl=…` (30 alan, özel alanlar
    bölümü **var**) → başlık+son tarih doldur → Oluştur → **detay sayfasına döndü** → yeni alt görev listede
    **göründü** → sayfa yenilendi → **hâlâ orada**.
- **İŞ 2a — tamamlanmış satırın zemini.** `--bs-secondary-bg` rgb(228,230,232) idi, üstündeki soluk metin
  **1.83** veriyordu. Zemin `--bs-light-bg-subtle`'a alındı (**token'a dokunulmadı** — ürünün her yerinde
  kullanılıyor), iki satır türü için **birlikte**.
  ⚠ **Zemin tek başına yetmedi:** yeni zeminde bile **2.09** çıktı, çünkü kaldıraç zemin değil METİN rengiydi.
  `--bs-body-color`'a geçildi. **SONUÇ (canlı, iki tema):**
  | | açık | koyu |
  |---|---|---|
  | tamamlanmış alt görev | 1.83 → **4.76** | → **6.09** |
  | tamamlanmış kontrol listesi | 1.83 → **4.76** | → **6.09** |
  | iptal edilmiş alt görev | 2.29 → **5.19** | → **6.54** |
  Üçü de AA (4.5) üstünde. **BEDELİ YAZILI:** tamamlanmış satır artık RENKLE geri çekilmiyor — gerek de yok,
  satır "bitti"yi zaten üç ayrı yolla söylüyor (kendi dolgusu, üstü çizili, tik glifi). Renk dördüncü söyleyişti
  ve okumayı zorlaştıran oydu. **BL-229 bu iki satır için kapandı**, ürünün geri kalanı için açık.
- **İŞ 2b — satır yüksekliği.** Alt görev **52px**, kontrol listesi 44px. Sebep: alt görevin "aç" düğmesi
  `btn btn-icon` taşıyordu (temanın 38px kontrol yüksekliği). Sınıf kaldırıldı → 40px, yani **fazla düştü**:
  iki satır FARKLI en-uzun-çocuktan boyutlanıyordu (kontrol listesi 30px taşıma kolu, alt görev 26px eylem).
  İkisine de **ortak taban** verildi; değer kontrol listesinin **ölçülmüş 44px**'i, yani hiçbir şey küçülmedi.
  **SONUÇ: 44 / 44, eşit** (iki tema, iki genişlik). Bilgi kaybı yok: aynı glif, aynı `title`, aynı `aria-label`.
- **İŞ 3 — üç fixture eklendi, hiçbiri bozulmadan.** Üçü de MEVCUT `ISLERIM-WORK-*` görevlerine alan olarak
  eklendi, yeni görev açılmadı; `[FIXTURE]` öneki gerçek kayıtla karışmasın diye.
  - **İptal edilmiş alt görev** (`S5`): önceki turda satırın stili yeniden yazılmıştı ama ne bir fixture ne de
    62 canlı öğe `status: 'cancelled'` taşıyordu — ekranda hiç görülememişti. S1–S4 dokunulmadı.
  - **`blocker.dependencyType`**: afişteki FS kısaltması önceki turda kartın dipnotuna çevrilmişti ama hiçbir
    veri bu alanı taşımadığı için o dal hiç çizilmiyordu.
  - **Gerçek `timeEntries` + `timesheet`**: `timeTracking` yetkisi 62 canlı görevin **0**'ında var, yani zaman
    kartı hiç ekrana gelmemişti. Liste `[]` idi, iki gerçek kayıt kondu.
- **MUTASYON (3, hepsi kırmızı):** `returnUrl` dış URL kabul etsin · zemin eski koyu değere dönsün · alt görev
  düğmesi `btn btn-icon`'a dönsün.
- **REGRESYON (canlı):** diyalog 32px ikon başlık satırında, hizalar 24/24/24/24, düğmeler iki uçta, "Vazgeç" ·
  gerekçe cümlesi eylemini söylüyor · `aria-describedby` çözülüyor · düğmeler y=523/523 · yapışkan ray ·
  bekçi **yeşil**, KNOWN_RAW **12 dosya**, büyümedi.
- **Gelecek regresyon riski: 🟢.**

### BL-232 kapanış notu (2026-08-24) — Tur B: dokuz ölü kart, iki ölü uç, iki boş panel, süre kartı
- **① ÜÇ GÖRÜNÜM MODU DETAYDAN ÇIKARILDI** (`renderCalendar` · `renderKanban` · `renderSplit`). Bunlar detay
  kartı değil: liste alıp sıralayıp kart üretiyorlar, yani **liste sayfasının görünüm modları**. Silinmediler —
  `scratchpad/view-modes.js`'e alındı; liste sayfası turunda bağlanacak → **BL-233**.
- **② EFOR KARTI BAĞLANDI.** ÖLÇÜM: kart baştan beri vardı ve **hiç çizilmemişti**. Veri toplanıyor
  (`FieldEstimateHours`/`FieldSpentHours`) ve saklanıyordu (`TaskItem.EstimateHours`/`SpentHours`), ama
  projeksiyon yalnız tahmini taşıyordu **ve** `taskContext` yetkisi sözleşmenin yetki listesinde **hiç yoktu** —
  yani hiçbir fixture da bildiremezdi. Eklenenler: DTO'ya `SpentHours`, sağlayıcıya koşullu
  `capabilities.Add("taskContext")` (yalnız rakam varsa — yetki, kartın gösterecek şeyi olduğu sözüdür),
  sözleşmeye `taskContext: ['effort']`, mapper'a düz çiftten `item.effort` kurulumu.
  ⚠ **ATAMA GEÇMİŞİ ÇİZİLMİYOR:** `assignmentHistory` mapper'da, sözleşmede ve **tüm backend'de 0 eşleşme**.
  Yarısı veriyle yarısı boş alt başlıkla çizilen kart, okuyucunun "kimse devretmemiş" ile "bunu izlemiyoruz"u
  ayırmasını engeller. Alan listesi → **BL-233**.
  ⚠ **FG-003:** ilerleme çubuğu `style="width:"` kullanıyordu; ürünün kendi `.wcn-progress-{0..100}` kademe
  sınıflarına çevrildi. Kesin oran `aria-valuenow`'da ve yandaki "7.5 / 12" okumasında duruyor.
- **③ ONAY KARTI — ÖLÇÜLDÜ, ④'E KATILDI.** `WorkflowApprovalWorkItemProvider` içinde `Amount`/`LineItem`/
  `Currency` **0 eşleşme**. Onaylar canlı geliyor ama tutar ve kalem taşımıyorlar; kart `item.amount == null`
  kapısında bekliyordu ve o kapı hiç açılmayacaktı. Alan listesi → **BL-233**.
- **④ DÖRT KART SİLİNDİ:** `renderReviewContext` · `renderExceptionContext` · `renderMeetingContext`
  (üçünün de arkasında sağlayıcı ve veri yok) · `renderThread` (yorum/etkinlik kartı bu işi zaten yapıyor).
  **SİLME KANITI:** dördü de `wwwroot/` + `Views/` altında **sıfır eşleşme** (testle kilitli, isimle raporlar).
- **İŞ 2a — `openNew` SİLİNDİ.** Dispatch ona yalnız task/note/meeting/source DIŞINDA bir `data-wcn-new` için
  gidiyordu; DOM'daki dördü de bilinen kind taşıyor → hiçbir tıklama ulaşamıyordu. Yerini başlıktaki Bootstrap
  dropdown almış.
- **İŞ 2b — TOPLU SEÇİM ŞERİDİ SİLİNDİ** (`bulkBar` · `runBulk` · `runBulkWithProgress` · `performBulk` +
  dispatch dalları). `data-wcn-check` **dört yerde okunuyor, hiçbir yerde çizilmiyordu**; seçim sütunu
  olmadan `state.tableSelected` hiç dolamaz, şerit hiç görünemezdi.
  ⚠ **İKİSİ DE GEÇEN TUR GÖRÜNÜM PAKETİNİ ALMIŞTI** — ölü kodun en tehlikeli hâli: bakımlı görünüyor.
- **İŞ 2c — NOTLAR VE AJANDA PANELLERİ SİLİNDİ.** İkisi de kalıcı boştu (besleyen `openQuickNote`/
  `openMeetingForm` geçen turda silinmişti, `state.notes`/`state.meetings` `[]` ile başlayıp hiç yüklenmiyor).
  BL-218 ile birlikte geri gelecekler.
- **İŞ 3 — SÜRE KARTI.** ⚠ **BAŞLAT/DURAKLAT KARTA TAŞINMADI**, ve teşhis bu turda değişti: sayaç bağımsız bir
  kumanda değil, görevin durumunun **yan etkisi** (`start`→işler, `pause`/`complete`→katlanır). Karta kumanda
  koymak, salt-okunur görünen bir kartın içinden yaşam döngüsünü değiştiren **ikinci bir otorite** açardı —
  bu oturumda doküman onayı için tam bu gerekçeyle reddedilmişti.
  Karta eklenenler: **"Süre gir"** (durum değiştirmez, kişisel ölçüm — rail'den alındı, rail'de `logTime`
  filtreleniyor), **durum satırı** ("Devam ediyor — sayaç işliyor" / "Duraklatıldı — sayaç durdu") ve tek
  satırlık **ipucu** ("Sayaç görevin durumunu izler; başlatma ve duraklatma aksiyonlardan yapılır"). 7 dil.
- **CANLI (1440×koyu, 900×açık):** süre kartı 3sa 45dk + durum + ipucu + "Süre gir" ✓ · rail'de "Süre gir"
  **yok** ✓ · efor kartı "7.5 / 12", `wcn-progress-60`, inline stil **yok** ✓ · satır yükseklikleri 44/44 ·
  kontrast done 4.76 / iptal 5.19 · yatay taşma 0.
- **⚠ SAYAÇ YENİLEMEDE KORUNMUYOR — ÖLÇÜLDÜ VE DÜZELTİLMEDİ:** canlı sayaç 37:29 → yenileme → **37:15**, yani
  devam etmedi, baştan başladı. Sebep: mapper `startedAt`'ı `Date.now() - (37 * 60000)` ile **her yüklemede
  yeniden üretiyor** ve `TaskItem`'da sayaç başlangıcı alanı **hiç yok** (DTO yalnız `TimerState` taşıyor).
  TOPLAM korunuyor (saklanan `loggedMinutes`'tan geliyor); tiklayan sayı fixture tiyatrosu → **BL-234**.
- **FIXTURE:** `ISLERIM-WORK-ACTIVE`'e `taskContext` yetkisi + `effort` eklendi, `timesheet` nesnesi
  `loggedMinutes`'a çevrildi (mapper `timesheet`i kendisi türetiyor, elle verilen ezilip kart "0sa 0dk"
  okuyordu — ölçüldü).
- **MUTASYON (3, hepsi kırmızı):** silinen bir render geri kondu · `SpentHours` DTO'dan düşürüldü ·
  `logTime` rail'e geri kondu.
- **REGRESYON:** diyalog 32px/24-24-24-24/iki uçta · gerekçe cümlesi · `aria-describedby` · düğmeler
  y=523/523 · yapışkan ray · bağımlılık cümlesi · "Tüm alanlar" kapısı yerinde · **bekçi yeşil, KNOWN_RAW 12**.
- **Gelecek regresyon riski: 🟢.**

### BL-233 — [ERTELENDİ] Üç kartın alan listesi ve üç görünüm modu
- **Onay kalem tablosu** (`renderApprovalContext`): tutar + para birimi + kalem satırları (hesap · masraf
  merkezi · miktar · birim fiyat · satır toplamı). Sağlayıcı bugün hiçbirini taşımıyor.
- **İnceleme imza geçmişi** (`renderReviewContext`): imzalayan · rol · karar · tarih · not.
- **Sapma kartı** (`renderExceptionContext`): beklenen · gerçekleşen · fark · eşik · gerekçe.
- **Atama geçmişi** (efor kartının çizilmeyen yarısı): devreden · devralan · tarih · gerekçe.
- **Üç görünüm modu** (`renderCalendar`/`renderKanban`/`renderSplit`): `scratchpad/view-modes.js`'te duruyor,
  liste sayfası turunda bağlanacak.
- Gerekçe: kartı yeniden yazmak yarım gün, **alanları yeniden düşünmek günler**.
- **Gelecek regresyon riski: 🟢.**

### BL-234 — [YAPILMADI] Çalışan sayaç sayfa yenilemesinde sıfırlanıyor
- ✅ **KAPANDI — CT DOĞRULADI 2026-08-24 (Tur C).** Tik tak eden gösterge ve saniyelik `setInterval` kaldırıldı; canlı ölçüm: kart yalnız "3sa 45dk girildi" diyor, `wcn-ts-live`/`wcnTimerValue` DOM'da yok. Entity alanı eklenmedi — doğru çözüm MOD-0280'e ait (blueprint, HCM, EA-TBD).
- Ölçüldü: canlı sayaç 37:29 → yenile → 37:15. Devam etmiyor, **yeniden başlıyor**.
- Sebep iki katmanlı: mapper `startedAt: Date.now() - (37 * 60000)` ile sabit bir başlangıç uyduruyor, VE
  `TaskItem`'da gerçek bir sayaç başlangıcı alanı **yok** — DTO yalnız `TimerState` (running/paused) taşıyor.
- Yani tiklayan sayı bugün fixture tiyatrosu; gerçek bir görevde de doğru olamaz.
- Gereken: `TaskItem`'a bir `TimerStartedAt`, projeksiyona taşınması, mapper'ın uydurmayı bırakması.
- TOPLAM etkilenmiyor — o saklanan `loggedMinutes`'tan geliyor ve yenilemede korunuyor.
- **Gelecek regresyon riski: 🟡** — kullanıcı sayaca güvenip yanlış süre bildirebilir.

### BL-234 GÜNCELLEME (2026-08-24, Tur C) — tik tak eden gösterge GEÇİCİ OLARAK KALDIRILDI
- Durum: **[GEÇİCİ ÇÖZÜM UYGULANDI]** — özellik yazılmadı, yalan söylemek durduruldu.
- Kaldırılan: `wcn-ts-live` / `wcnTimerValue` göstergesi ve onu boyayan bir saniyelik `setInterval`.
- **Kalanlar (hepsi gerçek):** TOPLAM (`loggedMinutes`, saklanıyor, yenilemede korunuyor) · görevin DURUMU ·
  **"Süre gir"** — kalıcı olan tek yazma yolu.
- İpucu cümlesi artık dürüst: *"Geçen süre kaydedilmiyor — harcadığınız süreyi elle girin."* (7 dil).
- ⚠ **DURUM CÜMLESİ DE DÜZELTİLDİ:** "Devam ediyor — sayaç işliyor" diyordu; gösterge kalkınca bu cümle bir
  alt satırdaki ipucuyla ÇELİŞİR hâle geldi (biri sayaç işliyor diyor, diğeri kaydedilmiyor). Artık yalnız
  görevin kendi durumunu söylüyor: "Devam ediyor" / "Duraklatıldı". 7 dil.
- ⚠ **ENTITY ALANI EKLENMEDİ, MIGRATION YAZILMADI** (testle kilitli: DTO'da `TimerStartedAt` yok). Doğru çözüm
  **MOD-0280**'e ait (blueprint, EA-TBD) — orada gerçek bir sayaç başlangıcı saklanınca gösterge geri gelir.
- **CANLI:** toplam 3sa 45dk · durum "Devam ediyor" · tik tak **yok** · yenileme öncesi/sonrası **birebir aynı**.
- **Gelecek regresyon riski: 🟢** (artık yanlış bir sayı gösterilmiyor).

### BL-235 — [KAYIT] Beş yetki sözleşmede var, sağlayıcıda yok
- Sağlayıcı (`ResolveCapabilities`) **altı** yetki biliyor: planning · execution · businessContext · checklist ·
  subtasks · dependencies · activity · **taskContext** (bu oturumda eklendi). Aşağıdaki beşinin **kod dalı hiç
  yok** — "veri yok" değil, üretilmiyor.
- | yetki | sözleşmedeki veri | arayüzdeki kart | arkasındaki veri (ÖLÇÜLDÜ) | engel |
  |---|---|---|---|---|
  | `timeTracking` | `timeEntries` | `renderTimesheet` ✓ | backend'de `TimeEntry` **0 eşleşme** | **MOD-0280**'e ait (blueprint, EA-TBD) |
  | `attachments` | `attachments` | `renderAttachments` ✓ | `TaskAttachment`/`IAttachmentStore`/`BlobStorage` → **0 dosya** | üründe **hiç ek deposu yok** |
  | `evidence` | `evidence` | `renderEvidence` ✓ | Task tarafında yalnız **iki `EvidenceRequired` boolean'ı** (kontrol listesi maddesinde); kodun kendi yorumu: *"evidence itself is MOD-0031's"* | görev kanıt İSTEYEBİLİYOR ama saklayacak yeri yok — sahibi **MOD-0031** |
  | `processStages` | `processStages` | **kart yok** (`renderProcess` 0) | `ProcessStage` backend'de **0 eşleşme** | iş süreci kavramı **hiç tasarlanmamış** |
  | `relatedRecords` | `relatedRecords` | `renderRelated` ✓ | yalnız `MaxRelatedRecords = 20` sabiti; `TaskItem`'da alan **0** | sınır var, **alan yok** — sağlayıcı eksik değil, model eksik |
- Gerçek veride 63 görevin hiçbirinde çıkmamalarının sebebi bu.
- **Gelecek regresyon riski: 🟢** (bugün kod yok).

### BL-236 — [KAYIT] `--bs-secondary-color` tokeni AA altında, ürün genelinde
- TARAMA (WorkCenterNext + detay + kaynak sayfası, iki tema): token'ı kullanan **20 ayrı metin**.
  | tema | token | kart yüzeyindeki oran | AA(4.5) altı | 3.0 altı |
  |---|---|---|---|---|
  | açık | `rgb(167,172,178)` | **2.29** | 20/20 | 20/20 |
  | koyu | `rgb(126,127,150)` | **3.49** | 20/20 | 1/20 |
- Yani sorun tek tek birleşimlerde değil, **tokenin kendisinde**: hiçbir kullanım 2.29'un (açık) üstüne
  çıkamıyor. Değeri değiştirmek bir **tasarım sistemi kararı** ve ürünün her ekranını repaint eder → bu turda
  **dokunulmadı**, kaydedildi.
- ⚠ **BİZİM KURDUĞUMUZ TEK KUSURLU BİRLEŞİM DÜZELTİLDİ:** `wcn-subtask-status` tokenin kendi tabanının da
  ALTINDAYDI (2.09 açık / 3.25 koyu), çünkü bu oturumda getirdiğimiz tamamlanmış-satır zemininde duruyor.
  `--bs-body-color`'a alındı → **6.09**. Seçtiğimiz bir zemin, bir metni tokenin tabanının altına itmemeli.
- ⚠ **YANLIŞ POZİTİF, KAYDA GEÇSİN:** taramada en düşük oran (1.83) `wcn-step-label.visually-hidden` — ekran
  okuyucu için var, **görünmüyor**. Kontrast ölçümü orada anlamsız.
- **Gelecek regresyon riski: 🟡** — her yeni "soluk metin" bu borcu büyütüyor.

### BL-184 GÜNCELLEME (2026-08-24, Tur C) — kararsızlık ÜREMEDİ
- Tam süit **art arda üç kez** koşuldu: **10 kırmızı / 1602 yeşil**, üçünde de **birebir aynı** — hiçbir test
  koşudan koşuya değişmedi.
- Yani bu oturumda üç kez araya giren kararsızlık, **bu turdaki hâliyle üremiyor**. Sebebi kesin olarak
  ölçülemedi; en güçlü aday BL-189'du (aynı belgede iki modül örneği, paylaşılan dinleyiciler) ve o bu turda
  **düzeltildi** — kararsızlığın kaybolmuş olması muhtemelen onun yan etkisi, ama **kanıtlanmadı**.
- Madde kapanmıyor: üremeyen bir hata, olmayan bir hata değildir.
- **Gelecek regresyon riski: 🟡.**

### BL-189 KAPANDI (2026-08-24, Tur C) — modül kendi dinleyicilerini söküyor
- Bütün dinleyiciler `document` üzerinde, dolayısıyla ikinci bir boot birincinin ÜSTÜNE biniyordu: tek tık
  `onClick`'i **iki kez**, iki farklı `state` nesnesine karşı çalıştırıyordu.
- Boot'ta `global.__wcnTeardown` çağrılıyor; click/change/input/keydown sökülüyor ve sayaç durduruluyor.
- ⚠ **Bu bir test uyarlaması DEĞİL, üretim davranışı:** bundle'ı iki kez yükleyen ya da yeniden enjekte eden
  herhangi bir sayfa aynı çakışmayı yaşar. Testte önce görülmesi yan fayda.
- Testle kilitli: eklenen her boot dinleyicisinin bir `removeEventListener` karşılığı olmalı.

### BL-222 KAPANDI (2026-08-24, Tur C) — "minimum görünür satır" yazıldı
- Kural `fixture-contract.js`'te **tek yerde**: `MINIMUM_VISIBLE_ROW` + `inTab`'den ÖLÇÜLEREK çıkarılmış dört
  koşul (`catalogVisible !== false` · `!dismissed` · `itemInScope` · `tab` eşleşmesi, `history` hariç terminal
  satırlar gizli).
- ⚠ **AÇIKÇA BİR TARİF, İKİNCİ BİR GERÇEKLEME DEĞİL:** kuralın KOŞTUĞU yer hâlâ `inTab`; ikisi çelişirse
  `inTab` haklıdır ve yorum bayattır. Testle kilitli (`const inTab` tek yerde).
- ⚠ **KENDİ TESTİMİN ZAYIFLIĞI, KAYDA GEÇSİN:** ilk hâli nesne silinmiş olmasına rağmen YEŞİL kaldı — çünkü
  aradığı dizeler açıklayıcı YORUMDA ve dışa aktarma satırında da vardı. Bir yorumun tatmin edebildiği kural,
  hiçbir şeyin zorlamadığı kuraldır. Test artık nesnenin kendisine bakıyor.

### Tur C kapanış notu (2026-08-24) — özet, kendi numarası YOK
- ⚠ Bu başlık `BL-237` diye yazılmıştı ve dosyada BL-237 iki kez sayıldı. Bir özet bir madde değildir;
  numara almaz. Düzeltildi 2026-08-25.
- İş 1 → BL-234 güncellemesi · İş 2 → BL-235 · İş 3 → BL-236 · İş 4 → BL-184/189/222.
- **MUTASYON (2, ikisi de kırmızı):** tik tak gösterge geri kondu · fixture kuralı silindi.
- **TAM REGRESYON (örnekleme yok, canlı):** diyalog ikon 32px (hesaplanan) · dolgu 24px · dört hiza da eşit ·
  düğmeler iki uçta · "Vazgeç" · onay "Evet, görevi iptal et" · gerekçe cümlesi eylemini söylüyor ·
  `aria-describedby` çözülüyor · düğmeler y=523/523 · "Tüm alanlar" kapısı yerinde · rail'de "Süre gir" YOK ·
  satır 44/44 · kontrast 6.09/6.54/6.09 · efor kartı 7.5/12 · bağımlılık cümlesi + sol ok + FS dipnotu ·
  yapışkan ray · silinen 15 fonksiyon sıfır eşleşme · bekçi yeşil, KNOWN_RAW 12 · FG-003 (inline stil yok).
- **SÜİT: 1612 geçti / 9 kırmızı** — dokuzu da Enterprise Strategy, oturum başından beri kırmızı, dokunulmadı.
- ⚠ **EKRAN GÖRÜNTÜSÜ ALINAMADI:** tarayıcı paneli bu turun sonunda boş kare döndürmeye başladı. Süre kartının
  yeni hâli **ölçümle** doğrulandı (DOM), **görüntüyle doğrulanmadı**.
- **Gelecek regresyon riski: 🟢.**

### BL-237 — [ÖLÇÜLDÜ] `pause` geçişi backend'de YOK; "Duraklat" yalnız mock'ta yaşıyor
- CT yaşam döngüsü turunda ölçtü (2026-08-24), gerçek görev `370ab18b`.
- `TasksController` geçiş listesi: `accept · claim · release · plan · start · inquire · submitReview ·
  return · reassign · complete · cancel` — **`pause` YOK.** Tasks uygulama katmanında da eşleşme yok.
- Frontend'de `case 'pause'` (app.js:5767) **yerel/mock durum değişimi**; showcase fixture'ında "Duraklat"
  düğmesi görünüyor ve tarayıcıda "çalışıyor". Gerçek görevde hiç sunulmuyor.
- ⚠ İki sonucu var: (1) süre kartını üstüne kurduğumuz "duraklat → sayaç katlanır" anlatısının **arkası yok**;
  (2) showcase, gerçek veride **imkânsız bir aksiyon** gösteriyor — bu oturumda tam bu sınıfı temizledik.
- Karar gerekiyor: `pause` backend'e eklenecek mi, yoksa mock'tan da kaldırılacak mı? İkisi de olur;
  ikisinin arasında kalmak olmaz.
- **Gelecek regresyon riski: 🟡** — sunulmadığı için kullanıcıyı bugün yanıltmıyor, ama showcase yanıltıyor.

### BL-238 — [ÖLÇÜLDÜ] Alt görev tik kutusu, başlatılmamış satırda yalnızca hata üretebiliyor
- CT ölçtü (2026-08-24), gerçek görev `370ab18b` / alt görev `ea832a9b`.
- Satırdaki tik kutusu doğrudan `complete` çağırıyor. Sunucu **409 `TASK_INVALID_STATE`** dönüyor:
  bir görev `start` edilmeden `complete` edilemiyor.
- Hata DÜRÜSTÇE gösteriliyor — *"Bu görev bu durumdayken tamamlanamaz. Önce başlatın."* (çevrilmiş, ham
  hata değil). Yani sistem doğru davranıyor; kusur davranışta değil, **sunulan yolda**.
- ⚠ Satır menüsünde **"başlat" yok** (yalnız aç · iptal et). Yani bir onay kutusunu işaretlemek için:
  alt görevi aç → başlat → geri dön → işaretle. **Üç gezinme, bir tık için.**
- API ile `start` + `complete` yapıldığında her şey doğru çalışıyor: satır `done`, ebeveynin
  "Bir alt görev hâlâ açık" gerekçesi kalkıyor, **Tamamla etkinleşiyor** (CT canlı doğruladı).
- Seçenekler: (a) tik kutusu gerekiyorsa `start`+`complete`i arka arkaya yapsın; (b) satır menüsüne
  "Başlat" eklensin; (c) başlatılmamış satırda kutu devre dışı olsun ve gerekçesini söylesin.
  CT önerisi: **(a)** — kullanıcının niyeti "bu iş bitti" demek; ara durumu ondan istemek, sistemin kendi
  kuralını kullanıcıya iş olarak geri vermektir.
- **Gelecek regresyon riski: 🟡** — veri bozulmuyor, ama en sık yapılan hareket üç adıma çıkıyor.

### BL-237 güncelleme (2026-08-25) — KAPANDI: `pause` mock'tan kaldırıldı, backend'e EKLENMEDİ
- CT kararı uygulandı. `case 'pause'` ve ona bağlı yerel durum değişimi silindi; `islerim-showcase-fixtures.js`
  ve `canonical-fixtures.js` içinde "Duraklat" sunan her şey kaldırıldı.
- **SİLME KANITI:** `'pause'` → `wwwroot/assets/js/WorkCenterNext/` altında **sıfır** eşleşme (yorumlar hariç
  tutuldu; yorum kaldırmanın gerekçesini kaydediyor, davranışı değil). Test bunu her koşuda ölçüyor.
- Ölü kalan iki şey de gitti: ulaşılamaz `case 'timerPause'` dalı, ve 7 dilden `ActPause` ("Duraklat") +
  `ToastTimerPaused` anahtarları.
- ⚠ **`TimerStatePaused` KALDI ve bu bir kalıntı DEĞİL.** `ResolveExecutionState`, `Waiting` ve `PendingReview`
  yaşam döngülerini `"paused"` diye yansıtıyor — yani bir görev gerçekten duraklamış olabilir, sadece bir
  DÜĞMEYLE duraklatılamaz. Geçişle birlikte bu dalı da silmek, bekleyen her görevde durum satırını boşaltırdı.
  Ölçüldü, silinmedi.
- ⚠ Backend'e eklenmemesinin gerekçesi kodun içine yazıldı: `pause` yeni bir yaşam döngüsü durumu demek — her
  listede, her filtrede yeni bir değer ve bir migration. Bu bir ürün kararı, temizlik değil. Ayrıca işi
  durdurmanın iki dürüst yolu zaten var: **Bilgi bekle** (başkasını bekliyorsunuz, gerekçesiyle) ve **Ertele**.
- **Gelecek regresyon riski: 🟢** — hiçbir şey kaybolmadı; var olmayan bir düğme kaldırıldı.

### BL-238 güncelleme (2026-08-25) — KAPANDI: tik kutusu artık `start` + `complete` yapıyor
- CT'nin (a) seçeneği uygulandı. Kutu, çocuk `not-started` ise önce `start`, sonra `complete` çağırıyor;
  zaten başlamış bir çocukta **tek** yazma yapıyor (gereksiz `start` = ikinci yazma, ikinci denetim kaydı,
  ikinci başarısızlık ihtimali).
- ⚠ İkinci çağrı YENİ sürümü kullanıyor. `start` sürümü artırıyor; eski jetonu tekrar kullanmak `complete`i,
  kullanıcının bir saniye önce kendi yaptığı değişiklik yüzünden eşzamanlılık çakışmasına düşürürdü. Sürüm
  sunucudan **yeniden okunuyor**, `expectedVersion + 1` diye tahmin edilmiyor.
- **CANLI DOĞRULAMA (gerçek görev, fixture değil)** — `0276e51e` / alt görev `eddb350d`:
  `start(v1)→204`, `complete(v2)→204`. Üç ölçüm de tuttu: satır `wcn-subtask-done` / "Tamamlandı" ·
  ebeveynin **"Bir alt görev hâlâ açık"** gerekçesi kalktı · **Tamamla etkinleşti**.
- **BAŞARISIZLIK YOLU (gerçek ret)** — `359bd3ee`, incelemeye bağlı alt görev: `start`→204, `complete`→**409
  `REVIEW_PENDING`**. Kullanıcının gördüğü: *"Alt görev başlatıldı ama tamamlanamadı: Görev, incelemeyi yapan
  kişinin yanıtını bekliyor."* Satır dürüstçe "Devam ediyor" kalıyor — çocuk gerçekten çalışır durumda ve
  kullanıcı bunu istememişti, o yüzden her iki yarı da söyleniyor.
- **Gelecek regresyon riski: 🟢** — sunucu hâlâ tek karar verici; istemci yalnızca aynı hedefe giden yolu
  yürüyor.

### BL-239 — [DÜZELTİLDİ 2026-08-25] Alt görev bildirimi ham `{1}` yer tutucusu gösteriyordu
- Canlı ölçümde ekranda okunan: **`{1} — 'BL-238 hata yolu — orta' işlemi tamamlandı`**.
- Neden: `ToastActionApplied` İKİ argüman istiyor (`{1} — '{0}' işlemi tamamlandı`, yani aksiyon etiketi +
  başlık); alt görev çağrı yeri yalnızca başlığı veriyordu.
- ⚠ Uyarı zaten kodun içinde yazılıydı — `afterPhase2Write`'ın kendi yorumunda: *"bir argüman eksik vermek
  ikincisinin yer tutucusunu bastırır, bu da ham-anahtar hatasının başka bir şapkayla dönmüş hâlidir."*
  Yorum uyarıyordu, hiçbir şey zorlamıyordu. **Bu, kusurun kendisiydi, aynı sınıftan.**
- Düzeltme: tek olguluk cümleye kendi tek argümanlı anahtarı verildi — `ToastSubtaskCompleted`, 7 dil.
  Test hem anahtarın kullanıldığını hem de 7 dilde değerin `{1}` İÇERMEDİĞİNİ ölçüyor.
- **Gelecek regresyon riski: 🟢**

### BL-240 — [DÜZELTİLDİ 2026-08-25] `REVIEW_PENDING` haritasız; kullanıcı "bir hata oluştu" okuyordu
- İlk ret ölçümünde sunucu **409 `REVIEW_PENDING`** dedi, ekranda **"İşlem sırasında bir hata oluştu."** çıktı.
- Mekanizma doğru çalışıyordu: `failureMessage` haritasız kodu genel cümleye düşürüyor **ve konsola yazıyor**,
  tam da bulunabilir kalsın diye. Eksik olan mekanizma değil, **haritanın kendisiydi** — üç halka birden:
  `REASON_CODE_MESSAGE_KEYS` · `BLOCKING_REASON_CODES` · `_IndexL10n.cshtml` yükü.
- ⚠ `APPROVAL_PENDING` cümlesi ödünç ALINMADI. Sunucunun kendi doc-yorumu gerekçeyi söylüyor: iki kapıyı
  **farklı kişiler** açar; incelemeci işi tutarken kullanıcıya "onay bekleniyor" demek onu yanlış kişiye
  yollar. Yeni anahtar `ErrorReviewPending`, 7 dil. Test iki cümlenin aynı olmadığını da ölçüyor.
- Yeniden ölçüldü (`359bd3ee`): kullanıcı artık *"…Görev, incelemeyi yapan kişinin yanıtını bekliyor."* okuyor.
- **Gelecek regresyon riski: 🟢**

### BL-241 — [ÖLÇÜLDÜ, AÇIK] Engelleyici kontrol listesi maddesi `complete`i engellemedi
- ✅ **KAPANDI — KUSUR YOK, CT ÖLÇTÜ 2026-08-24.** Kural sunucuda UYGULANIYOR: `TaskItemTransitionHandlers.cs:261` — `Done` hedefinde `BlocksCompletion(checklist)` doğruysa **409 `ChecklistIncomplete`**. Kuralın kapsamı da kodun kendi yorumunda yazılı: *"Only **Blocking** items gate completion — an unfinished `Required` item is an expectation, not a barrier."* Yeni eklenen maddenin varsayılan seviyesi **`Optional`** (`diten-checkitem.js:102`) ve ekleme satırındaki çip bunu ekranda "İsteğe bağlı" diye söylüyor. Yani seviyesi değiştirilmemiş bir madde engellemez ve `204` **beklenen** cevaptır. Ekran zorunlu olmayan bir adımı zorunlu göstermiyor.
- Ölçüm (2026-08-25, görev `de76acfa`): `addChecklistItem` ile `{ text, required: true, blocking: true }`
  eklendi (204 döndü), ardından aynı görev `complete` edildi — **204, ret yok**.
- ⚠ Bu tek başına kanıt DEĞİL: `addChecklistItem` isteğinin `required`/`blocking` bayraklarını okuyup
  okumadığı ölçülmedi; bayraklar sessizce düşmüş de olabilir. İki olasılıktan hangisi olduğu bilinmiyor,
  bu yüzden kapatılmadı ve "kural uygulanmıyor" diye YAZILMADI.
- Yapılacak ölçüm: maddeyi ekledikten sonra projeksiyonun `checklist.items[]` çıktısında `blocking: true`
  görünüyor mu; görünüyorsa `CHECKLIST_INCOMPLETE` neden dönmüyor.
- `CHECKLIST_INCOMPLETE` istemci tarafında zaten haritalı ve engelleyici sayılıyor — yani kopuk halka
  istemcide değil.
- **Gelecek regresyon riski: 🟡** — eğer kural gerçekten uygulanmıyorsa, ekran zorunlu bir adımı zorunluymuş
  gibi gösteriyor demektir; veri bozulmaz ama söz tutulmaz.

### BL-242 — [DÜZELTİLDİ 2026-08-25] Kapalı birincil aksiyonun gerekçesi düğmeye BAĞLI değildi
- Ölçüm (gerçek görev `f5d31d28`, kapalı "Tamamla"): gerekçe `<p>` ekranda, `role="note"` ile — ama `id` boş,
  düğmede `aria-describedby` yok. Gören okuyucu nedeni öğreniyordu; ekran okuyucu kullanan **yalnızca
  "Tamamla, kapalı" duyuyordu.**
- Kod bilerek böyleydi: birincil katmanın cümlesi kendi `<li>`'sinin içinde, düğmenin hemen altında duruyor —
  "yakınlık yeterli" varsayımı. ⚠ Yakınlık **görsel** bir argüman; sesli okunduğunda ayakta kalmıyor.
- Düzeltme: birincil de artık `aria-describedby` taşıyor; id yardımcı işlevden geliyor (ikincil/yıkıcı katmanlar
  zaten kullanıyordu, çakışma yok). Canlı doğrulandı: id tekil, işaret ettiği metin "Bir alt görev hâlâ açık".
- **Gelecek regresyon riski: 🟢**

### BL-243 — [DÜZELTİLDİ 2026-08-25] Yapışkan alt raydaki aynı düğme KAPALI GÖRÜNMÜYORDU
- Ölçüm (900px, `f5d31d28`): karttaki kapalı "Tamamla" `opacity: .55`; **yapışkan raydaki aynı düğme
  `opacity: 1`** — tam parlak yeşil, kendi "Bir alt görev hâlâ açık" cümlesinin hemen altında tıklanabilir
  görünüyor. `disabled` niteliği ikisinde de vardı; eksik olan yalnızca **görünüm**.
- Neden: sönükleştirme `.wcn-act-disabled .wcn-act-btn` kuralına, yani sarmalayan `<li>`'ye asılıydı — o sınıfı
  sadece KART yolu yazıyor. Ray başka bir render yolu, sarmalayıcısı yok.
- Düzeltme kuralı düğmenin **kendi durumuna** taşıdı: `.wcn-act-btn:disabled { opacity: .55 }`. Böylece bugün
  yazılmamış render yolları da kapsanıyor. Sarmalayıcı kuralı, `:disabled` alamayan `<a>` çeşidi için kaldı.
- Raya kendi gerekçe id'si verildi (`…-complete-bar`): ray `d-lg-none`, yani 992px üstünde gizli ama **DOM'da**,
  iki cümle her sayfada bir arada. Aynı id çift id demekti; `getElementById` kartınkini döndürürdü.
- ⚠ Bu, oturumda üçüncü kez aynı sınıf: **bir yüzeyi düzeltip ikizini unutmak.** Bu yüzden düzeltme JS'te değil
  CSS'te, davranışın kaynağında yapıldı.
- **Gelecek regresyon riski: 🟢**

### BL-208 güncelleme (2026-08-25) — KAPANDI: menüdeki devre dışı madde gerekçesini söylüyor
- **SEBEP, tahmin edilenlerden hiçbiri değildi.** Gerekçe "geçilmiyor" da değildi, "çizilip gizleniyor" da:
  yapışkan şeridin menüsü `actionMenuLi`'yi **hiç çağırmıyordu**. Kendi `<li><button class="dropdown-item">`
  şablonunu elle yazıyordu ve o şablon yalnızca ETİKET taşıyordu. Yani gizlenecek bir gerekçe yoktu —
  gerekçe kavramından hiç haberi olmayan **ikinci bir render yolu** vardı. `wcn-menu-reason` bu sırada
  uygulamadaki diğer BÜTÜN menülerde doğru çalışıyordu.
- ⚠ Bu, BL-243'ün ve oturumdaki diğer ikisinin aynısı: **bir yüzeyi düzeltip ikizini unutmak.** O yüzden
  düzeltme "şeride gerekçe eklemek" değil, şeridi paylaşılan satıra bağlamak oldu — menü satırlarına bundan
  sonra eklenecek şey ikinci kez hatırlanmayı beklemiyor.
- Kilit iki farklı türetimle hesaplanıyordu (`state.submittingItemId === item.id` ile
  `interactionLocked = !!submittingActionCode`). Paylaşılan satır artık kilidi isteğe bağlı olarak **dışarıdan
  alıyor**; şerit kendi cevabını geçiyor, mevcut çağıranların davranışı değişmedi.
- Erişilebilirlik: menü satırı kendi gerekçesine `aria-describedby` ile bağlandı. Id `-menu` ekiyle ayrıldı —
  detay sayfası aynı aksiyonu **üç kez** çizebiliyor (kart rayı · şeridin ana düğmesi `-bar` · bu menü).
  Canlı ölçüm (`848a624f`): dört gerekçe, dört tekil id, çift yok.
- ⚠ **KIRPILMIYORDU AMA OKUNMUYORDU.** `white-space: normal` zaten vardı, `max-inline-size: 15rem` bir TAVAN.
  Ölçüm: cümleye 97px düşüyordu — etiketlerin ihtiyaç duyduğu genişlik neyse o — ve "Bu görev devredilemez."
  iki-üç kelimelik bir sütuna sarıyordu. Kırpık değildi, sadece okunmuyordu; tavan bunu göremezdi. `13rem`
  **taban** eklendi: menü içeriğine göre büyüdüğü için içeride kırpmak yerine menüyü genişletiyor.
  Sonuç ölçüldü: menü 274px, cümle tek satır, ekran dışına taşma yok (900px).
- **Gelecek regresyon riski: 🟢**

### BL-207 güncelleme (2026-08-25) — KAPANDI: alt görev engeli afişten düştü, diğerleri kaldı
- **ENGEL TÜRÜ SAYIMI (ölçüldü, varsayılmadı).** Sağlayıcı `ResolveBlockers` ile tam **iki** kod üretiyor.
  Kontrol listesi, onay ve inceleme `blockedState`'e **hiç ulaşmıyor** — onlar `Gates` ve aksiyonun kendi
  `disabledReason`'ı üzerinden konuşuyor. Yani afişin baştan beri iki ailesi vardı:

  | Engel kodu | Afiş çiziyordu | Sayfada kendi kartı | Kart engeli adıyla + düzeltmesiyle gösteriyor mu | Karar |
  |---|---|---|---|---|
  | `SUBTASK_BLOCKED` | evet | ALT GÖREVLER | **evet** — çocuk adıyla, tik kutusuyla, kendi sarı satırıyla | **afişten DÜŞ** |
  | `DEPENDENCY_BLOCKED` | evet | BAĞIMLILIKLAR | **hayır** — ilişkiyi ve karşı görevin durumunu gösterir, ama "şu an hangi eylemi durdurduğunu" söylemez (`BlockedAffects*` yalnız afişte var) | **afişte KAL** |
  | `CHECKLIST_INCOMPLETE` · `APPROVAL_PENDING` · `REVIEW_PENDING` | **hayır, hiç** | — | — | afişi ilgilendirmiyor |

- Uygulama tek bir kümeye asıldı (`BANNER_SUPPRESSED_CODES`), tek bir kodun özel durumuna değil: ileride kendi
  kartını kazanan aile aynı yoldan düşer, kazanmayan cümlesini burada tutar. **Afiş silinmedi.**
- Karışık kümede yalnızca bastırılan kod düşüyor, kalanı tam liste olarak çiziliyor (test ediyor).
- Boş afiş kutusu bırakılmıyor: geriye engel kalmazsa hiç çizilmiyor.
- Ölü kalan her şey gitti: `allSubtasks` tek-satır dalı, `data-wcn-goto-subtasks` tıklama işleyicisi, CSS'i
  (`wcn-blocked-oneline` · `wcn-blocked-goto`) ve 7 dilden `BlockedSubtaskOneLine` + `BlockedGoToSubtasks`.
- ⚠ **ALTI TEST ESKİ KARARI YAZIYA DÖKMÜŞTÜ** ve kırmızıya döndü. Silinmediler — yeni kuralı ölçecek şekilde
  değiştirildiler, her birinde kararın nasıl iki kez değiştiği yazılı. Sayfanın **söylememesi gereken** şey de
  en az söylemesi gereken kadar kuraldır.
- ⚠ **BL-104 BU KARARLA CEVAPLANDI**, açıkta bırakılmadı: şikâyeti, toplanan satırın tek engeli artık ADIYLA
  anmamasıydı. İsim afişe hiç ihtiyaç duymuyordu — alt görev kartı onu, temizleyen tik kutusunun yanında
  zaten taşıyor.
- Canlı ölçüm (`848a624f`, 900px ve 1440px, iki tema): afiş yok · düğmenin gerekçesi yerinde · alt görev
  kartı adıyla ve kutusuyla yerinde.
- **Gelecek regresyon riski: 🟢**

### BL-219 güncelleme (2026-08-25) — KAPANDI: toplantı diyaloğu ne yaptığını söylüyor
- Eski cümle: *"İnceleme toplantısını takvime yazar; son tarih değişmez."* — **takvime hiçbir şey yazmıyor.**
  `applyReviewMeeting` `state.meetings`'e ekliyor ve `item._fixture`'ı yeniden yansıtıyor; sunucuya hiçbir şey
  gitmiyor, yenilemede kayboluyor.
- Yeni cümle üç olguyu da adıyla söylüyor — bağlantı kurulmadı · kayıt yalnız bu ekranda · kapatınca kaybolur —
  ve son tarih notunu koruyor. **7 dil.** Test hem eski iddianın 7 dilde de gitmiş olduğunu, hem üç işaretin
  7 dilde de bulunduğunu ölçüyor.
- ⚠ AKSİYON SİLİNMEDİ, sayaç ve `pause`'un aksine: ilan edilmiş sözleşmesi var
  (`reviewMeetingPolicy`, WorkAggregationModels.cs:832). **Yalan söylemeyi bırak, özellik yazma.**
- Canlı doğrulandı (`INBOX-REVIEW-REQUIRED-MEETING?fixtures=showcase`): diyalog yeni cümleyi gösteriyor.
- **Gelecek regresyon riski: 🟢**

### BL-244 — [SAYIM, DÜZELTİLMEDİ] `state.*` üzerine yazıp sunucuya hiç gitmeyen yollar
- CT iki tane bulup silmişti, üçüncüsü BL-219'du. **Dördüncüsü var** — ve beklenenden büyük.
- Sayım (`wwwroot/assets/js/WorkCenterNext/app.js`), dört yol:
  1. `applyReviewMeeting` → `state.meetings.push` — **BL-219, bu tur dürüstleştirildi** (silinmedi: sözleşmesi var).
  2. `applyTransition` içindeki `case 'accept'` / `itemType === 'meetingInvite'` → `state.meetings.push`.
     ⚠ **Kusur değil:** çağıranı `isRealTaskItem` ile çitli; gerçek görevler sunucuya gidiyor, fixture olmayan
     her şey için konsola "MOCK transition" uyarısı basılıyor. Yapı gereği dürüst.
  3. `createSelfTask`'ın fixture dalı → `state.items.push`. Aynı şekilde showcase'e çitli, kendi yorumunda yazılı.
  4. **`addGlobalNote` → `state.notes.unshift`, VE ULAŞILAMAZ.** Ölçüm: beş kanca hiç çizilmeyen işaretlemeyi
     dinliyor — `data-wcn-toggle` · `data-wcn-global-note-input` · `data-wcn-global-note-add` ·
     `data-wcn-note-convert` · `data-wcn-meeting-followup` — beşinin de `="` ile çizim sayısı **0**.
     Tur B'de `renderNotes`/`renderAgenda` silindiğinde işleyicileri kaldı. Yanlarında `state.notesOpen`,
     `state.agendaOpen` ve URL'in `panel` parametresi de yaşıyor.
- ⚠ CT'nin talimatı gereği **düzeltilmedi, kaydedildi**. Temizlenirse tek bir işte temizlenmeli: beş kanca,
  iki durum alanı, `panel` URL parametresi ve `convertGlobalNote`/`createMeetingFollowup` işlevleri.
- **Gelecek regresyon riski: 🟢 (ulaşılamaz kod kullanıcıya görünmüyor) / 🟡 bakım** — silinmiş bir yüzeyin
  işleyicisi, kodu okuyan için hâlâ var olan bir özellik gibi duruyor.

### BL-245 — [ÖLÇÜLDÜ, AÇIK] `cancel` menüde genel ok ikonuyla çiziliyor
- Ölçüm (`848a624f`, şerit menüsü): `inboxActionIcon` haritasında `cancel` yok, geri düşüş
  `bx-right-arrow-alt` — yani "Görevi iptal et" **ileri oku** takıyor. Metin kırmızı, ikon nötr ve yanlış yönde.
- ⚠ `inquire: bx-question-mark` DOKUNULMADI: sahibin bir tur önce onayladığı ikon.
- İkon seçimi sahibin ilgilendiği bir karar olduğu için düzeltilmedi, öneri olarak bırakıldı: `bx-x` veya
  `bx-block`. Sahibin seçmesi gerekiyor.
- **Gelecek regresyon riski: 🟢**

### BL-245 güncelleme (2026-08-25) — KAPANDI: `cancel: 'bx-x-circle'`
- CT kararı uygulandı, **haritaya** eklendi (`inboxActionIcon`), çağrı yerinde seçilmedi.
- `reject` ve `decline` zaten bu glifi taşıyordu; üçü bir aile — "bu iş ilerlemiyor". Anlamı ETİKET taşıyor,
  ikon TONU. Reddedilenler koda yazıldı: `bx-x` (kapat/vazgeç, kapatma düğmesinin glifi) · `bx-block`
  (yasak; iptal bir karar, yasak değil).
- **CANLI ÖLÇÜM:** listede dört menü birden açıldı — 40 menü satırı, **kod başına tam bir ikon**:
  `cancel → bx-x-circle` · `inquire → bx-question-mark` · `plan → bx-calendar-plus` · `reassign → bx-user-pin`.
  Aynı kodun iki ikonlu olduğu tek bir yer yok.
- ⚠ **RAYDA İKON YOK** — "ikisinde de aynı" koşulu rayda karşılanamıyor çünkü ray ikon çizmiyor. Ölçüldü:
  `complete` · `inquire` · `reassign` · `cancel`, dördü de metin-yalnız (`wcn-act-btn` içinde tek `<span>`).
  Bu katman tasarımı, bu turda değiştirilmedi.
- ⚠ **DİYALOG FARKLI BİR GLİF KULLANIYOR VE BU KUSUR DEĞİL.** "Görevi iptal et" onay diyaloğu
  `bx-error-circle text-danger` gösteriyor. Bu glif aksiyondan değil, diyaloğun TÜRÜNDEN geliyor
  (`_GlobalConfirmation` → `iconHtml(type)`), yani ürünün tamamındaki ~74 onay diyaloğuyla ortak dil:
  daire "bu yıkıcı bir onaydır" der, menü ikonu "bu aksiyon bir iptaldir" der. **İki eksen, eksen başına tek
  glif.** Birini diğerine benzetmek, sahibin onayladığı tek-diyalog-dili kararını bozardı.

#### İKON HARİTASI SAYIMI (CT kararı bekliyor — bu turda doldurulmadı)
- Çizilebilir kod sayısı **26** (sunucunun `BuildActions`'ı + showcase fixture'ları). Haritada **12**.
  **14'ü varsayılana (`bx-right-arrow-alt`, genel ileri oku) düşüyor:**

  | Kod | Kaynak | Kod | Kaynak |
  |---|---|---|---|
  | `claim` | sunucu + fixture | `acceptMeeting` | fixture |
  | `release` | sunucu | `acceptOffer` | fixture |
  | `start` | sunucu + fixture | `declineMeeting` | fixture |
  | `resume` | sunucu + fixture | `delegate` | fixture |
  | `complete` | sunucu + fixture | `dispute` | fixture |
  | `submitReview` | sunucu | `replan` | fixture |
  | *(`cancel` bu turda dolduruldu)* | — | `resolve` | fixture |

- ⚠ Ters yönde bir bulgu daha: **`reviewMeeting` haritada var, hiçbir kaynak üretmiyor** — ölü harita girdisi.
- ⚠ Bunların hepsi her yerde görünmüyor: ray ikon çizmiyor, yani varsayılan glif yalnız MENÜ satırlarında ve
  onay diyaloğunun başlığında görünür. Yine de `complete` ve `start` gibi en sık kodların menüde genel ileri
  oku takıması, `cancel` ile aynı sınıftan bir kusur.
- **Gelecek regresyon riski: 🟡** — yanlış bilgi vermiyor ama ikon dili yarım; okuyucu şekilden bilgi almayı
  bırakıyor.

### BL-244 güncelleme (2026-08-25) — KAPANDI: ulaşılamaz beş kanca ve taşıdıkları her şey silindi
- Silinenler: beş kanca (`data-wcn-toggle` · `data-wcn-global-note-input` · `data-wcn-global-note-add` ·
  `data-wcn-note-convert` · `data-wcn-meeting-followup`), üç işlev (`addGlobalNote` · `convertGlobalNote` ·
  `createMeetingFollowup`), iki durum bayrağı (`state.agendaOpen` · `state.notesOpen`), taşıdıkları veri
  (`state.notes`, `buildNotes`, `NOTES` fixture'ı) ve `panel` URL parametresi — hem okuması, hem yazması,
  hem beyaz listesi.
- **SİLME KANITI:** on iki adın hepsi `wwwroot/` altında **sıfır** eşleşme (yorumlar hariç tutuldu). Test
  her koşuda ad ad ölçüyor, hangisinin geri geldiğini adıyla söylüyor.
- ⚠ **KİŞİSEL NOT KARTINA DOKUNULMADI** ve bu teste yazıldı: `data-wcn-note-input` / `-note-add` / `-note-save`
  ile `addPersonalNote` duruyor ve **çizildiği** de ölçülüyor (`data-wcn-note-add="${item.id}"`), sadece
  anıldığı değil. İki ad bir kelime farkla ayrılıyor ve birbirleriyle ilgileri yok.
- ⚠ İki test bu silmeden düştü ve ikisi de kuralı koruyacak şekilde onarıldı:
  1. `buildNotes()` üzerindeki fixture-kapısı iddiası kaldırıldı (kapatılacak bir üretici kalmadı); kuralın
     kendisi var olan her üretici için aynen duruyor.
  2. `openCreateInSource` dilimi **üçüncü kez** bir komşunun adına bağlı olduğu için koptu — önce
     `openMeetingForm`, sonra `createMeetingFollowup` silinmişti. Her seferinde `indexOf` -1 döndü ve dilim
     DOSYANIN SONUNA kadar uzadı; ikinci sefer gürültülü koptu, üçüncüsünün kopacağının garantisi yoktu.
     **Kaybolmasına izin verilen bir koda çakılı pencere, pencere değildir**: dilim artık adı ne olursa olsun
     aynı girinti düzeyindeki bir sonraki bildirimde bitiyor, ayrıca boş-değil ve dosya-değil diye iki kez
     ölçülüyor.
- **BL-218 GÜNCELLEMESİ:** o kayıt "ertelendi, silinmedi" diyordu — artık kod da yok. Paneller geri geldiğinde
  **render'ları, kancaları, durum alanları ve URL parametresi yeniden yazılacak**; geriye yalnızca niyet kaldı.
- **Gelecek regresyon riski: 🟢** — silinen hiçbir şeye kullanıcı erişemiyordu.

### BL-245 güncelleme (2026-08-25) — yaşam döngüsü fiilleri haritaya alındı
- CT kararı: `start`+`resume → bx-play` · `complete → bx-check-double` · `claim → bx-user-plus` ·
  `release → bx-user-minus` · `submitReview → bx-send`. Kalan yedisi (acceptMeeting · acceptOffer ·
  declineMeeting · delegate · dispute · replan · resolve) **bilerek** varsayılanda; gerekçe koda yazıldı.
- Ölü `reviewMeeting` girdisi silindi (`scheduleReviewMeeting` canlı olan).
- ⚠ **GLİF VARLIĞI ÖLÇÜLDÜ**, ada bakılmadı: test artık her glifi `iconify-icons.css` içinde arıyor.
  Mutasyonla kanıtlandı — `bx-send` yerine uydurma bir glif konunca test kırmızıya döndü.
- **CANLIDA GÖRÜLEN DÖRT**: `bx-play` (Başlat) · `bx-check-double` (Tamamla) · `bx-send` (İncelemeye gönder) ·
  `bx-user-plus` (Havuz/Üstlen). ⚠ **`release` ve `resume` GÖRÜLEMEDİ** — 76 canlı görevin hiçbirinde ve
  hiçbir fixture'da yok. "Doğrulandı" yazılmadı.
- ⚠ Ara ölçümde bir kez yanıldım ve düzelttim: detay sayfasının rayı ikon çizmiyor, ama **liste satırının
  birincil düğmesi çiziyor** — beş glifin görünür olduğu yüzey orası.

### BL-246 — [DÜZELTİLDİ 2026-08-25] Ertelenmiş öğe sinyal çipinin sayısına sızıyordu
- Ölçüm: İşlerim'de "SLA riski **14**", altındaki segmentlerin toplamı **13** — fark tam olarak bir ertelenmiş
  satır (CT scroll testi, 2026-09-30).
- **SEBEP, ve neden yalnız BİR sayaç sızdırdı:** erteleme gizlemesi `passesFilters` içinde
  `except !== 'signal'` arkasında duruyor; `signalCount` ise sinyal eksenini atlayan tek faset. Tür ve segment
  sayaçları kuralı hep çalıştırıyordu ve hep doğruydu. Yani eksik bir kural değil, **kuralın üstünden atlayan
  bir faset** vardı.
- ⚠ **BL-045 TASARIMINA DOKUNULMADI.** Çipin segmentten bağımsız sayması bilerek verilmiş karardır ve öyle
  kaldı; çipe basınca segmentlerin yeniden hesaplanması da çalışmaya devam ediyor.
- ⚠ "Ertelenmiş" çipinin KENDİ sayacı ertelenmişleri saymaya devam ediyor — o çip onları açığa çıkarmak için
  var; 0 yazıp bir satır açan çip aynı yalanın tersidir. Kural `signalCount` içinde tek bir istisnayla yazıldı.
- ⚠ Çipin DAVRANIŞI değişmedi, yalnız ARİTMETİĞİ: `snoozed` açıkken `parkedOffScreen` herkese false döndüğü
  için sayaçlar listenin açığa çıkmış hâlini kendiliğinden takip ediyor.
- **KANIT TABLOSU** (çip sayısı = o çipe basınca segmentlerin toplamı) — üç sekme × her sinyal, **6/6 eşit**:

  | Sekme | Çip | Sayaç | Segment toplamı |
  |---|---|---|---|
  | Gelen Kutusu | SLA riski | 15 | 15 |
  | İşlerim | Bloke | 4 | 4 |
  | İşlerim | **SLA riski** | **13** (önce 14) | **13** |
  | İşlerim | Ertelenmiş | 1 | 1 |
  | Geçmiş | SLA riski | 10 | 10 |
  | Geçmiş | Ertelenmiş | 1 | 1 |

- **Gelecek regresyon riski: 🟢**

### BL-247 — [DÜZELTİLDİ 2026-08-25] İki çip satırı, iki farklı birleşme kuralı
- Ölçüm: tür ekseni OR (`typeFilter.has`), sinyal ekseni AND (`for … if (!TEST) return false`). Aynı ekranda,
  aynı görünümde, iki farklı mantık. Canlı sonuç: Bloke(4) + SLA(7) = **1**.
- Sinyal filtresi **OR** oldu; eksenler arası AND korundu (tür ∧ sinyal ∧ modül …).
- **Ölçüm sonrası:** Bloke(4) ∪ SLA(13) = **16** — yani biri iki sinyali birden taşıyor. URL yazma/okuma
  bozulmadı (`signals=blocked,sla-risk`), testle kilitlendi.
- Gerekçe koda yazıldı: bir sinyal "neye dikkat etmeliyim" sorusunu yanıtlar; iki tanesini seçmek **daha geniş**
  bir ağ ister, daha dar değil. Kesişim FARKLI sorular arasında doğrudur.
- **Gelecek regresyon riski: 🟢**

### BL-248 — [DÜZELTİLDİ 2026-08-25] Sayacı sıfır olan tür çipi çizilmeyecek
- Ölçüm: Gelen Kutusu'nun 7 tür çipinden **6'sı** gerçek veride 0 (Onay · İnceleme · Sorun · İstisna ·
  Toplantı Daveti), hepsi tıklanabilir, hepsi boş listeye götürüyor. Canlıda 7 çip → **3 çip**.
- Eski yorum "never dimmed at 0 (no perpetual grey chips)" diyordu: teşhis doğru, çözülen yarı yanlış. **Gri
  hiç sorun değildi; VAAT sorundu.**
- ⚠ "Tümü" çipi istisna olarak kaldı — o eksenin sıfır durumudur.
- ⚠ **KALICI GİZLEME DEĞİL** ve bu koda yazıldı: sayaç canlı projeksiyondan geliyor, başka bir modül inceleme
  ya da sorun göndermeye başladığı anda çip kendiliğinden geri gelir. Kimsenin geri koyması gerekmiyor.
- ⚠ İkinci bir mekanizma yazılmadı: sinyal çipleri ve diğer sekmelerin tür çipleri zaten
  `sayaç > 0 || filtre açık` kuralını kullanıyordu; aynısı kullanıldı.
- ⚠ **TESTİN KENDİ KUSURU YAKALANDI:** ilk hâli `buildInboxChips`'in TAMAMINDA guard satırını arıyordu ve o
  satır `riskChips`'te de var — guard tür çiplerinden silindiğinde test YEŞİL kaldı. Bu oturumda ikinci kez
  aynı sınıf: **başka bir satırın tatmin edebildiği kural, hiçbir şeyin zorlamadığı kuraldır.** Test artık
  yalnız `mainChips` bloğuna bakıyor; mutasyon tekrarlandı, kırmızıya döndü.
- **Gelecek regresyon riski: 🟢**

### BL-249 — [DÜZELTİLDİ 2026-08-25] Beş kontrol ailesi klavyeyle görünmezdi
- Ölçüm (GERÇEK Tab ile): `core.css`'teki küresel `button:focus, button:focus-visible { outline: 0 }` her düz
  düğmenin halkasını siliyor. Kendi kuralı olan üçü kurtuluyordu (`.wcn-row` · `.wcn-fchip` · sekme, 2px),
  beşi kurtulmuyordu: satır birincil düğmesi · satır menüsü · segment · görünüm düğmesi · sayfalama.
- Beşine de ürünün kendi halkası verildi: `2px solid var(--bs-primary)`, offset 2px — kurtulanların kullandığı
  değerlerin aynısı. ⚠ `--bs-btn-focus-box-shadow` **boş** ölçüldü; ondan türetmek hiçbir şey çizmezdi.
- ⚠ Küresel kurala DOKUNULMADI — bütün ürünü ilgilendirir, bu sayfanın vereceği karar değil.
- ⚠ **PROGRAMATİK `.focus()` YANILTIR** — `:focus-visible` doğmaz. Denetimde ilk ölçüm böyle yapılıp yanlış
  çıkmış, bu turda gerçek Tab ile hem ölçüldü hem doğrulandı: satır menüsü düğmesinde
  `solid 2px rgb(105,108,255)`, offset 2px.
- Testin kendi dilimi de bir kez yanlış geçti: CSS penceresi ilk `}` ile kesiliyordu ve üstündeki yorum
  `{ outline: 0 }` ifadesini ALINTILIYOR. Pencere kurala bağlandı.
- **Gelecek regresyon riski: 🟢**

### BL-250 — [DÜZELTİLDİ 2026-08-25] Liste satırı ile detay satırı iki ayrı dil konuşuyordu
- Denetimde altı ölçüm ayrışıyordu. **İkisi benim ölçüm hatamdı, ikisi bilerek farklı, ikisi düzeltildi.**

  | Ölçüm | Detay satırı | Liste satırı (önce) | Sonuç |
  |---|---|---|---|
  | yarıçap | 6px | 8px | **eşitlendi → 6px** |
  | hizalama | center | stretch | **eşitlendi → center** |
  | dolgu | 6px 8px | 12px 14px | **bilerek farklı** |
  | yükseklik | 52px | 98px | **bilerek farklı** |
  | kenarlık | 1px opak | 1px saydam | **bilerek farklı** |
  | hover | %3 tint | "renk değişmiyor" | **ÖLÇÜM HATASIYDI — zaten çalışıyor** |

- **Hizalama neden güvenli:** SLA vurgu çubuğu kendi `align-self: stretch`'ini taşıyor, yani ebeveyn
  `center` olunca da tam yükseklikte kalıyor. Hiçbir şey ebeveynin gerilmesine bağlı değildi.
- **Dolgu ve yükseklik neden farklı bırakıldı:** liste satırı iki satırlık (başlık + özet + çip şeridi), detay
  satırı tek satırlık. 6px/8px'e sıkıştırmak, bir sayıyı kazanmak için içeriği kırpmak olurdu. **Yükseklik bir
  kural değil, içeriğin sonucudur.**
- **Saydam kenarlık bilerek:** Sneat `data-skin` anahtarını `.card` gibi yansıtıyor (`bordered` skininde
  renkleniyor ve gölge kalkıyor) ve `:hover`'da zaten renkleniyor. Detay satırı KART İÇİNDE durduğu için opak;
  bu satır SAYFA üstünde durduğu için gölgeli. İki yüzey, iki doğru cevap.
- ⚠ **HOVER ÖLÇÜMÜ YANLIŞTI VE SEBEBİ ÖNEMLİ:** denetim `dispatchEvent(new MouseEvent('mouseover'))` kullanmış;
  bu `:hover` sözde-sınıfını DOĞURMAZ. Gerçek fareyle yeniden ölçüldü: `rgba(105,108,255,.035)` — tam olarak
  detay sayfasının %3 tint'i, üstüne kenarlık rengi. **Bu oturumda üçüncü kez sentetik olay yanlış negatif
  verdi** (programatik `.focus()`, `mouseover`, ve şimdi bu). Kural: sözde-sınıf gerçek girdiyle ölçülür.
- **Gelecek regresyon riski: 🟢**

### BL-251 — [DÜZELTİLDİ 2026-08-25] Sıralama ve sayfa numarası URL'ye yazılmıyordu
- Sekme, segment, çipler, arama, görünüm zaten round-trip yapıyordu; bu ikisi atlanmıştı. Yenileyince 1. sayfaya
  ve varsayılan sıraya dönülüyordu, sıralı bir liste paylaşılamıyordu.
- ⚠ **YOLA ÇIKARKEN BULUNAN ALTINCI ÖLÜ YOL:** `state.sortKey` · `state.sortDir` · `SORTERS` ve 8415'teki
  `[data-wcn-sort]` işleyicisi duruyor, ama **`data-wcn-sort="` sıfır kez çiziliyor**. Yani listeyi hiçbir
  kontrol sıralamıyor; sıralayan tek şey DataTables'ın kendi motoru ve o kimseye haber vermiyordu.
- Bu yüzden çözüm "state'i serileştir" değil, **grid'in sırasını state'e AYNALAMAK** oldu (`order.dt` kancası).
  Böylece var olan makine ölü kalmak yerine canlandı; ikinci bir ölü yol bırakılmadı.
- Grid'in açılış sırası artık `state`'ten, yani URL'den geliyor (`order: [[6,'asc']]` sabiti kalktı).
- ⚠ Sıralama anahtarı `SORTERS`'ın KENDİSİNE karşı doğrulanıyor, elle yazılmış bir kopya listeye karşı değil —
  yeni bir sütun eklendiği gün URL'den de sıralanabilir olur.
- ⚠ URL'de 1-tabanlı, state'te 0-tabanlı: `?page=0` kimsenin yazmayacağı bir bağlantı.
- **CANLI:** `?sort=priority` → `?sort=priority&dir=desc`; bağlantıyla geri dönüldüğünde `aria-sort=descending`
  ve sıra korunuyor. Sayfa: `?page=2` ile "11–20 / 30". **En uzun gerçekçi URL 110 karakter** (sekme + sıra +
  yön + iki sinyal + arama) — makul.
- **Gelecek regresyon riski: 🟢**

### BL-252 — [ÖLÇÜLDÜ, DEĞİŞİKLİK YOK] "SLA riski" iki yerde ama KOPYA DEĞİL
- Ölçüm önce yapıldı, karar sonra:
  - **Sinyal çipi** → `slaState ∈ {overdue, due-soon}`. Tek sabit ikili, tek anahtar.
  - **"SLA durumu" seçicisi** → DÖRT değer üzerinde çoklu seçim: `overdue · due-soon · on-track · no-sla`.
- Yani seçici kesin olarak daha ifadeli: "yalnız gecikmiş", "yolunda", "tarihi yok" çiple **sorulamıyor**.
  Çip, seçicinin bir ÖN AYARI; kopyası değil. Birini kaldırmak bir şeyi eksiltirdi.
- İkisi eksenler-arası AND kuralıyla birleşiyor — bu turda kurduğumuz kuralın aynısı, tutarlı.
- **Karar: ikisi de kalıyor.** Hiçbir URL parametresi kaldırılmadı, eski bağlantılar aynen çalışıyor.

### BL-253 — [DÜZELTİLDİ 2026-08-25] İki sütun hiçbir satırı ayırt etmiyordu
- Ölçüm (76 canlı görev): "Tip" her satırda **Görev**, "Modül" her satırda **Görevler**. Dokuz sütunun ikisi,
  sıralanabilir ve filtrelenebilir hâlde, sıfır bilgi taşıyor. Canlıda 9 sütun → **6 görünür sütun**.
- Sıfır sayaçlı çiple aynı karar, ve `priority` sütununun BL-032'den beri kullandığı **aynı mekanizma**: ayırt
  ediyorsa çiz, etmiyorsa çizme. İkinci bir mekanizma yazılmadı.
- ⚠ Test VERİYE bakıyor, sabit bir listeye değil: ikinci bir sağlayıcı iş göndermeye başladığı an sütun
  kendiliğinden geri gelir. Koda yazıldı ki kimse yokluğunu kusur diye bildirmesin.
- ⚠ Boş liste "ayrım yok" DEĞİLDİR: gösterecek bir şey yokken yargılanacak bir şey de yok, sütun kalıyor.
- **"Yalnız sabitli" filtresi KALDI.** Sabitleme çalışıyor — `item.pinned` gerçekten dönüyor; bugün sıfır
  olması "hiç kullanılmamış" demek, "imkânsız" değil. CT'nin ayrımı burada tam yerine oturdu.
- **Gelecek regresyon riski: 🟢**

### BL-254 — [ÖLÇÜLDÜ, AÇIK] Sabitleme sunucuya gitmiyor — BEŞİNCİ yerel-yalnız yol
- Ölçüm: sabitle düğmesi yalnız `item.pinned = !item.pinned` yapıyor, hiçbir API çağrısı yok. Canlı doğrulama:
  bir görev sabitlendi (`bfcfa8ba`), sayfa yenilendi, **sabitleme KAYBOLDU**.
- BL-244'ün sayımına eklenmesi gereken beşinci yol. Kıyas: **erteleme** aynı ailedeydi ve artık gerçek bir
  yazma (kodun kendi yorumu bunu söylüyor); sabitleme geride kalmış.
- ⚠ Bu, "Yalnız sabitli" filtresini kaldırma gerekçesi DEĞİL — filtre çalışan bir kontrolü süzüyor. Kusur
  filtrede değil, sabitlemenin kalıcı olmamasında.
- Karar gerekiyor: sabitleme kişisel veri olarak sunucuya mı yazılsın (erteleme gibi), yoksa oturumluk mu
  kalsın? Oturumluk kalacaksa ekranda bunu söylemeli — bugün hiçbir şey söylemiyor.
- **Gelecek regresyon riski: 🟡** — kullanıcı bir şey işaretliyor, sistem unutuyor ve unuttuğunu söylemiyor.

### BL-233 güncelleme (2026-08-25) — KAPANDI: üç görünüm modu liste sayfasına bağlandı
- Tur B'de detay sayfasından çıkarılmışlardı ve bu **doğruydu**: üçü de bir LİSTE alıp düzenliyor, yani liste
  sayfası görünümleri. Scratchpad'de bekletildiler, silinmediler — tam da bunun için.
- Üçü `TAB_VIEWS`'e, dispatch'e ve URL beyaz listesine eklendi. **Hiçbir gövde yeniden yazılmadı**; tek ekleme
  takvimin eksik boş durumu oldu.
- ⚠ **SÜZME YAPI GEREĞİ ÇALIŞIYOR**, tekrarlanan bir kuralla değil: üçü de `activeItems()` ile başlıyor —
  `renderList` ve `renderTable`'ın çağırdığı aynı işlev. **Kanıt tablosu:**

  | Durum | Liste | Kanban | Split | Takvim |
  |---|---|---|---|---|
  | çip kapalı | 30 | **30** | **30** | 6 |
  | Bloke açık | 4 | **4** | **4** | 2 |

- ⚠ **TAKVİM AYRI VE BUNU YAZIYORUM:** süzmeyi uyguluyor (6→2), ama **yalnız içinde bulunulan ayı** çiziyor ve
  **ay değiştirme kontrolü yok**. Yani 30 öğelik listenin 6'sı görünüyor. Boş değil ama tam da değil —
  CT'nin "boş bir takvim, takvim değildir" uyarısının komşusu. **Karar gerekiyor** (BL-256).
- ⚠ **KANBAN İÇİN "ÖNCE BAK" TALİMATI HAKLIYDI VE BENİM ÖLÇÜMÜM YANLIŞTI.** Denetimde "0 CSS kuralı" yazmışım;
  yalnız `.wcn-kanban` sarmalayıcısını aramışım. Gerçekte `.wcn-kboard`(1) + `.wcn-kcol`(4) + `.wcn-kcard`(9)
  = **14 kural** var. Ekranda bakıldı: Geçmiş sekmesinde iki sütun (Tamamlandı 9 · İptal edildi 9), flex satır,
  272px sütunlar, taşma yok. **Stil yazılmadı, gerek yoktu.**
- ⚠ Kanban İşlerim'de **tek sütun** çiziyor — kodun kendi dalı: segmenti olan sekmede segment tek sütun olur.
  Yani panonun asıl değeri segmentsiz sekmelerde (Havuz · Geçmiş). Davranış değiştirilmedi, ölçüldü ve yazıldı.
- Kanban Gelen Kutusu'na eklenmedi: sütunları yaşam döngüsü durumları, gelen kutusu satırının tek durumu var.
- **Gelecek regresyon riski: 🟢**

### BL-255 — [DÜZELTİLDİ 2026-08-25] Liste görünümü hiç sıralanamıyordu
- Yalnız tablo sıralanabiliyordu; `SORTERS`, `state.sortKey` ve `[data-wcn-sort]` işleyicisi duruyordu ama
  hiçbir kontrol onları sürmüyordu (geçen tur ölçülen altıncı ölü yol).
- Kontrol ürünün **mevcut** deseninden: satır taşma menüsünün ve kolon-görünürlüğü menüsünün kullandığı
  `.dropdown` + `.wcn-menu-item` satırları, aynı araç çubuğu yuvasında. Yeni desen icat edilmedi.
- ⚠ **TEK SIRALAYICI, TEK PARAMETRE.** `state.sortKey`/`sortDir`'i sürüyor — grid'in aynaladığı state — yani
  liste↔tablo geçişinde sıra korunuyor ve `?sort=&dir=` iki görünümde aynı şeyi söylüyor. İkinci bir sıralama
  uygulaması, bu ikisinin baştan ayrışmasının sebebiydi.
- ⚠ Tablo için çizilmiyor (kendi başlıklarından sıralanıyor); kanban ve takvim için de çizilmiyor — onlarda
  **düzenin kendisi sıralamadır** (sütun = durum, hücre = gün), orada bir kontrol hiçbir şeyi değiştirmezdi.
- Yalnız iki satırı ayırt edebilen anahtarlar sunuluyor — tablo sütunlarıyla **aynı yardımcı** (`distinguishes`).
- `SortLabel` 7 dilde eklendi.
- **Gelecek regresyon riski: 🟢**

### BL-254 güncelleme (2026-08-25) — KAPANDI: sabitleme sunucuya yazılıyor
- CT kararı uygulandı, **ertelemenin yolu aynen izlendi**: aynı `TaskPersonalOverlay` satırı (yeni bir tablo
  yok), aynı `PUT {id}/personal/…` deseni, aynı komut/işleyici biçimi, aynı `afterPhase2Write` (iyimser uygulama
  yok — projeksiyon yeniden okunuyor), showcase öğeleri için aynı fixture dalı.
- Yeni alan `TaskPersonalOverlay.Pinned`; **MongoDB olduğu için migration gerekmedi** — eksik alan `false`
  olarak okunuyor. Projeksiyonda `personal.pinned`; `Pinned` "söylenecek bir şey var mı" testine de eklendi,
  yoksa yalnızca sabitlenmiş bir görev `personal: null` yansıtıp işareti kaybederdi.
- ⚠ **İLK CANLI TIKLAMA 404 DÖNDÜ VE SEBEBİ KAYDA DEĞER:** Web tarafındaki `TasksController` bir **vekil** ve
  her uç tek tek yazılmış. Servis tarafında rota hazırken tarayıcı için hâlâ yoktu. Ölçüldü, tahmin edilmedi;
  vekil satırı eklendi ve teste bağlandı.
- **KALICILIK KANITI (gerçek tıklama):** sabitle → `PUT /personal/pin` → **204** → sayfa yenilendi →
  **hâlâ sabitli**, projeksiyonda `personal.pinned: true`.
- **"Yalnız sabitli" filtresi artık gerçekten sonuç döndürüyor:** 30 → **1**.
- Sabitli satırın listedeki yeri ÖLÇÜLDÜ ve **değiştirilmedi**: bugün ayrı bir üste-taşıma yok, satır kendi
  sıralamasındaki yerinde kalıyor. CT'nin "davranışı değiştirme" talimatı gereği dokunulmadı.
- **Gelecek regresyon riski: 🟢** — backend 2342/2342 yeşil.

### BL-256 — [ÖLÇÜLDÜ, AÇIK] Takvim yalnız içinde bulunulan ayı gösteriyor, ay değiştirilemiyor
- Ölçüm: İşlerim'de liste 30 öğe, takvimde **6** öğe. Sebep süzme değil — takvim `data.todayIso`'nun ayını
  çiziyor ve başka bir aya geçecek hiçbir kontrol yok. Canlı veride `dueAt` 76/76 dolu, ama tarihler aylara
  yayılmış.
- ⚠ `plannedDate` — okuyucunun KENDİ tarihi — 76'nın yalnız **4'ünde** dolu. Takvim iki türü ayrı gösterip
  açıklıyor (kırmızı = kaynak son tarih, mor = kişisel plan), yani yanıltmıyor; ama "planlama panosu" vaadi
  bugünkü veriyle karşılanmıyor.
- Karar gerekiyor: (a) ay ileri/geri kontrolü eklensin · (b) takvim "önümüzdeki 30 gün" gibi kayan bir
  pencereye geçsin · (c) bugünkü hâliyle kalsın ve başlık ayın adını taşıdığı için yeterli sayılsın.
- **Gelecek regresyon riski: 🟡** — kullanıcı 30 öğelik bir listeden 6'sını görüp gerisinin olmadığını sanabilir.

### BL-256 güncelleme (2026-08-25) — KAPANDI: takvim gezinilebilir ve dışarıda kalanı söylüyor
- CT kararı (a) uygulandı: **ay geri / Bugün / ay ileri**. "Bugün"deyken o düğme kapalı.
- Seçili ay URL'ye yazılıyor (`?month=2026-09`), varsayılan ay **URL'de yer kaplamıyor** — "Bugün"e dönünce
  parametre siliniyor. Okuma biçim kontrolüyle (`YYYY-MM`), beyaz listeyle değil: her ay meşru, ama yalnız
  `YYYY-MM` bir aydır.
- ⚠ **CÜMLE, OKLAR OLMASINA RAĞMEN KALDI** — CT'nin ayrı maddesi. Gezinme "bakayım" sorusunu yanıtlar; bu cümle
  "bakacak bir şey var mı" sorusunu. Okuyucu aylarca tıklayarak öğrenmemeli.
  Canlı: Ağustos'ta *"Başka aylarda 24 iş var."* · Eylül'de *"…9 iş var."* · Temmuz'da *"…27 iş var."*
- ⚠ **CÜMLE ÖĞE SAYIYOR, GİRDİ DEĞİL.** Kişisel planı ayrı bir güne düşen bir görev takvimde İKİ kez çiziliyor
  (son tarih + plan, ayrı ayrı açıklamalı). Temmuz'da 4 girdi = 3 öğe ölçüldü; **3 + 27 = 30 = liste.**
- ⚠ **TARİHSİZ ÖĞE AYRI SAYILIYOR** çünkü hiçbir gezinme onlara ulaşamaz. Canlı veride **sıfır** ölçüldü (her
  görevde `dueAt` var) — tam da bu yüzden varsayım değil, koşul olarak yazıldı (`CalOutsideAndUndated`).
- 5 yeni anahtar, **7 dil**.
- **SÜZME KANITI** (İşlerim): çip kapalı → liste 30 · kanban 30 · takvim (görünen + dışarıda) 30.
  Bloke açık → 4 · 4 · 4.
- **Gelecek regresyon riski: 🟢**

### BL-257 — [DÜZELTİLDİ 2026-08-25] Kanban sekmeye göre şekil değiştiriyordu
- Ölçüm: segmentsiz sekmelerde durum sütunları, segmentli sekmede tek "Aktif 30" sütunu — yani İşlerim'de pano
  bir listeden farksızdı. **Tek isim altında iki pano.**
- Artık her sekmede yaşam döngüsü sütunları. Segment süzgeci **çalışmaya devam ediyor**, yalnız sütunları
  belirlemiyor: `activeItems()` zaten "Aktif"e daraltıyor, pano onu aşamalara diziyor.
- Sıra **akışın sırası**: Beklemede → Devam ediyor → Bekliyor → Tamamlandı → İptal edildi. Alfabetik değil,
  çünkü soldan sağa okunabilmesi panoyu listeden ayıran tek şey.
- ⚠ **BOŞ SÜTUN ÇİZİLİYOR, İMKÂNSIZ SÜTUN ÇİZİLMİYOR — ve fark ÖLÇÜLDÜ.** `inTab` terminal işi Geçmiş'e,
  terminal olmayanı diğer sekmelere ayırıyor; yani Tamamlandı/İptal Geçmiş dışında **oluşamaz**, diğer üçü de
  Geçmiş'in içinde. Beşini her yerde çizmek, her panoya iki-üç kalıcı boş sütun koymak olurdu — bu oturumda
  çiplerden ve tablo sütunlarından kaldırdığımız "var olmayan topluluğu vaat etme" sınıfının aynısı.
  **Canlı sayım:** İşlerim 3 sütun (13 · 17 · **0**) · Havuz 3 sütun (2 · **0** · **0**) · Geçmiş 2 sütun (9 · 9).
  Yani ulaşılabilir ama boş aşama çiziliyor ve akış okunuyor.
- Ulaşılabilir her aşama boşsa pano ürünün **boş durum cümlesine** düşüyor — beş başlık altında beş boş kutu değil.
- **Gelecek regresyon riski: 🟢**

### BL-258 — [DÜZELTİLDİ 2026-08-25] Liste sıralaması state'i ve URL'yi yazıyor, SATIRLARI dizmiyordu
- CT ölçtü: `?sort=priority&dir=desc` ekranda `High · High · High · Medium · Low · High` — yani **Low'dan sonra
  High**, hiç sıralama yok. `renderList` `items.slice().sort(bySla)` ile sabit sıralıyor, `state.sortKey`'e
  bakmıyordu.
- ⚠ **GEÇEN TUR BUNU `aria-sort` OKUYARAK "DOĞRULADI" — o bir TABLO niteliği.** Listede öyle bir nitelik yok ve
  hiç olmadı; kontrol, test edilmeyen bir yüzeye karşı geçti. **Yanlış yüzeyi ölçmek** bu oturumun tekrar eden
  kusuru ve bu, en pahalı örneği: bir özellik teslim edildi diye raporlandı ve hiçbir şey yapmıyordu.
- ⚠ **GELEN KUTUSU DA DÜZELTİLDİ.** Kendi "önce onaylar, sonra SLA" sıralaması vardı; dokunulmasa kontrolün
  çalışmadığı tek sekme olarak kalacaktı — aynı kusur, yüzeyin dörtte birinde. Onaylar hâlâ önde, ama artık
  **bant** olarak; seçilen sıra her bandın içinde uygulanıyor.
- **SABİTLİ SATIR KARARI ve gerekçesi:** sabitleme "buna sonra döneceğim" demek, dolayısıyla onu gömen bir sıra
  sabitlemenin tek işlevini yok eder — ama okuyucunun az önce seçtiği sırayı sessizce ezmek de kendi başına bir
  yalandır. İkisi de **bantlama** ile cevaplanıyor: önce sabitliler, sonra diğerleri, **her bant seçilen sırada**.
  `sort` ES2019'dan beri kararlı olduğu için bant bölmesi üstteki sırayı bozmuyor.
- **KANIT — ham projeksiyondan okundu, ekrandaki çipten değil.** Sekiz satır, dört anahtar, iki yön: **8/8 dizili.**

  | Anahtar | asc | desc |
  |---|---|---|
  | sla | overdue×6 · due-soon | overdue · on-track×5 |
  | title | Ahmet → … | Zeynep-yönü → … |
  | priority | High×6 · Medium×2 | Low · Medium×6 |
  | status | In Progress×5 | Pending×5 |

  Dört `desc` durumunda da `&dir=desc` URL'de korundu. `dir=asc`'in URL'den düşmesi **kusur değil**: varsayılan
  değer, `sort=sla` gibi yazılmıyor ve geri okumada state zaten `asc`'ten başlıyor — round-trip ölçüldü, tutuyor.
- **Gelecek regresyon riski: 🟢**

### BL-259 — [ÖLÇÜLDÜ, DEĞİŞİKLİK YOK] Takvimde bir güne çok iş
- CT'nin sırası izlendi: önce ölç, sonra karar ver. Eylül 2026, bir güne **14 iş** düşen hücre:
  - hücre yüksekliği **315px** — hücre BÜYÜYOR, satırın tamamı onunla birlikte
  - `scrollHeight === clientHeight` (315 = 315) ve `overflow: visible` → **hiçbir şey kırpılmıyor**
  - öğe metni `text-overflow: ellipsis` ile kısalıyor ama `title` niteliği tam başlığı taşıyor
  - öğeler tıklanabilir (`data-wcn-row`) ve klavyeyle erişilebilir (`tabindex=0`)
  - sayfada yatay taşma yok — 900 ve 1440'ta aynı
- CT'nin kuralı: *"Kaydırılıyor ya da hücre büyüyorsa DOKUNMA, ölçümü yaz."* → **DOKUNULMADI.**
  Hücre başına sınır ve "+N daha" **eklenmedi**; ölçüm onu gerektirmiyor.
- ⚠ Not, kusur değil: 14 işlik bir gün satırı 315px'e çıkarıyor, yani o hafta ekranda baskın oluyor. Kırpma
  olmadığı için yanıltmıyor; yoğunluk sorunu isterse ayrı bir karar konusudur.
- **Gelecek regresyon riski: 🟢**

### BL-260 — [DÜZELTİLDİ 2026-08-25] Tür ekseni iki ayrı kodla çiziliyordu
- Gelen Kutusu `data-wcn-inbox-type`, diğer üç sekme `data-wcn-typechip`: **bir eksen, iki render'cı, iki
  işleyici, sayma-ve-gizleme kuralının iki kopyası.** Kullanıcı görmüyordu — onu hayatta tutan da buydu.
- Tek `typeChipHtml` + tek işleyici. Sinyal çipleri de aynı elden (`sigChipHtml`) çiziliyor.
- ⚠ **DAVRANIŞ FARKI GERÇEK VE KORUNDU:** Gelen Kutusu **tek-seçim** (birini seçince diğerleri kalkar, "Tümü"
  temizler), diğer sekmeler **çok-seçim**. Bu iki farklı okuma görevine dair bir ürün kararı; iki uygulama
  olarak değil, **tek bir yüklem** (`typesAreSingleSelect`) olarak yaşıyor.
- ⚠ "Tümü" tür çipi değil: ekseni temizler, o yüzden sıfırda da çizilmeye devam ediyor.
- **ÖNCE/SONRA — dört sekmede birebir aynı:**

  | Sekme | Önce | Sonra |
  |---|---|---|
  | Gelen Kutusu | Tümü 19 · Kabul Bekleyen 19 · SLA riski 15 | **aynı** |
  | İşlerim | Görev 36 · Bloke 4 · SLA riski 13 · Ertelenmiş 1 | **aynı** |
  | Havuz | Görev 2 · SLA riski 2 | **aynı** |
  | Geçmiş | Görev 18 · SLA riski 10 · Ertelenmiş 1 | **aynı** |

- URL sözcük dağarcığı değişmedi (`types=`), eski bağlantılar çalışıyor. Canlı: `?tab=islerim&types=task`.
- ⚠ Birleştirme **üç testi kırdı** ve üçü de kuralın ESKİ ADRESİNİ arıyordu. Silinmediler — kuralın yeni tek
  evine taşındılar, ve neden taşındıkları yazıldı. *Eski adrese bakan bir test doğru sebeple kırmızıya döner ve
  silinerek "düzeltilir" — bir kural sessizce böyle zorlanmaz olur.* Üstüne, kuralın kopya sayısını sayan yeni
  bir iddia eklendi.
- **Gelecek regresyon riski: 🟢**

### BL-184 güncelleme (2026-08-25) — KAPANDI: tekrarlamıyor
- Tam süit **BEŞ KEZ ÜST ÜSTE** koşuldu. Sonuç, beşinde de **birebir aynı**:

  | Koşu | Geçen | Kalan |
  |---|---|---|
  | 1 | 1721 | 9 |
  | 2 | 1721 | 9 |
  | 3 | 1721 | 9 |
  | 4 | 1721 | 9 |
  | 5 | 1721 | 9 |

  Dokuz kırmızının hepsi dokunulmayan Enterprise Strategy testleri (oturum başından beri kırmızı, başka
  modülün).
- ⚠ **NEDEN-SONUÇ HÂLÂ KURULMUŞ DEĞİL.** En güçlü aday BL-189'du (harness'ın çift yüklemesi / dinleyici sızıntısı)
  ve düzeltildi; kararsızlık ondan sonra hiç görülmedi. Ama "düzeltildikten sonra görülmedi" ile "o yüzden
  oluyordu" aynı cümle değil — kayıt bu ayrımı koruyarak kapanıyor.
- Kapanış gerekçesi: beş ardışık özdeş koşu, tekrarlamayan bir kararsızlığı açık tutmak için yeterli kanıt
  değil. Yeniden görülürse bu not ve BL-189 düzeltmesi başlangıç noktasıdır.
- **Gelecek regresyon riski: 🟢**

### BL-258 güncelleme (2026-08-25) — kapanış notu, liste sayfası
- Liste sıralaması, sabitleme bantlaması ve Gelen Kutusu'nun kendi sıralayıcısı bu turda kapandı; ayrıntı
  BL-255/258 kayıtlarında.

### BL-259 — [KURULDU 2026-08-25] DCP-005 dilim 1: GÖREV TÜRÜ
- Kardeşi `TaskFieldDefinition` kopyalandı, yeni desen icat edilmedi: aynı katman bölünmesi (Rules · Handlers ·
  QueryHandlers · Repository), aynı controller rota deseni (`[Route("Tasks/TaskTypes")]`), aynı görünüm ailesi
  (Index · Create · Edit · Details · _DataTable · _Filter · _Form · _IndexL10n), aynı izin adı deseni.
- **Varlık:** `TaskType` — Code (tekil, değiştirilemez) · Name · Description · RecordClass · GqmsDomain (TEK
  değer) · FunctionCode · IsQualityEvent · GroupDocuments[] · LocalDocuments (seyrek) · IsActive · DeletedAt.
- **İzin:** `platform.tasks.task-types.manage` yazma için; okuma `platform.tasks.read`. Manifest'te sayfa ve üç
  aksiyon kayıtlı — **DELETE aksiyonu YOK**, çünkü manifestte ilan edilen aksiyon katalogun sunacağı aksiyondur.
- ⚠ **MongoDB olduğu için migration gerekmedi.** `TaskItem.TaskTypeId` nullable eklendi ve öyle kalacak: canlı
  ölçüm — 77 görevin **76'sı türsüz**, projeksiyon bunu eksiklik değil normal durum olarak yansıtıyor.
- **CANLI ZİNCİR (gerçek tıklama):** tür oluştur (`dev-qms` → **DEV-QMS** normalize) → listede gör → düzenle →
  Code salt okunur → DOM'dan kurcalayıp gönder → **sunucu reddetti, kod korundu** → pasifleştir → onay diyaloğu
  ürünün dilinde → oluşturma seçicisinden **düştü**, yönetim listesinde **kaldı** → türle görev oluştur →
  projeksiyonda `taskType {code,name}` → yenile → duruyor.
- Doküman temizliği canlı ölçüldü: çift UID ve boş satır sessizce ayıklandı (yazanın niyetini değiştirmiyor).

#### ⚠ İKİ ŞEY VERİLDİĞİ SÖYLENDİ, DEPODA YOK — ÖLÇÜLDÜ
1. **19 değerlik FUNCTION listesi DCP-005'te YOK.** Pakette yalnız "Department → FUNCTION mapping | template
   supplied" satırı geçiyor; listenin kendisi hiçbir yerde yok. `FunctionCode` bu yüzden **normalize edilip
   uzunluk sınırlı serbest metin** olarak kuruldu ve `TaskTypeRules` içinde üyelik kontrolü için dikiş yeri
   hazır bırakıldı. **On dokuz değer uydurulmadı**: uydurulmuş bir liste karşı tarafın gerçek kodlarını
   reddeder, uydurma olanları kabul eder ve yanlış kodlanmış her görev sonradan yeniden kodlanır.
2. **`GMG_ERP_Task_Type_Seed_2026-08-24.csv` depoda YOK.** Aranan yerler: tüm depo (`*.csv` → yalnız
   DocumentManagement fixture'ı), `DEV-QMS`/`BATCH-RELEASE`/`SPEC-CONTROL` metin araması → yalnız bu turda
   yazdığım dosyalar. Bu yüzden **"31 satırın sütunları varlığa oturuyor mu"** sorusu **ÖLÇÜLEMEDİ**;
   "doğrulandı" YAZILMADI. Dosya verildiğinde ölçüm on dakikalık iştir.
   - Pakete göre sütunlar `record_class · gqms_domain · is_quality_event · governing_documents`; dördü de
     varlıkta birebir karşılığını buluyor. Oturmama riski taşıyan tek sütun **`function`**: kapalı liste
     bilinmediği için sınırlanmadı.
- **MUTASYON (4, hepsi kırmızı):** Code değiştirilebilir → 1 kırmızı · tür silinebilir → 1 kırmızı ·
  `GqmsDomain` liste alır → **derlenmiyor** (en güçlü hâli) · izinsiz kullanıcı tür açar → 1 kırmızı.
- ⚠ **DÖRDÜNCÜ MUTASYON İLK SEFERİNDE YEŞİL KALDI** ve bu kayda değer: testim iki SABİTİ karşılaştırıyordu,
  rotayı değil — POST rotası `Create`'e çevrildiğinde hiçbir şey kırmızıya dönmedi. Bu oturumda üçüncü kez aynı
  sınıf: **iki sabitin tatmin edebildiği kural, hiçbir şeyin zorlamadığı kuraldır.** Test artık dört yazma
  rotasının `[HasPermission]` niteliğini yansımayla okuyor, ve okuma rotasının `Read` istediğini de ayrıca
  ölçüyor — kuralın iki yarısı da bağlandı.
- **REGRESYON:** backend **2353/2353 yeşil** · frontend **1721 geçti / 9 kırmızı**, dokuzu da dokunulmayan
  Enterprise Strategy. Bir muhafız yeni alanı yakaladı (`diten-field-icons`: 17. alan haritaya eklenmemişti) —
  doğru yakalama, ikon haritaya eklendi.

### BL-260 — [DÜZELTİLDİ 2026-08-25] Sunucu doğrulama mesajları İngilizce geliyordu
- Canlı ölçüm: salt-okunur kod alanı kurcalanıp gönderildiğinde sunucu **doğru** reddetti ama cümle Türkçe
  formda İngilizce çıktı — *"A task type's code cannot be changed after it is created…"*.
- Şifre kurallarında kurulan köprünün aynısı: servis İngilizce mesajını KORUYOR (yedi çevirisini taşıyan bir
  servis, kuralın ikinci evi olur) ve yanına **kararlı kod** koyuyor — `TASK_TYPE_CODE_IMMUTABLE` ·
  `TASK_TYPE_CODE_TAKEN` · `TASK_TYPE_CLASSIFICATION_INVALID`. Kiracı yüzeyi kodu 7 dilde cümleye çeviriyor;
  haritasız bir kod sunucunun kendi sözlerine düşüyor, sessizliğe değil.
- Yeniden ölçüldü: *"Tür oluşturulduktan sonra kod değiştirilemez: bu türle açılmış görevler onu kimlik olarak
  taşır."*
- ⚠ Ara ölçümde bir kez yanıldım: köprü çalışmıyor sandım, oysa **platform servisini yeniden başlatmamıştım**;
  gelen kod hâlâ `VALIDATION_FAILED`'di. Tanı kaydı ekleyip ölçtüm, sebebi gördüm, kaydı kaldırdım.
- ⚠ İkinci bir ölçüm hatası: pasifleştirme onayının gövdesini okuyup "cümlem gelmedi" sandım. Ortak diyalog ilk
  argümanı **başlık** sayıyor; cümle `options.subtext` ile geçilmeliydi. Dört başka çağrı yerinin aynı hatayı
  yaptığı zaten o dosyanın yorumunda yazılıydı.
- **Gelecek regresyon riski: 🟢**

### BL-261 — [DÜZELTİLDİ 2026-08-25] Menüde Görevler modülünün sayfa adları
- ⚠ **ÖLÇÜM CT'NİN VARSAYIMINDAN DAR ÇIKTI, ve bu iyi haber:** "modülün tamamı İngilizce" değildi. Nav-görünür
  dört girdinin **ikisi zaten çeviriliydi** (`TASKFIELDDEFINITIONS`, `TASKRECURRENCERULES`), modül adı
  (`Nav.Module.TASKS`) ve alan adı (`Nav.Domain.WORKSPACE`) de vardı. Eksik olan tek nav-görünür sayfa
  **`TASKTYPES`**'tı — yani geçen turda benim eklediğim sayfa.
- ⚠ **VE BUNU BİR MUHAFIZ ZATEN SÖYLÜYORDU:** `NavManifestL10nGuardTests` kırmızıydı ve geçen turda o süiti
  koşmamıştım (`Diten.Web.Tests`, frontend vitest'ten ayrı bir proje). Muhafız manifestten anahtarları kendisi
  türetiyor ve yedi dilde arıyor — yani kural zaten zorlanıyordu, ben bakmamıştım.
- Yine de yedi sayfanın hepsine anahtar eklendi (**5 yeni × 7 dil**): nav-görünmez olanlar (`TASKS`,
  `TASKCREATE`, `TASKDETAIL`, `TASKEDIT`) menüde çizilmiyor ama Ctrl+K ve başka yüzeyler aynı köprüden geçiyor.
- ⚠ Manifest `DisplayName`'lerine **dokunulmadı** — onlar çeviri bulunamazsa görünecek geri düşüş.
- **Olay adları (`FallbackDisplayName`) ÖLÇÜLDÜ: farklı bir aile.** Bildirim olayı tanımlarına ait
  (`NotificationEventDefinition`), nav köprüsünden geçmiyor. Dokunulmadı; kapsamı ayrı bir iş.
- **YEDİ DİLDE MENÜ (canlı, `.AspNetCore.Culture` çerezi ile ölçüldü):**

  | | Görev Merkezi | Alan Tanımları | Görev Türleri | Yinelenen Kurallar |
  |---|---|---|---|---|
  | en | Task Center | Field Definitions | Task types | Recurring Task Rules |
  | tr | Görev Merkezi | Alan Tanımları | Görev türleri | Yinelenen Görev Kuralları |
  | fr | Centre des tâches | Définitions de champs | Types de tâche | Règles de tâches récurrentes |
  | es | Centro de tareas | Definiciones de campos | Tipos de tarea | Reglas de tareas recurrentes |
  | zh | 任务中心 | 字段定义 | 任务类型 | 重复任务规则 |
  | ar | مركز المهام | تعريفات الحقول | أنواع المهام | قواعد المهام المتكررة |
  | ru | Центр задач | Определения полей | Типы задач | Правила повторяющихся задач |

- **Gelecek regresyon riski: 🟢** — muhafız her yeni nav-görünür sayfayı yedi dilde zorluyor.

### BL-262 — [DÜZELTİLDİ 2026-08-26] `FunctionCode` kapalı listeye — ve bir KESİNTİ bulundu
- 19 değer DCP-005 §6.7'den **birebir** alındı, uydurma yok. Form serbest metin kutusundan **seçiciye** döndü
  (19 + "Fonksiyon yok"), 19 etiket × 7 dil eklendi. `TaskTypeRules`'taki dikiş yeri üyelik kontrolüyle kapandı.
- ⚠ **CT'NİN "var olan kayıtları ÖLÇ" TALİMATI BİR KESİNTİYİ AÇIĞA ÇIKARDI.** Alanı gerçek bir `enum` yaptım;
  ekran **500** verdi. Sebep: geçen turun canlı testinde `qa` yazmıştım, saklanan değer **`QA`** — pakette `QA`
  yok, **`QUA`** var. Mongo sürücüsü DESERIALIZATION sırasında `FormatException: Requested value 'QA' was not
  found` fırlattı: **tek bir eski değer, görev türü listesinin tamamını ve görev formundaki seçiciyi düşürdü.**
- **Karar:** alan **string** kalır, **liste YAZMADA kapanır** (`ParseFunctionCode`). Gerekçe:
  belge deposunda migration yok — yazılan yazılı kalır. Saklananı **temsil edemeyen** bir tip, veri sorununu
  kesintiye çevirir; iki alternatif (okumada değeri düşürmek, satırı reddetmek) sessiz veri kaybıdır.
  Böylece listeye uymayan yeni bir değer **giremiyor**, var olan **okunabilir ve görünür biçimde uyumsuz**
  kalıyor — CT'nin "sessizce silme" şartı da karşılanıyor.
- Canlı ölçüm sonrası: `DEV-GMP · fn=MFG` (yeni, geçti) · `DEV-QMS · fn=QA` (eski, duruyor);
  listeye uymayan kodla yazma denemesi **reddedildi ve hiçbir şey yazılmadı**, mesaj Türkçe:
  *"Bu, kurumun kullandığı fonksiyon kodlarından biri değil."*
- ⚠ Kalan iş: `DEV-QMS`'in `QA` değeri **elle düzeltilmeli** (`QUA` olmalı). Otomatik dönüştürme yazmadım —
  `QA` → `QUA` benim varsayımım olurdu; kaydı açan kişi karar vermeli.
- **ORG listesi (5 değer) koda GİRMEDİ** — yerel doküman katmanı ertelendi, pakette duruyor.
- **Gelecek regresyon riski: 🟢**

### BL-263 — [PAKETE YAZILDI 2026-08-26] Tür değişikliği kontrolü + neden referans veri değil
- `DCP-005` §6.8: görev türü değişiklikleri **MOD-0023 kapısıyla onaya tabi olacak**
  (`WorkflowTemplateCode` deseni). **Şimdi değil:** QA'nın 31 türü kabul edilmedi; boş bir listeye kapı koymak,
  31 satırın tamamını daha kimse doğru olduklarını kabul etmeden onay kuyruğundan geçirmek demek.
  ⚠ O güne kadar kontrol beyanı **yalnız izne** dayanıyor ve bu, değişiklik kontrolünden **zayıftır** — pakete
  zayıf olduğu yazıldı.
- `DCP-005` §6.9: neden referans veri motoruna konmadı — iki ölçüm.
  (1) `BusinessReferenceDataValue.Attributes` tipi `Dictionary<string,string>`; `group_documents[]` ve
  `local_documents[org][]` oraya ancak **metne gömülerek** sığar. (2) Kod listesi **ETİKET**, görev türü
  **KARAR** taşır (`record_class` başka kodun okuduğu bir kural).
  ⚠ **Bedeli yazıldı:** sığsaydı sürümleme, onay iş akışı, kanıt ve tüketici kaydı bedava gelecekti; sığmadığı
  için dördü de bizim yazacağımız iş. §6.8 bu faturanın ilk taksiti.

### BL-264 — [DÜZELTİLDİ 2026-08-26] Klasör adı alt sınırı 3 → 2
- `QmsFolderPathNormalizer`: `< 3` → `< 2`. Üst sınır **120 aynen** duruyor, tek karakter **hâlâ reddediliyor**.
- Gerekçe koda yazıldı: `HR` · `RA` · `PV` · `QA` bu sektörün standart kısaltmaları ve ikisi QA'nın kendi
  FUNCTION listesinde. Karşı tarafın kendi sözcüklerini reddeden bir kural dikkatli değil, katıdır.
- ⚠ **103 SATIRLIK TAKSONOMİ CSV'Sİ DEPODA YOK** — arandı: `*.csv` içinde yalnız
  `00_all_folders_2175.csv` (farklı bir fixture, 2176 satır) var. Bu yüzden prova ucu **koşulmadı** ve
  "103/103 geçti" **YAZILMADI**. Kural bunun yerine testle kilitlendi: `HR`·`RA`·`PV`·`QA` geçiyor, tek karakter
  reddediliyor, tavan hem 120'de geçip hem 121'de reddedilerek iki yönlü ölçülüyor.
- **Gelecek regresyon riski: 🟢**

### BL-265 — [KURULDU 2026-08-26] DCP-005 dilim 2: DOKÜMAN ARAMA LİSTESİ
- **358 kontrollü doküman bir ARAMA LİSTESİ olarak yüklendi. Tablo kurulmadı.** `documents` diye
  düzenlenebilir bir varlık yok: ne update komutu, ne edit ekranı, ne manifest'te bir EDIT aksiyonu — tek
  aksiyon IMPORT. Gerekçe §6.1: tablo olursa birisi er geç bir başlığı düzeltir ve dokümana ikinci bir otorite
  doğar; listede düzeltilecek kayıt yoktur.
- `ControlledDocument` **kullanılmadı** — `CollectionInstanceId` zorunlu kılıyor ve referans tarafında klasör
  yok. Yeni varlıklar: `DocumentReferenceListVersion` (sürüm) + `DocumentReferenceEntry` (satır).
- **17 sütunun 17'si de okunuyor.** Okunmayan sütun raporlanıyor (`unreadColumns`), sessizce atlanmıyor —
  canlı ölçümde **boş** döndü. Karşı tarafın dosyası **düzenlenmedi**.
- ⚠ **Kendi CSV ayrıştırıcısı yazıldı ve sebebi ölçüldü:** kayıt defterinin kendi bulgu cümlesi
  `link_blocked_reason` içinde **tırnak içinde virgül** taşıyor — yani naif bölme, tam da bir dokümanın neden
  bağlanamadığını açıklayan sütunu bozuyor. Test bunu ayrıca ölçüyor.

#### CANLI ÖLÇÜM (gerçek dosya, gerçek uç)
| Ölçüm | Sonuç |
|---|---|
| prova (dry-run) | 358 satır · **322 seçilebilir** · **36 bloke** · 0 hata · 0 okunmayan sütun |
| işle (1. yükleme) | **201**, sürüm `2026-08-24`, hash `a52205a8…` |
| **aynı dosya 2. kez** | **409 `DOCUMENT_LIST_ALREADY_IMPORTED`** — *"already stored as list version '2026-08-24'"* |
| saklanan sürüm sayısı | **1** (ikinci yükleme hiçbir şey yazmadı) |
| arama `GMG-QMS-SOP-0005` | bulundu: *Deviation and Incident Management*, V0.4, Draft, zorunlu Grup SOP'u |
| bloke satır | `GMG-GDP-SOP-0001` · `NOT REGISTERED` · gerekçesiyle **dönüyor**, gizlenmiyor |

- **SÜRÜM DESENİ taksonomininki:** anlamsal sürüm + SHA-256 içerik hash'i + kaynak anahtarı — ikinci bir
  sürümleme mekanizması yazılmadı. Satırlar sürümler arasında **taşınmıyor**; her yükleme kendi satırlarını
  taşır, böylece "bu görev hangi listeyi gördü" sorusu cevaplanabilir kalıyor.
- ⚠ **Aynı dosyanın ikinci kez yüklenmesi YENİ SÜRÜM ÜRETMİYOR, tanıyor.** Karar gerekçesi koda yazıldı: aynı
  öğleden sonra iki kişinin kayıt defterini yüklemesi iki "güncel" liste üretmemeli, çünkü ardından gelen soru
  hep "görev hangisine karşı çözümledi" olur ve iki cevap cevap değildir.
- ⚠ **BLOKE SATIRLAR GİRİYOR, GÖRÜNÜYOR, SEÇİLEMİYOR.** 23 planlı · 7 geri çekilmiş · 6 kayıtsız (QA'nın kendi
  açık bulgusu) — hepsi listede, hepsi gerekçesiyle. Bu, **sıfır sayaçlı çipleri gizleme kararının TERSİ** ve
  bilerek öyle: orada topluluk yoktu, burada doküman var ve bağlanamıyor. Gerekçesiz bir bloke satır **içe
  aktarma hatası** sayılıyor — "bunu kullanamazsın" cümlesinin "çünkü"süz hâli bu oturumun kaldırdığı sınıf.
- İçe aktarma **hep ya da hiç**: ayrıştırma hatası varsa sürüm de satır da yazılmıyor.
- İzin `platform.tasks.document-list.import` (yazma) · arama `platform.tasks.read` — dokümana atıf sıradan iş,
  kayıt defterini değiştirmek değil. Manifest'te sayfa + tek aksiyon; `Nav.Page.TASKDOCUMENTLIST` 7 dilde.
- ⚠ **BİR MUTASYON İLK SEFERİNDE YEŞİL KALDI (bu oturumda dördüncü kez).** Sürüm tanıma kontrolünü sildim,
  hiçbir test kırmızıya dönmedi: parser testleri hash'in **kararlılığını** ölçüyordu, **karşılaştırıldığını**
  değil. *Kimsenin karşılaştırmadığı bir hash, sürüm değil sağlama toplamıdır.* İçe aktarma katmanına dört test
  eklendi; mutasyon tekrarlandı, kırmızıya döndü.
- **REGRESYON:** backend **2375/2375** · frontend **1721 geçti / 9 kırmızı** (hepsi dokunulmayan Enterprise
  Strategy) · **`Diten.Web.Tests` 46/46** (bu tur ayrıca koşuldu, CT'nin talimatı) · FG-003 0 · KNOWN_RAW 12.
- **BU TURDA YAPILMAYANLAR (dilim 3/4):** göreve bağlama · dondurma altılısı · görev türünün
  `governing_documents`'ını listeye bağlama · taksonominin commit'i.

### BL-266 — [KURULDU 2026-08-26] Doküman listesi yönetim ekranı (DCP-005 dilim 2 tamamlama)
- **TAŞIMA ÇELİŞKİSİ ÖLÇÜLDÜ VE YOKTU.** Emsal (taksonomi sihirbazı) tarayıcıdan **MVC'ye** `multipart`
  gönderiyor; MVC base64'leyip gateway'e JSON yolluyor. Yani iki taşıma iki ayrı yüzey değil, **aynı yolun iki
  adımı** — ve bizim ucumuz zaten emsalinkiyle aynı sözleşmede (`ContentBase64`). Karar: **ekran emsale
  hizalandı**, uç değişmedi. Gerekçe: dosyayı tarayıcıda base64'lemek onu bellekte ikiye katlar ve dosya-imzası
  hesabını zorlaştırır; ayrıca gateway sözleşmesi zaten commit edilmiş.
- **Emsalin en önemli davranışı kopyalandı: dosya değişirse prova geçersiz olur, içe aktarma kilitlenir.**
  Canlı ölçüm: başta kilitli → prova sonrası açık → dosya değişince **yeniden kilitli**. Sunucunun 409'u bu
  hatayı ancak İŞ İŞTEN GEÇTİKTEN sonra yakalıyor; kilit onu ulaşılamaz kılıyor.
- **409 HATA DEĞİL, BİLGİ.** Canlı: `alert alert-info`, gövde metni okuyucunun dilinde. İlk hâlinde sunucunun
  İngilizce cümlesini basıyordu; görev türlerinde kurulan **kararlı kod köprüsü** buraya da uygulandı
  (`DOCUMENT_LIST_ALREADY_IMPORTED` → 7 dil), haritasız bir kod sunucunun sözlerine düşüyor.
- **Bloke satır görünür, seçilemez, gerekçesi okunur — ve bu RENKTEN FAZLASIYLA söyleniyor.** Canlı ölçüm
  (`GMG-GDP-SOP-0001`): satırda `aria-disabled="true"`, gerekçe METİN olarak satırın içinde, soluk sınıf ancak
  üçüncü sinyal. Yalnız griyle anlatılan bir "seçilemez", ekran okuyucu kullananın seçmeye çalışacağı satırdır.
- **32 anahtar × 7 dil.** Manifest `DisplayName` İngilizce bırakıldı — nav köprüsü stabil koda göre çeviriyor.
- ⚠ **MANİFEST GÖRÜNÜRLÜĞÜ ANCAK EKRAN AÇILDIĞI ÖLÇÜLDÜKTEN SONRA `true` YAPILDI.** CT'nin düzelttiği kusur:
  sayfa görünür yayınlanmıştı ama ne görünüm ne rota vardı — kenar çubuğu bir sonraki uzlaştırmada 404'e giden
  bir öğe kazanacaktı. Kural koda yazıldı: *manifestteki görünür bir sayfa, ekranı açıldığı ölçülen turda
  yayınlanır.* Test bunu üç parça olarak ölçüyor: manifest `true` · görünüm dosyası var · `[HttpGet]` rotası var.
- ⚠ **EMSALİN KONTRAST KUSURU KOPYALANMADI.** `.qms-steps` üç adımlı göstergesi ölçülmüş bir AA ihlali taşıyor
  (1.83 açık / 2.02 koyu, `--bs-secondary-color`). İşaretlemeyi kopyalamak kusuru ikinci ekrana taşırdı; yerel
  düzeltmek 197 kuralın bağlı olduğu bir ürün tokenını çatallardı. İki adım **kelimelerle** adlandırıldı, yeni
  renk gerekmedi. Token düzeltmesi ayrı dal (CT talimatı).
- **MUTASYON (3, ÖNCE koşuldu, üçü de kırmızı):** bloke satır seçilebilir · 409 hata olarak gösterildi ·
  dosya-değişti kilidi kaldırıldı.
- ⚠ Testimin kendi kusurunu yakaladım: 409 dalını **tam metne** çakmıştım; dala reason-code kontrolü eklenince
  davranış bozulmadan test kırıldı. *Büyümesine izin verilen bir koda çakılı pencere, pencere değildir* —
  koşulun kendisine bağlandı.
- **İKİ GENİŞLİK × İKİ TEMA (900/1440 × açık/koyu):** sayfa yatay kaymıyor; **sürüm tablosu kendi kabında**
  kayıyor; arama tablosu sığıyor; içe aktarma düğmesi doğru durumda.
- **REGRESYON:** frontend **1731 geçti / 9 kırmızı** (dokuzu da dokunulmayan Enterprise Strategy — bu turdan
  önce de kırmızıydı) · backend **2375/2375** · **`Diten.Web.Tests` 46/46** · FG-003 **0** · KNOWN_RAW **12**.

### BL-267 — [ÖLÇÜLDÜ, AÇIK — TASARIM TUZAĞI] Yanlış bir liste yüklemesi geri alınamıyor
- Doğrulama sırasında kendi kendine ortaya çıktı: ekranı sınamak için küçük bir `TEST-1` dosyası yükledim ve o
  **güncel liste** oldu (arama en yeni sürümü okuyor). Gerçek 358 satırlık listeyi yeniden güncel yapmak için
  tekrar yüklemeyi denedim → **409, "bu dosya zaten 2026-08-24 sürümü olarak duruyor"**.
- **İki kural tek başına doğru, birlikte tuzak:**
  (1) aynı baytlar tanınır, kopyalanmaz — iki kişinin aynı öğleden sonra yüklemesi iki "güncel" liste üretmesin
  diye; (2) en yeni sürüm kazanır.
  Birlikte: **yanlış bir dosya doğrunun ARDINDAN yüklenirse, doğru liste eski sürüm olarak mahsur kalır** ve
  onu geri getirmenin yolu yoktur.
- ⚠ Bu, geçen tur sorduğum sorunun canlı hâli ("sürüm silinemiyor — eski sürümü pasifleştirme gerekir mi?").
  CT `WithdrawnAt`'i sahibin kararına bıraktı ve bu turda yapılmamasını söyledi; **yapılmadı**.
- **Bugünkü durum dürüstçe:** geliştirme kiracısında güncel liste benim `TEST-1` satırım (1 kayıt), gerçek 358
  satırlık liste `2026-08-24` sürümü olarak duruyor ama güncel değil. Elle düzeltilemez.
- Seçenekler: (a) sürüm geri çekme (`WithdrawnAt`) — geri çekilen sürüm "en yeni" sayılmaz; (b) "güncel sürümü
  seç" işlemi — en yeni kuralını elle geçersiz kılar; (c) aynı baytların yeniden yüklenmesine izin ver — ama o
  zaman (1)'in koruduğu şey kaybolur.
- **Gelecek regresyon riski: 🔴 kısıtlı** — veri kaybı yok, ama yanlış yükleme yapan bir kiracı, dilim 3
  geldiğinde yanlış listeye karşı atıf yapar.

### BL-268 — BL-267 KAPANDI: sürüm geri çekme kuruldu (2026-08-26)
- **Durum: KURULDU.** CT (a) şıkkını seçti; `DocumentReferenceListVersion` artık `WithdrawnAt` /
  `WithdrawnReason` / `WithdrawnBy` taşıyor ve emsali `TaskComment.WithdrawnAt`.
- **Neden `DeletedAt` değil:** yumuşak silme satırı yürütme süzgecinden geçen HER okumadan düşürür — bir
  işaretin yapmaması gereken tam olarak budur. Geri çekilen sürüm listede kalır, "geri çekildi" damgasıyla
  görünür; yalnız "güncel" yarışından çıkar.
- **İki yarım ayrı ayrı ölçüldü** (kuru çalıştırma ile, yan etkisiz): geri çekilen baytlar yeniden
  yüklenebilir (`alreadyImportedAsVersion = null`), yaşayan baytlar hâlâ 409 verir. Yani (1) numaralı kuralın
  koruduğu şey kaybolmadı.
- **Gerekçe zorunlu**, ve ikinci bir geri çekme 409 ile reddedilir — ilk damganın üzerine yazılmasın diye.
- **Geliştirme kiracısı düzeltildi:** `TEST-1` gerçek tıklamayla, "doğrulama artığı" gerekçesiyle geri
  çekildi; 358/322/36 satırlık `2026-08-24` listesi yeniden güncel. Sahibin kuralına uyularak BAŞKA hiçbir
  test artığına dokunulmadı.
- **Gelecek regresyon riski: 🟢 katkısal** — üç alan eklendi, hiçbir okuma yolu daralmadı; eski satırlarda
  alanlar `null` ve `null` "geri çekilmemiş" demek.

### BL-269 — ortak onay kutusunun çağıran sayımı tavan değil nüfus sayımıydı (2026-08-26)
- `global-confirm-input-type.test.js` ürünün tamamındaki `showInput:` çağrılarını **altı**ya sabitliyordu;
  amacı doğru (bir modülün turu başka modülün diyaloğunu oynatmasın), sayıyı okuma biçimi eksikti.
- Doküman listesi geri çekmesi meşru bir YEDİNCİ çağıran olarak geldi ve muhafız kırmızıya döndü.
- **Çözüm ikili:** (a) sayım yediye taşındı ve yeni çağıran adıyla yazıldı — muhafızın koruduğu kural
  (hiçbir ÖNCEDEN VAR OLAN çağıran değişmedi) hiç oynamadı; (b) yeni çağıran `inputType` **vermiyor** —
  gerekçe düz yazıdır, bileşenin öntanımlısı da düz yazıdır; tip adlandırmak ürünün ikinci sapması olurdu.
- **Ders:** büyüyemeyen bir nüfus sayımı, ortak bileşeni bir sonraki modül için kullanılamaz kılar. Muhafızın
  yorumuna bu ayrım yazıldı ki gelecek tur sayıyı "düzeltmek" yerine anlasın.

### BL-270 — doküman listesi ekranı GENİŞ ekranda daha dar (2026-08-26, ölçüldü, düzeltilmedi)
- Ölçüm (canlı, iki genişlik): sürüm tablosunun kartı **900px'te 821px**, **1440px'te 611px**. Tablo 1027px
  istiyor; yani geniş ekranda yatay kaydırma DAHA fazla.
- Sebep: `col-lg-5` / `col-lg-7` — `lg` eşiğinin üstünde içe aktarma formu (üç kısa alan) ile yedi sütunlu
  sürüm tablosu yan yana geçiyor ve tabloya yarımdan azı kalıyor.
- Sayfa düzeyinde taşma YOK; kaydırma `.table-responsive` içinde, yani kırık değil — ama "ekran büyüdükçe
  daha az görüyorum" okuyucu için ters bir davranış.
- ⚠ Bu turda DÜZELTİLMEDİ: yerleşim kararı CT'nin. Seçenekler: (a) sürüm tablosunu tam genişliğe al, form
  üstte kalsın; (b) `col-lg-4/8`; (c) dosya adı sütununu kısalt (353px ile en geniş sütun o).
- **Gelecek regresyon riski: 🟢** — yalnız yerleşim; veri veya izin yolu etkilenmiyor.

### BL-271 — kanıt tarafı: göreve ek/kanıt belgesi eklenemiyor (2026-08-26, ölçüldü, kapsam dışı)
- Sahibin sorusu: "görev eklerken doküman ekleme yeri, ya da görevi alan kişi için doküman ekleme yeri
  yok mu?" Cevap ölçüldü: **yok, ve sözleşme gereği yok.**
- `TaskItem.cs:13` açıkça yazıyor: *"Attachments are out of scope (§12 Y4); binary storage belongs to an
  approved document/storage provider."* Ne oluşturma anında, ne atanan kişi için bir ek yolu var.
- ⚠ AYRIM — DCP-005'in tamamı **referans tarafı**dır: "bu iş hangi SOP'a göre yapılır" sorusuna bir
  işaretçi verir, dosya taşımaz. Sahibin sorduğu şey **kanıt tarafı**dır: "işi yaptım, işte doldurduğum
  form". QA yazışmasının dördüncü turunda da böyle kapandı — Faz 1 yalnız referans tarafını kapsar.
- Taksonomi içe aktarımının 103 klasörü **bu taraf için** gerekli: kayıtların yazılacağı yerler onlar.
  Bugün klasör örneklenmemesinin sebebi de bu — içine hiçbir şey yazılamayacak 103 boş klasör olurdu.
- Checklist tarafı ölçüldü ve KUSURLU DEĞİL: `ChecklistRunItem.EvidenceRequired` var, oluşturmada ve
  sonradan değiştirilebiliyor, ekranda ataç işareti çiziliyor ve altındaki cümle dürüst konuşuyor —
  *"Kanıt belgesi gerekiyor. Belge bağlantısı doküman modülü bağlandığında etkinleşecek."* Yani bu, tutulamayan
  bir söz değil, bilerek konmuş bir hatırlatma. Kaldırılmamalı.
- CT görüşü: **kanıtı olmayan bir kalite kaydı yarım kayıttır.** GxP'de kaydı kayıt yapan şey kanıttır.
  Bu gerçek bir boşluk ve sahibin sezgisi doğru — ama bir DİLİM değil, bir MODÜL: dosya saklama, saklama
  süresi, erişim denetimi, sürümleme, denetim izi. Yol haritasında MOD-0031.
- ⚠ Görev Merkezi'nin içine sıkıştırılmamalı. Emsal: "zaman takibi modülü yapalım mı" sorusunun doğru
  cevabı hayırdı, çünkü Görev Merkezi bitmemişti. Aynı gerekçe burada da geçerli.
- **Karar:** DCP-005 dilim 3 ve 4 bitip paket kapandıktan sonra, kendi işi olarak boyutlandırılacak.
  Boyut ÖLÇÜLMEDEN rakam verilmeyecek.
- **Gelecek regresyon riski: 🟡** — kanıt deposu geldiğinde `EvidenceRequired` bir işaretten bir kapıya
  dönüşecek; bugün tik atmayı engellemiyor, o gün engelleyecek. Checklist tik yolu o turda yeniden ölçülmeli.

### BL-272 — üretimde olay taşıması yapılandırılmamış: sessizce InMemory'e düşüyor (2026-08-26, ölçüldü, düzeltilmedi)
- PVG handoff ölçümü sırasında bulundu; Görev Merkezi ile ilgisi yok, ama üretim riski taşıdığı için kayda geçiyor.
- Ölçüm: `Diten.Platform.API/appsettings.json` içinde **`Eventing` bölümü HİÇ YOK**. Üst düzey anahtarlar:
  AllowedHosts · AuditRetentionSeed · AuthService · Authorization · BackgroundJobs · JwtSettings · Logging ·
  MdmService · MessagingProviders · MongoDbSettings · Observability · PublicBaseUrl · Smtp · TenantManagement.
  `appsettings.Development.json` ise `Transport: RabbitMQ` veriyor.
- `RabbitMqEventingOptions.Transport` öntanımlı değeri **`"InMemory"`**. `InMemory` seçildiğinde
  `Infrastructure/DependencyInjection.cs` MassTransit'i HİÇ kaydetmiyor — dört tüketici
  (`TenantActivatedV1Consumer`, `TenantLifecycleAuditConsumer`, `TenantLifecycleNotificationConsumer`,
  `EntitlementCacheInvalidationConsumer`) ayağa kalkmıyor. Outbox worker her koşulda kayıtlı olduğu için
  yayımlamaya devam ediyor — ama in-memory bus süreç sınırını geçemiyor.
- ⚠ DEPOYA BAKARAK KANIT: `Eventing__Transport` / `Eventing:Transport` veren **hiçbir dağıtım dosyası yok** —
  docker-compose yok, k8s yok, env şablonu yok. Yani dağıtım bunu dışarıdan vermiyorsa üretim InMemory koşar.
  ⚠ TERSİ KANITLANMADI: ortam değişkeniyle verilmiş olabilir. Bu, depodan ölçülemeyen bir şey —
  "üretim bozuk" DEĞİL, "üretimde doğrulanmamış" denmelidir.
- Zaten bilinen bir açık uç: `execution/portfolio/access-governance-completion-plan.md:139` aynı şeyi
  "DEPLOY-TIME VERIFICATION REQUIRED ... verify in staging with `Eventing:Transport=RabbitMQ`" diye yazıyor.
- İlgili ikinci ölçüm: **Polly hiçbir serviste yok** (0 dosya). Devre kesici yok, HttpClient retry politikası yok.
  Yeniden deneme üç ayrı elle yazılmış mekanizmada: MassTransit `UseMessageRetry` (üstel, 5 deneme, 10s→300s),
  outbox publisher'ın kendi üstel backoff'u + dead-letter, Audit outbox'ının ayrı politikası, Hangfire 5 deneme.
- **Gelecek regresyon riski: 🔴** — sessiz başarısızlık sınıfı. Yanlış yapılandırılmış bir üretimde hata
  görünmez: outbox yazar, worker yayımlar, kimse tüketmez. Kiracı sağlama, yetkilendirme senkronu ve bildirim
  zinciri sessizce durur. Bir hazırlık (readiness) kontrolü bunu yakalamalı.

### BL-273 — DCP-005 dilim 3 KURULDU: görev → doküman atfı (2026-08-26)
- **Altı alan donuyor** — `document_uid · document_code · title · version · status · referenced_at` — artı
  hangi liste sürümünden okundukları (`ListVersionId`). `TaskDocumentReference`'ın her özelliği `init`-only:
  ayarlanabilir bir özellik davettir, ve buradaki davet tam olarak yapılmaması gereken şeydir.
- **Dondurmanın tek yazma yeri** `TaskDocumentReferenceFreezer`. Var olan bir atfı ASLA yeniden çözümlemez:
  el yordamına görevin MEVCUT atıfları verilir, değişmeyenler dokunulmadan geri döner. "Başlık donmuştur"u
  temenni olmaktan çıkarıp doğru yapan şey bu — güncelleme, hiç çözümlemediği şeyi tazeleyemez.
- **Bloke satır iki yerde birden reddediliyor**: seçici gösterir ve seçtirmez (okuyucu NEDEN'i görsün), sunucu
  da reddeder (ekran bir sınır değildir; API çağıranı seçiciden geçmez).
- **Canlı kanıt (asıl ölçüm):** atıf yapıldıktan sonra kütüğe aynı UID'i `GMG-QMS-SOP-9999 / YENIDEN
  ADLANDIRILDI / V9.9` diye yazan yeni bir sürüm yüklendi. Eski görev hâlâ `GMG-QMS-SOP-0005 / Deviation and
  Incident Management / V0.4` okuyor; aynı anda arama kutusu yeni adı buluyor. Aynı UID, iki cevap, tek ekran.
- **Geri çekilmiş sürüme atıf okunmaya devam ediyor**, ama yeni atıf ondan alınamıyor — iki yarım ayrı ölçüldü.
- **Gelecek regresyon riski: 🟢 katkısal** — `TaskItem`'a bir liste, iki isteğe bağlı sondaki alan; null "atıf
  düzenlenmiyor" demek, yani eskiden yazılmış her yük aynen geçerli.

### BL-274 — "31 türün 9'u" ölçümü tutmadı: on beş, ve üç ayrı sebep (2026-08-26)
- CT'nin dokuz türlük listesi ölçüldüğünde **yedisi doğrulandı**, ikisi (DEV-GMP, DEV-GDP) yanlış çıktı: onlar
  bir atıf yapılabilir + bir bloke doküman taşıyor, yani önerileri **boş değil, KISMİ**.
- Kaynak kütükte (`GMG_ERP_Task_Type_Seed_2026-08-24.csv`, 31 satır) atıf yapılabilir yöneten dokümanı
  olmayan tür sayısı **15**, ve boşluk üç farklı şey demek:
  - **(1) hiçbir doküman belirtmiyor — 1 tür:** GEN-ADMIN. Eksik bir şey yok; bu iş yönetilen bir iş değil.
  - **(2) belirttiği doküman kütükte HİÇ YOK — 7 tür:** DI-REVIEW · MGMT-REVIEW · PV-CASE · PV-PERIODIC ·
    PV-QUALITY-IF · QAG · RECALL.
  - **(3) belirttiği doküman kütükte var ama atıf yapılamıyor — 7 tür:** ARTWORK · BATCH-RELEASE · GDP-OPS ·
    PQR · REG-VARIATION · SPEC-CONTROL · VAL-QUAL.
- Ekran üçünü **ayrı cümlelerle** söylüyor. Tek bir boş kutu üçünde de aynı görünür ve hiçbirini yanıtlamaz.
- ⚠ Geliştirme kiracısında yalnız 2 tür var (DEV-QMS, DEV-GMP); 31'i tohumlanmamış. CT'nin (d) adımı için
  istediği BATCH-RELEASE **yok**, karşılığı olarak (1) durumu DEV-GMP ile canlı ölçüldü.

### BL-275 — dilim 1'in kendi yolunda ÜÇ sessiz kayıp + bir DI tuzağı (2026-08-26, hepsi düzeltildi)
Üçü de derleme yeşilken, test yeşilken, ekranda doğru görünürken kayıp veriyordu. Hepsi canlı bulundu.
- **(a) `readForm` `taskTypeId`'yi hiç okumuyordu.** Yük oluşturucuda `taskTypeId: trimOrNull(draft.taskTypeId)`
  zaten vardı; taslak onu hiç taşımıyordu. DEV-QMS görünür şekilde seçiliyken oluşturulan görev
  `taskTypeId: null` olarak kaydedildi. GxP kaydında tür, kayıt sınıfını taşıyan alandır — yani hiç olmayan
  bir sınıflandırma.
- **(b) `writeForm` türü geri yazmıyordu.** Düzenleme formu türü olan bir görevde "Tür yok" açılıyordu ve
  kaydetme tam değiştirme — başlığı düzelten biri sınıflandırmayı siliyordu. Değer, seçicinin seçenekleri
  geldikten SONRA uygulanıyor: `<select>` tanımadığı değeri sessizce yutar, kusurun yarısı buydu.
- **(c) `LoadApiModelAsync` yöneten dokümanları metin kutusuna doldurmuyordu.** API liste döner, form metin
  düzenler, ikisini bağlayan bir şey yoktu. İki dokümanı olan bir tür boş formla açılıyordu; Kaydet ikisini de
  siliyordu, başarı mesajıyla. BL-024'ün aynı şekli: yazma yolu doğru, OKUMA yolu ona korunacak şeyi hiç vermiyor.
- **(d) DI tuzağı:** dondurucu el yordamlarında **isteğe bağlı** argüman (var olan test kurulumları bozulmasın
  diye). Kaydı unutmak derlenir, 2392 testin hepsini geçer, ve çalışma zamanında her atfı sessizce düşürür —
  ölçüldü: form iki doküman gösterdi, görev sıfır tane kaydetti. Kayıt artık testle çivili.
- **Ders:** "isteğe bağlıya çevirerek geriye uyumluluk" ucuz görünür; bedeli, unutulduğunda HİÇBİR ŞEY
  söylemeyen bir dikiş yeridir. Böyle her dikişin kendi kaydını doğrulayan bir testi olmalı.
- **Gelecek regresyon riski: 🟡** — (a) ve (b) düzeltildi ama daha ÖNCE oluşturulmuş görevlerde tür `null`
  kaldı; bu görevlerin kayıt sınıfı geriye dönük olarak doğmayacak.

### BL-276 — seçici, zaten atıf yapılmış dokümanı arama sonucunda yine listeliyor (2026-08-26, kozmetik)
- Ölçüldü: `GMG-QMS-SOP-0005` seçili çipken aynı doküman arama sonucunda da çıkıyor. Tıklamak zararsız
  (`Map` aynı UID'i tek kayıt tutar), ama okuyucu iki satır görüyor.
- Düzeltilmedi: bu turun kapsamı atıf sözleşmesiydi ve davranış yanlış değil, yalnız gereksiz.
- Seçenek: seçilmiş satırı sonuçtan düşürmek yerine "zaten eklendi" diye işaretlemek — düşürmek, arayıp
  bulamayan okuyucuya "bu doküman yok" dedirtir.
- **Gelecek regresyon riski: 🟢**

### BL-277 — Mongo test düzeneği: koşu başına veritabanı, ölçülen borç ve Bölüm B (2026-08-26, muhafız kuruldu, ihlaller DURUYOR)
Bu tur yalnız **tespit** kurdu; tek bir test düzeltilmedi ve paylaşılan harness'lara kasıtla dokunulmadı.

**Ölçüm (2026-08-26, `chore/mongo-test-database-guard`):**
- Koşu başına veritabanı adı üreten test dosyası: **18**
  (MDM 9 · Platform Application 5 · Platform Eventing 2 · Auth 1 · HCM 1)
- Test tarafından `MongoDbIndexConfigurations.EnsureIndexesAsync` çağıran dosya: **6** (7 çağrı yeri:
  BRD 3 dosya · Eventing 2 dosya/3 çağrı · Auth 1) → toplam benzersiz ihlalli dosya: **19**
- Bir `EnsureIndexesAsync` çağrısının kurduğu şema (Platform): **76 benzersiz koleksiyon**, **218 indeks
  modeli** (+ koleksiyon başına örtük `_id`). Auth: 9 koleksiyon / 21 indeks modeli.
- İki paylaşılan harness tek başına 14 test sınıfı taşıyor: `BusinessReferenceDataTestHarness`
  (`BusinessReferenceDataGskuCatalogLoadMongoTests.cs` içinde tanımlı, **7** sınıf; GUID veritabanı **ve**
  `EnsureIndexesAsync`) · `MongoIntegrationHarness` (**7** sınıf; GUID veritabanı, `EnsureIndexesAsync` YOK).

**CT'nin girdi sayılarıyla fark (ikisi de doğru, farklı şey sayıyor):**
- CT "Eventing 3" dedi → 3 **çağrı yeri**, 2 **dosya**. Muhafız dosya sayar.
- CT "bugünkü 14 ihlal" dedi → ölçülen 19 ihlalli dosya (18 + 6, kesişim 5). 14, iki harness'ın taşıdığı
  **sınıf** sayısıyla örtüşüyor; ihlalli **dosya** sayısı değil.
- CT "282 indeks" dedi → ölçülen 218 bildirilmiş model + 76 örtük `_id` = 294. Büyüklük mertebesi aynı,
  tam sayı farklı; kaynak dosyada bazı modeller tek `CreateMany` içinde toplu.

**Bölüm B (yapılmadı, GSKU ekibiyle ortak):** paylaşılan veritabanı + test başına `TenantId`. Sıra önerisi:
önce iki harness (14 sınıfı bir hamlede taşır), sonra harness kullanmayan tekil dosyalar. Her düzeltilen
dosya muhafızın listesinden **silinir**; silinmezse bayatlık testi kırmızı olur.

**Muhafızın bilinen zayıflığı (Bölüm B'den bağımsız borç):** kaynak **metni** eşleştiriyor, sözdizim ağacı
değil. Değişken adını değiştirmek (`var scratch = "x" + Guid.NewGuid()`) PER_RUN_DB'yi atlatır; iki ifadeye
bölünmüş bir ihlal de atlatır. Yorumlar eşleştirmeden önce ayıklanıyor (dize sabitleri KASITLA korunuyor,
çünkü ihlal genelde `$"..._{Guid.NewGuid():N}"` içinde yaşıyor). Dürüst yükseltme: `GetDatabase(...)`
argümanını çözen bir Roslyn geçişi. Yapılmadı — yayılan şey bu iki jetonluk desen.

**ERTELENDİ — kural metni ve komut kaydı (sahip kararı, 2026-08-26):** `.antigravity/**` ve `AGENTS.md`
korumalı yol (`AGENTS.md:87`); kural bu turda YAZILMADI, merge sonrası ortak pakete kalıyor. Yazılacak metin,
kaybolmasın diye burada duruyor — hedef: `.antigravity/rules/mongo-indexing.md` (DB-001'in devamı, çünkü o
doküman zaten "izolasyon `TenantId` ile sağlanır" diyor) + `.antigravity/workflows/test.md` standart listesine
tek satır atıf:
> - Bir Mongo testi koşu başına yeni bir veritabanı yaratmaz. İzolasyon veritabanı adıyla değil, kiracı
>   kimliğiyle sağlanır — üretimde nasıl sağlanıyorsa aynen öyle.
> - Bir test `MongoDbIndexConfigurations.EnsureIndexesAsync` çağırmaz. Bu üretim bootstrap'idir; platformun
>   tüm şemasını kurar. Şemayı paylaşılan test veritabanı bir kez taşır.
> - Neden (mekanizma, sayı değil): her koleksiyon ve her indeks işletim sisteminde açık dosyadır. Test sınıfı
>   başına bir veritabanı × platformun tam şeması = süreç başına dosya limiti. Limit aşılınca `mongod` fassert
>   ile kendini öldürür; ölünce `DisposeAsync` hiç çalışmaz, atılacak veritabanları birikir ve sonraki koşu
>   enkazın üstüne başlar. Testler yeşilken düzenek çöker — hata testte değil, altyapıda görünür.
> - Doğru desen: paylaşılan bir veritabanı + test başına yeni `TenantId`.
> - Kırmızıyı muhafızın listesine satır ekleyerek yeşile çevirmek yasaktır.

**Gelecek regresyon riski: 🟡** — muhafız yeni ihlali durdurur ama mevcut 19 dosya duruyor, yani makine
üzerindeki `mongod` çökmesi bu tur GEÇMEDİ. Ayrıca muhafız hiçbir toplu test komutunun içinde değil:
`AGENTS.md` regresyon listesi `services/*` altını sayıyor, `tests/architecture` altını saymıyor — bu satırın
eklenmesi de yukarıdaki ertelenmiş pakete dahil:
```bash
dotnet test tests/architecture/TenantArchitecture.ArchitectureTests
```
Kural yazılana kadar muhafızın gerekçesi yalnız kendi dosya başlığında yaşıyor.

### BL-278 — "EnsureIndexesAsync" iki VERİ işi çalıştırıyordu; ayrıldı, kaybolmadı (2026-08-26, düzeltildi + çivilendi)
Metodun adı "indexleri kur" diyordu; yaptığı üç işti ve ikisi satır yazıyordu.
- **(a) `SoftDeleteDomainsForDeletedTenantsAsync`** (eski satır 1106) — silinmiş kiracıların `tenant_domains`
  satırlarını soft-delete eden bir VERİ ONARIMI. Her açılışta koşuyordu.
- **(b) `moduleCatalogDocuments.UpdateManyAsync(Unset("Category"))`** (eski satır 1214) — emekliye ayrılmış
  `Category` alanını tüm modül kataloğundan silen bir VERİ GÖÇÜ. Her açılışta koşuyordu.
- **Sınıflandırma:** ikisi de şema değil, açılış yükümlülüğü. Yeni yerleri
  `Persistence/Schema/PlatformSchemaMigrations.cs`; manifest tamamen bildirimsel kaldı.
- **Neden profile KOYULMADI:** bir test profili, kullandığı 4 koleksiyonu kursun diye çağrılır. O çağrı aynı
  zamanda bir veri göçü çalıştırsaydı, "ucuz" yol dosyanın en pahalı ve en geri alınamaz davranışını, testin
  kendisinin sandığı bir veritabanına taşırdı.
- **Neden üretimden KAYBOLMADI:** ayırmak tam da bir açılış işinin sessizce düştüğü yoldur — yeni dosyaya
  taşınır, yeni dosyayı kimse çağırmaz, hiçbir şey patlamaz, onarım sadece olmamaya başlar. Bu yüzden
  `PlatformSchemaContractMongoTests.TheProductionPathStillBuildsEverythingAndRunsBothDataJobs` ikisinin de
  DAVRANIŞINI ölçüyor (silinmiş kiracının domain'i soft-delete oldu mu; `Category` alanı silindi mi), metodun
  varlığını değil. Mutasyon: iki çağrıdan birini yorum satırı yap → kırmızı, adını söyleyerek.
- **Sıra değişikliği (bilinçli):** 13 `DropIndexIfExists` çağrısı artık manifest'ten ÖNCE topluca koşuyor,
  eskisi gibi araya serpiştirilmiş değil. Korunan özellik aynı: tanımı değişen bir index yeniden kurulmadan
  önce düşürülmeli (yoksa `IndexOptionsConflict`). Tek gerçek sıra bağımlılığı — unique `CodeKey` index'inin
  `ModuleDomainDeduplicationMigration`'dan sonra gelmesi — etkilenmedi; o göç DI açılışında, bu metottan önce
  koşuyor.
- **Gelecek regresyon riski: 🟡** — üretim yolunun tamamı tek bir Mongo testine bağlı; o test atlanırsa (ör.
  mongod yokken) iki iş de sessizce düşebilir. Testin skip-if-unavailable kaçamağı YOK, kasıtlı olarak.

### BL-279 — depoda okunan ama manifestte olmayan koleksiyonlar (2026-08-26, kör nokta KAPANDI; index'ler HÂLÂ yok)
Manifest'i kurarken kontrat maddesi 1 ("deponun dokunduğu her koleksiyon manifestte var") ilk gün üç tane buldu:
| koleksiyon | okuyan | index |
|---|---|---|
| `business_reference_data_validation_results` | `BusinessReferenceDataStewardshipRepository` | yok |
| `document_reference_entries` | `TaskRepositories` | yok |
| `notification_event_definitions` | `NotificationEventDefinitionRepository` | yok |
- Üçü de artık manifestte, **bilerek boş index listesiyle**: manifest "ne VAR"ın kaydı; dışarıda bırakmak
  zaten bunların fark edilmeden indexsiz kalmasını sağlayan şeydi. Üretim davranışı değişmedi (hiçbir index
  kurulmuyordu, kurulmuyor).
- **Yapılacak:** her biri için doğru tenant-first index'i tasarlamak (DB-001). Bu tur boyutlandırma yapmadı.
- ⚠ `business_reference_data_validation_results` BRD profilini **8/8**'e çıkardı — sahiplerinin verdiği
  koleksiyon tavanı tam dolu. Bir sonraki BRD koleksiyonu bütçeyi kıracak; bu bir kaza değil, kasıtlı sıkılık.
**GÜNCELLEME (Aşama 4, 2026-08-26) — kör nokta ölçüldü ve kapatıldı.**
Sanılan sebep yanlıştı: `TenantRepository<T>` adı **türetmiyor**, adı kurucu argümanı olarak alıyor. Yani ad
yine bir literal — sadece `GetCollection<T>("…")` içinde değil, `: base(db, ctx, "…")` içinde yazılmış.
**70 çağrı yeri** bu biçimde. Taramanın göremediği koleksiyon sayısı: **6** (CT'nin "12 dosya" girdisi dosya
sayısıydı; eksik koleksiyon 6):

| koleksiyon | okuyan | profil | index |
|---|---|---|---|
| `task_comments` | `TaskCommentRepository` | WorkflowWorkCenter | yok |
| `task_types` | `TaskTypeRepository` | WorkflowWorkCenter | yok |
| `task_transitions` | `TaskTransitionRepository` | WorkflowWorkCenter | yok |
| `document_reference_list_versions` | `DocumentReferenceListRepository` | WorkflowWorkCenter | yok |
| `document_management_collection_deviations` | `DocumentCollectionDeviationRepository` | DocumentManagement | yok |
| `document_management_collection_provisioning_evidence` | `ProvisioningEvidenceRepository` | DocumentManagement | yok |

- Altısı da manifeste eklendi (bilerek boş index listesiyle) ve depoları artık `PlatformCollections` sabitini
  kullanıyor. **Manifestte olmayan koleksiyon kalmadı**; tek istisna `users` — o Auth'un veritabanında.
- `AuditEventRepository` **temiz çıktı**: adı zaten `AuditCollectionNames.AuditEvents` sabitinden alıyor.
- **Kontrat testi güçlendirildi, üç katmanlı:** (1) her iki çağrı biçimi (`GetCollection<T>("…")` **ve**
  `: base(…, "…")`); (2) manifestteki bir adın dışarıda tekrar yazılmaması; (3) **biçimden bağımsız arka
  durak** — `Persistence/` altında koleksiyon dilbilgisine uyan hiçbir literal, tek bildirim yeri dışında
  bulunmasın. Üçüncüsü yazıldığı gün iki tane daha buldu: `ModuleCatalogTaxonomyCanonicalizationMigration`
  `platform_module_domains` ve `platform_module_services` adlarını elle yazıyordu. Onlar da sabite bağlandı.
- **Neden üç katman:** ilk tarama tek çağrı biçimi gördüğü için altı koleksiyonu kaçırdı. İkinci biçimi
  eklemek aynı hatanın üçüncü biçimde tekrarlanmasını engellemiyor; (3) bu yüzden var.
- **AÇIK KALAN:** altı koleksiyonun hiçbirinde index yok — üçü tenant-scoped ve tenant'a göre sorgulanıyor,
  yani üretimde COLLSCAN. Doğru tenant-first index'in tasarımı (DB-001) yapılmadı, bu turun kapsamı değildi.
  BL-279'un asıl borcu budur ve **kapanmadı**; kapanan yalnız görünürlük.
- **Gelecek regresyon riski: 🟡** — yeni bir indexsiz koleksiyon artık sessizce giremez, ama var olan 9
  indexsiz koleksiyon (bu 6 + önceki 3) duruyor.

### BL-280 — profil sertleştirmesi bir testi doğru sebeple kırmızıya çevirdi (2026-08-26, düzeltildi)
`BusinessReferenceDataUsageLookupMongoTests` iki satırı AYNI `(TenantId, SetCode, ConsumerModule,
ConsumerName)` ile ekliyordu. Üretimde bu kombinasyon **unique index** ile yasak. Test yıllarca yeşildi çünkü
eski `MongoIntegrationHarness` HİÇ index kurmuyordu — yani test, üretimde var olmayan bir şemaya karşı
koşuyordu. Harness profil kurmaya başladığı an Mongo ikinci insert'i reddetti.
- Düzeltme: her satır kendi tüketicisini alıyor (`Organization` / `LegalEntity`). Test edilen sıralama
  davranışı değişmedi — iki satır gerekiyordu, iki AYNI tüketici değil.
- **Ders:** "index'siz test veritabanı" ucuz görünür; bedeli, üretimin reddedeceği veriyi kabul eden ve bunu
  hiç söylemeyen bir süittir. Kaç testin daha bu durumda olduğu ÖLÇÜLMEDİ (BRD ve MDM tarafı taşınmadı).
- **Gelecek regresyon riski: 🟢** — düzeltildi ve artık gerçek index altında koşuyor.

### BL-281 — Mongo dosya patlaması: bizim yarımız bitti, BRD tarafı DURUYOR (2026-08-26, ölçüm)
Bu makinede ölçüldü (`/opt/homebrew/var/mongodb` dosya sayısı, tek koşu deltası):

| koşu | delta dosya | mongod | kırmızı |
|---|---|---|---|
| Platform, BRD hariç — ÖNCE (450167bd) | 4 | ayakta | 50 |
| Platform, BRD hariç — SONRA | **621** | **ayakta** | **0** / 2445 |
| Platform, TAMAMI — SONRA | **8.973** | **ÖLDÜ** | 44 |

- ⚠ **"ÖNCE 4 dosya" bir başarı değil, teşhis:** eski `MongoIntegrationHarness` hiç index kurmadığı için o
  testler neredeyse hiç dosya yaratmıyordu — ve BL-280'in gösterdiği gibi üretimin şemasını hiç görmüyorlardı.
  621, testlerin ilk kez gerçek index'lerin altında koşmasının bedeli. Süre 53 s → 9 s.
- **Kalan ölüm tamamen BRD:** 7 sınıf hâlâ GUID adlı kendi veritabanını açıp `EnsureIndexesAsync` ile 82
  koleksiyonun tamamını kuruyor. Onların turu (sahipleri teyit etti). BRD hariç mongod AYAKTA KALIYOR.
- **Ara bulgu — kendi testimiz de pahalıydı:** `IAsyncLifetime` TEST BAŞINA koşar, dolayısıyla "izole"
  harness'ın veritabanını düşürüp yeniden kurması metod başına oluyordu: ölçülen 2.227 dosya. Veritabanını
  düşürmek yerine **dokümanları silmek** aynı boş sayfayı veriyor: 2.227 → ~0, süre 49 s → 1 s.
- **Ara bulgu — sırayla değişen Guid temsili:** harness süreç-genelinde `GuidSerializer(Standard)` kaydediyor;
  kendi `MongoClient`'ını kuran iki test bunu ayarlamadığı için ÖNCE HANGİ SINIFIN KOŞTUĞUNA göre Guid'leri
  farklı kodluyordu. "Tek başına geçer, süitte kalır" tam olarak buydu ve hata *veri kaybolmuş* gibi
  görünüyordu. İkisi de artık üretimin temsilini sabitliyor.
- **Aşama 4 (bu turda YOK):** `dbPath` bu oturumun ölçümleri sırasında 2.697 → 14.682 dosyaya çıktı. Artık
  temizliği ayrı tur; temiz bir "ÖNCE/SONRA" o temizlikten sonra alınmalı.
- **Gelecek regresyon riski: 🟡** — BRD taşınana kadar tam süit hâlâ mongod'u öldürüyor, yani "44 kırmızı"
  rakamı bir test kalitesi ölçüsü değil, çöküş sonrası artık.

### BL-282 — test artıkları artık kendi kendini topluyor (2026-08-26, kuruldu + kanıtlandı)
Harness bittiğinde kendi veritabanını düşürüyordu; ama "bittiğinde" hiç gelmiyor, çünkü bu işin tamamının
sebebi mongod'un koşu ortasında ölmesi. Ölçüldü: bu makinede 19 veritabanının **6'sı test artığıydı**, üçü
harness'ın iki aşama önce bıraktığı bir adlandırmadan kalma. Elle kimse temizlemezdi.

**İkinci savunma hattı:** `MongoResidueSweeper` — koşunun BAŞINDA, terk edilmiş artıkları düşürür.
- **Silmeye izin veren dört koşulun hepsi gerekli:** (1) ad, harness'ın üretebileceği dilbilgisine uyuyor
  (`diten_platform_itest` **tam segment** olarak + isteğe bağlı `_token`'lar); (2) veritabanı **bizim
  yazdığımız işareti** taşıyor (`__diten_harness_marker`); (3) işaretteki koşu kimliği ŞU ANKİ koşu değil;
  (4) işaret yaş eşiğini (1 saat) aşmış.
- ⚠ **Yük taşıyan kural ad değil, İŞARET.** Bu oturumda dizgi eşleşmesine dayanan muhafızların ne kadar
  zayıf olduğunu iki kez ölçtük. Bu harness'ın yaratmadığı bir veritabanı işaret taşımaz ve adı ne olursa
  olsun silinemez — üretim, `admin`/`config`/`local`, bir başkasının çalışma veritabanı, hepsi bu yüzden
  erişilemez.
- ⚠ **Önek parametre DEĞİL.** `SweepAsync` string parametre almıyor; çağıran öneki genişletemez. "Hangi
  öneki sileyim?" diye soran bir temizleyici, geliştirme veritabanını düşürmekten bir yazım hatası uzaktır ve
  o hata kimsenin dikkatle incelemediği bir test dosyasında yaşar. Bir test bunu refleksiyonla çiviliyor.
- ⚠ **Temizlik hatası test hatası olamaz.** Süpürme koşunun en başında, herhangi bir assertion'dan önce
  çalışır; fırlatsaydı ilk testi kendisiyle ilgisiz bir sebeple kırmızıya çevirir, altındaki gerçek kusur da
  "temizlik flaky" diye elenirdi. Sorunlar **döndürülüyor** ve ayrıca stderr'e yazılıyor.
- **İşaret her açılışta yeniden damgalanıyor**, bu yüzden aynı makinede paralel koşan ikinci bir süitin
  veritabanları her zaman yaş penceresinin içinde kalır. Bu koşul olmasa bir süit diğerininkini test
  ortasında silerdi ve kurbanın hataları tekrar üretilemez olurdu.
- **Uçtan uca kanıt (ölçüldü):** üç veritabanı ekildi — (a) sahip önek + geçerli, bayat, başka koşuya ait
  işaret; (b) aynı önek, işaret YOK; (c) `diten_platform_itestX` + geçerli bayat işaret. Koşu sonrası:
  **yalnız (a) silindi**, (b) ve (c) yerinde. 19 → 18 veritabanı.
- **Tek seferlik boşluk:** işaret mekanizmasından ÖNCE kalmış artıklar (bu makinedeki 3 GUID adlı) işaret
  taşımadığı için asla süpürülmez; onlar elle kaldırıldı (bu turda, ölçüm için). Bundan sonrası otomatik.
- **Gelecek regresyon riski: 🟢** — dört koşulun her biri ayrı bir mutasyonla çivili; birini kaldır, tam
  karşılığı olan test kırmızıya döner.

### BL-283 — BRD harness'ı kendi önekiyle artık bırakıyor; süpürücü ona DOKUNAMAZ (2026-08-26, GSKU'da)
Aşama 4'ün süpürücüsü yalnız `diten_platform_itest` önekini sahipleniyor. BRD harness'ı kendi veritabanlarını
`diten_brd_gsku_<guid>` / `diten_brd_pub_<guid>` diye adlandırıyor.

**Ölçüldü (2026-08-26, tam Platform süiti):** mongod koşu ortasında öldü; geriye **6 BRD artığı** kaldı
(`diten_brd_gsku_*` ×5, `diten_brd_pub_*` ×1) ve **bizim önekimizde sıfır artık**. Yani ikinci savunma hattı
çalışıyor, sadece kapsamadığı bir önek var.

- Tam süit hâlâ: **44 kırmızı · +9.485 dosya · mongod ÖLÜ**. BRD hariç: **2474/2474 · mongod AYAKTA**.
- **Yapılacak (GSKU):** BRD harness'ı da (a) sabit adlı veya kiracıyla izole veritabanına geçsin,
  (b) `MongoResidueSweeper` desenini kendi önekiyle uygulasın — işaret + yaş + koşu kimliği, aynı dört koşul.
  Süpürücüyü "birden çok önek alacak" hale getirmek **önerilmiyor**: öneki parametre yapmak, BL-282'de
  gerekçesiyle reddedilen tasarımdır.
- **Gelecek regresyon riski: 🔴** — bu kapanana kadar tam süit her koşuda mongod'u öldürüyor ve "44 kırmızı"
  bir kalite ölçüsü değil, çöküş artığı.

### BL-284 — üst üste koşu artık büyümüyor (2026-08-26, ölçüm — bu turun asıl kanıtı)
Sabit adlı veritabanı + doküman silme (veritabanı düşürme değil) + başlangıç süpürmesi birlikte, tekrar eden
koşuların dosya sayısını sabit hale getiriyor. Temiz başlangıçtan (1.175 `.wt`, 16 veritabanı), Platform
süiti BRD hariç arka arkaya üç kez:

| koşu | `.wt` öncesi → sonrası | delta | veritabanı | mongod | sonuç |
|---|---|---|---|---|---|
| 1 | 1.185 → 1.826 | **+641** | 19 → 18 (artık silindi) | ayakta | 2474/2474 |
| 2 | 1.826 → 1.822 | **−4** | 18 | ayakta | 2474/2474 |
| 3 | 1.822 → 1.646 | **−176** | 18 | ayakta | 2474/2474 |

- İlk koşu şemayı kurduğu için ödeme yapıyor; ikinci ve üçüncü koşu **hiç büyütmüyor**, WiredTiger geri
  kazandıkça küçülüyor. Aranan özellik buydu: koşu başına doğrusal büyüme yerine durağan hâl.
- Süre: 53 s (Aşama 2 öncesi ölçüm) → **10–13 s**.
- ⚠ `.wt` sayısı WiredTiger'ın kendi zamanlamasına bağlı olarak gecikmeli düşer; tek bir koşunun deltası
  değil, **arka arkaya koşuların eğilimi** anlamlıdır. Bu yüzden üç kez koşuldu.

### BL-285 — dondurucu kaydı artık dizgiyle değil, AÇILIŞLA korunuyor (2026-08-27, düzeltildi)
`TaskDocumentReferenceFreezer`, iki görev yazma işleyicisinde **isteğe bağlı** (`= null`) argümandı. Kaydı
unutmak derleniyor, her birim testini geçiyor ve çalışma zamanında her atfı sessizce düşürüyordu (canlı
ölçüm 2026-08-26: form iki doküman gösterdi, kaydedilen görev sıfır taşıdı).

Bu, `DependencyInjection.cs`'i diskten okuyup içinde `"AddScoped<…TaskDocumentReferenceFreezer>()"` dizgisi
arayan bir testle çivilenmişti. İki ayrı kusur: (a) konteyner hakkında hiçbir şey kanıtlamıyor — kayıt metin
olarak var olup erişilemez olabilir, başka türlü yazılmış çalışan bir kayıt ise testi düşürürdü; (b) kendi
kaynak ağacına derleme çıktısından **beş dizin tırmanarak** ulaşıyordu.

**Yapısal çözüm (DCP-005 §6.1), iki parça — ikisi de gerekli:**
1. Argüman **zorunlu** yapıldı (`CreateTaskItemHandler`, `UpdateTaskItemHandler`). C# zorunlu argümanı
   isteğe bağlıların önüne koymayı dayattığı için `documentFreezer`, `direction`/`upwardRequests` çiftinden
   önce duruyor — bu sıra taşıyıcı, çünkü satın alınan özellik tam olarak "varsayılanın arkasına
   saklanamaması".
2. `Program.cs` konteyneri `ValidateOnBuild = true` + `ValidateScopes = true` ile kuruyor. Tek başına
   "zorunlu" hatayı sessizlikten **ilk isteğe** taşırdı; asıl istenen açılış.

**Mutasyon (2026-08-27, ölçüldü):** `AddScoped` satırı silindi → uygulama **başlamadı**:
`"Some services are not able to be constructed … Unable to resolve service for type
TaskDocumentReferenceFreezer"`, `"Now listening on"` hiç görünmedi. Hiçbir dizgi araması yok.
- Dizgi testi silindi; yerine **refleksiyon** testi kondu: argüman yeniden isteğe bağlı/nullable yapılırsa
  kırmızı. Açılış kontrolü ancak argüman zorunlu kaldığı sürece güçlü, o yüzden bu ikisi bir çift.
- 15 test çağrı yeri (grep 11 buldu, doğru sayıyı **derleyici** verdi) tek bir ortak double'a bağlandı:
  `TaskDocumentFreezerDoubles.OverAnEmptyRegister()` — **gerçek** dondurucu, boş bir kayıt üstünde. Stub
  koymak, işleyicinin dondurmadan atıf yazmasına geri dönmesini görünmez kılardı.
- ⚠ `ValidateOnBuild` tüm Platform konteynerini doğruluyor ve **açılış temiz** (ölçüldü: uygulama ayağa
  kalktı, arka plan işleri koştu, 166 izin senkronize oldu). Yani başka çözülemeyen kayıt yok.
- **Gelecek regresyon riski: 🟢**

### BL-286 — worktree'de kırılan testler: `.git` bir DOSYA olabilir (2026-08-27, düzeltildi)
GSKU ekibinin tam süiti koşamamasının sebebi. İki ayrı kalıp, aynı hata: **checkout biçimini tahmin etmek**.

**(a) Beş dizin elle tırmanma** — `AppContext.BaseDirectory` + `".."×5`. 4 Platform testinde vardı
(`TaskDocumentReference`, `TaskUpwardRequest`, `TaskSeatDirectory`, `TaskRunningChildrenSignal`). Sayı yalnız
tek bir çıktı düzeninde doğru.

**(b) `.git`'i DİZİN sanmak** — 6 test doğru şekilde yukarı yürüyordu ama durma koşulu
`Directory.Exists(Path.Combine(dir, ".git"))` idi. Normal klonda `.git` bir dizindir; **git worktree'de bir
DOSYADIR** (`gitdir:` işaretçisi içerir). Koşul hiç sağlanmıyor, yürüyüş dosya sisteminin tepesine çıkıyor ve
"Could not locate the repository root" fırlatıyor. Etkilenen: `TaskCommentTests`,
`TaskActionCodeReachabilityTests`, `TaskDependencyTests`, `TaskDependencyEnforcementTests`,
`TaskSubtaskBlockingTests`, `DateTimeOffsetSortGuardTests`.
- ⚠ **(b) ilk taramada bulunamadı.** `".."` araması yalnız (a)'yı görür; (b) ancak süit **ayrı bir
  worktree'den** koşulunca ortaya çıktı. Bitirme koşulunun "depo kökünde geçmesi yetmez" demesinin sebebi bu.
- Onu da hepsi tek bir `RepoPaths` yardımcısına bağlandı: **AGENTS.md** işaretçisine kadar yukarı yürür.
  AGENTS.md izlenen bir dosya, yani her checkout biçiminde — worktree dahil — vardır.
- **KANIT:** ayrı worktree'den Platform süiti (BRD hariç) **2475/2475 · mongod ayakta**. Aynı worktree'de
  düzeltmeden önce 8 test kırmızıydı.
- **Gelecek regresyon riski: 🟡** — kalıp yeniden yazılabilir; `.git`/`".."` desenini yasaklayan bir muhafız
  YOK. Aşama 1'in Mongo muhafızı gibi bir kaynak taraması bunu kapatır, bu turda yapılmadı.

### BL-287 — Guid temsili: BRD testleri üretimin KULLANMADIĞI kodlamaya karşı yeşildi (2026-08-27, düzeltildi)
"Tek başına geçer, süitte kalır"ın kaynağı ölçüldü ve teşhis ilk sanılanın **tersi** çıktı.

**Ölçüm:** `BusinessReferenceDataTenantAssignment` + `BusinessReferenceDataPublishOperation` sınıfları tek
başına **13/13 geçiyor**; harness kullanan bir sınıf aynı sürece girince **11 kırmızı**. Hatalar veri
kaybı gibi okunuyor (`Assert.NotNull() Failure: Value is null`) — satırlar bir Guid kodlamasıyla yazılıp
diğeriyle sorgulandığı için. Mesajın hiçbir yerinde serileştirme geçmiyor; bu yüzden bir tur kaybettirdi.

**Ters teşhis:** kayıt EKSİK olduğu için değil, VAR olduğu için kırılıyorlar. Üretim
(`Infrastructure/DependencyInjection`) **ikisini birden** yapıyor:
`BsonSerializer.RegisterSerializer(new GuidSerializer(Standard))` (süreç-geneli) **ve**
`mongoClientSettings.GuidRepresentation = Standard` (istemci başına). BRD testleri kendi `MongoClient`'ını
kuruyor ve **ikisini de** ayarlamıyor — yani yalnız hiçbir şey global serializer'ı kaydetmemişken geçiyorlardı.
Bu, BL-280'in aynı şekli: üretimin sahip olmadığı bir kodlamaya karşı yıllarca yeşil.

- **Cevap "istemci başına yap" DEĞİL:** kayıt sürücüde tasarım gereği süreç-geneli ve üretim de öyle yapıyor;
  testte daraltmak, testleri üretimden ayırırdı. Kusur **kapsam** değil **zamanlama**ydı: kayıt harness'ın
  içinden TEMBEL yapılıyordu, yani global durum ancak harness kullanan bir sınıf önce koşmuşsa vardı.
- **Düzeltme:** `PlatformTestSerializers` + `[ModuleInitializer]` — assembly yüklenirken, ilk test vakasından
  önce, hem iki serializer hem `MongoDefaults.GuidRepresentation = Standard` (her `MongoClientSettings`'in
  devraldığı sürücü-geneli varsayılan). Böylece "hangi sınıf önce koştu" diye bir şey kalmıyor ve her istemci
  üretimin şeklini alıyor — her testin hatırlamasına gerek kalmadan.
- 11 kırmızı veren senaryo şimdi **17/17 yeşil**; sınıflar tek başına da yeşil.
- ⚠ **Ölçüm tuzağı:** aynı senaryo bir ara 17 dk 31 sn sürdü. Sebep bu değişiklik değil, biriken BRD artığı
  üzerinde boğulan mongod'du (BL-283); artık düşürülünce **1 dk 14 sn**. Süre ölçerken `dbPath` durumunu
  önce temizleyin.
- **Gelecek regresyon riski: 🟡** — yeni bir test kendi `MongoClient`'ını kurarsa artık doğru varsayılanı
  devralır, ama açıkça `GuidRepresentation` ayarlayıp yanlış değer verirse hâlâ sapabilir. Muhafız yok.

### BL-288 — veritabanı adları üç ayrı gelenekte, ikisi ne olduğunu söylemiyor, ikisi paylaşılıyor (2026-08-27, ölçüldü, ertelendi)
- Sahibin tespiti: *"bunun adı çok yanlış."* Ölçüm doğruladı ve sorunun adlandırmadan
  büyük olduğunu gösterdi.
- **Üç ayrı gelenek aynı anda:**
  `DitenEnterpriseDb` (PascalCase) · `DitenERP_Dev` (PascalCase+alt çizgi) ·
  `diten_personalization_dev` (snake_case) · `diten_auth_v3` (snake_case+sürüm)
- **İki ad yalan söylüyor:**
  · `diten_personalization_dev` aslında **Platform'un tamamı** — 93 koleksiyon, 17.8 MB,
    29.200 belge. "Kişiselleştirme" adı tek bir alt kümesinden kalmış.
  · `DitenERP_Dev` içinde **yalnız MDM verisi** var (`mdm_legal_entities`, 19 belge).
    "ERP" her şey demek, hiçbir şey söylemiyor.
- ⚠ **ASIL SORUN AD DEĞİL, PAYLAŞIM:** `DitenERP_Dev`'i MDM **ve** HCM birlikte kullanıyor
  (ikisinin de `appsettings.Development.json`'ı aynı adı gösteriyor). HCM bugüne kadar
  hiçbir şey yazmamış, yani sorun henüz görünür değil — ama iki servisin tek veritabanını
  paylaşması, servis sınırının veri katmanında olmaması demek.
- Taşıma maliyeti ÖLÇÜLDÜ ve düşük: `DitenERP_Dev` = 1 koleksiyon · 19 belge · 0.01 MB.
  Ad 5 yerde geçiyor (MDM ×2, HCM ×2 appsettings + süpürücünün "dokunma" test listesi).
  ⚠ `organization_units`'in **15 kaydının 15'i de** bu belgelerin `_id`'lerine bağlı;
    kopyalamada `_id` korunur, bağlar sağ kalır. Ama doğrulanmadan yapılmamalı.
- ⚠ `diten_personalization_dev` aynı işin **büyük yarısı** — 93 koleksiyon. İkisini ayrı
  ayrı planlamak aynı işi iki kez planlamaktır.
- **Karar (sahip onayladı 2026-08-27): ŞİMDİ YAPILMAYACAK.** İsimlendirme standardıyla
  BİRLİKTE yapılacak: kural (`diten_<servis>_<ortam>`) + yeni yanlış adı engelleyen muhafız
  + mevcutların tek seferde taşınması + servis paylaşımının çözülmesi. GSKU ekibiyle de
  bu şekilde mutabık kalınmıştı ("yeni adlar için standart evet, toplu yeniden adlandırma
  ayrı ve planlı iş").
- ⚠ `diten_mdm_dev` adı 2026-08-27'de boşaldı (terk edilmiş nesil silindi, BL-284). Yeni MDM
  veritabanı için o ad kullanılacaksa GSKU'ya haber verilmeli — daha önce "incelenmeden
  tekrar kullanılmasın" demişlerdi.
- **Gelecek regresyon riski: 🟡** — taşıma sırasında `_id` korunmazsa Organization→LegalEntity
  bağları kopar ve bu ancak ekranda fark edilir. Taşıma turunun kabul koşulu, taşımadan
  sonra o 15 eşleşmenin hâlâ 15 olması olmalıdır.

---

### BL-289 — bölüm başlığı idiom A → B: 180 başlık, altın referans artık B'de (2026-08-27, ölçüldü, ertelendi)
- **Ne:** Ürün aynı görüntüyü üreten **iki** başlık idiomu taşıyor. Bu tur altın referans
  (`GoldenReferenceCompact/_Form.cshtml`, 4 başlık) **B**'ye çevrildi; kural dosyası da B'yi
  gösteriyor. Kalan A'lar duruyor.
- **Ölçüm (2026-08-27, `Views/` altı):**
  · **A** — `<h6 class="text-uppercase text-heading fw-semibold …">` : **180 başlık**, 30+ dosya
    (Organization · Tasks alt ekranları · Platform · DevEnablement Details …)
  · **B** — `<h6 class="card-section-title …">` : **10 başlık** — 4'ü altın referans (bu tur),
    6'sı `Tasks/_Form.cshtml` sağ kolon
  · **C** — `<h5 class="card-title … me-2">` : **0**. Kural dosyasında yazıyordu, üründe hiç yoktu.
    Bu tur kural dosyasından SİLİNDİ; ölü idiom artık kimseyi yanlış yönlendirmiyor.
- **Neden B kazandı:** tek sınıf adı bütün tarifi taşıyor (uppercase + heading rengi + 600 +
  glifi primary'ye boyama), açıklama satırı (`.card-section-desc`) hazır geliyor, ve A beş
  yardımcı sınıfın dizilişi — sonraki bir düzenleme yarısını düşürebilir, düşürdüğü de
  görülmez.
- ⚠ **BU TUR TASKS'A DOKUNULMADI — bilinçli.** Sahip kararı: tur altın referans içindir.
  `Tasks/_Form.cshtml` bugün **ikisini birden** taşıyor (sol kolonda 4 A, sağ kolonda 6 B).
- ⚠ **Bir test bu ikiliğe bağlıydı ve bu tur GEVŞETİLDİ, kaybolmadı:**
  `tests/tasks-form-select2-notification.test.js` başlık tarifini altın referanstan TÜRETİYOR
  ve `text-uppercase` metnini birebir arıyordu. Referans B'ye geçince kırmızıya döndü. Testin
  kendi gerekçesi zaten "hangi yol değil, HEPSİ AYNI kasada olsun" diyordu — referans kontrolü
  de iki yolu birden kabul edecek şekilde düzeltildi, kasa ve paylaşılan kuralın uppercase
  olduğu iddiası duruyor. **A→B turu bittiğinde bu gevşetme geri alınabilir** ve tek yol
  yeniden çivilenebilir.
- **Tetikleyici:** bir ekran ailesine (Organization, Platform, Tasks alt ekranları) zaten
  dokunan bir tur — 180 başlığı ayrı bir "kozmetik süpürme" turu olarak yapmak, hiçbir
  kullanıcı sorununu çözmeden 30+ dosyayı kirletir.
- **Gelecek regresyon riski: 🟢 katkısal.** İki idiom aynı pikseli üretiyor; dönüşüm görüntüyü
  değiştirmez. Tek gerçek risk, dönüşümü yarım bırakıp `.dt-card-icon`'u erken silmek — bkz.
  [BL-291].

### BL-290 — kural dosyası `row g-6` diyordu, ürün 340 yerde g-4/g-3 (2026-08-27, ölçüldü, DÜZELTİLDİ)
- **Ne:** `.antigravity/rules/frontend-form-template.md` "Form sayfalarında `row g-6` boşluğu
  standarttır" diyordu. Ürün bunu hiç uygulamamış.
- **Ölçüm (2026-08-27, `Views/` altı):** `row g-4` **170** · `row g-3` **170** · `row g-2` 26 ·
  `row g-6` **4** — ve o dördü de **form değil**, DocumentManagement'ın Details sayfaları.
  Her iki altın referans da g-4 (kart satırı) + g-3 (kart içi alan satırı) kullanıyor.
- **Karar: kural ürüne uyduruldu, ürün kurala değil.** Kuralı okuyup g-6 yazan tek bir form
  sayfası çıkmadı; 340 satırı "kurala uysun" diye değiştirmek, hiç kimsenin şikâyet etmediği
  bir boşluğu bütün ürün genelinde büyütmek olurdu. Kural artık g-4/g-3 diyor ve NEDEN
  değiştiğini kendi içinde taşıyor.
- **Açık kalan (küçük):** DocumentManagement'ın 4 `row g-6` Details sayfası artık hiçbir
  kurala dayanmıyor. Form şablonu kuralı Details sayfalarını kapsamıyor, o yüzden ihlal
  değiller — ama o modüle dokunan bir sonraki tur g-4'e almalı.
- **Gelecek regresyon riski: 🟢** — düzeltilen bir belge satırı; kod değişmedi.

### BL-291 — `.dt-card-icon` tek dosya için yaşayan bir sınıf, `.card-section-title .bx` ile aynı şeyi yapıyor (2026-08-27, ölçüldü, ertelendi)
- **Ne:** `backbone-custom.css:6426` → `.dt-card-icon { flex: 0 0 auto; font-size: 1.125rem; color: var(--bs-primary); }`
- **Ölçüm:** üründe **4 kullanım**, hepsi **tek dosyada** (`Views/Tasks/_Form.cshtml` sol kolon,
  idiom A başlıkları). `.card-section-title .bx` kuralı (satır ~4552) `font-size: 1.125rem` +
  `color: var(--bs-primary)` ile **aynı iki bildirimi** taşıyor; `flex: 0 0 auto` ise
  `.card-section-title`'ın kendi `display:flex`'i altında zaten glifin doğal davranışı.
- **Yani:** iki isim, tek kural — ve ikisinden biri yalnız dört satır için var.
- ⚠ **Tek başına silinemez.** O dört kullanım idiom A başlıklarının içinde; sınıf ancak
  [BL-289]'un A→B dönüşümü `Tasks/_Form.cshtml`'i B'ye taşıdığında sahipsiz kalır.
  Erken silmek o dört başlığın glifini gri ve küçük bırakır — ve bu ancak ekranda fark edilir.
- **Tetikleyici:** [BL-289] kapandığı anda, aynı turda.
- **Gelecek regresyon riski: 🟢 katkısal** — ama sıra bağımlı: önce dönüşüm, sonra silme.

### BL-292 — iki altın referansın DA select placeholder'ı bozuktu, iki ZIT şekilde (2026-08-27, canlıda bulundu, DÜZELTİLDİ)
- **Nasıl bulundu:** sahip **ekrana bakarak**, ikon turunun canlı doğrulaması sırasında. 1850 testin
  hiçbiri görmüyordu; ikisi de bu turda çivilendi
  (`tests/golden-reference-form-icons.test.js`, "both references DECLARE the select placeholder").
- **Tek sebep, iki zıt belirti — ikisi de "placeholder'ı select2 kendi çıkarsın" ihmali:**
  · **Slim (offcanvas):** `placeholder: $el.data('placeholder') || ''` — markup'ta `data-placeholder`
    yok, yani select2'ye **BOŞ** placeholder verildi. Boş placeholder "placeholder yok" demek
    DEĞİL: select2 o zaman `<option value="">`in metni yerine
    `<span class="select2-selection__placeholder"></span>` çiziyor. Yerelleştirilmiş
    **"Seçiniz…" hiç ekrana çıkmadı** — oklu boş kutu.
  · **Compact (tam sayfa):** placeholder **hiç verilmedi** — select2 boş option'ı sıradan bir
    **SEÇİM** sanıp gövde rengiyle boyadı. Ölçüldü: `rgb(56,69,81)`, aynı karttaki her düz
    input'un placeholder'ı ise `rgb(167,172,178)`. **Boş alan, dolu alandan ayırt edilemiyordu.**
- **Düzeltme (ikisi de):** `placeholder: … || $el.find('option[value=""]').text() || ''`.
  Metin OPTION'da kalıyor → tek yerelleştirme kaynağı (markup'ın arkasındaki resx), hiçbir dil
  dosyasının güncellemeyeceği bir `data-` niteliğinde ikinci kopya değil.
  Doğrulandı: ikisi de artık `rgb(167,172,178)`.
- ⚠ **Kural dosyası bunu ZATEN doğru yazıyordu** (`create.js` şablonu, `initSelect2`:
  `placeholder: $el.find('option[value=""]').text() || ''`). İki referans da kuraldan sapmıştı —
  yani "kural doğru + referans yanlış" hâli, bu turun asıl teşhisinin (desenin kanalı boş) select2
  tarafındaki ikinci örneği.
- **Gelecek regresyon riski: 🟢** — davranış artık iki referansta da testle çivili.
