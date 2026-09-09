# Görev Merkezi kullanıma alma SOP'u

> **Sahibi:** CONTROL TOWER · **Yazıldı:** 2026-08-11 · **Kayıt:** [BL-073](./product-backlog.md)
> **İlgili:** [`product-backlog.md`](./product-backlog.md) · [`dev-environment.md`](./dev-environment.md)
> (dev ortam kurulumu — burada **tekrarlanmaz**, işaret edilir) ·
> [`workcenter-completion-plan.md`](./workcenter-completion-plan.md) (iş sırası)

**Neden var:** MOD-0024 çalışıyor, ama bir kiracı onu **açıp kullanamıyor**. Kullanılabilir hâle gelmesi
sıralı bir **ana veri zincirinin** doldurulmasına bağlı ve bu zincir bugüne kadar hiçbir yerde yazılı
değildi. Daha kötüsü: zincirin **her eksik halkası sessiz başarısızlık** üretiyor — hata mesajı yok, boş
liste var. Bu, 2026-08-11 oturumunda üç kez bizzat yaşandı.

**Bu doküman ne DEĞİLDİR:** sessiz başarısızlıkların **çözümü**. Onlar ayrı backlog maddeleri
([BL-072] · [BL-057] · [BL-065]). Burada **belgeleniyorlar**, düzeltilmiyorlar.

### Ölçüm kuralı
Her önkoşulun yanında onu **zorlayan kod** (`dosya:satır`), her sessiz başarısızlığın yanında **eleyen
satır** yazılıdır. Ölçülemeyen her satır açıkça **ÖLÇÜLMEDİ** olarak işaretlidir — tahmin yazılmamıştır.

**Ölçüm ortamı:** `localhost:5011` (Diten.Web) + `localhost:5057` (Platform.API) + Mongo
`diten_personalization_dev`, kiracı `DefaultTenant`, kullanıcı `admin@diten.com`. Rota durum kodları
oturum açılmış bir tarayıcıdan `fetch(..., {redirect:'manual'})` ile ölçüldü.

---

## BÖLÜM 1 — Sıralı ana veri zinciri

**Sıra bağlayıcıdır:** birim olmadan pozisyon, pozisyon olmadan atama açılamaz. Aşağıdaki altı adım
sırayla yapılırsa en küçük kurulum **~15 dakikadır**.

| # | Ne | Nereden (rota) | Kim | Zorunlu mu | Atlanırsa ne olur |
|---|---|---|---|---|---|
| 1 | **Legal Entity** (şirket) | `/LegalEntities` → **200** | BT + Mali işler | ✅ Zorunlu | Birim açılamaz — birim `LegalEntityId` **zorunlu** alan taşır (`Organization/OrganizationUnit.cs:9`) |
| 2 | **Organization Unit** (birim) | `/OrganizationUnits` → **200** | İK | ✅ Zorunlu | Pozisyon açılamaz; ayrıca görev **birim çözülemedi** hatasıyla reddedilir |
| 3 | **Position** (pozisyon) | `/Positions` → **200** | İK | ✅ Zorunlu | Kimseye iş atanamaz — atama pozisyona yapılır |
| 4 | **User** (kullanıcı/login) | `/Users` → **200** | BT | ✅ Zorunlu | İşi **login yapar**; hesabı olmayan kişi görevi göremez, "Tamamla"ya basamaz |
| 5 | **Position Assignment** | `/PositionAssignments` → **200** | İK | ✅ **ASIL BAĞ** | Kişi hiçbir seçicide **görünmez**. Zincirin en çok atlanan halkası budur |
| 6 | **ReportsToPositionId** (yönetici zinciri) | `/Positions` (pozisyon formu) → **200** | İK | ⚠ Bugün opsiyonel, **go-live'da zorunlu** | Kapsam kuralı ve yukarı/aşağı ayrımı çalışmaz ([BL-057] · [BL-023]) |

### Adım 1 — Legal Entity
- **Minimum örnek:** tek şirket, örn. `Örnek A.Ş.`
- **Zorlayan kod:** `OrganizationUnit.LegalEntityId` zorunlu (`Organization/OrganizationUnit.cs:9`);
  `TaskItem.OrganizationUnitId` zorunlu (`TaskItem.cs:86`). Yani **her görev zincir üzerinden bir şirkete
  bağlıdır.**

### Adım 2 — Organization Unit
- **Minimum örnek:** tek birim, `Genel Müdürlük`, üst birim **boş**.
- **⚠ ZORLAYAN KURAL — üst birim AYNI şirkette olmalı:**
  `CreateOrganizationUnitCommandHandler.cs:86` ve `UpdateOrganizationUnitCommandHandler.cs:84` →
  *"Parent Organization Unit must belong to the same Legal Entity."* (HTTP **409**).
  Bu **bilinçli** bir tasarımdır: birim ağacı mali/hukuki gerçeği taşır ve şirket sınırını geçemez.
  (Pozisyon zinciri geçebilir — bkz. adım 6.)
- **⚠ Ölçülen veri kalitesi riski:** dev veritabanında **11 pozisyondan 5'i**, `LegalEntityId`'si **null**
  olan bir birime bağlı. Şirket bazlı kapsam kuralı geldiğinde bu pozisyonlar hiçbir kapsam üretmez.
  Kurulum yaparken **her birimin şirketi dolu olmalı.**

### Adım 3 — Position
- **Minimum örnek:** `Uzman`, birim = `Genel Müdürlük`, **Status = Active**.
- **⚠⚠ EN ÇOK ISIRAN NOKTA — varsayılan `Draft`:** `Position.cs:19` →
  `public PositionStatus Status { get; set; } = PositionStatus.Draft;`
  Pozisyon **Draft** bırakılırsa o pozisyondaki kişi *"Bir kişi"* listesinde **hiç çıkmaz** ve ekranda
  **tek kelime açıklama yoktur** (eleyen satır: `GetTaskAssignmentPersonLookupHandler.cs:80-81`).
- **Ekran:** `~/Views/Organization/Positions/Form.cshtml` (`PositionsController.cs:30`).

### Adım 4 — User
- **Minimum örnek:** `ornek@ornek.com`, davet gönderilir.
- Kullanıcı oluşturma kiracı arayüzünde **var**: `UsersController.cs:45` (`POST /Users/create`), ayrıca
  `disable` / `enable` / `resend-invite` / `reset-password` (`:144-156`).
- **Görev tarafıyla bağı:** `TaskItem.AssigneeUserId` bir **login kimliğidir**. Employee kaydı görev
  atamaya yetmez — bkz. [BL-071].

### Adım 5 — Position Assignment · **ASIL BAĞ**
- **Minimum örnek:** `Uzman` ← `ornek@ornek.com`, `EffectiveFrom = bugün`, `EffectiveTo = boş`.
- **⚠ `EffectiveFrom` bugün veya geçmiş olmalı.** Aralık **yarı-açıktır**:
  `GetTaskAssignmentPersonLookupHandler.cs:62-63` →
  `EffectiveFrom <= now && (EffectiveTo is null || EffectiveTo > now)`.
- **⚠ VE SİSTEM SENİ UYARMAZ:** `CreatePositionAssignmentCommandHandler.cs:33-56` gelecekteki bir
  `EffectiveFrom`'u **reddetmez** — kaydı kabul eder. Kayıt başarıyla oluşur, kişi listede **yoktur**.
  Reddettiği tek şeyler: pozisyon yok (404) · kullanıcı referanslanamaz (404) · aynı aralıkta ikinci bir
  **Primary** atama (409, `:54-56`).
- **Birden çok pozisyon:** kişi birden fazla koltuk tutabilir; seçicide **tek satır** görünür ve
  `AssignmentType` sırasına göre **Primary** olan "ev" pozisyonu seçilir (`:65-72`).

### Adım 6 — ReportsToPositionId (yönetici zinciri)
- **Alan:** `Organization/Position.cs:10` (`Guid? ReportsToPositionId`).
- **Yürüyüş hazır:** `GetManagerChainQueryHandler.cs:22-46` — döngü tespiti, **32 derinlik** sınırı,
  arşiv kontrolü. Aynı yürüyüş ikinci kez `OrgDataScopeResolver.AddManagerChainScopesAsync:191-226`.
- **⚠ Şirket sınırını GEÇEBİLİR — bilerek:** `PositionReferenceGuard.ValidateAsync:8-39` yalnız
  kendine-rapor, varlık, döngü ve derinlik denetler; **tüzel kişi kısıtı yoktur**. Grup CEO'suna başka bir
  şirketteki fabrika müdürünün rapor vermesi bu yüzden mümkündür ve doğrudur.
- **Bugünkü durum (ölçüm, dev):** 11 pozisyondan **2'sinde** dolu (`Muhasebe Md → CFO`,
  `Staff → Manager`) ve **ikisi de tek şirket içinde**. Şirket sınırını geçen zincir **hiç test
  edilmemiş**.
- **Ne zaman zorunlu olur:** [BL-057] (kapsam) ve [BL-023] (yukarı/aşağı) yazıldığında. Bkz. **Bölüm 4**
  — bu veri o günden sonra dekoratif değildir.

---

## BÖLÜM 2 — Sessiz başarısızlık tablosu

Bu bölüm SOP'un **en değerli** kısmıdır: aşağıdaki her satır, kullanıcının hata mesajı **görmediği** bir
durumdur.

| Belirti (kullanıcının gördüğü) | Sebep | Nerede elendi | Çözüm |
|---|---|---|---|
| **"Aradığım kişi 'Bir kişi' listesinde yok"** — liste kısa, sebep yok | Kişinin **aktif pozisyon ataması yok** | `GetTaskAssignmentPersonLookupHandler.cs:60-66` (döngüye hiç girmez) | Adım 5: atama oluştur |
| aynı | `EffectiveFrom` **gelecekte** ya da `EffectiveTo` geçmiş | `:62-63` | Atamanın tarihini bugüne çek |
| aynı | Pozisyon **Draft** (ya da `Active` değil) | `:80-81` | Pozisyonu **Active** yap (varsayılan Draft, `Position.cs:19`) |
| aynı | Pozisyon **veya birim arşivli** | `:80` (pozisyon) · `:89` (birim) | Arşivden çıkar ya da yeni pozisyon aç |
| aynı | Atama **iptal** (`IsCancelled`) | `:61` | Yeni atama oluştur |
| **"Bir havuz" listesi boş** | Aynı kural setinin pozisyon tarafı: arşivli ya da `Active` olmayan pozisyon atlanıyor | `GetTaskAssignmentPositionLookupHandler.cs:65-75` | Pozisyonu **Active** yap; birimin arşivli olmadığını doğrula |
| **"Görev oluşturulamıyor: birim çözülemedi"** (bu **görünür** bir hata) | Kademeli çözüm başarısız: istekte birim yok → atananın pozisyonunun birimi yok → kiracı **kök birimi** yok | `CreateTaskItemHandler.cs:139-155`, `TaskReasonCodes.OrganizationUnitUnresolved` | Adım 2 + 5: kişiye bir birimde aktif pozisyon ver, ya da kiracıya kök birim tanımla |
| **"Son tarih hatırlatması gelmiyor"** | Süpürme işi **varsayılan KAPALI** | `appsettings.Development.json:52-57` → `"Diten.Platform.MOD-0024.TaskDueSoonSweepJob": false` | O anahtarı `true` yap ve Platform.API'yi yeniden başlat |
| aynı | Görevde **hatırlatma süresi seçilmemiş** (`ReminderLeadDays = null`) — bu **kasıtlı**: sistem kimsenin istemediği bir uyarı icat etmez | `SendDueSoonRemindersHandler.IsDue:205` → `if (task.ReminderLeadDays is not { } leadDays …) return false;` | Görev formunda *"Son tarihten önce hatırlat"* seç |
| aynı | Görev **kapanmış** ya da son tarih **geçmiş** (gecikme farklı bir mesajdır) | `IsDue:206-212` | — beklenen davranış |
| **"Yinelenen görev doğmuyor"** | O süpürme de **varsayılan KAPALI** | `appsettings.Development.json:54` → `"…TaskRecurrenceSweepJob": false` | Anahtarı `true` yap, yeniden başlat |
| **"Hiçbir e-posta gelmiyor" (dev)** | Mailpit varsayılan olarak AUTH duyurmuyor, config kimlik gönderiyor → MailKit `NotSupportedException` | Ortam uyumsuzluğu, ürün kusuru değil | **[`dev-environment.md` § Mail](./dev-environment.md)** — burada tekrarlanmaz |
| **"E-postalar yanlış dilde"** | Dil **kiracı kaydından** geliyor, okuyandan değil; kullanıcı başına dil alanı yok | [BL-068] | Ara çözüm: kiracının `Settings.Language`'ini ayarla |
| **"Süpürme temiz koştu ama hatırlatma gelmedi"** | Eski kusur, **düzeltildi** — sayaçlar artık her görevi tam bir kategoriye koyuyor | [BL-065] § A/B | Süpürme log satırına bak: kayıp varsa **Warning** |
| **"Bu görev siz bakarken değişti" — ama kimse değiştirmedi** | Alıcısı çözülemeyen görevde süpürme saatte **2 sürüm** şişiriyor (damgala + geri al) | [BL-065] § EK-F, `SendDueSoonRemindersHandler.cs:105-106` + `:180-181` | Açık kalem; bugün nadir |

> **Ortak desen — ve bu SOP'un asıl mesajı:** yukarıdaki **on bir** satırın **dokuzunda** kullanıcı
> hiçbir hata görmez. Tek görünür hata *"birim çözülemedi"*dir. Bu yüzden kurulum **Bölüm 5'teki kabul
> listesiyle** bitirilmelidir — "kaydettim, olmuştur" yeterli değildir.

---

## BÖLÜM 3 — Opsiyonel yapılandırma (ilk gün karar verilmeli)

Kullanmaya başlamak için **zorunlu değil**, ama sonradan değiştirmek mevcut görevleri etkiler.

### 3.1 Görev alan tanımları (yapılandırılabilir alanlar)
- **Ekran:** `/Tasks/FieldDefinitions` → **200** (`TaskFieldDefinitionsController.cs:23`).
- **Seçenek kaynakları üç türdür:** `Lookup` (kısa kod listesi) · `BusinessReferenceData` (ör. ülke) ·
  `ModuleRecord` (başka modülün kayıtları — ör. organizasyon birimi, pozisyon).
- **Karar gerektiren:** hangi alanlar açılacak ve hangisi **zorunlu** işaretlenecek. Zorunlu işaretlemek
  formda gerçekten kaydı **bloklar** (hem tarayıcıda hem sunucuda) — süsleme değildir.
- **Bugünkü kiracı verisi (ölçüm, dev):** aktif **tek** tanım var — `regulatory.market` ("Pazar",
  BusinessReferenceData/country). Diğer üç test tanımı 2026-08-11'de pasifleştirildi ([BL-070]).

### 3.2 Bildirim şablonları — ⚠ SINIR: uç var, EKRAN YOK
- **Seed durumu (ölçüm, `notification_templates`):** **5 görev olayı × 7 dil = 35 şablon** hazır —
  `platform.tasks.assigned · claimed · duesoon · completed · approvalrequested`, her biri
  `ar · en · es · fr · ru · tr · zh`. Olay tanımları da yayında
  (`notification_event_definitions`, 5 görev olayı, `Status = 1`).
- **⚠ Kiracı KENDİ metnini yazamıyor.** Kiracı-kapsamlı şablon CRUD ve önizleme uçları **var**
  (`NotificationsController.cs:90` · `:137` · `:160` · `:182` · önizleme `:115`) **ama hepsi
  `/api/platform/notifications` altında ve `platform.notifications.templates.*` izniyle korunuyor** —
  yani **Platform Admin operatör** yüzeyi. Kiracı kullanıcısının çağırabileceği bir uç ve bir ekran
  **yoktur**. Kiracıya özel metin isteniyorsa bugün **operatör** girer.
- Mevcut dört ekran (`Views/Platform/NotificationTemplates` · `NotificationEvents` ·
  `NotificationDispatches` · `NotificationSettings`) da Platform Admin ekranlarıdır.

### 3.3 E-posta olayları ve hatırlatma varsayılanları
- Görev başına hangi olayların e-posta üreteceği ve hatırlatma süresi **görev formunda** seçilir ([BL-065]).
- **Varsayılan:** gösterilen olayların hepsi **tikli**; hatırlatma süresi **3 gün** önseçili.
- **Hatırlatma süresi seçilmezse hiç hatırlatma gitmez** (Bölüm 2). Kiracı-geneli bir varsayılan
  ayarı **yoktur** — **ÖLÇÜLMEDİ:** böyle bir ayarın planlanıp planlanmadığı bu turda araştırılmadı.

---

## BÖLÜM 4 — Rol ve sorumluluk

| Adım | Sahip | Neden o |
|---|---|---|
| Legal Entity | **BT + Mali işler** | Hukuki/mali gerçek; şirket kayıt bilgileriyle eşleşmeli |
| Organization Unit · Position · Position Assignment · ReportsToPositionId | **İK** | Org şemasının tamamı tek elden yönetilmeli; parçalı girilirse zincir kopar |
| User (login), servis hesapları, süpürme işlerinin açılması | **BT** | Erişim ve altyapı |
| Alan tanımları, bildirim tercihleri | **Kiracı yöneticisi** | İş kuralı kararı, teknik karar değil |
| Bildirim şablon metni | **Platform operatörü** (bugün mecburen) | Kiracı ekranı yok — § 3.2 |

### ⚠ RİSK — `ReportsToPositionId` zincirini KİM girer, KİM doğrular?

**Bu soru canlıya geçmeden cevaplanmalıdır.**

- **Bugüne kadar:** `ReportsToPositionId` hiçbir davranışı belirlemiyordu — org şemasında bir çizgiydi.
- **[BL-057] ve [BL-023] yazıldıktan sonra:** bu alan **işin kime gideceğini belirleyen veridir**.
  Kapsam kuralının *"raporlama zincirimde altımda"* ayağı ve *"yukarı = talep, aşağı = atama"* ayrımı
  doğrudan bu zinciri okur.
- **Yanlış girilirse ne olur:** iş **yanlış kişiye yönlenir ve kimse fark etmez.** Sonuç bir hata değil,
  **makul görünen bir atamadır** — sistem itiraz etmez, kullanıcı da bir yanlışlık olduğunu anlamaz.
  Bu, Bölüm 2'deki tüm sessiz başarısızlıklardan daha tehlikelidir, çünkü ortada eksik bir liste bile
  yoktur.
- **Cevaplanacak iki soru:** (1) zinciri **kim girer** — İK merkezi olarak mı, yoksa her yönetici kendi
  altını mı? (2) **kim doğrular** — girişten sonra bir gözden geçirme adımı var mı, yoksa ilk yanlış
  atamaya kadar mı beklenecek?
- **ÖLÇÜLMEDİ:** zincir değişikliği için bugün bir onay/denetim akışı olup olmadığı araştırılmadı.
  (Yalnızca `PositionReferenceGuard`'ın **teknik** denetimleri ölçüldü — döngü, derinlik, varlık.)

---

## BÖLÜM 5 — Kabul kontrol listesi

*"Kurulum bitti"* demenin ölçülebilir tanımı. **Her satır tek tıkla doğrulanabilir.** Bölüm 2'nin
gösterdiği gibi "kaydettim, olmuştur" güvenilir değildir.

| ☐ | Doğrulanacak | Nasıl |
|---|---|---|
| ☐ | `/Tasks/Create` açılıyor | Tarayıcı → **200**, form geliyor |
| ☐ | **"Bir kişi" listesinde en az 1 kişi VAR** | `/Tasks/Create` → *Kime* = **Bir kişi** → açılan seçicide en az bir satır; satır *"Ad — Pozisyon — Birim"* biçiminde |
| ☐ | **"Bir havuz" listesinde en az 1 pozisyon VAR** | Aynı ekran → *Kime* = **Bir havuz** |
| ☐ | Görev oluşuyor **ve `organizationUnitId` DOLU dönüyor** | Görevi kaydet → `GET /Tasks/api/{id}` yanıtında `organizationUnitId` boş olmamalı (kademeli çözümün çalıştığının kanıtı) |
| ☐ | Kişiye atanan görev **o kişinin İşlerim listesinde** görünüyor | O kullanıcıyla giriş → `/WorkCenterNext` → *İşlerim* |
| ☐ | Havuz görevi **üstlenilebiliyor** | Havuz görevi aç → o pozisyonu tutan kullanıcıyla `/WorkCenterNext` → *Havuz* → **Üstlen** → görev *İşlerim*'e geçmeli |
| ☐ | **Test e-postası düşüyor** | Dev: Mailpit `http://localhost:8025`. Gerçek: alıcının kutusu. Yardım: [`dev-environment.md`](./dev-environment.md) |
| ☐ | **Süpürme işleri AÇIK** | `appsettings*.json` → `BackgroundJobs:EnabledJobs` içinde iki MOD-0024 anahtarı `true` **ve** `BackgroundJobs:Enabled = true` (base dosyada varsayılan **false**, `appsettings.json:49`) |
| ☐ | Süpürme Hangfire'da **registered** görünüyor | `http://localhost:5057/hangfire` → **200** (dev'de anonim erişim açık, `appsettings.Development.json:48`) → *Recurring jobs* listesinde iki MOD-0024 işi. **ÖLÇÜLMEDİ:** işler kapalıyken listede hiç görünmüyor mu, yoksa görünüp tetiklenmiyor mu — bu turda işler kapalı olduğu için ayırt edilemedi |
| ☐ | Hatırlatma **gerçekten geliyor** | Son tarihi 1 gün sonraya, hatırlatmayı *"1 gün önce"* ayarla → bir süpürme bekle → Mailpit. Log: kayıp varsa satır **Warning** |
| ☐ | Yinelenen kural görev üretiyor | `/Tasks/RecurrenceRules` → **200** → kural oluştur → bir süpürme sonrası görev doğmalı |

---

## BÖLÜM 6 — En küçük çalışan kurulum (kopyalanabilir)

### 6.1 Tek kişilik kurulum — ~15 dakika

```
1. /LegalEntities        → "Örnek A.Ş."
2. /OrganizationUnits    → "Genel Müdürlük"    · şirket = Örnek A.Ş. · üst birim = (boş)
3. /Positions            → "Uzman"             · birim = Genel Müdürlük · Status = ACTIVE  ⚠ varsayılan Draft
4. /Users                → ornek@ornek.com     · davet gönder
5. /PositionAssignments  → Uzman ← ornek@ornek.com
                           EffectiveFrom = BUGÜN  ⚠ gelecek tarih sessizce eler
                           EffectiveTo   = (boş)
6. /Tasks/Create         → Kime = "Bir kişi" → listede "… — Uzman — Genel Müdürlük" GÖRÜNMELİ
                           başlık + bitiş tarihi → Oluştur
```

**Tek doğrulama:** 6. adımda kişi listede görünüyorsa zincirin **beş halkası da** doğrudur. Görünmüyorsa
Bölüm 2'nin ilk beş satırı sırayla kontrol edilir — sebep kesinlikle onlardan biridir.

### 6.2 İkinci kişi + yönetici zinciri

```
7.  /Positions           → "Müdür" · birim = Genel Müdürlük · Status = ACTIVE
8.  /Users               → mudur@ornek.com
9.  /PositionAssignments → Müdür ← mudur@ornek.com · EffectiveFrom = BUGÜN
10. /Positions → "Uzman" formu → ReportsToPositionId = "Müdür"
                           ⚠ zincir şirket sınırını GEÇEBİLİR (PositionReferenceGuard tüzel kişi denetlemez)
                           ⚠ döngü ve 32 derinlik SUNUCUDA engellenir (409)
```

**Bugün ne değişir:** hiçbir görünür davranış. Zincir **bugün okunmuyor**.
**[BL-057] / [BL-023] sonrası ne değişir:** Müdür, Uzman'a **doğrudan atayabilir**; Uzman, Müdür'e
atayamaz — **talep** gönderir.

### 6.3 Çok şirketli kurulumun test edilmeyen köşesi

Kuralın asıl vakası — **şirket sınırını geçen raporlama zinciri** — dev'de **hiç kurulmamış**
(11 pozisyondan 2'sinde zincir var, ikisi de tek şirket içinde). [BL-057] yazılmadan önce üç satırlık bir
test zinciri kurulmalıdır:

```
Örnek A.Ş.      → "CEO"
Örnek Poland    → "Genel Müdür"  · ReportsToPositionId = CEO        ← şirket sınırını GEÇER
Örnek Poland    → "Fabrika Md"   · ReportsToPositionId = Genel Müdür
```

Beklenen: CEO, Fabrika Md'ye iş verebilmeli **((2) zincir ayağıyla, (1) şirket ayağıyla değil)**; Örnek
A.Ş.'deki bir muhasebeci **verememeli** — Poland'da kimse ona rapor vermiyor.

---

## Bu SOP'un kapsamadıkları

- **Sessiz başarısızlıkların düzeltilmesi** — [BL-072] (aday elenme ipucu) · [BL-057] (kapsam) ·
  [BL-065] § EK-F (sürüm şişmesi). SOP onları **belgeler**, çözmez.
- **Dev ortam kurulumu** (Mailpit, RabbitMQ, servis portları) — [`dev-environment.md`](./dev-environment.md).
- **Son kullanıcı kılavuzu** (görev nasıl kullanılır) — [BL-074],
  [`workcenter-user-guide.md`](./workcenter-user-guide.md).
- **İş sırası** — [`workcenter-completion-plan.md`](./workcenter-completion-plan.md).

## Ölçüm dökümü

| | Sayı |
|---|---|
| Ölçümle yazılan önkoşul / sessiz başarısızlık / rota | **16** |
| **ÖLÇÜLMEDİ** işaretli | **3** — (a) kiracı-geneli hatırlatma varsayılanı var mı · (b) Hangfire'ın kapalı işi listeleyip listelemediği · (c) zincir değişikliği için onay/denetim akışı olup olmadığı |
