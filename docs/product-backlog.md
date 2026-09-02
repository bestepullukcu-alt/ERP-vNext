# ERP-vNext — Product Backlog (Deferred / Out-of-Scope-for-Now)

> **Amaç:** Bilinçli olarak **ertelenen** özelliklerin tek kaydı. Her madde bir gerekçe ve bir "ne zaman yapılır" tetikleyicisiyle park edilir — böylece hiçbir şey sessizce unutulmaz ve hiçbir şey vaktinden önce yapılmaz.
> **Sahiplik modeli:** Claude = CONTROL TOWER (prompt yazar, canlı doğrular); yürütme = Antigravity ajanları. **Go-live kapsamı buradaki her şeyi HARİÇ tutar.**
> **Antigravity ajanları için (ZORUNLU):** Buradaki maddeler, onaylı bir module pack açıkça bu backlog'dan çıkarıp `approved`/`ready-for-dev` kapsamına almadıkça **UYGULANMAZ**. Bir backlog özelliğini "yardımcı olayım" diye kendiliğinden inşa etmek YASAKTIR. Bir talep bir backlog maddesine değiyorsa, kod yazmadan önce bu dosyayı referans göster ve module pack kapısına yönlendir.
> **Son güncelleme:** 2026-08-28 — kapanmış kayıtlar `docs/product-backlog-closed.md`'ye taşındı; her kayda DURUM/SAHİP alanı eklendi. Kuralı aşağıda.


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
Bir iş bitince, o turda: `DURUM: KAPANDI` yazılır **ve** kayıt `docs/product-backlog-closed.md`'ye taşınır.

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

> *Bu bölümün kayıtlarının tamamı KAPANDI ve `docs/product-backlog-closed.md`'ye taşındı.*

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

> *Bu bölümün kayıtlarının tamamı KAPANDI ve `docs/product-backlog-closed.md`'ye taşındı.*

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
- **Yapım tetikleyicisi:** **Blueprint'e (`docs/System Capability & Implementation Blueprint - master 7.xlsx`) bakılarak, org-model buna uygunsa yapılacak** — aksi halde mevcut tiplerle temsil devam.
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
- **"Hiçbir yerde yazılı değil" bayat:** `docs/workcenter-onboarding-sop.md` var — sıralı zincir, sessiz-hata tablosu, kabul listesi.
- **Ama zincir KODDA hâlâ ifade edilemiyor:** `ModuleManifestDocument`'ta bağımlılık/önkoşul alanı YOK. Platform genelinde onboarding/hazırlık ön-kontrolü yok (yalnız altyapı sağlık kontrolleri).
- ⚠ **Manifest sağlayıcıları bu kaydı KAPATMAZ:** `TaskManifestProvider` ve `WorkAggregationManifestProvider` 2026-07-25'te eklendi — kaydın 2026-08-11 ölçümünden ÖNCE. (CONTROL TOWER önce bunun aksini varsaydı; ölçüm düzeltti.)
- **YENİ BOŞLUK — kaydın bilmediği:** iki kiracı ana-veri yüzeyi sonradan geldi — `TASK_TYPES` ve `TASK_DOCUMENT_LIST`. SOP ikisinden de HİÇ bahsetmiyor. Yani düzyazı zincir artık koddan geri.

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
  · (b) `docs/dev-environment.md`'ye "yeni worktree kurunca `npm ci` koş" adımı — ucuz, disipline bağlı
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

### BL-329 — Görev detayında aynı yere giden İKİ "kaynak kaydı" düğmesi (2026-09-02, CANLI, ölçüldü)

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

> **DURUM:** AÇIK · **SAHİP:** SAHİPSİZ

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

**Karar sahibe ait, çünkü sınır sorusu:** MOD-0024 raporlar ve devreder, kaydı
modül sahiplenir ([[project_mod0024_approval_boundary]]) -- SAP/Oracle'da da gelen
kutusu *eylem* verir, nesne kendi uygulamasında düzenlenir. Önerim: yeteneği Görev
Merkezi'ne taşımak yerine devri görünür kılmak, yani rayda `/Tasks/{id}/Edit`'e
giden bir "Düzenle" bağlantısı. Yeni ekran yok, yeni sınır ihlali yok.

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
