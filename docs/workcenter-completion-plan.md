# WorkCenter bitirme planı

> **Sahibi:** CONTROL TOWER · **Onaylandı:** 2026-08-01
> **Kaynak:** 2026-07-31 canlı test turu (8 oturum) + backlog mutabakatı
> **İlgili:** [`docs/product-backlog.md`](./product-backlog.md) ·
> [`docs/workcenter-test-sequence.md`](./workcenter-test-sequence.md) ·
> [MOD-0024 pack](../execution/domains/platform-shared-services/module-packs/MOD-0024-task-engine-create-runtime.md)

Bu dosya **sırayı** tutar. Maddelerin gövdesi backlog'da; burada yalnız **ne, neden o sırada, kime
bağlı** yazılıdır. Sıra değişirse burası güncellenir — iki yerde iki sıra tutulmaz.

---

## Neden bu sıra — üç kısıt

1. **Dil paketi yeni ekranlardan önce gelir.** Aşama 1 tenant tablolarına 7 dil bağlıyor. Yeni bir
   yönetim ekranı ondan önce yapılırsa İngilizce metinlerle **doğar** ve aynı kusuru üretiriz.
2. **Platform girişi yeni ekranların canlı doğrulanmasını açar.** `TASKS` yetkilendirmesi olmadan
   yönetim ekranları menüde görünmüyor (Alan Tanımları bugün tam olarak bu yüzden görünmüyor).
3. **Dokümantasyon en sonda.** Kılavuz ekranları anlatır, UX turu ekranları değiştirir. Bu tek nokta
   pazarlık dışıdır.

---

## AŞAMA 1 — Yüzey dürüstlüğü · ajan

Hepsi "ekran doğruyu söylemiyor" sınıfı, hepsi aynı dosyalarda; tek derleme-doğrulama döngüsü.

| İş | Bugünkü hâli | Kayıt |
|---|---|---|
| Aramayı yerelden bağımsız katlamaya çevir (aksan ayırma + I/İ/ı/i ortak form) | `KAPANIŞ` → **0 sonuç**; `kapanış` çalışıyor | BL-044 |
| Çip tıklanınca segment sayaçları yeniden hesaplansın (faceted) | Çip "SLA riski 3", liste 2 satır; 3.'sü Bekleyen'de ve görünmüyor | BL-045 |
| Terminal durumda SLA rozeti dursun, kapanış anına donsun | "Tamamlandı · 11g gecikmiş" — yarın 12 olacak | BL-046 |
| Tablo dil paketi (7 dil) + tenant tarafındaki diğer tabloların kapsamını ölç | "Showing 1 to 9 of 9 entries" | BL-047 |
| Doğrulama mesajındaki ham alan adı — önce ölç: sebep-kodu köprüsü çözülünce kapanıyor mu | "'Request Title', 200 karakterden…" | BL-048 |
| Detaydaki ham GUID'i kaldır ya da destek katmanına taşı | "Kaynak kaydı 31a44983-…" | BL-049 |

---

## AŞAMA 1'' — Atama kapsamı · ajan · **UX turundan ÖNCE** · 2026-08-11 eklendi

Sıraya sonradan girdi ve **UX turunun önüne** kondu. İkisi de bir gerekçeye dayanıyor:

| İş | Neden burada | Kayıt |
|---|---|---|
| Şirket (Legal Entity) kapsam kuralı — atama · havuz · onaycı ayrımı | 🔴 **Temel, ekran işi değil.** Kişi seçicisi bugün şirket süzmüyor: `GetTaskAssignmentPersonLookupHandler.cs:51-53` üç `GetAllAsync`. Poland AB/GDPR, Turkey değil — hukuki. Ayrıca sonradan eklenen bir kapsam kuralı o güne kadar yazılmış her sorguyu yeniden gözden geçirtir. | BL-057 |
| "Aday sessizce eleniyor" ipucu | **Aynı turda olmak ZORUNDA:** kapsam gelince "neden listede yok"un cevabına *"farklı şirkette"* eklenir; ayrı turda aynı metin iki kez yazılır. | BL-072 |
| "Ekibim" kapsam seçici + yukarı/aşağı yön ayrımı | Header'a **yeni bir KONTROL** ekliyor (`Ben ▾ / Ekibim`). UX turu kontrollerin nasıl görüneceğine karar veriyor — önce dili kurup sonra kontrol eklemek header'ı **iki kez** elden geçirmektir. Bu, aşağıdaki *"Create'ten önce liste"* notunun aynı gerekçesi. Ayrıca yön tespiti BL-057'nin zincir yürüyüşünü kullanır: tek yürüyüş, iki soru. | BL-023 |

> **⚠ Sıra değişikliği:** BL-023 daha önce **Aşama 4b**'de ve *"UX turundan sonra"* yazılıydı. Bu bölüm
> onun yerini aldı; 4b artık buraya işaret ediyor.
> **Bu aşamanın önkoşulu değil ama go-live'ın önkoşulu:** gerçek org şeması (`ReportsToPositionId`).
> Dev'de 11 pozisyonun 2'sinde dolu ve ikisi de tek şirket içinde — şirket sınırını geçen zincir hiç test
> edilmemiş. Ayrıntı BL-057'de.

---

## AŞAMA 1' — UX/UI turu · SAHİP · Aşama 1 ile PARALEL

**Sona bırakılmaz.** Çip ve SLA rozetinin görünümü Aşama 1'de değişiyor; tur sonra yapılırsa aynı yer
iki kez elden geçer. Ayrıca Aşama 5 ve 6 bu turun çıktısını bekliyor.
**Ama Aşama 1''den SONRA:** BL-023 header'a bir kontrol ekliyor ve o kontrol bu turda değerlendirilmeli.

Kapsam: test dokümanının **8. oturumu** (7 dilde metin taşması · RTL · koyu tema · dar ekran) +
**segment ↔ çip görsel ayrımının keskinleştirilmesi** (BL-017) + genel yerleşim/tipografi/yoğunluk.

Sayfa sırası ve her sayfada nelere bakılacağı: **§ UX tur sırası** (aşağıda).

---

## AŞAMA 2 — Tasks yönetim alanını erişilebilir yapmak · SAHİP + CT

1. **`TASKS` modülünü Platform Admin Tenant'a yetkilendir** — `/platform/login` gerekiyor, **sahipte**
2. Menü temizliği: 4 çift bağlantı sil · eksik 1 manifest sayfa kaydı ekle · HCM'in katalogsuz sayfası ayrı iş
3. Alan Tanımları'nın menüde göründüğünü canlı doğrula (CT)

> ⚠ **Zincirin darboğazı burası.** Bu adım olmadan yeni yönetim ekranları menüde görünmez ve canlı
> doğrulanamaz. Erkene alınması önerilir.

---

## AŞAMA 3 — Platform borcu · ajan · ayrı dilim

WorkCenter'a değil **her modüle** dokunur; WorkCenter paketine sıkıştırılırsa her modülün hata
sözleşmesi habersiz değişir.

| İş | Neden | Kayıt |
|---|---|---|
| Doğrulama hatalarının sebep kodu taşıması | Kullanıcıya çevrilemez İngilizce cümle gidiyor; test turunda **iki kez** çıktı | BL-040 |
| Tarih alanlarının saklanma biçimi | `DateTimeOffset` dizi olarak saklanıyor, sıralama kırıyor; bir oturumda **3 kez** ısırdı | BL-030 |
| Eksik aksiyonlar — hangileri gerçekten gerekli (**ürün kararı**) | `decline`·`reject`·`dispute`·`delegate`·`pause`·`replan`·`logTime` yok; hepsi "işin yolunda gitmediği" durumlar | BL-034 |
| Bilgi talebinde "kimi bekliyorum" seçimi | Gerekçe yazılıyor, muhatap seçilemiyor | BL-036 |
| İki karar: "kaynak modülde oluştur" kalsın mı · ölü toplu-seçim bağlansın mı silinsin mi | İkisi de kullanıcıya boş yol gösteriyor | BL-037 · BL-039 |

---

## AŞAMA 4 — Eksik ekranlar · ajan

### 4a. Yinelenen görev kuralı ekranı — BL-052
Motor **tam** (sıklık/aralık/süre/şablon/atama, saatlik süpürme, dönem başına tam bir kez), ama
**ekran yok**: kural ancak API çağırarak oluşturulabiliyor.

Alan Tanımları ekranının deseni kopyalanır (golden DataTable + tam sayfa form). Formda:
sıklık (Günlük·Haftalık·Aylık·Çeyreklik·Yıllık) + aralık · başlangıç/bitiş · isteğe bağlı şablon ·
**kime** (kişi veya havuz — *"kendim" yasal değil*: arka plan işinin "kendi"si yoktur).

**Sıra şartı:** Aşama 1'den sonra (dil) **ve** Aşama 2'den sonra (menüde görünüp doğrulanabilsin).

### 4b. "Ekibim" kapsam seçici — BL-023 → **AŞAMA 1''e TAŞINDI (2026-08-11)**
Yöneticinin ekibinin yükünü görmesi. **CT önerisi: özet görünüm** — yönetici yükü görsün, başkasının
işini kendi listesindeymiş gibi işletmesin.
**Artık burada değil:** madde UX turundan **önceye** alındı — gerekçesi ve yeni yeri **§ AŞAMA 1''**.
Kısaca: header'a kontrol ekleyen bir iş, kontrollerin görünümüne karar veren turdan sonra yapılırsa
header iki kez elden geçer.

---

## AŞAMA 5 — Görünüm ve devir · ajan · UX turuna bağlı

- Kanban / Bölünmüş / Takvim görünümleri (tasarım kararı UX turundan çıkar) — BL-015
- "Başlattıklarım" sekmesi: bugün yalnız *sana gelen* iş görünüyor, *senin başlattığın* görünmüyor — BL-016
- Eski `/WorkCenter` yüzeyinin sökülmesi (bugün iki Görev Merkezi var; eskisi İngilizce mock) — BL-029
- Görev Merkezi'nin katalog self-registration'ı — BL-022

---

## AŞAMA 6 — Dokümantasyon · ajan · EN SON

API dokümanı (uç noktalar, sözleşme, hata kodları) + kullanıcı kılavuzu (sekmeler, kabul akışı,
kapılar, yinelenen kural, havuz).

**Kullanıcı kılavuzunun iskeleti 2026-08-11'de açıldı:**
[`docs/workcenter-user-guide.md`](./workcenter-user-guide.md) — 11 bölüm başlığı + bağımlılık haritası,
**metin yok** (BL-074). § 3 (sekme/segment/çip) UX turunun doğrudan çıktısı, § 9 (kim kime iş verebilir)
BL-057 + BL-023 olmadan yazılamaz.
❓ **Sahibe açık soru:** kılavuz kaç dilde olacak — CT önerisi önce yalnız Türkçe.

**Kullanıma alma SOP'u ise SIRADAN BAĞIMSIZ ve BUGÜN geçerli:**
[`docs/workcenter-onboarding-sop.md`](./workcenter-onboarding-sop.md) (BL-073) — ana veri zinciri,
sessiz başarısızlık tablosu ve **go-live kabul kontrol listesi**. Aşama 6'yı beklemez.

**Sahip kararı bekliyor:** dosya isimlendirmesi (`api.md` + `user-manual.md`) · tenant modülü nereye
(bugün `docs/platform/` altında, ismi tarihsel; taşımak 21 dosyanın linkini kırar — **CT önerisi: kalsın**).

---

## UX tur sırası (Aşama 1')

Sıranın mantığı: **kararlar aşağı doğru akar.** Listede verilen çip/renk/yoğunluk kararı detayı ve
formu bağlar; tersi doğru değildir. Bu yüzden en çok bakılan ve en çok kural taşıyan yüzeyden başlanır.

| # | Sayfa | Bakılacaklar |
|---|---|---|
| 1 | **İşlerim listesi** | Satır ritmi ve yoğunluk · **segment ↔ çip ayrımı** (bugün ikisi benzer görünüyor, oysa biri statü biri sinyal) · çip sırası ve rengin anlamı · sekme sayaçlarının okunurluğu · boş durum |
| 2 | **Gelen Kutusu** | Aynı dilbilgisi, farklı aksiyon kümesi — aks yasası **görsel olarak** da tutuyor mu · kabul/reddet düğmelerinin ağırlığı |
| 3 | **Görev detayı** | Yaşam döngüsü şeridi · "mevcut aksiyonlar" paneli · **kapılar** (kapalı düğme + sebep) · tarih rayı · alt görev ve yorum bölümleri |
| 4 | **Havuz + Geçmiş** | Kısa — çoğunu listeden miras alıyor; havuzda "üstlen" vurgusu, Geçmiş'te salt-okunurluğun görünürlüğü |
| 5 | **Oluşturma** (hızlı + ayrıntılı) | Görsel dil sabitlendikten **sonra**; iki formun aynı taslağı paylaştığı hissi · zorunlu alan işaretlemesi |
| 6 | **Yönetim ekranları** | Alan Tanımları, sonra (yapılınca) yinelenen kural — golden DataTable deseni |
| 7 | **Enine kesen** | 7 dilde metin taşması · RTL (Arapça) · koyu tema · dar ekran |

> **Not:** sahip "create'ten başlayalım" dedi; CT önerisi listeden başlamak. Gerekçe: kullanıcı
> zamanının çoğunu listede geçirir, en büyük açık görsel soru (BL-017) oradadır, ve formlar görsel
> dilini listeden miras alır — ters sırada aynı ekranı iki kez elden geçiririz.

---

## Kritik zincir

```
Aşama 2  (sahibin platform girişi)  →  4a canlı doğrulanabilir
Aşama 1  (dil paketi)               →  4a Türkçe doğar
Aşama 1'' (kapsam: BL-057→BL-072→BL-023)  →  Aşama 1'
Aşama 1' (UX turu)                  →  Aşama 5 · Aşama 6

Onaylı sıra (2026-08-11):
  1. kayıt turu (backlog/plan)      ← bitti
  2. BL-057  + BL-072  (aynı tur)
  3. BL-023
  4. liste UX turu (Aşama 1')
  5. diğer sayfalar
```
> **Go-live önkoşulu, sıradan ayrı:** gerçek org şeması girilmeden canlıya çıkılmaz — `ReportsToPositionId`
> artık dekoratif değil, **işin kime gideceğini belirleyen veri**. Kimin gireceği ve kimin doğrulayacağı
> (İK mı, yöneticiler mi) cevaplanmalı. Ayrıntı: BL-057 § go-live önkoşulu.
> **Kullanıma alma zinciri** (ana veri sırası, sessiz başarısızlıklar, kabul listesi):
> [`docs/workcenter-onboarding-sop.md`](./workcenter-onboarding-sop.md) — BL-073.

---

## Kapsam dışı (başka sahipler)

Legal Entity / referans veri maddeleri (BL-001…013) — başka geliştirici ·
Enterprise Strategy (BL-018 · BL-020 · BL-021) — Codex tarafı ·
Premium modal yayılımı (BL-027).
