# ERP-vNext — Product Backlog (Deferred / Out-of-Scope-for-Now)

> **Amaç:** Bilinçli olarak **ertelenen** özelliklerin tek kaydı. Her madde bir gerekçe ve bir "ne zaman yapılır" tetikleyicisiyle park edilir — böylece hiçbir şey sessizce unutulmaz ve hiçbir şey vaktinden önce yapılmaz.
> **Sahiplik modeli:** Claude = CONTROL TOWER (prompt yazar, canlı doğrular); yürütme = Antigravity ajanları. **Go-live kapsamı buradaki her şeyi HARİÇ tutar.**
> **Antigravity ajanları için (ZORUNLU):** Buradaki maddeler, onaylı bir module pack açıkça bu backlog'dan çıkarıp `approved`/`ready-for-dev` kapsamına almadıkça **UYGULANMAZ**. Bir backlog özelliğini "yardımcı olayım" diye kendiliğinden inşa etmek YASAKTIR. Bir talep bir backlog maddesine değiyorsa, kod yazmadan önce bu dosyayı referans göster ve module pack kapısına yönlendir.
> **Son güncelleme:** 2026-08-28 — kapanmış kayıtlar `docs/roadmap/backlog/product-backlog-closed.md`'ye taşındı; her kayda DURUM/SAHİP alanı eklendi. Kuralı aşağıda.


---

## 📏 BU DOSYANIN KENDİ KURALI (2026-08-28)

> Bu bölüm buraya, **dosyanın en başına**, bilerek kondu. Ölçüldü: `.antigravity/workflows/reconcile-records.md`
> ve `connect-module-to-workcenter.md` bu dosyaya atıf yapıyor, ama backlog'un **kendi** tutulma kuralı
> hiçbir yerde yazılı değildi — ne burada, ne `.antigravity/rules/` altında. Kural, uygulandığı yerin
> dışında yaşarsa okunmaz; bir iş akışı dosyasına konsaydı yalnız o akışı koşan görürdü.

#### K1 — Her kaydın SABİT yerinde iki alan vardır
`### BL-xxx` başlığının **hemen altındaki ilk satır**:

```
> **DURUM:** AÇIK | KAPANDI | ERTELENDİ · **SAHİP:** <ad> | SAHİPSİZ
```

⚠ **Bu satır BAŞLIKTAN daha yetkilidir.** Başlıklardaki `[YAPILMADI]` / `[ÖLÇÜLDÜ, AÇIK]` gibi işaretler
turlar arasında güncellenmemiş olabilir — ölçüldü: BL-211, BL-220 ve BL-241'in başlıkları gövdeleriyle
çelişiyordu. Başlıklar tarihî metin olarak **olduğu gibi** bırakıldı; doğru cevap DURUM alanındadır.

⚠ **SAHİP boş bırakılmaz.** Bilinmiyorsa `SAHİPSİZ` yazılır. Sahipsiz görünmesi amaçtır:
sahipsiz iş, unutulan iştir. 2026-08-28 ölçümü: 309 kaydın **307'si SAHİPSİZ**.

#### K2 — İş biten turda kapanır ve AYNI TURDA arşive taşınır
Bir iş bitince, o turda: `DURUM: KAPANDI` yazılır **ve** kayıt `docs/roadmap/backlog/product-backlog-closed.md`'ye taşınır.

⚠ **"Sonra toplu temizleriz" birikmenin sebebidir.** Bu dosya 6927 satıra ve 326 bloğa tam olarak böyle
ulaştı; sonraki temizlik 2026-08-28'de bir turun tamamını yedi ve içinde aynı numarayı taşıyan iki ayrı iş
(BL-259, BL-260) ile kendi içinde çelişen üç kayıt bulundu.

#### K3 — Kayıt SİLİNMEZ, taşınır
Kapanan kayıt arşive gider; oradan da silinmez. Bir kaydın tek işlevi "yapılacak" olmak değildir —
bu oturumda eski kayıtlar birkaç kez **bir hatanın geçmişini anlatan tek kaynak** oldu.

#### K4 — Bir numara bir iştir
Aynı `BL-xxx`'e ikinci bir `### ` bloğu AÇILMAZ. Güncelleme, kaydın **kendi gövdesine** eklenir;
eski metin gerekiyorsa **alıntı** olarak kalır, ayrı başlık olarak değil.

⚠ Gerekçe ölçümle: iki blok düştüğünde biri "kapandı" diğeri "açık" görünüyor ve iş hem bitmiş hem açık
okunuyor. 2026-08-28'de 17 kodun böyle olduğu, 10'unun bloklarının **çeliştiği** ölçüldü.

#### K5 — Şüphedeysen AÇIK bırak
Kapandığı **kanıtlanamayan** kayıt açık kalır. Açık duran bitmiş bir iş bir tur maliyetindedir;
kapalı görünen bitmemiş bir iş sessizce kaybolur.

## Nasıl kullanılır
- Bir özellik konuşulup bilinçli ertelendiğinde madde ekle: **ne olduğu**, **neden ertelendiği**, **hangi tetikleyiciyle yapılacağı**, **ilgili modül**.
- Bir maddeyi ancak onaylı bir module pack'e alınıp teslim edildiğinde kaldır/üstünü çiz.

---

> *Bu bölümün kayıtlarının tamamı KAPANDI ve `docs/roadmap/backlog/product-backlog-closed.md`'ye taşındı.*

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

> *Bu bölümün kayıtlarının tamamı KAPANDI ve `docs/roadmap/backlog/product-backlog-closed.md`'ye taşındı.*

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
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** CRUD'un ötesinde kurumsal/tüzel-kişi olayları için çalışma alanı — birleşme & devralma (M&A), sermaye değişikliği, yeniden yapılanma, unvan değişikliği / yeniden yerleşim (redomiciliation), fesih — kendi audit izleri ve (ileride) onay akışıyla.
- **Konuşulan yüzey:** Legal Entity liste/satır action'ı ("Corporate Action Workspace").
- **Neden ertelendi:** Başlı başına büyük bir modül; go-live için gerekli değil.
- **Yapım tetikleyicisi:** Ayrı onaylı module pack (corporate-actions).
- **İlgili:** MOD-0220 Legal Entity (yukarı-akış veri kaynağı).

### BL-002 — Filing Calendar / Inbox (Legal Entity compliance)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Resmi beyan/başvuru son-tarih takibi — yıllık raporlar, statüter/vergi beyanları, lisans yenilemeleri — tüzel kişi başına takvim + vadesi gelen/geçen yükümlülükler için bir inbox.
- **Konuşulan yüzey:** Legal Entity liste/satır action'ı ("Filing Calendar / Inbox").
- **Neden ertelendi:** Başlı başına bir compliance modülü; go-live için değil.
- **Yapım tetikleyicisi:** Ayrı onaylı module pack (compliance/filings).
- **İlgili:** MOD-0220 Legal Entity; document-management (başka ekip) ile örtüşür.

### BL-003 — Legal Entity governance/approval workflow bağlantısı
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** LE `Approval Status` (Draft→InReview→Approved) ve `Review Due` (periyodik yeniden-gözden-geçirme tarihi) alanlarını, Draft'ta duran statik alanlar olmaktan çıkarıp gerçek bir **veri-yönetişim / stewardship iş akışına** bağlamak.
- **Neden ertelendi:** Workflow motoru (MOD-0023) entegrasyonu + steward rolleri gerekir; go-live için değil.
- **Yapım tetikleyicisi:** governance-workflow capability pack.
- **İlgili:** MOD-0220 Legal Entity, MOD-0023 Workflow.

### BL-004 — Legal Entity evidence/belge toplama
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** LE `Evidence Status`'ünü gerçek destekleyici-belge toplamayla (kuruluş evrakı, vergi levhası) beslemek — compliance kanıt ilerlemesi.
- **Neden ertelendi:** document-management (başka ekip) + compliance akışına bağlı.
- **Yapım tetikleyicisi:** doc-management entegrasyon pack'i.
- **İlgili:** MOD-0220 Legal Entity, MOD-0028 Document Management.

### BL-005 — OrgUnit tiplerini genişlet (Warehouse / Plant / Sales / RepOffice)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** `OrgUnitType` enum'u şu an: Department, Division, Branch, Team, HQ. Grup yapısındaki depo (Monom, distributor deposu), üretim tesisi (Poland, Migual), saha satış (rep office) için ayrı tip yok — bugün Branch/Division ile temsil ediliyor.
- **Neden ertelendi:** Küçük ama ürün-kararı gerektiren bir tip genişletmesi.
- **Yapım tetikleyicisi:** **Blueprint'e (`docs/reference/blueprint/System Capability & Implementation Blueprint - master 8.1.xlsx`) bakılarak, org-model buna uygunsa yapılacak** — aksi halde mevcut tiplerle temsil devam.
- **İlgili:** MOD-0288 Organization.

### BL-007 — Business Partner / Distributor master
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Grubun kendi tüzel kişisi olmayan 3. parti taraflar (distributor'lar, onların branch/filyaları, müşteriler) için ayrı bir iş-ortağı/müşteri master'ı. Bunlar Legal Entity değildir. Ayrıca intercompany ticaret akışı (Poland→Group→Monom→AZ satış zinciri) da bu/ilişkili ticari kapsamda.
- **Neden ertelendi:** Legal Entity ve Organization kapsamı dışında, ayrı bir master + ticari ilişki modeli.
- **Yapım tetikleyicisi:** **Blueprint'e bakılarak, uygunsa yapılacak.**
- **İlgili:** MOD-0220 Legal Entity (ayrım netliği için), gelecek commercial/supply-chain kapsamı.

### BL-008 — Position-based access provisioning (birthright roles) + Employee model
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Bugün erişim tamamen role-based (`User → UserRoleAssignment → Role → Permission`); Position erişimden kopuk, sadece org-yapısı. Hedef: pozisyona rol(ler) bağlanır, bir kullanıcı o pozisyona atanınca pozisyonun rolleri/izinleri **otomatik** gelir ("birthright access"). Gerekenler: (1) Position→Role bağı, (2) Employee entity + `PositionAssignment → Employee → (opsiyonel) User` zinciri (bugün PositionAssignment doğrudan `UserId`'ye bağlı), (3) yetki çözümleyicinin kullanıcının aktif pozisyon atamalarını okuyup rol/izin türetmesi.
- **Neden ertelendi:** HR/Employee modülü henüz yok; RBAC bugün yalnız role-based; ciddi bir mimari katman.
- **Yapım tetikleyicisi:** **Blueprint'e bakılarak, org/HR modeli buna uygunsa yapılacak** — HR modülü (Employee) geldiğinde birlikte ele alınır.
- **İlgili:** MOD-0288 Organization (Position/PositionAssignment), MOD-0018 RBAC / Access Governance, gelecek HR modülü.

### BL-009 — Reference Data tam governance UI (olgun onay akışı)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Reference data yönetiminin "öner→onayla→yayınla" tam ekranları + tam değişiklik geçmişi (şu an basit hali var).
- **Neden ertelendi:** Blueprint bunu W-3'e (3. dalga) koymuş; go-live için basit hali yeter.
- **Yapım tetikleyicisi:** Blueprint W-3 / operatör onay ihtiyacı doğunca.
- **İlgili:** MOD-0048 Reference Data Management.

### BL-010 — Cascade (bağlı/dependent listeler)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Bir listenin başka listeye bağlı olması (ülke→şehir, kategori→alt-kategori). Value shape'e `parentCode` eklenerek additive gelir.
- **Neden ertelendi:** Go-live için düz listeler yeter; bağlı listeler ileri ihtiyaç.
- **Yapım tetikleyicisi:** Blueprint'e bakılarak, dependent liste ihtiyacı doğunca.
- **İlgili:** MOD-0048 Reference Data (BRD v2).

### BL-011 — Financial Dimensions / Cost Center registry
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Mali boyutlar, cost center, profit center, dimension set'leri — reference data'dan AYRI bir governance modülü (GL hareketsel defter ayrı kalır).
- **Neden ertelendi:** ERP mali kapsamı; go-live dışı.
- **Yapım tetikleyicisi:** Blueprint MOD-0291 sırası gelince.
- **İlgili:** Blueprint MOD-0291.

### BL-012 — dt-defaults.js button-group radius'unu inline-style'dan CSS'e taşı
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** [dt-defaults.js:364-440](../../frontend/Diten.Web/wwwroot/assets/js/dt-defaults.js) toolbar button-group'un köşe yuvarlaması/ayraçlarını runtime'da `this.style.setProperty('border-radius'…, 'margin-left'…, 'position'…)` ile **inline** basıyor (responsive gizlenen butonlar `:last-child` CSS'ini bozduğu için JS ile görünür ilk/son buton hesaplanıyor). FG-003 ihlali.
- **Çözüm:** JS inline-style yerine **class toggle** etsin (ör. `.dt-btn-visible-first/-last/-middle`), radius'lar `backbone-custom.css`'te class üzerinden tanımlansın.
- **Neden ertelendi:** Çalışıyor (bug değil), **tek kaynak** (dt-defaults.js) → ileride tek yerde değişir, tüm sisteme yansır, dağınık regresyon yok. Go-live aciliyeti yok. DİKKAT: körlemesine silme — grup butonlarının (ColVis+Filter) radius'u buna bağlı; standalone Add butonunda etkisiz (radius zaten default).
- **İlgili:** FG-003, tüm DataTable toolbar'ları.

### BL-013 — Country/Currency tam ISO genişletme
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** BRD `country` (şu an 22) ve `base-currency` (26) setlerini tam ISO 3166/4217'ye (~195 ülke / ~180 para) genişletmek. Şu an grubun faal ülkeleri (TR/CH/GE/AZ/PL + majör ekonomiler) kapsanıyor.
- **Neden ertelendi:** Faal footprint yeterli; tam ISO "someday" nicelik. Yeni ülke gerekince tek satır JSON + version bump ile eklenir (bkz. legal-entity-reference.json, catalog_version bump şart).
- **Yapım tetikleyicisi:** Daha geniş coğrafya ihtiyacı doğunca.
- **İlgili:** MOD-0048 Reference Data (BRD), FG-004.

### BL-017 — WorkCenter segment ↔ chip görsel ayrımını keskinleştir
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** İşlerim'de segment (Aktif/Bekleyen/Planlı, tek-seçim segmented-control) + chip (tip/sinyal, çoklu) tek satırda; segment beyaz kutuda dolu-mor aktif, chip'ler dışında. UX kritiği: **pasif segmentler hâlâ chip'lere benziyor** (ikisi de yuvarlak/sayaçlı). "9/10" için segmenti daha da ayrıştır.
- **Konuşulan yüzey:** İşlerim filter-row (2026-07-24).
- **Neden ertelendi:** Mevcut hâli çalışıyor ve yeterince ayrık; bu bir cila. Kullanıcı "şimdilik böyle kalsın" dedi.
- **Yapım tetikleyicisi:** UX polish turu. Seçenek: (a) segment başına `Durum:` etiketi/ikon, (b) pasif segmentleri pill değil düz-sekme göster (yalnız aktif dolu).
- **İlgili:** MOD-0024 WorkCenter, `.wcn-filterbar`/`.wcn-segments`.

### BL-320 — Görev geri çağırma (recall): başlatan işi üstlenenden geri alır
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** Başlattığı işi, üzerinde çalışan kişiden **geri alma** fiili. Bugün başlatanın elindeki iki
  fiil `cancel` (işi tümden iptal) ve `reassign` (başkasına ata) — ikisi de "bunu geri istiyorum, kendim
  yapacağım / şimdilik beklesin" demiyor. SAP bunu *withdraw*, Oracle BPM *withdraw/recall* diye adlandırır.
- **Neden bu turda YAPILMADI:** Spec §7 recall'ı **v1.5**'e koyuyor ve ortada **hiçbir endpoint yok**.
  Sağlayıcıya bir `recall` aksiyonu koymak, arkasında bir şey olmayan bir düğme çizmek olurdu — MOD-0024
  sağlayıcısının kendi kuralının (`Faz 2+ komutlar kasten yok: arkasında endpoint olmayan bir aksiyonu
  projeksiyona koymak mock döneminin kullanıcıyı yanılttığı yoldur`) doğrudan ihlali.
- **Gerçek iş:** yeni bir lifecycle geçişi (kim, hangi durumdan, hangi duruma), `TaskTransitionCodes`'a yeni
  bir kod, endpoint + yetki (yalnız requester), bildirim ("işi geri aldı") ve BL-016'nın sekmesine tek satır.
- **Muhafız var:** `TaskOutboxTests.Recall_is_NOT_offered_here_because_no_endpoint_answers_it` — "sadece
  düğmeyi ekleyelim" yolu bugün **kırmızı** teste çarpar.
- **İlgili:** BL-016 (kapandı), MOD-0024 WorkCenter, spec §7 v1.5.

### BL-321 — Başlattıklarım'da KAPANMIŞ iş: raporlama sorusu, sekme sorusu değil
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** `ListByCreatorAsync` **terminal işi dışarıda bırakıyor** (Done/Cancelled) — havuz okumasının
  yaptığının aynısı. Yani "başlattığım ve artık kapanmış işler" hiçbir yüzeyde yok.
- **Neden bilinçli:** Geçmiş sekmesinin bugünkü anlamı "bir zamanlar **benim panomda** olan ve kapanan iş".
  Hiç üstlenmediğim, sadece açtığım bir işi oraya koymak o sekmeye ikinci, ilan edilmemiş bir anlam yükler.
  Başlattıklarım'ın sorusu ise "başlattım, **hâlâ dışarıda**" — kapanmış iş o soruya da ait değil.
- **Doğru cevabın şekli:** bu bir **rapor** ("açtığım işler, tarih aralığı, kapanış süresi"), üçüncü bir
  ownership sekmesi değil. Ownership ekseni beş sekmeyle dolu; altıncısı ekseni filtreye çevirir.
- **Ölçüm (2026-08-29, dev kiracı):** yaratan ≠ atanan 24 kayıttan **3'ü kapanmış** (Cancelled), 21'i canlı.
  Yani bugünkü boşluk küçük ama gerçek.
- **İlgili:** BL-016 (kapandı), MOD-0024 WorkCenter.

### BL-322 — "Herkesin başlattığı işi gör": ayrı ve yetkiyle kapalı yüzey
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** BL-016 **kişisel** soruyu cevapladı: "ben ne başlattım". Yönetici/denetim sorusu — "bu kiracıda
  kim ne başlattı, nerede takıldı" — ondan farklı bir yüzeydir ve bir izinle kapatılmalıdır.
- **Sektör deseni, ve ikisinin AYRI olması tesadüf değil:** SAP'de kişisel yüzey Business Workplace / Fiori
  My Inbox, "hepsini gör" ise **SWI1** (Work Item Selection, yetkiyle). Oracle'da kişisel yüzey BPM Worklist
  "Initiated Tasks", "hepsini gör" ise **Administrative Tasks** rolü. İkisini tek yüzeyde birleştirmek, bir
  kullanıcıya bir başkasının işini kişisel panosunda gösterir.
- **Neden bu turda YAPILMADI:** kapsam kararı. BL-016'nın okuması `actor.UserId`'ye bağlıdır ve
  `TaskOutboxTests.Work_between_two_OTHER_people_reaches_no_read_at_all` bunu **muhafaza ediyor** — bu madde
  o muhafızın gevşetilmesi değil, **ayrı** bir okuma + ayrı bir izin demektir.
- **Gerekecek:** yeni izin anahtarı (manifest + rol senkronu), kiracı-kapsamlı okuma, kendi ekranı.
- **İlgili:** BL-016 (kapandı), MOD-0024 WorkCenter, `TaskPermissions`.

### BL-015 — WorkCenter alternatif görünümler (Bölünmüş / Kanban / Takvim)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

> **Not (2026-07-25):** Bu madde daha önce yanlışlıkla `BL-016` numarasıyla açılmıştı; `BL-016` "Başlattıklarım / Outbox" maddesine aittir (yukarıda). Alıntılar belirsizleşmesin diye bu madde **BL-026**'ya taşındı; içerik değişmedi.
- **Nedir:** Meeting invite, WorkCenter Inbox içindeki trigger-only “Hızlı Yanıt Bekleyenler” yüzeyinde `Kabul et / Reddet / Takvimde Aç` aksiyonlarıyla gösterilir. Yanıt verildiğinde trigger Inbox'tan çıkar; kabul edilen toplantı **İşlerim'e dönüşmez**. Authoritative toplantı kaydı, katılım durumu, tarih/saat, katılımcılar ve sonradan yapılan yanıt değişiklikleri Takvim modülünde yönetilir. WorkCenter ileride kabul edilmiş yaklaşan toplantıları “Bugünkü Ajanda” içinde salt-okunur özet ve `Takvimde Aç` bağlantısıyla gösterebilir.
- **Davranış sınırı:** Toplantıdan doğan gerçek işler ayrı `task`, `review`, `approval` veya davranışına göre acknowledgment work item olarak üretilir ve normal Task Detail açar. Meeting trigger'a task lifecycle uydurulmaz.
- **Neden ertelendi:** MOD-0024 mevcut slice'ı frontend-only canonical fixture/Task Detail kapsamındadır; gerçek Calendar provider, RSVP command, projection refresh ve Ajanda veri bağlantısı yoktur.
- **Yapım tetikleyicisi:** Calendar/meeting provider kontratı ve WorkCenter aggregation backend'i için ayrı onaylı capability/module pack; BL-015 Takvim görünümünden bağımsız olarak önce RSVP + source-navigation seam'i teslim edilebilir.
- **İlgili:** MOD-0024 WorkCenter, BL-015, WC-1 birleşik WorkItem kontratı, WC-2 çalışma-zamanı/takvim seam'i.

### BL-018 — Enterprise Strategy'yi WorkCenter sağlayıcısı yap (Binding A / MOD-0023)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** Enterprise Strategy onayları bugün serbest-metin `ApprovalStatus` alanı — gerçek bir kuyruk değil; hiçbir mekanizma bunları WorkCenter'a iş olarak itmiyor. Bu onayları MOD-0023 `ApprovalTask` kuyruğuna (Binding A) taşı ki ES WorkCenter'a **gerçek** iş itsin. Basit salt-okunur strateji durumu gerekirse doğrudan sağlayıcı (Binding B) olabilir.
- **Yapım tetikleyicisi:** WC-1 dilimi **shipped olduktan SONRAKİ** dalga. WC-1'in ilk kanıtı MOD-0023'ün kendi onaylarıdır (ES değil); ES bu ilk kanıttan sonra ikinci sağlayıcı olarak bağlanır.
- **İlgili:** DCP-004 OD-WC-02 · §10.4 (A/B binding law) · §17 · WC-1 birleşik WorkItem kontratı.

### BL-019 — Blueprint canonical MOD-xxxx tahsisi (CAND-CAP-0006 mezuniyeti)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** EA, Work Aggregation / Task Center (Görev Merkezi) için Blueprint'e canonical bir `MOD-xxxx` satırı açar ve `CAND-CAP-0006 → MOD-xxxx` deprecated-alias zincirini kaydeder (DCP-002). Blueprint'te bugün karşılık yok (doğrulandı); CAND-CAP-0006 geçici governance kimliğidir.
- **Yapım tetikleyicisi:** Yetenek **WC-1'de kanıtlanınca** (şimdi değil). CAND-CAP-0006 WC-1 dilimi boyunca kalır; MOD-xxxx tahsisi ayrı bir EA kararıdır.
- **İlgili:** DCP-004 §1 (EA follow-up) · §19.1 · OD-WC-03 · DCP-002 (kimlik canonicalization) · module-id-registry.

### BL-020 — MOD-0023 pack reconciliation (stale ifade düzeltmesi)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** MOD-0023 module pack'i "No code is produced by this pack" diyor ve Batch 01 kutuları işaretsiz; ama `ApprovalTask` entity + `GetMyWorkflowTasks` query/handler runtime'ı **gerçekte shipped**. Pack'in framing'ini (durum ifadesi + Batch 01 kutuları) gerçek runtime durumuna göre düzelt.
- **Yapım tetikleyicisi:** Ayrı bir governance edit'i (DCP-004 charter'ı MOD-0023 pack'ine dokunmadı; bu düzeltme ondan bağımsız yapılır).
- **İlgili:** DCP-004 §20 F1 · §19.4 · MOD-0023 module pack.

### BL-021 — Enterprise Strategy fixture-truth cleanup (QA)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** ES fixture'larındaki `processInstanceId` + `lifecycleOwner: workflow` temsilî; 3/3 deep-link rotası gerçek workflow rotasıyla uyumsuz (fixture-doğruluk borcu). Gerçek sağlayıcı bağlanınca fixture'lar gerçek rota/alan kullanmalı. Bu iş executable kontratı (`fixture-contract.js`) **DEĞİŞTİRMEZ** — yalnız fixture veri doğruluğunu düzeltir.
- **Yapım tetikleyicisi:** ES gerçek sağlayıcı olunca (BL-018 ile birlikte).
- **İlgili:** DCP-004 §20 F4 · §19.5 · BL-018 · WorkCenterNext ES provider fixtures.

### BL-023 — WorkCenter "Ekibim" kapsam seçici (yönetici görünümü)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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
  (`docs/reference/modules/tenant/workcenter/workcenter-completion-plan.md` § UX tur sırası notu). Maddenin eski yeri (Aşama 4b, "UX turundan
  **sonra**") bu yüzden değişti.
  **Yeni sıra:** 1. kayıt turu → 2. [BL-057] (+ [BL-072] aynı turda) → 3. **BL-023** → 4. liste UX turu →
  5. diğer sayfalar. Sıranın tek kaynağı `docs/reference/modules/tenant/workcenter/workcenter-completion-plan.md`.
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
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (2026-08-13):** BL-024 Faz 2 kuralı uçtan uca çalışıyor ama **ekranda kurulamıyor ve
  okunamıyor**: (a) alan-tanımı formunda `ViewPermission`/`EditPermission` girişi yok — kural yalnız API ile
  kuruluyor · (b) `redacted: true` telde geliyor ve hiçbir yüzey onu göstermiyor; kullanıcı yetkisi olmayan
  alanı **boş** görüyor, "gizli" değil — boş bir alanla saklanmış bir alan aynı görünüyor · (c) iki gerçek
  kullanıcıyla ekran doğrulaması yapılmadı (ikinci kullanıcının parolası CT'de yok).
- **Neden ayrı:** (a) bir yönetim ekranı işi (izin anahtarı seçici — sabit liste değil, MOD-0018 kataloğundan),
  (b) yedi dilde metin + kart tasarımı, (c) bir ortam/kimlik işi. Üçü de güvenlik kuralının kendisi değil.
- **Gelecek regresyon riski: 🟢 eklemeli** — sunucu kararı zaten veriliyor, ekran onu yalnız gösterecek.

### BL-025 — In-app bildirim kanalı + header çanını (bell) gerçek veriye bağlama
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** `premium-modal-standard.md` (MOD-0013) çıplak/özelleştirilmemiş SweetAlert2'yi yasaklıyor ve `swal-icon-circle` premium ikon haznesi + `rounded-4`/`shadow-lg` + `buttonsStyling:false` + Sneat butonları şart koşuyor. Ama projede **paylaşılan bir helper yoktu**: standardı uygulayan 6 dosya (`Account/login.js`, `Account/forgot-password.js`, `Account/reset-password.js`, `Governance/Users/index.js`, `Platform/AuditLog/index.js`, `Platform/Tenants/details.js`) aynı premium HTML'i **kendi içinde tekrar yazıyor**. MOD-0024 create dilimi ile `wwwroot/assets/js/shared/` altına tek bir helper eklendi (error/success/confirm/info) ve Tasks onu kullanıyor.
- **Kalan iş:** yukarıdaki 6 dosyayı (ve sonradan eklenen benzerlerini) helper'a geçir; kopyalanmış inline HTML bloklarını sil. Görsel çıktı birebir aynı kalmalı (regresyon yok).
- **Neden ertelendi:** Her dosya farklı akış (login/şifre sıfırlama/audit/tenant) — tek tek görsel doğrulama gerekiyor; MOD-0024 dilimini bloklamasın diye ayrıldı. Additive: helper zaten yerinde, migrasyon dosya bazında yapılabilir.
- **Yapım tetikleyicisi:** MOD-0024 Faz 1 kapandıktan sonra, tercihen frontend bakım dilimi içinde.
- **İlgili:** `.antigravity/rules/premium-modal-standard.md` (MOD-0013) · MOD-0024 create dilimi (helper'ın kaynağı) · FG-003 (inline CSS yasağı — helper'da da geçerli).

### BL-029 — Eski `/WorkCenter` yüzeyinin sökülmesi
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** Diten.Web'de **iki** Görev Merkezi yüzeyi var, ikisi de "Görev Merkezi" başlıklı: `/WorkCenter` (`WorkCenterController` — kendi İngilizce mock'u, sekmeler "Gelen Kutusu / All Work", fixture tarihleri 2026-03/04'te donmuş) ve `/WorkCenterNext` (canlı MOD-0024 + MOD-0023 sağlayıcıları). Bütün DCP-004 işi ikincisinde. Sol menü doğru şekilde `/WorkCenterNext`'e gidiyor.
- **Gerekenler:** (a) `WorkCenterController.Index`'in `/WorkCenterNext`'e 302 forward etmesi (Tasks/Index'te uygulanan aynı desen — kalıcı 301 değil); (b) `Meeting` ve `Task` sayfalarının akıbeti: WorkCenterNext'in kendi detay yüzeyi bunları karşılıyorsa silinir, karşılamıyorsa taşınır — **karar önce**; (c) `DevScenarios` geliştirici yüzeyi ya WorkCenterNext altına taşınır ya kaldırılır; (d) eski mock verisinin (`MEETINGS`/`NOTES` dışındaki İngilizce fixture'lar) temizliği.
- **Neden ertelendi:** CT canlı doğrulamasında ortaya çıktı (2026-07-26). Giriş yönlendirmesi ayrı ve acil bir hataydı (5 yerde `/WorkCenter` default'u) ve hemen düzeltildi; **yüzeyin sökülmesi** ise `Meeting`/`DevScenarios`'un nereye gideceği kararına bağlı olduğu için ayrı dilim. Karar verilmeden silinirse çalışan iki sayfa kaybolur.
- **Yapım tetikleyicisi:** MOD-0024 Faz 4-5 sonrası; toplantı daveti çipinin gerçek bir sağlayıcıya bağlandığı dilimle birlikte yapılması doğal.
- **İlgili:** `AccountController` post-login default · MOD-0024 pack (Tasks/Index → WorkCenter 302 forward emsali) · `mock-data.js` `MEETINGS`/`NOTES`.

### BL-030 — `DateTimeOffset` BSON dizi temsili: kök neden migrasyonu
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Nedir:** MongoDB C# sürücüsü `DateTimeOffset`'i varsayılan olarak **BSON dizisi** (`[ticks, offsetMinutes]`) olarak saklar. `Diten.Platform.Infrastructure/DependencyInjection.cs:170-171` yalnız `GuidSerializer` ve `DecimalSerializer` kaydediyor; `DateTimeOffsetSerializer` **kayıtlı değil**. `Diten.Platform.Common.Persistence.BaseEntity` ise `CreatedAt` (`DateTimeOffset`) ve `UpdatedAt` (`DateTimeOffset?`) taşıyor ve Platform'daki **her** tenant-scoped varlığın atası. Sonuç: iki tarih alanına birden sıralayan her sorgu `MongoCommandException: cannot sort with keys that are parallel arrays` ile **çalışma zamanında** patlar. Derleme temiz geçer, testler (fake repository'ler) yeşil kalır.
- **Kanıtlanmış vaka:** `WorkflowRepositories.GetLatestByObjectRefAsync` (`StartedAt` + `CreatedAt`) → MOD-0023 geçiş kapısı hiç değerlendirilemiyordu; canlı doğrulamada yakalandı (2026-07-26). Ayrıca `DocumentManagementAccessMatrixRepositories.cs:70` aynı sınırlamaya çarpıp **bellekte sıralayarak** geçmiş — yorumu duruyor ("in-memory sort … avoids the limitation"), yani bilgi vardı ama genellenmedi.
- **Neden ertelendi:** Kök neden düzeltmesi (global `DateTimeOffsetSerializer` kaydı) **diskteki temsili değiştirir** — mevcut dokümanlar dizi olarak kalır, dolayısıyla veri migrasyonu ister ve tüm servisleri etkiler. Acil olan tek çağrı yerinde cerrahi olarak düzeltildi; sınıfın tamamı ayrı ve onaylı bir dilim olmalı.
- **Gerekenler:** (a) hedef temsile karar (`BsonType.DateTime` — UTC'ye normalize, offset kaybı kabul mü? — yoksa alt-doküman/string); (b) mevcut koleksiyonlar için migrasyon; (c) `DateTime` (skaler, güvenli) ile `DateTimeOffset` (dizi) ayrımının neden **iki farklı `BaseEntity`** sınıfında yaşadığının temizliği (`Domain.Common.BaseEntity` `DateTime` kullanıyor, `Common.Persistence.BaseEntity` `DateTimeOffset`); ~~(d) yeni çok-anahtarlı tarih sıralamasını yakalayan guard~~ → **yapıldı** (2026-07-26), aşağı bak.
- **Doğrulandı ve düzeltildi (2026-07-26):** `BusinessReferenceDataStewardshipRepository.GetUsageRegistrationsAsync` (`UpdatedAt` + `CreatedAt`) gerçek MongoDB'ye karşı koşuldu ve **kırık çıktı** — `UpdatedAt`'i dolu tek bir kayıt tüm listelemeyi öldürüyordu. Aynı desenle (bellekte sıralama) düzeltildi. Platform'da bilinen başka çok-anahtarlı `DateTimeOffset` sıralaması kalmadı; `SavedViewRepository` `DateTime` kullandığı için etkilenmiyor.
- **Guard yerinde:** `DateTimeOffsetSortGuardTests` tüm `services/**` üretim kaynağını tarayıp iki `DateTimeOffset` anahtarlı `SortBy*/ThenBy*` zincirlerini reddediyor. BL-030 kapatılıp global serializer kaydedildiğinde bu guard ve koruduğu bellek-içi sıralamalar **birlikte** kaldırılmalı; `WorkflowInstanceLookupMongoTests.Server_side_sort_on_two_date_time_offset_keys_is_still_rejected_by_mongo` o anda kırılarak bunu hatırlatır.
- **İlgili:** [[feedback_live_verification_gap]] deseni — katmanlar arası sözleşme (burada BSON temsili) test kapsamı dışında.

- **EK BULGU — SESSİZ HÂLİ (2026-08-28, ölçüldü, sahip kararıyla buraya yazıldı):** Bu kayıt bugüne kadar
  yalnız **gürültülü** hâli kapsıyordu — iki `DateTimeOffset` anahtarına birden sıralayan sorgu Mongo'da
  `cannot sort with keys that are parallel arrays` ile patlar, yani **çalışma zamanında görünür**. Sessiz
  hâli ölçüldü ve daha geniş: **tek anahtar + ARTAN sıralama hata vermez, yanlış sıra döndürür.**
  · Kanıt (canlı Mongo, gerçek deney): gerçek zaman sırası `v3·v1·v2·v4` iken Mongo artan sıralaması
    `v3·v2·v4·v1` döndü. Sebep: Mongo bir diziyi ARTAN sıralarken **en küçük** elemanı kullanır — o da
    `offsetMinutes` (-300…180), `ticks` değil. Yani artan sıralama **zamana göre değil, saat dilimine göre**
    yapılıyor. AZALAN ise **en büyük** elemanı alır (= `ticks`) → tesadüfen doğru.
  · ⚠ Hata index'te değil, **veri biçiminde**: index'siz COLLSCAN'de de aynı. Bir index onu yalnız
    *görünmez ve hızlı* yapardı — bu yüzden `ImportedAt` index'i BL-279 Aşama 5'te **kasten eklenmedi**.
- **KAPSAM — ölçüldü 2026-08-28, 26 vakanın hepsi elle doğrulandı (entity okundu, miras zinciri izlendi):**
  | | |
  |---|---|
  | Mongo'ya yazılan entity, kendi `DateTimeOffset` alanı taşıyan | **93** (221 alan) |
  | `CreatedAt`/`UpdatedAt`'i **miras alan** entity | **107** |
  | Etkilenen `BaseEntity` sınıfı | **3 / 5** (`Platform.Common.Persistence`, `DevEnablement.Domain`, `AuthService.GlobalEntityBase`) |
  | Sunucu tarafı sıralama çağrısı (index tanımları hariç) | 226 → 121 ASC · 105 DESC |
  | **`DateTimeOffset` üzerinde ARTAN** | **26** |
  İkiye ayrılıyor ve ele geçirilebilirlikleri farklı:
  · **1–15 API parametresiyle sürülüyor** — `descending ? Sort.Descending(...) : Sort.Ascending(...)`.
    ⚠ Yani ARTAN dalı **istemci seçiyor**: dışarıdan `descending=false` gönderen bir çağrı, saat dilimine
    göre sıralanmış bir liste alır. `FeatureDefinition` · `ModuleCatalogItem` · `ModuleDomain` ·
    `ModuleService` · `PlatformAdministrator` · `SubscriptionPlan` · `Tenant`.
  · **16–26 sabit ARTAN** — yön seçimi yok: `AuditOutboxMessage` · `TaskItem` (×4) · `TaskAssignment` ·
    `ApprovalTask` (×2) · `NotificationDispatch` · `OutboxMessage` · `ProductAbbreviationHistoryEntry`.
  · Yanıltıcı tek vaka: `OutboxEventRepository.cs:30,59` `CreatedAt` üzerinde ARTAN sıralıyor ama o alan
    `DateTime` (skaler) → **etkilenmez**.
  · Bugün kurtaran şey ikinci bir tesadüf: geliştirme ortamında tüm offsetler aynı (+03:00).
    **Çok bölgeli veri geldiği gün bozulur** — ve hiçbir test bunu tutmaz.
- ⚠ **MEVCUT MUHAFIZ BU 26 VAKANIN SIFIRINI GÖRÜYOR** (`DateTimeOffsetSortGuardTests`, ölçüldü):
  · regex'i `Builders<T>.Sort.Ascending(...)` desenini **hiç tanımıyor** (0 eşleşme) → vaka 1–16 görünmez
  · `(?<rest>…)+` niceleyicisi `.ThenBy` zincirini **zorunlu** kılıyor → tek anahtarlı vaka 17–26 eşleşmiyor
  Yani muhafız var, yeşil, ve koruduğu şey bu değil. ⚠ Ayrıca çok anahtarlı
  `Builders<T>.Sort.Ascending(a).Ascending(b)` zincirleri de kapsam dışı (üründe 11 tane; bugün hiçbirinde
  iki tarih anahtarı yok, yani açık değil ama korumasız).
- **Daha önce bulunmuştu ve genellenmemişti:** [[BL-078]] (2026-08-12) tam olarak bu sessiz hâli
  `TaskAssignmentRepository.ListByTaskIdAsync` üzerinde ölçmüş ve doğru teşhis etmiş — *"ofsetler dev
  ortamında aynı olduğu için sonuç doğru görünüyor; farklı saat dilimlerinden yazılmış iki kayıt geldiğinde
  sıra sessizce bozulur."* Tek vaka olarak kaydedilmiş, sınıf olarak genellenmemiş. Bugünkü ölçüm 26 vaka
  olduğunu gösterdi.
- **Serileştirici kaydetmenin bedeli — ölçülmüş EMSAL var, ama bu alan için ölçülmedi:**
  `PlatformTestSerializers.cs:50-62` `GuidSerializer` kaydedildiğinde ne olduğunu yazıyor: iki Mongo test
  sınıfı **11 testle** kırılmış ve kırılma **sessiz** olmuş — *"gürültülü başarısız olmuyor; id ile sorgu
  hiçbir şey bulmuyor ve test 'veri yok' diyor."* ⚠ `DateTimeOffset` için kanıt değildir, ama bu depoda
  global serileştirici kaydının nasıl seyrettiğine dair **tek ölçülmüş örnektir**.
  ⚠ Eski dizi belgelerinin serileştirici sonrası **okunabilir kalıp kalmayacağı ÖLÇÜLEMEDİ** — depoda bu
  soruya cevap veren yazılı bir ifade yok.
- **SAHİP KARARI (GSKU, 2026-08-28):** *"Global serializer'ı doğrudan değiştirmeyin; mevcut BSON verisi için
  migration/compatibility riski var. Önce bütün tek-alan ascending kullanımlarını çıkarın; yanlış sıralamayı
  kanıtlayan guard ekleyin ve ayrı migration/serializer planı hazırlayın. Bu iş BRD index değişikliğine
  karıştırılmasın."* Ayrıca: *"Yeni backlog kimliği açmayın; mevcut BL-030'a ek bulgu ve guard genişletmesi
  olarak yazın."* — BL-299 olarak açılan kayıt bu yüzden buraya taşındı ve kaldırıldı.
- **Sıradaki iş (tek tur, ikisi ayrılamaz):** (1) muhafızı genişlet — `Builders<T>.Sort.Ascending` desenini
  ve tek anahtarlı sıralamayı da tanısın; (2) 26 vakayı karara bağla (bellekte sırala · azalana çevir ·
  kabul et). ⚠ (1)'i (2)'siz yapmak süiti kırmızıya döndürür.

### BL-041 — SLA "yaklaşıyor" sınırı yarım gün kaydı (kabul edildi, kayıt için)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Nedir:** WC-2'de SLA hesabı istemciden sunucuya taşınırken **sınır vakası kaydı**. Eski istemci (`mock-data.js computeSla`) takvim günü sayıyordu: `diffDays = round((son_tarih_gunu - bugun_gunu)/1gun)`, `<= 2` ise `due-soon`. Yeni sunucu hesabı pencereyi `Add(deadline, -2)` ile **son tarih gününün sonundan** geri yürüyor.
- **CT ölçümü (2026-07-30, bugün = 30 Tem):** bugün son tarihli → ikisi de `due-soon` ✓ · **+2 gün (1 Ağu) → eski `due-soon`, yeni `on-track`** ✗ · +3 gün → ikisi de `on-track` ✓. Yani yalnız eşiğin adlandırdığı sınır kaydı; yaklaşık yarım günlük kayma.
- **Ajanın raporu bunu "eşik birebir korundu, kimsenin gördüğü sessizce değişmedi" diye kaydetmişti — ölçümde tutmadı.** Karar yanlış değil, **parite iddiası** yanlıştı.
- **KARAR: bırakılıyor.** Gerekçe: gerçek çalışma takvimi geldiğinde "gün başı" anlamını yitirir (Pazartesi 09:00 mı, Cuma 17:00 mı?), ve `Add` tabanlı tanım o dünyada tutarlı kalan tek tanımdır. Pariteyi kurmak, bugün doğru görünüp takvim gelince yeniden bozulacak bir tanımı sabitlerdi.
- **Etkisi:** sahibin test dokümanı ve beklentileri eski sınıra göre yazılmıştı; "+2 gün" vakası artık `due-soon` değil. Test turunda kusur sayılmamalı.
- **İlgili:** WC-2 (`be0cc190`) · `WorkItemSlaCalculator.DueSoonWithinWorkingDays` (yapılandırma, sözleşme değil).

### BL-040 — 🔴 PLATFORM GENELİ: her FluentValidation hatası sebep kodunu kaybediyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-29. Diten.Platform'da KAPANDI, başlıktaki "PLATFORM GENELİ" iddiası HÂLÂ GERÇEK.**
- **Diten.Platform düzeldi:** `ValidationBehavior.cs:57-59` artık tek sonuç — `ValidationException(failures)`, `ValidationFailure` nesneleri bütün gidiyor. Kenarda kod ekleniyor: `GlobalExceptionHandler.cs:117-121` → `Extensions["reason_code"]`. Kod alan+kuraldan türetiliyor, mesajdan değil; `ValidationReasonCodeTests` sabitliyor.
- ⚠ **Dört servis hâlâ kodsuz:** `Diten.MdmService` (`ValidationBehavior.cs:39-46`), `Diten.DevEnablementService` (`:43`), `Diten.CrmService` (`:47-53`) eski yansımalı `Fail(...)`'i kullanıyor; `Diten.AuthService` `GlobalExceptionHandler.cs:22` çıplak 400 "Validation failed" döndürüyor, `reason_code` YOK.
- ⚠ **Bu yüzden kayıt KAPATILAMAZ.** Yalnız Platform'a bakıp kapatan biri dört servisi kodsuz bırakır.
- **Ölçülmedi:** yayılan kodların frontend resx köprüsünde karşılığı var mı — bakılmadı, ayrı ölçüm gerekiyor.

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-29.**
- **Sorulan uçtan uca kayma KAPANDI ve muhafızlandı:** sağlayıcı 11 kod yayınlıyor, gönderici aynı 11'i destekliyor, vekil aynı 11'i kabul ediyor, Platform 11'ini de sunuyor. Yayınlanıp ucu olmayan ya da ucu olup yayınlanmayan kod YOK. `TaskActionCodeReachabilityTests.cs:40-75` bunu sabitliyor.
- **Kaydın asıl yarısı hâlâ gerçek:** tasarlanan 7 fiil hiç yok — `decline` · `reject` · `dispute` · `delegate` · `pause` · `replan` · `logTime` (Features/Tasks altında 0 eşleşme).
- **Canlı sonuç:** `app.js:3993` zaman çizelgesi düğmesi için `logTime` kodlu bir aksiyon arıyor; hiçbir sağlayıcı üretmiyor, yani düğme kalıcı olarak yok.

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

### BL-036 — Bilgi talebi: kimi beklediğini seçebilme (orta yol) ve tam soru-cevap sistemi
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- **Bugün:** `inquire` tek serbest metin gerekçe alıyor; alıcı yok, cevap yok, `Devam et` her zaman açık.
- **Mockup'ın istediği (tam sistem):** aynı görevde **birden çok** talep · her biri **belirli bir kişiye** · **zorunlu/isteğe bağlı** · cevaplandı/cevaplanmadı takibi · ve **zorunlu talepler cevaplanmadan `Devam et` kapalı**. Ekranda *"2 requests pending, 2 required before resuming"* ve *"Waiting on Mert Demir; Waiting on Zeynep Arslan"*.
- **Sahip kararı (2026-07-28):** şimdilik **orta yol** — beklenen kişinin seçilebilmesi (tipli kimlik, `waitingContext.waitingOn` alanı bunun için zaten ayrılmış ve bugün boş gönderiliyor). Tam soru-cevap sistemi **ertelendi**; iş süreçleri gerektirirse ileride değerlendirilecek.
- **Neden ertelendi:** tam hali yeni bir veri yapısı (talep koleksiyonu), kişi ataması, cevap akışı ve devam etmeyi kapılayan bir kural demek — kendi dilimi. Orta yol ise mevcut alana veri koymak.

### BL-037 — "Kaynak modülde oluştur" hiçbir şey yapmıyor: kalsın mı, kalkacak mı?
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Bugün (canlı ölçüm 2026-07-30):** `+ Yeni → Kaynak modülde oluştur` bir modül seçtiriyor, sonra yalnız *"{modül} modülünde oluşturma açılacaktı (mock)"* toast'ı basıyor. Hiçbir sekme açılmıyor, hiçbir şey oluşmuyor. Toast dürüst — "(mock)" diyor — ama akış kullanıcıya üç tıklama harcatıp hiçbir sonuç vermiyor.
- **Nasıl bu hâle geldi:** eskiden seçilen modülden **rastgele mevcut bir kaydın** detay sayfasını açıyordu; kullanıcı "oluştur" derken alakasız bir kayıt göstermek yanlış eylemdi ve `39a0819f`'te kaldırıldı (kaldırma gerekçesi kodda blok yorum olarak duruyor, "geri koyma" uyarısıyla). Kaldırıldıktan sonra akışın hiçbir işi kalmadı.
- **Neden kurulamıyor:** başka modülde oluşturmak o modülün **create URL**'ini gerektirir; kanonik projeksiyon yalnız `deepLink` taşıyor ve o **mevcut bir nesneyi** adresliyor. Kontrata `createLink` benzeri bir alan eklemek WC-1'i genişletmek demek — her sağlayıcının doldurması gereken yeni bir zorunluluk.
- **Karar gerekiyor:** (a) menü kalemini **kaldır** — WorkCenter'ın işi işi *yürütmek*, başka modülde kayıt açmak değil; kullanıcı o modüle sol menüden gider; (b) kontrata `createLink` ekle ve gerçekten çalıştır; (c) showcase olarak bırak. **CT önerisi: (a).** Aggregator'ın "ASLA" listesi kaynak-tanımlama işlerini dışarıda tutuyor; oluşturma tam olarak kaynak-tanımlama. (c) ise kullanıcıya boş yol gösterir.
- **İlgili:** DCP-004 §5 (aggregator ASLA listesi) · WC-1 kontrat kapsamı · `app.js openCreateInSource`.

---

## CT test turu — 2026-07-31 (BL-042 … BL-049)

> **Kapanış kaydı.** `docs/reference/modules/tenant/workcenter/workcenter-test-sequence.md` oturum 1–7 ve 9, canlı sistemde CONTROL TOWER
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

### BL-054 — 🟠 Görev şablonu yönetim ekranı yok: yinelenen görev bu yüzden içeriksiz üretiyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (CT canlı):** formdan oluşturulan kural `isActive: false` doğuyor → listede **Pasif**, ve
  pasif kural hiçbir şey üretmiyor. Yani "kaydedildi" diyen ama çalışmayan bir kural — bu turda beş
  kez düzelttiğimiz *"başarı raporlayıp bir şey yapmama"* deseninin aynısı.
- **Sahip kararı (2026-08-10): AKTİF doğsun.** Kural oluşturmak zaten "bunu istiyorum" demektir.
- **İkinci istek (sahip):** satır aksiyonlarına **Duraklat / Devam ettir** eklensin. Bugün aksiyonlar
  `Görüntüle · Düzenle · Sil`; bir kuralı geçici durdurmak için forma girip kutu kaldırmak gerekiyor.
  Geçici durdurma sık yapılan iştir, tek tık olmalı.

### BL-056 — 🟡 Görev oluşturma formuna "Tekrarlama" alanı (⚠ BL-054'ten SONRA)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ATAMA TARAFI SUNUCUDA DA KAPANDI (KISMİ) — 2026-09-10, commit `01bc0915`.** Kural artık yalnız
seçicilerde değil, her yazma yolunda soruluyor: `TaskAssigneeEligibility.Judge` (aktif pozisyon + canlı birim
+ kapsam) tek kural; `TaskAssignmentGuard` oluşturma (kişi: `platform.tasks.assign` + uygunluk + kapsam ·
havuz: uygunluk + kapsam), şablondan oluşturma, yeniden atama ve tekrarlayan kural oluştur/güncelle
yollarında kayıt yazılmadan önce çağrılıyor; zamanlanmış üretim muaf (`IsScheduledGeneration`, yalnız
komutta). Kanıt E3: görev testleri 1234/1234 (97 yeni); CT sabotajı — oluşturmadaki koruma çağrısı kaynakta
durup hiç çalışmayınca 13 kırmızı, geri konunca 97/97. **Canlı oturumla doğrulanmadı** → ✅ sahibin kontrol
turunda. Sonuçları: BL-352 (kural kapatılamıyor) · BL-356 (pozisyonsuz kullanıcı iş veremiyor) · BL-353,
BL-354, BL-355 (aynı kuralın hâlâ sorulmadığı üç yol). Listeleme yarısı hâlâ AÇIK; BL-349 (detay erişimi) ona bağlı.

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-29, kayıt İKİYE AYRILIYOR.**
- **Atama tarafı ARTIK AÇIK (bu yarı kapandı):** kural tek yerde — `TaskAssignmentScopeResolver.cs:112-116` (aynı şirket **veya** ast pozisyon **veya** verilmiş birim), `IDataScopeResolver`'dan besleniyor; iki arama işleyicisi de `scope.Allows(position, unit, legalEntityId)` çağırıyor. Kaydın "yeniden ölç" ipuçları da bayatlamış: `AssignablePersonDto` `LegalEntityId` TAŞIYOR (`TaskModels.cs:816`).
- **LİSTELEME tarafı hâlâ örtük (bu yarı AÇIK):** repository yüklemleri kiracı + atanan, ya da kiracı + havuz pozisyonu — **hiçbir yerde şirket koşulu yok** (`TaskRepositories.cs:34-40`, `:82-99`). Projeksiyonda ve `WorkItemActor`'da `LegalEntityId` YOK (grep: 0). `TaskItem`'da da yok — yalnız `OrganizationUnitId`, yani şirket iki sıçramalık bir birleştirme. Ekranda şirket seçici yok.
- **Bugün kimin neyi gördüğünü belirleyen:** kiracı + kullanıcı kimliği + aktif pozisyonlar + izin anahtarları. Şirket DEĞİL.

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-062 — 🔴→🟢 Görev formu 2. tur: kişi alanları çalışmıyordu (KOD YAZILDI, CANLI DOĞRULAMA BEKLİYOR)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM 2026-08-30 — KOD VE TEST YERİNDE; EKSİK OLAN TEK ŞEY CANLI DOĞRULAMA.**
Üç alan gerçek `<select>` (`_Form.cshtml:412,475,494`), hepsi aynı arama kaynağına bağlı (`form-page.js:717-733`), izleyiciler dizi olarak telde (`form.js:267,799`). Native tarih girişi kalmamış, 5 flatpickr alanı var. Test: `tasks-form-pickers-dates-governance.test.js`, 396 satır, ~20 test.
⚠ **Bu kaydı yalnız SAHİP kapatabilir** — giriş gerektiriyor, Control Tower şifre giremiyor.

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM 2026-08-30 — KOD VE TEST YERİNDE; EKSİK OLAN TEK ŞEY CANLI DOĞRULAMA.**
Köprü bağlamanın içinde: `form.js:1290 enhanceSelects` → `:1325 select2` → `:1341-1347` native `change` yeniden yayını. ⚠ Döngü koruması kaydın dediği gibi bayrakla değil, jQuery'nin `event.originalEvent` ayrımıyla (`:1346`). Test: `tasks-form-select2-notification.test.js`, 458 satır.
⚠ **Bu kaydı yalnız SAHİP kapatabilir.**

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-067 — 🟡 Yinelenen görevler şablonsuz doğuyor: hatırlatma, öncelik, etiket hiç ayarlanamıyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-073 — 🔴 MOD-0024 çalışıyor ama kiracı onu KULLANIMA ALAMIYOR: ana veri zinciri hiçbir yerde yazılı değil
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-29.**
- **"Hiçbir yerde yazılı değil" bayat:** `docs/reference/modules/tenant/workcenter/workcenter-onboarding-sop.md` var — sıralı zincir, sessiz-hata tablosu, kabul listesi.
- **Ama zincir KODDA hâlâ ifade edilemiyor:** `ModuleManifestDocument`'ta bağımlılık/önkoşul alanı YOK. Platform genelinde onboarding/hazırlık ön-kontrolü yok (yalnız altyapı sağlık kontrolleri).
- ⚠ **Manifest sağlayıcıları bu kaydı KAPATMAZ:** `TaskManifestProvider` ve `WorkAggregationManifestProvider` 2026-07-25'te eklendi — kaydın 2026-08-11 ölçümünden ÖNCE. (CONTROL TOWER önce bunun aksini varsaydı; ölçüm düzeltti.)
- **YENİ BOŞLUK — kaydın bilmediği:** iki kiracı ana-veri yüzeyi sonradan geldi — `TASK_TYPES` ve `TASK_DOCUMENT_LIST`. SOP ikisinden de HİÇ bahsetmiyor. Yani düzyazı zincir artık koddan geri.

- **Sorun:** motor bitti, ekranlar var, testler yeşil — ama bir kiracının Görev Merkezi'ni **açıp
  kullanabilmesi** sıralı bir ana veri zincirinin doldurulmasına bağlı (şirket → birim → pozisyon →
  kullanıcı → **pozisyon ataması** → yönetici zinciri) ve bu zincir bugüne kadar **hiçbir belgede
  yazılı değildi**. Daha kötüsü: **her eksik halka sessiz başarısızlık üretiyor** — hata mesajı yok,
  boş liste var. 2026-08-11 oturumunda üç kez bizzat yaşandı.
- **📄 GÖVDE AYRI DOSYADA:** [`docs/reference/modules/tenant/workcenter/workcenter-onboarding-sop.md`](./workcenter-onboarding-sop.md).
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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Okuyucu:** sistemi kullanacak **kiracı çalışanı**. Geliştirici değil, yönetici değil. Dolayısıyla
  dosya:satır **yok**, kod adı **yok**, *"handler / endpoint / DTO"* gibi kelimeler **yok** — yalnız
  kullanıcının **ekranda gördüğü** isimler.
- **⚠ [BL-073] ile karıştırılmayacak:** `workcenter-onboarding-sop.md` **kurulum/ana veri** dokümanıdır
  ve **yöneticiye** hitap eder. Bu, **kullanım** kılavuzudur. İkisi birbirine işaret eder, içerik
  **kopyalanmaz**.
- **📄 Dosya (bu turda İSKELET olarak açıldı):**
  [`docs/reference/modules/tenant/workcenter/workcenter-user-guide.md`](./workcenter-user-guide.md) — 11 bölüm başlığı + her bölümün altında
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

### DEC-002 — Zaman kaydının sahibi (DCP-003 B2) · ✅ **TEYİT EDİLDİ — sahip, 2026-09-17: zaman girişinin SoR'u MOD-0280**
> **DURUM:** TEYİT EDİLDİ (2026-09-17) · **PAKET:** [`MOD-0280-FU01` Time Entry & Weekly Timesheet](../../../execution/domains/human-capital-management/module-packs/MOD-0280-FU01-time-entry-weekly-timesheet.md) (ready-for-dev, 2026-09-29; kararlar D1–D13, yerleşim ADR-004)

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

### BL-077 — 🟢 `personInitials` iki bundle'da iki kopya
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-29. Kaydın kanıtı bayat, UYARDIĞI RİSK BÜYÜDÜ.**
- **"Hiçbir alan görevi belgeye bağlamıyor" ARTIK YANLIŞ:** `TaskItem.DocumentReferences` var (`TaskItem.cs:273`), dondurulmuş değer tipi `:379-405`, uçtan uca bağlı — dondurucu kaydı, yazma yolu, okuma DTO'su ve iki ekran.
- ⚠ **AMA mekanizma, kaydın şart koştuğu ayırıcı OLMADAN kuruldu:** ne `Purpose` alanı var ne `checklistItemCode` — ne varlıkta ne DTO'da. Yani tek mekanizma bugün tek amaca hizmet ediyor (**referans**), ve kanıt/kapanış raporu ondan ayrılamıyor.
- **`EvidenceRequired` hâlâ hiçbir şeyi zorlamıyor:** saklanıyor, düzenlenebiliyor, projeksiyona bayrak olarak çıkıyor — kapı yok.
- **Sonuç:** kayıt kapanmıyor; kanıt bölümü yeniden yazıldı ve risk daha keskin hâle geldi.

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (2026-08-13):** "yalnız yorumlar" çipi ancak **12+ olay** varken çiziliyor
  (`ACTIVITY_FILTER_MIN_EVENTS = 12`). Kiracıdaki iki gerçek görevin olay sayısı **8 ve 6**. Dolayısıyla
  "rozet filtre uygulanınca değişmiyor" kuralı **canlı sayfada tetiklenemedi**; yalnız gerçek app.js'i süren
  birim testinde (14 kayıt: 12 olay + 2 yorum) ölçüldü.
- **Neden bırakıldı:** 12 olaylı gerçek görev üretmek için bir görevi 12 kez durum değiştirmek gerekir; bu, test
  verisi uğruna kiracı verisini kirletmek olur. Eşiği düşürmek de ürün kararını teste feda etmek olurdu.
- **Yapılacak (isteğe bağlı):** dev sandbox'ta 12+ geçişli bir tohum görev; o zaman kural canlı da ölçülür.
- **Gelecek regresyon riski: 🟢** — kural birim testinde kilitli, yalnız canlı kanıt eksik.

### BL-086 — 🟢 Kaynak tarayan testler YORUMLARI da tarıyor (kuralı açıklayan metin kuralı düşürüyor)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-090 — 🟢 Detay sayfası 1024'te değil 992'de tek sütuna iniyor; 992–1200 arası rayın kendi tasarımı yok
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (2026-08-13):** sütunlar `col-lg-8` / `col-lg-4`; Bootstrap `lg` = **992px**. 1024px'te ray hâlâ SAĞDA
  (canlı ölçüm: `stacked:false`, hiza 0, tepe 379/379). Yığılma 900px'te doğrulandı (`stacked:true`).
- **Bu turda yapılan:** yığılmış durumda içerik son kartı ile ray ilk kartı arasındaki dikiş **16px** ölçüldü —
  sayfadaki tek 16px, çünkü iki sütun hiç buluşmadığı bir düzenden artakalan çıplak satır oluğuydu. Tek sütunda
  bunlar artık kart-karta bir aralık; `@media (max-width: 991.98px)` içinde **24px**'e getirildi.
- **Açık kalan:** 992–1200 arasında ray ~%33 × ~1000px ≈ 330px'e düşüyor; "Mevcut aksiyonlar" düğmeleri ve durum
  kartı bu genişlik için ayrıca tasarlanmadı. Tabletin kendi kırılma noktası kararı sahibin.
- **Gelecek regresyon riski: 🟢 eklemeli** — mevcut iki kırılma noktası korunuyor.

### BL-091 — 🟡 `ChecklistRequiredOpen` artık adını yalanlıyor; çoğul biçimler hâlâ "item(s)" hilesiyle
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (2026-08-13):** anahtar adı bilinçli olarak KORUNDU (kablo değeri `Required` ve enum ile aynı hizada
  kalsın diye), ama gösterdiği metin artık "beklenen / expected". Yani anahtar adı ile içeriği ayrıştı.
- **İkinci ve daha ciddi kusur:** yedi dilin hiçbirinde gerçek çoğul kuralı yok — `{0} élément(s) attendu(s)`,
  `{0} elemento(s) esperado(s)`, `{0} expected item(s)`. Rusça'nın üç çoğul biçimi, Arapça'nın altısı var;
  parantezli "(s)" hepsinde yanlış. Bugün sayı her zaman ≥1 olduğu için kimse fark etmiyor.
- **Yapılacak:** ICU MessageFormat / `.resx` çoğul desteği kararı — bu tek dize için değil, sayı içeren TÜM
  dizeler için tek seferde. Anahtar adı yeniden adlandırması ancak o göç sırasında anlamlı olur.
- **Gelecek regresyon riski: 🟡** — çoğullaştırma altyapısı gelirse sayı içeren her dize yeniden yazılır.

### BL-092 — 🟡 Kontrol listesi yazmalarının HİÇBİRİ task_transitions'a düşmüyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-095 — 🟠 Sıralama ucu sahiplik sormuyor; sıra da bir anlam taşıyabilir
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (2026-08-14):** `backbone-custom.css` içinde `.wcn-detail-tab`, `.wcn-detail-tab:hover`,
  `.wcn-detail-tab.active`, `.wcn-detail-tab:focus-visible`, `.wcn-detail-tab i`, `.wcn-detail-tab span` ve
  `.wcn-detail-tabpanel` kuralları var. Markup ise `nav-link border shadow-none wc-tab-compact` sınıflarını ve
  `data-wcn-detail-tab` **niteliğini** kullanıyor — `.wcn-detail-tab` **sınıfı** hiçbir yerde uygulanmıyor.
- **Somut sonucu:** sekmelerin odak halkası yazılmıştı ve çalışmıyordu; `:focus-visible` kuralı var olmayan bir
  sınıfı bekliyordu. Bu turda halka `.wc-tab-compact` üzerinden verildi, yani **semptom kapandı, ölü blok durdu**.
- **Yapılacak:** ya blok silinsin, ya markup o sınıfı taşısın. İkisinden biri; ikisi birden değil.
- **Gelecek regresyon riski: 🟢** — bugün hiçbir şeyi boyamıyor.

### BL-102 — 🟢 [YAŞAM DÖNGÜSÜ KARTI] Hedef 96px tutmadı: 177 → 114px (%36), 18px açık kaldı
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (2026-08-14):** `Tab` gerçek tuş olarak çalışıyor (60 durak kaydedildi). `Return` ve `Enter`
  gönderildiğinde odaklanmış düğmede **`keydown` bile tetiklenmiyor** (dinleyici kuruldu, olay dizisi boş kaldı).
  Yani tuş sayfaya ulaşmıyor.
- **Bunun yerine kanıtlanan:** engel uyarısının bağlantısı native `<button>` (Tab ile ulaşılıyor, `tabIndex 0`),
  handler delegasyonlu `click` dinleyicisinde ve **canlı tıklamayla** çalıştığı doğrulandı; ayrıca
  `scrollIntoView` ve `focus` çağrılarının doğru öğeye yapıldığı köstebekle ölçüldü.
- **Yapılacak (istenirse):** panel görünürken elle Enter/Space denemesi.
- **Gelecek regresyon riski: 🟢** — ölçüm boşluğu.

### BL-105 — 🟠 [KOMUT KARTI] `closedAt` normalizasyonu sözleşme muhafızını sessizce siliyordu (BU TURDA YAKALANDI)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-30. Başlık açık hata gibi okunuyor; açık olan yalnız KUYRUK.**
- **Adı geçen kusur KAPANDI:** `work-items-api.js:92-94` artık `closedAt`i ayrıştırılabilirlik kontrolünden geçiriyor; koruduğu sözleşme kuralı `fixture-contract.js:420` (`CLOSED_AT_INVALID`), testi `workcenter-next-sla-closed-freeze.test.js:128`.
- **Kaydın kendi kuyruk maddesi GEÇERLİ:** üç kardeş tarih hâlâ koşulsuz normalleştiriliyor — `dueAt` (`:63`), `plannedDate` (`:66`), `startAt` (`:73`). Bugün zararsız, çünkü o üçü için `*_INVALID` kuralı yok (grep: sıfır).
- **Kalıcı çözüm yapılmadı:** ham DTO önce doğrulanıp sonra uyarlanmalıydı; sıra hâlâ uyarla-sonra-doğrula (`:140-141`).

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-110 — 🟠 [MEVCUT AKSİYONLAR] Brief'in iki varsayımı ölçümde çürüdü — cümle SİLİNMEDİ, uyarı TAŞINMADI
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ — 2026-08-30. (a) geçerli, (b) kaydın kendisinden ESKİ bir işi istiyor.**
- **(a) doğru:** açıklama "kabul edilmemiş olarak" diyor (`WorkCenterNextIndex.tr.resx:1759`), etiket demiyor (`:1537`). Silinmemiş.
- **(b) yanlış istek:** kayıt "sunucu alt görev engelini `disabledReasonCode` ile bildirmeli mi?" diye soruyor — **zaten bildiriyor**: `SUBTASK_BLOCKED` (`TaskModels.cs:292`), engel `TaskWorkItemProvider.cs:1212-1220`, düğmeye yazımı `:575-585`. Ve bu yol kaydın tarihinden **iki hafta önce** geldi (`e531b24b`, 2026-07-29); kayıt 2026-08-14.
- **Gerçekte açık olan tek şey ÖNCELİK kuralı:** yeniden yazım yalnız hâlâ etkin bir aksiyona uygulanıyor (`:578`). Canlı ölçümde `CHECKLIST_INCOMPLETE` görülmesinin sebebi buydu — sistem özelliği değil, tek görevlik gözlem.

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

### BL-112 — 🟡 [MEVCUT AKSİYONLAR] Odak halkası bizim sınıfa eklendi ama tema 3px kendi rengiyle eziyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (gerçek Tab):** kart düğmelerinde önce **0/3** odak göstergesi vardı (BL-100'ün `.btn` boşluğu).
  `.wcn-act-btn:focus-visible` eklendikten sonra **3/3** gösterge var.
- **Ama uygulanan kural bizimki değil:** hesaplanan `outline` **3px** ve düğmenin kendi türünün rengi
  (ikincil `rgb(133,146,163)`, yıkıcı `rgb(255,62,29)`), bizim kuralımızın `2px var(--bs-primary)`'si değil.
  Yani `core.css`'teki `.btn:focus-visible` kazanıyor; bizim kuralımız yalnız `outline: 0` bastırmasını
  kaldırmış oluyor.
- **Sonuç bugün kabul edilebilir** — hatta tür rengi tek tip primary halkadan okunaklı. Ama **ev tokenının
  uygulandığı sanılmamalı**; BL-100 çözülürken bu etkileşim yeniden ölçülmeli.
- **Gelecek regresyon riski: 🟡** — BL-100 dokunulduğunda bu kart yeniden ölçülmeli.

### BL-115 — 🟡 [MEVCUT AKSİYONLAR / ÖZET] Kartlar kısaldı ama İKİ KART kendi içinde büyüdü
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Tanım listesi ızgaranın yerini alınca `summaryFact` yardımcısının **hiç çağıranı kalmadı** (ölçüldü) ve
  kaldırıldı; kuralı ("boş alan çizilmez") `renderSummary`'nin `row()`'unda yeniden ifade edildi.
- `.wcn-facts`, `.wcn-fact-wide`, `.wcn-fact-body`, `.wcn-fact-label`, `.wcn-fact-value`, `.wcn-fact-tags`
  **ölü olarak işaretlendi, silinmedi** (bir tur geri dönüş payı). **`.wcn-facts-grid` (iş bağlamı bölümleri) ve
  dosyanın üst kısmındaki ayrı `.wcn-fact` bloğu farklı bileşenler — dokunulmadı.**
- **Yapılacak:** görünüm kabul edilince blok silinsin.
- **Gelecek regresyon riski: 🟢.**

### BL-117 — 🟢 [ÖZET] Golden referanstan BİLİNÇLİ SAPMA: boş alanda "-" basmıyoruz
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Golden referans** (`Views/DevEnablement/GoldenReferenceCompact/Details.cshtml`) boş değer için `-` basıyor:
  `@(string.IsNullOrWhiteSpace(Model.Code) ? "-" : Model.Code)`.
- **Özet kartı basmıyor — alanı hiç çizmiyor.** Gerekçe: tire, "alan kontrol edildi ve boş bulundu" iddiasıdır;
  okuyucu bunu "yüklenemedi" durumundan ayırt edemez. Bu sayfada olguların çoğu isteğe bağlı (başlangıç tarihi,
  tahmini süre, etiketler), dolayısıyla tire basmak kartın yarısını anlamsız çizgiyle doldururdu.
- **TEK İSTİSNA — Atanan:** boşsa satır YİNE çizilir ve "Atanmamış" der. Atanansız görev eksik alan değil,
  sonucu "kimse fark etmezse iş bekler" olan bir OLGU.
- **Sapma bilinçli ve kayıtlı** — golden referansı takip eden bir sonraki ekran bunu drift sanmasın diye.
- **Gelecek regresyon riski: 🟢.**

### BL-119 — 🟠 [VERİ] Seed görevlerinin açıklaması durum cümlesi gibi yazılmış
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-123 — 🟡 [ALT GÖREVLER / KONTROL LİSTESİ] Hover tonu ölçülebilir ama neredeyse görünmez
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-127 — 🟡 [KAYNAK] Yabancı sağlayıcı kipi CANLI DOĞRULANMADI
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM DÜZELTMESİ 2026-08-30:** kaydın önkoşulu *"ikinci sağlayıcı geldiğinde"* **ZATEN GERÇEKLEŞTİ** — `WorkflowApprovalWorkItemProvider` var ve DI'da kayıtlı (`DependencyInjection.cs:298`). Kalan tek şey canlı koşu.

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**⚠ ÖLÇÜM NOTU 2026-08-30:** BL-135 aynı boşluğun ikinci kaydı, aynı çözümü bekliyor. **Birleştirilmeli.**

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

### BL-132 — 🟠 [KOMPOZİSYON] 900px'te aksiyonlar 1876px aşağıda — DOM/görsel sıra çelişkisi kararı sizde
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-135 — 🟡 [ÖLÇÜM DİSİPLİNİ] İkinci kez kendi CSS eklemem stil dosyasını kırdı
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-137 — 🟠 [DAR EKRAN] Şerit klavye kullanıcısına kısayol SAĞLAMIYOR
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Ölçüm (900px):** şeridin düğmeleri sekme sırasında **139/149** — yani en sonda, çünkü şerit DOM'da en son.
  Görsel olarak ekranın altında sabit duruyor ama Tab ile oraya varmak için sayfanın tamamı geçiliyor.
- **Sonuç:** şerit fare/dokunmatik kullanıcı için kısayol, klavye kullanıcısı için değil. Aksiyon kartı da
  900px'te içerikten sonra geldiği için klavye kullanıcısı her hâlükârda uzakta.
- **Neden `order`/DOM taşıma yapılmadı:** sahip kararı bu turda aksiyon kartının yerini değiştirmemekti; DOM'da
  şeridi öne almak da görsel/sekme ayrışması yaratırdı (BL-132'nin aynısı).
- **Seçenekler:** (a) şeride `accesskey` · (b) sayfa başına "aksiyonlara atla" bağlantısı · (c) BL-132'nin
  kararıyla birlikte çözülsün.
- **Gelecek regresyon riski: —** (karar bekliyor).

### BL-142 — [KİŞİSEL KATMAN] Dört ayar projeksiyonda; ekranda yeri kararlaştırılmadı
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Projeksiyona giren şekiller (canlı ölçüldü, 2026-08-14):
  `watchers: [{ person: {id, displayName, isCurrentUser}, role: "Watcher|Consultant|Informed" }]` · yoksa alan yok
  `delegationAllowed: true|false` · `notifications: { emailEnabled: bool, events?: string[] }` (events **yoksa**
  = "hiç seçilmedi, hepsi gönderilir"; **boş dizi** = "hiçbiri seçilmedi") · `reminderLeadDays: 3` · yoksa alan yok.
- Canlı doğrulama, seed edilip geri alınan bir görevle: dördü de tel üstünde göründü, izleyici adıyla birlikte.
- **Ekrana konmadı, bilerek.** Hangi kartta duracakları tasarım kararı. Öneri (CT'ye): izleyiciler ve devir
  izni Özet'e; bildirim tercihleri + hatırlatma günü tek bir "Bildirimler" satırına.
- **Gelecek regresyon riski: 🟢** — hepsi opsiyonel ve null'da atlanıyor.

### BL-143 — [KİŞİSEL KATMAN] Erteleme gelen kutusu süzmesi hâlâ istemcide
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **ÖLÇÜLDÜ:** `segmentFor` ve liste süzgeci `item.snoozedUntil`'ı tarayıcıda okuyor. Artık sunucudan geliyor,
  ama **kararı** hâlâ istemci veriyor: sayfalama sunucuda olsaydı ertelenmiş işler sayıya dahil olurdu.
- Bugün zararsız (sayfalama istemcide). Sunucu tarafı sayfalama geldiği gün süzme de sunucuya taşınmalı, yoksa
  "3 iş" yazan bir sekme 2 satır gösterir.
- **Bu turda uygulanmadı, karar kaydedildi.** **Gelecek regresyon riski: 🟡.**

### BL-145 — [GÖÇ] 137 görevin 136'sında overlay belgesi yok; geri doldurma yapılmadı
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **ÖLÇÜM (2026-08-14, dev):** `task_items` = 137, `task_personal_overlays` = 1 (bu turda canlı testte yazılan).
  Yani **mevcut her görev** overlay'siz.
- Davranış ölçüldü: overlay yoksa `personal` alanı **hiç gönderilmiyor** (boş kap değil), istemci `item.notes`'u
  boş diziye normalleştiriyor, kart yalnız ekleme satırını çiziyor. Geri doldurma **gerekmiyor ve yapılmadı** —
  boş bir belge yazmak, 137 kaydı hiçbir şey için üretmek olurdu.
- Aynı şey erteleme için: süresi geçmiş bir erteleme `null` olarak yansıtılıyor, kararı sunucu veriyor.
- **Gelecek regresyon riski: 🟢.**

### BL-148 — [ÖLÇÜM SINIRI] Alt görev satırının hizası kural listesinden doğrulandı, DOM'dan değil
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Kusur 4'te "satır dili üç listede aynı" iddiası için kontrol listesi satırı (`.diten-checkitem`) ve not satırı
  (`.wcn-note-row`) **canlı DOM'da** `center` ölçüldü. Alt görev satırı (`.wcn-subtask`) test görevinde yoktu;
  `center` değeri tarayıcının **kural listesinden** okundu.
- Alt görevi olan bir görevde DOM ölçümü yapılmadı. Küçük ama açıkça yazılıyor.
- **Gelecek regresyon riski: 🟢.**

### BL-149 — 🔴 [ENTERPRISE STRATEGY] Legacy emeklilik kapısı: eşdeğerlik matrisi olmadan silme yok
> **DURUM:** AÇIK · **SAHİP:** CONTROL TOWER

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

### BL-153 — [ÖLÇÜM SINIRI] Kişisel kart 900px'te ekran görüntüsüyle doğrulanamadı (dördüncü kez)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- 900px'te Kişisel kart sayfa-y **2180**'de başlıyor (2.4 ekran aşağıda) ve bu ortam oraya kaydıramıyor
  (`scrollY` 0'da kalıyor; BL-098). Bu turda dördüncü kez.
- **Ne ÖLÇÜLDÜ:** kartın tüm hesaplanmış stilleri, satır hizası, satır dili karşılaştırması, kontrol sayıları —
  `getComputedStyle` ve `getBoundingClientRect` kaydırmadan bağımsız çalışıyor. **Ne ÖLÇÜLEMEDİ:** kartın 900px'te
  nasıl GÖRÜNDÜĞÜ (ekran görüntüsü).
- **Gelecek regresyon riski: 🟢** — ölçüm boşluğu, kod boşluğu değil.

### BL-156 — [ÖLÇÜM] Yönlendirme ve engel uyarısı canlı veride hiç yan yana gelmiyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Sıra kararı verildi ve gerekçelendirildi: **yönlendirme önce, engel sonra.** Yönlendirme SIRADAKİ işi söyler;
  engel bir şeyin HENÜZ yapılamadığını söyler — engeli önce okuyan okuyucunun onu bağlayacağı bir şey yoktur.
- **⚠ CANLI ÖLÇÜLEMEDİ:** yüzeydeki 20 görev `pendingAcceptance`, 4 görev engelli/bekleyen, **kesişim sıfır**.
  İkisi bugün gerçek bir görevde asla birlikte çıkmıyor. Sıra, ikisini birden taşıyabilen bir fixture ile testte
  sabitlendi; canlı boşluk ölçümü yapılamadı.
- **Gelecek regresyon riski: 🟢.**

### BL-157 — [BRİFİNG DÜZELTMESİ] Ölçüm görevinin kontrol listesi boş, tek maddeli olan başka görev
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Brifing 46f6a43a'yı "alt görev, tek maddeli kontrol listesi" diye veriyordu. **Ölçüm: 0 madde.**
  Tek maddeli olan **d77e97d6** (o da bir alt görev). 98d1f94e'de 6 madde var.
- Kusur 3 ölçümü d77e97d6 (1) ↔ 98d1f94e (6) üzerinden yapıldı.
- **Gelecek regresyon riski: 🟢** — veri seçimi hatası, kod değil.

### BL-158 — [ÖLÇÜM] Alt görev satırında sıralama denetimi YOK, aynı desen orada geçerli değil
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- "Kardeşini bırakma" kuralı gereği ölçüldü: alt görev satırının çocukları `wcn-subtask-check · wcn-subtask-body ·
  wcn-subtask-status · dropdown` — **taşı düğmesi ya da tutamak yok.** Alt görevler sıralanamıyor, dolayısıyla
  madde sayısına bağlı yükseklik değişimi orada oluşamaz.
- Düzeltme yalnız kontrol listesine uygulandı, çünkü desen yalnız orada var. Ölçülüp yazıldı.
- **Gelecek regresyon riski: 🟢.**

### BL-159 — [TEST] "cancelling a subtask" testi tam süit altında kararsız (flaky)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- `wcn-detail-three-regions.test.js :: calls the cancel transition once the user confirms` bir tam süit
  koşusunda düştü ("reached no endpoint at all"), hemen ardından dosya tek başına 208/208 geçti ve ikinci tam
  süit koşusunda da geçti.
- Sebep: test sahte `showConfirm`'ü `setTimeout(…, 5)` ile çözüp `setTimeout(…, 30)` bekliyor. Tam süit yükü
  altında 25ms'lik pay yetmiyor. **Bu turdaki değişikliklerle ilgisi yok** — zamanlamaya duyarlı bir bekleme.
- **Yapılacak:** sabit beklemeyi bir koşul beklemesiyle değiştir (çağrı gelene kadar yokla). Bu turda
  yapılmadı; testin kendi konusu bu turun konusu değil.
- **Gelecek regresyon riski: 🟡** — yalancı kırmızı, gerçek bir kusuru gizlemez ama güveni aşındırır.

### BL-160 — ⛔ YAPILAMADI — İki uyarı YAPISAL OLARAK bir arada olamıyor (İş 4b'nin cevabı)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Brifing "bildirim ALICININ dilinde gitmeli, bu modülde dil seçimi çözülmüş, aynısını kullan" diyordu.
  **ÖLÇÜM: çözülmüş olan şey kiracı dili.** `TaskNotificationService` `Locale: null` geçiyor ve
  `INotificationLocaleResolver` kiracının yapılandırılmış dilini döndürüyor — çünkü **AuthService'in User
  varlığında dil alanı yok** (servisin kendi yorumu bunu uzun uzun yazıyor).
- Canlı kanıt: yorum bildirimi `Locale = en` ile gitti (kiracı dili), alıcı `agent@diten.com`.
- Yorum şablonu yine de **yedi dilde** tohumlandı; eksik olan alıcı başına dil, şablon değil.
- **Yapılacak (bu turda YAPILMADI):** User'a dil alanı + dil grubuna göre gönderim. Bu MOD-0018 işi.
- **Gelecek regresyon riski: 🟡** — çok dilli bir kiracıda herkes aynı dili alıyor.

### BL-162 — [BİLDİRİM] Çözülemeyen alıcı sessizce düşüyor (loglanıyor ama kimseye söylenmiyor)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Canlı ölçüm: `task.notification.recipients_unresolved Count=1` — adaylardan biri AuthService'te
  çözülemedi ve **bildirilmedi**. Log var, ekranda iz yok.
- Bugün doğru davranış (yazma başarısız olmamalı), ama "izleyici ekledim, haber gitmedi" durumunu kimse göremiyor.
- **Öneri:** çözülemeyen alıcı sayısını görev detayında sessiz bir satır olarak göster, ya da yönetici için bir
  rapor. Karar CT'de.
- **Gelecek regresyon riski: 🟢.**

### BL-168 — [TEST] `creating a subtask in detail` testi tam süit altında zaman aşımına uğrayabiliyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Bir tam süit koşusunda 5000ms vitest zaman aşımı; dosya tek başına 117/117, ikinci tam koşuda da geçti.
  BL-159/BL-163 ile aynı sınıf: yük altında yetmeyen bekleme. **Bu turun değişiklikleriyle ilgisi ölçülmedi
  ama yol farklı** (alt görev paneli, `inquire` diyaloğu değil).
- **Yapılacak:** aynı `until(...)` desenine çevir. Bu turda yapılmadı.
- **Gelecek regresyon riski: 🟡** — yalancı kırmızı.

### BL-170 — [İŞ 3 CEVABI] Mevcut "kayıt öncesi" cümlesi alan değişiklikleri için DOĞRU DEĞİL
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Kısıtlı bir alanın (`ViewPermission` taşıyan) değerini GÖREMEYEN aktör onu YAZAMIYOR da — canlı 400.
  Doğru davranış, ama bu turun test edeceği şey okuma yolu olduğu için geçmiş satırı doğrudan Mongo'ya kondu.
- Okuma ölçümü: değerler (45000/52000), tanım kodu ve etiket **tüm yanıtta hiç geçmiyor**; ekranda satır
  **"bir alan değiştirildi"** olarak duruyor.
- **Açıkça yazılıyor:** ikinci aktör için parola CT'de yok; ölçüm **API katmanında** yapıldı, ekranla değil.
- **Gelecek regresyon riski: 🟢.**

### BL-172 — [KARAR] Geçmişte "aktör" alanı olmayan eski satırlar var
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Bu turdan önce yazılmış iki `Edited` satırı `ActorUserId = null` taşıyor (BL-169 düzeltilmeden önce
  üretildiler) ve ekranda "İsim bulunamadı" diyorlar. **Geriye doldurma YAPILMADI** — kim olduğu kayıtlı değil
  ve üretilemez; uydurmak günlüğün tek işini bozardı.
- Bunlar yalnızca dev veritabanındaki test kayıtları. Üretimde aynı durum oluşamaz (alan günlüğü bu turla
  birlikte, aktör bildirimiyle birlikte geliyor).
- **Gelecek regresyon riski: 🟢.**

### BL-174 — [KARAR SENİN] `DelegationAllowed` varsayılanı `false` ve canlı veri bunu doğruluyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Enum: `Watcher`, `Consultant`. "Bilgilendirilen" (RACI'nin *Informed*'ı) **yok**. Tasarım kararı "ad + sessiz
  rol soneki" dediği için üçüncü bir rol uydurulmadı; yanlışlıkla eklenen `WatcherRoleInformed` anahtarı yedi
  dilden de geri alındı.
- Üçüncü rol istenirse enum, create formu ve yedi dil birlikte açılmalı — ekran tarafı zaten hazır.
- **Gelecek regresyon riski: 🟢.**

### BL-177 — [YAPILMADI] `.wcn-notes-composer` ayırıcısı eşit değil (yan panel, detay kartı değil)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Süpürmede bulundu: `margin-block-start: 1rem` üstte, `padding-block-start: .75rem` altta → **16 / 12**, eşit değil.
- Sekiz detay kartından biri değil; hızlı notlar YAN PANELİNDE yaşıyor. Dahası panel bugün **arayüzden
  açılamıyor**: `state.notesOpen`'ı çeviren bir düğme render edilmiyor ("Hızlı not" başka bir akış).
- Bu yüzden **CSS metninden ölçüldü, ekrandan değil** — ve bu turda değiştirilmedi: ölçemediğim bir yüzeyde
  düzeltme yapmak, düzelttiğimi ekranda gösteremeyeceğim bir değişiklik demek.
- **Gelecek regresyon riski: 🟢** (kart ailelerinden bağımsız).

### BL-185 — [KARAR SENİN] Ortak modalin girdisine alan ikonu takılamıyor (İş 2b'nin ölçümü)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Ertele diyaloğunun placeholder'ı yeni bir biçim icat etmedi: **ürünün kendi maskesi** kullanıldı — create
  formundaki iki tarih alanı (`Views/Tasks/_Form.cshtml:173,189`) `YYYY-MM-DD` yazıyor, yedi dilde de aynı.
- Ama oradaki değer **doğrudan .cshtml'e gömülü**, bir kaynak anahtarı değil: Türkçe bir okuyucu için "AA/GG"
  demek isteseydik, o iki alan için kod değişikliği gerekirdi. Ertele'nin anahtarı 7 dilde AYRI duruyor
  (bugün hepsi aynı değeri taşıyor), yani orada karar koda dokunmadan değişebilir.
- **Karar senin:** maskeler yerelleşsin mi (o zaman create formu da anahtara taşınmalı), yoksa ürün genelinde
  nötr `YYYY-MM-DD` mi kalsın.
- **Gelecek regresyon riski: 🟢.**

### BL-194 — [ÖLÇÜM] Textarea'lı diyaloglarda Enter onaylamaz (kütüphane davranışı)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Geriye uyum ölçümünde çıktı: tek satırlık girdide Enter **onaylıyor**; `textarea` kullanan diyaloglarda
  **onaylamıyor** — çünkü orada Enter satır başıdır. SweetAlert'in kendi davranışı, bu turda değişmedi.
- `showInput` kullanan altı çağrının beşi textarea; yani onlarda Enter zaten hiç onaylamıyordu. Kayıt, ileride
  "Enter çalışmıyor" diye bildirilirse kusur mu davranış mı sorusunu bir kez daha ölçmemek için.
- **Gelecek regresyon riski: 🟢.**

### BL-195 — [ÖLÇÜM] Sol menü bir süre sonra kısalıyor, yenileyince geri geliyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-198 — [KARAR SENİN] "Ertelenmiş" çipi Havuz ve Geçmiş'te de görünüyor (ama orada gizlemiyor)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Kapsam kararı gereği gizleme yalnız `inbox`/`islerim`'de. Çip ise sayısı sıfırdan büyükse **her sekmede**
  çiziliyor ve orada **normal daraltan** bir sinyal gibi davranıyor.
- Canlı görüldü: Geçmiş'te "Ertelenmiş 1" çıktı — ertelenip sonra tamamlanmış işi bulmaya yarıyor, hiçbir şeyi
  gizlemiyor. Zararsız, hatta faydalı; ama aynı çip iki sekmede iki farklı şey yapıyor.
- **Karar senin:** (a) böyle kalsın (Geçmiş'te "parkettiğim ve sonra bitirdiklerim" araması); (b) çip yalnız
  `SNOOZE_TABS`'ta çizilsin.
- **Gelecek regresyon riski: 🟢.**

### BL-209 — [YAPILMADI] Enterprise Strategy testleri kırmızı (bu turdan önce de kırmızıydı)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- `npx vitest run tests/` → **1517 geçti, 9 kırmızı**; hepsi `strategy-apis`, `objectives-edit-hydration`,
  `planning-cycles-*`, `strategy-periods-*` dosyalarında.
- `git stash` ile doğrulandı: bu turun değişikliklerinden **önce de** kırmızıydılar. WorkCenterNext'e ait
  değil, bu turda düzeltilmedi.
- Ayrıca `wcn-text-in-boxes.test.js` içindeki BL-201 testlerinden biri (`inline-size: 100%` bekleyen) de
  bu turdan önce kırmızıydı — o test bloğu BL-206 ile tamamen değiştirildi.

### BL-215 — [YAPILMADI] Görünüm paketinin DÖRT eski kopyası WorkCenter dışında duruyor
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Ölçüldü: `popup: 'rounded-4 shadow-lg'` dizesi bu turdan ÖNCE de dört dosyada kendi kopyasını taşıyordu —
  `shared/premium-modal.js`, `Account/login.js`, `Account/forgot-password.js`, `Account/reset-password.js`.
- A3'ün kapsamı WorkCenter'dı: bu turda **hiçbiri değiştirilmedi**. Test onları **listeliyor** (birine
  dokunulursa kırmızı olur) ve WorkCenter'ın **sıfır** kopya taşıdığını kilitliyor.
- Doğrusu: dördü de `window.DitenDialogAppearance()` okumalı. Account ekranları ayrı bir tur.
- **Gelecek regresyon riski: 🟡** — paket değişirse bu dört ekran ayrışır.

### BL-218 — [ERTELENDİ, silinmedi] Genel not ve ajanda: ürünün istediği, arkası olmayan iki özellik
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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

### BL-225 — [TASARLANDI, ÖLÇÜM BEKLİYOR] Onay diyaloğunda ağırlık kademesi
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- **Soru:** her yıkıcı aksiyonun bir geri alma yolu var mı? Grep'lenebilir: `reopen`, `reactivate`, `restore`,
  `undo`, soft-delete alanları.
- **Bilinen tek ölçüm:** WorkCenterNext'te "Görevi iptal et" **geri alınamıyor** — `reopen`/`reactivate`/`undo`
  yok.
- Çıktısı BL-225'in girdisi. CT "ucuz bir tur" dedi.
- **Gelecek regresyon riski: 🟢.**

### BL-229 — [YAPILMADI] Ürünün "geri çekilmiş metin" tonu WCAG AA altında
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Ölçüldü (canlı, iki tema): `--bs-secondary-color` üzerine kurulu geri-çekilmiş satırlar —
  iptal edilmiş alt görev **2.29** (açık) / **3.49** (koyu); **tamamlanmış** alt görev kendi dolgusu üzerinde
  **1.83**. AA eşiği normal metin için 4.5.
- Bu ton temanın kendi devre-dışı rengi; elle daha koyu bir gri seçmek aynı kusuru başka mekanizmayla geri
  getirir. Doğru çözüm token seviyesinde ve **bütün ürünü** etkiler.
- BL-228'de tanıtılmadı (opaklık kaldırılınca kontrast **arttı**), sadece görünür oldu.
- **Gelecek regresyon riski: 🟡** — okunabilirlik borcu, her yeni "soluk" satırda büyüyor.

### BL-235 — [KAYIT] Beş yetki sözleşmede var, sağlayıcıda yok
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-252 — [ÖLÇÜLDÜ, DEĞİŞİKLİK YOK] "SLA riski" iki yerde ama KOPYA DEĞİL
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Ölçüm önce yapıldı, karar sonra:
  - **Sinyal çipi** → `slaState ∈ {overdue, due-soon}`. Tek sabit ikili, tek anahtar.
  - **"SLA durumu" seçicisi** → DÖRT değer üzerinde çoklu seçim: `overdue · due-soon · on-track · no-sla`.
- Yani seçici kesin olarak daha ifadeli: "yalnız gecikmiş", "yolunda", "tarihi yok" çiple **sorulamıyor**.
  Çip, seçicinin bir ÖN AYARI; kopyası değil. Birini kaldırmak bir şeyi eksiltirdi.
- İkisi eksenler-arası AND kuralıyla birleşiyor — bu turda kurduğumuz kuralın aynısı, tutarlı.
- **Karar: ikisi de kalıyor.** Hiçbir URL parametresi kaldırılmadı, eski bağlantılar aynen çalışıyor.

### BL-259 — [ÖLÇÜLDÜ, DEĞİŞİKLİK YOK] Takvimde bir güne çok iş
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-263 — [PAKETE YAZILDI 2026-08-26] Tür değişikliği kontrolü + neden referans veri değil
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-270 — doküman listesi ekranı GENİŞ ekranda daha dar (2026-08-26, ölçüldü, düzeltilmedi)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-274 — "31 türün 9'u" ölçümü tutmadı: on beş, ve üç ayrı sebep (2026-08-26)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-276 — seçici, zaten atıf yapılmış dokümanı arama sonucunda yine listeliyor (2026-08-26, kozmetik)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- Ölçüldü: `GMG-QMS-SOP-0005` seçili çipken aynı doküman arama sonucunda da çıkıyor. Tıklamak zararsız
  (`Map` aynı UID'i tek kayıt tutar), ama okuyucu iki satır görüyor.
- Düzeltilmedi: bu turun kapsamı atıf sözleşmesiydi ve davranış yanlış değil, yalnız gereksiz.
- Seçenek: seçilmiş satırı sonuçtan düşürmek yerine "zaten eklendi" diye işaretlemek — düşürmek, arayıp
  bulamayan okuyucuya "bu doküman yok" dedirtir.
- **Gelecek regresyon riski: 🟢**

### BL-277 — Mongo test düzeneği: koşu başına veritabanı, ölçülen borç ve Bölüm B (2026-08-26, muhafız kuruldu, ihlaller DURUYOR)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-279 — depoda okunan ama manifestte olmayan koleksiyonlar (2026-08-26, kör nokta KAPANDI; index'ler HÂLÂ yok)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

**GÜNCELLEME (Aşama 5, 2026-08-27) — asıl borç ölçüldü ve 9 koleksiyonun 8'inde ödendi.**
Sayı **9** olarak doğrulandı (manifestte `Array.Empty` ile duran koleksiyonlar sayıldı — 6 değil; 6 bu turun
bulduğu, 3 önceki turun). Her koleksiyonun deposu okundu, sorgu deseni çıkarıldı ve **explain ile ölçüldü**:
önce canlı `diten_personalization_dev` üzerinde (yalnız okuma), sonra aynı verinin bir kopyası üzerinde
index'ler kurulup yeniden. **Önce → sonra:**

| koleksiyon | sorgu (deposundan) | önce | sonra | eklenen index |
|---|---|---|---|---|
| `task_comments` | `{TenantId, IsDeleted, TaskItemId}` (+`$in`) | COLLSCAN 44 | IXSCAN 2 | `ix_task_comments_tenant_task` |
| `task_transitions` | `{TenantId, IsDeleted, TaskItemId}` (+`$in`) | COLLSCAN 102 | IXSCAN 6 | `ix_task_transitions_tenant_task` |
| `task_types` | `{TenantId, IsDeleted, Code}` · `ListActive` sort `Code` | COLLSCAN + SORT | IXSCAN, SORT yok | `ux_task_types_tenant_code_active` (unique, partial) |
| `document_reference_entries` | `{TenantId, DeletedAt, ListVersionId}` sort `DocumentCode` | SORT+COLLSCAN 717 | IXSCAN 50, SORT yok | `ix_…_tenant_version_code` |
| `document_reference_entries` | `… + DocumentUid $in` | SORT+COLLSCAN 717 | IXSCAN 1 | `ix_…_tenant_version_uid` |
| `document_reference_list_versions` | `{TenantId, IsDeleted, ContentHash, WithdrawnAt}` | COLLSCAN | IXSCAN 1 | `ix_…_tenant_hash` |
| `notification_event_definitions` | `{IsDeleted, EventCode}` · liste sort `EventCode` | COLLSCAN + SORT | IXSCAN, SORT yok | `ux_…_event_code_active` (unique, partial) |
| `document_management_collection_provisioning_evidence` | `{TenantId, IsDeleted, CollectionInstanceId}` / `…BaselineReleaseId` | COLLSCAN 3000\* | IXSCAN 1 / 100 | `ux_…_tenant_instance_active` + `ix_…_tenant_baseline` |
| `document_management_collection_deviations` | `{TenantId, IsDeleted, BaselineReleaseId[, Status]}` | COLLSCAN 3000\* | IXSCAN 100 | `ix_…_tenant_baseline_status` |

\* bu iki koleksiyon hiçbir canlı veritabanında YOK (henüz hiç yazılmadı); rakamlar tohumlanmış kopyadandır.

- **Üç aday ölçülüp REDDEDİLDİ** — "manifest üyeliği ≠ index gereksinimi" şartının fiilî uygulanışı:
  - `task_types` için ikinci `{TenantId, IsActive, Code}` index'i (kardeş `TaskFieldDefinition`'da var, simetri
    onu isterdi): unique index tek başına `ListActive`'i aynı maliyetle ve SORT'suz karşılıyor. Planı
    değiştirmeyen index, karşılığı olmayan bir yazma maliyetidir.
  - `document_reference_list_versions` için `ImportedAt` index'i: alan bir **DateTimeOffset**, yani BSON
    **dizi** `[ticks, offsetMinutes]` — üstündeki her index MULTIKEY olur. Karışık offset'le denendi: deponun
    kullandığı **azalan** sıra doğru kalıyor, **artan** sıra yanlış (v3,v1,v5,v4,v2). Yanlışlık verinin
    biçiminde ve index'siz COLLSCAN'de birebir aynı — yani index'in getirdiği bir gerileme değil, ama
    sıralaması tesadüfi olan bir anahtarı kutsamak olurdu; üstelik içe aktarma başına tek satır tutan bir
    koleksiyon için ölçülebilir hiçbir kazanç yok. → **BL-030** (sessiz artan sıralama).
  - `ContentHash` üzerinde unique: geri çekilmiş (withdrawn) sürüm silinmediği için aynı hash yasal olarak
    tekrar edebilir; `IsDeleted:false` partial-unique meşru bir yeniden içe aktarmayı reddederdi.
- **`business_reference_data_validation_results` ölçüldü ama EKLENEMEDİ.** Index belli:
  `{TenantId, BusinessReferenceDataVersionId, RuleId}` (ESR-tam; 250→25 belge, SORT kalkıyor). Engel
  `SchemaProfileBudget.BusinessReferenceData` = **MaxLogicalIndexes 18** ve profil zaten tam 18'de. Tavan
  sahiplerinin verdiği sayı (GSKU, 2026-08-26) ve `SchemaProfileBudget`'in kendi başlığı onu değişikliğe
  uydurmayı açıkça yasaklıyor. → **BL-298** (2026-08-28'de kapandı: sahip tavanı 19 yaptı, index kondu,
  partial filter ölçülüp REDDEDİLDİ).
- **Mutasyon muhafızı yazıldı:** `PlatformSchemaContractMongoTests.TheQueriesBL279SizedRunOnAnIndexAndNotACollectionScan`
  her deponun gerçek filtre/sıralamasını `explain`'den geçirir ve planın beklenen index üzerinde IXSCAN
  olmasını şart koşar. ⚠ Madde 2 bu iş için YETMEZ: o, manifestin **beyan ettiklerini** dolaşır — beyanı
  silersen döngü ona hiç bakmaz ve test yeşil kalır; dokuz koleksiyonun ilk etapta içinden düştüğü delik tam
  olarak budur. On index'in **onu da** tek tek silinip testin kırmızıya döndüğü doğrulandı.
- `WorkflowWorkCenter` ve `DocumentManagement` profilleri madde 2'nin `[InlineData]` listesine eklendi; o güne
  kadar bu iki profilin beyan ettiği hiçbir index gerçek Mongo'ya karşı doğrulanmıyordu.
- `NotificationEventDefinitionRepository`'deki "unique index kurucuda best-effort kuruluyor" yorumu **yalandı**
  — öyle bir kod yoktu, iş anahtarını yalnız iki eşzamanlı çağrının ikisinin de geçtiği bir oku-sonra-yaz
  kontrolü koruyordu. Yorum düzeltildi, index manifeste kondu.
- **Gelecek regresyon riski: 🟢** — dokuzdan sekizi index'lendi ve her biri plan seviyesinde bir teste bağlandı;
  kalan tek koleksiyon bütçe kararı bekliyordu (BL-298) — 2026-08-28'de sahip tavanı 19'a çıkardı ve o index
  de kondu, yani dokuzun dokuzu index'li ve plan seviyesinde teste bağlı.

### BL-300 — sapma (deviation) kaydının kimlik anahtarı adlandırılmamış (2026-08-27)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

`DocumentCollectionDeviation` "tespit idempotenttir — read-back tekrarı açık bir sapmayı çoğaltmaz, günceller"
diyor, ama kimliğin hangi alanlara göre yargılandığını hiçbir yer söylemiyor ve uzlaştırma servisi bir anahtarla
okumuyor. BL-279 bu yüzden bu koleksiyona unique index KOYMADI: anahtarı tahmin etmek (yol+tip? yol+tip+önem?)
aynı klasör üzerindeki meşru ikinci sapmayı üretimde patlayan bir yazmaya çevirirdi.
- **Yapılacak:** anahtarı sahibiyle adlandır, sonra `{TenantId, …}` partial-unique index ile bağla.
- **Karşılaştırma:** kardeş `DocumentCollectionProvisioningEvidence`'ta anahtar belliydi (`CollectionInstanceId`
  başına tek kanıt, servis oku-sonra-yaz upsert ediyor) ve bu turda unique index'e bağlandı — orada index
  yalnız hızlandırmıyor, iki eşzamanlı read-back'in ikisinin de "yok" görüp ikisinin de eklemesi yarışını
  kapatıyor. Sapmalarda aynı şey yapılamadı çünkü anahtar yazılı değil.
- **Gelecek regresyon riski: 🟡** — anahtarsız kaldıkça yinelenen sapma satırları birikebilir ve "açık sapma
  sayısı" raporu sessizce şişer.

### BL-281 — Mongo dosya patlaması: bizim yarımız bitti, BRD tarafı DURUYOR (2026-08-26, ölçüm)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-283 — BRD harness'ı kendi önekiyle artık bırakıyor; süpürücü ona DOKUNAMAZ (2026-08-26, GSKU'da)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

### BL-288 — veritabanı adları üç ayrı gelenekte, ikisi ne olduğunu söylemiyor, ikisi paylaşılıyor (2026-08-27, ölçüldü, ertelendi)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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

### BL-291 — `.dt-card-icon` tek dosya için yaşayan bir sınıf, `.card-section-title .bx` ile aynı şeyi yapıyor (2026-08-27, ölçüldü, ertelendi)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

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

### BL-295 — `ShellAccessFilter` anahtar rotasyonunu tanımıyor (2026-08-27, ölçüldü, düzeltilmedi)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- `Program.cs:196` → `IssuerSigningKeys = jwtRotationResolver.GetValidationKeys()` — **geçerli + önceki**
  sırlar (`JwtSettings:Secret` + `JwtSettings:PreviousSecrets`).
- `ShellAccessFilter.cs:139` → `IssuerSigningKey = new SymmetricSecurityKey(...jwtSecret)` — **tek** anahtar.
- **Sonuç:** bir sır rotasyonundan sonra, önceki sırla imzalanmış geçerli bir belirteç köprüde doğrulanır ama
  filtrede doğrulanmaz. Bağımsız, sessiz bir çıkış sebebi — BL-293'ten ayrı ve onun düzeltmesiyle kapanmıyor.
- **Gelecek regresyon riski: 🟡** — yalnız rotasyon anında görünür, yani en kötü zamanda.

### BL-296 — `ClockSkew.Zero` iki serviste, 30 sn diğerlerinde (2026-08-27, ölçüldü)
> **DURUM:** DÜZELTİLDİ — dalda (`fix/clock-skew-consistency`, 2026-08-28) · merge sonrası KAPANDI'ya taşınacak
> · **SAHİP:** SAHİPSİZ

**DÜZELTME (2026-08-28).** Dokuz gelen-istek doğrulayıcısı tek bir sabite bağlandı:
`Diten.BuildingBlocks.Security.Secrets.JwtValidationDefaults.ClockSkew` = **30 sn**.

- **Neden 30 sn, neden Zero değil** — ölçüme göre: kütüphane varsayılanı 5 dk (yani 30 sn zaten on kat
  sıkı) · erişim belirteci ömrü 15 dk (kod varsayılanı) / 120 dk (AuthService appsettings) — 30 sn,
  kısasının %3,3'ü · dokuz doğrulayıcının **yedisi** zaten 30 sn'deydi · Zero'da hizalamak Gateway,
  Platform, HCM ve web kabuğunu gerçek saat kaymasına karşı **sıkılaştırırdı**, oysa hata gevşeklik değil
  **anlaşmazlık**.
- **Davranışı DEĞİŞEN üç yüzey (hepsi gevşedi, hiçbiri sıkılaşmadı):** MdmService · DevEnablementService ·
  `PlatformActorHangfireAuthorizationFilter`. Zero → 30 sn. Hepsi sistemin geri kalanıyla aynı hizada.
- `AuthService/TokenService.cs:170` kaydın sandığı gibi çelişki değildi: orada `ValidateLifetime = false`,
  yani kütüphane `ClockSkew`'e **hiç bakmıyor**. Ölü satır kaldırıldı, yerine sebebi yazıldı.
- **MUHAFIZ:** `tests/architecture/…/JwtClockSkewGuardTests.cs` — üretim kodunda kendi `ClockSkew`
  değerini yazan ya da `ValidateLifetime = true` deyip sabiti hiç anmayan dosyada test kırılır. İkisi de
  kırdırılıp kırmızıya döndüğü ölçüldü, sonra geri alındı.
- **Gelecek regresyon riski: 🟢** — değer tek satırda; ikinci bir yere yazmak muhafızı kırmızıya çevirir.

`ClockSkew = TimeSpan.Zero`: `MdmService/Program.cs:37` · `DevEnablementService/Program.cs:51`
(ayrıca `AuthService/TokenService.cs:170` ve `PlatformActorHangfireAuthorizationFilter.cs:84` — ikisi de
doğrulama yardımcıları, ayrı değerlendirilmeli).
`ClockSkew = 30 sn`: Web · Gateway · Platform · Auth · Hcm.
- **Sonuç:** saatler birkaç saniye kayarsa MDM ve DevEnablement, diğer her servisin kabul ettiği bir belirteci
  reddeder. Tutarsızlık kasıtlı mı, karar verilmedi.
- **Gelecek regresyon riski: 🟢** — tek bir değere hizalamak ucuz; hangi değer olduğu ürün/güvenlik kararı.

### BL-301 — yeni worktree'de frontend testleri koşulamıyor; 49 worktree'nin 43'ünde `node_modules` boş (2026-08-28, ölçüldü, düzeltilmedi)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- BL-297'nin ikinci yüzü. Orada yeni bir worktree'de **Platform açılmıyordu** (gizli anahtar yok);
  burada **frontend testleri koşulmuyor** (`node_modules` yok). İkisi de kurulum, ikisi de sessiz değil,
  ama ikisi de aramayan için görünmez.
- Ölçüm (2026-08-28): 49 worktree'nin **43'ünde** `frontend/Diten.Web/node_modules` **0 girdi**.
  Dolu olan 6: ana ağaç · cookie-nav · domain-norm · es · index · ppm-int — hepsi bu oturumda kuruldu
  ya da bir tur tarafından `npm ci` ile düzeltildi.
- ⚠ **SESSİZ GEÇİŞ YOK — ölçüldü.** `node_modules` boşken `npx vitest run`:
  · çıkış kodu **1**
  · `⎯ Startup Error ⎯` başlığı
  · `Tests …` özet satırı **hiç üretilmiyor**
  Yani bir tur "vitest N kırmızı" diye bir sayı raporladıysa o sayı **gerçekten koşulmuştur**;
  bu çıktıdan uydurulamaz. Risk "yanlış yeşil" değil.
- **Gerçek risk iki tane, ikisi de farklı:**
  1. Bir tur bu hatayı görüp vitest'i **hiç raporlamamış** olabilir — "koştum, taban" demek yerine
     sessizce atlamış olabilir. Geçmiş raporlar elde olmadığı için ölçülemedi.
  2. O 43 worktree'de frontend'e dokunan her tur **kontrolsüz** — frontend regresyonu koşulamıyor.
- ⚠ Bugünkü (2026-08-27/28) altı turun hepsi dolu olan ağaçlarda çalıştı; o raporlar bu boşluktan
  etkilenmiyor. CT kendi doğrulamalarını ana ağaçta koştu.
- Seçenekler (hiçbiri seçilmedi):
  · (a) `git worktree add` sarmalayan betik — `npm ci` + dev sırrını birlikte kurar (BL-297 ile aynı betik)
  · (b) `docs/guides/operations/dev-environment.md`'ye "yeni worktree kurunca `npm ci` koş" adımı — ucuz, disipline bağlı
  · (c) `node_modules`'ü paylaşılan bir konumdan bağlamak (symlink) — hızlı ama sürüm sapması riski
- **Gelecek regresyon riski: 🟡** — kod değil kurulum. Ama her paralel tur bir kez ödüyor ve
  frontend'e dokunan turda regresyon boşluğu bırakıyor.

### BL-302 — `AProfileBuildsItsOwnCollectionsAndNothingElse` sıraya bağımlı: aynı kod, farklı sonuç (2026-08-28, ölçüldü, AÇIKLANAMADI)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- BRD index turu (`262997c5`) `integration/2026-08-27`'ye birleştirildikten sonra `PlatformSchemaContractMongoTests`
  içinden 4-5 test kırmızıya döndü. **Her iki tur kendi dalında yeşildi** (BRD 2668, index 2667).
- Gözlem dizisi — aynı kod, aynı makine, art arda:
  | koşu | sonuç |
  |---|---|
  | birleşme sonrası tam süit | **4 kırmızı** |
  | test veritabanları temizlendi, tam süit | **5 kırmızı** |
  | yalnız `AProfileBuilds…` | **KIRMIZI** |
  | yalnız `AProfileBuilds…`, tekrar | **YEŞİL** |
  | tam süit ×2 | **YEŞİL (2668/2668)** |
- Hata iletisi iki koşuda FARKLI koleksiyon listesi verdi:
  · bir kez `document_management_*` + `document_reference_*` + `task_comments/types/transitions`
  · bir kez `checklist_*` + `task_assignments/dependencies/field_definitions/items/…`
  İkisi de WorkflowWorkCenter ve DocumentManagement profillerine ait — BRD'ye değil.
- **Ne DEĞİL, ölçüldü:**
  · profil etiketleri doğru — beş koleksiyon tek tek kontrol edildi, hepsi `SchemaProfile.WorkflowWorkCenter`
  · `PlatformSchemaManifest.For()` `c.Profile`'a göre filtreliyor — kod okundu
  · `SchemaCollection.ApplyAsync` yalnız kendi `Name`'ine index kuruyor — başka koleksiyon yaratmıyor
  · `InitializeAsync` her testte `DropDatabaseAsync` çağırıyor → temiz başlamalı
  · `MongoResidueSweeper.TouchAsync` yalnız işaret koleksiyonunu yazıyor
  · `[Collection("platform-schema-contract")]` adını başka sınıf kullanmıyor
  · sınıfın kendi veritabanı var: `diten_platform_itest_schema_contract`
- ⚠ **SEBEP BULUNAMADI.** "Şimdi geçiyor" bir teşhis değildir. Sıraya bağımlı bir test, bugün yeşil
  yarın kırmızı olur ve güveni aşındırır — bu oturumda tam olarak bu sınıftan bir hata (süreç-geneli
  `GuidSerializer` zamanlaması) iki testi "tek başına geçer, süitte kalır" hâline sokmuştu.
- **Şüpheliler (ölçülmedi):** `DropDatabaseAsync`'in Mongo tarafında eşzamansız tamamlanması ·
  çalışan Platform servisinin (5057) aynı mongod üzerindeki yükü · xUnit'in aynı koleksiyon içinde
  örnek yeniden kullanımı.
- **Yapılacak:** testi kendi izole veritabanına al (`MongoIntegrationHarness.CreateIsolatedAsync` deseni
  mevcut) ve düşürmenin tamamlandığını doğrula — ya da düşürme yerine koleksiyonları tek tek sil.
- **Gelecek regresyon riski: 🟡** — kırmızı gürültülü, sessiz yanlış değil. Ama açıklanamayan bir
  kırmızı, gerçek bir kırmızının yanında görünmez hâle gelir.

### BL-303 — sağlayıcı toplaması SIRALI: en kötü hâl N × zaman aşımı (2026-08-28, ölçüldü, bilinçli ertelendi)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- DCP-004 §2 D3 kapatılırken her sağlayıcı **kendi** zaman aşımına alındı
  (`WorkAggregation:Resilience:ProviderTimeout`, varsayılan 10 sn). Döngü **sıralı kaldı**.
- **Sonuç, aritmetik:** N sağlayıcının hepsi asılırsa okuma **N × 10 sn** sürer. Bugün N=2 → 20 sn.
  Bugün ikisi de süreç-içi Mongo okuması, ikisi de milisaniyelerde yanıtlıyor; yani bu tavan **bugün
  görünmüyor**. İlk ağ tabanlı sağlayıcı onu görünür kılan sağlayıcıdır — D3'ün kendisinin sebebi de buydu.
- **Neden paralelleştirilmedi (karar, unutma değil):** sağlayıcılar `Scoped` kayıtlı
  (`DependencyInjection.cs:205` ve `:209`). Eşzamanlı çağrı **aynı DI kapsamını ve aynı Mongo oturumunu**
  iki iş parçacığında paylaşır. Bu ayrı bir tehlike ve ayrı bir karar; bu tur **hata toleransını** değiştirdi,
  altındaki eşzamanlılık modelini değil. İkisini tek turda değiştirmek, kırıldığında hangisinin kırdığını
  söyleyemez hâle getirirdi.
- **Yeniden bakılacak eşik:** sağlayıcı sayısı 2'yi geçtiğinde **ya da** ilk ağ tabanlı sağlayıcı bağlandığında
  — hangisi önce olursa.
- Seçenekler (hiçbiri seçilmedi):
  · (a) her sağlayıcı için ayrı DI kapsamı açıp `Task.WhenAll` — doğru ama kapsam sahipliğini bu katmana taşır
  · (b) toplam (aggregate) bir bütçe daha eklemek — sıralılığı korur, tavanı sabitler, ama son sağlayıcıyı
    ilk sağlayıcının yavaşlığı yüzünden cezalandırır
  · (c) olduğu gibi bırakmak — N küçük kaldığı sürece dürüst
- **Gelecek regresyon riski: 🟢** — eklemeli. Bugünkü davranış (sıralı) zaten mevcut davranıştı; bu tur yalnız
  tavanı **ölçülebilir** hâle getirdi. Sessiz yanlış üretmiyor: aşan sağlayıcı `UnavailableSources`'ta
  `TIMEOUT` olarak görünür.

### BL-304 — manifest aksiyon sözlüğü ile projeksiyon aksiyon kodları arasında eşleme YOK (2026-08-28, ölçüldü, ertelendi)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- Modül manifestosu `CREATE · UPDATE · ASSIGN · CLAIM · COMPLETE · CANCEL · DELETE · BULK_DELETE` diyor;
  projeksiyon `claim · accept · start · plan · inquire · submitReview · return · reassign · complete ·
  cancel · release` yayınlıyor. **Aralarında hiçbir eşleme yok**, ve Görev Merkezi'nin kendi manifestosu
  `Actions: []` bildiriyor.
- WC-D2 bunu kapatmadı, kapatmak zorunda da değildi: dispatch, manifestoyu değil **sağlayıcının kendi
  gönderici kaydını** okur. Ama iki sözlük yan yana durduğu sürece, birini okuyan bir okuyucu diğerinin
  var olduğunu bilmez.
- **Yeniden bakılacak eşik:** kataloğa dayalı bir yetkilendirme (entitlement) aksiyon düzeyine indiğinde.
- **Gelecek regresyon riski: 🟢** — bugün hiçbir yol manifest aksiyonlarını okumuyor; eklemeli.

### BL-305 — aksiyonun teldeki hâli hâlâ uç/metot/izin TAŞIMIYOR; üç kopya elle senkron (2026-08-28, ölçüldü, bilinçli)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- `WorkItemActionDto` WC-D2'den sonra da yalnız kodu, etiketi ve etkinliği taşıyor. Uç, metot ve izin anahtarı
  **sunucuda** çözülüyor (`IWorkItemActionDispatcher`), telde değil.
- Yani bir aksiyonun üç tanımı hâlâ yan yana: sağlayıcının `BuildActions`'ı, göndericinin `Permissions`
  haritası, ve `RequiredActionPermissions`. Bu tur bunları **muhafız testiyle** bağladı (her gönderici
  anahtarı, eşleşen sağlayıcının bildirdiği kümede olmak zorunda) — ama tek bir bildirim hâline getirmedi.
- **Neden bu tur yapılmadı:** teli genişletmek, projeksiyonu tüketen yürütülebilir sözleşmeyi (fixture-contract.js)
  ve yedi dildeki fixture'ları da değiştirir. Ayrı bir karar, ayrı bir tur.
- **Gelecek regresyon riski: 🟡** — muhafız testi kaymayı yakalar, ama yalnız *izin* boyutunda. Bir sağlayıcı
  yeni bir aksiyon kodu yayınlayıp göndericiye eklemezse test kırmızı olur; kodu yayınlayıp **yanlış komuta**
  bağlarsa hiçbir test bunu söylemez.

### BL-306 — MOD-0023 dispatch'inde idempotency anahtarı sunucuda üretiliyor (2026-08-28, bilinçli, riski yazıldı)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- MOD-0023'ün onay/ret/bilgi-isteme uçları `IdempotencyKey` zorunlu tutuyor. Görev Merkezi bugün bir anahtar
  göndermiyor, bu yüzden `WorkflowApprovalWorkItemActionDispatcher` her çağrıda **yeni bir GUID** üretiyor.
- **Sonuç:** aynı düğmeye iki kez basmak (yavaş ağ, sabırsız kullanıcı) MOD-0023 için iki AYRI karar denemesidir.
  İkincisi bugün `WORKFLOW_TASK_INVALID_STATE` ile reddedilir — yani zarar görünmez, ama koruma **durum
  makinesinden** geliyor, idempotency'den değil.
- **Neden şimdi yapılmadı:** doğru anahtar istemcide üretilip aynı kullanıcı jestine bağlanmalı (aynı tıklama =
  aynı anahtar). Bu, dialog/onay akışının kendi turudur; sunucuda uydurmak sorunu gizlerdi.
- **Gelecek regresyon riski: 🟡** — MOD-0023 bir gün aynı durumdan iki geçişe izin verirse (örn. `requestInfo`
  tekrarlanabilir hâle gelirse) koruma sessizce kaybolur.

### BL-307 — /Tasks ve Görev Merkezi iki ayrı YAZMA yolu (2026-08-28, bilinçli, göç edilmedi)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- WC-D2 tek bir adres ekledi: `POST /api/v1/work-items/{id}/actions/{code}`. `/Tasks` ekranları kendi
  `/Tasks/api/{id}/{verb}` yolunu ve `TaskTransitionRoutes.cs` kısıtını **aynen** korudu.
- Bu bir eksiklik değil, tur kuralıydı: çalışan bir yolu yenisi için bozmak takas değil kayıptır. Ama sonuç,
  aynı geçişin iki kapısı olmasıdır ve ikisi de aynı komuta iner.
- **Ölçülmüş bedel:** `TaskTransitionRoutes` regex'i hâlâ elle tutuluyor. Bir aksiyon kodu eklenip oraya
  yazılmazsa `/Tasks` ekranlarında düğme çizilir, basılır, vekil 404 verir (dosyanın kendi yorumu bunu anlatıyor).
  Görev Merkezi bu tuzağa artık düşmüyor; `/Tasks` düşüyor.
- **Yeniden bakılacak eşik:** `/Tasks` ekranları Görev Merkezi bileşenlerine geçtiğinde ya da üçüncü bir
  yazma yüzeyi çıktığında.
- **Gelecek regresyon riski: 🟡** — sessiz değil (404 görünür), ama tek yönlü: yalnız `/Tasks` tarafında.

### BL-308 — Tasks l10n köprüsü ELLE tutuluyor (157 satır); otomatik sayıma çevrilmedi (2026-08-28, ölçüldü, bilinçli ertelendi)
> **DURUM:** ERTELENDİ · **SAHİP:** SAHİPSİZ

- İki köprü iki farklı mekanizma kullanıyor:
  · `Views/WorkCenterNext/_L10n.cshtml` → `Localizer.GetAllStrings(true)` — tüm resx otomatik sayılıyor,
    resx'e eklenen anahtar köprüye **kendiliğinden** gelir, kayma **imkânsız**.
  · `Views/Tasks/_IndexL10n.cshtml` → **elle tutulan 157 satır**. resx'te olup burada olmayan anahtar
    **sessizce düşer** ve okuyucuya ham anahtar ya da genel hata mesajı olarak varır.
- `Tasks/api.js`'in kendi yorumu bunun üç kez olduğunu yazıyor: *"a code mapped in api.js without a line here
  reaches the reader as the generic error."* Yani bu teorik bir risk değil, üç kez ölçülmüş bir kusur sınıfı.
- **Neden bu turda çevrilmedi (karar, unutma değil):** `GetAllStrings(true)` davranış değiştirir —
  (a) yük büyür (TasksIndex resx'i 157 anahtardan çok daha geniş), (b) bugün `SharedLocalizer` ve `Localizer`
  aynı isimde iki anahtar taşıyorsa hangisinin kazandığı elle yazılmış sırayla belirleniyor; otomatik sayımda
  bu sıra değişir. Bu tur bir **muhafız** turuydu, davranış turu değil.
- **Bugünkü kısmi koruma:** `workcenter-next-l10n-key-guard.test.js`, `quick-create.js`'in TASKS köprüsünden
  okuduğu 7 anahtarı **hem** partial'da **hem** resx'te arıyor. Yani WorkCenterNext klasöründen Tasks köprüsüne
  bağlanan dosyalar korunuyor; `Tasks/` klasörünün kendi JS'i (form-page, details-page, api.js, form.js)
  **korunmuyor**.
- **Yeniden bakılacak eşik:** Tasks yüzeyi Görev Merkezi bileşenlerine geçtiğinde ya da elle liste 200 satırı
  aştığında.
- **Gelecek regresyon riski: 🟡** — sessiz ve okuyucuya görünür. Yeni bir Tasks anahtarı ekleyen biri partial'a
  satır yazmayı unutursa hiçbir test kırmızıya dönmez; kusur ancak ekranda görülür.

### BL-310 — Görev Merkezi köprüsünün referans tüketicisi GEÇİCİ; gerçek bir modül ucunu açınca SİLİNECEK (2026-08-28, bilinçli)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- `Diten.DevEnablementService/…/Controllers/ReferenceWorkItemProviderController.cs` + Platform'un
  `appsettings.Development.json` içindeki `dev-reference` satırı.
- **Neden var:** WC-D1 köprüsü yazıldığı gün hiçbir modül `GET api/v1/work-items/projection` ucunu açmamıştı.
  Gerçek modülü beklemek turu bloke ederdi; kanıtsız kapatmak ise DCP-004'ün kendi yazdığı hatayı — "tek
  uygulama üzerinde kanıtlanan bir dikiş hiçbir şey kanıtlamaz" — tekrarlardı. İkisi de yapılmadı.
- **Ne kanıtlıyor:** ayrı bir serviste, gerçek soket üzerinden, çağıranın kendi JWT'si ve kiracı başlığıyla;
  okuma → düğme → uzak durum değişimi → yeniden okumada yeni durum. Simüle edilen hiçbir halka yok.
- **Ne değil:** iş anlamı yok, veritabanı yok — durum statik bir sözlükte, süreçle birlikte ölüyor.
  `WorkItemReferenceProvider:Enabled` olmadan kapalı ve yalnızca dev'de açık.
- **Silme eşiği:** ilk gerçek modül (PVG ya da Global SKU) kendi projeksiyon ucunu açtığı gün. O gün hem
  controller hem yapılandırma satırı silinir; köprü kodu değişmez.
- **Gelecek regresyon riski: 🟢** — üretimde kapalı; riski unutulup "gerçek bir kaynakmış gibi" okunması,
  bu yüzden `Temporary: true` bayrağı, dosya başlığı ve bu kayıt üçü birden var.

### BL-312 — Modül adresinin OTOMATİK gelmesi (D1'in manifest yarısı) hâlâ açık (2026-08-28, bilinçli)
> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

- WC-D1 adresi **operatörün** yazdığı yapılandırmaya bağladı ve bu bir sahip kararıydı. Kapanan yarı bu.
- **Kapanmayan yarı:** kendini kaydeden bir modülün adresini Platform'a otomatik bildirmesi. Manifest
  istemci tarafından üretilir; içindeki bir adres, çağrılan tarafın "beni şuradan ara" demesidir — çağıranın
  JWT'sini nereye göndereceğini çağrılanın yazması. Depoda örneği yok ve eklemek bir güvenlik kararı.
- **Karar gerektiren soru:** çağrılan tarafça bildirilen bir host nasıl doğrulanır (imzalı manifest? operatör
  onayı kuyruğu? sabit host allow-list?). Bu soru cevaplanmadan otomatikleştirme yapılmamalı.
- **Bugünkü bedel:** modül başına bir satır, elle. Yedi mevcut servis-arası adres zaten böyle duruyor.
- **Gelecek regresyon riski: 🟢** — bugün hiçbir şey kırılmıyor; yalnızca bir kolaylık eksik.

### BL-313 — 🔴 ACİL · üretimde olay taşıyıcısı yok; mesajlar SESSİZCE SİLİNİYOR ve "gönderildi" işaretleniyor (2026-08-28, ölçüldü)
> **DURUM:** AÇIK · **SAHİP:** Beste Pullukçu


> **SAHİP ATAMASI: Beste Pullukçu — ACİL.** Sahip kararı 2026-08-28.
> Görev Merkezi ekibini **engellemiyor** (ölçüldü: WorkAggregation ve köprü
> `IEventTransportPublisher`/`OutboxMessage`'ı hiç kullanmıyor, tamamı düz HTTP).

- **Ölçüm — üretim ayarında `Eventing` bloğu YOK.** `appsettings.Development.json`
  içinde var, `appsettings.json` içinde yok. Kod `Eventing:Transport` okuyor
  (`Program.cs:98`) ve şu dala giriyor:

  ```csharp
  // DependencyInjection.cs:428 vs :433
  if (Transport == "RabbitMQ")  → MassTransitRabbitMqEventPublisher
  else                          → InMemoryEventBus
  ```

- **`InMemoryEventBus` ne yapıyor:** mesajı süreç-içi bir `ConcurrentQueue`'ya
  ekliyor ve **başarılı dönüyor**. O kuyruğu kimse boşaltmıyor; süreç kapanınca
  kuyruk yok oluyor.

- **⚠ ASIL SORUN — mesaj beklemiyor, KAYBOLUYOR:**

  ```
  OutboxPublisherProcessor.cs:45   await _publisher.PublishAsync(...)   ← başarılı döner
                            :46   outboxEvent.MarkPublished();          ← "teslim edildi"
                            :47   await _outboxRepository.UpdateAsync() ← kalıcı yazılır
  ```

  Outbox kaydı kalıcı olarak kapatılıyor. **RabbitMQ'yu sonradan açmak geçmiş
  mesajları geri getirmez.**

- **⚠ Sağlık kontrolü de sessiz:** RabbitMQ sağlık kontrolü yalnız
  `Transport == "RabbitMQ"` iken ekleniyor (`Program.cs:97-102`). Yani bugünkü
  hâlde sistem **"sağlıklı"** raporluyor, hiçbir mesaj teslim edilmezken.

- **Somut etki:** `Diten.AuthService` Platform'dan gelen yetki/abonelik
  senkronizasyonunu dinliyor (`EntitlementSyncConsumer`). Bellek-içi taşıyıcıda
  o mesaj AuthService'e **hiç ulaşmaz** → abonelikten doğan yetkiler kullanıcıya
  yansımaz, hiçbir yerde hata görünmez.
  ⚠ AuthService'te bir HTTP yolu da var (`internal/events`: `tenant-activated`,
  `tenant-admin-invited`). Üretimde hangi akışın hangi yolu kullandığı
  **ölçülmedi** — yapan kişi önce bunu ölçmeli.

- **Taşıyıcı seçeneği tek:** pakette yalnız `MassTransit.RabbitMQ` var. Azure
  Service Bus / AWS / Kafka paketi yok, kodda üçüncü dal yok. Başka bir broker
  seçmek = yeni taşıyıcı adaptörü yazmak, ayrı bir iş.

- **Yapılacak (Beste Pullukçu):**
  1. Üretimde RabbitMQ sunucusu kurulsun; kimlik bilgileri sır yönetiminden gelsin
  2. `Eventing:Transport = "RabbitMQ"` üretim ayarına yazılsın
     (bu yazıldığı an sağlık kontrolü kendiliğinden devreye girer)
  3. **Muhafız:** üretim profilinde `Transport` boşsa uygulama AÇILMASIN.
     Sessizce bellek-içine düşmek bu kaydın sebebidir; ayar unutulunca
     yine sessiz kalmamalı
  4. Bugüne kadar "gönderildi" işaretlenmiş outbox kayıtlarının kaybı
     **ölçülsün ve raporlansın** — telafi gerekiyorsa ayrı iş olarak açılsın

### BL-316 — MOD-0162'nin beyan edilmiş SoR'u kavram modelini KAPSAMIYOR; ConceptGraph sınırın dışına yazıldı (2026-09-01, ölçüldü)
> **DURUM:** RESOLVED (2026-09-07, DEC-SCMM-01 seçenek H) · **SAHİP:** CAND-CAP-0011 (yeni Marketing owner) — concept foundation MOD-0162'de kalır; SCMM/UCLN üst katman CAND-CAP-0011'e; MOD-0167'den UCLN kaldırıldı. Bkz. docs/decisions/DEC-SCMM-01-bl316-ownership-decision-brief.md

- **Ölçüm — Blueprint master 8.1, `Blueprint_Data` sayfası, MOD-0162 satırı:**

  ```
  Capability Group : Service
  Module Name      : Knowledge Base
  SoR Module       : SoR: knowledge articles (if designated), article feedback
  Aim (group)      : Execute service delivery with SLA governance and
                     closed-loop satisfaction telemetry.
  ```

- **Ne yazıldı:** MOD-0162-FU03 kapsamında beş aggregate
  `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/` altına kondu:
  `ConceptType`, `ConceptNode`, `ConceptRelationship`, `ConceptChainTemplate`,
  `KnowledgeContentConceptLink`. Bunların hiçbiri "knowledge article" ya da
  "article feedback" değil — bir kavram grafiği ve zincir şablonudur.

- **⚠ Sapma iki katmanlı:**
  1. **SoR sapması** — yazılan nesneler modülün beyan edilmiş SoR'unda yok.
  2. **Capability grubu sapması** — MOD-0162 `Service` grubunda ve grubun hedefi
     servis teslimi/SLA. Kavram modeli ise **promosyonel mesaj mimarisi**
     (legacy `Marketing / UCLN` ekranlarının karşılığı), vaka çözümü değil.
     Yani iş Marketing hedefine hizmet ediyor, Service modülünde duruyor.

- **⚠ İkinci ölçüm — registry kendi içinde çelişiyor.** `execution/registries/module-id-registry.md:259`
  ve `execution/domains/commercial-suite/crm-sor-boundary.md:21`
  "MOD-0167 owns Segment / TargetCustomer / **UCLN**" diyor. Oysa MOD-0167'nin blueprint SoR'u
  yalnız `segments, segment versions, segment usage logs`. Sonuç: **UCLN üç yerde üç farklı sahibe
  yazılı** — blueprint'te hiçbir modülde, registry'de MOD-0167'de, kodda MOD-0162'de.

- **Neden ŞİMDİ değiştirilmiyor:** kod teslim edilmiş ve çalışıyor (FU03 smoke 32/32, verifier 85/9).
  Taşıma maliyeti = collection adları + `RegisterClassMaps` kayıtları + `crm.knowledge-concept.*`
  RBAC anahtarları + gateway yolları + frontend rota kökü (`CRM/KnowledgeConcepts`).
  Sıfır işlevsel kazanç, yüksek regresyon riski. Bu bir **yönetişim borcu**, hata değil.

- **Tetikleyici:** legacy UCLN akışının üst katmanı için — Strategy Template / UCLN Book / Book Pages —
  module pack açıldığında. O pack zaten yeni bir capability sahibi tanımlamak zorunda
  (registry'de bir sonraki boş kimlik `CAND-CAP-0011`), dolayısıyla sınır kararı orada
  **tek seferde** verilmeli. Ayrı bir tur harcamaya değmez.

- **Karar gerektiren soru — üç seçenek, henüz seçilmedi:**
  1. **MOD-0162'nin SoR'unu genişlet** (blueprint 8.2'de satır güncellenir: `+ concept model`).
     En az riskli; ama Marketing işi Service grubunda kalmaya devam eder.
  2. **MOD-0057 / MOD-0058 ile bölüş.** MOD-0057 (Semantic Tagging & Taxonomy Management) SoR'u
     `taxonomies, tags, tagging rules, tag approvals`; MOD-0058 (Knowledge Graph / Entity Linking)
     SoR'u `KG entities/links, link audit trails`. Kavramsal olarak en doğru eşleşme —
     **ama iki platform modülü de henüz yok**, yani bu seçenek bir bağımlılık yaratır.
  3. **Yeni Marketing capability'sine taşı.** En temiz sonuç, en pahalı yol.

- **Seçenekten BAĞIMSIZ olarak yapılacak:** registry'deki "MOD-0167 owns UCLN" kaydı **yanlış** ve
  silinmeli. Tek satırlık düzeltme, bugün de yapılabilir; üç seçeneğin hiçbirini önceden bağlamaz.

- **Gelecek regresyon riski: 🟢** — bugün hiçbir şey kırılmıyor, hiçbir test düşmüyor.
  Bedeli, sınır sorusu bir sonraki module pack'te yeniden açıldığında ödenir.

- **Kaynak:** legacy UCLN akış/karşılaştırma raporu (bölüm E — Blueprint 8.1 yerleşimi),
  ölçüm tarihi 2026-09-01.
### BL-317 — Başlık kimlik/vekâlet çipi tasarımı bir stash'te bekliyor, bir aydır (2026-08-29, ölçüldü)

> **DURUM:** AÇIK · **SAHİP:** Ali Tufanoğlu

- **Ölçüm:** `stash@{0}` (`7cb7a895`), 2026-07-20'de `feature/workcenter` dalında alınmış,
  mesajı *"wip-idpill-idmenu-experiment (Codex, yarim)"*. İki dosya, 49 ekleme / 22 silme:
  `backbone-custom.css` + `WorkCenterNext/app.js`.
- **İçindeki iş İNMEMİŞ.** main'de ölçüldü:
  `.wcn-header-actions` ✅ var — ama `.wcn-idpill` · `.wcn-idmenu` · `.wcn-idpill-active` ·
  `.wcn-idpill-caret` · `.wcn-idpill-warn` · `.wcn-idpill:hover` → **hiçbiri yok.**
- **Bugün yerini ne dolduruyor:** başlıktaki "Myself ▾" seçici Bootstrap'ın genel
  `btn btn-label-secondary dropdown-toggle` sınıflarıyla çiziliyor. Yani özel çip
  tasarımı hiç uygulanmamış, genel bir dropdown'a düşülmüş.
- ⚠ **Bu kayıt bir iş talebi değil, bir HATIRLATMADIR.** Stash bir aydır bekliyor ve
  hiçbir yerde yazılı değildi; kaybolmasının tek sebebi kimsenin bilmemesi olurdu.
- **Karar sahibinde:** (a) `git stash branch feature/wcn-idpill stash@{0}` ile kendi dalına
  çıkarıp bitir · (b) tasarım artık istenmiyorsa stash'i düşür ve bu kaydı kapat.
- ⚠ **Nasıl olursa olsun `pop` KULLANMA, `apply`/`branch` kullan** — 2026-08-29'da bu depoda
  çözülmemiş bir `pop` iki dosyayı çakışma işaretleriyle bıraktı ve dal değiştirmeyi engelledi.
### BL-318 — Rol İzinleri ekranı modül adlarını ham slug olarak basıyordu (2026-08-29, ölçüldü)

> **DURUM:** KAPANDI · **SAHİP:** CONTROL TOWER

- **Sorun:** `Governance/RoleAssignments/index.js` grup başlıklarında ve modül filtresinde
  izin kataloğundan gelen HAM slug'ı gösteriyordu — `work-aggregation`,
  `product-item-sku-master`, `test-beta-mod`. Kullanıcı hangisinin ne olduğunu anlamıyordu.
- **Çözüm — yeni dize üretilmedi, çalışan kaynak tüketildi:** adlar `/TenantNavigation/api/menu`
  üzerinden çözülüyor; o uç kiracı override'ını ve 7 dilli yerelleştirmeyi ZATEN uyguluyor
  (kenar çubuğu da onu kullanıyor). Menünün tanımadığı kod, kodun kendisinden türetilen
  okunur bir ada düşüyor (`test-beta-mod` → "Test Beta Mod").
- **Kimlik korundu:** yalnız GÖRÜNEN metin değişti. Gruplama anahtarı, filtre değeri ve izin
  anahtarının kendisi kod olarak kaldı — izlenebilirlik bozulmadı.
- **Muhafız:** `tests/role-assignments-module-label.test.js`, 9 test. Karar mantığı
  (`module-label.js`) DOM'suz ve saf tutuldu ki doğrudan test edilebilsin; ham slug sızdıran
  bir regresyon derlemeyi düşürür.
- ⚠ **CONTROL TOWER hatası, kayda geçiyor:** bu turun altı dosyası `git add -A` ile bir
  backlog commit'ine süpürüldü (`c527ff35`). Kayıp olmadı, ama commit yanlış mesaj altında
  duruyordu. Ayrıldı; iki commit'in dosya kümesi eskisiyle birebir doğrulandı.
  Aynı hata bu oturumda ikinci kez oldu.

### BL-324 — kiracı çelişkisi (BL-323 durum 1) DÖRT kiracı-çözüm noktasında hâlâ dayatılmıyor (2026-08-29, ölçüldü)

> **DURUM:** KISMEN KAPANDI (2026-08-30) — beş uyumsuz noktanın ÜÇÜ kapandı (AuthService,
> CrmService, HcmService); İKİSİ açık kalıyor (`Platform.Common`, gateway) ve aşağıda tam olarak
> hangi karara bağlı oldukları yazılı. · **SAHİP:** CONTROL TOWER
> *Geldiği kayıt:* BL-323 (kapandı, arşivde) — kural onaylandı, kapsamı köprüydü; bu, kural
> yazılırken ÖLÇÜLEN artıktır.

BL-323 sahibin kararıyla şunu kurala bağladı: **başlık kiracısı ile JWT kiracısı ÇELİŞİYORSA
400.** O turda köprünün kendi servisi (`Diten.DevEnablementService`) düzeltildi ve muhafızlandı.
Aynı turda yedi kiracı-çözüm noktasının hepsi okundu; sonuç:

| Nokta | Çelişkide bugünkü davranış |
|---|---|
| `Diten.MdmService/.../TenantResolutionMiddleware.cs` | **400** ✅ kurala uygun |
| `Diten.DevEnablementService/.../TenantResolutionMiddleware.cs` | **400** ✅ BL-323'te düzeltildi |
| `Diten.AuthService/.../TenantResolutionMiddleware.cs` | **400** ✅ 2026-08-30'da kapandı (önce: "JWT kazanır" + uyarı logu) |
| `Diten.Platform.Common/src/.../Tenancy/TenantResolutionMiddleware.cs` | ⚠ "JWT kazanır" + uyarı logu — **AÇIK, karar bekliyor** |
| `gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs` | ⚠ "JWT kazanır" + uyarı logu (alt alan adı için de aynısı) — **AÇIK, karar bekliyor** |
| `Diten.HcmService/.../TenantResolutionMiddleware.cs` | **400** ✅ 2026-08-30'da kapandı (önce: JWT'yi HİÇ okumuyordu) |
| `Diten.CrmService/.../TenantResolutionMiddleware.cs` | **400** ✅ 2026-08-30'da kapandı (önce: JWT'yi HİÇ okumuyordu) |

#### ✅ KAPANAN KISIM — 2026-08-30 (`fix/tenant-contradiction-remaining-sites`)

- **AuthService:** çelişkide artık 400. Önceki `ResolveTenant` ("JWT kazanır" + uyarı logu)
  SİLİNDİ — bir uyarı logu reddetme değildir; istek yine çalışıyordu ve aşağıdaki handler'ın iki
  çelişen değerden hangisini okuduğu sessizdi. Login etkilenmez: çelişki İKİ değeri gerektirir,
  login'de henüz token yoktur.
- **CrmService / HcmService:** çelişkide artık 400. Bu ikisinde kapatılan açık şuydu — servis
  kimlik doğruluyor (`UseAuthentication`, 38/38 ve 3/3 controller'da `[Authorize]`) ve middleware
  `UseAuthentication`'dan SONRA çalışıyor, yani token'ın kiracısı elin altındaydı ve hiç
  okunmuyordu. `[Authorize]` kimin olduğunu kanıtlar, hangi kiracı adına hareket edebileceğini
  değil.
  ⚠ **Kapsam çizgisi, bilerek:** JWT burada çelişkiyi SAPTAMAK için okunuyor, kiracıyı ÇÖZMEK
  için değil. Çözüm eskisi gibi başlık-güdümlü kaldı; başlık yokken `Clear()` davranışı da
  aynen korundu. "JWT ikinci bir kiracı kaynağı olmalı mı" sorusu ayrı bir güven kararıdır ve
  AÇIK kalır (aşağıda).
- **Muhafızlar (davranış, gerçek middleware üzerinde):** üç serviste
  `Tenancy/TenantContradictionGuardTests.cs`. Her biri isteğin GERÇEKTEN reddedildiğini
  (handler çalışmadı) doğruluyor — "403 değil ve 404 değil" tek başına yetmez, hiçbir şey
  yapmayan middleware 200 döner ve o iddiayı yanlış sebeple geçer; dünkü BL-323 muhafızında
  bulunan zafiyet tam olarak buydu. Kontroller (çelişmeyen istekler geçmeli) her şeyi reddeden
  bir middleware'in de kırmızı olmasını sağlıyor.
- **Repo genelinde statik muhafız ARTIK YAZILDI:**
  `tests/architecture/.../TenantContradictionSiteGuardTests.cs`. Önceki turun itirazı (istisna
  listesi sapmayı "kabul edilmiş" yapar) listeyi YAZMAYARAK değil KÜÇÜLTEREK karşılandı: liste
  artık bir kolaylık listesi değil, aşağıdaki TEK adlandırılmış karardan ibaret. Yedi nokta
  sayısı tam olarak sabitlendi (ne `> 0` ne "boş değil") — sekizinci bir nokta ne kuralı ne
  kararı devralmadan ortaya çıkarsa kırmızı olur. Reddetmeyi yapısal olarak ölçüyor: koşulu
  yazıp 400 döndürmeyen (yalnız loglayan) bir dosya "uyguluyor" sayılmıyor — ölçüldü.

#### ⚠ AÇIK KALAN KISIM — iki nokta, tek karar

`Platform.Common` ve gateway **tahminle değil, ölçümle** açık bırakıldı:

- **Korkulan kırılma yok:** `Platform.Common`'ın middleware'ini gerçekten kullanan TEK uygulama
  `Diten.Platform.API` (diğer altı servisin her birinin kendi yerel kopyası var). Platform
  admin'in "bir kiracı adına hareket etmesi" akışı `X-Tenant-Id` KULLANMIYOR — rota parametresi
  üzerinden gidiyor (`/api/admin/tenants/{id}/...`), middleware'e hiç uğramıyor. `admin` yolları
  başlığın varlığında zaten 400 veriyor; `TenantOnTheWire.cs` de CT'nin 2026-08-28 kararını
  kayda geçiriyor: acting-for-a-tenant İNŞA EDİLMEYECEK.
- **Ama gerçek bir karar var — SIRALAMA:** olağan kiracı yolunda kiracı, `actor_type` 403
  sınırından ÖNCE çözülüyor. Çelişki reddi öne konursa, platform aktörünün çelişen başlıkla
  geldiği istek bugünkü **403**'ün ("Tenant endpoints require tenant_user tokens") yerine
  **400** alır. Hangi reddin kazanacağı bir ERİŞİM SINIRI kararıdır, temizlik değil.
- **Gateway'de ek olarak:** kuralın hiç bahsetmediği ÜÇÜNCÜ bir kiracı kaynağı var — istek alt
  alan adı. Üç yönlü uyuşmazlıkta "çelişki" henüz tanımlı değil.
- **CrmService/HcmService'te açık kalan:** JWT'nin kiracı KAYNAĞI olup olmayacağı ve başlık
  yokken `Clear()` yerine reddedilip reddedilmeyeceği. Bu turda bilerek dokunulmadı.

- **Gelecek regresyon riski: 🟡 → 🟢'ye yakın** — kural artık yedi noktanın beşinde dayatılıyor
  ve kalan ikisi statik muhafızla adlandırılmış durumda, yani "yazılı ama tutmuyor" sessizliği
  bitti. Kalan risk yalnızca yukarıdaki sıralama kararının verilmemiş olmasıdır.

### BL-325 — Oluştur'a iki kez basmak iki görev yaratıyor (2026-08-31, CANLI HATA, sahip gördü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**Belirti (sahip canlıda gördü):** Görev oluşturma sırasında "Oluştur"a iki kez
basılınca modal kapanmadan istek iki kez gidiyor ve **iki ayrı görev** oluşuyor.

**Ölçüm (2026-08-31):**
- `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/quick-create.js` — çift
  gönderim koruması **sıfır**: `disabled = true`, `isSubmitting`, `submitting`
  desenlerinin hiçbiri yok. Düğme basılabilir kalıyor, modal açık kalıyor.
- `frontend/Diten.Web/wwwroot/assets/js/Tasks/form-page.js` — koruma sinyali **0**.
- `frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js` — 2 sinyal var; iki oluşturma
  yolu **aynı korumaya sahip değil**.
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs` —
  oluşturma ucunda `IdempotencyKey` **yok**. Yani sunucu da aynı isteği iki kez
  kabul ediyor; koruma tamamen istemcinin refleksine bağlı.

**Neden önemli:** kullanıcının yavaş ağda iki kez basması olağan. Sonuç, sessizce
çoğalan görev — kimse hata görmüyor, iş listesinde iki kopya beliriyor.

⚠ **Yalnız düğmeyi kilitlemek YETMEZ.** İstemci kilidi sekme yenilemesini, ağ
tekrarını veya iki sekmeden aynı formu göndermeyi engellemez. Kalıcı çözüm
sunucuda idempotency anahtarıdır; düğme kilidi onun yerine geçmez, yanına gelir.

**Kardeş kayıt:** [[BL-306]] — MOD-0023 dispatch'inde idempotency anahtarı sunucuda
üretiliyor, aynı sınıf kusur, farklı yüzey. İkisi birlikte ele alınmalı: anahtarı
İSTEMCİ üretmeli ki tekrar gönderim aynı anahtarı taşısın.

**Kapanış ölçütü:** (a) iki oluşturma yolunda da düğme kilidi + modal kapanışı,
(b) sunucuda idempotency anahtarı, (c) aynı anahtarla iki kez gönderilen isteğin
TEK görev ürettiğini ölçen test — ve o testin, korumayı geri alınca kırmızıya
döndüğü kanıtlanmış olmalı.

### BL-326 — Alt görev kartı sessizce yok oluyor, sebebi söylenmiyor (2026-09-01, sahip gördü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**Belirti:** Sahip bir görev açtı, detayına gitti, alt görev kartını göremedi ve
hata sandı. Ekran hiçbir açıklama vermiyor.

**Ölçüm (2026-09-01) — davranış DOĞRU, açıklama YOK:**
- `TaskItem.cs:244` kuralı yazıyor: *"One level only. A task carrying a parent
  may not itself be a parent; the server enforces it."*
- `TaskWorkItemProvider.cs:545` —
  `var subtasks = task.ParentTaskItemId is null ? ToSubtasks(...) : null;`
- `:875-878` — `subtasks` null ise `"subtasks"` yeteneği bildirilmiyor
- `app.js:3400` — `if (!hasCap(item, 'subtasks') || !item.subtasks) { return ''; }`

Yani üstü olan bir görevde kart **doğru** biçimde çizilmiyor. Ama kullanıcı
bunu bilmiyor ve eksiklik sanıyor.

⚠ **Bu, bu üründe tekrar eden kusur sınıfı:** ekran bir şeyin NEDEN olmadığını
söylemiyor. Aynı sınıf: [[BL-072]] (kişi seçicide "neden kısa" ipucu sunucuda
hesaplanıp tarayıcıda ölüyor), ve kurulmamış kiracıda Görev Merkezi'nin
"Her şey tamam ✓" demesi.

**Kapanış ölçütü:** üstü olan bir görevde, alt görev kartının yerinde tek cümle:
*"Bu görev bir alt görev. Alt görevler tek seviyedir; bunun altına başka görev
eklenemez."* — 7 dilde, ve kartın çizilmediği durumu ölçen bir test.

⚠ **l10n kapısı AÇIK** (yeni metin, 7 dil). Tek başına bir tur değil; bir sonraki
l10n paketine katılmalı.

### BL-327 — Birim ve pozisyon TASLAK doğuyor, hiçbiri aktif değil, ekran sebebini söylemiyor (2026-09-01, CANLI, sahip gördü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**MODÜL:** ORGANIZATION (Organizasyon) + TASKS (Görevler)
**SAYFA:** Organizasyon Birimleri `/OrganizationUnits` · Pozisyonlar `/Positions`
          · Görev Oluştur `/Tasks/Create`

**Belirti (sahip canlıda, yönetim sunumu sırasında):** Görev oluştururken
İngilizce bir uyarı:
*"No organization unit could be determined for this task. Ask an administrator
to assign you a position or define a root organization unit."*

**Ölçüm (2026-09-01, dev veritabanı):**

    organization_units:  15 kayıt
        Status = 0 (taslak) → 13
        Status = null       →  2
        Status = 1 (AKTİF)  →  0     ⚠ HİÇBİRİ AKTİF DEĞİL

    positions:           14 kayıt · aktif 6 · taslak 3 · null 5

Kod `CreateTaskItemHandler.cs:170-176` kademeli düşüyor:
    1. formda seçilen birim
    2. atananın AKTİF pozisyonunun birimi
    3. kiracının AKTİF kök birimi
    4. yoksa hata

`ResolveTenantRootUnitAsync` (:558-567) üç şart arıyor: üstü yok + arşivsiz +
`Status == Active`. Kök birim VAR (8 tane, üstü yok, arşivsiz) ama **hiçbiri
Active değil** → 3. basamak boş dönüyor → hata.

**Üç ayrı kusur, tek belirti:**

1. ⚠ **Varsayılan taslak.** Birim ve pozisyon `Draft` doğuyor. Kullanıcı
   oluşturur, kaydeder, ve farkında olmadan KULLANILAMAZ bir kayıt yaratır.
   ⚠ "Taslak birim" kavramının bir karşılığı yok — bir birim ya vardır ya
   yoktur. Pozisyon için taslak anlamlı olabilir, birim için değil.

2. ⚠ **Liste yalan söylüyor.** `Positions/index.js:51-53` rozeti yalnız
   `IsArchived`'dan türetiyor, `Status`'ü HİÇ okumuyor. Taslak pozisyon
   listede yeşil "Aktif" görünüyor, kişi seçicide yok. Sessiz değil,
   AKTİF OLARAK YANILTICI.

3. ⚠ **Mesaj 7 dilin 1'inde.** `ErrorOrganizationUnitUnresolved` yalnız
   `Views/Tasks/TasksIndex.en.resx`'te. tr/fr/es/zh/ar/ru: yok. Türkçe
   arayüzde İngilizce uyarı çıkıyor.
   Ve mesaj DOĞRU şeyi söylemiyor: "bir kök birim tanımlayın" diyor, ama
   15 kök birim VAR — eksik olan AKTİF olmaları. Kullanıcı olmayan bir şeyi
   yaratmaya çalışıyor.

**Önerilen çözüm (sahip tercihi bekliyor):**
- ⭐ (a) Kök birim varsayılan olarak AKTİF doğsun — "taslak birim"in karşılığı yok
- (b) Rozet `Status`'ten türetilsin (XS, l10n gerekmez —
      `StatusDraft`/`StatusActive`/`StatusFrozen`/`StatusClosed` `details.js:40-41`'de
      zaten kullanımda)
- (c) Mesaj 7 dile çevrilsin VE gerçek durumu söylesin:
      "15 biriminiz var ama hiçbiri aktif değil" — "kök birim tanımlayın" değil

**Geçici çözüm (bugün uygulandı):** `/OrganizationUnits` → bir kök birimi
Düzenle → Durum: Aktif → Kaydet.

**Kardeş kayıtlar:** [[BL-073]] ana veri zinciri · [[BL-071]] Employee↔PositionAssignment

### BL-331 — Temanın ikincil metin rengi WCAG AA'dan kalıyor, ürün genelinde (2026-09-02, CANLI, ölçüldü)

> **DURUM:** ✅ KAPANDI (2026-09-02, `a651db91`) · **SAHİP:** CONTROL TOWER
>
> Sahip kararı verdi (her sayfayı etkilediği söylendikten sonra). Uygulanan:
> ışık `#68707a` (4.61 gövde / 5.02 kart) · karanlık `#9294ab` (5.18 / 4.58).
> Kenar çubuğu başlıkları ayrıca `--bs-gray-400` palet adımından `--bs-secondary-color`
> metin rolüne çekildi. Canlı: ipucu 2.10→5.02 · başlıklar 2.29→5.02 · karanlık 3.94→4.58.
>
> ⚠ Bu değişikliği bir nöbetçi durdurdu ve haklıydı: `wcn-turc-honesty` "leaves the token
> alone" testi token'ın ezilmesini yasaklıyordu (TUR C aynı zemini ölçmüş ama kararı
> geliştiriciye ait görmemiş). Test SİLİNMEDİ — sonucu koruyacak şekilde çevrildi:
> token yeniden ayarlanabilir, ama iki temada da 4.5:1'in altına düşemez.
>
> **Kapsam notu:** aynı sayfada 19 eleman hâlâ AA'dan kalıyor, farklı sebeplerle —
> marka moru kırıntı yolu bağlantıları (3.33-3.72:1) ve renkli zeminli çipler (2.99:1).
> Bu token'ın meselesi değiller; ayrı kayıt ve ayrı karar isterler.

**MODÜL:** ürün geneli (tema) -- tek bir modülün kusuru değil
**SAYFA:** her sayfa; ölçüm Görev detayı · `/WorkCenterNext/Details/{id}` üzerinde yapıldı
**KONUM:** `wwwroot/assets/vendor/css/core.css` → `--bs-secondary-color: #a7acb2`

**Ölçüm (2026-09-02, canlı oturum, her elemanın GERÇEK zeminine göre):**

    #a7acb2 · beyaz zemin → 2.29:1
    WCAG AA gereği       → 4.5:1 (normal metin)

    tek sayfada bu rengi kullanan görünür METİN elemanı:
        AA'dan kalan : 23
        AA'yı geçen  :  0

    örnekler: kenar çubuğu modül başlıkları ("İnsan Sermayesi Yönetimi", "Satış",
    "Doküman Yönetimi", "Ana Veri Yönetimi"), Görev Merkezi eylem ipuçları

Karşılaştırma: temanın kendi gövde rengi `--bs-body-color: #646e78` = **5.20:1**, geçiyor.
Yani tema tutarsız -- gövde metni erişilebilir, ikincil metin değil.

**Vendor dosyası DÜZENLENMEZ** (proje kuralı; navbar-shift düzeltmesini `backbone-custom.css`de
tutan kuralın aynısı). Düzeltme `backbone-custom.css` içinde bir `:root` ezmesi olur.

**Neden karar sahibe ait:** bu, ürünün HER sayfasındaki soluk metni koyulaştırır. Küçük bir
kod değişikliği (bir satır), ama görsel etkisi geniş. Bir modülün sınıfını tek başına yamamak
YANLIŞ olur -- 23 elemandan yalnız birini düzeltip diğer 22'sini okunamaz bırakır.

**Ölçülmüş aday:** `#6f767e` → 4.60:1 (AA geçer). Sahibin tasarım tercihi başka bir ton
olabilir; şart olan 4.5:1.

İlgili: [[BL-330]] bu kusurun sahibi tarafından "düzenleme yok" olarak görülen yüzü

---

### BL-329 — Görev detayında aynı yere giden İKİ "kaynak kaydı" düğmesi (2026-09-02, CANLI, ölçüldü)

> **DURUM:** ✅ KAPANDI (2026-09-02, `951fe12a`) · **SAHİP:** CONTROL TOWER
>
> Karar uygulandı: ray bağlantısı kaldı, kart düğmesi çekildi. Nöbetçi `railLeadsToSource`
> (href VE çizen bir eylem katmanı) -- salt href yeterli değil, çünkü kapalı görevde ray
> hiç kapı çizmiyor ve salt-href nöbetçisi sayfanın TEK kapısını kaldırırdı.
> Canlı ölçüm: ray 1 · kart 0 · toplam 1 (önce 2). Kaynak kartı kimlik satırlarıyla duruyor.

**MODÜL:** MOD-0024 (Görev Merkezi)
**SAYFA:** Görev detayı · `/WorkCenterNext/Details/{id}`
**KONUM:** `assets/js/WorkCenterNext/app.js:2475` (eylem rayı) + `app.js:4345` (Kaynak kartı)

**Ölçüm (2026-09-02, canlı oturum):**

    eylem rayı   → <a href="/Tasks/{id}">        "Kaynak kayıtta aç"
    Kaynak kartı → <button data-wcn-open="{id}"> "Kaynak kaydını aç"
    ikisi de görünür: true · hedef aynı · etiketler neredeyse aynı

Kodun kendi yorumu kuralı zaten koymuş (`app.js:4341`):
*"Two controls for one destination is the duplication this page keeps removing --
so here it stands down."* Ama nöbetçi yalnız `actionDepth === 'deeplink'` durumunu
tanıyor; ray burada **çıplak** bir bağlantı çizdiği için koşul tutmuyor. Yazılmış
ama bu durumu kapsamayan bir kural.

**Karar sahibe ait:** hangisi kalacak? Önerim eylem rayındaki bağlantı -- eylemler
rayda yaşar, Kaynak kartı kaydın *kimliğini* gösterir. Bu, yorumun zaten yazdığı
kuralın genişletilmesi olur: ray aynı yere götürüyorsa kart düğmesi stand down eder.

İlgili: [[BL-309]] kaynak gezinme modeli

---

### BL-330 — Görev Merkezi detayında "Düzenle" kısayolu yok (2026-09-02, CANLI, sahip gördü)

> **DURUM:** ✅ KAPANDI (2026-09-02, [[BL-331]] ile) · **SAHİP:** CONTROL TOWER
>
> Yeni bağlantı ve yeni string EKLENMEDİ. Cevabı zaten veren cümle okunur hâle geldi:
> `ActionOpenInSourceHint` canlıda 2.10:1 → 5.02:1.

**MODÜL:** MOD-0024 (Görev Merkezi)
**SAYFA:** Görev detayı · `/WorkCenterNext/Details/{id}`
**KONUM:** eylem rayı (`app.js` eylem listesi)

**Ölçüm (2026-09-02, canlı oturum):** düzenleme YETENEĞİ eksik değil --

    /Tasks/{id}/Edit   → "Görevi Düzenle" sayfası ÇALIŞIYOR
                         (TasksController.cs:92, başlık dolu, 2 tarih alanı,
                          9 select2, "Kaydet")
    /Tasks/{id}        → "Düzenle" bağlantısı VAR → /Tasks/{id}/Edit

Yani yol şu: Görev Merkezi detayı → "Kaynak kayıtta aç" → `/Tasks/{id}` →
"Düzenle". İki tık, ama ilk tıkın etiketi düzenlemeyi çağrıştırmıyor; sahip
"düzenleme yok" olarak gördü.

**⚠ 2026-09-02 EK ÖLÇÜM — bu kaydın ilk gerekçesi YANLIŞTI.** Ürün devri zaten
ANLATIYOR. Kaynak kapısının altında, 7 dilde, `ActionOpenInSourceHint` duruyor:

    "Görev Merkezi işin yürütüldüğü yerdir; kaydın kendisi -- başlığı,
     açıklaması ve alanları -- orada düzenlenir."

Canlıda ölçüldü: cümle RENDER EDİLİYOR ve `offsetParent` dolu, yani görünür. Ama:

    .wcn-act-outcome → font-size 12px · color var(--bs-secondary-color) = #a7acb2
    beyaz zemin üzerinde kontrast = 2.29:1 · WCAG AA gereği 4.5:1 → KALDI

Sahibin "düzenleme yok" görmesinin sebebi bu: cevabı veren cümle ekranda, okunmuyor.

**Bu yüzden yeni bir "Düzenle" bağlantısı + 7 dilde yeni string YANLIŞ ÇÖZÜM olur** --
doğru şeyi zaten söyleyen bir cümlenin üstünü örtmek olur. Doğru çözüm kontrast, ve o
da bu kaydın dışında: bkz. [[BL-331]].

BL-330 kapanışı BL-331'e bağlı: cümle okunur olunca sahibin sorusu ekranda yanıtlanmış
olur. Okunur hâliyle hâlâ kısayol isteniyorsa, o zaman ray bağlantısı ayrıca konuşulur.

---

### BL-328 — Kiracı tarafında "Şifremi unuttum" hiçbir yere gitmiyor (2026-09-01, CANLI, sahip gördü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

**MODÜL:** ACCESS-GOVERNANCE (Erişim Yönetimi)
**SAYFA:** Giriş · `/account/login`
**KONUM:** `frontend/Diten.Web/Views/Account/Login.cshtml:122`

**Ölçüm (2026-09-01):**

    <a href="@(authMode == "platform" ? "/platform/forgot-password" : "#")">

    platform yöneticisi → /platform/forgot-password  ✅ çalışıyor
                          (AccountController.cs:220 GET, :227 POST)
    kiracı kullanıcısı  → "#"                        ⚠ HİÇBİR YER

`AccountController`'da yalnız `PlatformForgotPassword` var; kiracı karşılığı
hiç yazılmamış.

⚠ **Pratik sonucu:** kiracı kullanıcısı şifresini unutursa kendi başına
kurtaramaz. Bir yöneticinin `/Users` ekranından "Şifre Sıfırla" yapması
gerekiyor — ama kullanıcı bunu bilmiyor, çünkü ekran ona tıklanabilir bir
bağlantı gösteriyor ve tıklayınca hiçbir şey olmuyor.

⚠ Bu, bu üründe tekrar eden kusur ailesinin bir üyesi: EKRAN BİR ŞEYİN NEDEN
OLMADIĞINI SÖYLEMİYOR. Kardeşleri: [[BL-326]] alt görev kartı sessizce yok
oluyor · [[BL-327]] birim/pozisyon taslak doğuyor, sebebi söylenmiyor ·
kişi seçicideki "neden kısa" ipucunun tarayıcıda ölmesi.

**İki aşamalı çözüm — 1. aşama bugün yapıldı:**

- ⭐ **Aşama 1 (XS, YAPILDI):** yalan bağlantıyı kaldır. Kiracı tarafında
  tıklanabilir bir bağlantı yerine, ne yapılacağını söyleyen bir cümle:
  "Şifrenizi unuttuysanız yöneticinize başvurun." — 7 dilde.
- **Aşama 2 (M, AÇIK):** kiracı için gerçek şifre sıfırlama akışı — uç,
  e-postayla jeton, süre sınırı, sıfırlama ekranı, 7 dil. Platform
  tarafındaki emsal hazır (`AccountController.cs:220-227`,
  `Views/Account/ForgotPassword.cshtml`).

**Kapanış ölçütü (Aşama 2):** kiracı kullanıcısı giriş ekranından şifresini
sıfırlayabilmeli, ve akışın çalıştığını ölçen bir test — bağlantının varlığını
değil, sıfırlamanın gerçekleştiğini ölçen.

---

### BL-333 — Yetkiler JWT'ye claim olarak gömülüyor; token 21,5 KB ve büyüyor (2026-09-04, CANLI, ölçüldü)

**Durum:** AÇIK · **Boyut:** M · **Sahip:** Auth / platform altyapı
**Semptomu bugün geçici olarak kapatıldı** (header tavanı 32→64 KB); kök neden duruyor.

`TokenService.cs:50-52` her yetkiyi ayrı bir claim olarak access token'a yazıyor:

```csharp
foreach (var permission in permissions)
    claims.Add(new Claim("permission", permission));
```

**Ölçüm (2026-09-04, `diten_auth_v3`):**

    tanımlı yetki          407
    kiracı admin'in aldığı 408   (hepsi)
    ortalama ad uzunluğu    37 karakter
    → token ~21,5 KB, altı parçalı çerez olarak taşınıyor
      (access_token=chunks-6, access_tokenC1..C6)

Sayı yerine ölçüm komutu — kayıt bayatlamasın diye:

    mongosh --quiet diten_auth_v3 --eval 'print(db.permissions.countDocuments({}))'

**Neden bugün patladı.** Kestrel'in varsayılan başlık tavanı 32 KB. Tarayıcı
gateway'e DOĞRUDAN giden sayfalarda (`direct-gateway-profile`: Golden Reference,
Governance/*, CRM'in bir kısmı) 21,5 KB çerez + CORS başlıkları tavanı aştı ve
Kestrel **431** döndürdü — istek hiçbir servise ulaşmadan.

⚠ Arıza servis düşmüş gibi göründü: DataTable "Loading…" da takılı kaldı, ve
`initComplete` hiç çalışmadığı için `mountInlineFilter()` çağrılmayıp inline
filtre sayfanın dibinde açıldı. İki ayrı "hata" tek kök nedendi. Aynı anda
`proxy-profile` sayfaları sorunsuz çalışıyordu — çünkü orada MVC controller
token'ı sunucu tarafında `Authorization: Bearer`'a çeviriyor ve tarayıcının
çerezleri gateway'e hiç gitmiyor.

**Bugün yapılan (semptom):** `MaxRequestHeadersTotalSize = 64 KB` — gateway, web
ve arkadaki beş servisin hepsine. Kenarda yükseltmek YETMEDİ: ölçüldü, gateway
200 verirken arkasındaki her servis hâlâ 431 veriyordu; Ocelot çağıranın
başlıklarını forward ediyor.

**Neden bu bir çözüm değil.** Token her yeni modülün yetkileriyle büyüyor —
407 ve artıyor. Tavanı yükseltmek kırılma gününü erteler; bir sonraki modül
64 KB'ı da aşabilir ve o gün sistem hiç açılmaz.

**Kalıcı çözüm:** yetkiler token'dan çıkar. Token yalnız kullanıcı kimliği +
rolleri taşır; yetkiler istek anında sunucu tarafında çözülür (zaten
AuthService'te duruyorlar, `rolePermissions` 1126 satır). Beklenen etki:
token ~21,5 KB → ~1 KB.

**Yan kazanç:** her istekte taşınan 21,5 KB fazladan başlık kalkar. Bir sayfa
~25 istek atıyorsa sayfa başına ~525 KB gereksiz trafik demek — başka bir
makinedeki Codex'in "ortak gateway/layout performans sorunu" bulgusu büyük
olasılıkla bunun aynısı (doğrulanmadı, raporu görülmedi).

**Kapanış ölçütü:** kiracı admin'i ile giriş yapıldığında `access_token`
çerezi TEK parça olmalı (chunks-* yok) ve toplam çerez < 4 KB; ayrıca
`direct-gateway-profile` sayfaları 64 KB tavan GERİ İNDİRİLDİĞİNDE de
çalışmalı — tavan geri 32 KB'a indirilerek doğrulanır.

**İlişkili teknik borç:** `direct-gateway-profile` ile `proxy-profile` karışık
uygulanmış. Kural (`frontend-js-standard.md:40-41`) Platform/admin için
`proxy-profile` ZORUNLU diyor, ama `Governance/Roles`, `Governance/Permissions`
gibi platform ekranları `direct-gateway` kullanıyor. Proxy profili bu arızaya
yapısal olarak bağışık olduğu için, bu tutarsızlık yalnız stil meselesi değil.
Ayrı madde açılmalı.

---

### BL-335 — 29 offcanvas hâlâ Golden Slim'in ESKİ desende; ikon sözleşmesi yayılmadı (2026-09-08, ölçüldü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

Sahip Roller offcanvas'ını açtı ve "Golden Slim'i güncelledik ama burası eski"
dedi. Doğru. Bu turda Roller ve Kullanıcılar taşındı; geri kalanı burada.

**Ölçüm (2026-09-08, Roller/Kullanıcılar taşındıktan SONRA):**

    Views altında düzenlenebilir alan taşıyan offcanvas : 33
      desene uygun (.diten-field + ikon)                :  4
        DevEnablement/GoldenReferenceSlim  (referansın kendisi)
        Tasks/_QuickCreateOffcanvas
        Governance/Roles                   (bu tur)
        Governance/Users                   (bu tur)
      eski desende                                      : 29
        CRM 10 · Platform 6 · MasterDataManagement 4 · Organization 3
        EnterpriseStrategy 2 · PPM 2 · MDM 1 · ManagementGovernance 1

⚠ Sahibin kendi sayımı "24 offcanvas · 23 eski"ydi. Aradaki fark sayım
sınırında: yukarıdaki 33, `Views/**/*Offcanvas*.cshtml` dosyalarından
`form-control`/`form-select` taşıyanların tamamı — yalnız `_CreateEditOffcanvas`
adını taşıyanlar değil. Beş PPM offcanvas'ı hiç alan içermediği için sayının
dışında: işaretlenecek alanı yok.

**Neden sessizce geride kaldılar.** İki muhafız var ve ikisi de dört dosya
okuyor: `diten-field-icons.test.js` → `Views/Tasks/_Form.cshtml` +
`_QuickCreateOffcanvas`; `golden-reference-form-icons.test.js` → iki referans
form. Diğer 29 hiçbir muhafızın kapsamında değildi, yani "referans güncellendi,
ürün güncellenmedi" **yapısal olarak görünmezdi** — kırmızıya dönmeyen bir test
değil, hiç sorulmamış bir soru.

**Bu turda kapatılan boşluk:** `diten-field-icons.test.js`'e ürün geneli sayım
eklendi, `KNOWN_NO_ICONS` bilinen-ihlal listesiyle (`mongo-indexing.md` deseni).
Liste yalnız **küçülebilir**: listede olmayan yeni bir offcanvas desensiz gelirse
kırmızı, listedeki bir dosya düzelip listede kalırsa yine kırmızı. Yani bu kaydın
kapanması, listenin boşalmasıyla **otomatik olarak ölçülür**.

**Tetikleyici:** her modül kendi turuna geldiğinde o modülün offcanvas'ı taşınır
ve `KNOWN_NO_ICONS`'tan satırı silinir. Toplu bir "29'unu birden" turu ÖNERİLMEZ:
ikon seçimi alanın ne olduğuna dair bir karardır, otuz ekranı tanımadan verilemez.

⚠ **Taşırken taklit edilir, kopyalanmaz.** Referansta karşılığı olmayan yardım
metinleri (`NameImmutableHint` gibi) korunur.

⚠ **Sarmalayıcı doğrulama mesajını yutuyordu.** Ölçüldü: core.css hatayı
`.was-validated :invalid ~ .invalid-feedback` ile — KARDEŞ birleştiricisiyle —
gösteriyor; kontrolü `.diten-field` içine almak mesajı sarmalayıcının kardeşi
yapıyor ve satır içi hata sessizce görünmez oluyor. Bu turda `backbone-custom.css`'e
`:has()` tabanlı karşılığı eklendi (temanın `.input-group` için yaptığının aynısı),
yani sonraki taşımalar bu tuzağı miras almaz.

**İlişkili:** [[BL-336]] (aynı turda ölçülen UAS-001 borcu)

---

### BL-336 — 35 ekran yetkisiz kullanıcıya yarım çiziliyor (UAS-001) (2026-09-08, ölçüldü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

Sahip `/Roles`'u yetkisiz bir kullanıcıyla açtı: başlık, dört KPI kartı (hepsi 0),
**"+ Rol Ekle" butonu**, sonsuz dönen "Loading…", *"0 kayıttan 0-0 arasındaki
kayıtlar"* ve üstünde İngilizce bir `Permission denied` bildirimi. Kullanıcının
çıkardığı sonuç "yetkim yok" değil, "veri yok" veya "sistem bozuk".

**Ölçüm (2026-09-07 tabanı, `unauthorized-surface-standard.md` §7):**

    58 controller
      37  yalnız [Authorize]   → Roller gibi davranır
      18  izin kontrolü yapar

Bu turda Roller ve Kullanıcılar UAS-001'e uyarlandı → **kalan 35**.

**Bu turda hazırlanan altyapı — sonraki ekranlar sıfırdan başlamaz:**

- `Views/Shared/_AccessDenied.cshtml` — 403 için **kabuk içinde** çalışan
  paylaşılan partial. Görev Merkezi'nin kendi içinde çözdüğü yüzey paylaşıma
  çıkarıldı; CSS kuralı kopyalanmadı, `.wcn-system-page` seçicisine katıldı.
- `AccessDeniedTitle` / `AccessDeniedMessage` — yedi dilde, `SharedResource`.
- `tests/unauthorized-surface.test.js` — kapı VAR mı, sayfadan ÖNCE mi,
  yönlendirme yok mu, teknik metin sızıyor mu, yedi dil tam mı.

**Bir ekranı uyarlamak (üç satır):**

    @inject Diten.Web.Services.IPermissionSnapshot Perms
    @if (!Perms.Has("<kanonik.izin.anahtarı>"))
    {
        <partial name="_AccessDenied" model="@Localizer["<Ekran>Title"].Value" />
        return;
    }

⚠ `return;` yalnız gövdeyi durdurmaz, `@section Scripts`'in kaydolmasını da
engeller — ve bu bir yan etki değil, amacın kendisi: geçidi çağıran, 403'ü alan
ve İngilizce toast'ı basan şey o bölümdeki `index.js`'ti.

⚠ **Bu bir görüntüleme kararıdır, yetki kararı değil.** Backend `[HasPermission]`
tek doğruluk kaynağı olarak kalır.

**Tetikleyici:** her ekran kendi turuna geldiğinde. `unauthorized-surface.test.js`
içindeki `GATED` haritasına satır eklenerek kapatılır; kapanma ölçütü 35 → 0.

⚠ **Toplu tur önerilmez, ama sebebi BL-335'inkinden farklı:** her ekranın hangi
kanonik izin anahtarına bakacağı, o ekranın verisini gerçekten koruyan anahtardır
ve backend'den okunmalıdır — tahmin edilen bir anahtar, yetkisi OLAN kullanıcıyı
dışarıda bırakır. Yanlış yönde bir hata, kusurun kendisinden pahalıdır.

**İlişkili:** [[BL-335]] · `.antigravity/rules/unauthorized-surface-standard.md`

### BL-337

**Sneat tema özelleştiricisi üç kabuktan da kaldırılacak**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-08

Sahip, Kullanıcı Ekle offcanvas'ında sağda mavi bir dişli gördü ve alan ikonu sandı.
Değil: Sneat şablonunun **tema özelleştirici** paneli — tıklayınca Theme · Skin ·
Layout · Primary Color · RTL ayarlarını açar. Şablon satıcısının kendi tanıtım
sayfası için koyduğu demo aracı.

    template-customizer.js            91 KB
    yüklendiği kabuk                  _Layout · _LayoutTenantShell · _LayoutPlatformAdmin
    koşul                             YOK — dev/prod ayrımı yapılmıyor
    backbone-custom.css'te kural      YOK — konum/z-index vendor varsayılanında

Üç ayrı sorun: (a) sağ kenara sabit, offcanvas'ın üstüne biniyor ve alan ikonu
sanılıyor; (b) tema değiştirme zaten üst çubukta var, bu ikinci bir yol;
(c) "Primary Color" ve "RTL" son kullanıcının değiştireceği şeyler değil —
RTL dil seçiminden gelmeli, marka rengi ürünün kararı.

**Kapanma ölçütü:** üç kabukta da `template-customizer` referansı 0, ve panelin
açtığı ayarların (tema) üst çubuktaki karşılığı çalışmaya devam ediyor.

⚠ "Yalnız geliştirmede göster" çözümü ÖNERİLMİYOR: bir `IsDevelopment()` kontrolü
gerektirir ve bu depoda tam o kontrolün unutulduğu bir örnek aynı gün düzeltildi
(`PositionSeed` üretimde çalışıyordu). Hiç yüklememek daha sağlam; 91 KB da her
sayfadan düşer.

### BL-338

**Varsayılan `_Layout`'a düşen 97 sayfa — hangisi hâlâ yaşıyor?**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-08

⚠ **Bu madde ilk yazıldığında yanlış kurulmuştu.** "97 sayfanın kabuk kararı
verilmemiş" diye açılmıştı; sahip "bunların hepsi eski" deyince ölçüldü ve haklı
çıktı. Sorun kabuk seçimi değil, bu sayfaların **hâlâ neden durduğu**.

    370 sayfa view · 266 kendi kabuğunu seçiyor · 7 Layout = null
    ~97 hiç Layout satırı taşımıyor → Views/_ViewStart.cshtml varsayılanı "_Layout"

Dağılım ve her birinin gerçek durumu:

| sayfa | modül | durum |
|---:|---|---|
| 52 | EnterpriseStrategyBusinessPerformance | eski yüzey; **yenisi Codex'te yazılıyor** |
| 18 | WorkCenter | eski mock; gerçek yüzey `WorkCenterNext` (3 dosya). Eskisinin controller'ı canlı ve **navbar'da** |
| 12 | ManagementGovernance | `feat(mg): add default-off process modeling local surface` — yeni ama **varsayılan kapalı** |
| 7 | DeliveryExecutionManagement | son commit **2026-04-15** |
| 4 | DemandIdeas | son commit **2026-04-15** |
| 1 | InventoryGovernance | son commit **2026-04-15** |
| 1 | DecompositionTreeBuilder | son commit **2026-04-15** |
| 3 | Shared | ⚠ **yanlış sayım** — `Error` ve `NotAuthorized` zaten Layout satırı taşıyor, kapsam dışı |

Yani hiçbiri "kabuk kararı bekleyen canlı sayfa" değil. Dördü beş aydır donmuş,
biri kapalı, ikisi yerine yenisi yazılıyor.

**Asıl soru:** bu 94 sayfa silinecek mi, dondurulacak mı, yoksa canlandırılacak mı?
Karar verilmeden kabuk seçtirmek, silinecek bir yüzeyi cilalamak olur.

**Kapanma ölçütü:** her modül için üç cevaptan biri yazılı — *silinecek* /
*dondurulacak, dokunulmayacak* / *canlandırılacak (o zaman kabuğunu seçer)*.
Silinenler gittikten sonra kalanlar için bir muhafız test "Layout satırı olmayan
sayfa view" sayısını ölçer ve sayı yalnız küçülür.

⚠ Sıralama: WorkCenter kendi başına ele alınmalı — iki Görev Merkezi yüzeyi yan
yana duruyor ve **giriş akışı eskisine yönlendiriyor**. Bu, ölü kod değil, canlı
bir yanlış yönlendirme.


---

### BL-339

**Katalogdan modül silmek Auth izinlerini silmiyor — `test-beta-mod` bunun kanıtı**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-08

`ICatalogPermissionSyncService` iki yönlü tasarlandı: `SyncPermissionAsync` bir
izin anahtarını AuthService kataloğuna iter (Faz 1), `RemovePermissionAsync` ise
son katalog tanımı silindiğinde onu geri alır (Faz 1.5). **İkincisi bağlı değil.**
Bir modül katalogdan düştüğünde izinleri `diten_auth_v3.permissions` içinde
kalıyor ve her rolün izin listesinde görünmeye devam ediyor.

Bugünün kanıtı ölçüldü:

    test-beta-mod.test.view      IsSystem=true  Scope=Tenant
    test-beta-mod.test.create    IsSystem=true  Scope=Tenant

Bu iki anahtar kodda **hiçbir yerde tanımlı değil** — ne `DataSeeder`'da, ne bir
manifest sağlayıcıda. Yalnız iki test fixture'ında adı geçiyor, o da humanize
davranışını ölçmek için.

⚠ **Düzeltme (2026-09-08, CONTROL TOWER ölçümü).** İlk yazımda "katalog
veritabanında da yok" deniyordu; yanlış veritabanlarına bakılmıştı. Katalog
`diten_personalization_dev.platform_module_catalog` içindedir ve kayıt **oradadır**:

    ModuleCode="TEST-BETA-MOD"  DisplayName="Test Beta"  Status=4 (Beta)  IsDeleted=true

Yani modül **kullanıcı arayüzünden silinmiş** (soft delete), izinleri kalmış. Bu
maddeyi zayıflatmaz, güçlendirir: kayıt gizemli bir artık değil, DELETE-sync'in
tetiklenmesi gereken tam senaryonun kanıtıdır.

Kayıtlar 2026-09-08'de sahip talimatıyla elle silindi (2 izin + SuperAdmin'e
bakan 2 `rolePermissions` satırı, birlikte — yalnız izinler silinseydi grant
satırları sarkan referansa dönerdi). **Silme mekanizmayı düzeltmez.**

⚠ Ölçek uyarısı: bugün 2 satır. Gerçek bir modül emekliye ayrıldığında aynı
sızıntı o modülün tüm anahtarlarıyla olur (Doküman Yönetimi tek başına 123 izin
taşıyor) ve hiçbiri elle fark edilmez — kimse silinmiş bir modülün izinlerini
aramaz.

**`IsSystem=true` sorusunun cevabı bulundu.** Mekanizma doğru kurulmuş:
`InternalPermissionsController` katalogdan gelen bir izni yarattıktan hemen sonra
`permission.MarkAsUserDefined()` çağırıyor, yani `IsSystem=false` yapıyor; tam da
DELETE-sync silebilsin diye. Elle tohumlananlar (`auth.*`) `IsSystem=true` kalıp
korunuyor. Silinen iki satır o çağrı eklenmeden **önce** yaratılmış eski
kayıtlardı. Yani bu bir mekanizma hatası değil, geçmiş veri.

⚠ Ama bu, maddenin son cümlesini doğruluyor: mekanizma bağlansa bile geçmişte
`IsSystem=true` ile yaratılmış katalog izinleri **kendiliğinden silinmez**, çünkü
DELETE-sync onları 409 ile reddeder. Faz 1.5 turunun reconcile adımı bunları da
ayıklamalı:

    mongosh "mongodb://localhost:27017/diten_auth_v3" --quiet --eval \
      'db.permissions.aggregate([{$match:{Key:/^platform\./}},
       {$group:{_id:"$IsSystem",n:{$sum:1}}}]).toArray()'

**Ne zaman yapılır:** Faz 1.5 DELETE-sync bağlanırken. O turda ayrıca "hiçbir
tanıma bakmayan izin" için bir reconcile/rapor gerekir — çünkü mekanizma
bağlandıktan sonra bile **geçmişte** sızmış anahtarlar kendiliğinden gitmez.

### BL-340

**`tasks` modülü çalışma zamanı/ayar olarak ayrılsın mı — Meeting kapsamı netleşince**

DURUM: AÇIK · SAHİP: SAHİPSİZ · TETİKLEYİCİ: Meeting module pack

`tasks` tek modül olarak **kalmasına** karar verildi (ADR-001 §2); ayrım yerine
ekrana özel ad köprüsü seçildi. Karar bugünün maliyet dengesine dayanıyor, kalıcı
bir mimari ilkeye değil — bu yüzden kapatılmadı, ertelendi.

Bugünkü denge: rol düzeyinde ayrım zaten mümkün (izinler çip çip veriliyor).
Ayırmanın tek kazancı **modül hakkı** düzeyinde ayrım olurdu: "bu kiracı görev
kullanabilsin ama görev tipi tanımlayamasın". Maliyeti bir module pack, iki
manifest, bir migration ve MOD-0024 kimlik kararına dokunmak.

**Denge şu üç şeyden biri olursa değişir:**

1. Bir kiracı çalışma yüzeyini isteyip ayar ekranlarını istemiyor (veya tersi) —
   yani ayrım artık bir rol ayarı değil, bir satın alma sınırı.
2. Meeting aynı şekli alıyor ve üçüncü, dördüncü modül de aynı çift-anlamlılığı
   üretiyor — o zaman köprü bir desen değil, bir yama olmaya başlar.
3. `tasks` altındaki izin sayısı, tek grup başlığı altında okunamayacak kadar
   büyüyor.

⚠ Yeniden değerlendiren tur ADR-001'i **okumadan** başlamasın: orada reddedilen
iki alternatif ve gerekçeleri yazılı. Özellikle "adı geri al" seçeneği menüde iki
kusur geri getirir ve bu ölçülmüştür.

### BL-341

**"Görevi Güncelle" tek çipi, görev yaşam döngüsünün tamamını veriyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-08

`platform.tasks.update` Rol İzinleri ekranında tek bir "Güncelle" çipi olarak
görünüyor. Arkasında **on dört uç** var:

    grep -c "HasPermission(TaskPermissions.Update)" \
      services/Diten.Platform/src/Diten.Platform.API/Controllers/TasksController.cs

    PUT    /{id}                              accept · plan · start
    POST   /{id}/submitReview                 inquire · return
    POST   /{id}/checklist/items              PUT/DELETE .../items/{code}
    POST   /{id}/checklist/items/state        PUT /{id}/checklist/order
    POST   /{id}/dependencies                 DELETE /{id}/dependencies/{id}

Yani "alanları düzenleyebilsin" diye verilen çip, aynı zamanda görevi kabul etme,
planlama, başlatma, incelemeye gönderme, iade etme, kontrol listesini ve
bağımlılıkları yönetme yetkisini de veriyor.

**Bu bir güvenlik açığı değil** — hepsi aynı görev üzerinde ve hepsi yetkili bir
kullanıcının yapabileceği işler. Bir **sürpriz**: rol kuran kişi verdiğini
sandığından fazlasını veriyor ve ekran bunu göstermiyor.

**Ne zaman yapılır:** Görev Merkezi rol modeli canlı kullanıma girdikten sonra,
gerçek rollerle. Erken bölmek 14 ucu 5 izne dağıtır ve hiçbiri istenmemişken rol
kurmayı zorlaştırır. Önce ölçülmeli: kiracılar "düzenleyebilsin ama başlatamasın"
diye bir ayrım istiyor mu.

⚠ Ara adım (ucuz): izin çipinin üstüne, o iznin kaç ucu kapsadığını gösteren bir
ipucu. Bölmeden önce görünürlük.

### BL-342

**Üç grup başlığı modül adı değil: `mod0251`, `person`, `lookups`**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-08

Modül atfı düzeltmesinden (ADR-001 §1) sonra Rol İzinleri ekranındaki gruplar
gerçek modül kodlarını taşıyor. Üçü taşımıyor:

| grup | izin | sorun |
|---|---:|---|
| `mod0251` | 14 | ham iş kalemi numarası; ekranda **"Mod0251"** diye çizilir. Anahtarlar `mod0251.employee.*` — gerçek adı HCM çalışan ana verisi. |
| `person` | 3 | `platform.person.*` türetmesinden çıktı. Modül değil; muhtemelen HCM'e ya da paylaşılan bir arama servisine ait. |
| `lookups` | 1 | `platform.lookups.read` — tek satırlık bir grup. |

⚠ `mod0251` bu turun ürünü **değil**: ad alanı zaten `mod0251` idi ve servis adı
olmadığı için türetme ona dokunmadı. Yani hata daha eski, yalnız artık görünür.

`person` ve `lookups` ise türetmenin ürünü — ama düzeltilmeden önceki halleri
`platform` kutusunun içinde kaybolmaktı, yani gerileme değil.

**Ne yapılır:** anahtarları yeniden adlandırmak **değil** (anahtar sabittir, ADR-001
§1). Doğru düzeltme, bu üç izin kümesinin gerçek sahibi modülü tespit edip
`moduleOverride` / manifest `ModuleCode` ile açık atıf vermektir — türetmeye gerek
kalmadan. `mod0251` için sahibi HCM ekibidir.

Ara çare: `Perm.Module.*` köprüsüne okunur ad yazmak. Grup adını düzeltir, atfı
düzeltmez — bu yüzden çare, çözüm değil.

---

#### ÖLÇÜM GÜNCELLEMESİ — 2026-09-08 (tohum hijyeni turu)

Üçünün sahibi arandı. **Hiçbirine atıf verilmedi**; sebepleri aşağıda. Sahip
kararı: üçü de olduğu gibi kalsın, sorular burada beklesin.

**`lookups` (1 izin) — "reference-data'dır" varsayımı ÖLÇÜMLE ÇÜRÜDÜ.**
`LookupsController` (`/api/lookups`) şunları sunuyor: countries · currencies ·
locales · languages · timezones · tenant-tiers · feature-categories ·
subscription-cycles · audit/{categories,operations,outcomes} ·
module-catalog/{domains,services,permission-modules}. Bunlar **PSS sistem
lookup'ları**. `reference-data` modülü ise BusinessReferenceData'dır ve rotaları
`/Platform/ReferenceData/*`. [PSS-LOOKUPS-001](../../../.antigravity/rules/platform-lookups-reference-data.md)
ikisini açıkça ayırır ve PSS lookup'ın kendi pack adresini verir:
`execution/domains/platform-shared-services/module-packs/PSS-011-lookups-reference-data.md`.
Canlı katalogda (32 kayıt) PSS lookup diye bir modül yok.
→ **Soru:** PSS lookup yüzeyinin modül kodu nedir, ve bir manifest onu
yayınlayacak mı? Yoksa `platform.lookups.read` kalıcı olarak sahipsizdir.

**`person` (3 izin) — kanıt iki yöne çekiyor.**
Lehine: MOD-0288'in kanonik adı *"Organization, Person & Position Directory"*, ve
`platform.person.lookup_validation` tohumda organization-units bloğunun hemen
üstünde duruyor. Aleyhine: `OrganizationManifestProvider` **hiçbir person sayfası
beyan etmiyor** (yalnız OrganizationUnits / Positions / PositionAssignments), yani
hiçbir manifest bu izinleri sahiplenmiyor. Ayrıca üçü de `Scope=PlatformAdmin` ve
`organization` **PlatformAdminModules'te değil** — atıf verilseydi Scope'un elle
`PlatformAdmin`'e sabitlenmesi gerekirdi, yoksa Tenant'a düşerdi (ADR-001 §1).
Elle sabitleme gereği, işaretin zayıf olduğunun kendisidir.
→ **Soru:** person referans yüzeyi MOD-0288'e mi ait? Öyleyse kalıcı çözüm
`OrganizationManifestProvider`'ın person sayfalarını beyan etmesidir — `moduleOverride`
değil. `platform.person.search` ve `.view` zaten tohumda yok (worker üretiyor,
`IsSystem=false`), yani yalnız tohumu düzeltmek üç anahtarı iki gruba bölerdi.

**`mod0251` (14 izin) — HCM ekibine sorulacak soru.**
→ *"`mod0251.*` anahtarlarının sahibi modül kodu nedir, ve self-registration
manifestinizde bu kod yayınlanıyor mu?"* Sahibi `services/Diten.HcmService`
(çalışan ana verisi). Katalogda HCM modülü yok; kod uydurmak, HCM kendi
manifestini gönderdiğinde ikinci bir yanlış atıf yaratır.
⚠ **Modül kodu bu turda dokunulmadı.** Aksiyonlardaki snake_case
(`view_sensitive`, `change_status`, `edit_legal`, `edit_employment`,
`create_draft`, `attach_evidence`, `view_status_history`, `data_quality`) ise
düzeldi — ama bir HCM kararı olarak değil, `Permission` kurucusundaki tek
yazım kuralının (`PermissionSegmentNormalizer`) kaçınılmaz sonucu olarak.
Anahtarlar değişmedi. Kurala istisna listesi açmak, bu depoda tekrar tekrar
cezalandırılan desendir; onun yerine kural tek ve istisnasız tutuldu.

### BL-343

**Ana dalda kırmızı duran muhafızlar: bir gizlilik sözleşmesi, on üç ön yüz dosyası, Platform'da yetmişe yakın test**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-08 · GENİŞLETİLDİ: 2026-09-10

RBAC turunun bağımsız doğrulaması sırasında ölçüldü. **Hiçbiri o turun ürünü
değil**; hepsi dal açılmadan önce kırmızıydı ve bu yüzden ayrı bir madde.

**a) Gizlilik sözleşmesi kırmızı — iki test**

    UserLookupValidationContractTests.ResponseDtoContainsOnlyUserIdAndReferenceable
    UserLookupValidationContractTests.ResponseJsonDoesNotLeakTenantOrProfileAuthorizationOrStatusDetails

    Beklenen: ["Referenceable", "UserId"]
    Gerçek:   ["MaskedEmail", "MaskedName", "Referenceable", "UserId"]

Kullanıcı arama doğrulama yanıtına `MaskedEmail` ve `MaskedName` eklenmiş; sözleşme
testi o yanıtın **yalnız** kimlik ve doğrulanabilirlik taşımasını şart koşuyor.
Ekleyen commit `0f71a237` ve **ana dalda**, yani bu muhafız main'de kırmızı duruyor.

⚠ İkisinden biri yanlış: ya alanlar oraya ait değil (maskeli de olsa profil verisi
sızdırıyor), ya sözleşme eskimiş ve gerekçesiyle güncellenmeli. Karar verilmeden
kapatılamaz — bir gizlilik muhafızını sessizce yeşile çekmek, onu yazmamış olmakla
aynı şeydir.

**b) On üç ön yüz test dosyası kırmızı — 25 test**

    campaign-targeting-admin-ui · consent-preference-admin-ui · dialog-one-implementation
    diten-tags · global-confirm-input-type · objectives-edit-hydration
    planning-cycles-owner-position · planning-cycles-register · pvg-case-intake-triage-ui
    strategy-apis · strategy-periods-owner-position · strategy-periods-register
    wcn-dialog-one-language

İkisi (`diten-tags`, `wcn-dialog-one-language`) `backbone-custom.css` okuyor, yani
RBAC turunun CSS değişikliğinden şüphelenildi. Ölçüldü: CSS değişikliği geri
alınıp koşulduklarında **yine kırmızı**. Sebep başka.

⚠ Ölçek: 154 dosyanın 13'ü, 2381 testin 25'i. Küçük bir oran, ama kırmızı bir
takım "testler geçiyor mu" sorusunu cevaplanamaz hale getirir — her tur bu 13'ü
elle ayıklamak zorunda kalır ve bir gün biri fazladan bir kırmızıyı da eski
sanar. Bu maddenin asıl maliyeti budur.

**d) `TenantArchitecture.ArchitectureTests` — `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` kırmızı (ölçüldü 2026-09-11,
main'i dala aldıktan sonra, 96b0eaf1).** Muhafız iki dosyayı gösteriyor: `Audit/PpmAuditRetentionPolicySeedMongoTests.cs` ve
`Persistence/DisposableStandaloneMongo.cs` — ikisi de `KnownPerRunDatabase` listesinde değil. Üç dosya (muhafız + ikisi) tabandan
(7b11f3e9) beri değişmemiş; son commit'ler 27–31 Ağustos → main o günden beri bu kuralda kırmızı. Sahip: PPM denetim testi (Audit) /
Platform test altyapısı. **Ölçüm komutu:** `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter MongoTestDatabaseGuard`

**c) `Platform.Application.Tests` — bu madde onları hiç yazmıyordu**

2026-09-10 tam koşum: **4070 testin 75'i kırmızı**. Dal `fix/workcenter-role-testing`; bu
dalın `main`'e göre değiştirdiği Platform dosyalarının **hiçbiri** aşağıdaki alanlara
dokunmuyor (`git diff --name-only main...HEAD -- services/Diten.Platform` → yalnız WorkReport
dosyaları ve `DocumentReferenceListTests.cs` düzeltmesi). Yani hepsi dal öncesi.
**Düzeltilmedi, yalnız kaydedildi** — sahipleri başka ekipler.

⚠ Sayılar bu koşumdan; kayar. Her satırın **ölçüm komutu** sayının yerine geçer.

| sebep | sınıflar | 2026-09-10 | sahip |
|---|---|---:|---|
| aynı aksiyonda birden çok `[HasPermission]` (`0f71a237`) → `GetCustomAttribute<HasPermissionAttribute>` `AmbiguousMatchException` | `Mod0029Fu29aEndpointAttributionTests` | 3 | Doküman Yönetimi |
| 2 menü sayfası eklendi, test güncellenmedi (`fb4245f0`) | `DocumentManagementManifestProviderTests` | 3 | Doküman Yönetimi |
| yaşam döngüsü / onay kapısı / eğitim — disk okumuyor, saf mantık | `DocumentLifecycleStatusTests` · `DocumentReleaseGateTests` · `DocumentTrainingMatrixTests` | 14 | Doküman Yönetimi |
| **ayrıca:** testin aradığı `Name = CorporateActiveInstanceIndexName` metni index yapılandırmasında artık yok | `CorporateCollectionInstanceFoundationTests.Corporate_unique_index_uses_positive_active_filter_only` | 1 | Doküman Yönetimi |
| **ayrıca:** 8 abonelik handler'ından yalnız `SuspendTenantSubscriptionCommandHandler`, `Handle` çağrı grafiğinde `TenantSubscriptionTransactionWriter` göstermiyor (test `261f9910`) | `SubscriptionHandlerTransactionArchitectureTests` | 1 | Kiracı abonelikleri |
| BRD Mongo ailesi — aşağıya bak | `BusinessReferenceData*MongoTests` (sweeper hariç) | 49 | BRD / ortam |
| flaky — ardışık koşumlar birbirinin artığını süpürüyor | `BusinessReferenceDataMongoResidueSweeperTests` | 4 | BRD |

**BRD ailesi TEK sebep değil, ve dağılımı koşumdan koşuma değişiyor.** Brifingde "≈43, hepsi
`Timestamp`" deniyordu (tek örnek mesajdan). Her mesaj ölçüldü, iki ayrı belirti çıktı:

- `System.FormatException : ObjectSerializer does not support BSON type 'Timestamp'.` —
  test Mongo'su **replica set** (`rs0`, `DisposableMongoReplicaSet.cs`), komut yanıtında
  `Timestamp` taşıyor.
- `MongoCommandException : Command dropDatabase failed: The database is currently being dropped.`
  — sınıflar paralel koşarken aynı anda veritabanı düşürüyor.

Aynı test bir koşumda birinden, sonrakinde ötekinden düşüyor: tam koşumda **36 Timestamp +
13 dropDatabase**, hemen ardından yalnız BRD koşumunda **11 Timestamp + 38 dropDatabase**
(+3 sweeper). Toplam ~50 sabit, bölüşüm değil. Ayrıca ölçüldü: iki gün önceki koşumlardan
kalma **4 sahipsiz `diten-platform-mongo-rs-*` `mongod`** süreci hâlâ açık — harness
süreçlerini her zaman kapatmıyor.

**Ölçüm komutları:**

    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~Mod0029Fu29aEndpointAttributionTests"
    git show 0f71a237 -- services/Diten.Platform/src/Diten.Platform.API/Controllers/ | grep "^+.*HasPermission"
    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~DocumentManagementManifestProviderTests"
    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~DocumentLifecycleStatusTests|FullyQualifiedName~DocumentReleaseGateTests|FullyQualifiedName~DocumentTrainingMatrixTests"
    grep -c "Name = CorporateActiveInstanceIndexName" services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Configurations/MongoDbIndexConfigurations.cs
    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~SubscriptionHandlerTransactionArchitectureTests"
    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~BusinessReferenceData" --logger "console;verbosity=normal" | grep -c "BSON type 'Timestamp'"
    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~BusinessReferenceData" --logger "console;verbosity=normal" | grep -c "currently being dropped"
    ps -eo command | grep -c '[d]iten-platform-mongo-rs-'

**d) Mimari muhafız da kırmızı — bir test**

    MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun
      → Audit/PpmAuditRetentionPolicySeedMongoTests.cs · Persistence/DisposableStandaloneMongo.cs

Her koşumda veritabanı adını yeni bir GUID'den kuran iki dosyayı gösteriyor. Ölçüm:

    dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter "FullyQualifiedName~MongoTestDatabaseGuardTests"

---

**2026-09-15 (a) kapandı — `d9d7b90e`:** `UserLookupValidationContractTests` haklıydı; `0f71a237`'nin karar atfı olmadan eklediği `MaskedName`/`MaskedEmail` kaldırıldı, sözleşme testleri değişmeden yeşil. Tek okuyucu CRM onay ekranının yedek etiketi (BL-416).

### BL-345

**Kontrol listesinde "kanıt zorunlu" işaretlenemiyor — form sabit `false` gönderiyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-09

Kontrol listesi maddesinde `EvidenceRequired` alanı var, saklanıyor, izdüşüme taşınıyor
ve **motor gerçekten uyguluyor** — kanıt yoksa eylem kapatılıyor:

    WorkItemProjectionService.cs:186
      Disabled("approve", ActionApproveKey, WorkAggregationReasonCodes.EvidenceRequired, …)

Ama görev formundan madde eklerken değer **sabit yazılıyor**:

    form-page.js:226
      { text, requirement: checklistDraftLevel, evidenceRequired: false }

Yani alan var, motor sayıyor, **hiç kimse işaretleyemiyor**. Kutu ekranda yok, dolayısıyla
`EvidenceRequired = true` olan bir madde yalnız şablondan gelebilir.

⚠ Bu, MOD-0024 paketinin anlattığı `reasonCode: null` hatasının **birebir kardeşi**:
istemci bir alanı sabit gönderir, motor sadakatle onu yazar, ve sütun aylarca boş kalır
— kimse fark etmez çünkü hata bir istisna değil, bir varsayılan.

**Ne zaman yapılır:** ⚠ Tek başına DEĞİL. Kanıtın kendisi **MOD-0031 (Evidence Linking
Service)**'in işi ve o modül **kayıtta var, kodda yok** (`services/` altında karşılığı
bulunmuyor, 2026-09-09 ölçümü). Bugün kutuyu açmak, işaretlenebilir ama sağlanamaz bir
zorunluluk üretir: madde işaretlenemez, ekleyecek kanıt da yoktur.

Doğru turu **MOD-0024 Faz 2 (görev kapanış zarfı)** — aynı dosyalara dokunuyor ve aynı
soruyu cevaplıyor: bir görev kapanırken neyi kanıtlamış olması gerekir.

**Ölçüm komutu:**

    grep -n "evidenceRequired: false" frontend/Diten.Web/wwwroot/assets/js/Tasks/form-page.js
    grep -rn "EvidenceRequired" services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/

### BL-346

**MOD-0024 Dilim 1e — İş Raporu dışa aktarma: düğme ve davranış birlikte**

DURUM: ⚠️ KAPANIŞ (KISMİ) · SAHİP: ali.tufanoglu · KOD: 2026-09-10 (commit: CONTROL TOWER alacak)

**Ne yapıldı.** `GET /api/v1/tasks/work-report/export?format=csv|json` + web proxy
`/Tasks/api/work-report/export` + araç çubuğunda, filtre rozetinin yanında indirme menüsü
(CSV · JSON). Dosya, raporun **arkasındaki satırlar**: her görev bir kez, ve her satırda
ekrandaki hangi hücreye girdiğini söyleyen `1/0` sütunları (`Opened`, `Late`,
`AgingOlderThan30Days`, …). Bir sütunu toplamak, ekrandaki o sayıyı verir.

**Kararlar ve neden.**
- **Aynı sorgu, ayrı sorgu değil.** `WorkReportRepository.ExportAsync`, sayıların kullandığı
  `ReadAsync`'i çağırır; üyelik `WorkReportTally.Select`'ten gelir. Kapsam
  (`IWorkReportScopeSource` → MOD-0018-FU15 `IDataScopeResolver`), dönem, beş filtre ve
  kapsam tercihi ekranınkiyle aynıdır. Reddedilen: `work-report/items`'ı sayfa sayfa
  çağırmak — o uç **tek hücre** döndürür, "raporun satırları" diye bir hücre yok.
- **İzin aynı:** `WorkReportRead`. Ayrı `work-report.export` açılmadı.
- **Sınır aşılırsa ret, kesme yok:** 50 000 (audit'in `MaxRows`'u).
  `WORK_REPORT_EXPORT_TOO_LARGE` → ekran "filtreyi daraltın" der, "başarısız" demez.
- **Sütun başlıkları çevrilmez** — ölçüldü: audit CSV'si sabit İngilizce tanımlayıcı yazıyor.
- **Audit'ten iki bilinçli fark:** UTF-8 BOM (Excel'de Türkçe/Arapça başlık bozulmasın) ve
  sayılar invariant kültürde (`1,5` virgüllü dosyada hücre böler).
- **Dosya adı öneki yerelleşir:** sunucu `work-report_{ilk gün}_{son sayılan gün}` yazar,
  ekran yalnız öneki okuyucunun diline çevirir (`is-raporu_…`).
- **Object URL `finally` içinde bırakılır** — audit'te `click()`'ten sonraki satırdaydı.

**Kasten yapılmayanlar.** XLSX (audit deseninde yok, kütüphane getirir) · grafik/PNG ·
zamanlanmış rapor · **dışa aktarma denetim kaydı** (→ BL-347).

**Canlı doğrulama — 2026-09-10, `admin@diten.com`, `/Tasks/WorkReport`:**

Ölçüldü ✅
- Rota üç katmanda var: web proxy 302 (oturumsuz → giriş), gateway 401, Platform 401;
  karşılaştırma için var olmayan yol 404.
- Düğme menüsü açılıyor, iki giriş (CSV · JSON) çalışıyor; rapor yüklenince etkin.
- CSV: 200, `text/csv; charset=utf-8`, ilk üç bayt `EF BB BF` (BOM), başlık satırı doğru.
  `Content-Disposition` ve `X-Work-Report-Export-Row-Count` gateway + web proxy'den sağ geçiyor.
- JSON: 200, `application/json`, dizi. `format=xlsx` → 400 `VALIDATION_FAILED`.
- Dosya adı yerel önekle ve dönemle iniyor: `is-raporu_2026-08-12_2026-09-10.csv`,
  `تقرير-العمل_2026-08-12_2026-09-10.csv`.
- Her indirmede 1 `createObjectURL`, aynı URL için 1 `revokeObjectURL`; sayfada `<a download>` kalmıyor.
- Işık ve koyu tema; tr ve ar (`dir="rtl"`, menü sola açılıyor, bildirim Arapça).
- Konsol: tek hata, bilerek gönderilen `format=xlsx` isteğinin 400'ü.

Ölçülemedi ⚠️ — **kapanışı bekleten madde bu**
- **Sütun toplamı = kart, filtresiz ve iki filtreli.** Geliştirme veritabanında
  (`diten_personalization_dev`) görev yok: `scripts/seed-closure-outcomes-dev.sh --status` →
  `tasks: 0`. Rapor boş, dosya yalnız başlık; 0 = 0 kanıt değildir. Kimlik bugün
  `WorkReportExportTests.Summing_a_column_…` ve S1/S2 sabotajlarıyla korunuyor, canlıda değil.
- **403 → yetki cümlesi.** İzinsiz bir kullanıcıyla oturum açılmadı; yalnız vitest'te ölçüldü.

**Ölçüm komutları:**

    dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter "FullyQualifiedName~WorkReportExport"
    (cd frontend/Diten.Web && npx vitest run tests/work-report-export.test.js)
    grep -n "await ReadAsync(criteria, ct" services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/WorkReportRepository.cs

**Kalan ölçümün yeri:** `docs/guides/operations/organization-chain-walkthrough.md` §8 —
sahibin walkthrough'unda gerçek görevlerle, filtresiz ve tüzel kişilik filtreli iki
indirmeyle kapanır. Ajanın test görevi yazması reddedildi: dev veritabanında tüzel kişilik 0,
atama 0 (2026-09-10 ölçümü) — "iki filtreli" kontrol için gereken veri, sahibin elle girmek
üzere sıfırladığı walkthrough verisinin ta kendisi.

### BL-347

**İş Raporu dışa aktarması denetim kaydı bırakmıyor — kiracı tarafında uygun yazıcı yok**

DURUM: KAPANDI (kod; canlı kontrol bekliyor) — `5681eaac` (görev motoru dalı, 2026-09-15), toplantı zincirine `aaa66e29` · SAHİP: altyapı CT · ÖLÇÜLDÜ: 2026-09-10

**Kapanış (2026-09-15).** Ayrı bir yazıcı yazıldı: `IDataExportAuditWriter` (`Features/Audit/Services/DataExportAuditWriter.cs`). İş Raporu indirmesi dosyayı ürettikten sonra, kullanıcıya vermeden önce bir DataExport kaydı yazar. Aktör türü jetondaki `actor_type`'tan gelir (tenant_user / platform_admin / partner_admin); tanınmayan tür kayıt uydurmaz, indirmeyi reddeder. Kayıt ilgili kiracıya aittir (`IsPlatformGlobal=false`), filtre özetinde kişi kimliği yok. Kayıt yazılamazsa dosya verilmez: 503 `DATA_EXPORT_AUDIT_NOT_RECORDED` (ekran bugün genel "dışa aktarılamadı" mesajını gösteriyor; özel mesaj 7 dil, PSS takip WP'sinde). Platform denetim dışa aktarması değişmedi (regresyon testi). Ajan sabotajı S1–S3 kırmızı; CT sabotajı (kiracı kullanıcısı platform yöneticisi diye kaydedilince) 4 kırmızı → geri yükleme → 49/49 yeşil. **Canlı kontrol:** kiracı kullanıcısıyla CSV/JSON indir → `audit_outbox`'ta bir kayıt → `audit_events`'te TenantUser satırı. Ayrı ve eski kusur: genel komut denetim hattı aktör türünü yanlış yazıyor → BL-409.

Dilim 1e (BL-346) audit export'unu taklit etti, bir yer hariç: audit handler'ı indirmeden
sonra `AuditMetaAuditWriter.WriteAsync(... AuditCategory.DataExport ...)` çağırıyor. Aynı
çağrı İş Raporu için **yazılmadı**, çünkü o yazıcı her olayı şöyle damgalıyor:

    AuditMetaAuditWriter.cs  ActorType = PlatformAdministrator · IsPlatformGlobal = true · SourceModule = "Audit"

Bir kiracı kullanıcısının indirmesi bununla kaydedilseydi, kayıt "bir platform yöneticisi
yaptı" diyecekti — yanlış kayıt, kayıtsızlıktan kötüdür.

**Risk:** GxP bağlamında "kim, hangi veriyi, ne zaman sistemden çıkardı" sorusunun bugün
cevabı yok. Veri kapsamla sınırlı (kişi zaten görebildiğini indiriyor), ama iz yok.

**Ne zaman yapılır:** kiracı tarafı denetim yazıcısı (`IAuditableCommand` hattının sorgu
eşdeğeri veya `AuditMetaAuditWriter`'a aktör tipi parametresi) karara bağlandığında. Tek
satırlık ekleme olarak değil — önce aktör tipini doğru yazan bir yazıcı gerekir.

**Ölçüm komutu:**

    grep -n "ActorType\|IsPlatformGlobal\|SourceModule" services/Diten.Platform/src/Diten.Platform.Application/Features/Audit/Services/AuditMetaAuditWriter.cs
    grep -n "AuditMetaAuditWriter\|DataExport" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/QueryHandlers/WorkReportExportQueryHandler.cs

### BL-348

**Docs yol muhafızı: `docs/` altında beşli dışına işaret eden kod artık derlemede değil testte düşer**

DURUM: ✅ KAPANDI · SAHİP: ali.tufanoglu · KOD: 2026-09-10 · COMMIT: `fe8aaa6e` (CT doğruladı: yeşil → kendi sabotajı `scripts/ct-sabotage-docsguard.ps1:2 → docs/audits` kırmızı → yeşil)

**Ne yapıldı.** `tests/architecture/TenantArchitecture.ArchitectureTests/DocsPathGuardTests.cs`
— kod dosyalarında (`.cs .cshtml .js .py .sh .ps1 .json .csproj .css .html .yaml .yml .xml
.resx`) `docs/` altında `vendor · records · roadmap · guides · reference` dışındaki bir
klasöre giden yolu bulur; hata `dosya:satır → docs/<eski> — bkz. docs-organization.md §4`.
`.antigravity/rules/docs-organization.md` §4 madde 4'e `.ps1` ve testin adı eklendi.

**Neden.** 2026-09-07 taşıması (`9d8551e1`) kodu dört kez kırdı — `.py`, `.css`,
`.cs` (`DocumentReferenceListTests`, 10 test), `.ps1` — ve kural bunu **hatırlamaya**
bırakıyordu; listesinde `.ps1` bile yoktu.

**Kararlar.**
- Klasör sayılan yalnız ayraçla devam eden segment (`docs/x/`, `docs\x\`) ve ardından
  başka argüman gelen `Path.Combine(…, "docs", "x", …)`. `docs/` kökündeki DOSYA başka soru.
- `Path.Combine` biçiminde klasör adı boşluk içeremez — ilk yanlış pozitif bir UI etiketiydi
  (`DemandIdeaCapturePageMapper.cs:197`, `"docs", "Supporting documents attached"`).
- Servislerin kendi `docs/` klasörleri (`Diten.AuthService`, `Diten.Platform`) diskten
  bulunur, listelenmez. Yorumlar taranır — kırılan CSS bir yorumdu.
- `.yml` listeye eklendi (CI iş akışları); brifingde `.yaml` vardı.

**Sabotaj kanıtı.**
1. `DocumentReferenceListTests.cs`'de `"docs", "integration"` → kırmızı,
   `…DocumentReferenceListTests.cs:30 → docs/integration`.
2. `scripts/smoke-mod0155-visit-planning-authenticated.ps1`'e `docs/audits/x.md` → kırmızı,
   `…:72 → docs/audits` — `.ps1` kapsamını kanıtlar.
İkisi de geri alındı (sağlama değerleri aynı), muhafız yeşil.

**Kasten yapılmayanlar.** `docs/` kökündeki var olmayan DOSYAYA işaret eden referanslar
(ör. `ActivateModuleCatalogItemCommandHandler.cs:50` → `docs/workflow-transition-gate-standard.md`)
bu muhafızın kapsamı dışında; ayrı iş. Metin taraması — iki ifadeye bölünmüş yol kaçar.

**Ölçüm komutu:**

    dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter "FullyQualifiedName~DocsPathGuardTests"

### BL-349

**Görev detayı: kimliğini bilen herkes açabiliyor — liste süzülüyor, detay süzülmüyor**

DURUM: KAPANDI — PSS dalı `feature/pss/mod-0024-review-meeting-policy` (WP-PSS-MOD0024-TASK-READ-ACCESS-01; CT sabotajla doğruladı, 2026-09-13). Detay, ek listesi ve ek içeriği tek okuma kuralını soruyor; ilişkisiz okuyana var olmayan görevle birebir aynı 404. Canlı doğrulama sabah sahipte (ikinci kullanıcıyla) · SAHİP: PSS · ÖLÇÜLDÜ: 2026-09-10

**Ek (2026-09-14):** @ ile etiketleme (`b9476a4e`, `feature/pss/mod-0024-task-mentions`, MOD-0024 paketi §21) bu kurala bağlandı;
kural `ResolveDataLegCandidatesAsync` ile adayları sayabiliyor. **Verimlilik notu (engel değil):** `CanReadAsync` artık her çağrıda bütün
ilişki bacaklarını (havuz sahipleri, izleyiciler, üst görev) hesaplıyor; etiketleme doğrulaması bunu etiketlenen kişi başına (en çok 10)
tekrarlıyor. Detay açılışında birkaç sorgu, 10 kişilik yorumda ~30 sorgu. Gerekirse adaylar bir kez hesaplanıp kişiler o kümede aranır.

`GetTaskItemListHandler` yalnız bana atanan + havuzumdaki görevleri döner. `GetTaskItemByIdHandler` ise
yalnız kiracı filtresi + `platform.tasks.read` ister: görevle hiçbir ilişkisi olmayan kullanıcı, kimliğini
bilirse (bağlantı, tahmin, başka ekrandan kopya) kiracıdaki her görevin başlığını, açıklamasını ve alanlarını
okur. Toplantı modülü görev bağlarını göstermeye başlayınca bu yol görünür olur.

**Karar gerekli:** "kim hangi görevi görebilir" — atanan/talep eden/izleyen mi, birim/şirket kapsamı mı,
yoksa okuma yetkisi olan herkes mi (bugünkü fiilî durum). Cevap BL-057'nin listeleme yarısıyla aynı yerden
çıkmalı; iki ayrı kural yazılmamalı.

**Ölçüm komutu:**

    grep -n "GetByIdAsync\|_actor" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/QueryHandlers/GetTaskItemByIdHandler.cs

**Öneri (2026-09-13, WP-PSS-MOD0024-TASK-SCOPE-SECURITY-01 Kısım B — karar sahipte):** detay ucunu okuyan her yer ölçüldü:
Görev Merkezi alt görev paneli ve alt görev sürüm okuması (üst görevin sahibi, alt görevin sahibi farklı olabilir), alt görev
eklerken üst görev okuması, `/Tasks/Details` ve `/Tasks/Edit` (bugün kiracıdaki herkes). Önerilen TEK kural: görevin sahibi veya
havuzu + açan + izleyici + ÜST görevin sahibi veya havuzu + `TaskAssignmentScope` (bir yönetici, iş atayabildiği kişinin görevini okur).
Kiracı geneli okuma gerçekten gerekiyorsa ayrı bir `platform.tasks.read-all` anahtarı, yalnız o role. Liste kuralıyla aynı yerden
türetilir; ikinci kural yazılmaz.

### BL-350

**Tekrarlayan kural formunda kişi listesi hep boş — ekran düz liste bekliyor, sunucu zarf gönderiyor**

DURUM: ⚠️ KAPANDI (KISMİ) · SAHİP: CT · KOD: 2026-09-10 · COMMIT: `b7d8918b` · ✅ için: sahibin kontrol turu (canlı oturumla doğrulanmadı)

**Ne yapıldı.** `fetchJson` dizi değilse `rows.people` / `rows.People` zarfını açıyor (`Tasks/api.js`'in kendi `assignablePeople()`
yolunun aynısı); havuz ve şablon listeleri değişmedi; kayıtlı seçim düzenlemede yine yerine geliyor. 3 test
(`recurrence-rule-assignable-people-envelope.test.js`) eski kodda 2 kırmızı, yenide yeşil — CT kendi sabotajıyla ölçtü.
**Not:** aynı kalıp `Tasks/Templates/form.js:26`'da da var; o üç uç bugün zarf döndürmediği için bozuk değil, gizli aynı hata.

`Tasks/RecurrenceRules/form.js:17-30` `fetchJson` yalnız dizi kabul ediyor (`Array.isArray(rows) ? rows : []`).
`GET api/v1/tasks/lookups/assignable-people` ise `AssignablePersonLookupDto { People, Excluded }` döner
(`TaskModels.cs:975`). Sonuç: kişi seçici boş; kural yalnız havuza yazılabiliyor. Görev formu aynı uca
`Tasks/api.js:307` üzerinden gidiyor ve zarfı açıyor — kural formu onu kullanmıyor.

**Ölçüm komutu:**

    sed -n 17,30p frontend/Diten.Web/wwwroot/assets/js/Tasks/RecurrenceRules/form.js
    grep -n "record AssignablePersonLookupDto" -A2 services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/TaskModels.cs

### BL-351

**`Tasks/api.js`: aynı hata kodu iki mesaja bağlı — "bekleme kişisi" reddinde yanlış cümle**

DURUM: ⚠️ KAPANDI (KISMİ) · SAHİP: CT · KOD: 2026-09-10 · COMMIT: `be8c3ecf` · ✅ için: sahibin kontrol turu (canlı oturumla doğrulanmadı)

**Ne yapıldı.** Temel eşleme tek: atama cümlesi (çağıranların hepsi atama). `INQUIRE_REASON_CODE_OVERRIDES`
bekleme cümlesini taşıyor; `failureMessage(result, overrides)` ikinci argüman aldı; `inquire`'ı gönderen TEK yer
(`WorkCenterNext/app.js` `submitRealTransition`) onu geçiyor. Yeni resx yok, sunucu değişmedi. Muhafızlar: temel eşlemede
her kod bir kez · geçersiz kılma yalnız verilince · `submitRealTransition` içindeki her çağrı geçiyor ve inquire'ı başka
gönderen yok — her biri kendi yarısı çıkarılınca kırmızı (alt ajan + CT sabotajı).

`REASON_CODE_MESSAGE_KEYS` içinde `TASK_ASSIGNEE_NOT_ASSIGNABLE` iki kez: satır 68 (`errorWaitingOnNotAssignable`)
ve 155 (`errorAssigneeNotAssignable`). JavaScript nesne sabitinde sonraki kazanır; `InquireTaskItemHandler`
(`TaskItemTransitionHandlers.cs`, bekleme kişisi reddi) aynı kodu döndüğünde kullanıcı "Bu kişiye iş atanamaz.
Listeden birini seçin." okur, oysa beklediği kişiyi seçiyordu. `ErrorWaitingOnNotAssignable` 7 dilde var ama
hiç gösterilmiyor (ölü anahtar).

**Ölçüm komutu:**

    grep -n "TASK_ASSIGNEE_NOT_ASSIGNABLE" frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js

### BL-352

**Atananı uygunluğunu kaybetmiş tekrarlayan kural kapatılamıyor — koruma her kayıtta soruyor**

DURUM: ⚠️ KAPANDI (KISMİ) · SAHİP: CT · KOD: 2026-09-11 · COMMIT: `2eb678e6` · ✅ için: sahibin kontrol turu (canlı oturumla doğrulanmadı)

**Ne yapıldı.** Güncellemede koruma yalnız bu kayıt bir atama SEÇİYORSA sorulur: hedef (tür/kişi/havuz) kayıtlıdan farklıysa
VEYA kural pasiften aktife dönüyorsa (CT eki: yeniden etkinleştirme = yeniden atama). Değişmeyen hedef — kapatmak dahil —
sorgusuz kaydedilir. Oluşturma ve zamanlanmış üretim değişmedi; koruma çağrısı yazmadan önce ve zorunlu bağımlılık olarak
duruyor (kaynak tarayan muhafız yeşil). 5 test kayıt eden koruma ikizi üzerinde: kapatma / başka alan düzenleme sormaz,
yeniden etkinleştirme reddedilir, hedef değişince sorar. Sabotaj: alt ajan (a,b kırmızı) + CT iki ayrı (1 + 2 kırmızı).
Tasks+WorkAggregation 1355/1355. Açık kalan BL-354: aktif kalan kuralın uygunsuz atananına üretim devam eder.

`01bc0915` ile `UpdateTaskRecurrenceRuleHandler` (`TaskRecurrenceRuleHandlers.cs:162`) her kayıtta
atama korumasını çağırıyor. Kuralın kişisi pozisyonunu kaybettiyse kural **pasife almak için bile**
kaydedilemiyor (400 `TASK_ASSIGNEE_NOT_ASSIGNABLE`); önce başkasına atamak gerekiyor.

**Karar (sahip):** koruma yalnız atama hedefi (tür/kişi/havuz) **değiştiğinde** sorulur; kuralı kapatmak her
zaman serbesttir. Atamayı değiştirmeden kaydeden, o atamadan sorumlu değildir (yeniden atama ile aynı ilke).
Açık kalan: BL-354 — aktif kalan ama atananı uygunsuz kural üretmeye devam eder.

**Ölçüm komutu:**

    grep -n "_assignmentGuard.CheckTargetAsync" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/TaskRecurrenceRuleHandlers.cs

### BL-353

**Şablon kaydında varsayılan havuz kapsamdan geçmiyor**

DURUM: KAPANDI — PSS dalı `22b2e198` (WP-PSS-MOD0024-TASK-SCOPE-SECURITY-01), CT doğruladı 2026-09-13

`TaskTemplateHandlers.cs:98,134` (oluştur) ve `:216,240` (güncelle) `DefaultPoolPositionId`'yi yalnız biçim
olarak doğruluyor (`TaskTemplateRules.ValidateAssignment`): pozisyon aktif mi, birimi canlı mı, kaydedenin
kapsamında mı sorulmuyor. Şablondan görev açma `01bc0915`'ten beri soruyor; yani kapsam dışı havuzlu şablon
kaydedilebilir ama ondan görev açılamaz — kullanıcıya "şablon bozuk" diye görünür. Şablonun kişi alanı yok.

**Ölçüm komutu:**

    grep -n "DefaultPoolPositionId\|_assignmentGuard" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/TaskTemplateHandlers.cs

### BL-354

**Zamanlanmış üretim atananın uygunluğunu yeniden sormuyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-10

`GenerateDueRecurringTasksHandler.cs:87-99` yalnız "atama hedefi söylenmiş mi" (`TaskAssignmentIntentRules`)
bakıyor; havuz için "pozisyon aktif mi" `CreateTaskItemHandler`'ın havuz dalında kalıyor; kişi için
pozisyon/birim, her ikisi için kapsam sorulmuyor (`IsScheduledGeneration` muafiyeti — bilinçli: çağıran yok).
Kural kaydedilirken kapsam soruldu; kişi sonradan ayrılırsa üretim ona görev açmaya devam eder. Kabul
edilebilir davranış: hedef uygunsuzsa dönemi yakmadan atla ve `task.recurrence.rule_unassigned` gibi logla;
kimin kapsamıyla sorulacağı (kuralı kaydeden mi?) karar ister. BL-352 ile birlikte ele alınmalı.

**Ölçüm komutu:**

    grep -n "IsScheduledGeneration\|TaskAssignmentIntentRules" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/GenerateDueRecurringTasksHandler.cs

### BL-355

**Görev oluşturmada istekle gelen `OrganizationUnitId` kapsamdan geçmiyor**

DURUM: KAPANDI — PSS dalı `22b2e198` (WP-PSS-MOD0024-TASK-SCOPE-SECURITY-01), CT doğruladı 2026-09-13

`CreateTaskItemHandler.cs:193` (havuz) ve `:200` (kişi) `request.OrganizationUnitId` verilmişse olduğu gibi
alıyor; birimin var/aktif olduğu ve çağıranın kapsamında olduğu sorulmuyor. Ekran birim göndermiyor
(pack §12 K6: "kullanıcı birim seçmez"), yani yalnız doğrudan API çağrısı; ama görev başka şirketin birimine
kaydedilebilir ve BL-057'nin şirket raporları yanlış şirkete yazar.

**Ölçüm komutu:**

    grep -n "request.OrganizationUnitId" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/CreateTaskItemHandler.cs

### BL-356

**Pozisyonsuz kullanıcı başkasına iş veremiyor — "sorumluluk alanı" kavramı yok**

DURUM: KARAR VERİLDİ (sahip, 2026-09-10) · SAHİP: sahip (veri) · ÖLÇÜLDÜ: 2026-09-10

`01bc0915` sonrası sunucu, seçicinin zaten uyguladığı kuralı uyguluyor: kapsam (`OrgDataScopeResolver.cs:9-24,
65-78`) yalnız **aktif pozisyon atamasından** türetiliyor; pozisyonu olmayan kullanıcı (yerel `admin@diten.com`
dahil) boş kapsamla başkasına kişi/havuz ataması, yeniden atama ve kural yazamıyor; kendine açabiliyor.

**Karar:** iş dağıtacak gerçek kullanıcılara pozisyon tanımlanır (SAP: iş dağıtan herkes org şemasında bir
pozisyondadır). `admin@diten.com` kurulum hesabıdır, iş dağıtmaz. **Ertelenen:** Oracle'daki "sorumluluk alanı"
gibi hatta olmayan birimlere (Kalite, İK) elle verilen kapsam — gerektiğinde `EntitlementDataScope` üzerinden;
`Allows`'ın (3) bacağı zaten "bana verilmiş birim/pozisyon" diye okuyor, yalnız kaynağı yok.

**Ölçüm komutu:**

    sed -n 9,24p services/Diten.Platform/src/Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs

### BL-357

**Devir bayrağı görevi açana da uygulanıyor — canlıdan gelen bug**

DURUM: ⚠️ KAPANDI (KISMİ) · SAHİP: CT · KOD: 2026-09-10 · COMMIT: `d35f8f32` · ✅ için: sahibin kontrol turu (canlı oturumla doğrulanmadı)

**Ne yapıldı** (WP-PSS-MOD0024-REASSIGN-REQUESTER-01, Antigravity; CT doğruladı). Sıra: önce "sen kimsin" (üçüncü kişi →
403 `TASK_REASSIGN_NOT_PERMITTED`, gerçek sebep), sonra bayrak yalnız `isHolder && !isRequester` için (409, mesaj aynı), sonra
`01bc0915` koruması değişmeden (Assign + uygunluk + kapsam talep sahibine de sorulur). Projeksiyon aynı kural: talep sahibinin
satırında reassign bayrak kapalıyken de ENABLED; yalnız holder gri + `DELEGATION_NOT_ALLOWED`. **Davranış değişikliği:** üçüncü
kişi + bayrak kapalı artık 409 değil 403 (eskiden yanlış sebep). `The_policy_answers_before_the_who_are_you_check` testi
yeniden yazıldı — "rakip" aktörü görevin AÇANIydı, yani test bu hatayı doğru davranış diye kilitliyordu. +5 test; ajanın
sabotajı 5 kırmızı, CT'nin iki ayrı sabotajı (handler / projeksiyon) 2+2 kırmızı; Tasks+WorkAggregation 1350/1350; yazma
koruması 97/97. Yeni metin yok. Eski /Tasks ekranında reassign düğmesi hiç yok (tarandı).

`ReassignTaskItemHandler` (`TaskItemTransitionHandlers.cs:1220`) `DelegationAllowed` kapalıysa aktör kim
olursa olsun 409 `TASK_DELEGATION_NOT_ALLOWED` döner; holder/requester ayrımı (`:1227`) ondan SONRA; kapsam
koruması (`:1256`) en sonda. Görev Merkezi de aynı bayrakla düğmeyi herkese kapatıyor
(`TaskWorkItemProvider.cs:1924-1928`). Bayrağın kodda tanımı "policy flag only" (`TaskItem.cs:232`) — kime
uygulanacağı yazılmamış. Sonuç: görevi açan (talep sahibi) kendi görevini başkasına veremiyor; kendine açtığı
görevi de.

**Karar (sahip):** bayrak **alan kişinin** ileri devrini sınırlar; **açan kişi** her zaman yeniden atar (Assign
yetkisi + `01bc0915` kapsam koruması yine sorulur). Kendi açtığı kendi görevi → bayrak gerekmez. SAP'de iletme
kısıtı alıcıya konur, işi başlatan yeniden atar; Oracle'da görev sahibi her zaman reassign yapabilir.

**Ölçüm komutu:**

    grep -n "DelegationAllowed" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/TaskItemTransitionHandlers.cs services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs

### BL-358

**Canlı bildirim: "kişi kendine görev açamıyor" — kanıt bekliyor**

DURUM: DEV'DE ÇÖZÜLDÜ (CT, 2026-09-11 gece; sahip onayı) · ÜRÜN SORUSU BL-366 AÇIK · daha önce: YENİDEN ÜRETİLDİ (CT, 2026-09-11, sahibin oturumuyla) · SAHİP: sahip (veri) + ürün kararı · KAYIT: 2026-09-10

**DEV ÇÖZÜMÜ (CT, 2026-09-11):** `DevSeeds:OrganizationPositions=true` yerelde açılıp Platform bir kez başlatıldı (ayar geri alındı, commit yok): DefaultTenant'a
1 birim (HEADQUARTERS) + 5 pozisyon (CEO, CTO, HR_MGR, DEV_LEAD, DEV_ENG) yazıldı — tohum pozisyonları **Draft** yazıyor ve atama yazmıyor; CT beş pozisyonu Positions
API'siyle Active yaptı ve admin@diten.com'a CEO ataması ekledi (Pozisyon Atamaları API'si, sahibin oturumu). Sonuç: toplantı aday listesi "Diten Admin · CEO · Headquarters".
**Canlı kanıt (E4, aynı oturum):** toplantı oluştur 201 → toplantıdan görev 201 (RecordLink `preparation`) → görevden inceleme toplantısı 201 (RecordLink `reviewMeeting`) →
bağlı görevler listesinde görünüyor — MOD-0357 S3/S4 canlı doğrulandı. Sahip kontrol turunda bu veriyi silip baştan kurabilir; tohum tekrarlanabilir.
Tohum notu: `PositionSeed` pozisyonları Draft yazdığı ve `PositionAssignmentSeed` DefaultTenant'a atama yazmadığı için tek başına yetmiyor (BL-366'ya ek: onboarding kök birim + 
ilk yöneticinin pozisyonu).

**EK (CT, 2026-09-11):** Aynı veri eksiği DCP-005 Adım 2'nin (BL-369) canlı kanıtını da engelliyor — `CreateTaskItemHandler` organizasyon
birimini alıntı dondurmadan ÖNCE çözer (`:208-219` vs `:331`), dev kiracısında birim olmadığı için görev hiç açılamıyor. Kontrol turu için karar
(elle MOD-0288 ekranları vs tek seferlik `DevSeeds:OrganizationPositions=true`) bu yüzden iki işi birden açıyor. **Üçüncü etki (CT, S8 kabulü, 2026-09-11):** toplantı oluşturma da dev'de `MEETING_ORGANIZER_INVALID` ile düşüyor — organizatör uygunluğu
görev kişi aramasıdır (aktif pozisyon), pozisyon yok → uygun kimse yok; MOD-0357 K8 kutu 1'in canlı kanıtı da buna bağlı. **Dördüncü (S4, 2026-09-11):** toplantıdan görev açma ve görevden inceleme toplantısı planlama da aynı engele takılıyor (görev/toplantı oluşturma freezer/bridge'e gelmeden düşüyor); S4 canlı turu BL-358 sonrası.

**Yeniden üretim:** Görev Merkezi → + Yeni → Hızlı görev → Kime: Kendim → Oluştur → `POST /Tasks/api` 400, ekranda
"Bu görev için organizasyon birimi belirlenemedi. Yöneticinizden size bir pozisyon atamasını veya bir kök organizasyon birimi
tanımlamasını isteyin." (`ORGANIZATION_UNIT_UNRESOLVED`). Sebep veri: dev kiracısında `organization_units` 0, `positions` 0,
`position_assignments` 0 (2026-09-10 tohum temizliğinden sonra) → `CreateTaskItemHandler` basamaklı geri düşüşü (istek → pozisyonun birimi
→ kök birim) hiçbir yerde birim bulamıyor. Kod kural gereği davranıyor (pack §12 K6: her görevin birimi olur, uydurulmaz). Canlıda aynı
mesaj görülüyorsa o kiracıda ya kullanıcının pozisyonu yok ya da aktif kök birim yok — kontrol turunda bakılacak.

**Ürün sorusu (BL-366):** organizasyon yapısı kurulmamış bir kiracıda görev açmak tümden kilitli mi kalmalı? Öneri: kiracı
kurulumu (onboarding) bir varsayılan kök birim ("Şirket") oluştursun; böylece pozisyonsuz kullanıcı kendine görev açabilir, başkasına
atama yine pozisyon ister (BL-356).

Testlerde kendine açma 201 (`TaskAssignmentWriteGuardHttpTests.A_task_for_MYSELF_is_201_without_assign`);
canlıdaki kod `01bc0915`'i içermiyor. Koddaki tek aday: pozisyonsuz kullanıcının görevi kök birime düşer
(`CreateTaskItemHandler.cs:587-600`, HQ öncelikli tek kök); aktif kök yoksa `ORGANIZATION_UNIT_UNRESOLVED`.
Tahmin değil ölçüm için ekran görüntüsü + kullanıcı + ortam gerekiyor.

### BL-359

**PPM `assign-owner` izni otomatik grant yollarından dışlanmıyor — altyapı (AuthService) işi**

DURUM: KAPALI (CT, 2026-09-11, `0bf3b283`) · KARAR VERİLDİ (sahip, 2026-09-11 — doğrudan onay): assign-owner hiçbir otomatik yolla verilmez — SuperAdmin tam katalog, kiracı Admin
modül eşitlemesi, başlangıç rol şablonu dahil; yalnız açık ve yetkili atama · SAHİP: CT (altyapı) · TALEP: Codex / PPM, 2026-09-10

**KAPANIŞ (CT, 2026-09-11, commit `0bf3b283`, dal `feature/mg/mod-0357-management-review-cadence`):** WP-INFRA-PPM-ASSIGN-OWNER-01.
Tek liste `Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs` (`ppm.portfolios.assign-owner`); dört otomatik yol onu okur:
`FullCatalogPermissionGrantService` (imza `permissionKey` aldı), `DefaultRolePermissionTemplate.SelectFor` (SuperAdmin dahil; DataSeeder +
RoleProvisioningService aynı noktadan geçer), `EntitlementPermissionSyncService.GrantPermissionsToRolesAsync` süzgeci, `InternalPermissionsController`
create/reactivate. Resolver dokunulmadı (anahtar = açık rol grant'i ∩ onaylı PPM entitlement). Platform manifesti: PORTFOLIOS sayfasına `ASSIGN_OWNER`
RowAction (25 anahtar). Kanıt: liste boşaltılınca 5 test kırmızı (dört yol); Auth 667/670 (3 kırmızı önceden: CRM knowledge baseline-CSV, user-lookup DTO);
paylaşımlı yerel Mongo'da katalog 429, anahtarı tutan rol 0, SuperAdmin bu anahtar dışında her aktif anahtarı tutuyor, kiracı Admin 77 değişmedi.
Yayım girdisi FU01 SHA `4a10cd92` (`git show`). Açık: Codex'in "named-human / assignable target" ve SOP-0029 kayıt erişimi (PPM tarafı, FU01 kapsamı dışı).

**Yetki girdisi kuralı (CT, 2026-09-11):** PPM'nin push edilmiş amendment SHA'sı (`git show <sha>:<pack yolu>`) dar Auth işinin
(dışlama mekanizması + BL-360) girdisi olarak kabul edilir; anahtarın kataloğa/manifeste YAYIMI ise üst paketin
approved/ready-for-dev olmasını bekler (izin yüzeyi PPM kapsamı).

Anahtar `ppm.portfolios.assign-owner` henüz hiçbir dalda/pakette yok (`git log --all -S'assign-owner'` boş;
MOD-0117/DCP-006 kapsamı genişletilmeli). Eklendiğinde: `FullCatalogPermissionGrantService` ve
`DefaultRolePermissionTemplate` PPM politikasına hiç bakmıyor (`_ppmPolicy.Applies` yalnız
`TenantEffectivePermissionResolver.cs:51` ve `EntitlementPermissionSyncService.cs:55,75,185`'te) → SuperAdmin
tam katalogla, kiracı Admin modül eşitlemesiyle anahtarı otomatik alır; onaylı karar "portföy sahibi ataması
yalnız açık grant". Genel dışlama mekanizması yok. Sıra: PPM yönetişim genişletmesi (anahtar) → Auth dışlama +
BL-360 aynı WP'de. Auth, PPM için korumalı yol; iş CT'nin altyapı kulvarında.

**Ölçüm komutu:**

    grep -rn "_ppmPolicy.Applies\|IPpmEntitlementPermissionPolicy" services/Diten.AuthService/src --include=*.cs

### BL-360

**PPM toplu-grant kapısında harf tutarsızlığı: `Applies` "PPM" (Ordinal) sorulur, kod sonra küçültülür**

DURUM: KAPALI (CT, 2026-09-11, `0bf3b283`) · SAHİP: CT (altyapı) · ÖLÇÜLDÜ: 2026-09-10

**KAPANIŞ (CT, 2026-09-11, commit `0bf3b283`):** Önerilen düzeltmenin ilk yarısı uygulandı, ikinci yarısı bilinçli olarak DEĞİL: `Applies`
(Ordinal "PPM") resolver'a bakan yüzey olduğu için harf duyarsız yapılmadı; yerine dar `IsPpmModuleCodeAnyCase` eklendi ve yalnız
`EntitlementPermissionSyncService`'in üç kapısında (`GrantModuleAsync`, `GrantModuleWithKeysAsync`, `RevokeModuleAsync`) kullanıldı. Resolver süzme
matrisi testle birebir. CT bulgusu: ajanın grant-tarafı "PPM/ppm/Ppm" teorileri boş kanıttı (paylaşılan test kataloğunda PPM izni yoktu; iki kapı
Ordinal'e döndürülünce 38 test yeşil kaldı) → katalog PPM izinleriyle genişletildi, aynı sabotaj 4 vaka kırmızı.
**Ek kapanış (CT, 2026-09-11, Codex'in 0bf3b283 okuması):** kapı ham kodla soruluyordu, `IsPpmModuleCodeAnyCase` yalnız harf duyarsızdı; `" PPM "` gibi boşluklu
kod kapıyı geçip `NormalizeModuleCode` sonrası `ppm` olarak işlenebilirdi. Düzeltme: üç kapı (grant, grant-with-keys, revoke) önce normalize eder, sonra sorar;
tanıyıcı ayrıca Trim yapar. Teoriler `" PPM "`, `" ppm "`, `"\tPpm\n"` ile genişletildi (gerçek PPM kataloğu); ham-kod kapısına dönülünce 9 vaka kırmızı.

`PpmEntitlementPermissionPolicy.Applies` (`:16-17`) `StringComparison.Ordinal` ile `"PPM"` arar.
`EntitlementPermissionSyncService` (`:55,:75,:185`) kapıyı `NormalizeModuleCode` (trim + lowercase,
`ModulePermissionResolver.cs:50-51`) çağrısından ÖNCE soruyor; Auth DB'de modül `"ppm"`. `"ppm"`/`"Ppm"` gelen
kod kapıyı geçer → PPM izinleri kiracı Admin'e toplu grant edilir; `"PPM"` gelen reddedilir. Düzeltme: kapıyı
normalize edilmiş kodla sor **ve** `Applies`'ı harf duyarsız yap; resolver (`TenantEffectivePermissionResolver.cs:51`)
aynı `Applies`'ı paylaştığından süzme sonucunun değişmediği testle gösterilir (sessiz genişleme yok).

**Ölçüm komutu:**

    grep -n "_ppmPolicy.Applies\|NormalizeModuleCode" services/Diten.AuthService/src/Diten.AuthService.Application/Common/Services/EntitlementPermissionSyncService.cs

### BL-361

**Görev üzerindeki yazma yolları ilişki sormuyor: yetkisi olan herkes, kimliğini bildiği her görevi başlatıp tamamlayabiliyor**

DURUM: ⚠️ İLK YARI KAPANDI (KISMİ) · COMMIT: `e904bdfe` (2026-09-11) · İKİNCİ YARI AÇIK (güncelle/sil/toplu sil/bağımlılık/kontrol listesi + okuma BL-349) · ✅ için: sahibin kontrol turu

**İlk yarı — ne yapıldı** (WP-PSS-MOD0024-LIFECYCLE-AUTHORITY-01, Antigravity; CT doğruladı). Handler'lar: start/resume/complete/
submitReview yalnız holder, plan holder veya talep sahibi; üçüncü kişi 403 (`PERM_DENIED`, ön yüz `errorNoAccess`), CanTransition'dan
sonra, kapılardan ve yazmadan önce; reddedilende geçiş kaydı ve bildirim yok. Projeksiyon: holder olmayana holder fiilleri sunulmaz;
talep sahibi outbox'ta plan görür; Ekibim'de yönetici yalnız talep sahibiyse cancel/reassign. 15 yeni test (11 HTTP gerçek yönlendirme
+ [HasPermission], 3 Ekibim, 1 kendine açılan); 2 outbox testi karara göre güncellendi. Sabotaj: ajan 8 hunk → 8 kırmızı; CT handler /
projeksiyon ayrı → 2+2 kırmızı. Tasks+WorkAggregation 1370/1370. Kullanıcı dışı çağıran yok (tarandı). Alt görev özet listesi
(`WorkItemSubtaskDto`) aksiyon taşımıyor; alt görevin kendi satırı aynı kuraldan geçiyor.

**Karar ve bölme (2026-09-11):** başlat / sürdür / tamamla / incelemeye gönder = yalnız holder; planla = holder veya talep
sahibi; kabul / sor / bırak projeksiyonda holder olmayana sunulmaz (BL-362) — hepsi **WP-PSS-MOD0024-LIFECYCLE-AUTHORITY-01**.
Güncelle / sil / toplu sil / bağımlılık / kontrol listesi işaretleme **bu WP'de değil**: "talep sahibi + ayrı yönetim yetkisi"
yeni izin anahtarı (manifest, AuthService eşitlemesi, 7 dilde çip etiketi → l10n kapısı) ister; ayrı karar ve paket. Okuma
tarafı (BL-349) da o pakette.

`TransitionTaskItemHandler` (`TaskItemTransitionHandlers.cs:201-707`) tek aktör kontrolünü iptal için yapıyor (talep sahibi,
`:296-303`); **başlat** (`platform.tasks.update`), **sürdür**, **tamamla** (`platform.tasks.complete`) holder/talep sahibi sormuyor.
`SubmitTaskForReviewHandler` (`:708-834`) ve `PlanTaskItemHandler` (`:835-921`) hiç aktör sormuyor. Update/Delete/BulkDelete
(`TaskItemWriteHandlers.cs`), bağımlılık ekle/çıkar (`TaskDependencyHandlers.cs`), kontrol listesi işaretle/sırala: aynı —
yalnız yetki + kiracı filtresi. Görünür kılan: **Ekibim** kapsamı (BL-023; `WorkItemsController.cs:77-88`,
`TaskWorkItemProvider.cs:217-240`) astların görevlerini listeliyor ve projeksiyon başlat/tamamla düğmelerini `isHolder`'a bakmadan
etkin çiziyor (`TaskWorkItemProvider.cs:1741-1752, 1781-1826`) → yönetici astının görevini tek tıkla başlatır/tamamlar; geçiş
kaydı yöneticinin adıyla yazılır, kapanış kaydını işi yapan yazmamış olur (kapanış paketinin "yazarlık tersine dönmesin"
ilkesine aykırı). Task-User rolünde Güncelle çipi var → her görev kullanıcısı için geçerli. Kod düzeyinde kesin; kimin hangi
yetkiyi taşıdığı canlıda ölçülmedi.

**Karar:** görev erişim modeli — (a) yalnız yetki + kiracı (bugün) · (b) ilişki şart. **CT önerisi:** başlat / sürdür /
tamamla / incelemeye gönder = yalnız holder · planla = holder veya talep sahibi · güncelle / sil = talep sahibi (yönetim yetkisi
ayrı, açık) · "X adına" işlem ileride açık ve damgalı bir özellik (rebuild spec'teki "X adına" damgaları). SAP: iş kalemini yalnız
olası ajanı yürütür, yönetici vekâlet/yönlendirme ile; Oracle: sahip/yönetici "on behalf" yalnız açık yetkiyle. Okuma tarafı
BL-349 ve listeleme BL-057 ile aynı karardan çıkmalı; BL-362 aynı WP'de. Tablo:
`docs/records/audits/2026-09/task-action-rules-matrix-2026-09-11.md`.

**Ölçüm komutu:**

    awk 'NR>=201 && NR<=921' services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Handlers/CommandHandlers/TaskItemTransitionHandlers.cs | grep -n "AssigneeUserId\|CreatedByUserId\|403"
    grep -n "Build(\"start\"\|Build(\"complete\"\|var isHolder" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs

### BL-362

**Projeksiyon holder'a bakmıyor: kabul et / sor / bırak düğmeleri holder olmayana etkin çiziliyor, sunucu 403 diyor**

DURUM: ⚠️ KAPANDI (KISMİ) · COMMIT: `e904bdfe` (2026-09-11, BL-361 ile aynı WP) · ✅ için: sahibin kontrol turu

**Ne yapıldı.** accept (`:1721-1739` dalı), inquire, release yalnız `isHolder` ise sunuluyor; holder olmayana gizli (gri değil). Ekibim testleri kırmızı→yeşil (CT sabotajı 2 kırmızı).

Handler'lar doğru: Accept (`TaskItemTransitionHandlers.cs:28-31`), Release (`:133-137`), Inquire (`:966-971`) holder değilse 403.
Projeksiyon `isHolder`'ı hesaplıyor (`TaskWorkItemProvider.cs:1648`) ama accept (`:1712`), inquire (`:1847`), release (`:1877`)
yapılarında kullanmıyor → Ekibim'de yönetici tıklar, 403 alır ("düğme açık, sunucu reddediyor"; K5 üretici ≠ tüketici).
Düzeltme: holder olmayana bu üçü hiç sunulmaz (gizle; gri değil — "senin değil" için sebep metni gerekmez, yeni resx yok).
BL-361 kararından bağımsız; aynı fonksiyon değiştiği için aynı WP'de.

**Ölçüm komutu:**

    grep -n "Build(\"accept\"\|Build(\"inquire\"\|Build(\"release\"\|var isHolder" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs

### BL-363

**Görev motorunda üç küçük tutarsızlık (alt ajan tablosu; CT doğrulamadı)**

DURUM: AÇIK · SAHİP: SAHİPSİZ · KAYIT: 2026-09-11

- Bağımlılık ekleme: döngü/çift kontrolü oku-sonra-yaz, kilit/versiyon yok (`TaskDependencyHandlers.cs:65-140`) — dar yarış:
  iki eşzamanlı ekleme tek tek geçip birlikte döngü kurabilir.
- `ChecklistWriteGuards` yorumu "ekle fiilinde kapalı görev kontrolü yok (BL-093)" diyor; `AddChecklistItemHandler` kontrolü
  yapıyor (`ChecklistHandlers.cs:126-139` vs `:391-393`) — bayat yorum.
- Yorum güncelle/geri çek kapalı görevde `Lifecycle` sormuyor, ekleme soruyor (`TaskCommentHandlers.cs:78-83` vs `:170-243`) —
  kasıtlıysa belgelenmeli.

Tablo: `docs/records/audits/2026-09/task-action-rules-matrix-2026-09-11.md`.

### BL-364

**Görev Merkezi: iki kart sözleşmede olmayan yeteneklere bakıyor (`compliance`, `approvalChain`)**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-11 (alt ajan, `ee10229c` turunda; CT doğrulamadı)

`WorkCenterNext/app.js` `renderCompliance` (`hasCap(item, 'compliance')`, ~:4409, canlı bağlı ~:5025) ve `renderApprovalChain`
(`hasCap(item, 'approvalChain')`, ~:4392, ~:5125): iki yetenek adı da `fixture-contract.js` CAPABILITIES listesinde yok; hiçbir
fixture ya da sağlayıcı üretmiyor. `related`/`relatedRecords` yakın-kaçığından farklı olarak sıfır eşleşme → ölü ya da sözleşme
öncesi kod. Karar: sözleşmeye eklenir mi (kim üretir?) yoksa kart silinir mi.

**Ölçüm komutu:**

    grep -n "hasCap(item, 'compliance')\|hasCap(item, 'approvalChain')" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js
    grep -n "'compliance'\|'approvalChain'" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js

### BL-366

**Kiracı kurulumu kök organizasyon birimi oluşturmuyor — yapısız kiracıda görev ve toplantı açılamaz**

DURUM: AÇIK · SAHİP: SAHİPSİZ · ÖLÇÜLDÜ: 2026-09-11

Görev oluşturma her göreve bir birim ister (`CreateTaskItemHandler` basamağı: istek → atananın pozisyon birimi → aktif kök birim →
`ORGANIZATION_UNIT_UNRESOLVED`). Toplantı düzenleyeni ve katılımcıları aktif pozisyon ister (MOD-0357 D2). Kiracı oluşturma (Platform
onboarding, bkz. `project_platform_tenant_onboarding_gaps`) hiçbir organizasyon kaydı yaratmıyor → yeni kiracıda yönetici bile kendine görev
açamaz (BL-358 dev yeniden üretimi: 0 birim / 0 pozisyon). Öneri: onboarding tek bir aktif kök birim (tüzel kişiliğe bağlı, kod `ROOT`/
ad kiracı adı) yaratır; pozisyon ve atamalar organizasyon ekranlarından (MOD-0288). SAP'ta şirket kodu ve kök org birimi kurulumun parçasıdır.

**Ölçüldü (2026-09-14, WP-INFRA-BL384 Kısım 2 — ajan uydurmadan durdu, CT doğruladı):** kök birim kurulumda OLUŞTURULAMAZ:
`OrganizationUnit.LegalEntityId` zorunlu (`OrganizationUnit.cs:9`, validator boş kabul etmiyor), birim oluşturma MDM'den tüzel kişiliğin
başvurulabilir olduğunu doğruluyor (`CreateOrganizationUnitCommandHandler.cs:45-49`), `RegisterTenantCommand` tüzel kişilik taşımıyor ve MDM'de
kiracı oluşturma olayını dinleyen yok. Görev oluşturmanın kök birim araması tüzel kişiliği kontrol etmediği için uydurma bir kimlikle ham kayıt
"çalışırdı" — yasak olan tam da bu. **Sahip kararı gereken seçenekler:** (1) kiracının ilk tüzel kişiliği MDM'de aktifleşince kök birim oluşsun
(yeni MDM olayı + Platform tüketicisi) · (2) kiracı yöneticisine ilk kurulum adımı: tüzel kişilik oluştur + aktifleştir, sonra var olan birim
oluşturma (yeni arka uç yok) · (3) tüzel kişiliksiz birime izin (MOD-0288 sözleşmesi değişir) · (4) kurulum MDM'de tüzel kişiliği servisler
arası oluştursun (yeni iç uç + yeni zorunlu kurulum alanı). CT önerisi: (2) hemen (belge/kılavuz), (1) sonra.
**Sahip kararı (2026-09-14):** şimdi (2) — kontrol listesi §6'da; sonra (1) — MDM olayı + Platform tüketicisi, ayrı prompt (SAP/Oracle/Workday: önce tüzel kişilik, sonra birim).

**Ölçüm komutu:**

    mongosh "mongodb://localhost:27017/diten_personalization_dev" --quiet --eval 'print(db.organization_units.countDocuments({}), db.positions.countDocuments({}), db.position_assignments.countDocuments({}))'

### BL-367

**Görev Merkezi'nin hata/onay pencereleri hâlâ ham SweetAlert; ürünün tek diyalog bileşeni kullanılmıyor**

DURUM: AÇIK · SAHİP: CT (WorkCenter) · ÖLÇÜLDÜ: 2026-09-11 (sahip canlıda gördü: hızlı görev hatası "organizasyon birimi belirlenemedi" modalı)

**EK BULGU (CT, 2026-09-11):** `wcn-dialog-one-language` testi ("declares the package once") main'den beri kırmızı: diyalog görünüm paketi parmak izi
(`popup: 'rounded-4 shadow-lg'`) `frontend/Diten.Web/wwwroot/assets/js/PPM/Initiatives/index.js` içinde de var (Codex, `7f37e172`) — PPM kulvarında ikinci bir
diyalog görünümü kopyası. Kapanış PPM'de: `DitenDialog`/`window.showConfirm` kullanımı; CT bilgi verdi.

**Düzeltme (CT, 2026-09-11, envanterden):** sahibin gördüğü pencere ham Swal değil, `wwwroot/assets/js/shared/premium-modal.js` (`DitenModal.error`,
`quick-create.js:116`) — `_GlobalConfirmation.cshtml`'in `DitenDialogAppearance` paketi yanında **ikinci** bir paylaşımlı görünüm tanımı; muhafızın
"paket bir kez tanımlanır (5 bekleniyor, 6 bulundu)" kırmızısına adaydır. Karar (sahip): kanonik görünüm `_GlobalConfirmation`'ınki ise `DitenModal`
ona devreder ya da kaldırılır; Meetings/form.js ve Tasks/form-page.js de bunu kullanıyor.

Standart: `.antigravity/rules/premium-modal-standard.md` (MOD-0013) — varsayılan/özelleştirilmemiş SweetAlert2 yasak; onaylar `window.showConfirm`
(`backbone-shell.js:76`, `_GlobalConfirmation.cshtml`) üzerinden; muhafızlar `tests/dialog-one-implementation.test.js` ("one confirm implementation,
product-wide") ve `tests/wcn-dialog-one-language.test.js`. Ölçüm: `WorkCenterNext/app.js` hâlâ doğrudan `global.Swal.fire(` çağırıyor
(`:8489`, `:8580`; dosyanın kendi yorumu `:7677` "on beş çağrı vardı" diye başlıyor — ikiye inmiş, sıfır değil) ve toplam 18 `Swal`
referansı taşıyor. Sahip 2026-09-11: "bu modal yanlış, biz modallarda değişiklik yaptık" — hızlı görev hata penceresi standart dışı.
İki muhafız main'de zaten kırmızı ama sebebi başka ekip: `PPM/Initiatives/index.js` (paylaşılan bileşen dışı diyalog) + görünüm paketinin
6. tanımı — BL-343 ailesi.

**Yapılacak (paylaşımlı parça girişimi BL-365 ile birlikte):** WCN'deki kalan ham `Swal.fire` çağrıları ve hata/onay/gerekçe diyalogları tek
bileşene taşınır; bileşen gerekçe (textarea) ve "hata + Tamam" biçimlerini destekliyorsa kullanılır, desteklemiyorsa önce bileşen genişletilir
(metin → l10n kapısı). Toplantı S3'ün kendi Bootstrap iptal modalı (`#cancelMeetingModal`) da aynı bileşene geçer.

**Ölçüm komutu:**

    grep -n "Swal\.fire(" frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js
    npx --prefix frontend/Diten.Web vitest run tests/dialog-one-implementation.test.js tests/wcn-dialog-one-language.test.js

### BL-365

**Görev Merkezi'nin ön yüz parçaları paylaşımlı bileşen olarak çıkarılır; toplantı ekranları yeniden yazmaz**

DURUM: AÇIK · SAHİP: CT (WorkCenter) · KARAR: sahip, 2026-09-11 ("liste, kart gibi şeyleri tekrar tekrar yapmayalım") · ENVANTER: `docs/records/audits/2026-09/workcenter-reusable-ui-inventory-2026-09-11.md`

**İLERLEME (CT, 2026-09-11, `6bab238c`):** Görev Merkezi'nin `bindDialogSelect2` / `dialogLook` kopyaları kapandı — app.js tek satır delegasyon,
dört test (`wcn-dialog-seven-defects`, `wcn-dialog-rhythm`, `wcn-dialog-one-language`, `wcn-detail-three-regions`) shared dosyayı okuyor, muhafızın istisna listesi
yalnız `shared/diten-dialog.js`; ikinci gövde eklenince muhafız kırmızı (CT sabotajı). `.wcn-dialog-select` CSS kaldı (delegasyon sınıf adını option olarak taşıyor).
Kalan: #4 aylık takvim → ortak takvim bileşeni (S3b, sahip: önce tasarım konuşulacak).
**#4 KAPANIR (WP-UI-CALENDAR-VIEW-01, 2026-09-29, CT kabulü bekliyor):** eski `renderCalendar` kaldırıldı; ortak bileşen
`shared/diten-calendar.js` (+ `shared/diten-zoned-time.js`, `Views/Shared/_CalendarAssets.cshtml`, yerel FullCalendar 6.1.15) Görev
Merkezi'nin bütün sekmelerinde çiziyor (İşlerim planlama, diğerleri salt okunur). Toplantılar sayfası aynı bileşeni 2c'de kullanır.
**2c (WP-UI-MEETINGS-CALENDAR-01, 2026-09-29, CT kabulü bekliyor):** Toplantılar sayfası aynı bileşeni kullanıyor (salt okunur); davet kartı
tek modül `shared/diten-invite-card.js`'e çıkarıldı, Görev Merkezi Davetler paneli ve Toplantılar davet paneli onu çağırıyor (muhafız:
`meetings-calendar-view.test.js` "the invitation card is drawn in ONE module").

**Hemen değiştir (çıkarma gerekmez):** Meetings iptal modalı → `showConfirm` (textarea, zorunlu) · Meetings tarih-saat → `DitenDateField.enhance({enableTime:true})`.
**Önce çıkar, sonra kullan (öncelik sırası):** 1 diyalog görünüm adaptörü (`app.js:7727-7787`) → S5/S6 gerekçe diyalogları + Meetings düzenleyen-değiştir ·
2 kişi/avatar seçici (`Tasks/form.js:487-813`) → Meetings katılımcı seçici (üçüncü kopya) · 3 ilişkili kayıt satırı (`app.js:4402-4407, 4480-4561`) → S4 ·
4 aylık takvim (`app.js:5918-5999`) → S3b · 5 üç bölgeli detay kabuğu (`app.js:4563-5180`) → S4–S6 detay.
**Kalsın:** hata kodu köprüsü (modüle özgü kodlar; iskelet aynalanmış).

**İlerleme (2026-09-11, CT doğruladı):** E1 diyalog adaptörü, E2 kişi seçici + select2 adaptörü, E3 ilişkili kayıt satırı `shared/` altında; Meetings iptal/düzenleyen
modalları `showConfirm`, tarih-saat `DitenDateField`, bağlı görevler paylaşımlı satır. **Kalan:** `bindDialogSelect2`/`dialogLook` WCN kopyası + `.wcn-dialog-select`
CSS'i (üç diyalog testi kaynak metnini pinliyor) → sonraki ön yüz dilimi (S8) testleri paylaşımlıya çevirip kopyayı siler · takvim (S3b) · detay kabuğu (S4+).
Muhafız `shared-ui-parts-one-implementation.test.js` — CT düzeltmesi: her tanım sınıflanır (ilki değil).
**Kural:** her ekran/dilim prompt'u "mevcut parçayı kullan; yalnız app.js içinde gömülüyse önce çıkar" satırı taşır ([[feedback_reuse_frontend_partials]]).
Metin/CSS taşıyan çıkarmalar l10n/FG-003 kapısından geçer (Antigravity); saf JS çıkarmalar CT alt ajanıyla yapılabilir.

### BL-368

**Web yeniden başladıktan / oturum tazelendikten sonra ilk API çağrıları 401, tekrarı 200 — DataTable konsola hata yazıyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · GÖZLEM: 2026-09-11 (CT, sahibin oturumu, /Meetings)

Ağ kaydı: `GET /Meetings/api/lookups/types` 401 · `/lookups/attendees` 401 · `/api/list?pageSize=1000` 401 → hemen ardından aynı üçü 200.
Sayfa toparlıyor (oturum yenileme sonrası tekrar), kullanıcı fark etmiyor; ama `[DtDefaults] Ajax error {status: 401 …}` konsola hata düşüyor
ve ilk yanıt gelmeden ikinci istek `&&_=` (çift ampersand) ile üretiliyor. Sorular: yenileme İSTEKTEN ÖNCE yapılamaz mı (token süresi biliniyor)?
DtDefaults 401'i hata olarak mı loglamalı, sessiz tekrar mı? Görev Merkezi ve Görevler sayfalarında aynı desen var mı? İlgili: geçmiş
`invalid_token` / yenileme kaskadı kaydı (çözüldü, main).

**Ölçüm komutu:** tarayıcı ağ sekmesi, Web yeniden başlatıldıktan sonra ilk `/…/api/*` çağrıları; `grep -n "401" frontend/Diten.Web/wwwroot/assets/js/shared/dt-defaults*.js`

### BL-369

**"Kontrollü Dokümanlar" sayfası (`/Tasks/DocumentList`) ve CSV kütük araması emekli edilecek — taşıma canlıda çalıştıktan SONRA**

DURUM: AÇIK · KARAR: sahip + DM geliştiricisi, 2026-09-10 (G1 = (a), WP-0029-EFFECTIVENESS-P2.md:150) · SAHİP: taşımayı yapan (DM geliştiricisi) · KAYIT: 2026-09-11

**KAPANDI (CT, 2026-09-12):** WP-DM-DCP005-RETIRE-CSV-01, dal `feature/dm/dcp-005-retire-csv-list`, commit `60189308` (sahip push edecek, kendi PR'ı).
Kaldırılan: `/Tasks/DocumentList` görünümü + istemcisi + 7 dil anahtarları (38), Web proxy aksiyonları, Platform'daki 5 uç (`document-list/*`), manifest sayfası, ekranın test dosyası.
Kalan (bilerek): CSV koleksiyonları ve `DocumentReferenceListParser` (kütük içe aktarma onu kullanıyor; eski dondurulmuş alıntılar `ListVersionId` ile o satırlara bakıyor).
Ayrıca içe aktarma ekranının iki küçük kusuru düzeltildi: değişmeyen satır artık "güncellendi" sayılmıyor (önizleme = onay, dördüncü sütun) ve ikinci onay 409'unda ekran kırmızıya düşmüyor, yerelleştirilmiş uyarı gösteriyor.
**İki takip (CT):** (1) kaldırılan uçların arkasındaki handler/repository sınıfları artık erişilemez — ayrı bir temizlik turunda silinecek; (2) `platform.tasks.document-list.read` / `.import` anahtarları manifest evsiz kaldı, muhafız bunu "bilinen yetim" olarak kabul ediyor — Auth kataloğundan temizlik Faz 1.5'te CT'nin işi.

**İLERLEME (CT, 2026-09-11):** Devir planı **Adım 2 teslim edildi** — WP-PSS-DCP005-STEP2-CITATION-REPOINT-01, dal `feature/pss/dcp-005-citation-repoint`
(main `11befc07` üzerinden, commit `ac2a8d54`, worktree `.claude/worktrees/pss+dcp-005-citation-repoint`, sahip push edecek, kendi PR'ı).
Görev formu seçicisi `GET api/v1/tasks/lookups/document-citations` → `IControlledDocumentCitationPort`; freezer yeni alıntıları kütükten dondurur
(`ListVersionId` nullable, eski CSV alıntıları olduğu gibi); görev türü yöneten-doküman okuması porta; CSV sayfası ve içe aktarma KALDI (bu maddenin
emekliliği hâlâ canlı sonrası). **CT düzeltmesi:** WP'nin AC4'ü "engelli doküman durumu donar, görev açılır" demişti — ana daldaki kural geri alındı:
engelli (Superseded/Retired/Draft…) doküman alıntı anında `DOCUMENT_REFERENCE_BLOCKED` ile reddedilir, ölçüt kütüğün kendi `Citable` yargısı
(Effective ∨ UnderRevision); ekran ile API tutarlı, Adım 3 (`/active`, Kural 4/G3) ayrı. Sabotaj: `Citable` kontrolü kaldırılınca 1 test kırmızı.

**⚠ CANLIYA ÇIKIŞ KAPISI (CT canlı ölçümü, dev, 2026-09-11):** uç zinciri çalışıyor (Web proxy → gateway → Platform → port, 200) ama
`document_management_master_register` 358 satırın **tümü kiracı `97c59330-dbc4-4665-b29c-0c26dbb5cc93`'te**, oturumun kiracısı DefaultTenant
(`…0001`) → seçici boş; ayrıca yaşam döngüsü dağılımı **Draft 350 · InReview 1 · Retired 7 → alıntılanabilir 0/358**. CSV listesi aynı belgeleri
`linkableInErp=true` sayıyordu. Sonuç: Adım 2 birleşse bile **kütük gerçek yaşam döngüsü durumlarıyla ve doğru kiracıda tohumlanmadan
(Adım 0 register tohumu — WP-0029:150 "bizim sıradaki WP", DM geliştiricisi) hiçbir doküman alıntılanamaz.** Sıra: Adım 0 → Adım 2 merge → canlı
doğrulama → bu maddenin emekliliği. Dondurulmuş alıntı kanıtı yerelde alınamadı: `task_items` 0 kayıt ve BL-358 (org birimi yok) freezer'dan önce
kesiyor.

**ADIM 0 TESLİM + KAPI AÇILDI (CT, 2026-09-11):** WP-DM-DCP005-STEP0-REGISTER-SEED-01, dal `feature/dm/dcp-005-step0-register-seed` (main `11befc07`
üzerinden, commit `1e07712b`, worktree `.claude/worktrees/dm+dcp-005-step0-register-seed`, sahip push edecek, kendi PR'ı). (A) Dev tohumu DefaultTenant'a
(izlenen `appsettings.Development.json` TenantId — sır değil; merge sonrası her geliştiricinin dev Platform'u ilk açılışta 358 satırı DefaultTenant'a tohumlar,
marker kiracı başına). (B) Sahip kararı Seçenek 1: `DocumentMasterRegisterEntry.CitableByQualityDecision` = CSV `linkable_in_erp` (tohum + runtime ingest aynı
eşlemeden); alıntı yargısı `IsOperationallyEffective ∨ (karar ∧ Draft/InReview/ApprovedPendingEffective)`; Retired/Void/Suspended/Superseded/ObsoleteCopy asla;
yürürlük kapısı (`IsOperationallyEffective`, effectiveness handler) DOKUNULMADI (35 test yeşil; alıntı formülü sabote edilince yalnız 4 alıntı testi kırmızı).
Dev Mongo: DefaultTenant 358 satır, 322 karar=evet (Draft 321 + InReview 1), 36 hayır (Draft 29 + Retired 7), Retired+evet 0; eski kiracının 358 satırına alan
yazılmadı. **CT canlı E4, birleşik ağaç (main + Adım 0 + Adım 2, çakışma yok):** `GET /Tasks/api/lookups/document-citations?term=SOP` DefaultTenant oturumuyla
200 ve satırlar: GMG-COM-SOP-0001/2/3 `linkableInErp=true` (Kalite kararı), GMG-GDP-SOP-0001 `false` + gerekçe "Draft" (kararsız Taslak). HTTP/JWT katmanı kapandı.
**Sıra:** Adım 0 PR → Adım 2 PR (ya da birlikte) → merge → canlıda görev açarak dondurma kanıtı (BL-358 org verisi şart) → bu maddenin emekliliği.
**Açık karar (sahip/DM):** üretim kiracısına gerçek ingest'in tetiklenme şekli (yönetici CSV yükleme ekranı mı, tek seferlik iç uç mu) — `IngestDocumentMasterRegisterCommand`
hazır, tetikleyici yok. → **KAPANDI (CT, 2026-09-11):** WP-DM-DCP005-REGISTER-IMPORT-UI-01, dal `feature/dm/dcp-005-register-import-ui`
(base Adım 0 dalı `1e07712b`, commit `c22eb1ce`, worktree `.claude/worktrees/dm+dcp-005-register-import-ui`; sahip push edecek, kendi PR'ı, Adım 0'dan sonra merge).
Önizle (yazmaz; Created/Updated/Unchanged/Blocked, hash, "daha önce yüklendi") → `window.showConfirm` → onayla (409 IMPORT_CONTENT_CHANGED / IMPORT_ALREADY_APPLIED;
mevcut ingest komutu çağrılır; IAuditableCommand) → yükleme geçmişi (`document_management_register_import_batches`, (TenantId, ContentHash) unique). Yeni izin
`platform.document-management.master-register.import` üç uçta + sayfada; 7 dil 36 anahtar tek resx setinde. CT: 23/23 test, hash kontrolü kapatılınca 1 kırmızı;
canlıda sayfa yeni izni oturum yenilenmeden görmez (UAS doğru davranış) — yeni oturumla tıklama kanıtı açık. → **KAPANDI (CT, 2026-09-11 23:00, sahibin yeni oturumu):** sayfa açıldı; 2 satırlık CSV ile önizleme
(2 toplam / 0 yeni / 0 güncellenecek / 2 değişmeyecek / 2 Kalite kararıyla alıntılanabilir / Draft 2) → showConfirm → onay 201 → toast + geçmiş satırı
(admin@diten.com, 22:58:50) → aynı dosya tekrar önizleme "zaten içe aktarılmış" uyarısı → onay → sunucu 409 IMPORT_ALREADY_APPLIED, ekranda "Bu dosya zaten içe aktarılmış."
**İki küçük bulgu (DM, ayrı küçük düzeltme, engel değil):** (1) geçmiş satırı "0 / 2 / 0" yazıyor — ingest komutu her upsert'i "güncellendi" sayıyor, önizleme ise
"değişmeyecek" demişti; sayım tutarlılığı (Unchanged ayrımı commit sonucunda da) · (2) 409 sonrası önizleme kartı "Önizleme tamamlanamadı" durumuna düşüyor ve
sunucunun İngilizce ayrıntı cümlesi ("This exact file was already imported on …") "Satır hataları" altında ham görünüyor — sebep kodu köprüsü var ama ayrıntı
metni yerelleştirilmemiş. Üretim yüklemesi = DM/Kalite'den izinli kişi bu ekrandan;
kanıt = geçmiş satırı + denetim kaydı.

**Karar:** tek doküman kaynağı Doküman Yönetimi'nin Ana Kütüğü (Master Register). Görev tarafındaki CSV kütüğü (`document_reference_list_versions`,
`GET /Tasks/api/document-list/search`) geçicidir; kütük CSV UID'lerini sahiplenir (`PermanentUid = CSV uid`), görev formu `by="uid"` ile
`document-master-register/citations/search` (DM-2b, main'de) ucunu çağırır. Dondurulmuş görev alıntıları (`TaskDocumentReference`) olduğu gibi
okunur kalır; hiçbir kapanmış görevin alıntısı yeniden çözümlenmez (DCP-005 §6.2).

**Sayfanın kaderi:** Görev Tanımları → Kontrollü Dokümanlar (`TaskManifestProvider` sayfası `/Tasks/DocumentList`, `platform.tasks.document-list.*`
izinleri, içe aktarma dahil) CSV listesinin ekranıdır; taşıma (devir planı Adım 2) **canlıda çalıştıktan sonra** menüden ve koddan kaldırılır,
yerine Doküman Yönetimi'nin kütük ekranı. Yarım kaldırma yok (K4): sayfa gitmeden önce arama + sürüm + durum bilgisinin DM ucunda karşılandığı
ölçülür. Kaldırılan izin anahtarları Auth kataloğunda kalır (DELETE-sync Faz 1.5, BL-340 komşusu) — ayrıca temizlenir.

**Ölçüm komutu:**

    grep -n "document-list" frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js
    grep -n "DocumentList" services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/SelfRegistration/TaskManifestProvider.cs

### BL-370

**Görevde kanıt dosyası (evidence attachment) yok — dosya deposu + kapanış zarfı, sıralı iki iş**

DURUM: AÇIK · KARAR: sahip, 2026-09-11 ("yapacağız", sırayla) · SAHİP: DM geliştiricisi (depo) → CT (zarf) · KAYIT: 2026-09-11

**Ölçüm:** görev formunda kontrollü doküman ATIFI var (`TaskDocumentReference`, dondurulmuş); dosya yükleme hiçbir görev ekranında yok (`type="file"` yalnız
CSV içe aktarma sayfasında). `ChecklistTemplateItem.EvidenceRequired` / `ChecklistRunItem.EvidenceRequired` saklanıyor, hiçbir şeyi zorlamıyor; ekranda
"Kanıt belgesi gerekiyor. Belge bağlantısı doküman modülü bağlandığında etkinleşecek." ~~Depoda dosya/nesne deposu soyutlaması yok~~ **DÜZELTME (CT, 2026-09-11 gece):** dosya deposu VAR ve main'de — MOD-0262-FU01 Document Binary Store (`IContentStorageGateway`, `DocumentRepositoryService`, `api/v1/document-repository/*`, yerel dosya sistemi sağlayıcısı, kiracı izolasyonu, SHA-256, izin listesi, testler; UI yok). İlk ölçümde yanlış anahtar kelimelerle arandı.
İş Raporu doküman/kanıt göstermiyor.

**Sıra:**
1. ~~MOD-0262-FU01 Document Binary Store~~ — ZATEN VAR (main, `c9bedba8` ve sonrası); adım düşer.
2. **MOD-0024 Slice ATT-1 görev ekleri** (create-runtime pack §20/3 altında ready-for-dev, CT 2026-09-11) — DM geliştiricisinin sohbetine prompt verildi 2026-09-11 gece: `task_attachments`, kanıt zorunluluğu zorlanır, Görev Merkezi "Ekler". Kapanış zarfı (Faz 2) ayrı kalır.
3. **MOD-0024 Faz 2 kapanış zarfı** (pack draft; §7: kanıt/çıktı "her zaman var") — CT, toplantı modülü bittikten sonra; tahmin 2 prompt: kapanışta kanıt
   yükleme (depoya bağlanır), `EvidenceRequired` maddede gerçek zorlama, Görev Merkezi detayında kanıt listesi, İş Raporu'nda kanıt sütunu.
3. Kalite izi tamamlanır: "hangi prosedüre göre yaptım" (atıf, DCP-005) + "işte kanıtı" (bu madde).

**DURUM (CT, 2026-09-12):** Adım 2 (görev ekleri, Slice ATT-1) **TESLİM** — dal `feature/pss/mod-0024-att1-task-attachments`, commit `a5fcb330` (sahip push edecek, kendi PR'ı; main ile hizalı).
Görevde artık dosya var: kanıt / çıktı / düz ek, kiracıya ayrı klasörde, indirme dosya adıyla, silme yumuşak (bayt kalır). `EvidenceRequired` gerçekten zorluyor (409 CHECKLIST_EVIDENCE_REQUIRED).
Yetki: ekleme/silme holder∨requester + `platform.tasks.update`, okuma/indirme `platform.tasks.read`, başka kiracı 404, kapalı görev 409, ekleme ve silme denetim kaydı.
CT eklemesi: `TaskAttachmentRepositoryMongoTests` (gerçek Mongo) — diğer tüm testler sahte depo kullanıyordu ve sahte depo silinmişleri kendi süzüyordu; süzgeç üretimden düşerse artık kırmızı.
**Açık (küçük, ATT-1 kusuru DEĞİL):** Görev Merkezi **detay sayfası** dev'de iş öğesini çözemiyor — akış (`/WorkCenterNext/api/work-items`) tek öğe döndürüyor
("Reference work item"), o öğenin kendi detay adresi bile "İstenen iş öğesi bulunamadı" diyor; sayfa öğeyi yüklü akıştan `itemById` ile arıyor. Bu yüzden "Ekler"
kartının canlı ekran görüntüsü alınamadı; kart jsdom'da gerçek app.js ile 16 testle, uçlar ise canlı API turuyla kanıtlandı. Bir sonraki Görev Merkezi canlı turunda bakılacak.

**Kalan (Adım 3):** kapanış zarfı (MOD-0024 Faz 2) — kapanışta çıktı/kanıt alanları; iş raporunda dosya YOK (rapor görevden okur).

### BL-371

**Auth hesap türü (Unknown/Human/Service) + kiracı-içi kullanıcı arama/hesap doğrulama uçları — PPM portföy sorumlusu için hesap olgusu**

DURUM: TESLİM (CT, 2026-09-11) · dal `feature/infra/auth-account-kind` (base toplantı dalı `a699b3eb`; worktree `.claude/worktrees/infra+auth-account-kind`;
sahip push edecek; PR'ı toplantı ara-nokta PR'ından SONRA) · SAHİP: CT (altyapı) · TALEP: Codex/PPM · KARAR: sahip 2026-09-11

**DÜZELTME C1+C2 (CT, 2026-09-11 gece):** Codex'in üç bulgusu kapandı. (1) Paylaşımlı ortam olayı mevcut kanıttan ayrıştırıldı (yeni sorgu/yazım yok). (2) Fixture:
yanlış hedef host kurulmadan ÖNCE reddedilir (ortam değişkeni · runner ≠ paylaşımlı sunucu · efektif konfigürasyon ön-okuması), başlangıç hatasında factory/env/mongod
temizliği, kilitli seri başlatma, koşuma özel rastgele JWT anahtarı (çıktıya yazılmaz). (3) Guid serializer: uyumlu tekrar = sessiz, UYUMSUZ = fırlatır (ajanın "uyar ve devam"
sapması reddedildi); testte izolasyon Platform emsaliyle: test derlemesinde [ModuleInitializer] Standard + MongoDefaults.GuidRepresentation. Ayrıca sahip kararı:
`auth.users.lookup` TenantSelfServicePermissions'a (mevcut kiracıların Admin'i açılış reconcile'ında alır). Auth 781/784 ×2 (3 eski kırmızı); sabotajda refusal testi kırmızı.
Dal main ile hizalandı; PR 5 açılabilir (sahip push eder).
**C3 (CT, 2026-09-12 00:xx):** Codex'in ikinci fixture bulgusu (ortam override'ı kilit dışında geri alınıyor → iki host birbirinin ortamını bozuyor) kapandı:
override penceresi = kilit penceresi (kur → ön-kontrol → host → ayar yakala → geri al → bırak, tek finally); Dispose ortama dokunmaz; factory/runner ayrı try/catch,
ilk hata orijinal yığınla yeniden fırlar; T1–T6 (A→B, B→A, host çalışırken env eski, başarısız başlangıç, dispose hatası, sır sızıntısı). CT sabotajı: geri alma
Dispose'a taşınınca T1/T2 kırmızı. Auth 787/790 (3 eski). PR 5 sabah.

**Karar:** sınıflandırma yalnız açık atanan `auth.users.account-kind.manage` ile (ExplicitGrantOnly); Portfolio uygunluğu = aynı kiracıda aktif + Human (PPM kararı);
ek pozisyon/birim şartı yok; mevcut hesaplar otomatik sınıflandırılmaz (hepsi Unknown). Uçlar: `GET api/users/lookup`, `GET api/users/{id}/account-assertion`
(`auth.users.lookup`, sıradan kiracı anahtarı, modül access-governance), `POST api/users/{id}/account-kind`. 404 = yok/başka kiracı aynı gövde.
İzole gerçek-sağlayıcı fixture: `tests/.../Testing/AccountKindAcceptance.cs` (EphemeralMongo.Core 1.1.3 + WebApplicationFactory, DB `diten_auth_itest_account_kind`,
makinede mongod ikilisi şart: `DITEN_ITEST_MONGOD_BIN_DIR` → PATH → Homebrew).

**CT doğrulaması:** Auth 758/761 (3 eski kırmızı) · sabotaj: manage anahtarı listeden çıkınca 7 kırmızı, Register'a Human yazılınca 3 kırmızı · izole uç testleri 20/20 ·
Web 137/137 + vitest 106/106 · 7 dil tek anahtar seti · paylaşımlı dev K3: katalog +2, lookup SuperAdmin+DefaultTenant Admin'de, manage hiçbir rolde, denetim olayı 0,
admin@diten.com AccountKind=Unknown (seeder'ın kendi upsert'i); ajanın ilk host denemesi paylaşımlı Mongo'ya bir kez değdi (18:32Z), raporunda açıkladı.

**Açık:** (1) daha önce kurulmuş kiracıların Admin rolü `auth.users.lookup`'ı otomatik almıyor (reconcile yalnız self-service anahtarları) → PPM okuyucusuna elle ya da
CT reconcile kararı; (2) ilk sınıflandırıcıyı sahip Rol İzinleri'nden elle atar; (3) doğrulama hataları ProblemDetails (zarf dışı) — servis geneli, PPM adapter'ı
bunu "sağlayıcı/sözleşme hatası → 503" sınıfına koyar; (4) PPM HTTP eşlemesi (403 vs 409, 401) açık delta, iki taraf henüz dondurmadı; (5) Codex'in kendi pack
düzeltmesi (MOD-0117 / DCP-006) PPM'de.

### BL-372

**S4'ün açtığı 9 kırmızı WorkAggregation testi — CT kabulünde kaçtı, 2026-09-12'de bulundu ve kapatıldı**

DURUM: KAPANDI (commit `d6e75fe3`) · BULAN: S5c ajanı (raporunda "9 pre-existing" diye bildirdi), CT doğruladı · KAYIT: 2026-09-12

**Ne oldu:** S4 (`680cb888`) görev projeksiyonuna `scheduleReviewMeeting` eylemini ekledi. İki muhafız o günden beri kırmızıydı:
`WorkItemActionDispatchTests` (projeksiyondaki her eylemin gönderim yolu olmalı) ve `ProviderActionPermissionTests` (beyan edilen izinler verilince
hiçbir eylem PermissionDenied kalmamalı). Eylem `TaskPermissions.Read` ile kapılanmıştı; oysa sağlayıcının kendi kuralı "okuma/oluşturma/silme uçları korur,
projeksiyondaki eylemleri değil" ve Read beyan listesinde yok.
**Neden kaçtı:** S4 ve S5 kabullerinde `Meetings|Tasks` filtresiyle koştum, **WorkAggregation** paketini koşturmadım. Ders: bir sağlayıcı/projeksiyon değişince
WorkAggregation da koşulacak.
**Düzeltme:** eylem `TaskPermissions.Update` ile kapılandı (beyan edilen, görev tarafı yetkisi; toplantı tarafı yetkisi alıcı uçta kalır, MOD-0024 → MOD-0357
bağımlılığı yok, ADR-003); gönderim muhafızına `DispatchedByAnotherModule` listesi eklendi (S4'ün `TaskActionCodeReachabilityTests`'te zaten kullandığı desen)
ve liste kendini denetliyor: listedeki kod burada gönderilebilir hale gelirse test kırmızı. Sonuç: Meetings+WorkAggregation+Tasks 1513/1513.
**Ek not:** aynı koşuda 3 Mongo testi kırmızı göründü (AgendaItem, RecordLink, TaskCommentOrder); tek başlarına ve tekrar koşumda yeşiller — paylaşımlı
yerel Mongo çakışması (BL-343 d), S5c'nin işi değil.
---

### BL-373

**Seri üretimi her örnekte davet postası atıyor — organizatör dahil**

DURUM: YERİNE GEÇİLDİ — BL-387 (`1da09a16`, sahip kararı 2026-09-14): düzenleyen artık düz davet değil kendi "takviminize eklendi" postasını alır; 2026-09-13'teki "olduğu gibi kalsın" kararı geçersiz · BULAN: CT (S11 kabulünde ölçüldü, ajan raporunda yoktu) · KAYIT: 2026-09-12

**Karar gerekçesi:** S5b'den beri davet postası `.ics` taşıyor ve Google entegrasyonu olmadığı için toplantının organizatörün kendi takvimine düşmesinin TEK yolu bu posta. Organizatörü dışarıda bırakmak onun takviminden kendi toplantısını silmek olurdu. Posta hacmi, aynı toplantıları elle açmakla aynı.

**Ölçülen davranış:** S11'in süpürmesi bir örnek ürettiğinde `CreateMeetingCommand` normal yolunu kullanıyor; o yol da her YENİ
toplantı için `IMeetingInviteMailer.SendInviteAsync` çağırıyor (`MeetingCommandHandlers.cs:197-199`). Mailer alıcı listesinden
yalnız *eylemi yapan kullanıcıyı* çıkarır (`MeetingInviteMailer.cs:69-73`); arka plan süpürmesinde eylemi yapan kimse olmadığı için
`actingUserId = Guid.Empty` gider ve **hiç kimse dışarıda kalmaz** — organizatör de kendi serisinin toplantısı için davet alır.

**Neden bir karar:** haftalık bir seri + 10 katılımcı = haftada 11 posta ve organizatör hiç planlamadığı bir toplantı için davet alıyor.
Davranış savunulabilir (gerçekten kimse "yapmadı", herkesin haberi olmalı) ama bilinçli seçilmedi, ortaya çıktı.

**Seçenekler:** (a) olduğu gibi bırak, dokümante et · (b) süpürme yolunda organizatörü hariç tut (mailer'a "sistem üretti" sinyali) ·
(c) seri örnekleri için ayrı bir "seri toplantısı planlandı" şablonu (S5'in üç şablonuna dördüncü) · (d) seri kuralında
`SendInviteMail` bayrağı.

**Nerede:** `GenerateDueMeetingSeriesHandler.GenerateInstanceAsync` → `CreateMeetingCommand` / `ScheduleFollowUpMeetingCommand`.
Testle sabitlenmedi: seri işleyici testleri `RecordingMediator` kullanıyor, gerçek oluşturma hattını (dolayısıyla posta yolunu)
çalıştırmıyor. Karar hangisi olursa olsun, o kararı kanıtlayan test aynı işte yazılır.

---

### BL-374

**S5b `.ics` ekinin iki sınırı — tekrar denemede ek düşüyor, organizatör e-postası çözülemezse ORGANIZER boş**

DURUM: KAPANDI (ikinci kez) — S10B canlı turu ilk düzeltmenin hiç çalışamadığını gösterdi: ilk gönderimde düşen posta NextRetryAt almıyordu ve süpürme onu hiç görmüyordu. `EmailDispatchRetryPolicy` ile ilk tekrar deneme artık zamanlanıyor (CT, 2026-09-13). Canlı yeniden doğrulama bekliyor · BULAN: S5b ajanı (1) + CT (2) + S10B canlı tur (3) · KAYIT: 2026-09-13

**Teslim:** ek (≤ 256 KB) dispatch kaydına yazılıyor ve tekrar deneme onu gönderiyor; kuyruğa alınırken şablonun SemanticVersion'ı damgalanıyor; tekrar denemede gövde yalnız değişkenlerde maskelenmiş değer yoksa ve şablon sürümü değişmemişse yeniden üretiliyor, aksi halde önizleme + sebep kodlu `email.dispatch.retry_degraded` logu; organizatör e-postası çözülemezse `.ics` hiç eklenmiyor. Maskeleme kodu değişmedi. ⚠ Pratik sınır: BL-377 yüzünden toplantı davetlerinde gövde yeniden üretimi neredeyse hiç devreye girmez — takvim eki (asıl kazanç) her durumda gider.

**⚠ CT düzeltmesi (aynı gün):** sahibe önce "tam metni kayda yazalım" önerildi. Ölçüm bunu yanlış çıkardı: kayıttaki gövde KASITLI maskeli (`QueueEmailNotificationHandler.MaskSensitiveValues`, değişkenler `SanitizeVariables` ile `[REDACTED]`) — geçici şifre gibi değerler veritabanına yazılmasın diye. Tam gövdeyi saklamak bu korumayı geri alır. Doğru şekil: tekrar denemede gövde şablondan ve temizlenmiş değişkenlerden YENİDEN üretilir; bir değişken maskelenmişse sessizce eksik posta gönderilmez; takvim eki sır içermediği için ayrıca saklanır. WP: WP-MG-MOD0357-BL374-RETRY-FIDELITY-01.

**(1) Tekrar deneme eki taşımıyor.** Davet postası ilk denemede SMTP'de düşerse `EmailDispatchSweepJob` →
`EmailDispatchJob` postayı kalıcı `NotificationDispatch` satırından yeniden kurar; o satırda ek yok (S5b eki bilerek
kalıcılaştırmadı) ve gövdenin de yalnız önizlemesi var. Sonuç: tekrar denenen davet takvim eki olmadan ve kısaltılmış
gövdeyle gider. Gövde kısıtı S5b'den önce de vardı; ek kısıtı S5b ile görünür oldu.
Seçenekler: (a) ek içeriğini dispatch kaydına yazmak (kalıcılık kararı, PII değil ama boyut) · (b) tekrar denemede
.ics'i toplantıdan yeniden üretmek (dispatch → meeting bağı gerekir) · (c) kabul edip dokümante etmek.

**(2) Organizatör çözülemezse `ORGANIZER;…:mailto:` boş.** `MeetingInviteMailer` organizatörü çözemediğinde boş
e-postalı bir yedek kayıt kullanıyor; RFC 5546'ya göre METHOD:REQUEST bir ORGANIZER adresi ister, Outlook böyle bir
daveti "desteklenmeyen takvim iletisi" olarak gösterebilir. Gövde yine gider (K12). Yalnız e-postası olmayan bir
organizatörde olur; Auth kullanıcılarında e-posta zorunlu olduğu için bugün pratikte beklenmiyor.

---

### BL-375

**Görev tipi güncellemesinde eşzamanlılık koruması yok — son yazan kazanır**

DURUM: KAPANDI — `3a26bef1` (PSS, `feature/pss/mod-0024-attachments-ux`; CT sabotajla doğruladı, 2026-09-13) · BULAN: PSS ajanı (WP-PSS-MOD0024-REVIEW-MEETING-POLICY-01), CT doğruladı · KAYIT: 2026-09-13

`UpdateTaskTypeRequest` / `UpdateTaskTypeHandler` `ExpectedVersion` taşımıyor; güncelleme tam değiştirme. İki yönetici
aynı tipi aynı anda düzenlerse ikincisi birincinin değişikliğini sessizce ezer. Görev tipi L3 bir yapılandırma
(kayıt sınıfı, GQMS alanı, yönetici belgeler, kapanış sonuçları ve artık gözden geçirme toplantısı gerekliliği).
Not: CT'nin prompt'u bu korumanın var olduğunu varsaymıştı; yanlıştı, ajan doğru ölçtü ve kapsamı genişletmedi.
Çözüm şekli bu depoda hazır: toplantı ve seri güncellemelerinin `ExpectedVersion` + 409 deseni.

---

### BL-376

**Auth'ta ad/soyad uzunluk sınırı yok — PPM'in 200 karakterlik etiket kabulüyle çelişebilir**

DURUM: AÇIK (karar gerekli, acil değil) · BULAN: CT (WP-INFRA-AUTH-DISPLAY-LABEL-01 hazırlığı) · KAYIT: 2026-09-13

**Ölçüm:** `User.FirstName` / `LastName` hiçbir validator'da, istek modelinde veya entity'de uzunlukla sınırlanmıyor
(RegisterCommandHandler, CreateUserCommandHandler, UpdateUserCommandHandler, InternalEventsController,
PlatformAuthController — hepsi olduğu gibi yazıyor). Yeni `display-label` ucu etiketi KESMEDEN döndürecek (PPM şartı:
sessiz kesme yok). PPM ise 200 karakteri aşan etiketi zarf ihlali sayıp 503 veriyor
(`PortfolioService.cs:147` aday listesi için aynı kural). Sonuç: adı+soyadı 200 karakteri aşan gerçek bir kullanıcı
portföy sahibi yapılamaz.
**Seçenekler:** (a) Auth'a ad ve soyad için yazma sınırı (ör. 100 + 100 → etiket ≤ 201; tam 200 için 99 + 100 veya
birleşik kontrol) — mevcut uzun kayıtlar için okuma tarafı yine kesmez · (b) PPM kendi sınırını yükseltir ·
(c) kabul edilir, dokümante edilir. Karar Auth (altyapı CT) + PPM ortak.

---

### BL-377

**Bildirim değişken temizleyicisi boşluk içeren HER değeri sır sayıyor — denetim kaydı ve tekrar deneme gereksiz yere körleşiyor**

DURUM: AÇIK · BULAN: BL-374 ajanı, CT doğruladı · KAYIT: 2026-09-13

**Ölçüm:** `NotificationParsing.LooksLikeRawSecret` (`NotificationParsing.cs:38-52`) değerde bir boşluk veya `=` görürse `true` dönüyor.
`QueueEmailNotificationHandler.SanitizeVariables` bu kontrolü anahtar adından bağımsız uyguluyor, yani "Haftalık Kalite Toplantısı",
"Ayşe Yılmaz" gibi masum değerler `VariablesJson`'a `[REDACTED]` olarak yazılıyor. (Önizleme gövdesi yalnız anahtar adına göre
maskelendiği için etkilenmiyor.)
**Sonuç:** (1) gönderim kaydındaki değişkenler denetimde okunamaz; (2) BL-374'ün gövde yeniden üretimi, `[REDACTED]` varken bilerek
çalışmadığından toplantı davetlerinde neredeyse hiç devreye girmez.
**Yön (karar değil):** sırrı DEĞERİN şekline göre değil, şablonun değişkeni hassas olarak İŞARETLEMESİNE göre maskelemek (anahtar tabanlı
liste + şablon meta verisi). Güvenlik kodudur: tek başına gevşetilmez, ayrı WP + sabotaj ister.

---

### BL-378

**Görev Merkezi iş bağlamı kartı sunucunun göndermediği alan adlarını okuyor — evet/hayır ve bağlantı alanları ham çiziliyor**

DURUM: AÇIK (küçük) · BULAN: PSS ajanı (Faz 2a), CT doğruladı · KAYIT: 2026-09-13

`app.js` `renderBusinessContext` (≈4394) değeri `field.kind === 'boolean'` / `field.kind === 'link'` ile biçimliyor; sunucu
`WorkItemBusinessFieldDto` (`WorkAggregationModels.cs:746`) `kind` değil `valueType` gönderiyor. Sonuç: evet/hayır alanı "true/false"
olarak, bağlantı alanı düz metin olarak görünüyor. Gizleme (`redacted`) doğru okunuyor — güvenlik etkisi yok. Faz 2a'nın kapanış kartı
doğru adları (`valueType`) kullandı; iki çizim birleştirilmeli (BL-365 parça yeniden kullanımı).

---

### BL-379

**Görev Merkezi sözleşmeyi geçemeyen iş öğelerini SESSİZCE atıyor — S4'ün toplantı politikası bir çok görevi listeden düşürdü**

DURUM: KAPANDI — WP-WCN-REVIEW-MEETING-CONTRACT-FIX-01, canlıda doğrulandı (2026-09-13) · BULAN: DM ajanı (WP-WCN-DETAIL-ITEM-RESOLVE-01 teşhisi), CT doğruladı · KAYIT: 2026-09-13

**Teslim:** toplantı politikası ve "Toplantı planla" eylemi birlikte, yalnız görevin sahibi/açanına ve görev açıkken yayınlanıyor; toplantı zaten bağlıysa eylem pasif (`REVIEW_MEETING_ALREADY_SCHEDULED`). Kapanmış görevde ve başkasının görevinde ikisi de yok — "ölü işte eylem yok" ve "astın görevinde eylem yok" kuralları korundu (ilk tasarım CT'nindi ve bunlarla çakıştı). Gerçek sağlayıcı çıktısı 24 hücrelik bir golden matrisle sabitlendi ve GERÇEK `validateWorkItem`'dan geçiriliyor. Görev Merkezi reddedilen öğeleri artık saklamıyor: konsol uyarısı + görünür not; detay sayfası "sözleşme hatası" diyor. Canlı: 7 ham öğeden 6'sı görünüyor, `0c3b7a4c` açılıyor.
**Aynı teşhisten çıkan ikinci hata (CT düzeltti):** `HttpWorkItemProvider.GateActions` izni olmayan uzak eylemi pasife çevirirken sebep ETİKETİNİ boş bırakıyordu (`DisabledReason = action.DisabledReason`, etkin eylemde null) → `DISABLED_REASON_REQUIRED` → uzak sağlayıcının (dev-reference, MOD-0023 onayları) o öğesi de sessizce düşüyordu. Artık `WorkAggregation_ActionDisabled_PermissionDenied`.

**Ölçüm (canlı + kod):** `/WorkCenterNext/api/work-items` 7 öğe döndürüyor, ekranda 4 görünüyor. `work-items-api.js` `validateItems` her öğeyi
WC-1 sözleşmesiyle doğruluyor ve geçemeyeni `state.items`'a hiç koymuyor; `app.js` `loadWorkItems` bu hataları okumuyor. Detay sayfası öğeyi
yalnız `state.items`'ta aradığı için "İstenen iş öğesi bulunamadı" diyor.
**Kök neden:** MOD-0357 S4 `TaskWorkItemProvider` `reviewMeetingPolicy`'yi HER göreve koyuyor ama `scheduleReviewMeeting` eylemini yalnız
kapanmamış, toplantısı henüz olmayan görevde, sahibine/açanına ekliyor. Sözleşme kuralı `REVIEW_MEETING_ACTION_REQUIRED`
(`fixture-contract.js:582`): politika `notAllowed` değilse eylem OLMALI. Başkasının görevi, kapanmış görev veya toplantısı planlanmış görev →
öğe düşüyor. CT'nin S4 kabulü birim testlerle yapıldı; sözleşme doğrulayıcısı gerçek sağlayıcı çıktısına karşı koşulmadı.
**Ek:** dev-reference öğesi `DISABLED_REASON_REQUIRED` ile düşüyor; kaynağı düzeltme WP'sinde ölçülecek.

---

### BL-380

**Belge yürürlüğe alma: onay kanıtı hiç değerlendirilmemişse uyarıyla geçiyor — Kalite teyidi + ekran/davranış tutarsızlığı**

DURUM: KAPANDI (kod, DM kulvarı) — `91901559` (`feature/dm/dcp-005-retire-csv-list`, 2026-09-15). Boş onay kanıtı artık yürürlüğe almayı engelliyor; durum ekranı ve işlem tek kuralı kullanıyor. Not: NotRequired kodda yazılabiliyor ama hiçbir kritiklik onu üretmiyor — onaya tabi olmayan belge türü bugün yok (Kalite teyidiyle birlikte konuşulacak) · önceki: AÇIK · BULAN: DM ajanı (WP-DM-DOCMGMT-RED-TESTS-01), CT · KAYIT: 2026-09-13

**(1) Kalite sorusu (Kural 4 ile birlikte sorulabilir):** `DocumentLifecycleService` artık onay kanıtı durumu boş (FU09 onay rotası hiç
çalışmamış) bir belgeyi Effective'e uyarıyla geçiriyor; `Complete` ve `NotRequired` dışındaki her dolu değer engelliyor (CT, fail-closed).
Bu, özelliğin kendi testinin (`MarkEffective_without_evidence_or_gate_succeeds_with_warnings`, 0f71a237) yazıldığı davranış; FU10'un
devre dışı bırakılamayan sürüm kapısı 3 aynı alanı okumaya devam ediyor. Soru: onay rotası çalıştırılmamış bir belgenin uyarıyla yürürlüğe
girmesi GxP açısından kabul mü, yoksa boş durum da engellemeli mi?
**(2) Tutarsızlık:** `GetStateAsync` (`DocumentLifecycleService.cs` ≈61-74) hazır olma durumunu hâlâ eski kuralla (yalnız `Complete`, gate
yoksa engel) raporluyor. Ekran "hazır değil" derken işlem geçebilir. Düzeltme (1)'in cevabına göre yapılmalı.
**2026-09-14:** CT önerisi engellemek (onaya tabi olmayan tür açıkça "onay gerekmez"; Veeva/MasterControl ve 21 CFR Part 11 ile uyumlu). Sahip soruyu Kural 4 ile birlikte Kalite'ye iletiyor.
**2026-09-15:** sahip önerileri onayladı ve eksiklerin bu doğrultuda bitirilmesini istedi → BL-380 (boş onay kanıtı da engeller) ve Kural 4 (yürürlükte olmayan belgeye bağlı görev türü aktif edilemez) DM kulvarında uygulanıyor (WP-DM-DCP005-BL380-KURAL4-01). Kalite farklı cevap verirse kural tek yerde, geri çevrilebilir.

---

### BL-381

**Görev Merkezi'nde "Toplantı planla" gerçek görevde çalışmıyordu — eylem sunucu çıktısında tarayıcı girdisine eşlenmiyordu**

DURUM: KAPANDI (iki katman) — `bea716aa` tıklamayı zamanlayıcıya yönlendirdi; S10B canlı turu ikinci katmanı buldu: Görev Merkezi sayfaları `Meetings/api.js`'i yüklemiyordu, pencere sessizce açılmıyordu. İki görünüm artık yüklüyor, `MeetingsApi` yazma bağımlılığı olarak boot'ta denetleniyor (CT, 2026-09-13) · BULAN: MOD-0357 S10 + S10B canlı tur · KAYIT: 2026-09-13

`mock-data.js` sunum eşleyicisi yalnız `plan` için girdi türetiyordu; sunucunun eylem DTO'sunda `input` alanı yok. Gerçek
`scheduleReviewMeeting` toplantı planlama penceresini açmadan genel eylem ucuna düşüyordu (400 `WORK_ITEM_ACTION_UNKNOWN`).
CT'nin S4 kabulündeki test kaynak metnini okuyordu, gerçek eylemi hiç eşleyiciden geçirmemişti. Artık görev sağlayıcısının
golden çıktısı gerçek eşleyiciden geçiriliyor.

---

### BL-382

**Seri süpürme ve tatil çekme işleri DI'da kayıtlı değildi; kapatılan iş Hangfire'da çalışmaya devam ediyordu**

DURUM: KAPANDI — `87f660ac` + `a36308a2` (CT, 2026-09-13) · BULAN: MOD-0357 S10 canlı tur · KAYIT: 2026-09-13

(1) `MeetingSeriesSweepJob` ve `HolidayAutoFetchJob` kayıt listesindeydi ama DI'da yoktu; bayrak açıldığı an her koşu
"No service for type" ile düşüyordu. `BackgroundJobHandlerRegistrationTests` artık gerçek `AddApplication` kompozisyonunu okuyor.
(2) Devre dışı kayıt Hangfire'dan silinmiyordu; bir kez açılan iş bayrak kapansa da cron'unda çalışmaya devam ediyordu —
artık `RemoveIfExists`. (3) `BackgroundJobContractsTests`'in iki kayıt testi yer tutucu dönemden kalma olduğu için uzun süredir
kırmızıydı; gerçek on iki işin kimlik listesini adlandırıyor.

---

### BL-383

**Depo kökünü klasör ADIYLA ya da ilk `.git` klasörüyle bulan testler iç içe worktree'lerde yanlış checkout'u okuyor/yazıyor**

DURUM: AÇIK (bir örneği düzeltildi) · BULAN: CT (S10B raporundaki "başka oturum değiştirdi" dosyası CT'nin kendi yazımıydı) · KAYIT: 2026-09-13

Worktree'ler ana klonun içinde (`.claude/worktrees/…`) duruyor. `TaskProviderContractGoldenTests` kökü `ERP-vNext` adlı klasöre yürüyerek
buluyordu; entegrasyon worktree'sinde çalışan yeniden üretim ana kopyanın fixture'ını ezdi, karşılaştırma da yanlış dosyayı okudu —
testler "yeşil" göründü. Düzeltildi: en yakın `.git` GİRİŞİ (klasör ya da dosya). Aynı kök bulma kalıbını kullanan başka testler var
(`grep '".git"'`: ManagementGovernance mimari testleri, PPM GateI kanıt testleri, `DocsPathGuardTests`); her biri worktree'de koşunca
başka checkout'un dosyalarını tarıyor olabilir — tek tek ölçülmeli.

**Ölçüldü (2026-09-13, go-live kapı koşusu):** `run_phase1_gates.sh` ana checkout'ta koşunca mimari testler `.claude/worktrees/` altındaki
kopyaları da taradı: `TenantContradictionSiteGuardTests` 8 yerine 48 nokta saydı, `MongoTestDatabaseGuardTests` istisna listesindeki
dosyaları başka yollarda bulup "listede yok" dedi → 4 sahte kırmızı. Aynı commit (`9f65800f`) iç içe kopya içermeyen temiz bir checkout'ta
18/18 geçti. CI temiz checkout kullandığı için etkilenmez; yerelde kapı yalnız worktree'de ya da ayrı temiz kopyada koşulmalı.

---

### BL-384

**Eski sürüm yeni belgeleri okuyamıyor — Mongo varlıklarında bilinmeyen alan toleransı yok (geri alma riski)**

DURUM: KAPANDI (tolerans) — `4663682c` (CT, `feature/infra/auth-display-label`, 2026-09-14): Platform süreç genelinde `IgnoreExtraElementsConvention`; CT sabotajı (kural kapsamı boş) 3/3 kırmızı → geri → 3/3. Kalan: bu değişikliği içermeyen eski sürüme geri alma yine çöker; eski sürüm tüm belgeyi yeniden yazarsa yeni alanlar kaybolur → canlıda "yedek + ileri düzeltme" kuralı sürer · BULAN: CT, 2026-09-13 · KAYIT: 2026-09-13

Görev motoru dalının Platform'u, toplantı dalının yazdığı `RecordLink` belgelerini okurken `FormatException: Element 'IdempotencyKey' does
not match any field` ile düştü; Görev Merkezi'nin görev kaynağı ve toplantı listesi 500 verdi. Aynı şey canlıda bir sürümü GERİ ALMAK
gerektiğinde olur: yeni sürümün eklediği her alan eski sürümü çökertir. Seçenekler: varlık başına `[BsonIgnoreExtraElements]`, global
convention (`IgnoreExtraElementsConvention`), ya da "geri alma yok, yalnız ileri düzeltme" politikası. Karar CT + sahip.

---

### BL-385

**Tekrar denemede gönderilen posta "Başarısız" kalıyordu; tarama onu dakikada bir yeniden gönderiyordu**

DURUM: KAPANDI — CT, 2026-09-13 (`NotificationDispatch.TryMarkSent`) · BULAN: go-live test ajanı (`EmailDispatchRetrySweepMongoTests`), CT kodda doğruladı · KAYIT: 2026-09-13

`TryMarkSent` yalnız `Queued` satırı `Sent` yapıyordu. Tekrar deneme ise zaten `Failed` olan satırı gönderiyor: sağlayıcı kabul
ediyor, işaret 409 ile reddediliyor, `EmailDispatchJob` reddi yok sayıyor; satır `Failed` kalıyor, `RetryCount` artmıyor,
`NextRetryAt` geçmişte kalıyor → her dakikalık taramada aynı posta yeniden gidiyor, `MaxRetryCount` onu durdurmuyor. İş yolundaki
tekrar denemelerde main'de de vardı; BL-374 düzeltmesi (ilk gönderim hatasına `NextRetryAt` yazmak) her ilk hatayı bu döngüye soktu.
Düzeltme: `Failed → Sent` geçişine izin. Sabotaj: eski koşul → kırmızı ("Expected Sent, Actual Failed", sonraki tarama yeniden
kuyruğa aldı) → geri → yeşil; Meetings|Notifications|WorkAggregation 544/544.

---

### BL-386

**Toplantıdan çıkarılan katılımcıya iptal postası gitmiyor — takviminde toplantı kalıyor**

DURUM: KAPANDI — `d9dfec87` (`feature/mg/mod-0357-ui-polish`, WP-MG-MOD0357-FOLLOWUPS-01; CT sabotajla doğruladı, 2026-09-14; canlıda denenmedi): yeni `platform.meetings.removed` şablonu (7 dil, linksiz), çıkarmada sürüm artışı, düzenleyenin satırı çıkarılamaz, kendini çıkarana posta yok · BULAN: go-live test ajanı · KAYIT: 2026-09-13

`RemoveMeetingAttendeeHandler` (`MeetingCommandHandlers.cs`, katılımcı silme) posta göndermiyor; satırı silip 200 dönüyor. Sonraki
değişiklik/iptal postaları o kişiyi atlıyor, yani daveti kabul etmiş kişinin takviminde toplantı sonsuza kadar kalıyor. Beklenen:
çıkarılan kişiye `platform.meetings.cancel`, davetle aynı UID, `METHOD:CANCEL`. Test şekli: Alice + Bob ile toplantı, Bob çıkarılır →
yalnız Bob'a tek bir iptal postası.

**Ölçüldü (2026-09-14, WP-MG-MOD0357-BL386-REMOVED-ATTENDEE-CANCEL-01 — ajan şablon kapısında doğru durdu, kod yazılmadı):**
(1) `platform.meetings.cancel` şablonu 7 dilde "davetli olduğunuz toplantı iptal edildi" diyor ve toplantı linki veriyor; çıkarılan
kişi toplantının herkes için iptal edildiğini sanar, link ona 404 verir (`MeetingEligibility.CanView`). → yeni şablon anahtarı
(`platform.meetings.removed`, linksiz, 7 dil, manifest olayı); seed yalnız EKSİK şablonu eklediği için var olanı yeniden yazmak
kurulu veritabanına ulaşmaz. (2) `.ics` SEQUENCE = `meeting.Version`, çıkarma toplantı satırını yazmıyor → aynı SEQUENCE; öneri:
çıkarmada beklenen-sürümlü yazımla sürüm artışı (SEQUENCE hiç düşmez). (3) Davetin gönderildiğini kaydeden alan yok; yaklaşık kural:
toplantı Planlandı + kişi düzenleyen değil + kişi işlemi yapan değil. (4) Düzenleyenin katılımcı satırı bugün silinebiliyor (koruma
yok). (5) Kendini çıkaran kişiye posta gitmez (davet kuralıyla aynı) — sahip teyidi.

---

### BL-387

**Seri süpürmenin oluşturduğu toplantıda düzenleyen de kendi toplantısına davet postası alıyor (karar)**

DURUM: KAPANDI — `1da09a16` (`feature/mg/mod-0357-organizer-calendar-mail`, WP-MG-MOD0357-BL387-ORGANIZER-CALENDAR-MAIL-01; CT sabotajla doğruladı, 2026-09-15; canlıda denenmedi): düzenleyen işlemi kendisi yapmadıysa `platform.meetings.organizer-added / -updated / -cancelled` (7 dil) + aynı .ics; kendisi yaptıysa posta yok; yeni düzenleyen atanınca "eklendi" postası · BULAN: go-live test ajanı · KAYIT: 2026-09-13

Süpürmede oturum açmış kullanıcı yok; oluşturma işleyicisi eylemi yapan kişi olarak boş kimlik geçiyor, `MeetingInviteMailer`'ın
"düzenleyene davet gitmez" kuralı bu yüzden işlemiyor (ölçüm: 3 alıcı). Elle oluşturulan toplantıda düzenleyen posta almaz. Seride
istenen davranış mı (düzenleyenin takvimine de düşsün) yoksa kural mı uygulanmalı — sahip seçer.

---

### BL-388

**Görev alan tanımı: "Sıra" boş bırakılınca kayıt 400 veriyor, hata ham JSON olarak sayfaya basılıyor**

DURUM: KAPANDI — `0d551337` (PSS dalı, CT sabotajla doğruladı, 2026-09-14; canlıda denenmedi): boş Sıra 0 gider, ProblemDetails alan mesajı olarak gösterilir. Aynı ham JSON düşüşü 21 kardeş controller'da duruyor (TaskTypes, ChecklistTemplates, RecurrenceRules, Templates, OrganizationFieldDefinitions, Roles, ModuleCatalog, SubscriptionPlans, 11 CRM…) · BULAN: CT canlı tur · KAYIT: 2026-09-13 · main'de de var

Web formu `SortOrder`'ı `int?` taşıyor, API isteği (`CreateTaskFieldDefinitionRequest`) `int` bekliyor → boş alan `null` gider,
JSON dönüşümü 400 ile düşer. `TaskFieldDefinitionsController.ExtractGatewayErrorsAsync` ProblemDetails'i tanımadığı için ham gövdeyi
hata metni olarak ekrana yazıyor ("The JSON value could not be converted…"). Düzeltme: yükte `SortOrder ?? 0` (güncelleme dahil) ve
ProblemDetails `errors` sözlüğünü okuyan hata eşleyici.

---

### BL-389

**"İnceleme toplantısı planla" eyleminin başarı bildirimi "Onay toplantısı planlandı" diyor**

DURUM: KAPANDI — `d9dfec87` (`feature/mg/mod-0357-ui-polish`, WP-MG-MOD0357-FOLLOWUPS-01; CT sabotajla doğruladı, 2026-09-14; canlıda denenmedi) · BULAN: CT canlı tur · KAYIT: 2026-09-13

Görev Merkezi'nde eylem ve pencere başlığı "İnceleme toplantısı", bildirim "Onay toplantısı". Metin anahtarı ölçülmedi; düzeltme
çeviri kapısından geçer (7 dil). Ölçüm: `grep -rn "Onay toplantısı planlandı" frontend/Diten.Web`.

---

### BL-390

**Toplantılar listesi düzenleyeni bulunamayan kayıtta ham GUID gösteriyor**

DURUM: KAPANDI — `d9dfec87` (`feature/mg/mod-0357-ui-polish`, WP-MG-MOD0357-FOLLOWUPS-01; CT sabotajla doğruladı, 2026-09-14; canlıda denenmedi): liste, detay ve tutanakta 6 yer · BULAN: CT canlı tur · KAYIT: 2026-09-13

Dev'de "S5c E4 Canlı Doğrulama Daveti" satırının Düzenleyen hücresi `22222222-2222-2222-2222-222222222222`. Kullanıcısı olmayan (silinmiş
ya da test) kimlikte etiket yerine GUID'e düşülüyor; ürünün "ekranda GUID yok" kuralına aykırı. Beklenen: "Bilinmeyen kullanıcı"
benzeri bir etiket.

---

### BL-391

**Tarih alanına yanlış biçimde yazılan tarih hata vermeden başka bir tarihe dönüşüyor**

DURUM: KAPANDI (paylaşılan alan + Pozisyon Ataması) — `d9dfec87` (`feature/mg/mod-0357-ui-polish`, WP-MG-MOD0357-FOLLOWUPS-01; CT sabotajla doğruladı, 2026-09-14; canlıda denenmedi). Açık kalan: doğrudan flatpickr kullanan 14 ekran (CRM ×5, Tüzel Kişilik ×2, Organizasyon Birim/Pozisyon, Kiracılar, Talep Fikirleri ×2, Kurumsal Strateji ×2) · BULAN: CT canlı tur · KAYIT: 2026-09-13

Tarih alanları flatpickr `altInput` + `allowInput` ile gösterim biçiminde (gg.aa.yyyy) yazı kabul ediyor. `2026-09-13` yazıldığında
kayıt `2026-06-20` oldu, uyarı yok. Takvimden seçim doğru çalışıyor. Geçerlilik tarihleri GxP kaydı olduğu için sessiz kayma riskli:
tanınmayan girişte alan boşaltılmalı ya da hata göstermeli.

---

### BL-392

**`platform.tasks.work-report.read-tenant-wide` yalnız-açık-yetki listesinde değil — modül yetkilendirmesiyle varsayılan rollere dağılabilir (karar)**

DURUM: KAPANDI (yeni otomatik atamalar) — `aa96b147` (`feature/pss/mod-0024-review-meeting-policy`; CT sabotajla doğruladı, 2026-09-14). AÇIK KALAN — SAHİP KARARI: bugün bu izni otomatik tutan rolleri "açıkça verilmiş" hale çevirmek API ile mümkün değil (System/Module satırı geri alınamıyor, elle atama tekil indekse takılıyor) → veri adımı: tutulacak satırlarda GrantSource=Manual, kalanları sil + kiracı rol-atama sürümünü artır + sahiplerin refresh token'larını iptal et. Sahipler için salt okunur sorgu kontrol listesinde (§5) · BULAN: PSS ajanı · KAYIT: 2026-09-13 · **Sahip kararı 2026-09-15:** (b) — canlıya geçişte önce salt okunur sorguyla sahipler listelenir, kiracı yöneticisi tek tek onaylar, sonra veri adımı

`ExplicitGrantOnlyPermissions.Keys` bu WP'den önce yalnız iki anahtar taşıyordu (`ppm.portfolios.assign-owner`,
`auth.users.account-kind.manage`); BL-349 üçüncüsü olarak `platform.tasks.read-all`'ı ekledi. İş Raporu'nun kiracı geneli okuma anahtarı
manifest eylemi olarak modül yetkilendirme senkronuna giriyor, yani Görev Motoru yetkilendirilen kiracıda Admin rolüne kendiliğinden
düşebilir. Listeye eklemek, bu yetkiyi bugün tutan rollerden geri alır — bu yüzden sessizce yapılmadı. Karar: listeye alınsın mı, alınırsa
mevcut atamalar nasıl ele alınsın.

---

### BL-395

**Paylaşılan Platform test veritabanı eşzamanlı koşularda siliniyor — aynı makinede iki test koşusu birbirine sahte kırmızı veriyor**

DURUM: KAPANDI — `8e0e8ca7` (altyapı dalı, 2026-09-15), görev motoruna `f0a1456d`, toplantıya `b3c14be2` · BULAN: toplantı düzeltmeleri ajanı ("collection dropped", 7 geçici kırmızı), CT ölçtü · KAYIT: 2026-09-14

`MongoResidueSweeper` (`Persistence/MongoResidueSweeper.cs`) önceki koşudan kalan `diten_platform_itest` önekli veritabanlarını düşürüyor;
`MongoIntegrationHarness` da dispose'ta kendi veritabanını düşürüyor. Aynı makinede iki worktree ya da iki ajan Platform testlerini aynı anda
koşunca biri diğerinin veritabanını silebiliyor → testler rastgele kırmızı. Tekrar koşuda kaybolan kırmızı kod hatası sanılmamalı.
Geçici kural: Mongo'lu Platform test koşuları aynı makinede SIRAYLA. Kalıcı çözüm: koşu başına benzersiz önek ya da sahiplik işareti
(İş Referans Verisi temizleyicisinin işaret deseni) — sweeper yalnız kendi koşusunun izini düşürsün.

**2026-09-15 CT ölçümü — sebep büyük olasılıkla temizleyici değil.** `MongoResidueSweeper` zaten harness işareti + farklı RunId + 1 saat bayatlık şartı arıyor. Asıl yarış: `MongoIntegrationHarness` kapsamlı veritabanlarını SABİT adla (`diten_platform_itest_<scope>`, DB-010 gereği) `emptyFirst: true` ile açıyor; ikinci koşu, birincinin kullandığı veritabanını açılışta boşaltıyor. Koşu başına ad DB-010'u ve `MongoTestDatabaseGuardTests`'i bozacağı için çözüm yönü değişti: makine genelinde özel dosya kilidi (sabit yol, TMPDIR'den bağımsız); ikinci test süreci birincinin bitmesini bekler.

**Kapanış (2026-09-15).** `Persistence/PlatformMongoTestLock.cs`: `/tmp/diten-platform-itest.lock` üzerinde işletim sistemi dosya kilidi (macOS'ta ölçüldü: gerçek flock, SIGKILL'de bırakılıyor). Paylaşılan mongod'daki sabit adlı veritabanına dokunan her yol kilidi süreç başına bir kez alır ve süreç bitene kadar tutar: harness, şema sözleşmesi, iş akışı kapısı, İş Referans Verisi temizleyicisi ve harness'ları. Bekleyen süreç 30 sn'de bir kimin tuttuğunu yazar; 30 dk sonra test düşer, kilitsiz asla koşmaz; dosya kilitleme kapalıysa (`DOTNET_SYSTEM_IO_DISABLEFILELOCKING`) reddeder. Bir koruma testi 27017 adresi olup kilidi almayan dosyayı adıyla kırmızı verir. Ölçüm: yıkıcı filtre iki süreçte aynı anda — önce 17 ve 16 yarış kaynaklı ek kırmızı, sonra 0. Ajan sabotajı (kilit çağrısı kaldırıldı) 3/4 kırmızı; CT sabotajı (dosya paylaşımlı açıldı, kilit var gibi görünüp kilitlemiyor) iki süreç testi kırmızı → bayt bayt geri → 4/4 yeşil. **Kalanlar:** (1) Eventing.Tests (`_eventing_golden_flow`, `_eventing_failure_path`, `_tenant_lifecycle`; RabbitMQ yoksa atlanıyor) ve BackgroundJobs.Tests (`diten_platform_itest_container_validation`) sabit adları kilide bağlı değil — ayrı test projeleri. (2) Bekleme satırları varsayılan `dotnet test` ayrıntısında görünmüyor, `--logger "console;verbosity=normal"` ile görünüyor; zaman aşımı her ayrıntıda test hatası olarak görünür. (3) Linux ve Windows ölçülmedi. (4) Kilit yalnız kilidi taşıyan dallarda çalışır: zincire birleşmemiş kulvar dalları birleşene kadar eski davranışta.

---

### BL-396

**Toplantı raporu / aksiyon kaydı yok — toplantılar arası izleme ve dışa aktarma**

DURUM: KAPANDI (kod; canlı kontrol bekliyor) — `6a62eec6`, toplantı zincirine `340be2e7` (2026-09-15). CT sabotajı: gecikme saati ileri çekilince AC4 kırmızı → geri → 308/308 yeşil. Dışa aktarma ibaresi 7 dilde ekran ve dosyada tek terime indi (`bc0efda8`, zincirde `03cdafbd`: "kontrollü kopya değildir; yalnız bilgi amaçlıdır"); **süreç notu:** CT'nin ilk doğrulama betiğinde sabotaj anahtarı iki yerde eşleştiği için uygulanmadı ve betik durmadan commit'ledi — sabotaj commit sonrası satır bazında yeniden yapıldı (İngilizce dosya satırı "controlled document" → Platform 2 + vitest 2 kırmızı → geri → yeşil). Sözcükler Kalite onayı ve 5 dilin anadil gözden geçirmesini bekliyor; verify_datatable_page.py CRUD sayfaları içindir, salt okunur rapor kapsam dışı (CT kararı) · önceki: PAKET READY-FOR-DEV (MOD-0357 §23, sahip kararları 2026-09-15: izin A · "şu an taşındığı" sütunu evet · yalnız yayınlanmış tutanak · DataTable · dışa aktarılan dosya "o anın görüntüsü" [Kalite teyidi bekliyor] · ortak denetim yazıcısı altyapı CT) · önkoşul BL-347 yazıcısı hazır (`5681eaac`) · uygulama WP-MG-MOD0357-S12-MEETING-REPORT-01 (2026-09-15) · SAHİP KARARI: 2026-09-14 · KAYIT: 2026-09-14

Bütün dallarda ölçüldü: toplantılar için rapor ekranı ya da dışa aktarma ucu yok; toplantı başına kayıt tutanak. **Karar:** içerik = dönem/tür/
düzenleyen filtreli toplantı listesi, katılım oranı, kararlar, toplantılardan doğan açık ve geciken aksiyonlar (Blueprint "Follow-up Register";
ISO 9001 §9.3.3; QMS araçlarının aksiyon kaydı) · görünürlük = toplantı kuralı (düzenleyen + katılımcı + read-all) · dışa aktarmada denetim izi
ŞART → BL-347 ile aynı kiracı tarafı denetim yazıcısı kararına bağlı. Sıra: MOD-0357 paket dilimi (module-pack-author) → sahip onayı → uygulama.

---

### BL-397

**14 ekranda doğrudan tarih seçici: yanlış biçimde yazılan tarih hâlâ sessizce başka tarihe dönüşebilir**

DURUM: AÇIK · BULAN: toplantı düzeltmeleri ajanı (BL-391 kalanı) · KAYIT: 2026-09-14

BL-391 yalnız paylaşılan `diten-datefield.js` ve Pozisyon Ataması formunu düzeltti. `allowInput` ile doğrudan flatpickr kuran ekranlar dokunulmadı: CRM/ContentEngagementJourneys, CRM/Knowledge, CRM/KnowledgeConcepts, CRM/KnowledgePaths, CRM/Segments, MasterData/LegalEntities (index + wizard), Organization/OrganizationUnits/form.js, Organization/Positions/form.js, Platform/Tenants/details.js, demand-ideas (capture + list), enterprise-strategy (esbp-horizon-dates, project-ppm-form-init). Öneri: bu ekranları paylaşılan alana taşımak ya da aynı `guardAgainstSilentMisparse` korumasını bağlamak; her ekran sahibinin kulvarında.

---

### BL-398

**21 ekran sunucu doğrulama hatasında ham ProblemDetails JSON'unu sayfaya basıyor**

DURUM: KISMEN KAPANDI — görev ekranları `fddc01a6` (TaskFieldDefinitions, TaskTypes, TaskChecklistTemplates, TaskRecurrenceRules, TaskTemplates tek paylaşılan okuyucuda; CT sabotajı 10 kırmızı → geri → yeşil). Kalan 17 ekran (CRM, Roles, Platform) sahip kulvarlarında · önceki: AÇIK · BULAN: PSS ajanı (BL-388 kalanı) · KAYIT: 2026-09-14

BL-388 yalnız `TaskFieldDefinitionsController`'ı düzeltti. Aynı ham gövde düşüşü: TaskTypes, TaskChecklistTemplates, TaskRecurrenceRules, TaskTemplates, OrganizationFieldDefinitions, GoldenReferenceCompact, GoldenReferenceSlim, Roles, Platform/ModuleCatalog, Platform/SubscriptionPlans ve 11 CRM controller'ı. Ortak yardımcı yok; `UsersController`'ın kendi ayrıştırıcısı var. Ek not: alan mesajları sunucunun İngilizce teknik metniyle geliyor (ör. JSON dönüşüm hatası); yerelleştirmek çeviri kapısından geçer. Öneri: tek paylaşılan hata çıkarıcı + kardeşleri ona bağlamak.

---

### BL-399

**Görev okuma kuralı her çağrıda bütün ilişki bacaklarını hesaplıyor — etiketlemede kişi başına tekrar**

DURUM: KAPANDI — `fddc01a6`: @ doğrulaması okuma bacaklarını yazma başına bir kez çözüyor (10 kişi: 11 okuma → 2); okuma kuralının anlamı değişmedi · önceki: AÇIK (verimlilik, engel değil) · BULAN: CT (@ ile etiketleme doğrulaması) · KAYIT: 2026-09-14

`TaskReadAccessPolicy.CanReadAsync` artık `ResolveDataLegCandidatesAsync` ile havuz sahiplerini, izleyicileri ve üst görevi her seferinde çözüyor (önceden ilk eşleşmede duruyordu). Görev detayında birkaç ek sorgu; @ etiketleme doğrulaması bunu etiketlenen her kişi için (en çok 10) tekrarlıyor → ~30 sorgu. Öneri: adaylar görev başına bir kez hesaplanıp kişiler o kümede aranır; kapsam/read-all bacakları yalnız çağıran için ayrıca.

---

### BL-400

**@ ile etiketleme: var olan yorumu düzenlerken etiket ekleme ekranı yok, eski /Tasks/Details'te etiketleme yok**

DURUM: KAPANDI (Görev Merkezi) — `fddc01a6`: yorum düzenlerken etiketler dolu gelir, aynı seçici kullanılır. Eski /Tasks/{id} sayfasında yorum arayüzü hiç yok; etiketleme bildiriminin o sayfaya götürmesi → BL-414 · önceki: AÇIK · BULAN: PSS ajanı (WP-PSS-MOD0024-TASK-MENTIONS-01) · KAYIT: 2026-09-14

Arka uç her ikisini destekliyor (`UpdateTaskCommentRequest.MentionedUserIds`, yalnız yeni eklenene bildirim). Görev Merkezi'nin yorum düzenleme penceresi seçiciyle genişletilmedi; eski /Tasks/Details ekranı dilime alınmadı. MOD-0024 paketi §21'de işaretli.

---

### BL-401

**Kiracının ilk tüzel kişiliği aktifleşince kök organizasyon birimi otomatik oluşsun**

DURUM: AÇIK (sahip kararı: sonra) · BULAN: CT (BL-366 seçenek 1) · KAYIT: 2026-09-14

BL-366 kararı: şimdi kurulumda elle adım, sonra otomatik. İhtiyaç: MDM'de tüzel kişilik aktifleşme olayı (bugün MDM `TenantCreatedV1` dinlemiyor, tüzel kişilik Taslak açılıp elle aktifleşiyor) + Platform'da tüketici; kiracı kapsamında kök birim, tüketilen-olay deposu ve (TenantId, Code) tekil indeksiyle idempotent. Çok servisli iş (MDM + Platform); ayrı paket/prompt.

---

### BL-402

**Bilinmeyen alan toleransı yalnız Platform'da — Auth, MDM, HCM ve PPM'de durum farklı**

DURUM: AÇIK · BULAN: altyapı ajanı (BL-384 takibi) · KAYIT: 2026-09-14

BL-384 Platform'a süreç genelinde `IgnoreExtraElementsConvention` ekledi (`4663682c`). Ölçülen: Auth aynı convention'ı zaten kaydediyor (`Diten.AuthService.Persistence/DependencyInjection.cs:37-41`, testi ölçülmedi) · MDM yalnız BrandProduct class map'lerinde (`BrandProductClassMaps.cs:36,47,57`) · HCM'de yok · PPM bilerek katı (`PpmBsonConfiguration.cs:30-32`). Her servisin geri alma riski ayrı değerlendirilmeli; PPM'nin katılığı bilinçli karar mı teyit edilmeli.

---

### BL-403

**Dev kiracısında görev rolleri: Task-Manager'ın hiç izni yok, Task-User'ın izinleri görevle ilgisiz**

DURUM: KAPANDI (ölçüldü; kod hatası değil, dev test verisi) — canlıyı ilgilendiren boşluklar BL-410 ve BL-411'e ayrıldı (2026-09-15) · BULAN: CT canlı tur (2026-09-13) · KAYIT: 2026-09-14

Ölçüm (`diten_auth_v3`): Task-Manager rolüne bağlı izin satırı 0; Task-User'da yalnız `ppm.benefit-commitments.change-lifecycle`, `mdm.brands.create`, `auth.roles.create`. Canlı tur için CT Task-Manager'a toplantı okuma + görev izinlerini elle verdi. Soru: bu roller hangi tohum/şablondan geliyor, canlı kiracılarda aynı boşluk var mı? Varsa rol şablonu düzeltilmeli.

**Kapanış (2026-09-15, CT alt ajanı, salt okuma).** İki rolü hiçbir tohum, şablon ya da eşitleme açmadı: `admin@diten.com` 2026-09-08 11:29–11:30'da `POST api/roles` ile elle oluşturdu (Auth denetim günlüğü), aynı gün 8 dev kullanıcıya atadı. Task-User'daki üç ilgisiz anahtar aynı gün 12:36–13:53 arası yapılan elle atama/geri alma denemelerinden kalma (`GrantSource: Manual`); Task-Manager'da CT'nin 2026-09-13 atamalarına kadar izin yoktu. Eşitleme kodla elendi: yalnız Admin/Viewer ve ürün kısaltmalı rolleri hedefliyor. Yeni kiracıya bu roller hiç kurulmuyor, yani aynı boşluk canlıda tekrarlamaz. Asıl canlı boşluklar: görev/toplantı modüllerinin hiçbir planda olmaması ve Viewer'ın Görev Merkezi anahtarını alamaması → BL-410; iki şablon anahtarının "yalnız platform" kapsamı → BL-411. Yan bulgular: Auth'ta izin atamaları `AssignedBy: "System"` yazıyor → BL-412; dev'de `c9b39e99` kiracısında 65 rol-izin satırı var ama rol ve kullanıcı yok (dev veri artığı, dokunulmadı).

---

### BL-404

**Dev'de /health Unhealthy — `business_reference_data_provider` kontrolü**

DURUM: AÇIK — sebep ölçüldü (2026-09-15); kod düzeltmesi İş Referans Verisi kulvarında (MOD-0048-FU01 paket revizyonu gerekir), ops adımı canlı kontrol listesi §7/11'de · BULAN: CT (2026-09-13 dev yığını) · KAYIT: 2026-09-14

**2026-09-15 kıyas (WP-CT-DECISION-BENCHMARK-01; Microsoft ASP.NET Core health checks, Kubernetes probes, OCI load balancer, SAP Cloud ALM):** trafik yönlendirme HAZIRLIK ucuna, yeniden başlatma CANLILIK ucuna bakar. Bu yüzden hedef: İş Referans Verisi kontrolü "yapılandırılmamış" durumda **Degraded (200)** döner (paketin kendisi `:1080-1084` hostun diğer her şeyi sunmaya devam ettiğini söylüyor; sağlayıcı uçları yine 503), ardından dengeleyici `/health/ready`'ye bakar; `/health/live` yalnız yeniden başlatma yoklaması. Dengeleyiciyi `/health/live`'a bağlamak ancak belgelenmiş GEÇİCİ adım olabilir (Mongo düşmüş örneğe trafik gitmeye devam eder). Önkoşul: MOD-0048-FU01 paket revizyonu (İş Referans Verisi kulvarı).

`/health` 503; tek kırmızı kontrol `business_reference_data_provider` ("configuration or state is invalid"). Diğerleri (mongodb, rabbitmq, hangfire_storage, masstransit-bus) sağlıklı. Toplantı/görev turundan önce de böyleydi. Canlıda aynı kontrolün değeri okunmalı; yük dengeleyici sağlık kontrolü bu uca bakıyorsa servis dışı sayılabilir.

**2026-09-15 ölçümü (CT alt ajanı, CT satırları doğruladı).** `BusinessReferenceDataProviderReadinessHealthCheck` önce `GetRequiredReferenceTenantId()` çağırıyor; `BusinessReferenceData:Provider:ReferenceTenantId` dev'de hiçbir yerde yok (hiçbir dalda hiç ayarlanmamış) → `REFERENCE_PROVIDER_CONFIGURATION_INVALID` → Unhealthy. `0f71a237`'nin eklediği "pilot yapılandırılmamışsa sağlıklı" erken dönüşü bu çağrıdan SONRA; eksik ayarda hiç çalışmıyor. Birim testi çözümleyiciyi taklit edip GUID döndürdüğü için dev'deki durumu hiç sınamıyor — sabotaj kanıtı olmayan koruma. Paket (MOD-0048-FU01 `:1335`) eksik ayarda Unhealthy'yi tasarım olarak istiyor ve `CatalogLoad:TenantId`'ye düşmeyi yasaklıyor; "yalnız ready etiketi" isteği de `/health` filtresiz eşlendiği için korumuyor. Depoda dağıtım yapılandırması yok; belgeler dengeleyici için `/health/live` diyor, gerçek canlı ayarı bilinmiyor. Öneri: (A) dev'de ayarı açıkça ver; (B) canlı ortama ekle ya da dengeleyicinin `/health/live` kullandığını teyit et (kontrol listesi §7/11); (C) İş Referans Verisi kulvarı: yapılandırılmamış sağlayıcıda Degraded (200) ya da readiness dışı etiket + gerçek çözümleyiciyle test — paket revizyonu ister.

---

### BL-405

**CI'da koşmayan eski kırmızı testler — İş Referans Verisi 53, Doküman Yönetimi 15, Auth 3, ön yüz 25**

DURUM: KISMEN KAPANDI — Auth'un 3 kırmızısı `d9d7b90e` (2026-09-15): izin kapsamı referans dosyasına 11 crm.knowledge satırı (birleştirme sırası; kapsam doğru), kullanıcı referans doğrulama cevabı onaylı CAND-CAP-0001 §6 haline döndü (maskeli ad/e-posta kaldırıldı; CRM etiketi → BL-416). Auth Application 849/849. CT sabotajı: referans dosyasında crm.knowledge.read platform kapsamı → kırmızı → geri → yeşil. Kalan: İş Referans Verisi Mongo (replica set Timestamp), Doküman Yönetimi (DM kulvarında düzeltildi, dördüncü PR), ön yüz 25 · önceki: AÇIK (borç) · BULAN: CT (go-live öncesi tam paket karşılaştırması, temiz main `e5681231`) · KAYIT: 2026-09-14

`run_phase1_gates.sh` tam Platform/Auth paketlerini ve vitest'i koşmuyor. Temiz main'de kırmızılar: Platform 68 (İş Referans Verisi Mongo 53 — çoğu yerel replica set/harness gerektiriyor; Doküman Yönetimi 15 — DM kulvarının birleşmemiş dalında düzeltilmiş), Auth 3 (`PermissionScopePreservationTests.Baseline…`, `UserLookupValidationContractTests` ×2), vitest 25 test / 13 dosya (CRM campaign/consent, dialog-one-implementation, diten-tags, global-confirm-input-type, objectives, planning-cycles ×2, pvg-case-intake, strategy ×3, wcn-dialog-one-language). Karşılaştırma listeleri: CT scratchpad. **2026-09-15 (BL-395 ajanı):** İş Referans Verisi'nin 49 Mongo testi tek süreçte de kırmızı; sebep ölçüldü: yerel mongod bir replica set (`rs0`) ve `RunCommandAsync<object>("{ ping: 1 }")` cevaptaki Timestamp türünü `ObjectSerializer` ile okuyamıyor (GSKU `:363`, TenantAssignment `:28`, PublishOperation `:29`); ardından dispose "database is currently being dropped" ile düşüyor. İş Referans Verisi kulvarının işi. Öneri: sahipli kulvarlara dağıtmak; yeşillenen paketleri kapıya eklemek.

**Ek 2026-10-04 (WP-MDM-GSKU-ACCEPT-01 FIX1 ve WP-EMAIL-SHELL-01 FIX1 raporları):** (1) kapı MDM çözümünü yalnız DERLİYOR, MDM testlerinin
hiçbirini koşmuyor (Application.Tests ve yeni `Diten.MdmService.Api.Tests` dahil); koşturmak runner'da mongod ister (BL-527'nin geçici mongod
düzeniyle birlikte çözülür). (2) `Diten.BuildingBlocks.Email.Tests` `Diten.Platform.sln`'e girdi, kapıda derleniyor ama koşmuyor (86 test,
20 ms: kapıya eklemek ucuz). (3) Auth Application'da 59 test macOS'ta hiç koşamıyor: 48'i `OperationalMongoFixture`'daki Windows'a sabit
`C:\Program Files\MongoDB\…\mongod.exe` yolu, 9'u Windows API (6 adlı semafor, 2 kernel32, 1 boru), 2'si açık test URI'si istiyor. Bu kırmızılar
her teslimde "ortam kaynaklı" diye geçiyor ve gerçek bir kırmızıyı gizleyebilir. Düzeltme: mongod yolu `DITEN_TEST_MONGOD` / PATH'ten,
Windows'a bağlı testler `[SkippableFact]` + işletim sistemi koşulu.

---

### BL-406

**Tekrar denemeleri biten e-posta kalıcı başarısız kalıyor — kimseye söylenmiyor**

DURUM: KAPANDI (kod; canlı kontrol bekliyor) — `a9c40ee0` (S9 ile aynı commit), toplantı zincirine `c2126222` (2026-09-15). Son denemede kalıcı başarısız olan toplantı postası düzenleyene tek uygulama içi bildirim + katılımcı satırında "posta iletilemedi" (7 dil); toplantı postaları artık alıcı başına ayrı gönderiliyor. **Test boşluğu (CT sabotajı):** bildirimi her başarısız denemede gönderen değişiklik 58 testin hiçbirini kırmızıya çevirmedi — kapandı `3c420ced`, zincirde `ab194b71`: son deneme olmayan başarısızlık kimseye bildirim yazmıyor; MaxRetryCount 5 ve 2 ile yalnız sınıra ulaşan deneme tek bildirim üretiyor (kalıcı başarısızlık 1 ilk gönderim + 5 tekrar = 6. gönderim). Ajan sabotajı (bir deneme erken) ve CT sabotajı (bir deneme geç, 4 kırmızı) → geri → 518/518 yeşil · önceki: AÇIK · BULAN: CT canlı tur (Ali'nin daveti 5 denemede durdu) · KAYIT: 2026-09-14

`EmailDispatchSweepJob` `MaxRetryCount` (5) dolunca satırı bir daha seçmiyor; satır `Failed` kalıyor. Düzenleyen davetin hiç ulaşmadığını bilmiyor, ekranda iz yok. Öneri: son denemede düzenleyene uygulama içi bildirim ya da toplantı detayında "davet iletilemedi" durumu; ops için kalıcı başarısız dispatch sayısı metriği.

---

### BL-407

**Görev Merkezi'nde kullanılmayan `ActReviewMeeting` metin anahtarı**

DURUM: KAPANDI (geçersiz) — ölçüm: `ActReviewMeeting` Görev Merkezi örnek kartlarında (`fixtures/inbox-showcase-fixtures.js:39,58,76`) kullanılıyor; silinirse o satırlarda ham anahtar görünür. Değişiklik yapılmadı · önceki: AÇIK (küçük temizlik) · BULAN: toplantı düzeltmeleri ajanı · KAYIT: 2026-09-14

`WorkCenterNextIndex.*.resx` içindeki `ActReviewMeeting` app.js'te hiç referanslı değil (eylem etiketi `WorkAggregation_Action_ScheduleReviewMeeting`'den geliyor). BL-389'da yalnız metni düzeltildi. 7 dilde silinmesi çeviri kapısından geçer.

---

### BL-408

**CI kapısının "veritabanları arası erişim" adımı `rg` yoksa hiçbir şey denetlemeden "passed" diyor**

DURUM: KAPANDI — `8e0e8ca7` (BL-395 ile aynı commit) · BULAN: PSS ajanı (BL-392 kapı koşusu) · KAYIT: 2026-09-14

`scripts/check_cross_db_enforcement.sh:7` aramayı `rg` ile yapıyor ve hatayı `|| true` ile yutuyor. `rg` kurulu olmayan makinede (yerel dev makinesi ölçüldü: `rg: command not found`) adım hiçbir dosyayı taramadan geçiyor. Ajan aynı denetimi grep ile koştu: 0 ihlal. GitHub `ubuntu-latest` imajında `rg` olup olmadığı ölçülmedi. Öneri: araç yoksa adım başarısız olsun ya da grep'e düşsün; `|| true` yalnız "eşleşme yok" çıkış kodunu yutsun.

**Kapanış (2026-09-15).** Ölçüm: GitHub ubuntu-24.04 imajının araç listesinde ripgrep YOK, iş akışı yalnız jq kuruyor — yani CI'da da adım hiçbir şey taramıyordu. Düzeltme: `rg` varsa o, yoksa aynı kapsamda `grep -E -r` (obj/bin, gizli klasörler ve ikili dosyalar hariç; tüm ağaçta iki araç birebir aynı 60 satırı buldu). Yalnız çıkış 1 ("eşleşme yok") geçer; araç yok (127) ya da hata (2) adımı düşürür. Kanıt: temiz ağaç iki modda geçiyor; eklenen ihlal iki modda kırmızı; CT sabotajında tarayıcı komutu bozulunca "could not scan … 127" ile düştü. GNU grep yerelde koşulmadı.

---

### BL-409

**Genel komut denetim hattı her kaydı "Sistem" aktörüyle yazıyor — kiracı kullanıcısının yaptığı değişiklik kimin türüyle kaydedildiğini söylemiyor**

DURUM: KAPANDI — `94985da5`, toplantı zincirine `cf2f03cf` (2026-09-15). CT sabotajı: oturumsuz komut Bilinmiyor'a çözülünce iki test kırmızı → geri → 278/278 yeşil. Kalan: aktör türü taşımayan jetonun kiracı ara katmanından geçmesi → BL-413 (yapılıyor) · önceki: AÇIK · BULAN: BL-347 ajanı (WP-PSS-MOD0024-BL347-TENANT-AUDIT-WRITER-01), CT · KAYIT: 2026-09-15

`Contracts/Behaviors/AuditBehavior.cs` `IAuditableCommand` hattındaki her kaydı isteğin varsayılan aktör türüyle (`AuditActorType.System`) yazıyor; jetondaki `actor_type` okunmuyor. Kullanıcı kimliği kayıtta var, ama "bunu bir kiracı kullanıcısı mı, platform yöneticisi mi, sistem işi mi yaptı" sorusunun cevabı yanlış. GxP denetim izinde aktör türü ayırt edici bilgi. Öneri: BL-347'nin `ResolveActorType` eşlemesi tek bir paylaşılan çözümleyiciye çıkarılır; `AuditBehavior` ve `DataExportAuditWriter` onu kullanır; gerçekten arka plan işi olan komutlar `System` kalır. Etki: 62 denetlenen komut; denetim ekranı ve dışa aktarma aktör türünü gösteriyorsa görünen değer değişir (ölçülmeli). Çeviri/ekran işi yok; altyapı CT alt ajanı, BL-395 kilidi birleştikten sonra.

---

### BL-410

**Görev ve toplantı modülleri hiçbir abonelik planında yok, Viewer Görev Merkezi'ni açamıyor — yeni kiracıda kimse bu ekranları kullanamaz**

DURUM: KAPANDI — sahip kararı "evet" (2026-09-15); uygulama `147f0ae0`, toplantı zincirinde `ed717b55`, main eşitlemesinden sonra `89de80a3` (2026-09-16). Görev Merkezi artık temel (baseline) modül ve sayfası izin anahtarı istemiyor; menüde her kiracı kullanıcısına görünüyor. `mine` ve `team-availability` yalnız oturum ister, tek görev okuması yalnız `platform.tasks.read` ister, anahtar bildirmeyen eylem reddedilir, "+ Yeni ▸ Görev" `platform.tasks.create` olmadan gizlenir. `inbox.view` katalogda kalır ama hiçbir uç onu sormaz. CT sabotajı: 5 sabotaj, 5 kırmızı · ÖNCEKİ: AÇIK — sahip kararı bekliyor · BULAN: CT alt ajanı (BL-403 incelemesi), CT doğruladı · KAYIT: 2026-09-15

**2026-09-15 kıyas (WP-CT-DECISION-BENCHMARK-01):** Blueprint MOD-0024/MOD-0023 "Platform Workflow Service / Platform Backbone"; DCP-004 `:80` ve `:270-272` Görev Merkezi'ni SAP Task Center ve Oracle Worklist örneğinde her kişinin tek iş yüzeyi olarak tanımlıyor. SAP Task Center `TaskCenterEveryone` rol koleksiyonu, Oracle "Employee" soyut rolü + global başlıktaki bildirim listesi: gelen kutusu HER kullanıcıda; görev oluşturan/yapılandıran yetenekler lisanslı uygulamayla gelir. **CT önerisi güncellendi:** bu tur canlı kiracıya dört modülün açık yetkilendirmesi (değişmedi); kalıcı çözüm "yalnız Viewer istisnası" değil, Görev Merkezi gelen kutusu `view` anahtarının her kiracı kullanıcısına verilen adlandırılmış bir temel izin kümesi (Tenant Settings gibi baseline). Gelen kutusunun kendi yazma eylemi yok; bir koruma testi bunu sabitlemeli. DCP-004 `:191`'deki EA kararını (erişim yetkilendirmeyle) tersine çevirdiği için sahip/EA kararı gerekir. **SAHİP KARARI 2026-09-15: EVET** — Görev Merkezi gelen kutusu her kiracı kullanıcısına açılır (DCP-004 `:191` EA kararı bu yönde değişir). Görev/toplantı oluşturma ve ayar yetkileri modül yetkilendirmesinde kalır; ekip görünümü (astların işi) herkese açılmaz. Uygulama tasarımı ölçülüyor (WP-WCN-INBOX-FOR-EVERY-TENANT-USER-DESIGN-01).

**Tasarım ölçümü (2026-09-15, salt okuma):** gelen kutusu hiçbir yazma yetkisi vermiyor — her eylem kaynak modülün kendi anahtarı ve ilişki kuralıyla yeniden denetleniyor (görev, iş akışı onayı, toplantı daveti, uzak sağlayıcı). Bugün `inbox.view` yalnız Admin'e eşitlemeyle gidiyor; Viewer (`read` kuralı), özel roller ve rolsüz kullanıcılar hiç almıyor; her kiracı girişi `/WorkCenterNext`'e düştüğü için bu kullanıcılar yetkisiz kartına iniyor. Yetkilendirme API'de güvenlik duvarı değil (HasPermission yetkilendirmeye bakmıyor), yalnız menü ve eşitleme hedefini belirliyor. **Önerilen (O1 + O3b):** kişisel gelen kutusu ve tek görev okuması yalnız oturum açmış kiracı kullanıcısı ister (emsal `MyNotificationsController`); modül `IsBaseline`, sayfa anahtarsız → menü ve Ctrl+K herkese; oturum-yalnız uçlar adlandırılmış bir öznitelik + yansıma testiyle listelenir; "+ Yeni ▸ Görev" `tasks.create` yoksa gizlenir (UAS-001 §6); boş izin anahtarı döndüren dağıtıcıyı yakalayan koruma testi (`WorkItemsController.cs:190-192` deliği). Önkoşul: DCP-004 dilimi/revizyonu (demir kural 9), BL-414'ten sonra. **Açık karar — ekip görünümü:** bugün `inbox.view` + astı olan herkes astların tüm görev satırlarını görüyor (ayrı anahtar yok). Gelen kutusu herkese açılınca bu her yöneticiye açılır. Seçenekler: (i) ayrı `team.view` anahtarı (açık verilir), (ii) organizasyon ilişkisine bağlı otomatik (astı olan yönetici görür; Oracle HCM "Line Manager" rolünün astı olanlara otomatik atanması, SAP SuccessFactors'ta ilişkiye göre dinamik yönetici grubu). BL-417 (okuma kuralında ast bacağı) aynı karara bağlı. **SAHİP KARARI 2026-09-15: (ii)** — ekip görünümü organizasyon ilişkisine bağlı otomatik (astı olan yönetici görür, ayrı anahtar yok, tüzel kişilik sınırı yok); okuma kuralına ast bacağı eklenir (BL-417 a). Karar DCP-004 sonuna işlendi; uygulama WP-WCN-INBOX-FOR-EVERY-TENANT-USER-01.

Ölçüldü (dev): yeni kiracıya yalnız Admin ve Viewer kuruluyor (`RoleProvisioningService.cs:11-15`). Admin'in hazır listesinde (`DefaultRolePermissionTemplate.AdminModules`) `platform.tasks.*`/`platform.meetings.*` yok; dev Admin 29 görev/toplantı anahtarının hiçbirini tutmuyor. TASKS, MEETINGS, WORK-AGGREGATION ve WORK-REPORT `IsTenantAssignable=true`, `IsBaseline=false` ve beş planın hiçbirinde yok; plan hiç vermez, kiracı başına açık yetkilendirme gerekir. Viewer yalnız `read` eylemli anahtarları alıyor; `platform.work-aggregation.inbox.view` `view` eylemli olduğu için Viewer (ve eşitleme yoluyla da) Görev Merkezi gelen kutusunu açamıyor. Dev varsayılan kiracıda WORK-AGGREGATION yetkilendirmesi açık olduğu hâlde Admin/Viewer'da `inbox.view` yok — incelenmedi. Sahip kararları: (1) modüller planlara mı girer, yoksa canlı kiracıya açık yetkilendirme mi (öneri: bu turda açık yetkilendirme, kontrol listesi §5; plan kararı sonra); (2) `inbox.view` Viewer'a nasıl gider (öneri: Görev Merkezi anahtarı için eşitleme ve şablonda dar istisna; tüm `view` eylemlerini Viewer'a açmak geniş etkili, önce liste ölçülür). `AdminModules`'e eklemek sahibin "liste küratörlü kalsın" kararına ters.

---

### BL-411

**İki görev şablonu izni Auth kataloğunda "yalnız platform" kapsamında — kiracı rolüne atanamıyor**

DURUM: KAPANDI (kod; canlı kontrol bekliyor) — `17bce635` (Auth), zincirde `8498d25d` (2026-09-15). İzin listesiyle yalnız iki anahtar PlatformAdmin → Tenant düzeltiliyor (sunucu tarafı koşul, tekrar koşuda etkisiz, bir kez log). Sonraki eşitleme görev modülü açık kiracıların Admin rolüne bu iki anahtarı verir (testle sabit, Viewer almaz). Kök sebep → BL-419; eşitlemenin kapsamı denetlememesi → BL-418. Canlı kontrol: dev Auth yeniden başlayınca iki anahtar Scope 0 ve "BL-411 scope correction" log satırı bir kez · önceki: AÇIK — canlı katalog ölçümü + sahip kararı bekliyor · BULAN: CT alt ajanı, CT dev veritabanında doğruladı · KAYIT: 2026-09-15

**2026-09-15 kıyas:** Blueprint MOD-0024 sistem kaydı "Task templates, checklist templates"; varlıklar `TenantScopedEntity`; yollar kiracı yolu (kural §2c); SAP flexible workflow şablonları anahtar kullanıcılar tarafından, Oracle Fusion "Checklist Templates" müşterinin kurulum görevi. Öneri doğrulandı: kapsam 1 → 0 dar veri adımı. Önce ölçülecek: sonraki başlangıç eşitlemesinin kapsamı yeniden 1'e çevirip çevirmediği ve bu anahtarları hâlihazırda tutan roller.

`diten_auth_v3.permissions`: `platform.tasks.checklist-templates.manage` ve `platform.tasks.templates.manage` → `Scope: 1` (PlatformAdmin); diğer görev anahtarları `Scope: 0`. Sayfaları kiracı yollarında (`TaskManifestProvider.cs:292`, `:314`). Kapsamı platform olan anahtar hiçbir kiracı rolüne verilemez, elle atama 403 → kontrol listesi şablonları ve görev şablonları ekranları kiracıda kullanılamaz. Olası sebep (çıkarım): başlangıç işçisi anahtarları manifest kapsamı gelmeden kaydetti (`TaskManifestProvider.cs:9-13` tam bu uyarıyı taşıyor); Auth'ta kapsam düşürme yolu yok. Öneri: canlı kataloğu salt okunur ölç (kontrol listesi §5); aynıysa yalnız bu iki anahtar için Auth veri adımı (kapsam 1 → 0), yükseltme sınırına dokunduğu için dar tutulur ve sahip onayıyla. Altyapı CT kulvarı.

---

### BL-412

**Auth'ta role izin atama kaydı `AssignedBy: "System"` yazıyor — atamayı yapan kişi yalnız denetim günlüğünde**

DURUM: KAPANDI (izin atama, rol oluşturma, rol düzenleme) — `e7b5d956` (`feature/infra/auth-display-label`, 2026-09-15). Üç işleyici oturumdaki kişiyi `ICurrentUserAccessor`'dan yazıyor; kişi yoksa 401 ve yazma yok (tek çağıran RolesController, JWT + izin istiyor). Tohum, rol kurulumu, tam katalog ve eşitleme kendi sistem değerlerini yazıyor; yansıma koruması bu yolların kişiye ulaşamadığını gösteriyor. CT sabotajı: rol düzenleme güncelleyeni yazmayınca birim + HTTP testi kırmızı → geri → 120/120 yeşil. **Kalanlar:** (1) Kullanıcıya rol atama ve rol silme de kişiyi yazıyor — `db2ed920` (CT sabotajı: silme "system" yazınca birim + HTTP testi kırmızı → geri → 44/44 yeşil). Platform'dan API anahtarıyla gelen kiracı yöneticisi daveti ve platform yöneticisi kurulumu "system" yazıyor; tetikleyen platform yöneticisi Platform tarafında izlenmeli (ölçülmedi); `RegisterCommandHandler.cs:83` kendi kaydında "System" (anonim akış, ayrıca değerlendirilecek). (2) `RolePermissionRepository.RevokeAsync` satırı fiziksel siliyor ve filtrelemeden önce tüm kiracıların atamalarını belleğe alıyor. (3) Rol silme artık UpdatedBy yazıyor (`db2ed920`). Kendi kaydı `17bce635` ile kapandı: varsayılan rol satırı "system", kaydı açan kişi `tenant_user_self_registered` denetim olayında aktör (kıyas D6, 21 CFR Part 11 atfedilebilirlik). Olay-ya-da-hiçbiri sınırı → BL-420. CT sabotajı: olay hiç yazılmayınca 3 kırmızı → geri → 878/878. (4) DataSeeder'ın açtığı rollerde CreatedBy boş, sistem rolü upsert'ü "system" yazıyor · önceki: AÇIK · BULAN: CT alt ajanı (BL-403 incelemesi) · KAYIT: 2026-09-15

`AssignPermissionCommandHandler.cs:54` atayanı sabit `"System"` yazıyor; rol belgelerinde `CreatedBy` boş. Gerçek aktör yalnız `authAuditLogs`'ta. Rol-izin satırına bakan biri elle yapılmış atamayı sistem ataması sanır. Platform tarafındaki BL-409 ile aynı aile (aktörün yanlış kaydı), ama Auth servisinde. Öneri: işleyici oturumdaki kullanıcıyı yazsın, eşitleme ve tohum `System` kalsın; mevcut satırlar değişmez. Altyapı CT kulvarı, çeviri işi yok.

---

### BL-413

**Aktör türü taşımayan jeton kiracı yollarından geçiyor — o komut denetim kaydı bırakmıyor**

DURUM: KAPANDI — `84230a60` (görev motoru dalı, 2026-09-15). Kiracı yolları ve kiracı modu kişiselleştirme aynı kontrolü kullanıyor: eksik ya da boş aktör türü, oturum açmış kimlikte tanınmayan değerle aynı 403. Bu ara katmanı yalnız Platform.API kullanıyor (CRM, DevEnablement, MDM, Auth kendi ara katmanlarını kullanıyor). Jeton üreten tek yer Auth TokenService; her jeton aktör türü taşıyor. CT sabotajı: yalnız kişiselleştirme dalı eski koşula dönünce onun 4 testi kırmızı, kiracı dalı yeşil kaldı → geri → 42/42 yeşil. Not: gateway'in kendi ara katmanı da eksik aktör türünü geçiriyor; Platform artık kendisi reddettiği için risk kapandı, gateway ayrı iş · önceki: YAPILIYOR (CT alt ajanı, görev motoru dalı, 2026-09-15) · BULAN: BL-409 ajanı · KAYIT: 2026-09-15

BL-409 sonrası oturum açmış ama `actor_type` taşımayan kimlik Bilinmiyor'a çözülüyor; denetim servisi Bilinmiyor'u reddettiği için komut çalışıyor ama kayıt yazılmıyor (yalnız uyarı). Platform'un kiracı ara katmanı tanınmayan değeri 403 ile reddediyor, eksik olanı geçiriyor (HTTP ile ölçüldü). Bugün `actor_type`'sız jeton üreten gerçek kaynak yok. Düzeltme: eksik ya da boş değer de aynı şekilde reddedilir; önce tüm jeton kaynakları ölçülür.

---

### BL-414

**Görev bildirimleri eski /Tasks/{id} sayfasına götürüyor — o sayfada yorumlar hiç yok**

DURUM: KAPANDI (kod; canlı kontrol bekliyor) — `3c5eacb2`, toplantı zincirine `bf424e8c` (2026-09-15). Görev Merkezi detayı görev listede yoksa `GET api/v1/work-items/{id}` ile okuyor (okuma kuralı + listeyle aynı projeksiyon; eksik/başka kiracı/okunamaz tek 404; `inbox.view` + `tasks.read`). `TaskLinks.Detail` görev bildirimi ve toplantıdaki ilişkili görev bağlantısında; `TaskLinks.Record` (/Tasks/{id}) kayda çıkış kapısında kaldı. Ekibim'den astın görevi artık açılıyor (başka tüzel kişilikteki ast hariç → BL-417). CT sabotajı: `tasks.read` şartı kaldırılınca kırmızı → geri → Platform 2063, Web 189, vitest 40 yeşil · önceki: YENİDEN KAPSAMLANDI, YAPILIYOR (CT alt ajanı, `feature/mg/mod-0357-task-deeplinks`, 2026-09-15) · önceki: AÇIK — S9 düzeltmesi birleşince yapılacak (aynı sağlayıcı dosyası) · BULAN: PSS ajanı (WP-PSS-MOD0024-FOLLOWUPS-02), CT doğruladı · KAYIT: 2026-09-15

**2026-09-15 ölçüm (alt ajan durdu, kod yazmadan):** bağlantıları doğrudan Görev Merkezi detayına çevirmek hatalı olurdu. Detay sayfası görevi kimliğinden okumuyor; yalnız kişinin `mine` listesinde (atanan, havuz, açtığı) arıyor. İzleyici, üst görev/kapsam/tümünü okuma yetkilisi — etiketlenen kişilerin çoğu — "Görev bulunamadı" görürdü. `Source.DeepLink` (/Tasks/{id}) bilerek kayda çıkış kapısı ("Kaynak kayıtta aç", satır Düzenle); değişmemeli. Bugün tıklanabilir görev bildirimi yok: görev e-postalarında bağlantı yok, uygulama içi bildirimleri gösteren zil arayüzü yok (TargetUrl yalnız saklanıyor). **CT kararı (Blueprint + SAP/Oracle):** SAP My Inbox yalnız kişinin görevlerini listeler, izlenen/yetkili nesne kendi nesne sayfasında yetki denetimiyle açılır; Oracle bildirim bağlantıları kayıt sayfasına gider. Bu yüzden önce Görev Merkezi detayı okuma kuralıyla korunan tek görev okumasına kavuşur (liste sekmeleri, BL-016, değişmez), sonra bildirim ve toplantıdaki ilişkili görev bağlantısı detaya çevrilir; kayıt kapısı `/Tasks/{id}` kalır. Ayrıca ölçülecek: yönetici ekip görünümünden astının görevini açınca detay "bulunamadı" mı diyor.

`TaskNotificationService.TaskDeepLink` → `/Tasks/{taskId}` (etiketleme dahil tüm görev bildirimleri, uygulama içi ve e-posta); ayrıca `TaskWorkItemProvider.cs:757` DeepLink ve toplantıların ilişkili kayıt satırı (`TaskRelatedRecordResolver.cs:38`) aynı adresi veriyor. Eski `Views/Tasks/Details.cshtml` sayfasında yorum akışı ve yorum kutusu yok: "sizi bir yorumda etiketledi" bildirimine tıklayan kişi o yorumu göremiyor. Canlı yüzey Görev Merkezi detayı: `/WorkCenterNext/Details/{id}` (görev kimliğini doğrudan alıyor, app.js aynı adresi kullanıyor). Öneri: üç bağlantı Görev Merkezi detayına çevrilir; eski sayfa silinmez. Çeviri işi yok, alt ajan.

---

### BL-415

**Aktif bir görev türüne bağlı belge sonradan yürürlükten kalkarsa hiçbir şey olmuyor**

DURUM: AÇIK · BULAN: WP-CT-DECISION-BENCHMARK-01 · KAYIT: 2026-09-15

Kural 4 (DCP-005 Adım 3) yalnız aktifleştirme anında denetliyor; sözleşme (`dcp-005-effectiveness-contract-v2.md:135-140`) aktifleşme sonrası belge durum değişikliğini açıkça kapsam dışı bırakıyor. Örnek: "Kalibrasyon kontrolü" türü SOP-0042 v2 yürürlükteyken aktif edildi; SOP-0042 v2 emekliye ayrılıp v3 yürürlüğe girdiğinde tür eski sürüme bağlı ve aktif kalıyor, çalışanlar eski prosedürle görev açabiliyor. Kıyas: Veeva'da eğitim atamaları belge durumuna bağlı eylemle yeniden tetikleniyor; Oracle Agile'da değişiklik emri bağlı nesneleri etkiler listesine alıyor. Öneri: belge yaşam döngüsü olayında (Effective'ten çıkış) bağlı aktif türleri listeleyen bir kontrol (bildirim ya da "gözden geçirme gerekli" işareti); otomatik pasife alma sahip kararı. Kalite ile birlikte değerlendirilecek.

---

### BL-416

**CRM onay/tercih ekranı "kim oluşturdu" etiketini maskeli e-posta ile gösteriyor — Auth'un görünen ad ucuna geçmeli**

DURUM: AÇIK (CRM kulvarı) · BULAN: CT alt ajanı (WP-INFRA-AUTH-LONG-RED-TESTS-01) · KAYIT: 2026-09-15

`frontend/Diten.Web/Controllers/CRM/ConsentPreferencesController.cs:571-609` oluşturan/güncelleyen/arşivleyen kişinin etiketini üç adımda buluyor: `GET /api/users/{id}` (auth.users.read) → `lookup-validation` yanıtındaki `MaskedName`/`MaskedEmail` → GUID. Maskeli alanlar onaylı CAND-CAP-0001 §6'ya aykırı olarak `0f71a237`'de karar atfı olmadan eklenmişti; altyapı CT bu alanları kaldırıyor (sözleşme testleri `UserLookupValidationContractTests`). Etki: varsayılan Admin/Viewer adımı 1'de adı görüyor, değişiklik yok; `auth.users.read` olmayan özel rollerde etiket GUID'e düşer. Doğru kaynak: Auth'un kimlikten görünen ad okuması (WP-INFRA-AUTH-DISPLAY-LABEL-01, `b368c9df`; yalnız ad, e-posta asla; `auth.users.lookup`). Öneri: adım 2 bu uca geçsin, `AuditUserLookupDto` kaldırılsın. Kıyas: SAP/Oracle denetim alanlarında kullanıcı adı ya da kimliği gösterilir, yetkisiz kullanıcıya e-posta gösterilmez (veri azaltma).

---

### BL-417

**Ekip görünümü astın görevini listeliyor ama okuma kuralı "astımın görevi" yolunu tanımıyor — başka tüzel kişilikteki astın görevi detayda "bulunamadı"**

DURUM: KAPANDI — seçenek (a), `147f0ae0`, toplantı zincirinde `ed717b55`, main eşitlemesinden sonra `89de80a3` (2026-09-16). Okuma kuralına ast bacağı eklendi: ekip listesiyle AYNI çözücü ve AYNI yüklem (`TaskTeamScope.Covers`), tüzel kişilik sınırı yok (sahip kararı). Liste ile detay tek cevap verir; `TaskTeamReadParityTests` iki tüzel kişilikte pariteyi ölçer. CT sabotajı: ast bacağı çağırandan başkasına da cevap verecek şekilde bozulduğunda kırmızı · ÖNCEKİ: AÇIK — tasarım kararı · BULAN: BL-414 ajanı (WP-WCN-TASK-DETAIL-READ-AND-LINKS-01) · KAYIT: 2026-09-15

Ekibim (`scope=team`, BL-023) astların işini listeliyor. Görev okuma kuralı (`TaskReadAccessPolicy`, BL-349) ise yöneticiyi yalnız aynı tüzel kişilik ya da verilmiş birim bacağından, ReadAll'dan ya da görevle doğrudan ilişkiden içeri alıyor; kapsam denetimi (`AllowsUnit`) boş pozisyon kimliğiyle çağrıldığı için astın pozisyonu bacağı hiç eşleşmiyor. Sonuç: listede görünen bir ast görevi (başka tüzel kişilikte, birim izni yok) detayda "bulunamadı" diyor. Liste ile okuma kuralı aynı soruya iki cevap veriyor. Seçenekler: (a) okuma kuralına "yöneticinin astlarının görevleri" bacağı eklemek (SAP'te yönetici ekip görevlerini organizasyon yapısından görür; Oracle'da yönetici hiyerarşisi erişimi), (b) ekip listesini okuma kuralıyla aynı kümeye daraltmak. **SAHİP KARARI 2026-09-15: (a)** (BL-410 ekip görünümü (ii) ile birlikte; tüzel kişilik sınırı yok). Öneri: (a) — ekip görünümü bilinçli bir yönetici yetkisi (BL-023) ve Blueprint organizasyon yapısına dayanıyor; ancak tüzel kişilikler arası görünürlük GxP/kiracı politikası sorusu olduğu için sahip kararı gerekir.

---

### BL-418

**Modül eşitlemesinin anahtarla çalışan yolu izin kapsamına bakmıyor — dev'de kiracı Admin rolleri 15 "yalnız platform" izni tutuyor**

DURUM: AÇIK — salt okuma inceleme başladı · BULAN: BL-411 ajanı (WP-INFRA-AUTH-REGISTER-ACTOR-AND-TEMPLATE-SCOPE-01) · KAYIT: 2026-09-15

**2026-09-15 inceleme sonucu (WP-INFRA-ESCALATION-BOUNDARY-AUDIT-01, salt okuma):** 15 satırın hiçbiri gerçek yükseltme değil ve kaynağı eşitleme değil **dev DataSeeder** (`system` aktörü; varsayılan PLATFORM kiracısının Admin rolü, 0 kullanıcı). 13'ü bilinçli iş akışı istisnası (`ModulePermissionResolver.cs:39-47` `PlatformHostedTenantModules = {workflow}`, `286d019e`; kiracı sınırlı, onaylayan Admin olmak zorunda); 2'si yanlış etiketlenmiş kiracı anahtarı (`platform.person.lookup_validation`, `platform.audit.events.append` — MOD-0251 HCM kullanıcı jetonuyla çağırıyor). **Anahtar yolu açığı gerçek:** 5 kiracıda (sonra silinen rollerde) eşitleme PlatformAdmin anahtarları vermiş; yetkilendirme olursa WORKING-CALENDAR'ın 3 gerçek platform anahtarı (sınıf C, gizli; API yönetici önekinde olduğu için bugün etkisiz), REFERENCE-DATA 13 ve DOCUMENT-MANAGEMENT 34 anahtarı kiracı Admin'ine gider. İş akışı istisnası hiçbir pakette/ADR'de kayıtlı değil ve §2c'ye aykırı. Dev Viewer rolünde 17 PlatformAdmin okuma izni artığı var. **Önerilen düzeltme:** `GrantPermissionsToRolesAsync` içinde yalnız `IsTenantAssignable` ya da izin listesindeki modül anahtarları kalsın (iki yol aynı cevabı versin) — DOCUMENT-MANAGEMENT 34 anahtarının kapsam kararı ve REFERENCE-DATA'nın kiracıya açık mı sorusuyla birlikte; iş akışı istisnası §2c/MOD-0023'e kaydedilsin. Ayrı kayıtlar: BL-421 denetim olayı aktörü istekten, BL-422 iş akışı yükseltme saati istemciden.

`ModulePermissionResolver.cs:99-103` kapsamı yalnız modül adıyla çözülen yedek yolda denetliyor; eşitleme tüketicisinin normal kullandığı anahtar yolu (`EntitlementPermissionSyncService.cs:110-113`) katalog satırını anahtarla seçip Scope'a hiç bakmıyor. Ölçüm (dev `diten_auth_v3`, salt okuma): kiracı Admin rollerinde modül kaynaklı 15 PlatformAdmin kapsamlı izin satırı — 13'ü `workflow`, 2'si `mod0251`. Nereden geldikleri izlenmedi. Not: iş akışı için bilinçli bir izin listesi istisnası vardı (plan eşitlemesi, "workflow allow-list bypasses platform.* boundary"); hangi satırların bu istisnaya, hangilerinin sızıntıya ait olduğu ölçülecek. Kiracı–platform yetki sınırı (escalation boundary) konusu; altyapı CT.

---

### BL-419

**Platform izin otomatik kayıt işçisi 60 sn beklemeden sonra anahtarları kapsamsız kaydediyor — Auth onları "yalnız platform" damgalıyor ve bir daha düşürmüyor**

DURUM: AÇIK · BULAN: BL-411 ajanı · KAYIT: 2026-09-15

`PlatformPermissionAutoRegistrationWorker.cs:45-54` kendi kayıt kapısı 60 sn'de zaman aşımına uğrayınca devam ediyor; `:81` her anahtarı modül ve kapsam null ile eşitliyor. Auth kurucusu `platform` önekini PlatformAdmin sınıflıyor (`Permission.cs:44,63`); sonraki manifest eşitlemesi Tenant gönderse de eşitlemenin eşitlik bozma kuralı PlatformAdmin'i asla düşürmüyor (`InternalPermissionsController.cs:144-153`). BL-411'deki iki şablon anahtarının takılma sebebi bu (DCP-004 tehlike B2). BL-411 izin listesiyle yalnız o ikisini düzeltti; kök sebep açık. Öneri: işçi manifest gelmeden kapsamsız oluşturma yapmasın ya da Auth kapsamsız oluşturmayı rota kuralıyla sınıflasın — tasarım kararı gerekir. Ayrıca: aynı veritabanında ikinci canlı Auth süreci (kademeli yeniden başlatma) düzeltmeyi bir sonraki başlangıca kadar geri alabilir (kapalı yönde).

---

### BL-420

**Kendi kaydında denetim olayı yazılamazsa kullanıcı olaysız kalıyor — yeniden deneme 409**

DURUM: AÇIK (kabul edilen sınır, düşük öncelik) · BULAN: BL-412 inceleme ajanı · KAYIT: 2026-09-15

`RegisterCommandHandler` olayı kullanıcı ve rol satırı kaydedildikten sonra yazıyor; yazma hata verirse kullanıcı olaysız kalır, yeniden deneme "zaten var" (409) alır. Giriş ve şifre değiştirme de aynı sırada. 21 CFR Part 11 "olay ya da hiçbiri" gerektirirse çözüm outbox ya da işlem (transaction). 

---

### BL-421

**Kiracı denetim olayı ekleme ucu aktörü istek gövdesinden alıyor — kiracı kullanıcısı kendi denetim kaydına "platform yöneticisi" ya da "sistem" adına olay yazabilir**

DURUM: KAPANDI — `809cbc34`, toplantı zincirinde `ff5e611b` (2026-09-15). Kiracı denetim ekleme ucu aktörü hep kimliği doğrulanmış çağırandan yazıyor (BL-409 çözümleyicisi); gövdede başka aktör → 400 `actor_type_mismatch`/`actor_id_mismatch`, adlandırılamayan çağıran → 403 `actor_unresolved`. HCM istemcisi bugün çağırmıyor, CRM zaten uyumlu; iç S2S ekleme ucu değişmedi. CT sabotajı: kaydedilen aktör türü System'e sabitlenince 4 kırmızı → geri → temiz kopyada Audit|Workflow|Escalation 426/426. Takipler BL-424 · önceki: YAPILIYOR (CT alt ajanı, 2026-09-15) · BULAN: WP-INFRA-ESCALATION-BOUNDARY-AUDIT-01 · KAYIT: 2026-09-15

`POST /api/v1/platform/audit/events` (`PlatformAuditAppendController.cs:36-40,66,75`) kiracıyı çağıranınkine sabitliyor ama `ActorType`/`ActorId` gövdeden geliyor (`AuditAppendApiModels.cs:10-11,80-120`), çağıranla bağlanmıyor. `platform.audit.events.append` tutan biri kendi kiracısının denetim izine istediği aktörle olay yazabilir. Kiracılar arası değil ama GxP / 21 CFR Part 11 bütünlük açığı. Bilinen çağıran: HCM `GovernedHcmAuditAppendClient` (kullanıcının jetonunu iletiyor). Düzeltme: aktör kimliği doğrulanmış çağırandan (BL-409 çözümleyicisi), gövdedeki farklı aktör 400. SAP/Oracle denetim günlükleri de kimliği doğrulanmış kullanıcıyı yazar.

---

### BL-422

**İş akışı yükseltme çalıştırması saati istemciden alıyor — yetkili biri süresi dolmamış görevleri kendi kiracısında zaman aşımına uğratabilir**

DURUM: KAPANDI — `809cbc34`, zincirde `ff5e611b` (2026-09-15). Yükseltme çalıştırması yalnız enjekte `TimeProvider`; istekte `NowUtc` → 400 `WORKFLOW_ESCALATION_CLOCK_NOT_ACCEPTED`. Ekrandaki NowUtc alanının kaldırılması BL-424 · önceki: YAPILIYOR (CT alt ajanı, 2026-09-15) · BULAN: WP-INFRA-ESCALATION-BOUNDARY-AUDIT-01 · KAYIT: 2026-09-15

`RunWorkflowEscalationsHandler.cs:41` istemcinin verdiği `NowUtc`'yi kabul ediyor; `platform.workflow.escalations.run` tutan biri geleceği vererek süresi dolmamış görevleri yükseltebilir. Düzeltme: API yolu sunucu saati; testler zamanı enjekte saatle yönetir.

---

### BL-423

**Dev ve dev tohumlaması: Viewer'da 17 PlatformAdmin okuma izni artığı, PLATFORM kiracısı Admin'ine her açılışta iş akışı ve mod0251 izinleri**

DURUM: AÇIK (sahip onayı gereken temizlik) · BULAN: WP-INFRA-ESCALATION-BOUNDARY-AUDIT-01 · KAYIT: 2026-09-15

Varsayılan kiracı Viewer rolünde 2026-07-03/06 tarihli 17 System kaynaklı PlatformAdmin okuma izni (bugünkü şablon Tenant kapsamı istiyor; hiçbir kod System izinlerini geri almıyor; 14'ü `/api/platform/*` yönetici yollarında, rolde kullanıcı yok). DataSeeder her açılışta PLATFORM kiracısı Admin'ine 15 izni geri yazıyor (`DataSeeder.cs:159,953,1101,1794`) — Platform bu kiracıyı iş akışına yetkilendirmiyor. Canlı ortamlarda aynı artık var mı ölçülmedi. PKS-001: `mod0251.*` ve `lookup_validation` alt çizgi kullanıyor (standart yasaklıyor).

---

### BL-424

**BL-421/422 takipleri: yükseltme ekranındaki NowUtc alanı artık 400 alıyor · denetim olayında OccurredAtUtc hâlâ istekten · HCM istemci testinin sözleşme yaması**

DURUM: AÇIK · BULAN: WP-INFRA-AUDIT-APPEND-ACTOR-AND-ESCALATION-CLOCK-01 · KAYIT: 2026-09-15

(1) **Ekran (iş akışı kulvarı, 7 dil):** `/Platform/Workflow` Escalations ve Index ekranlarında "NowUtc" tarih alanı (`Escalations.cshtml:43-44`, `Index.cshtml:153`; `escalations.js:400`, `index.js:1110`) doldurulursa sunucu artık `WORKFLOW_ESCALATION_CLOCK_NOT_ACCEPTED` (400) döner — alan ve l10n anahtarı kaldırılmalı, çalıştırma yalnız sunucu saatiyle. (2) **OccurredAtUtc:** kiracı denetim ekleme ucunda olayın zamanı hâlâ gövdeden geliyor (kayıt ayrıca sunucu `WrittenAtUtc` tutuyor, `AuditOutboxPayloadMapper.cs:58-59`). 21 CFR Part 11 zaman damgası güvenilir kaynaktan olmalı: öneri, geçmişe/geleceğe kabul edilebilir bir pencere dışında reddetmek ya da yalnız sunucu zamanı; karar altyapı CT. (3) **HCM kulvarı:** `GovernedHcmAuditAppendClientTests.cs` için gövdedeki `actorId`'nin yük aktörü olduğunu sabitleyen ve 400 aktör reddinin eklemeyi durdurduğunu gösteren test değişikliği CT tarafından commit'e alınmadı (başka kulvarın dosyası); yama: CT scratchpad `ctverify/hcm-governed-audit-client-tests.BL-421.patch`. Ayrıca HCM `MapDraftEvent` aktör kimliği yokken "System" gönderiyor — kullanıcı jetonuyla HTTP'den gönderilirse artık reddedilir.

---

### BL-425

**Yönetişim iş kuyruğu panosu (Views/ManagementGovernance/WorkQueue.cshtml) gerçek veriye ve yetkiye bağlı değil**

DURUM: AÇIK · BULAN: WP-WCN-KANBAN-01 (Dilim 4) · KAYIT: 2026-09-18

`Views/ManagementGovernance/WorkQueue.cshtml` + `mg-page.js` bugün bellekte üretilen sahte veriyle çalışıyor: görev tablosuna (TaskItem/WorkAggregation) bağlı değil, denetleyici sınıfında `[Authorize]` yok, ekran metinleri yalnız İngilizce (7 dil değil), "Closed" sütunu hiçbir zaman dolmuyor (hiçbir yazma yolu onu doldurmuyor). Önce gerçek veri (WorkAggregation projeksiyonuna bağlanmak) ve yetki (uygun `[Authorize]`/izin) gelmeli; pano bundan sonra Görev Merkezi'nin Kanban bileşeninin (WP-WCN-KANBAN-01) salt-okunur bir modu olarak gelebilir — sütun/kart/sürükleme görünümünü ikinci kez yazmak yerine aynı bileşeni okuma-yalnız açmak. Gelecek gerileme riski: düşük (eklemeli; mevcut Görev Merkezi Kanban davranışına dokunmuyor).

---

### BL-426

**Görev Merkezi ilk yükleme ekranı iskelet (skeleton) olmalı, spinner kartı değil**

DURUM: AÇIK · BULAN: WP-WCN-KANBAN-01 (Dilim 4) · KAYIT: 2026-09-18

Ölçülen (bu worktree'de bu düzeltme turunda yeniden ölçüldü, satırlar güncel): ilk yüklemede sayfa, ortada spinner + "İşleriniz yükleniyor" başlığı + üç gri çubuklu bir kart gösteriyor (`app.js` `renderLoadingState` :6787; kendi `.wcn-skeleton` sınıfı, `backbone-custom.css` :7892-7893). Sayfanın gerçek şekli (sekme şeridi, filtre/segment çubuğu, seçili görünüme göre liste satırları / pano sütunları / takvim ızgarası) yüklenirken hiç görünmüyor; veri gelince düzen birden değişiyor. Platformun ortak iskelet dili (`.backbone-skeleton` + `.skeleton-row`, `backbone-custom.css` :347-368) burada kullanılmıyor — Work Report ekranı bu dili doğru şekilde yeniden kullanıyor ve kendi `⚠ NO NEW SKELETON LANGUAGE` notunu taşıyor (`backbone-custom.css` :9499-9501: bloklar `.shimmer` + `.skeleton-row`, yalnız BOYUTLAR ekrana özel), bu Görev Merkezi'nin izleyeceği canlı örnek. İstenen: ilk yüklemede spinner kartı yerine sayfanın kendi şeklinde bir iskelet (sekmeler, filtre çubuğu, seçili görünüme göre satır/sütun/takvim yerleri), ortak sınıflarla; yazma sonrası yeniden okumada iskelet değil mevcut içerik kalmalı (`state.loadState` TEK yerde `'loading'` olarak atanıyor — yalnız ilk yüklemede, `app.js` :10770 — dosyada başka hiçbir yeniden-okuma yolu bu satırı tekrar çağırmıyor; kural zaten korunuyor, yeni iskelet de aynı kurala uymalı); `role=status` metni kalmalı; `prefers-reduced-motion`'da animasyon olmamalı; Takvim görünümü geldiğinde aynı kural ona da uygulanmalı. Gelecek gerileme riski: düşük (yalnız yükleme görünümü; veri yolu değişmez).

---

### BL-427

**`ErrorTitle` anahtarı Görev Merkezi'nde genel eylem-hatası bildirimi olarak kullanılıyor ama metni sayfanın YÜKLENEMEDİĞİNİ söylüyor**

DURUM: AÇIK · BULAN: WP-WCN-KANBAN-01 (Dilim 3b/4) · KAYIT: 2026-09-18

Ölçüldü (bu düzeltme turunda satırlar yeniden doğrulandı): `WorkCenterNextIndex.*.resx`'te `ErrorTitle` = "Görev Merkezi yüklenemedi" — `renderErrorState`'in (`app.js:6847`) sayfa hiç yüklenemediğinde gösterdiği başlık, komşu anahtarı `ErrorDesc` = "Filtreleriniz korundu. Çalışma alanını yeniden yüklemek için tekrar deneyin." ile birlikte. Ancak `t('ErrorTitle')` `app.js`'te 6 ayrı yerde, sayfa gayet yüklüyken tek bir EYLEMİN başarısız olduğu anlarda genel hata tostu olarak çağrılıyor (`app.js:5580,8019,8475,8549,9246,10932` — biri artık Kanban bırakma akışının ağ-hatası dalı, WP-WCN-KANBAN-01 Dilim 3b `runKanbanDrop`'un `.catch`'i). Beşi TEK BAŞINA (`toast(t('ErrorTitle'), 'error')`); altıncısı (`:9246`) tek başına DEĞİL — bir 403 koşulunda `NoAccessTitle`'a düşen bir üçlü operatörün ELSE dalı (`result.status === 403 ? t('NoAccessTitle') : t('ErrorTitle')`), yani orada `ErrorTitle` yalnız 403-DIŞI eylem hatalarında çağrılıyor. Bir sürükle-bırağın ağ hatasında kullanıcı "Görev Merkezi yüklenemedi" okuyor — sayfa yüklü, yalnız o işlem başarısız oldu. Genel eylem-hatası için ayrı bir anahtar açılmalı (7 dil, gerçek çeviri, ör. "İşlem tamamlanamadı") ve altı çağrı yeri ona geçirilmeli (`:9246`'daki ELSE dalı dahil, `NoAccessTitle` dalına dokunmadan); `ErrorTitle`/`ErrorDesc` çifti yalnız gerçek sayfa-yükleme hatasında (`renderErrorState`) kalmalı. Gelecek gerileme riski: düşük (yalnız metin/anahtar; davranış değişmiyor).

---

### BL-428

**Durum raporu ve kanıt standardı: elle yazılan yüzdeler yerine üretilen tablo**

DURUM: AÇIK · BULAN: CT (MVP6 lojistik raporu incelemesi) · KAYIT: 2026-09-18 · SAHİP KARARI: 2026-09-18 onaylandı

Ölçüldü: Tedarik zinciri durum raporu "bounded runtime %22,2 — 2/9" gibi satırlar taşıyordu. "bounded runtime" ve
"bounded CT kabulü" terimleri depoda hiçbir yerde tanımlı değil (`AGENTS.md`, `.antigravity/rules/**`,
`docs/guides/operations/control-tower-sop.md`: sıfır eşleşme). Kanıt bağlantıları başka bir makinedeki kopyayı
(`/Users/natig/Projects/ERP-vNext-recovery/...`) ve bir `/private/tmp/...` yolunu gösteriyordu; ikisi de bizde
açılamıyor. Depo ölçümü: `origin/feature/mvp6-logistics` (`4a8d4d4b`) MOD-0183 sevkiyat kodunu (42 dosya,
`ShipmentsController`) ve 68 kanıt dosyasını taşıyor; MOD-0184 (Carrier) için tek satır kod yok; iki CT karar kaydı
depoda yok; dal main'in 1 commit önünde, **400 commit gerisinde**; o daldaki dokuz paketin sekizi hâlâ `draft`
(MOD-0184 dahil, ki rapor onu "E4, CT kabul" diye gösteriyordu). Sonuç: rapor doğrulanamıyor ve sayılar depodaki
durumla uyuşmuyor.

İstenen:
1. **Durum/portföy raporu şablonu** (SOP'a yeni bölüm, §22'nin yanına): sabit sütunlar — modül · kapsam cümlesi ·
   kanıt seviyesi (E0–E5) · kanıtın yeri (`depo-yolu@commit`) · CT kararı (§29'daki hangi Done) · sıradaki tek eksik.
   Yüzde ancak altındaki liste ile birlikte yazılır.
2. **Kanıt kuralı** (`.antigravity/rules/`): kanıt depoya işlenir ve gönderilir, `docs/records/audits/<yyyy-ay>/`
   altında durur, `yol@commit` diye gösterilir. Kişisel makine yolu, `/tmp` ve gönderilmemiş dal kanıt sayılmaz.
3. **Bayatlık kuralı**: kanıt koşusundan önce dal ana dalla senkronlanır; rapor dalın kaç commit geride olduğunu yazar.
4. **Terim disiplini**: yeni statü adı uydurulmaz; yalnız E0–E5 ve §29 Done seviyeleri. Yeni terim önce SOP'a yazılır.
5. **CT karar kaydı künyesi**: her karar kaydının başına dört satırlık künye (modül/iş paketi, kanıt seviyesi, karar,
   commit) — makine okuyabilsin diye.
6. **`scripts/status_report.sh`**: paket `status:` alanlarını, karar kaydı künyelerini ve git'teki geri kalmışlığı
   okuyup 1. maddedeki tabloyu üretir. Geliştirici tablo yazmaz, betiği çalıştırır.

Gelecek gerileme riski: düşük (belge + salt-okuyan betik; üretim koduna dokunmaz). 1–4 tek başına da işe yarar ama
elle yazım sürer; asıl kolaylık 6'dan gelir.

---

### BL-429

**"Ad bilgisi yok" etiketi Görev Merkezi'nin başka üç yerinde de gerçek ad gibi kullanılıyor olabilir**

DURUM: AÇIK · BULAN: WP-WCN-KANBAN-01 (Dilim 4) · KAYIT: 2026-09-18

Ölçüldü: `toPresentation`'ın `personName()`'i sunucu `displayName` göndermediğinde `PersonNameUnavailable`
etiketini ("Ad bilgisi yok") döndürüyor; bu bir ad değil, adın bilinmediğini söyleyen cümle. Kanban kartı bunu ad
sanıp baş harf üretiyordu; Dilim 4'te `assigneeNameKnown`/`requesterNameKnown` bayrakları eklenerek düzeltildi.
Aynı `item.assignee || item.requester` doğruluk denetimi deseni Kanban DIŞINDA da duruyor: liste satırı çipi
(`app.js:1591`), detay sayfası atanan alanı (`:2994-2995`) ve devir kartı (`:4711`). Bu WP'nin kapsamı yalnız
Kanban olduğu için oralara dokunulmadı.

İstenen: üç yerin her biri ölçülür; etiketi ad gibi gösteren varsa aynı bayraklarla kapatılır, kanıtı test olur.
Gelecek gerileme riski: düşük (yalnız gösterim).

---

### BL-430

**Eksik ikon adı ekrana dolu bir kare çiziyor — "Görev Merkezi geçici olarak kullanılamıyor" sayfasında canlıda görüldü**

DURUM: AÇIK · BULAN: sahip (canlı önizleme, 2026-09-18) · KAYIT: 2026-09-18

Ölçüldü: Kiracı kabuğu ikonları `assets/vendor/fonts/iconify-icons.css` ile yüklüyor. Bu yöntemde `.bx` kutusu
`background-color: currentColor` alıp şekli `mask-image: var(--svg)` ile kesiyor; `--svg` değişkenini ikonun kendi
sınıfı tanımlıyor. İkon sınıfı sette YOKSA maske tanımsız kalır ve kutu rengiyle **dolu bir kare** olarak boyanır.
Sahibin gördüğü turuncu kare budur: hata ekranı `unavailable` durumunda `bx-cloud-off` kullanıyor (`app.js`
`LOAD_ERROR_STATES`), ve `bx-cloud-off` `iconify-icons.css`'te yok. Renk `.wcn-system-error > i`'nin
`var(--bs-danger)` değeri, boyut 2.5rem.

Sette bulunmayan diğer adlar (tarandı, `wwwroot/assets/js/**`):
- Kare çizen gerçek ikon adları: `bx-cloud-off`, `bx-flag-alt`, `bx-hospital`, `bx-x-square`
- İkon değil, boxicons'ın yardımcı sınıfları: `bx-spin` (dönme), `bx-lg` (boyut) — iconify ile sessizce hiçbir şey
  yapmıyorlar; ör. `bx bx-loader-alt bx-spin` dönmüyor.

İstenen:
1. Dört ikon adı sette var olan karşılıklarıyla değiştirilir ya da ikonlar sete eklenir; hata ekranı için sette duran
   `bx-wifi-off` uygun bir karşılık.
2. `bx-spin` / `bx-lg` yerine projenin kendi yolu kullanılır (dönme için mevcut spinner deseni, boyut için CSS).
3. Koruma testi: `wwwroot/assets/js/**` içinde geçen her `bx-*` sınıfı ikon setinde tanımlı olmalı; değilse test
   kırmızı. Bugün hiçbir test bunu yakalamıyor.
4. Aynı turda hata ekranının yerleşimi gözden geçirilir (ikon başlığa çok yakın; "Tekrar dene" düğmesinin yeri).

Gelecek gerileme riski: düşük (ikon adları + koruma testi; davranış değişmez).
İlgili: BL-427 (aynı ekranın başlığı yanlış anahtardan geliyor).

---

### BL-431

**İki Satınalma ekranı ortak onay bileşenine kendi seçenek listesini geçiriyor — "üründe tek diyalog" kuralı kırıldı**

DURUM: AÇIK · BULAN: CT (Kanban dalını main ile birleştirirken) · KAYIT: 2026-09-21

Ölçüldü: `frontend/Diten.Web/tests/wcn-dialog-one-language.test.js` ürün genelinde tek bir onay diyaloğu
kuralını koruyor ve `showConfirm`'e `inputOptions` geçen dosyaları adıyla sayıyor. Birleşme sonrası liste
beklenen 2 yerine 4 dosya veriyor; yeni gelenler `wwwroot/assets/js/Procurement/InvoiceMatch/details.js` ve
`.../index.js`. Aynı dosyada ikinci bir kırmızı da var: "declares the package once" testi 5 yerine 6 dosya
görüyor. Yani main'de bu test zaten kırmızı; CI vitest koşmadığı için fark edilmemiş.

Bu bir yanlış pozitif DEĞİL: kural bilerek ürün genelinde. Test gevşetilmez; iki ekran ortak bileşenin kendi
yoluna taşınır (BL-367 ile aynı aile).

İstenen: Satınalma sahibi iki dosyayı ortak diyalog yoluna taşır ya da kuralın değişmesi için gerekçe getirir;
test yeşile döner. CT tarafında yapılacak bir şey yok, kayıt bilgi amaçlıdır.
Gelecek gerileme riski: düşük (yalnız iki ekranın diyalog çağrısı).

---

### BL-432

**Eski modüller veri kapsamını hiç sormuyor — okuma yetkisi olan kiracının bütün satırlarını görüyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · BULAN: CT (sahip sorusu: "kişiyi organizasyon birimine atıyorsun, o şekilde datayı görmüyor mu?") · KAYIT: 2026-09-21

Ölçüldü (`origin/main`, 2026-09-21): `IDataScopeResolver` üretim kodunda yalnız üç yüzeyde tüketiliyor —
`Features/Tasks/Handlers/QueryHandlers/WorkReportQueryHandler.cs` (+ `WorkReportScopeSource`),
`Features/Tasks/Services/TaskAssignmentScopeResolver.cs` ve `API/Authorization/Explain/SelfAccessExplainService.cs`.
Motor çalışıyor ve gerçek: `OrgDataScopeResolver` (MOD-0018-FU15) MOD-0288 organizasyon verisinden OrgUnit (alt ağaç
düzleştirilmiş) · Position · ManagerChain · LegalEntity üretiyor, döngü güvenli ve kapalı başlıyor. Sorulmuyor.

Sonuç: bir modülün okuma yetkisini alan kişi, bir birime atanmış olsa bile o modülün kiracıdaki **bütün**
satırlarını görüyor. Kiracı izolasyonu sağlam (`TenantId` her sorguda); eksik olan aynı şirket içindeki ayrım.

Yeni iş bu kapıdan geçemez: kural `.antigravity/rules/data-scope-enforcement.md` (SEC-002) olarak yazıldı ve yeni
modüller ile hâlen üzerinde çalışılan modüller için bugünden geçerli. Bu madde **geriye dönük** bağlama işidir.

İstenen (modül başına ayrı dilim, sırayla): CRM önce — hangi alan kapsamı taşır (müşteri sahibi / birim), kapsam
çevirisi `WorkReportScope` kalıbıyla, tenant-wide için ayrı izin (`<modül>.<özellik>.read-tenant-wide`), iki kişi
iki birim testi + sabotaj. Ardından Satınalma, Doküman Yönetimi, MDM listeleri.
Gelecek gerileme riski: orta. Bugün herkes her satırı görüyor; kapsam açıldığında bazı kullanıcıların listesi
kısalacak. Bu bir gerileme değil düzeltmedir, ama kiracı yöneticisine önceden söylenmeden açılmaz — her modül
dilimi kendi geçiş cümlesini yazar (kimin neyi görmeyi bırakacağı).

---

### BL-433

**Servis hesabı bugün yalnız bir etiket — ekrandan giriş yapabiliyor, kişi seçicilerde çıkıyor, görev sahibi olabiliyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · BULAN: CT (sahip sorusu: "bu servis hesabı nasıl olmalı, etiketleme dışında ne işe yarar?") · KAYIT: 2026-09-21

**2026-10-02 — sahip:** büyük iş, sonraya; Kullanıcılar modülünün kapanışını bekletmez.

Ölçüldü (`origin/main`, 2026-09-21): `AccountKind` (Unknown | Human | Service) `Diten.AuthService.Domain/Enums/AccountKind.cs`'de
tanımlı; değiştirmek `auth.users.account-kind.manage` iznini istiyor ve bu izin `ExplicitGrantOnlyPermissions`
listesinde (hiçbir role kendiliğinden gelmez, SuperAdmin otomatiği dahil); `GET api/users/{id}/account-assertion`
kararı değil olguyu döndürüyor (`Active` + `AccountKind`), kararı çağıran (PPM) veriyor; tohumlama hiçbir hesabı
sınıflandırmıyor (`DataSeeder` → `AccountKind.Unknown`). Yani sınıflandırma katmanı doğru kurulmuş.

Davranış katmanı yok. `Service` işaretli bir hesap bugün: `/account/login` ekranından şifreyle girebiliyor,
kişi/atama seçicilerinde insanların arasında listeleniyor, görev sahibi ve onaycı olabiliyor. Bir denetçi "bu
onayı kim verdi" diye sorduğunda cevap bir robot olabilir ve ekranda insandan ayırt edilmiyor.

İstenen (küçük, tek dilim): `Service` hesabı (1) etkileşimli giriş akışında reddedilir — hata metni kendi kodunu
taşır, "şifre yanlış" denmez; (2) kişi seçicilerinden ve atama havuzlarından düşer; (3) görev sahibi/onaycı
olamaz. Üçü de tek bir "bu hesap insan mı" sorusunu okur; test: her üç yüzey için `Service` ile kırmızı, `Human`
ile yeşil. Kimlik/anahtar tarafı buraya girmez — o BL-434.
Gelecek gerileme riski: düşük. Bugün hiçbir hesap `Service` değil (tohumlama `Unknown` veriyor), yani kural
açıldığında kimsenin girişi kesilmez; yanlış işaretlenmiş bir hesap ise zaten bugün de yanlış.

---

### BL-434

**Servis hesabının kimlik bilgisi yok — insan şifresiyle çalışan entegrasyon, süresi ve iptali olmayan erişim demek**

DURUM: AÇIK · SAHİP: SAHİPSİZ · BULAN: CT (sahip sorusu: "servis hesabı belli süreliğine mi açılıyor, o hesaba belli sayfalar yetki mi veriliyor?") · KAYIT: 2026-09-21

**2026-10-02 — sahip:** büyük iş, sonraya; Kullanıcılar modülünün kapanışını bekletmez.

Bugünkü durum: bir entegrasyonun sistemimize bağlanma yolu, birinin insan hesabı açıp şifresini entegrasyona
vermesidir. Bunun üç sonucu var — şifre bir insanın parola politikasına tabi (dolayısıyla bir gün süresi dolar ve
entegrasyon gece yarısı durur), kimsenin elinde "bu anahtar nerede kullanılıyor" listesi yoktur, ve erişimi
kesmenin tek yolu hesabı kapatmaktır (hangi entegrasyonun kırılacağı bilinmeden).

SAP ve Oracle bu ihtiyacı ayrı bir kullanıcı türüyle karşılar: SAP'de `System`/`Communication` kullanıcı türü
(diyalog girişi yapamaz, parola politikası ayrıdır), Oracle'da entegrasyon kullanıcısı + belirteç. Bizde karşılığı
`AccountKind.Service`, ama yalnız sınıflandırma tarafı var (BL-433).

İstenen (büyük — kendi iş paketi, tek dilimde yapılmaz): servis hesabına ait anahtar/istemci kimliği, verilme ve
bitiş tarihi, planlı döndürme (eskisi geçerliyken yenisi çalışır), anında iptal, ve her kullanımın denetim kaydı
(hangi anahtar, hangi IP, hangi uç nokta). Yetki tarafı mevcut RBAC'tir — servis hesabına da rol verilir; gördüğü
veri BL-432'deki kapsam kuralıyla belirlenir (servis hesabına da pozisyon/birim verilir, ayrı bir veri mekanizması
kurulmaz).
Gelecek gerileme riski: yüksekse de yönetilebilir — bu iş Auth'un kimlik doğrulama yoluna dokunur. Bu yüzden
BL-433'ten sonra ve ayrı bir iş paketi olarak planlanır; ikisi tek dilime konmaz.

---

### BL-435

**PPM Girişimler ekranı ortak onay bileşenini atlayıp kendi diyaloğunu açıyor — "üründe tek diyalog" kuralı kırık**

DURUM: AÇIK · SAHİP: SAHİPSİZ (PPM) · BULAN: CT (Kullanıcılar davet diyaloğunu düzeltirken tam paket koşusu) · KAYIT: 2026-09-21

Ölçüldü (`origin/main`, 2026-09-21): `tests/dialog-one-implementation.test.js` → "opens no dialog outside the
shared component" tek bir dosyayı işaret ediyor: `wwwroot/assets/js/PPM/Initiatives/index.js`. Kural ürün
genelinde ve bilerek öyle: ham `Swal.fire` yazan dosya ya `window.showConfirm`'e geçer ya da gerekçesiyle
`KNOWN_RAW` listesine yazılır. Bu dosya ikisini de yapmamış, yani main'de bu test bugün kırmızı (CI vitest
koşmadığı için görünmüyor — BL-431 ile aynı aile).

Ham diyalog tek başına yasak değildir; alan taşıyan bir diyalog ham olmak zorundadır. Kural, ham olanın da
ürünün görünümünü **okumasını** ister: `window.DitenDialogAppearance` paketi + `iconHtml` + `description`.
Kullanıcılar modülündeki aynı hata bugün düzeltildi ve örneği orada duruyor
(`Governance/Users/index.js` → `showInviteLink`).

İstenen: PPM sahibi ya `showConfirm`'e geçer ya da gerekçesini yazıp `KNOWN_RAW`'a ekler **ve** yayımlanmış
paketi okur. Test gevşetilmez. CT tarafında yapılacak bir şey yok; kayıt bilgi amaçlı.
Gelecek gerileme riski: düşük (tek ekranın diyalog çağrısı).

---

### BL-436

**Hiç değer almamış alan tanımı silinemiyor — yanlışlıkla açılan satır listede sonsuza kadar kalıyor**

DURUM: AÇIK · SAHİP: SAHİPSİZ · BULAN: sahip (organizasyon alan tanımlarını girerken) · KAYIT: 2026-09-21

Ölçüldü: MOD-0288-FU02'de tanım için **silme uç noktası yok**; tek eylem `deactivate` (tekil + toplu).
Denetleyicinin kendi cümlesi: *"IT DEACTIVATES; IT DOES NOT DELETE."* Bu, kullanılmış bir tanım için doğru —
kaydedilmiş her değer tanımı kimliğiyle işaret ediyor ve yazıldığı andaki tipini/sınıflandırmasını kopyalıyor,
tanım silinirse yorumlanamayan değerler kalır. Oracle'da kullanılmış flexfield segmenti devre dışı bırakılır,
SAP'de karakteristik ancak hiç kullanılmamışsa silinir.

Eksik olan ikinci yarı: **hiç değer yazılmamış** bir tanım da silinemiyor. `Kod` oluşturulduktan sonra
değiştirilemediği ve pasife alma tek yönlü olduğu için, yazım hatasıyla açılmış bir tanım kalıcı: listede
durur, 50'lik kotadan düşmez (pasifler sayılmıyor, bu doğru) ama gözden hiç kalkmaz.

İstenen: "değeri yoksa silinir, varsa silinemez" kuralı — silme, o tanıma ait değer sayısı sıfırsa kabul edilir,
değilse 409 ve mevcut pasife alma yolu önerilir. Sayım sunucuda yapılır.
Gelecek gerileme riski: düşük (yeni bir uç nokta, mevcut davranış değişmiyor).

---

### BL-437

**Onay iş kaleminin başlığı "Onay: tasks &lt;guid&gt;" — onaylayan neye onay verdiğini görmüyor**

DURUM: KAPANDI — `db44c3bec` → integration `86aced98f` (WP-PSS-WA-APPROVAL-TITLE-01, 2026-09-24; canlı doğrulandı) · BULAN: sahip (Görev Merkezi geri bildirimi) · KAYIT: 2026-09-23

Ölçüldü: `WorkItemProjectionService.cs:25` sabit bir kaynak anahtarı kuruyor —
`WorkAggregation_Title_Approval` → tr metni `Onay: {objectType} {objectId}`. Görevin başlığı hiç kullanılmıyor.
"Bu bana neden geldi" bilgisi de yok: `Requester` alanı DTO'da var ama onay yolunda doldurulmuyor
(`WorkAggregationModels.cs:358`; görev projeksiyonu dolduruyor, onay projeksiyonu doldurmuyor), kaynak görev
bağlantısı `DeepLink: null` (`:58-63`), bekleme sebebi bilerek null (`:88`).

Sonuç: onaylayan kişi gelen kutusunda ham bir kimlik görüyor ve neyi onayladığını anlamak için tahmin etmek
zorunda. Bu, toplantı → karar → görev → onay zincirini test edilemez de kılıyor.

İstenen: başlık kaynak görevin başlığını taşısın, `Requester` doldurulsun, kaynağa tıklanır bağlantı ve tek
cümlelik sebep eklensin ("X onayını bekliyor"). Projeksiyon katmanında toplu iş; metinler yedi dilde.
Gelecek gerileme riski: düşük (yalnız projeksiyon; iş akışı kuralları değişmiyor).

**Kapanış (2026-09-24):** `IApprovalSourceResolver` (WorkAggregation) → `TaskApprovalSourceResolver` (MOD-0024, salt okunur, sayfa
başına toplu): başlık = görevin kendisi, talep sahibi = incelemeye SON gönderen (onay/iş talebi: oluşturucu), `DeepLink` → /Tasks/{id},
yeni sözleşme alanı `ArrivalReason` (2 anahtar × 7 dil; adsız cümle ayrı, ad yerine asla id). CT guard'ları (ajanın 6 sabotajının ötesinde
6 boşluk): en son gönderen, id-yerine-ad, IsCurrentUser, boşluk bağlantı, XSS kaçışı, {name} yer tutucu. **Canlı (CT, admin oturumu):**
mevcut görev incelemeye alınıp `accept → start → submitReview` sonrası gelen kutusunda "CT canlı: toplantıdan doğan görev" başlığı,
"Diten Admin bu görevi onayına gönderdi · Kaynak kaydını aç" cümlesi ve `/Tasks/{id}` bağlantısı; guid yok. Not: dev kiracısında
pozisyon/atama yok → yeni görev açılamıyor (`ORGANIZATION_UNIT_UNRESOLVED`, BL-358/366) — canlı zincir var olan görevle koşuldu;
kontrol turundan önce organizasyon verisi gerekir. Sahibe kalan: iki kişili senaryo (gönderen ≠ okuyan) ve "Görevi aç" metni tercihi
(bugün `DetailOpenSource` "Kaynak kaydını aç").

---

### BL-438

**Klavye kısayolları tek ekranda yaşıyor, yarısı hiçbir yerde yazmıyor**

DURUM: KAPANDI (kısmi canlı) — `dc1e0b8b0` → integration `22074790e` (WP-UI-SHORTCUTS-01, 2026-09-24) · BULAN: sahip (Görev Merkezi geri bildirimi) · KAYIT: 2026-09-23

Ölçüldü: kısayollar yalnız `WorkCenterNext/app.js:10038-10082`'de tanımlı ve `document`'e bağlı. Tuşlar:
`j` sonraki, `k` önceki, `Enter`/`o` aç, `a` kabul, `r` reddet, `Escape` seçimi temizle, sekme şeridinde
ok tuşları. İpucu açılır menüsü (`app.js:1230`, metin `KeyboardHint`) bunların yalnız **dördünü** yazıyor —
`o`, `Escape` ve oklar hiçbir yerde geçmiyor — ve menü `d-none d-lg-block` ile küçük ekranlarda gizli. Ortak
bir kısayol katmanı yok; Görevler ekranlarında kısayol hiç çalışmıyor.

İstenen: ortak kısayol katmanı (tek yerde tanımlı, her ekranın kaydolduğu) + `?` tuşuyla açılan tam liste,
küçük ekranlarda da erişilebilir. Yeni eylemler (devret, onaya git, soruyu cevapla) listeye oradan girer.
Gelecek gerileme riski: orta — `document` seviyesinde tuş yakalayan ortak katman, form alanlarında ve
diyaloglarda susmak zorunda; bunun testi baştan yazılır.

**Kapanış (2026-09-24):** `wwwroot/assets/js/shared/diten-shortcuts.js` (`window.DitenShortcuts`: register/unregister/open/list) tek
dinleyici + susma kuralı (alan, contenteditable, açık modal/offcanvas/swal, Ctrl/Cmd/Alt) + gürültülü çakışma reddi + `?` listesi
(kayıtlı olandan üretilir, ortak diyalog görünümü, her genişlikte). WCN kaydoldu (j/k/Enter/o/Space/a/r/Esc + sekme okları), Görev
detayı `e`/Esc (başlıktaki bağlantıya basar), Create/Edit yalnız `?` (forma harf bağlamak yarım formu gönderirdi). Metinler
`SharedResource` 7 dil × 18 anahtar. Kanıt: +56 test, ajan 6 + CT 6 sabotaj kırmızı. Canlı (CT): `?` listesi 9 satırla açıldı,
dialog açıkken `j` sessiz, arama kutusunda `j` harf, görev detayında `e` → Düzenle, Esc → geri. **Sahibe kalan:** WCN j/k/Enter
(CT oturumu "süresi doldu" verdi, liste boştu), dar ekranda klavye düğmesi, `ar` sağdan sola diyalog görünümü.

---

### BL-439

**Soru sorulan kişiye hiçbir şey gitmiyor — "Bilgi bekle" akışının ikinci yarısı yok**

DURUM: KAPANDI (sahip yeniden testi bekliyor) — WP-PSS-TASK-INQUIRY-02, `aa452d0bf` (feat/pss-task-inquiry, 61ad9b41d tabanı)
→ merge `9d711ae18` + CT guard `154f9e2fb` (integration/2026-09-21-test, 2026-09-24) · SAHİP: CT · BULAN: sahip (Görev Merkezi
geri bildirimi) · KAYIT: 2026-09-23

**Ne yapıldı:** "Bilgi bekle" ile kişi seçilince o kişinin gelen kutusuna ayrı bir kalem düşüyor (`workIntent=inquiry`,
tek eylem `answer`; soru gövdede, soran "talep eden" satırında; "Kaynak kaydını aç" bağlantısı görevin detayına).
Cevap görevin geçmişine `inquiryAnswered` olarak yazılıyor, bekleme temizleniyor, görev park edildiği yaşam döngüsüne dönüyor;
çalışan işe dönüş `resume`'un sorduğu onay (MOD-0023) ve bağımlılık kapılarını yeniden soruyor — engel varsa Open'a iner,
cevap yine kaydedilir. Tek yüklem (`TaskInquiryRules.IsAskedOf` = Waiting ∧ WaitingOnUserId): kim görür, kim okur, kim
cevaplar aynı kişi; okuma kuralına EKLEMELİ (mevcut erişim daralmadı; talep sahibi sorulduğunda detay sayfası duruyor).
Kendine bekleme reddi (answer, resume'un etrafından dolanamaz). Soran bildirim: `platform.tasks.inquiryasked`; cevap gelince
sahibe `platform.tasks.inquiryanswered` (şablon tohumu 7 dil). Sahibin kartı "X cevapladı" çipi + cevabı tooltip'te — yalnız
açık işte ve son söz cevapken. WCN + Tasks resx 13+3 anahtar × 7 dil.

**CT kabulü:** Platform 5096/49 kırmızı = taban (İş Referans Verisi Mongo, eski borç); vitest 3133/24 = taban; Web 229; mimari 18.
CT sabotajları (ajanınkinden farklı) 7/8 kırmızı: yaşam döngüsüz IsAskedOf · kapısız dönüş · daimi okuma · kiracısız Mongo
sorgusu (HTTP+Mongo tel testi yakaladı) · kendine bekleme · answer→reason · ar `{title}`. Kırmızı vermeyen C6 (bitmiş işte
çip) için `TaskInquiryCtTests` eklendi. Merge çakışması yalnız `WorkAggregationModels.cs` (BL-437 ArrivalReason + BL-439
InquiryAnswer, ikisi de tutuldu). Birleşik ağaç: subset 166, Web 229, vitest 3235/24 = taban, Platform 5121/50 → fazladan tek
kırmızı `BusinessReferenceDataMongoResidueSweeperTests` "database is currently being dropped" yarışı, tek başına 5/5 yeşil.

**Canlı (2026-09-24, birleşik yapı, iki kullanıcı — kayıt `docs/records/tests/task-center/2026-09-24-inquiry.md`):**
admin "Bilgi bekle" ile Ayşe'ye sordu → Ayşe'ye e-posta (Mailpit; ilk deneme Mailpit kapalıyken reddedildi, kuyruk 1 dk sonra
teslim etti) → Ayşe'nin gelen kutusunda tek kalem "Soru · S10B-Planla Testi", tek eylem Cevapla; görevi okuyabildi (200) →
cevapladı → listesi boşaldı, görev ona 404 → admin'e e-posta → görev InProgress v4, kartta `inquiryAnswer`, detayda "Ayşe
Korkmaz cevapladı: …", Etkinlik'te "Soru cevaplandı". Ön koşul: dev org verisi (HEADQUARTERS ekrandan yumuşak silinmişti)
sahip tarafından yeniden kuruldu. Bulgular: BL-443 (seçicide fare tıklaması diyaloğu kapatıyor), BL-444 (detayda dört
bekleme kutusu), BL-445 (bildirim e-postası dili en).

**Bilinen boşluk (ayrı kayıt):** BL-442 — yorum ekleme uç noktası okuma kuralını sormuyor.

Ölçüldü: soru hem "kime" hem "neden" olarak saklanıyor (`TaskItem.WaitingOnUserId`, `WaitingReason`;
`InquireTaskItemRequest(ExpectedVersion, Reason, WaitingOnUserId?)`) ve **soranın** kartında doğru gösteriliyor
(`app.js:2104-2110` → "X bekleniyor (sebep)"). Görev soranın üzerinde `Waiting` durumunda kalıyor.

Eksik olan: **sorulan kişiye giden hiçbir şey yok.** `TaskNotificationService` ve `TaskReadAccessPolicy` içinde
`WaitingOnUserId` hiç geçmiyor (grep boş) — ne bildirim, ne gelen kutusunda bir kalem. O kişi sorulduğunu ancak
soran söylerse öğreniyor. Yani özellik yarım: soru kaydediliyor, iletilmiyor.

İstenen (ayrı iş paketi): sorulan kişiye bir iş kalemi düşsün ("X sana sordu: …"), cevapladığında görev
beklemeden çıksın, cevap görevin geçmişine yazılsın. Bildirim yolu + yeni kalem türü + cevap akışı + kimin
neyi okuyabileceği kuralı gerekiyor.
Gelecek gerileme riski: orta-yüksek — yeni bir gelen kutusu kalemi türü, MOD-0024 projeksiyonuna dokunur.

---

### BL-440

**Liste ekranları referansı kopyalıyor, kullanmıyor — 138 listede yapı tek tek elle yazılıyor**

DURUM: KARAR VERİLDİ, YÜRÜYOR · SAHİP: CT (paketler prompt olarak çıkar) · BULAN: CT + sahip (Kullanıcılar modülü testi) · KAYIT: 2026-09-23 · KARAR: 2026-09-23

Ölçüldü (2026-09-23, test dalı):

| | |
|---|---|
| Liste bileşeni (`_DataTable.cshtml`) | **138** |
| `data-dt-standard="v2"` taşıyan | 133 |
| Bir tür iskelet markup'ı olan | 132 |
| **Toplu seçim sütunu (`dt-checkboxes`) olan** | **26** |
| Ortak şekilli iskelet parçasını kullanan | 6 |

Altın referans (Golden Reference Slim/Compact) bugünkü kuralların hepsini taşıyor: alan ikonları, değişmez
alan boyası, offcanvas select2, seçim sütunu, yeni iskelet parçası. **Referans doğru; ekranlar ona bakmıyor.**

Kullanıcılar ekranı bunun canlı örneği: JS'i referanstan kopyalanmış (`bindBulkSelection` çağırıyor) ama
markup'ı kopyalanmamış (seçim sütunu yok) → toplu eylem çubuğu hiç çıkmıyor. Aynı boşluktan üç bulgu daha
çıktı: gizli sütunlar dışa aktarmada görünüyor (sütun listesi ekranda elle sabitlenmiş), yükleme göstergesi
farklı (eski iskelet bloğu), araç çubuğu ikonlarının hizası farklı.

⚠ DÜZELTME: CT ilk ölçümünde "ekranların yarısı ortak katmandan geçmiyor" dedi; yanlıştı. Kullanıcılar
`DitenDataTable.createCrudTable` → `DtDefaults.create` zincirinden geçiyor (`diten-datatable.js:263`).
Sorun ortak katmanın yokluğu değil, **markup ile JS'in ayrı ayrı yazılması**.

**İki model, karar ekibin:**

1. **Guard** (ucuz, hızlı): bir test, JS'in beklediği ile markup'ın sunduğunu karşılaştırsın — toplu çubuk
   bekleyen ekranın seçim sütunu olsun, iskelet bekleyen ekranın parçası olsun. Bugün uymayanlar gerekçesiyle
   listelenir; yeni ekran listeye eklenemez. **Bu bir iskele, model değil** — kozmetik sapmayı değil gerçek
   kusuru yakalar, ama kopyalamayı ortadan kaldırmaz.
2. **Bileşen** (doğru model, pahalı): liste bir kopyalama kaynağı değil, **kullanılan bir bileşen** olsun —
   ekran sütunlarını ve seçeneklerini verir; kart, iskelet, araç çubuğu, seçim sütunu ve tablo kabuğu
   bileşenden gelir. Kopya olmayınca ayrışacak bir şey de olmaz. SAP Fiori (SmartTable / List Report) ve
   Oracle Redwood sayfa şablonları bu modeli seçmiştir; ikisi de "şu sayfaya bak ve benzet" demez.

**CT'nin önerisi:** (1) şimdi, doğru biçimiyle (tutarlılık guard'ı) · (2) bir sonraki YENİ liste ekranında
doğsun, 138 ekranlık göç programı olarak değil · eskiler modül test turlarında tek tek düşsün (Kullanıcılar'da
bugün yapıldığı gibi).

Gelecek gerileme riski: (1) düşük — yalnız test. (2) YÜKSEK ve bilinçli: ürünün bütün listelerinin şeklini
belirler; bu yüzden CT tek başına karar vermiyor, ekip tartışması için buraya yazıldı.

**KARAR (sahip, 2026-09-23, ekip sayfası: "Altın Referans Sözleşmesi"):** ikisi de — guard şimdi, bileşen ilk yeni listede.
İki ek karar:
1. **Veri modeli her sayfada sunucu değil, kurala göre:** module pack `data_mode: server | client` (+ istemcide `data_mode_max_rows`);
   ayrım kümenin sınırlı olup olmadığı. Kural: `frontend-datatable-template.md` → Veri modeli; `module-pack-standard.md`.
2. **Eski sayfalar şimdi değişmez; dokunma protokolü:** eski bir liste ekranına dokunan görev sapmaları listeler ve sahibe sorar
   (`frontend-datatable-template.md` → Dokunma protokolü; altı ajan; Claude Code PostToolUse kancası `list_screen_touch_hook.py`).

**Paketler:** 0 guard'lar + kural + kanca (CT, bitti) · 1 liste kabuğu bileşeni · 2 JS fabrikası (`createCrudTable` büyür) ·
3 sunucu veri modu (pilot Auth/Users sorgusu) · 4 altın referanslar bileşene + kural dosyaları · 5 Kullanıcılar pilot ekran.
Sıra zorunlu; her paket ayrı prompt, CT kabul eder. Ölçüm (paket 0): Kullanıcılar 17 sapma; Golden Slim/Compact yeni kontrollerde temiz,
yalnız eski `personalizationClient` kontrolü kırmızı (HEAD'de de kırmızıydı, ayrı borç).

**Paket 1 notları (2026-09-23, WP-UI-LIST-SHELL-01):** `_ListShell.cshtml` + `DataTableListShellViewModel` (TableId/DataMode required,
DataMode fail-closed); iki altın `_DataTable.cshtml` kabuğu kullanıyor; render eşitliği testi önce/sonra HTML'i teste gömülü tutuyor.
İki bilinen zayıflık, bilerek ertelendi: (a) doğrulayıcı Razor yorumlarını okuyor — altın `Index.cshtml` v2 işaretini yorumda taşıyor ve
`is_v2` oradan geçiyor; yorum ayıklama 138 sayfanın sonucunu değiştirir → paket 4'te kural dosyalarıyla birlikte; (b) kabuk başlıkları
`.Value` ile aldığı için HTML-encode ediyor, eski `@Localizer[...]` etmiyordu — `_BulkActionBar` ile aynı davranış, altın başlıklarda
özel karakter yok; resx'e HTML koyan bir sayfa kabuğa geçerken bunu görecek.

**Paket 2 notları (2026-09-23, WP-UI-LIST-FACTORY-01, merge e9e00106e):** `DitenDataTable.createList` (dataMode fail-closed, filters,
savedView, quickView, form, toolbar); Golden Slim 991→246, Compact 685→163; 79 tesisat ismi fabrikada. CT'nin 6 sabotajından 4'ü
yeşil kalmıştı — tesisat boşlukları (arama sonrası dirty, populate, isDefault, filtre kancası kapsamı) → `list-factory-wiring.test.js`.
Canlı (DevEnablement açık): Apply/Reset/rozet/panel, Save View kaydet → yeniden yükle → otomatik uygulanıyor → sil, hızlı görünüm,
toplu seçim, düzenleme offcanvas'ı — hepsi ölçüldü. Bilinen: `createCrudTable` 84 eski çağıran için `dataMode`'suz kalıyor (bilinçli;
zorunluluk `createList`'te); `normalizeScalar` sayıyı `String()` yapıyor (eski sayfalar `''` yapıyordu — `1 ≡ "1"` kuralının doğru hâli).
**CT canlı bulgusu (paket 2 kabulü):** kayıtlı görünümle açılan sayfa dirty görünüyordu (Save View düğmesi görünür, oysa
captured == saved bayt bayt aynı): `applyState` filtreleri `applyViewToTable`'ın çiziminden SONRA atıyordu, çizimin
tetiklediği search/order olayları dirty'yi eski (boş) filtrelerle hesaplıyordu. Düzeltildi (filtreler önce, sonda senkron);
guard `list-factory-wiring.test.js` 3b — sayfanın lookup fetch'ini taklit eden bir await ile (onsuz hata görünmez).

**Paket 3 notları (2026-09-24, WP-UI-LIST-SERVER-01, merge 650057798):** fabrika `dataMode:'server'` (düz sorgu: start/length/search/
orderBy/orderDir/draw + filtreler anahtarıyla; zarf `{items,total,filteredTotal}` → DataTables); DevEnablement compact liste sorgusu
(orderBy beyaz listesi 400, Regex.Escape arama, Id ile biten sort, TenantId ile sınırlı sayımlar); Altın Compact = **sunucu referansı**
(`data_mode: server`), Slim = istemci (`client`, 200). Doğrulayıcı pack front matter'ını üçüncü kaynak olarak okuyor. Ajanın bulduğu:
ilk istek DataTables'ın 0. sütunuyla (`orderBy=id`) gidiyordu → kayıtlı görünümün/sayfanın sırası. CT'nin 6 sabotajından 2'si yeşil
kalmıştı (URL kodlama, orderDir büyük/küçük) → `list-factory-server-mode-wire-ct.test.js`. Canlı (14 geçici kayıt, sonra silindi):
sayfalama 15/10+5, sıralama priority desc, arama 5/15, filtre `status=Passive` 8/15, Save View → yeniden yükle → uygulanıyor, Save gizli;
telde `columns[` yok. Açık: liste sorgusu için Mongo indeksi yok (büyük kiracıda düşünülmeli); eski `personalizationClient` kırmızısı duruyor.

**Paket 3a notları (2026-09-24, WP-AUTH-USERS-LIST-QUERY-01, merge 1296644ab):** `GET api/users` sunucu sözleşmesi (start/length/search/
orderBy/orderDir/status/roleId/accountKind → `{items,total,filteredTotal,summary}`), parametresiz çağrı eski şekil; `IUserListReader`
(türetilmiş durum tek aggregate ifadesi; rol adları tek $lookup; TenantId her sorguda); `UserListRules` (400 + `USERS_LIST_*`). Auth
925/926 → 1008/1009. CT'nin 6 sabotajından 3'ü yeşil kalmıştı (yabancı rolün adı, çok kelimeli arama, yalnız roleId) → `UserListQueryTests.Ct.cs`.
Ajanın bilinçli seçimleri: varsayılan sıra createdAt desc; length>500 → 400; eski çağrıda pageSize=0 → 20. **Paket 5'e taşınan:** sayfanın
filtre değeri `Passive`, Auth `Inactive` bekler (eşleme); `USERS_LIST_*` kodları için Web'de 7 dilli köprü; liste sorgusu için Mongo indeksi yok.
Ajanın bildirdiği sınır ihlali: kendi `.bak` dosyasını `rm` ile sildi — yalnız o dosya, kayda geçti.

**Paket 5 notları (2026-09-24, WP-UI-USERS-LIST-01, feat a69630b92 → integration dd22b4f6f):** Kullanıcılar `_ListShell` + `createList`
(`dataMode:'server'`) üzerinde; index.js 1162 → 435; 79 tesisat ismi yok; dokunma protokolü 17 → 1 sapma; `_Filter` `Inactive`;
rol filtresi id; KPI'lar `data.summary`'den; #10 silme onayı 7 dil; K17/#12 bilinçli (toplu uç nokta yok, `HasSelection = false`
beyanı doğrulayıcıda tek istisna). CT: 7 ayrı sabotaj kırmızı; vitest tek başına 24 (paralel yükte iki "gerçek DataTables" testi
4–5 sn zaman aşımı — kırılgan, `list-factory-server-mode-real-datatables` ve `governance-users-list-server-wire`, süre artırılmalı).
Canlı (CT): tel `start/length/orderBy=email/orderDir`, filtre `status=Inactive|Invited` (0 / 7 satır, hepsi davetli), Reset, KPI 8/1/0/7,
hızlı görünüm, silme onayı metni + çöp ikonu, "+ Ekle" var, toplu çubuk yok, iskelet ortak. **Sahibe kararlar:** (a) Users pack'i yok —
MOD-0018-FU9 beş ekranı kapsıyor; pack şemasına ekran başına `data_mode` (ör. `screens:` haritası) eklensin mi? (b) "İşlem" menüsündeki
fabrika varsayılanı "İçe aktar (Yakında)" Kullanıcılar'da kalsın mı? **CT'ye kalan küçükler:** silme koruması kodları (`USER_DELETE_SELF`,
`USER_DELETE_LAST_STEWARD`) ekranda hâlâ "Delete failed." — önceden de öyleydi, 2 anahtar × 7 dil; K16/#11 dışa aktarma görünür
sütunlar (dt-defaults, sunucu modunda yalnız sayfa — tam dışa aktarma sunucu ucu ister). **Fabrika istekleri (paket 2.1, CT):**
`hideQuickView`/suppress, form başarı kancası (davet diyaloğu), antiforgery yardımcısı, kayıtlı filtre eşanlamı, dışa aktarma seçenekleri.
**Kanca:** ana checkout'un dalında yok (integration main'e girene kadar oradan açılan sohbetlerde koşmaz); kök artık düzenlenen dosyanın
worktree'sinden (a5af7f7bb).

**Ek 2026-10-04 — ürün listeleri (sahip yakaladı; sahip onayı "evet hepsi").** Global Ürün kabul edildi ama liste ekranı ortak kabuğa / fabrikaya
taşınmadı (dokunma protokolü GP turlarında koşmadı). Ölçüm (`verify_datatable_page.py --data-mode server --api-profile proxy --format gaps`): GSKU 1
(yalnız BL-452; FIX3 taşıdı) · Global Ürün 11 · ABB kayıt 18 · LSKU 18 · Tüzel Kişi Kapsamı 17 · FG 17 — beşi de kabuk / fabrika dışında. Toplu silme
sapmaları GxP ana veride `_ListShell` `HasSelection=false` ile düşer. Plan: WP-MDM-GP-LIST-01 (MDM sohbeti, GSKU-FIX4'ten sonra) · WP-MDM-ABB-LIST-01
(Roller, ABB-FIX4 kabulünden sonra) · LSKU FIX2 / Kapsam FIX1 / FG FIX1'de zorunlu madde. Kural: bir modülün kabul turu listelerinin `--format gaps`
çıktısını ister.

**Ek 2026-10-05 — liste taşımalarının ortak tuzakları (ABB-LIST ve GP-LIST gözden geçirmeleri).** (1) **Sessiz kısmi dışa aktarma yok (GxP):**
sunucu modundaki listede DataTables yalnız sayfayı tutar; Yazdır / CSV / Excel / PDF ya bütün satırları yazar (sunucu dışa aktarma, BL-452) ya da her
öğe açıkça "yalnız bu sayfa" der — GP eski etiketini kaybetmişti, ABB eskiden ürünün bütün kayıtlarını yazıyordu. (2) **Kayıtlı görünüm sütun
kayması:** seçim sütunu kalkınca dizinler kayar; eski görünüm ya taşınır ya sürümlenip bir kez bilgiyle sıfırlanır. (3) **Derin bağlantı yarışı:**
fabrika tutamacı sunucu modunda `initComplete`'ten önce döner; sayfa süzgeci init'ten SONRA uygulanmalı. (4) Liste okuma hataları sayfanın kendi 7 dil
cümlesine eşlenir (`onListError`). (5) Yetkisiz yüz testi sayfayı gerçek düzenle çizer (aksi halde "betik yok" ölçülmez). Kural: her liste taşıma
promptuna bu beş madde konur.

---
### BL-441

**İçe aktarma merkezi bir modül olmalı — sayfa başına "İçe aktar" düğmesi kaldırıldı**

DURUM: AÇIK — KARAR VERİLDİ (sahip, 2026-09-24) · SAHİP: CT (düğme kaldırma yapıldı; modül BL olarak bekliyor) · KAYIT: 2026-09-24

**Ne vardı:** `createList` fabrikası her liste ekranının Action menüsüne "İçe aktar" kalemi koyuyordu; tıklayınca yalnız
"Yakında" toast'ı çıkıyordu. Kullanıcılar ekranı testinde sahip fark etti: her sayfada var, hiçbirinde çalışmıyor.

**Karar (sahip):** düğme hiç konmaz — ne şimdi ne "Yakında" diye. İçe aktarma ilerde tek bir merkezi modülün işi olur;
ajanlar sayfa yaparken "içe aktarma ister misin" diye sormaz.

**Neden merkezi (Blueprint + SAP + Oracle):**

| | Sayfa başına düğme | Merkezi modül |
|---|---|---|
| SAP S/4HANA | Yok. Migration Cockpit (LTMC/LTMOM) ve Fiori "Import Data" uygulamaları: şablon indir → doldur → yükle → simülasyon → hata listesi → yükleme günlüğü | ✔ |
| Oracle Fusion | Yok. Import Management (CX) ve FBDI (ERP): şablon (xlsm) → CSV/ZIP → UCM'e yükle → ESS işi → hata raporu | ✔ |
| GxP (Veeva/MasterControl) | Yok. Yükleme = veri girişi olayı: kim, ne zaman, hangi dosya, hangi satır reddedildi; denetim izinde | ✔ |
| Blueprint | İçe aktarma satırı yok — bu BL onu açıyor | — |

En kolay olan (her sayfaya bir düğme) en doğru olan değil: doğrulama, eşleme, hata raporu ve denetim izi her sayfada
ayrı ayrı yazılamaz; yazılırsa her biri farklı davranır.

**Yapıldı (2026-09-24, entegrasyon dalı):** fabrikadaki `importBtn` varsayılanı silindi; `dt-defaults.js` içindeki
opt-in `extraButtons.importBtn` yolu duruyor (çağıran sayfa yok — 0 ölçüldü) ama kural onu yasaklıyor; kural satırı
`frontend-datatable-template.md` başlık bloğuna eklendi; guard `tests/list-factory-no-import-button-ct.test.js`
(fabrikanın DtDefaults'a verdiği toolbar'da `importBtn` yok + kaynakta `bx-import` yok).

**Modülün kapsamı (yapılınca):** varlık başına şablon (kolon sözlüğü + zorunlu alanlar), yükleme (dosya → satır
doğrulama → önizleme → onay), hata raporu (satır/kolon/sebep), kısmi yükleme kuralı (ya hep ya hiç mi, satır satır mı —
GxP için ya hep ya hiç), denetim izi (`IAuditableCommand`), yetki (`{module}.import` anahtarı), 7 dil. Tenant modülü.

---


### BL-442

**Yorum ekleme, görevi okuyamayan kişiye de açık — uç nokta yetki anahtarına bakıyor, okuma kuralına değil**

DURUM: AÇIK · SAHİP: SAHİPSİZ · BULAN: WP-PSS-TASK-INQUIRY-02 ajanı (rapor), CT doğruladı · KAYIT: 2026-09-24

Ölçüldü: `POST api/v1/tasks/{id}/comments` yalnız `[HasPermission(TaskPermissions.Read)]` ile korunuyor;
`AddTaskCommentHandler` `ITaskReadAccessPolicy`'yi yalnız @bahsedilen kişileri doğrulamak için kullanıyor
(`TaskMentionValidation`), yazarın görevi okuyabilip okuyamadığını sormuyor. `platform.tasks.read` anahtarı olan herkes
kiracıdaki her göreve (okuyamadığı dahil) yorum yazabilir; aynı kişi görevi GET ile açamaz (okuma kuralı 404 verir) ama id'yi
bilirse yorum bırakır. BL-439 bunu büyütmedi (sorulan kişi zaten okuma kazanıyor) ama gördü.

Düzeltme: handler'da `_readAccess.CanReadAsync(task, _currentUser.UserId)` → değilse 404 (okuma kuralının verdiği cevapla
aynı; 403 görevin varlığını sızdırır). PUT/DELETE yorum yolları da aynı soruyu sormalı. Test: okuyamayan yazar → 404,
hiçbir yorum yazılmadı; sorulan kişi (BL-439) → yazabilir. Gerileme riski: düşük — ek kontrol, mevcut yazarlar (holder,
talep sahibi, izleyici, yönetici) zaten okuma kuralından geçiyor.

---

### BL-443

**Diyalog içindeki kişi seçicide seçeneğe fareyle tıklayınca diyalog kapanıyor**

DURUM: KAPANDI — YANLIŞ ALARM (CT ölçüm hatası, 2026-09-24 akşam) · SAHİP: CT · BULAN: CT canlı tur (BL-439) · KAYIT: 2026-09-24

**Düzeltme:** kusur yok. Sabahki iki kapanma, CT'nin tarayıcı bölmesinde yanlış koordinat çerçevesiyle tıklamasından oldu (ekran
görüntüsü 800×763 iken 1333 genişlik varsayıldı; tıklamalar diyaloğun dışına, arka plana düştü; arka plana tıklama diyaloğu
tasarım gereği kapatır). Akşam aynı diyalogda gerçek fare tıklamasıyla kutu açıldı, "Ayşe Korkmaz" seçildi, diyalog açık kaldı
(iki kez: programatik açılış ve gerçek tıklama). Açılır liste popup'ın içinde (`dropdownParent`), SweetAlert 11.14.5. Kod
değişikliği yok.

Ölçüldü: WCN "Bilgi bekle" diyaloğunda `#wcnWaitingOn` select2 seçicisi (`DitenDialog.bindDialogSelect2`); açılır listeden
"Ayşe Korkmaz"a fareyle tıklanınca SweetAlert diyaloğu seçim yapılmadan kapandı (iki kez). Klavye (Aşağı + Enter) çalıştı.
Olası neden: select2 açılır listesi popup'ın dışına çiziliyor ve SweetAlert `allowOutsideClick` bunu dışarı tıklama sayıyor;
ya da `dropdownParent` popup değil. "Başkasına ata" aynı yardımcıyı kullanır — onda da beklenir. Test: gerçek DOM'da
select2 seçeneğine mousedown+click → diyalog açık kalmalı, değer seçilmeli.

---

### BL-444

**Bekleyen görevin detay sayfasında aynı bekleme cümlesi dört kutuda tekrar ediyor**

DURUM: KAPANDI (CT, 2026-09-25; sahip canlı bakacak) · SAHİP: CT · BULAN: CT canlı tur (BL-439) · KAYIT: 2026-09-24

Ölçüldü: görev Waiting'e alınınca WCN detayında üst üste dört kutu: "Şu an duraklatıldı: Ayşe Korkmaz bekleniyor — …",
"Bu görev duraklatıldı: Ayşe Korkmaz bekleniyor — …", "Bu görev başkasından gelecek bilgiyi bekliyor.", "Ayşe Korkmaz
bekleniyor — …" (resx: `…duraklatıldı: {0}` ×2, `NoticeWaitingExternal`, `{0} bekleniyor — {1}`). Bilgi aynı, dört kaynak
(durum şeridi rehberi + BL-437/439 rehberi + bekleme notu + bekleme çipi). Tek cümle + çip yeter; hangisinin kalacağı UX kararı.

**Sahip onayladı (2026-09-24), CT uyguladı (2026-09-25):** duraklatılmış görevde rehber kutusu çizilmiyor; çözümleyicinin genel
"başkasından bilgi / onay / inceleme bekleniyor" notu yalnız sayfada kişi/neden cümlesi yokken çiziliyor (o zaman tek satır o).
Kalan iki yüzey: yaşam döngüsü şeridi + bekleme notu. Metinler resx'te duruyor. Guard: `workcenter-next-detail-page.test.js` BL-444
describe'ı (2 test; sabotaj: süzgeç kaldırılınca ve rehber geri gelince kırmızı).

CT önerisi (uygulanan): üstteki durum şeridi ("Şu an duraklatıldı: …") ve satır çipi
("X bekleniyor — …") kalır; "Bu görev duraklatıldı: …" rehber kutusu ve "Bu görev başkasından gelecek bilgiyi bekliyor." notu kalkar.
İş küçük (WCN app.js, metin silinmez, yalnız çizim), CT yapar.

---

### BL-445

**Görev bildirim e-postaları Türkçe kiracıda İngilizce gidiyor**

DURUM: KAPANDI — VERİ, kod kusuru değil (CT ölçümü 2026-09-24 akşam) · SAHİP: sahip (ayar) · BULAN: CT canlı tur (BL-439) · KAYIT: 2026-09-24

**Ölçüldü:** dil zinciri kodda yazılı (`TenantNotificationLocaleResolver`): çağıranın verdiği dil → `Tenant.Settings.Language` →
`Tenant.DefaultLanguage` → "en". Kullanıcı başına dil alanı YOK (Auth `User` entity'sinde Locale/Language yok; resolver yorumu bunu
"bilinen boşluk" diye yazmış). admin@diten.com'un kiracısı `00000000-…-0001` = "Platform Admin Tenant" (kod PLATFORM), dili
`DefaultLanguage=en`, `Settings.Language=en` → e-postalar İngilizce. Arayüzün Türkçe olması tarayıcı çerezinden (kültür çerezi),
kiracı dilinden değil. **Sahip adımı:** Kiracı Ayarları'nda dil = tr (ya da "Dev Tenant" gibi tr kiracıyla test). Kalan ürün
boşluğu: kullanıcı başına tercih dili (Auth alanı + resolver 1.5. halka) — ayrı karar, şimdilik açılmadı.

Ölçüldü: `platform.tasks.inquiryasked` ve `inquiryanswered` şablonları 7 dilde tohumlu; dispatch günlüğü `Locale="en"`;
Mailpit'teki iki e-posta İngilizce ("A task is waiting for your answer", "Your question was answered"); arayüz Türkçe.
Alıcının dili çözülmüyor (kullanıcı tercihi / kiracı varsayılanı) ya da varsayılan en. Karar: alıcı dili = kullanıcı tercihi →
kiracı varsayılanı → en; ölçüm: Türkçe tercihli alıcıya tr şablon.

---

### BL-332

**KYS Tasarımcısı'nda üç switch her kayıtta sessizce false'a düşüyor**

DURUM: AÇIK · SAHİP: DOKÜMAN YÖNETİMİ GELİŞTİRİCİSİ (bu modül bizim değil — sahip kararı 2026-09-02: dokunulmadı, yalnız kayda geçirildi) · BULAN: CT · KAYIT: 2026-09-02 (eski `docs/product-backlog.md`'den taşındı 2026-09-24)

Modül: Doküman Yönetimi · Sayfa: KYS Temel Çizgileri → Tasarımcı (`/DocumentManagement/QmsBaselines/Designer`) ·
Konum: `frontend/Diten.Web/Views/DocumentManagement/QmsBaselines/Designer.cshtml:158-174`.

Ölçüm (2026-09-02): MVC aynı adlı iki alandan ilkini bağlar. `allowsManualChildren` (158→159) checkbox önce, hidden sonra → doğru;
`templatesAllowed` (163→164), `isMandatory` (168→169), `isProtected` (173→174) hidden ÖNCE → hep false. Daha kötüsü: panel mevcut
değeri yükleyip switch'i AÇIK gösteriyor (`designer.js:435-438`), kullanıcı başka alanı değiştirip kaydedince üçü false yazılıyor;
`isProtected` bir koruma bayrağı, sessizce temizleniyor, uyarı yok.

Düzeltme küçük: üç `<input type="hidden">`'ı kendi checkbox'larının ALTINA almak (158-159 deseni). Aynı desen daha önce dört formda
düzeltilmişti; artıklar: CRM "Birincil kişi" switch'i, Dev Sandbox (Golden Slim/Compact) switch'leri. İlgili: canlı doğrulama
boşluğu — bu sınıf hatayı geçen testler görmez, yalnız işaretleme sırası ya da canlı ekran gösterir.

---

### BL-446

**CI phase1 kapısı main'de 2026-08-10'dan beri kırmızı — runner'ın dili boş, görünüm testleri düşüyor**

DURUM: KAPANDI — entegrasyon dalında düzeltildi (2026-09-24, PR #122) · SAHİP: CT · BULAN: PR #122 CI'ı · KAYIT: 2026-09-24

Ölçüldü: `phase1-gates` main'de son beş koşuda kırmızı (452a6be, f53e29ec, 7ea32d45, f1681da2, 5500530a); her seferinde aynı altı test:
`ActiveSwitchBindingTests` × `Views/Tasks/FieldDefinitions/_Form.cshtml` (3 test × 2 anahtar), hata
`MissingManifestResourceException: No manifests exist for the current culture` — görünümün 16. satırı
`StringLocalizer.GetAllStrings(includeParentCultures: true)` (2026-08-10, be7918ed) çağırıyor; ubuntu runner'da `LANG` boş →
süreç kültürü invariant → nötr resx olmadığı için kaynak kümesi yok. Geliştirici makinesinde tr/en olduğu için yeşil.
Üretimde RequestLocalization her isteğe desteklenen 7 dilden birini verir; test yardımcısı (`RenderAsync`) boru hattını atlayıp
süreç kültürünü miras alıyordu. Düzeltme: yardımcı üretimin garantisini sabitliyor (`CurrentUICulture = en`, try/finally).
Sabotaj: yardımcıya invariant sabitlenince aynı 6 kırmızı; düzeltmeyle 12/12 ve Web 229/229.

Aynı deseni kullanan diğer görünümler (`_DitenShortcuts`, WCN `_L10n`, RoleAssignments `_IndexL10n`, `_WorkflowL10n`) CI'da
render edilmiyor; edilirse aynı sabitleme gerekir. Not: `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` bunu taklit ETMEZ
(`new CultureInfo("en")` orada CultureNotFound verir); doğru taklit süreç içinde `CultureInfo.InvariantCulture` sabitlemek.

---

### BL-447

**Toplantı dilimlerinin sırası — takvim (S3b) tutanak (S6) ve takipten (S7) önce mi?**

DURUM: AÇIK — KARAR BEKLİYOR (sahip 2026-09-24: "sonra konuşuruz, acil değil") · SAHİP: CT · KAYIT: 2026-09-24

MOD-0357 kalan dilimler: S6 tutanak → S7 takip → S3b takvim → S9 inceleme kapısı → S11 tekrarlama → S10 canlı geçiş.
Sahip takvimi öne almak istiyor (Görev Merkezi + Toplantılar ortak bileşen). Takvim S6/S7'ye bağımlı değil (kabul edilen +
bekleyen davetler zaten projeksiyonda; reddedilenler gizli). Karar: (a) S3b önce, S6/S7 sonra — takvim erken görünür, tutanak
gecikir; (b) sıra korunur — ISO 9001 §9.3.2 (önceki toplantının açık aksiyonları sonraki gündemde) S7 ile kapanır, takvim bekler.
CT önerisi: (a), çünkü takvim iki motor dilimini (plan bloğu) tetikliyor ve Kanban/WCN ile aynı dosyalara dokunuyor; S6/S7 toplantı
tarafında ayrı ilerler. Karar takvim konuşmasında verilecek.

---

### BL-448

**Başlattıklarım'da görevin kimde olduğu görünmüyor — her satır "talep eden" çipini çiziyor**

DURUM: KAPANDI — `fix/wc-outbox-assignee` (2026-09-24; canlı doğrulama sahibin girişinden sonra) · SAHİP: CT · BULAN: sahip (kontrol turu) · KAYIT: 2026-09-24

Ölçüldü: liste satırının kişi çipi her zaman `item.requester` (app.js `chip('requester', …)`); atanan yalnız detay sayfasında
(`DetailAssignee`). Sahibin başlattığı ve başkasına verdiği işlerde çip "Ben"/kendi adı → "Ali'ye verdiğim hangisi" sorusu
cevapsız. Düzeltme: satır, okuyanın başlattığı ve ADI BİLİNEN başka birinin tuttuğu işte tutan kişiyi çizer (ikon
`bx-user-check`, tooltip `DetailAssignee` = "Atanan", 7 dilde var); gelen kutusu, kendine açılan iş, soru ve onay kalemleri
aynen kalır. Guard `tests/wcn-outbox-assignee-chip-ct.test.js` (3): sabotaj (eski davranış) ilk testi kırmızı yapar.

**İkinci yarı (sahip, aynı gün): "filtreden de seçemiyorum".** Filtre panelinde yalnız Başlattıklarım sekmesinde "Atanan" seçicisi
(çoklu; seçenekler sekmede gerçekten var olan tutan kişiler, yer tutucu/etiket `DetailAssignee`, yeni metin yok); arama kutusu
artık tutan kişinin adıyla da buluyor; sıfırlama iki yolda da ekseni temizliyor. Guard +3 (seçici yalnız Başlattıklarım'da ve
tutanları listeler · seçince yalnız o kişinin işi kalır, temizleyince hepsi döner · arama adla bulur); üç sabotaj (süzgeç kaldırıldı ·
arama atananı görmüyor · seçici her sekmede) ayrı ayrı kırmızı. Canlı (2026-09-24, admin): S10B-Planla Testi Ayşe'ye atandı →
Başlattıklarım satırında "Ayşe Korkmaz" çipi (tooltip Atanan), filtrede "Atanan" seçicisi tek seçenekle Ayşe.

---

### BL-449

**Talep sahibi başkasının tuttuğu görevde "Planla" görüyor — kural mı, kusur mu?**

DURUM: KARAR VERİLDİ (sahip, 2026-09-29): `plan` yalnız işi tutan kişide; talep sahibi beklentiyi son tarihle verir — 2a motor promptunda uygulanır · önceki: AÇIK — KARAR BEKLİYOR (sahip sordu 2026-09-24) · SAHİP: CT · KAYIT: 2026-09-24

Ölçüldü (admin, Başlattıklarım, Ayşe'nin tuttuğu S10B-Planla Testi): eylemler `reassign` (birincil), `plan`, `cancel`,
`scheduleReviewMeeting`; satır menüsünde "Planla" görünüyor. Bu, 2026-09-11 kural incelemesinde (BL-361) yazılan kuralın sonucu:
"planla = tutan VEYA talep sahibi". Yani bugün kusur değil, karar. Ancak takvim kararlarıyla çelişir: planlama kişisel zaman bloğu
olacak ("kendi planladığım işte sunucu sert engeller"), talep sahibinin başkasının gününe blok koyması anlamsız. SAP/Oracle'da da
iş planı (ne zaman yapılacağı) işi yapanın, talep sahibinin elindeki alan son tarihtir. CT önerisi: `plan` yalnız tutan kişide;
talep sahibi beklentiyi kaynak son tarihle ifade eder. Karar gelince BL-361 kural tablosu ve projeksiyon güncellenir (küçük iş).

---

### BL-450

**Kullanıcı silme / pasife alma koruma kodları ekranda ham kod olarak görünüyor — 3 anahtar × 7 dil**

DURUM: AÇIK · SAHİP: l10n prompt (CT yazar) · BULAN: CT (Kullanıcılar testi, paket 5 kabulü) · KAYIT: 2026-09-24

Auth `DeleteUserCommandHandler` iki reddi kodla döner: `USER_DELETE_SELF` (kendini silemezsin) ve `USER_DELETE_LAST_STEWARD`
(son yöneticiyi silemezsin). 2026-09-25 eklendi: `SetUserActiveStatusCommandHandler` → `USER_DEACTIVATE_SELF` (kendini pasife
alamazsın; bulgu 33). Kullanıcılar ekranının hata köprüsünde (`Governance/Users/index.js` → `UsersIndex.*.resx`) bu üç
kodun karşılığı yok → toast İngilizce sunucu cümlesini basar. İş: köprüye üç anahtar + 7 resx'e metin; guard: kod ⇔ köprü ⇔ resx
(Auth `UserLifecycleErrorCodeContractTests` kalıbı). Kural gereği metin işi l10n ajanına gider (Tenant = 7 dil).

---

### BL-451

**Takvim başlangıç kabulleri — sahip kararı bekliyor (kontrol turu sonrasına ertelendi)**

DURUM: KARAR VERİLDİ (sahip, 2026-09-29: üç kabulün üçü de evet) — motor promptu (2a) yazıldı, worktree `task+calendar-engine` · önceki: AÇIK — KARAR BEKLİYOR · SAHİP: sahip (karar), CT (prompt) · KAYIT: 2026-09-24

Ortak takvim (Görev Merkezi + Toplantılar, MOD-0357 S3b; tasarım kararları 2026-09-17 konuşmasında verildi: ay/hafta/gün, ay'a
bırakma = gün, hafta/güne bırakma = saat bloğu, bırakma = Planla, çakışma = kendi işinde sert engel / davette uyarı, sol panel üç
sekme, WCN kartı yeniden kullanılır, FullCalendar yerel paket + 7 dil + RTL, Codex ile sınır: tek kişisel plan bloğu MOD-0024'ün).
Prompt yazılmadan önce üç kabul:
1. **Çalışma saatleri:** v1'de kiracı düzeyi varsayılan saatler + çalışma takvimi (CAND-CAP-0010) günleri; kişi/vardiya HR (MOD-0280)
   gelince aynı yerden okunur. Ölçüldü: projede vardiya/çalışma düzeni varlığı yok; HumanCapitalService'teki "TimeAttendanceLeave"
   yalnız hazırlık kaydı.
2. **Önce motor:** `TaskItem`'a planlanan başlangıç saati + süre; "Planla" saat alır; çakışma kuralı sunucuda; sonra bileşen.
3. **Ekstra özellik listesi:** sahip hatırlamıyor → yok sayılır; aklına gelince backlog.
BL-449 (talep sahibinde "Planla") ile birlikte karar. Sonra iki prompt: (2a) motor plan bloğu, (2b) bileşen + Görev Merkezi görünümü;
(2c) Toplantılar sayfası + davet kartları. Timesheet (DEC-002, MOD-0280 zaman girişi dilimi) ayrı, takvimden sonra.


**Karar notu (2026-09-29):** (1) çalışma saatleri v1 = kiracı varsayılanı 09:00–18:00 (öğle arası düşülmez) + çalışma takvimi günleri ve
tatilleri; sahip "ileride düzeltmek regresyon çıkarır mı" diye sordu → tasarım kuralı: tek dikiş `IWorkingHoursProvider` ("kişi P, gün D
için çalışma pencereleri"), Oracle HCM'deki gibi kişi → atama → birim → tüzel kişi → kiracı varsayılanı çözüm zinciri; v1 yalnız son
halkayı doldurur. Başka hiçbir kod kiracı ayarını doğrudan okumaz (koruma testi). Planlar mutlak başlangıç + süre olarak saklanır; saat
değişince var olan plan taşınmaz, "çalışma saati dışında" uyarısı çıkar. (2) önce motor, sonra bileşen. (3) ekstra özellik yok, gelirse
backlog.
---

### BL-452

**Dışa aktarma standardı — izin rolde, içerik ekranın tamamı, kalite kayıtlarında denetim izi**

DURUM: KARAR VERİLDİ (sahip, 2026-09-24) — YÜRÜYECEK (4 prompt) · SAHİP: CT · KAYIT: 2026-09-24

**2026-09-25 — Paket 1 birleştirildi** (WP-UI-EXPORT-01, bd9d4cc6f, merge 24c078de0 `chore/ct-round-2`): ortak yapı taşı
`Diten.BuildingBlocks.ListExport`; Auth `GET api/users/export` + Altın Compact `/export`; fabrika `export: { mode: 'server', url }`;
kural + doğrulayıcı sapması. Pakette bilerek yok: PDF/Yazdır kontrollü kopya (paket 2), ayrı dışa aktarma izni (paket 3), eşik üstü
arka plan işi (paket 4), denetim kaydı (Platform dışındaki servislerde yazıcı yok; kalite kaydı listeleri Platform'da). Bulunan:
BL-457 (DevEnablement testleri sln/CI dışında).

**CT kabul incelemesi (bağımsız ajan + CT, 2026-09-25):** kiracı süzgeci jetondan, izin listeyle aynı, beyaz liste yalnız ekran
sütunları, 7 dil tam, fabrika kapalı-varsayılan — doğrulandı. **Düzeltildi (kabulde):** 413 reddi sayımdan sonra 50 000 tam belgeyi
belleğe çekip atıyordu ve sayım ile okuma arasında gelen satır dosyayı sessizce kesebiliyordu → okuyucu sayım kapı aşınca hiç okumuyor
(`RefuseAbove`), okuma cap+1 satır istiyor, fazlası ret (Auth + Golden aynı şekil; testler: "reads no user document", yarış testi,
Golden depo testi). Blob URL'si bir dakika sonra serbest (Firefox/Safari büyük indirmeyi düşürebiliyordu). Kural: XLSX'te önek yok,
metin hücre. **Nota alındı (bu pakette değil):** Golden `records.export` anahtarı `records.read`'e bağlı değil (eski davranış; paket 3
izin tasarımında çözülür) · XLSX 50 000 × 8 hücre stil maliyeti ölçülmedi (paket 4 eşiği) · 401'de tablo yenileniyor, dışa aktarma
yeniden denenmiyor · başında boşluk/LF olan formül hücresi korunmuyor (düşük risk).

**Paket 2 kabul edildi (WP-UI-EXPORT-02, c3de1c02c → merge 0f7c17d0a, 2026-09-25):** PDF ve Yazdır tek kontrollü-kopya nesnesinden
(ekran, şirket, filtre, arama, sıralama, satır, oluşturan, zaman; altbilgi "kontrolsüz kopya" + sayfa x/y); sunucu modunda satırlar
paket 1'in CSV ucundan; zh/ar'da PDF yazdır penceresine (pdfmake yalnız Roboto: Arapça 0/256, CJK 0/20 992 ölçüldü). CT kabul
düzeltmesi: Content-Type text/html olan 200 dosya sayılmaz (giriş yönlendirmesi). **Canlı: sahip A–D ok (2026-09-25)** — Yazdır bloğu,
PDF = ekran (Soyad gizli, Davet edildi süzgeci), İşlem menüsü Türkçe, Arapça'da yazdır yolu. **Nota alınan:** `createList` kullanmayan eski
sayfalarda (≈150, `exportButtons` çağıranlar) Yazdır/PDF hâlâ ekrandaki sayfayı basar ve başlık "Satır: 10 / Filtre: yok" der —
altın geçişi (BL-440) tamamlandıkça kapanır, ayrı iş açılmadı · Chrome'da kontrolsüz kopya satırı iki kez (kenar kutusu + gövde sonu
yedeği), Firefox/Safari'de sayfa x/y yok · zaman dilimi tarayıcının, adıyla yazılıyor · CSV dışa aktarma sürerken PDF tıklaması
sessizce yok sayılıyor · sunucu CSV'sindeki formül koruma kesme işareti PDF'te görünüyor (kozmetik; sunucu tarafı PDF paket 4).

**2026-09-25 — Paket 2 hazır, CT kabulü bekliyor** (WP-UI-EXPORT-02, `feat/ui-export-pdf`, commit CT'de): PDF + Yazdır =
kontrollü kopya (başlık bloğu: ekran, şirket, filtre, arama, sıralama, satır, oluşturan, saat dilimli tarih; altbilgi: kontrolsüz kopya +
sayfa x/y), sunucu modunda satırlar paket 1'in CSV'sinden (`exportUrl('csv')`), 7 dil, kural + doğrulayıcı mekaniği. **Paket 4'e not
(sunucu tarafı PDF):** vendored pdfmake yalnız Roboto (Arapça 0/256, CJK 0/20 992) → zh/ar'da PDF düğmesi tarayıcı yazdırmasına
düşüyor; gerçek zh/ar PDF'i ve 50 000 satırlık PDF (tarayıcı belleği) sunucuda üretilmeli. Firefox/Safari'de yazdırmada sayfa x/y
yok (`@page` kenar kutusu desteklenmiyor).

**Bugün:** liste ekranlarında tarayıcı tarafı düğmeler (Yazdır/CSV/Excel/PDF/Kopyala, `dt-defaults.js`); sunucu modunda yalnız
ekrandaki sayfa çıkıyor; denetim kaydı ve yetki ayrımı yok. Doğru yapılmış iki örnek: İş Raporu (sunucu tarafı, ekranla aynı sorgu,
kiracı denetim kaydı `IDataExportAuditWriter`, BL-346/347) ve CRM Kişiler (XLSX şablon = dışa aktarma şeması, geri yüklenebilir,
MOD-0150). SAP: dışa aktarma ayrı yetki, ekranın filtre/sıralama/sütunlarıyla tüm satırlar, büyük veri arka plan işi, resmi çıktı
rapor. Oracle Fusion: "Export to Excel" görünen sütun ve filtreyle tüm satırlar, BI Publisher biçimli çıktı, rolle indirme yetkisi.
GxP: kalite kaydı listesinin dışa aktarımı kontrollü kopya (kim/ne zaman/filtre/satır denetim izinde).

**Standart (sahip onayı 2026-09-24):**
1. **İzin rolde:** modül başına `{modül}.export`; kapalıysa menü çizilmez (UAS-001 ile aynı ruh).
2. **Menü aynı beş seçenek:** CSV, Excel, PDF, Kopyala, Yazdır.
3. **Dosya = ekran:** görünen sütunlar (K16 ✔), uygulanan filtre + sıralama, TÜM eşleşen satırlar. Sunucu modunda dışa aktarma
   sunucudan aynı sorguyla (İş Raporu deseni); tarayıcı tarafı düğmeler yalnız istemci modunda.
4. **Denetim izi:** kalite kaydı taşıyan listelerde her dışa aktarma `IDataExportAuditWriter` ile (kim, ne, filtre, satır, biçim).
5. **PDF kontrollü kopya:** başlıkta kiracı, ekran, kullanıcı, tarih, filtre özeti; altta satır sayısı, sayfa X/Y.
6. **Eşik:** sınır üstü (öneri 50 bin satır) arka plan işi + bildirimle indirme bağlantısı (Hangfire var).
7. **Simetri:** dışa aktarma şeması = içe aktarma şablonu (kod sütunları dahil); BL-441 modülü bunu kullanır.

**Paketler:** (1) fabrikaya `export: { mode: 'server', url }` + ortak sunucu uç nokta sözleşmesi + Kullanıcılar ve Golden Compact
(referans) · (2) PDF kontrollü kopya · (3) izin anahtarı + manifest + denetim izi · (4) arka plan işi. Kural dosyası ve doğrulayıcı
(1) ile güncellenir; 7 dil menü metinleri zaten var.

---

### BL-453

**Auth her açılışta beş sahte kullanıcı ekliyordu — kontrol turu ortasında silinen kullanıcılar "geri geldi"**

DURUM: KAPANDI — `chore/ct-round-2` (2026-09-24) · SAHİP: CT · BULAN: sahip (kontrol turu: "benim açmadığım bir sürü kullanıcı geldi") · KAYIT: 2026-09-24

Ölçüldü: `DataSeeder.SeedMockUsersForTenantAsync` (Auth Persistence), DefaultTenant ve Tenant97c5 için kiracıda admin dışında
kullanıcı yoksa (`count <= 1`) john.doe/jane.smith/bob.johnson/alice.williams/charlie.brown (`.def@diten.com`, Aktif, rolsüz)
ekliyor; anahtar yok, her Auth açılışında koşuyor. Sahip test kullanıcılarını silip servisler yeniden başlayınca beş kişi belirdi.

Düzeltme: tohum opt-in — `DevSeeds:MockUsers` (Platform `PositionSeed` kalıbı: Development VE anahtar); `SeedAsync(database,
seedMockUsers=false)`, DI kapısı `AddPersistence`. Varsayılan kapalı; yerel, commit'lenmeyen appsettings ile açılır. Auth tohum
testleri 282/283 (tek kırmızı eski `PermissionScopePreservationTests`). Sahip beş sahte kullanıcıyı ekrandan siler; Auth yeniden
başlasa da dönmez (canlı kanıt bekleniyor).

---

### BL-454

**E-posta bildirimleri: tasarım yok, gönderici Gmail hesabı — kurumsal şablon ve kurumsal gönderici**

DURUM: AÇIK — sahip kararı 2026-09-24 ("Kullanıcılar sayfasından sonra bakalım") · SAHİP: CT (tasarım + gönderim prompt'u) · KAYIT: 2026-09-24

**2026-10-02 — sahip kararları (Kullanıcılar turu, CT'ye iletildi):** (a) **E-posta kabuğu** ayrı iş paketi, önce tasarım onayı: tek kabuk
(logo, kiracı adı, başlık, gövde, tek eylem düğmesi, altbilgi) `Diten.Building.Blocks` altında; her e-posta yalnız içeriğini verir; dil alıcının
dili; Outlook / koyu mod bir kez çözülür. Bugün elle yazılmış ayrı HTML şablonları iki serviste: Auth (`TenantUserInvitationEmailTemplate`,
`PlatformPasswordResetEmailTemplate`, OTP) ve Platform (`AdminUserInvitationEmailTemplate`, `PlatformAdministratorInvitationEmailTemplate`,
`SmtpMessagingProvider`). İlk tüketici kullanıcı daveti, sonra görev atama. Sahibe iki örnekli prototip gösterildi (davet + görev ataması);
onay bekliyor. (b) **Gönderen adı kiracıya göre** ("Diten Pharma (Di10 üzerinden)"), yanıt adresi kiracının — küçük iş, kabuk paketiyle birlikte.
(c) **İki servisin gönderen adı tek olsun** (bugün Auth "Diten ERP", Platform "Diten PPM") — aynı pakette. Kiracının kendi alan adından gönderim
büyük iş → BL-497.

**Sahibin gözlemi:** e-postalar "berbat gidiyor": düz metin, marka yok, başlık/altbilgi yok; dil kiracı ayarından (BL-445). Sunucu
şu an bir Gmail hesabıyla gönderiyor (canlı SMTP ayarı); kurumsal hesap/adres olmalı.

**İki iş:**
1. **Tasarım:** tek e-posta düzeni (logo + kiracı adı başlık, gövde, eylem düğmesi, altbilgi: gönderen sistem, bildirim ayarı
   bağlantısı, "bu e-postaya yanıt vermeyin"), tüm şablonlar bu düzene oturur (davet, parola, görev soru/cevap, onay, toplantı daveti
   .ics ile); 7 dil; koyu/açık istemcilerde okunur; metin kopyası (plain) her zaman birlikte. SAP ve Oracle sistem e-postaları
   markalı şablon + sabit sistem gönderici ile gider (Oracle: bildirim şablonları/BI Publisher; SAP: SAPconnect + şablon).
2. **Gönderici:** Gmail kişisel/posta kutusu yerine kurumsal alan adından no-reply adresi (Google Workspace altyapısı var, bkz. toplantı
   modülü kararı); SPF/DKIM/DMARC kaydı; ayar platform Bildirim Ayarları ekranından, kimlik bilgisi sır deposunda (kodda/appsettings'te
   değil); gönderim başarısızlığı Bildirim Gönderimleri ekranında görünür (var). Kiracı başına gönderici adı (ör. "Diten Pharma ERP").

**Ölçülecek (prompt'tan önce):** canlı SMTP ayarı nerede (platform Bildirim Ayarları mı, appsettings mi), hangi şablonlar var (7 dil
tohumu), .ics ekli şablonların düzeni. Sıra: Kullanıcılar modülü testi bitince.

---

### BL-455

**Ctrl+K komut arar mı? — bugün yalnız sayfa; "Kullanıcı ekle" gibi eylemler ayrı iş**

DURUM: AÇIK — sahip 2026-09-24: v1 böyle kalsın, komutlar backlog · SAHİP: CT (WorkCenter/UI) · KAYIT: 2026-09-24

Ölçüldü: `TenantSearchController` kenar menüsüyle aynı kaynağı okur (`/api/platform/navigation/menu`, abonelik + yetki süzgeçli) →
Ctrl+K yalnız kullanıcının açabildiği SAYFALARI listeler; CRM yetkisi olmayan CRM görmez (doğru). "Kullanıcı ekle" yazınca Kullanıcılar
sayfası çıkar, ekleme formu açılmaz. SAP Fiori arama: uygulamalar (rol kataloğu) + kayıtlar; Oracle: gezgin rol bazlı, komut yok.
İleride: "komut" kalemleri (sayfa + eylem, ör. `/Users?create=1` ile ekleme formu açık; görev oluştur; toplantı planla), aynı yetki
süzgeciyle, 7 dilde ad; kısayol katmanı (BL-438) ile ortak kayıt. Karar gelince paket.

**Güncelleme 2026-10-02 (sahip):** ikinci sürüm şu an gerekli değil, iş listesinde kalır. Sahibin sorusu: "yeni modüllerde bunun olması, kurala
eklemek çok mu zor?" CT cevabı: kuralı yazmak kolay, ama kuralın karşılığı olan düzenek yoksa yeni modül uyamaz. İki adım:
(1) **Şimdi, ucuz (kod yok):** modül paketine "Hızlı eylemler" bölümü — modülün Ctrl+K'da bulunması gereken eylemleri (ad, açtığı adres ya da
form, gereken izin; kiracı modülünde 7 dilde ad). Yeni modül bunu paketinde yazar; ikinci sürüm yapıldığında eski modüller için borç birikmez.
(2) **İkinci sürümle:** bu liste modül kaydına (self-registration manifest) alan olarak girer, Ctrl+K menüyle aynı yetki süzgecinden okur.
SAP Fiori'de arama, her uygulamanın kataloğa bildirdiği "anlamsal nesne + eylem" çiftlerini bulur; düzenek aynıdır. Sahip (1) için onay verirse
"?" yardım kuralıyla (BL-495) aynı pakette yazılır: `.antigravity` kuralı + paket bölümü + küçülen defter.

---

### BL-456

**Kullanıcı yaşam döngüsü denetlenmiyor — ekleme/silme/davet/parola sıfırlama hiçbir denetim günlüğüne yazılmıyor**

DURUM: KOD TAMAM (WP-AUTH-PLATFORM-LINKS-01, merge a8406b353, 2026-09-25) — sahip canlı kontrolü bekliyor · önceki: AÇIK · SAHİP: CT (Auth/Platform) · BULAN: sahip (platform kontrolü P1: "en son kayıt bu mu?") · KAYIT: 2026-09-24

Ölçüldü: `CreateUserCommandHandler` ve `DeleteUserCommandHandler` ne `IRbacAuditRecorder` ne `IAuditableCommand` çağırıyor;
Auth'un kendi günlüğü (`AuthAuditLog`) yalnız kayıt (Register), hesap türü (SetAccountKind) ve RBAC olaylarını (rol/izin/atama)
tutuyor ve ekranı yok. Platform Denetim Günlüğü (`audit_events`) Auth olaylarını almıyor (MDM'nin S2S iletimi gibi bir köprü yok).
Sahibin platformda gördüğü son kayıt ("Execute → AuditEvent, DataPrivacy, kiracı …2121") başka bir kiracıda bir denetim
kaydının görüntülenmesi/maskelenmesi olayı — kullanıcı eklemeyle ilgisiz. GxP + KVKK: kimlik değişiklikleri denetlenmek zorunda.

İş: (1) Auth: kullanıcı oluştur/güncelle/sil/pasifleştir/aktifleştir/davet yeniden gönder/parola sıfırla → `IRbacAuditRecorder`
kalıbıyla `authAuditLogs` (kim, kime, önce/sonra, korelasyon); (2) Auth → Platform merkezi denetime S2S iletim (MDM `Faz 2`
deseni) → Platform Denetim Günlüğü'nde "Kimlik" kategorisi; (3) ekran: Platform Denetim Günlüğü kiracıyı adıyla göstersin
(bugün GUID), kategori/kiracı filtresi; Auth günlüğü için ayrı ekran gerekmez. Gerileme riski düşük (eklemeli).


### BL-457

**DevEnablement testleri hiçbir yerde koşmuyor — sln test projesini içermiyor, CI de servisi kapsamıyor**

DURUM: YARIM — sln tamam, CI karar bekliyor · SAHİP: CT (küçük) · BULAN: WP-UI-EXPORT-01 raporu (2026-09-25) · KAYIT: 2026-09-25

**2026-09-25 CT:** test projesi sln'e eklendi (`dotnet test` sln ile artık testleri buluyor). CI yarısı ölçüldü: `run_phase1_gates.sh`
HİÇBİR servis test projesini koşturmuyor (yalnız tenancy, architecture, Web.Tests); Auth/Platform/DevEnablement testleri EphemeralMongo
ile kendi `mongod` ikilisini ister, runner'da yok. Bu BL-457'den büyük bir CI kararı: runner'a mongod (apt/brew) + phase1'e üç servis
satırı. Sahip kararı bekliyor; o zamana kadar servis testleri yalnız yerelde koşuyor.

`services/Diten.DevEnablementService/Diten.DevEnablementService.sln` içinde `Diten.DevEnablementService.Api.Tests` yok; `dotnet test`
sln ile koşunca 0 test ölçüyor. `scripts/run_phase1_gates.sh` de DevEnablement'ı derlemiyor/test etmiyor. Bugün 91 test (Altın
Compact sunucu listesi + dışa aktarma sözleşmesi) yalnız csproj yoluyla elle koşuluyor; kimse koşmazsa kırmızı görünmez. İş:
test projesini sln'e ekle, phase1 kapısına DevEnablement Api.Tests satırı ekle (mongod gerektiren sınıflar CI'da atlanabilir
olmalı; Auth'un kalıbı). Kabul: CI günlüğünde DevEnablement test sayısı > 0.


### BL-458

**Access Governance sayfalarının eylemleri katalogda yok — izin eşitlemesi Kullanıcılar/Roller'i yönetmiyor**

DURUM: KOD TAMAM (merge a8406b353, 2026-09-25) — sahip canlı kontrolü bekliyor · önceki: AÇIK · SAHİP: CT (Roller turunda, BL-452 paket 3 ile) · BULAN: CT platform bağlantıları tablosu · KAYIT: 2026-09-25

`AccessGovernanceManifestProvider.cs:37` USERS sayfasını eylemsiz (`[]`) beyan ediyor; 388 eylem tanımının 0'ı ACCESS-GOVERNANCE'ın.
Katalog→Auth eşitlemesi bu yüzden yalnız `auth.users.read` ve `auth.users.assign-role`'ü biliyor; create/update/delete/
account-kind.manage/lookup Auth tohumunda (`DataSeeder.cs:393-405`). Rol İzinleri ekranı Auth tohumunu gösteriyor, katalog değil.
İş: USERS/ROLES/ROLE_PERMISSIONS/USER_ROLES sayfalarına eylemleri (create, update, delete, account-kind.manage, export=paket 3,
assign-role, assign-permission) manifestte beyan et; eşitleme tohumlu anahtarları korur (`ICatalogPermissionSyncService.cs:23`).
Tasarım notu: Auth manifestini Platform kendi içinde beyan ediyor (öz-kayıt değil); MDM/DevEnablement HTTP ile gönderiyor — bu
karar değiştirilmeyecekse belgelenir. Kabul: katalogda eylemler görünür, Auth'ta aynı anahtarlar, guard: manifest ⇔ tohum eşitliği.


**Kapsam (2026-09-25 ölçümü):** yazmayan işleyiciler CreateUser, DeleteUser, SetUserActiveStatus, AdminResetPassword,
ResendUserInvitation, UpdateUser (alan değişiklikleri); Platform'a köprü için kalıp MDM `PlatformAuditForwarder.cs` →
`InternalAuditController`. Bkz. Kullanıcılar kaydı "Platform bağlantıları" tablosu.
---

### BL-459

**Plan kullanıcı kotası (`UsersMax`) kiracı yöneticisinin kullanıcı eklemesinde uygulanmıyor; kiracı kullanıcı özeti yanlış sayıyor**

DURUM: KOD TAMAM (merge a8406b353, 2026-09-25) — canlı kota testi platform turunda planlı yeni kiracıyla (dev test kiracısı plansız) · önceki: AÇIK · SAHİP: prompt (Auth + Platform) · BULAN: CT platform bağlantıları tablosu · KAYIT: 2026-09-25

`QuotaKeys.UsersMax` yalnız Platform'un yönetici davetinde tüketiliyor (`InviteTenantAdminUserCommandHandler.cs:76`); Auth'ta kota
kodu yok → kiracı yöneticisi sınırsız kullanıcı ekleyebilir, abonelik planı boşa düşer. `GetTenantUsersSummaryQueryHandler.cs:34`
yalnız Platform `tenant.AdminUsers`'ı sayıyor, Auth kullanıcılarını değil → platform kiracı sayfasındaki sayı yanlış. İş: Auth
CreateUser (davet dahil) Platform'dan kota okur ve aşımda kodlu ret (`USER_QUOTA_EXCEEDED`, 7 dil); özet Auth'tan sayar. SAP/Oracle:
lisans sayımı her zaman merkezi ve sert.

---

### BL-460

**KVKK: kullanıcı silme kişisel veriyi bırakıyor; silme/anonimleştirme yolu yok**

DURUM: AÇIK · SAHİP: prompt (Auth, Platform ile) · BULAN: CT platform bağlantıları tablosu · KAYIT: 2026-09-25

Silme yalnız `IsDeleted` (`UserRepository.cs:161-163`): e-posta, ad, soyad Mongo'da kalıyor; `auth.login.empty_roles` günlük satırı
ham e-posta saklıyor (`AuthAuditService.cs:18`). Platform'un tek redaksiyonu kendi `audit_events`'indeki aktör alanları
(`redact-actor`); Auth'a hiç ulaşmıyor. İş: (a) silinen kullanıcı için saklama süresi + anonimleştirme (e-posta → hash, ad → boş),
(b) Platform veri-gizliliği talebi Auth'a köprü, (c) günlük meta verisinde ham e-posta yerine kullanıcı kimliği. GxP/ISO 27001 ile
çelişmemesi için denetim izinin kendisi kalır, kimlik anonimleşir. Kabul: silinen kullanıcı 30 gün sonra aranamaz, günlük satırları
kimlikle bağlı kalır.


### BL-461

**Elle verilen modül yetkilendirmesinde bitiş tarihi ve gerekçe zorunlu olsun**

DURUM: AÇIK · SAHİP: CT (platform turu, küçük) · BULAN: sahip sorusu + CT ölçümü · KAYIT: 2026-09-25

Ölçüm: kiracı erişimi her modül için `HasAccessAsync` ile (temel modül ya da yetkilendirme); plansız kiracı giriş yapar, yalnız temel
modülleri görür. Plan dışı elle yetkilendirme tasarımda var: `TenantModuleEntitlement` kaydı `Source` (plan/elle), `IsEnabled`,
`ExpiryDateUtc`, `Reason` taşır ve denetim günlüğüne düşer. Boşluk: plan değişince elle verilenler kendiliğinden kalkmaz; bitiş
tarihi girilmezse kalıcı olur ve kimse neden verildiğini bilmez. İş: kiracı yönetiminde elle yetkilendirmede bitiş tarihi + gerekçe
zorunlu (form + komut doğrulaması, 2 dil), bitiş geçince erişim kapanır (zaten `ExpiryDateUtc` okunuyor mu ölçülecek), kiracı
sayfasında "elle / plan / bitiş" görünür. Kullanıcı sınırı elle verilen modülden etkilenmez (kota planın). SAP/Oracle: trial
entitlement her zaman süreli ve gerekçeli.


### BL-462

**Denetim Günlüğü kendi okunmasını ana listede gösteriyor — iş olayları sayfadan kayıyor**

DURUM: AÇIK · SAHİP: CT (platform turu) · BULAN: sahip canlı test (2026-09-25) · KAYIT: 2026-09-25

Platform, denetim listesini açmayı ve bir kaydın detayına bakmayı da denetliyor (DataPrivacy, `IsMetaAudit=true`,
`PlatformAudit.GetAuditEventListQuery/GetAuditEventByIdQuery`; dev'de ~970 satır). Doğru bir kural (kim neyi okudu, KVKK/GxP), ama bu
satırlar aynı listede iş olaylarının arasına giriyor: sahip her detaya baktığında yeni bir "Execute" satırı eklendi ve kullanıcı olayları
ikinci sayfaya kaydı ("eskiler kayboluyor"). İş: liste varsayılanında meta-denetim satırları gizli + "Okuma kayıtlarını göster" süzgeci
(SAP SM20'de okuma denetimi ayrı süzülür; Veeva'da "audit trail views" ayrı rapor). Kayıt yazılmaya devam eder; yalnız görünüm.


### BL-463

**Yetki değişikliği kullanıcıya bir sonraki girişte ulaşıyor — geri almada 120 dakikaya kadar gecikme**

DURUM: AÇIK · SAHİP: CT (Roller turu) · BULAN: sahip canlı test (2026-09-25) · KAYIT: 2026-09-25

Ölçüm: izinler erişim jetonunun içinde (TokenService `permission` iddiaları); Web sayfası izinleri o jetondan okur
(`_PermissionBootstrap` → `IPermissionSnapshot`). Jeton ömrü 120 dk (`AccessTokenExpirationMinutes`), yenileme 7 gün. Sonuç: rolüne
izin eklenen kullanıcı çıkıp girene (ya da jeton yenilenene) kadar yeni izni görmez; sahip Ayşe'ye dışa aktarma verdiğinde menü ancak
yeniden girişte geldi. Verme yönünde bu sektörde normal (SAP SU01/PFCG: değişiklik bir sonraki oturumda; Oracle Fusion: yeni oturumda).
**Geri alma yönünde risk:** bir yetki kaldırıldığında sunucu tarafı `[HasPermission]` da aynı jetona baktığı için kullanıcı 120 dk'ya
kadar işlemi yapmaya devam edebilir. İş (Roller turu, MOD-0018-FU13 "izin önbelleği geçersizleştirme" paketiyle birlikte ölçülecek):
(a) Rol İzinleri / Rol Atama ekranında "değişiklik kullanıcının bir sonraki girişinde geçerli olur" notu (7 dil), (b) izin KALDIRMA ve
rol geri alma anında etkilenen kullanıcıların yenileme jetonları iptal + erişim jetonu ömrü kısaltma (ör. 15 dk) ya da jetona izin
sürümü damgası ve sunucuda sürüm kontrolü. Karar Roller turunda Blueprint + SAP + Oracle karşılaştırmasıyla.


### BL-464

**Görev şablonu görev tipi taşımıyor — tekrarlayan kuraldan üreyen görev tipsiz doğuyor**

DURUM: AÇIK — KARAR BEKLİYOR · SAHİP: CT (karar sonrası prompt) · BULAN: CT ölçümü (sahip sorusu: canlıda tekrarlayan görev) · KAYIT: 2026-09-25

Ölçüm: `TaskTemplate` alanları Kod, Ad, Başlık/Açıklama şablonu, Öncelik, varsayılan atama, Vade (gün), Kontrol listesi, Şirket,
varsayılan alan değerleri — `TaskTypeId` yok; `CreateTaskFromTemplateRequest` da tip almıyor. Tekrarlayan kural şablondan görev
üretir (`GenerateDueRecurringTasksHandler` → `CreateTaskItemFromTemplateCommand`), dolayısıyla üreyen görevin tipi yok: tipin
kapanış alanları ve sonuç kodları (QA tohumundaki 31 tip, Kural 4 v2) bu görevlerde çıkmaz. "Aylık registrasyon planı kontrolü"
gibi kanıt isteyen kontroller için sonuç alanı gerekiyorsa şablona tip bağlanmalı. SAP PM bakım planı görev listesine/tipe bağlıdır;
Oracle'da periyodik iş şablonu iş tipini taşır. İş: şablona isteğe bağlı `TaskTypeId` (tipin şirket/aktiflik kuralıyla), from-template
yolu tipi göreve yazar, şablon formunda tip seçici (7 dil). Başlık şablonunda yer tutucu yok (metin olduğu gibi kopyalanıyor) — "{ay}"
gibi dönem etiketi istenirse aynı işe eklenir.


### BL-465

**Görev yöneticisi kimseye iş atayamıyor: atama kapsamı yalnız kendi şirketi / astları; açık kapsam verme ekranı yok**

DURUM: AÇIK — KARAR BEKLİYOR · SAHİP: CT (Roller turu ile) · BULAN: canlı kurulum (sahip, 2026-09-25) · KAYIT: 2026-09-25

Ölçüm (canlı, ditenteknoloji.com): sahibin hesabıyla "Yinelenen Görev Kuralı Ekle" ekranında kişi ve pozisyon seçicileri boş;
`/Tasks/api/assignable-people` → `people: []`, `excluded: { total: 7, outOfScope: 7 }`; `assignable-positions` → `[]`. Kural BL-057
(`TaskAssignmentScopeResolver`): aday (1) aynı tüzel kişilikte, (2) raporlama zincirinde altımda ya da (3) bana açıkça verilmiş birim/
pozisyon kapsamında ise atanabilir; kapsam atayanın AKTİF pozisyonundan türetilir (`OrgDataScopeResolver`). Sahibin hesabının
organizasyonda aktif pozisyonu yok → kimse kapsamda değil. (3) numaralı "açık kapsam" için ekran yok. Sonuç: merkezi görev yöneticisi
(tekrarlayan kural, şablon kuran kişi) kimseye iş atayamıyor. Oracle'da görev/İK yöneticilerine "View All" güvenlik profili, SAP'de
yapısal yetkide geniş kapsam verilir. Öneri: açıkça verilen, denetlenen kiracı geneli atama izni (`platform.tasks.assign-any`, SuperAdmin
şablonunda değil, elle verilir) ya da (3) için kapsam verme ekranı. GDPR/KVKK sınırı (Polonya–Türkiye) nedeniyle varsayılan değil,
açık izin.

---

### BL-466

**Yinelenen kural "ayın ilk pazartesisi" gibi gün kalıbını desteklemiyor**

DURUM: AÇIK · SAHİP: CT · BULAN: canlı kurulum (sahip isteği, 2026-09-25) · KAYIT: 2026-09-25

Ölçüm: `TaskRecurrenceFrequency` = Günlük/Haftalık/Aylık/Çeyreklik/Yıllık + aralık; aylık kural başlangıç gününe bağlı
(`TaskRecurrenceSchedule.OccurrenceAt` → `AddMonths`). "Ayın ilk pazartesisi", "ayın son iş günü" gibi n'inci gün kalıbı yok (iCalendar
RRULE `BYDAY=1MO`). Geçici yol: ayın 1'inde üret, vade 7 gün (ilk pazartesi her zaman 1–7 arasındadır). SAP PM ve Oracle zamanlayıcıları
n'inci hafta içi günü kalıbını destekler. İş: kurala isteğe bağlı "ayın n'inci {gün}" ve "ayın son iş günü" (çalışma takvimiyle), form
+ zamanlama + testler (7 dil).


### BL-467

**Yinelenen kural listesi ve detay sayfası kişiyi/pozisyonu ve şablonu adıyla göstermiyor**

DURUM: AÇIK · SAHİP: CT (küçük) · BULAN: sahip, canlı (2026-09-25) · KAYIT: 2026-09-25

Canlıda Aliye'ye bağlı kural listede "Kime: Bir kişiye" olarak görünüyor; detay sayfası da yalnız atama türünü yazıyor, kişi/pozisyon
ve şablon adı yok. Sahip "Aliye'yi nereden bağladın" diye sordu. (Aynı gün bulunan asıl risk — Düzenle formunun kayıtlı kişi/şablonu
geri yüklememesi ve kaydetmenin atamayı silebilmesi — `_Form.cshtml` `data-selected` ile düzeltildi, render testi +
`recurrence-rule-assignable-people-envelope` testi, sabotaj kırmızı.) İş: kural DTO'suna ad alanları (kişi görünen adı Auth
`internal/users/display-names` ile, pozisyon ve şablon adı Platform'dan) ya da liste/detayda aynı arama; "Kime" sütunu "Aliye Celikel"
/ "Havuz: Regulatory Affairs Officer" gösterir; detayda şablon adı. 7 dil etkisi yok (veri).


### BL-468

**Kendi dal disiplinimiz: büyük birikmiş dal, gönderilmemiş iş ve yalnız bu bilgisayardaki 46 eski dal**

DURUM: AÇIK — KARAR BEKLİYOR (sahip) · SAHİP: CT (sınıflandırma), sahip (gönderim ve PR sıklığı) · BULAN: CT öz denetimi · KAYIT: 2026-09-29

Yöneticinin hızlanma belgeleri incelenirken aynı ölçüm bize uygulandı (2026-09-28):
- `chore/ct-round-2`: origin/main'in 31 commit önünde, 38 commit gerisinde; 157 dosya, +8.003 satır; 2026-09-24'ten beri uzak depoda
  değil ("tek PR, en sonda" kuralının sonucu). Birleştirme o gün temizdi.
- PR #122: 90 commit, 249 dosya, +23 bin satır. #124 ve #125: 3–4 commit (örnek alınacak boyut).
- Uzak depoda olmayan ve çalışma dalına girmemiş 46 yerel dal, 336 tekil commit; 264'ünün ana dalda patch eşdeğeri yok (kesin kanıt
  değil: içerik çakışma çözülmüş birleştirmelerle girmiş olabilir). Alanlar: ManagementGovernance, Web, Platform, Auth; bazı entegrasyon
  dallarında PpmService dosyaları var (Codex'in, dokunulmaz). Çoğu 2026-08-04…08-30.
- CI 27 test projesinin 3'ünü koşturuyor (BL-457); sabotaj koşu çıktıları geçici klasörde, commit mesajında özetleniyor (REP-001'e tam
  uymuyor).
İstenen: (1) sahip kararı — "tek PR sonda" yerine her gün uzak depoya gönderme ve modül başına küçük PR; (2) CT 46 dalı sınıflandırır:
ana dalda karşılığı olan / gerçekten bekleyen / terk edilmiş; hiçbiri silinmez, terk edilenler etiketle arşivlenir, bekleyenler için
sahibe liste; Codex/PPM hariç; (3) sabotaj koşu özetleri `docs/records/audits/<yyyy-ay>/` altına (REP-001).


**Sınıflandırma sonucu (CT, 2026-09-29, salt okunur; hiçbir dala dokunulmadı):**
- **A · içeriği ana dalda (4):** `feature/mg/mod-0354-decomposition-work-structuring-engine` (dosyaların hepsi main'de; 114 aynı, 7'si main'de
  daha yeni), `feature/pss/mod-0018-fu13-effective-permissions`, `feature/pss/mod-0288-decision-authority-governance-reconcile`,
  `integration/2026-09-13` (net fark yok). Arşivlenebilir.
- **B · başka bir yerel dalın içinde (15):** kendi dalları kararlaştırılınca onlarla birlikte gider (örn. `feature/integration/control-tower-final`
  → `feature/mg/mod-0355-process-performance-closure`, `feature/esbp/mod-0136-budgeting` → `...-fu16-http-context`).
- **C · ana dalda olmayan gerçek iş, karar gerekiyor (5 tema, 27 uç dal):**
  1. **FP&A / Kurumsal Strateji:** `Diten.FpaService` (bütçe MOD-0136, senaryo MOD-0138) ve `Diten.DecisionIntelligenceService` (karar
     kayıtları MOD-0072) main'de HİÇ yok; ES eski eşlik + MOD-0352 dilim 3. Bu dallarda yerel entegrasyondan gelen PPM dosyaları da var
     (Codex'in) — ayıklanmadan birleştirilemez.
  2. **Yönetim-yönetişim MOD-0355** süreç performansı / yazma ekranı / auth-gateway uçtan uca / yerel test çalışma zamanı: dört iç içe uç dal,
     her birinde main'de olmayan ~230–250 dosya.
  3. **PSS Gate-I güvenlik temeli (Auth):** S2S imzalı kanıt, vekil aktör, açık rol verme, izin kataloğu manifest kaydı, yetki hakkı
     işlemleri, denetim girişi — ~96 Auth dosyası main'de yok, adları başka yerde de yok. Main sonradan bazılarını farklı yoldan çözdü
     (katalog→Auth izin eşitlemesi, yetki hakkı eşitlemesi); kısmen aşılmış olabilir, CT incelemesi gerekir.
  4. **Entegrasyon altyapısı:** kanonik port kaydı (08-04), DCP-006 altın akış, MG yerel entegrasyon, port 5017.
  5. **Yalnız belge:** `feature/bpm/dcp-005-business-process-management`, `future/platformcontrol` (Mayıs).
Öneri: (i) önce yedek — sahip 46 dalı `archive/2026-08/<ad>` adıyla uzak depoya gönderir (silme yok); (ii) tema başına sahip kararı:
sürdür (PR + yeniden plan) ya da arşivde bırak; (iii) tema 3'ü CT, bugünkü main ile karşılaştırıp "aşılmış / hâlâ gerekli" listesine çevirir.
Ayrıntılı tablo: CT çalışma notu (dal · son tarih · tekil commit · main'de olmayan dosya · PPM dosyası).
---

### BL-469

**Yöneticinin hızlanma önerileri (MVP6 önerileri + bireysel performans kuralları, Rev 2) — karar kaydı**

DURUM: AÇIK — YÖNETİCİ CEVABI BEKLİYOR · SAHİP: CT (SOP taslağı), sahip + yönetici (karar) · KAYIT: 2026-09-29

CT görüşü yöneticiye gönderilmek üzere hazırlandı: https://claude.ai/artifact/V8nfvuvT7pvuyfXGKDC16W (bağımsız ikinci değerlendirmeyle
aynı sonuç). Özet: **alınacak** erken dikey akış, geliştirme öncesi tek kabul tablosu (HTTP/tarayıcı/veritabanı), "hazır" ile "karar
bekliyor" ayrımı (karar başına sahip + tarih), aynı anda 2 ürün + 1 ortam işi (+ dal 50 commit geride ya da 5 gün gönderilmemişse yeni iş
yok); **değiştirilerek** ortak yazar yalnız giriş/ağ geçidi/başlangıç/izinler için, kanıt devralma yalnız ilgili dosyalarda hiç değişiklik
yoksa ve ana dal birleştirmesi olmadıysa, önceden verilmiş düzeltme yetkisi açık dosya listesi + PR incelemesiyle; **alınmayacak**
tahmine dayalı bireysel kredi, bireysel pano, zaman aralığı defteri, oturum saatleri tablosu, ayrı terimler (VER, HELD, bounded, preimage),
git dışı kaynak manifesti. Ölçüm: ekip düzeyinde git/CI'dan dört ölçü (ana dala birleşme sıklığı, işin başlangıcından ana dala giriş süresi,
bozulan/geri alınan değişiklik oranı, ana daldan uzaklaşan dallar); "teslim" = ana dala girmiş iş. MVP6 için ilk adım: dal
(`feature/mvp6-logistics`, 1 önde / 602 geride, birleştirme temiz) ana dalla birleştirilir, depo dışındaki modül kodu gönderilir,
tedarik zinciri testleri CI'a eklenir, sevkiyat için PR. Yönetici onaylarsa: SOP'a eklenecek maddelerin taslağı + haftalık otomatik akış
raporu (`scripts/status_report.py` yanına).


### BL-470

**Ağ geçidi testi ana dalda kırmızı: Tedarik rotaları tanımsız 5065 portunda**

DURUM: AÇIK · SAHİP: Tedarik (Procurement) ekibi; CT bilgi verir · BULAN: CT (takvim motoru kabulü) · KAYIT: 2026-09-29

`OcelotConfigurationTests.EveryRoute_DownstreamPortIsInKnownServiceSet` origin/main'de kırmızı: `/api/suppliers`, `/api/sourcing`,
`/api/requisitions` (ve `{everything}` eşleri) 5065'e gidiyor; test bilinen port kümesinde 5065'i tanımıyor. Kaynak: `de174eb97` "fix(procurement):
resolve OD-5 port conflict — Procurement 5062 -> 5065" — rota değişti, testteki `KnownDownstreamPorts` ve port kaydı (CLAUDE.md port listesi
5011–5060 bandını söylüyor, 5065 bandın dışında) güncellenmedi. İş: Tedarik ekibi port kararını port kaydına işler ve testteki kümeyi
günceller; bandın dışına çıkış bilinçliyse CLAUDE.md port satırı da güncellenir. CI'da ağ geçidi testleri koşmadığı için görünmedi (BL-457).

---

### BL-471

**Takvimde uyarı işareti kalıcı değil: akış görev başına uyarı taşımıyor**

DURUM: KAPANDI (CT kabulü bekliyor) — WP-UI-MEETINGS-CALENDAR-01 (takvim 2c, P-2026-09-29-04) · SAHİP: CT (Görev Merkezi / MOD-0024) · BULAN: takvim 2b ajanı, CT kabulü · KAYIT: 2026-09-29

**Kapanış notu (2026-09-29):** `GET /api/v1/work/calendar` her görev satırına `warnings[]` taşıyor — plan yazımıyla aynı şekil ve kodlar
(`TASK_PLAN_OVERLAPS_MEETING` başlık+saatle, `TASK_PLAN_OUTSIDE_WORKING_HOURS`), okuma anında motorun kuralıyla hesaplanır
(`GetMyWorkCalendarHandler.WarningsFor`: `TaskPlanBlockRules.Fit` + kişinin kendi toplantıları, yarı açık). Saklı blok artık penceresine
sığmıyorsa (saatler değişti) da "çalışma saati dışında" — plan taşınmaz, işaretlenir (sahip kuralı 2026-09-29). Görev Merkezi `state.calendarNotes`
kaldırıldı; işaret akıştan, sayfa yenilenince durur. Testler: Platform HTTP/Mongo `BL471_*` (4), vitest "BL-471 — a block's warning mark comes
from the feed" (4); sabotaj: akış yok sayılınca kırmızı.

Plan yazımı `warnings[]` döndürüyor (toplantıyla çakışma, çalışma saati dışı); takvim bloğa işareti yalnız o oturumda koyuyor, sayfa yenilenince
kayboluyor, çünkü `GET /api/v1/work/calendar` görev başına uyarı taşımıyor. İş: akışın görev satırına okuma anında hesaplanan uyarı kodları
(motorun aynı kuralı), ekran `state.calendarNotes` yerine akıştan okur. Küçük motor + ekran işi. Gelecek regresyon riski: 🟢 ekleme.

### BL-472

**Eski toast yollarında başlık kaçışsız: Notyf `innerHTML` yazıyor**

DURUM: KAPANDI — `b649e88c3` (2026-10-01; main'de PR #130): mesaj tek kapıda (showToast → DitenToastText) metne çevriliyor; gerçek Notyf ile test, dev'de canlı kanıt · SAHİP: CT (Görev Merkezi) · BULAN: takvim 2b bağımsız gözden geçirme · KAYIT: 2026-09-29

`showToast` Notyf ile mesajı `innerHTML` olarak basıyor. Takvim 2b kendi yeni yollarını kaçışladı (CalPlanSaved, toplantı uyarısı, çakışma);
Görev Merkezi'nin eski toast'ları (ör. `ToastClaimed` ve başlık geçiren diğerleri) hâlâ iş/toplantı başlığını ham koyuyor: başlığı
`<img onerror=…>` olan bir iş, onu üstlenen kişinin tarayıcısında betik çalıştırır. İş: ya showToast metin modunu varsayılan yapmak (HTML
isteyen çağrılar açıkça ister) ya da bütün başlık geçiren çağrıları kaçışlamak; başlığı `<img>` olan iş ile koruma testi. Gelecek regresyon
riski: 🟡 merkezi değişiklik HTML bekleyen çağrıları bozabilir — önce çağıranları say.

### BL-473

**Kişinin yerel günü kiracının tek saat diliminden: TR + CH aynı kiracıda yanlış gün**

DURUM: AÇIK · SAHİP: CT (Platform çalışma saatleri) · BULAN: MOD-0280-FU01 paket ajanı · KAYIT: 2026-09-29

`WorkingHoursProvider.cs:62` saat dilimini kiracıdan alıyor (tatil ülkesi de, :75). Türkiye ve İsviçre tüzel kişilerini birlikte tutan bir
kiracıda yerel gün ve gece yarısı tek bölgeden gelir (takvim motoru ve MOD-0280-FU01). Öneri: tüzel kişi halkası (sıra 40) bir bölge de
döndürür; tüketiciler değişmez. Gelecek regresyon riski: 🟡 saklanan satırlar yakaladıkları `TimeZoneId`'yi korur, yeniden hesaplanmaz.

### BL-474

**Çalışma takvimi paketinin kimliği eski: dosya adı CAND-CAP-0008, kayıt CAND-CAP-0010**

DURUM: AÇIK · SAHİP: CT · BULAN: MOD-0280-FU01 paket ajanı · KAYIT: 2026-09-29

`execution/domains/platform-shared-services/module-packs/CAND-CAP-0008-working-calendar-public-holidays.md` (ve FU02) dosya adı ve ön
bilgisi eski kimliği taşıyor; kayıt defteri CAND-CAP-0010 diyor, CAND-CAP-0008 artık MOD-0354'ün eski takma adı. İş: dosya adı + ön bilgi +
içeriden bağlantılar tek committe; kimlik denetim betiği (`verify_module_id.py --check-all`) yeşil kalır. Gelecek regresyon riski: 🟢.

---

### BL-475

**MOD-0023 tek atanan: çok adaylı onay, üstlenme ve vekâlet yok**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: MOD-0280-FU01 T1a kabulü, bağımsız gözden geçirme · KAYIT: 2026-09-29

MOD-0023 bir adıma yalnız ilk adayı atıyor (`StartWorkflowInstanceHandler`, `normalizedCandidates[0]`); aynı koltuğun diğer sahipleri ve havuz üyeleri
karar veremiyor. Zaman çizelgesi bu yüzden MOD-0023'ün gerçekten atadığı kişiyi saklıyor; yükseltme ya da devretmeden sonra bu kişi eskiyor ve yeni
atanan onay listesinde haftayı görmüyor. İş: aday kümesi + üstlenme (claim), yokluk vekâleti, zaman çizelgesinde atanan kişinin MOD-0023'ten okunması.
SAP/Oracle: onay kuyruğu gruba gider, biri üstlenir. Gelecek regresyon riski: 🟡 atama modeli değişir, mevcut örnekler tek atananla kalır.

### BL-476

**MOD-0023 iptal yalnız izne bakıyor: kim olduğuna değil**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: MOD-0280-FU01 T1a kabulü, bağımsız gözden geçirme · KAYIT: 2026-09-29

`CancelAsync` `requireAssignment: false` ile yükleniyor (`WorkflowTaskTransitionSupport.cs` ~499): `platform.workflow.tasks.cancel` izni olan herkes kiracıdaki
herhangi bir onayı iptal edebiliyor (başlatmamış, kendisine atanmamış olsa da). Zaman çizelgesinde bu, haftayı Draft'a düşürür; görev onayında görev
sonsuza kadar kapılı kalır (Cancelled ne onay ne ret sayılıyor). İş: iptal = başlatan ya da nesnenin sahibi modül; yönetici iptali ayrı izin + sebep +
denetim. Gelecek regresyon riski: 🟡 bugün iptal eden yönetici akışları varsa daralır — önce çağıranları say.

### BL-477

**MOD-0023 kişi kimliği serbest metin: büyük harf/küme parantezli GUID görevi karar verilemez bırakır**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: MOD-0280-FU01 T1a kabulü, bağımsız gözden geçirme · KAYIT: 2026-09-29

B4'ten beri işlem yapan her zaman `Guid.ToString()` (küçük harf); `ResolvedPrincipalId` birebir metin karşılaştırmasıyla eşleniyor. Devretme hedefi ve API ile
başlatılan örneklerin aday listesi yalnız uzunlukla doğrulanıyor: büyük harf ya da `Ellipsis` biçimli bir GUID, kimsenin karar veremediği bir görev bırakır;
aynı yolla "kendine devretme" kontrolü de aşılır (`DelegateWorkflowTaskValidator.cs:17`). İş: girişte GUID ayrıştır ve kanonik biçime çevir, karşılaştırmayı
Guid ile yap. Gelecek regresyon riski: 🟢.

### BL-478

**Yükseltilmiş (Escalated) görev onayı ve incelemesi kapatılamıyor**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: MOD-0280-FU01 T1a kabulü, bağımsız gözden geçirme · KAYIT: 2026-09-29

Onayla/reddet/devret/bilgi iste Escalated görevi reddediyor, herkese açık iptal de (B3 geri alındı); görev onayı ve inceleme temizliği yalnız bekleyen
görevleri iptal ediyor (`TaskApprovalService.cs:202`, `TaskReviewService.cs:231`). Görev Merkezi Escalated'i bekleyen sayıp Onayla'yı açık gösteriyor, tıklayınca
409. Yalnız kiracı onay şablonuna SLA kuralı eklerse olur. İş: yükseltilen görevin yeni atananı karar verebilsin ya da sahip modül kapatabilsin (zaman
çizelgesindeki `AllowEscalated` gibi). Gelecek regresyon riski: 🟢.

### BL-479

**Zaman çizelgesi taraması: 200 sınırında açlık, toplam işareti için indeks yok**

DURUM: KISMEN KAPANDI 2026-10-01 — açlık kapalı: tarama onaylı-toplamsız haftaları ayrı sorguyla ve kendi payıyla okuyor (kod T1b'de girmişti; WP-TIMESHEET-HARDENING-01 testini ekledi: 205 eski bekleyen hafta, yeni onaylı haftanın toplamını aynı taramada engellemiyor). **Kalan:** `TotalsAppliedAtUtc` için indeks yok · SAHİP: CT (MOD-0280-FU01) · BULAN: T1a kabulü · KAYIT: 2026-09-29

`ListNeedingFinalizationAsync` kiracı başına en eski 200 haftayı alıyor ve karar bekleyen bütün Submitted haftalar da bu listede: 200'den fazla eski bekleyen
varsa onaylanmış ama toplamı düşmüş yeni hafta taranmıyor. `TotalsAppliedAtUtc` için indeks yok (onaylı dal her koşuda bütün onaylı haftaları okuyor); onay
listesi toplamları yeniden denemiyor (yalnız tek hafta görünümü ve kişinin kendi okuması). Tarama varsayılan kapalı. İş (T1b'ye küçük madde): iki ayrı sorgu
(önce onaylı-toplamsız), indeks. Gelecek regresyon riski: 🟢.

### BL-480

**Takvim 2c küçükleri**

DURUM: AÇIK · SAHİP: CT (Görev Merkezi / Toplantılar) · BULAN: takvim 2c bağımsız gözden geçirme · KAYIT: 2026-09-29

(1) Görünen aralıktan bir günden fazla önce başlayan çok günlü toplantı Toplantılar takviminde çizilmiyor (liste API'si StartAt ile süzüyor). (2) Düzenleyen
değişince yeni düzenleyenin katılımcı satırı yok; toplantısı "diğer" görünüyor, daha önce reddetmişse hiç görünmüyor. (3) Görev Merkezi davet kartı ortak
modüle geçince SLA çipi, sabitleme ve okunmadı/seçili görünümünü kaybetti; `inviteButtons` ve `calendarCardExtras` davet dalı ölü kod. (4) Kabul çakışma
uyarısı yalnız kart modülünü yükleyen sayfalarda; Görev Merkezi ayrıntı sayfasından kabulde uyarı yok. Gelecek regresyon riski: 🟢.

### BL-481

**Sebepli yönetici müdahalesi (başkası adına onay işlemi)**

DURUM: FİKİR (ihtiyaç doğarsa) · SAHİP: CT · KAYIT: 2026-09-29

Platform › Workflow ekranındaki serbest "Actor Id" alanı B4 ile kaldırıldı; artık kimse başkası adına onay işlemi yapamaz. Gerçekten gerekirse ayrı izin +
zorunlu sebep + denetim kaydı + etkilenen kişiye bildirim ile ayrı bir özellik olarak yapılır (SAP/Oracle'da "admin override" böyledir). Gelecek regresyon
riski: 🟢.

### BL-482

**Platform testleri paylaşılan dev Mongo'ya (27017) bağlanıyor: BusinessReferenceData Mongo testleri**

DURUM: KAPANDI — `7be738ead` (2026-10-01; kalan: sahibin elle sileceği eski kalıntı veritabanları) · SAHİP: CT · BULAN: T1a ve takvim 2c ajanları · KAYIT: 2026-09-29

Platform Application paketinin tamamı koşulunca `BusinessReferenceData*` Mongo testleri paylaşılan 27017'de kendi `diten_platform_brd_itest_*` veritabanlarını
açıp siliyor ve aralarında yarışıyor ("database is currently being dropped" → tabandaki ~49 kırmızının kaynağı). Yeni testler atılır mongod kullanıyor. İş: BRD
testlerini `DisposableStandaloneMongo`'ya taşı; kırmızı taban listesi temizlenir. Gelecek regresyon riski: 🟢.
**KISMEN KAPANDI 2026-10-01 (CT, merge sonrası):** `MongoIntegrationHarness` yarışı ve yapışkan kalıntı giderildi — başlangıç temizliği
her çağıranı bekletiyor, başarısız şema kurulumu "kuruldu" sayılmıyor, E11000 kalıntısı yalnız bu sürecin kendi damgaladığı test
veritabanında, yalnız hatanın adını verdiği kiracı anahtarlı index'te ve bu sürecin canlı kiracısı olmayan gruplarda siliniyor; kilit testi
alt süreci ortak veritabanına hiç girmiyor (08b34b980, 1c449df3d, 826ddc6c9). Ölçüm: Toplantı+Bildirim 653/653 iki kez. **Kalan:**
BRD yarısı (GSKU harness'i: kişi başı sweeper + replika-set ping `<object>`), 49 kırmızı; sabit adlı eski
`diten_platform_itest_eventing-outbox-idempotency` veritabanı tireli olduğu için süpürülmüyor (sahip elle düşürür).
**KAPANDI 2026-10-01 (WP-BRD-TESTS-01, `7be738ead`: teslim `533a44083` + CT kabulü `5d7f85d72`):** kök neden üç ayrı kusurdu — düzenek
ping cevabını `object` olarak okuyordu (replika setinde cevap BSON Timestamp taşır, okuma patlar; veritabanı oluştuktan sonra patladığı için
her test bir veritabanı bırakıyordu), her sınıf aynı kalıntıyı aynı anda düşürmeye çalışıyordu, iki test eskimiş tohum sayısı bekliyordu.
Şimdi: BRD düzeneği `MongoIntegrationHarness` üstünde; beş sınıf ortak veritabanında taze kiracıyla, dört sınıf (tüm koleksiyon üzerine iddia
edenler) sabit adlı kapsam veritabanında; koşu başına veritabanı yok. Kapsam adları KAPALI bir listede (8 ad) — CT sabotajı, listesiz hâlde
`run_{Guid}` adının bütün korumalardan geçtiğini gösterdi. Süpürücü testleri özel mongod'da. Aynı `object` okuması MDM'de de vardı
(`LskuRegisterMongoTests`), düzeltildi; kural artık depo genelinde mimari testinde (`NoTestReadsAMongoCommandReplyAsObject`).
Ölçüm (hat, birleşme sonrası): **Platform tam koşu 5755/5755** (ilk kez tam yeşil), BRD 196/196, mimari 19/19, MDM LSKU 3/3;
koşu öncesi/sonrası ortak mongod'da yeni kalıntı yok. CT sabotajı: 8/8 adı yazılı testte kırmızı.
**Sahibin elle yapacağı:** eski Guid adlı `diten_platform_brd_itest_*` kalıntıları (ölçüm: 43) ve `diten_platform_itest_eventing-outbox-idempotency`;
artık hiçbir kod bunları silmiyor (birleşmemiş dallardaki eski düzenek hâlâ üretip süpürebilir). Emekli olan üç kapsam veritabanını
(`…_brd_market_publish`, `…_brd_market_resolve`, `…_brd_verified_publish`) ortak düzeneğin bayat damga süpürmesi kendisi kaldırır.
Ürün devralma dalına not: oradaki Codex testlerinde `RunCommandAsync<object>` varsa yeni mimari kuralı birleşince adını verir.
**Ek (2026-09-30, T3 kabulü):** aynı sınıftan ikinci belirti — `MongoIntegrationHarness` ortak `diten_platform_itest`
veritabanını kullanıyor; `meeting_series`'te önceki koşulardan kalan çift kayıt ("Haftalık Kalite Toplantısı") benzersiz index kurulumunu
E11000 ile düşürüyor ve o veritabanını açan her test (Toplantı benzersiz index testleri, `NotificationDispatchPermanentFailureMongoTests`)
zincirleme kırmızı oluyor; T3'lü ve T3'süz dalda aynı. Kalıntıyı "benzersiz index çifti reddeder" testleri, index yokken çift yazarak
bırakıyor. İş aynı: bu testler de `DisposableStandaloneMongo`'ya (ya da `emptyFirst`'e) taşınır.

### BL-483

**T1a küçükleri**

DURUM: KISMEN KAPANDI 2026-10-01 — (1) ve (2) kapandı (WP-TIMESHEET-HARDENING-01 + CT kabulü, hatta `36b42bb71`); (3)–(6) açık · SAHİP: CT (MOD-0280-FU01 / MOD-0023) · BULAN: T1a kabulü · KAYIT: 2026-09-29

**(1) kapandı:** geri çekmede "çok geç" yalnız biri gerçekten karar verdiyse (onay / ret) söyleniyor; MOD-0023 iptali reddetti ama kimse karar vermediyse
`TIMESHEET_CONCURRENCY_CONFLICT` dönüyor (yeniden denenebilir); başkasının iptal ettiği onay karar sayılmıyor, geri çekme geçiyor. Bilinen: kısmi
iptalden sonra MOD-0023 örneği zaten İptal'dir; sonraki okuma haftayı Taslak'a döndürür (kişinin istediği sonuç), ama geri çekme damgası ve onaycıya
"geri çekildi" e-postası olmadan. **(2) kapandı:** karar çekici ne yaptığını üç ayrı sayıyla söylüyor (uygulanan / başarısız / denenen); tarama
`AppliedWeeks` ve `FailedWeeks` yazıyor, yutulan hata artık "değişiklik" sayılmıyor. CT kabulünde: hata satırı alanlarla (hafta, neden kodu, hata türü)
yazılıyor ve hatanın kendisi de günlüğe gidiyor (yoksa bütün sonlandırma hataları aynı satırdı).

(1) Geri çekmede MOD-0023 geçiş çakışması ya da kısmi iptal de 409 WITHDRAW_TOO_LATE ("onaylayan karar verdi") diyor, kimse karar vermemişken. (2) Karar
çekici yutulan hatayı değişiklik sayıyor (tarama sayacı şişer). (3) Görev Merkezi başlatan kontrolü yalnız `StartedByUserId`'ye bakıyor; eski örneklerde Onayla
açık görünüp 409 dönüyor. (4) İnceleyen = atanan kişiyle görev oluşturulabiliyor; o kişi incelemeye gönderemez (409) ta ki inceleyen değişene kadar.
(5) MOD-0023'te idempotency tekrar kontrolü atama kontrolünden önce: atanmamış kişi başkasının anahtarıyla 200 idempotent alıyor (değişiklik yok).
(6) Tüzel kişi sayaç anahtarı tüzel kişinin varlığını denetlemiyor (MDM başka serviste). Gelecek regresyon riski: 🟢.

### BL-484

**Zaman çizelgesi okuma yolları satır başına okuyor**

DURUM: KISMEN KAPANDI 2026-10-01 (CT, 30ce6048d + b1b47deb3): (1) onay listesi işaretleri sayfa başına bir kez okunuyor (6 satır 49 → 34 okuma), (2) toplu onay yalnız seçili haftaları `weekIds` ile yeniden okuyor — 500'ü aşan kuyrukta geçerli seçim artık reddedilmiyor, (3) hafta GET görev okuma kuralını bir kez soruyor (6 görev 16 → 11). **Dağıtım sırası: önce Platform, sonra Web** (eski Platform `weekIds`'i yok sayar, seçimler güvenli yönde NOT_FOUND döner). **2026-10-01 ikinci tur (WP-TIMESHEET-HARDENING-01 + CT kabulü, hatta `36b42bb71`):** çalışma takviminin girdileri (kiracı, koltuklar, pozisyonlar, birimler) sayfa başına BİR kez okunuyor (`GetWorkingWindowsForManyAsync`), karar çekici MOD-0023'e kuyruk için BİR kez soruyor (`ListByIdsAsync`, kiracı süzgeçli). Ajan ölçümü: 6 satırlık onay sayfası 34 → 9 okuma — **dikkat: bu sayı test düzeneğinin takvim taklidiyle ölçüldü; gerçek çalışma takvimi sağlayıcısı gün başına okumaya devam ediyor** (aynı çağrı içinde aynı gün+kapsam bir kez sorulur). CT kabulünde eklendi: toplu MOD-0023 okuması hata verirse kuyruk hafta hafta sorulur — okunamayan tek bir örnek yalnız kendi haftasını bekletir (tarama her koşuda aynı en eski haftaları aldığı için aksi halde kiracının bütün kuyruğu kalıcı dururdu). **Hâlâ kalan:** çalışma takvimi sağlayıcısının gün başına okuması (aralık okuması gerekir); hatırlatma işi takvimi ve işaretleri kişi başına okuyor; görev seçici her tuşta yeniden hesaplıyor. Önceki kalan notu: çalışma takvimi hâlâ (kişi, hafta) başına 4 okuma, karar çekici kuyruk haftası başına bir okuma, görev seçici her tuşta hesaplıyor (4). Önceki durum: AÇIK · SAHİP: CT (MOD-0280-FU01, T4 ile) · BULAN: T2a/T2b kabulleri · KAYIT: 2026-09-30

(1) Onay listesi işaretleri (otomatik kapanan gün, mesai dışı dakika, tatil) ve Görev Merkezi iş öğesi kimliğini sayfadaki her satır için ayrı okuyor
(`GetApprovalListHandler`, satır başına ~4 okuma). (2) Toplu onay listeyi `length=500` ile yeniden okuyor: bir tıklama ~2 000 okuma. Kuyruk 500 haftadan
uzunsa fazlası sessizce `TIMESHEET_APPROVAL_NOT_FOUND` alıyor. (3) Hafta GET'i okuma kuralını her görev için ayrı soruyor. (4) Görev seçici her tuş
vuruşunda yeniden hesaplıyor. İş: toplu okuma (haftalar → girişler / onay görevleri / sayaç parçaları tek sorgu), toplu onayda seçili kimliklerle okuma.
Gelecek regresyon riski: 🟢 (yalnız performans; sonuç aynı).

### BL-485

**Görev Merkezi'nde kalan sahte "süre kaydet" iletişim kutusu**

DURUM: KAPANDI 2026-10-01 — WP-WCN-DELEGATE-LOGTIME-01 (`07bc157ca`, hatta `4a9d39d71`): kutu, altı metin anahtarı (7 dil), kutuya özel stil ve gösterim verisindeki ölü alanlar kaldırıldı; süre girişi yalnız Zaman Çizelgem'de. Eski `/WorkCenter` ekranına dokunulmadı · SAHİP: CT (Görev Merkezi) · BULAN: T2b bağımsız gözden geçirme · KAYIT: 2026-09-30

T2b tarayıcı sayacını kaldırdı, ama `logTime` iletişim kutusu hâlâ duruyor: yalnız `action.input === 'minutes'` ile açılıyor (fixture denetimi yok), bellekte
etkinlik yazıp "(mock)" bildirimi gösteriyor; `LogTimeSubtext` "kaydedilmiş süreye ekler" diyor ama kart artık değişmiyor. Gösterim fixture'ında ölü
`loggedMinutes: 225` ve eski yorum var (`islerim-showcase-fixtures.js`). Bugün gerçek sağlayıcı bu eylemi göndermediği için canlıda görünmüyor. İş: kutuyu
ve anahtarlarını (7 dil) kaldır ya da Zaman Çizelgem'e yönlendiren bir bağlantıya çevir. Gelecek regresyon riski: 🟢.

### BL-486

**T2b küçükleri**

DURUM: AÇIK · SAHİP: CT (MOD-0280-FU01 / Görev Merkezi) · BULAN: T2b bağımsız gözden geçirme · KAYIT: 2026-09-30

(1) Başlat/Durdur hem kartta hem ··· menüsünde görünüyor (sağlayıcı `overflowActionCodes`'a da ekliyor). **(1) KAPANDI 2026-10-01 (WP-WCN-DELEGATE-LOGTIME-01 + CT kabulü, `4a9d39d71`):** detay sayfasında sayaç yalnız süre kartında çiziliyor; kart çizilmeyen yüzeyde eylem yerinde kalıyor. Sunucudaki yerleşim (`overflowActionCodes`) bilerek DEĞİŞMEDİ — yerleşime göre çizen ve kartı olmayan bir tüketici sayacı kaybederdi; "karta bırakma" kararı kartı çizen yüzeyin. Kilitli sayaç düğmesinin nedeni artık kartta yazıyor. Bilinen ve kabul edilen: kart "Genel" sekmesinde, "Etkinlik" sekmesinde sayaç bir sekme uzakta (sekme değişimi sayfayı yeniden çizmiyor). (2) Ayarlar sayfası ayar okuması başarısız olursa
sürüm 0 ile devam ediyor; sunucu sürüm çakışmasını reddeder ama sayfa hatayı söylemeli. **(2) KAPANDI 2026-10-01 (CT):** okunamayan ayar ya da sayaç satırı artık "okunamadı" diyor; havuz, hatırlatma ve sayaç düğmeleri kilitli, hiçbir şey gönderilmiyor (7 dil, 2 test + sabotaj). (3) `ITimeEntryTimerAvailability` `Contracts/` yerine
`Features/TimeEntry/TimeEntryPorts.cs`'de (`ITaskSpentTimeSource` `Contracts/`'ta); Tasks dağıtıcısı TimeEntry komutlarını doğrudan tanıyor. (4) `taskContext`
ile `effort` aynı koşulu iki yerde hesaplıyor (`ResolveCapabilities` ve `Effort:`); bugün tutarlı, ileride ayrışabilir — koşul tek yerden gelmeli.
Gelecek regresyon riski: 🟢.

### BL-487

**`wcn-calendar-view` "boş saate bırakma" testi tam vitest koşusunda ara sıra kırmızı**

DURUM: AÇIK · SAHİP: CT (Görev Merkezi takvimi) · BULAN: T2b kabulü · KAYIT: 2026-09-30

"a drop on an EMPTY hour lands on the slot row" testi tam koşuda (3 772 test) bir kez kırmızı, tek başına 3/3 yeşil. Yük altında zamanlamaya bağlı; bilinen
24 kırmızının dışında görünürse bu kayıt. İş: testin beklediği çizim/zamanlayıcıyı açıkça bekle. Gelecek regresyon riski: 🟢. **Ölçüm 2026-10-01 (CT):** dosya 6 paralel süreçle 3 tur (18 koşu) → 18/18 yeşil; T3 ve BL-482 tam koşularında da çıkmadı. Tekrar görülürse tam çıktı saklanıp buraya eklenir.


### BL-488

**T3 bildirimlerinin ertelenen küçükleri**

DURUM: KISMEN KAPANDI 2026-10-01 — (1) kapandı (WP-TIMESHEET-HARDENING-01 + CT kabulü, hatta `36b42bb71`): hatırlatma alıcıları 100'lük gruplarla çözülüyor (123 kişi: 123 → 2 soru); koşu bütçesi aşılmıyor, pasif kullanıcı ne işaretleniyor ne e-posta alıyor, bir kişinin başarısız gönderimi grubun kalanını etkilemiyor; dizin yanıt vermezse kimse işaretlenmiyor ve sonraki koşu hepsini hatırlatıyor. (2) ve (3) açık · SAHİP: CT (MOD-0280-FU01 / Platform bildirimleri) · BULAN: T3 bağımsız gözden geçirme · KAYIT: 2026-09-30

(1) Hatırlatma işi alıcıları kişi başına AuthService'ten çözüyor (her çağrı kiracının kullanıcı sayfalarını tarıyor); 100'lük gruplarla toplu çözüm.
(2) Yeniden denenen e-posta, `QueueEmailNotificationHandler`'ın gizleme kuralı boşluk ve `=` içeren değerleri sakladığı için (`WeekLabel`,
`TimesheetUrl`) şablonu yeniden işleyemiyor ve kısa önizlemeyi gönderiyor — platform davranışı, bu kısa e-postalarda zararsız; kural değerin
sır olup olmadığına daha iyi bakmalı. (3) E-postalar istek içinde sırayla gönderiliyor (gönderimde aday başına bir SMTP); büyük havuz + yavaş
SMTP gönderimi yavaşlatır — kuyruk (`EmailDispatchJob`) üzerinden gönderim. Gelecek regresyon riski: 🟢.


### BL-489

**Bildirim olay adları hiçbir modülde çevrilmemiş**

DURUM: AÇIK · SAHİP: CT (Platform bildirimleri) · BULAN: T3 v3 raporu (L1) · KAYIT: 2026-09-30

Görevler (MOD-0024), Toplantılar (MOD-0357) ve Zaman Çizelgesi (MOD-0280-FU01) manifestleri olaylara `DisplayNameKey: NotificationEvent_*` veriyor,
ama bu anahtarların hiçbir dalda çevirisi yok ve anahtarı okuyan bir ekran da yok (bildirim yönetim ekranı olay kodunu gösteriyor). Anahtarlar
ilk dağıtımdan önce yerinde (senkron alanı yalnız oluşturmada yazıyor) — önemli olan buydu. İş: olay adını kiracı kullanıcısına gösteren ilk ekran
(ör. bildirim tercihleri) geldiğinde üç modülün anahtarları `SharedResource` içinde 7 dilde birlikte çevrilir; Platform yönetim ekranı en,tr.
Gelecek regresyon riski: 🟢 (anahtarlar sabit).

---

### BL-490

**Görev Merkezi onay öğeleri: adım adı, aday pozisyonlar, isteğe bağlı onay notu, karar sonrası listeye dönüş (REQ-WCN-01)**

DURUM: KAPANDI (kod) — `5bb8dae82` (WP-WCN-APPROVAL-UX-01; teslim `67b2e0b5c`, CT testleri `7f53db6fa`, CT düzeltmeleri `3ee1442e8`) · CANLI DOĞRULAMA: BEKLİYOR · SAHİP: CT · İSTEYEN: CRM İddialar ekibi (2026-09-29) · KAYIT: 2026-10-01

MOD-0023 onay öğesi artık adımın görünen adını rozet olarak taşıyor (W-1), `commentRequired` değilken Onayla penceresinde isteğe bağlı not
soruyor (W-2), kişi adlandırılmamış adımda aday pozisyon adlarını gösteriyor ("Onay bekleyen: …", W-3) ve detaydan verilen karardan sonra
"bulunamadı" yerine listeye dönüyor (W-4). Alanların üçü de telde isteğe bağlı; başka sağlayıcıların çıktısı değişmedi. 7 dil.
Bağımsız gözden geçirme dört kusur buldu, hepsi düzeltildi: devredilmiş (ve yükseltildikten sonra devredilmiş) görev artık pozisyon adı
yazmıyor (kural görevin güncel atama anlık görüntüsünden okunuyor, bilinmeyen kaynak = kimse adlandırılmaz); yarım gelen listede ve tek öğe
okuması 404 dışında başarısızken listeye dönülmüyor; başarı bildirimi 20 sn'de eskiyor ve dönüş adresi başka bir detay sayfası olamıyor.
CT sabotajı: 16/16 adı yazılı testte kırmızı. Testler: WorkAggregation 229/229, tarayıcı dosyası 43/43.

Canlı doğrulama neden bekliyor: geliştirme verisinde adım adı ve pozisyon adayı taşıyan şablon yok (tek açık onay `task-review`, adsız adım).
W-1 ve W-3 canlıda CRM'in MLR şablonlarıyla (`CLAIM-CORE-MLR`) doğrulanacak; W-2 ve W-4 geliştirme ortamında.
Kapsam dışı bırakılanlar: onay öğelerinde Etkinlik akışı (CRM kendi onay geçmişinde history ucundan gösterir).
Ürün devralma dalına not: oradaki `trusted_runtime_candidates` kaynağı birleşince sınıflandırma testi kırmızı verir; "adımın kendi adayı"
kümesine eklenecek.
Gelecek regresyon riski: 🟢 (eklemeli, isteğe bağlı alanlar).

---

### BL-491

**Görev Merkezi'nden "Devret" hiç çalışmıyor — kişi seçici yok**

DURUM: KAPANDI 2026-10-01 — WP-WCN-DELEGATE-LOGTIME-01 (`07bc157ca`) + CT kabul düzeltmeleri (`092861e6e`), hatta `4a9d39d71`; canlı ekran kontrolü dev'de bekliyor · SAHİP: CT (Görev Merkezi) · BULAN: WP-WCN-APPROVAL-UX-01 ajan ölçümü + bağımsız gözden geçirme · KAYIT: 2026-10-01

**Ne yapıldı:** Devret penceresi kişiyi soruyor (zorunlu) ve isteğe bağlı not alıyor; kişi `targetPrincipalId` olarak gidiyor. Hangi eylemin kişi
sorduğunu sunucu söylüyor (`requiresTargetPerson`), tarayıcı eylem kodundan türetmiyor; devreden ve iş akışını başlatan kişi sunulmuyor
(`excludedTargetPrincipalIds`). **CT kabulünde eklenenler:** (1) MOD-0023 artık başlatana devri reddediyor (`SOD_VIOLATION`, atama kontrolünden
sonra — görev kendisinde olmayan kişi başlatanın kim olduğunu öğrenemez); (2) kullanıcı kimliği tek yazımda tutuluyor, "kendine devredemezsin"
kuralı kimliği büyük harfle / süslü parantezle yazarak aşılamıyor; (3) pencere kişileri görev **atama** listesinden değil **karar verebilecekler**
listesinden okuyor (atama listesi şirket kapsamıyla sınırlı; onay yetkisi sürece aittir — BL-057); (4) liste okunamazsa (403, bağlantı) "kimse
yok" değil gerçek hata söyleniyor; (5) kişi zarfı (`{ people, excluded }`) iki liste için de tek yerde, TasksApi'de açılıyor. Ölçüm: Platform
(Workflow + WorkAggregation + Tasks) 2074/2074, Web.Tests 395/395, vitest 3868 + bilinen 24; CT sabotajı 13/13 kırmızı. Kalanlar → BL-494.

MOD-0023 onay öğesinde Devret penceresi düz bir onay; kimi seçeceğini sormuyor, `targetPrincipalId` gönderilmiyor ve dağıtıcı isteği
`PayloadInvalid` ile reddediyor (WorkflowApprovalWorkItemActionDispatcher). Yani eylem sunuluyor ama her seferinde hata veriyor; bu iş
paketinden önce de böyleydi. İş: Devret penceresine kişi seçici (MOD-0023'ün kabul edeceği adaylar; sunucunun doğruladığı listeyle aynı
olmalı) ve isteğe bağlı not; not bayrağı (`acceptsNote`) o zaman Devret'e geri verilir. O zamana kadar bayrak bilerek kapalı.
Gelecek regresyon riski: 🟢.

---

### BL-492

**MOD-0023: "aynı kişi aynı kayıtta birden fazla adımı onaylayamaz" — şablon bazında seçenek, varsayılan kapalı**

DURUM: AÇIK (acil değil) · SAHİP: CT (iş akışı motoru) · İSTEYEN: CRM İddialar ekibi (2026-10-01) · KAYIT: 2026-10-01

Canlıda tek kullanıcı üç MLR pozisyonunu tutup Medikal, Hukuk ve Ruhsat adımlarının üçünü de onayladı. Motor yalnız "başlatan kendi kaydını
onaylayamaz" kuralını uyguluyor. CRM kuralı istiyor ama şablon bazında açılabilir ve varsayılanı KAPALI olarak: bugün organizasyonlarında bir
kişinin birden fazla MLR işlevi üstlenmesi kabul edilmiş, MLR şablonlarında (`CLAIM-*`, `KP-MLR-*`) şimdilik kapalı kalacak; işlevler ayrı
kişilere geçince açacaklar. Karşılaştırma: Veeva Vault'ta bir katılımcı sonraki adımlardan dışlanabiliyor; SAP ve Oracle'da "aynı onaylayan
ardışık adımlarda atlanır / engellenir" kuralı adım bazında ayarlanıyor. Tasarımda dikkat: kural karar anında uygulanır (aday listesinden
düşürmek yetmez; devir ve yükseltme yolları da aynı kişiye varabilir) ve reddedilen karar kendi neden koduyla döner.
Gelecek regresyon riski: 🟡 (karar yoluna dokunur; varsayılan kapalı olduğu sürece mevcut akışlar değişmez).

---

### BL-493

**Ortak bildirim (toast) mesajı HTML olarak basıyor — kullanıcının yazdığı metni alıntılayan her bildirim bir enjeksiyon kapısı**

DURUM: KAPANDI — `b649e88c3` (2026-10-01; main'de PR #130): mesaj tek kapıda (showToast → DitenToastText) metne çevriliyor; gerçek Notyf ile test, dev'de canlı kanıt · SAHİP: CT (ortak ön yüz) · BULAN: WP-WCN-APPROVAL-UX-01 bağımsız gözden geçirme, CT kodda doğruladı · KAYIT: 2026-10-01

`window.showToast` (Views/Shared/_GlobalNotification.cshtml) mesajı Notyf'e olduğu gibi veriyor; Notyf `message.innerHTML = options.message`
yazıyor. Görev Merkezi "X uygulandı: {görev başlığı}" gibi bildirimlerde başkasının yazdığı başlığı alıntılıyor; başlığa işaretleme yazan
biri, o görevde işlem yapan kişinin tarayıcısında betik çalıştırabilir. Platform genelinde ve bu iş paketinden eski. Ölçüm: çağıranların
hiçbiri mesajda bilerek HTML göndermiyor (52 doğrudan çağrı tarandı); Content-Security-Policy başlığı yok. Düzeltme kaynağında: `showToast`
mesajı Notyf'e vermeden önce metne çevirir; koruma testi + canlı sayfada kanıt.
Gelecek regresyon riski: 🟢 (HTML'e yaslanan çağıran yok).

---

### BL-494

**Devret'i kiracı kullanıcısına açmak için kalanlar (MOD-0023 + Görev Merkezi)**

DURUM: AÇIK · SAHİP: CT (iş akışı motoru + Görev Merkezi) · BULAN: WP-WCN-DELEGATE-LOGTIME-01 CT kabulü + bağımsız gözden geçirme · KAYIT: 2026-10-01

BL-491 pencereyi çalışır hale getirdi; aşağıdakiler aynı pakette, motor paketiyle (BL-475 / BL-476 / BL-478) birlikte yapılmalı:
(1) **Yetki anahtarı kiracıya verilemiyor.** `platform.workflow.tasks.delegate` (ve `request-info`) platform-yönetici kapsamında; kiracı rol ekranı
göstermiyor. Bugün Devret'i fiilen yalnız yönetici görüyor. Karar sahibin: üç onay anahtarı için açılan tek kapıya (`SeedOwnedTenantScopeKeys`)
eklenir mi. (2) **Aday listesi iş akışının kendi ucu olmalı.** Pencere bugün görev modülünün `decision-makers` ucunu okuyor (yetki: görev oluşturma);
yalnız iş akışı yetkisi olan onaycı 403 alır. Doğru şekil: devret yetkisiyle korunan, onay öğesine özel aday ucu — kendini ve başlatanı sunucuda
düşer; o zaman `excludedTargetPrincipalIds` tarayıcıya hiç gitmez (bugün başlatanın kullanıcı kimliği, talep sahibi alanı boş olan onaylarda da
tarayıcıya gidiyor — aynı kiracı, yalnız karar verebilecek kişi görür, düşük hassasiyet). (3) **Motor hedefi doğrulamıyor.** Var olmayan ya da
başka kiracıdan bir kimliğe devredilen onay sahipsiz kalır (yalnız iptal ya da yükseltme kurtarır); hedef "bu kiracının yaşayan bir kişisi"
olmalı, tercihen "bu adımı karara bağlayabilecek biri". (4) **Ret mesajları genel.** `WORKFLOW_DELEGATE_SAME_ACTOR_INVALID`,
`WORKFLOW_ACTOR_DENIED`, `SOD_VIOLATION` Görev Merkezi'nde "hata oluştu" olarak görünüyor (7 dil gerekir); çözümlenmiş kişi olmayan aday
Devret'i açık görüp genel hata alıyor (BL-475 ile aynı kök). (5) Pencerede "ne olacak" cümlesi yok. Karşılaştırma: SAP iş akışında iletme
adımın "olası işleyenleri" ile sınırlanır, başlatan "hariç tutulan işleyen" olur; Oracle onay kurallarında yeniden atama dizinden herkese
açıktır ama kısıtlanabilir ve talep sahibinin kendi kaydını onaylaması ayrı kuralla engellenir. Gelecek regresyon riski: 🟡 (karar yoluna ve
yetki kapsamına dokunur).

---

### BL-495

**Bağlamsal yardım: "bu sayfa nasıl kullanılır" + klavye kısayolları her sayfada tek girişten**

DURUM: KARAR VERİLDİ (sahip, 2026-10-02: "ok, yapalım; .antigravity kuralına da eklensin ki yeni modüllerde de olsun") — prompt denetim standardı (borç defteri yöntemi) hatta girince yazılacak · **2026-10-09:** sahip canlıdan aynı talebi yineledi ("kullanıcılar sayfaları kullanmayı bilmiyor; başlıkta öğretici bir modül") → CT sıraya aldı, boşalan ilk sohbete; ilk paket ortak "?" paneli + Kullanıcılar + Roller; adım adım tur sonraki aşama; örnek veri ve rol önerisi BL-580 · SAHİP: CT (ortak ön yüz) · BULAN: sahip (canlı kullanıcılar "sayfayı nasıl kullanacağız" bilgisi istiyor) · KAYIT: 2026-10-02

**Karar:** (1) üst çubukta tek "?" düğmesi, her sayfada aynı yerde; (2) içerik: bu sayfa ne işe yarar (2 cümle) + nasıl kullanılır (3–5 adım) + varsa sayfanın klavye kısayolları; (3) metni sayfayı yapan ekip yazar — modül paketinin zorunlu bölümü, kiracı ekranında 7 dil; yeni sayfa yardım metni olmadan teslim edilemez (kural dosyası + ajan kapıları + borç defteri: eski sayfalar listeye yazılır, liste yalnız küçülür). İlk paket: ortak panel + Kullanıcılar ve Roller metinleri. İlk kullanım turu sonraya.

Ölçüm: ortak kısayol katmanı var (`assets/js/shared/diten-shortcuts.js`, BL-438) ama `_DitenShortcuts` yalnız Görev Merkezi (Index, Details)
ve Görevler (Create, Edit, Details) sayfalarına yükleniyor; düğme Görev Merkezi kutusunun içinde. Başka hiçbir sayfada "ne işe yarar,
nasıl kullanılır" bilgisi yok. Öneri: üst çubukta tek "?" girişi — (a) bu sayfa hakkında: ne işe yarar + 3–5 adım, metni modül verir, kiracı
ekranında 7 dil; (b) klavye kısayolları: kısayolu olmayan sayfada satır çıkmaz; (c) ilk kullanım turu: sonraya. Karşılaştırma tasarım
konuşmasında ölçülerek yapılacak (SAP Web Assistant / Enable Now, Oracle Guided Learning — bu kayıttaki adlar hafızadan, doğrulanmadı).
Modül paketi şablonuna "sayfa yardım metni" satırı eklenmesi aynı işin parçası. Gelecek regresyon riski: 🟢 (ek).

---

### BL-496

**Giriş ekranı retleri İngilizce ve ortak diyaloğun dışında — "Account is disabled."**

DURUM: AÇIK — sahip: düzeltilecek (2026-10-02); prompt Roller kapanışı hatta girince verilecek (ortak `diten-refusal.js` oradan geliyor) · SAHİP: CT (Auth + ortak ön yüz) · BULAN: sahip (Kullanıcılar turu) · KAYIT: 2026-10-02

Ölçüm: `LoginCommandHandler.cs:102` "Account is disabled." sabit İngilizce; aynı dosyada 5 ham ret daha (72, 78, 82, 93, 129). `login.js:77`
`problem.detail`'i olduğu gibi basıyor. Mesaj parola doğrulandıktan SONRA dönüyor (hesap varlığını sızdırmıyor) — bu sıra korunmalı.
`login.js`, `forgot-password.js`, `reset-password.js` ürünün ortak diyaloğunu değil kendi `Swal.fire` görünümünü çiziyor; vendor dışında toplam
20 dosyada ham `Swal.fire` var, hangilerinin ortak görünümü giydiği ayrılmadı. İş: (1) giriş / parola akışı retlerine kalıcı kod + 7 dil
(WP-ROLES-CLOSE-01'de yazılan ortak köprü), hangi durumda hangi cümlenin gösterileceği hesap varlığını sızdırmayacak şekilde; (2) üç sayfanın
ortak diyaloğa geçmesi; (3) 20 dosyalık `Swal.fire` envanteri (hangisi ortak, hangisi değil) — düzeltmesi ayrı karar.
İlgili: BL-328 (kiracı girişinde "Parolamı unuttum" yok — sahip 2026-10-02: sonraya, başka geliştirici yapabilir). Gelecek regresyon riski: 🟡 (giriş yolu).

---

### BL-497

**Kiracının kendi alan adından e-posta gönderimi**

DURUM: AÇIK (büyük iş, sonraya — sahip 2026-10-02) · SAHİP: SAHİPSİZ (Platform bildirimleri) · BULAN: sahip · KAYIT: 2026-10-02

Bugün bütün e-postalar servis ayarındaki tek hesaptan gidiyor (`Smtp:FromEmail` — Auth ve Platform `appsettings.json`); kiracıya göre değişen
bir şey yok. Küçük adım (gönderen adı + yanıt adresi kiracıya göre) BL-454'ün e-posta kabuğu paketinde. Bu kayıt büyük adım: kiracı başına
gönderim kimliği (SPF / DKIM doğrulaması, kiracı başına SMTP ya da sağlayıcı), doğrulanmamış alan adından gönderimin reddi, geri dönen
iletilerin (bounce) izlenmesi. Gelecek regresyon riski: 🟡 (bildirim teslimi).

---

### BL-498

**MDM ürün onayı: "elle düzeltilmeli" (karantina) durumundan çıkış yolu yok ve durum sessiz**

DURUM: AÇIK · SAHİP: CT (MDM ürün modülleri, devralma) · BULAN: WP-GP-1B-A-RESILIENCE-01 Aşama 1 ölçümü · KAYIT: 2026-10-02

Ölçüm (takeover `4012838c1`, kod okuması): karantinaya düşen operasyonu kodla geri alan hiçbir yol yok — `AbandonedBeforeWorkflowStart` /
`Superseded` değerlerini yazan kod yok; benzersiz (kiracı, ürün, sürüm) dizini yüzünden taslak o sürümde kilitli kalıyor. İşlemcide logger yok,
runner istisnayı yutuyor (`catch { failedCount++; }`), işçi döngü sonucunu atıyor: karantina hiçbir günlükte görünmüyor.
WP-GP-1B-A-RESILIENCE-01 servis hatalarının karantinaya DÜŞMESİNİ engelliyor ve günlüğü ekliyor; bu kayıt kalan iş: (1) operatör için
"yeniden dene / vazgeç" eylemi (yetkili, gerekçeli, denetimli), (2) kiracı kullanıcısına durumun ve nedeninin gösterilmesi (GP-2 kayıt geçmişi),
(3) taslağın yeni sürümle serbest bırakılması. Karşılaştırma: SAP'de hatalı entegrasyon iletileri izleme ekranından toplu yeniden başlatılır ya da
iptal edilir; iş belgesi kilitli kalmaz. Gelecek regresyon riski: 🟡 (durum makinesi).

---

### BL-499

**Bildirim e-postaları: alıcının kendi dili yok, sağlayıcı geri-düşme politikasını yok sayıyor**

DURUM: AÇIK · SAHİP: CT (Platform bildirimleri) · BULAN: WP-EMAIL-SHELL-01 Aşama 1 ölçümü · KAYIT: 2026-10-02

(1) **Alıcı dili.** Bildirim dili bugün kiracının dilinden geliyor (çağıranın verdiği dil → `Tenant.Settings.Language` → `Tenant.DefaultLanguage` → "en");
kullanıcıya özel dil hiçbir yerde tutulmuyor (Web'in arayüz dili yalnız çerezde, Auth `User`'da alan yok). WP-EMAIL-SHELL-01 kiracı diliyle
ilerliyor (sahip + CT kararı 2026-10-02). Kalan iş: `User.PreferredLanguage` (boş = kiracı dili), profil ekranı, `internal/users/contacts`
sözleşmesi, bildirim dil zincirinde yeni halka. Karşılaştırma: SAP ve Oracle'da bildirim dili kullanıcı kaydındaki dil alanından gelir,
yoksa sistem varsayılanına düşer. (2) **Geri-düşme politikası.** `SmtpMessagingProvider.ResolveSettingsAsync` kiracının `FallbackPolicy` ayarını
yok sayıyor: kapalı kiracı satırı hep platform varsayılanına düşüyor (çözümleyici politikaya uyuyor, sağlayıcı uymuyor).
(3) Yeniden denemede gövde 2000 karakterlik önizlemeden gidebiliyor (değişkenler maskelenmişse ya da şablon sürümü değişmişse) — BL-488 (2) ile aynı kök.
Gelecek regresyon riski: 🟢.

---

### BL-500

**Platform → Kiracı → Modüller ekranı: askıya alma çalışmıyor, süresi dolan modül uzatılamıyor, yapılamayacak işlem sunuluyor**

DURUM: AÇIK · SAHİP: CT (Platform, kiracı ticari yönetimi) · BULAN: sahip (canlı dev kontrolü, kiracı "ali") + CT ölçümü · KAYIT: 2026-10-02

Sahip üç şey denedi, üçü de kusurlu çıktı (dev, lane `c79c5a49b`):
(1) **Askıya alma (kırmızı ⊘) reddediliyor:** Active bir Add-on satırında (GOLDENSLIM) "Entitlement was modified by another process." (409).
Kayıtta `Version=1`, `RowVersion` 16 bayt ve değişmemiş; ekran `row.rowVersion || null` gönderiyor (`Platform/Tenants/details.js:1558-1614`),
işleyici `UpdateAsync(session, entitlement, request.Request.RowVersion, …)` ile karşılaştırıyor
(`DisableTenantModuleEntitlementCommandHandler.cs:88`). Kök neden ölçülmedi: liste yanıtının `rowVersion` taşıyıp taşımadığı ve bayt dizisinin
gidiş-dönüşte aynı kalıp kalmadığı ilk bakılacak yer. Sonuç: ekrandan modül askıya alınamıyor.
(2) **Süresi dolmuş modül uzatılamıyor:** "Expired" satırda sunulan yeşil ✓ yalnız `IsEnabled`'ı açıyor; kayıt zaten açık, bitiş tarihi geçmiş →
hiçbir şey değişmiyor ama "kaydedildi" bildirimi çıkıyor. Süre uzatma eylemi (`UpdateTenantModuleEntitlementExpiryCommand` var) bu satırda sunulmuyor.
(3) **Temel modülde "kaldır" sunuluyor:** Görev Merkezi (baseline) satırında kaldırma var; sunucu "Baseline modules are entitlement-free and cannot
be removed." ile reddediyor. Kural doğru, ekran yanlış: eylem çizilmemeli. Üç mesaj da İngilizce (Platform ekranı: en, tr).
**Yan etki:** WP-AUDIT-STANDARD-01'in K1 sorusu (31 Ağustos'tan beri yetkilendirme değişiklikleri denetim günlüğüne düşüyor mu) bu yüzden dev'de
ölçülemedi — ekrandan başarılı bir yetkilendirme değişikliği yapılamıyor; `audit_outbox`'ta `physical-entitlement:` önekli satır sayısı hâlâ 0.
Gelecek regresyon riski: 🟡 (kiracı yetkilendirmesi).

---

### BL-501

**Ortak denetim ileticisi: izi olmayan on serviste denetim kaydı yazmanın tek yolu (K4 = A)**

DURUM: AÇIK — KARAR VERİLDİ (CT, 2026-10-02) · SAHİP: CT (altyapı) · BULAN: WP-AUDIT-STANDARD-01 bağımsız gözden geçirmesi · KAYIT: 2026-10-02

Denetim kaydı standardı (AUD-001) on serviste (CRM, HumanCapital, HCM, ESBP, PPM, Procurement, Talent, DevEnablement, MG, PVG) kabul edilmiş
hiçbir iz bulamıyor: bu servislerde yeni bir yazma komutu ya kırmızıdır ya da borç listesine yazılır. Kural "önce iletici kurulur" diyor ama
ileticinin ne olduğunu söylemiyordu. **Karar:** işaret arayüzü + boru hattı davranışı + Platform'a ileten istemci `Diten.BuildingBlocks` içinde
tek kopya durur (MDM'deki `AuditForwardingBehavior` kalıbının ortaklaştırılmışı); servis başına kopya yasak. Kapsam: ortak paket, MDM ve
Auth'un kendi kopyalarının ona taşınması, anahtar boşken sessiz atlamanın açılışta yüksek sesle söylenmesi (bugün MDM'de `LogDebug`), mimari
testinin ortak belirteci tanıması, bir pilot serviste (CRM ekibiyle birlikte) ilk kullanım. Karşılaştırma: SAP'de değişiklik belgeleri
(change documents) ve Oracle'da denetim politikaları uygulama geliştiricisinin yazdığı kod değil, platformun verdiği tek mekanizmadır.
O zamana kadar bu servislerde yeni yazma komutu = "engelli, CT'ye yaz". Gelecek regresyon riski: 🟡 (her servisin istek hattına bir davranış eklenir).

---

### BL-502

**Onay motorunun ertelenen üç parçası: üstlenme, güvenilir iptalde yükseltilmiş görev, zaman çizelgesi onay sayfası**

DURUM: AÇIK · SAHİP: CT (MOD-0023 iş akışı motoru) · BULAN: WP-WF-ENGINE-CANDIDATES-01 Aşama 1 (ölçüm) · KAYIT: 2026-10-02

Motor paketi "bir pozisyonda iki kişi varsa ikisi de karar verebilir, ilk karar kazanır" kuralını getiriyor (SAP ve Oracle varsayılanı).
Bilerek dışarıda bırakılanlar: (1) **Üstlenme (claim / release):** "bu iş bende" demek için isteğe bağlı kolaylık; yeni eylem, yeni ekran metni
(7 dil) ve bırakılmayan üstlenme için zaman aşımı ister. SAP'deki "rezerve et" karşılığı; kapı değil, kolaylık. (2) **Güvenilir iptalde
yükseltilmiş (Escalated) görev:** MDM istemcisi ön kontrolde yalnız Active + Waiting durumlarını kabul ediyor; yükseltilmiş bir onay MDM
tarafından geri çekilemiyor. İstemciyle birlikte değişmeli. (3) **Zaman çizelgesi onay sayfası adaylara göre (D5):** hafta, motorun ilk
adayını `AssignedApproverUserId` olarak saklıyor ve sayfa buna göre süzüyor; motor paketinden sonra ikinci koltuk sahibi haftayı Görev
Merkezi'nden onaylayabilir ama `/TimeEntry/Approvals` sayfasında göremez. Bozulma değil, tutarsızlık. Gelecek regresyon riski: 🟢 (üçü de eklemeli).

**Ek (2026-10-02, D1 gözden geçirmesi):** madde (3) D1 düzeltme turuna ÇEKİLDİ (ikinci koltuk sahibi haftayı açamadan onaylayabiliyordu).
Listeye eklenenler: (4) **Yürütücü beklemesi:** işlem yürütücüsü üç commit denemesinden sonra beklemeden vazgeçiyor; geri çekilme süresi ayrı
iş. (5) **Yükseltilmiş durumun devirle silinmesi:** devir ve bilgi isteme görevi yeniden bekleme durumuna alıyor; ondan sonra iptal yeniden
kabul ediliyor. Görevde "yükseltildi" olgusu kalıcı tutulmuyor; ölçüm ve öneri D1 düzeltme turunun raporunda.

**Ek 2 (2026-10-03, D1 düzeltme turu gözden geçirmesi):** (6) **Onay listesi bütün gönderilmiş haftaları okuyor:** zaman çizelgesi onay
listesi kiracının gönderilmiş bütün haftalarını okuyup aday kontrolünü bellekte yapıyor. Şimdilik kalır; çözüm yönü motor tarafında "adayı X
olan açık görev" sorgusu + çok anahtarlı dizin (kopya liste yok). Kullanılmayan `ix_time_entry_weeks_tenant_status_approver` dizini o işe kadar
silinmez. (7) **Süresi geçen görev taraması tıkanabilir (doğrulanmalı):** `ListOverdueTasksAsync` (`WorkflowRepositories.cs:521-541`)
Escalated ve TimedOut görevleri de alıyor, en eski `DueAt`'ten sıralayıp `Limit(maxItems)` uyguluyor ve hiçbir şey `DueAt`'i ileri almıyor;
100'den fazla böyle görev birikirse yeni süresi geçenlere hiç sıra gelmeyebilir. (8) **Commit aşamasında etiketsiz Mongo istisnası 500:**
`UnknownTransactionCommitResult` etiketi olmayan bir commit hatası yürütücüden sarılmadan çıkıyor (`PlatformTransactionExecutor.cs:79-91`) →
500. (9) **Bildirim alıcıları eski:** zaman çizelgesi bildirimleri `week.ApproverCandidateUserIds`'e gidiyor (`TimeEntryNotifier.cs:107,121`);
devir ya da yükseltmeden sonra eski kişiler (erişim için kullanılmıyor). (10) Yükseltilmiş haftanın satırında karar verilecek görev kimliği yok
(`ApprovalWeekFacts` `ActiveStatuses` kullanıyor, Escalated dışarıda) — D5 ile.

---

### BL-503

**Ürün devralma dalında 23 yazma komutu denetim kaydı yazmıyor — Global Product canlıya çıkmadan kapanır**

DURUM: AÇIK — CANLI ÖNCESİ ENGELLEYİCİ (Global Product) · SAHİP: CT (MDM ürün modülleri devralması) · BULAN: denetim kaydı standardının testi
(`AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`), hat → devralma birleştirmesi `96cf7d888` · KAYIT: 2026-10-02

Denetim standardı (AUD-001) devralma dalıyla ilk kez karşılaştı ve dalın standarttan önce yazdığı 23 komutu adıyla saydı:
**MDM 20** (Global Product / GSKU / LSKU kimlik akışı: taslak güncelleme, onaya gönderme, geri çekme, emekliye ayırma, düzeltme ve emeklilik
akışlarını başlatma, karar mutabakatı; ürün–tüzel kişi kapsam politikası oluştur / değiştir / sonlandır) · **Platform 2**
(`StartTrustedWorkflowInstanceCommand`, `CancelTrustedWorkflowInstanceCommand`) · **Auth 1** (`IssueServiceIdentityTokenCommand`).
**Karar (CT):** borç listesine PARK EDİLMEDİ — defter yeni borç kabul etmiyor ve ürün ana verisi GxP sınıfındadır. Test devralma dalında bilerek
kırmızı duruyor (28'de 27). Kapanış: MDM komutları MDM'in mevcut iletim yoluna (yol b) bağlanır; Platform'un iki güvenilir komutu ve Auth'un
belirteç komutu için bugün yazılan iz ölçülür (iş akışı geçiş günlüğü, `authAuditLogs`) ve kabul edilmiş ize bağlanır ya da eksikse yazılır.
Karşılaştırma: SAP'de malzeme ana verisinin her değişikliği değişiklik belgesi üretir; Veeva'da ürün kaydının her durum geçişi denetim izindedir.
Gelecek regresyon riski: 🟡 (20 komutun istek hattına iletim davranışı girer).

---

### BL-504

**Denetim kaydı standardının (AUD-001) testinin hâlâ göremedikleri — iki gözden geçirmeden kalanlar**

DURUM: AÇIK · SAHİP: CT (altyapı) · BULAN: WP-AUDIT-STANDARD-01 düzeltme turunun ikinci bağımsız gözden geçirmesi · KAYIT: 2026-10-02

Düzeltme turu ve CT düzeltmeleri hatta (`3f42003a3`): sayılar ve kabul edilmiş izler sabit, zayıf yazıcı biçimi daraltıldı, CI adımı var.
Kural bunları "yakalamayan" sütununda dürüstçe sayıyor; kapatılması gerekenler:
(1) **Dar sorgu algılayıcısı:** adı `*Query` olan bir istek, `…Repository / …Store / …Collection` dışındaki bir yoldan yazarsa (servis
üzerinden, `SaveChangesAsync`, `ArchiveAsync` gibi listede olmayan fiiller) görünmüyor. (2) **Ad değiş tokuşu:** borç listesinden bir adı silip
yerine yeni denetimsiz bir komutun adını yazmak sayıyı korur; yalnız PR'da CI adımı yakalar, `push`'ta adım atlanır. (3) **Miras alan handler:**
`IRequestHandler`'ı taban sınıftan alan handler çözümleme testine görünmüyor; MG'nin jenerik muafiyeti ada göre. (4) **Yazan üç sorgu** (kiracı
yönetici kullanıcılarını okurken ilk yöneticiyi oluşturan iki sorgu, giriş ayarlarını okurken varsayılanı yazan sorgu): yazma komuta taşınmalı.
(5) **Platform'da K2 sınırı:** 192 komut özellik klasörüne göre seçildi (platform yöneticileri, kiracılar, belge yönetimi); pozisyon atamaları
dışarıda — onaycıyı belirlediği için içeri alınması tartışılmalı. (6) **Paket talimatı üç yerde üç türlü:** izi olmayan serviste paket "engelli
yazılır" / "hazır sayılmaz" / "komut pakete hiç yazılmaz"; tek cümleye inmeli. (7) `capability-pack-standard.md` §7.1 hâlâ "üye modül altyapıyı
kurar" diyor (K4 kararına aykırı); `debugger` / `debug` akışlarında kapı yok. Gelecek regresyon riski: 🟢 (yalnız test ve kural metni).

---

### BL-505

**GÜVENLİK — ortak onay penceresi kayıt adını HTML olarak basıyor (kalıcı XSS), 152 çağrı**

DURUM: KISMEN KAPANDI 2026-10-02 — ortak onay penceresi kapandı (`ca81e75e1` teslim + `335ed06d5` CT, hat `20113725c`; dev'de canlı ölçüldü);
ortak pencerenin DIŞINDAKİ ham pencereler AÇIK · SAHİP: CT (ortak ön yüz) · BULAN: Kullanıcılar paketinin bağımsız gözden geçirmesi, CT kodda
doğruladı · KAYIT: 2026-10-02

`Views/Shared/_GlobalConfirmation.cshtml:243` çağıranın verdiği `entityName` değerini, aynı blok `subtext` değerini kaçışsız olarak SweetAlert
`html` içine yazıyor. `wwwroot/assets/js` altında 127 dosyada 152 `entityName:` çağrısı var; hiçbiri kendi satırında kaçış yapmıyor. Adında HTML
olan bir kayıt (kullanıcı e-postası, rol adı, görev başlığı) oluşturan kişi, o kaydın silme / devre dışı bırakma penceresini açan başka bir
kullanıcının oturumunda betik çalıştırır; görev başlığını her kiracı kullanıcısı yazabildiği için yetki yükseltme yoludur. Bildirim (toast)
kapısı aynı şeyi doğru yapıyor (`_GlobalNotification.cshtml`, kapıda kaçış). Çözüm: pencere çağıranın verdiği her değeri METİN olarak basar,
kaçış kapıda tek yardımcıda; önceden kaçış yapan çağıranlardan kaçış kalkar; biçim isteyen çağıran varsa adı açık ayrı giriş. Karşılaştırma:
SAP UI5 ve Oracle JET'te ileti kutuları varsayılan olarak metin basar, HTML ayrı ve açık bir seçenektir. Gelecek regresyon riski: 🟡 (ürünün her
onay penceresi).

**Kapanan (2026-10-02):** pencere çağıranın verdiği her değeri metin olarak basıyor (başlık, cümle, kayıt adı, düğmeler, seçenek etiketleri,
doğrulama iletisi); biçimli gövde yalnız adı açık `subtextHtml` girişinden (üç ekran + Görev Merkezi'nin iki penceresi, adlarıyla sabit). Bağımsız
gözden geçirme teslimde bir gerileme buldu ve CT kapattı: Görev Merkezi ve davet kartı pencereye takma adla ulaşıyor, sayımda görünmemişlerdi ve
metinlerini önceden kaçırıyorlardı — düzeltme haliyle girseydi her Görev Merkezi onayında ham `<div …>` görünecekti. Ek: çizilemeyen pencere artık
işlemi ONAYLAMIYOR (eskiden `callback()` çağrılıyordu). Dev'de canlı ölçüm: Kullanıcılar'da kayıt adı metin, simge korumalı; Görev Merkezi'nde
tarih penceresi ve kurulu gövde doğru; CRM Kişiler dışa aktarma penceresi dev'de açılamadı (yalnız testle ölçüldü).
**Açık kalan (aynı sınıf, ortak pencerenin dışında — 34 doğrudan `Swal.fire` çağrısından 8'i veriyi kaçışsız basıyor):** giriş sayfası
(`Account/login.js:211`, sunucunun `detail` metni `html` içine) · parola sıfırlama (`reset-password.js:76`) · Görev Merkezi `app.js:10465`
(sunucudan gelen eylem etiketi düğme metnine) · `demand-ideas` iki bildirim · `PPM/Initiatives/index.js:106` · `diten-unauthorized.js:37` ·
`shared/premium-modal.js` (başlık ve düğme metni kaçışsız; 45 çağrı bugün yalnız sabit metin veriyor). Giriş sayfaları BL-496 paketiyle birlikte
ele alınır; PPM dosyası o ekibe yazılır. Ayrıca iki kaçış yardımcısı (bildirim ve onay) iki dosyada duruyor; bir test farkı yakalıyor, tek
dosyaya taşımak iki layout'a ortak betik eklemeyi gerektirir.

---

### BL-506

**Onay izni kiracı rollerine verilemiyor — Admin olmayan yönetici onaylayamıyor (düzeltme devralma dalında bekliyor)**

DURUM: AÇIK — düzeltme yazıldı ve kabul edildi (`b314cb8b8` + `9d108368c`, dal `feature/mdm/product-five-takeover`), hatta TAŞINMADI · SAHİP: CT
(Auth) · BULAN: CT ölçümü, sahibin "Rol İzinleri'nde ne var" sorusu üzerine (dev `diten_auth_v3`) · KAYIT: 2026-10-02

`platform.workflow.tasks.approve / reject` (ve iş akışının diğer 11 anahtarı) hat, ana dal ve canlıda platform kapsamında: Rol İzinleri ekranı
göstermiyor, yalnız `Admin` ve `SuperAdmin` rollerinde var. Sonuç: "Ekip Lideri" gibi bir kiracı rolündeki yönetici ne zaman çizelgesi haftasını
ne Görev Merkezi'ndeki bir onay görevini onaylayabilir. Sahibin 1 Ekim kararı (iş akışı yürütme anahtarları tek belgeli listeden kiracı
kapsamına alınır: `SeedOwnedTenantScopeKeys`) yalnız devralma dalında uygulandı. Yapılacak: iki commit'in Auth kısmı Roller paketi birleştikten
sonra hatta taşınır (aynı tohum dosyasına dokunuyorlar), Auth testleri, dev'de izinlerin ekranda göründüğünün ölçümü. Zaman Çizelgesi canlı
pilotunda onaylayan kişi o zamana kadar Admin rolünde olmalı. İlgili: BL-494 (Devret için aynı kapı). Gelecek regresyon riski: 🟡 (izin kapsamı).

---

### BL-507

**Rol İzinleri ekranında "mod0251" grubu: İK Çalışan Ana Kaydı izinleri ad yerine modül numarasıyla görünüyor (Rİ1)**

DURUM: AÇIK · SAHİP: etiket CT (Rol İzinleri ekranı), kalıcı ad İK ekibi (MOD-0251 tohumu) · BULAN: sahip (Rol İzinleri, dev) · KAYIT: 2026-10-02

`DataSeeder` MOD-0251 (Core HR / Employee Master) için 13 izni `Module = "mod0251"` ile ekiyor; aynı alanın diğer izinleri `human-capital` (73) ve
`hcm-employee-master` (1) gruplarında. Ekran grup başlığını modül kodundan türettiği için okuyana anlamsız bir "mod0251" başlığı çıkıyor.
Yapılacak: ekranın grup adı köprüsüne okunur ad (7 dil), ve tohumda üç grubun tek modül adında birleşmesi (anahtarlar değişmez: ADR-001).
Gelecek regresyon riski: 🟢 (yalnız gruplama etiketi).

---

### BL-508

**Rol İzinleri ekranında İngilizce kalan satır ve düğme adları — "Görüntüle" ile "Read All" yan yana, sahip yanlışını işaretledi (Rİ2)**

DURUM: AÇIK · SAHİP: CT (Rol İzinleri ekranı; kiracı ekranı = 7 dil) · BULAN: CT, sahibin oturumunda ekran okuması (dev, tr) + sahibin "senaryoda
read diyorsun, ekranda view var" geri bildirimi · KAYIT: 2026-10-02

Ölçüldü (`/RoleAssignments`, dil tr): **satır adları** — Zaman Çizelgesi grubunda `Approvals`, `Categories`, `Settings`, `Timesheets`, `Weeks`;
Görevler grubunda `Checklist Templates`, `Templates`; CRM'de `Content Scope`, `Content Set`, `Eligibility`, `Visit Frequency Policy` ve başkaları.
**Düğme adları** — `Read All`, `Reopen`, `Minutes Write`, `Minutes Publish`, `Series Manage`, `Types Manage`, `Lookup`, `Resolve`, `Confirm`,
`Write Value`. Ekranın etiket köprüsü tanımadığı kodu İngilizce "insanlaştırıyor"; köprü yalnız tekrar eden fiilleri çeviriyor.
**Sonuç (ölçüldü):** sahip Çalışan rolüne "Görüntüle" yerine yanındaki `Read All`'u (kiracının tüm görevlerini görme) verdi. Yapılacak:
(1) kapsam taşıyan fiiller (`read-all`) `read-tenant-wide` gibi ayrı "Tüm kiracı" çipiyle gösterilsin, düz bir fiil gibi durmasın;
(2) eksik fiil ve satır adları 7 dilde köprüye; (3) köprünün çevirmediği kodu sayan bir test (küçülen defter), yeni modül kendi adlarını getirsin.
Karşılaştırma: SAP'de yetki nesnesi alanları ve Oracle'da ayrıcalık adları kullanıcının dilinde listelenir; geniş kapsamlı yetki ayrı işaretlenir.
Gelecek regresyon riski: 🟢 (yalnız etiket), ama yetki yanlış verilmesine yol açtığı için öncelik orta.

---

### BL-509

**Şirketler arası iş: aynı kiracıda, farklı tüzel kişilerdeki iki meslektaş birbirine görev atayamıyor — "görev talebi" ve "çalışma grubu"**

DURUM: AÇIK — SAHİP KARARI BEKLİYOR (Görev Merkezi turunda, senaryo adım 6) · SAHİP: CT (Görev Merkezi / MOD-0024) · BULAN: sahip (kullanıcı
ihtiyacı: aynı pozisyonda, farklı şirketlerde iki kişi) · KAYIT: 2026-10-02

Ölçüldü (`TaskAssignmentScopeResolver`, BL-057): bir kişi (1) kendi tüzel kişisindeki herkese, (2) pozisyon zincirinde altındaki herkese,
(3) kendisine ayrıca verilmiş birim / pozisyon kapsamına görev atayabilir. Farklı şirketteki eşit düzey meslektaş üçüne de girmez; ret kodu
`TASK_ASSIGNEE_NOT_ASSIGNABLE`. Sınır bilerek kondu: önceden Miguel Garriga'daki kullanıcı Polonya ve Türkiye şirketlerinin bütün çalışanlarını
görüp iş atayabiliyordu (GDPR sınırı). (3) için kapsam verecek ekran YOK; kapsamlar yalnız organizasyon verisinden türüyor.
**Öneri (iki ayrı özellik, ikisi de "ata"dan ayrı):**
(a) **Görev talebi** — kapsam dışındaki birine "şunu yapar mısın": karşı taraf kabul ederse kendi görevi olur, reddederse gerekçe yazar; talep
eden emir veremez. Motorun "Kabul et / Reddet" adımı var; eksik olan kapsam dışındaki kişiyi adresleyebilmek. Hukuki nokta: talep gönderebilmek
için başka şirketteki kişinin en az adının, şirketinin ve pozisyonunun görünmesi gerekir → yönetim kararları belgesine eklenir.
(b) **Çalışma grubu** — farklı şirketlerden kişiler bir gruba alınır, grup içinde atama serbesttir (kuralın 3. bacağına ekran).
Karşılaştırma: SAP'de (yapısal yetki) ve Oracle'da (güvenlik profili) doğrudan iş verme organizasyon yapısına bağlıdır; şirketler arası iş
talep / bildirim olarak gider ve karşı taraf ya da yöneticisi üstlenir; sürekli ortak çalışma matris / proje ekibiyle tanımlanır.
Kod yazmadan bugün: iki kişinin üstünde ortak bir grup pozisyonu varsa o yönetici ikisine de atayabilir. İkinci koltuk vermek çalışır ama
önerilmez (onay zincirini ve zaman çizelgesi onaycısını da değiştirir). Gelecek regresyon riski: 🟡 (atama kapsamı, kişisel veri görünürlüğü).

**Ek 2026-10-05 — (a) GÖREV TALEBİ CT KABULÜ (kod): dal `feature/platform/task-request` `f748673cb` (WP-WCN-TASK-REQUEST-01 D1, FIX1–FIX7).** FIX7 bağımsız
gözden geçirmesi engelleyici ve zorunlu bulmadı; CT sabotajı (vitest) 2/3 — J2 eşdeğer mutasyon (her hata cümleyi `failure()`'dan önce yeniden yazdığı için başarıda
sıfırlama gözlenemez). **Birleştirme bekliyor:** motor (D2 + BL-547) takeover dalında; görev talebi dalı oraya birleştirilirken CT `TaskItemWriteHandlers` /
`TaskItemTransitionHandlers` uzlaştırmasını ve gönderimde açık inceleme kodunu düzeltir (motor PendingReview + açık incelemede `TASK_INVALID_STATE` dönüyor →
`REVIEW_PENDING` olmalı; görev talebinin `errorReviewAlreadyOpen` eşlemesi buna bağlı). Takeover'da GSKU FIX7 sürdüğü için birleştirme o teslimden sonra.
Kabulde kalan küçükler: `inputTooShort` cümlesi ölü (select2 `minimumInputLength` 0) · açılış çağrısında try/catch yok (gerçek istemciyle ulaşılamaz) · AgendaItemDto
tarayıcısı blok gövdeli eşleyiciyi ve `global::` adı görmüyor · 403 ve 500 aynı "yeniden deneyin" cümlesi.

---

### BL-510

**Ürün modüllerinin denetim izinde iki bilinen eksik: değişen alan adları (sözleşme v2) ve reddedilen denemeler**

DURUM: AÇIK · SAHİP: CT (MDM ürün modülleri + denetim standardı) · BULAN: WP-GP-AUDIT-TRAIL-01 Aşama 1 / 1b ölçümü · KAYIT: 2026-10-02

Global Product ve kardeş modüller kaydı iş verisiyle aynı belgeye "niyet" olarak yazıyor (işlem içi outbox); iz `mdm-urun-niyeti` olarak kabul
edilecek (BL-503). İki eksik bu paketin dışında bırakıldı: (1) **Değişen alan adları:** niyet sürüm numarası + özet + anlık görüntü referansı
taşıyor, hangi alanların değiştiğini taşımıyor (kural §3). Eklemek MDM ↔ Platform tel sözleşmesinin sürümünü değiştirir (Platform tek sürüm
kabul ediyor): üretici, ayrıştırıcı, parmak izi, makbuz doğrulaması ve iki servisin testleri; bekleyen eski sürümlü niyetler için iki sürümün
birlikte kabulü. (2) **Reddedilen / başarısız denemeler** üç serviste de yazılmıyor (MDM niyeti yalnız başarılı yazımda oluşur; Platform'un
güvenilir başlat / iptal komutları reddi kaydetmiyor; Auth belirteç reddi BL-503 D3'te ele alınıyor). Kural §3 "reddedilen komut da kaydedilir"
diyor; hiçbir test bunu ölçmüyor. Karşılaştırma: Veeva ve MasterControl'de başarısız imza / onay denemesi de denetim izine düşer.
Gelecek regresyon riski: 🟡 (iki servis arası sözleşme).

---

### BL-511

**Görevde "kabul" kapısı yalnız ekranda: sunucu kabul edilmemiş işte başlat / tamamla / planla isteğini geri çevirmiyor**

DURUM: AÇIK · SAHİP: CT (MOD-0024 Görev Merkezi) · BULAN: WP-WCN-TASK-REQUEST-01 Aşama 1 ölçümü · KAYIT: 2026-10-02

`start` / `complete` / `plan` sunucuda yalnız "çağıran atanan mı" diye bakıyor (`TaskItemTransitionHandlers.cs:317-323`, `:1099-1110`);
`AcceptedByUserId` okunmuyor. İş raporu, takvim, zaman çizelgesi seçicisi ve son tarih hatırlatması da kabul edilmemiş işi ilk andan atananın
işi sayıyor. Ekran düğmeyi göstermediği için kullanıcı fark etmiyor; uç doğrudan çağrılırsa kabul adımı atlanır. Görev talebi (BL-509) bu
kusuru tekrarlamıyor (teklif edilmiş işte atanan boş). Düzeltme: kabul kuralı sunucuda tek yerde; diğer yüzeylerin "kabul edilmemiş"i nasıl
sayacağı ayrı karar (iş raporunda yaşlanma oluşturma anından mı kabulden mi). Karşılaştırma: SAP ve Oracle iş listelerinde "rezerve et / üstlen"
sunucu durumudur. Gelecek regresyon riski: 🟡 (beş yüzey aynı kuralı okuyacak).

---

### BL-512

**Onaycı seçicisi kiracıdaki herkesi döküyor: `lookups/decision-makers` aramasız ve sınırsız**

DURUM: KAPANDI (şeritte; canlı denetim sahip girişiyle) · SAHİP: CT (MOD-0024 Görev Merkezi) · BULAN: WP-WCN-TASK-REQUEST-01 Aşama 1 ölçümü · KAYIT: 2026-10-02

`TasksController.cs:1203-1209` (izin `platform.tasks.create`) pozisyonu olan herkesi ad + pozisyon + birim + tüzel kişi kimliğiyle tek
istekte döndürüyor (`GetTaskAssignmentPersonLookupHandler.cs:80-81`); BL-057 atama kapsamını daraltırken bu ucu bilerek muaf tutmuştu
(onaycı başka şirkette olabilir). Sonuç: görev açabilen her kullanıcı, öbür ülke şirketlerinin çalışan listesini alabiliyor: BL-057'nin
kapattığı kapının yan penceresi. Görev talebi için yazılan arama ucu (en az 3 harf, en çok 10 sonuç, hız sınırı, yalnız ad + şirket +
pozisyon) bu ucun da yerini almalı. Hukuk kararları belgesine girer (şirketler arası ad görünürlüğü). Gelecek regresyon riski: 🟡.

**Ek 2026-10-04 — yalnız sunucu değişikliği iki akışı kırar; paket ayrıldı.** WP-PLATFORM-SCOPE-SMALL-01 madde B olarak gönderildi;
yürütme sohbeti kod yazmadan durdu, CT kodda doğruladı (lookup-scope @605f37d68). Ucu açılışta tüm listeyle dolduran iki çağıran var:
(1) görev formu `Tasks/form-page.js:844-848`: gözden geçiren ve onay yöneticisi seçicileri tüm listeyle çiziliyor, düzenlemede kayıtlı
kimlik var olan seçenek seçilerek gösteriliyor. Arama ucunda açılışta seçenek olmaz: kayıtlı onaycı boş görünür, düzenleme kaydı onu
silebilir. (2) Görev Merkezi devretme penceresi `WorkCenterNext/app.js:10215-10240`: liste boşsa eylem `DelegateNoEligiblePeople` ile
reddediliyor; aramasız istek boş dönerse onay devretme tamamen kırılır. Paket birlikte gitmeli: (a) sunucu `?search=` (en az 2–3 harf,
en çok 10–20, yalnız ad + pozisyon + birim) + kayıtlı kimlikleri ada çeviren `?ids=` çözme ucu, (b) form seçicileri select2 ajax arama
(aynı formdaki `resolveStoredRecords` kalıbı), (c) devret penceresinde arama kutusu; "boş liste → reddet" kuralı "aramada kimse yok"
mesajına döner. Hız sınırı: Platform'da istek başına sınırlayıcı ölçülmedi, paketin ilk ölçümü. Ayrı paket olarak açılacak
(WP-PLATFORM-DECISION-MAKERS-SEARCH-01), dokunulan dizgiler 7 dil (kiracı ekranı).

**Kapanış 2026-10-04 — dal `fix/platform/decision-makers-search` @37b05a216, şeritte 0f1f30cdd.** Uç yalnız `?search=` (en az 2 harf, en çok
20 sonuç, ad ve pozisyon adında aksan duyarsız) ya da `?ids=` (en çok 10) cevaplıyor; kullanıcı başına "people-search" hız sınırı
(dakikada 60). Rehber 60 sn, eksik rehber 10 sn önbellekte; ad çözümü sınırlı çağrılardan ve adlı, zaman aşımlı istemciden geçiyor. Form
seçicileri ve devret penceresi ortak `shared/diten-people-search.js` select2 taşıyıcısını kullanıyor; kayıtlı onaycı `?ids=` ile adına
çevriliyor. Aynı dalda BL-531 (toplantı katılımcıları) kapandı. Üç tur bağımsız gözden geçirme; CT sabotajı 3/3 adı yazılı testle kırmızı.
Canlı (dev, 2026-10-04, sahip oturumu, şerit `7d9d8009d` yapısı): aramasız / tek harf → 400 `PEOPLE_SEARCH_TOO_SHORT`; "ad" → 2 kişi,
yalnız userId + ad + pozisyon + birim; toplantı katılımcı ucu aynı kural; görev formundaki onay yöneticisi seçicisi "en az 2 karakter" diyor, "ad"
yazınca iki aday. Bulunan veri açığı → BL-542 (silinmiş kullanıcı pozisyonda "Bulunamayan kişi" olarak). Kalan: Görev Merkezi devret penceresi.

---

### BL-513

**Üste atanan iş ("yukarı yön talebi") görev talebiyle aynı düzene taşınsın: bugün yarım**

DURUM: AÇIK · SAHİP: CT (MOD-0024 Görev Merkezi) · BULAN: WP-WCN-TASK-REQUEST-01 Aşama 1 ölçümü · KAYIT: 2026-10-02

Üste atanan iş bir iş akışı örneği açıyor (`TaskUpwardRequestService.cs:65`, nesne tipi `task-request`); cevabı kimse okumuyor, ret görevi
iptal etmiyor (BL-023'ün açık kalanı). Görev talebi (BL-509) teklif / kabul / ret / geri çekme düzenini kuruyor; yukarı yön de aynı kavram
olmalı (tek "talep"). Taşıma, görev talebinin sunucu ve ekran dilimleri kabul edildikten sonra. Gelecek regresyon riski: 🟡 (mevcut açık
örneklerin geçişi).

---

### BL-514

**Platform'da işlem içi yazılan denetim kaydı merkezi günlüğe hiç ulaşmıyor (25 komut); abonelik uçları bir aydır boru hattında düşüyor**

DURUM: AÇIK — düzeltme yazılıyor (WP-PLATFORM-AUDIT-INTX-01, dal `feature/platform/audit-in-transaction`) · SAHİP: CT (denetim standardı) · BULAN: WP-PLATFORM-TENANT-MODULES-01 K1 ölçümü + CT dev ölçümü · KAYIT: 2026-10-02

Kiracı modül yetkilendirmesi, kiracı aboneliği ve genel uygulanabilirlik (modül kataloğu, abonelik planları, modül kaydı) komutları kaydı iş
verisiyle aynı işlemde `audit_outbox`'a yazıyor ama yük, taşıyıcının zorunlu alanlarını (kiracı, aktör, kategori, kaynak servis) taşımıyor:
satır kalıcı hataya düşüyor, `audit_events`'e geçmiyor, aktör hiçbir yerde yok. Uçtan uca testle ve dev verisiyle kanıtlı (7 satırın 7'si
ölü, hepsi modül kaydı). Defterde `platform-islem-ici` izi bu yüzden "aday"dı. İkinci bulgu: 8 abonelik komutu `AuditBehavior`'ın izin
listesine hiç eklenmemiş (261f99105, 2026-08-31); boru hattından geçen abonelik uçları istisnayla bitiyor. Kapı: kiracı modülleri ekranı
(BL-500) bu düzeltme kabul edilmeden ana dala gitmez (K2). Paket dışında kalan: var olan ölü satırların onarımı (canlıda sayı bilinmiyor;
aktör bilgisi satırda yok). Karşılaştırma: SAP ve Oracle'da lisans / abonelik değişikliği değişiklik belgesi olmadan yazılamaz.
Gelecek regresyon riski: 🟡 (merkezi yolla ortak kurucu).

---

### BL-515

**Liste yüklenemediğinde ekran tarayıcının İngilizce uyarı kutusunu gösteriyor ya da hiçbir şey göstermiyor (ortak liste bileşeni)**

DURUM: AÇIK · SAHİP: CT (BL-440 liste bileşeni) · BULAN: Kullanıcılar ekranı FIX2 bağımsız gözden geçirmesi · KAYIT: 2026-10-02

Ortak liste bileşeninde ve varsayılanlarda liste isteğinin reddi için bir işleyici yok; DataTables'ın kendi varsayılanı (`errMode: "alert"`)
çalışıyor. Liste isteği 401 / 403 / 5xx dönerse tarayıcı `DataTables warning: table id=… - Ajax error` diye İngilizce bir uyarı kutusu açıyor;
ağ kesikse hiçbir şey görünmüyor (boş tablo). Yalnız üç sayfa kendi işleyicisini yazmış (PPM, Referans Veri, Farmakovijilans vaka girişi).
Sayfa standardının "hata hali" maddesi her liste sayfasında bu yüzden eksik. Düzeltme tek yerde: bileşen uyarı kutusunu kapatır, tablonun
içinde okuyanın dilinde "Liste yüklenemedi" cümlesi ve "Yeniden dene" düğmesi gösterir; oturum düşmüşse girişe yönlendirme kuralı aynen.
Ortak tarayıcı dosyası olduğu için canlı sayfa bakışı zorunlu. Karşılaştırma: SAP Fiori ve Oracle Redwood listelerinde yükleme hatası
tablonun içinde mesaj + yeniden dene olarak gösterilir. Gelecek regresyon riski: 🟢 (eklemeli; üç sayfanın kendi işleyicisi bileşene taşınır).

---

### BL-516

**Auth servisi 500 hatasında istisnanın kendi mesajını yanıta yazıyor**

DURUM: AÇIK — Kullanıcılar dalının kabulünde CT düzeltiyor · SAHİP: CT (Auth) · BULAN: Kullanıcılar ekranı FIX2 bağımsız gözden geçirmesi · KAYIT: 2026-10-02

`GlobalExceptionHandler` her durumda `detail = exception.Message` yazıyor; 500'de bu, veritabanı sürücüsünün ya da bir kütüphanenin iç
mesajıdır (sunucu adı ve portu içerebilir). Liste, dışa aktarma ve silme istekleri tarayıcıdan doğrudan gateway'e gittiği için yanıt
gövdesi ağ sekmesinde okunabilir (ekran ve konsol okumuyor). Düzeltme: 5xx'te sabit, içerik taşımayan bir cümle + izleme numarası; istisna
yalnız sunucu günlüğüne. 4xx'teki uygulama cümleleri değişmez. Aynı kalıp öbür servislerin istisna işleyicilerinde de aranmalı (ayrı ölçüm).
Gelecek regresyon riski: 🟢.

---

### BL-517

**Web uygulamasının günlüğüne arama terimleri düşüyor (istek adresinin sorgu kısmı)**

DURUM: AÇIK · SAHİP: CT (altyapı) · BULAN: Kullanıcılar ekranı FIX2 bağımsız gözden geçirmesi · KAYIT: 2026-10-02

`Logging:LogLevel` hem `Default` hem `Microsoft.AspNetCore` için `Information`; HTTP istemci fabrikası giden her isteğin tam adresini,
ASP.NET de gelen isteğin yolunu sorgu dizgisiyle yazıyor. Kişi arama uçlarında (`…/lookup?search=…`) yazılan ad parçaları günlüğe giriyor.
Kullanıcılar vekili kendi hata satırında yalnız yolu yazıyor ama fabrikanın ve sunucunun satırları aynı adresi tam yazıyor. Karar gerekir:
üretimde bu iki kategori `Warning`'e mi alınır, yoksa sorgu dizgisi günlükten süzülür mü. Hukuk kararları belgesine girer (günlüklerde
kişisel veri). Gelecek regresyon riski: 🟢.

---

### BL-518

**MOD-0290 (Ürün, Kalem ve SKU Ana Verisi) tamamlama programı: yazılmış sekiz parçanın dışında on bir eksik**

DURUM: AÇIK — sahip kararı 2026-10-02: "eksikleri çıkarıp hepsini yapmalıyız" · SAHİP: CT (MDM ürün modülleri) · BULAN: sahip + CT ölçümü (devralma dalı `feature/mdm/product-five-takeover`) · KAYIT: 2026-10-02

Blueprint bu modül için ürün kaydı, KALEM kaydı, SKU, ölçü birimi eşlemesi, ürün tanımlayıcıları ve yaşam döngüsü sayıyor. Yazılmış ve
kabul bekleyen: Global Ürün, GSKU, LSKU, Bitmiş Ürün, Kısaltma Kaydı, Şirket Kapsamı, Marka, Ürünler (CRM tarafı). Eksikler (ölçüldü; kodda
dosyası yok): (1) **malzeme / kalem kaydı** (hammadde, ambalaj malzemesi): Blueprint'te var, modül tanımında ve kodda hiç yok; stok ekibi
(MOD-0173, dal `feature/sce/mod-0173-inventory-ledger-valuation`, henüz kodu gönderilmedi) buna bağlanmak zorunda; (2) ölçü birimi eşlemesi
ve çevrimi; (3) ürün tanımlayıcıları (GTIN); (4) Ürün Tanımı Sürümünün alanları (form, veriliş yolu, güç: tasarımda var, kodda yalnız
kimlik); (5) ikinci sürüm kuralı (REV-002: bugün sürüm yalnız ilk GSKU ile birlikte doğuyor); (6) madde + bileşim (bileşim Ürün Tanımı
Sürümüne bağlanır; bugün istekte bileşim alanı reddediliyor); (7) pazar ticari adı (LSKU'ya bağlı; tasarımda var, kod yok); (8) Marka ↔
Global Ürün bağı ve marka tescilleri (Marka bugün yalnız CRM tarafındaki Ürünler ekranına bağlı); (9) ambalaj hiyerarşisi; (10) eski sistem
kodları ve toplu veri taşıma; (11) Global Ürün adını değiştirme ve geçmişi. Modülün dışında ayrı modül olanlar: ruhsat (RIM), pazar arz
ataması, etiket / prospektüs, üretim reçetesi, dış sistem beslemeleri. Tahmin: yaklaşık 13 paket (≈ 50 prompt; iki sohbet paralel 3–4 hafta).
Önerilen sıra: Global Ürün kabulü → malzeme / kalem + ölçü birimi → tanım alanları + ikinci sürüm → madde + bileşim → tanımlayıcılar,
pazar ticari adı, marka bağı, ambalaj → veri taşıma. Karşılaştırma: SAP'de tek malzeme kaydının görünümleri; Oracle'da kalem ana verisi +
ürün merkezi; ilaç tarafında IDMP / Veeva ürün tanımı + madde. Gelecek regresyon riski: 🟡 (ikinci sürüm kuralı ve stok ekibiyle ortak kimlik).

---

### BL-519

**Onaylarda elektronik imza: parolayı yeniden doğrulama kanıtı + ortak imza servisi + onay motoruna bağlama**

DURUM: AÇIK — sahip konuşmak istiyor (2026-10-02) · SAHİP: CT (Auth + MOD-0023 onay motoru) · BULAN: sahip · KAYIT: 2026-10-02

Ölçüm: Doküman Yönetimi'nde imza temeli var (MOD-0029-FU23: kim, neyi, hangi anlamla, kaydın hangi parmak izine karşı), ama "yeniden
doğrulama yapıldı" bilgisi çağıranın beyanı (`ReAuthenticationPerformed = authContext is not null`); Auth'ta işlem anında parola doğrulayan
bir uç yok; onay motorunda imza kavramı yok (onay = izin + aday). Kodun kendi notu: nitelikli e-imza değildir, 21 CFR Part 11 / Annex 11
uyum iddiası yoktur. Kararlar: hangi onaylar imza ister (öneri: Global Ürün kimlik onayı için onay + denetim kaydı yeter; bileşim, ürün
tanımı, ruhsat verisi, kalite kayıtları imza ister) · imza anında parola + anlam + kayıtta ad / tarih / anlam · tek ortak servis · sistem
doğrulaması (validasyon) ayrı iş. Dilimler: tasarım → Auth imza kanıtı → ortak imza servisi → motor adım kuralı → ekran (7 dil) → ilk
kullanan modül → doğrulama kanıt paketi; tahmin ≈ 12 prompt, 5–7 iş günü. Karşılaştırma: Veeva ve MasterControl'de onay anında kullanıcı
adı + parola yeniden girilir ve imzanın anlamı kayıtta görünür. Gelecek regresyon riski: 🟡 (onay kapısına yeni şart).

---

### BL-520

**Üst çubuktaki arama kutusu her dilde İngilizce yazıyor: "Search [CTRL + K]"**

DURUM: AÇIK · SAHİP: CT (kabuk) · BULAN: CT canlı sayfa kontrolü (Kullanıcılar kabulü, 2026-10-03) · KAYIT: 2026-10-03

Etiket `wwwroot/assets/js/main.js:213`'te sabit (`placeholder: 'Search [CTRL + K]'`); Türkçe arayüzde de İngilizce görünüyor. Aramanın kendisi
yerelleştirilmiş çalışıyor (sonuçlar "YÖNETİM → Kullanıcılar, Kullanıcı Rolleri"). Düzeltme: etiket kabuğun dil sözlüğünden okunur (kiracı kabuğu
7 dil, platform kabuğu en + tr); kısayol tuşu adı da dile göre (macOS'ta ⌘K). Ortak tarayıcı dosyası olduğu için canlı sayfa bakışı. Küçük; "?"
sayfa yardımı (BL-495) ile aynı pakette yapılabilir. Gelecek regresyon riski: 🟢.

---

### BL-521

**İş ortağı yöneticisi (partner_admin) bütün kiracıları görüp değiştirebiliyor: "izin verilen kiracılar" hiçbir yerde okunmuyor**

DURUM: AÇIK · SAHİP: CT (Platform yetki) · BULAN: WP-PLATFORM-TENANT-MODULES-01 FIX1 ölçümü (2026-10-03) · KAYIT: 2026-10-03

`PlatformActor` politikası platform_admin ile partner_admin'i aynı sayıyor (`Infrastructure/DependencyInjection.cs:99-101`); `[HasPermission]`
her platform aktörünü her anahtarda geçiriyor (`API/Security/HasPermissionAttribute.cs:36-38`); kiracı çözümleme ara katmanı yönetim yolunda
yalnız aktör tipine bakıyor (`TenantResolutionMiddleware`: Common 347-351, gateway 426-430). `PlatformAdministrator.PartnerId` ve
`AllowedTenantIds` yalnız yönetici kaydında yazılıyor; JWT'de yok (`TokenService.cs:107-113`), sorgu filtresinde ve denetleyicilerde
okunmuyor; `EntitlementDenyReason.PartnerScopeViolation` tanımlı ama kullanılmıyor; `Tenant` varlığında iş ortağı alanı yok. Sonuç: bir
iş ortağı yöneticisi kiracı listesini, ayrıntısını, düzenleme / askıya alma / silme uçlarını, abonelik ve modül uçlarını BÜTÜN kiracılar
için kullanabiliyor. Bir test (`HasPermissionAttributeDualReadTests:86`) sınırsız geçişi sabitliyor. Kapalı olan iki yer: denetim uçları
(`PlatformAdminOnly`) ve kiracı tarafı yetki işleyicileri. Düzeltme dört aile birlikte: izin verilen kiracılar ya belirtece girer ya
sunucuda okunur; liste süzülür, kimlikle erişimde kapsam dışı = bulunamadı; denetim kaydı. Karşılaştırma: SAP'de iş ortağı erişimi
organizasyon birimi / şirket koduyla, Oracle'da veri erişim kümeleriyle sınırlanır. Önce sahibe: canlıda partner_admin hesabı var mı?
Gelecek regresyon riski: 🟡 (yetki katmanına dokunur).

**Ölçüm (2026-10-03, tam liste — WP-PLATFORM-TENANT-MODULES-01 FIX1 raporu):** kapılar `Infrastructure/DependencyInjection.cs:91-103`
(PlatformActor), `API/Security/HasPermissionAttribute.cs:37-38` + `~:75`, `API/Security/ClaimsActorPermissionContext.cs:38-45,57`,
`Platform.Common/Tenancy/TenantResolutionMiddleware.cs:113-130,347-351`, gateway `Middleware/TenantResolutionMiddleware.cs:151-165,426-430`,
`WorkItemsController.cs:302-306`, `PlatformActorDashboardAuthorization.cs:25` (Hangfire panosu partner'a açık),
`SelfAccessExplainService.cs:23,182-184`. Veri: `PlatformAdministrator.cs:14-15` (`PartnerId`, `AllowedTenantIds`) yalnız yönetici
CRUD'unda yazılıyor; `PartnerId` süzgeci yalnız YÖNETİCİ listesinde (`GetPlatformAdministratorsHandler.cs:24-36`); belirteçte kapsam yok
(`Auth TokenService.cs:107-113`, `RefreshTokenCommandHandler.cs:136-141,192-195`); `EntitlementDenyReason.PartnerScopeViolation` kullanılmıyor.
Aileler: kiracı listesi / istatistik (`Admin/TenantsController.cs:14-56`, `TenantRegistryRepository.cs:92-123`), kiracı `{id}` yolları
(`:65-212`: düzenle, marka, askıya al, sil, toplu sil, modüller, kullanıcılar, ayarlar), abonelik (`TenantCommercialSubscriptionsController.cs`),
modüller (`TenantModuleEntitlementsController.cs`). Testler: `HasPermissionAttributeDualReadTests.cs:86-88` sınırsız geçişi SABİTLİYOR (bu iş onu
değiştirir); kapalılığı doğrulayanlar `TenantModuleAuthorizationHandlerTests.cs:189`, `TenantFeatureAuthorizationHandlerTests.cs:189`.
**Şart:** bir iş ortağına Platform yönetici hesabı açılmadan önce bitmeli (sahip 2026-10-03: canlıyı yalnız kendi ekibi kullanıyor).

**İlerleme 2026-10-04 (dal `fix/platform/partner-admin-scope`, henüz hatta değil):** d12add3d5 (kapsam, varsayılan kapalı) → c857e0b12 FIX1
(iş ortaklığı kayıttan; yenileme türü kayıttan; anahtarsız eylem kapalı; tür / askıdan çıkarma / ticari / kota yazmaları yalnız platform
yöneticisi; sistem kiracısı kapsama girmez) → FIX2 yazılıyor: giriş ve zorunlu parola değişimi türü hâlâ Auth'un kopyasından basıyor ve
`NormalizeActorType` `partner_admin`'ı `platform_admin` yapıyor (iş ortağı kendine platform_admin belirteci bastırabilir; Platform kayda
baktığı için güvende, MDM ve Hangfire panosu değil), tel sözleşmesi testsiz, geçici hata oturumları iptal ediyor, son platform yöneticisi
korumasız. CT sabotajı d12add3d5'te P1–P3 yeşil kalmıştı (FIX1 testleri kapattı). Dağıtım sırası: Platform Auth'tan önce.
**Sahip kararı bekleyen (iş ortağı hesabı açılmadan önce):** (1) iş ortağı kendi kiracılarında ticari işlem yapsın mı (plan ata, modül aç,
iptal; varsayılan HAYIR yazıldı) · (2) var olan kullanıcının e-postasıyla yönetici daveti o kullanıcının parolasını sıfırlayıp Admin veriyor:
iş ortağına açık kalsın mı · (3) iş ortağının yazdığı alan adı platformun davet e-postası bağlantısına giriyor (oltalama yolu) · (4) giriş
ayarlarıyla MFA kapatma / IP kilidi / denetim saklama süresini kısaltma iş ortağına açık mı · (5) askıya alma açık, askıdan çıkarma kapalı:
böyle kalsın mı · (6) admin@diten.com tohum hesabı her açılışta PlatformAdmin + etkin yapılıyor (kalıcı düşürülemez); canlıda da → BL-534 ·
(7) Platform'da onaylı HER platform yöneticisi her izin anahtarını geçiyor (`HasPermissionAttribute.cs:93`, a2445b8f2'den beri): ReadOnly / SupportAdmin
rolleri Platform içinde fiilen SuperAdmin; roller yalnız belirteçle diğer servislere gidince anlam kazanıyor. Platform yöneticileri yalnız rollerinin izinlerini
alsın mı? (Canlıdaki platform yöneticilerinin davranışını değiştirir; önce rol / izin eşlemesinin tam olduğu ölçülmeli.) FIX3 kendi rolünü değiştirmeyi kapatıyor.

**Ek 2026-10-04 — kiracı giriş kapısı platform kiracısına açık (FIX5 ölçümü, CT kodda doğruladı; main ve origin/main'de de aynı).** `POST
api/tenant-auth/login` + `X-Tenant-Id` = platform sistem kiracısı + bir platform yöneticisinin parolası → 200, `actor_type=tenant_user`, 274 izin
talebi (175'i `platform.*`, ayrıca `auth.users.read` / `update`). Bu belirteçle başka bir platform yöneticisinin Auth hesabına `disable`, `enable`,
`reset-password`, rol atama açılıyor; `LoginCommandHandler` platform kiracısını ayırmıyor, `PermissionAuthorizationHandler` yalnız `permission` talebine
bakıyor, Platform yönetici kaydına (askı, iş ortağı, silinme) hiç sorulmuyor. Platform tarafı `actor_type` ilkeleriyle kapalı. CT kararı A + D (FIX5):
kiracı kapısı platform kiracısına belirteç basmaz (genel 401) + Auth platform kiracısının `tenant_user` belirtecini platform dışı yollarda 403 ile keser.
Verilmiş belirteçler `SessionTimeoutMinutes` (vars. 60) dolana kadar geçerli. Canlıda da açık olabilir (canlı ölçülmedi; giriş ayarları platform kiracısı
için döner — kod okuması). Gelecek regresyon riski: 🟢 (kapı kapatma; platform kapısı değişmez).

**Ek 2026-10-05 — CT KABULÜ (kod): dal `fix/platform/partner-admin-scope` `6dea5a0c4` (FIX6 kararı A' + D' platform HESABINA daraltılmış, FIX7 işaret onarımı +
Mongo süzgeci + rol düzeyi, FIX8 fail-closed işaret yazımı + tersine görünürlük + boşluk farkında tek süzgeç).** FIX8 bağımsız gözden geçirmesi: engelleyici
ve zorunlu yok; CT sabotajı 4/4 KIRMIZI (FIX6'da NOT RED olan P1 FIX7'de, FIX7'de NOT RED olan Q2 FIX8'de kızardı). **Şeride birleştirme BEKLİYOR:** dev'deki tek
platform hesabı (`a***@diten.com`) kiracı tarafında 74 oturum açmış — birleşince bu hesapla kiracı girişi kapanır; sahip için sıradan bir kiracı yöneticisi
hesabı gerekir (canlı ekip için de aynı soru açık). Birleştirmede uzlaştırılacaklar: e-posta dalının `PlatformAdministratorProvisioningService.cs:90` satırı,
admin-reset dalının PlatformAuthController / LoginCommandHandler / RefreshTokenCommandHandler değişiklikleri. Kabulde kalan küçükler (BL-550'ye eklendi):
platform zorunlu değişiminde rol yakınsaması işaret yazımından önce (`PlatformAuthController.cs:307-314`; giriş ve yenilemede tersi) · süzgeç yorumunda
var olmayan test adı (`PlatformAccountFilterParityTests`) · parite testi `char.IsWhiteSpace`'in ~25 karakterinin yalnız birkaçını deniyor · bir test kâhini
emekli kuralı (`!= null`) kullanıyor.

---

### BL-522

**Ana dalda CRM'in derleme çıktıları izleniyor: 324 dosya, 301 MB**

DURUM: AÇIK · SAHİP: CT (depo düzeni) · BULAN: kalem kaydı (P1) tasarım ölçümü, CT doğruladı (2026-10-03) · KAYIT: 2026-10-03

`origin/main`'de `services/Diten.CrmService/src/Diten.CrmService.Api/.tmp-*/Debug/…` altında 324 dosya (toplam 301,0 MB) izleniyor;
2026-08-28 tarihli bir ara kayıt commit'iyle (`0f71a237c`) girmiş, o günden beri her dalda ve her klonda var. `.gitignore` yalnız `tmp/` ve
`.tmp/` içeriyor, `.tmp-*` kalıbını yakalamıyor. Düzeltme: dosyalar ayrı bir dalda `git rm -r --cached` ile izlemeden çıkarılır, `.gitignore`'a
`.tmp-*/` eklenir, PR ile main'e. Geçmişteki 301 MB yerinde kalır (geçmişi yeniden yazmak bütün dalları kırar; yapılmaz). CRM'in dosyası
değil derleme artığı olduğu için CRM ekibine yalnız bilgi verilir. Gelecek regresyon riski: 🟢.

---

### BL-523

**Global Ürün onayı: iptal kanıtı servis kimliğiyle okunamıyor; inmiş ama talep edeni dönmeyen geri çekme süresiz bekliyor**

DURUM: AÇIK · SAHİP: CT (MOD-0023 iş akışı motoru + MOD-0290 Global Ürün) · BULAN: WP-GP-1B-A-RESILIENCE-01 FIX3 ölçümü (2026-10-03) · KAYIT: 2026-10-03

Geri çekmede Platform'a giden iptal inmiş olabilir ama cevabı kaybolmuşsa, MDM ürünü Taslak'a çevirmek için iptal kanıtı (günlük kimliği,
sıra, sürümler) ister. Bu kanıtı bugün yalnız iptal ucu veriyor ve o da kullanıcı belirteci istiyor; servis kimliğiyle okunabilen
`terminal-decision-evidence` iptal edilmiş örnekte 409 INCONSISTENT dönüyor (`GetTrustedWorkflowTerminalDecisionEvidenceHandler.cs:66-77,
213-219`), `start-result` yalnız "Cancelled" diyor (`GetTrustedWorkflowStartResultHandler.cs:97`). Sonuç: talep eden aynı kimlikle hiç dönmezse
kayıt süresiz `WithdrawalOutcomeUnknown`'da kalır. İş: (1) Platform'da servis kimliğiyle, idempotency anahtarına göre iptal kanıtı dönen uç;
(2) MDM'de unutulan geri çekmenin izi + karar yoklaması 409 aldığında `start-result` "Cancelled" ise karantina yerine "motor iptal etti,
talep edeni bekliyor" durumu (kanıt gelince Taslak); (3) GP canlıya çıkmadan önce keşif dizini: eski park edilmiş `AwaitingMakerReplay`
satırları bir kez taşınır ya da yeni adla kısmi dizin kurulur (aynı adla anahtar değişirse depo kurucusu `IndexKeySpecsConflict` ile MDM'yi
açtırmaz; dev'de bu koleksiyonda 0 belge, 2026-10-03 salt okunur sayım); (4) yükseltilmiş (Escalated / TimedOut) onay: GP şablonuna SLA
kuralı kurulursa ön kontrol 404 → geri çekme "kapandı", karar yoklaması 409 → karantina (BL-502 ile birlikte). Gelecek regresyon riski: 🟡.

**Ek (2026-10-03, FIX3 gözden geçirmesi — GP'nin sonraki turu):** (5) "kapandı" reddi kapanma nedeni okunmadan saklanıyor
(`GlobalProductIdentityWorkflowProcessor.cs:325-328, 467-472`): yükseltilmiş onayda ön kontrol NotFound der ama onay hâlâ karar verilebilir; ya da
Platform iptali commit edip 409 `WORKFLOW_TRUSTED_CANCEL_INCOHERENT` dönebilir → kayıt kalıcı "kapandı", karar yoklaması karantina. Düzeltme:
reddi saklamadan önce terminal kanıtı oku (`ResolveAbandonedWithdrawalAsync` gibi); kararsız kapanma WithdrawalOutcomeUnknown kalır. (6) Aynı komutla
iki eşzamanlı istekte tekrar "ilk deneme" görünebiliyor (bellekteki bayrak + kontrol noktası; kira kuşağı karşılaştırılmalı). (7) Ekranda
Platform'un açık uçlu ham kodları yerine tek sabit MDM kodu (Platform kodu yalnız günlükte) — 7 dile çevrilebilir küme. (8) `Completed` +
`WithdrawalCommandId` belirsiz: onay kazandığında `…WITHDRAWAL_REPLAY_DRIFT` yanıltıcı. (9) Testsiz kurallar: proc:665 kontrol noktası şartı,
:753 / :624 dizi alanları, :328 eski satır cevabı, :1821 sınır (129) ve kontrol karakteri, policy:88 `ApprovalChanging`, :749-751 alarm cümleleri.
Sahiplenmenin sınırsız beklemesi (A3) ve park edilen geri çekmenin alarmı (A1) GSKU FIX1'de ortak desen olarak düzeltiliyor.

---

### BL-525

**Görevlerin yazma uçları görevin okunabilirliğini sormuyor: yorum, düzenleme, kontrol listesi, bağımlılık, alt görev, zaman girişi**

DURUM: AÇIK — düzeltme WP-WCN-TASK-REQUEST-01 D1 FIX2'de (`scratchpad/treq-fix2-prompt.md`) · SAHİP: CT (Görev Merkezi / görevler) · BULAN: WP-WCN-TASK-REQUEST-01 D1 FIX1 ölçümü + bağımsız gözden geçirme (2026-10-03) · KAYIT: 2026-10-03

**Genişletme (gözden geçirme, CT doğruladı):** açık yalnız yorumda değil. `UpdateTaskItemHandler` görevi yalnız kimlikle buluyor
(`TaskItemWriteHandlers.cs:54-102`, okuma kontrolü yok); kontrol listesi (`ChecklistHandlers.cs:40-52, 136-155`), bağımlılık
(`TaskDependencyHandlers.cs:40-59`), alt görevde üst görev (`CreateTaskItemHandler.cs:334-360`) ve zaman girişi (`SaveTimeEntriesHandler.cs:144`)
aynı. `tasks.update` taşıyan herhangi bir kiracı kullanıcısı, kimliğini bildiği HER görevi değiştirebiliyor; kimlikler `lookups/link-candidates`
ile listelenebiliyor. Bu açık talep paketinden önce de vardı → canlıdaki Görev Merkezi'nde de olmalı. CT kararı: okuma kuralı her görev
yazımının tabanı (okuyamayan 404), ilişki kuralları onun içinde daraltır.

`AddTaskComment` işleyicisi yorumu yazmadan önce çağıranın o görevi okuyup okuyamadığını sormuyor; görev kimliğini bilen ya da tahmin eden,
okuma anahtarı olan herhangi bir kiracı kullanıcısı başka birimin, başka şirketin görevine yorum yazabiliyor (yorum o görevin etkinlik akışına
ve bildirimlerine girer). Düzeltme: yorum, okuma kuralıyla (`TaskReadAccessPolicy`) aynı kapıdan geçer; okuyamayan 404 (varlık sızdırılmaz);
görev talebindeki "sorulan kişi yanıtlamadan yorum yazamaz" kuralı korunur. HTTP testi: başka birimin görevine yorum → 404, yorum yok.
Karşılaştırma: SAP / Oracle'da not ve yorum, nesnenin okuma yetkisine bağlıdır. Görev talebi paketinin bir sonraki turunda. Gelecek regresyon
riski: 🟢 (yalnız daraltır).

---

### BL-526

**Organizasyon: koltuğa ikinci bir tarih yazımı Mongo'da "cannot index parallel arrays" ile reddediliyor (doğrulanmalı)**

DURUM: KAPANDI — `a292033fb`, hat `2ca3d05ef` (2026-10-03; hata GERÇEKTİ: ekrandan koltuk bitirme ve iki tarihli koltuk oluşturma 500 veriyordu) · SAHİP: CT (MOD-0288 organizasyon) · BULAN: WP-WCN-TASK-REQUEST-01 D1 FIX1 testi (2026-10-03) · KAYIT: 2026-10-03

Görev talebi testinde koltuğu sona eren bir alıcı kurulmak istendi; koltuğu tarihle bitirmek için yapılan ikinci tarih yazımı Mongo'da
"cannot index parallel arrays" hatası verdi (DateTimeOffset'in `[ticks, ofset]` dizisi olarak saklanması + aynı belgede dizinli ikinci dizi —
BL-030 ailesi). Testte koltuk silinerek geçildi. Organizasyon deposunda gerçek bir hata olabilir: canlıda bir koltuğun bitiş tarihini yazmak
düşüyor olabilir. İş: depoda ve şemadaki dizinlerde ölç; gerçekse koltuk bitişi için hangi alanın dizinli dizi oluşturduğunu bul, düzelt,
gerçek Mongo testi. Gelecek regresyon riski: 🟡 (dizin değişikliği gerekebilir — yeni adla).

**Kapanış (2026-10-03):** kök neden `position_assignments` üzerindeki iki bileşik dizin (`…_position_interval`, `…_user_interval`) aynı
belgede iki dizi alanını (`EffectiveFrom`, `EffectiveTo`) birlikte içeriyordu. Yeni dizinler `EffectiveTo` olmadan, yeni adla (`…_position_from`,
`…_user_from`); eski iki ad servis açılışında şema bildiriminden önce düşürülüyor (mevcut emekli dizin deseni) — canlıda elle iş gerekmez;
doğrulama: `getIndexes`'te `*_interval` yok, `*_from` var, ekrandan bir koltuk bitirilir. Koruma testi: şema bildirimindeki hiçbir dizin dizi
olarak saklanan iki alan taşıyamaz (bugün yalnız bu ikisini yakaladı). Açık kalan (BL-030 ailesi): `HasOverlapAsync` `Lt`/`Gt`'yi dizi olarak
saklanan tarihlerde yapıyor — çakışma kontrolünün doğruluğu ayrı ölçülmeli.

---

### BL-540

**Yetim onay / inceleme / yukarı yön talebi örnekleri: düzeltmelerden önce oluşanlar hiç taranmıyor; yukarı yön talebi bağı hiçbir yolda geri çekilmiyor**

DURUM: AÇIK · SAHİP: CT (MOD-0023 · MOD-0024) · BULAN: WP-WF-ENGINE-CANDIDATES-01 D2-FIX3 bağımsız gözden geçirmesi · KAYIT: 2026-10-04

D2-FIX2 / FIX3 / FIX4 (dal `feature/platform/wf-engine-candidates`) görevi kapatan / silen / devreden yolların bağlı onay ve incelemeyi önce geri çekmesini sağladı. İki açık kalıyor:
(1) bu düzeltmelerden ÖNCE silinmiş görevlerin ve devirlerde üzerine yazılmış 1. tur incelemelerin örnekleri canlı ve karar verilebilir duruyor (yükseltiliyor, kimse okumuyor): bir kerelik
salt okunur sayım + kontrollü süpürme (motorun kendi iptal komutuyla, kayıtlı); (2) yukarı yön talebinin bağı (`RequestWorkflowInstanceId`, BL-023, `CreateTaskItemHandler.cs:393-397`) için kapı ve geri
çekme yok: görev iptal / silme / devirde yöneticinin "task-request" kararı açık kalıyor. BL-513 (yukarı yön talebinin görev talebi düzenine taşınması) ile birlikte çözülmesi doğal.
Gelecek regresyon riski: 🟢 (süpürme kuru koşuyla).

---

### BL-539

**Onay motoru tamamlanmamış güvenilir başlatmayı kendisi bitirmiyor ya da temizlemiyor: tüketici başlatmayı terk ederse motorda karar verilemeyen bir görev kalıyor**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: WP-MDM-GSKU-ACCEPT-01 FIX3 ölçümü (MDM sohbeti) · KAYIT: 2026-10-04

MDM bir gönderimi motorun "tamamlanmamış başlatma" (WORKFLOW_START_NOT_COMPLETED) cevabında park edip taslak düzenlenince terk edebiliyor; motor tamamlanmamış başlatmayı
kendiliğinden bitirmiyor, temizlemiyor da: Görev Merkezi'nde karar verilemeyen bir görev kalıyor. Düzeltme: motor tarafında süresi dolan tamamlanmamış başlatmaları kapatan
(ya da tüketicinin iptal edebildiği) bir yol; tüketiciler terk etmeden önce iptal ister. Gelecek regresyon riski: 🟢.

---

### BL-538

**Görev Merkezi'nden "aç" derin bağlantısı yalnız görevleri çözüyor: ABB (ve diğer sağlayıcıların) öğesi için `GET work-items/{id}` 404**

DURUM: AÇIK · SAHİP: CT (MOD-0024 Görev Merkezi) · BULAN: WP-MDM-ABB-ACCEPT-01 FIX2 DUR'u · KAYIT: 2026-10-04

`WorkItemsController.GetById` → `GetTaskWorkItemByIdQuery`: yalnız Tasks sağlayıcısı. MDM'nin ABB onay öğesi (ve HttpWorkItemProvider üzerinden gelen diğer
sağlayıcılar) için derin bağlantı 404; ABB ekranındaki düğme bugün kök sayfayı açıyor. Düzeltme: kimliğin önekinden sağlayıcıyı seçen tek çözümleyici
(sağlayıcı sözleşmesine "tek öğe oku" eklenir). Aynı turda ABB emeklilik talebini geri çekme (yeni `ProductAuditOperation` + Platform
`TrustedSourceAuditIntentOperationMap` satırı) ayrı iş olarak bekliyor. Gelecek regresyon riski: 🟢.

---

### BL-537

**MDM, CRM, DevEnablement ve Procurement'ın iç anahtarlı / kimlik başlıklı istemcileri yönlendirme izliyor**

DURUM: AÇIK · SAHİP: CT (altyapı) · BULAN: WP-EMAIL-SHELL-01 FIX2 bağımsız gözden geçirmesi · KAYIT: 2026-10-04

Bağlayıcı kural: kimlik bilgisi başka hedefe / yönlendirme üzerinden taşınmaz. Auth'un 7 istemcisi (FIX1–FIX2) ve Platform / Web istemcileri (FIX3) kapatılıyor.
Kalanlar: MDM `PlatformAuditForwarder.cs:92`, `ModuleRegistrationHostedService.cs:108-111` (kimlik sırrı başlığı); CRM `AuthUserDisplayNameClient`; DevEnablement ve
Procurement modül kayıt servisleri. Düzeltme: her serviste tek yardımcı (`AllowAutoRedirect = false`) + üretim kaydından test. MDM, devralma dalı kabul edilince.
Gelecek regresyon riski: 🟢.

---

### BL-536

**Üretim temel yapılandırmasında arka plan işleri tamamen kapalı ve olay taşıyıcı bellek içi: e-posta yeniden denemesi, yinelenen görevler, hatırlatmalar, yükseltme, toplantı serisi ve zaman çizelgesi işleri hiç koşmuyor**

DURUM: AÇIK — SAHİP KONTROLÜ GEREKİYOR (canlı ortam değişkenleri) · SAHİP: CT (altyapı) · BULAN: WP-EMAIL-SHELL-01 FIX2 ölçümü + bağımsız gözden geçirme · KAYIT: 2026-10-04

`Platform.API/appsettings.json:54` `BackgroundJobs.Enabled: false`, `:62` `EnabledJobs: {}`; `Infrastructure/DependencyInjection.cs:795-798` Hangfire'ı hiç kurmuyor;
ayrıca her iş `EnabledJobs[id] == true` istiyor (yalnız `Enabled`'ı açmak yetmez). Depoda `appsettings.Production.json`, docker-compose, helm ya da `BackgroundJobs__`
geçersiz kılması yok. Koşmayan işler: e-posta tarama (MOD-0027), iş akışı yükseltme (MOD-0023), görev yineleme ve yaklaşan bitiş (MOD-0024), toplantı serisi (MOD-0357),
zaman çizelgesi karar taraması / gece yarısı kapatma / hatırlatma (MOD-0280), tatil çekme; yer tutucu abonelik / kota işleri. Hosted service'ler (denetim outbox işçisi,
olay yayıncısı) koşuyor, ama temel yapılandırmada `Eventing` bölümü yok → taşıyıcı `InMemory`: olaylar servisler arasında geçmiyor (kiracı açılışının Auth'a gitmesi
gibi). Canlı ortam değişkenleriyle açılmış olabilir: SAHİP canlıda `BackgroundJobs__Enabled`, `BackgroundJobs__EnabledJobs__*` ve `Eventing__Transport` değerlerini
kontrol etmeli (1 Ekim'deki yinelenen kayıt görevinin "son üretim" sorusu buna bağlı). Düzeltme: canlı için açık bir `EnabledJobs` kümesi ve RabbitMQ yapılandırması
(sahip kararı + dağıtım), açılış günlüğüne "kapalı işler" satırı. Gelecek regresyon riski: 🟡 (işleri açmak birikmiş işleri birden çalıştırır: önce kuru koşu).

---

### BL-535

**Platform'un üretim başlangıcını (Program.cs ara katman sırası) koşturan bir test altyapısı yok: `WebApplicationFactory<Program>` bu projede çalışmıyor**

DURUM: AÇIK · SAHİP: CT (test altyapısı) · BULAN: BL-512 FIX1 DUR'u · KAYIT: 2026-10-04

`PlatformContainerValidationTests` özetinde ölçülmüş üç neden: minimal hosting'de fabrikanın yapılandırması `Build()` sırasında uygulanıyor ama
`AddInfrastructure` yapılandırmayı daha önce okuyor; `AddInfrastructure` süreç geneli BSON serileştirici kaydı yapıyor (Application.Tests
`[ModuleInitializer]`'da zaten yaptığı için ikinci kayıt atıyor); göçler ve tohumlar satır içi, `AllowStartupWithoutDatabase` dışında koşuyor.
Sonuç: ara katman sırası (kimlik doğrulama → kiracı → yetki → hız sınırı) yalnız elle kurulan test hatlarıyla ölçülüyor; BL-512 FIX1 dörtlüyü tek
uzantıya taşıdı (`UsePlatformAccessPipeline`), uzantı çağrısının Program.cs'ten silinmesi yine metin denetimiyle yakalanıyor. Düzeltme: başlangıcı
yapılandırılabilir yapan (serileştirici kaydı idempotent, tohumlar bayrakla) ve WAF'ı çalıştıran tek bir test düzeneği. Gelecek regresyon riski: 🟢.

---

### BL-534

**`admin@diten.com` tohumu her ortamda (canlı dahil) her açılışta çalışıyor: kaynak kodda sabit parola özeti, kalıcı düşürülemez, e-postası değişirse Platform açılmıyor**

DURUM: AÇIK — SAHİP KARARI BEKLİYOR · SAHİP: CT (Auth / Platform) · BULAN: BL-521 FIX2 ölçümü (2026-10-04) · KAYIT: 2026-10-04

Platform `PlatformAdministratorSeed` (`DependencyInjection.cs:549` ortam koşulsuz) her açılışta bu kaydı PlatformAdmin + Active + yalnız SuperAdmin +
silinmemiş yapıyor: hesap kalıcı olarak düşürülemiyor / askıya alınamıyor. Auth `DataSeeder.SeedUsersAsync` her açılışta: kullanıcı yoksa ya da aynı
e-postayla başka Id'li bir kullanıcı varsa onu ve rollerini fiziksel olarak SİLİP kaynak koddaki sabit bir parola özetiyle yeniden yaratıyor
(`DataSeeder.cs:1055-1062`); Id'si sabit kullanıcı varsa parolasına dokunmuyor. Kayıt e-postası değiştirilirse sonraki açılışta aynı sabit Id ile ekleme
DuplicateKey alıyor ve Platform açılmıyor (ölçüldü). Risk: canlıda bu hesap silinir / yeniden yaratılırsa kaynak koda erişen herkesin bildiği parolayla
SuperAdmin doğar. Öneri: tohum yalnız Development'ta; canlı ilk kurulum tek seferlik bir komutla rastgele parola + zorunlu değişim; e-posta değişikliği
bu kayıtta yasak; tohum açılışı asla çökertmez. Karşılaştırma: SAP ve Oracle'da ilk yönetici kurulum sırasında, rastgele / kurulumcunun verdiği parolayla
bir kez yaratılır, açılışta yeniden yazılmaz. Gelecek regresyon riski: 🟡 (canlı ilk kurulum yolu).

---

### BL-579

**MDM'in kalıtsal iki etkileşimli referans istemcisi (doğrulanmış GSKU ve pazar çözümleyicileri) yönlendirme izliyor ve kimlik bilgisi başlıklarını günlükte maskelemiyor**

DURUM: AÇIK · SAHİP: CT (MDM / altyapı) · BULAN: WP-PLT-ITEM-LISTS-01 güvenlik incelemesi (R1, önceden var) · KAYIT: 2026-10-08

Yer: MDM `Infrastructure/DependencyInjection.cs:37` ve `:48` — `AddHttpClient<…>()` işleyici ayarı olmadan. Aynı dosyadaki diğer kimlik bilgisi taşıyan istemciler `AllowAutoRedirect = false` + `RedactLoggedHeaders([...])` kullanıyor (`TrustedLegalEntityScopeProviderDependencyInjectionTests.cs:49-62` sabitliyor). Kalem listeleri istemcisi FIX1'de düzeltiliyor; bu ikisi kalıyor.

Risk: Platform tabanı ya da önündeki giriş 307 / 308 dönerse özel `X-Verified-Gsku-Credential` sır başlığı ve gövde hedef sunucuya yeniden gönderilir; gelen cevap Platform'unmuş gibi işlenir. Trace günlüğünde başlık değerleri (JWT dahil) yazılır.

Yapılacak: iki istemciye aynı işleyici ayarı + maskeleme; kaynak-grep kural testi bütün `AddHttpClient` kayıtlarını kapsar; 307 davranış testi.

Karşılaştırma: SAP RFC / HTTP hedeflerinde yönlendirme izleme hedef bazında açıkça yapılandırılır, varsayılan kapalıdır; Oracle Integration bağlantıları da kimlik bilgisini yalnız tanımlı uç noktaya gönderir.

Gelecek regresyon riski: 🟡 (ağ yapılandırması hatasında servis sırrı başka sunucuya gidebilir).

---

### BL-580

**Canlı kullanıcılar ekranları kullanmayı bilmiyor: üç iş koluna göre canlı rol önerisi + gerçekçi örnek veri seti (eğitim / tanıtım)**

DURUM: AÇIK · SAHİP: CT (ürün) · BULAN: sahip (canlıdan talep, 2026-10-09) · KAYIT: 2026-10-09

Sahibin iki fikri var (2026-10-09). Şirket üç iş kolunu birlikte yürütüyor: kendi üretimi, başkası için fason üretim, satış / dağıtım.

1. **Canlı rol önerisi.** Modüller koddan taranır: hangi rol gerekir, hangi izni alır, kim hangi iki izni birlikte tutamaz (GxP görev ayrımı, hazırlayan ≠ onaylayan).
   - CT analizi başlattı (2026-10-09, alt ajan); çıktı sahibe tablo olarak gider.
   - Canlıya rol girişi sahibin kayıt başına "evet"iyle olur (canlı yazma kuralı).
   - Eksik izin anahtarı ya da adı olmayan izin bulunursa ayrı BL.
2. **Örnek veri seti.** Diten Pharma senaryosu (`docs/records/tests/diten-pharma-scenario.md`) gerçekçi veriyle her modüle girilir: kişiler, pozisyonlar, roller, görevler, haftalar, ürünler; her rol için bir örnek kişi ve yaptığı iş.
   - Yeri: ayrı bir tanıtım / eğitim kiracısı. Canlı kiracının gerçek verisine ve DefaultTenant'a yazılmaz.
   - Tohumlayıcı tanıtım kiracısı dışında çalışmayı REDDEDER (test edilir).
   - Kullanım: yeni kullanıcı eğitimi, ekran tanıtımı, BL-495 yardım panelindeki örnekler.

Karşılaştırma:
- SAP: Best Practices paketleri hazır örnek veri ve iş rolü şablonlarıyla (SAP_BR_*) gelir.
- Oracle: Fusion hazır iş / görev rolleri ve tanıtım ortamı sunar.
- (Şablon adları hafızadan, doğrulanmadı.)

Gelecek regresyon riski: 🟢 ayrı kiracıda. 🔴 tohumlayıcı yanlış kiracıya yazarsa; koruması bu işin kabul ölçütüdür.

---

### BL-581

**Varsayılan Viewer rolü, açılan modülün bütün "read" izinlerini alıyor: İK'nın ücret / yan hak ve hassas erişim ekranları da dahil**

DURUM: AÇIK · SAHİP: CT (Auth yetki) · BULAN: canlı rol analizi (2026-10-09, alt ajan; CT kodu okuyarak doğruladı) · KAYIT: 2026-10-09

Kural (`origin/main`):
- `EntitlementPermissionSyncService.SelectForRole`, Viewer'a modülün eylemi tam `read` olan her iznini veriyor (`:367-375`).
- `DefaultRolePermissionTemplate` Viewer süzgeci yalnız kiracı ayarları ve ürün okumalarını dışarıda bırakıyor (`:73-92, 127-132`).

Sonuç:
- İnsan Sermayesi modülü açık bir kiracıda Viewer rolündeki herkes `hcm.compensation-benefits.read` (ücret / yan hak) ve `hcm.sensitive-access.read` ekranlarını açabilir.
- Yetenek Ekosistemi açıksa `tep.salary-benchmarking.read` için de aynısı geçerli.
- `platform.meetings.read-all` "yalnız elle" listesinde değil, `platform.tasks.read-all` ise listede. Admin, toplantıların hepsini modül açılınca otomatik alıyor.

Düzeltme:
- Hassas okuma anahtarları `ExplicitGrantOnlyPermissions`'a girer ya da Viewer süzgecinde açıkça dışlanır.
- Var olan Viewer atamalarının geri alınması ayrı karar: geri alma denetim satırıyla yapılır.

Sahibe soru: canlıda İnsan Sermayesi modülü açık mı, Viewer rolü kimlerde?

Karşılaştırma:
- SAP: HR bilgi tipleri ayrı yetki nesnesiyle korunur (P_ORGIN); genel görüntüleme rolüne girmez.
- Oracle: hassas kişi verisi ayrı veri güvenlik politikasıyla korunur.

Gelecek regresyon riski: 🔴 (kişisel ve ücret verisi geniş bir role açık).

---

### BL-582

**Canlı rol analizinin bulduğu rol kurma boşlukları: QA / onaycı rolü kiracı ekranından kurulamıyor, bazı izinler katalogda yok**

DURUM: AÇIK · SAHİP: CT (Auth yetki + ilgili modül sahipleri, XMC) · BULAN: canlı rol analizi (2026-10-09) · KAYIT: 2026-10-09

Kayıt: `docs/records/analysis/roles/2026-10-09-canli-rol-onerisi.md` §8 (her biri dosya:satır kanıtlı). Özet:

- **G1:** Doküman Yönetimi'nin 124 izninden 123'ü platform kapsamlı; kiracı yöneticisi SOP okumayı bile bir role veremez. QA rolü kurulamaz.
- **G2:** İş akışı başlat / onayla / reddet platform kapsamlı. Takeover dalı düzeltiyor (`SeedOwnedTenantScopeKeys.cs:58-65`); tek PR'la gelir.
- **G3:** Viewer yalnız eylemi `read` olanı alıyor; `view` / `search` kullanan modüller okuyucuya hiçbir şey vermiyor.
- **G5:** Modülle Admin'e gelen izin kaldırılamıyor. Admin'i 1–2 BT kişisiyle sınırlama önerisi.
- **G8 / G9:** Satın alma ve denetim okuma izinleri kodda zorunlu ama katalogda yok; kimseye verilemez.
- **G10:** Kiracının denetim izi ekranı yok.
- **G13:** `platform.workflow.instances.*` ekranda "Doküman Örnekleri" diye görünüyor.
- **G16:** CAPA kapatma onayı için ayrı izin yok.
- **G18:** GQD / QADocumentation rolleri yalnız test kiracısında ve izinsiz.

Her madde ayrı WP olarak sınıflanır; başka modülün dosyası gerekiyorsa XMC-001 ile sahibe sorulur.

Gelecek regresyon riski: 🟡 (GxP görev ayrımı canlıda rol ile kurulamıyor).

---

### BL-583

**Ürün ana verisi okuma sözleşmesi servis kimliğiyle okunamıyor: Auth yalnız MDM'ye ve yalnız Platform hedef kitlelerine servis belirteci veriyor**

DURUM: AÇIK · SAHİP: CT (Auth + MDM) · BULAN: FU04 S4 ölçümü (2026-10-09) · KAYIT: 2026-10-09

`/api/product-master` (S4) bugün yalnız kullanıcı belirteciyle (`mdm.product-master.read`) okunuyor. Stok servisi çağıranın belirtecini iletiyor (istek belgesi R-01), bu yüzden MVP-1 için yeterli. Ancak:
- arka plan işleri (gece uzlaştırması, Satın Alma'nın otomatik GRN denetimi, BOM doğrulaması) kullanıcısız okur;
- Auth'un servis belirteci yolu yalnız `Diten.MDM` istemcisine ve yalnız Platform hedef kitlelerine açık.

Yapılacak:
- tüketici servis istemcisi kaydı;
- `mdm` hedef kitlesi;
- salt okuma kapsamı;
- kiracı izni (servis istemcisi kayıt deseni).
Yeni güven yolu olduğu için güvenlik incelemesiyle.

Karşılaştırma:
- SAP: sistemler arası okuma teknik kullanıcı + yetki rolüyle yapılır.
- Oracle: entegrasyon kullanıcısı + veri erişim kümesiyle yapılır.

Gelecek regresyon riski: 🟡 (ilk kullanıcısız tüketici geldiğinde gerekecek).

---

### BL-577

**CI geçidi (phase1-gates, ubuntu-latest) hiçbir servis testini koşmuyor: Platform, Auth ve MDM testleri yalnız yerelde koşuluyor; Linux'a özgü kod (BL-570) hiç ölçülmüyor**

DURUM: AÇIK · SAHİP: CT (altyapı / CI) · BULAN: BL-570 FIX1 ölçümü + CT · KAYIT: 2026-10-08 · TEK PR'DAN ÖNCE KARAR

`scripts/run_phase1_gates.sh` bugün yalnız şunları koşuyor:
- `TenantArchitecture.TenancyTests`
- `TenantArchitecture.ArchitectureTests`
- `Diten.Web.Tests`

Auth çözümü yalnız derleniyor (`:119`). Platform Application (~7000 test), Auth Application (~1680), MDM Application / Api (~2200 / ~750) CI'da hiç koşmuyor. Bu yüzden BL-570'in Unix sır kanalı ve kilidi Linux'ta hiç ölçülmüyor. Koşucu ubuntu 24.04, glibc 2.39.

Yapılacak:
- Servis test adımları: Mongo kullanan testler kendi geçici mongod'larını açıyor. Koşucuya mongod ikilisi kurulur (MongoDB topluluk tarball'ı, sürüm sabit) ve `DITEN_TEST_MONGOD` verilir.
- Her adımda "atlanan = 0" iddiası. Sessiz atlama yok; `[UnixFact]` / `[OwnedMongoFact]` Linux'ta koşar.
- Süre bütçesi: Platform tam koşu ~13 dk. Ayrı iş (job) ya da paralel matris.
- İlk koşuda Linux'a özgü kırmızılar beklenir (BL-570). Ayrı ayrı sınıflanır.

Karşılaştırma: SAP'de CI/CD (Cloud ALM / CTS+) aktarım öncesi birim ve entegrasyon testlerini zorunlu koşar. Oracle'ın sürüm boru hattı da her yamada tam regresyon paketini çalıştırır.

Gelecek regresyon riski: 🔴 (servis testleri yalnız geliştiricinin makinesinde; Linux davranışı hiç ölçülmüyor).

---

### BL-578

**Auth işletim komutlarının tek örnek kilidi makine yerel (`Global\` adlı mutex, `/tmp/.dotnet`); sunucular arası dışlama ve kilit dosyası bütünlüğü yok**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: BL-570 FIX1 R4 ölçümü · KAYIT: 2026-10-08

BL-570 FIX1'de CT kararı A: `Global\` önekli adlı mutex. macOS'ta ölçüldü: oturumlar arası dışlıyor. Kullanıcılar arası ölçülmedi.

Kalan maruziyetler:
- `/tmp/.dotnet/shm/global` ve `lockfiles/global` 0777 ve sticky bit yok. Yerel bir kullanıcı kilit dosyasını silerek dışlamayı kırabilir. Bu öneksiz adda da aynıydı.
- Yerel bir kullanıcı kilidi tutarsa komut `OPERATION_ALREADY_RUNNING` ile çalışmaz. Kapalı başarısızlık, yalnız erişilebilirlik.
- İki ayrı sunucuda aynı anda çalıştırma dışlanmaz.

Yapılacak: Mongo'da kira kaydı (sahip, süre, saat; ortak Auth veritabanı), sunucular arası. Kira süresi dolunca devralma adlı olur.

Karşılaştırma: SAP'de arka plan işleri merkezi kuyruk sunucusunda (enqueue server) kilitlenir. Oracle ESS'te de çakışma denetimi veritabanındaki istek tablosundadır. İkisi de işletim sistemi yerel nesnesine bağlı değildir.

Gelecek regresyon riski: 🟡 (çok sunuculu kurulumda iki operatör aynı anda uzlaştırma çalıştırabilir).

---

### BL-576

**MDM ürün varlıklarında (GSKU / LSKU / Bitmiş Ürün …) IgnoreExtraElements sınıf haritası yok: yeni bir alan yazıldıktan sonra eski ikiliye geri dönülürse o kayıtların okunması fırlar**

DURUM: AÇIK · SAHİP: CT (MDM devralma) · BULAN: FU04 S3 ölçümü · KAYIT: 2026-10-07 · TEK PR'DAN ÖNCE, CANLIDA İLK STOK YAZIMINDAN ÖNCE KAPANMALI

Yalnız Marka / Ürün ve üç GP yaşam döngüsü işleminin `IgnoreExtraElements` haritası var (`GlobalProductLifecycleClassMaps`). S3 ilk stok alanını yazdıktan sonra S3 öncesi ikili o GSKU / LSKU / FG'yi okurken "Element 'BaseUomCode' does not match" ile fırlar. Yani geri alma o kayıtların okunmasını bozar.

S3 haritayı kendisi eklemedi, çünkü:
- aynı süreçte daha önce yapılan bir okuma haritayı kendiliğinden kaydederse açılıştaki `Verify` deseni düşer (BL-569 ile aynı kök);
- haritanın eklenmesi kimlik varlıklarının eşlemesini değiştirir, S3'ün kapsamı değil.

Yapılacak:
- GP desenindeki harita, MDM'in bütün ürün kimliği varlıkları için (GSKU, LSKU, FG, ürün tanımı revizyonu, iş akışı işlemleri; ölçülür).
- Test sırası koruması (modül başlatıcı).
- Ek alanlı belgeyle eski ikili simülasyonu testi.

Bağlam: ürün modülleri henüz canlıda değil, bugün "eski ikili" canlıda koşmuyor. Risk, gelecek ek alanlar ve geri almalar için yapısal.

Karşılaştırma: SAP'de tablo genişletmeleri (append structure) eski programların okumasını bozmaz. Oracle'da da esnek alanlar (DFF) eski sürüm okumasında yok sayılır. İkisinde de ileri uyumluluk şemanın özelliğidir.

Gelecek regresyon riski: 🔴 geri almada (kayıt okunamaz); kapanana kadar.

---

### BL-575

**Denetim defteri: İ5 istisnasındaki komuta iz dışı bir yazım eklenirse mimari kural yakalamıyor; merkezi eşleme bekleme kesimi (≥ 79) sürüm çıkınca ileri alınmazsa her yeni işlem bir gün bekler**

DURUM: AÇIK · SAHİP: CT (mimari kural / MDM devralma) · BULAN: WP-MDM-SETTLE-CAP-01 raporu (SC-3e ölçüm sınırı + madde 2 notu) · KAYIT: 2026-10-07

(1) **İ5 ölçüm sınırı.** `AuditTrailStandardTests`, İ5 ("hiçbir kalıcı kaydı değiştirmez") komutunun gerçekten yazmadığını, yalnız bildirilmiş iz üyeleri üzerinden ölçüyor. İz dışı bir depo yazımı eklenirse istisna geçerli görünmeye devam eder. Bugün etkilenen: `RetireGskuIdentityPairCommand`, `RetireLskuIdentityCommand` (her ikisi yalnız 403).
Öneri: İ5 komutunun işleyicisi hiçbir depo yazma üyesini (`*Async` yazma adları, `IMongoCollection` yazımları) çağırmaz; kaynak taramasıyla ölçülür.

(2) **Kesimin ileri alınması.** `CentralMappingRollout.FirstOperationOfThisRelease = 79`: 79 ve sonrası Platform haritası gelene kadar ~1 gün bekler. Sürüm canlıya çıkıp Platform haritası dağıtıldıktan sonra kesim ileri alınmazsa, sonraki her yeni işlem (Platform'un bilerek eşlemediği dahil) gerçek bir 409 yerine bir gün yeniden dener.
Öneri: sürüm notu adımı ("Platform haritası canlıda → kesimi bir sonraki ilk işleme al") ve kesimin Platform'un dağıtılmış harita sürümünden türetilmesi.

Karşılaştırma: SAP'de değişiklik belgesi nesnesi tanımı ile uygulama aynı aktarımda taşınır, sürüm boşluğu bekletilmez. Oracle'da da denetim politikası değişikliği yamayla birlikte uygulanır.

Gelecek regresyon riski: (1) 🟡 (istisnalı komuta ileride yazım eklenirse denetimsiz kalır); (2) 🟢 (yalnız gecikme, kayıp yok).

---

### BL-574

**MDM yol b denetim kayıtları değişen alanları, önce / sonra değerlerini ve ret nedenini taşımıyor (AUD-001 §3 madde 10); Platform ucu bunları zaten alıyor, eksik MDM'in iletim isteğinde**

DURUM: AÇIK · SAHİP: CT (MDM devralma) · BULAN: FU04 S1 bağımsız incelemesi (R3) · KAYIT: 2026-10-07

`AuditForwardRequest` (`IPlatformAuditForwarder.cs:16`) yalnız şunları taşıyor: Operation, EntityType, EntityId, Outcome, RequestType, Category, SourceModule, CorrelationId. Platform'un `api/internal/audit/append` ucu (`AuditAppendRequest`) ise `BeforeState`, `AfterState` ve `Metadata`'yı da kabul ediyor.

Sonuç: MDM'in bütün yol b komutlarında (Marka, ürün komutları, kısaltmalar) kayıt "neyin değiştiğini" ve "neden reddedildiğini" söylemiyor. GMP açısından ana veri değişikliğinin önce / sonra değeri beklenir (Annex 11 / 21 CFR Part 11 denetim izi).

FU04 S1-FIX1 mekanizmayı ekliyor: isteğe bağlı alanlar, geriye uyumlu. Kalem komutları bunları dolduruyor.

Yapılacak:
- Diğer yol b komutları (Marka, GP / GSKU / LSKU / FG yol b komutları, kısaltma, S3 stok davranışı) aynı alanları doldurur.
- Bir mimari kural, "değişiklik yapan komut değişen alanları taşır" kuralını ölçer.

Karşılaştırma: SAP'de malzeme ana verisi değişiklik belgeleri (CDHDR / CDPOS) alan bazında eski ve yeni değeri tutar. Oracle'da da öğe denetimi (Audit Trail) öznitelik bazında önce / sonra kaydeder.

Gelecek regresyon riski: 🔴 GMP kullanımında (değişiklik izi eksik); kalem canlıya çıkmadan önce kalem için kapanıyor, diğerleri için açık.

**Ek (CT 2026-10-07, S1-FIX1):** neden alanı bugün serbest metin; denetim kaydına yazılmaz (AUD-001 §3, kişisel veri riski), kayıtta yalnız `reasonProvided`. **S5:** kullanım dışı bırakma / emekliye ayırma için kodlu neden listesi (MOD-0048 ya da kalem modülü listesi); gelince `Metadata.reasonCode` liste kodu olarak döner.

---

### BL-573

**Ürün stok davranışı izinleri (O-12) Auth'ta yok: üç `edit-stock-behaviour` anahtarı ürün yaşam döngüsü profilinin tam küme denetimini fırlatıyor; S3 geçici olarak kimlik anahtarlarını kullanıyor**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: FU04 S3 DUR ölçümü · KAYIT: 2026-10-07 · S5'TEN ÖNCE KAPANMALI

`ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet`, `mdm.gskus.*`, `mdm.lskus.*` ve `mdm.finished-goods.*` ile başlayan her anahtarı yaşam döngüsü adayı sayıyor. Bu küme `PermissionKeys` ile birebir eşit olmalı ("exact eight base, eighteen lifecycle and one non-human recovery key"). Manifeste üç yeni anahtar girerse ürün modülünün Auth uzlaştırması tümüyle fırlar.

CT kararı (S3, b): bu dilimde geçici olarak `mdm.gskus.update`, `mdm.lskus.create` ve `mdm.finished-goods.create` kullanılıyor (FG taslak iptali emsali). Komut ve izin sabiti ayrı; yalnız değer geçici.

Yapılacak:
- Üç anahtar profilin `PermissionKeys`'ine ve ProductDataSteward rolüne eklenir; hata metnindeki sayı güncellenir.
- S3 sabitlerinin değeri kendi anahtarına çevrilir.
- FG taslak iptali için de kendi anahtarı değerlendirilir.
- Plan eşitlemesi ve rol matrisi testleri.

Karşılaştırma: SAP'de malzeme ana verisinin görünümleri (temel veri, depo, MRP) ayrı yetki nesneleriyle (M_MATE_STA) korunur. Oracle'da da kalem öznitelik grupları ayrı rol ayrıcalığıyla düzenlenir. İkisinde de stok görünümü kimlik düzenleyiciden ayrı yetkidir.

Gelecek regresyon riski: 🟡 (geçici dönemde stok davranışını kimlik düzenleyicisi düzenler; ayrı veri sorumlusu rolü yok).

**Ek (CT 2026-10-08, BL-573 Auth turu DUR):** tam küme kuralı her tek taraflı sırayı kırıyor: Auth önce gelirse manifest 18 bildirir, Auth 21 bekler; manifest önce gelirse tersi. CT kararı A: Auth üç anahtarı "ya hepsi ya hiçbiri" isteğe bağlı küme olarak kabul eder (geçerli: 18 ya da 18 + tam bu 3; kısmi ya da yabancı anahtar ret); Steward'a verme yalnız katalog üçünü taşıyorsa. **İkinci yarı (açık):** S5 manifesti her kiracıda canlıya çıktıktan sonra üçlü zorunlu yapılır (tek küme 21). Karşılaştırma: SAP'de yetki nesnesi profil üretecinde bildirilmeden role girmez; Oracle'da da bildirilmemiş fonksiyona yetki verilemez.

---

### BL-571

**Onay motoru kurtarma: kiracı başına tarama sınırı ilerlemiyor; 5000'den fazla yaşlı bekleyen onayı olan kiracıda sınırın dışında kalan yarım başlangıç hiç sayılmıyor, hiç kapatılmıyor**

DURUM: AÇIK · SAHİP: CT (Platform / onay motoru) · BULAN: WP-WF-ENGINE-RECOVERY-01 FIX2 bağımsız incelemesi (gerekli 3) · KAYIT: 2026-10-07

`WorkflowStartRecoveryStore.cs` `$limit`'i görevsiz süzgecinden önce uyguluyor. FIX3 dizinden `$sort` (en eski önce), kapatma kipinde Warning ("süpürme bir alt kümeyi görüyor") ve runbook cümlesi ekliyor.

Kalıcı çözüm: kiracı başına ilerleyen imleç. Her döngü son baktığı noktadan devam eder; tur bitince başa döner. Böylece her yarım başlangıç sonlu sürede görülür.

Karşılaştırma: SAP'de arka plan iş yönetimi büyük kümeleri paketlere bölüp kaldığı paketten devam eder. Oracle ESS de toplu işleri "chunk" ve devam noktasıyla koşar.

Gelecek regresyon riski: 🟡 (yalnız çok büyük kiracıda; sayaç "≥ N" ve Warning ile görünür).

**Ek (CT 2026-10-07, WF FIX3 madde 3 DUR):** dizinden `$sort` bugün yapılamıyor. `LastTransitionAt` bir `[ticks, offset]` dizisi; K3 dizini çok anahtarlı (isMultiKey), plan bellek içi SORT yapıyor ve artan sıralama en küçük öğeyi (offset) kullanıyor. İşçi ölçtü: `(TenantId, Status, LastTransitionAt.0)` konumsal dizini çok anahtarlı değil; tick süzgeci + `LastTransitionAt.0` sıralamasıyla plan IXSCAN → FETCH → LIMIT, SORT yok, totalDocsExamined = 10 ("tick ya da null" `$or` biçimi de aynı plan). CT kararı: WF FIX3'te sırasız tarama + Warning + runbook; konumsal dizin + tick süzgeci bu BL'nin ilerleyen imleç işinin temeli olur (aynı iş).

---

### BL-572

**Onay motoru kurtarma: kapat-ve-yeniden-başlat (supersede) yalnız onay ve inceleme servislerinde; üst talep ve zaman çizelgesi onayı sabit anahtarla kilitli kalabilir**

DURUM: AÇIK · SAHİP: CT (Platform / onay motoru) · BULAN: WP-WF-ENGINE-RECOVERY-01 FIX2 bağımsız incelemesi (küçük 8) · KAYIT: 2026-10-07

`TaskUpwardRequestService.cs:119` sabit, görev başına bir anahtar kullanıyor (`task-request:{tenant}:{task}`). `TimesheetApprovalService.cs:164` için de aynı soru açık.

Başarısız bir başlangıçtan sonra atanan değişirse ya da süpürme yarım başlangıcı kapatırsa, sonraki her deneme 409 alır. FIX2'nin "tekrar yaşı tazelemez" değişikliği bu etkiyi büyütüyor. FIX3 açıkta kalan tüketicileri runbook'a yazıyor.

Yapılacak: bu tüketicilere de `SupersedeWorkflowHalfStartCommand` ile kapat-ve-yeniden-başlat; testli.

Karşılaştırma: SAP Business Workflow'da başlamış ama ilerlemeyen iş öğesi yönetici tarafından mantıksal olarak silinip yeniden başlatılır. Oracle BPM'de de "withdraw + resubmit" aynı kaydı yeni örnekle sürdürür.

Gelecek regresyon riski: 🟡 (atanan değişiminde kullanıcı talebi 409'da takılır).

---

### BL-570

**MDM devralma dalındaki Auth işletim kodu Windows'a bağlı: adlı semafor ve `kernel32` / `ntdll` boruları macOS ve Linux'ta çalışmıyor; 59 Auth testi bu yüzden Windows dışında kırmızı**

DURUM: AÇIK · SAHİP: CT (Auth / MDM devralma) · BULAN: CT, takeover ← fg-accept birleştirme doğrulaması (`314d8db2d`) · KAYIT: 2026-10-07 · TEK PR'DAN ÖNCE KAPANMALI

Yalnız `feature/mdm/product-five-takeover`'da var; main ve CT hattında yok (ölçüldü).
- `EntitlementReconciliationOperationalRunner.cs:33`: `new Semaphore(1, 1, "Diten.Auth.ProductIdentityEntitlementReconciliation")`. Adlı semafor Windows dışında `PlatformNotSupportedException` atar.
- `ServiceClientSecretOutputSink.cs:122-129`: `DllImport("ntdll.dll")` ve `DllImport("kernel32.dll")`.

Takeover'da Auth Application testi 1591/1650:
- 51 `ServiceIdentityTokens` (kernel32);
- 6 `ProductIdentityEntitlementReconciliationCommandContractTests` (adlı semafor);
- 2 `ProductAbbreviationPermissionOnboardingMongoTests` ("explicit owned-test Mongo URI" ister).

Birleştirmenin dokunduğu dosyalardan hiçbiri bu testlerde yok, kırmızı önceden var. Canlı sunucu Linux ise iki operatör komutu ilk çalıştırmada çöker. CI Linux koşucusunda da 57 test kızarır.

Yapılacak:
- Tek-örnek kilidi için taşınabilir yol: Mongo kira kaydı ya da dosya kilidi.
- Sır çıkışı için `PipeStream` / anonim boru gibi .NET'in çapraz platform API'si. Windows'a özgü sertleştirme gerekiyorsa `OperatingSystem.IsWindows()` arkasında, eşdeğer Unix yoluyla.
- Mongo testleri için sahipli test veritabanı fikstürü: mevcut `DITEN_TEST_MONGOD` deseni, ortak Mongo yok.
- macOS + Linux'ta 1650/1650.

Karşılaştırma: SAP'de arka plan iş kilidi uygulama sunucusundan bağımsız, veritabanında (ENQUEUE) tutulur. Oracle'da eşzamanlı yönetici kilidi de veritabanı tablosundadır. İkisi de işletim sistemi adlı nesnesine bağlı değildir.

Gelecek regresyon riski: 🔴 (Linux canlıda operatör komutları çöker; CI kırmızı).

---

### BL-567

**Ürün okumalarında kapsam koruması yalnız `InvalidOperationException`'ı eşliyor: okunamayan rollout kaydı (BSON eşleme / Mongo okuma hatası) ürün listesinde 500, yazımda ve tamlıkta 503**

DURUM: AÇIK · SAHİP: CT (MDM devralma, SCOPE-INT-01) · BULAN: WP-MDM-SCOPE-ACCEPT-01 FIX3 raporu + bağımsız inceleme · KAYIT: 2026-10-07

Kapsam işi FIX3'te yazım ve tamlık yollarına `ProductLegalEntityScopeWriteFenceCoordinator.IsRolloutUnreadable` korumasını koydu (IOE, Format, Bson*, Mongo* → kodlu 503). GP ürün okuması (`GetGlobalProductsHandler.cs:135`) hâlâ yalnız IOE eşliyor; okunamayan bir rollout kaydı ürün ekranında 500 verir. GP dosyası o sırada son kabul turundaydı, bu yüzden entegrasyon turuna (SCOPE-INT-01) kaldı. Yapılacak: aynı korumayı ürün okumalarına uygula (GP, GSKU, LSKU, FG okuma işleyicileri), bir test ile sabitle. Karşılaştırma: SAP'de yetki kontrolü (AUTHORITY-CHECK) okunamazsa kayıt gösterilmez, kodlu mesaj verilir; Oracle Fusion veri güvenliği de "kapsam okunamadı" durumunu ayrı hata olarak döner. Gelecek regresyon riski: 🟡 (Enforced kiracıda bozuk rollout kaydı ürün ekranını 500 yapar).

---

### BL-568

**Onay motoru: tamamlanmamış güvenilir rezervasyonun başlangıç görevi Görev Merkezi'nde görünüyor ama karar reddediliyor; ayrı operatör servis kimliği yok**

DURUM: AÇIK · SAHİP: CT (Platform / onay motoru) · BULAN: WP-WF-ENGINE-RECOVERY-01 raporu (kural 5 ölçümü) + FIX1 madde 1 DUR · KAYIT: 2026-10-07

(1) Bir güvenilir başlatmanın rezervasyonu tamamlanmadan kalırsa (MDM başlangıç cevabını almadan düşerse) başlangıç görevi adayın Görev Merkezi listesinde görünür, ama karar reddedilir (rezervasyon Completed değil). Süpürme 30 dk sonra kapatır (iş varsayılan KAPALI). Öneri: Görev Merkezi listesi tamamlanmamış rezervasyonun görevini göstermez ya da "hazırlanıyor" durumuyla, karar düğmesi kapalı gösterir. (2) Platform'un güvenilir servis şemaları tam 10 talep istiyor ve yalnız `Diten.MDM` servis adını kabul ediyor; operatör araçları için ayrı bir servis kimliği yok. WF FIX1'de yetim kurtarma bu yüzden HTTP ucu yerine tek atımlık komut oldu (CT kararı D). Kalıcı çözüm: Auth'ta ayrı bir operatör servis kimliği + hedef kitle (ör. `TRUSTED_PLATFORM_OPERATOR`), izin taşıyabilen belirteç modeli. Karşılaştırma: SAP'de arka plan işleri kendi teknik kullanıcısıyla (RFC / sistem kullanıcısı) koşar; Oracle'da entegrasyon kullanıcısına iş rolü verilir — ikisi de kimlik başına. Gelecek regresyon riski: 🟢 (1: kullanıcı karar veremeyeceği görevi görür, kafa karışıklığı; 2: operatör uçları açılınca gerekir).

---

### BL-569

**MDM test serileştirici sırası: Guid Standard serileştiricisinden önce bir yaşam döngüsü deposu kuran test, sınıf eşlemelerini erken dondurur (Api.Tests'te modül başlatıcı koruması yok)**

DURUM: AÇIK · SAHİP: CT (MDM test altyapısı) · BULAN: WP-MDM-GP-BL552 akış 3 FIX2 incelemesi (🟢 4) · KAYIT: 2026-10-07

GP FIX2 beş yaşam döngüsü deposuna statik `Register()` ekledi; kayıt kilitli ve tekrarlanabilir. Ama bir süreç Guid Standard serileştiricisini kaydetmeden önce bu depolardan birini kurarsa sürücü varsayılan Guid serileştiricisini önbelleğe alır (BL-280 şekli: boş okumalar); `Verify` statik kurucuda fırlarsa tür o süreç için kalıcı `TypeInitializationException` olur. Üretim ve bugünkü testler güvenli (Application.Tests `MdmTestSerializers` sırayı kuruyor). Öneri: Api.Tests'e de modül başlatıcı (Guid → Register) ve "önce depo kur, sonra Guid Standard mı" testi. Gelecek regresyon riski: 🟢 (yalnız yeni bir testin sırasına bağlı).

---

### BL-566

**Ürün denetim niyetleri: ölü mektuba düşen niyeti yeniden sürecek bir araç yok; "Platform bu işlemi henüz eşlemiyor" beklemesi yalnız GP'nin üç işlemini kapsıyor (LSKU 82–86 ve FG 87+ aynı dağıtım sırası boşluğuna düşer)**

DURUM: AÇIK · SAHİP: CT (MDM devralma) · BULAN: WP-MDM-GP-BL552 akış 3 FIX1 raporu (risk 🟡) + CT · KAYIT: 2026-10-07

GP akış 3 FIX1 (`1204887c3`) `AuditIntentDeliveryProcessor`'a sınırlı bir bekleme ekledi: Platform 409 + `AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED` derse, YALNIZ GP'nin üç yeni işlemi (79/80/81) 2880 deneme (~1 gün, sayılı `AwaitingCentralMapping`) bekler; geri kalan her işlem ve her 409 eskisi gibi ölü mektup olur. İki açık: (1) **tekrar aracı yok** — MDM, Platform'dan önce dağıtılır ve bekleme süresi aşılırsa ya da bir operatör hatasıyla niyet ölü mektuba düşerse onu yeniden kuyruğa koyacak denetimli bir yol (CLI / iç uç, kiracı sınırlı, kuru koşu, denetim satırı) yok; kayıt kaybolmaz ama merkezi denetim izinde eksik kalır. (2) **bekleme listesi sabit kodlu** — LSKU'nun 82–86 ve FG'nin 87+ yeni işlemleri birleşince aynı dağıtım sırası boşluğuna düşer ve beklemeden ölü mektup olur. Öneri: liste "bu sürümün eklediği işlemler" tanımına bağlanır (tek kaynak: işlem eklerken aynı yerde işaretlenir, test eşitliği), LSKU / FG kabulünde genişletilir; tekrar aracı salt ölü mektup → bekliyor geçişi yapar, içeriği değiştirmez. Karşılaştırma: SAP'de qRFC / bgRFC kuyruğundaki hatalı birim SMQ1/SBGRFCMON'dan yeniden başlatılır; Oracle Integration'da hatalı örnek "resubmit" edilir — ikisinde de operatör yeniden sürer, içerik değişmez. Gelecek regresyon riski: 🟡 (LSKU / FG birleşince boşluk büyür; Platform önce dağıtılırsa yok).

**Ek (CT 2026-10-07, S1 madde 6 kararı A):** (3) **seçili yeniden iletim FG ve Kalem'i almıyor** — `SelectedAuditIntentDeliveryRequest.IsAllowed` FinishedGood / Item kümesini içermiyor ve `SelectedAuditIntentDeliveryMongoTests.Reservation_scope_never_promotes_FG_unproven_or_wrong_command_evidence` FG reddini bilinçli sabitliyor (P5 seçili denetim tasarımı). FG / IT niyetleri olağan iletimden gider; seçili yola alınmaları ayrı kapsam genişletmesidir: önce FG / IT kanıt modeli ölçülür (rezervasyon kanıtı, komut kanıtı), sonra izinli listeye eklenir ve FG testi bilinçli değişir. Bugünkü durum: (1)'deki tekrar aracı yazılırken FG / IT de kapsanmalı. Risk: 🟢 (olağan iletim çalışıyor; yalnız operatörün seçili yeniden sürme aracı FG / IT'yi kapsamıyor).

---

### BL-565

**Kayıt kodu standardı (RCS-001) mevcut modüllerde uygulanmıyor: Organizasyon Birimi, Pozisyon, Tüzel Kişilik, Görev Türü, Abonelik Planı ve PPM kodları elle ve gerekçesiz; ortak kod üretici yok**

DURUM: AÇIK · SAHİP: CT (Platform + MDM; PPM kısmı PPM sahibinde / Codex) · BULAN: sahibin paylaştığı yönetim karar notu (Kodlama Standardı v1.0, 2026-09-04) ile kod karşılaştırması · KAYIT: 2026-10-07

Yönetim kararı (RCS-001 §1–§4): `{ŞİRKET}-{TÜR}-{6+ hane, hiç sıfırlanmayan}` (`MG-ORG-000042`). Organizasyon Birimi + Pozisyon OTOMATİK (değiştirilemez). Tüzel Kişilik, Görev Türü, PPM Portföy / Program / Proje,
Girişim / Yatırım Dosyası ve Abonelik Planı İKİSİ DE (öneri + gerekçeli değişiklik, denetimli). Müşteri, Kampanya, Ürün ve Doküman KALSIN. Mevcut kodlar değişmez; yalnız yeni kayıtlar.
Ölçüm (2026-10-07, `chore/ct-round-2`): hiçbiri uygulanmıyor. Kodlar elle; OrgUnit / Position / LE / Plan / PPM serbestçe ve gerekçesiz değiştirilebiliyor; Görev Türü değişmez ama öneri yok; ortak üretici yok
(dört ayrı sayaç: CRM hesap, CRM kampanya, MDM ürün, Doküman); hiçbir yerde `MG-` yok; normalleştirici Türkçe harf bırakıyor (`SATI-BÖLÜMÜ-1`).
Yapılacak: (1) ~~yönetim teyidi~~ sahip 2026-10-07'de RCS-001 §5'teki 12 öneriyi ONAYLADI (Türkçe harf yok; `MG` yalnız örnek) — sahipten kalan tek girdi: şirketlerin kısa kod listesi; (2) ortak kod üretme servisi
(önek ayarı, atomik sayaç, öneri, gerekçe + denetim); (3) Tüzel Kişiliğe kısa kod alanı; (4) türlerin bağlanması: önce Organizasyon Birimi + Pozisyon (organizasyon verisi girişi buna bağlı),
sonra Tüzel Kişilik / Görev Türü / Abonelik Planı, en son PPM (Codex sahibiyle). Ayrıca notta sayılmayan elle kodlu türler (Marka, MDM marka ürünü vb.) için sınıf kararı. Karşılaştırma: SAP'de
numara aralıkları (SNRO) nesne + şirket kodu başına tanımlanır, iç / dış numaralama seçilir; Oracle Fusion'da "Document Sequences" kategori + iş birimi başına. Gelecek regresyon riski: 🟡 (her yeni
kayıt eski elle düzene ekleniyor; geç başladıkça "yeni kayıtlar" penceresi büyüyor).

---

### BL-564

**`verify_module_id.py` yalnız bulunduğu dalın registry'sini okuyor: başka dalda alınmış bir modül kimliğine "OK" diyor (Marka ve kalem işinde iki kez)**

DURUM: AÇIK · SAHİP: CT (belge / araç) · BULAN: WP-MDM-ITEM-MASTER-G0 (MDM sohbeti) · KAYIT: 2026-10-06

MOD-0290-FU03 "Product Legal Entity Scope Assignment" paketi kapsam dalında duruyor, CT şeridinin registry'sinde satırı yoktu; araç FU03'ü kalem işi için de "OK" saydı. Aynı tuzak Marka paketlerinde
(FU01 = ABB, FU02 = Market Supply ile çakışan adlandırma) çıkmıştı. Geçici önlem: şerit registry'sine FU03 AYRILMIŞ satır (`4f9c63309`). Düzeltme: `--check-id` aday kimliği `git log --all -S` ile
tüm dallarda arar; başka paket izi varsa BLOCKED. Ayrıca sözleşme dosyası `docs/analysis/contracts/product-master-bundle.openapi.yaml` beş klasör kuralının dışında — §4 protokolüyle
`docs/reference/architecture/contracts/`'a taşınmalı (ayrı küçük iş). Gelecek regresyon riski: 🔴 (kimlik çakışması sessiz).

---

### BL-563

**Ürün kimliği denetim niyetleri canlıda merkeze ulaşmıyor: teslim işçisi kapalı, temporal cutover kapalı, niyetlerde sözleşme sürümü boş ve korelasyon GUID değil — FG taslak iptali bu yüzden canlıda sonuçlanamaz (BL-559 canlıda açık kalır)**

DURUM: AÇIK · SAHİP: CT (MDM devralma) + sahip (canlı ayar kararı, BL-536) · BULAN: WP-MDM-FG-ACCEPT-01 FIX1 madde 1 ölçümü · KAYIT: 2026-10-06

MOD-0290 s21 FG taslak iptalini ancak doğrulanmış merkezi G4 makbuzuyla kesinleştiriyor ("PendingAudit is not final and does not free the parent"). Ölçüm: (a) `AuditIntentDeliveryWorker` varsayılan kapalı;
(b) temporal-storage scalar cutover etkin değilken depo talep edilen yükü vermiyor (testte 14 keşif / 14 talep / 0 kabul); (c) FG oluşturma, LSKU ve GSKU dahil hiçbir ürün kimliği niyeti genel yoldan
teslim edilemiyor (`ContractVersion` boş, `CorrelationId` GUID değil — MOD-0290 madde 10 bilinen borç). Sonuç: canlıda iptal edilen taslak FG PendingAudit kalır ve GSKU'yu engellemeye devam eder (güvenli,
görünür, gösterge sayar; bugünle aynı). Düzeltme: ürün kimliği niyetlerinin sözleşme / korelasyon borcu kapanır, temporal cutover açılır, teslim işçisi canlıda açılır (sahip kararı). Ayrıca:
FG / LSKU taslak iptali için tam `cancel-draft` izin anahtarları (v1 oluşturma anahtarıyla) ve sonuçlandırıcının "güncel kiracı Active" koşulu (yeni Platform okuması). Karşılaştırma: SAP'de
değişiklik belgesi (CDHDR) işlemle aynı LUW'da yazılır, ayrı teslim beklemez; Oracle Fusion denetimi de işlem içi. Gelecek regresyon riski: 🟡 (FG canlıda kullanılmıyor; API açık).

---

### BL-561

**Depolarda ortak `IsDeleted == false` koşulu, alanı hiç yazılmamış çok eski belgeyle eşleşmez (eksik alan ≠ false)**

DURUM: AÇIK · SAHİP: CT (MDM devralma; genel kural) · BULAN: GP BL-552 akış 1 FIX1 eski belge denetimi (MDM sohbeti) · KAYIT: 2026-10-06

BL-529 FIX6'da gerçek hata olarak çıkan sınıf: Mongo'da `Eq(field, false)` alanı OLMAYAN belgeyle eşleşmez. MDM depolarının neredeyse hepsi etkin kayıt süzgecinde `IsDeleted == false` kullanıyor;
`EntityBase.IsDeleted` sürücü varsayılanıyla hep yazıldığından bugünkü belgelerde alan var, ama betikle içe aktarılmış ya da çok eski bir belgede yoksa kayıt sessizce "yok" sayılır (okunmaz, kilit
alamaz, liste dışı kalır). Düzeltme: ortak süzgeç `Ne(IsDeleted, true)` olur ya da bir kerelik salt-okunur sayım ile alanı eksik belge olmadığı kanıtlanır (dev + canlı, sahip onayıyla).
Karşılaştırma: SAP'de silme işareti (LVORM) boş = silinmemiş, karakter alanı hep var; Oracle'da `DELETED_FLAG` NULL = 'N' sayılır. Gelecek regresyon riski: 🟢 (bugün ölçülmüş belge yok; içe aktarma olursa 🟡).

---

### BL-562

**Marka v2 ertelenenleri: arşivi geri alma, Global Ürün ↔ Marka bağı, marka değişikliğinde ikinci onay, tescil listesi**

DURUM: AÇIK · SAHİP: CT (MDM devralma) · BULAN: WP-MDM-BRAND-ACCEPT-01 FIX1 sahip kararı olguları · KAYIT: 2026-10-06 (sahip kararları CT'ye devretti)

v1'de arşiv geri alınamaz (onayda uyarı var); geri alma ayrı izinli, denetimli bir komut olarak gelir (SAP silme işareti kaldırılabilir; Oracle öğe durumu Etkin'e dönebilir). Global Ürün'e isteğe bağlı Marka
başvurusu (yalnız bağlanabilir markaya) sonraki bir GP turunda. Marka yazımlarında maker-checker v1'de yok (denetim izi var). Tescil (marka tescil) listesi Marka'nın değil, ileride RIM modülünün işi.
Gelecek regresyon riski: 🟢.

---

### BL-560

**Tamamlanmamış güvenilir başlatmayı tüketici kimliksiz iptal edemiyor; kapatıldığını da kendi koduyla öğrenemiyor (anahtarla iptal + "kapatıldı" cevabı + MDM bağlantısı)**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru + MDM devralma) · BULAN: WP-WF-ENGINE-RECOVERY-01 ölçümü (motor sohbeti) · KAYIT: 2026-10-06

Tamamlanmamış güvenilir başlatmada start-result yalnız 409 WORKFLOW_START_NOT_COMPLETED dönüyor; örnek / görev kimliği ve sürüm yok (`GetTrustedWorkflowStartResultHandler.cs:44-51`).
Mevcut iptal sözleşmesi (`TrustedWorkflowCancellationRequest`, `WorkflowModels.cs:356`) örnek + görev kimliği, iki sürüm ve Active örnek / Waiting* görev istiyor (`TrustedWorkflowCancellationCoordinator.cs:115-122`).
Bu yüzden tüketici, terk etmeden önce tamamlanmamış başlatmayı iptal edemiyor. Bugünkü yol (WF-RECOVERY'de ölçülüp belgelenen A): yeniden oynat (koordinatör tamamlar) → preflight → cancel. Bedeli: tamamlanma ile
iptal arasındaki kısa pencerede görev onaylayanlara görünür. WF-RECOVERY'nin süpürmesi süresi dolmuş rezervasyonu kapatır; start-result o anahtara bilinmeyen anahtarla aynı 404'ü verir. Tüketicinin "pencere
sonrası NotFound → bitir + bırak" kuralı böylece doğru sonucu verir (ilke A), ama tüketici kapatıldığını adıyla öğrenmez. Düzeltme: Platform'da idempotency anahtarıyla iptal ucu (rezervasyonu motorun kendi
kapatma yolu kapatır, görev yazılmışsa geri çekilir) + kapatılmış rezervasyon için start-result'ta adlı bir kod + MDM'nin bu kodu sonlandırmaya bağlaması (`PlatformProductIdentityWorkflowClient.cs:278`), birlikte.
Karşılaştırma: SAP Business Workflow'da başlatılamamış iş akışı SWPR / SWIA ile mantıksal silinir; Oracle BPM'de örnek kimliği olmadan "abort by correlation key" yolu var. Gelecek regresyon riski: 🟢 (süpürme kapatıyor, tüketici kuralı doğru sonuç veriyor).

---

### BL-559

**Taslak bir Bitmiş Ürün (FG) bağlı olduğu GSKU'yu düzeltmeye ve emekliye ayırmaya kalıcı kapatıyor: FG'nin çıkışı yok, GSKU onu engel sayıyor**

DURUM: AÇIK · SAHİP: CT (MDM devralma) — ara önlem kararı sahibin · BULAN: FG salt-okunur ön incelemesi (75c286f7d) · KAYIT: 2026-10-06

FG bugün yalnız taslak temeli: iptal / gönder / emekli ucu yok (`FinishedGoodsController.cs:20-44`). `GskuRepository.FindRetirementBlockerAsync` (:745-762) Taslak dahil Emekli olmayan her FG'yi engel sayıyor →
GSKU düzeltme başlatması (`GskuCorrectionWorkflowProcessor.cs:47`, karar anı :490) ve emeklilik talebi (`GskuRetirementRequestWorkflowProcessor.cs:42`) 409. FG ekranı menüde gizli ama API açık ve kiracı Admin
şablonu `mdm.finished-goods.create` alıyor (`DefaultRolePermissionTemplate.cs:91-92`). Tek bir FG taslağı → o GSKU'da yanlış paket / ölçü kalıcı. Düzeltme: FG FIX1 (`scratchpad/fg-fix1-prompt.md` — denetimli taslak
iptali, engel sorgusu yalnız sonuçlanmış iptali dışlar), LSKU FIX1 kabulünden sonra. **Ara önlem (sahip kararı):** FIX1 gelene kadar `mdm.finished-goods.create`'i varsayılan Admin şablonundan çıkarmak (Auth).
Ayrıca: FG'nin P1A işlem deposu (bağlı değil) GSKU'nun FIX3–FIX7'de kaldırdığı kalıcı kilit kalıbını yeniden kuruyor — yaşam döngüsü turundan önce yeniden işlenmeli. Karşılaştırma: SAP MDG'de
taslak alt nesne üst nesnenin değişikliğini engellemez (taslak kendi değişiklik talebinde yaşar); Oracle PIM'de taslak öğe üst öğeyi kilitlemez. Gelecek regresyon riski: 🔴 (API açık).

---

### BL-558

**"Son yönetici" koruması silinen kişi yönetici değilken de reddediyor (KR1)**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: sahibin Kullanıcı Rolleri canlı kontrolü · KAYIT: 2026-10-05

Kiracıda `auth.users.create` taşıyan aktif hesap kalmayınca (o gün yönetici hesabı silinmişti — BL-521 olayı) rolsüz yeni bir "deneme" kullanıcısını silmek "Bu, kullanıcı
ekleyebilen son hesap olduğu için silinemez" ile reddedildi. Koruma "silinince kalan yönetici sayısı 0 mı" diye bakıyor, "silinen kişi yönetici mi" diye değil → yönetici
kalmamış kiracıda HİÇBİR kullanıcı silinemez ve cümle yanlış kişiyi suçlar. Düzeltme: yalnız hedef kendisi yöneticiyse ve ondan başka aktif yönetici yoksa reddet; yöneticisiz
kiracı durumu ayrı bir uyarı / kurtarma yolu ister (platform yöneticisi kurtarır). Karşılaştırma: SAP'de son SAP_ALL kullanıcısının kilitlenmesi uyarıyla korunur, diğer
kullanıcılar etkilenmez; Oracle'da son IDCS yöneticisi korunur. Gelecek regresyon riski: 🟢.

---

### BL-557

**Parola sıfırlama belirteci özetinde dizin yok ve kiracı `set-password` kapısı sınırsız**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: BL-529 FIX4 bağımsız gözden geçirmesi · KAYIT: 2026-10-05

Anonim kiracı `api/users/set-password` kapısı kullanıcıyı `PasswordResetTokenHash` ile arıyor; `MongoDbIndexConfigurations`'ta bu alanda dizin yok (her deneme koleksiyon taraması)
ve kapı hız sınırsız. Belirteç tahmin edilemez (64 bayt), ama sınırsız tarama bir yük kaldıracı. Düzeltme: seyrek dizin + BL-529 FIX5'teki kural (önce belirteç, yalnız geçersizler
sayılır). Gelecek regresyon riski: 🟢.

---

### BL-556

**Auth tohumlayıcısı `admin@diten.com`'u sabit bir bcrypt özetiyle HER ortamda oluşturuyor**

DURUM: AÇIK · SAHİP: CT (Auth) — canlı hesap kararı sahibin · BULAN: BL-529 FIX4 bağımsız gözden geçirmesi · KAYIT: 2026-10-05

`Persistence/DependencyInjection.cs:90-92` yalnız sahte kullanıcıları Development'a bağlıyor; `DataSeeder.cs:1062-1070` platform yöneticisini depoda yazılı sabit bir parola özetiyle
her ortamda oluşturuyor (bulunamazsa). Kaynak koduna erişen herkes yeni kurulan bir ortamda bu hesabın parolasını bilir. Düzeltme: üretimde tohum yok — ilk yönetici kurulum
komutuyla / tek kullanımlık kurulum bağlantısıyla oluşturulur (`MustChangePassword` + MFA zorunlu); var olan canlı hesapların parolası bu özetle eşleşiyor mu ölçülür (sahip kararı).
Karşılaştırma: SAP'de kurulumda varsayılan parolalar (SAP*, DDIC) kurulum sonrası değiştirilmek ZORUNDA ve denetimde raporlanır; Oracle Cloud'da ilk yönetici etkinleştirme
e-postasıyla gelir. Gelecek regresyon riski: 🔴 (yeni ortam kurulumunda bilinen parolalı yönetici).

---

### BL-555

**MDM denetim defteri: takeover'da 19 yazma komutu ne denetimli ne beyanlı istisna ne borç (`EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt` kırmızı)**

DURUM: AÇIK · SAHİP: CT (MDM devralma) · BULAN: görev talebi → takeover birleştirmesi (main'in sıkı AuditTrailStandardTests'i) · KAYIT: 2026-10-05

Takeover dalında mimari testi 18 MDM komutuyla zaten kırmızıydı (ABB / GSKU turlarının raporlarında "önceden var"). Main'den gelen sıkı sürüm bir ad daha yakaladı:
`DispatchProductAbbreviationWorkItemActionCommand` — onay / ret / iptal / emeklilik için denetlenen komutlara `_mediator.Send` ile yönlendiriyor ve bir yolda denetim niyetini
kendisi yazıyor (`AppendAuditIntentIfAbsentAsync`), ama `audit-ledger/Diten.MdmService.md`'de satırı yok. Düzeltme: 19 komutun her biri için defter satırı — doğrudan
denetimli, dolaylı (ispat testinin kabul ettiği "üzerinden" türüyle) ya da beyanlı istisna; hiçbiri sessiz borç kalmaz; sabit sayılar CT kararıyla güncellenir.
Bu, birleştirmelerde her seferinde "bilinen kırmızı" satırı doğuruyor ve yeni bir denetimsiz MDM komutunu gizleyebilir. Karşılaştırma: SAP'de değişiklik belgesi
(CDHDR/CDPOS) nesne sınıfı bazında zorunludur; Oracle Fusion'da denetim politikası iş nesnesi bazında açılır — ikisinde de "denetimsiz yazma" yapılandırma kararıdır,
sessiz durum değil. Gelecek regresyon riski: 🟡.

---

### BL-554

**Organizasyon birimi üst değişikliği yük altında iki eşzamanlı değişikliğin ikisini de yazabiliyor: yapı sayacı kenar yazılmadan ÖNCE ilerliyor (ağaçta döngü riski)**

DURUM: AÇIK · SAHİP: Organizasyon (MOD-0288) sahibi; CT yönlendirir · BULAN: WP-WCN-TASK-REQUEST-01 FIX7 tam koşusu (`OrganizationMatrixReportingTests.Two_concurrent_reparentings_cannot_both_land`
"round 5: both re-parentings landed") + bağımsız gözden geçirme, CT kodda doğruladı · KAYIT: 2026-10-05

`UpdateOrganizationUnitCommandHandler.cs` yapı belirtecini doğrulamadan önce okuyor (:36, doğru), doğruluyor, `TryAdvanceStructureTokenAsync` ile koşullu ilerletiyor (:137) ve
KENARI ANCAK SONRA yazıyor (:152, düz ReplaceOne). Koşullu ilerletme sağlam, ama ilerletme ile yazım arasında pencere var: ikinci yazan belirteci birincinin ilerletmesinden
SONRA, ağacı birincinin yazımından ÖNCE okursa taze belirteç + bayat ağaçla döngü denetimini geçer, kendi ilerletmesini kazanır ve kenarını yazar → iki kenar birden (çapraz iki
değişiklikte döngü). Testin güvenlik iddiası (`landed <= 1`) tam takımın yükünde bir kez kırmızı; tek başına 3/3 yeşil — kararsız test DEĞİL, yükle açılan gerçek yarış
(çıkarım; yeniden üretilmedi). Düzeltme sırayı değiştirmekle bitmez (yazım önce olursa kaybedenin kenarı zaten yazılmış olur): ya kenar ile belirteç tek işlemde (Mongo
işlemi), ya "bekleyen" durumu okuyucuların reddettiği iki aşamalı yazım, ya yazımdan sonra yeniden denetim + geri alma. Karşılaştırma: SAP HCM'de (PP01 / OM) yapı değişikliği
nesne kilidiyle (ENQUEUE) seri hâle getirilir; Oracle HCM'de hiyerarşi değişikliği sürümlenir ve tek işlemde yazılır. Gelecek regresyon riski: 🔴 (organizasyon ağacında döngü;
onay zincirleri ve kapsam çözümü ağaca dayanıyor).

---

### BL-553

**Liste standardı artıkları (ABB turlarından): 413 için paylaşılan cümle "50.000 satır" diyor; dışa aktarma URL'si yalnız-yerel alanı taşıyor; görünüm taşımasında "bitti" işareti yok**

DURUM: AÇIK · SAHİP: CT (liste bileşeni — BL-440) · BULAN: WP-MDM-ABB-LIST-01 FIX1 bağımsız gözden geçirmesi · KAYIT: 2026-10-05

(a) Fabrikanın 413 tostu paylaşılan `ExportTooLarge` anahtarını kullanıyor ("50.000'den fazla satır, süzgeci daraltın"); MDM'nin tek ürün okuması 500'de kesilen ABB gibi
listelerde eşik başka ve daraltmak işe yaramıyor → fabrika sunucunun 413 gövdesindeki kodu / eşiği okuyup sayfaya özgü cümleye izin vermeli. (b) Fabrika dışa aktarma URL'sini
kendisi kurarken süzgeç alanlarının hepsini ekliyor; ABB'nin görüntü metni alanı (`globalProductText`) sunucuya gidiyor (sunucu yok sayıyor) → alan için "yalnız yerel"
seçeneği. (c) Sürümlenmiş sayfa anahtarına görünüm taşımasında "bitti" işareti yok: görünümü olmayan kullanıcı her açılışta fazladan `getViews` yapıyor, iki sekme iki v2
varsayılanı yazabiliyor, kalıcı depo hatasında uyarı her açılışta çıkıyor. (d) İlk ajax başarısızsa DataTables initComplete'i tetiklemiyor → fabrikaya bağlı süzgeç / derin
bağlantı akışı ölü kalıyor (fabrika bir "init başarısız" kancası sunmalı). Karşılaştırma: SAP Fiori List Report'ta dışa aktarma sınırı ve mesajı uygulama bazında
yapılandırılır; Oracle VBCS tablolarında da. Gelecek regresyon riski: 🟡 (her yeni sunucu modlu listede tekrarlanır).

**Ek 2026-10-05 — ABB LİSTESİ CT KABULÜ (kod): `feature/mdm/abb-accept` `9020e844f` (WP-MDM-ABB-LIST-01, FIX1–FIX3).** FIX3 gözden geçirmesi engelleyici / zorunlu yok;
CT sabotajı 2/2. Paylaşılan fabrikada tek geriye uyumlu satır: `data-dt-describe="false"` olan alan kontrollü kopyanın süzgeç satırına yazılmaz (CT izni; BL-440 sahibi
CT). Takeover'a birleştirme bekliyor (takeover worktree'sinde GP işi sürüyor). Kabulden kalan küçükler: (e) ürün etiketi okuması sütun gizlense de yapılıyor — kapsam
dışı üründe (GP okuma 404) dışa aktarma 502 · ürün okumasının 401'i de 502 ve günlüksüz · izinli okuyucunun DataTables Copy düğmesi derin bağlantıdan sonra GUID kopyalıyor ·
derin bağlantıdan sonra süzgeç satırı "Yok" diyor ("Global Ürün: —" daha doğru) · FIX1/FIX2 derlemelerinin kaydettiği görünümlerde metin = GUID (yalnız dev) · süzgeç
satırı istemci metninden — ad değişince eski ad. (f) ABB izin denetimi `PermissionClaims`'i kullanmıyor ("*" yetkisini tanımaz; güvenli yöne düşer).

---

### BL-552

**Global Ürün onay akışları BL-547'nin üç cevabını hâlâ karantinaya çeviriyor: Görev Merkezi'nde iptal edilen bir GP onayı ürünü kalıcı kilitleyebilir**

DURUM: AÇIK · SAHİP: CT (MDM devralma) · BULAN: WP-MDM-GSKU-ACCEPT-01 FIX5 bağımsız gözden geçirmesi · KAYIT: 2026-10-05

GSKU FIX5 COR / RRP'de üç cevabı ayırdı (kararsız kapandı → biter + kilit bırakılır; tutarsız → takılı + talep edenin komutu çıkış; bitmedi → bekler). Global Ürün işlemcileri
(`GlobalProductCorrectionWorkflowProcessor.cs:259, :98`, `GlobalProductRetirementRequestWorkflowProcessor.cs:205, :77`, `GlobalProductIdentityWorkflowProcessor.cs:1205`)
CLOSED_WITHOUT_DECISION, INCONSISTENT ve 401/403 ServiceWait'i hep ManualAsync / QuarantineAsync'e gönderiyor; kontrol noktası uç, 409, tekrar yolu yok → iptal edilmiş bir
GP onayı ya da bir servis kimliği arızası ürünü kalıcı kilitler (çıkarım: işlemciler bırakmıyor ve uzlaştırmıyor). Düzeltme: GSKU FIX6 kabul edildikten sonra ayrı WP —
aynı durum ⇔ çıkış tablosu GP'ye uygulanır, başlatma araması ile başlatma cevabı ayrılır (GSKU FIX6 madde 1), testler gerçek istemci cevaplarıyla. Karşılaştırma: SAP MDG'de
iptal edilen değişiklik talebi nesneyi kilitten çıkarır (USMD_CREQUEST iptali); Oracle PIM'de reddedilen / geri çekilen onay öğeyi düzenlenebilir duruma döndürür.
Gelecek regresyon riski: 🔴 (canlıda tek bir iptal kalıcı kilit doğurur).

**Ek 2026-10-05 — GSKU CT KABULÜ ve GP işinin başlaması.** GSKU (WP-MDM-GSKU-ACCEPT-01) takeover `75c286f7d`'de kabul edildi (FIX7; gözden geçirme engelleyici / zorunlu
yok; CT sabotajı 4/4). GP işi WP-MDM-GP-WF-ANSWERS-01 olarak MDM sohbetine verildi (`gp-bl552-prompt.md`). GSKU kabulünden kalan küçükler (GP işiyle ya da sonra): depo
değişmezi sessiz `return false` (koşucu `deferred++`, günlük yok) · bekleme saati yedeği her talepte yenileniyor (`UpdatedAt`) · kalıcı RELEASE_CONFLICT'te tekrar sonrası gösterge
inip çıkıyor · runbook'ta FIX7 öncesi cümleler (`service-trust-chain-runbook.md:390, :402-404, :442-443`) · bir başvuru verisi 500 / 502'si artık kişiye düşüyor (yalnız 503 / 504 /
401 / 403 geçici sayılıyor; `ApplyAsync`) · FIX6 derlemesinin yazdığı Manual + Retryable satırları için onarım yok (yalnız dev) · `GSKU_LIFECYCLE_RELEASE_CONFLICT` dört yerde
çıplak dizgi.

---

### BL-551

**İki kararsız test gerçek kırmızıyı gölgeliyor: `DocumentDowntimeTemporaryIssueTests.Close_downtime_after_all_issues_reconciled_succeeds` saate bağlı, `TimesheetNotificationHttpMongoTests.T3_04` aralıklı**

DURUM: AÇIK · SAHİP: CT (altyapı; testlerin sahipleri: belge yönetimi, Zaman Çizelgesi) · BULAN: WP-WCN-TASK-REQUEST-01 FIX5 ÖNCE/SONRA ölçümü · KAYIT: 2026-10-05

Belge kesintisi testi kesintiyi "şimdi − 2 saat"te başlatıp `DurationWorkingDays == 0` bekliyor; gece yarısına yakın koşunca 1 çıkıyor (9fd968d97 arşivinde
de aynı anda kırmızı — dalın işi değil). T3_04 aynı ağaçta bir koşuda kırmızı, sonrakinde yeşil. İkisi de her ÖNCE/SONRA tablosunda "bilinen kırmızı" satırı
doğuruyor ve sabotaj tabanını bozuyor. Düzeltme: belge testi sabit bir saatle (`TimeProvider` / sahte saat) kurulur; T3_04'ün yarışı bulunur (büyük olasılıkla
bildirim yazımının beklenmeden okunması) ve bariyerle sabitlenir. Karşılaştırma: SAP ve Oracle CI'larında kararsız test karantina listesine alınır ve sahibine
bilet açılır; karantina süresiz değildir. Gelecek regresyon riski: 🟡 (gürültü gerçek bir kırmızıyı "bilinen" diye geçirebilir).

---

### BL-550

**BL-521 artıkları: kiracı aktörünün sistem dışı rol işlemleri platform hesabının yetkisini daraltabiliyor; somut depo açık; işaret onarımı yarışta geri alınabiliyor; tek seferlik işaret doldurma yok**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: WP-PLATFORM-PARTNER-SCOPE-01 FIX7 bağımsız gözden geçirmesi + alt ajan raporu · KAYIT: 2026-10-05

FIX7–FIX8 kapsamı dışında kalanlar: (a) `DeleteRoleCommandHandler.cs:44-46` / `UpdateRoleCommandHandler.cs:46` yalnız sistem rollerini koruyor; kiracı aktörü,
bir platform kapısı aktörünün platform hesabına verdiği sistem dışı bir rolü silebilir ya da `RevokePermissionCommandHandler` ile izin kaldırabilir —
oturum sonlanmaz ama hesabın bir sonraki basımda yetkisi daralır. (b) Somut `UserRepository` DI'da açık kayıtlı (`Persistence/DependencyInjection.cs:71`);
`VisibleUserIdsAsync` orada no-op — somut türü enjekte eden her şey kapsamı atlar; mimari testi: işleyiciler somut depoyu istemez. (c) İşaret onarımı hedefli
`$set`, ama işaretten önce yüklenmiş bir kiracı isteğinin (girişi, parola değişimi) bütün belge yazımı onu geri alır; bir sonraki platform basımı yeniden onarır.
(d) Kiracı giriş kapısının ret satırı anonim tekrarla çoğaltılabilir (sayaç / örnekleme yok). (e) Platform kapısından hiç geçmemiş, kopyası eksik hesap işaretsiz
kalır; tek seferlik doldurma (Platform kayıtlarından Auth'a) gerekir — o zamana dek o hesabın kiracı oturumu SessionTimeoutMinutes'ı aşarak yenilenebilir.
(f) BL-521 FIX8 kabulünden kalan küçükler: platform zorunlu değişiminde rol yakınsaması işaret yazımından ÖNCE (`PlatformAuthController.cs:307-314`) — işaret
yazılamazsa değişim yapılmaz ama roller yeniden yazılmış olur; süzgeç yorumunda var olmayan test adı; parite testi bütün `char.IsWhiteSpace` üyelerini denemiyor;
bir test kâhini emekli `!= null` kuralını kullanıyor.
Karşılaştırma: SAP'de platform (BASIS) kullanıcıları iş istemcisinden ayrı istemcide tutulur; Oracle Fusion'da yönetici kimlikleri IDCS'de ayrı alan adı ile
ayrılır — ikisi de "aynı kiracıda işaretle ayır" yolunu kullanmaz (uzun vadeli öneri: platform hesaplarını ayrı kiracıya taşımak, sahip kararı).
Gelecek regresyon riski: 🟡.

---

### BL-549

**Yarım kalmış genel başlatma (örnek var, motor görevi yok) görevin onayını / incelemesini süresiz kilitliyor; motorda kurtarma yolu yok**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: WP-WF-ENGINE-CANDIDATES-01 D2-FIX5 bağımsız gözden geçirmesi · KAYIT: 2026-10-04

Genel başlatma örneği, görevi ve anlık görüntüyü ayrı yazımlarla yazıyor (`StartWorkflowInstanceHandler.cs:~230`); ilkinden sonra çöküş görevsiz Active
örnek bırakır. D2-FIX5 böyle bir örneği benimsemiyor (doğru: kimsenin karar veremeyeceği bir incelemeye gitmez), ama yerine bir şey de açmıyor: gönderim
409 `REVIEW_START_FAILED`'da, onay `TaskApprovalService.cs:206-209`'da süresiz kalır. Kapat / aç yardım etmez (en son örnek hâlâ açık);
`CancelWorkflowTaskCommand` görev ister; güvenilir iptal `TrustedConsumerClientId` ister. FIX5 öncesi de anahtar tekrarı 409 veriyordu (gerileme değil).
Düzeltme motor tarafında: anahtar tekrarında eksik görev + anlık görüntü tamamlanır, ya da görevsiz Active örnekleri kapatan süpürme (BL-540 ile birlikte).
Aynı kayıtta küçük not: rakip yazım bir devirse (gereksinim açık, görev açık) 1. tur yeni sahibe kalıyor (FIX3 "yeni sahip incelemeyi devralmaz"
ilkesine aykırı; düşük). Karşılaştırma: SAP Business Workflow'da yarım kalan iş akışı SWPR ile yeniden başlatılır; Oracle BPM'de hatalı örnek
kurtarma kuyruğuna düşer. Gelecek regresyon riski: 🟢.

---

### BL-548

**Test dosyalarında sır biçimli sabitler (`password=hunter2`, `user:…@` bağlantı dizgileri, AKIA / ghp_ biçimleri): dış gizli tarayıcılar ve GitHub push koruması tetiklenebilir**

DURUM: AÇIK · SAHİP: CT (altyapı) · BULAN: WP-WF-ENGINE-CANDIDATES-01 D2-FIX4 ve WP-EMAIL-SHELL-01 FIX3 gözden geçirmeleri · KAYIT: 2026-10-04

`Eventing.Tests/TenantLifecycleEventContractTests.cs:133, 138` (`password=hunter2`) ve en az altı başka test dosyasında benzer dizgiler var; depo içi
tarayıcı yalnız `appsettings.json` okuyor. E-posta FIX4 kendi dosyalarını çalışma anında birleştirmeye çeviriyor; motor D2-FIX4 kendi bağlantı dizgisini
`TEST-ONLY-NOT-A-SECRET@example.invalid` yaptı. Düzeltme: depo çapında tarama (gitleaks varsayılan kuralları), bulunan test sabitleri çalışma anında
birleştirilir ya da açıkça sahte biçime çevrilir; tarayıcı `tests/**`'ı da okur (sahte biçim izin listesi ile). Karşılaştırma: GitHub push protection ve
gitleaks CI adımı bu sınıfı commit anında durdurur. Gelecek regresyon riski: 🟢.

---

### BL-547

**Onay motorunun güvenilir kanıt ucu kararsız kapanışı (Cancelled / TimedOut) ayırt etmiyor: MDM her kararsız kapanışı karantinaya alıyor**

DURUM: AÇIK · SAHİP: CT (MOD-0023 onay motoru) · BULAN: WP-MDM-GSKU-ACCEPT-01 FIX3 E1 DUR'u · KAYIT: 2026-10-04

GSKU FIX3 (CT kararı 3) çifti yalnız motorun `WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION` kodunda taslağa döndürüyor; başka her çakışma karantina. Platform'un
güvenilir kanıt ucu ise Cancelled / TimedOut için de `WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT` dönüyor ve ön uçuş yalnız Active'i tanıyor: bu kod
yayılana kadar canlıda her kararsız kapanış karantinaya düşer (güvenli; ekran doğru söylüyor ama insan uzlaştırması gerekir). Düzeltme: kanıt ucu
kararsız kapanışı kendi koduyla döndürür (Cancelled / TimedOut / Withdrawn ayrı alt nedenle), sözleşme testi + MDM tarafında uçtan uca. Motor şeridinde
(WP-WF-ENGINE-CANDIDATES-01 D2 kabulünden sonra). Karşılaştırma: SAP Flexible Workflow ve Oracle BPM iş akışı sonucu "onaylandı / reddedildi / geri
çekildi / süresi doldu" olarak ayrı döndürür. Gelecek regresyon riski: 🟢 (yeni kod; eski kod karantinayı korur).

**İlerleme 2026-10-05 — Platform tarafı yapıldı (dal `feature/platform/wf-engine-candidates`, e2e9dc0eb + FIX1 82ed16c0d).** Yükseltilmiş açık onay →
`WORKFLOW_DECISION_NOT_TERMINAL`; kanıtı tutarlı iptal / zaman aşımı → yeni `WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION`; çelişkili kanıt `INCONSISTENT`;
çalışan örnek okunurken karar işlenirse bir kez yeniden okuma; günlüğü olmayan onaylı görev `INCONSISTENT`. MDM tüketici daraltması GSKU-FIX5'te.
Kalan küçük sertleştirmeler: (a) yeniden okuma kararını "tutarsız" sezgisi yerine örnek sürümünün iki okuma arasında değişip değişmediğine bağlamak
(iki ardışık milisaniyelik pencere bugün INCONSISTENT verebilir); (b) "onaylı görev sayısı = onay günlüğü sayısı" kuralını görev başına birebir
eşleşmeye çevirmek (yalnız bozuk veride fark eder).

---

### BL-546

**Ürün çocuk kabulü (GP → GSKU, GSKU çocukları) denetim niyeti yazmıyor**

DURUM: AÇIK (GSKU kabulünden sonra, denetim D2 ile) · SAHİP: CT (MDM ürün modülleri devralma · Denetim standardı) · BULAN: WP-MDM-GSKU-ACCEPT-01 FIX3
madde 5 DUR'u · KAYIT: 2026-10-04

Çocuk kabulü (`ChildCreationAdmissions` Acquire / Complete) yalnız kabul kaydını itiyor / çekiyor; denetim defterine niyet düşmüyor: GxP açısından
"kim, neyi, ne zaman kabul etti" izi yok. Gerekenler: (a) `ProductAuditOperation.GskuChildAdmitted` (sona eklenir, 79) · (b) MDM
`AuditIntentDeliveryRepository.IsSelectedOperationAllowed` kolu · (c) Platform `TrustedSourceAuditIntentOperationMap` satırı + sözleşme testi · (d)
kabul `Version` artırmıyor → niyetin sıra / sürüm kararı (Version artışı emeklilik çitiyle yarışır). GP → GSKU kabulünde aynı boşluk; ikisi birlikte.
Karşılaştırma: SAP'de ana veri değişiklik belgeleri (CDHDR / CDPOS) her kabulde yazılır; Oracle PIM'de onay geçmişi denetim tablosunda tutulur.
Gelecek regresyon riski: 🟢 (ekleme).

---

### BL-545

**E-posta yeniden deneme kapıları süreçler arasında ayrışabilir; değişkenleri yalnız tarama siler**

DURUM: AÇIK · SAHİP: CT (MOD-0027 Bildirimler) · BULAN: WP-EMAIL-SHELL-01 FIX3 bağımsız gözden geçirmesi · KAYIT: 2026-10-04

FIX3'te sıraya alma, kayıtçının üç kapısını (zamanlayıcı açık, standart işler kayıtlı, işin kendi bayrağı) okuyor; tek süreçte ikisi aynı seçenekten
okuduğu için ayrışmıyor. Ayrışma birden çok kopyada olur: API kopyalarında işler kapalı, bir işçi kopyasında açıksa her hata kalıcı işaretlenir ve
işçinin taraması hiç yeniden denemez. İş bayrağı sıraya alındıktan SONRA kapatılırsa ya da tarama hiç koşmazsa o satırların `VariablesJson`'ı süresiz
kalır. Düzeltme: işletim notu (bütün kopyalarda aynı `BackgroundJobs` yapılandırması; bkz. BL-536) + bayrak kapanınca bekleyen satırları kapatan tek
seferlik adım. Karşılaştırma: SAP'de arka plan işi sunucu grubuna bağlanır (SM36 hedef sunucu), Oracle ESS'te iş tanımı tek merkezden çalışır.
Gelecek regresyon riski: 🟢.

---

### BL-544

**Bildirim şablonu kaydında sürüm denetimi okuma-sonra-yazma: milisaniyelik pencerede son yazan kazanır**

DURUM: AÇIK · SAHİP: CT (MOD-0027 Bildirimler) · BULAN: WP-EMAIL-SHELL-01 FIX3 sapması (CT kabul etti) · KAYIT: 2026-10-04

FIX3 şablon kaydına `RowVersion` ekledi; eski okumadan yapılan kayıt 409 TEMPLATE_CHANGED alıyor. Denetim işleyicide taze okumaya karşı yapılıyor;
okuma ile `ReplaceOne` arasındaki milisaniyelik pencerede iki kayıt hâlâ son-yazan-kazanır. Paket şablon deposuna dokunamadığı için atomik yazım
yapılmadı. Düzeltme: depoda `ReplaceOne` süzgecine sürüm (BL-533 deseni, `ReadVersionWrites`). Karşılaştırma: SAP'de belge kilidi (enqueue), Oracle'da
OBJECT_VERSION_NUMBER. Gelecek regresyon riski: 🟢.

---

### BL-543

**ABB okuma yolundaki `PendingSinceUtcTicks` doldurması geçicidir; kaldırılmalı**

DURUM: AÇIK (ABB kabulünden sonra) · SAHİP: CT (MDM ürün modülleri devralma) · BULAN: WP-MDM-ABB-ACCEPT-01 FIX3 madde 9 kararı · KAYIT: 2026-10-04

ABB bekleyen işleri sıralamak için `PendingSinceUtcTicks` alanını kullanıyor; FIX3 öncesi satırlarda alan yok. CT kararıyla (FIX4) ilk sayfa okunmadan
önce kiracı sınırı içinde, süreç başına kiracı başına bir kez, hata okumayı düşürmeyecek biçimde dolduruluyor. ABB hiç yayınlanmadı; eski satırlar
yalnız dev verisinde. Kaldırma: dev verisi bir kez dolduktan (ya da sıfırlandıktan) sonra doldurma kodu ve testleri silinir; okuma yolunda yazım kalmaz.
Karşılaştırma: SAP ve Oracle bu tür alan doldurmayı sürüm geçiş programında (XPRA / upgrade script) bir kez yapar, okuma yolunda yapmaz.
Gelecek regresyon riski: 🟢 (silme).

---

### BL-542

**Auth'ta silinmiş kullanıcı Platform'da pozisyonunu tutmaya devam ediyor; onaycı seçicisinde adsız aday olarak görünüyor**

DURUM: AÇIK · SAHİP: CT (MOD-0024 Görev Merkezi · Organizasyon) · BULAN: BL-512 canlı denetimi (dev, 2026-10-04) · KAYIT: 2026-10-04

Dev'de "Development Team Lead" pozisyonunun sahibi Auth'ta `IsDeleted=true`; Platform'daki pozisyon ataması açık kaldı. Kişi araması bu sahibi aday
olarak döndürüyor, Auth adı vermediği için ad boş; adıyla aranamıyor, pozisyon adıyla ("lead") bulunuyor. Silinmiş kişi onay veremez: seçilirse onay
sahipsiz kalır. Düzeltme: (a) rehber, Auth'un tam cevabında adı olmayan (silinmiş / yok) kişiyi aday saymaz (Auth erişilemiyorsa bugünkü "eksik
rehber" kuralı geçerli); (b) kullanıcı silinince / devre dışı kalınca pozisyon atamaları biter (işten ayrılış akışı, HCM ile birlikte). Karşılaştırma:
SAP HCM'de ayrılış eylemi pozisyon atamasını sonlandırır (PA40), Oracle HCM'de sonlandırma atamayı kapatır ve onay hiyerarşisinden düşürür.
Gelecek regresyon riski: 🟡 (onay zinciri çözümü).

---

### BL-541

**Toplantı raporu ve dışa aktarımı, toplantıdan doğan görevlerin başlığını, sorumlusunu ve bitiş tarihini görev okuma kuralına sormadan gösteriyor; ters yönde görev okuru toplantı başlığını görüyor**

DURUM: AÇIK · SAHİP: CT (Toplantılar · MOD-0024 Görev Merkezi) · BULAN: WP-WCN-TASK-REQUEST-01 FIX3/FIX4 bağımsız gözden geçirmeleri · KAYIT: 2026-10-04

Görev talebi dalı (FIX3–FIX5) toplantı yüzeylerinde görev başlığını okuma kuralına bağladı: bağlı görev listesi (`TaskRelatedRecordResolver`), taşınan
gündem satırı (okuma anında kod). Aksiyon kaydı ise dışarıda kaldı: `MeetingReportQueryHandlers.cs:~149` toplantıyı görebilen herkese o toplantıdan
doğan görevlerin `Title`, `AssigneeUserId`, `DueAt` ve yaşam döngüsünü veriyor; rapor ekranı ve dışa aktarım aynı satırları kullanıyor.
`ITaskReadAccessPolicy` hiç sorulmuyor. Ters yönde `MeetingRelatedRecordResolver` görev okuruna bağlı toplantının başlığını, toplantı görünürlüğüne
bakmadan veriyor olabilir (ölçülmedi). Düzeltme: aksiyon satırları okuyanın okuma kuralından toplu geçer (`ReadableTaskIdsAsync`); okunamayan satır kalır
(karar var) ama başlık / sorumlu / tarih gitmez, ekranda 7 dilde yer tutucu; dışa aktarım aynı kuralla. Ters yön: toplantı görünürlük kuralıyla aynı
desen. Karşılaştırma: SAP'de rapor satırı yetki nesnesiyle süzülür (S_TCODE + nesne yetkisi), Oracle'da veri güvenliği (data security policy)
raporlara da uygulanır. Gelecek regresyon riski: 🟢 (okuma süzgeci ekleme; yazım yok).

---

### BL-533

**Sürümsüz bütün belge değiştirme: toplantı, tutanak, zaman çizelgesi, iş akışı ve görev yorumu depoları eski okumayla yeni belgenin üzerine yazabiliyor**

DURUM: AÇIK · SAHİP: CT (Toplantılar · Zaman Çizelgesi · MOD-0023 · MOD-0024) · BULAN: WP-WCN-TASK-REQUEST-01 FIX3 bağımsız gözden geçirmesi · KAYIT: 2026-10-04

Görev deposunda (ve altı sürümlü görev deposunda) FIX3 merkezi denetimi kurdu: yazım yalnız okunan sürüme iner (`TaskStoreVersioning.IsTheDocumentRead`).
Aynı kusur sınıfı başka depolarda duruyor: `MeetingRepositories.cs:169, 370, 407, 485, 545` (toplantı, gündem, toplantı türü, tutanak, seri),
`TimeEntryRepositories.cs:68…`, `WorkflowRepositories.cs:35, 250, 375`, görev yorum deposu (`TaskRepositories.cs:477-485`; FIX4'te). Tutanak
yayımlama GxP açısından önemli: iki eşzamanlı yazımdan biri sessizce kaybolur. Ayrıca bir görev kararı başka belgelere yazımı (kontrol listesi koşusu,
ek, bağımlılık, kayıt bağı) görev sürümüne bağlamıyor. Düzeltme: her depoda "okunan duruma koşullu yazım" (sürüm alanı yoksa önce alan), depo başına
yarış testi. Karşılaştırma: SAP'de belge kilidi (enqueue), Oracle'da satır sürümü (OBJECT_VERSION_NUMBER) her yazımda zorunlu. Gelecek regresyon riski: 🟡.

**İlerleme 2026-10-04 — toplantı + zaman çizelgesi kapandı (şeritte `01ed9c811`, dal @37f1261ed).** Toplantı, gündem, toplantı türü, tutanak, seri ve
zaman çizelgesi depoları yalnız okunan sürüme yazıyor (`ReadVersionWrites`); tutanak yayımlama (tutanak + katılım + Completed) ve haftalık kayıt (hafta
+ satırlar + düzeltmeler) birer Platform işlemi; yeniden denenen işlem gövdesi okunan sürümlerden başlıyor (`ReadVersionSnapshot`; CT iki yeniden
deneme testi ekledi, sabotaj V1–V4 kırmızı). Görev yorumları görev talebi dalında (FIX4, kabul bekliyor). AÇIK KALAN: (1) iş akışı depoları
(`WorkflowRepositories.cs:35, 250, 375`) · (2) aynı kısmi-yazım sınıfı `AcceptTimeSuggestion`, `RequestTimesheetCorrection`, `DiscardCorrectionDraft`,
`TimerDraftWriter`'da (birden çok belge, işlemsiz) · (3) denetim kaydı işlemin dışında yazılıyor (yazım geri alınırsa denetim satırı kalabilir) · (4)
işlem desteklemeyen Mongo'da bu yollar 500 dönüyor (ABB'deki gibi 503 + açılış uyarısı olmalı).

---

### BL-532

**Parola sıfırlama / rütbe düşürme sonrası açık erişim belirteci süresi dolana kadar çalışıyor (kiracıda 1440 dakikaya kadar); ağ geçidi `pwd_change_required`'ı uygulamıyor**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: BL-529 ve BL-521 gözden geçirmeleri · KAYIT: 2026-10-04

BL-529 sıfırlamada parola özetini değiştirip yenileme belirteçlerini iptal ediyor; BL-521 iş ortaklığını kayıttan belirliyor. İkisi de daha önce basılmış
ERİŞİM belirtecini durduramıyor: kiracıda `SessionTimeoutMinutes` (varsayılan 60, en çok 1440), platformda 15 dakika. Ağ geçidi `pwd_change_required`'ı
uygulamıyor (yalnız Web `Program.cs:491-507`): ele geçirilmiş bir yöneticinin belirteci bu pencerede arka kapı kullanıcı açabilir. Düzeltme: kullanıcı
başına güvenlik damgası (parola / rol / tür değişiminde artar), belirteçte taşınır, Auth / ağ geçidi / servisler kısa önbellekle doğrular; ağ geçidi
`pwd_change_required` taşıyan belirteçle yalnız parola değişim uçlarına izin verir. Karşılaştırma: ASP.NET Identity `SecurityStamp`, Oracle IDCS
oturum iptali. Gelecek regresyon riski: 🟡 (her istekte doğrulama).

**Ek 2026-10-05 — SAHİP KARARI: şimdi öne alınmıyor.** Canlı kontrolde görüldü: Rol İzinleri'nde kaldırılan izin sayfa yenilemede duruyor, çıkış / girişte
gidiyor (beklenen; izinler oturum belirtecinde). Erteleme riski: bugün 🟡 (canlıyı yalnız sahibin ekibi kullanıyor; davranış yeni değil, kötüleşmiyor) · iş ortağı
hesabı, dış kullanıcı ya da GxP doğrulamasından ÖNCE 🔴 — işten ayrılan / pasife alınan kişi belirteç süresi bitene kadar (kiracıda en çok 24 saat) erişir,
BL-529'un sıfırlaması ve BL-521'in kapısı açık erişim belirtecini durdurmaz. Ara önlem (kod gerektirmez): canlı kiracıların `SessionTimeoutMinutes`'ı 60 ya da
altında tutulur. Geri dönüş maliyeti: izin denetimi ağ geçidinde ve her serviste belirteçten okunuyor — damga doğrulaması ağ geçidinde tek noktada yapılırsa
sonradan eklemek orta maliyet; servis sayısı arttıkça artar. Ön koşul olarak işaretlendi: BL-521 iş ortağı hesabı açılmadan önce.

---

### BL-531

**Toplantı katılımcı seçicisi kiracıdaki herkesi döküyor: `meetings/lookups/attendees` aramasız ve sınırsız (BL-512'nin kardeşi)**

DURUM: KAPANDI 2026-10-04 (WP-MEETINGS-ATTENDEE-SEARCH-01, dal `fix/platform/decision-makers-search` @37b05a216, şeritte 0f1f30cdd; ayrıntı BL-512 kapanış notunda; canlı denetim sahip girişiyle) · SAHİP: CT (Toplantılar)
· BULAN: WP-PLATFORM-DECISION-MAKERS-SEARCH-01 ölçümü · KAYIT: 2026-10-04

`MeetingsController.cs:254-260` (izin `meetings.create`) `GetMeetingAttendeeLookupHandler` ile görevlerin karar listesini
(`GetTaskAssignmentPersonLookupQuery(Decision)`) TAM döndürüyor: toplantı açabilen her kullanıcı öbür şirketlerin çalışan listesini alabiliyor.
Altı ön yüz çağıranı (oluşturma, düzenleme, seri, tutanak editörü, liste ve rapor süzgeçleri). Düzeltme: BL-512 ile aynı arama sözleşmesi ve
aynı hız sınırı kovası; rehber gerekmeyen çağıran toplantının kendi katılımcılarını kullanır. Gelecek regresyon riski: 🟡 (toplantı formları).

---

### BL-530

**Ürün kimliği onayında "elle uzlaştırma" çıkışı yalnız gönderende: gönderen ayrılırsa GSKU taslağı donuk kalır**

DURUM: AÇIK (D2'den sonra) · SAHİP: CT (MDM ürün modülleri devralma) · BULAN: WP-MDM-GSKU-ACCEPT-01 FIX2 DUR'u · KAYIT: 2026-10-04

GSKU FIX2'de son olmayan her işlem durumuna bir çıkış konuyor. `ManualReconciliationRequired`'da (motor başlamış olabilir, sonuç bilinmiyor)
insan çıkışı gönderenin aynı gönderimi yeniden oynatması (var olan `mdm.gskus.submit`): motor yeniden okunur, kanıt varsa karar uygulanır,
motorda bir şey yoksa taslak açılır. Açık: gönderen şirketten ayrıldıysa ya da yetkisi alındıysa kimse çıkaramaz. Global Ürün'ün uzlaştırma
işleyicisi D1'de göndericisiz olduğu için silinmişti (a248dc1ef), örnek uç yok. Düzeltme: `POST /api/gskus/{id}/identity-reconciliation`
+ yeni anahtar `mdm.gskus.reconcile-identity` (önerilen sahipler ProductIdentityApprover, Retirement Steward / Lead); yeni MediatR yazma komutu
denetim D2 (MDM niyet izinin teslimi) kapanmadan mimari listesini 18 → 19 yapacağı için D2'den sonra. Aynı ihtiyaç Global Ürün ve LSKU için de
geçerli. Gelecek regresyon riski: 🟢 (ekleme).

---

### BL-529

**Yöneticinin "Parolayı Sıfırla"sı eski parolayı geçersiz kılmıyor, açık oturumları da düşürmüyor**

DURUM: AÇIK · SAHİP: CT (Auth) · BULAN: WP-EMAIL-SHELL-01 FIX1 bağımsız gözden geçirmesi, CT kodda doğruladı · KAYIT: 2026-10-04

`AdminResetPasswordCommandHandler` yalnız sıfırlama belirteci yazıp `RequirePasswordChange` diyor; parola özetine dokunmuyor, yenileme
belirteçlerini iptal etmiyor (dosyada `Revoke` yok). `LoginCommandHandler.cs:90` eski parolayı kabul ediyor; giriş "parolayı değiştir"
adımına düşüyor ve eski parolayı bilen kişi yeni parolayı kendisi belirliyor. Senaryo: yönetici hesap ele geçirildi diye sıfırlar, kullanıcı
hiçbir şey yapmaz, saldırgan eski parolayla girip hesabı kendine alır. Ek: e-posta FIX1 sıfırlama metnine "eski parolanız artık geçerli
değil" yazmıştı (7 dil); doğru olmadığı için FIX2'de kalkıyor, bu kayıt kapanınca geri gelebilir. Düzeltme: sıfırlama anında parola özeti
rastgele bir değere çevrilir (davetteki gibi) ve kullanıcının bütün yenileme belirteçleri iptal edilir; denetim kaydı ve güvenlik olayı
yazılır. Karşılaştırma: SAP'de (SU01 "parola sıfırla") ve Oracle'da yönetici sıfırlaması eski parolayı anında geçersiz kılar, kullanıcı ilk
girişte yeni parola belirler. Dikkat: dev'de "Parolayı Sıfırla" parola dağıtma yolu olarak kullanılıyor; düzeltmeden sonra kullanıcı bağlantıyı
kullanana kadar giremez (istenen davranış). Gelecek regresyon riski: 🟡 (giriş akışına dokunur).

---

### BL-528

**Ağ geçidinden geçen servis çağrıları (HCM, CRM) denetim kaydında asıl isteğin korelasyonunu taşımıyor — imzalı korelasyon kimliği**

DURUM: AÇIK · SAHİP: CT (altyapı / ağ geçidi) · BULAN: WP-PLATFORM-AUDIT-INTX-01 FIX2 gözden geçirmesi + FIX3 ölçümü (2026-10-03) · KAYIT: 2026-10-03

INTX FIX2 ağ geçidinde dışarıdan gelen korelasyona güveni kapattı (istemci başka bir isteğin korelasyonunu seçemesin diye); ağ geçidi kendi
kimliğini üretiyor, istemcinin değerini `X-Client-Correlation-Id` ile iletiyor. HCM denetim eki (`GovernedHcmAuditAppendClient`), CRM denetimi
(`HttpCrmAuditPublisher`), HCM iş akışı başlatma ve CRM ağ geçidi istemcileri Platform'a ağ geçidi üzerinden SON KULLANICININ belirteciyle gidiyor;
Platform'da "doğrulanmış servis çağıranı" kavramı yok (Auth yalnız üç insan aktör türü veriyor, iç anahtar yolu ağ geçidinden geçmiyor). Sonuç: bu
kayıtların korelasyon alanı ağ geçidinin yeni kimliği; asıl isteğin korelasyonu yalnız `Metadata.ClientCorrelation`'da (veri kaybı yok, gruplama o
alandan yapılabilir). Önerilen çözüm: ağ geçidi ürettiği kimliği imzalar (HMAC, kimliğin içinde ya da ayrı başlıkta) ve gelen istekte yalnız KENDİ
imzaladığı kimliği benimser; servisler kimliği olduğu gibi iletir. Gizli anahtar yönetimi gerektirir (dev betiği + canlı ayar; değer hiçbir yere
yazılmaz). Gelecek regresyon riski: 🟡 (her servisin korelasyon akışına dokunur).

---

### BL-527

**MDM testleri dev Mongo'nun koştuğu sunucuda sabit adlı bir test veritabanını paylaşıyor: bir dalın koşusu başka dalların testlerini kırıyor**

DURUM: AÇIK · SAHİP: CT (test altyapısı) · BULAN: WP-MDM-GSKU-ACCEPT-01 FIX1 ölçümü (2026-10-03) · KAYIT: 2026-10-03

Birçok MDM test sınıfı depoları `localhost:27017` üzerindeki sabit adlı `diten_mdm_product_scope_itest` veritabanıyla kuruyor; depo kurucuları
dizinleri `CreateMany` ile oraya yazıyor. Sonuç: GSKU turunun eklediği yeni dizin (`ix_mdm_first_gsku_identity_workflow_gsku_unfinished`) o ortak
veritabanında kaldı ve eski koddaki dallarda "tam dört dizin" testi (`FirstGskuIdentityWorkflowOperationMongoTests…`) kırmızıya döndü (onay motoru
ve denetim izi sohbetleri aynı tabanda kırmızı, MDM sohbeti yeşil ölçtü — sıraya bağlı). Aynı sunucuda geliştirme veritabanı (`DitenERP_Dev`) da
duruyor: test ile dev aynı süreçte. Düzeltme: MDM test düzenekleri geçici bir mongod (Platform'daki `DITEN_TEST_MONGOD` / EphemeralMongo deseni)
ve koşu başına benzersiz veritabanı kullanır, sonunda siler; Platform'daki `MongoTestDatabaseGuardTests` benzeri bir koruma testi 27017'ye ve sabit
veritabanı adlarına bağlanan testi kırmızıya çevirir. Ortak test veritabanındaki fazladan dizin elle düşürülmez (testler yeniden kurar).
Gelecek regresyon riski: 🟢 (yalnız testler).

---

### BL-524

**Platform modül kaydı ve abonelik: işlem dışında kalan iki yazım**

DURUM: AÇIK · SAHİP: CT (Platform) · BULAN: WP-PLATFORM-AUDIT-INTX-01 FIX1 (ajan DUR önerisi + gözden geçirme, 2026-10-03) · KAYIT: 2026-10-03

(1) **Sayfa / eylem yazımları katalog işleminden sonra:** modül kendini kaydederken katalog satırı ve denetim kaydı tek işlemde, ama sayfa ve
eylem tanımları (`IModulePageDescriptorRepository`, `IModulePageActionDescriptorRepository`) işlemden sonra yazılıyor; bunlardan biri çift anahtar
dışında bir hatayla düşerse denetim kaydı (Outcome=Succeeded) olmamış değişiklikleri anlatır. İş: oturumlu depo üyeleri + çakışmaları önceden
eleyen plan (FIX1'deki `PlanSurfaceChangeAsync`'in genişletilmişi) + tek işlem; Auth izin eşitlemesi dış çağrı olduğu için işlemden sonra kalır.
(2) **Abonelik tekil dizini:** `(TenantId, Status)` üzerinde, Current kümesine kısmi; Trialing ile Active'in yan yana durmasına izin veriyor.
FIX1 + FIX2 işlem içi iki koruma koydu; dizinin "kiracı başına tek canlı abonelik" olarak yeniden kurulması canlıda düşür / yeniden kur demek
— ayrı karar, canlıdaki veri önce sayılır. Gelecek regresyon riski: 🟡.

---

### BL-393

**Tek CI hattı (`phase1-gates`) 2026-08-30'dan beri main'de kırmızıydı — iki eski test kuralı yeni kodu bilmiyordu**

DURUM: KAPANDI — `657050ac` + mimari kural düzeltmesi (CT, `feature/infra/auth-display-label`, 2026-09-13; o dalda kapı uçtan uca geçti: kiracı 3/3, mimari 18/18, Web 137/137) · BULAN: CT (go-live öncesi yerel kapı koşusu) · KAYIT: 2026-09-13

`cccd541f` gateway'e "jetondaki kiracıyı başlık ya da alt alan adı çelişirse 400 Tenant mismatch" kuralını getirdi ve kendi test
paketini ekledi; `tests/tenancy/.../UnitTest1.cs` içindeki `JwtTenant_OverridesConflictingHeader` ise hâlâ eski davranışı (başlığın
üzerine yazılıp isteğin geçmesi) bekliyordu. `run_phase1_gates.sh` ilk hatada durduğu için mimari testleri ve Web testleri de o
tarihten beri CI'da hiç koşmadı; o arada birleşen PR'lar (ör. #105, #106) kırmızı hatla girdi. Ölçüm: temiz `origin/main`
(`e5681231`) üzerinde aynı test kırmızı. Test bugünkü sözleşmeye çevrildi; sabotaj: çelişki kontrolü kapatılınca kırmızı.

**İkinci kırmızı (ilki düzelince görüldü):** `MongoTestDatabaseGuardTests.NoTestCreatesItsOwnDatabasePerRun` 2026-08-31'de main'e giren
`PpmAuditRetentionPolicySeedMongoTests` ve `DisposableStandaloneMongo`'yu işaretliyordu (temiz main'de de kırmızı). İkisi de paylaşılan
mongod'a dokunmuyor: kendi geçici `mongod` sürecini açıp kapatıyor, klasörü siliyor; kuralın koruduğu dosya tanıtıcı baskısı ve artık
veritabanı oluşmuyor. Gerekçesiyle istisna listesine eklendi — kuralın "kırmızıyı yeşile çevirmek için satır ekleme" uyarısı bilinerek;
sahip isterse bu iki test ortak veritabanına taşınır ve satırlar silinir. Sabotaj: satır silinince kural o dosyayı adıyla kırmızı verdi.
**Not:** tam Platform/Auth test paketleri ve vitest CI'da hiç koşmuyor; oradaki eski kırmızılar (Doküman Yönetimi, İş Referans Verisi,
3 Auth, 25 vitest) bu kapıyı etkilemiyor, ayrı borç.

---

### BL-394

**Abonelik işlem mimari testleri derlenmiş kodu kaba bayt taramasıyla okuyor — kod değişmeden sahte kırmızı veriyor**

DURUM: KAPANDI — `0d551337` (PSS dalı, 2026-09-14): opcode tablosuyla gerçek IL yürüyüşü; birleşik toplantı derlemesinin kopyasında eski test 2 kırmızı, yeni 10/10; sabotaj (Suspend yazıcı çağrısı, AssignPlan kota çağrısı kaldırılınca) kırmızı · BULAN: CT (go-live öncesi tam paket karşılaştırması) · KAYIT: 2026-09-13

`SubscriptionHandlerTransactionArchitectureTests.GetExecutableCalls` IL baytlarını sırayla gezip 0x28/0x6f gördüğü her yeri çağrı sayıyor;
komut uzunluklarını bilmediği için bir operandın içindeki bayta takılıp kayabiliyor. Derlemeye başka yerde üye eklenince metadata numaraları
değişiyor ve tarama gerçek çağrıyı atlıyor. Ölçüm (`scratchpad/ilprobe`, opcode uzunluklarını bilen doğru okuma ile testin taramasının
birebir kopyası): toplantı dalının birleşik hali (`9f65800f`, MethodDefs 49 449) `SuspendTenantSubscriptionCommandHandler`'da doğru okumayla
`TenantSubscriptionTransactionWriter::UpdateAsync`'i buluyor, kaba tarama bulamıyor (38 yerine 36 çağrı); `AssignPlanToTenantCommandHandler`'da
`IQuotaService::InitializeSubscriptionQuotasAsync` için aynı. Auth (`3b004763`), görev motoru (`bdc6972d`) ve toplantının birleşme öncesi
(`9e1a82f6`) derlemelerinde iki okuma da çağrıyı buluyor ve testler yeşil; işlemlerin kodu ve IL boyu (786 / 429 bayt) her derlemede aynı.
Yani gerileme yok, test kırılgan. CI tam Platform paketini koşmadığı için PR'ı engellemez. Düzeltme: `System.Reflection.Metadata` ile
opcode uzunluğunu bilen gerçek bir IL okuyucu (ilprobe'daki döngü yeterli).
