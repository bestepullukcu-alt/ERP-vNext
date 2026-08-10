# CONTROL TOWER — Çalışma Yöntemi (SOP)

> **Kimin için:** bir modülü sahiplenen geliştirici + ona eşlik eden CONTROL TOWER.
> **Nereden çıktı:** 2026-07/08 WorkCenter (MOD-0024) turu. Buradaki her kural bir
> **ölçümden** doğdu; kuralın yanında onu doğuran vaka yazılıdır. Vakasız kural yoktur —
> gerekçesini kaybeden kural altı ay içinde delinir.
> **İlgili:** [`product-backlog.md`](./product-backlog.md) ·
> [orchestrator demir kural #10](../.antigravity/agents/orchestrator.md) ·
> [`/reconcile-records`](../.antigravity/workflows/reconcile-records.md)

---

## 1. CONTROL TOWER nedir, ne değildir

**Bir rol, bir kişi değil.** Modülü geliştiren kod ajanları; CONTROL TOWER onları
yönlendiren, çıktılarını **ölçen** ve kaydı tutan taraftır.

| CONTROL TOWER **yapar** | CONTROL TOWER **yapmaz** |
|---|---|
| Prompt yazar, kapsamı belirler | Modülün kodunu yazar |
| Ajan çıktısını **canlıda ölçer** | Rapora bakıp "tamam" der |
| Mimari kararı verir ve **gerekçesini kaydeder** | Kararı ajana bıraktırır |
| Sahibin isteğine **itiraz eder** | Sahibin istediğini onaylar geçer |
| Kaydı (backlog/pack) günceller | Kaydı "sonra yazarız" der |

> **Küçük istisna:** tek satırlık, mekanik bir düzeltme CONTROL TOWER tarafından
> yapılabilir — ama **testiyle birlikte** ve **kırmızı kanıtıyla**. Bir turda bunun
> örneği: `0g gecikmeyle kapandı` sınır vakası; tek koşul, yeni dize yok, test önce
> kırmızı gösterildi.

---

## 2. Turun akışı — her seferinde aynı sekiz adım

```
1. ÖLÇ        kusuru/ihtiyacı canlıda veya kodda ölç — RAPORA GÜVENME
2. KARAR VER  mimari kararı CT verir, gerekçesi yazılır
3. PROMPT     ajana tek dilim ver: ne, neden, neyi YAPMA, nasıl doğrula
4. DERLE      değişen katmanı derle (aşağıda hangi değişiklik neyi gerektirir)
5. YENİDEN BAŞLAT + TAZELİK KONTROLÜ  süreç başlangıcı > ikili tarihi olmalı
6. CANLI ÖLÇ  ajanın verdiği adımları koş, ekran + sunucu birlikte
7. KAYDET     backlog kapanış kaydı + pack kutusu + gerekiyorsa seam register
8. COMMIT     iş korunsun; commit ≠ kapanış
```

Adım 5 atlanırsa adım 6 **yalan söyler**. Bunun ölçülmüş vakası §4'te.

---

## 3. Demir kurallar

### K1 — Kapanış kod değil, **doğrulamadır**
Bir madde kod yazıldığında değil, **davranış canlıda ölçüldüğünde** kapanır. Kayıt iki
aşamalı: iş biter bitmez **⚠️ KAPANIŞ (KISMİ)** + doğrulanacak adımların listesi; `✅`
ancak canlı turdan sonra.

> **Vaka:** aynı gün iki madde `✅` kapatıldı. Kod doğruydu, 2054 test yeşildi. Yine de
> devretme akışı çalışmıyordu (istemci sunucunun göndermediği alanı okuyordu) ve kabul
> kapısı devretmede açılmıyordu. **İkisi de yalnız canlı turda göründü.**

### K2 — Rapora değil ölçüme güven
Ajan raporu bir **iddiadır**. Kendi ölçümünle karşılaştır. Ajanlar dürüst davranır ama
baktıkları yer yanlış olabilir.

> **Vaka:** ajan "böyle bir pack yok" dedi — yanlış klasöre bakmıştı, pack duruyordu.
> Başka bir ajan "`RequestTitle` diye özellik repoda yok" dedi — ad bir özellik değil,
> FluentValidation'ın ifade yolundan **türettiği** görünen addı.

### K3 — Testin kusuru gerçekten yakaladığını kanıtla (vacuity)
Yeşil test kanıt değildir. Düzeltmeyi **geri al**, testin **kırmızı** olduğunu göster,
sonra geri koy.

> **Vaka:** iki test `…lands in the inbox unaccepted` adını taşıyordu ve baştan sona
> geçiyordu — çünkü ikisi de **hiç kabul edilmemiş** bir görevden başlıyordu.
> "Sonrasında hâlâ kabul edilmemiş" kendiliğinden doğruydu. Kusur bu yüzden testten geçti.

### K4 — Yarım düzeltme, kusurun kendisinden kötü olabilir
Bir düzeltme iki tarafı da kapsamıyorsa **yapma**; "yapılmadı" diye yaz.

> **Vaka:** kapanmış görevin SLA rozeti. Sunucu yarısı gönderildi → ekran `-2g kaldı`
> dedi. Negatif koruması eklendi → bu kez **zamanında biten iş** "1g gecikmiş" dedi.
> İki adımda da ekran **öncekinden farklı bir yalan** söyledi. Ancak sözleşme alanı
> (`closedAt`) eklenince gerçekten çözüldü.

### K5 — Arz düzeldi ≠ teslimat oldu
Bir değer ürettiğinde, **onu okuyan tarafın gerçekten aldığını** ölç.

> **Vaka:** tablo dil paketi. Payload'a altı anahtar eklendi, iş "bitti" sayıldı. Ama
> tüketici başka sözlüğe bakıyordu; ekran İngilizce kaldı. Test "payload'da var" diyordu,
> "tüketici okuyor" demiyordu.

### K6 — Bir olgu iki yerde yaşıyorsa, sessizce kayar
Bu deponun **en sık** kusur sınıfı. Bir değer/kural iki yerde tanımlıysa ve sözleşme
hiçbirini bildirmiyorsa, biri değişince diğeri sessizce yanlışa döner.

> **Vakalar:** istemci `note` gönderiyordu, sunucu `Reason` istiyordu → üç geçiş hiç
> çalışmadı. İstemci `person.id` okuyordu, sunucu `userId` gönderiyordu → seçici boş
> değer üretti. "Kabul edilmiş" anlamı yeni alana taşındı, kapıyı **açan** üç handler
> eski sinyali sıfırlamaya devam etti.
>
> **Kür:** değeri **iki tarafı da dosyadan okuyan** bir testle bağla. Yorumla değil.

### K7 — Kayıtta sayı yerine **ölçüm komutu**
"7 aksiyon üretiyor", "3576 satır" gibi sayılar kodla birlikte kayar; kayıt sessizce
yanlışa döner. Sayı gerekiyorsa **onu üreten komutu** yaz.

### K8 — Erteleme = regresyon beyanı
Bir maddeyi ertelerken **gelecekteki maliyetini** de yaz: "şimdi ekran işi, sonra her
sorguyu gözden geçirmek gerekir" gibi. Temel (foundation) maddeleri ertelendikçe pahalılaşır.

### K9 — Sahibe itiraz et, sonra kararına uy
İstek teknik olarak yanlışsa **bir kez** ölçümle itiraz et. Sahip ısrar ederse
**kararını kaydet ve uygula**. Kayıt, kararın kimin olduğunu ve neden verildiğini taşır.

### K10 — Görünmeyeni değil, ölçüleni yaz
"Muhtemelen", "sanırım", "olmalı" ile kapanış kaydı yazılmaz. Emin değilsen
**"emin değilim: <neden>"** yaz. Tahmin, ölçümden kötüdür.

---

## 4. Canlı doğrulama — teknik zorunluluklar

### 4.1 Süreç canlı ≠ kod canlı

> **Vaka:** düzeltme çalışmıyor sanıldı. Gerçek: `5057` süreci `18:12`'de başlamıştı,
> yeni ikili `22:55`'te derlenmişti, servis `--no-build` ile watch'suz koşuyordu.
> Dosyada yeni kod, bellekte eski kod.

**Her canlı turdan önce:**
```bash
ps -o lstart= -p $(lsof -nP -iTCP:<port> -sTCP:LISTEN -t | head -1)   # süreç başlangıcı
ls -l <proje>/bin/Debug/net8.0/<Assembly>.dll                          # ikili tarihi
# süreç başlangıcı, ikili tarihinden SONRA olmalı
```

### 4.2 Hangi değişiklik neyi gerektirir

| Değişen | Gereken |
|---|---|
| `.js` · `.css` | Sert yenileme (F5 yetmeyebilir) |
| `.cshtml` · `.resx` | **Web projesi derlenir** + yeniden başlatılır |
| `.cs` (servis) | **Servis derlenir** + yeniden başlatılır |
| Varlık/şema | Yukarıdaki + **migration/backfill** canlıda ölçülür |

### 4.3 `dotnet build` / `dotnet test` çalışan servisleri **düşürür**

> **Vaka:** `dotnet test` koşuldu, altı portun altısı da düştü.

Test koşacaksan servisleri sonradan **geri kaldırmayı planla**.

### 4.4 Ölçüm ekrandan **ve** sunucudan birlikte

Yalnız ekrana bakmak yanıltır (ekran doğru görünüp sunucu yanlış olabilir), yalnız
sunucuya bakmak da yanıltır (sunucu doğru, ekran çelişebilir).

> **Vaka:** sunucu `on-track` diyordu, ekran "1g gecikmiş" yazıyordu. Yalnız birine
> bakılsaydı kusur görünmezdi.

### 4.5 Şifre gerektiren adımlar sahibindir

CONTROL TOWER giriş yapamaz. Oturum gerektiren adımları **sahibe listele**, ve
**yapılmadıysa "doğrulandı" yazma**.

---

## 5. Prompt nasıl yazılır

İyi prompt beş şey taşır:

```
1. NE       ölçülmüş belirti — "şu ekranda şu yazıyor", tahmin değil
2. NEDEN    kök neden, dosya:satır ile
3. NASIL    yön + reddedilen alternatif ve gerekçesi
4. YAPMA    kapsam dışı olan ne, ve neden
5. DOĞRULA  kırmızı kanıtı + canlı adımlar + kapanış kuralı
```

**Ek kurallar**
- **Tek dilim.** Platform geneli bir iş, modül işine sıkıştırılmaz.
- **Sığmazsa yapma.** "Yarısını yap" deme; "yapılmadı diye yaz" de.
- **Çok konulu iş `@orchestrator`'a gider** — l10n kapısını o uygular.
- **Tenant modülü = 7 dil** (en, tr, fr, es, zh, ar, ru). Eksik dil = iş bitmemiş.
- **Görsel kararlar sahibin turunda verilir**; ajana "süsleme yapma, golden desene sadık kal" denir.

---

## 6. Kayıt disiplini — üç yer birden

Bir iş bitince **üçü birlikte** güncellenir; biri güncellenip diğerleri bırakılırsa
kayıtlar birbirinden ayrışır.

1. **Backlog kapanış kaydı** — commit hash + tarih · ne yapıldı · **hangi kararlar ve neden**
   (özellikle reddedilen alternatif) · **kasten yapılmayanlar** · yeniden ölçüm komutları
2. **Module pack** `Acceptance Criteria` kutusu
3. **Seam register** (bir dikiş kurulduysa)

**Karar koddan okunamaz.** Okunamayan karar altı ay sonra ya yeniden tartışılır ya
sessizce geri alınır. Kayıt bunun için var.

**Periyodik:** `/reconcile-records` kayıtları koda karşı **ölçer** (düzeltmez). Modül
kapanışında ve ayda bir koşulur.

---

## 7. Yeni bir modül devralırken — ilk gün

```
1. Pack'i oku          execution/domains/**/module-packs/
2. Backlog'u tara      o modülün BL maddeleri, açık kararlar
3. Kodu ölç            pack'in iddia ettiği kod gerçekten var mı
4. Canlı aç            ekranları gez, ölç — "çalışıyor" iddiasını sınama
5. Fark listesi çıkar  pack ne diyor / kod ne yapıyor / ekran ne gösteriyor
6. Sahiple hizala      açık kararları listele, önerini gerekçesiyle ver
```

> **Vaka:** bir modülün pack'inde **20 kutu işaretsizdi ama işi yapılmıştı**; seam
> register'ın **5 satırı** "yapılmıyor" derken beşi de shipped'di. Kayıt kodun
> gerisinde kalmıştı — devralan kişi kayda inansaydı var olan işi yeniden yapardı.

---

## 8. Sık düşülen tuzaklar — kontrol listesi

- [ ] Süreç başlangıcı ikili tarihinden **sonra** mı?
- [ ] Test, düzeltmeden **önce kırmızı** mıydı?
- [ ] Değer üretildi, **tüketici aldı** mı?
- [ ] Düzeltme **iki tarafı** da kapsıyor mu?
- [ ] Ekran ile sunucu **birbirini doğruluyor** mu?
- [ ] Yeni metin **7 dilde** mi?
- [ ] Kayıtta **sayı yerine komut** mu var?
- [ ] Kasten yapılmayanlar **yazıldı** mı?
- [ ] Kapanış `✅` ise **canlı doğrulandı** mı?
- [ ] Erteleme varsa **regresyon riski** beyan edildi mi?

---

## 9. Roller ve sınırlar

| | Sahip (geliştirici) | CONTROL TOWER | Kod ajanı |
|---|---|---|---|
| Ürün kararı | **verir** | önerir, itiraz eder | vermez |
| Mimari karar | onaylar | **verir + kaydeder** | uygular |
| Kod | — | (istisna: tek satır) | **yazar** |
| Canlı doğrulama | şifre gereken adımlar | **koşar + kaydeder** | koşamaz |
| Kayıt | okur | **tutar** | kapanış taslağı yazar |

> **En sık ihlal:** ajanın kendi işini `✅` kapatması. Ajan canlı doğrulayamıyorsa
> `✅` yazamaz — bu bir eksiklik değil, **kuralın kendisidir**.
