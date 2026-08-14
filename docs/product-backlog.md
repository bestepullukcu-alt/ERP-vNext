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
