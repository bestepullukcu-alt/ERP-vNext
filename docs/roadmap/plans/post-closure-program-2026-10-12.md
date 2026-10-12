# Kapanış sonrası program — sıra, İK sayfa haritası ve bekleyenler

**Durum:** PLAN · **Tarih:** 2026-10-12 · **Sahibi:** Control Tower · **Onay:** sahip (2026-10-12, "zamanı gelince buradan bakıp ona göre çalıştırırız")
**Çalışma şekli:** tek sohbet, sahibin yönlendirmesiyle. CT her adım için prompt'u, hangi sohbeti (yeni ya da mevcut), modeli ve effort'u verir. Sayfa sayfa gidilir; her sayfada eksikleri ve o sayfanın backlog kayıtlarını CT çıkarıp sahibe sorar.

> Bu belge ileriye dönük bir plandır. Kapanış bittikçe ve sayfalar ölçüldükçe güncellenir.

---

## 1. Sıra

1. **Kapanış:** açık işlerin bitmesi, hat doğrulaması, tek PR. Bu belge yazılırken sürüyor.
2. **Stok ekibine "hazır"** (2–3 gün; başka bir ekip bekliyor). Bkz. §2.
3. **Yöneticinin programı:** Görev Merkezi, doküman yönetimi, toplantı, görev, toplantı raporu (tutanak), zaman çizelgesi ve bunların doğru çalışması için gereken İK modülleri.
   - Önce **1 günlük durum ölçümü**: her alanda ne var, ne eksik, ne bozuk; İK'dan tam olarak ne gerekiyor. Sonunda büyüklük ve sırayla tek plan çıkar; sahip yöneticisiyle onaylar.
   - Beklenen sıra:
     1. İK temeli (§3);
     2. görev ve Görev Merkezi;
     3. toplantı ve tutanak;
     4. zaman çizelgesi;
     5. doküman yönetimi.
4. **Paralel (ayrı worktree, ayrı sohbet):** oturum kapatma güvenlik işi (§4.1).
5. **Sahiple konuşulacaklar:**
   - MOD-0290'ın sırası (P6 GTIN, P9 ambalaj, İTS / barkod modülü BL-616);
   - iş ortağı modeli (BL-620);
   - Driver.js sayfa turları;
   - backlog önceliklendirmesi.

## 2. Stok ekibine "hazır" için kalanlar

Kaynak: `docs/roadmap/plans/mdm-product-master-completion-plan-2026-10-02.md` §4a (stok ekibiyle uzlaşma, S0–S7).

| Kalan | Büyüklük | Not |
|---|---|---|
| S2 kalem etkinleştirme onayı: hatla birleşim | orta | kapanışın içinde |
| S6 birim çevrimi kayıtlar arası tutarlılığı (çelişen oranlar, aynı boyut, hassasiyet) | küçük–orta (1–2 prompt) | tek kayıt kuralları S3'te var |
| MDM yetki manifesti adımı (`mdm.product-master.read` + stok anahtarları; Platform listesi 38 → 41) | küçük (1 prompt) | BL-592 |
| S7 dev örnek verisi + stok ekibine "hazır" bildirimi | küçük | veriyi sahip girer, adımları CT verir |
| BL-585 MDM'i stok varlığı ucuna bağlama | küçük–orta (1–2 prompt) | Stok ekibi ucu verdi (2026-10-12): `GET http://127.0.0.1:5067/api/internal/inventory/stock-presence?skuId=…&skuLevel=Item|Gsku|Lsku|FinishedGood`; başlıklar `X-Tenant-Id` + `X-Internal-Api-Key: ditenmdmservice:<sır>`; `Authorization` GÖNDERİLMEZ (kullanıcı belirteçli istek reddedilir). Sır sahibin parola yöneticisinde; sahip MDM user-secrets'a kendisi girer, CT sırsız komut verir; hiçbir dosyada / günlükte / mesajda geçmez. 5067 servis port bandının (5011–5060) dışında → port listesine eklenir. |

Yapılmış olanlar: S0 sözleşme, S1 kalem çekirdeği, S3 stok davranışı alanları, S4 okuma sözleşmesi, S5 Kalemler sayfası + GSKU / LSKU / Bitmiş Ürün "Stok davranışı" panelleri, stok izin anahtarları (BL-573).

## 3. İK sayfa haritası (Görev Merkezi / toplantı / zaman çizelgesi için)

Ölçüm: Blueprint master 8.1 (`docs/reference/blueprint/…master 8.1.xlsx`, Module Pages + Blueprint_Data) ve `origin/main` (2026-10-12).

| Sayfa | Neden gerekli (örnek) | Blueprint | origin/main'de |
|---|---|---|---|
| Çalışan kaydı | Göreve atanan, toplantıya katılan kişi buradan gelir | MOD-0251 | `HCM/Employees` (gmg-10, 2026-07) |
| Organizasyon birimi | ekip / departman; "Ekibim" görünümü | MOD-0288 | `Organization/OrganizationUnits` (CT, 2026-09-21) |
| Pozisyon | onaycıyı pozisyonla bulmak ("Kalite Müdürü onaylar") | MOD-0288 | `Organization/Positions` (CT) |
| Pozisyon ataması / yönetici hiyerarşisi | kim kime bağlı; yönetici ekibinin görevlerini görür | MOD-0299 | ⚠ **İki tane:** `Organization/PositionAssignments` (CT) + `HumanCapital/PositionAssignments` (Cihan, 2026-09-14). Hangisinin kalacağı kararı gerekli. |
| Vekâlet | izindeki yöneticinin onayları vekile | MOD-0288 Delegation & Assignment Matrix | sayfa görünmüyor; ölçülecek |
| Çalışma takvimi + resmi tatiller | bitiş tarihi iş günüyle; tatile toplantı konmaz; beklenen saat | Platform çalışma takvimi | `Platform/WorkingCalendars`, `WorkingCalendarImports`, `WorkingCalendar/Overrides` (CT, 2026-08-28) |
| İzin / devamsızlık | izindekine görev atanırken uyarı / vekâlet; toplantıda "izinli"; zaman çizelgesinde izin günü | MOD-0280 + MOD-0315 | kısmen: `HumanCapital/TimeAttendanceLeave` (Cihan, 7 dosya); ne kadarı çalışıyor, ölçülecek |
| İzin bakiyesi + izin talebi | talep onaya gider (Görev Merkezi) | MOD-0315 Leave Balance View | görünmüyor |
| Vardiya / çalışma planı | vardiyalı çalışanın beklenen saati, toplantı zamanı | MOD-0315 Timesheet / Roster | **yok** |
| Çalışan / yönetici self servis | Görev Merkezi ile çakışır (Approval Inbox) | MOD-0319 | yok |
| İşe alıştırma, çalışan projeksiyonları | dolaylı | MOD-0303 | `HumanCapital/EmployeeOnboarding`, `EmployeeProjections` (Cihan) |

**Sahibin notu:** main'deki İK sayfalarının bir kısmı başka geliştiricinin ve %100 bitmiş olmayabilir. Ölçümde her sayfa açılıp şunlar kontrol edilir:
- gerçek mi taklit mi veri;
- kaydetme;
- yetki (UAS-001);
- 7 dil.

**İK temeli sırası:**
1. çalışan;
2. organizasyon / pozisyon;
3. pozisyon ataması (yinelenme kararıyla);
4. çalışma takvimi;
5. izin;
6. vardiya.

## 4. Bekleyen backlog (kapanıştan sonra, sırayla)

### 4.1 Paralel worktree: oturum kapatma (büyük, 4+ prompt)
- BL-587: pasife alınan ya da parolası sıfırlanan kişinin açık oturumu 2 saate kadar çalışıyor.
- BL-532, BL-463: yetki değişikliği geç yansıyor.
- Kullanıcılar + Roller kapanışının madde 1'i: 401'de isteği yenileyen ortak katman (~80 dosya) ve kısa belirteç (`min(15, SessionTimeoutMinutes)`).

### 4.2 Tek sohbet, sırayla: Kullanıcılar / Roller / Rol İzinleri

| Sayfa | Ne görülür (düzelmediyse) | Kayıt | Büyüklük |
|---|---|---|---|
| Kullanıcılar | "admin@diten.com" her ortamda kodda sabit parola özetiyle oluşturuluyor | BL-556 / BL-534 | orta (canlı kararı sahibin) |
| Kullanıcılar | yönetici olmayan birini silerken de "son yönetici silinemez" | BL-558 | küçük |
| Kullanıcılar | e-postası olmayan kullanıcıya parola bağlantısı ulaşmıyor | BL-618 | küçük |
| Kullanıcılar | silme / pasife alma reddi ham kod; "Parolayı sıfırla" eski parolayı geçersiz kılmıyor; liste hatasında İngilizce uyarı | BL-450 / BL-529 / BL-515 | düzeltilmiş görünüyor, sahip kontrolüyle kapatılacak |
| Roller | admin olmayan yöneticiye onay izni verilemiyor | BL-506 | küçük (takeover dalından taşıma) |
| Roller | kalite / onaycı rolü kiracı ekranından kurulamıyor (Doküman Yönetimi izinleri platform kapsamlı, bazı izinler katalogda yok) | BL-582 | orta |
| Roller | firma yöneticisinin rol işlemleri platform hesabının yetkisini daraltabiliyor | BL-550 | orta |
| Rol İzinleri | yetkisiz kişi sayfa iskeletini görüyor | BL-617 | küçük |
| Rol İzinleri | grup başlıkları "mod0251 / person / lookups" kod olarak | BL-507 / BL-342 | küçük |
| Rol İzinleri | İngilizce kalan satır / düğme ("Read All") | BL-508 | küçük (7 dil) |
| Rol İzinleri | "Görevi Güncelle" tek izni 14 işlemi açıyor | BL-341 | orta: önce çipe "kapsadıkları" ipucu, sonra 3–4 izne bölme (düzenle / yürüt / incele / yapı) |
| Rol İzinleri | katalogdan silinen modülün izinleri listede kalıyor | BL-339 | küçük |

### 4.3 Diğer
- BL-619: eski Görev Merkezi (`/WorkCenter`, taklit) kaldırılır; aynı denetimde kullanılmayan / yarım sayfalar listelenir.
- BL-620: iş ortağı kaydı yok (kimlik ve izinli kiracılar elle GUID). Karar: modeli tamamla ya da rolü kaldır.

Büyüklük ölçüsü:
- küçük: 1 prompt, yaklaşık yarım gün;
- orta: 2–3 prompt, 1–2 gün;
- büyük: 4 ve üzeri prompt, 3–5 gün.

## 5. Toplantı adımı için notlar (2026-10-12)

Yöneticinin programının "toplantı ve tutanak" adımında bu notlarla başlanır. Ayrıntılı görüşme notu sahipte (Toplantı Türleri Değerlendirmesi sayfası).

### 5.1 Toplantı türleri
- İki liste var:
  - Eylül: görev türü kataloğunun "MeetingTypes" sayfası, 23 tür, işe göre.
  - Ekim: yöneticinin 12 Ekim listesi, 33 tür, biçime göre.
- Öneri: ikisi birleşir (34 tür, gruplu). Yönetimin Gözden Geçirmesi, Kalite Kurulu, Değişiklik Kontrol Kurulu ve Sapma / CAPA Değerlendirmesi kalır. Mülakat çıkar (İK işe alım). Birebir tek tür ve gizli.
- Aksiyon görev türü: 32 yeni tür açılmaz.
  - Kalite işleri mevcut kalite türlerine bağlanır: Değişiklik Kontrolü, Sapma, CAPA, Kalite Risk Değerlendirmesi, Eğitim Ataması.
  - Geri kalanlar Eylül kataloğunun birim bazlı aksiyon türlerine bağlanır. Bu türler görev türü kataloğu yüklenince gelir.
- Yöneticiye sorulan 8 soru görüşme notunda. Cevaplar gelince bu bölüm güncellenir.

### 5.2 Toplantı modülünün eksikleri (ölçüm: hat `def9acdc9`)

| Eksik | Büyüklük | Öncelik |
|---|---|---|
| Gündem maddesi başına not ve sonuç (tutanakta bugün yalnız katılım, kararlar, görevler var) | orta | liste yüklenmeden önce |
| Kalite kaydı türlerinde zorunlu gündem maddesi (ISO 9001 §9.3.2) | küçük–orta | liste yüklenmeden önce |
| Gizli toplantı türü ("tüm toplantıları okuma" yetkisi görmez) | küçük–orta | liste yüklenmeden önce |
| "Katılım zorunlu" kuralı (herkes işaretlenmeden yayın yok) | küçük | liste yüklenmeden önce |
| Kalite kaydı / e-imza işaretleri etkin değilken tür ekranında uyarı | küçük | liste yüklenmeden önce |
| Yayımlanan tutanağın katılımcılara gönderilmesi | küçük | kısa vade |
| Tek toplantının tutanak çıktısı (PDF) | orta | kısa vade |
| Tutanağı e-imzaya bağlama. **İmza altyapısını başka geliştirici yapıyor**; bizim payımız bağlamak. | küçük–orta | altyapı hazır olunca |
| Kalite kaydı tutanağının silinmezliği ve saklanması | küçük | kısa vade |
| Ön okuma paketi ve ekler (görev eki deposu yeniden kullanılır) | orta | kısa vade |
| Gündem maddesine sunan kişi ve süre | küçük | kısa vade |
| Dış katılımcı (önce saklama kararı) | orta | kısa vade |
| Türe özel tutanak alanları (görev türüne alan bağlama ile aynı altyapı) | büyük | ileride |
| Kurullar, yeter sayı, ayrı karar defteri (Blueprint MOD-0359, MOD-0007) | büyük | ileride |
| Google Takvim / Meet | büyük | ileride |

Ayrıca, toplantı türündeki üç işaret bugün hiçbir şey yapmıyor: "kalite kaydı", "e-imza" ve "katılım zorunlu" yalnız kaydediliyor.

### 5.3 Görev türü ↔ toplantı türü
- **Toplantı türü → görev türü:** toplantıdan görev açılınca önceden seçili gelen tür; kullanıcı değiştirebilir. Bu bir kilit değil, önerilen değer.
- **Görev türü → toplantı:** görev türü ekranında "İnceleme toplantısı: İzin verilmez / İsteğe bağlı / Zorunlu" var. "Zorunlu" ise bağlı toplantının tutanağı yayımlanmadan görevde karar verilemez. Kod `a9c40ee0` (2026-09-15); canlı denemesi bu adımda yapılır.

### 5.4 Yöneticinin isteği: görev / toplantı verirken kişinin zamanı var mı?
- **Bugün:** yalnız kişinin kendi plan görünümü var. Kendi bloğu bir toplantıyla çakışınca ya da mesai dışına düşünce uyarı alıyor.
- **Eksik:** atayan ya da düzenleyen, başkasının boş / meşgul durumunu göremiyor; izin kontrolü yok.
- **Öneri:** üç kademe; engel değil uyarı. Gizli toplantının konusu sızmasın diye yalnız "meşgul" gösterilir, detay gösterilmez.
  1. Atama / davet anında uyarı: başka toplantı, plan bloğu, tatil; İK izin modülü gelince izin. Küçük–orta.
  2. Kişi seçicide iş yükü ("12 açık görev, bu hafta 4'ü bitiyor") ve yönetici için ekip görünümü. Orta.
  3. Google takviminden boş / meşgul. Google entegrasyonundan sonra. Büyük.
- **Hazır alternatif:** rutin işte görevi havuza / pozisyona atamak; müsait olan alır.
- **Yöneticiye soruldu:**
  - kademe 1 yeterli mi, kademe 2 de isteniyor mu?
  - rutin işte havuza atama kullanılsın mı?

### 5.5 Yöneticinin iki sorusu (2026-10-12)

**1. Toplantı oluştururken gündem ve tekrar ayarlanamıyor, sonradan yapılıyor.**
- Ölçüm (hat `def9acdc9`): oluşturma formunda başlık, tür, başlangıç / bitiş, yer, açıklama, devam ettiği toplantı ve katılımcılar var. Oluşturma isteğinde gündem alanı yok (`CreateMeetingRequest`).
- Türün gündem şablonu kayıttan SONRA, detay sayfası ilk açılınca uygulanıyor (`Meetings/form.js:195-197`, `?prefillAgenda=1`).
- Tekrar (seri) ayrı bir sayfa: `/Meetings/Series`.
- Sonuç: davet e-postası gündemsiz gidiyor.
- Outlook / Google Takvim ve Oracle Unifier'da gündem ve tekrar oluşturma formunun içinde.
- **Öneri:**
  - Oluşturma formuna gündem bölümü: türden dolar, kaydetmeden önce düzenlenir, aynı işlemde yazılır. Küçük–orta.
  - "Tekrarla" seçeneği: arkada seri ve ilk toplantı açılır; seri sayfası yönetim için kalır. Orta.

**2. Görev Merkezi'nden birden çok görev için tek inceleme toplantısı.**
- Ölçüm: inceleme toplantısı görevin "…" menüsünden ve yalnız o görev için açılıyor (`scheduleReviewMeeting`, `ScheduleReviewMeetingForTaskCommand(TaskId, …)`).
- Görev Merkezi'nde çoklu seçim yok: 2026-08-24'te yarım olduğu için kaldırıldı (`app.js:11705`).
- Bugünkü geçici yol: bir görevden toplantı açılır, toplantı detayında "mevcut görevi bağla" ile diğerleri eklenir (`linkExistingTask`). Toplantı ↔ görev bağı zaten çoka çok.
- **Değerlendirme: mantıklı.** Haftalık ekip / kişi gözden geçirmesi yaygın. Eylül kataloğunda "Görev Gözden Geçirme" türü var. Oracle Unifier ve Veeva'da bir toplantıda birçok madde, her birine ayrı karar yazılıyor.
- **Şartlar:**
  1. Seçilen her görev, göreve bağlı ayrı bir gündem maddesi olur.
  2. Tutanakta her görevin kendi sonucu yazılır ("onaylandı / düzeltme istendi / ertelendi"). Gündem maddesi başına not ve sonuç (§5.2'nin ilk satırı) buna bağlı.
  3. "İnceleme toplantısı zorunlu" türündeki görevde kapı, tutanağın yayımlanmasına değil, o görevin kendi maddesinin sonucuna bakar. Yoksa 10 görevli bir toplantının tutanağı, konuşulmayanların da kilidini açar.
  4. Düzenleyenin her görevde yetkisi olmalı (görev yazma kapısı, atama kapsamı); yetkisi olmayan görev listeye girmez.
  5. Katılımcı önerisi: seçilen görevlerin sahipleri ve incelemeci.
  6. Üst sınır: 20 görev.
- **UI önerisi:** genel çoklu seçimi geri getirmek yerine inceleme toplantısı penceresine "başka görevler ekle" seçicisi. Varsayılan liste: aynı kişinin ya da ekibimin açık görevleri. Çoklu seçim + toplu işlem çubuğu ayrı ve daha büyük iş.
- **Büyüklük:** orta (2–3 prompt), §5.2'nin ilk satırından sonra.

### 5.6 Yöneticinin cevabı: Toplantı Oluşturma Kataloğu (2026-10-12)

Yönetici değerlendirmemize kataloğun yeni bir sürümüyle cevap verdi: 23 ana tür altında 46 alt tür.
- **Alanlar:** başlık şablonu, tür / alt tür, varsayılan aksiyon görev türü, süre, tekrar, katılımcılar, yer / bağlantı, açıklama şablonu, devamı olduğu toplantı kuralı.
- **Ekranda doldurma:** başlık, tür, yer, açıklama ve katılımcılar şablondan gelir. Başlangıç kullanıcının seçimi; bitiş = başlangıç + süre.

**Önerilerimizden kabul edilenler:**
- iki liste birleşti;
- Yönetimin Gözden Geçirmesi, Kalite Kurulu, Sapma / CAPA Değerlendirmesi, Değişiklik Kontrol Kurulu geri geldi;
- aksiyon türleri Eylül'ün birim bazlı kodları oldu;
- Mülakat çıktı.

**Bizim modele oturuşu:**
- Alt tür = bugünkü `MeetingType` satırı; ana tür = yeni bir gruplama alanı (seçicide gruplu görünür).
- Yeni alanlar: başlık şablonu, varsayılan süre, varsayılan tekrar, varsayılan yer, varsayılan katılımcılar, açıklama şablonu, devamı olduğu toplantı kuralı.
- Kalanlar: gündem şablonu, varsayılan aksiyon türü, kalite kaydı, e-imza, katılım zorunlu. Eklenen: gizli.

**Eksik ya da düzeltilmesi gerekenler:**
1. **Bu katalogda gündem şablonu ve kalite / e-imza / katılım / gizlilik sütunları yok.** Önceki iki listede vardı. Kalmalı: gündem satırları önceki listelerden alınır; işaretleri QA doldurur.
2. **Katılımcılar rol olarak yazılmış** ("CEO, Directors", "QA, SMEs").
   - Otomatik doldurmak için her rol bir organizasyon pozisyonuna bağlanır; pozisyonda kim varsa o davet edilir.
   - "Employee", "Individual Manager" gibi kişiye göre değişen roller toplantı açılırken seçilir.
   - Dev kiracısında pozisyon verisi az (BL-356).
3. **Kodlar.** Üç kod Eylül kataloğundaki 113 görev türünde YOK: `COM-ACTION`, `FIN-ACTION`, `ITG-ACTION`. `BCM-TEST` "iş sürekliliği tatbikatı"; gerçek kriz aksiyonu için yanlış tür.
4. **İkili değerler.** 5 satırda iki aksiyon türü var (ör. "CORP-ACTION / MFG-ACTION"). Alan tek varsayılan tutar; biri seçilir, kullanıcı görev açarken değiştirebilir.
5. **Tekrar değerleri.**
   - "Monthly / Quarterly" gibi ikililerden biri varsayılan seçilir.
   - "As needed", "Per release", "Before launch", "After implementation", "Per audit" tekrar değil, tek seferlik.
   - "Daily" için seri sıklığına günlük eklenmeli. Bugün yok: `MeetingSeriesFrequency` = haftalık / aylık / üç aylık / yıllık.
6. **Başlık şablonundaki yer tutucular.**
   - Tarihten gelenler kendiliğinden dolar: `{Period}`, `{Week}`, `{Month}`, `{Year}`, `{Date}`, `{Quarter}`. Seride her toplantı için ayrı hesaplanır.
   - Diğerlerini (`{Manager}`, `{Project}`, `{Topic}`, `{Record}`…) kullanıcı kaydetmeden önce doldurur.
7. **"Devamı olduğu toplantı kuralı"** alan olarak var ama değerleri verilmemiş. Öneri: "yok / serinin bir önceki toplantısı / kullanıcı seçer". Tekrarlayan türlerde varsayılan "bir önceki"; açık aksiyonlar zaten kendiliğinden taşınıyor.
8. **Listeden düşenler:**
   - Tedarikçi Değerlendirmesi (GxP kalite toplantısı);
   - İSG Kurulu (50+ çalışanlı işyerinde yasal zorunluluk, 6331 sayılı Kanun);
   - Bilgi Güvenliği Olayı Değerlendirmesi (ISO 27001);
   - Eğitim Toplantısı;
   - Satış ve Operasyon Planlama.
   - Bilerek mi çıkarıldı, sorulmalı.
9. **İki birebir türü kaldı:** Operasyonel Birebir (iş takibi) ve İK Birebir (kişisel). Davranış farkı gizlilik olmalı: İK Birebir gizli.
10. **Ekran eşlemesinde gündem ve tekrar yok.** İkisi de oluşturma formunda dolmalı (§5.5 madde 1).

**Yapılacaklar (toplantı adımında):**
| İş | Büyüklük |
|---|---|
| Tür alanları (ana tür, şablonlar, süre, tekrar, yer, katılımcı pozisyonları, kural, gizli) + tür ekranı, 7 dil | orta |
| Oluşturma formunda otomatik doldurma: başlık yer tutucuları, bitiş = başlangıç + süre, pozisyondan katılımcı, gündem, tekrar | orta |
| Seri sıklığına günlük | küçük |
| Kataloğun tek seferlik yüklenmesi (46 satır). Önce görev türü kataloğu yüklenmeli; kodlar ona bağlı. | küçük |

Toplam yaklaşık 6–9 prompt, 4–6 iş günü. §5.2 (tutanak eksikleri) ve §5.5 ile birlikte planlanır.

**Yöneticinin "haftalık yönetici gözden geçirmesi" için seçtiği satır:** Operasyonel Birebir, haftalık, 30 dk, `CORP-ACTION`. Uygun. Çoklu görev incelemesi (§5.5 madde 2) tam bu toplantıya oturur: gündeme kişinin açık / geciken görevleri gelir, her birine ayrı sonuç yazılır.

### 5.7 Yöneticinin kararları (2026-10-12 akşamı) — bağlayıcı

| Konu | Karar |
|---|---|
| Model | Ana tür + alt tür. Eylül'ün kalite / birim toplantıları korunur, Ekim'in genel toplantıları eklenir, aynı davranıştakiler birleştirilir. |
| Görev türü kataloğu (Eylül, 113 tür) | Onaylı; **önce o yüklenir**. Toplantı aksiyon kodları ona bağlı. |
| Aksiyon türleri | 32 yeni tür açılmaz. Mevcut kalite türleri + birim bazlı aksiyon türleri. Çift kodlu satırda tek varsayılan; kullanıcı görev açarken değiştirebilir. |
| Eksik kodlar | `COM-ACTION`, `FIN-ACTION`, `ITG-ACTION` görev türü kataloğuna eklenir. Alternatif: `CRM-ACTION`, `FIN-CLOSE`, `ITG-CHANGE`. |
| Kriz toplantısı | `BCM-TEST` değil. `GRC-ACTION` ya da `CORP-ACTION`; gerçek olay varsa olay görevi. |
| Geri eklenen türler | Tedarikçi Değerlendirmesi, İSG Kurulu, Bilgi Güvenliği Olayı Değerlendirmesi, Eğitim, Satış ve Operasyon Planlama. |
| Kalan türler | Yönetimin Gözden Geçirmesi, Kalite Kurulu, Değişiklik Kontrol Kurulu, Sapma / CAPA kesin kalır. Karar ve Onay → tek "Karar / Onay Toplantısı". |
| Mülakat | Toplantı türü değil, İK işe alım modülünün işi. Geçici kullanılırsa gizli. |
| Birebir | Operasyonel Birebir (iş takibi) ve İK Birebir (gizli) ayrı alt türler. |
| Tekrar | Tek varsayılan; diğerleri seçenek. |
| Devamı olduğu toplantı kuralı | Yok / serinin bir önceki toplantısı / kullanıcı seçer. |
| Katılım zorunlu | Her davetli için katıldı / katılmadı / mazeretli seçilmeden tutanak yayımlanmaz. |
| Gündem | "Owner", "Due date", "Action items" gündemde tekrar edilmez (sistem alanı). |
| Kim doldurur | Gündemleri iş sahibi / yönetici, kalite ve e-imza işaretlerini QA, rol → pozisyon eşlemesini İK / organizasyon yöneticisi. Davet pozisyon bazlı. |
| QA sorumlusu | Bir QA süreç sahibi atanır. **İsmi sahip / yönetim verecek** (açık). |
| Yükleme öncesi 5 yazılım işi | Evet, önce: gizlilik, zorunlu katılım, gündem notu / sonucu, işaretlerin dürüst gösterimi, zorunlu gündem maddesi. |
| Kişinin zamanı | Faz 1 yalnız uyarı (başka toplantı, izinli, yoğun), engellemez. Faz 2: kişi seçerken açık görev sayısı ve bu hafta biten görev sayısı. |
| Rutin işler | Kişi yerine pozisyon / havuz üzerinden atanır (özellik var; kullanım kuralı). |
| Çoklu görev incelemesi | Her görev için ayrı karar / sonuç satırı şart. |

**Toplantı adımının sırası (bu kararlarla):**
1. Görev türü kataloğunun yüklenmesi + üç eksik kod (`COM-ACTION`, `FIN-ACTION`, `ITG-ACTION`). Katalog faz 1: içe aktarıcı ve tür sütunlarının ilk dalgası. Orta.
2. Yükleme öncesi 5 yazılım işi (§5.2 "liste yüklenmeden önce" grubu). Orta, toplam 3–5 gün.
3. Ana tür / alt tür ve varsayılan alanlar; oluşturma formunda otomatik doldurma (gündem, tekrar, başlık yer tutucuları, süre, pozisyondan katılımcı); günlük tekrar. Orta, 4–6 prompt.
4. Toplantı kataloğunun yüklenmesi. Gündemler (iş sahibi), işaretler (QA) ve pozisyon eşlemesi (İK) tamamlanınca. Küçük.
5. Çoklu görev incelemesi (görev başına sonuç; kapı görevin kendi sonucuna bakar). Orta.
6. Kişinin zamanı faz 1 (uyarı); faz 2 iş yükü sonra.
7. E-imza bağlama: imza altyapısını yapan geliştirici hazır olunca.

**Sahipten beklenen:** QA süreç sahibinin adı.
