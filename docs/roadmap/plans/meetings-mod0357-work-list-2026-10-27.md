# Toplantılar — kapsamlı iş listesi (2026-10-27)

> **Durum: TASLAK — öneri, onay değil.** Bu belge kod, paket, kuyruk ya da dağıtım izni vermez. Modül:
> Toplantılar = Yönetim Gözden Geçirme ve Toplantı Döngüsü (MOD-0357). Hazırlayan: CT · Toplantılar.
> Ölçüm tabanı: `origin/main` `826150ef4` (2026-10-27). **Hiçbir şey çalıştırılarak denenmedi**; her
> "bugün" cümlesi kod ya da belge okumasıdır. Çıkarımlar "(çıkarım)" diye işaretlidir.

## Kısa liste — insanlar kullanmaya başlasın diye (öneri — onay değil)

Modül bugün kullanılabilir durumda. Aşağıdaki **A** grubu, kullanıma açmadan önce kapanması gerekenlerdir: bir
güvenlik açığı, canlıda çalışmayan işler ve yanıltan ekranlar. **B** grubu ilk kullanım ayında, kullanıcı geri
bildirimiyle yapılır. Geri kalan her şey (**C**) bekleyebilir; ayrıntısı §5'tedir.

**A — kullanıma açmadan önce (yaklaşık 6–8 prompt + sahip kontrolleri, 1–2 hafta)**

| # | İş | Neden şart | Kim |
|---|---|---|---|
| A1 | Canlı ortamda arka plan işlerini kontrol etmek ve açmak (BL-536) | kapalıysa tekrarlayan toplantılar hiç üretilmez, gönderilemeyen davet e-postası yeniden denenmez (ilk gönderim çalışır) | sahip (canlı ayar) |
| A2 | Canlı kiracıda organizasyon kökü ve pozisyonlar var mı bakmak (BL-366) | pozisyonu olmayan kişi toplantı açamaz ve davet edilemez | sahip / İK |
| A3 | Raporda, görmeye yetkisi olmayana görevin başlığı görünmesin (BL-541; Dalga 1.1) | güvenlik açığı | geliştirme (görev tarafı başka modül → sorulur) |
| A4 | Tür ekranındaki üç işaret ("kalite kaydı", "e-imza", "katılım zorunlu") bugün çalışmıyor: "henüz etkin değil" yazsın (Dalga 3.4) | kullanıcı imzalandığını ya da kalite kaydı olduğunu sanmasın | geliştirme |
| A5 | İki kişiyle canlı deneme: davet, kabul / ret, değişiklik ve iptal e-postası (Dalga 1.3) | bu kısım hiç canlı denenmedi | CT |
| A6 | Başlangıç için 4–6 toplantı türü elle (ör. Ekip toplantısı, Operasyonel Birebir, Yönetimin Gözden Geçirmesi, Karar / Onay Toplantısı) | 46 satırlık katalog başka sahiplerin verisini bekliyor; tür yoksa form boş | yönetici seçer, CT girer |
| A7 | Ekranda anlatım: Toplantılar sayfalarına "?" yardım metni (7 dil) ve Toplantı tanıtım turu (Driver.js). Kural UXH-001 (`.antigravity/rules/page-help-and-keyboard.md` §5–6) Toplantı'yı tur yapılacak **beş akıştan biri** sayıyor; bugün turu yok, 7 toplantı sayfasının yedisi de yardım borç defterinde (`docs/roadmap/backlog/page-help-ledger.md:134-140`) | kullanıcı modülü ekrandan öğrensin | geliştirme + l10n (2 prompt) |
| A8 | Canlıya veri girişi: organizasyon / pozisyon, kullanıcıların pozisyonu, rollere `platform.meetings.*` izinleri, e-posta (SMTP), başlangıç türleri. Eylül rehberi var (`docs/guides/operations/go-live-2026-09-meetings-task-engine.md` §3–§7); tür ve görev türü kataloğu satırları eklenip tazelenir | veri yoksa ekran boş ya da kilitli | CT hazırlar, sahip / İK girer |

Google Meet beklemeden kullanılabilir: Meet ya da Teams bağlantısı "Yer" alanına yapıştırılırsa davet dosyasına
(`.ics`, LOCATION satırı) girer (kod okundu, denenmedi).

**B — ilk kullanım ayında (yaklaşık 6–9 prompt)**

| # | İş | Neden |
|---|---|---|
| B1 | Gündem maddesi başına not ve sonuç (Dalga 3.3) | tutanağın asıl eksiği; B2'nin temeli |
| B2 | Onay toplantısı: bir toplantıda birden çok görev, görev başına sonuç, kapının görevin kendi sonucuna bakması (Dalga 6.1–6.3) | sizin örneğiniz; bugün konuşulmayan görevin de kilidi açılıyor |
| B3 | Takvimde görev kartında "bağlı toplantı", toplantı kartında "bağlı görevler" (Dalga 7.1) | Görev Merkezi ile toplantı arasındaki bağ ekranda görünsün |
| B4 | Katılım zorunlu kuralı (Dalga 3.2) | kalite toplantıları için |
| B5 | Gizli toplantı türü (Dalga 3.1) — **İK birebiri kullanılacaksa A'ya çıkar** | gizli konuşmanın konusu sızmasın |

**Toplantı türleri — yöneticinin "Meeting Creation Catalogue" listesi (2026-10-27'de yeniden iletildi)**

- Liste 23 ana tür / 46 alt türdür; 12 Ekim'de değerlendirilen listeyle aynıdır (plan §5.6). **İki kademe (ana tür +
  alt tür) yönetici kararıdır** (plan §5.7, 2026-10-12). Bugün kodda tek kademe var (`MeetingType` düz liste).
- Listede başlangıç / bitiş saklanmaz; varsayılan süre saklanır, kullanıcı tarihi seçer, bitiş = başlangıç + süre.
  Bu, Dalga 4.1–4.2'dir.
- Liste ile 12 Ekim kararları arasında farklar (yüklemeden önce düzeltilir):
  1. Kriz toplantısı `BCM-TEST` gösteriyor; karar `GRC-ACTION` ya da `CORP-ACTION`.
  2. "Decision Meeting" ile "Approval Meeting" ayrı; karar tek "Karar / Onay Toplantısı".
  3. Geri eklenen beş tür listede yok: Tedarikçi Değerlendirmesi, İSG Kurulu, Bilgi Güvenliği Olayı, Eğitim, S&OP.
     (Görev türü kataloğu v3.1'in MeetingTypes sayfasında ilk dördü ve S&OP var.)
  4. İK Birebir gizli olmalı (B5 gerekir); Mülakat yok (doğru, İK işe alımda).
  5. `COM-ACTION`, `FIN-ACTION`, `ITG-ACTION` görev türü kataloğu v3.1'de **yok** (130 tür ölçüldü).
  6. Katılımcılar unvan ("CEO, Directors"); pozisyon eşlemesi İK'dan gelir. "Line Manager / Employee" gibi kişiye
     göre değişenler toplantı açılırken seçilir.
  7. "Daily" tekrar bugün yok; "Monthly / Quarterly" gibi çift değerde tek varsayılan; "Before launch / Per release"
     olaya bağlı → tek seferlik.
- Görev türü kaynağı: CEO'nun 25 Eylül yanıtı (Platform_Review_Answers) v3.1'in "doğrudan içe aktarılmasını"
  onaylıyor. Ayrıca gelen `gorev-turleri-ve-kapanis-sonuclari.xlsx` farklı kodlar kullanıyor (`QMS-DEV`, `QMS-CAPA` …)
  ve kendini "öneri, bağlayıcı değil" diye tanıtıyor → kaynak olarak v3.1 alınır, bu dosya kapanış sonuçları için
  girdi sayılır (çıkarım; sahip doğrular).

**C — bekleyebilir:** tür modeli ve form otomatik doldurma, katalog yükleme, kişinin zamanı uyarıları, PDF,
tutanak dağıtımı, ekler / ön okuma, e-imza, Google Meet / Teams entegrasyonu, dış katılımcı, oda, yapay zekâ,
kurullar, karar defteri, kalite modülü bağlantıları (§5 Dalga 2, 4, 5, 8–11).

---

## 0. Okunan kaynaklar

| Kaynak | Ne için |
|---|---|
| `docs/records/decisions/2026-09/ADR-003-meetings-are-mod-0357-now-and-live-in-platform.md` | kimlik, barınma, Aşama 1 / ertelenenler |
| `execution/domains/management-governance/module-packs/MOD-0357-management-review-cadence.md` (§2, §7, §19–§23) | dilimler, Faz 2, sahip kararları |
| `docs/roadmap/plans/post-closure-program-2026-10-12.md` §5 | yöneticinin 12 Ekim kararları ve eksik listesi |
| `docs/roadmap/backlog/product-backlog.md` | açık toplantı maddeleri (BL-…) |
| Blueprint 8.8 `Blueprint_Data` (MOD-0357, MOD-0007, MOD-0359 ve kalite / GRC satırları) | modülün tam kapsamı ve komşuları |
| `execution/registries/module-id-registry.md` | komşu modüllerin durumu |
| Kod: `Features/Meetings`, `Features/Tasks/Services/ReviewMeeting*`, `Features/WorkAggregation/Calendar`, `Features/TimeEntry/Adapters/MeetingGatewayAdapter.cs`, `Domain/Entities/Meetings` | bugünkü bağlantılar |
| SAP / Oracle karşılaştırması (alt ajan, Sonnet, effort orta; 3 web araması) | §4 — **çoğu hücre doğrulanmadı** |

**Kısmi:** `management-governance/domain-config.md` ve sahipteki "Toplantı Türleri Değerlendirmesi" görüşme notu
okunmadı. SAP / Oracle tarafında yalnız Oracle Unifier tutanak belgesi, Veeva QMR yardım sayfası ve SAP Sales Cloud
Teams entegrasyonu kaynakla doğrulandı.

---

## 1. Bugün elimizde ne var (kısa)

Toplantı kaydı, davet (Görev Merkezi'nde kabul / ret + `.ics` e-posta), toplantıdan görev (öncesi, sırası, tutanak),
görevden inceleme toplantısı, tutanak (katılım, kararlar, taslak → yayın, gerekçeli düzeltme), devam toplantısı
(açık aksiyonlar gündeme taşınır), tekrarlayan seri, toplantı türü (gündem şablonu, varsayılan aksiyon görev türü,
üç işaret), toplantı raporu ve aksiyon kaydı, liste ↔ takvim görünümü, Görev Merkezi takviminde toplantılar.

Paketin 15 dilimi: 14'ü ana dalda. Canlı doğrulama dilimi (S10) **yarım**: iki kullanıcı gerektiren davet kabul / ret,
değişiklik ve iptal e-postaları "koşulmadı" (paket §19 S10). Hiçbir dilimin kabul kaydı yok.

---

## 2. Fazlar — ne yapıldı, ne kaldı

### 2.1 Aşama 1 (ADR-003 §5) — yapıldı, eksik kalanlar

| Aşama 1 maddesi | Durum |
|---|---|
| Toplantı kaydı, katılımcı, gündem | yapıldı |
| ERP içi davet + `.ics` e-posta | yapıldı; iki kullanıcıyla canlı denenmedi (S10) |
| Toplantıdan görev: önce / sırası / tutanak | yapıldı |
| Görevden toplantı (inceleme toplantısı) | yapıldı; tek görev için |
| Tutanak, düzeltme sürümü | yapıldı; **gündem maddesi başına not / sonuç yok** |
| Devam toplantısı | yapıldı |
| Erteleme / iptal bildirimi | yapıldı (çıkarım: kod var, canlı denenmedi) |
| Toplantı türü ayarı (kalite kaydı, e-imza, katılım zorunlu) | alanlar var; **üç işaret hiçbir şey yapmıyor** (plan §5.2) |
| 7 dil, yetki, kiracı sınırı | yapıldı |
| Tekrarlayan seri (sonradan Aşama 1'e alındı) | yapıldı; **canlıda arka plan işleri kapalı** (BL-536) → seri üretmez |

### 2.2 Faz 2 ve ertelenenler (ADR-003 §6, paket §20) — hiçbiri yapılmadı

| Madde | Bugün | Not |
|---|---|---|
| **Google Takvim / Meet** | yok. Yer alanı serbest metin; kişinin takvimine düşmenin tek yolu `.ics` e-posta | Faz 2. Teams de yok |
| İki yönlü takvim (Google / Outlook'tan iptal, saat değişikliği geri gelsin) | yok | SAP'de bile tek yönlü olabildiği not edildi (§4) |
| Tutanağa e-imza | yok; alan yalnız kaydediliyor | e-İmza Servisi (MOD-0022) başka geliştiricide, "in-progress" |
| Dosya ekleri / ön okuma paketi | yok | Belge İkili Deposu (MOD-0262-FU01) "ready-for-dev" |
| Dış katılımcı (kiracı kullanıcısı olmayan) | yok | önce saklama / gizlilik kararı (paket §21) |
| Dış katılımcıya görev | yok | |
| Oda / kaynak rezervasyonu | yok | |
| Yapay zekâ aksiyon önerisi | yok | GxP'de insan onayı şart |
| Kurumsal Strateji "Strategic Reviews" ekranlarının bu motora bağlanması | yok | |
| Karar satırının Karar Defteri'ne (MOD-0007) bağlanması | yok | MOD-0007 yapılmadı |
| Toplantı türünün Kurul kaydına (MOD-0359) bağlanması | yok | MOD-0359 yapılmadı |

### 2.3 Blueprint'in tam kapsamına göre (MOD-0357 "Soft Pages")

| Blueprint sayfası | Bugün |
|---|---|
| Management Cadence Calendar | var (Toplantılar takvimi) |
| Review Cycle Register | kısmen (seri sayfası) |
| Review Instance Workspace | var (toplantı ayrıntısı + tutanak) |
| Agenda Builder | kısmen (gündem listesi; sunan kişi, süre, madde sonucu yok) |
| **Review Pack Builder** (gözden geçirme paketi) | **yok** |
| **Governance Forum Workspace** | **yok** (MOD-0359'a bağlı) |
| Review Outcomes & Decision Links | kısmen (kararlar tutanakta düz satır) |
| Follow-up Register | var (rapor + aksiyon kaydı) |
| **Cadence Analytics** | **yok** (raporda katılım oranı var, döngü analizi yok) |

---

## 3. Diğer modüllerle bağlantı

Bağ kaydı (`RecordLink`) kaynak ve hedef **modül kodu** taşıyor; yani genel. Bugün yalnız görevlerle (MOD-0024)
kullanılıyor. Başka modüllere bağlamak için yeni tablo gerekmez (çıkarım: kod alanları buna izin veriyor, başka
modülle hiç denenmedi).

| Modül | Bugün | Eksik | Sahip / not |
|---|---|---|---|
| **Görevler (MOD-0024)** | çoka çok bağ: hazırlık · gündemde · toplantıdan doğdu · inceleme toplantısı | görev ayrıntısında toplantıdan doğan görevin başlığı okuma kuralına sorulmadan gösteriliyor (BL-541, açık) | aynı servis; MOD-0024 dosyasına dokunmak başka modül sayılır (XMC-001) |
| **Görev onayı — "inceleme / onay toplantısı" kapısı** | görev türü "inceleme toplantısı zorunlu" ise, bağlı bir toplantının tutanağı yayımlanmadan görev tamamlanamaz ve incelemeye gönderilemez | **(1)** kapı tutanağın yayımına bakıyor, görevin kendi sonucuna değil → 10 görevli toplantı, konuşulmayan görevlerin de kilidini açar · **(2)** toplantı penceresi tek görev için açılıyor; birden çok görevi tek toplantıya almak elle · **(3)** tutanakta görev başına sonuç ("onaylandı / düzeltme istendi / ertelendi") yok | plan §5.5, §5.7: "her görev için ayrı sonuç şart" |
| **İş akışı onayı (MOD-0023)** | toplantıyla hiçbir bağ yok; kapı bilerek iş akışına dokunmuyor | bir onay adımının "toplantıda karar verildi" diye kapanması; tutanak onayı iş akışsız (yayınla / kilitle) | ADR-003 §2: ilk aşamada iş akışsız; ileride karar gerekir |
| **Görev Merkezi takvimi** | kişinin görevleri + toplantıları **yan yana**; kabul çakışma uyarısı var | görev kartında "bağlı toplantı", toplantı kartında "bağlı görevler" **yok**; Toplantılar takviminde görevler yok (yalnız "bağlı görevi olanlar" süzgeci). BL-480'in 4 küçük hatası açık (çok günlü toplantı çizilmiyor, düzenleyen değişince görünmüyor, davet kartı görünüm kaybı, uyarı yalnız bazı sayfalarda). BL-026 açık | takvim bileşeni Görev Merkezi'nin (MOD-0024 WC) |
| **Zaman çizelgesi (MOD-0280-FU01)** | tutanaktaki katılım zaman çizelgesine okunuyor | — | çalışıyor (kod) |
| **İzin (MOD-0280, İK izin CT'si)** | yok | davette / görev atamada "izinli" uyarısı | İzin ve devamsızlık CT'si |
| **Çalışma takvimi + resmi tatil (MOD-0362)** | toplantı tarafında hiç kontrol yok | tatile / mesai dışına toplantı uyarısı | plan §5.4 "kişinin zamanı" faz 1 |
| **Organizasyon / pozisyon (MOD-0288)** | düzenleyen ve katılımcı aktif pozisyon ister | pozisyondan katılımcı önerme (tür ayarı); yeni kiracıda kök birim yok → toplantı açılamaz (BL-366) | |
| **e-İmza (MOD-0022)** | yok | yayımlanan tutanağın imzalanması (anlamı: gözden geçirdim / onayladım) | başka geliştirici; bizim payımız bağlamak |
| **Doküman / kanıt (MOD-0262, MOD-0029, MOD-0031)** | yok | ön okuma paketi, ekler, kontrollü dokümana salt okunur bağ | MOD-0262-FU01 hazır olunca |
| **Karar Defteri (MOD-0007)** | kararlar tutanakta düz satır | toplantılar arası aranabilir karar listesi | MOD-0007 yapılmadı |
| **Kurullar (MOD-0359)** | tür ayarı yerine geçiyor | yeter sayı, üyelik, kurul toplantısı | MOD-0359 yapılmadı |
| **Kalite: Sapma (MOD-0207), CAPA (MOD-0208), Değişiklik Kontrolü (MOD-0209), Şikâyet (MOD-0211), Denetim (MOD-0214)** | bu modüller kayıt defterinde yok (yapılmadı) | gündem maddesinin bu kayıtlara bağlanması; yönetim gözden geçirmesine (ISO 9001 §9.3.2) girdilerin kendiliğinden gelmesi | modüller gelince; bugün yalnız görev türleriyle |
| **Proje (MOD-0117)** | yok | "Toplantı raporları" alt modülü planlı ama numarası yok | portföy domain'i |
| **S&OP onayları (MOD-0190)** | yok | aylık S&OP toplantısı + onay | tedarik zinciri domain'i |
| **Bildirim / e-posta** | davet, değişiklik, iptal postaları | kurumsal e-posta kabuğu (BL-454); masum değerlerin "[REDACTED]" yazılması (BL-377) | altyapı CT |
| **Rol izinleri ekranı** | — | "Minutes" gibi İngilizce kalan düğme adları (BL-508) | |

---

## 4. SAP / Oracle ve benzerleri ne yapıyor

> **Güven:** alt ajan yalnız 3 web araması yaptı. Kaynaklı satırlar: Oracle Primavera Unifier tutanak belgesi,
> Veeva QualityOne QMR yardım sayfası, SAP Sales Cloud V2 Teams entegrasyonu. Diğer hücreler genel bilgidir ve
> "doğrulanmadı" sayılır. Karar vermeden önce kritik satırlar ayrıca doğrulanmalı.

| Yetenek | SAP | Oracle | Diğerleri | Bizde |
|---|---|---|---|---|
| Çevrim içi toplantı bağlantısı | Sales Cloud V2: randevuda "Teams toplantısı" anahtarı (kaynaklı) | takvim eşitleme; bağlantı üretimi doğrulanmadı | Teams / Meet yerleşik | yok |
| Müsaitlik (meşgul / boş) | Outlook eşitleme (doğrulanmadı) | doğrulanmadı | Outlook, Google "zaman bul" | yalnız kendi planla çakışma uyarısı |
| Oda / kaynak | ayrı uygulama (doğrulanmadı) | doğrulanmadı | Outlook / Google oda takvimi | yok |
| Dış katılımcı | müşteri kişileri katılımcı (kaynaklı) | doğrulanmadı | e-postayla | yok |
| Gündem maddesi başına sonuç | doğrulanmadı | Unifier: her tutanak satırı ayrı kayıt ve durum (kaynaklı) | Veeva: şablondan madde (sonuç doğrulanmadı) | **yok** |
| Satırdan aksiyon | belge akışıyla takip (doğrulanmadı) | Unifier: her satırdan aksiyon (kaynaklı) | Veeva: QMR görev kaydı | var (karar → görev) |
| Açık maddeleri sonraki toplantıya taşıma | doğrulanmadı | Unifier'da yapılandırma ister; Primavera Contract Mgmt taşıyor | | **var** (devam toplantısı) |
| Tutanak onay akışı | doğrulanmadı | Unifier: iş akışı (kaynaklı) | Veeva: sonlandırma adımlı akış | yayınla / kilitle (akışsız) |
| e-imza (Part 11) | doğrulanmadı | doğrulanmadı | Veeva Vault'un çekirdeği (QMR için doğrulanmadı) | yok |
| Ek / ön okuma | doğrulanmadı | doğrulanmadı | Veeva: Vault belgesine bağ (kaynaklı) | yok |
| Karar defteri | doğrulanmadı | satır = kayıt | | tutanakta düz satır |
| Yeter sayı / kurul | büyük olasılıkla yok | büyük olasılıkla yok | Veeva QMR'da görülmedi | yok |
| Yapay zekâ özet / aksiyon | doğrulanmadı | doğrulanmadı | Teams Copilot, Meet "not al" | yok |
| Mobil | var (doğrulanmadı) | doğrulanmadı | var | duyarlı web (çıkarım) |

**Karşılaştırmadan çıkan sonuç (çıkarım):** Görev ile toplantıyı iki ayrı kayıt ve aralarında bir bağ olarak tutmak
(ADR-003) SAP, Oracle ve Veeva çizgisiyle uyumlu. Açık maddeleri sonraki toplantıya taşımada bizim modül Unifier'dan
ileride. En belirgin üç açık şunlar: gündem maddesi başına sonuç, ön okuma paketi ve e-imza. Onlardan sonra çevrim
içi toplantı bağlantısı geliyor.

---

## 5. Kapsamlı iş listesi

Sütunlar: **Tür** = Belge · Arka uç · API · Ekran · Yapılandırma · Kapı. **Prompt** = geliştirme prompt'u sayısı,
**tahmin** (bir dilim ≈ 1–2). **Kimi bekliyor** = başlamadan önce gereken karar ya da sahip.

### Dalga 0 — kayıt düzeni (kod yok, CT işi)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 0.1 | Paketi bağımsız incelemeye vermek (kayıt yok; SOP §20.4) | Belge | inceleme 1 tur | sahip: başlatma onayı |
| 0.2 | Kimlik kaydında "planned" → paketin gerçek durumuyla eşitlemek; paket başlığındaki "status: draft" cümlesini düzeltmek; S12 satırını "teslim" yapmak | Belge | 0 (CT) | inceleme sonrası |
| 0.3 | Toplantılar iş kuyruğu ve kayıt satırı | Yapılandırma | 0 (CT) | sahip onayı (`kuyruk-onay`) |
| 0.4 | İş takip sayfası (CT-TRK-01) | Belge | 0 (CT) | sahip `sayfa-ac` |

### Dalga 1 — önce düzeltilecekler (hata, güvenlik, canlı)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 1.1 | Rapor ve dışa aktarım, toplantıdan doğan görevin başlığını / sorumlusunu görev okuma kuralına sorsun (BL-541) | Arka uç | 1–2 | görev tarafı başka modül → sor (XMC-001) |
| 1.2 | Takvim küçükleri: çok günlü toplantı, düzenleyen değişimi, davet kartı görünümü, uyarının her sayfada çıkması (BL-480) | Ekran | 1–2 | Görev Merkezi bileşeni → sor |
| 1.3 | İki kullanıcıyla canlı doğrulama: davet kabul / ret, değişiklik ve iptal postaları, `.ics` (S10 kalanı) | Kapı | 1 | dev kiracısında ikinci kullanıcı |
| 1.4 | Canlıda arka plan işleri kapalı → seri, hatırlatma, e-posta yeniden denemesi çalışmaz (BL-536) | Yapılandırma | 0–1 | **sahip: canlı ortam değişkenleri** |

### Dalga 2 — görev türü kataloğu (plan sırası 1)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 2.1 | Görev türü kataloğunun (113 tür) yüklenmesi + `COM-ACTION`, `FIN-ACTION`, `ITG-ACTION` (ticari, finans, BT aksiyonu) | Yapılandırma + Arka uç | 2–3 | **başka modül (Görevler, MOD-0024)** → kimin yapacağı sorulur |

### Dalga 3 — "liste yüklenmeden önce" 5 yazılım işi (plan sırası 2; yönetici onayladı)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 3.1 | Gizli toplantı türü ("tüm toplantıları oku" yetkisi görmez; takvimde yalnız "meşgul") | Arka uç + Ekran | 1–2 | — |
| 3.2 | Katılım zorunlu: herkes katıldı / katılmadı / mazeretli işaretlenmeden tutanak yayımlanmaz | Arka uç + Ekran | 1 | — |
| 3.3 | Gündem maddesi başına not ve sonuç (3.5 ve Dalga 6'nın temeli) | Arka uç + Ekran | 2 | — |
| 3.4 | Kalite kaydı / e-imza işaretleri etkin değilken tür ekranında dürüst uyarı | Ekran | 1 | — |
| 3.5 | Kalite kaydı türlerinde zorunlu gündem maddesi (ISO 9001 §9.3.2) | Arka uç + Ekran | 1 | QA: madde listesi |

### Dalga 4 — tür modeli ve oluşturma formu (plan sırası 3)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 4.1 | Ana tür + alt tür; varsayılan başlık şablonu, süre, tekrar, yer, katılımcı pozisyonları, açıklama şablonu, "devamı olduğu toplantı" kuralı | Arka uç + Ekran | 2 | — |
| 4.2 | Oluşturma formunda gündem ve tekrar (bugün sonradan); başlık yer tutucuları, bitiş = başlangıç + süre, pozisyondan katılımcı | Ekran + API | 2 | — |
| 4.3 | Seri sıklığına "günlük" | Arka uç | 1 | — |

### Dalga 5 — katalog yükleme (plan sırası 4)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 5.1 | Toplantı kataloğu (46 satır) tek seferlik yükleme | Yapılandırma | 1 | gündemler (iş sahibi), işaretler (QA), pozisyon eşlemesi (İK); **QA süreç sahibinin adı (sahip)** |

### Dalga 6 — onay / inceleme toplantısı (plan sırası 5; sizin örneğiniz)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 6.1 | Bir toplantıda birden çok görev: inceleme penceresine "başka görevler ekle" (varsayılan: aynı kişinin ya da ekibimin açık görevleri) | Ekran + API | 1 | görev ekranı → sor |
| 6.2 | Tutanakta her görev için ayrı sonuç: onaylandı / düzeltme istendi / ertelendi | Arka uç + Ekran | 1–2 | 3.3'ten sonra |
| 6.3 | Kapı tutanağın yayımına değil, **o görevin kendi sonucuna** baksın; "düzeltme istendi" görevi geri açsın | Arka uç | 1 | görev kapısı başka modül dosyası → sor |
| 6.4 | (Karar) Bir iş akışı onay adımının (MOD-0023) toplantıda karara bağlanması | Belge (karar) | 0 | sahip: isteniyor mu? |

### Dalga 7 — takvim ve Görev Merkezi bağlantısı

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 7.1 | Görev Merkezi takviminde görev kartında "bağlı toplantı", toplantı kartında "bağlı görevler" | API + Ekran | 1–2 | Görev Merkezi → sor |
| 7.2 | Toplantılar takviminde toplantıdan doğan görevlerin bitiş tarihleri (açılıp kapanan katman) | API + Ekran | 1 | — |
| 7.3 | BL-026'nın kalanı: davet yanıtından sonra Görev Merkezi Ajanda bağlantısı | Ekran | 1 | 7.1 ile birlikte |

### Dalga 8 — kişinin zamanı (plan sırası 6)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 8.1 | Davet / atama anında uyarı (engel değil): başka toplantı, plan bloğu, resmi tatil, mesai dışı | Arka uç + Ekran | 2 | Çalışma takvimi (MOD-0362) okuma |
| 8.2 | "İzinli" uyarısı | Arka uç | 1 | İzin modülü hazır olunca (İzin ve devamsızlık CT'si) |
| 8.3 | Faz 2: kişi seçerken açık görev sayısı / bu hafta biten | Ekran | 1–2 | sonra |

### Dalga 9 — tutanağı tamamlamak (kısa vade)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 9.1 | Yayımlanan tutanağın katılımcılara e-postayla gönderilmesi | Arka uç | 1 | e-posta kabuğu (BL-454) ile uyum |
| 9.2 | Tek toplantının tutanak çıktısı (PDF) | Arka uç + Ekran | 2 | — |
| 9.3 | Kalite kaydı tutanağının silinmezliği ve saklama kuralı (D5) | Arka uç | 1 | — |
| 9.4 | Gündem maddesine sunan kişi ve süre | Ekran | 1 | — |
| 9.5 | Ön okuma paketi ve ekler (Blueprint "Review Pack Builder") | Arka uç + Ekran | 2–3 | Belge İkili Deposu (MOD-0262-FU01) |
| 9.6 | Tutanağı e-imzaya bağlama | Arka uç + Ekran | 1–2 | e-İmza Servisi (MOD-0022) hazır olunca |

### Dalga 10 — Faz 2 ve dış dünya

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 10.1 | **Google Meet bağlantısı** toplantı oluşturulunca (ve/veya **Teams**) | Arka uç + Yapılandırma | 3–4 | **sahip: Google mı, Microsoft mu, ikisi mi?** Kiracı başına hesap / izin kararı |
| 10.2 | Google / Outlook takvimine iki yönlü eşitleme (iptal ve saat değişikliği geri gelsin) | Arka uç | 3–5 | 10.1'den sonra |
| 10.3 | Dış katılımcı (önce saklama / gizlilik kararı) | Arka uç + Ekran | 2 | sahip: hangi bilgi saklanır |
| 10.4 | Oda / kaynak rezervasyonu | Arka uç + Ekran | 2–3 | Google / Outlook oda takvimi mi, kendi kaydımız mı? |
| 10.5 | Yapay zekâ ile tutanaktan aksiyon önerisi (insan onaylı) | Arka uç | 2 | sonra; GxP değerlendirmesi |

### Dalga 11 — Blueprint'in kalan kapsamı ve komşu modüller (ileride)

| # | İş | Tür | Prompt | Kimi bekliyor |
|---|---|---|---|---|
| 11.1 | Kararların Karar Defteri'ne (MOD-0007) bağlanması | Arka uç | 1–2 | MOD-0007 |
| 11.2 | Kurul, yeter sayı, üyelik (MOD-0359 "Governance Forum Workspace") | Arka uç + Ekran | büyük | MOD-0359 |
| 11.3 | Yönetim gözden geçirmesine girdilerin kendiliğinden gelmesi (denetim, şikâyet, CAPA, KPI) | Arka uç | büyük | kalite modülleri (MOD-0207, 0208, 0209, 0211, 0214) |
| 11.4 | Gündem maddesinin Sapma / CAPA / Değişiklik Kontrolü kaydına bağlanması | Arka uç + Ekran | 1–2 / modül | o modüller |
| 11.5 | Döngü analizi (Cadence Analytics): zamanında yapılan toplantı, aksiyon kapanma süresi | Ekran | 2 | — |
| 11.6 | Türe özel tutanak alanları | Arka uç + Ekran | büyük | görev türüne alan bağlama altyapısı |
| 11.7 | Proje (MOD-0117), S&OP (MOD-0190), Strategic Reviews ekranlarının bu motoru kullanması | — | modül başına | o modüllerin sahipleri |

**Toplam (tahmin):** Dalga 1–9 yaklaşık 35–45 prompt; Dalga 10 yaklaşık 12–16; Dalga 11 komşu modüllere bağlı,
tahmin edilmedi. Plan §5.6'nın "6–9 prompt" tahmini yalnız Dalga 4–5'i kapsıyordu.

---

## 6. Açık kararlar (sahip)

1. QA süreç sahibinin adı (plan §5.7; Dalga 5'i bekletiyor).
2. Canlı ortamda arka plan işleri açılacak mı (BL-536; seri ve hatırlatmalar buna bağlı).
3. Görev türü kataloğunu (Dalga 2) kim yapar: bu CT mi, Görevler (MOD-0024) tarafı mı? (XMC-001)
4. Bir iş akışı onay adımı toplantıda karara bağlanabilsin mi (6.4)?
5. Çevrim içi toplantı: Google Meet, Teams ya da ikisi (10.1)?
6. Dış katılımcı için saklanacak bilgi (10.3).
6a. **Yöneticiden beklenen belge (sahip, 2026-10-27).** İçeriği henüz bilinmiyor; iş onu beklemeden sürer. Belge
    gelince paket revizyonu (iki kademeli tür, kod alanı) ve bu listeyle karşılaştırılır; çelişen yer varsa sahibe
    sorulur. Kod alanı notu: toplantı türü RCS-001'e göre İKİSİ DE sınıfı, önek `MTY`; ortak kod servisi (BL-565)
    gelene kadar kodsuz (önerilen).
7. Bu listede önerilen sıra: 0 → 1 → 3 → 6 → 7 → 4 → 2 / 5 → 8 → 9 → 10. Gerekçe: önce hatalar; sonra yöneticinin
   onayladığı 5 iş, çünkü gündem maddesi sonucu (3.3), onay toplantısı (Dalga 6) için şart; takvim bağlantısı
   (Dalga 7) bağımsız ve kullanıcı en çok onu görür. Katalog yükleme (Dalga 2 ve 5) başka sahiplerin verisini bekliyor.
   **Öneri — onay değil.**
